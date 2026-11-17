using System.Collections.Generic;
using UnityEngine;

// Ground only one member of staff may step onto, once they've scanned their finger at its FingerprintScanner: the
// raised platform at the Ferris wheel's leg (the ride technician, for its hinge: WheelHinge) and the operator's booth
// round the control panel (the operator). Going about his job the owner walks in and out as he likes; in his body,
// the player has to scan in. Visitors never get in, and with keepSpiritOut the spirit can't float in either, so
// nobody inside can be reached to be possessed.
public class FingerprintArea : MonoBehaviour
{
    public static readonly List<FingerprintArea> All = new();

    public enum Staff { Technician, Operator }

    public Staff owner = Staff.Technician;
    public Vector2 size = new(3f, 2.4f); // x by z, centred on this object
    public bool deck = true;             // draw the raised steel deck; off = a painted line round it
    public bool keepSpiritOut;

    public bool Open { get; set; }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
        if (!deck)
        {
            Outline();
            return;
        }
        var floor = ParkArt.Make("Deck", transform, "Props/platform", size.y, Color.white, ParkArt.DecalOrder + 1, true);
        floor.transform.parent.localPosition = Vector3.up * 0.03f;
        ParkArt.Lit(floor);
        Vector3 s = floor.sprite.bounds.size;
        floor.transform.localScale = new Vector3(size.x / s.x, size.y / s.y, 1f); // stretched to the area
    }

    // A yellow line round it, like the Ferris wheel's closed area.
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

    bool Lets(Visitor v)
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
