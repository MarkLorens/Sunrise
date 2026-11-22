using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Values are fixed because they're saved in the scene (the stall's goods, the spawner's items).
public enum VisitorItem { None = 0, Drink = 3, Keys = 4, WaterBottle = 5, Screwdriver = 6 }

// A park visitor going about their day: wandering, buying drinks at the stall, lining up for the Ferris wheel,
// carrying their things in their hand. They stop and stare at someone acting possessed, and scream and run when
// something terrible happens nearby (ParkChaos). When the spirit leaves they stand around confused for a few seconds.
// After an event they gawk at the scene from outside its police line (Aftermath); Reset Agents move them along.
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public class Visitor : MonoBehaviour
{
    // Every active visitor, so the spirit can find who it's touching without physics queries.
    public static readonly List<Visitor> All = new();

    public enum State { Idle, Wander, Queueing, Riding, Possessed, Confused, Panic, Down, Stare }

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

    // Places people like to hang around (benches, views). Empty = just roam.
    public Transform[] pointsOfInterest = new Transform[0];
    [Range(0, 1)] public float pointOfInterestChance = 0.3f;
    [Range(0, 1)] public float listenChance = 0.2f;  // per stop, chance to go listen to a speaker whose crowd isn't full
    public Vector2 listenTime = new(8f, 20f);         // how long they stand listening

    [Range(0, 1)] public float gawkChance = 0.5f;    // per stop, chance to go gawk at a scene nearby (Aftermath)
    public float gawkRange = 14f;                     // how far from a scene's gawking ring they notice it
    public Vector2 gawkTime = new(6f, 12f);           // how long they stand gawking

    public float stareRadius = 3f;
    public float stareTime = 1.5f;
    public float sendAwayDistance = 7f; // how far a Reset Agent shoos people (MoveAlong)

    public State Current { get; private set; }
    public VisitorGroup Group { get; set; } // came with friends (VisitorGroup); null: on their own
    bool Following => Group != null && Group.Leader != this;

    // Taken over by a script for a moment (the tutorial): standing still, or walking to scriptedSpot. Free() hands
    // them back. Possessing them frees them.
    bool scripted;
    Vector3? scriptedSpot;
    public bool Walking => scripted && scriptedSpot.HasValue;
    public bool CanBePossessed => possessable && Look.Visible && Current is not (State.Possessed or State.Riding or State.Down);
    public CharacterSprite Look { get; private set; }
    public Item Held { get; private set; }
    public VisitorItem HeldKind => Held != null ? Held.kind : VisitorItem.None;
    public Vector3 Facing { get; private set; } = Vector3.right;
    public float HeadHeight => Look.HeadHeight;
    public Vector3 HeadPoint => transform.position + Vector3.up * HeadHeight;
    // The hand nearer the camera, where whatever they carry sits.
    public Vector3 CarryPoint => Look.HandPoint;
    bool IsHost => Possession.Instance != null && Possession.Instance.Host == this;

    GroundWalker walker;
    Rigidbody rb;
    RideQueue queue;
    Vector3 target;
    Vector3? gawkAt; // heading to gawk at this scene (Aftermath)
    public Vector3 Target => target; // where they're walking to
    Vector3 panicFrom;
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

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Update()
    {
        Vector3 v = rb.linearVelocity;
        if (new Vector2(v.x, v.z).sqrMagnitude > 0.04f) Facing = new Vector3(v.x, 0f, v.z).normalized;
        if (brain != null && Current != State.Possessed) return;
        if (scripted && Current != State.Possessed)
        {
            if (scriptedSpot.HasValue && !WalkTo(scriptedSpot.Value)) scriptedSpot = null; // there
            if (!scriptedSpot.HasValue) walker.move = Vector2.zero;
            return;
        }

        if (Current is State.Idle or State.Wander or State.Stare or State.Confused)
        {
            if (ParkChaos.Threat(transform.position, false, out Vector3 horror))
            {
                Panic(horror, Random.Range(3f, 5f));
                return;
            }
            if (Current != State.Confused && LookForPossessed()) return;
        }

        // With friends: they go where the leader goes (panicking, staring and the rest they still do on their own).
        if (Following && Current is State.Idle or State.Wander)
        {
            Follow();
            return;
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
                    if (!Gawk()) Idle();
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
        }
    }

    public void Hold()
    {
        Group?.Leave(this); // a cutscene's taken them
        scripted = true;
        scriptedSpot = null;
        Idle();
    }

    // run: in a hurry (running speed and legs), like someone who really has to go.
    public void SendTo(Vector3 spot, bool run = false)
    {
        Group?.Leave(this);
        LeaveQueue();
        scripted = true;
        scriptedSpot = spot;
        SetState(run ? State.Panic : State.Wander);
    }

    public void Free()
    {
        if (!scripted) return;
        scripted = false;
        scriptedSpot = null;
        if (Current != State.Possessed) Idle();
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
        // Groups never queue for rides or go to the speakers (VisitorGroup).
        var q = RideQueue.All.Count > 0 ? RideQueue.All[Random.Range(0, RideQueue.All.Count)] : null;
        if (q != null && Group == null && Random.value < queueChance && q.Join(this))
        {
            EnterQueue(q);
            return;
        }
        if (Held == null && Stall.All.Count > 0 && Random.value < shopChance)
        {
            var stall = Stall.All[Random.Range(0, Stall.All.Count)];
            if (Head(stall.ServePoint + new Vector3(Random.Range(-0.4f, 0.4f), 0f, 0f))) return;
        }
        if (Random.value < gawkChance && HeadToGawk()) return;
        var speaker = Group == null ? Speaker.Wanting() : null;
        if (speaker != null && Random.value < listenChance && Head(speaker.ListenSpot())) return;
        PickTarget();
    }

    void Idle()
    {
        SetState(State.Idle);
        walker.move = Vector2.zero;
        // In front of a speaker they stay a while to listen.
        var speaker = brain == null ? Speaker.Near(transform.position) : null;
        if (speaker == null)
        {
            stateEndsAt = Time.time + Random.Range(idleTime.x, idleTime.y);
            return;
        }
        stateEndsAt = Time.time + Random.Range(listenTime.x, listenTime.y);
        Look.Face(speaker.transform.position);
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
        gawkAt = null;
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

    // Into a cabin (out of sight until the ride's over). Only visitors going about their day ride, never the spirit.
    public void StartRide()
    {
        queue = null; // the queue already took us off the line
        SetState(State.Riding);
        walker.move = Vector2.zero;
        Look.SetVisible(false);
    }

    // Back on the ground.
    public void EndRide(Vector3 at)
    {
        rb.position = transform.position = new Vector3(at.x, transform.position.y, at.z);
        Look.SetVisible(true);
        Idle();
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
        stateEndsAt = Time.time + stareTime;
        nextStare = stateEndsAt + Random.Range(6f, 9f);
        return true;
    }

    // ---------------------------------------------------------------- with friends (VisitorGroup)

    // To their slot by the leader, hurrying if they've fallen behind; there, they stand facing the way it faces.
    void Follow()
    {
        var lead = Group.Leader;
        Vector3 slot = Group.Slot(this);
        Vector3 d = slot - transform.position;
        d.y = 0f;
        if (d.magnitude > 0.5f)
        {
            if (Current != State.Wander) SetState(State.Wander);
            walker.speed = lead.wanderSpeed * (d.magnitude > 2f ? 1.6f : 1.1f);
            if (WalkTo(slot)) return;
        }
        if (Current != State.Idle) SetState(State.Idle);
        walker.move = Vector2.zero;
        Look.Face(transform.position + (lead.Look.FacingLeft ? Vector3.left : Vector3.right));
    }

    // ---------------------------------------------------------------- gawking (Aftermath)

    // Off to a spot on the ring round a scene nearby, keeping its distance, on the side they're coming from.
    bool HeadToGawk()
    {
        if (!Aftermath.Nearest(transform.position, gawkRange, out Vector3 center, out float keepBack)) return false;
        Vector3 from = transform.position - center;
        from.y = 0f;
        if (from.sqrMagnitude < 0.01f) from = Vector3.back;
        for (int i = 0; i < 8; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f) * from.normalized;
            if (!Head(center + dir * (keepBack + Random.Range(0.5f, 2f)))) continue;
            gawkAt = center;
            return true;
        }
        return false;
    }

    // Arrived at the ring: stand and stare at it for a while.
    bool Gawk()
    {
        if (!gawkAt.HasValue) return false;
        SetState(State.Stare);
        Look.panicFace = true; // only the face for now: the body joins in if they run (Panic)
        walker.move = Vector2.zero;
        Look.Face(gawkAt.Value);
        stateEndsAt = Time.time + Random.Range(gawkTime.x, gawkTime.y);
        gawkAt = null;
        return true;
    }

    // A Reset Agent telling the crowd to move along (ResetAgent): off they go, away from them.
    public void MoveAlong(Component by)
    {
        if (Following)
        {
            Group.Leader.MoveAlong(by); // the whole group goes
            return;
        }
        if (brain != null || Current is State.Possessed or State.Riding or State.Down or State.Queueing) return;
        SendAway(by);
    }

    // Told to move along by staff: walk off away from them.
    void SendAway(Component by)
    {
        LeaveQueue();
        Vector3 away = transform.position - by.transform.position;
        away.y = 0;
        if (away.sqrMagnitude < 0.01f) away = Vector3.back;
        for (int i = 0; i < 12; i++)
        {
            Vector3 spot = transform.position + Quaternion.Euler(0f, Random.Range(-50f, 50f), 0f) * away.normalized * sendAwayDistance;
            if (Head(spot)) return;
        }
        Idle();
    }

    // ---------------------------------------------------------------- things they carry

    public bool GrabItem(Item it, bool silent = false)
    {
        if (Held != null || it == null) return false;
        Held = it;
        it.PickedUp(this, silent);
        return true;
    }

    // Let go of whatever they hold (panicking, going down): it tumbles to the ground.
    public void DropHeld()
    {
        if (Held == null) return;
        var it = Held;
        Held = null;
        it.Dropped(transform.position + Facing * 0.6f + Vector3.up * 1f);
    }

    // Used up (poured out).
    public void TakeItem()
    {
        if (Held == null) return;
        var it = Held;
        Held = null;
        it.Consume();
    }

    // Gone for good: a by-product of the chaos (electrocuted in the puddle, the wheel), never the spirit's own doing.
    // Frozen on the spot at once; with a twitch, they stand still for `wait`, jolt for `twitch` seconds, then drop.
    // zapped: electrocuted (DrinkingFountain): face and body panic at once (arms up while they jolt), and the face
    // stays that way once they're down.
    public void Die(float wait = 0f, float twitch = 0f, bool zapped = false)
    {
        Group?.Leave(this);
        if (Current == State.Down) return;
        if (brain != null) brain.enabled = false;
        LeaveQueue();
        DropHeld();
        walker.readKeyboard = false;
        walker.move = Vector2.zero;
        Look.SetVisible(true);
        SetState(State.Down);
        if (zapped) Look.panicBody = true; // the face too; the fallen pose takes over the body once they drop
        if (twitch <= 0f)
        {
            Collapse();
            return;
        }
        Look.pose = null; // still standing until the twitching's over
        StartCoroutine(Electrocuted(wait, twitch));
    }

    IEnumerator Electrocuted(float wait, float twitch)
    {
        yield return new WaitForSeconds(wait);
        Look.Twitch(twitch);
        yield return new WaitForSeconds(twitch);
        Collapse();
    }

    // Down, greyed out, and anyone nearby runs (ParkChaos sets them panicking).
    void Collapse()
    {
        Look.SetTint(new Color(0.6f, 0.6f, 0.65f));
        Look.pose = Look.fallDown;
        ParkChaos.Add(transform.position, 9f, 30f, ParkChaos.Terrifying);
    }

    // Flattened by something big.
    public void KnockDown() => Die();

    // ---------------------------------------------------------------- panic

    public void Panic(Vector3 from, float duration)
    {
        if (brain != null || IsHost || Current is State.Possessed or State.Down) return;
        if (Current == State.Riding) Look.SetVisible(true);
        LeaveQueue();
        DropHeld();
        panicFrom = from;
        SetState(State.Panic);
        Look.panicBody = true; // panic body and face, until they calm down (SetState)
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
        Group?.Leave(this); // their friends carry on without them (and stare: LookForPossessed)
        scripted = false;
        scriptedSpot = null;
        LeaveQueue();
        SetState(State.Possessed);
        if (brain != null) brain.enabled = false;
        walker.readKeyboard = true;
        Look.Cheer(0.4f);
        Look.SetTint(possessedTint);
        Look.SetPossessed(true);
    }

    public void EndPossession()
    {
        walker.readKeyboard = false;
        walker.move = Vector2.zero;
        Look.SetPossessed(false);
        if (Current == State.Down) return;
        Look.SetTint(Color.white);
        if (brain != null)
        {
            SetState(State.Idle);
            brain.enabled = true; // last, so the brain's own pose survives SetState
            return;
        }

        SetState(State.Confused);
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
        Look.panicFace = Look.panicBody = false; // Gawk and Panic set them after
        Look.pose = s switch
        {
            State.Confused => Look.think,
            State.Down => Look.fallDown,
            _ => null,
        };
    }
}
