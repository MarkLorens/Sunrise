using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Fills the park with people on random ground tiles around this object when the scene starts.
// Some visitors start out lined up for the ride; the rest roam (and may wander over to the line later), many of them
// carrying a drink or a water bottle. Plus the staff: the mechanic (keys in hand) and the Reset Agents behind the
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

    // Each entry goes to a different roaming visitor, so the puzzle items are always somewhere in the park.
    public VisitorItem[] guaranteedItems = { VisitorItem.Drink, VisitorItem.Drink, VisitorItem.WaterBottle, VisitorItem.WaterBottle };
    [Range(0, 1)] public float carryChance = 0.4f; // everyone else: chance of one of `carryItems` in hand
    public VisitorItem[] carryItems = { VisitorItem.Drink, VisitorItem.WaterBottle };

    public RideQueue queue;
    public int startInQueue = 5;

    public string[] characters =
    {
        "body_standard_1", "body_standard_2", "body_standard_3", "body_standard_4",
        "body_standard_5", "body_standard_6", "body_standard_7", "body_standard_8",
    };
    public Transform[] pointsOfInterest = new Transform[0];
    public string mechanicCharacter = "mechanic";
    public string mechanicName = "Mechanic";
    public Transform mechanicPost; // the staff door: the mechanic and Reset Agents wait behind it; empty = a random spot
    public int resetAgents = 2;
    public string resetAgentCharacter = "malePersonTeal";
    public string technicianCharacter = "malePersonRed";
    public string technicianName = "Technician";
    public Transform technicianPost; // empty = no technician (his booth goes here)
    public string hotDogManCharacter = "malePerson";
    public string hotDogManName = "Hot Dog Man";
    public Transform hotDogManPost;  // behind the hot dog stand; empty = no hot dog man
    public string mascotCharacter = "femalePersonPink";
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
        foreach (var (after, lines) in GameText.Gossip)
            if (Mischief.IsDone(after)) heard.AddRange(lines);
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
        if (near.Count > 0) GameHud.Shout(near[Random.Range(0, near.Count)].transform, heard[Random.Range(0, heard.Count)], 2.5f);
    }

    void Start()
    {
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
        }
        SpawnStaff();
    }

    void SpawnStaff()
    {
        Vector3 door;
        if (mechanicPost != null) door = mechanicPost.position;
        else if (!RandomTile(out door)) return;

        var body = Staff<Mechanic>(mechanicName, door, mechanicCharacter);
        body.GrabItem(Item.Create(VisitorItem.Keys, body.CarryPoint), true);
        for (int i = 0; i < resetAgents; i++) Staff<ResetAgent>($"Reset Agent {i + 1}", door, resetAgentCharacter);
        if (technicianPost != null)
        {
            var tech = Staff<RideTechnician>(technicianName, technicianPost.position, technicianCharacter);
            tech.GrabItem(Item.Create(VisitorItem.Screwdriver, tech.CarryPoint), true);
        }
        if (hotDogManPost != null) Staff<HotDogMan>(hotDogManName, hotDogManPost.position, hotDogManCharacter);
        if (mascotPost != null) Staff<Mascot>(mascotName, mascotPost.position, mascotCharacter);
    }

    Visitor Staff<T>(string name, Vector3 pos, string character) where T : MonoBehaviour
    {
        var go = Body(name, pos, character, personScale);
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
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
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
