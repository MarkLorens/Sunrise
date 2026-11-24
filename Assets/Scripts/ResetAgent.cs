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
    public static bool Call(Job job) => Dispatch(All, backlog, job) != null;

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

    // The scene's cleared: whoever's watching it (or still about to) goes back to work or inside.
    public static void StandDown(Vector3 scene)
    {
        scene.y = 0f;
        for (int i = backlog.Count; i > 0; i--)
        {
            var j = backlog.Dequeue();
            if (!(j.stay && Same(j.at, scene))) backlog.Enqueue(j);
        }
        foreach (var a in All) a.LeavePost(scene);
    }

    bool taped;
    Vector3 tapedAt;

    protected override void Update()
    {
        base.Update();
        Tape();
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

    // Construction tape round whatever they're at while they're at it (ConstructionTape): out at the police line round a
    // scene they're guarding (a stay job: they stand on it), close round anything else.
    const float TapeIn = 0.4f; // a scene's tape stands this far inside its police line (they guard on the line)

    void Tape()
    {
        bool there = OnSite(out var j);
        Vector3 site = new(j.at.x, 0f, j.at.z);
        if (there && Aftermath.Around(site, out Vector3 scene, out float keepBack))
        {
            // A scene: taped off once, staying up as they go from job to job in it, till it's cleaned up.
            ConstructionTape.Scene(scene, Mathf.Max(1f, keepBack - TapeIn));
            if (taped) ConstructionTape.TakeDown(tapedAt);
            taped = false;
            return;
        }
        if (taped && (!there || !Same(site, tapedAt)))
        {
            ConstructionTape.TakeDown(tapedAt);
            taped = false;
        }
        if (!there || taped) return;
        Vector3 d = j.spot - j.at;
        ConstructionTape.Put(site, j.stay ? new Vector2(d.x, d.z).magnitude : 0f);
        taped = true;
        tapedAt = site;
    }

    protected override void OnEnable()
    {
        All.Add(this);
        base.OnEnable();
    }

    protected override void OnDisable()
    {
        All.Remove(this);
        if (taped) ConstructionTape.TakeDown(tapedAt); // possessed, or gone: not holding a ring up
        taped = false;
        base.OnDisable();
    }
}
