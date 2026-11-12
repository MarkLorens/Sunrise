using System.Collections.Generic;
using UnityEngine;

// Animates a flat character sprite (walk / run / idle, cheering with arms up, or a held pose), gives it a soft
// shadow, a tiny hop on every step while walking, squashes it when it's hit, and knows where its hand is on every
// frame (HandPoint), so what it carries stays in its hand.
// Frames come from Resources/Visitors/<character>/character_<character>_<pose>.png, or for the new lineart bodies
// (character = "body_<variant>") from Resources/ArtLineartTorso/body_<variant>_idle / _walk1..4. If a body has white fill
// layers (body_<variant>_<pose>_skin/top/bottom/shoes, made by Tools > Split Coloured Body Art) they're stacked behind
// the line, follow its frame, and each gets a random colour from its palette. Bodies also get a random face and hair
// from Resources/ArtLineartHead (face_<n>[_open/_closed], hair_<n> + _hair/_hat fills), pinned to the head of
// whatever frame the body shows; faces with a _closed frame blink.
[RequireComponent(typeof(SpriteRenderer))]
public class CharacterSprite : MonoBehaviour
{
    public string character = "malePerson";
    public float walkFps = 10f;
    public float stepBounce = 0.04f; // how much they stretch up between footfalls (0.04 = 4% taller); half again when running

    [HideInInspector] public Sprite idle, think, interact, fallDown;
    [HideInInspector] public Sprite[] walk, run, cheer;

    // Held pose that overrides walking/idle (e.g. confused, knocked down). Null = automatic.
    [HideInInspector] public Sprite pose;
    [HideInInspector] public bool running;

    public bool Visible => body.enabled;
    public bool FacingLeft => facingLeft;
    public float HeadHeight => idle.bounds.size.y * transform.lossyScale.y;

    SpriteRenderer body, shadow;

    // Fill layers under the lineart. Palettes are placeholders until the art direction settles.
    static readonly string[] FillLayers = { "skin", "top", "bottom", "shoes" };
    static readonly Color[][] Palettes =
    {
        Hex("#FFE0C4", "#F1C296", "#E0AC69", "#C68642", "#8D5524"),                       // skin
        Hex("#E74C3C", "#3498DB", "#2ECC71", "#F1C40F", "#9B59B6", "#F4F4F4", "#FF8FAB"), // top
        Hex("#3B5B8C", "#C9B48A", "#3A3A42", "#7A7F87", "#556B2F"),                       // bottom
        Hex("#F4F4F4", "#2B2B2B", "#7B4A2A", "#C0392B"),                                  // shoes
    };
    static readonly Color[] HairPalette = Hex("#1C1C1C", "#3B2618", "#6B4226", "#A0522D", "#C99B4F", "#8C8C8C");
    static readonly Color[] HatPalette = Hex("#E74C3C", "#3498DB", "#2ECC71", "#F1C40F", "#F4F4F4", "#2B2B2B");
    static Dictionary<string, Sprite> headArt;
    static readonly List<string> faceIds = new(), hairIds = new();
    static readonly Dictionary<Sprite, Vector3> headAnchors = new();
    static readonly Dictionary<Sprite, Vector3> handAnchors = new();
    Transform head;
    SpriteRenderer face;
    Sprite faceOpen, faceClosed;
    float nextBlink, blinkUntil;
    readonly List<(SpriteRenderer r, Color c)> headParts = new();

    // While possessed the face is the spirit's (Resources/MC/MC_face_eyes *), glancing left -> center -> right like
    // SpiritSprite's. Its drawing (eyes + grin) is PossessedFaceWidth pixels of the head art wide on everyone (the
    // faces are ~160 px; not measured per face, since face_5's glasses make it 247), centred on their own face.
    static Sprite[] mcEyes;
    const float PossessedFaceWidth = 250f;
    SpriteRenderer possessedFace;
    bool possessed;
    int eyeStep;
    float eyeUntil;

