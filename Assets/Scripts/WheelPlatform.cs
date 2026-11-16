using System.Collections.Generic;
using UnityEngine;

// The raised platform at the Ferris wheel's leg, where its support hinge is (WheelHinge). It's locked with a
// fingerprint scanner (FingerprintScanner): nobody steps onto it but the ride technician, once he's scanned in.
public class WheelPlatform : MonoBehaviour
{
    public static readonly List<WheelPlatform> All = new();

    public Vector2 size = new(3f, 2.4f); // x by z, centred on this object

    public bool Open { get; set; }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
        var deck = ParkArt.Make("Deck", transform, "Props/platform", size.y, Color.white, ParkArt.DecalOrder + 1, true);
        deck.transform.parent.localPosition = Vector3.up * 0.03f;
        ParkArt.Lit(deck);
        // The art is drawn size.x : size.y already only roughly; stretch it to the platform.
        Vector3 s = deck.sprite.bounds.size;
        deck.transform.localScale = new Vector3(size.x / s.x, size.y / s.y, 1f);
    }

    public bool Contains(Vector3 p)
    {
        Vector3 d = p - transform.position;
        return Mathf.Abs(d.x) <= size.x * 0.5f && Mathf.Abs(d.z) <= size.y * 0.5f;
    }

    // Can this person step from `from` to `to`? Only the technician, and only once it's open. The spirit floats anywhere.
    public static bool Allows(Visitor v, Vector3 from, Vector3 to)
    {
        if (v == null) return true;
        foreach (var p in All)
            if (p.Contains(to) && !p.Contains(from) && !(p.Open && v.brain is RideTechnician)) return false;
        return true;
    }
}
