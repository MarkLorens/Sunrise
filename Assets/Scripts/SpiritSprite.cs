using UnityEngine;

// The spirit's look, built from separate pieces of art in Resources/MC and put together exactly as in the artist's
// compile guide ("MC compile guide.PNG"):
//   Body:  MC_idle_1..4, looping (the flame on its head flickers).
//   Eyes:  MC_face_eyes left -> center -> right -> center -> left ..., on a timer like the visitors' blinking;
//          where it moves doesn't change where it looks.
//   Hands: a small up-and-down bob. The left hand sits behind the body, the right hand in front of it.
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
    public float handBobPixels = 8f;
    public float handBobSpeed = 4f;

    // Measured from the compile guide: each piece's centre in pixels from the centre of the guide, y up (the pieces
    // use the same 100 pixels per unit, so 100 px = 1 unit before the spirit's own scale). The hands are drawn
    // at 78% of their file's size. The guide is the same size as the old single picture, so the spirit stays put.
    static readonly Vector2 BodyAt = new(28.5f, 21f);
    static readonly Vector2 EyesAt = new(-17.5f, -68f);
    static readonly Vector2 HandLeftAt = new(-147.5f, -187f);
    static readonly Vector2 HandRightAt = new(105f, -186.5f);
    const float HandScale = 0.78f;
    const float PixelsPerUnit = 100f;
    // Front to back (lower z is nearer the camera): the lit shader writes depth, so pieces are spaced, not sorted.
    const float Layer = 0.02f;

    SpriteRenderer root, body, eyes, handLeft, handRight;
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
        handLeft = Part("Hand Left", Art("hand left"), HandLeftAt, Layer, HandScale);
        body = Part("Body", idle[0], BodyAt, 0f, 1f);
        eyes = Part("Eyes", eyeCycle[0], EyesAt, -Layer, 1f);
        handRight = Part("Hand Right", Art("hand right"), HandRightAt, -2f * Layer, HandScale);

        root.forceRenderingOff = true; // the pieces draw the spirit now
        eyeUntil = Time.time + Random.Range(eyeHold.x, eyeHold.y);
    }

    static Sprite Art(string name) => Resources.Load<Sprite>("MC/" + name);

    SpriteRenderer Part(string name, Sprite sprite, Vector2 at, float z, float scale)
    {
        var r = new GameObject(name).AddComponent<SpriteRenderer>();
        r.transform.SetParent(visual, false);
        r.transform.localPosition = new Vector3(at.x / PixelsPerUnit, at.y / PixelsPerUnit, z);
        r.transform.localScale = Vector3.one * scale;
        r.sprite = sprite;
        r.sharedMaterial = ParkArt.CharacterMaterial; // a person too: never see-through
        ParkArt.CastShadows(r);
        return r;
    }

    void LateUpdate()
    {
        bool shown = root.enabled;
        if (body.enabled != shown) body.enabled = eyes.enabled = handLeft.enabled = handRight.enabled = shown;
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

        // A small bob, the right hand a touch behind the left.
        float bob = handBobPixels / PixelsPerUnit;
        Bob(handLeft, HandLeftAt, bob * Mathf.Sin(Time.time * handBobSpeed));
        Bob(handRight, HandRightAt, bob * Mathf.Sin(Time.time * handBobSpeed - 0.8f));
    }

    static void Bob(SpriteRenderer hand, Vector2 at, float offset)
    {
        var p = hand.transform.localPosition;
        p.y = at.y / PixelsPerUnit + offset;
        hand.transform.localPosition = p;
    }
}
