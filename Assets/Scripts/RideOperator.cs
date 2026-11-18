using System.Collections.Generic;
using UnityEngine;

// The ride operator. His Visitor body can be possessed like anyone's (this script is switched off while it is).
// He runs the ride from his post and keeps an eye on its closed area. When the wheel loses power he calls the ride
// technician (RideTechnician), who doesn't pick up, and stands at his post at a loss. When the locker room catches
// fire (LockerRoom) he leaves his booth and runs over to it, and stays there gawping.
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public class RideOperator : MonoBehaviour
{
    // Operators going about their job (not possessed).
    public static readonly List<RideOperator> All = new();

    public enum State { AtPost, Returning, Calling, Stumped, AtFire }

    public Transform post;
    public FerrisWheel wheel;
    public float walkSpeed = 2f;
    public float callTime = 4f;      // ringing before he gives up

    public State Current { get; private set; }
    public bool AtPost => enabled && Current == State.AtPost;

    GroundWalker walker;
    CharacterSprite look;
    float stateEndsAt;
    Vector3 fire;

    // The locker room's burning: off he runs (quiet: loading a save, he's already there).
    public void GoToFire(Vector3 spot, bool quiet = false)
    {
        Set(State.AtFire);
        fire = spot;
        walker.speed = walkSpeed * 2f;
        look.running = true;
        if (!quiet) return;
        var rb = GetComponent<Rigidbody>();
        rb.position = transform.position = new Vector3(spot.x, transform.position.y, spot.z);
    }

    // Switched off while possessed; on release he picks up again from wherever the spirit left him.
    void OnEnable()
    {
        All.Add(this);
        if (walker == null) return; // first enable, before Awake has run
        if (Current is State.Calling or State.Stumped) look.pose = look.think;
        else if (Current != State.AtFire) Set(State.Returning); // at the fire: Update takes him back to it
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
        if (!wheel.Powered && Current is State.AtPost or State.Returning)
        {
            Set(State.Calling);
            stateEndsAt = Time.time + callTime;
            look.pose = look.think;
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

            case State.AtFire:
                if (WalkTo(fire)) break;
                look.running = false;
                look.pose = look.think;
                break;

            case State.Calling:
                WalkTo(post.position);
                if (Time.time < stateEndsAt) break;
                foreach (var t in RideTechnician.All) t.Ring();
                Mischief.Complete(Mischief.Task.CallTechnician);
                Set(State.Stumped);
                look.pose = look.think;
                break;
        }
    }

    void Set(State s)
    {
        Current = s;
        walker.speed = walkSpeed;
        look.pose = null;
    }

    bool WalkTo(Vector3 p) => walker.WalkTo(p);
}
