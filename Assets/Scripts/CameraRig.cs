using UnityEngine;

// Tilted perspective camera that trails whoever you control (the spirit, or the visitor it's possessing)
// with a little look-ahead, and shakes on big hits. GameHud puts it on the main camera.
public class CameraRig : MonoBehaviour
{
    public static CameraRig Instance { get; private set; }

    public Vector3 offset = new(0f, 6f, -8f);
    public float smoothTime = 0.3f;
    public float lookAhead = 0.6f;

    Vector3 velocity;
    Vector3 lastTarget;
    Transform target;
    float shake;
    bool placed;

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
    }

    void LateUpdate()
    {
        var focus = Possession.Instance != null ? Possession.Instance.Focus : null;
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
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        }
        transform.rotation = Quaternion.LookRotation(-offset, Vector3.up);

        if (shake > 0f)
        {
            shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 1.5f);
            transform.position += (transform.right * Random.Range(-1f, 1f) + transform.up * Random.Range(-1f, 1f)) * shake * 0.5f;
        }
    }

    static Vector3 Ground(Vector3 p) => new(p.x, 0f, p.z);
}
