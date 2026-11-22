using System.Collections.Generic;
using UnityEngine;

// The scenes of what the player's done (the electrocution, the stuck wheel, the explosion, the fire, the wreck), for
// a while afterwards. Each has an invisible police line `keepBack` out from its middle: visitors going about their
// day don't step over it (GroundWalker), they come and gawk from just outside it (Visitor), and a Reset Agent stands
// guard on it, telling the crowd to move along (ResetAgent.Guard). Staff and the player's body cross it freely.
// Unlike ParkChaos zones, nobody panics at these.
public static class Aftermath
{
    struct Site
    {
        public Vector3 center;
        public float keepBack, until;
    }

    static readonly List<Site> sites = new();

    public static void Reset() => sites.Clear();

    // A new scene, and a Reset Agent sent to stand watch on its line.
    public static void Add(Vector3 center, float keepBack, float seconds)
    {
        center.y = 0f;
        sites.Add(new Site { center = center, keepBack = keepBack, until = Time.time + seconds });
        ResetAgent.Guard(center, keepBack);
    }

    // The line comes down (a scene that lasts until its clean-up's done: DrinkingFountain).
    public static void Remove(Vector3 center)
    {
        center.y = 0f;
        sites.RemoveAll(s => (s.center - center).sqrMagnitude < 0.01f);
    }

    // Can this person step from `from` to `to`? Only visitors going about their day are kept out; stepping out's fine.
    public static bool Allows(Visitor v, Vector3 from, Vector3 to)
    {
        if (v == null || v.brain != null || v.Current == Visitor.State.Possessed) return true;
        foreach (var s in sites)
            if (Time.time <= s.until && Inside(s, to) && !Inside(s, from)) return false;
        return true;
    }

    static bool Inside(Site s, Vector3 p)
    {
        Vector3 d = p - s.center;
        return new Vector2(d.x, d.z).magnitude < s.keepBack;
    }

    // The nearest scene whose gawking ring is within `range` of this spot.
    public static bool Nearest(Vector3 p, float range, out Vector3 center, out float keepBack)
    {
        sites.RemoveAll(s => Time.time > s.until);
        center = p;
        keepBack = 0f;
        float best = float.MaxValue;
        foreach (var s in sites)
        {
            Vector3 d = p - s.center;
            d.y = 0f;
            float gap = Mathf.Abs(d.magnitude - s.keepBack);
            if (gap > range || gap >= best) continue;
            best = gap;
            center = s.center;
            keepBack = s.keepBack;
        }
        return best < float.MaxValue;
    }
}