    readonly List<SpriteRenderer> fills = new();
    readonly List<Color> fillColors = new();
    readonly Dictionary<Sprite, Sprite[]> fillFrames = new(); // line frame -> its fill frame per layer
    Color tint = Color.white;
    Rigidbody rb;
    Sprite gesture;
    float gestureUntil, cheerUntil;
    float animOffset;
    Vector3 baseScale;
    float stride;    // 0..1: eases the step hop in when they set off and out when they stop
    float punchAge = 1f;
    bool loaded;
    bool facingLeft, drawnFacingLeft; // Kenney art faces right, the lineart faces left
    static readonly int TintId = Shader.PropertyToID("_Tint");

    void Awake() => Load();

    public void Load()
    {
        if (loaded) return;
        loaded = true;
        body = GetComponent<SpriteRenderer>();
        // Characters whose renderer was auto-added (e.g. the Mechanic) start on Unity's default sprite material.
        // People never get a see-through hole cut into them (their fill layers copy this material below).
        body.sharedMaterial = ParkArt.CharacterMaterial;
        ParkArt.CastShadows(body);
        rb = GetComponent<Rigidbody>();
        animOffset = Random.value * 10f;
        baseScale = transform.localScale;

        if (character.StartsWith("body_"))
        {
            // Lineart bodies only have idle + a 4-frame walk so far; the other poses borrow those until they're drawn.
            string line = $"ArtLineartTorso/{character}_";
            idle = Resources.Load<Sprite>(line + "idle");
            walk = new Sprite[4];
            for (int i = 0; i < 4; i++) walk[i] = Resources.Load<Sprite>(line + "walk" + (i + 1));
            run = walk;
            think = interact = fallDown = idle;
            cheer = new[] { idle, idle };
            drawnFacingLeft = true;
            LoadFills(line);
            LoadHead();
            ApplyTint();
        }
        else
        {
            string path = $"Visitors/{character}/character_{character}_";
            idle = Resources.Load<Sprite>(path + "idle");
            think = Resources.Load<Sprite>(path + "think");
            interact = Resources.Load<Sprite>(path + "interact");
            fallDown = Resources.Load<Sprite>(path + "fallDown");
            walk = Frames(path + "walk", 8);
            run = Frames(path + "run", 3);
            cheer = Frames(path + "cheer", 2);
            if (cheer[0] == null) cheer = new[] { interact, interact };
        }
        body.sprite = idle;

        shadow = ParkArt.Make("Shadow", transform, "Markers/shadow", 0.42f, Color.white, ParkArt.DecalOrder, true);
        shadow.transform.parent.localPosition = new Vector3(0f, 0.02f, 0f);
    }

    void LoadFills(string line)
    {
        var poses = new List<(string name, Sprite sprite)> { ("idle", idle) };
        for (int i = 0; i < walk.Length; i++) poses.Add(("walk" + (i + 1), walk[i]));

        for (int k = 0; k < FillLayers.Length; k++)
        {
            var frames = new Sprite[poses.Count];
            bool any = false;
            for (int p = 0; p < poses.Count; p++)
            {
                frames[p] = Resources.Load<Sprite>($"{line}{poses[p].name}_{FillLayers[k]}");
                if (frames[p] != null) any = true;
            }
            if (!any) continue;

            var r = new GameObject(FillLayers[k]).AddComponent<SpriteRenderer>();
            r.transform.SetParent(transform, false);
            // Behind the line: the lit shader writes depth, so layers are spaced in z rather than sorted.
            r.transform.localPosition = new Vector3(0f, 0f, 0.002f + 0.0005f * fills.Count);
            r.sharedMaterial = body.sharedMaterial;
            ParkArt.CastShadows(r);
            int layer = fills.Count;
            for (int p = 0; p < poses.Count; p++)
            {
                if (poses[p].sprite == null) continue;
                if (!fillFrames.TryGetValue(poses[p].sprite, out var set)) fillFrames[poses[p].sprite] = set = new Sprite[FillLayers.Length];
                set[layer] = frames[p];
            }
            var palette = Palettes[k];
            fills.Add(r);
            fillColors.Add(palette[Random.Range(0, palette.Length)]);
        }
        ApplyTint();
    }

