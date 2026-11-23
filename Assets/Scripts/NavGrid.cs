using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// A map of the park for people finding their way around things (A* search). The floor is cut into half-tile squares,
// each marked open or blocked with the same rules that stop someone walking (GroundWalker: a ground tile underfoot, no
// object or prop in the way), plus which squares are staff-only (StaffArea), so routes and bumping always agree.
// Cheap on purpose: the map is built once (and again only when a prop is added), a search runs only when someone picks
// a new destination (Route), and only a few searches run in any one frame.
public static class NavGrid
{
    public enum Result { Found, NoRoute, Busy }

    public const float CellSize = 0.5f;
    const float Clearance = 0.4f;      // a little more than GroundWalker.bodyRadius, so routes don't scrape corners
    const int MaxSearchesPerFrame = 8; // anyone else asking this frame waits for the next one
    const float Diagonal = 1.41421356f;

    static Tilemap builtFor;
    static bool dirty = true;
    static int width, height;
    static float originX, originZ;
    static bool[] blocked, staffOnly;
    static FingerprintArea[] fenced; // each square's scan-in area, if any: only those it lets in route through it
    static Visitor walker;           // who's being routed right now (FindPath)
    static FingerprintArea startedIn; // and the scan-in area they're standing in: they can always route out
    // Open, but some of the square is within Clearance of something: a straight line cutting across it could scrape
    // what the walker bumps into (a centre 0.5 away, a line through the square's edge 0.34 away). Routes still go
    // through these squares, just not as shortcuts across them (Clear).
    static bool[] tight;
    static float[] floor; // each square's floor height (RaisedFloor): neighbours more than a stair apart don't connect

    // Search scratch, reused so a search allocates nothing; `stamp` tells this search's entries from older ones.
    static float[] cost;
    static int[] cameFrom, seenIn, closedIn;
    static int stamp;
    static readonly List<(float f, int cell)> heap = new();
    static readonly List<int> cells = new();
    static readonly List<Rect> boxes = new(); // what's in the way, while mapping (Build)
    static int searchFrame = -1, searchesThisFrame;

    // Something in the way changed (a prop was added): remap before the next search.
    public static void Invalidate() => dirty = true;

    // Corners to walk past on the way from `from` to `to`, the last one being `to` itself. Staff may cut through
    // staff-only areas; so may anyone already standing in one, to get out.
    public static Result FindPath(Tilemap ground, Vector3 from, Vector3 to, bool staff, List<Vector3> corners, Visitor who = null)
    {
        if (Time.frameCount != searchFrame)
        {
            searchFrame = Time.frameCount;
            searchesThisFrame = 0;
        }
        if (searchesThisFrame >= MaxSearchesPerFrame) return Result.Busy;
        searchesThisFrame++;
        if (dirty || builtFor != ground) Build(ground);

        corners.Clear();
        walker = who;
        int here = ToCell(from);
        startedIn = here >= 0 ? fenced[here] : null;
        bool mayUseStaff = staff || (here >= 0 && staffOnly[here]);
        int start = Nearest(from, mayUseStaff), goal = Nearest(to, mayUseStaff);
        if (start < 0 || goal < 0 || !Search(start, goal, mayUseStaff)) return Result.NoRoute;

        cells.Clear();
        for (int i = goal; i != start; i = cameFrom[i]) cells.Add(i);
        cells.Add(start);
        cells.Reverse();

        // Keep only the corners: from each one, skip ahead to the furthest square still in plain sight. The last square
        // is replaced by the destination itself.
        Vector3 at = from;
        for (int k = 1; k < cells.Count;)
        {
            int far = k;
            while (far + 1 < cells.Count && Clear(at, Center(cells[far + 1]), mayUseStaff)) far++;
            if (far == cells.Count - 1) break;
            at = Center(cells[far]);
            corners.Add(at);
            k = far + 1;
        }
        corners.Add(to);
        return Result.Found;
    }

