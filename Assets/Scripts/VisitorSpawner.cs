using UnityEngine;
using UnityEngine.Tilemaps;

// Fills the park with visitors on random ground tiles around this object when the scene starts.
// Some start out lined up for the ride; the rest roam (and may wander over to the line later).
// Looks come from Resources/Visitors/<character>/ (Kenney Toon Characters 1, CC0).
public class VisitorSpawner : MonoBehaviour
{
    public Tilemap ground;
    public Material spriteMaterial;
    public int count = 16;
    public float spawnRadius = 12f;
    [Range(0, 1)] public float childChance = 0.3f;
    public float childScale = 0.7f;

    // Each entry goes to a different roaming visitor, so the puzzle items are always somewhere in the park.
    public VisitorItem[] guaranteedItems = { VisitorItem.SpicyHotdog, VisitorItem.ToiletKey, VisitorItem.Drink, VisitorItem.Drink };

    public RideQueue queue;
    public int startInQueue = 5;

    public string[] characters = { "femaleAdventurer", "femalePerson", "maleAdventurer", "malePerson" };
    public Transform[] pointsOfInterest = new Transform[0];

    void Start()
    {
        for (int i = 0; i < count; i++)
        {
            bool child = Random.value < childChance;
            string label = $"Visitor {i + 1}{(child ? " (child)" : "")}";

            int queued = i - guaranteedItems.Length;
            if (queue != null && queued >= 0 && queued < startInQueue && queue.HasRoom)
            {
                var v = Spawn(label, queue.transform.position, child, VisitorItem.None);
                queue.Join(v);
                v.EnterQueue(queue);
                v.transform.position = queue.SlotPosition(v); // already standing in line
                continue;
            }

            if (!RandomTile(out Vector3 pos)) break;
            var item = i < guaranteedItems.Length ? guaranteedItems[i] : VisitorItem.None;
            Spawn(label, pos, child, item);
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

    Visitor Spawn(string name, Vector3 pos, bool child, VisitorItem item)
    {
        // Built inactive so every field is set before the components' Awake runs.
        var go = new GameObject(name);
        go.SetActive(false);
        go.transform.SetParent(transform, true);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * (child ? childScale : 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sharedMaterial = spriteMaterial;

        // Visitors don't collide with anything: they float on the floor plane and only the tile check limits them.
        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var walker = go.AddComponent<GroundWalker>();
        walker.ground = ground;

        var look = go.AddComponent<CharacterSprite>();
        look.character = characters[Random.Range(0, characters.Length)];

        var v = go.AddComponent<Visitor>();
        v.item = item;
        v.wanderSpeed = Random.Range(1.5f, 2.5f);
        v.pointsOfInterest = pointsOfInterest;

        go.SetActive(true);
        return v;
    }
}