    void LoadHead()
    {
        if (headArt == null)
        {
            headArt = new Dictionary<string, Sprite>();
            foreach (var s in Resources.LoadAll<Sprite>("ArtLineartHead"))
            {
                headArt[s.name] = s;
                var parts = s.name.Split('_'); // face_3_open, face_6, hair_7, hair_7_hat
                string id = parts[0] + "_" + parts[1];
                if (parts[0] == "face" && !faceIds.Contains(id)) faceIds.Add(id);
                if (parts[0] == "hair" && parts.Length == 2) hairIds.Add(id);
            }
        }
        if (faceIds.Count == 0 && hairIds.Count == 0) return;

        head = new GameObject("Head").transform;
        head.SetParent(transform, false);
        // In front of the body line (lower z is nearer the camera): face, then hair fills, then the hair line.
        if (faceIds.Count > 0)
        {
            string id = faceIds[Random.Range(0, faceIds.Count)];
            faceOpen = HeadArt(id + "_open") ?? HeadArt(id);
            faceClosed = HeadArt(id + "_closed");
            face = HeadPart("Face", faceOpen, -0.002f, Color.white);
            nextBlink = Time.time + Random.Range(1f, 4f);
        }
        if (hairIds.Count > 0)
        {
            string id = hairIds[Random.Range(0, hairIds.Count)];
            HeadPart("HairFill", HeadArt(id + "_hair"), -0.003f, HairPalette[Random.Range(0, HairPalette.Length)]);
            HeadPart("HatFill", HeadArt(id + "_hat"), -0.003f, HatPalette[Random.Range(0, HatPalette.Length)]);
            HeadPart("Hair", HeadArt(id), -0.004f, Color.white);
        }
    }

    static Sprite HeadArt(string name) => headArt.TryGetValue(name, out var s) ? s : null;

    // Swap to the spirit's face (Possession, via Visitor.BeginPossession / EndPossession).
    public void SetPossessed(bool on)
    {
        Load();
        if (face == null) return;
        if (on && possessedFace == null)
        {
            if (mcEyes == null)
            {
                var center = Resources.Load<Sprite>("MC/MC_face_eyes center");
                mcEyes = new[] { Resources.Load<Sprite>("MC/MC_face_eyes left"), center, Resources.Load<Sprite>("MC/MC_face_eyes right"), center };
            }
            if (mcEyes[1] == null) return;
            possessedFace = new GameObject("Possessed Face").AddComponent<SpriteRenderer>();
            possessedFace.transform.SetParent(head, false);
            possessedFace.sharedMaterial = body.sharedMaterial;
            ParkArt.CastShadows(possessedFace);
            // Centre the spirit's drawing on the visitor's face drawing (both tight meshes), at a fixed width.
            Rect mine = DrawnRect(faceOpen), theirs = DrawnRect(mcEyes[1]);
            float scale = PossessedFaceWidth / faceOpen.pixelsPerUnit / theirs.width;
            Vector2 at = mine.center - theirs.center * scale;
            possessedFace.transform.localPosition = new Vector3(at.x, at.y, -0.002f);
            possessedFace.transform.localScale = Vector3.one * scale;
            possessedFace.sprite = mcEyes[0];
            ApplyTint();
        }
        possessed = on && possessedFace != null;
        eyeStep = 0;
        eyeUntil = Time.time + Random.Range(1.5f, 3f);
        if (possessedFace != null) possessedFace.sprite = mcEyes[0];
        SetVisible(body.enabled);
    }