    static void Build(Tilemap ground)
    {
        builtFor = ground;
        dirty = false;
        ground.CompressBounds();
        BoundsInt b = ground.cellBounds;
        Vector3 a = ground.CellToWorld(b.min), c = ground.CellToWorld(b.max);
        originX = Mathf.Min(a.x, c.x);
        originZ = Mathf.Min(a.z, c.z);
        width = Mathf.CeilToInt(Mathf.Abs(c.x - a.x) / CellSize);
        height = Mathf.CeilToInt(Mathf.Abs(c.z - a.z) / CellSize);

        int n = width * height;
        blocked = new bool[n];
        staffOnly = new bool[n];
        fenced = new FingerprintArea[n];
        tight = new bool[n];
        floor = new float[n];
        cost = new float[n];
        cameFrom = new int[n];
        seenIn = new int[n];
        closedIn = new int[n];
        stamp = 0;
        // Near something in the way: marked from each obstacle's box over just the squares around it, with the same test
        // as GroundWalker.NearObstacle. (Asking NearObstacle square by square checked every box for every square: half
        // a second as the park opened, the loading screen stuck meanwhile.)
        bool[] near = new bool[n], nearWide = new bool[n];
        GroundWalker.ObstacleBoxes(boxes);
        foreach (Rect r in boxes)
        {
            Mark(r, Clearance, near);
            Mark(r, Clearance + CellSize * 0.5f, nearWide);
        }
        for (int i = 0; i < n; i++)
        {
            Vector3 p = Center(i);
            Vector3Int cell = ground.WorldToCell(p);
            cell.z = 0;
            blocked[i] = !ground.HasTile(cell) || near[i];
            tight[i] = !blocked[i] && nearWide[i];
            floor[i] = RaisedFloor.Height(p);
            foreach (var area in StaffArea.All)
                if (area.Contains(p)) staffOnly[i] = true;
            foreach (var area in FingerprintArea.All)
                if (area.Contains(p)) fenced[i] = area;
            if (floor[i] > RaisedFloor.Ground) staffOnly[i] = true; // up on the platform: visitors only go there to board (GroundWalker)
        }
    }

