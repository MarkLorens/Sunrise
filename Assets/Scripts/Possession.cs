using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The player: an evil spirit floating around the park, possessing visitors.
//   Spirit:     WASD float - a weak spirit can't drift far from where it last left a body - E possess whoever you touch.
//   Possessed:  WASD walk, J grab (even out of someone's hands) - tap to whack, hold to charge a throw - K drop,
//               Space shout, E use things, Q leave the body.
// You get 20 s in a body and can't jump straight back into the same person. Anyone in range can be possessed.
[RequireComponent(typeof(GroundWalker))]
public class Possession : MonoBehaviour
{
    public enum Mode { Spirit, Diving, Possessing }

    public static Possession Instance { get; private set; }

    // Set while a menu or cutscene owns the keyboard.
    public static bool InputBlocked;

    public float touchRange = 1.2f;        // weak spirit: has to actually touch someone
    public float leashRadius = 9f;         // and can't drift far from the body it last left
    public float duration = 20f;           // max seconds inside one person
    public float sameTargetCooldown = 10f; // before the same person can be possessed again
    public float abilityCooldown = 2f;     // after leaving anyone
    public float grabRange = 1.8f;
    public float shoutRadius = 5f;

    public Mode State { get; private set; }
    public Visitor Host { get; private set; }
    public float TimeLeft { get; private set; }
    public float CooldownLeft => Mathf.Max(0f, readyAt - Time.time);
    public Vector3 LeashCenter { get; private set; }

    // Who you'd possess with E right now (may be someone you can't), and why not.
    public Transform Candidate { get; private set; }
    public string CandidateName { get; private set; }
    public string CandidateBlocked { get; private set; }
    public Item GrabCandidate { get; private set; }
    public bool Charging { get; private set; }
    public float Charge { get; private set; }
    public Vector3 AimDirection { get; private set; } = Vector3.right;
    public Visitor AimTarget { get; private set; }
    public float ShoutedAt { get; private set; } = float.NegativeInfinity;

    // What the camera follows.
    public Transform Focus => Host != null ? Host.transform : transform;
    // Where souls fly to.
    public Vector3 SpiritPoint => Host != null ? Host.HeadPoint : transform.position + Vector3.up * 0.5f;

    const float TapTime = 0.18f;

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

    void Awake()
    {
        Instance = this;
        InputBlocked = false;
        walker = GetComponent<GroundWalker>();
        rb = GetComponent<Rigidbody>();
        body = GetComponent<Collider>();
        sprite = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        LeashCenter = transform.position;
    }

