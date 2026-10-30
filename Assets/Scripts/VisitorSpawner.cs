using UnityEngine;
using UnityEngine.Tilemaps;

// Fills the park with people on random ground tiles around this object when the scene starts.
// Some visitors start out lined up for the ride; the rest roam (and may wander over to the line later),
// some with a kid in tow and most carrying a snack, drink or key. Plus the mechanic, screwdriver in hand.
// Looks come from Resources/Visitors/<character>/ (Kenney Toon Characters 1, CC0, plus recoloured variants).
public class VisitorSpawner : MonoBehaviour
{
    public Tilemap ground;
    public Material spriteMaterial;
    public int count = 16;
    public float spawnRadius = 12f;
    public float personScale = 1.35f; // people at a believable size next to the rides and stalls
    [Range(0, 1)] public float childChance = 0.3f; // chance a roaming grown-up brings a kid along
    public float childScale = 0.7f;

    // Each entry goes to a different roaming visitor, so the puzzle items are always somewhere in the park.
    public VisitorItem[] guaranteedItems = { VisitorItem.SpicyHotdog, VisitorItem.ToiletKey, VisitorItem.Drink, VisitorItem.Drink };
    [Range(0, 1)] public float carryChance = 0.4f; // everyone else: chance of a drink or a hotdog in hand

    public RideQueue queue;
    public int startInQueue = 5;

    public string[] characters = { "femaleAdventurer", "femalePerson", "maleAdventurer", "malePerson", "malePersonTeal", "malePersonRed", "femalePersonPink", "femalePersonYellow" };
    public string[] childCharacters = { "maleAdventurer", "maleAdventurerRed", "femaleAdventurer", "femaleAdventurerPurple" };
    public string[] names = { "Budi", "Sari", "Rina", "Dodi", "Wati", "Andi", "Maya", "Joni", "Tika", "Bayu", "Lina", "Eko", "Nina", "Rudi", "Dewi", "Agus", "Fitri", "Hendra", "Intan", "Yusuf" };
    public string[] childNames = { "Kiki", "Bobi", "Lala", "Dika", "Nana", "Tono" };
    public Transform[] pointsOfInterest = new Transform[0];
    public string mechanicCharacter = "guard";
    public string mechanicName = "Mechanic Joko";
    public Transform mechanicPost; // where the mechanic hangs around; empty = a random spot

    int nameIndex, childIndex;

    void Start()
    {
        Shuffle(names);
        Shuffle(childNames);

        for (int i = 0; i < count; i++)
        {
            int queued = i - guaranteedItems.Length;
            if (queue != null && queued >= 0 && queued < startInQueue && queue.HasRoom)
            {
                var v = Spawn(NextName(), queue.transform.position, characters, personScale, false);
                queue.Join(v);
                v.EnterQueue(queue);
                v.transform.position = queue.SlotPosition(v); // already standing in line
                continue;
            }

            if (!RandomTile(out Vector3 pos)) break;
            var grownUp = Spawn(NextName(), pos, characters, personScale, false);
            var item = i < guaranteedItems.Length ? guaranteedItems[i]
                : Random.value < carryChance ? (Random.value < 0.5f ? VisitorItem.Drink : VisitorItem.SpicyHotdog)
                : VisitorItem.None;
            if (item != VisitorItem.None) grownUp.GrabItem(Item.Create(item, grownUp.CarryPoint, grownUp), true);

            if (Random.value < childChance)
            {
                string kidName = $"little {childNames[childIndex++ % childNames.Length]}";
                var kid = Spawn(kidName, pos + new Vector3(0.6f, 0f, -0.2f), childCharacters, personScale * childScale, true);
                kid.parent = grownUp;
            }
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
            if (ground.HasTile(cell)) return true;
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

    Visitor Spawn(string name, Vector3 pos, string[] looks, float scale, bool child)
    {
        var go = Body(name, pos, looks[Random.Range(0, looks.Length)], scale);
        var v = go.AddComponent<Visitor>();
        v.child = child;
        v.wanderSpeed = Random.Range(1.5f, 2.5f);
        v.strength = child ? 1f : Random.Range(2f, 4.5f);
        v.pointsOfInterest = pointsOfInterest;

        go.SetActive(true);
        return v;
    }
}