    // Each square whose middle is within `radius` of box `r` (GroundWalker.Within), looking only at the squares around it
    // (a square either side to spare).
    static void Mark(Rect r, float radius, bool[] near)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt((r.xMin - radius - originX) / CellSize) - 1);
        int x1 = Mathf.Min(width - 1, Mathf.CeilToInt((r.xMax + radius - originX) / CellSize) + 1);
        int z0 = Mathf.Max(0, Mathf.FloorToInt((r.yMin - radius - originZ) / CellSize) - 1);
        int z1 = Mathf.Min(height - 1, Mathf.CeilToInt((r.yMax + radius - originZ) / CellSize) + 1);
        for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                int i = z * width + x;
                if (!near[i] && GroundWalker.Within(r, Center(i), radius)) near[i] = true;
            }
    }

    static bool Open(int i, bool mayUseStaff) =>
        !blocked[i] && (mayUseStaff || !staffOnly[i]) && (fenced[i] == null || fenced[i] == startedIn || fenced[i].Lets(walker));

    // Up or down a stair at most, never off the side of a raised floor.
    static bool Step(int a, int b) => Mathf.Abs(floor[a] - floor[b]) <= GroundWalker.MaxStep && NoLedge(Center(a), Center(b));

    // Walking straight from a to b, sampled as finely as the floor is mapped (RaisedFloor) the way GroundWalker steps it:
    // never a rise or drop of more than a stair. Squares are coarser than stairs are narrow, so comparing just their
    // middles can miss the side of a flight right beside you.
    public static bool NoLedge(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        d.y = 0;
        int steps = Mathf.CeilToInt(d.magnitude / RaisedFloor.Cell);
        Vector3 at = a;
        float last = RaisedFloor.Height(a);
        for (int s = 1; s <= steps; s++)
        {
            Vector3 p = a + d * (s / (float)steps);
            float h = RaisedFloor.Height(p);
            if (Mathf.Abs(h - last) > GroundWalker.MaxStep || RaisedFloor.Railed(at, p)) return false;
            at = p;
            last = h;
        }
        return true;
    }

    static Vector3 Center(int i) => new(originX + (i % width + 0.5f) * CellSize, 0f, originZ + (i / width + 0.5f) * CellSize);

    static int ToCell(Vector3 p)
    {
        int x = Mathf.FloorToInt((p.x - originX) / CellSize), z = Mathf.FloorToInt((p.z - originZ) / CellSize);
        return x < 0 || z < 0 || x >= width || z >= height ? -1 : z * width + x;
    }

    // The open square nearest `p` on its own level: its own, or one close by (a destination right up against a bench, say;
    // someone at the very edge of a flight of stairs, whose square's middle is up on the deck beside it).
    static int Nearest(Vector3 p, bool mayUseStaff)
    {
        int px = Mathf.Clamp(Mathf.FloorToInt((p.x - originX) / CellSize), 0, width - 1);
        int pz = Mathf.Clamp(Mathf.FloorToInt((p.z - originZ) / CellSize), 0, height - 1);
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int r = 0; r <= 4 && best < 0; r++)
            for (int z = pz - r; z <= pz + r; z++)
                for (int x = px - r; x <= px + r; x++)
                {
                    if (Mathf.Max(Mathf.Abs(x - px), Mathf.Abs(z - pz)) != r) continue; // only this ring
                    if (x < 0 || z < 0 || x >= width || z >= height) continue;
                    int i = z * width + x;
                    if (!Open(i, mayUseStaff) || !NoLedge(p, Center(i))) continue; // not one up or down a ledge from you
                    Vector3 d = Center(i) - p;
                    float distance = d.x * d.x + d.z * d.z;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = i;
                    }
                }
        return best;
    }

    // Can you walk straight from `a` to `b` without crossing a blocked or tight square? (`a` itself may be in one.)
    static bool Clear(Vector3 a, Vector3 b, bool mayUseStaff)
    {
        Vector3 d = b - a;
        d.y = 0;
        int steps = Mathf.CeilToInt(d.magnitude / (CellSize * 0.5f));
        int prev = ToCell(a), first = prev, last = ToCell(b); // the ends' own squares may be tight: you start or stop there
        for (int s = 1; s <= steps; s++)
        {
            int i = ToCell(a + d * (s / (float)steps));
            if (i < 0 || !Open(i, mayUseStaff) || tight[i] && i != first && i != last || prev >= 0 && !Step(prev, i)) return false;
            prev = i;
        }
        return NoLedge(a, b);
    }

    // A*: always grow the route from the square with the lowest (distance so far + straight-line guess to the goal).
    static bool Search(int start, int goal, bool mayUseStaff)
    {
        stamp++;
        heap.Clear();
        int gx = goal % width, gz = goal / width;
        cost[start] = 0f;
        seenIn[start] = stamp;
        cameFrom[start] = start;
        Push(Guess(start, gx, gz), start);

        while (heap.Count > 0)
        {
            int current = Pop();
            if (closedIn[current] == stamp) continue;
            if (current == goal) return true;
            closedIn[current] = stamp;

            int cx = current % width, cz = current / width;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = cx + dx, nz = cz + dz;
                    if (nx < 0 || nz < 0 || nx >= width || nz >= height) continue;
                    int next = nz * width + nx;
                    if (closedIn[next] == stamp || !Open(next, mayUseStaff) || !Step(current, next)) continue;
                    bool diagonal = dx != 0 && dz != 0;
                    // No squeezing diagonally past the corner of something.
                    if (diagonal && (!Open(cz * width + nx, mayUseStaff) || !Open(nz * width + cx, mayUseStaff))) continue;

                    float c = cost[current] + (diagonal ? Diagonal : 1f);
                    if (seenIn[next] == stamp && c >= cost[next]) continue;
                    seenIn[next] = stamp;
                    cost[next] = c;
                    cameFrom[next] = current;
                    Push(c + Guess(next, gx, gz), next);
                }
        }
        return false;
    }

    // Steps to the goal if nothing were in the way (straight and diagonal moves).
    static float Guess(int i, int gx, int gz)
    {
        int dx = Mathf.Abs(i % width - gx), dz = Mathf.Abs(i / width - gz);
        return Mathf.Max(dx, dz) + (Diagonal - 1f) * Mathf.Min(dx, dz);
    }

    // Binary min-heap on f.
    static void Push(float f, int cell)
    {
        heap.Add((f, cell));
        for (int i = heap.Count - 1; i > 0;)
        {
            int parent = (i - 1) / 2;
            if (heap[parent].f <= heap[i].f) break;
            (heap[parent], heap[i]) = (heap[i], heap[parent]);
            i = parent;
        }
    }

    static int Pop()
    {
        int top = heap[0].cell;
        int last = heap.Count - 1;
        heap[0] = heap[last];
        heap.RemoveAt(last);
        for (int i = 0; ;)
        {
            int l = i * 2 + 1, r = l + 1, smallest = i;
            if (l < heap.Count && heap[l].f < heap[smallest].f) smallest = l;
            if (r < heap.Count && heap[r].f < heap[smallest].f) smallest = r;
            if (smallest == i) break;
            (heap[smallest], heap[i]) = (heap[i], heap[smallest]);
            i = smallest;
        }
        return top;
    }
}

