using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Draws a flat character sprite (standing, cheering with arms up, or a held pose), gives it a soft shadow, and knows
// where its hand is (HandPoint), so what it carries stays in its hand. There's no walk cycle: people waddle, rocking
// from side to side round their feet as they go (Waddle, in LateUpdate).
// Art comes from Resources/Visitors/<character>/character_<character>_<pose>.png, or for the lineart bodies
// (character = "body_<variant>") from Resources/ArtLineartTorso/body_<variant>_idle. If a body has white fill layers
// (body_<variant>_idle_skin/top/bottom/shoes, made by Tools > Split Coloured Body Art) they're stacked behind the
// line and each gets a random colour from its palette. Bodies also get a random face and hair from
// Resources/ArtLineartHead (face_<n>[_open/_closed], hair_<n> + _hair/_hat fills), drawn on canvases that line up
// with the body's; faces with a _closed frame blink.
// Staff can be workers (character = "worker_<role>"): Resources/Workers/worker_<role>_body with its own
// worker_<role>_face on the same canvas, and Expression() swaps in worker_face_<name> (surprised_1, surprised_2).
// Scared (panicFace / panicBody): lineart bodies switch to body_<variant>_panic (arms up, with its own fills) and the
// face to one of Resources/ArtLineartHeadPanic/face_panic_<n> (each visitor has their own).
// The mascot (character = "mascot"): Resources/Mascot/mascot_idle, mascot_pose_1/2 (poses) and mascot_panic.
[RequireComponent(typeof(SpriteRenderer))]
public class CharacterSprite : MonoBehaviour
{
    public string character = "malePerson";
    [Tooltip("How far they lean to each side as they waddle, in degrees (wider when running).")]
    public float waddleAngle = 7f;
    [Tooltip("Rocks per second at a standstill's pace; each unit/second of speed adds waddlePerSpeed.")]
    public float waddleRate = 0.6f, waddlePerSpeed = 0.5f;
    public float stepBounce = 0.04f; // how much they stretch up passing upright mid-waddle (0.04 = 4% taller); half again when running

    [HideInInspector] public Sprite idle, think, interact, fallDown;
    [HideInInspector] public Sprite[] cheer;

    // Held pose that overrides walking/idle (e.g. confused, knocked down). Null = automatic.
    [HideInInspector] public Sprite pose;
    [HideInInspector] public bool running;
    // Scared: the panic face (gawking at a scene) and the panic body (running from it). Null art = no change.
    [HideInInspector] public Sprite panic;
    [HideInInspector] public Sprite[] poses = new Sprite[0]; // the mascot's show-off poses
    [HideInInspector] public bool panicFace, panicBody;
    // Rocking from side to side on the spot, as if walking (the mascot showing off in a pose).
    [HideInInspector] public bool dance;
    // Asleep on their feet (the ride technician in his booth): slow deep breaths, the head lolling side to side, and now
    // and then nodding right off and jerking back up.
    [HideInInspector] public bool dozing;
    // Squashed flat (the Ferris wheel rolled over them): slammed down, then lying flat on the ground like a paper cutout,
    // a little wider, arms up, with a boing. Squash() starts it; for good.
    public bool Flat { get; private set; }
    // Toppled over (electrocuted: Visitor.Die zapped): falling stiff as a plank round their feet onto their side, a
    // little bounce as they hit the ground, then lying there. FallOver() starts it; for good.
    public bool Fallen { get; private set; }
    float fallSince, fallSide;
    float flatSince, flatTilt;
    float nodAt = -1f; // when the next nod starts
    // True while they're walking (moving, visible, not held in a pose): Footsteps plays while this is on.
    public bool Stepping { get; private set; }

    public bool Visible => body.enabled;
    public Bounds Bounds => body.bounds; // the drawn body, wherever the waddle has it
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
    static readonly Dictionary<Sprite, Vector3> handAnchors = new();
    Transform head;
    SpriteRenderer face;
    Sprite faceOpen, faceClosed, facePanic;
    static Sprite[] panicFaces;

