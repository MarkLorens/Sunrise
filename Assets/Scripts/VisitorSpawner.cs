using UnityEngine;
using UnityEngine.Tilemaps;

// Fills the park with people on random ground tiles around this object when the scene starts.
// Some visitors start out lined up for the ride; the rest roam (and may wander over to the line later), many of them
// carrying a drink or a water bottle. Plus the mechanic, screwdriver in hand.
// Looks come from Resources/Visitors/<character>/ (Kenney Toon Characters 1, CC0, plus recoloured variants).
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

    public string[] characters = { "femaleAdventurer", "femalePerson", "maleAdventurer", "malePerson", "malePersonTeal", "malePersonRed", "femalePersonPink", "femalePersonYellow" };
    public string[] names = { "Budi", "Sari", "Rina", "Dodi", "Wati", "Andi", "Maya", "Joni", "Tika", "Bayu", "Lina", "Eko", "Nina", "Rudi", "Dewi", "Agus", "Fitri", "Hendra", "Intan", "Yusuf" };
    public Transform[] pointsOfInterest = new Transform[0];
    public string mechanicCharacter = "mechanic";
    public string mechanicName = "Mechanic Joko";
    public Transform mechanicPost; // where the mechanic hangs around; empty = a random spot

    int nameIndex;

    void Start()
    {
        Shuffle(names);

        for (int i = 0; i < count; i++)
        {
            int queued = i - guaranteedItems.Length;
            if (queue != null && queued >= 0 && queued < startInQueue && queue.HasRoom)
            {
                var v = Spawn(NextName(), queue.transform.position);
                queue.Join(v);
                v.EnterQueue(queue);
                v.transform.position = queue.SlotPosition(v); // already standing in line
                continue;
            }

            if (!RandomTile(out Vector3 pos)) break;
            var visitor = Spawn(NextName(), pos);
            var item = i < guaranteedItems.Length ? guaranteedItems[i]
                : Random.value < carryChance ? carryItems[Random.Range(0, carryItems.Length)]
                : VisitorItem.None;
            if (item != VisitorItem.None) visitor.GrabItem(Item.Create(item, visitor.CarryPoint, visitor), true);
        }
        SpawnMechanic();
    }

    void SpawnMechanic()
    {
        Vector3 pos;
        if (mechanicPost != null) pos = mechanicPost.position;
        else if (!RandomTile(out pos)) return;
        var go = Body(mechanicName, pos, mechanicCharacter, personScale);
        go.AddComponent<Mechanic>().wheel = queue != null ? queue.ride : null;
        go.SetActive(true);
        var body = go.GetComponent<Visitor>();
        body.GrabItem(Item.Create(VisitorItem.Screwdriver, body.CarryPoint, body), true);
    }

    string NextName() => names[nameIndex++ % names.Length];

    static void Shuffle(string[] list)
    {
        for (int i = list.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
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
        v.strength = Random.Range(2f, 4.5f);
        v.pointsOfInterest = pointsOfInterest;

        go.SetActive(true);
        return v;
    }
}
