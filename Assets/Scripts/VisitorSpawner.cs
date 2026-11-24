using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Fills the park with people on random ground tiles around this object when the scene starts.
// Some visitors start out lined up for the ride; the rest roam (and may wander over to the line later), many of them
// carrying a drink or a water bottle, some of them in groups of friends (VisitorGroup). Plus the staff: the mechanic (keys in hand) and the Reset Agents behind the
// staff door, the ride technician asleep in his booth (screwdriver in hand) and the
// hot dog man behind his stand.
// Visitors use the lineart bodies in Resources/ArtLineartTorso (body_standard_1..8, each with a random face, hair and
// colours); staff still use Resources/Visitors/<character>/
// (Kenney Toon Characters 1, CC0). Nobody has a personal name: visitors are all "Visitor", staff go by their job.
public class VisitorSpawner : MonoBehaviour
{
    public Tilemap ground;
    public Material spriteMaterial;
    public int count = 16;
    public float spawnRadius = 12f;
    public float personScale = 1.35f; // people at a believable size next to the rides and stalls
    public float workerScale = 2f;  // staff drawn as workers ("worker_..." characters) stand a little taller

    // Each entry goes to a different roaming visitor, so the puzzle items are always somewhere in the park.
    public VisitorItem[] guaranteedItems = { VisitorItem.Drink, VisitorItem.Drink, VisitorItem.WaterBottle, VisitorItem.WaterBottle };
    [Range(0, 1)] public float carryChance = 0.4f; // everyone else: chance of one of `carryItems` in hand
    public VisitorItem[] carryItems = { VisitorItem.Drink, VisitorItem.WaterBottle, VisitorItem.CottonCandy, VisitorItem.Popcorn }; // what no stand sells

    [Range(0, 1)] public float groupChance = 0.35f; // per roaming visitor, chance they start a group with the next few
    public Vector2Int groupSize = new(2, 4);         // smallest and biggest group (VisitorGroup)

    public RideQueue queue;
    public int startInQueue = 5;

    public string[] characters =
    {
        "body_standard_1", "body_standard_2", "body_standard_3", "body_standard_4",
        "body_standard_5", "body_standard_6", "body_standard_7", "body_standard_8",
    };
    public Transform[] pointsOfInterest = new Transform[0];
    public string mechanicCharacter = "worker_mechanic";
    public string mechanicName = "Electrician";
    public Transform mechanicPost; // the staff door: the mechanic and Reset Agents wait behind it; empty = a random spot
    public int resetAgents = 2;
    public string resetAgentCharacter = "worker_construction_{n}"; // {n}: 1-3 in turn
    public string technicianCharacter = "worker_ride_technician";
    public string technicianName = "Technician";
    public Transform technicianPost; // empty = no technician (his booth goes here)
    public string announcerCharacter = "worker_announcer";
    public string announcerName = "Announcer";
    public float announcerBack = 0.3f; // behind the information booth's counter (its park_stand_PA picture)
    public float announcerDrop = 0f;  // drawn this much lower there (world units), to sit their face under the booth's roof
    public string hotDogManCharacter = "worker_hot_dog";
    public string hotDogManName = "Hot Dog Man";
    public Transform hotDogManPost;  // behind the hot dog stand; empty = no hot dog man
    [Tooltip("Seconds the hot dog man stays hidden behind his stand after the rats chase him off.")]
    public float hotDogManHiddenTime = 10f;
    public float hotDogLineOut = 1.4f; // his line starts this far (plus standLineGap) in front of him: past the counter
    // Stand sellers, one per stand tile on the Objects tilemap (no scene objects: paint a stand, it gets a seller).
    public string iceCreamCharacter = "worker_ice_cream";
    public Vector3 iceCreamSlot = new(0f, 0f, 0.25f);   // where the seller stands, from the tile's cell centre: in the gap
    public Vector3 iceCreamBackOffset = new(0f, 0f, 0.4f); // the back layer, from the front tile (like the hot dog stall's GlizzyBG)
    public float iceCreamBackScale = 1.4f; // the back's drawing is narrower than the window: grown from its bottom middle to cover it
    public string balloonCharacter = "worker_balloon";
    public Vector3 balloonSpot = new(1.4f, 0f, -0.7f);  // beside the balloon stand, a little in front (mirrored if blocked)

