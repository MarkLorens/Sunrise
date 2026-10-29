using UnityEngine;

// Animates a flat character sprite (walk / run / idle, cheering with arms up, or a held pose), gives it a soft
// shadow, squashes it when it's hit, and pops thought bubbles over its head to telegraph what it's thinking,
// like the townsfolk in Untitled Goose Game.
// Frames come from Resources/Visitors/<character>/character_<character>_<pose>.png.
[RequireComponent(typeof(SpriteRenderer))]
public class CharacterSprite : MonoBehaviour
{
    public string character = "malePerson";
    public float walkFps = 10f;

    [HideInInspector] public Sprite idle, think, interact, fallDown;
    [HideInInspector] public Sprite[] walk, run, cheer;

    // Held pose that overrides walking/idle (e.g. confused, knocked down). Null = automatic.
    [HideInInspector] public Sprite pose;
    [HideInInspector] public bool running;

    public bool Visible => body.enabled;
    public bool FacingLeft => body.flipX;
    public float HeadHeight => idle.bounds.size.y * transform.lossyScale.y;

    SpriteRenderer body, bubble, shadow;
    Material litMaterial;
    Rigidbody rb;
    Sprite gesture;
    float gestureUntil, cheerUntil;
    float animOffset;
    Vector3 baseScale;
    float punchAge = 1f;
    bool loaded;

    string bubbleName;
    float bubbleAge, bubbleDuration;
    bool bubbleSticky;

    void Awake() => Load();

    public void Load()
    {
        if (loaded) return;
        loaded = true;
        body = GetComponent<SpriteRenderer>();
        litMaterial = body.sharedMaterial;
        rb = GetComponent<Rigidbody>();
        animOffset = Random.value * 10f;
        baseScale = transform.localScale;

        string path = $"Visitors/{character}/character_{character}_";
        idle = Resources.Load<Sprite>(path + "idle");
        think = Resources.Load<Sprite>(path + "think");
        interact = Resources.Load<Sprite>(path + "interact");
        fallDown = Resources.Load<Sprite>(path + "fallDown");
        walk = Frames(path + "walk", 8);
        run = Frames(path + "run", 3);
        cheer = Frames(path + "cheer", 2);
        if (cheer[0] == null) cheer = new[] { interact, interact };
        body.sprite = idle;

        float h = idle.bounds.size.y;
        bubble = ParkArt.Make("Bubble", transform, null, 0.8f, Color.white, ParkArt.OverheadOrder);
        bubble.transform.localPosition = new Vector3(0.3f, h + 0.45f, -0.02f);
        bubble.enabled = false;

        shadow = ParkArt.Make("Shadow", transform, "Markers/shadow", 0.42f, Color.white, ParkArt.DecalOrder, true);
        shadow.transform.parent.localPosition = new Vector3(0f, 0.02f, 0f);
    }

    static Sprite[] Frames(string prefix, int n)
    {
        var frames = new Sprite[n];
        for (int i = 0; i < n; i++) frames[i] = Resources.Load<Sprite>(prefix + i);
        return frames;
    }

    // ---------------------------------------------------------------- thought bubbles

    // Show a thought bubble: Park/Bubbles/<name> (question, exclaim, anger, heart, skull, snack, swords)
    // or an icon from Resources/Icons. Sticky ones stay until replaced or hidden (e.g. anger while chasing).
    public void Show(string name, float seconds = 1.8f, bool stay = false)
    {
        Load();
        var s = ParkArt.Get("Bubbles/" + name);
        if (s == null) s = ParkArt.Get("Icons/" + name);
        if (s == null) return;
        if (bubble.enabled && bubbleName == name && bubbleAge > 0.2f)
        {
            bubbleDuration = Mathf.Max(bubbleDuration, bubbleAge + seconds);
            bubbleSticky = stay;
            return;
        }
        bubbleName = name;
        bubble.sprite = s;
        ParkArt.Fit(bubble, 0.8f);
        bubble.enabled = body.enabled;
        bubbleAge = 0f;
        bubbleDuration = seconds;
        bubbleSticky = stay;
    }

