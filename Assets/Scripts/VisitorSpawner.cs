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
    public VisitorItem[] carryItems = { VisitorItem.Drink, VisitorItem.WaterBottle };

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
    public string mechanicName = "Mechanic";
    public Transform mechanicPost; // the staff door: the mechanic and Reset Agents wait behind it; empty = a random spot
    public int resetAgents = 2;
    public string resetAgentCharacter = "worker_construction_{n}"; // {n}: 1-3 in turn
    public string technicianCharacter = "worker_ride_technician";
    public string technicianName = "Technician";
    public Transform technicianPost; // empty = no technician (his booth goes here)
    public string hotDogManCharacter = "worker_hot_dog";
    public string hotDogManName = "Hot Dog Man";
    public Transform hotDogManPost;  // behind the hot dog stand; empty = no hot dog man
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

    const string VisitorName = "Visitor";
    float nextGossip;
    readonly List<string> heard = new();

    // After an event, now and then someone near the player talks about it.
    void Update()
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
        if (hotDogManPost != null) Staff<HotDogMan>(hotDogManName, hotDogManPost.position, hotDogManCharacter);
        if (mascotPost != null) Staff<Mascot>(mascotName, mascotPost.position, mascotCharacter);
        SpawnVendors();
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
                Staff<Vendor>("Ice Cream Seller", slot, iceCreamCharacter, v => { v.inside = true; v.paceHalfWidth = room; });
            }
            else if (s.name.StartsWith("balloon_stand"))
            {
                Vector3 at = feet + balloonSpot;
                var probe = new Vector3(feet.x - balloonSpot.x, 0f, feet.z + balloonSpot.z);
                if (GroundWalker.NearObstacle(at, 0.35f) && !GroundWalker.NearObstacle(probe, 0.35f)) at = probe;
                Staff<Vendor>("Balloon Seller", at, balloonCharacter, _ => { });
            }
        }
    }

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
            if (ground.HasTile(cell) && !StaffArea.All.Exists(a => a.Contains(spot))) return true;
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