    public string mascotCharacter = "mascot";
    public string mascotName = "Mascot";
    public Transform mascotPost;     // in front of the locker room's propane fan; empty = no mascot

    public Vector2 gossipEvery = new(5f, 10f); // seconds between someone near the player gossiping (GameText.Gossip)
    public float gossipRange = 12f;
    // Seconds between the announcer's PA announcements (GameText.Announcements): the first while the park's calm,
    // the second once everything's gone wrong (Unease), in between on the way.
    public Vector2 announceEvery = new(60f, 20f);
    // The finale (once the locker room's burned): the ice cream sellers are gone and their stands closed, a sign
    // (Props/toilet_sign, "CLOSED") this tall hung this high on each counter.
    public float closedSignHeight = 0.8f, closedSignLift = 1.1f;

    const string VisitorName = "Visitor";
    float nextGossip, nextAnnouncement;
    readonly List<string> heard = new();
    Visitor announcer;
    readonly List<(Visitor seller, Vector3 signAt)> stalls = new();
    bool stallsClosed;
    string lastAnnouncement;

    void Update()
    {
        if (!stallsClosed && Mischief.IsDone(Mischief.Task.LockerFire)) CloseStalls();
        Announce();
        Gossip();
        KeepToiletsBusy();
    }

    // The toilets always have a line (toiletLineAtLeast): now and then, while one's short, someone nearby with nothing
    // better to do walks over and joins it (so there's always somebody passing that way too).
    public int toiletLineAtLeast = 3;
    public float toiletCallRange = 25f;
    float nextToiletCall;

    void KeepToiletsBusy()
    {
        if (Time.time < nextToiletCall || Unease.Mayhem) return;
        nextToiletCall = Time.time + Random.Range(2f, 4f);
        foreach (var line in StandQueue.All)
        {
            if (!line.name.StartsWith("Toilet") || line.Waiting >= toiletLineAtLeast || !line.HasRoom) continue;
            Visitor best = null;
            float bestD = toiletCallRange;
            foreach (var v in Visitor.All)
            {
                if (v.brain != null || v.Group != null || !v.Look.Visible || v.Current is not (Visitor.State.Idle or Visitor.State.Wander)) continue;
                if (Possession.Instance != null && Possession.Instance.Host == v) continue;
                Vector3 d = v.transform.position - line.transform.position;
                float far = new Vector2(d.x, d.z).magnitude;
                if (far >= bestD) continue;
                best = v;
                bestD = far;
            }
            if (best != null && line.Join(best)) best.EnterQueue(line);
        }
    }

    // The finale: the sellers have fled their stands (the hot dog man is going nowhere: he burned in his) and a CLOSED
    // sign hangs on each. Worked out from the locker fire, so a loaded game is the same.
    void CloseStalls()
    {
        stallsClosed = true;
        foreach (var (seller, signAt) in stalls)
        {
            if (seller != null) seller.gameObject.SetActive(false);
            var sign = ParkArt.Lit(ParkArt.Make("Closed Sign", null, "Props/toilet_sign", closedSignHeight, Color.white));
            sign.transform.position = signAt;
        }
    }

