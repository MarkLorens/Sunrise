using System.Collections.Generic;
using UnityEngine;

// Park staff who wait out of sight behind the staff door until they're called out to a job. They walk over, work on
// it for a while (a bar over their head shows how long), then take the next job waiting for their crew or go back
// inside. While they're out their Visitor body can be possessed like anyone's (this script is switched off while it
// is); on release they go back to the job they were on.
// Each crew (Mechanic, ResetAgent) keeps its own roster and backlog and calls Dispatch to hand out jobs, so a new
// use for a crew is just a new Job sent its way.
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public abstract class StaffWorker : MonoBehaviour
{
    public enum State { Inside, Going, Working, Returning }

    // Somewhere to go, something to do there, and what happens when it's done.
    public struct Job
    {
        public Vector3 spot;          // where they stand to work
        public Vector3 at;            // what they face while working
        public float seconds;
        public string shout;          // said when they set off (null = nothing)
        public System.Action done;
    }

    public float walkSpeed = 2.2f;
    public float runSpeed = 3.6f;

    public State Current { get; private set; }
    public bool Free => enabled && Current is State.Inside or State.Returning;

    protected GroundWalker walker;
    protected CharacterSprite look;
    protected Vector3 home;

    Job job;
    bool hasJob;
    float workEndsAt;
    WorldBar bar;

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
        bar = WorldBar.Create(transform, Vector3.up * (look.HeadHeight / transform.lossyScale.y + 0.45f));
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

    protected virtual void OnDisable() => bar.Set(0f, Color.white, false);

    // Straight to this one, skipping the crew's queue (the electrical box's cutscene).
    public void Take(Job j)
    {
        job = j;
        hasJob = true;
        Set(State.Going);
    }

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
                float left = workEndsAt - Time.time;
                bar.Set(1f - left / job.seconds, new Color(0.6f, 1f, 0.92f), true);
                if (left > 0f) break;
                bar.Set(0f, Color.white, false);
                hasJob = false;
                job.done?.Invoke();
                NextJob();
                break;

            case State.Returning:
                if (!walker.WalkTo(home)) Set(State.Inside);
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
        if (s != State.Working) bar.Set(0f, Color.white, false);
    }
}
