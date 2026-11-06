using System.Collections.Generic;
using UnityEngine;

// Values are fixed because they're saved in the scene (the stall's goods, the spawner's items).
public enum VisitorItem { None = 0, Drink = 3, Screwdriver = 4, WaterBottle = 5 }

// A park visitor going about their day: wandering, buying drinks at the stall, lining up for the Ferris wheel,
// carrying their things over their head. They stop and stare at someone acting possessed, gather round to watch a
// fight, and scream and run when something terrible happens nearby (ParkChaos). When the spirit leaves they stand
// around confused for a few seconds.
// While possessing someone else, E on a visitor picks a fight with them (a brawl one of them won't walk away from);
// staff send them on their way instead.
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public class Visitor : MonoBehaviour, IInteraction
{
    // Every active visitor, so the spirit can find who it's touching without physics queries.
    public static readonly List<Visitor> All = new();

    public enum State { Idle, Wander, Queueing, Riding, Possessed, Confused, Panic, Down, Stare, Fighting, Watching }

    public bool possessable = true;
    // Staff (the ride operator, the mechanic) have their own AI. It's switched off while the spirit is inside, and
    // the usual wandering and panic below are skipped for them.
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

    public float stareRadius = 3f;
    public float stareTime = 1.5f;
    public float crowdRadius = 10f;  // how far away a brawl draws them over to watch
    public float sendAwayDistance = 7f;

    public State Current { get; private set; }
    public bool CanBePossessed => possessable && Look.Visible && Current is not (State.Possessed or State.Riding or State.Down or State.Fighting);
    public bool CanBeHit => isActiveAndEnabled && Current is not (State.Riding or State.Down or State.Fighting);
    public CharacterSprite Look { get; private set; }
    public Item Held { get; private set; }
    public VisitorItem HeldKind => Held != null ? Held.kind : VisitorItem.None;
    public Vector3 Facing { get; private set; } = Vector3.right;
    public float HeadHeight => Look.HeadHeight;
    public Vector3 HeadPoint => transform.position + Vector3.up * HeadHeight;
    public Vector3 CarryPoint => transform.position + Vector3.up * (HeadHeight + 0.3f);
    // The cabin they're sitting in while riding.
    public Transform Cabin { get; private set; }
    bool IsHost => Possession.Instance != null && Possession.Instance.Host == this;

    GroundWalker walker;
    Rigidbody rb;
    RideQueue queue;
    Vector3 target;
    Vector3 panicFrom;
    Vector3 spectacle;    // the brawl they're watching
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
        Idle();
    }

    // Staff set `brain` in their own Awake, so by now we know whether this is someone you can pick a fight with.
    void Start()
    {
        if (brain != null) return;
        var i = gameObject.AddComponent<Interactable>();
        i.range = 1.3f;
        i.prompt = name;
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

        if (Current is State.Idle or State.Wander or State.Stare or State.Confused or State.Watching)
        {
            if (ParkChaos.Threat(transform.position, false, out Vector3 horror))
            {
                Panic(horror, Random.Range(3f, 5f));
                return;
            }
            if (Current is not (State.Confused or State.Watching) && LookForPossessed()) return;
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

            case State.Stare:
            case State.Confused:
                if (Time.time >= stateEndsAt) Idle();
                break;

            case State.Panic:
                if (Time.time >= stateEndsAt) Idle();
                else if (Time.time >= nextTurn) RunAway();
                break;

            case State.Watching:
                if (!WalkTo(target)) Look.Face(spectacle);
                if (Time.time >= stateEndsAt) Idle();
                break;
        }
    }

    // Walks toward a point, around whatever's in the way; returns false once there.
    bool WalkTo(Vector3 p) => walker.WalkTo(p);

    float Distance(Component other)
    {
        Vector3 d = other.transform.position - transform.position;
        d.y = 0;
        return d.magnitude;
    }

    // ---------------------------------------------------------------- going about their day

    void Decide()
    {
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
        if (!walker.IsWalkable(spot) || ParkChaos.Threat(spot, false, out _)) return false;
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

    public void StartRide(Transform cabin)
    {
        queue = null; // the queue already took us off the line
        Cabin = cabin;
        SetState(State.Riding);
        walker.readKeyboard = false;
        walker.move = Vector2.zero;
        Look.SetVisible(false);
    }

    // Back on the ground. The spirit, if it rode along, takes the wheel again.
    public void EndRide(Vector3 at)
    {
        Cabin = null;
        rb.position = transform.position = new Vector3(at.x, transform.position.y, at.z);
        Look.SetVisible(true);
        if (!IsHost)
        {
            Idle();
            return;
        }
        SetState(State.Possessed);
        walker.readKeyboard = true;
    }

    // Someone nearby is acting strange (possessed): stop and stare for a moment.
    bool LookForPossessed()
    {
        if (Current == State.Stare || Time.time < nextStare) return false;
        nextStare = Time.time + Random.Range(0.4f, 0.8f);

        var host = Possession.Instance != null ? Possession.Instance.Host : null;
        if (host == null || host == this || Distance(host) > stareRadius) return false;

        SetState(State.Stare);
        walker.move = Vector2.zero;
        Look.Face(host.transform.position);
        Look.Show("question", stareTime);
        stateEndsAt = Time.time + stareTime;
        nextStare = stateEndsAt + Random.Range(6f, 9f);
        return true;
    }

    // A brawl nearby: people going about their day wander over and stand round it until it's over.
    public void Watch(Vector3 brawl, float until)
    {
        if (brain != null || Current is not (State.Idle or State.Wander or State.Stare or State.Confused)) return;
        Vector3 away = transform.position - brawl;
        away.y = 0;
        if (away.magnitude > crowdRadius) return;
        if (away.sqrMagnitude < 0.01f) away = Vector3.back;
        spectacle = brawl;
        target = brawl + Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f) * away.normalized * Random.Range(1.8f, 2.6f);
        SetState(State.Watching);
        stateEndsAt = until + Random.Range(0.5f, 2f);
        Look.Show("exclaim", 1.2f);
    }

    // Told to move along by staff: walk off away from them.
    void SendAway(Visitor by)
    {
        LeaveQueue();
        Vector3 away = transform.position - by.transform.position;
        away.y = 0;
        if (away.sqrMagnitude < 0.01f) away = Vector3.back;
        Look.Show("question", 1.5f);
        GameHud.Say($"{by.name}: Staff only around here. Move along, please.");
        Mischief.Complete(Mischief.Task.SendAway);
        for (int i = 0; i < 12; i++)
        {
            Vector3 spot = transform.position + Quaternion.Euler(0f, Random.Range(-50f, 50f), 0f) * away.normalized * sendAwayDistance;
            if (Head(spot)) return;
        }
        Idle();
    }

    // ---------------------------------------------------------------- E on this person (while possessing someone else)

    public string Prompt(Visitor host) =>
        host != null && host.brain != null ? $"Tell {name} to move along" : $"Pick a fight with {name}";

    public string Blocked(Visitor host)
    {
        if (!CanBeHit || Current == State.Possessed) return $"{name} is busy";
        if (host.brain == null && !host.CanBeHit) return "You're in no state to fight";
        return null;
    }

    public void Interact(Visitor host)
    {
        if (host.brain != null)
        {
            SendAway(host);
            return;
        }
        Look.Face(host.transform.position);
        GameHud.Say($"{host.name}: Hey! What are YOU looking at?!");
        Brawl.Begin(host, this);
    }

    // ---------------------------------------------------------------- things they carry

    public bool GrabItem(Item it, bool silent = false)
    {
        if (Held != null || it == null) return false;
        if (it.Holder != null) it.Holder.LoseHeld();
        Held = it;
        it.PickedUp(this, silent);
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

    // Used up (poured out, jammed in the gears).
    public void TakeItem()
    {
        if (Held == null) return;
        var it = Held;
        Held = null;
        it.Consume();
    }

    // Hit by something thrown: knocked back a little.
    public void GetHit(float power, Vector3 direction)
    {
        if (!CanBeHit) return;
        direction.y = 0;
        knock = (direction.sqrMagnitude > 0.01f ? direction.normalized : Facing) * 5f * power;
        knockUntil = Time.time + 0.2f;
        Look.Punch();
        Sfx.Punch();
        Fx.Burst("Fx/sparkle", HeadPoint, 10, new Color(1f, 0.9f, 0.4f), 3f, 0.3f, 0.4f);
        Fx.Shake(0.12f);
        if (Current != State.Possessed && brain == null) Look.Show("exclaim", 1.5f);
    }

    // ---------------------------------------------------------------- brawls

    public void EnterFight()
    {
        LeaveQueue();
        DropHeld();
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

    // Gone for good: a by-product of the chaos (a brawl, the wheel), never the spirit's own doing.
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
        ParkChaos.Add(transform.position, 9f, 30f, ParkChaos.Terrifying);
    }

    // Flattened by something big.
    public void KnockDown() => Die();

    // ---------------------------------------------------------------- panic

    public void Panic(Vector3 from, float duration)
    {
        if (brain != null || IsHost || Current is State.Possessed or State.Down or State.Fighting) return;
        if (Current == State.Riding) Look.SetVisible(true);
        LeaveQueue();
        DropHeld();
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
        if (Current is State.Fighting or State.Riding) return; // a rider left in a cabin just enjoys the view
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
        else if (s is State.Idle or State.Wander or State.Queueing) Look.HideBubble();
    }
}
