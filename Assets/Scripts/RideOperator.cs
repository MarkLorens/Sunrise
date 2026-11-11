using System.Collections.Generic;
using UnityEngine;

// The ride operator. His Visitor body can be possessed like anyone's (this script is switched off while it is).
// He runs the ride from his post and keeps an eye on its closed area. A brawl nearby pulls him off his post for a
// while to break it up. When the wheel loses power he calls the ride technician (RideTechnician), who doesn't pick up,
// and stands at his post at a loss.
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public class RideOperator : MonoBehaviour
{
    // Operators going about their job (not possessed).
    public static readonly List<RideOperator> All = new();

    public enum State { AtPost, Returning, Attending, Calling, Stumped }

    public Transform post;
    public FerrisWheel wheel;
    public float walkSpeed = 2f;
    public float runSpeed = 4f;
    public float attendRadius = 15f; // how far away a brawl pulls him off his post
    public float attendTime = 15f;   // how long he stays after it's over, seeing to whoever got hurt
    public float callTime = 4f;      // ringing before he gives up

    public State Current { get; private set; }
    public bool AtPost => enabled && Current == State.AtPost;
    public bool BreakingUpFight => Current == State.Attending;

    GroundWalker walker;
    CharacterSprite look;
    float stateEndsAt;
    Vector3 brawlAt, attendSpot;

    // Switched off while possessed; on release he picks up again from wherever the spirit left him.
    void OnEnable()
    {
        All.Add(this);
        if (walker == null) return; // first enable, before Awake has run
        if (Current is State.Calling or State.Stumped) look.pose = look.think;
        else Set(State.Returning);
    }

    void OnDisable() => All.Remove(this);

    void Awake()
    {
        walker = GetComponent<GroundWalker>();
        look = GetComponent<CharacterSprite>();
        if (!TryGetComponent(out Visitor body)) body = gameObject.AddComponent<Visitor>(); // loads the sprite
        body.brain = this;
        walker.speed = walkSpeed;
    }

    void Update()
    {
        if (!wheel.Powered && Current is State.AtPost or State.Returning or State.Attending)
        {
            Set(State.Calling);
            stateEndsAt = Time.time + callTime;
            look.pose = look.think;
            look.Show("question", callTime);
            GameHud.Say("Operator: The power's gone?! Hang on, folks! *calls the ride technician*");
        }

        switch (Current)
        {
            case State.AtPost:
            case State.Stumped:
                WalkTo(post.position);
                break;

            case State.Returning:
                if (!WalkTo(post.position)) Set(State.AtPost);
                break;

            case State.Calling:
                WalkTo(post.position);
                if (Time.time < stateEndsAt) break;
                foreach (var t in RideTechnician.All) t.Ring();
                GameHud.Say("Operator: ...He's not picking up. Sorry folks, the ride's stuck!");
                Mischief.Complete(Mischief.Task.CallTechnician);
                Set(State.Stumped);
                look.pose = look.think;
                break;

            case State.Attending:
                if (!WalkTo(attendSpot)) look.Face(brawlAt);
                if (Time.time < stateEndsAt) break;
                GameHud.Say("Operator: Somebody call first aid... I've got a ride to run.");
                Set(State.Returning);
                break;
        }
    }

    // A brawl nearby: he leaves his post to break it up, then sees to whoever got hurt.
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
        GameHud.Say("Operator: Hey! HEY! Break it up, you two!");
    }

    void Set(State s)
    {
        Current = s;
        walker.speed = s == State.Attending ? runSpeed : walkSpeed;
        look.running = s == State.Attending;
        look.pose = null;
    }

    bool WalkTo(Vector3 p) => walker.WalkTo(p);
}