    static Rect DrawnRect(Sprite s)
    {
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        foreach (var p in s.vertices) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    SpriteRenderer HeadPart(string name, Sprite sprite, float z, Color color)
    {
        if (sprite == null) return null;
        var r = new GameObject(name).AddComponent<SpriteRenderer>();
        r.transform.SetParent(head, false);
        r.transform.localPosition = new Vector3(0f, 0f, z);
        r.sprite = sprite;
        r.sharedMaterial = body.sharedMaterial;
        ParkArt.CastShadows(r);
        headParts.Add((r, color));
        return r;
    }

    // Head pieces pivot on the head's top-left corner; find that corner on a body frame from its tight mesh, which
    // hugs the drawing ~3 px outside it. Same head drawing in every body, so it's the same spot up to the bob.
    static Vector3 HeadAnchor(Sprite s)
    {
        if (headAnchors.TryGetValue(s, out var a)) return a;
        var v = s.vertices;
        float top = float.MinValue, left = float.MaxValue, pad = 3f / s.pixelsPerUnit;
        foreach (var p in v) top = Mathf.Max(top, p.y);
        foreach (var p in v) if (p.y > top - 200f / s.pixelsPerUnit) left = Mathf.Min(left, p.x); // the head's rows
        return headAnchors[s] = new Vector3(left + pad, top - pad, 0f);
    }

    // Where a held item goes: the hand nearer the camera, which is the back hand as drawn (the lineart faces left, so
    // its right-hand one; Kenney art faces right, so its left-hand one). It's the outermost point of the frame's tight
    // mesh on that side between 20% and 50% of the way up (below the sleeves, above the feet), moved in by HandInset. Measured on every lineart frame, it lands on that hand while walking too. Poses that move the arms
    // (cheering, falling, reaching) keep the idle frame's spot.
    public Vector3 HandPoint
    {
        get
        {
            Load();
            var s = body.sprite;
            bool armsDown = s == idle || System.Array.IndexOf(walk, s) >= 0 || System.Array.IndexOf(run, s) >= 0;
            return transform.TransformPoint(HandAnchor(armsDown ? s : idle, drawnFacingLeft));
        }
    }

    // How far in from the hand's outer edge its middle is, as a fraction of the body's height (measured on the
    // lineart: hands are ~0.14 of the height wide, their middle ~0.075 in, plus the tight mesh's few pixels of margin).
    const float HandInset = 0.08f;

    static Vector3 HandAnchor(Sprite s, bool rightSide)
    {
        if (handAnchors.TryGetValue(s, out var a)) return a;
        var v = s.vertices;
        float bottom = float.MaxValue, top = float.MinValue;
        foreach (var p in v)
        {
            bottom = Mathf.Min(bottom, p.y);
            top = Mathf.Max(top, p.y);
        }
        float h = top - bottom, side = rightSide ? 1f : -1f;
        Vector2 hand = new(0f, bottom + 0.35f * h);
        float outermost = float.MinValue;
        foreach (var p in v)
        {
            if (p.y < bottom + 0.2f * h || p.y > bottom + 0.5f * h || p.x * side <= outermost) continue;
            outermost = p.x * side;
            hand = p;
        }
        if (outermost > float.MinValue) hand.x -= side * HandInset * h;
        return handAnchors[s] = new Vector3(hand.x, hand.y, 0f);
    }

    // Fills show the frame that matches whatever the line is showing; the head follows its bob.
    void SyncFills()
    {
        if (head != null)
        {
            head.localPosition = HeadAnchor(body.sprite);
            if (faceClosed != null)
            {
                if (Time.time > nextBlink) { blinkUntil = Time.time + 0.12f; nextBlink = Time.time + Random.Range(2f, 5f); }
                face.sprite = Time.time < blinkUntil ? faceClosed : faceOpen;
            }
            if (possessed && Time.time >= eyeUntil)
            {
                eyeStep = (eyeStep + 1) % mcEyes.Length;
                eyeUntil = Time.time + (eyeStep % 2 == 1 ? 0.15f : Random.Range(1.5f, 3f));
                possessedFace.sprite = mcEyes[eyeStep];
            }
        }
        if (fills.Count == 0) return;
        fillFrames.TryGetValue(body.sprite, out var set);
        for (int k = 0; k < fills.Count; k++) fills[k].sprite = set?[k];
    }

    static Color[] Hex(params string[] codes)
    {
        var c = new Color[codes.Length];
        for (int i = 0; i < codes.Length; i++) ColorUtility.TryParseHtmlString(codes[i], out c[i]);
        return c;
    }

    static Sprite[] Frames(string prefix, int n)
    {
        var frames = new Sprite[n];
        for (int i = 0; i < n; i++) frames[i] = Resources.Load<Sprite>(prefix + i);
        return frames;
    }

    // ---------------------------------------------------------------- body

    public void SetVisible(bool visible)
    {
        Load();
        body.enabled = visible;
        shadow.enabled = visible;
        foreach (var f in fills) f.enabled = visible;
        foreach (var (r, _) in headParts) r.enabled = visible;
        if (face != null) face.enabled = visible && !possessed;
        if (possessedFace != null) possessedFace.enabled = visible && possessed;
    }

    // Tinted characters (possessed, dead). SpriteRenderer.color doesn't reach the lit shader graph in Unity 6,
    // so the tint goes through SpriteLit3D's _Tint; they keep their shadow.
    public void SetTint(Color c)
    {
        Load();
        tint = c;
        ApplyTint();
    }

    // The overall tint (possessed, dead) multiplies each fill layer's own colour.
    void ApplyTint()
    {
        Tint(body, tint);
        for (int k = 0; k < fills.Count; k++) Tint(fills[k], tint * fillColors[k]);
        foreach (var (r, c) in headParts) Tint(r, tint * c);
        if (possessedFace != null) Tint(possessedFace, tint);
    }

    static void Tint(SpriteRenderer r, Color c)
    {
        var block = new MaterialPropertyBlock();
        r.GetPropertyBlock(block);
        block.SetColor(TintId, c);
        r.SetPropertyBlock(block);
    }

    // Turn toward a point (holds until they next walk somewhere).
    public void Face(Vector3 point)
    {
        float dx = point.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.05f) facingLeft = dx < 0;
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
        if (Mathf.Abs(v.x) > 0.05f) facingLeft = v.x < 0;

        Sprite[] cycle = running ? run : walk;
        float frame = (Time.time + animOffset) * walkFps;
        bool stepping = false;
        if (Time.time < gestureUntil) body.sprite = gesture;
        else if (Time.time < cheerUntil) body.sprite = cheer[(int)(Time.time * 6f) % 2];
        else if (pose != null) body.sprite = pose;
        else if (moving)
        {
            body.sprite = cycle[(int)frame % cycle.Length];
            stepping = true;
        }
        else body.sprite = idle;
        SyncFills();

        // Squash and stretch (the feet stay put: the sprite's pivot is at its feet).
        // Walking: two steps per cycle, footfalls on frames 0 and 4 - a slight squash as the foot lands, a slight
        // stretch as the legs pass. A tiny hop that says "moving" without anyone noticing it as an effect.
        stride = Mathf.MoveTowards(stride, stepping ? 1f : 0f, Time.deltaTime * 6f);
        float hop = (Mathf.Abs(Mathf.Sin(frame / cycle.Length * 2f * Mathf.PI)) - 0.3f) * stepBounce * (running ? 1.5f : 1f) * stride;
        float sx = 1f - hop * 0.5f, sy = 1f + hop;
        // Hit: a big squash that wobbles back.
        if (punchAge < 0.35f)
        {
            punchAge += Time.deltaTime;
            float k = Mathf.Sin(punchAge / 0.35f * Mathf.PI) * (1f - punchAge / 0.35f);
            sx *= 1f + k * 0.35f;
            sy *= 1f - k * 0.3f;
        }
        // Facing is a negative x scale, not flipX: the lit shader draws flipped asymmetric sprites washed out.
        float flip = facingLeft != drawnFacingLeft ? -1f : 1f;
        var scale = new Vector3(baseScale.x * flip * sx, baseScale.y * sy, baseScale.z);
        if (transform.localScale != scale) transform.localScale = scale;
    }
}
