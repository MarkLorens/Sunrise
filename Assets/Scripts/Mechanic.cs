using System.Collections.Generic;
using UnityEngine;

// The park mechanic, screwdriver in hand. Waits at his spot until someone makes a racket at the Ferris wheel's
// electrical box, then walks over and pokes at it for a while (a good moment to possess him). When the wheel is
// wrecked he goes over to keep people back until the chaos dies down. His Visitor body can be possessed like
// anyone's (this script is switched off while it is).
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public class Mechanic : MonoBehaviour
{
    // Mechanics going about their job (not possessed, not dead).
    public static readonly List<Mechanic> All = new();

    public enum State { AtPost, GoingToBox, Inspecting, Returning, Securing }

    public FerrisWheel wheel;
    public float walkSpeed = 2.2f;
    public float runSpeed = 3.6f;
    public float inspectTime = 10f;
    public float secureDistance = 6f; // how far from the wreck he stands to keep people back

    public State Current { get; private set; }

    // Someone's already on the way to (or looking at) the electrical box.
    public static bool Busy => All.Exists(m => m.Current is State.GoingToBox or State.Inspecting);

    GroundWalker walker;
    CharacterSprite look;
    Vector3 home;
    Vector3 boxSpot;
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

    void Set(State s)
    {
        Current = s;
        walker.move = Vector2.zero;
        walker.speed = s is State.GoingToBox or State.Securing ? runSpeed : walkSpeed;
        look.running = s == State.Securing;
        look.pose = null;
    }

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
}
