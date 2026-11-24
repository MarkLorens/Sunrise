using System.Collections.Generic;
using UnityEngine;

// Construction tape barriers (Props/New/construction_tape_1..3) in a ring round wherever the Reset Agents are working
// (ResetAgent). Round a scene (Aftermath: a puddle being mopped, a fire, a wreck): put up once, just inside its police
// line, when the first of them gets to work there, and left standing while they go from job to job in it, until the
// scene's cleaned up (Aftermath.Remove). Round a job anywhere else (putting a sign back): close round it, while they're
// at it. Standing pictures only: nothing bumps into them (who may pass is Aftermath's say).
public static class ConstructionTape
{
    const float Width = 1.5f;       // each barrier, end to end
    const float SmallRadius = 1.6f; // round a job that isn't a guarded scene

    class Ring
    {
        public Vector3 at;
        public float radius;
        public int users;
        public Tape tape;
    }

    static readonly List<Ring> rings = new();

    // Round a scene, once: nothing if it's taped off already (Clear takes it down).
    public static void Scene(Vector3 center, float radius)
    {
        center.y = 0f;
        rings.RemoveAll(r => r.tape == null);
        foreach (var r in rings)
            if (Flat(center - r.at) < 0.1f) return;
        rings.Add(new Ring { at = center, radius = radius, users = int.MaxValue, tape = Tape.Make(center, radius) });
    }

    // The scene at `center` is cleaned up: its tape comes down.
    public static void Clear(Vector3 center)
    {
        center.y = 0f;
        rings.RemoveAll(r => r.tape == null);
        foreach (var r in rings.ToArray())
            if (Flat(center - r.at) < 0.1f)
            {
                r.tape.Down();
                rings.Remove(r);
            }
    }

    // Up round `site` (`radius` out; 0: a small job's), or a hand more on the ring already round it.
    public static void Put(Vector3 site, float radius)
    {
        site.y = 0f;
        if (radius <= 0f) radius = SmallRadius;
        rings.RemoveAll(r => r.tape == null); // the level's gone since
        foreach (var r in rings)
            if (Flat(site - r.at) <= Mathf.Max(0.5f, r.radius - 0.5f))
            {
                r.users++;
                return;
            }
        rings.Add(new Ring { at = site, radius = radius, users = 1, tape = Tape.Make(site, radius) });
    }

    // One less there; the last to go takes it down.
    public static void TakeDown(Vector3 site)
    {
        site.y = 0f;
        rings.RemoveAll(r => r.tape == null);
        foreach (var r in rings)
            if (Flat(site - r.at) <= Mathf.Max(0.5f, r.radius - 0.5f))
            {
                if (--r.users > 0) return;
                r.tape.Down();
                rings.Remove(r);
                return;
            }
    }

    static float Flat(Vector3 d) => new Vector2(d.x, d.z).magnitude;

    // The barriers of one ring: each springs up in turn round it, and shrinks away when it comes down.
    class Tape : MonoBehaviour
    {
        const float Pop = 0.25f, Stagger = 0.04f;
        readonly List<(Transform t, Vector3 scale, float delay)> parts = new();
        float started, downAt = -1f;

        public static Tape Make(Vector3 site, float radius)
        {
            var tape = new GameObject("Construction Tape").AddComponent<Tape>();
            int n = Mathf.Max(5, Mathf.RoundToInt(2f * Mathf.PI * radius / (Width * 1.1f)));
            float turn = Random.value * 360f;
            for (int i = 0; i < n; i++)
            {
                float a = (turn + i * 360f / n) * Mathf.Deg2Rad;
                Vector3 p = site + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                if (GroundWalker.NearObstacle(p, 0.35f)) continue; // not into a building or a lamp
                p.y = RaisedFloor.Height(p);
                var r = ParkArt.Lit(ParkArt.Make("Tape", tape.transform, "Props/New/construction_tape_" + (i % 3 + 1), 1f, Color.white));
                ParkArt.Fit(r, Width * r.sprite.bounds.size.y / Mathf.Max(0.01f, r.sprite.bounds.size.x)); // Width wide
                r.transform.position = p;
                r.flipX = Random.value < 0.5f;
                tape.parts.Add((r.transform, r.transform.localScale, i * Stagger));
                r.transform.localScale = Vector3.zero;
            }
            tape.started = Time.time;
            return tape;
        }

        public void Down()
        {
            if (downAt < 0f) downAt = Time.time;
        }

        void Update()
        {
            float up = Time.time - started;
            float gone = downAt < 0f ? 0f : (Time.time - downAt) / Pop;
            foreach (var (t, scale, delay) in parts)
            {
                float k = Mathf.Clamp01((up - delay) / Pop);
                k = 1f + 2.7f * Mathf.Pow(k - 1f, 3f) + 1.7f * Mathf.Pow(k - 1f, 2f); // ease out, a little overshoot
                t.localScale = scale * Mathf.Max(0f, k * (1f - Mathf.Clamp01(gone)));
            }
            if (gone >= 1f) Destroy(gameObject);
        }
    }
}