    // After an event, the announcer at the information booth speaks up about it now and then: more often the more
    // uneasy the park.
    void Announce()
    {
        if (announcer == null || Time.time < nextAnnouncement) return;
        float unease = Unease.Level;
        nextAnnouncement = Time.time + Mathf.Lerp(announceEvery.x, announceEvery.y, unease) * Random.Range(0.8f, 1.2f);
        heard.Clear();
        var prank = FindAnyObjectByType<ToiletPrank>();
        foreach (var (after, lines) in GameText.Announcements)
            if (Mischief.IsDone(after) && (after != Mischief.Task.TutorialToilet || prank != null && prank.Erupted))
                heard.AddRange(lines);
        if (heard.Count > 1) heard.Remove(lastAnnouncement); // never the same twice running
        if (heard.Count == 0) return;
        lastAnnouncement = heard[Random.Range(0, heard.Count)];
        GameHud.Hush(announcer.transform); // a new announcement cuts off the last
        GameHud.Shout(announcer.transform, lastAnnouncement, 4f, true);
        if (AudioManager.TryPlayOn(e => e.paAnnouncer, announcer.gameObject)) ClosedCaptions.Show(Captions.PaAnnouncement);
    }

    // After an event, now and then someone near the player talks about it.
    void Gossip()
    {
        if (Time.time < nextGossip || Possession.Instance == null) return;
        nextGossip = Time.time + Random.Range(gossipEvery.x, gossipEvery.y);
        heard.Clear();
        var prank = FindAnyObjectByType<ToiletPrank>();
        foreach (var (after, lines) in GameText.Gossip)
            // The tutorial's steps also count as done when it's skipped or a game's loaded: only gossip about a geyser
            // someone saw.
            if (Mischief.IsDone(after) && (after != Mischief.Task.TutorialToilet || prank != null && prank.Erupted))
                heard.AddRange(lines);
        if (heard.Count == 0) return;

        var near = new List<Visitor>();
        Vector3 player = Possession.Instance.Focus.position;
        foreach (var v in Visitor.All)
        {
            Vector3 d = v.transform.position - player;
            if (v.brain == null && v.Look.Visible && v.Current is Visitor.State.Idle or Visitor.State.Wander or Visitor.State.Stare
                && new Vector2(d.x, d.z).magnitude <= gossipRange)
                near.Add(v);
        }
        if (near.Count > 0) GameHud.Shout(near[Random.Range(0, near.Count)].transform, heard[Random.Range(0, heard.Count)], 2.5f, true);
    }

    void Start()
    {
        var roaming = new List<Visitor>();
        for (int i = 0; i < count; i++)
        {
            int queued = i - guaranteedItems.Length;
            if (queue != null && queued >= 0 && queued < startInQueue && queue.HasRoom)
            {
                var v = Spawn(VisitorName, queue.transform.position);
                queue.Join(v);
                v.EnterQueue(queue);
                v.transform.position = queue.SlotPosition(v); // already standing in line
                continue;
            }

            if (!RandomTile(out Vector3 pos)) break;
            var visitor = Spawn(VisitorName, pos);
            var item = i < guaranteedItems.Length ? guaranteedItems[i]
                : Random.value < carryChance ? carryItems[Random.Range(0, carryItems.Length)]
                : VisitorItem.None;
            if (item != VisitorItem.None) visitor.GrabItem(Item.Create(item, visitor.CarryPoint), true);
            roaming.Add(visitor);
        }
        MakeGroups(roaming);
        SpawnStaff();
        StartCoroutine(FillStandLines(roaming));
    }

    // A few people already in the stands' lines when the park opens (startInStandLine each), so it looks busy from the
    // start: visitors on their own, taken from where they were standing.
    public Vector2Int startInStandLine = new(0, 2);

    System.Collections.IEnumerator FillStandLines(List<Visitor> roaming)
    {
        yield return null; // every prop made in its own Start first (the lines keep clear of them: StandQueue)
        ToiletLines();
        foreach (var line in StandQueue.All) line.KeepAway();
        var free = roaming.FindAll(v => v != null && v.Group == null && v.brain == null && v.Current is Visitor.State.Idle or Visitor.State.Wander);
        foreach (var line in StandQueue.All)
        {
            int n = Random.Range(startInStandLine.x, startInStandLine.y + 1);
            for (int i = 0; i < n && free.Count > 0; i++)
            {
                var v = free[Random.Range(0, free.Count)];
                free.Remove(v);
                if (!line.Join(v)) break;
                v.EnterQueue(line);
                v.transform.position = line.SlotPosition(v); // already standing in line
            }
        }
    }