    void Update()
    {
        var kb = Keyboard.current;
        bool live = !InputBlocked && kb != null;
        bool q = live && kb.qKey.wasPressedThisFrame;
        bool j = live && (kb.jKey.isPressed || (Mouse.current != null && Mouse.current.leftButton.isPressed));
        bool jDown = live && (kb.jKey.wasPressedThisFrame || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame));
        bool k = live && (kb.kKey.wasPressedThisFrame || (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame));
        bool space = live && kb.spaceKey.wasPressedThisFrame;

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
                Possessing(q, j, jDown, k, space);
                break;
        }
    }

    // A weak spirit can't drift far from the body it last left.
    void LateUpdate()
    {
        if (State == Mode.Possessing && Host != null)
        {
            // Ride along inside the host.
            Vector3 p = Host.transform.position;
            transform.position = new Vector3(p.x, transform.position.y, p.z);
            return;
        }
        if (State != Mode.Spirit || leashRadius <= 0f) return;
        Vector3 offset = rb.position - LeashCenter;
        offset.y = 0f;
        if (offset.magnitude <= leashRadius) return;
        Vector3 clamped = LeashCenter + offset.normalized * leashRadius;
        rb.position = transform.position = new Vector3(clamped.x, rb.position.y, clamped.z);
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
        TimeLeft = duration;
        sprite.enabled = false;
        Host.BeginPossession();
        Fx.Burst("Fx/sparkle", Host.HeadPoint, 18, Fx.Spirit, 3.5f, 0.45f, 0.6f);
        Fx.Shake(0.15f);
        GameHud.Say($"You possessed {Host.name}.", "UI/spirit");
        Mischief.Complete(Mischief.Task.Possess);
    }

    // ---------------------------------------------------------------- possessed

    void Possessing(bool q, bool j, bool jDown, bool k, bool space)
    {
        if (Host == null || Host.Current != Visitor.State.Possessed)
        {
            Release(); // knocked down, thrown into a brawl...
            return;
        }
        TimeLeft -= Time.deltaTime;
        if (TimeLeft <= 0f)
        {
            GameHud.Say($"Your grip slips. {Host.name} shakes you off!");
            Release();
            return;
        }
        if (q)
        {
            Release();
            return;
        }
        if (space) Shout();

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
        Charge = Mathf.Clamp01((chargeTime - TapTime) / 0.9f);
        Aim();
        if (j) return;
        Charging = false;
        if (chargeTime < TapTime) Whack();
        else Throw();
        Charge = 0f;
    }

    Item FindGrabbable()
    {
        Item best = null;
        float bestScore = float.MaxValue;
        foreach (var it in Item.All)
        {
            if (it.InFlight || it.Holder == Host || (it.Holder != null && !it.Holder.CanBeHit)) continue;
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

    void Grab(Item it)
    {
        var from = it.Holder;
        Host.GrabItem(it);
        if (from != null)
        {
            GameHud.Say($"You swiped {from.name}'s {it.DisplayName}!", "Bubbles/snack");
            Mischief.Complete(Mischief.Task.Swipe);
        }
        else if (it.Owner != null && it.Owner != Host) Mischief.Complete(Mischief.Task.Swipe);
    }

    void Aim()
    {
        AimDirection = Host.Facing;
        AimTarget = null;
        float best = 30f; // gentle aim assist toward someone roughly in front
        foreach (var v in Visitor.All)
        {
            if (v == Host || !v.CanBeHit) continue;
            Vector3 to = Flat(v.transform.position - Host.transform.position);
            float dist = to.magnitude;
            if (dist > 12f || dist < 0.5f) continue;
            float angle = Vector3.Angle(Host.Facing, to);
            if (angle < best)
            {
                best = angle;
                AimTarget = v;
                AimDirection = to / dist;
            }
        }
    }

    // How far a throw at this charge can reach a locked-on target.
    public static float MaxRange(float power) => 3f + 11f * power;

    public bool AimLocked => AimTarget != null && Host != null &&
                             Flat(AimTarget.transform.position - Host.transform.position).magnitude <= MaxRange(Charge);

    public Vector3 ThrowVelocity(float power)
    {
        float g = Item.Gravity;
        if (AimLocked)
        {
            // A lob that arrives at the target's chest: faster throws with more charge.
            Vector3 start = Host.CarryPoint;
            Vector3 goal = AimTarget.transform.position + Vector3.up * AimTarget.HeadHeight * 0.5f;
            Vector3 flat = Flat(goal - start);
            float dist = Mathf.Max(0.1f, flat.magnitude);
            float speed = 6f + 8f * power;
            float t = Mathf.Max(0.15f, dist / speed);
            float vy = (goal.y - start.y + 0.5f * g * t * t) / t;
            return flat / dist * speed + Vector3.up * vy;
        }
        return AimDirection * (5f + 9f * power) + Vector3.up * (3.5f + 2.5f * power);
    }

    // Where a throw released now would land.
    public Vector3 PredictLanding()
    {
        if (AimLocked) return new Vector3(AimTarget.transform.position.x, 0.03f, AimTarget.transform.position.z);
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
        AimTarget = null;
    }

    void Whack()
    {
        Host.Look.Gesture(Host.Look.interact, 0.25f);
        Sfx.Play(Sfx.HitSoft, 0.5f, 1.4f);
        foreach (var v in Visitor.All.ToArray())
        {
            if (v == Host || !v.CanBeHit) continue;
            Vector3 to = Flat(v.transform.position - Host.transform.position);
            if (to.magnitude < 1.6f && Vector3.Angle(Host.Facing, to) < 70f)
            {
                v.GetHit(Host, Host.Held.HitPower * 0.45f, to);
                Sfx.Play(Sfx.HitWood, 0.8f, 1.1f);
            }
        }
    }

    // Everyone nearby is startled: grown-ups get annoyed, kids get scared.
    void Shout()
    {
        ShoutedAt = Time.time;
        Host.Look.Cheer(0.7f);
        Host.Look.Show("Scream", 0.8f);
        Sfx.Play(Sfx.Huh, 0.9f, 0.6f);
        Fx.Burst("Fx/sparkle", Host.HeadPoint, 8, new Color(1f, 1f, 1f, 0.8f), 2.5f, 0.3f, 0.4f);
        ParkChaos.Add(Host.transform.position, shoutRadius, 0.5f, ParkChaos.Scary);
        foreach (var v in Visitor.All.ToArray())
        {
            if (v == Host || v.child) continue;
            if (Flat(v.transform.position - Host.transform.position).magnitude < shoutRadius) v.AddAnger(22f, Host);
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
        LeashCenter = new Vector3(at.x, 0f, at.z);
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
