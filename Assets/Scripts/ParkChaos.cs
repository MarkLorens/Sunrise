using System.Collections.Generic;
using UnityEngine;

// Places where something frightening is happening: a brawl, a body, the Ferris wheel breaking loose, or just a
// possessed visitor shouting. For a while visitors who come near scream and run (kids scare more easily),
// and guards go over to keep people back from the serious ones.
public static class ParkChaos
{
    public const float Scary = 0.3f;      // frightens kids
    public const float Terrifying = 0.6f; // frightens everyone

    struct Zone
    {
        public Vector3 center;
        public float radius;
        public float until;
        public float strength;
    }

    static readonly List<Zone> zones = new();

    public static void Reset() => zones.Clear();

    public static void Add(Vector3 center, float radius, float seconds, float strength = Terrifying)
    {
        center.y = 0f;
        zones.Add(new Zone { center = center, radius = radius, until = Time.time + seconds, strength = strength });
    }

    // Is this spot frightening enough to send someone running? `center` is where to run away from.
    public static bool Threat(Vector3 position, bool timid, out Vector3 center) =>
        Nearest(position, 0f, timid ? Scary : Terrifying, out center, out _);

    // The nearest zone at least this frightening whose edge is within `range` of this spot.
    public static bool Nearest(Vector3 position, float range, float strength, out Vector3 center, out float radius)
    {
        zones.RemoveAll(z => Time.time > z.until);
        center = position;
        radius = 0f;
        float best = float.MaxValue;
        foreach (var z in zones)
        {
            if (z.strength < strength) continue;
            Vector3 d = position - z.center;
            d.y = 0f;
            float gap = d.magnitude - z.radius;
            if (gap > range || gap >= best) continue;
            best = gap;
            center = z.center;
            radius = z.radius;
        }
        return radius > 0f;
    }
}
