using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A leaky drinking fountain dripping into a puddle. Every press of its button (K, in anyone's body) makes the leak
// worse: the puddle starts small right under it and, press by press, slowly spreads out towards puddleOffset. Dip a live cable in it (LiveCable) and everyone standing in it is electrocuted. Enough
// of a scene and the mechanic comes out to fix the fountain; either way the Reset Agents come to mop up. Once fixed
// it stops leaking for good.
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
    public int payoffVictims = 5;    // more zapped than this and the camera cuts to it (Payoff)
    public float payoffPan = 1f;     // seconds for the camera to get there
    public float payoffTwitch = 2f;  // seconds the victims twitch
    public float pressCooldown = 3f; // before the button can be pressed again
    public float spreadSpeed = 0.45f; // how fast the water creeps out after a press (radius per second)
    public Sprite puddleArt;         // empty = a tinted round blob
    [Range(0f, 1f)] public float puddleOpacity = 0.6f;

    // Under the fountain while small, sliding out to puddleOffset as it grows.
    public Vector3 PuddleCenter => new Vector3(transform.position.x, 0f, transform.position.z)
        + new Vector3(puddleOffset.x, 0f, puddleOffset.z) * Mathf.InverseLerp(startRadius, maxRadius, Radius);
    public float Radius { get; private set; }
    public bool Fixed { get; private set; }

    static readonly Color Water = new(0.45f, 0.75f, 1f);

    SpriteRenderer puddle;
    bool mechanicCalled, nearLeak, electrocutedOnce;
    float nextDrip, readyAt, target; // target: the radius the water is still spreading to
    int presses;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
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

        if (Fixed || Time.time < nextDrip) return;
        nextDrip = Time.time + 1.2f;
        Fx.Burst("Fx/sparkle", transform.position + Vector3.up * 0.9f, 2, Water, 1.2f, 0.18f, 0.5f);
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

    public string Prompt(Visitor host) => Fixed ? "Drinking fountain" : "Press the leaky fountain's button";

    public string Blocked(Visitor host)
    {
        if (Fixed) return GameText.FountainFixed;
        if (mechanicCalled) return GameText.MechanicOnIt;
        if (target >= maxRadius) return GameText.PuddleFull;
        if (Time.time < readyAt) return GameText.FountainBusy;
        return null;
    }

    public void Interact(Visitor host)
    {
        readyAt = Time.time + pressCooldown;
        var lines = GameText.ThinkFountainPress;
        if (presses < lines.Length) GameHud.Think(lines[presses]);
        presses++;
        target = Mathf.Min(maxRadius, Mathf.Max(target, startRadius) + (maxRadius - startRadius) / tapsToFull); // Update lets it creep out
        Sfx.Play(Sfx.HitSoft, 0.6f, 1.4f);
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

    public void Electrify(Vector3 from)
    {
        var zapped = new List<Visitor>();
        foreach (var v in Visitor.All)
            if (v.Look.Visible && v.Current is not (Visitor.State.Down or Visitor.State.Riding) && InPuddle(v.transform.position))
                zapped.Add(v);
        int victims = zapped.Count;

        if (victims > payoffVictims)
        {
            foreach (var v in zapped) v.Die(payoffPan, payoffTwitch, true);
            StartCoroutine(Payoff(from, zapped));
        }
        else
        {
            foreach (var v in zapped) v.Die(0f, 0f, true);
            Zap(from, zapped, 0.3f);
        }
        if (victims == 0) return;

        if (victims <= payoffVictims) ThinkElectrocuted(); // a payoff thinks it once it's over
        Mischief.Complete(Mischief.Task.Electrocute);
        ResetAgent.Call(new StaffWorker.Job
        {
            spot = PuddleCenter,
            at = PuddleCenter + Vector3.back,
            seconds = mopTime,
            shout = "Clean-up on the fountain! Everybody out of the way!",
            done = Mop,
        });
        Aftermath.Add(PuddleCenter, Radius + 1f, 90f); // another agent guards its edge

        if (mechanicCalled) return;
        if (victims < victimsToCallMechanic) return; // not much of a scene: nobody's coming to fix it yet
        CallMechanic(false);
        Mischief.Complete(Mischief.Task.LureMechanic);
    }

    // The mechanic comes out to fix it, and keeps an eye on it from then on. Already fixed (loading a save): he just
    // comes back to stand by it.
    void CallMechanic(bool alreadyFixed)
    {
        mechanicCalled = true;
        Fixed = alreadyFixed;
        Mechanic.Call(new StaffWorker.Job
        {
            spot = transform.position + Vector3.back * 0.9f,
            at = transform.position,
            seconds = alreadyFixed ? 0.01f : fixTime,
            shout = alreadyFixed ? null : "What happened here?! Who let that fountain leak all over the place?",
            done = alreadyFixed ? null : Fix,
            stay = true,
        });
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
        if (called) CallMechanic(isFixed);
    }

    static readonly Color Spark = new(1f, 0.95f, 0.4f);

    void Zap(Vector3 from, List<Visitor> zapped, float shake, float shakeFor = 0f)
    {
        Sfx.Play(Sfx.Crash, 1f, 1.6f);
        Fx.Burst("Fx/sparkle", from, 20, Spark, 5f, 0.4f, 0.6f);
        Fx.Shake(shake, shakeFor);
        foreach (var v in zapped) Fx.Burst("Fx/sparkle", v.HeadPoint, 10, Spark, 3f, 0.3f, 0.5f);
    }

    // A big zap gets its moment: the camera pans to the crowd, it shakes while they twitch (Visitor.Die), they drop,
    // the survivors run (their collapse sets everyone nearby panicking), then the camera eases back to the player.
    IEnumerator Payoff(Vector3 from, List<Visitor> zapped)
    {
        Possession.InputBlocked = true;
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
        CameraRig.EndSpotlight();
        yield return new WaitForSeconds(1f); // the camera easing back
        Possession.InputBlocked = false;
        Destroy(shot.gameObject);
        ThinkElectrocuted();
    }

    void ThinkElectrocuted()
    {
        if (!electrocutedOnce) GameHud.Think(GameText.ThinkElectrocuted);
        electrocutedOnce = true;
    }

    void Mop()
    {
        SetRadius(target = Fixed ? 0f : startRadius);
        Fx.Burst("Fx/puff", PuddleCenter + Vector3.up * 0.3f, 10, Color.white, 2f, 0.5f, 0.6f);
    }

    void Fix()
    {
        Fixed = true;
        Sfx.Play(Sfx.HitWood, 0.8f);
    }
}