    public void HideBubble()
    {
        bubbleSticky = false;
        bubbleDuration = Mathf.Min(bubbleDuration, bubbleAge + 0.15f);
    }

    // Older API: a sticky bubble, or null to hide it.
    public void SetIcon(string name)
    {
        if (string.IsNullOrEmpty(name)) HideBubble();
        else Show(name, 999f, true);
    }

    // ---------------------------------------------------------------- body

    public void SetVisible(bool visible)
    {
        Load();
        body.enabled = visible;
        shadow.enabled = visible;
        if (!visible) bubble.enabled = false;
    }

    // The lit sprite material ignores colour, so tinted characters (possessed, dead) switch to the unlit one.
    public void SetTint(Color c)
    {
        Load();
        body.color = c;
        body.sharedMaterial = c == Color.white ? litMaterial : ParkArt.Material;
    }

    // Turn toward a point (holds until they next walk somewhere).
    public void Face(Vector3 point)
    {
        float dx = point.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.05f) body.flipX = dx < 0;
    }

    // Briefly strike a pose, e.g. reaching out when handing something over.
    public void Gesture(Sprite s, float seconds = 0.6f)
    {
        gesture = s;
        gestureUntil = Time.time + seconds;
    }

    // Arms in the air: shouting, celebrating, outraged.
    public void Cheer(float seconds = 0.6f) => cheerUntil = Time.time + seconds;

    // Squash and stretch when hit.
    public void Punch() => punchAge = 0f;

    void LateUpdate()
    {
        Vector3 v = rb ? rb.linearVelocity : Vector3.zero;
        bool moving = new Vector2(v.x, v.z).sqrMagnitude > 0.01f;
        if (Mathf.Abs(v.x) > 0.05f) body.flipX = v.x < 0;

        Sprite[] cycle = running ? run : walk;
        if (Time.time < gestureUntil) body.sprite = gesture;
        else if (Time.time < cheerUntil) body.sprite = cheer[(int)(Time.time * 6f) % 2];
        else if (pose != null) body.sprite = pose;
        else if (moving) body.sprite = cycle[(int)((Time.time + animOffset) * walkFps) % cycle.Length];
        else body.sprite = idle;

        if (punchAge < 0.35f)
        {
            punchAge += Time.deltaTime;
            float k = Mathf.Sin(punchAge / 0.35f * Mathf.PI) * (1f - punchAge / 0.35f);
            transform.localScale = new Vector3(baseScale.x * (1f + k * 0.35f), baseScale.y * (1f - k * 0.3f), baseScale.z);
        }
        else if (transform.localScale != baseScale) transform.localScale = baseScale;

        AnimateBubble();
    }

    // Pop in with overshoot, bob, shrink away at the end.
    void AnimateBubble()
    {
        if (bubbleName == null) return;
        bubbleAge += Time.deltaTime;
        if (!bubbleSticky && bubbleAge >= bubbleDuration)
        {
            bubble.enabled = false;
            bubbleName = null;
            return;
        }
        bubble.enabled = body.enabled;
        float pop = bubbleAge < 0.18f ? Mathf.Sin(bubbleAge / 0.18f * Mathf.PI * 0.5f) * 1.15f : Mathf.Lerp(1.15f, 1f, (bubbleAge - 0.18f) * 8f);
        float outro = bubbleSticky ? 1f : Mathf.Clamp01((bubbleDuration - bubbleAge) / 0.15f);
        float size = 0.8f / bubble.sprite.bounds.size.y;
        bubble.transform.localScale = Vector3.one * size * Mathf.Max(0.01f, Mathf.Min(pop, 1.15f) * outro);
        var p = bubble.transform.localPosition;
        p.y = idle.bounds.size.y + 0.45f + Mathf.Sin(bubbleAge * 4f) * 0.05f;
        bubble.transform.localPosition = p;
    }
}
