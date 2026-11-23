using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A propane tank feeding the park's stands. Loosen its valve (E, in anyone's body) and gas hisses out into the fan
// beside it (TankFan), which carries it the way it faces. Any fan straight ahead within `fanReach` picks it up and
// carries it on its own way, and so on down the chain (Route). When the last fan blows it at `target`, a cloud builds
// up round it (propane's invisible, so it's drawn as a faint green haze); anywhere else, it just blows away. Once the
// cloud's thick enough, a spark sets it off:
//   - at the hot dog stand, the hot dog man lighting his stove (HotDogMan): the stand goes up in a fireball, the crowd
//     runs, and the bang wakes the ride technician (RideTechnician.Wake);
//   - at the locker room, its broken cable (LockerBox): the building catches fire (LockerRoom.Burn).
[RequireComponent(typeof(Interactable))]
public class PropaneTank : MonoBehaviour, IInteraction
{
    public TankFan fan;
    [UnityEngine.Serialization.FormerlySerializedAs("stall")]
    public Transform target;           // where the cloud gathers
    public float fanReach = 10f;       // how far a fan's stream carries gas to the next fan, or to the target
    public float fanLane = 1.5f;       // how far off to the side of the stream the next fan can stand
    public Mischief.Task aimTask = Mischief.Task.AimFan; // the chain first carries the gas all the way
    public LockerRoom burns;           // set: the locker room's tank (it burns); empty: the hot dog stand's (wakes the technician)
    public Mischief.Task leakTask = Mischief.Task.LeakPropane;
    public Mischief.Task cloudTask = Mischief.Task.GasCloud;
    public Mischief.Task boomTask = Mischief.Task.Explode;
    public float fillTime = 12f;       // seconds of fan-blown gas to fill the cloud
    public float cloudRadius = 2.6f;
    public int cloudPuffs = 14;
    public float payoffTime = 2.5f;    // the camera on the fireball

    static readonly Color Gas = new(0.7f, 0.95f, 0.55f);

    public bool Ready => level >= 1f && !spent;
    bool HotDogStand => burns == null;
    const float StandScorchRadius = 4f;                           // the hot dog stand's pictures, round where it blows
    static readonly string[] StandPictures = { "hot_dog_stand" };

    bool leaking, spent, thoughtBlown;
    float level, nextWisp;
    SpriteRenderer[] puffs;
    Vector3[] puffSpots;

    Vector3 Center => new(target.position.x, 0f, target.position.z);

    // A full cloud waiting for a spark within `range` of `p`, if any.
    public static PropaneTank ReadyNear(Vector3 p, float range)
    {
        foreach (var t in FindObjectsByType<PropaneTank>())
        {
            Vector3 d = t.Center - p;
            if (t.Ready && new Vector2(d.x, d.z).magnitude <= range + t.cloudRadius) return t;
        }
        return null;
    }

    // The gas's way from the valve: through each fan in turn, ending at the target (true) or blown off past the last
    // fan (false). `path` gets the points it passes.
    readonly List<Vector3> path = new();
    readonly List<TankFan> chain = new();

    bool Route()
    {
        path.Clear();
        chain.Clear();
        path.Add(transform.position);
        var f = fan;
        while (f != null)
        {
            path.Add(f.transform.position);
            chain.Add(f);
            // The nearest thing in its stream: another fan, or the target.
            TankFan next = null;
            float best = f.Blows(Center, cloudRadius, fanReach) ? f.Ahead(Center) : float.MaxValue;
            foreach (var other in TankFan.All)
                if (!chain.Contains(other) && f.Blows(other.transform.position, fanLane, fanReach) && f.Ahead(other.transform.position) < best)
                {
                    next = other;
                    best = f.Ahead(other.transform.position);
                }
            if (next == null && best < float.MaxValue)
            {
                path.Add(Center);
                return true;
            }
            if (next == null) path.Add(f.transform.position + f.Wind * 3f); // off to nowhere
            f = next;
        }
        return false;
    }

    void Start()
    {
        ParkArt.Solid(transform, "Props/propane_tank", 1.3f, new Color(0.9f, 0.9f, 0.92f));
        puffs = new SpriteRenderer[cloudPuffs];
        puffSpots = new Vector3[cloudPuffs];
        for (int i = 0; i < cloudPuffs; i++)
        {
            Vector2 r = Random.insideUnitCircle * cloudRadius;
            puffSpots[i] = Center + new Vector3(r.x, Random.Range(0.4f, 2.2f), r.y * 0.6f);
            puffs[i] = ParkArt.Make("Gas", null, "Fx/puff", Random.Range(1.6f, 2.6f), Color.clear, ParkArt.OverheadOrder - 1);
            puffs[i].transform.position = puffSpots[i];
        }
    }

