using System.Collections.Generic;
using UnityEngine;

// The hot dog man, behind his stand, guarding the propane tank's fan from anyone who'd touch it. Rats out of the gutter
// (Gutter) send him running; he comes back once they're gone and fires up his stove, which is the spark the propane
// cloud is waiting for (PropaneTank). After that he stands there charred and dazed.
// He works inside his stand, between the counter and the back wall, out of reach: he can only be possessed while he's
// out of it (chased off, or charred). The gap's too tight to walk in and out of, so he hops the end of the counter
// (Hop) to and from a spot just outside it (exitOffset). His stand is one solid box, counter to back wall (Awake), so
// nobody slips in at the ends; behind the counter he potters along it (paceHalfWidth) with ignoreObstacles.
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public class HotDogMan : MonoBehaviour
{
    public static readonly List<HotDogMan> All = new();

    public enum State { AtStall, Hopping, Fleeing, Returning, Charred }

    public float awayTime = 10f;   // seconds he stays away from the rats
    public float fleeDistance = 9f;
    public float guardRadius = 12f; // how far from his stall he keeps an eye on the fan
    public Vector3 exitOffset = new(3.2f, 0f, 0f); // where he lands hopping out of the stall, relative to his post
    public float hopTime = 0.45f;
    public float hopHeight = 0.8f;
    public float paceHalfWidth = 1.4f; // how far either side of his post he wanders behind the counter

    public State Current { get; private set; }
    // Back behind his stand, watching his fan.
    public bool Guarding => Current == State.AtStall;

    GroundWalker walker;
    Visitor body;
    CharacterSprite look;
    Rigidbody rb;
    Vector3 post, away;
    float backAt, stoveAt;
    float hopStart;
    Vector3 hopFrom, hopTo;
    State afterHop;
    Vector3 pacingTo;
    float nextPace;

    Vector3 Exit => post + exitOffset;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Awake()
    {
        walker = GetComponent<GroundWalker>();
        rb = GetComponent<Rigidbody>();
        look = GetComponent<CharacterSprite>();
        if (!TryGetComponent(out Visitor body)) body = gameObject.AddComponent<Visitor>(); // loads the sprite
        body.brain = this;
        this.body = body;
        walker.speed = 2f;
        post = transform.position;
        pacingTo = post;
        stoveAt = float.MaxValue;
        BlockStand();
    }

    // The counter and back wall are separate thin colliders with a gap between them (his spot): fill it in, one box
    // over both, so nobody walks in at the ends.
    void BlockStand()
    {
        Bounds? stand = null;
        foreach (var c in FindObjectsByType<Collider>())
        {
            if (c.isTrigger || c.attachedRigidbody != null || c.TryGetComponent<UnityEngine.Tilemaps.Tilemap>(out _)) continue; // not the floor
            Bounds b = c.bounds;
            if (b.size.x > 8f) continue; // something far bigger than a stall
            if (post.x < b.min.x || post.x > b.max.x || Mathf.Abs(b.center.z - post.z) > 1.2f) continue; // not his stand
            if (stand is Bounds s) { s.Encapsulate(b); stand = s; }
            else stand = b;
        }
        if (stand is Bounds all)
            GroundWalker.Block("Hot Dog Stand Block", Rect.MinMaxRect(all.min.x, all.min.z, all.max.x, all.max.z));
    }

    // Rats! Off he runs, away from `from`, and stays away for a while.
    public void Scare(Vector3 from)
    {
        if (Current is State.Fleeing or State.Charred) return;
        Vector3 dir = transform.position - from;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.right;
        away = post;
        for (int i = 0; i < 12; i++)
        {
            Vector3 spot = post + Quaternion.Euler(0f, Random.Range(-60f, 60f), 0f) * dir.normalized * fleeDistance;
            if (walker.IsWalkable(spot)) { away = spot; break; }
        }
        walker.speed = 4.5f;
        look.running = true;
        look.pose = null;
        backAt = Time.time + awayTime;
        GameHud.Shout(transform, GameText.HotDogRats, 2f);
        look.Expression("surprised_1", awayTime);
        Hop(Exit, State.Fleeing); // over the end of the counter, then off he runs
    }

    // A quick arc from where he is to `to`, straight over whatever's in the way, then on to `next`.
    void Hop(Vector3 to, State next)
    {
        Current = State.Hopping;
        walker.move = Vector2.zero;
        walker.ignoreObstacles = false; // only inside, behind the counter
        hopFrom = transform.position;
        hopTo = new Vector3(to.x, hopFrom.y, to.z);
        hopStart = Time.time;
        afterHop = next;
        look.Cheer(hopTime);
        Sfx.Play(Sfx.Grab, 0.4f, 0.8f);
    }

    // Blown up (PropaneTank): blackened, dazed, done cooking for the day.
    public void Char()
    {
        Current = State.Charred;
        walker.move = Vector2.zero;
        if (Current == State.Hopping) Place(hopTo);
        look.running = false;
        look.pose = look.think;
        look.SetTint(new Color(0.3f, 0.27f, 0.25f));
        look.Expression("surprised_2", 0f); // dazed, for good
    }

    void Update()
    {
        body.possessable = Current != State.AtStall;
        switch (Current)
        {
            case State.AtStall:
                Pace(); // up and down behind the counter
                // The cloud's waiting for a spark: he lights his stove a moment after he's back at it.
                var tank = PropaneTank.ReadyNear(post, 2f);
                if (tank == null) { stoveAt = float.MaxValue; break; }
                if (stoveAt == float.MaxValue)
                {
                    stoveAt = Time.time + 2f;
                    GameHud.Shout(transform, GameText.HotDogStove, 1.8f);
                }
                if (Time.time >= stoveAt) tank.Explode();
                break;

            case State.Hopping:
                float k = Mathf.Clamp01((Time.time - hopStart) / hopTime);
                Place(Vector3.Lerp(hopFrom, hopTo, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * hopHeight);
                if (k < 1f) break;
                Current = afterHop;
                walker.ignoreObstacles = afterHop == State.AtStall;
                break;

            case State.Fleeing:
                walker.WalkTo(away);
                if (Time.time < backAt) break;
                Current = State.Returning;
                walker.speed = 2f;
                look.running = false;
                GameHud.Shout(transform, GameText.HotDogBack, 2.5f);
                break;

            case State.Returning:
                if (!walker.WalkTo(Exit)) Hop(post, State.AtStall); // back over the counter
                break;

            case State.Charred:
                if (Random.value < Time.deltaTime * 2f) Fx.Burst("Fx/puff", transform.position + Vector3.up * 1.6f, 1, new Color(0.25f, 0.25f, 0.25f), 0.8f, 0.3f, 1f);
                break;
        }
    }

    // Over to a spot along the counter, a pause there, then another.
    void Pace()
    {
        walker.ignoreObstacles = true;
        Vector3 to = pacingTo - transform.position;
        to.y = 0f;
        if (to.magnitude > 0.08f)
        {
            walker.speed = 1.2f;
            walker.move = new Vector2(to.x, to.z).normalized;
            return;
        }
        walker.move = Vector2.zero;
        if (Time.time < nextPace) return;
        nextPace = Time.time + Random.Range(2f, 5f);
        pacingTo = post + Vector3.right * Random.Range(-paceHalfWidth, paceHalfWidth);
    }

    void Place(Vector3 p) => rb.position = transform.position = p;
}
