using System.Collections.Generic;
using UnityEngine;

// Ground only one member of staff may step onto, once they've scanned their finger at its FingerprintScanner: the
// raised platform at the Ferris wheel's leg (the ride technician, for its hinge: WheelHinge) and the operator's booth
// round the control panel (the operator). Going about his job the owner walks in and out as he likes; in his body,
// the player has to scan in. Visitors never get in, and with keepSpiritOut the spirit can't float in either, so
// nobody inside can be reached to be possessed.
// It's fenced in square with metal barriers (Props/New/metal_barrier_1..3), like a pool fence: the front and back runs
// face the camera, their sections end to end; the sides run back from the front corners to the back ones (turned to
// run along them, so they're seen going back); and one section of the front is its gate (the lock:
// FingerprintScanner, which draws it). The fence is only a picture: who may step in is still Allows' say.
public class FingerprintArea : MonoBehaviour
{
    public static readonly List<FingerprintArea> All = new();

    public enum Staff { Technician, Operator }

    public Staff owner = Staff.Technician;
    public Vector2 size = new(3f, 2.4f); // x by z, centred on this object
    public bool deck = true;             // draw the raised steel deck; off = a painted line round it
    public bool keepSpiritOut;

    public bool Open { get; set; }

    void OnEnable()
    {
        All.Add(this);
        NavGrid.Invalidate(); // routes go round it
    }

    void OnDisable()
    {
        All.Remove(this);
        NavGrid.Invalidate();
    }

    const float BarrierWidth = 1.2f; // about: each run is split into equal sections, end to end

    void Start()
    {
        Fence();
        if (!deck) return;
        var floor = ParkArt.Make("Deck", transform, "Props/platform", size.y, Color.white, ParkArt.DecalOrder + 1, true);
        floor.transform.parent.localPosition = Vector3.up * 0.03f;
        ParkArt.Lit(floor);
        Vector3 s = floor.sprite.bounds.size;
        floor.transform.localScale = new Vector3(size.x / s.x, size.y / s.y, 1f); // stretched to the area
    }

    // The four runs: back, then the sides, then the front with the gate's section left for the gate.
    void Fence()
    {
        Vector2 h = size * 0.5f;
        Vector3 c = transform.position;
        GateSection(out _, out _, out int gate);
        int n = 0;
        void Run(Vector3 from, Vector3 to, int skip)
        {
            float length = Vector3.Distance(from, to);
            int count = Sections(length);
            float width = length / count;
            bool along = Mathf.Abs(to.z - from.z) > Mathf.Abs(to.x - from.x); // a side, running back
            for (int i = 0; i < count; i++)
            {
                if (i == skip) continue;
                var r = ParkArt.Lit(ParkArt.Make("Barrier", transform, "Props/New/metal_barrier_" + (n++ % 3 + 1), 1f, Color.white));
                ParkArt.Fit(r, width * r.sprite.bounds.size.y / Mathf.Max(0.01f, r.sprite.bounds.size.x)); // just its section wide
                r.transform.position = Vector3.Lerp(from, to, (i + 0.5f) / count);
                if (along) r.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // along the side (the sprite's two-sided)
            }
        }
        Run(c + new Vector3(-h.x, 0f, h.y), c + new Vector3(h.x, 0f, h.y), -1);   // back
        Run(c + new Vector3(-h.x, 0f, -h.y), c + new Vector3(-h.x, 0f, h.y), -1); // left side
        Run(c + new Vector3(h.x, 0f, -h.y), c + new Vector3(h.x, 0f, h.y), -1);   // right side
        Run(c + new Vector3(-h.x, 0f, -h.y), c + new Vector3(h.x, 0f, -h.y), gate); // front, its gate's section left out
    }

    static int Sections(float length) => Mathf.Max(1, Mathf.RoundToInt(length / BarrierWidth));

    // The front run's section that's the gate (the one where its scanner stands), its middle and its width.
    public void GateSection(out Vector3 at, out float width, out int index)
    {
        float gateX = transform.position.x;
        foreach (var s in FindObjectsByType<FingerprintScanner>())
            if (s.platform == this) gateX = s.transform.position.x;
        int count = Sections(size.x);
        width = size.x / count;
        float left = transform.position.x - size.x * 0.5f;
        index = Mathf.Clamp(Mathf.FloorToInt((gateX - left) / width), 0, count - 1);
        at = new Vector3(left + (index + 0.5f) * width, transform.position.y, transform.position.z - size.y * 0.5f);
    }

    // A yellow line round it, like the Ferris wheel's closed area (no longer drawn: the fence stands in for it).
    void Outline()
    {
        var line = gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = ParkArt.Material;
        line.sortingOrder = ParkArt.DecalOrder;
        line.loop = true;
        line.widthMultiplier = 0.08f;
        line.startColor = line.endColor = new Color(1f, 0.82f, 0.15f);
        Vector2 h = size * 0.5f;
        Vector3 c = transform.position + Vector3.up * 0.03f;
        line.positionCount = 4;
        line.SetPositions(new[] { c + new Vector3(-h.x, 0f, -h.y), c + new Vector3(h.x, 0f, -h.y), c + new Vector3(h.x, 0f, h.y), c + new Vector3(-h.x, 0f, h.y) });
    }

    public bool Contains(Vector3 p)
    {
        Vector3 d = p - transform.position;
        return Mathf.Abs(d.x) <= size.x * 0.5f && Mathf.Abs(d.z) <= size.y * 0.5f;
    }

    public bool IsOwner(Visitor v) => v != null && (owner == Staff.Technician ? v.brain is RideTechnician : v.brain is RideOperator);

    public bool Lets(Visitor v)
    {
        if (v == null) return !keepSpiritOut;
        return IsOwner(v) && (Open || v.Current != Visitor.State.Possessed);
    }

    // Can this person (null: the spirit) step from `from` to `to`? Stepping out is always fine.
    public static bool Allows(Visitor v, Vector3 from, Vector3 to)
    {
        foreach (var a in All)
            if (a.Contains(to) && !a.Contains(from) && !a.Lets(v)) return false;
        return true;
    }
}
