using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

// Walks on the X/Z floor, but only onto cells that have a tile on the ground tilemap, not into anything on the
// Objects tilemap (trees, lamps, benches, fences: things you bump into; walk-over decor like flowers and bushes
// lives on its own Decor tilemap) or into a prop with a Box Collider (the stalls), and, for visitors, not into a
// staff-only area: StaffArea.
// Driven by WASD/arrows when readKeyboard is on, otherwise by whatever sets `move`: the visitor and staff AI call
// WalkTo, which finds a way around whatever's in the way (NavGrid).
// Gravity + the ground's collider keep it standing on the floor.
[RequireComponent(typeof(Rigidbody))]
public class GroundWalker : MonoBehaviour
{
    public Tilemap ground;
    public Tilemap objects; // things you bump into; found under the same Grid if left empty
    public float bodyRadius = 0.35f; // about half a person's width: how close your middle gets to an object
    public float speed = 5f;
    public bool readKeyboard = true;
    [HideInInspector] public Vector2 move; // x = world X, y = world Z; used when readKeyboard is off

    Rigidbody rb;
    Visitor visitor; // people are kept out of staff-only areas (StaffArea); the spirit isn't
    readonly Route route = new();

    // What's in the way, worked out once and shared by everyone, as boxes on the floor (x, z):
    // - each object on the Objects tilemap, as wide as the bottom of its sprite (a bench's legs, a tree's trunk rather
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
        TryGetComponent(out visitor);

        // Visitors and the mechanic are spawned from code, so look the Objects tilemap up next to the ground.
        Transform t = ground.transform.parent.Find("Objects");
        if (objects == null && t != null) objects = t.GetComponent<Tilemap>();
        if (obstaclesFor != ground) FindObstacles(ground, objects);

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

        rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.y);
    }

    static Vector2 KeyboardInput()
    {
        var kb = Keyboard.current;
        if (kb == null || Possession.InputBlocked) return Vector2.zero;
        return new Vector2(
            (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0),
            (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0)).normalized;
    }

    public bool IsWalkable(Vector3 worldPos)
    {
        Vector3Int cell = ground.WorldToCell(worldPos);
        cell.z = 0; // height above the floor doesn't matter
        return ground.HasTile(cell)
            && !BumpsIntoObject(transform.position, worldPos)
            && StaffArea.Allows(visitor, transform.position, worldPos);
    }

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
        bool staff = visitor == null || visitor.brain != null;
        Vector3 d = route.Next(ground, transform.position, p, staff) - transform.position;
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

    static bool Within(Rect r, Vector3 p, float radius) =>
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

    static void FindObstacles(Tilemap floor, Tilemap map)
    {
        obstaclesFor = floor;
        footprints.Clear();
        props.Clear();
        NavGrid.Invalidate();

        // Solid scenery that stays put: colliders without a Rigidbody (the spirit and the operator have one), other
        // than the floor's own collider on the ground tilemap.
        foreach (Collider c in FindObjectsByType<Collider>())
            if (!c.isTrigger && c.attachedRigidbody == null && !c.TryGetComponent<Tilemap>(out _)) props.Add(c);

        if (map == null) return;
        foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
        {
            Sprite s = map.GetSprite(cell);
            if (s == null) continue;
            // The outline's lowest 15% is the part touching the ground.
            float bottom = s.bounds.min.y + s.bounds.size.y * 0.15f;
            float left = float.MaxValue, right = float.MinValue;
            foreach (Vector2 p in s.vertices)
            {
                if (p.y > bottom) continue;
                left = Mathf.Min(left, p.x);
                right = Mathf.Max(right, p.x);
            }
            if (left > right) (left, right) = (s.bounds.min.x, s.bounds.max.x);
            Vector3 c = map.GetCellCenterWorld(cell);
            footprints.Add(Rect.MinMaxRect(c.x + left, c.z - ObjectFront, c.x + right, c.z - ObjectFront));
        }
    }
}
