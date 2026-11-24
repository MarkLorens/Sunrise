using UnityEngine;

// Cartoon stars circling over whoever (or whatever) just got flattened (Visitor.Squash, StandingProps.Squash): a ring
// of little gold stars going round over their top for a few seconds, twinkling, then fading away.
public class DizzyStars : MonoBehaviour
{
    const int Count = 5;
    const float Spin = 2.6f;     // radians a second round the ring
    const float StarSize = 0.6f; // big enough to read as cartoon stars from the game's camera
    const float Life = 5f, Fade = 1f;

    Transform[] stars;
    SpriteRenderer[] pictures;
    Vector3[] sizes;
    float width, started;

    // Over `at` (the top of what's squashed), a ring `width` across, following `on` (null: staying where it is).
    public static void Over(Transform on, Vector3 at, float width)
    {
        var ring = new GameObject("Dizzy Stars").AddComponent<DizzyStars>();
        ring.transform.position = at;
        if (on != null) ring.transform.SetParent(on, true);
        ring.width = Mathf.Max(0.6f, width);
    }

    void Start()
    {
        started = Time.time;
        stars = new Transform[Count];
        pictures = new SpriteRenderer[Count];
        sizes = new Vector3[Count];
        for (int i = 0; i < Count; i++)
        {
            var r = ParkArt.Make("Star", transform, "Fx/sparkle", StarSize, Fx.Gold, ParkArt.OverheadOrder);
            stars[i] = r.transform;
            pictures[i] = r;
            sizes[i] = r.transform.localScale;
        }
    }

    void Update()
    {
        float t = Time.time - started;
        float alpha = Mathf.Clamp01((Life - t) / Fade);
        for (int i = 0; i < Count; i++)
        {
            float a = t * Spin + i * Mathf.PI * 2f / Count;
            // An ellipse seen from the camera's angle: wide across, shallow front to back; nearer ones a touch lower.
            stars[i].localPosition = new Vector3(Mathf.Cos(a) * width * 0.5f, Mathf.Sin(a) * 0.12f, Mathf.Sin(a) * -0.25f);
            float twinkle = 0.75f + 0.25f * Mathf.Sin(t * 9f + i * 1.7f);
            stars[i].localScale = sizes[i] * twinkle;
            var c = Fx.Gold;
            c.a = alpha;
            pictures[i].color = c;
        }
        if (t >= Life) Destroy(gameObject);
    }
}
