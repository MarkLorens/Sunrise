using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Values are fixed because they're saved in the scene (the stall's goods, the spawner's items).
public enum VisitorItem { None = 0, Drink = 3, Keys = 4, WaterBottle = 5, Screwdriver = 6, Balloon = 7, CottonCandy = 8, HotDog = 9, IceCream = 10, Popcorn = 11 }

// A park visitor going about their day: wandering, buying drinks at the stall, lining up for the Ferris wheel or at
// a stand (StandQueue), having their photo taken with the mascot (Mascot), carrying their things in their hand. They stop and stare at someone acting possessed, and scream and run when
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
    [Range(0, 1)] public float standChance = 0.25f; // per stop, chance to line up at a stand with room (StandQueue)
    public float standRange = 25f;                   // how far off a stand they'll go to
    public Vector2 standAgain = new(30f, 60f);       // seconds after being served before they'll line up again
    [Range(0, 1)] public float photoChance = 0.15f; // per stop near the mascot, chance to go have a photo with him
    public float photoRange = 16f;
    public Vector2 photoAgain = new(90f, 150f);      // seconds before they'd want another
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
    IVisitorLine queue;
    float giveUpAt = -1f; // walking to their spot in a stand's line: past this they give up (they can't get there)
    float nextStand, nextPhoto;
    Mascot photoWith;     // heading over for a photo with him
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
            if (fleeOut.HasValue && scriptedSpot.HasValue && Vector3.Dot(transform.position - scriptedSpot.Value, fleeAlong) > -0.4f)
            {
                scriptedSpot = fleeOut; // through the gate: off every which way
                fleeOut = null;
            }
            if (scriptedSpot.HasValue && !WalkTo(scriptedSpot.Value))
            {
                scriptedSpot = null; // there
                if (fleeing) gameObject.SetActive(false); // out of the park (Flee)
            }
            if (!scriptedSpot.HasValue) walker.move = Vector2.zero;
            return;
        }

        // The park's gone mad (Unease.Mayhem): nobody's calm any more, not even in the queue. Running nowhere in
        // particular, turning this way and that (RunAway), and off again as soon as they stop.
        if (brain == null && Current is State.Idle or State.Wander or State.Stare or State.Confused or State.Queueing && Unease.Mayhem)
        {
            Vector2 r = Random.insideUnitCircle.normalized * 3f;
            Panic(transform.position + new Vector3(r.x, 0f, r.y), Random.Range(2f, 4f));
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

        // Complaining at the information booth (BoothCrowd): there they stand (scares and the rest still come first).
        if (protestSpot.HasValue && Current is State.Idle or State.Wander)
        {
            Protest();
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
                    if (!Photo() && !Gawk()) Idle();
                }
                else if (Time.time >= stateEndsAt) Idle();
                break;

            case State.Queueing:
                if (queue is StandQueue)
                {
                    StandInLine();
                    break;
                }
                WalkTo(queue.SlotPosition(this));
                break;

            case State.Riding: // still on the way up to get on (Board); once on, nothing to do till the ride's over
                if (Boarding && !atBoardingSpot && !WalkTo(boardingSpot)) atBoardingSpot = true;
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
        LeaveQueue();
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

    // The finale (FerrisWheel): drop everything, staff included, and run for `exit`, gone once there. Given a `gate`:
    // all squeezed through it together first, then scattering every which way once out (`scatter`: degrees either
    // side of straight on), each to a spot as far off as the exit.
    bool fleeing;
    public bool Fleeing => fleeing; // out of the park at the end: the only ones who walk out over its grass (GroundWalker)
    Vector3? fleeOut;
    Vector3 fleeAlong;

    public void Flee(Vector3 exit, Vector3? gate = null, float scatter = 0f)
    {
        if (Current is State.Down or State.Possessed || !gameObject.activeInHierarchy || !Look.Visible) return; // not the mascot gone home
        if (brain != null) // a worker's job no longer matters
        {
            brain.enabled = false;
            brain = null;
        }
        walker.ignoreObstacles = false; // out from behind any counter like everyone else
        StopProtesting();
        fleeing = true;
        fleeOut = null;
        if (gate is Vector3 g)
        {
            g.y = 0f;
            Vector3 along = exit - g;
            along.y = 0f;
            fleeAlong = along.normalized;
            Vector3 across = Vector3.Cross(Vector3.up, fleeAlong);
            Vector3 through = g + across * Random.Range(-1.2f, 1.2f); // shoulder to shoulder in the gateway
            if (Vector3.Dot(transform.position - through, fleeAlong) < 0f) // not out already
            {
                fleeOut = through + Quaternion.Euler(0f, Random.Range(-scatter, scatter), 0f) * fleeAlong * along.magnitude * Random.Range(1.1f, 1.4f);
                SendTo(through, true);
                return;
            }
        }
        SendTo(exit, true);
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
        if (Group == null && Time.time >= nextStand && Random.value < standChance && JoinStand()) return;
        if (Time.time >= nextPhoto && Random.value < photoChance && HeadToPhoto()) return;
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
        photoWith = null;
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
    public void EnterQueue(IVisitorLine q)
    {
        queue = q;
        giveUpAt = -1f;
        SetState(State.Queueing);
    }

    // ---------------------------------------------------------------- a stand's line (StandQueue)

    // The nearest stand within standRange with room in its line: in it.
    bool JoinStand()
    {
        StandQueue best = null;
        float bestD = standRange;
        foreach (var s in StandQueue.All)
        {
            float d = Distance(s);
            if (d >= bestD || !s.HasRoom) continue;
            best = s;
            bestD = d;
        }
        if (best == null || !best.Join(this)) return false;
        EnterQueue(best);
        return true;
    }

    // To their spot, then facing the counter. Frightened (something terrible nearby), they bolt; if they can't get to
    // their spot in good time (something in the way, a police line round the stand) they give up on it.
    void StandInLine()
    {
        if (queue is StandQueue stand && !stand.Has(this))
        {
            queue = null; // the line got shorter (StandQueue.KeepAway): on with their day
            Idle();
            return;
        }
        if (ParkChaos.Threat(transform.position, false, out Vector3 horror))
        {
            Panic(horror, Random.Range(3f, 5f)); // out of the line
            return;
        }
        Vector3 slot = queue.SlotPosition(this);
        if (WalkTo(slot))
        {
            if (giveUpAt < 0f) giveUpAt = Time.time + Vector3.Distance(transform.position, slot) / wanderSpeed * 2f + 3f;
            else if (Time.time > giveUpAt)
            {
                LeaveQueue();
                nextStand = Time.time + Random.Range(standAgain.x, standAgain.y);
                Idle();
            }
            return;
        }
        giveUpAt = -1f;
        if (queue.Faces(out Vector3 counter)) Look.Face(counter);
    }

    // Served at the front of a stand's line (StandQueue): pleased, off they go with what they bought (an ice cream, a
    // balloon, a hot dog). `pleased` false: the stand closed on them (burnt down), so nothing, and no cheer.
    public void Served(VisitorItem bought = VisitorItem.None, bool pleased = true)
    {
        queue = null;
        giveUpAt = -1f;
        nextStand = Time.time + Random.Range(standAgain.x, standAgain.y);
        if (Current != State.Queueing) return;
        if (pleased)
        {
            Buy(bought);
            Look.Cheer(0.5f);
        }
        PickTarget();
    }

    // Into their hand. Something from a stand they were already holding (finished by now) makes way for it; anything
    // else they keep (a drink or a water bottle, which the puzzles need about the park; staff's keys), going without.
    void Buy(VisitorItem bought)
    {
        if (bought == VisitorItem.None) return;
        if (Held != null)
        {
            if (Held.kind is not (VisitorItem.Balloon or VisitorItem.CottonCandy or VisitorItem.HotDog or VisitorItem.IceCream or VisitorItem.Popcorn)) return;
            TakeItem();
        }
        GrabItem(Item.Create(bought, CarryPoint), true);
    }

    // ---------------------------------------------------------------- a photo with the mascot (Mascot)

    // The mascot nearby, if he's free: over to stand beside him.
    bool HeadToPhoto()
    {
        var m = Mascot.ForPhoto(transform.position, photoRange);
        if (m == null || !m.Reserve(this, 30f)) return false; // he stops where he is to wait for them
        if (!Head(m.PhotoSpot(transform.position)))
        {
            m.Release(this);
            return false;
        }
        m.Reserve(this, Vector3.Distance(transform.position, target) / wanderSpeed * 2f + 4f);
        photoWith = m;
        nextPhoto = Time.time + Random.Range(photoAgain.x, photoAgain.y);
        return true;
    }

    // Beside him: he strikes a pose, they throw their arms up, a flash, and that's the photo.
    bool Photo()
    {
        var m = photoWith;
        photoWith = null;
        if (m == null || !m.ReservedFor(this)) return false;
        SetState(State.Stare); // standing still (and not staring at anyone else meanwhile)
        walker.move = Vector2.zero;
        stateEndsAt = Time.time + 6f; // the longest it takes
        Look.Face(m.transform.position);
        StartCoroutine(TakePhoto(m));
        return true;
    }

    IEnumerator TakePhoto(Mascot m)
    {
        m.Pose(this);
        GameHud.Shout(transform, GameText.SayCheese, 1.4f, true);
        for (float t = 0f; !m.Posing && t < 3f; t += Time.deltaTime)
        {
            if (Current != State.Stare || !m.Guarding) yield break; // something came up
            yield return null;
        }
        if (Current != State.Stare || !m.Guarding) yield break;
        Look.Face(m.transform.position);
        Look.Cheer(1.2f);
        Vector3 between = (transform.position + m.transform.position) * 0.5f + Vector3.up * 1.6f;
        Fx.Burst("Fx/sparkle", between, 12, Color.white, 3.5f, 0.35f, 0.35f); // the camera's flash
        yield return new WaitForSeconds(1.3f);
        if (Current == State.Stare) stateEndsAt = Time.time; // done: on with their day
    }

    void LeaveQueue()
    {
        if (queue != null) queue.Leave(this);
        queue = null;
        giveUpAt = -1f;
    }

    // Their turn (RideQueue): up the platform to `spot` behind the wheel, where they get on (StartRide). Nobody can
    // possess them on the way: they're as good as riding.
    public bool Boarding { get; private set; }
    public bool AtBoardingSpot => Boarding && atBoardingSpot;
    Vector3 boardingSpot;
    bool atBoardingSpot;

    public void Board(Vector3 spot)
    {
        queue = null; // the queue already took us off the line
        Group?.Leave(this);
        SetState(State.Riding);
        Boarding = true;
        boardingSpot = spot;
        atBoardingSpot = false;
    }

    // Into a cabin (out of sight until the ride's over). Only visitors going about their day ride, never the spirit.
    public void StartRide()
    {
        Boarding = false;
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

    // ---------------------------------------------------------------- complaining (BoothCrowd)

    Vector3? protestSpot;
    Vector3 protestFacing;

    public bool Protesting => protestSpot.HasValue;
    public bool AtProtest => protestSpot.HasValue && Current == State.Idle;

    // Off to `spot` in front of the information booth to stand facing `facing` and complain, until told otherwise.
    public void Protest(Vector3 spot, Vector3 facing)
    {
        Group?.Leave(this);
        protestSpot = spot;
        protestFacing = facing;
    }

    public void StopProtesting() => protestSpot = null;

    void Protest()
    {
        Vector3 d = protestSpot.Value - transform.position;
        d.y = 0f;
        if (d.magnitude > 0.4f)
        {
            if (Current != State.Wander) SetState(State.Wander);
            if (WalkTo(protestSpot.Value)) return;
        }
        if (Current != State.Idle) SetState(State.Idle);
        walker.move = Vector2.zero;
        Look.Face(protestFacing);
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

    // Carried off by the clean-up crew (DrinkingFountain): out of the park for good, saves included (SaveGame).
    public bool CarriedOff { get; private set; }

    public void CarryOff()
    {
        CarriedOff = true;
        gameObject.SetActive(false);
    }

    // Gone for good: a by-product of the chaos (electrocuted in the puddle, the wheel), never the spirit's own doing.
    // Frozen on the spot at once; with a twitch, they stand still for `wait`, jolt for `twitch` seconds, then drop.
    // zapped: electrocuted (DrinkingFountain): face and body panic at once (arms up while they jolt), and the face
    // stays that way once they're down.
    public bool Zapped { get; private set; } // electrocuted: lying where they toppled (SaveGame keeps it)

    public void Die(float wait = 0f, float twitch = 0f, bool zapped = false)
    {
        if (Current != State.Down) Zapped = zapped;
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

    // Down, greyed out, and anyone nearby runs (ParkChaos sets them panicking). Electrocuted, they're singed darker and
    // topple over, smoking, the odd spark still crackling off them.
    void Collapse()
    {
        Look.SetTint(Zapped ? new Color(0.5f, 0.47f, 0.47f) : new Color(0.6f, 0.6f, 0.65f));
        Look.pose = Look.fallDown;
        if (Zapped)
        {
            Look.FallOver();
            StartCoroutine(Smoulder());
        }
        ParkChaos.Add(transform.position, 9f, 30f, ParkChaos.Terrifying);
    }

    // Wisps of smoke curling up off them, and a few last sparks, for a few seconds.
    IEnumerator Smoulder()
    {
        for (float t = 0f; t < 5f; t += 0.25f)
        {
            Vector3 at = transform.position + Vector3.up * Random.Range(0.2f, 0.6f) + Vector3.back * 0.1f;
            var r = ParkArt.Make("Smoke", null, "Fx/puff", Random.Range(0.35f, 0.6f), new Color(0.2f, 0.2f, 0.22f, 0.5f), ParkArt.OverheadOrder - 2);
            r.transform.position = at;
            var bit = r.gameObject.AddComponent<FxBit>();
            bit.velocity = new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.6f, 1f), 0f);
            bit.life = Random.Range(1.2f, 2f);
            if (t < 2.5f && Random.value < 0.35f)
                Fx.Burst("Fx/sparkle", at, 3, new Color(0.7f, 0.85f, 1f), 2.5f, 0.15f, 0.3f);
            yield return new WaitForSeconds(0.25f);
        }
    }

    // Loading a save: electrocuted, already lying there.
    public void LieZapped()
    {
        Zapped = true;
        Look.panicBody = true;
        Look.SetTint(new Color(0.5f, 0.47f, 0.47f));
        Look.FallOver(true);
    }

    // Flattened by something big.
    public void KnockDown() => Die();

    // Squashed flat as a pancake (the Ferris wheel rolling over them): down for good like Die, but drawn flat
    // (CharacterSprite.Squash), with a puff of dust and stars. quiet: loading a save, already lying there.
    public void Squash(bool quiet = false)
    {
        Group?.Leave(this);
        if (Current == State.Down) return;
        if (brain != null) brain.enabled = false;
        scripted = false;
        scriptedSpot = null;
        LeaveQueue();
        DropHeld();
        walker.readKeyboard = false;
        walker.move = Vector2.zero;
        Look.SetVisible(true);
        SetState(State.Down);
        Look.Squash(quiet);
        if (quiet) return;
        Fx.Burst("Fx/puff", transform.position + Vector3.up * 0.2f, 10, new Color(0.75f, 0.7f, 0.65f), 3f, 0.5f, 0.6f);
        Fx.Burst("Fx/sparkle", transform.position + Vector3.up * 0.6f, 6, Fx.Gold, 2f, 0.25f, 0.8f);
        ParkChaos.Add(transform.position, 9f, 30f, ParkChaos.Terrifying);
    }

    public bool Flat => Look.Flat;

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
        Scream();
        RunAway();
    }

    // NPC-01 as they bolt, following them as they run. Not every time: someone who keeps panicking near something that
    // stays frightening screams again only after ScreamCooldown, and only ScreamsPerSecond start across the whole park,
    // so a crowd bolting at once is a few screams, not a wall of them (ParkCrowd's panic layer fills in the rest).
    const float ScreamCooldown = 8f;
    const int ScreamsPerSecond = 3;
    static readonly Queue<float> recentScreams = new();
    float nextScream;

    void Scream()
    {
        if (Time.time < nextScream || !Look.Visible) return;
        while (recentScreams.Count > 0 && Time.time - recentScreams.Peek() > 1f) recentScreams.Dequeue();
        if (recentScreams.Count >= ScreamsPerSecond) return;
        if (!AudioManager.TryPlayOn(e => e.npcScream, gameObject)) return;
        recentScreams.Enqueue(Time.time);
        nextScream = Time.time + ScreamCooldown;
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
        // Coming round, staff included: ambient, so it doesn't hold up "Objective Completed!".
        GameHud.Shout(transform, GameText.ComingRound[Random.Range(0, GameText.ComingRound.Length)], 2.2f, true);
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
        if (s != State.Riding) Boarding = false; // scared off, sent off or worse on the way up: not getting on after all
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
