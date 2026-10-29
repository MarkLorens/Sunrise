using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The spirit's one ability: touch a visitor and press Q to take them over.
// While possessing, the spirit hides inside the host (so the camera and E-interactions follow the host)
// until time runs out or Q is pressed again.
[RequireComponent(typeof(GroundWalker))]
public class Possession : MonoBehaviour
{
    public float touchRange = 1f;          // weak spirit: has to actually touch someone
    public float duration = 20f;           // max seconds inside one person
    public float sameTargetCooldown = 10f; // before the same person can be possessed again
    public float abilityCooldown = 2f;     // after leaving anyone

    public Visitor Host { get; private set; }
    public float TimeLeft { get; private set; }
    public float CooldownLeft => Mathf.Max(0f, readyAt - Time.time);

    readonly Dictionary<Visitor, float> lockedUntil = new();
    float readyAt;
    RigidbodyInterpolation interpolation;

    GroundWalker walker;
    Rigidbody rb;
    Collider body;
    SpriteRenderer sprite;

    void Awake()
    {
        walker = GetComponent<GroundWalker>();
        rb = GetComponent<Rigidbody>();
        body = GetComponent<Collider>();
        sprite = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        var kb = Keyboard.current;
        bool pressed = kb != null && kb.qKey.wasPressedThisFrame;

        if (Host != null)
        {
            TimeLeft -= Time.deltaTime;
            if (pressed || TimeLeft <= 0f || Host.Current == Visitor.State.Down) Release();
        }
        else if (pressed && Time.time >= readyAt)
        {
            var v = Touching();
            if (v != null) Possess(v);
        }
    }

    // After the host has moved this frame, ride along so the camera (a child of the spirit) follows.
    void LateUpdate()
    {
        if (Host == null) return;
        Vector3 p = Host.transform.position;
        transform.position = new Vector3(p.x, transform.position.y, p.z);
    }

    public Visitor Touching()
    {
        Visitor best = null;
        float bestDist = touchRange;
        foreach (var v in Visitor.All)
        {
            if (!v.CanBePossessed || IsLocked(v)) continue;
            Vector3 d = v.transform.position - transform.position;
            d.y = 0;
            if (d.magnitude <= bestDist)
            {
                best = v;
                bestDist = d.magnitude;
            }
        }
        return best;
    }

    bool IsLocked(Visitor v) => lockedUntil.TryGetValue(v, out float t) && Time.time < t;

    void Possess(Visitor v)
    {
        Host = v;
        TimeLeft = duration;
        v.BeginPossession();

        walker.enabled = false;
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;
        interpolation = rb.interpolation;
        rb.interpolation = RigidbodyInterpolation.None; // we place it by hand in LateUpdate
        body.enabled = false;
        sprite.enabled = false;
    }

    public void Release()
    {
        if (Host == null) return;
        Host.EndPossession();
        lockedUntil[Host] = Time.time + sameTargetCooldown;
        readyAt = Time.time + abilityCooldown;
        Host = null;

        rb.isKinematic = false;
        rb.interpolation = interpolation;
        rb.linearVelocity = Vector3.zero;
        body.enabled = true;
        sprite.enabled = true;
        walker.enabled = true;
    }
}