    // Every toilet block but the tutorial's (out of order while it runs, broken after: ToiletPrank) has a line at its
    // door too, from just in front of it back toward the camera: nothing to buy, just a wait (the line's serve time)
    // and off they go.
    void ToiletLines()
    {
        var prank = FindAnyObjectByType<ToiletPrank>();
        foreach (var map in GroundWalker.SolidMaps(ground))
        foreach (var cell in map.cellBounds.allPositionsWithin)
        {
            var s = map.GetSprite(cell);
            if (s == null || !s.name.StartsWith("bathroom")) continue;
            Vector3 feet = map.GetCellCenterWorld(cell);
            if (prank != null && Mathf.Abs(feet.x - prank.Block.center.x) < prank.Block.extents.x && Mathf.Abs(feet.z - prank.Block.center.z) < 1.5f)
                continue; // the tutorial's
            Rect front = GroundWalker.FootprintOf(map, cell);
            var door = new Vector3(front.center.x, 0f, front.yMin);
            StandQueue.Make("Toilet Line", transform, new Vector3(door.x, 0f, front.yMin - standLineGap), LineDirection(), door, ground);
        }
    }

    // Some of the roaming visitors came with friends: 2-4 of them, standing together, walking at the leader's pace.
    void MakeGroups(List<Visitor> roaming)
    {
        for (int i = 0; i < roaming.Count;)
        {
            int size = Mathf.Min(Random.Range(groupSize.x, groupSize.y + 1), roaming.Count - i);
            if (size < 2 || Random.value >= groupChance)
            {
                i++;
                continue;
            }
            var group = new VisitorGroup();
            var lead = roaming[i];
            var walker = lead.GetComponent<GroundWalker>();
            group.Add(lead);
            for (int k = 1; k < size; k++)
            {
                var v = roaming[i + k];
                v.wanderSpeed = lead.wanderSpeed;
                for (int tries = 0; tries < 8; tries++)
                {
                    Vector2 r = Random.insideUnitCircle * 1.2f;
                    Vector3 spot = lead.transform.position + new Vector3(r.x, 0f, r.y);
                    if (!walker.IsWalkable(spot)) continue;
                    v.transform.position = spot;
                    v.GetComponent<Rigidbody>().position = spot;
                    break;
                }
                group.Add(v);
            }
            i += size;
        }
    }

    void SpawnStaff()
    {
        Vector3 door;
        if (mechanicPost != null) door = mechanicPost.position;
        else if (!RandomTile(out door)) return;

        var body = Staff<Mechanic>(mechanicName, door, mechanicCharacter);
        body.GrabItem(Item.Create(VisitorItem.Keys, body.CarryPoint), true);
        // "{n}" in the character picks one of the construction workers in turn (1-3).
        for (int i = 0; i < resetAgents; i++)
            Staff<ResetAgent>($"Reset Agent {i + 1}", door, resetAgentCharacter.Replace("{n}", (i % 3 + 1).ToString()));
        if (technicianPost != null)
        {
            var tech = Staff<RideTechnician>(technicianName, technicianPost.position, technicianCharacter);
            tech.GrabItem(Item.Create(VisitorItem.Screwdriver, tech.CarryPoint), true);
        }
        SpawnAnnouncer();
        if (hotDogManPost != null)
        {
            Staff<HotDogMan>(hotDogManName, hotDogManPost.position, hotDogManCharacter, h => h.awayTime = hotDogManHiddenTime);
            // His line, in front of his counter: served while he's behind it (not off after the rats or guarding his
            // fan), closed for good once the stand's burnt (charred).
            var line = StandQueue.Make("Hot Dog Line", transform, hotDogManPost.position + Vector3.back * (standLineGap + hotDogLineOut),
                LineDirection(), hotDogManPost.position, ground);
            if (line != null)
            {
                line.gives = VisitorItem.HotDog;
                line.serving = () => HotDogMan.All.Exists(h => h.Current == HotDogMan.State.AtStall);
                line.shut = () => HotDogMan.All.TrueForAll(h => h.Current == HotDogMan.State.Charred);
            }
        }
        if (mascotPost != null) Staff<Mascot>(mascotName, mascotPost.position, mascotCharacter);
        SpawnVendors();
    }

