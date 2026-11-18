using System.Collections;
using UnityEngine;

// A propane tank feeding the park's stands. Loosen its valve (E, in anyone's body) and gas hisses out. Its fan
// (TankFan) blows it away to nothing, until someone turns the fan round to blow it at `target`. Anything else blowing
// there (`blowers`: the hot dog stand's own StallFan) blows it straight back out until it's switched off; with those
// off, a cloud builds up round the target (propane's invisible, so it's drawn as a faint green haze). Once it's thick
// enough, a spark sets it off:
//   - at the hot dog stand, the hot dog man lighting his stove (HotDogMan): the stand goes up in a fireball, the crowd
//     runs, and the bang wakes the ride technician (RideTechnician.Wake);
//   - at the locker room, its broken cable (LockerBox): the building catches fire (LockerRoom.Burn).
[RequireComponent(typeof(Interactable))]
public class PropaneTank : MonoBehaviour, IInteraction
{
    public TankFan fan;
    [UnityEngine.Serialization.FormerlySerializedAs("stall")]
    public Transform target;           // where the cloud gathers
    public StallFan[] blowers = new StallFan[0];
    public LockerRoom burns;           // set: the locker room's tank (it burns); empty: the hot dog stand's (wakes the technician)
    public Mischief.Task leakTask = Mischief.Task.LeakPropane;
    public Mischief.Task cloudTask = Mischief.Task.GasCloud;
    public Mischief.Task boomTask = Mischief.Task.Explode;
    public float fillTime = 12f;       // seconds of fan-blown gas to fill the cloud
    public float cloudRadius = 2.6f;
    public int cloudPuffs = 14;
    public float payoffTime = 2.5f;    // the camera on the fireball
    public float wakeShot = 4.5f;      // then on the technician waking up (and coming out, if the wheel's down)

    static readonly Color Gas = new(0.7f, 0.95f, 0.55f);

    public bool Ready => level >= 1f && !spent;
    bool HotDogStand => burns == null;

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

    bool Blowing()
    {
        foreach (var b in blowers)
            if (b != null && b.On) return true;
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
        bool aimed = fan != null && fan.Aimed;
        if (aimed && level < 1f && Blowing())
        {
            // Another fan scatters it as fast as it comes.
            level = Mathf.Max(0f, level - Time.deltaTime / fillTime);
            if (!thoughtBlown && Possession.PlayerNear(Center, cloudRadius + 4f))
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
                GameHud.Think(HotDogStand ? GameText.ThinkCloud : GameText.ThinkLockerCloud);
            }
        }

        // The haze round the target, breathing a little.
        for (int i = 0; i < puffs.Length; i++)
        {
            puffs[i].color = new Color(Gas.r, Gas.g, Gas.b, level * 0.22f);
            puffs[i].transform.position = puffSpots[i] + new Vector3(Mathf.Sin(Time.time * 0.7f + i), Mathf.Sin(Time.time * 0.5f + i * 2f) * 0.3f, 0f) * 0.25f;
        }

        // Gas wisps off the valve, carried along by the fan: at the target, or off to nowhere.
        if (Time.time < nextWisp) return;
        nextWisp = Time.time + 0.25f;
        Vector3 wind = fan != null ? fan.Wind : Vector3.up * 0.3f;
        var w = ParkArt.Make("Wisp", null, "Fx/puff", 0.5f, new Color(Gas.r, Gas.g, Gas.b, 0.45f), ParkArt.OverheadOrder);
        w.transform.position = transform.position + Vector3.up * 1.3f;
        var bit = w.gameObject.AddComponent<FxBit>();
        bit.velocity = wind * (aimed ? 3.5f : 2.5f) + Vector3.up * 0.3f;
        bit.life = aimed ? 2f : 1.2f;
    }

    // ---------------------------------------------------------------- the valve

    public string Prompt(Visitor host) => "Loosen the propane valve";

    public string Blocked(Visitor host) => spent ? GameText.ValveSpent : leaking ? GameText.ValveOpen : null;

    public void Interact(Visitor host)
    {
        leaking = true;
        Sfx.Play(Sfx.HitWood, 0.5f, 1.8f);
        GameHud.Say(GameText.OpenedValve);
        GameHud.Think(GameText.ThinkValve);
        Mischief.Complete(leakTask);
    }

    // ---------------------------------------------------------------- the spark

    public void Explode()
    {
        if (spent) return;
        spent = true;
        foreach (var p in puffs) p.enabled = false;
        if (HotDogStand)
            foreach (var h in HotDogMan.All) h.Char();
        StartCoroutine(Payoff());
    }

    IEnumerator Payoff()
    {
        Possession.InputBlocked = true;
        Sfx.Play(Sfx.Crash, 1f, 0.6f);
        Sfx.Play(Sfx.Thud, 1f, 0.5f);
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
            GameHud.Think(GameText.ThinkFire);
            yield break;
        }

        // Across the park, someone's nap is over.
        foreach (var t in RideTechnician.All)
        {
            CameraRig.Spotlight(t.transform, 999f, 1.5f);
            yield return new WaitForSeconds(1f);
            t.Wake();
            yield return new WaitForSeconds(wakeShot);
            break;
        }
        CameraRig.EndSpotlight();
        yield return new WaitForSeconds(1f); // the camera easing back
        Possession.InputBlocked = false;
        GameHud.Think(GameText.ThinkBoom);
    }

    // Loading a save (SaveGame).
    public void Restore()
    {
        spent = Mischief.IsDone(boomTask);
        leaking = Mischief.IsDone(leakTask);
        level = !spent && Mischief.IsDone(cloudTask) ? 1f : 0f;
        if (spent && HotDogStand)
            foreach (var h in HotDogMan.All) h.Char();
    }
}
