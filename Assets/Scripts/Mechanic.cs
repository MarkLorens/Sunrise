using System.Collections.Generic;
using UnityEngine;

// The park mechanic, keys to the Ferris wheel's electrical box on his belt. He only comes out from behind the staff
// door when something's broken and the fuss is big enough (Mechanic.Call), fixes it, and goes back inside: the
// moment to possess him and use his keys.
public class Mechanic : StaffWorker
{
    // Mechanics going about their job (not possessed, not dead).
    public static readonly List<Mechanic> All = new();
    static readonly Queue<Job> backlog = new();

    protected override Queue<Job> Backlog => backlog;

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
}
