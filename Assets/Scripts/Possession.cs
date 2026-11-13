using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The player: an evil spirit floating around the park, possessing visitors.
//   Spirit:     WASD float anywhere in the park - E possess whoever you touch.
//   Possessed:  WASD walk, E use things (or, as staff, tell a visitor to move along), Q leave the body.
// Anyone can be possessed, by touching them, for as long as you like. After leaving someone you have to wait 10 s
// before possessing the same person again; anyone else straight away. Whatever they hold is yours to use.
[RequireComponent(typeof(GroundWalker))]
public class Possession : MonoBehaviour
{
    public enum Mode { Spirit, Diving, Possessing }

    public static Possession Instance { get; private set; }

    // Set while a menu or cutscene owns the keyboard.
    public static bool InputBlocked;

    public float touchRange = 1.2f;        // weak spirit: has to actually touch someone
    public float sameTargetCooldown = 10f; // before the same person can be possessed again
    public float abilityCooldown = 0f;     // after leaving anyone (none: you can jump straight into someone else)

    public Mode State { get; private set; }
    public Visitor Host { get; private set; }
    public float CooldownLeft => Mathf.Max(0f, readyAt - Time.time);

    // Who you'd possess with E right now (may be someone you can't), and why not.
    public Transform Candidate { get; private set; }
    public string CandidateName { get; private set; }
    public string CandidateBlocked { get; private set; }

    // What the camera follows.
    public Transform Focus => Host != null ? Host.transform : transform;
    // Where souls fly to.
    public Vector3 SpiritPoint => Host != null ? Host.HeadPoint : transform.position + Vector3.up * 0.5f;

    readonly Dictionary<Visitor, float> lockedUntil = new();
    float readyAt;
    float diveTime;
    Vector3 diveFrom;
    Visitor diveTarget;
    Vector3 baseScale;
    RigidbodyInterpolation interpolation;

    GroundWalker walker;
    Rigidbody rb;
    Collider body;
    SpriteRenderer sprite;
    LoopingSound hover; // the spirit's wobbly drift: only while it moves (in a body, Footsteps takes over)

    void Awake()
    {
        Instance = this;
        InputBlocked = false;
        walker = GetComponent<GroundWalker>();
        rb = GetComponent<Rigidbody>();
        body = GetComponent<Collider>();
        sprite = GetComponent<SpriteRenderer>();
        sprite.sharedMaterial = ParkArt.CharacterMaterial; // the spirit is a person too: never see-through
        if (!TryGetComponent(out SpiritSprite _)) gameObject.AddComponent<SpiritSprite>(); // its body, eyes and hands
        baseScale = transform.localScale;
        hover = new LoopingSound(gameObject, rb);
    }

    void OnDestroy() => hover.Release();

    void Update()
    {
        var kb = Keyboard.current;
        bool live = !InputBlocked && kb != null;
        bool q = live && kb.qKey.wasPressedThisFrame;

        switch (State)
        {
            case Mode.Spirit:
                FindCandidate();
                if (q) TryPossess();
                break;
            case Mode.Diving:
                Dive();
                break;
            case Mode.Possessing:
                Possessing(q);
                break;
        }
        Hover();
    }

    void Hover()
    {
        var events = FMODEvents.instance;
        bool drifting = State == Mode.Spirit && !rb.isKinematic && Flat(rb.linearVelocity).sqrMagnitude > 0.04f;
        if (events != null && drifting) hover.Play(events.spiritHover);
        else hover.Stop();
    }

    // Ride along inside the host.
    void LateUpdate()
    {
        if (State != Mode.Possessing || Host == null) return;
        Vector3 p = Host.transform.position;
        transform.position = new Vector3(p.x, transform.position.y, p.z);
    }

    // ---------------------------------------------------------------- spirit

    void FindCandidate()
    {
        Candidate = null;
        CandidateName = CandidateBlocked = null;
        float best = touchRange;
        foreach (var v in Visitor.All)
        {
            if (v.Current is Visitor.State.Down or Visitor.State.Riding || !v.Look.Visible) continue;
            float d = Flat(v.transform.position - transform.position).magnitude;
            if (d > best) continue;
            best = d;
            Candidate = v.transform;
            CandidateName = v.name;
            CandidateBlocked = !v.CanBePossessed ? GameText.CantBePossessed(v.name)
                : IsLocked(v, out float left) ? GameText.StillShaken(v.name, Mathf.CeilToInt(left))
                : CooldownLeft > 0f ? GameText.TooWeak(Mathf.CeilToInt(CooldownLeft))
                : null;
        }
    }