    // The information booth (the scene's park_stand_PA picture) has the announcer behind its counter, staying put and
    // out of reach (a Vendor inside: nobody can possess them there).
    void SpawnAnnouncer()
    {
        foreach (var r in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            if (r.sprite == null || !r.sprite.name.StartsWith("park_stand_PA")) continue;
            var at = new Vector3(r.transform.position.x, 0f, r.transform.position.z + announcerBack); // the picture's own spot
            announcer = Staff<Vendor>(announcerName, at, announcerCharacter, v => v.inside = true);
            announcer.Look.Sink(announcerDrop);
            var complaints = gameObject.AddComponent<BoothCrowd>(); // the crowd that gathers there once things go wrong
            complaints.booth = r;
            nextAnnouncement = Time.time + announceEvery.x * 0.5f; // not straight away
            return;
        }
    }

    // Every ice cream stand tile gets its back layer, a solid box from its front to its back (nobody walks in, from any
    // side) and a seller inside, pottering along the counter; every balloon stand tile, a seller beside it.
    void SpawnVendors()
    {
        var back = ParkArt.Get("Props/hot_dog_stand_back");
        foreach (var map in GroundWalker.SolidMaps(ground)) // the stands are on See Through, as buildings
        foreach (var cell in map.cellBounds.allPositionsWithin)
        {
            var s = map.GetSprite(cell);
            if (s == null) continue;
            Vector3 feet = map.GetCellCenterWorld(cell);
            if (s.name.StartsWith("ice_cream_stand"))
            {
                if (back != null) BackLayer(map, cell, back);
                Rect front = GroundWalker.FootprintOf(map, cell);
                GroundWalker.Block("Ice Cream Stand Block", Rect.MinMaxRect(front.xMin, front.yMin, front.xMax, feet.z + iceCreamBackOffset.z + 0.1f));
                Vector3 slot = feet + iceCreamSlot;
                float room = Mathf.Max(0f, Mathf.Min(slot.x - front.xMin, front.xMax - slot.x) - 0.45f); // their middle, off the posts
                var seller = Staff<Vendor>("Ice Cream Seller", slot, iceCreamCharacter, v => { v.inside = true; v.paceHalfWidth = room; });
                StandAudio.Add(seller.gameObject, e => e.iceCreamStall, Captions.IceCreamJingle);
                stalls.Add((seller, new Vector3(feet.x, closedSignLift, front.yMin - 0.05f))); // the sign hangs on the counter's front
                // Its line, from just in front of the counter back toward the camera.
                var line = StandQueue.Make("Ice Cream Line", transform, new Vector3(slot.x, 0f, front.yMin - standLineGap),
                    LineDirection(), slot, ground);
                if (line != null) line.gives = VisitorItem.IceCream;
            }
            else if (s.name.StartsWith("balloon_stand"))
            {
                Vector3 at = feet + balloonSpot;
                var probe = new Vector3(feet.x - balloonSpot.x, 0f, feet.z + balloonSpot.z);
                if (GroundWalker.NearObstacle(at, 0.35f) && !GroundWalker.NearObstacle(probe, 0.35f)) at = probe;
                var balloons = Staff<Vendor>("Balloon Seller", at, balloonCharacter, _ => { });
                StandAudio.Add(balloons.gameObject, e => e.balloonSeller, Captions.Balloons);
                // Its line, in front of the seller.
                var line = StandQueue.Make("Balloon Line", transform, at + Vector3.back * (standLineGap + 0.4f), LineDirection(), at, ground);
                if (line != null) line.gives = VisitorItem.Balloon;
            }
        }
    }

