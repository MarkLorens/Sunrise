using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Anything visitors line up in (Visitor.EnterQueue): the Ferris wheel's line (RideQueue) and the stands' (StandQueue).
public interface IVisitorLine
{
    Vector3 SlotPosition(Visitor v);
    void Leave(Visitor v);
    // Where someone standing in it looks (the stand's counter), if anywhere in particular.
    bool Faces(out Vector3 point);
}

// The line at a stand (the ice cream stands, the balloon seller): at most `capacity` visitors (4) waiting in front of
// the seller, one behind the other going back from the counter. The one at the front is served for a few seconds, then
// off they go about their day (Visitor.Served) and the rest shuffle up. Made by VisitorSpawner for each stand; only as
// long as its spots are on walkable ground (a stand by a wall gets a shorter line, or none) and keep KeepClear away
// from anything the player uses (Interactable: the toilet's valve, the fans, the electrical boxes...) and from the
// mascot, so a line never crowds a puzzle. That's checked again every few seconds, as some of those are made later.
// Nobody stays stuck in it: anyone who stops queueing (possessed, panicking, taken by the tutorial, carried off) drops
// out of it straight away, and anyone who can't reach their spot gives up (Visitor). Groups and staff never join.
public class StandQueue : MonoBehaviour, IVisitorLine
{
    public static readonly List<StandQueue> All = new();

    public const int MaxLine = 4;
    public Vector3 direction = new(0.2f, 0f, -1f); // from the front of the line toward its back
    public float spacing = 0.9f;
    public const float KeepClear = 3f;
    public Vector2 serveTime = new(5f, 9f); // scooping an ice cream, picking a balloon
    public Vector3 counter; // where the people in line look

    readonly List<Visitor> line = new();
    int capacity, groundCapacity;
    float nextCheck;
    float servedAt = -1f;

    public bool HasRoom => line.Count < capacity;
    public int Waiting => line.Count;

    // A line at `front`, as long as its spots are on `ground` and clear of props (up to MaxLine); null if there's no
    // room for even one.
    public static StandQueue Make(string name, Transform parent, Vector3 front, Vector3 direction, Vector3 counter, Tilemap ground)
    {
        var q = new GameObject(name).AddComponent<StandQueue>();
        q.transform.SetParent(parent, true);
        q.transform.position = new Vector3(front.x, 0f, front.z);
        q.direction = direction;
        q.counter = counter;
        while (q.capacity < MaxLine)
        {
            Vector3 p = q.Slot(q.capacity);
            Vector3Int cell = ground.WorldToCell(p);
            cell.z = 0;
            if (!ground.HasTile(cell) || GroundWalker.NearObstacle(p, 0.3f)) break;
            q.capacity++;
        }
        q.groundCapacity = q.capacity;
        q.KeepAway();
        if (q.capacity > 0) return q;
        Destroy(q.gameObject);
        return null;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public bool Join(Visitor v)
    {
        if (!HasRoom || line.Contains(v) || ParkChaos.Threat(transform.position, false, out _)) return false;
        line.Add(v);
        return true;
    }

    public void Leave(Visitor v) => line.Remove(v);

    public bool Has(Visitor v) => line.Contains(v);

    // The line ends before its first spot that's too close to something the player uses, or to the mascot; anyone
    // past the end is out of it (Visitor notices and goes on with their day).
    public void KeepAway()
    {
        var clear = new List<Vector3>();
        foreach (var it in FindObjectsByType<Interactable>(FindObjectsSortMode.None)) clear.Add(it.transform.position);
        foreach (var m in Mascot.All) clear.Add(m.transform.position);
        capacity = 0;
        while (capacity < groundCapacity && !Crowds(Slot(capacity), clear)) capacity++;
        if (line.Count > capacity) line.RemoveRange(capacity, line.Count - capacity);
    }

    static bool Crowds(Vector3 p, List<Vector3> clear)
    {
        foreach (var c in clear)
            if (new Vector2(p.x - c.x, p.z - c.z).magnitude < KeepClear) return true;
        return false;
    }

    public Vector3 SlotPosition(Visitor v) => Slot(Mathf.Max(0, line.IndexOf(v)));

    public bool Faces(out Vector3 point)
    {
        point = counter;
        return true;
    }

    Vector3 Slot(int i) => transform.position + direction.normalized * spacing * i;

    void Update()
    {
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + 5f;
            KeepAway();
        }
        // Anyone who's stopped queueing (possessed, panicking, held by the tutorial, gone) is out of the line.
        line.RemoveAll(v => v == null || !v.isActiveAndEnabled || v.Current != Visitor.State.Queueing);
        if (line.Count == 0)
        {
            servedAt = -1f;
            return;
        }

        var front = line[0];
        Vector3 d = front.transform.position - Slot(0);
        d.y = 0f;
        if (d.magnitude > 0.5f)
        {
            servedAt = -1f; // still walking up
            return;
        }
        if (servedAt < 0f)
        {
            servedAt = Time.time + Random.Range(serveTime.x, serveTime.y);
            return;
        }
        if (Time.time < servedAt) return;
        line.RemoveAt(0);
        servedAt = -1f;
        front.Served();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        for (int i = 0; i < Mathf.Max(capacity, 1); i++) Gizmos.DrawWireSphere(Slot(i), 0.25f);
    }
}
