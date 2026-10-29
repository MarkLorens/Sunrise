using System.Collections.Generic;
using UnityEngine;

// Animates a flat character sprite (walk / run / idle, or a held pose) and shows one icon above its head.
// Frames come from Resources/Visitors/<character>/character_<character>_<pose>.png.
[RequireComponent(typeof(SpriteRenderer))]
public class CharacterSprite : MonoBehaviour
{
    public string character = "malePerson";
    public float walkFps = 10f;

    [HideInInspector] public Sprite idle, think, interact, fallDown;
    [HideInInspector] public Sprite[] walk, run;

    // Held pose that overrides walking/idle (e.g. confused, knocked down). Null = automatic.
    [HideInInspector] public Sprite pose;
    [HideInInspector] public bool running;

    static readonly Dictionary<string, Sprite> iconCache = new();

    SpriteRenderer body;
    SpriteRenderer icon;
    Rigidbody rb;
    Sprite gesture;
    float gestureUntil;
    float animOffset;
    bool loaded;

    void Awake() => Load();

    public void Load()
    {
        if (loaded) return;
        loaded = true;
        body = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody>();
        animOffset = Random.value * 10f;

        string path = $"Visitors/{character}/character_{character}_";
        idle = Resources.Load<Sprite>(path + "idle");
        think = Resources.Load<Sprite>(path + "think");
        interact = Resources.Load<Sprite>(path + "interact");
        fallDown = Resources.Load<Sprite>(path + "fallDown");
        walk = Frames(path + "walk", 8);
        run = Frames(path + "run", 3);
        body.sprite = idle;

        var go = new GameObject("Icon");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0, idle.bounds.size.y + 0.35f, -0.01f);
        go.transform.localScale = Vector3.one * 0.4f;
        icon = go.AddComponent<SpriteRenderer>();
        icon.sharedMaterial = body.sharedMaterial;
        icon.enabled = false;
    }

    static Sprite[] Frames(string prefix, int n)
    {
        var frames = new Sprite[n];
        for (int i = 0; i < n; i++) frames[i] = Resources.Load<Sprite>(prefix + i);
        return frames;
    }

    // Icon name from Resources/Icons, or null to hide it.
    public void SetIcon(string name)
    {
        Load();
        if (string.IsNullOrEmpty(name))
        {
            icon.enabled = false;
            return;
        }
        if (!iconCache.TryGetValue(name, out var s)) iconCache[name] = s = Resources.Load<Sprite>("Icons/" + name);
        icon.sprite = s;
        icon.enabled = true;
    }

    public void SetVisible(bool visible)
    {
        Load();
        body.enabled = visible;
        if (!visible) icon.enabled = false;
    }

    public void SetTint(Color c) => body.color = c;

    // Briefly strike a pose, e.g. reaching out when handing something over.
    public void Gesture(Sprite s, float seconds = 0.6f)
    {
        gesture = s;
        gestureUntil = Time.time + seconds;
    }

    void LateUpdate()
    {
        Vector3 v = rb ? rb.linearVelocity : Vector3.zero;
        bool moving = new Vector2(v.x, v.z).sqrMagnitude > 0.01f;
        if (Mathf.Abs(v.x) > 0.05f) body.flipX = v.x < 0;

        Sprite[] cycle = running ? run : walk;
        if (Time.time < gestureUntil) body.sprite = gesture;
        else if (pose != null) body.sprite = pose;
        else if (moving) body.sprite = cycle[(int)((Time.time + animOffset) * walkFps) % cycle.Length];
        else body.sprite = idle;

        // Gentle bob so head icons read as "floating".
        if (icon.enabled)
        {
            var p = icon.transform.localPosition;
            p.y = idle.bounds.size.y + 0.35f + Mathf.Sin(Time.time * 3f + animOffset) * 0.05f;
            icon.transform.localPosition = p;
        }
    }
}