    void Update()
    {
        if (!leaking || spent) return;
        bool aimed = Route();
        if (aimed && !Mischief.IsDone(aimTask))
        {
            Mischief.Complete(aimTask);
            GameHud.Think(GameHud.ThoughtPriority.Action, GameText.PickFanAimed());
        }
        if (!aimed && level < 1f)
        {
            // Not reaching: what's there thins out again. A guarded fan blowing it away is worth a thought.
            level = Mathf.Max(0f, level - Time.deltaTime / fillTime);
            var last = chain.Count > 0 ? chain[chain.Count - 1] : null;
            if (!thoughtBlown && last != null && last.guardedBy != TankFan.Guard.None && Possession.PlayerNear(last.transform.position, 6f))
            {
                thoughtBlown = true;
                GameHud.Think(GameText.ThinkGasBlownAway);
            }
        }
        else if (aimed && level < 1f)
        {
            level = Mathf.Min(1f, level + Time.deltaTime / fillTime);
            if (level >= 1f)
            {
                Mischief.Complete(cloudTask);
                GameHud.Think(GameHud.ThoughtPriority.Action, HotDogStand ? GameText.ThinkCloud : GameText.ThinkLockerCloud);
            }
        }

        // The haze round the target, breathing a little.
        for (int i = 0; i < puffs.Length; i++)
        {
            puffs[i].color = new Color(Gas.r, Gas.g, Gas.b, level * 0.22f);
            puffs[i].transform.position = puffSpots[i] + new Vector3(Mathf.Sin(Time.time * 0.7f + i), Mathf.Sin(Time.time * 0.5f + i * 2f) * 0.3f, 0f) * 0.25f;
        }

        // Gas wisps drifting along every leg of its way: fan to fan, to the target or off to nowhere.
        if (Time.time < nextWisp) return;
        nextWisp = Time.time + 0.3f;
        for (int i = 0; i + 1 < path.Count; i++)
        {
            Vector3 leg = path[i + 1] - path[i];
            leg.y = 0f;
            var w = ParkArt.Make("Wisp", null, "Fx/puff", 0.5f, new Color(Gas.r, Gas.g, Gas.b, 0.45f), ParkArt.OverheadOrder);
            w.transform.position = path[i] + Vector3.up * 1.2f;
            var bit = w.gameObject.AddComponent<FxBit>();
            bit.drag = 0f;
            bit.velocity = leg.normalized * WispSpeed;
            bit.life = Mathf.Max(0.3f, leg.magnitude / WispSpeed);
        }
    }

    const float WispSpeed = 3.5f;

    // ---------------------------------------------------------------- the valve

    public string Prompt(Visitor host) => "Loosen the propane valve";

    public string Blocked(Visitor host) => spent ? GameText.ValveSpent : leaking ? GameText.ValveOpen : null;

    public void Interact(Visitor host)
    {
        leaking = true;
        ClosedCaptions.Show(Captions.GasLeak);
        GameHud.Say(GameText.OpenedValve);
        GameHud.Think(GameHud.ThoughtPriority.Action, GameText.PickValve());
        Mischief.Complete(leakTask);
    }

    // ---------------------------------------------------------------- the spark

    public void Explode()
    {
        if (spent) return;
        spent = true;
        foreach (var p in puffs) p.enabled = false;
        if (HotDogStand)
        {
            foreach (var h in HotDogMan.All) h.Char();
            Scorch.BurnNear(Center, StandScorchRadius, false, StandPictures); // the stand, burning
        }
        StartCoroutine(Payoff());
    }

    IEnumerator Payoff()
    {
        Possession.InputBlocked = true;
        ClosedCaptions.Show(Captions.Explosion);
        Fx.Burst("Fx/puff", Center + Vector3.up, 40, new Color(1f, 0.55f, 0.15f), 8f, 1.2f, 0.9f);
        Fx.Burst("Fx/puff", Center + Vector3.up, 25, new Color(1f, 0.85f, 0.3f), 5f, 0.9f, 0.6f);
        Fx.Burst("Fx/sparkle", Center + Vector3.up, 30, new Color(1f, 0.9f, 0.4f), 9f, 0.4f, 0.8f);
        Fx.Shake(0.5f, 1.5f);
        ParkChaos.Add(Center, 10f, 20f, ParkChaos.Terrifying); // nobody's hurt, everybody runs
        Mischief.Complete(boomTask);
        Aftermath.Add(Center, cloudRadius + 1.5f, 120f);

        var shot = new GameObject("Boom Payoff").transform;
        shot.position = Center + Vector3.up;
        CameraRig.Spotlight(shot, 999f, cloudRadius + 1f);
        for (float t = 0f; t < payoffTime; t += 0.3f)
        {
            Fx.Burst("Fx/puff", Center + Vector3.up * 1.5f, 4, new Color(0.25f, 0.25f, 0.25f), 2f, 1f, 1.5f);
            yield return new WaitForSeconds(0.3f);
        }
        Destroy(shot.gameObject);

        if (!HotDogStand)
        {
            yield return burns.Burn(); // the fire, the PA, the operator running over
            CameraRig.EndSpotlight();
            yield return new WaitForSeconds(1f);
            Possession.InputBlocked = false;
            GameHud.Think(GameHud.ThoughtPriority.Critical, GameText.ThinkFire);
            yield break;
        }

        // Across the park, someone's nap is over.
        if (RideTechnician.All.Count > 0) yield return RideTechnician.All[0].WakeUp();
        CameraRig.EndSpotlight();
        yield return new WaitForSeconds(1f); // the camera easing back
        Possession.InputBlocked = false;
        GameHud.Think(GameHud.ThoughtPriority.Critical, GameText.ThinkBoom);
    }

    // Loading a save (SaveGame).
    public void Restore()
    {
        spent = Mischief.IsDone(boomTask);
        leaking = Mischief.IsDone(leakTask);
        level = !spent && Mischief.IsDone(cloudTask) ? 1f : 0f;
        if (spent && HotDogStand)
        {
            foreach (var h in HotDogMan.All) h.Char();
            Scorch.BurnNear(Center, StandScorchRadius, true, StandPictures); // already burnt
        }
    }
}
