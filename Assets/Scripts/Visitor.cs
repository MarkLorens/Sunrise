using System.Collections.Generic;
using UnityEngine;

public enum VisitorItem { None, SpicyHotdog, ToiletKey, Drink, Screwdriver }

// A park visitor going about their day: wandering, buying snacks and drinks at the stalls, lining up for the Ferris
// wheel, carrying their things over their head. Kids stick close to their grown-up (even a possessed one).
// Like the townsfolk in Untitled Goose Game they react to you: they stare at someone acting possessed, chase down
// whoever swipes their things, and get angrier each time they're hit or shouted at. Once furious they charge at
// whoever did it and start a brawl that one of them won't walk away from. When something terrible happens nearby
// they scream and run (ParkChaos). When the spirit leaves they stand around confused for a few seconds.
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public class Visitor : MonoBehaviour
{
    // Every active visitor, so the spirit can find who it's touching without physics queries.
    public static readonly List<Visitor> All = new();

    public enum State { Idle, Wander, Queueing, Riding, Possessed, Confused, Panic, Down, Follow, Stare, Angry, Retrieving, Fighting }

    public bool possessable = true;
    public bool child;
    // Staff (the ride operator) have their own AI. It's switched off while the spirit is inside, and the usual
    // wandering, anger and panic below are skipped for them.
    public MonoBehaviour brain;
    public float wanderSpeed = 2f;
    public float possessedSpeed = 3.5f;
    public float panicSpeed = 4.5f;
    public float wanderRadius = 6f;
    public Vector2 idleTime = new(1f, 4f);
    public float confusedTime = 3f;
    [Range(0, 1)] public float queueChance = 0.15f; // per stop, chance to go line up for a ride
    [Range(0, 1)] public float shopChance = 0.35f;  // per stop with empty hands, chance to go buy something
    public Color possessedTint = new(0.75f, 0.55f, 1f);
    public float strength = 3f; // stronger visitors usually win brawls

    // Places people like to hang around (benches, views). Empty = just roam.
    public Transform[] pointsOfInterest = new Transform[0];
    [Range(0, 1)] public float pointOfInterestChance = 0.3f;

    // Kids: the grown-up they came with. Null = a grown-up (or a kid who lost theirs).
    public Visitor parent;
    public float followDistance = 1.2f;
    public float stareRadius = 3f;
    public float stareTime = 1.5f;

    public State Current { get; private set; }
    public bool CanBePossessed => possessable && Look.Visible && Current is not (State.Possessed or State.Riding or State.Down or State.Fighting);
    public bool CanBeHit => isActiveAndEnabled && Current is not (State.Riding or State.Down or State.Fighting);
    public CharacterSprite Look { get; private set; }
    public Item Held { get; private set; }
    public VisitorItem HeldKind => Held != null ? Held.kind : VisitorItem.None;
    public float Anger { get; private set; }
    public Visitor AngryAt { get; private set; }
    public Vector3 Facing { get; private set; } = Vector3.right;
    public float HeadHeight => Look.HeadHeight;
    public Vector3 HeadPoint => transform.position + Vector3.up * HeadHeight;
    public Vector3 CarryPoint => transform.position + Vector3.up * (HeadHeight + 0.3f);

    GroundWalker walker;
    Rigidbody rb;
    RideQueue queue;
    WorldBar angerBar;
    Item wanted;          // our things someone took
    Visitor thief;
    Vector3 target;
    Vector3 panicFrom;
    Vector3 followOffset;
    Vector3 knock;
    float knockUntil;
    float stateEndsAt;
    float nextTurn;
    float nextStare;

    void Awake()
    {
        walker = GetComponent<GroundWalker>();
        rb = GetComponent<Rigidbody>();
        Look = GetComponent<CharacterSprite>();
        Look.Load();
        walker.readKeyboard = false;
        followOffset = new Vector3(Random.Range(0.5f, 0.9f) * (Random.value < 0.5f ? -1 : 1), 0f, Random.Range(-0.4f, 0.2f));
        angerBar = WorldBar.Create(transform, new Vector3(0f, Look.idle.bounds.size.y + 0.2f, -0.02f), 0.7f);
        Idle();
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void FixedUpdate()
    {
        // Knocked back by a hit: slide for a moment, then walk again.
        bool knocked = Time.time < knockUntil;
        if (walker.enabled == knocked) walker.enabled = !knocked;
        if (!knocked) return;
        knock = Vector3.MoveTowards(knock, Vector3.zero, 25f * Time.fixedDeltaTime);
        Vector3 next = rb.position + knock * Time.fixedDeltaTime;
        rb.linearVelocity = walker.IsWalkable(next) ? knock : Vector3.zero;
    }

    void Update()
    {
        Vector3 v = rb.linearVelocity;
        if (new Vector2(v.x, v.z).sqrMagnitude > 0.04f) Facing = new Vector3(v.x, 0f, v.z).normalized;
        if (brain != null && Current != State.Possessed) return;

        if (Current is not State.Angry) Anger = Mathf.Max(0f, Anger - 7f * Time.deltaTime);
        angerBar.Set(Anger / 100f, WorldBar.Heat(Anger / 100f), Anger > 1f && Look.Visible && Current is not (State.Possessed or State.Down or State.Fighting));

        if (Current is State.Idle or State.Wander or State.Follow or State.Stare or State.Confused)
        {
            if (ParkChaos.Threat(transform.position, child, out Vector3 horror))
            {
                Panic(horror, Random.Range(3f, 5f));
                return;
            }
            if (Current != State.Confused && LookForPossessed()) return;
        }

        switch (Current)
        {
            case State.Idle:
                if (Time.time >= stateEndsAt) Decide();
                break;

            case State.Wander:
                // Arrived, or stuck on an edge for too long: take a break and pick somewhere else.
                if (!WalkTo(target))
                {
                    TryShop();
                    Idle();
                }
                else if (Time.time >= stateEndsAt) Idle();
                break;

            case State.Queueing:
                WalkTo(queue.SlotPosition(this));
                break;

            case State.Follow:
                FollowParent();
                break;

            case State.Stare:
            case State.Confused:
                if (Time.time >= stateEndsAt) Idle();
                break;

            case State.Panic:
                if (Time.time >= stateEndsAt) Idle();
                else if (Time.time >= nextTurn) RunAway();
                break;

            case State.Angry:
                ChargeAtCulprit();
                break;

            case State.Retrieving:
                Retrieve();
                break;
        }
    }

    // Steers toward a point; returns false once there.
    bool WalkTo(Vector3 p)
    {
        Vector3 to = p - transform.position;
        to.y = 0;
        if (to.magnitude < 0.3f)
        {
            walker.move = Vector2.zero;
            return false;
        }
        walker.move = new Vector2(to.x, to.z).normalized;
        return true;
    }

    float Distance(Component other)
    {
        Vector3 d = other.transform.position - transform.position;
        d.y = 0;
        return d.magnitude;
    }

    // ---------------------------------------------------------------- going about their day

    void Decide()
    {
        if (parent != null)
        {
            SetState(State.Follow);
            return;
        }
        var q = RideQueue.All.Count > 0 ? RideQueue.All[Random.Range(0, RideQueue.All.Count)] : null;
        if (q != null && Random.value < queueChance && q.Join(this))
        {
            EnterQueue(q);
            return;
        }
        if (Held == null && Stall.All.Count > 0 && Random.value < shopChance)
        {
            var stall = Stall.All[Random.Range(0, Stall.All.Count)];
            if (Head(stall.ServePoint + new Vector3(Random.Range(-0.4f, 0.4f), 0f, 0f))) return;
        }
        PickTarget();
    }

    void Idle()
    {
        SetState(State.Idle);
        walker.move = Vector2.zero;
        stateEndsAt = Time.time + Random.Range(idleTime.x, idleTime.y);
    }

    void PickTarget()
    {
        bool toPoi = pointsOfInterest.Length > 0 && Random.value < pointOfInterestChance;
        Vector3 center = toPoi ? pointsOfInterest[Random.Range(0, pointsOfInterest.Length)].position : transform.position;
        float radius = toPoi ? 2f : wanderRadius;

        for (int i = 0; i < 10; i++)
        {
            Vector2 r = Random.insideUnitCircle * radius;
            if (Head(new Vector3(center.x + r.x, transform.position.y, center.z + r.y))) return;
        }
        Idle();
    }

    // Set off for a spot, if it's somewhere you can walk and nothing terrible is happening there.
    bool Head(Vector3 spot)
    {
        if (!walker.IsWalkable(spot) || ParkChaos.Threat(spot, child, out _)) return false;
        target = spot;
        SetState(State.Wander);
        // Generous time budget; if they still aren't there they're probably stuck.
        stateEndsAt = Time.time + Vector3.Distance(transform.position, target) / wanderSpeed * 2f + 1f;
        return true;
    }

    // Arrived at a stall with empty hands: buy something.
    void TryShop()
    {
        if (Held != null) return;
        foreach (var s in Stall.All)
        {
            Vector3 d = s.ServePoint - transform.position;
            d.y = 0;
            if (d.magnitude < 1.2f)
            {
                s.Sell(this);
                return;
            }
        }
    }

    // Called by the queue (or the spawner) once a spot in line is reserved.
    public void EnterQueue(RideQueue q)
    {
        queue = q;
        SetState(State.Queueing);
    }

    void LeaveQueue()
    {
        if (queue != null) queue.Leave(this);
        queue = null;
    }

    public void StartRide()
    {
        queue = null; // the queue already took us off the line
        SetState(State.Riding);
        walker.move = Vector2.zero;
        Look.SetVisible(false);
    }

    public void EndRide(Vector3 at)
    {
        rb.position = transform.position = new Vector3(at.x, transform.position.y, at.z);
        Look.SetVisible(true);
        Idle();
    }

    // Keep close to the grown-up, hurrying to catch up when they get ahead.
    void FollowParent()
    {
        if (parent == null || !parent.isActiveAndEnabled || parent.Current == State.Down)
        {
            parent = null; // on their own now
            Idle();
            return;
        }
        float d = Distance(parent);
        bool far = d > followDistance * 2.5f;
        walker.speed = far ? parent.possessedSpeed : wanderSpeed;
        Look.running = far;
        if (d > followDistance) WalkTo(parent.transform.position + followOffset);
        else walker.move = Vector2.zero;
    }

    // Someone nearby is acting strange (possessed): stop and stare for a moment.
    bool LookForPossessed()
    {
        if (Current == State.Stare || Time.time < nextStare) return false;
        nextStare = Time.time + Random.Range(0.4f, 0.8f);

        var host = Possession.Instance != null ? Possession.Instance.Host : null;
        if (host == null || host == this || host == parent || Distance(host) > stareRadius) return false;

        SetState(State.Stare);
        walker.move = Vector2.zero;
        Look.Face(host.transform.position);
        Look.Show("question", stareTime);
        stateEndsAt = Time.time + stareTime;
        nextStare = stateEndsAt + Random.Range(6f, 9f);
        return true;
    }

    // ---------------------------------------------------------------- things they carry

    // Take hold of an item (lifting it out of someone else's hands if need be).
    public bool GrabItem(Item it, bool silent = false)
    {
        if (Held != null || it == null) return false;
        if (it.Holder != null) it.Holder.LoseHeld();
        Held = it;
        it.PickedUp(this, silent);
        if (it.Owner != null && it.Owner != this) it.Owner.NoticeTheft(it, this);
        if (it.Owner == this && wanted == it) wanted = null;
        return true;
    }

    public void LoseHeld() => Held = null;

    public void DropHeld()
    {
        if (Held == null) return;
        var it = Held;
        Held = null;
        it.Dropped(transform.position + Facing * 0.6f + Vector3.up * 1f);
    }

    public void ThrowHeld(Vector3 velocity)
    {
        if (Held == null) return;
        var it = Held;
        Held = null;
        Look.Gesture(Look.interact, 0.3f);
        it.Thrown(this, CarryPoint, velocity);
    }

    // Handed over and used up (eaten, poured out, left in a lock).
    public void TakeItem()
    {
        if (Held == null) return;
        var it = Held;
        Held = null;
        it.Consume();
    }

    // Someone took our things: go and get them back.
    void NoticeTheft(Item it, Visitor by)
    {
        if (brain != null || Current is State.Possessed or State.Down or State.Fighting or State.Riding or State.Panic or State.Angry) return;
        if (Distance(by) > 10f) return;
        LeaveQueue();
        wanted = it;
        thief = by;
        SetState(State.Retrieving);
        stateEndsAt = Time.time + 12f;
        Look.Show("snack", 2.5f);
        Look.Cheer(0.5f);
        GameHud.Say($"{name}: Hey! That's my {it.DisplayName}!", "Bubbles/snack");
    }

    void Retrieve()
    {
        if (wanted == null)
        {
            // Used up by someone else. Unforgivable.
            Idle();
            if (thief != null) AddAnger(70f, thief);
            return;
        }
        if (Time.time > stateEndsAt)
        {
            // Gave up the chase, fuming.
            wanted = null;
            Idle();
            if (thief != null) AddAnger(40f, thief);
            return;
        }

        var holder = wanted.Holder;
        if (holder == this)
        {
            wanted = null;
            Idle();
            return;
        }
        walker.speed = panicSpeed * 0.85f;
        Look.running = true;
        if (holder == null)
        {
            // Lying around (or still in the air): go pick it up.
            if (!wanted.InFlight && !WalkTo(wanted.transform.position)) GrabItem(wanted);
            return;
        }
        if (Distance(holder) < 0.9f)
        {
            GrabItem(wanted);
            Idle();
            GameHud.Say($"{name} snatches back their {Held.DisplayName}!");
            AddAnger(35f, holder);
            return;
        }
        WalkTo(holder.transform.position);
    }

    // ---------------------------------------------------------------- anger and brawls

    public void GetHit(Visitor attacker, float power, Vector3 direction)
    {
        if (!CanBeHit) return;
        direction.y = 0;
        knock = (direction.sqrMagnitude > 0.01f ? direction.normalized : Facing) * 5f * power;
        knockUntil = Time.time + 0.2f;
        Look.Punch();
        Sfx.Punch();
        Fx.Burst("Fx/sparkle", HeadPoint, 10, new Color(1f, 0.9f, 0.4f), 3f, 0.3f, 0.4f);
        Fx.Shake(0.12f);
        if (Current == State.Possessed) return;
        AddAnger(100f * power, attacker);
    }

    public void AddAnger(float amount, Visitor culprit)
    {
        if (brain != null || Current is State.Possessed or State.Down or State.Fighting or State.Riding or State.Angry) return;
        if (culprit != null && culprit != this) Look.Face(culprit.transform.position);
        if (child)
        {
            // Kids don't fight back: they cry and run.
            if (amount >= 20f && culprit != null) Panic(culprit.transform.position, 3f);
            return;
        }
        Anger = Mathf.Clamp(Anger + amount, 0f, 100f);
        if (Anger >= 100f && culprit != null && culprit != this && culprit.CanBeHit) BecomeAngry(culprit);
        else Look.Show(amount >= 20f ? "exclaim" : "question", 1.5f);
    }

    void BecomeAngry(Visitor culprit)
    {
        LeaveQueue();
        AngryAt = culprit;
        Anger = 100f;
        SetState(State.Angry);
        stateEndsAt = Time.time + 15f;
        Look.Show("anger", 999f, true);
        Look.Cheer(0.6f);
        Sfx.Play(Sfx.Thud, 0.6f, 0.8f);
        GameHud.Say($"{name} is furious at {culprit.name}!", "Bubbles/anger");
    }

    void ChargeAtCulprit()
    {
        if (AngryAt == null || !AngryAt.CanBeHit || Time.time > stateEndsAt)
        {
            CalmDown();
            return;
        }
        walker.speed = panicSpeed;
        Look.running = true;
        if (Distance(AngryAt) < 0.9f) Brawl.Begin(this, AngryAt);
        else WalkTo(AngryAt.transform.position);
    }

    public void CalmDown()
    {
        AngryAt = null;
        Anger = 0f;
        Look.HideBubble();
        Idle();
    }

    public void EnterFight()
    {
        LeaveQueue();
        DropHeld();
        wanted = null;
        AngryAt = null;
        Anger = 0f;
        SetState(State.Fighting);
        walker.move = Vector2.zero;
        Look.HideBubble();
        Look.SetVisible(false);
    }

    public void ExitFight(bool won)
    {
        Look.SetVisible(true);
        if (!won)
        {
            Die();
            return;
        }
        SetState(State.Confused); // dazed
        Look.Show("swords", 2f);
        stateEndsAt = Time.time + 2f;
    }

    // Gone for good: a by-product of the chaos, never the spirit's own doing. The soul drifts to the spirit.
    public void Die()
    {
        if (Current == State.Down) return;
        if (brain != null) brain.enabled = false;
        LeaveQueue();
        DropHeld();
        walker.readKeyboard = false;
        walker.move = Vector2.zero;
        Look.SetVisible(true);
        Look.SetTint(new Color(0.6f, 0.6f, 0.65f));
        SetState(State.Down);
        Look.Show("skull", 3f);
        GameHud.Casualties++;
        ParkChaos.Add(transform.position, 9f, 30f, ParkChaos.Terrifying);
        SoulWisp.Spawn(HeadPoint);
    }

    // Flattened by something big.
    public void KnockDown() => Die();

    // ---------------------------------------------------------------- panic

    public void Panic(Vector3 from, float duration)
    {
        if (brain != null || Current is State.Possessed or State.Down or State.Fighting) return;
        if (Current == State.Riding) Look.SetVisible(true);
        LeaveQueue();
        DropHeld();
        AngryAt = null;
        panicFrom = from;
        SetState(State.Panic);
        stateEndsAt = Time.time + duration;
        Look.Cheer(0.4f);
        RunAway();
    }

    void RunAway()
    {
        Vector3 away = transform.position - panicFrom;
        away.y = 0;
        Vector2 dir = away.sqrMagnitude > 0.01f ? new Vector2(away.x, away.z).normalized : Random.insideUnitCircle.normalized;
        // Wobble so a crowd doesn't flee in one straight line; on a dead end, pick any direction.
        dir = Quaternion.Euler(0, 0, Random.Range(-50f, 50f)) * dir;
        if (!walker.IsWalkable(transform.position + new Vector3(dir.x, 0, dir.y))) dir = Random.insideUnitCircle.normalized;
        walker.move = dir;
        nextTurn = Time.time + Random.Range(0.5f, 1.2f);
    }

    // ---------------------------------------------------------------- possession

    public void BeginPossession()
    {
        LeaveQueue();
        AngryAt = null;
        Anger = 0f;
        wanted = null;
        SetState(State.Possessed);
        if (brain != null) brain.enabled = false;
        walker.readKeyboard = true;
        Look.HideBubble();
        Look.Cheer(0.4f);
        Look.SetTint(possessedTint);
    }

    public void EndPossession()
    {
        walker.readKeyboard = false;
        walker.move = Vector2.zero;
        if (Current == State.Down) return;
        Look.SetTint(Color.white);
        if (Current == State.Fighting) return;
        if (brain != null)
        {
            SetState(State.Idle);
            brain.enabled = true; // last, so the brain's own pose and icon survive SetState
            return;
        }

        SetState(State.Confused);
        Look.Show("question", confusedTime);
        stateEndsAt = Time.time + confusedTime;
    }

    void SetState(State s)
    {
        Current = s;
        walker.speed = s switch
        {
            State.Possessed => possessedSpeed,
            State.Panic => panicSpeed,
            _ => wanderSpeed,
        };
        Look.running = s == State.Panic;
        Look.pose = s switch
        {
            State.Confused => Look.think,
            State.Down => Look.fallDown,
            _ => null,
        };
        if (s == State.Panic) Look.Show("Scream", 999f, true);
        else if (s is State.Idle or State.Wander or State.Queueing or State.Follow) Look.HideBubble();
    }
}