    // The people's art, read in ahead by the loading screen (while the park waits to be switched to), a folder a frame:
    // the crowd's all made at once as the park starts (VisitorSpawner), and reading it from disk there and then held
    // that frame up. Kept here so it's not let go before they ask for it (Resources.Load then finds it loaded).
    static readonly string[] PreloadFolders = { "ArtLineartHead", "ArtLineartHeadPanic", "ArtLineartTorso", "Workers" };
    static readonly List<Sprite> preloaded = new();

    public static IEnumerator Preload()
    {
        if (preloaded.Count > 0) yield break;
        foreach (var folder in PreloadFolders)
        {
            preloaded.AddRange(Resources.LoadAll<Sprite>(folder));
            yield return null;
        }
    }
    float nextBlink, blinkUntil;
    readonly List<(SpriteRenderer r, Color c)> headParts = new();

    // While possessed the face is the spirit's (Resources/MC: MC_face_eyes *, glancing left -> center -> right like
    // SpiritSprite's, over its MC_face_mouth grin). Its drawing (eyes + grin) is PossessedFaceWidth pixels of the head
    // art wide on everyone (the faces are ~265 px; not measured per face, since face_6's glasses make it 384), centred
    // on their own face. The spirit's pieces are drawn on one shared canvas, so eyes and grin line up as they are.
    static Sprite[] mcEyes;
    static Sprite mcMouth;
    SpriteRenderer possessedMouth;
    const float PossessedFaceWidth = 400f;
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
    float gestureUntil, cheerUntil, twitchUntil;
    float waddle;    // where they are in their side-to-side rock (radians; random start, so crowds don't rock in step)
    // The drawn character (body, fills, head) hangs off this child, and the waddle leans and flips it, never the
    // root: the root carries the Rigidbody, and any script move of an interpolated body's transform stops its
    // interpolation until the next physics step, so walking people (and the camera on them) stuttered at 50Hz.
    Transform drawn;
    float stride;    // 0..1: eases the waddle in when they set off and out when they stop
    bool loaded;
    bool facingLeft, drawnFacingLeft; // Kenney art faces right, the lineart faces left
    static readonly int TintId = Shader.PropertyToID("_Tint");

    void Awake() => Load();

