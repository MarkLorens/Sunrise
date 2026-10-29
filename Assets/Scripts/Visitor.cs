using System.Collections.Generic;
using UnityEngine;

public enum VisitorItem { None, SpicyHotdog, ToiletKey, Drink }

// A park visitor: wanders around, lines up for rides, and can be possessed by the spirit.
// When the spirit leaves they stand around confused for a few seconds; when things go wrong they panic.
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public class Visitor : MonoBehaviour
{
    // Every active visitor, so the spirit can find who it's touching without physics queries.
    public static readonly List<Visitor> All = new();

    public enum State { Idle, Wander, Queueing, Riding, Possessed, Confused, Panic, Down }

    public VisitorItem item;
    public bool possessable = true; // masked staff set this off
    public float wanderSpeed = 2f;
    public float possessedSpeed = 3.5f;
    public float panicSpeed = 4.5f;
    public float wanderRadius = 6f;
    public Vector2 idleTime = new(1f, 4f);
    public float confusedTime = 3f;
    [Range(0, 1)] public float queueChance = 0.15f; // per stop, chance to go line up for a ride
    public Color possessedTint = new(0.75f, 0.55f, 1f);

    // Places people like to hang around (stalls, benches). Empty = just roam.
    public Transform[] pointsOfInterest = new Transform[0];
    [Range(0, 1)] public float pointOfInterestChance = 0.3f;

    public State Current { get; private set; }
    public bool CanBePossessed => possessable && Current is not (State.Possessed or State.Riding or State.Down);
    public CharacterSprite Look { get; private set; }

    GroundWalker walker;
    Rigidbody rb;
    RideQueue queue;
    Vector3 target;
    Vector3 panicFrom;
    float stateEndsAt;
    float nextTurn;

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
        switch (Current)
        {
            case State.Idle:
                if (Time.time >= stateEndsAt) Decide();
                break;

            case State.Wander:
                // Arrived, or stuck on an edge for too long: take a break and pick somewhere else.
                if (!WalkTo(target) || Time.time >= stateEndsAt) Idle();
                break;

            case State.Queueing:
                WalkTo(queue.SlotPosition(this));
                break;

            case State.Confused:
                if (Time.time >= stateEndsAt) Idle();
                break;

            case State.Panic:
                if (Time.time >= stateEndsAt) Idle();
                else if (Time.time >= nextTurn) RunAway();
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

    void Decide()
    {
        var q = RideQueue.All.Count > 0 ? RideQueue.All[Random.Range(0, RideQueue.All.Count)] : null;
        if (q != null && Random.value < queueChance && q.Join(this))
        {
            EnterQueue(q);
            return;
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
            Vector3 candidate = new(center.x + r.x, transform.position.y, center.z + r.y);
            if (!walker.IsWalkable(candidate)) continue;

            target = candidate;
            SetState(State.Wander);
            // Generous time budget; if they still aren't there they're probably stuck.
            stateEndsAt = Time.time + Vector3.Distance(transform.position, target) / wanderSpeed * 2f + 1f;
            return;
        }
        Idle();
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

    public void Panic(Vector3 from, float duration)
    {
        if (Current is State.Possessed or State.Down) return;
        if (Current == State.Riding) Look.SetVisible(true);
        LeaveQueue();
        panicFrom = from;
        SetState(State.Panic);
        stateEndsAt = Time.time + duration;
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

    // Flattened by something big. A by-product, never the spirit's own doing.
    public void KnockDown()
    {
        if (Current == State.Down) return;
        LeaveQueue();
        walker.readKeyboard = false;
        walker.move = Vector2.zero;
        Look.SetVisible(true);
        Look.SetTint(Color.white);
        SetState(State.Down);
    }

    public void TakeItem()
    {
        item = VisitorItem.None;
        RefreshIcon();
    }

    public void BeginPossession()
    {
        LeaveQueue();
        SetState(State.Possessed);
        walker.readKeyboard = true;
        walker.speed = possessedSpeed;
        Look.SetTint(possessedTint);
    }

    public void EndPossession()
    {
        walker.readKeyboard = false;
        walker.move = Vector2.zero;
        Look.SetTint(Color.white);
        if (Current == State.Down) return;

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
        Look.pose = s switch
        {
            State.Confused => Look.think,
            State.Down => Look.fallDown,
            _ => null,
        };
        RefreshIcon();
    }

    void RefreshIcon()
    {
        Look.SetIcon(Current switch
        {
            State.Riding or State.Down => null,
            State.Confused => "Confused",
            State.Panic => "Panic",
            _ => item == VisitorItem.None ? null : item.ToString(),
        });
    }
}
