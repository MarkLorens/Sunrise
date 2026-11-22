using System.Collections.Generic;
using UnityEngine;

// The park's clean-up crew. They put the park back the way it was after a mess: mopping up the drinking fountain's
// puddle, and standing guard at the scene of anything worse (Aftermath), telling the gawkers to move along
// (GameText.MoveAlong, in turns) and shooing off anyone who gets too close. Anything else that needs resetting is
// just another ResetAgent.Call.
public class ResetAgent : StaffWorker
{
    public static readonly List<ResetAgent> All = new();
    static readonly Queue<Job> backlog = new();

    public float shoutRadius = 5f;      // visitors this close get told
    public float shooRadius = 2.5f;     // and this close actually move along
    public Vector2 shoutEvery = new(4f, 7f);

    protected override Queue<Job> Backlog => backlog;

    float nextShout;
    int nextLine;

    // The nearest free agent takes it; with everyone busy it waits for the first one done.
    public static bool Call(Job job) => Dispatch(All, backlog, job);

    // Stand watch over a scene from `keepBack` away, on the side facing the middle of the park.
    public static void Guard(Vector3 scene, float keepBack)
    {
        Vector3 toPark = -scene;
        toPark.y = 0f;
        if (toPark.sqrMagnitude < 0.01f) toPark = Vector3.back;
        Vector3 spot = scene + toPark.normalized * keepBack;
        for (int i = 0; i < 8 && GroundWalker.NearObstacle(spot, 0.4f); i++)
            spot = scene + Quaternion.Euler(0f, (i + 1) * 45f, 0f) * toPark.normalized * keepBack;
        Call(new Job { spot = spot, at = scene, seconds = 0.5f, stay = true });
    }

    protected override void Update()
    {
        base.Update();
        if (Current != State.Staying || Time.time < nextShout) return;
        nextShout = Time.time + Random.Range(shoutEvery.x, shoutEvery.y);

        bool anyone = false;
        foreach (var v in Visitor.All)
        {
            if (v.brain != null || !v.Look.Visible) continue;
            Vector3 d = v.transform.position - transform.position;
            float dist = new Vector2(d.x, d.z).magnitude;
            if (dist > shoutRadius) continue;
            anyone = true;
            if (dist <= shooRadius) v.MoveAlong(this);
        }
        if (!anyone) return;
        var lines = GameText.MoveAlong;
        GameHud.Shout(transform, lines[nextLine++ % lines.Length], 2f, true);
    }

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