// One person's way to where they're heading (GroundWalker.WalkTo): the corners still to walk past. Worked out again
// only when the destination moves, or when they stop getting anywhere.
public class Route
{
    const float ReplanDistance = 0.75f; // a destination moving less than this (a queue slot jiggling) keeps the route
    const float StuckTime = 1.5f;       // no closer to the next corner for this long: work the route out again

    readonly List<Vector3> corners = new();
    int next;
    Vector3 goal;
    bool planned;
    float retryAt, closest, progressAt;

    public Vector3 Next(Tilemap ground, Vector3 from, Vector3 to, bool staff, Visitor who)
    {
        Vector3 moved = to - goal;
        moved.y = 0;
        bool stuck = corners.Count > 0 && Time.time - progressAt > StuckTime;
        bool noRoute = planned && corners.Count == 0;
        if ((!planned || moved.magnitude > ReplanDistance || stuck || noRoute) && Time.time >= retryAt) Plan(ground, from, to, staff, who);
        if (corners.Count == 0) return to; // no route (or not worked out yet): head straight there

        // Passed a corner: on to the next, once it's a straight walk from here (not cutting a corner on the stairs into a ledge).
        while (next < corners.Count - 1 && Flat(corners[next] - from) < 0.35f && NavGrid.NoLedge(from, corners[next + 1]))
        {
            next++;
            closest = float.MaxValue;
        }
        float distance = Flat(corners[next] - from);
        if (distance < closest - 0.05f)
        {
            closest = distance;
            progressAt = Time.time;
        }
        // On the last leg, aim at where the destination is now, not where it was when planned: a slot creeping along
        // with a group leader would otherwise be overshot and walked back to, over and over.
        return next == corners.Count - 1 ? to : corners[next];
    }

    void Plan(Tilemap ground, Vector3 from, Vector3 to, bool staff, Visitor who)
    {
        var result = NavGrid.FindPath(ground, from, to, staff, corners, who);
        if (result == NavGrid.Result.Busy) return; // too many searches this frame: try again next frame
        goal = to;
        planned = true;
        next = 0;
        closest = float.MaxValue;
        progressAt = Time.time;
        if (result == NavGrid.Result.NoRoute) retryAt = Time.time + 1f;
    }

    // Select someone in the Scene view to see where they're going.
    public void DrawGizmos(Vector3 from)
    {
        Gizmos.color = Color.yellow;
        Vector3 at = from;
        for (int i = next; i < corners.Count; i++)
        {
            Gizmos.DrawLine(at + Vector3.up * 0.1f, corners[i] + Vector3.up * 0.1f);
            Gizmos.DrawWireSphere(corners[i] + Vector3.up * 0.1f, 0.12f);
            at = corners[i];
        }
    }

    static float Flat(Vector3 v)
    {
        v.y = 0;
        return v.magnitude;
    }
}
