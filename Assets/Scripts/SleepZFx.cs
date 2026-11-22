using UnityEngine;

// Someone asleep (the ride technician in his booth): Zs (Resources/Park/Fx/sleep_z_1..3, each on a soft warm glow,
// sleep_z_N_glow, so the dark ink reads at night) drift up from here one after another, swaying and growing as they go,
// and fade away. Switch it off to stop: the ones in the air finish drifting.
public class SleepZFx : MonoBehaviour
{
    public Vector2 every = new(0.7f, 1.1f);     // seconds between Zs
    public Vector2 height = new(0.28f, 0.38f);  // world units tall when they appear (they grow to half again)
    public Vector2 life = new(1.8f, 2.4f);

    static readonly string[] Zs = { "Fx/sleep_z_1", "Fx/sleep_z_2", "Fx/sleep_z_3" };
    float next;
    int last = -1;

    void OnEnable() => next = Time.time + 0.2f;

    void Update()
    {
        if (Time.time < next) return;
        next = Time.time + Random.Range(every.x, every.y);
        int pick = (last + Random.Range(1, Zs.Length)) % Zs.Length; // never the same Z twice in a row
        last = pick;
        var r = ParkArt.Make("Z", null, Zs[pick], Random.Range(height.x, height.y), Color.white, ParkArt.OverheadOrder);
        r.transform.position = transform.position;
        var glow = ParkArt.Make("Glow", r.transform, Zs[pick] + "_glow", 0f, Color.white, ParkArt.OverheadOrder - 1);
        glow.transform.localScale = Vector3.one; // the same canvas as the Z: right behind it
        glow.transform.localPosition = new Vector3(0f, 0f, 0.01f);
        var z = r.gameObject.AddComponent<DriftingZ>();
        z.life = Random.Range(life.x, life.y);
    }

    // One Z: pops in, drifts up and off to the right with a sway and a tilt, growing, then fades out.
    class DriftingZ : MonoBehaviour
    {
        public float life = 2f;
        SpriteRenderer[] parts; // the Z and its glow
        Vector3 start, scale;
        float age, phase;

        void Start()
        {
            parts = GetComponentsInChildren<SpriteRenderer>();
            start = transform.position;
            scale = transform.localScale;
            phase = Random.value * Mathf.PI * 2f;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= life)
            {
                Destroy(gameObject);
                return;
            }
            float t = age / life;
            float sway = Mathf.Sin(phase + age * 2.6f);
            transform.position = start + new Vector3(age * 0.22f + sway * 0.1f, age * 0.5f, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, sway * 12f);
            float pop = Mathf.Clamp01(age / 0.25f);
            transform.localScale = scale * (Mathf.Lerp(0.5f, 1f, 1f - (1f - pop) * (1f - pop)) + t * 0.5f);
            float a = Mathf.Min(pop, Mathf.Clamp01((1f - t) / 0.35f));
            foreach (var p in parts)
            {
                var c = p.color;
                c.a = a;
                p.color = c;
            }
        }
    }
}