    public void Load()
    {
        if (loaded) return;
        loaded = true;
        // The root keeps its renderer (RequireComponent) but switched off and empty; the one that draws is on Pose.
        var root = GetComponent<SpriteRenderer>();
        drawn = new GameObject("Pose").transform;
        drawn.SetParent(transform, false);
        body = drawn.gameObject.AddComponent<SpriteRenderer>();
        body.sortingLayerID = root.sortingLayerID;
        body.sortingOrder = root.sortingOrder;
        root.sprite = null;
        root.enabled = false;
        // Characters whose renderer was auto-added (e.g. the Mechanic) start on Unity's default sprite material.
        // People never get a see-through hole cut into them (their fill layers copy this material below).
        body.sharedMaterial = ParkArt.CharacterMaterial;
        ParkArt.CastShadows(body);
        rb = GetComponent<Rigidbody>();
        if (!TryGetComponent<Footsteps>(out _)) gameObject.AddComponent<Footsteps>();
        waddle = Random.value * Mathf.PI * 2f;

        if (character.StartsWith("body_"))
        {
            // Lineart bodies only have the idle pose so far; the other poses borrow it until they're drawn.
            string line = $"ArtLineartTorso/{character}_";
            idle = Resources.Load<Sprite>(line + "idle");
            panic = Resources.Load<Sprite>(line + "panic");
            think = interact = fallDown = idle;
            cheer = new[] { idle, idle };
            drawnFacingLeft = true;
            LoadFills(line);
            LoadHead();
            ApplyTint();
        }
        else if (character.StartsWith("worker_"))
        {
            // One drawn pose so far, like the lineart bodies; the face is theirs, drawn on the same canvas.
            idle = Resources.Load<Sprite>($"Workers/{character}_body");
            think = interact = fallDown = idle;
            cheer = new[] { idle, idle };
            drawnFacingLeft = true;
            var own = Resources.Load<Sprite>($"Workers/{character}_face");
            if (own != null)
            {
                head = new GameObject("Head").transform;
                head.SetParent(drawn, false);
                faceOpen = own;
                face = HeadPart("Face", own, -0.002f, Color.white);
            }
            ApplyTint();
        }
        else if (character == "mascot")
        {
            idle = Resources.Load<Sprite>("Mascot/mascot_idle");
            panic = Resources.Load<Sprite>("Mascot/mascot_panic");
            poses = new[] { Resources.Load<Sprite>("Mascot/mascot_pose_1"), Resources.Load<Sprite>("Mascot/mascot_pose_2") };
            think = interact = fallDown = idle;
            cheer = new[] { idle, idle };
        }
        else
        {
            string path = $"Visitors/{character}/character_{character}_";
            idle = Resources.Load<Sprite>(path + "idle");
            think = Resources.Load<Sprite>(path + "think");
            interact = Resources.Load<Sprite>(path + "interact");
            fallDown = Resources.Load<Sprite>(path + "fallDown");
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
        if (panic != null) poses.Add(("panic", panic));

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
            r.transform.SetParent(drawn, false);
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
        head.SetParent(drawn, false);
        // In front of the body line (lower z is nearer the camera): face, then hair fills, then the hair line.
        if (faceIds.Count > 0)
        {
            string id = faceIds[Random.Range(0, faceIds.Count)];
            faceOpen = HeadArt(id + "_open") ?? HeadArt(id);
            faceClosed = HeadArt(id + "_closed");
            face = HeadPart("Face", faceOpen, -0.002f, Color.white);
            nextBlink = Time.time + Random.Range(1f, 4f);
            // A random panic face of their own, kept for good.
            panicFaces ??= Resources.LoadAll<Sprite>("ArtLineartHeadPanic");
            if (panicFaces.Length > 0) facePanic = panicFaces[Random.Range(0, panicFaces.Length)];
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
                // The ruckus face (red eyes and grin in one drawing, on the visitors' face canvas); else the spirit's own.
                var ruckus = Resources.Load<Sprite>("Ruckus/ruckus_center");
                if (ruckus != null)
                {
                    mcEyes = new[] { Resources.Load<Sprite>("Ruckus/ruckus_left"), ruckus, Resources.Load<Sprite>("Ruckus/ruckus_right"), ruckus };
                    mcMouth = null;
                }
                else
                {
                    var center = Resources.Load<Sprite>("MC/MC_face_eyes center");
                    mcEyes = new[] { Resources.Load<Sprite>("MC/MC_face_eyes left"), center, Resources.Load<Sprite>("MC/MC_face_eyes right"), center };
                    mcMouth = Resources.Load<Sprite>("MC/MC_face_mouth grin");
                }
            }
            if (mcEyes[1] == null) return;
            possessedFace = new GameObject("Possessed Face").AddComponent<SpriteRenderer>();
            possessedFace.transform.SetParent(head, false);
            possessedFace.sharedMaterial = ParkArt.FaceMaterial; // faces don't wobble, only bodies
            ParkArt.CastShadows(possessedFace);
            // Centre the spirit's drawing on the visitor's face drawing (both tight meshes), at a fixed width.
            Rect mine = DrawnRect(faceOpen), theirs = DrawnRect(mcEyes[1]);
            if (mcMouth != null)
            {
                Rect grin = DrawnRect(mcMouth);
                theirs = Rect.MinMaxRect(Mathf.Min(theirs.xMin, grin.xMin), Mathf.Min(theirs.yMin, grin.yMin),
                    Mathf.Max(theirs.xMax, grin.xMax), Mathf.Max(theirs.yMax, grin.yMax));
            }
            // Drawn on the same canvas as their face (the ruckus face on a visitor): it goes exactly where it was drawn.
            // Otherwise it's fitted over their face: a fixed width on visitors, a bit wider than a worker's own face.
            bool sameCanvas = faceOpen.rect.size == mcEyes[1].rect.size && Mathf.Approximately(faceOpen.pixelsPerUnit, mcEyes[1].pixelsPerUnit);
            float width = character.StartsWith("worker_") ? mine.width * 1.5f : PossessedFaceWidth / faceOpen.pixelsPerUnit;
            float scale = sameCanvas ? 1f : width / theirs.width;
            Vector2 at = sameCanvas ? Vector2.zero : mine.center - theirs.center * scale;
            possessedFace.transform.localPosition = new Vector3(at.x, at.y, -0.002f);
            possessedFace.transform.localScale = Vector3.one * scale;
            possessedFace.sprite = mcEyes[0];
            if (mcMouth != null)
            {
                possessedMouth = new GameObject("Possessed Mouth").AddComponent<SpriteRenderer>();
                possessedMouth.transform.SetParent(possessedFace.transform, false);
                possessedMouth.transform.localPosition = new Vector3(0f, 0f, -0.001f); // over the eyes
                possessedMouth.sprite = mcMouth;
                possessedMouth.sharedMaterial = ParkArt.FaceMaterial;
                ParkArt.CastShadows(possessedMouth);
            }
            ApplyTint();
        }
        possessed = on && possessedFace != null;
        eyeStep = 0;
        eyeUntil = Time.time + Random.Range(1.5f, 3f);
        if (possessedFace != null) possessedFace.sprite = mcEyes[0];
        SetVisible(body.enabled);
    }

