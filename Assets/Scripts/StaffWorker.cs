using System.Collections.Generic;
using UnityEngine;

// Park staff who wait out of sight behind the staff door until they're called out to a job. They walk over, work on
// it for a while (tinkering away: Tinker), then take the next job waiting for their crew or go back
// inside (or, after a Job.stay one, back to it to hang around). While they're out their Visitor body can be
// possessed like anyone's (this script is switched off while it is); on release they go back to the job they were on.
// Each crew (Mechanic, ResetAgent) keeps its own roster and backlog and calls Dispatch to hand out jobs, so a new
// use for a crew is just a new Job sent its way.
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public abstract class StaffWorker : MonoBehaviour
{
    public enum State { Inside, Going, Working, Returning, Staying }

    // Somewhere to go, something to do there, and what happens when it's done.
    public struct Job
    {
        public Vector3 spot;          // where they stand to work
        public Vector3 at;            // what they face while working
        public float seconds;
        public string shout;          // said when they set off (null = nothing)
        public System.Action done;
        public bool stay;             // hang around here afterwards instead of going back inside (still takes new jobs)
        public float roam;            // while hanging around: wander within this far of the spot (0 = stand still)
    }

    public float walkSpeed = 2.2f;
    public float runSpeed = 3.6f;
    public float tinkerBeat = 0.4f;  // seconds between taps while working

    public State Current { get; private set; }
    public bool Free => enabled && Current is State.Inside or State.Returning or State.Staying;

    protected GroundWalker walker;
    protected CharacterSprite look;
    protected Vector3 home;

    Job job;
    bool hasJob;
    Job post;     // the job they stayed on at (Job.stay), if any
    bool hasPost;
    float workEndsAt, nextTap, roamAt;
    bool tapDown;
    Vector3 roamTo;

    // The crew's own queue of jobs nobody was free to take.
    protected abstract Queue<Job> Backlog { get; }

    // Hand a job to the nearest free member of a crew, or leave it for the next one who's free.
    protected static bool Dispatch<T>(List<T> crew, Queue<Job> backlog, Job job) where T : StaffWorker
    {
        T best = null;
        foreach (var w in crew)
            if (w.Free && (best == null || Distance(w, job.spot) < Distance(best, job.spot)))
                best = w;
        if (best == null)
        {
            backlog.Enqueue(job);
            return false;
        }
        best.Take(job);
        return true;
    }

    static float Distance(Component c, Vector3 p)
    {
        Vector3 d = c.transform.position - p;
        d.y = 0f;
        return d.magnitude;
    }

    protected virtual void Awake()
    {
        walker = GetComponent<GroundWalker>();
        look = GetComponent<CharacterSprite>();
        if (!TryGetComponent(out Visitor body)) body = gameObject.AddComponent<Visitor>(); // loads the sprite
        body.brain = this;
        home = transform.position;
        Set(State.Inside);
    }

    // Switched off while possessed; on release they get back to it.
    protected virtual void OnEnable()
    {
        if (Current == State.Inside && !hasJob) return; // just woke up, or nothing to get back to
        Resume();
    }

    // Back to the job they were on, the next one waiting, or back inside.
    protected void Resume()
    {
        if (hasJob) Set(State.Going);
        else NextJob();
    }

    protected virtual void OnDisable() { }

    // Straight to this one, skipping the crew's queue (the electrical box's cutscene).
    public void Take(Job j)
    {
        job = j;
        hasJob = true;
        Set(State.Going);
    }

    // No more standing watch at `at` (a stay job facing it): off to the next job or back inside.
    public void LeavePost(Vector3 at)
    {
        if (hasJob && job.stay && Same(job.at, at)) job.stay = false;
        if (!hasPost || !Same(post.at, at)) return;
        hasPost = false;
        if (Current == State.Staying) NextJob();
    }

    protected static bool Same(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 0.01f;

    void NextJob()
    {
        if (Backlog.Count > 0) Take(Backlog.Dequeue());
        else Set(State.Returning);
    }

    protected virtual void Update()
    {
        switch (Current)
        {
            case State.Going:
                if (walker.WalkTo(job.spot)) break;
                Set(State.Working);
                workEndsAt = Time.time + job.seconds;
                look.pose = look.think;
                break;

            case State.Working:
                look.Face(job.at);
                if (Time.time < workEndsAt)
                {
                    Tinker();
                    break;
                }
                hasJob = false;
                if (job.stay) (post, hasPost) = (job, true);
                job.done?.Invoke();
                NextJob();
                break;

            case State.Returning:
                if (walker.WalkTo(hasPost ? post.spot : home)) break;
                Set(hasPost ? State.Staying : State.Inside);
                break;

            case State.Staying:
                if (post.roam > 0f && walker.WalkTo(roamTo)) break; // strolling round the spot
                look.Face(post.at);
                if (post.roam <= 0f || Time.time < roamAt) break;
                roamAt = Time.time + Random.Range(2f, 5f);
                for (int i = 0; i < 6; i++)
                {
                    Vector2 r = Random.insideUnitCircle * post.roam;
                    Vector3 p = post.spot + new Vector3(r.x, 0f, r.y);
                    if (!walker.IsWalkable(p)) continue;
                    roamTo = p;
                    break;
                }
                break;
        }
    }

    protected void Set(State s)
    {
        Current = s;
        walker.move = Vector2.zero;
        walker.speed = s == State.Going ? runSpeed : walkSpeed;
        look.running = false;
        look.pose = null;
        look.SetVisible(s != State.Inside); // behind the staff door
        roamTo = transform.position;
        roamAt = Time.time + Random.Range(1f, 3f);
    }

    // Hard at it: hands going up and down on the beat, each tap kicking up a little dust (and a clank, if the player's
    // close enough to hear it) at whatever they're working on.
    void Tinker()
    {
        if (Time.time < nextTap) return;
        nextTap = Time.time + tinkerBeat * Random.Range(0.8f, 1.2f);
        tapDown = !tapDown;
        look.pose = tapDown ? look.interact : look.think;
        if (!tapDown) return;
        Vector3 hit = Vector3.Lerp(transform.position, job.at, 0.6f) + Vector3.up * 0.3f;
        Fx.Burst("Fx/puff", hit, 3, new Color(0.85f, 0.85f, 0.8f, 0.8f), 1.2f, 0.15f, 0.35f);
        if (Possession.PlayerNear(transform.position, 8f)) Sfx.Play(Sfx.HitSoft, 0.2f, Random.Range(1.2f, 1.6f));
    }
}
