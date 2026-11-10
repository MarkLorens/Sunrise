using System.Collections.Generic;
using UnityEngine;

// The park's clean-up crew. They put the park back the way it was after a mess: for now, mopping up the drinking
// fountain's puddle. Anything else that needs resetting is just another ResetAgent.Call.
public class ResetAgent : StaffWorker
{
    public static readonly List<ResetAgent> All = new();
    static readonly Queue<Job> backlog = new();

    protected override Queue<Job> Backlog => backlog;

    // The nearest free agent takes it; with everyone busy it waits for the first one done.
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
