using System.Collections.Generic;
using UnityEngine;

// The ride operator. His Visitor body can be possessed like anyone's (this script is switched off while it is).
// Back at his post he puts an overdriven wheel back to normal (unless the panel's fried) and keeps an eye on the
// ride's closed area. A brawl nearby pulls him off his post for a while to break it up.
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public class RideOperator : MonoBehaviour
{
    // Operators going about their job (not possessed).
    public static readonly List<RideOperator> All = new();

    public enum State { AtPost, Returning, Shocked, Attending }

    public Transform post;
    public FerrisWheel wheel;
    public float walkSpeed = 2f;
    public float runSpeed = 4f;
    public float watchRadius = 3f; // how close he has to be to notice someone at the panel
    public float attendRadius = 15f; // how far away a brawl pulls him off his post
    public float attendTime = 15f;   // how long he stays after it's over, seeing to whoever got hurt

    public State Current { get; private set; }
    public bool AtPost => enabled && Current == State.AtPost;
    public bool BreakingUpFight => Current == State.Attending;
    public bool WatchingPanel => enabled && (Current == State.AtPost ||
                                 (Current == State.Returning && Vector3.Distance(transform.position, post.position) < watchRadius));

    GroundWalker walker;
    CharacterSprite look;
    float stateEndsAt;
    bool friedNoticed;
    Vector3 brawlAt, attendSpot;

    // Switched off while possessed; on release he picks up again from wherever the spirit left him.
    void OnEnable()
    {
        All.Add(this);
        if (walker == null) return; // first enable, before Awake has run
        if (Current != State.Shocked) Set(State.Returning);
        else
        {
            look.pose = look.think;
            look.SetIcon("Panic");
        }
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
        if (Current == State.Shocked && !wheel.IsBroken)
        {
            // The wheel's been rebuilt: back to work.
            look.pose = null;
            look.SetIcon(null);
            Set(State.Returning);
        }
        if (wheel.IsBroken && Current is State.AtPost or State.Returning or State.Attending)
        {
            Set(State.Shocked);
            walker.move = Vector2.zero;
            look.pose = look.think;
            look.SetIcon("Panic");
        }

        switch (Current)
        {
            case State.AtPost:
                WalkTo(post.position);
                FixWheel();
                break;

            case State.Returning:
                if (WalkTo(post.position)) break;
                Set(State.AtPost);
                FixWheel();
                break;

            case State.Attending:
                if (!WalkTo(attendSpot)) look.Face(brawlAt);
                if (Time.time < stateEndsAt) break;
                GameHud.Say("Operator: Somebody call first aid... I've got a ride to run.");
                Set(State.Returning);
                break;
        }
    }

    void FixWheel()
    {
        if (!wheel.PanelFried) friedNoticed = false;
        if (!wheel.IsOverdrive || friedNoticed) return;
        look.Gesture(look.interact, 1f);
        if (wheel.PanelFried)
        {
            friedNoticed = true; // nothing he can do about it now
            GameHud.Say("Operator: It won't slow down! Who fried the panel?!");
            return;
        }
        wheel.ResetToNormal();
        GameHud.Say("Operator: Whoa, who touched this?! *click* ...Back to normal.");
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
