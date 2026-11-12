using System.Collections.Generic;
using UnityEngine;

// The park mechanic, keys to the Ferris wheel's electrical box on his belt. He only comes out from behind the staff
// door when something's broken and the fuss is big enough (Mechanic.Call), fixes it, and goes back inside: the
// moment to possess him and use his keys. A brawl near where he's working pulls him away to break it up.
public class Mechanic : StaffWorker
{
    // Mechanics going about their job (not possessed, not dead).
    public static readonly List<Mechanic> All = new();
    static readonly Queue<Job> backlog = new();

    public float attendRadius = 15f;  // how far away a brawl pulls him off what he's doing
    public float attendTime = 15f;    // how long he stays after it's over, seeing to whoever got hurt

    public bool BreakingUpFight => Current == State.Attending;

    protected override Queue<Job> Backlog => backlog;

    Vector3 brawlAt, attendSpot;
    float attendEndsAt;

    // Something broken: the nearest free mechanic comes to fix it. False if they're all busy (it waits its turn).
    public static bool Call(Job job) => Dispatch(All, backlog, job);

    protected override void OnEnable()
    {
        All.Add(this);
        base.OnEnable();
    }

    protected override void OnDisable()
    {
        All.Remove(this);
        base.OnDisable();
    }

    protected override void Update()
    {
        if (Current != State.Attending)
        {
            base.Update();
            return;
        }
        if (!walker.WalkTo(attendSpot)) look.Face(brawlAt);
        if (Time.time < attendEndsAt) return;
        Resume();
    }

    // A brawl nearby: he goes over to break it up, then sees to whoever got hurt. Not from behind the staff door.
    public void Attend(Vector3 brawl, float over)
    {
        if (Current is State.Inside or State.Attending) return;
        Vector3 away = transform.position - brawl;
        away.y = 0;
        if (away.magnitude > attendRadius) return;
        if (away.sqrMagnitude < 0.01f) away = Vector3.back;
        brawlAt = brawl;
        attendSpot = brawl + away.normalized * 1.8f;
        Set(State.Attending);
        attendEndsAt = over + attendTime;
    }
}
