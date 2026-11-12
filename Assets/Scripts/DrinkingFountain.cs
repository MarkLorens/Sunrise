using System.Collections.Generic;
using UnityEngine;

// A leaky drinking fountain dripping into a puddle. Every press of its button (E, in anyone's body) makes the leak
// worse and the puddle spread. Dip a live cable in it (LiveCable) and everyone standing in it is electrocuted. Enough
// of a scene and the mechanic comes out to fix the fountain; either way the Reset Agents come to mop up. Once fixed
// it stops leaking for good.
[RequireComponent(typeof(Interactable))]
public class DrinkingFountain : MonoBehaviour, IInteraction
{
    public static readonly List<DrinkingFountain> All = new();

    public Vector3 puddleOffset = new(-1f, 0f, -1f); // where the puddle spreads from, relative to the fountain
    public float startRadius = 0.5f;
    public float growth = 0.45f;     // per press
    public float maxRadius = 4.2f;
    public int crowdToFlood = 3;     // listeners standing in it before it counts as reaching the crowd
    public int victimsToCallMechanic = 3; // how big a scene it takes before the mechanic bothers
    public float fixTime = 12f;      // how long the mechanic works on it: the window to possess him
    public float mopTime = 8f;

    public Vector3 PuddleCenter => new(transform.position.x + puddleOffset.x, 0f, transform.position.z + puddleOffset.z);
    public float Radius { get; private set; }
    public bool Fixed { get; private set; }

    static readonly Color Water = new(0.45f, 0.75f, 1f);

    SpriteRenderer puddle;
    bool mechanicCalled;
    float nextDrip;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
        ParkArt.Solid(transform, "Props/drinking_fountain", 1f, new Color(0.7f, 0.78f, 0.85f));
        puddle = ParkArt.Make("Puddle", null, "Markers/shadow", 1f, new Color(Water.r, Water.g, Water.b, 0.55f), ParkArt.DecalOrder, true);
        puddle.transform.parent.position = PuddleCenter + Vector3.up * 0.02f;
        SetRadius(startRadius);
    }

    void Update()
    {
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
    }

    // ---------------------------------------------------------------- the button

    public string Prompt(Visitor host) => Fixed ? "Drinking fountain" : "Press the leaky fountain's button";

    public string Blocked(Visitor host)
    {
        if (Fixed) return GameText.FountainFixed;
        if (mechanicCalled) return GameText.MechanicOnIt;
        if (Radius >= maxRadius) return GameText.PuddleFull;
        return null;
    }

    public void Interact(Visitor host)
    {
        SetRadius(Mathf.Min(maxRadius, Mathf.Max(Radius, startRadius) + growth));
        Sfx.Play(Sfx.HitSoft, 0.6f, 1.4f);
        Fx.Burst("Fx/puff", transform.position + Vector3.up * 0.8f, 6, Water, 2f, 0.35f, 0.5f);
        GameHud.Say(GameText.PressedFountain);
        if (VisitorsInPuddle() >= crowdToFlood) Mischief.Complete(Mischief.Task.FloodPuddle);
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
        Sfx.Play(Sfx.Crash, 1f, 1.6f);
        Fx.Burst("Fx/sparkle", from, 20, new Color(1f, 0.95f, 0.4f), 5f, 0.4f, 0.6f);
        Fx.Shake(0.3f);

        int victims = 0;
        foreach (var v in Visitor.All)
        {
            if (!v.Look.Visible || v.Current is Visitor.State.Down or Visitor.State.Riding || !InPuddle(v.transform.position)) continue;
            Fx.Burst("Fx/sparkle", v.HeadPoint, 10, new Color(1f, 0.95f, 0.4f), 3f, 0.3f, 0.5f);
            v.Die();
            victims++;
        }
        if (victims == 0) return;

        Mischief.Complete(Mischief.Task.Electrocute);
        ResetAgent.Call(new StaffWorker.Job
        {
            spot = PuddleCenter,
            at = PuddleCenter + Vector3.back,
            seconds = mopTime,
            shout = "Clean-up on the fountain! Everybody out of the way!",
            done = Mop,
        });

        if (mechanicCalled) return;
        if (victims < victimsToCallMechanic) return; // not much of a scene: nobody's coming to fix it yet
        mechanicCalled = true;
        Mechanic.Call(new StaffWorker.Job
        {
            spot = transform.position + Vector3.back * 0.9f,
            at = transform.position,
            seconds = fixTime,
            shout = "What happened here?! Who let that fountain leak all over the place?",
            done = Fix,
        });
        Mischief.Complete(Mischief.Task.LureMechanic);
    }

    void Mop()
    {
        SetRadius(Fixed ? 0f : startRadius);
        Fx.Burst("Fx/puff", PuddleCenter + Vector3.up * 0.3f, 10, Color.white, 2f, 0.5f, 0.6f);
    }

    void Fix()
    {
        Fixed = true;
        Sfx.Play(Sfx.HitWood, 0.8f);
    }
}
