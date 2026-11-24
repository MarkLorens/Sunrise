using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A leaky drinking fountain dripping into a puddle. Every press of its button (K, in anyone's body) makes the leak
// worse: the puddle starts small right under it and, press by press, slowly spreads out towards puddleOffset. Dip a live cable in it (LiveCable) and everyone standing in it is electrocuted.
// The scene's taped off (Aftermath) while Reset Agents carry the bodies away one by one and mop up. Enough of a scene
// and it blows the crowd speakers too: the music stops and the mechanic comes out to fix the speaker. Once every job's
// done the tape comes down, the music's back on, and it can all happen again.
[RequireComponent(typeof(Interactable))]
public class DrinkingFountain : MonoBehaviour, IInteraction
{
    public static readonly List<DrinkingFountain> All = new();

    public Vector3 puddleOffset = new(-1f, 0f, -1f); // where the puddle spreads from, relative to the fountain
    public float startRadius = 0.5f;
    public int tapsToFull = 4;       // presses from startRadius to maxRadius
    public float maxRadius = 4.2f;
    public int crowdToFlood = 3;     // listeners standing in it before it counts as reaching the crowd
    public int victimsToCallMechanic = 3; // how big a scene it takes before the mechanic bothers
    public float fixTime = 12f;      // how long the mechanic works on it: the window to possess him
    public float mopTime = 8f;
    public float bodyTime = 10f;     // a Reset Agent's time carrying off each body
    public float cleanupDelay = 3f;  // after the zap's played out (and the mechanic's shown up), before the clean-up crew comes
    public int payoffVictims = 5;    // more zapped than this and the camera cuts to it (Payoff)
    public float payoffPan = 1f;     // seconds for the camera to get there
    public float payoffTwitch = 2f;  // seconds the victims twitch
    public float pressCooldown = 3f; // before the button can be pressed again
    public float spreadSpeed = 0.45f; // how fast the water creeps out after a press (radius per second)
    public Sprite puddleArt;         // empty = a tinted round blob
    [Range(0f, 1f)] public float puddleOpacity = 0.6f;
    [Tooltip("Metres from the player: the leak is at full volume within the first number and fades out to silent at the second.")]
    public Vector2 leakRange = new(1f, 6f);

    // Under the fountain while small, sliding out to puddleOffset as it grows.
    public Vector3 PuddleCenter => new Vector3(transform.position.x, 0f, transform.position.z)
        + new Vector3(puddleOffset.x, 0f, puddleOffset.z) * Mathf.InverseLerp(startRadius, maxRadius, Radius);
    public float Radius { get; private set; }
    public bool Fixed { get; private set; } // the speaker, by the mechanic (at least once): he hangs around it after
    public bool Cleaning => jobsLeft > 0;

    static readonly Color Water = new(0.45f, 0.75f, 1f);

    SpriteRenderer puddle;
    bool mechanicCalled, nearLeak, electrocutedOnce;
    float nextDrip, readyAt, target; // target: the radius the water is still spreading to
    int presses, jobsLeft;
    Vector3 scene; // the taped-off puddle being cleaned up

    LoopingSound leak; // AL-03, until the mechanic fixes it
    LoopingSound drip; // the faucet dripping (FMODEvents.faucetDrip), alongside the leak until it's fixed
    float dripRange;   // how far off the drip is heard (the event's max distance): the caption shows within it

    void OnEnable() => All.Add(this);

    void OnDisable()
    {
        All.Remove(this);
        leak?.Stop();
        drip?.Stop();
    }

    void OnDestroy()
    {
        leak?.Release();
        drip?.Release();
    }

    void Start()
    {
        leak = new LoopingSound(gameObject);
        leak.SetRange(leakRange.x, leakRange.y);
        drip = new LoopingSound(gameObject);
        puddle = ParkArt.Make("Puddle", null, "Markers/shadow", 1f, new Color(Water.r, Water.g, Water.b, 0.55f), ParkArt.DecalOrder, true);
        if (puddleArt != null)
        {
            puddle.sprite = puddleArt;
            puddle.color = new Color(1f, 1f, 1f, puddleOpacity);
        }
        puddle.transform.parent.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f); // not the same blob every time
        SetRadius(target = startRadius);
    }

    void Update()
    {
        var events = FMODEvents.instance;
        if (Fixed || events == null) leak.Stop();
        else leak.Play(events.waterLeak);
        Drip(!Fixed && events != null && !events.faucetDrip.IsNull);

        if (Radius < target)
        {
            SetRadius(Mathf.MoveTowards(Radius, target, spreadSpeed * Time.deltaTime));
            if (VisitorsInPuddle() >= crowdToFlood) Mischief.Complete(Mischief.Task.FloodPuddle);
        }

        // Ruckus notices the leak whenever he comes into range, until someone has pressed the button (or it's fixed).
        // Leave and come back → another line. Standing still → only once per visit.
        bool inRange = !Fixed && presses == 0 && Possession.PlayerNear(transform.position, GetComponent<Interactable>().range);
        if (inRange && !nearLeak) GameHud.Think(GameText.PickFountainNear());
        nearLeak = inRange;

        if (Time.time < nextDrip) return;
        nextDrip = Time.time + 1.2f;
        Fx.Burst("Fx/sparkle", transform.position + Vector3.up * 0.9f, 2, Water, 1.2f, 0.18f, 0.5f);
    }

    // The faucet dripping the whole time it leaks, captioned while you're close enough to hear it.
    void Drip(bool on)
    {
        if (!on)
        {
            drip.Stop();
            return;
        }
        var sound = FMODEvents.instance.faucetDrip;
        if (dripRange <= 0f)
        {
            dripRange = 20f; // FMOD's default, if the event can't be looked up
            if (FMODUnity.RuntimeManager.StudioSystem.getEventByID(sound.Guid, out var description) == FMOD.RESULT.OK)
                description.getMinMaxDistance(out _, out dripRange);
        }
        drip.Play(sound);
        if (Ears.Near(transform.position, dripRange)) ClosedCaptions.Hold(Captions.FaucetDrip);
    }

    public bool InPuddle(Vector3 p)
    {
        Vector3 d = p - PuddleCenter;
        d.y = 0f;
        return d.magnitude <= Radius;
    }

    // How far from the puddle's edge this spot is (0 inside it).
    public float Gap(Vector3 p)
    {
        Vector3 d = p - PuddleCenter;
        d.y = 0f;
        return Mathf.Max(0f, d.magnitude - Radius);
    }

    void SetRadius(float r)
    {
        Radius = r;
        puddle.enabled = r > 0f;
        if (r > 0f) ParkArt.Fit(puddle, r * 2f);
        puddle.transform.parent.position = PuddleCenter + Vector3.up * 0.02f;
    }

    // ---------------------------------------------------------------- the button

    public string Prompt(Visitor host) => "Press the leaky fountain's button";

    public string Blocked(Visitor host)
    {
        if (Cleaning) return GameText.CleaningUp;
        if (target >= maxRadius) return GameText.PuddleFull;
        if (Time.time < readyAt) return GameText.FountainBusy;
        return null;
    }

    public void Interact(Visitor host)
    {
        readyAt = Time.time + pressCooldown;
        var lines = GameText.ThinkFountainPress;
        if (presses < lines.Length) GameHud.Think(GameHud.ThoughtPriority.Action, lines[presses]);
        presses++;
        target = Mathf.Min(maxRadius, Mathf.Max(target, startRadius) + (maxRadius - startRadius) / tapsToFull); // Update lets it creep out
        ClosedCaptions.Show(Captions.FountainPress);
        AudioManager.TryPlay(e => e.fountainPress, transform.position); // AL-02
        Fx.Burst("Fx/puff", transform.position + Vector3.up * 0.8f, 6, Water, 2f, 0.35f, 0.5f);
        GameHud.Say(GameText.PressedFountain);
    }

    int VisitorsInPuddle()
    {
        int n = 0;
        foreach (var v in Visitor.All)
            if (v.brain == null && v.Look.Visible && v.Current is not (Visitor.State.Down or Visitor.State.Riding) && InPuddle(v.transform.position))
                n++;
        return n;
    }

    // ---------------------------------------------------------------- a live cable in the water

    // The FMOD electrocution sound where the cable hit the water; the old one if that isn't set up.
    void PlayZap(Vector3 at)
    {
        ClosedCaptions.Show(Captions.Electrocution);
        var events = FMODEvents.instance;
        if (events != null && !events.electrocution.IsNull && AudioManager.instance != null) AudioManager.instance.PlayOneShot(events.electrocution, at);
    }

    public void Electrify(Vector3 from)
    {
        var zapped = new List<Visitor>();
        foreach (var v in Visitor.All)
            if (v.Look.Visible && v.Current is not (Visitor.State.Down or Visitor.State.Riding) && InPuddle(v.transform.position))
                zapped.Add(v);
        int victims = zapped.Count;

        // Big enough a scene and the mechanic comes out, but only once the zap's over (MechanicComes).
        bool big = victims >= victimsToCallMechanic, payoff = victims > payoffVictims;
        foreach (var v in zapped)
            if (payoff) v.Die(payoffPan, payoffTwitch, true);
            else v.Die(0f, 0f, true);
        if (!payoff) Zap(from, zapped, 0.3f);
        if (victims == 0) return;

        if (!payoff) ThinkElectrocuted(); // a payoff thinks it once it's over
        Mischief.Complete(Mischief.Task.Electrocute);
        jobsLeft++; // the clean-up crew's place, held until they're called: no dipping again meanwhile
        if (big)
        {
            Speaker.SetPlaying(false);
            jobsLeft++; // the mechanic's job, held open until he's called: the clean-up can't finish without him
        }
        StartCoroutine(Aftershock(from, zapped, payoff, big));
    }

    // The zap plays out (the payoff, or the mechanic coming out after a small one), then a moment later the clean-up
    // crew arrives.
    IEnumerator Aftershock(Vector3 from, List<Visitor> zapped, bool payoff, bool big)
    {
        if (payoff) yield return Payoff(from, zapped, big);
        else if (big) yield return AfterZap();
        yield return new WaitForSeconds(cleanupDelay);
        jobsLeft--; // the place Electrify held for the crew: their jobs count themselves
        CleanUp(zapped);
    }

    // Taped off until every job below is done (another agent guards the tape): each body carried off, the puddle mopped.
    void CleanUp(List<Visitor> zapped)
    {
        scene = PuddleCenter;
        Aftermath.Add(scene, Radius + 1f, float.PositiveInfinity);
        foreach (var v in zapped)
        {
            var body = v;
            Job(false, new StaffWorker.Job
            {
                spot = body.transform.position + Vector3.right * 0.7f,
                at = body.transform.position,
                seconds = bodyTime,
                done = () => CarryOff(body),
            });
        }
        Job(false, new StaffWorker.Job
        {
            spot = PuddleCenter,
            at = PuddleCenter + Vector3.back,
            seconds = mopTime,
            shout = "Clean-up on the fountain! Everybody out of the way!",
            done = Mop,
        });
    }

    // A small zap (no payoff): a beat for it to land, then the mechanic.
    IEnumerator AfterZap()
    {
        Possession.InputBlocked = true;
        yield return new WaitForSeconds(1f);
        yield return MechanicComes();
        CameraRig.EndSpotlight();
        yield return new WaitForSeconds(1f); // the camera easing back
        Possession.InputBlocked = false;
    }

    // Out he comes, the camera following him over to the blown speaker until he gets to work. The caller holds the
    // player's input and eases the camera back.
    IEnumerator MechanicComes()
    {
        jobsLeft--; // the place Electrify held for him: Job counts it again
        var m = CallMechanic(false);
        Mischief.Complete(Mischief.Task.LureMechanic);
        if (m == null) yield break; // busy (or possessed): he'll come when he can
        CameraRig.Spotlight(m.transform, 999f);
        CutsceneLight.On(m); // the park dark round him
        // His running steps (npcRun) while the camera follows him over; the game hears from the shot (Ears).
        var steps = m.GetComponent<Footsteps>();
        if (steps != null) steps.hurrying = true;
        float giveUp = Time.time + 10f; // in case something stops him on the way
        while (m.Current == StaffWorker.State.Going && Time.time < giveUp) yield return null;
        if (steps != null) steps.hurrying = false;
        yield return new WaitForSeconds(1f);
        CutsceneLight.Off();
    }

    // A clean-up job; the last one done takes the tape down and puts the music back on.
    StaffWorker Job(bool mechanic, StaffWorker.Job job)
    {
        jobsLeft++;
        var then = job.done;
        job.done = () =>
        {
            then?.Invoke();
            if (--jobsLeft > 0) return;
            Aftermath.Remove(scene);
            ResetAgent.StandDown(scene);
            Speaker.SetPlaying(true);
        };
        if (mechanic) return Mechanic.Call(job);
        ResetAgent.Call(job);
        return null;
    }

    void CarryOff(Visitor body)
    {
        if (body == null || body.Current != Visitor.State.Down || Possession.Instance?.Host == body) return; // back up (a save loaded) or already gone
        Fx.Burst("Fx/puff", body.transform.position + Vector3.up * 0.3f, 8, Color.white, 2f, 0.4f, 0.5f);
        body.CarryOff();
    }

    // The mechanic comes out to fix the blown speaker, and keeps an eye on it from then on. Already fixed (loading a
    // save): he just comes back to stand by it.
    StaffWorker CallMechanic(bool alreadyFixed)
    {
        mechanicCalled = true;
        if (alreadyFixed) Fixed = true;
        var speaker = Speaker.All.Find(s => s.livePlug && s.fountain == this) ?? (Speaker.All.Count > 0 ? Speaker.All[0] : null);
        Vector3 at = speaker != null ? speaker.transform.position : transform.position;
        var job = new StaffWorker.Job
        {
            spot = at + Vector3.back * 0.9f,
            at = at,
            seconds = alreadyFixed ? 0.01f : fixTime,
            shout = alreadyFixed ? null : "What happened here?! Who blew the speakers?",
            done = alreadyFixed ? null : Fix,
            stay = true,
            roam = 2.5f, // pottering round the speaker, keeping an eye on it
        };
        return alreadyFixed ? Mechanic.Call(job) : Job(true, job);
    }

    // ---------------------------------------------------------------- saving (SaveGame)

    public float Target => target;
    public int Presses => presses;
    public bool MechanicCalled => mechanicCalled;

    public void Restore(float radius, int pressCount, bool called, bool isFixed)
    {
        presses = pressCount;
        nearLeak = false; // next Update can notice the leak again if still unpressed
        electrocutedOnce = Mischief.IsDone(Mischief.Task.Electrocute);
        SetRadius(target = radius);
        if (called) CallMechanic(true); // saved mid clean-up: the speaker's as good as fixed
    }

    static readonly Color Spark = new(1f, 0.95f, 0.4f);

    void Zap(Vector3 from, List<Visitor> zapped, float shake, float shakeFor = 0f)
    {
        PlayZap(from);
        Fx.Burst("Fx/sparkle", from, 20, Spark, 5f, 0.4f, 0.6f);
        Fx.Shake(shake, shakeFor);
        foreach (var v in zapped) Fx.Burst("Fx/sparkle", v.HeadPoint, 10, Spark, 3f, 0.3f, 0.5f);
    }

    // A big zap gets its moment: the camera pans to the crowd, it shakes while they twitch (Visitor.Die), they drop,
    // the survivors run (their collapse sets everyone nearby panicking), then (mechanic) it pans over to the mechanic
    // coming out, and eases back to the player.
    IEnumerator Payoff(Vector3 from, List<Visitor> zapped, bool mechanic)
    {
        Possession.InputBlocked = true;
        AudioManager.MusicOff = true;
        Vector3 middle = Vector3.zero;
        foreach (var v in zapped) middle += v.transform.position;
        var shot = new GameObject("Zap Payoff").transform;
        shot.position = middle / zapped.Count;
        CameraRig.Spotlight(shot, 999f, 2.5f);

        yield return new WaitForSeconds(payoffPan);
        Zap(from, zapped, 0.45f, payoffTwitch);
        for (float t = 0f; t < payoffTwitch; t += 0.2f)
        {
            var v = zapped[Random.Range(0, zapped.Count)];
            Fx.Burst("Fx/sparkle", v.HeadPoint, 4, Spark, 2f, 0.2f, 0.3f);
            yield return new WaitForSeconds(0.2f);
        }

        yield return new WaitForSeconds(1.5f); // they drop, everyone else runs
        if (mechanic) yield return MechanicComes();
        CameraRig.EndSpotlight();
        AudioManager.MusicOff = false;
        yield return new WaitForSeconds(1f); // the camera easing back
        Possession.InputBlocked = false;
        Destroy(shot.gameObject);
        ThinkElectrocuted();
    }

    void ThinkElectrocuted()
    {
        if (!electrocutedOnce) GameHud.Think(GameHud.ThoughtPriority.Critical, GameText.ThinkElectrocuted);
        electrocutedOnce = true;
    }

    void Mop()
    {
        SetRadius(target = startRadius); // still leaking: it can all start again
        Fx.Burst("Fx/puff", PuddleCenter + Vector3.up * 0.3f, 10, Color.white, 2f, 0.5f, 0.6f);
    }

    void Fix()
    {
        Fixed = true;
        ClosedCaptions.Show(Captions.FountainFixed);
    }
}
