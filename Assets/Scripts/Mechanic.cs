using System.Collections.Generic;
using UnityEngine;

// The park mechanic, screwdriver in hand. Waits at his spot until someone makes a racket at the Ferris wheel's
// electrical box, then walks over and pokes at it for a while (a good moment to possess him: as staff he can walk
// into the ride's closed area and loosen the support). A brawl nearby pulls
// him away to break it up. When the wheel is wrecked he goes over to keep people back until the chaos dies down.
// His Visitor body can be possessed like anyone's (this script is switched off while it is).
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public class Mechanic : MonoBehaviour
{
    // Mechanics going about their job (not possessed, not dead).
    public static readonly List<Mechanic> All = new();

    public enum State { AtPost, GoingToBox, Inspecting, Returning, Securing, Attending }

    public FerrisWheel wheel;
    public float walkSpeed = 2.2f;
    public float runSpeed = 3.6f;
    public float inspectTime = 10f;
    public float secureDistance = 6f; // how far from the wreck he stands to keep people back
    public float attendRadius = 15f;  // how far away a brawl pulls him off his post
    public float attendTime = 15f;    // how long he stays after it's over, seeing to whoever got hurt

    public State Current { get; private set; }
    public bool BreakingUpFight => Current == State.Attending;

    // Someone's already on the way to (or looking at) the electrical box.
    public static bool Busy => All.Exists(m => m.Current is State.GoingToBox or State.Inspecting);

    GroundWalker walker;
    CharacterSprite look;
    Vector3 home;
    Vector3 boxSpot;
    Vector3 brawlAt, attendSpot;
    float stateEndsAt;

    void Awake()
    {
        walker = GetComponent<GroundWalker>();
        look = GetComponent<CharacterSprite>();
        if (!TryGetComponent(out Visitor body)) body = gameObject.AddComponent<Visitor>(); // loads the sprite
        body.brain = this;
        home = transform.position;
    }

    // Switched off while possessed; on release he heads back to his spot.
    void OnEnable()
    {
        All.Add(this);
        Set(State.Returning);
    }

    void OnDisable() => All.Remove(this);

    // The racket at the electrical box: the nearest free mechanic comes to look. False if nobody's coming.
    public static bool Lure(Vector3 box)
    {
        Mechanic best = null;
        foreach (var m in All)
            if (m.Current is State.AtPost or State.Returning &&
                (best == null || Vector3.Distance(m.transform.position, box) < Vector3.Distance(best.transform.position, box)))
                best = m;
        if (best == null) return false;
        best.boxSpot = box + Vector3.back;
        best.Set(State.GoingToBox);
        best.look.Show("exclaim", 1.5f);
        GameHud.Say($"{best.name}: Hey! Who's banging on the electrical box?!");
        return true;
    }

    void Update()
    {
        if (wheel != null && wheel.InChaos && Current != State.Securing)
        {
            Set(State.Securing);
            GameHud.Say($"{name}: Stay back! Everybody stay back!");
        }

        switch (Current)
        {
            case State.GoingToBox:
                if (WalkTo(boxSpot)) break;
                Set(State.Inspecting);
                stateEndsAt = Time.time + inspectTime;
                look.pose = look.think;
                look.Show("question", inspectTime);
                break;

            case State.Inspecting:
                if (Time.time < stateEndsAt) break;
                GameHud.Say($"{name}: Huh. Looks fine to me...");
                Set(State.Returning);
                break;

            case State.Returning:
                if (!WalkTo(home)) Set(State.AtPost);
                break;

            case State.Attending:
                if (!WalkTo(attendSpot)) look.Face(brawlAt);
                if (Time.time >= stateEndsAt) Set(State.Returning);
                break;

            case State.Securing:
                if (!wheel.InChaos)
                {
                    Set(State.Returning);
                    break;
                }
                // Stand between the wreck and the rest of the park, facing it.
                Vector3 wreck = wheel.WreckPoint;
                Vector3 away = home - wreck;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = Vector3.back;
                if (!WalkTo(wreck + away.normalized * secureDistance)) look.Face(wreck);
                break;
        }
    }

    // A brawl nearby: he goes over to break it up, then sees to whoever got hurt.
    public void Attend(Vector3 brawl, float over)
    {
        if (Current is not (State.AtPost or State.Returning)) return;
        Vector3 away = transform.position - brawl;
        away.y = 0;
        if (away.magnitude > attendRadius) return;
        if (away.sqrMagnitude < 0.01f) away = Vector3.back;
        brawlAt = brawl;
        attendSpot = brawl + away.normalized * 1.8f;
        Set(State.Attending);
        stateEndsAt = over + attendTime;
        look.Show("exclaim", 1.5f);
        GameHud.Say($"{name}: Whoa, whoa! Break it up!");
    }

    void Set(State s)
    {
        Current = s;
        walker.move = Vector2.zero;
        walker.speed = s is State.GoingToBox or State.Securing or State.Attending ? runSpeed : walkSpeed;
        look.running = s == State.Securing;
        look.pose = null;
    }

    bool WalkTo(Vector3 p) => walker.WalkTo(p);
}