    // The lines at the stands (StandQueue): from standLineGap in front of the counter, going back toward the camera and
    // leaning a little to one side, every other stand the other way.
    public float standLineGap = 0.6f;
    int lines;

    Vector3 LineDirection() => new(lines++ % 2 == 0 ? 0.2f : -0.2f, 0f, -1f);

    // The stand's back: the tile's cut, where the tile is, moved by iceCreamBackOffset and grown by iceCreamBackScale.
    void BackLayer(Tilemap map, Vector3Int cell, Sprite art)
    {
        Matrix4x4 m = map.transform.localToWorldMatrix
                      * Matrix4x4.Translate(map.CellToLocalInterpolated(cell + map.tileAnchor))
                      * map.orientationMatrix * map.GetTransformMatrix(cell);
        var r = new GameObject("Stand Back").AddComponent<SpriteRenderer>();
        r.transform.SetParent(transform, true);
        r.transform.SetPositionAndRotation((Vector3)m.GetColumn(3) + iceCreamBackOffset, m.rotation);
        r.transform.localScale = m.lossyScale * iceCreamBackScale;
        r.sprite = art;
        var tr = map.GetComponent<TilemapRenderer>();
        r.sharedMaterial = tr.sharedMaterial;
        r.sortingLayerID = tr.sortingLayerID;
        r.sortingOrder = tr.sortingOrder;
        r.shadowCastingMode = tr.shadowCastingMode;
        r.receiveShadows = tr.receiveShadows;
    }

    Visitor Staff<T>(string name, Vector3 pos, string character, System.Action<T> setup) where T : MonoBehaviour
    {
        var go = Body(name, pos, character, character.StartsWith("worker_") ? workerScale : personScale);
        setup(go.AddComponent<T>());
        go.SetActive(true);
        return go.GetComponent<Visitor>();
    }

    Visitor Staff<T>(string name, Vector3 pos, string character) where T : MonoBehaviour
    {
        var go = Body(name, pos, character, character.StartsWith("worker_") ? workerScale : personScale);
        go.AddComponent<T>();
        go.SetActive(true);
        return go.GetComponent<Visitor>();
    }

    bool RandomTile(out Vector3 pos)
    {
        for (int tries = 0; tries < 50; tries++)
        {
            Vector2 r = Random.insideUnitCircle * spawnRadius;
            pos = new Vector3(transform.position.x + r.x, 0f, transform.position.z + r.y);
            Vector3Int cell = ground.WorldToCell(pos);
            cell.z = 0;
            Vector3 spot = pos;
            if (GroundWalker.InPark(ground, cell) && RaisedFloor.Height(spot) <= RaisedFloor.Ground // never up on the platform: visitors don't climb it
                && !StaffArea.All.Exists(a => a.Contains(spot))) return true;
        }
        pos = default;
        return false;
    }

    // Built inactive so every field is set before the components' Awake runs.
    GameObject Body(string name, Vector3 pos, string character, float scale)
    {
        var go = new GameObject(name);
        go.SetActive(false);
        go.transform.SetParent(transform, true);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * scale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sharedMaterial = spriteMaterial;

        // People don't collide with anything: they float on the floor plane and only the tile check limits them.
        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation; // not Y: GroundWalker places the height (stairs)
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var walker = go.AddComponent<GroundWalker>();
        walker.ground = ground;

        var look = go.AddComponent<CharacterSprite>();
        look.character = character;
        return go;
    }

    // One more visitor, standing at `pos` (the Ferris wheel's finale crowds where it'll land).
    public Visitor SpawnVisitor(Vector3 pos) => Spawn(VisitorName, pos);

    Visitor Spawn(string name, Vector3 pos)
    {
        var go = Body(name, pos, characters[Random.Range(0, characters.Length)], personScale);
        var v = go.AddComponent<Visitor>();
        v.wanderSpeed = Random.Range(1.5f, 2.5f);
        v.pointsOfInterest = pointsOfInterest;

        go.SetActive(true);
        return v;
    }
}
