using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Walks on the X/Z floor, but only onto the park's cells (InPark: a ground tile that isn't the grass field round the
// park; only someone fleeing it at the end walks out over that, Visitor.Fleeing), not into anything on the
// Objects or See Through tilemaps (SolidMaps: lamps, benches, fences; and the buildings and trees the player shows
// through; walk-over decor like flowers and bushes lives on its own Decor tilemap) or into a prop with a Box Collider
// (the stalls), and, for visitors, not into a
// staff-only area: StaffArea, nor onto ground behind a fingerprint lock: FingerprintArea.
// Driven by Player/Move (WASD/arrows, Controls) when readKeyboard is on, otherwise by whatever sets `move`: the visitor and staff AI call
// WalkTo, which finds a way around whatever's in the way (NavGrid).
// Gravity + the ground's collider keep it standing on the floor.
[RequireComponent(typeof(Rigidbody))]
public class GroundWalker : MonoBehaviour
{
    public Tilemap ground;
    public Tilemap objects; // more things you bump into, besides the Objects and See Through tilemaps next to the ground
    public float bodyRadius = 0.35f; // about half a person's width: how close your middle gets to an object
    public float speed = 5f;
    public bool readKeyboard = true;
    [HideInInspector] public Vector2 move; // x = world X, y = world Z; used when readKeyboard is off
    // A stall worker behind their counter: inside their stall's solid box (Block), which they'd otherwise be stuck in.
    // Their own script keeps them to the stall.
    [HideInInspector] public bool ignoreObstacles;

    // The most anyone steps up or down at once: a stair, not the side of a raised floor (RaisedFloor).
    public const float MaxStep = 0.45f; // PlatformFW2's stairs rise up to 0.35 between NavGrid squares; off their sides onto the deck is 0.55 or more
    const float ClimbSpeed = 6f; // how fast you rise or sink to the floor's height on stairs

    Rigidbody rb;
    float hover; // how far above the floor this body sits (the spirit floats; people stand on it)
    Visitor visitor; // people are kept out of staff-only areas (StaffArea); the spirit isn't
    // Looked up when needed: staff in the scene get their Visitor body added in their own Awake, which may run after ours.
    Visitor Body => visitor != null ? visitor : visitor = GetComponent<Visitor>();
    readonly Route route = new();

    // What's in the way, worked out once and shared by everyone, as boxes on the floor (x, z):
    // - each object on the Objects and See Through tilemaps, as wide as the bottom of its sprite (a bench's legs, a tree's trunk rather
    //   than its leaves), so a wide bench blocks more than its one cell;
    // - each prop with a Box Collider (the stalls). People have no colliders of their own (so they don't shove each
    //   other around), so physics alone would let them walk straight through it.
    // An object's box is a line just in front of its base. Grown by your body's radius, it keeps your feet off its
    // front but lets you tuck in right behind it (the camera looks +z, so the object hides you there).
    const float ObjectFront = 0.25f;
    static readonly List<Rect> footprints = new();
    static readonly List<Collider> props = new();
    static Tilemap obstaclesFor;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Height is placed by hand (FixedUpdate); a Y freeze would snap it back every physics step, so no stairs.
        rb.constraints &= ~RigidbodyConstraints.FreezePositionY;
        hover = Mathf.Max(0f, transform.position.y - RaisedFloor.Height(transform.position));

        // Visitors and the mechanic are spawned from code, so look the tilemaps up next to the ground.
        if (obstaclesFor != ground)
        {
            var maps = SolidMaps(ground);
            if (objects != null && !maps.Contains(objects)) maps.Add(objects);
            FindObstacles(ground, maps);
        }