    // A worker pulls a face (Resources/Workers/worker_face_<name>: surprised_1, surprised_2) for `seconds`; 0 = for good.
    public void Expression(string name, float seconds)
    {
        Load();
        if (face == null || !character.StartsWith("worker_")) return; // drawn on the workers' canvas only
        expression = Resources.Load<Sprite>("Workers/worker_face_" + name);
        expressionUntil = seconds > 0f ? Time.time + seconds : float.MaxValue;
    }

    // Drawn this far (world units) below where they stand: someone behind a stand's counter, only their top half
    // showing over it. 0 puts them back on their feet.
    public void Sink(float units)
    {
        Load();
        drawn.localPosition = new Vector3(0f, -units / Mathf.Max(0.01f, transform.lossyScale.y), 0f);
    }

    // Back to their own face (an expression held for good: Expression(name, 0)).
    public void ClearExpression() => expressionUntil = 0f;

    Sprite expression;
    float expressionUntil;

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
        r.sharedMaterial = ParkArt.FaceMaterial; // the head (face, hair) holds still: only the body wobbles
        ParkArt.CastShadows(r);
        headParts.Add((r, color));
        return r;
    }

    // Where a held item goes: the hand nearer the camera, which is the back hand as drawn (the lineart faces left, so
    // its right-hand one; Kenney art faces right, so its left-hand one). It's the outermost point of the idle frame's
    // tight mesh on that side between 20% and 50% of the way up (below the sleeves, above the feet), moved in by
    // HandInset. Poses that move the arms (cheering, falling, reaching) keep the idle frame's spot. Through the
    // transform, so it leans with the waddle.
    public Vector3 HandPoint
    {
        get
        {
            Load();
            return drawn.TransformPoint(HandAnchor(idle, drawnFacingLeft));
        }
    }

    // How far in from the hand's outer edge its middle is, as a fraction of the body's height (measured on the
    // body_standard lineart: hands are ~0.17 of the height wide from wrist to fingertips, their middle ~0.09 in, plus
    // the tight mesh's margin past the fingertips). 0.08 left held things out at the fingertips.
    const float HandInset = 0.12f;

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

    // Fills show the frame that matches whatever the line is showing; the face blinks.
    void SyncFills()
    {
        if (head != null)
        {
            if (faceClosed != null)
            {
                if (Time.time > nextBlink) { blinkUntil = Time.time + 0.12f; nextBlink = Time.time + Random.Range(2f, 5f); }
                face.sprite = Time.time < blinkUntil ? faceClosed : faceOpen;
            }
            // Scared: the panic face, no blinking.
            if (facePanic != null && (panicFace || panicBody)) face.sprite = facePanic;
            else if (facePanic != null && face.sprite == facePanic) face.sprite = faceOpen;
            if (expression != null)
            {
                face.sprite = Time.time < expressionUntil ? expression : faceOpen;
                if (Time.time >= expressionUntil) expression = null;
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
        if (possessedMouth != null) possessedMouth.enabled = visible && possessed;
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
        if (possessedMouth != null) Tint(possessedMouth, tint);
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

    // Electrocuted: flailing, flipping and jolting on the spot.
    public void Twitch(float seconds) => twitchUntil = Time.time + seconds;

    void LateUpdate()
    {
        Vector3 v = rb ? rb.linearVelocity : Vector3.zero;
        float speed = new Vector2(v.x, v.z).magnitude;
        bool moving = speed > 0.1f;
        if (Mathf.Abs(v.x) > 0.05f) facingLeft = v.x < 0;

        bool stepping = false;
        bool twitching = Time.time < twitchUntil;
        // Scared, the panic body stands in for the idle one (lineart cheering borrows idle too).
        Sprite standing = panicBody && panic != null ? panic : idle;
        if (twitching)
        {
            var jolt = (int)(Time.time * 14f) % 2 == 0 ? cheer[0] : idle;
            body.sprite = jolt == idle ? standing : jolt;
        }
        else if (Time.time < gestureUntil) body.sprite = gesture;
        else if (Time.time < cheerUntil) body.sprite = cheer[(int)(Time.time * 6f) % 2] == idle ? standing : cheer[(int)(Time.time * 6f) % 2];
        else if (pose != null) body.sprite = pose;
        else
        {
            body.sprite = standing;
            stepping = moving;
        }
        SyncFills();
        Stepping = stepping && body.enabled;

        // The waddle: they rock from foot to foot round their feet (the sprite's pivot), a step to each side per rock,
        // stretching up a touch as they pass upright and settling a touch at each lean. Quicker the faster they go and
        // wider when running; it eases in as they set off and back to standing straight when they stop.
        // Dancing on the spot, they rock as if walking at an easy pace (2 units/second) without going anywhere.
        bool dancing = dance && body.enabled;
        stride = Mathf.MoveTowards(stride, stepping || dancing ? 1f : 0f, Time.deltaTime * 5f);
        float pace = dancing && !moving ? 2f : speed;
        waddle = (waddle + Time.deltaTime * (waddleRate + pace * waddlePerSpeed) * Mathf.PI * 2f) % (Mathf.PI * 2f);
        float lean = Mathf.Sin(waddle) * waddleAngle * (running ? 1.4f : 1f) * stride;
        float lift = (Mathf.Abs(Mathf.Cos(waddle)) - 0.5f) * stepBounce * (running ? 1.5f : 1f) * stride;
        float sx = 1f - lift * 0.5f, sy = 1f + lift;
        if (twitching)
        {
            sx = Random.Range(0.85f, 1.15f);
            sy = Random.Range(0.85f, 1.15f);
            if (Random.value < 0.15f) facingLeft = !facingLeft;
        }
        else if (dozing && !moving) Doze(ref lean, ref sx, ref sy);
        if (Flat) Pancake(ref lean, ref sx, ref sy);
        if (Fallen) Topple(ref lean);
        // Facing is a negative x scale, not flipX: the lit shader draws flipped asymmetric sprites washed out.
        float flip = facingLeft != drawnFacingLeft ? -1f : 1f;
        drawn.localScale = new Vector3(flip * sx, sy, 1f);
        drawn.localRotation = Quaternion.Euler(0f, 0f, lean); // the shadow's on the root: it stays flat on the ground
        if (Flat) drawn.localRotation = Quaternion.Euler(flatTilt, 0f, 0f);
    }

    // Over they go: `quiet` (loading a save), already lying there.
    public void FallOver(bool quiet = false)
    {
        Load();
        Fallen = true;
        fallSince = quiet ? -10f : Time.time;
        fallSide = Random.value < 0.5f ? 1f : -1f;
        dance = running = dozing = false;
    }

    // Slowly at first, then faster (a stiff body tipping), 90 degrees over FallTime; then a small bounce off the ground.
    const float FallTime = 0.9f, BounceTime = 0.35f, BounceTilt = 9f;

    void Topple(ref float lean)
    {
        float t = Time.time - fallSince;
        float angle;
        if (t < FallTime)
        {
            float k = t / FallTime;
            angle = 90f * k * k;
        }
        else
        {
            float u = Mathf.Clamp01((t - FallTime) / BounceTime);
            angle = 90f - BounceTilt * Mathf.Sin(u * Mathf.PI) * (1f - u * 0.5f);
        }
        lean = fallSide * angle;
        // Turning round their feet would sink half of them under the ground: lifted by however far their side reaches
        // down as they turn, so they lie on top of it.
        float half = body.sprite != null ? body.sprite.bounds.extents.x * FlatWidthOnSide : 0f;
        drawn.localPosition = new Vector3(0f, Mathf.Sin(angle * Mathf.Deg2Rad) * half, 0f);
    }

    const float FlatWidthOnSide = 0.8f; // the drawn body is narrower than its picture (see-through round it)

    // Squashed flat: `quiet` (loading a save) skips the boing.
    public void Squash(bool quiet = false)
    {
        Load();
        Flat = true;
        flatSince = quiet ? -10f : Time.time;
        drawn.localPosition = new Vector3(0f, 0.02f / Mathf.Max(0.01f, transform.lossyScale.y), 0f); // just off the ground
        pose = null;
        panicBody = panicFace = true; // arms up, eyes wide: caught by surprise
        dance = running = dozing = false;
    }

    // Slammed down in a blink (SlamTime) to a squat FlatHeight of their height and FlatWidth as wide, still standing
    // (a cartoon squash), wobbling like jelly a moment before they settle.
    const float SlamTime = 0.15f, WobbleTime = 0.6f, FlatWidth = 1.45f, FlatHeight = 0.32f;

    void Pancake(ref float lean, ref float sx, ref float sy)
    {
        float t = Time.time - flatSince;
        lean = 0f;
        flatTilt = 0f; // still on their feet
        if (t < SlamTime)
        {
            float k = t / SlamTime;
            sy = Mathf.Lerp(1f, FlatHeight * 0.6f, k); // slammed a little past it...
            sx = Mathf.Lerp(1f, FlatWidth, k);
            return;
        }
        float w = Mathf.Clamp01((t - SlamTime) / WobbleTime);
        float jelly = Mathf.Sin(w * Mathf.PI * 4f) * (1f - w) * 0.25f; // ...and boinging back up to it
        sx = FlatWidth * (1f - jelly * 0.5f);
        sy = FlatHeight * (1f + jelly);
    }

    // Breathing in (taller and a touch narrower) and out over about 3.5 seconds; a slow sway; and every 6 to 10 seconds a
    // nod: drooping forward for a second and a half, then a quick jerk back upright.
    const float BreathTime = 3.5f, Breath = 0.035f, Sway = 2.5f, NodTilt = 9f, NodDroop = 1.5f, NodSnap = 0.25f;

    void Doze(ref float lean, ref float sx, ref float sy)
    {
        float t = Time.time;
        float breath = Mathf.Sin(t * Mathf.PI * 2f / BreathTime);
        sy *= 1f + breath * Breath;
        sx *= 1f - breath * Breath * 0.4f;
        lean += Mathf.Sin(t * 0.6f) * Sway;

        if (nodAt < 0f) nodAt = t + Random.Range(3f, 6f);
        float into = t - nodAt;
        if (into >= NodDroop + NodSnap) nodAt = t + Random.Range(6f, 10f);
        else if (into > 0f)
        {
            float k = into < NodDroop
                ? Mathf.SmoothStep(0f, 1f, into / NodDroop)       // drooping off...
                : 1f - (into - NodDroop) / NodSnap;               // ...and snapping back up
            float forward = facingLeft ? 1f : -1f;                // tipping toward the way they face
            lean += forward * NodTilt * k;
            sy *= 1f - 0.03f * k;                                 // and sinking a little
        }
    }
}
