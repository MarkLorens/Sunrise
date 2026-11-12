using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The player: an evil spirit floating around the park, possessing visitors.
//   Spirit:     WASD float anywhere in the park - E possess whoever you touch.
//   Possessed:  WASD walk, E use things (or pick a fight with someone), J pick up your own things - hold J to charge
//               a throw, release to throw - K drop, Q leave the body.
//   Riding:     on the Ferris wheel the camera moves into the cabin (CameraRig) - WASD look around, Q leave the body.
// Anyone can be possessed, by touching them, for as long as you like. After leaving someone you have to wait 10 s
// before possessing the same person again; anyone else straight away. Whatever they hold or own is yours to use.
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
    public float grabRange = 1.8f;

    public Mode State { get; private set; }
    public Visitor Host { get; private set; }
    public float CooldownLeft => Mathf.Max(0f, readyAt - Time.time);

    // Who you'd possess with E right now (may be someone you can't), and why not.
    public Transform Candidate { get; private set; }
    public string CandidateName { get; private set; }
    public string CandidateBlocked { get; private set; }
    public Item GrabCandidate { get; private set; }
    public bool Charging { get; private set; }
    public float Charge { get; private set; }
    public Vector3 AimDirection { get; private set; } = Vector3.right;
    // Sitting in a cabin on the Ferris wheel.
    public bool Riding => Host != null && Host.Current == Visitor.State.Riding;

    // What the camera follows.
    public Transform Focus => Host != null ? Host.transform : transform;
    // Where souls fly to.
    public Vector3 SpiritPoint => Host != null ? Host.HeadPoint : transform.position + Vector3.up * 0.5f;

    readonly Dictionary<Visitor, float> lockedUntil = new();
    float readyAt;
    float chargeTime;
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
        baseScale = transform.localScale;
        hover = new LoopingSound(gameObject, rb);
    }

    void OnDestroy() => hover.Release();

    void Update()
    {
        var kb = Keyboard.current;
        bool live = !InputBlocked && kb != null;
        bool q = live && kb.qKey.wasPressedThisFrame;
        bool j = live && (kb.jKey.isPressed || (Mouse.current != null && Mouse.current.leftButton.isPressed));
        bool jDown = live && (kb.jKey.wasPressedThisFrame || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame));
        bool k = live && (kb.kKey.wasPressedThisFrame || (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame));

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
                Possessing(q, j, jDown, k);
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
            if (v.Current is Visitor.State.Down or Visitor.State.Riding or Visitor.State.Fighting || !v.Look.Visible) continue;
            float d = Flat(v.transform.position - transform.position).magnitude;
            if (d > best) continue;
            best = d;
            Candidate = v.transform;
            CandidateName = v.name;
            CandidateBlocked = !v.CanBePossessed ? $"{v.name} can't be possessed"
                : IsLocked(v, out float left) ? $"{v.name} is still shaken ({Mathf.CeilToInt(left)}s)"
                : CooldownLeft > 0f ? $"Too weak to possess anyone ({Mathf.CeilToInt(CooldownLeft)}s)"
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
            GameHud.Say(CandidateBlocked + ".");
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
            // They got away (onto a ride, into a brawl...).
            BackToSpirit(diveTarget.transform.position, abilityCooldown);
            return;
        }
        Host = diveTarget;
        State = Mode.Possessing;
        sprite.enabled = false;
        Host.BeginPossession();
        Fx.Burst("Fx/sparkle", Host.HeadPoint, 18, Fx.Spirit, 3.5f, 0.45f, 0.6f);
        Fx.Shake(0.2f, 0.3f); // a small knock that sways and settles, like a landed hit
        GameHud.Say($"You possessed {Host.name}.", "UI/spirit");
        if (Host.brain == null) Mischief.Complete(Mischief.Task.Possess);
        else if (Host.brain is CableMan) Mischief.Complete(Mischief.Task.PossessCableMan);
        else if (Host.brain is Mechanic) Mischief.Complete(Mischief.Task.PossessMechanic);
    }

    // ---------------------------------------------------------------- possessed

    void Possessing(bool q, bool j, bool jDown, bool k)
    {
        if (Host == null || Host.Current is not (Visitor.State.Possessed or Visitor.State.Riding))
        {
            Release(); // knocked down, thrown into a brawl...
            return;
        }
        if (q)
        {
            Release();
            return;
        }
        if (Riding)
        {
            Charging = false;
            GrabCandidate = null;
            return;
        }
        if (Host.Held == null)
        {
            Charging = false;
            GrabCandidate = FindGrabbable();
            if (jDown && GrabCandidate != null) Grab(GrabCandidate);
            return;
        }

        GrabCandidate = null;
        if (k)
        {
            Charging = false;
            Host.DropHeld();
            return;
        }
        if (jDown)
        {
            Charging = true;
            chargeTime = 0f;
        }
        if (!Charging) return;
        chargeTime += Time.deltaTime;
        Charge = Mathf.Clamp01(chargeTime / 0.9f);
        Aim();
        if (j) return;
        Charging = false;
        Throw();
        Charge = 0f;
    }

    Item FindGrabbable()
    {
        Item best = null;
        float bestScore = float.MaxValue;
        foreach (var it in Item.All)
        {
            // Only your own things, lying around: whatever someone else holds or owns isn't yours to use.
            if (it.InFlight || it.Holder != null || (it.Owner != null && it.Owner != Host)) continue;
            Vector3 to = Flat(it.transform.position - Host.transform.position);
            float dist = to.magnitude;
            if (dist > grabRange) continue;
            float score = dist + Vector3.Angle(Host.Facing, to) / 180f; // prefer things in front
            if (score < bestScore)
            {
                bestScore = score;
                best = it;
            }
        }
        return best;
    }

    void Grab(Item it) => Host.GrabItem(it);

    void Aim() => AimDirection = Host.Facing;

    public Vector3 ThrowVelocity(float power) =>
        AimDirection * (5f + 9f * power) + Vector3.up * (3.5f + 2.5f * power);

    // Where a throw released now would land.
    public Vector3 PredictLanding()
    {
        Vector3 start = Host.CarryPoint;
        Vector3 v = ThrowVelocity(Charge);
        float g = Item.Gravity;
        float time = (v.y + Mathf.Sqrt(v.y * v.y + 2f * g * start.y)) / g;
        Vector3 land = start + new Vector3(v.x, 0f, v.z) * time;
        land.y = 0.03f;
        return land;
    }

    void Throw()
    {
        Host.ThrowHeld(ThrowVelocity(Charge));
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
        Charging = false;
        Charge = 0f;
        GrabCandidate = null;
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
}