    public Visitor Touching() => Candidate != null && CandidateBlocked == null ? Candidate.GetComponent<Visitor>() : null;

    bool IsLocked(Visitor v, out float left)
    {
        left = lockedUntil.TryGetValue(v, out float t) ? t - Time.time : 0f;
        return left > 0f;
    }

    // Dive into whoever the spirit is touching, if it can right now.
    public bool TryPossess()
    {
        if (State != Mode.Spirit) return false;
        FindCandidate();
        if (Candidate == null) return false;
        var v = Touching();
        if (v == null)
        {
            Sfx.Play(Sfx.Refuse, 0.5f);
            GameHud.Refuse(Candidate, CandidateBlocked, true);
            return true; // E was used up on trying
        }
        State = Mode.Diving;
        diveTarget = v;
        diveFrom = transform.position;
        diveTime = 0f;
        walker.enabled = false;
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;
        interpolation = rb.interpolation;
        rb.interpolation = RigidbodyInterpolation.None; // we place it by hand from here on
        body.enabled = false;
        Sfx.Play(Sfx.Possess, 0.7f);
        return true;
    }

    // Swoop into the host's head, spinning and shrinking.
    void Dive()
    {
        diveTime += Time.deltaTime / 0.35f;
        float t = Mathf.SmoothStep(0f, 1f, diveTime);
        transform.position = Vector3.Lerp(diveFrom, diveTarget.HeadPoint, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.2f;
        transform.localScale = baseScale * Mathf.Lerp(1f, 0.1f, t);
        transform.localRotation = Quaternion.Euler(0f, 0f, t * 540f);
        if (diveTime < 1f) return;

        transform.localScale = baseScale;
        transform.localRotation = Quaternion.identity;
        if (!diveTarget.CanBePossessed)
        {
            // They got away (onto a ride...).
            BackToSpirit(diveTarget.transform.position, abilityCooldown);
            return;
        }
        Host = diveTarget;
        State = Mode.Possessing;
        sprite.enabled = false;
        Host.BeginPossession();
        Fx.Burst("Fx/sparkle", Host.HeadPoint, 18, Fx.Spirit, 3.5f, 0.45f, 0.6f);
        Fx.Shake(0.2f, 0.3f); // a small knock that sways and settles, like a landed hit
        GameHud.Say(GameText.Possessed(Host.name));
        if (Host.brain == null) Mischief.Complete(Mischief.Task.Possess);
        else if (Host.brain is CableMan) Mischief.Complete(Mischief.Task.PossessCableMan);
        else if (Host.brain is Mechanic) Mischief.Complete(Mischief.Task.PossessMechanic);
    }

    // ---------------------------------------------------------------- possessed

    void Possessing(bool q)
    {
        if (Host == null || Host.Current != Visitor.State.Possessed)
        {
            Release(); // knocked down...
            return;
        }
        if (q)
        {
            Release();
            GameHud.Say(GameText.LeftBody);
        }
    }

    // ---------------------------------------------------------------- leaving

    public void Release()
    {
        if (State == Mode.Possessing) Leave(abilityCooldown);
    }

    void Leave(float cooldown)
    {
        var host = Host;
        Host = null;
        if (host == null)
        {
            BackToSpirit(transform.position, cooldown);
            return;
        }
        host.EndPossession();
        lockedUntil[host] = Time.time + sameTargetCooldown;
        BackToSpirit(host.transform.position, cooldown);
        Sfx.Play(Sfx.Leave, 0.6f);
        Fx.Burst("Fx/sparkle", host.HeadPoint, 14, Fx.Spirit, 3f, 0.4f, 0.5f);
    }

    void BackToSpirit(Vector3 at, float cooldown)
    {
        State = Mode.Spirit;
        readyAt = Time.time + cooldown;
        transform.position = new Vector3(at.x, transform.position.y, at.z);
        rb.isKinematic = false;
        rb.interpolation = interpolation;
        rb.linearVelocity = Vector3.zero;
        rb.position = transform.position;
        body.enabled = true;
        sprite.enabled = true;
        walker.enabled = true;
    }

    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

    // Is whoever you control (the spirit, or the body it's in) within `range` of p, along the floor?
    public static bool PlayerNear(Vector3 p, float range) =>
        Instance != null && Flat(Instance.Focus.position - p).magnitude <= range;
}