        ground.CompressBounds();
        var b = ground.localBounds;
        var floor = ground.GetComponent<BoxCollider>();
        floor.center = new Vector3(b.center.x, b.center.y, 0.5f);
        floor.size = new Vector3(b.size.x, b.size.y, 1f);
    }

    void FixedUpdate()
    {
        Vector2 input = readKeyboard ? KeyboardInput() : move;
        Vector2 v = Vector2.ClampMagnitude(input, 1f) * speed;

        // Check each axis separately so you can slide along edges instead of sticking.
        if (!OnTile(new Vector3(v.x, 0, 0))) v.x = 0;
        if (!OnTile(new Vector3(0, 0, v.y))) v.y = 0;
        // Each axis alone can be clear while the diagonal step cuts the corner into a tree: don't.
        if (!OnTile(new Vector3(v.x, 0, v.y))) v.y = 0;

        // Up and down stairs: no gravity, so the height is set by hand, as a speed. Placing the body there instead
        // (rb.position) switches its interpolation off for that step: the body, and the camera on it, jumped a whole
        // step's height in one frame, a bump on every stair.
        Vector3 at = rb.position;
        float y = Mathf.MoveTowards(at.y, RaisedFloor.Height(at) + hover, ClimbSpeed * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(v.x, (y - at.y) / Time.fixedDeltaTime, v.y);
    }

    // Switched back on (out of a possession, or a body teleported somewhere): straight to the floor's height.
    void OnEnable()
    {
        if (rb == null) return; // first enable: Awake has the starting height
        Vector3 at = transform.position;
        transform.position = rb.position = new Vector3(at.x, RaisedFloor.Height(at) + hover, at.z);
    }

    // Player/Move in the project's input actions (Controls).
    static Vector2 KeyboardInput() => Possession.InputBlocked ? Vector2.zero : Controls.Move;

    public bool IsWalkable(Vector3 worldPos)
    {
        Vector3Int cell = ground.WorldToCell(worldPos);
        cell.z = 0; // height above the floor doesn't matter
        return (Body != null && Body.Fleeing ? ground.HasTile(cell) : InPark(ground, cell))
            && Mathf.Abs(RaisedFloor.Height(worldPos) - RaisedFloor.Height(transform.position)) <= MaxStep // stairs, not the sides
            && !RaisedFloor.Railed(transform.position, worldPos) // nor off the side of a flight halfway up
            && (ignoreObstacles || !BumpsIntoObject(transform.position, worldPos))
            && StaffArea.Allows(Body, transform.position, worldPos)
            && FingerprintArea.Allows(Body, transform.position, worldPos)
            && Aftermath.Allows(Body, transform.position, worldPos)
            && (!WanderingVisitor || RaisedFloor.Height(worldPos) <= RaisedFloor.Height(transform.position) + 0.01f); // the stairs sample as a gentle slope: no climbing at all
    }

    // A visitor going about their day (not possessed, not boarding a ride): they never climb onto the platform, though
    // one left up there can come down. Staff and the spirit's hosts go up as they like.
    bool WanderingVisitor => Body != null && Body.brain == null && !Body.Boarding && Body.Current != Visitor.State.Possessed;

    bool OnTile(Vector3 velocity) => IsWalkable(rb.position + velocity * Time.fixedDeltaTime);

    // AI walking (visitors, the mechanic, the operator): heads for `p` along a route around whatever's in the way, and
    // returns false once there. Staff may cut through staff-only areas.
    public bool WalkTo(Vector3 p)
    {
        Vector3 to = p - transform.position;
        to.y = 0;
        if (to.magnitude < 0.3f)
        {
            move = Vector2.zero;
            return false;
        }
        bool staff = Body == null || Body.brain != null || Body.Boarding; // boarding visitors may use the platform too
        Vector3 d = route.Next(ground, transform.position, p, staff, Body) - transform.position;
        move = new Vector2(d.x, d.z).normalized;
        return true;
    }

    void OnDrawGizmosSelected() => route.DrawGizmos(transform.position);

    // The park's ground: a tile on the ground tilemap that isn't part of the grass field round the park. Everyone keeps
    // to it (walking, routes, where people spawn, queue and gather).
    public static bool InPark(Tilemap ground, Vector3Int cell) => ground.HasTile(cell) && !Field(ground).Contains(cell);

    // `p` in the park and `margin` in from its edge every way (Visitor.RunAway keeps the panicking to the middle).
    public static bool WellInside(Tilemap ground, Vector3 p, float margin)
    {
        foreach (var o in new[] { Vector3.zero, Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
        {
            Vector3Int cell = ground.WorldToCell(p + o * margin);
            cell.z = 0;
            if (!InPark(ground, cell)) return false;
        }
        return true;
    }

    // The middle of the park (of all its walkable ground), worked out once per ground.
    static Tilemap middleOf;
    static Vector3 middle;

    public static Vector3 ParkMiddle(Tilemap ground)
    {
        if (middleOf == ground) return middle;
        middleOf = ground;
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        foreach (var c in ground.cellBounds.allPositionsWithin)
        {
            var cell = new Vector3Int(c.x, c.y, 0);
            if (!InPark(ground, cell)) continue;
            Vector3 w = ground.GetCellCenterWorld(cell);
            min = Vector3.Min(min, w);
            max = Vector3.Max(max, w);
        }
        middle = (min + max) * 0.5f;
        middle.y = 0f;
        return middle;
    }

    // The grass field round the park, flood-filled in from the ground's edge, once per ground: a lawn inside the park
    // (cut off from the edge) stays walkable. Outer lawn tiles are img_2890–2897_* (was tiles_grass_full*). Those
    // same sheets are also painted inside the park, so a cell only counts as the field when it sits outside Park Fence.
    public const string FieldTile = "tiles_grass_full";
    static Tilemap fieldOf;
    static readonly HashSet<Vector3Int> field = new();
    static bool fenceReady, hasFence;
    static float fenceMinX, fenceMaxX, fenceMinZ, fenceMaxZ;

    static bool IsFieldTile(TileBase t)
    {
        if (t == null) return false;
        string n = t.name;
        if (n.StartsWith(FieldTile)) return true;
        // img_2890_tl … img_2897_br
        return n.Length >= 8 && n.StartsWith("img_289") && n[7] >= '0' && n[7] <= '7';
    }

    // Axis-aligned ring of Park Fence. Inside it, img_2890–2897 is ordinary ground.
    static bool OutsideFence(Vector3 w)
    {
        if (!fenceReady)
        {
            fenceReady = true;
            var fence = GameObject.Find("Park Fence");
            if (fence != null)
            {
                fenceMinX = fenceMinZ = float.PositiveInfinity;
                fenceMaxX = fenceMaxZ = float.NegativeInfinity;
                foreach (var t in fence.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != "Barrier" && t.name != "Fence Block") continue;
                    Vector3 p = t.position;
                    if (p.x < fenceMinX) fenceMinX = p.x;
                    if (p.x > fenceMaxX) fenceMaxX = p.x;
                    if (p.z < fenceMinZ) fenceMinZ = p.z;
                    if (p.z > fenceMaxZ) fenceMaxZ = p.z;
                    hasFence = true;
                }
            }
        }
        if (!hasFence) return true;
        return w.x <= fenceMinX || w.x >= fenceMaxX || w.z <= fenceMinZ || w.z >= fenceMaxZ;
    }

    static HashSet<Vector3Int> Field(Tilemap ground)
    {
        if (fieldOf == ground) return field;
        fieldOf = ground;
        field.Clear();
        BoundsInt b = ground.cellBounds;
        bool Grass(Vector3Int c) => c.x >= b.xMin && c.y >= b.yMin && c.x < b.xMax && c.y < b.yMax
                                    && IsFieldTile(ground.GetTile(c))
                                    && OutsideFence(ground.GetCellCenterWorld(c));
        var open = new Queue<Vector3Int>();
        for (int x = b.xMin; x < b.xMax; x++)
            for (int y = b.yMin; y < b.yMax; y++)
            {
                var c = new Vector3Int(x, y, 0);
                bool edge = x == b.xMin || y == b.yMin || x == b.xMax - 1 || y == b.yMax - 1;
                if (edge && Grass(c) && field.Add(c)) open.Enqueue(c);
            }
        while (open.Count > 0)
        {
            var c = open.Dequeue();
            foreach (var n in new[] { c + Vector3Int.right, c + Vector3Int.left, c + Vector3Int.up, c + Vector3Int.down })
                if (Grass(n) && field.Add(n)) open.Enqueue(n);
        }
        return field;
    }

    // For a prop made from code after the scene has loaded (the Ferris wheel's electrical box, hinge and gear box):
    // the scene's own props were already collected when the first person woke up.
    public static void AddObstacle(Collider c)
    {
        if (!props.Contains(c)) props.Add(c);
        NavGrid.Invalidate();
    }

    // A solid box on the floor nobody walks into (a stall, front to back), from code. Stall workers inside it walk
    // with ignoreObstacles.
    public static BoxCollider Block(string name, Rect floor)
    {
        var box = new GameObject(name).AddComponent<BoxCollider>();
        box.transform.position = new Vector3(floor.center.x, 1f, floor.center.y);
        box.size = new Vector3(floor.width, 2f, floor.height);
        AddObstacle(box);
        return box;
    }

    // The base of a tile's object as a line on the floor (x from the bottom of its sprite, z just in front of its cell).
    public static Rect FootprintOf(Tilemap map, Vector3Int cell)
    {
        var (left, right) = Base(map.GetSprite(cell));
        Vector3 c = map.GetCellCenterWorld(cell) + Vector3.forward * map.GetTransformMatrix(cell).m23; // a tile nudged off its cell
        return Rect.MinMaxRect(c.x + left, c.z - ObjectFront, c.x + right, c.z - ObjectFront);
    }

    // How far a drawn prop reaches in world x where it touches the ground (its legs, not an awning overhead).
    public static (float left, float right) BaseOf(SpriteRenderer r)
    {
        var (left, right) = Base(r.sprite);
        float x = r.transform.position.x, scale = r.transform.lossyScale.x;
        return (Mathf.Min(x + left * scale, x + right * scale), Mathf.Max(x + left * scale, x + right * scale));
    }

    // The outline's lowest 15% is the part touching the ground: how far it reaches either side of the pivot.
    static (float left, float right) Base(Sprite s)
    {
        float bottom = s.bounds.min.y + s.bounds.size.y * 0.15f;
        float left = float.MaxValue, right = float.MinValue;
        foreach (Vector2 p in s.vertices)
        {
            if (p.y > bottom) continue;
            left = Mathf.Min(left, p.x);
            right = Mathf.Max(right, p.x);
        }
        return left > right ? (s.bounds.min.x, s.bounds.max.x) : (left, right);
    }

    // Is there an object or a prop where someone `radius` wide standing at `p` would touch it? (NavGrid maps with this.)
    public static bool NearObstacle(Vector3 p, float radius)
    {
        foreach (Rect r in footprints)
            if (Within(r, p, radius)) return true;
        foreach (Collider c in props)
        {
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
            Bounds b = c.bounds;
            if (Within(Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z), p, radius)) return true;
        }
        return false;
    }

    // Every box NearObstacle checks right now (the objects' and the props'), for NavGrid to map the squares around each.
    public static void ObstacleBoxes(List<Rect> into)
    {
        into.Clear();
        into.AddRange(footprints);
        foreach (Collider c in props)
        {
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
            Bounds b = c.bounds;
            into.Add(Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z));
        }
    }

    public static bool Within(Rect r, Vector3 p, float radius) =>
        p.x > r.xMin - radius && p.x < r.xMax + radius && p.z > r.yMin - radius && p.z < r.yMax + radius;

    // Would stepping from `from` to `to` walk into an object or a prop?
    bool BumpsIntoObject(Vector3 from, Vector3 to)
    {
        foreach (Rect r in footprints)
            if (Bumps(r, from, to)) return true;
        foreach (Collider c in props)
        {
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
            Bounds b = c.bounds; // follows the prop if it moves
            if (Bumps(Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z), from, to)) return true;
        }
        return false;
    }

    // Stepping into the box (grown by your body's radius) is blocked. Someone already a little inside (spawned there,
    // or shoved in) can step back out, but never further in.
    bool Bumps(Rect r, Vector3 from, Vector3 to)
    {
        Rect box = Rect.MinMaxRect(r.xMin - bodyRadius, r.yMin - bodyRadius, r.xMax + bodyRadius, r.yMax + bodyRadius);
        return Depth(box, to) > Mathf.Max(0f, Depth(box, from));
    }

    // How far inside the box a point is: below zero when it's outside.
    static float Depth(Rect box, Vector3 p) =>
        Mathf.Min(Mathf.Min(p.x - box.xMin, box.xMax - p.x), Mathf.Min(p.z - box.yMin, box.yMax - p.z));

    // The tilemaps of things you bump into, next to `ground` under its Grid: Objects (drawn whole) and See Through
    // (buildings and trees, which open up round the player: SeeThrough).
    public static List<Tilemap> SolidMaps(Tilemap ground)
    {
        var maps = new List<Tilemap>();
        foreach (var name in new[] { ParkArt.ObjectsMap, ParkArt.SeeThroughMap })
        {
            var t = ground != null ? ground.transform.parent.Find(name) : null;
            if (t != null && t.TryGetComponent(out Tilemap map)) maps.Add(map);
        }
        return maps;
    }

    static void FindObstacles(Tilemap floor, List<Tilemap> maps)
    {
        obstaclesFor = floor;
        footprints.Clear();
        props.Clear();
        NavGrid.Invalidate();

        // Solid scenery that stays put: colliders without a Rigidbody (the spirit and the operator have one), other
        // than the floor's own collider on the ground tilemap.
        foreach (Collider c in FindObjectsByType<Collider>())
            if (!c.isTrigger && c.attachedRigidbody == null && !c.TryGetComponent<Tilemap>(out _)) props.Add(c);

        foreach (var map in maps)
        foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            if (map.GetSprite(cell) != null) footprints.Add(FootprintOf(map, cell));
    }
}
