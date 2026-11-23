using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Walks on the X/Z floor, but only onto cells that have a tile on the ground tilemap, not into anything on the
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
        return ground.HasTile(cell)
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
