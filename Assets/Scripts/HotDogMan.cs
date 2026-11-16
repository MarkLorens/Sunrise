using System.Collections.Generic;
using UnityEngine;

// The hot dog man, behind his stand, guarding the propane tank's fan from anyone who'd touch it. Rats out of the gutter
// (Gutter) send him running; he comes back once they're gone and fires up his stove, which is the spark the propane
// cloud is waiting for (PropaneTank). After that he stands there charred and dazed.
// ponytail: can't be possessed, or you'd just walk him away from the fan and skip the rats.
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public class HotDogMan : MonoBehaviour
{
    public static readonly List<HotDogMan> All = new();

    public enum State { AtStall, Fleeing, Returning, Charred }

    public float awayTime = 25f;   // seconds he stays away from the rats
    public float fleeDistance = 9f;
    public float guardRadius = 12f; // how far from his stall he keeps an eye on the fan

    public State Current { get; private set; }
    // Back behind his stand, watching his fan.
    public bool Guarding => Current == State.AtStall;

    GroundWalker walker;
    CharacterSprite look;
    Vector3 post, away;
    float backAt, stoveAt;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Awake()
    {
        walker = GetComponent<GroundWalker>();
        look = GetComponent<CharacterSprite>();
        if (!TryGetComponent(out Visitor body)) body = gameObject.AddComponent<Visitor>(); // loads the sprite
        body.brain = this;
        body.possessable = false;
        walker.speed = 2f;
        post = transform.position;
        stoveAt = float.MaxValue;
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
        Current = State.Fleeing;
        walker.speed = 4.5f;
        look.running = true;
        look.pose = null;
        backAt = Time.time + awayTime;
        GameHud.Shout(transform, GameText.HotDogRats, 2f);
    }

    // Blown up (PropaneTank): blackened, dazed, done cooking for the day.
    public void Char()
    {
        Current = State.Charred;
        walker.move = Vector2.zero;
        look.running = false;
        look.pose = look.think;
        look.SetTint(new Color(0.3f, 0.27f, 0.25f));
    }

    void Update()
    {
        switch (Current)
        {
            case State.AtStall:
                if (walker.WalkTo(post)) break;
                look.Face(post + Vector3.back);
                // The cloud's waiting for a spark: he lights his stove a moment after he's back at it.
                if (PropaneTank.CloudReady == null) { stoveAt = float.MaxValue; break; }
                if (stoveAt == float.MaxValue)
                {
                    stoveAt = Time.time + 2f;
                    GameHud.Shout(transform, GameText.HotDogStove, 1.8f);
                }
                if (Time.time >= stoveAt) PropaneTank.CloudReady.Explode(this);
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
                if (!walker.WalkTo(post)) Current = State.AtStall;
                break;

            case State.Charred:
                if (Random.value < Time.deltaTime * 2f) Fx.Burst("Fx/puff", transform.position + Vector3.up * 1.6f, 1, new Color(0.25f, 0.25f, 0.25f), 0.8f, 0.3f, 1f);
                break;
        }
    }
}
