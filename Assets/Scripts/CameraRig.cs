using UnityEngine;
using UnityEngine.InputSystem;

// Tilted perspective camera that trails whoever you control (the spirit, or the visitor it's possessing)
// with a little look-ahead, and shakes on big hits. GameHud puts it on the main camera.
// While the host rides the Ferris wheel it moves into their cabin and looks out over the park, rising and falling
// with the wheel; WASD turns the rider's head.
public class CameraRig : MonoBehaviour
{
    public static CameraRig Instance { get; private set; }

    public Vector3 offset = new(0f, 6f, -8f);
    public float smoothTime = 0.3f;
    public float lookAhead = 0.6f;

    [Header("Riding")]
    public float rideFov = 85f;
    public float rideTilt = 25f;     // degrees looking down when you get in
    public float lookYaw = 100f;     // how far you can turn your head either way
    public Vector2 lookPitch = new(-5f, 70f);
    public float lookSpeed = 80f;    // degrees per second
    public Vector3 seat = new(0f, -0.2f, -2f);   // eye position from the cabin's hook: a step back toward the park, for a wider view

    Vector3 velocity;
    Vector3 lastTarget;
    Transform target;
    float shake;
    bool placed;

    Camera cam;
    float baseFov;
    Transform cabin;
    Vector2 look;    // yaw, pitch
    float inCabin;   // 0 = following on the ground, 1 = settled in the cabin

    // Adds screen shake (0.1 = light hit, 0.3 = big impact). Decays on its own.
    public static void Shake(float amount)
    {
        if (Instance != null) Instance.shake = Mathf.Min(Instance.shake + amount, 0.5f);
    }

    void Awake()
    {
        Instance = this;
        // The scene has the camera riding on the spirit; it follows by itself now.
        transform.SetParent(null, true);
        transform.localScale = Vector3.one;
        cam = GetComponent<Camera>();
        baseFov = cam.fieldOfView;
    }

    void LateUpdate()
    {
        var spirit = Possession.Instance;
        var ride = spirit != null && spirit.Riding ? spirit.Host.Cabin : null;
        if (ride != cabin)
        {
            cabin = ride;
            look = new Vector2(0f, rideTilt);
            velocity = Vector3.zero;
            if (cabin == null) target = null; // back on the ground: pick the follow up afresh
        }
        if (cabin != null) Ride();
        else Follow(spirit != null ? spirit.Focus : null);
        cam.fieldOfView = Mathf.Lerp(baseFov, rideFov, Mathf.SmoothStep(0f, 1f, inCabin));

        if (shake > 0f)
        {
            shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 1.5f);
            transform.position += (transform.right * Random.Range(-1f, 1f) + transform.up * Random.Range(-1f, 1f)) * shake * 0.5f;
        }
    }

    void Follow(Transform focus)
    {
        float dt = Time.unscaledDeltaTime;
        inCabin = Mathf.MoveTowards(inCabin, 0f, dt / 0.8f);
        if (focus != target)
        {
            target = focus;
            if (target != null) lastTarget = Ground(target.position);
            if (target != null && !placed) transform.position = lastTarget + offset; // start right there
            placed |= target != null;
        }
        if (target != null)
        {
            Vector3 p = Ground(target.position);
            Vector3 move = (p - lastTarget) / Mathf.Max(Time.deltaTime, 0.0001f);
            lastTarget = p;
            Vector3 goal = p + offset + Vector3.ClampMagnitude(move, 5f) * (lookAhead / 5f) * 2f;
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, Mathf.Lerp(smoothTime, 0.6f, inCabin), Mathf.Infinity, dt);
        }
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(-offset, Vector3.up), 1f - Mathf.Exp(-6f * dt));
    }

    // In the cabin, facing out over the park (the wheel stands at the back of it).
    void Ride()
    {
        float dt = Time.unscaledDeltaTime;
        inCabin = Mathf.MoveTowards(inCabin, 1f, dt / 1.2f);

        var kb = Keyboard.current;
        if (!Possession.InputBlocked && kb != null)
        {
            float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
            look.x = Mathf.Clamp(look.x + x * lookSpeed * dt, -lookYaw, lookYaw);
            look.y = Mathf.Clamp(look.y - y * lookSpeed * dt, lookPitch.x, lookPitch.y);
        }

        // Swoop in from the follow camera, then sit still in the seat.
        Vector3 eye = cabin.position + seat * cabin.lossyScale.y;
        transform.position = Vector3.SmoothDamp(transform.position, eye, ref velocity, Mathf.Lerp(0.5f, 0.02f, inCabin), Mathf.Infinity, dt);
        Quaternion view = Quaternion.Euler(look.y, 180f + look.x, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, view, 1f - Mathf.Exp(-Mathf.Lerp(3f, 30f, inCabin) * dt));
    }

    static Vector3 Ground(Vector3 p) => new(p.x, 0f, p.z);
}
