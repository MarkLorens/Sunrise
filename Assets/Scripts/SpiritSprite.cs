using UnityEngine;

// The spirit's look, built from separate pieces of art in Resources/MC and put together exactly as in the artist's
// compile guide ("MC compile guide.PNG"). Every piece is drawn on the guide's full canvas, already in its place, so
// they're simply stacked on one spot (back to front: body, eyes, mouth, hands):
//   Body:  MC_idle_1..4, looping (the flame on its head flickers).
//   Eyes:  MC_face_eyes left -> center -> right -> center -> left ..., on a timer like the visitors' blinking;
//          where it moves doesn't change where it looks.
//   Mouth: MC_face_mouth grin, always the same.
//   Hands: hands (both hands in one picture).
//   Bob:   body (with its face) and the hands float up and down together, in step.
//   Pose:  on top of the guide, the body leans forward (bodyAngle) and the face and hands are nudged to sit on it.
// Like the possessed visitors it turns to face the way it floats: the art faces left, so moving right mirrors it.
// The pieces hang off a "Visual" child, so mirroring never touches the spirit's own collider (or the camera). They
// show and hide with the spirit's own SpriteRenderer (Possession hides that while you're inside someone), which no
// longer draws anything itself: its sprite only stands in for the spirit's size (SeeThrough reads its bounds).
// Possession adds this to the spirit.
[RequireComponent(typeof(SpriteRenderer))]
public class SpiritSprite : MonoBehaviour
{
    public float idleFps = 8f;
    [Tooltip("Seconds it looks left, or right, before glancing the other way (random in this range).")]
    public Vector2 eyeHold = new(1.5f, 3f);
    [Tooltip("Seconds on the centre frame while the eyes swing across.")]
    public float eyeTransition = 0.15f;
    [Tooltip("How far the hands bob up and down, in pixels of the drawing.")]
    public float handBobPixels = 16f;
    [Tooltip("How far the body (and its eyes) bobs up and down, in pixels of the drawing.")]
    public float bodyBobPixels = 8f;
    [Tooltip("How fast everything bobs (radians per second: one full bob takes 2\u03c0 / speed seconds).")]
    public float handBobSpeed = 4f;

    [Header("Pose (as drawn, facing left; mirrored with it)")]
    [Tooltip("The body's lean in degrees: negative tips it forward, so the flame streams out behind.")]
    public float bodyAngle = -25f;
    [Tooltip("Where the mouth sits on the leaning body, in units of the drawing (1 = 100 px).")]
    public Vector2 faceOffset = new(-0.15f, 0.35f);
    [Tooltip("Where the eyes sit on the leaning body, in units of the drawing (1 = 100 px). Further forward than the " +
             "mouth, so the front eye pokes just past the face's outline.")]
    public Vector2 eyesOffset = new(-0.38f, 0.35f);
    [Tooltip("Where both hands sit, in units of the drawing (1 = 100 px).")]
    public Vector2 handsOffset = new(-0.5f, -0.1f);
    [Tooltip("The hands' tilt in degrees.")]
    public float handsAngle = -8f;

    // The pieces use 100 pixels per unit (100 px = 1 unit before the spirit's own scale); all sit centred on the spirit.
    const float PixelsPerUnit = 100f;
    // Front to back: the lit shader writes depth, so the pieces are spaced, not sorted. Each one sits Layer world units
    // nearer the camera than the one behind, straight along its view, so the gap doesn't nudge the face across the
    // screen. It has to beat the body's doodle wobble (which can bring the body ~0.6 x its strength nearer at a
    // pixel), or the body pokes through the face and the face flickers away.
    const float Layer = 0.03f;

    SpriteRenderer root, body, eyes, mouth, hands;
    Transform visual;
    Sprite[] idle;
    Sprite[] eyeCycle; // left, center, right, center
    int eyeStep;
    float eyeUntil;
    bool facingLeft = true; // as drawn
    Rigidbody rb;

