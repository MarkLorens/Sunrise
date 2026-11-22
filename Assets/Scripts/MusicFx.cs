using UnityEngine;

// A loudspeaker playing, always on: the two Speakers (Speaker adds it) and the four theme song speakers up on the
// park's corners (added in the scene): the box thumps to the beat, squashing down a touch from its feet,
// and music notes (Resources/Park/Fx/note_1..3, each on a soft warm glow, note_N_glow, so the dark ink reads at night)
// puff out of the cone now and then, float up swaying and fade away.
public class MusicFx : MonoBehaviour
{
    public float bpm = 116f;               // the thump's beat
    public float thump = 0.035f;           // how much taller it gets on a beat (0.035 = 3.5%)
    public Vector2 noteEvery = new(0.45f, 0.85f); // seconds between notes
    public Vector2 noteHeight = new(0.42f, 0.6f); // world units tall
    public Vector2 noteLife = new(1.8f, 2.5f);

    static readonly string[] Notes = { "Fx/note_1", "Fx/note_2", "Fx/note_3" };

    SpriteRenderer body;
    Vector3 baseScale;
    float beat, nextNote;

    void Start()
    {
        body = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        beat = Random.value; // the two speakers don't thump in step
        nextNote = Time.time + Random.Range(0f, noteEvery.y);
    }

    void OnDisable()
    {
        if (body != null) transform.localScale = baseScale; // stopped: not frozen mid-thump
    }

    void Update()
    {
        // The thump: a quick kick on each beat that settles before the next (feet stay put: the pivot's at the bottom).
        beat = (beat + Time.deltaTime * bpm / 60f) % 1f;
        float kick = Mathf.Exp(-beat * 9f);
        transform.localScale = new Vector3(baseScale.x * (1f - kick * thump * 0.5f), baseScale.y * (1f + kick * thump), baseScale.z);

        if (Time.time < nextNote || body == null || !body.enabled) return;
        nextNote = Time.time + Random.Range(noteEvery.x, noteEvery.y);
        Note();
    }

    // Out of the speaker's box, a little over halfway up: the right part of a wide picture (speaker 1 trails its cable
    // off to the left), the left part of a narrow one (speaker 2's plug sticks out on the right).
    void Note()
    {
        Bounds b = body.bounds;
        bool wide = b.size.x > b.size.y * 1.4f;
        float from = wide ? 0.55f : 0.12f, to = wide ? 0.9f : 0.68f;
        Vector3 at = new(Mathf.Lerp(b.min.x, b.max.x, Random.Range(from, to)), b.min.y + b.size.y * Random.Range(0.55f, 0.85f), b.center.z);
        // In front of the speaker as seen from the camera, so a speaker turned at an angle (the corner ones) doesn't hide it.
        var cam = Camera.main;
        if (cam != null) at += (cam.transform.position - at).normalized * (b.extents.z + 0.3f);
        string art = Notes[Random.Range(0, Notes.Length)];
        var r = ParkArt.Make("Note", null, art, Random.Range(noteHeight.x, noteHeight.y), Color.white, ParkArt.OverheadOrder);
        r.transform.position = at;
        // The glow: the same canvas as the note, so it sits exactly behind it.
        var glow = ParkArt.Make("Glow", r.transform, art + "_glow", 0f, Color.white, ParkArt.OverheadOrder - 1);
        glow.transform.localScale = Vector3.one;
        glow.transform.localPosition = new Vector3(0f, 0f, 0.01f);
        var note = r.gameObject.AddComponent<FloatingNote>();
        note.life = Random.Range(noteLife.x, noteLife.y);
        note.drift = Random.Range(-0.25f, 0.25f);
    }

    // One note: pops in, rises, sways side to side with a little tilt, and fades out.
    class FloatingNote : MonoBehaviour
    {
        public float life = 2f, drift;
        SpriteRenderer[] parts; // the note and its glow
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
            float sway = Mathf.Sin(phase + age * 3.2f);
            transform.position = start + new Vector3(drift * age + sway * 0.12f, age * 0.75f, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, sway * 14f);
            float pop = Mathf.Clamp01(age / 0.25f);
            transform.localScale = scale * Mathf.Lerp(0.5f, 1f, 1f - (1f - pop) * (1f - pop));
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