    void Awake()
    {
        root = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody>();
        idle = new Sprite[4];
        for (int i = 0; i < idle.Length; i++) idle[i] = Art("MC_idle_" + (i + 1));
        var center = Art("MC_face_eyes center");
        eyeCycle = new[] { Art("MC_face_eyes left"), center, Art("MC_face_eyes right"), center };
        if (idle[0] == null || eyeCycle[0] == null)
        {
            Debug.LogWarning("SpiritSprite: art missing from Resources/MC, keeping the old picture.");
            enabled = false;
            return;
        }

        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);
        body = Part("Body", idle[0], ParkArt.SpiritMaterial);
        eyes = Part("Eyes", eyeCycle[0], ParkArt.FaceMaterial); // the face holds still: no doodle wobble
        mouth = Part("Mouth", Art("MC_face_mouth grin"), ParkArt.FaceMaterial);
        hands = Part("Hands", Art("hands"), ParkArt.SpiritMaterial);

        root.forceRenderingOff = true; // the pieces draw the spirit now
        eyeUntil = Time.time + Random.Range(eyeHold.x, eyeHold.y);
    }

    static Sprite Art(string name) => Resources.Load<Sprite>("MC/" + name);

    // The middle of the body as it's drawn (its picture's tight outline, leaning and bobbing): where the see-through
    // hole goes (SeeThrough). Not the spirit's own SpriteRenderer, which doesn't draw and may have no picture.
    public bool HasBody => enabled && body != null;
    public Vector3 Middle => body.bounds.center;

    SpriteRenderer Part(string name, Sprite sprite, Material material)
    {
        var r = new GameObject(name).AddComponent<SpriteRenderer>();
        r.transform.SetParent(visual, false);
        r.sprite = sprite;
        r.sharedMaterial = material; // a person's, never see-through (the spirit's own copies, for its doodle wobble)
        ParkArt.CastShadows(r);
        return r;
    }

    void LateUpdate()
    {
        bool shown = root.enabled;
        if (body.enabled != shown) body.enabled = eyes.enabled = mouth.enabled = hands.enabled = shown;
        if (!shown) return;

        // Face the way it floats (keeps the last way while standing still).
        float vx = rb != null && !rb.isKinematic ? rb.linearVelocity.x : 0f;
        if (Mathf.Abs(vx) > 0.05f) facingLeft = vx < 0f;
        visual.localScale = new Vector3(facingLeft ? 1f : -1f, 1f, 1f);

        body.sprite = idle[(int)(Time.time * idleFps) % idle.Length];

        // left -> center -> right -> center -> left ...
        if (Time.time >= eyeUntil)
        {
            eyeStep = (eyeStep + 1) % eyeCycle.Length;
            eyeUntil = Time.time + (eyeStep % 2 == 1 ? eyeTransition : Random.Range(eyeHold.x, eyeHold.y));
            eyes.sprite = eyeCycle[eyeStep];
        }

        // One step nearer the camera, in the pieces' own space (so the spirit's scale and flip are taken care of).
        var cam = Camera.main;
        Vector3 step = cam != null ? visual.InverseTransformVector(-cam.transform.forward * Layer) : Vector3.zero;

        // Everything bobs together on one wave: the hands by handBobPixels, the body and its face by bodyBobPixels.
        float wave = Mathf.Sin(Time.time * handBobSpeed) / PixelsPerUnit;
        Place(body, 0, bodyBobPixels * wave, step, Vector2.zero, bodyAngle);
        Place(eyes, 1, bodyBobPixels * wave, step, eyesOffset, 0f);
        Place(mouth, 2, bodyBobPixels * wave, step, faceOffset, 0f);
        Place(hands, 3, handBobPixels * wave, step, handsOffset, handsAngle);
    }

    static void Place(SpriteRenderer part, int layer, float bob, Vector3 step, Vector2 offset, float angle)
    {
        part.transform.localPosition = step * layer + new Vector3(offset.x, offset.y + bob, 0f);
        part.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}
