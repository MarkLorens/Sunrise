using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A flat Ferris wheel: the wheel sprite spins in its own plane and the cabins hang upright around it.
// Its power runs through the mechanic's electrical box next to it (ElectricalBox): cut it and the wheel winds down
// to a stop with whoever's riding stuck up in their cabins.
// Started again (ElectricalBox) after its leg hinge was loosened (WheelHinge), it runs wobbly: lurching side to side and
// speeding up and slowing down, the cabins rocking all the way round.
// Turned up to max speed like that (ControlPanel), it spins faster and faster until it tears off its hinge, flings
// its cabins, and rolls away across the park flattening everyone in its path, then topples over.
// When the scene starts it marks out its staff-only closed area (StaffArea) and puts the electrical box just outside
// it. Scale the whole object to resize the ride: radii, part offsets and the closed area are all in unscaled units
// and grow with it.
public class FerrisWheel : MonoBehaviour
{
    public static readonly List<FerrisWheel> All = new();

    public enum State { Running, Unpowered, Overdrive, Breaking, Rolling, Wrecked }

    public Transform wheel;
    public Sprite cabinSprite;
    public int cabinCount = 12;
    public float cabinRadius = 3.4f; // where cabins hang from, in wheel units
    public float wheelRadius = 3.5f; // outer rim, where the light bulbs go
    public float normalSpeed = 12f;  // degrees per second
    public float windDownTime = 5f;  // seconds to coast to a stop once the power's cut
    public Vector3 electricalBoxOffset = new(-3.3f, 0f, -0.6f);
    public Transform electricalBoxAt; // set: the box stands here instead, drawn by the Objects tilemap (no picture of its own)
    public Sprite electricalBoxArt; // drawn at its own size, pivot at its feet (the palette's props); empty = Props/electrical_box
    public Vector3 closedAreaOffset = new(0f, 0f, 0.3f);
    public Vector2 closedAreaSize = new(5.4f, 3.4f);
    public RideQueue queue;
    public float payoffTime = 6f;     // the camera stays on the wheel this long once the power's cut
    public float payoffPullBack = 8f;  // how much further back the camera sits to fit the whole wheel
    public int screamingCabins = 4;   // cabins shouting in turns (GameText.CabinScreams, or CabinScreamsWobbly) in a payoff
    public float cabinShake = 4f;     // degrees the stuck cabins rock
    public float wobble = 0.08f;      // how far the wheel lurches side to side once the hinge is loose (wheel units)
    public float overdriveSpeed = 140f; // degrees per second at max speed
    public float spinUpTime = 6f;     // seconds to get there
    public float holdTime = 2f;       // seconds the loose hinge holds at max speed
    public float breakDelay = 1.5f;   // seconds of shuddering before it tears off
    public float rollSpeed = 6f;
    public float rollDistance = 22f;
    public Vector3 rollDirection = new(1f, 0f, -0.6f); // across the ground; it has to go mostly sideways to read as rolling

    public State Current { get; private set; }
    public bool Powered => Current != State.Unpowered;
    public bool Broken => Current >= State.Breaking;
    public bool Wobbly { get; private set; }
    public StaffArea Area { get; private set; }
    public IReadOnlyList<Transform> Cabins => cabins;
    float Scale => transform.lossyScale.x;

    readonly List<Transform> cabins = new();
    float angle;
    float speed;
    float shakeUntil;
    Vector3 wheelHome;
    float stateTime, rolled;
    readonly List<Vector3> cabinVelocity = new();

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    // Before anyone's Start, so the spawner can keep visitors out of it.
    void Awake() => Area = ClosedArea();

    void Start()
    {
        speed = normalSpeed;
        wheelHome = wheel.localPosition;
        if (!TryGetComponent(out FerrisWheelAudio _)) gameObject.AddComponent<FerrisWheelAudio>();
        var box = Part<ElectricalBox>("Electrical Box", electricalBoxOffset, "Props/electrical_box", 1.3f, electricalBoxArt, electricalBoxAt);
        box.gameObject.AddComponent<ZapFx>(); // live electrics, always buzzing
        box.wheel = this;
        var mat = wheel.GetComponent<SpriteRenderer>().sharedMaterial;
        for (int i = 0; i < cabinCount; i++)
        {
            var go = new GameObject($"Cabin {i + 1}");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = cabinSprite;
            sr.sharedMaterial = mat;
            ParkArt.CastShadows(sr);
            cabins.Add(go.transform);
        }
    }

    // Loading a save (SaveGame) with the power already cut: standing still, no payoff.
    public void RestoreUnpowered()
    {
        Current = State.Unpowered;
        speed = 0f;
    }

    // Loading a save (SaveGame) with the wheel already started again: running wobbly, no payoff.
    public void RestoreWobbly()
    {
        Current = State.Running;
        Wobbly = true;
    }

    // Started again on a loosened hinge (ElectricalBox): it runs, but not right. The camera takes it in.
    public void Restart()
    {
        if (Powered) return;
        Current = State.Running;
        Wobbly = true;
        StartCoroutine(WobblyPayoff());
    }

    IEnumerator WobblyPayoff()
    {
        Possession.InputBlocked = true;
        CameraRig.Spotlight(wheel, 999f, (wheelRadius + 1f) * Scale, payoffPullBack);
        ScreamIn(GameText.CabinScreamsWobbly);
        yield return new WaitForSeconds(1.5f);
        Fx.Shake(0.15f, payoffTime - 1.5f);
        yield return new WaitForSeconds(payoffTime - 1.5f);
        CameraRig.EndSpotlight();
        yield return new WaitForSeconds(1f); // the camera easing back
        Possession.InputBlocked = false;
        GameHud.Think(GameHud.ThoughtPriority.Action, GameText.ThinkWobble);
    }

    public void CutPower()
    {
        if (!Powered) return;
        Current = State.Unpowered; // FerrisWheelAudio hears it and powers the sounds down
        StartCoroutine(Payoff());
    }

    // The power's out: the camera takes in the whole wheel as it winds down, the cabins rock, and a few of them
    // scream in turns. Then it pans to the ride technician's booth, his phone ringing, him snoring, and eases back
    // to the player.
    IEnumerator Payoff()
    {
        Possession.InputBlocked = true;
        CameraRig.Spotlight(wheel, 999f, (wheelRadius + 1f) * Scale, payoffPullBack);
        shakeUntil = Time.time + payoffTime;

        ScreamIn(GameText.CabinScreams);

        yield return new WaitForSeconds(payoffTime);
        // Meanwhile the operator's ringing the technician, who sleeps right through it.
        foreach (var t in RideTechnician.All)
            if (!t.IsAwake)
            {
                yield return t.SleepThroughCall();
                break;
            }
        CameraRig.EndSpotlight();
        yield return new WaitForSeconds(1f); // the camera easing back
        Possession.InputBlocked = false;
    }

    // Short bursts with gaps, starting at different moments, so the cabins talk over each other rather than in chorus.
    readonly HashSet<string> shouting = new(); // lines up on screen right now

    // A few cabins at random shout `lines` in turns for the length of the payoff.
    void ScreamIn(string[] lines)
    {
        var picks = new List<Transform>(cabins);
        for (int i = 0; i < Mathf.Min(screamingCabins, picks.Count); i++)
        {
            int j = Random.Range(i, picks.Count);
            (picks[i], picks[j]) = (picks[j], picks[i]);
            StartCoroutine(Scream(picks[i], lines));
        }
    }

    IEnumerator Scream(Transform cabin, string[] lines)
    {
        float end = Time.time + payoffTime - 0.4f;
        yield return new WaitForSeconds(Random.Range(0.4f, 1.5f));
        while (Time.time < end)
        {
            float show = Random.Range(0.6f, 1.1f);
            string line = lines[Random.Range(0, lines.Length)];
            while (shouting.Contains(line) && shouting.Count < lines.Length) line = lines[Random.Range(0, lines.Length)];
            shouting.Add(line); // nobody else picks it while it's up
            GameHud.Shout(cabin, line, show);
            yield return new WaitForSeconds(show);
            shouting.Remove(line);
            yield return new WaitForSeconds(Random.Range(0.2f, 0.9f));
        }
    }

    // Offsets are relative to the wheel's base. `at` set: it stands there instead, already drawn in the scene (a tile),
    // so its own picture is hidden (kept for its shape: the tutorial lights it up) and it adds nothing to bump into.
    T Part<T>(string name, Vector3 offset, string sprite, float height, Sprite art = null, Transform at = null) where T : Component
    {
        var go = new GameObject(name);
        go.SetActive(false); // so Interactable's Awake finds the rules component
        go.transform.position = at != null ? at.position : transform.position + offset * Scale;
        var pic = Prop(go.transform, sprite, height, Vector3.zero);
        if (art != null)
        {
            pic.sprite = art;
            pic.transform.localScale = Vector3.one;
            pic.transform.localPosition = Vector3.zero;
        }
        var part = go.AddComponent<T>();
        go.AddComponent<Interactable>();
        go.SetActive(true);
        if (at != null)
        {
            pic.enabled = false;
            return part;
        }

        // Something people bump into: a box the size of its picture. Made after the scene loaded, so GroundWalker
        // is told about it.
        var box = pic.gameObject.AddComponent<BoxCollider>();
        Bounds b = pic.sprite.bounds;
        box.center = b.center;
        box.size = new Vector3(b.size.x, b.size.y, 0.2f);
        GroundWalker.AddObstacle(box);
        return part;
    }

    static SpriteRenderer Prop(Transform parent, string sprite, float height, Vector3 localFeet)
    {
        var r = ParkArt.Lit(ParkArt.Make("Sprite", parent, sprite, height, Color.white));
        r.transform.localPosition = localFeet + new Vector3(0f, height * 0.5f, 0f);
        return r;
    }

    // The staff-only ground at the wheel's foot: a painted line round it, barriers at the sides, a sign at the front.
    StaffArea ClosedArea()
    {
        var area = new GameObject("Closed Area").AddComponent<StaffArea>();
        area.transform.position = transform.position + closedAreaOffset * Scale;
        area.size = closedAreaSize * Scale;
        Vector2 h = area.size * 0.5f;

        var line = area.gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = ParkArt.Material;
        line.sortingOrder = ParkArt.DecalOrder;
        line.loop = true;
        line.widthMultiplier = 0.08f;
        line.startColor = line.endColor = new Color(1f, 0.82f, 0.15f);
        line.positionCount = 4;
        line.SetPositions(new[]
        {
            area.transform.position + new Vector3(-h.x, 0.03f, -h.y), area.transform.position + new Vector3(h.x, 0.03f, -h.y),
            area.transform.position + new Vector3(h.x, 0.03f, h.y), area.transform.position + new Vector3(-h.x, 0.03f, h.y),
        });

        Prop(area.transform, "Props/barrier", 0.6f, new Vector3(-h.x, 0f, 0f));
        Prop(area.transform, "Props/barrier", 0.6f, new Vector3(h.x, 0f, 0f));
        Prop(area.transform, "Props/staff_sign", 1.1f, new Vector3(h.x + 0.2f, 0f, -h.y - 0.1f));
        return area;
    }

    // ---------------------------------------------------------------- max speed (ControlPanel)

    public void MaxSpeed()
    {
        if (Current != State.Running) return;
        Set(State.Overdrive);
        StartCoroutine(MaxSpeedPayoff());
    }

    void Set(State s)
    {
        Current = s;
        stateTime = 0f;
    }

    // The camera stays on the wheel the whole way: spinning up, tearing off, rolling, toppling. Then back to the player.
    IEnumerator MaxSpeedPayoff()
    {
        Possession.InputBlocked = true;
        CameraRig.Spotlight(wheel, 999f, (wheelRadius + 1f) * Scale, payoffPullBack);
        ScreamIn(GameText.CabinScreamsWobbly);
        while (Current != State.Wrecked) yield return null;
        yield return new WaitForSeconds(2f);
        CameraRig.EndSpotlight();
        yield return new WaitForSeconds(1f); // the camera easing back
        Possession.InputBlocked = false;
        Mischief.Complete(Mischief.Task.WheelRolled);
        Aftermath.Add(new Vector3(wheel.position.x, 0f, wheel.position.z), wheelRadius * Scale + 1f, 9999f);
        GameHud.Think(GameHud.ThoughtPriority.Critical, GameText.ThinkRolled);
    }

    void Update()
    {
        stateTime += Time.deltaTime;
        switch (Current)
        {
            case State.Overdrive:
                speed = Mathf.MoveTowards(speed, overdriveSpeed, overdriveSpeed / spinUpTime * Time.deltaTime);
                float k = speed / overdriveSpeed;
                Spin(Lurch(1f + 3f * k));
                if (speed < overdriveSpeed * 0.95f) stateTime = 0f; // the hinge holds `holdTime` once it's flat out
                else if (stateTime >= holdTime)
                {
                    Set(State.Breaking);
                    Fx.Shake(0.25f, breakDelay);
                    Sfx.Play(Sfx.HitWood, 1f, 0.5f); // *CREAK*
                }
                break;

            case State.Breaking:
                Spin(wheelHome + (Vector3)(Random.insideUnitCircle * 0.25f));
                if (stateTime >= breakDelay) Detach();
                break;

            case State.Rolling:
                Roll();
                break;

            case State.Wrecked:
                break;

            default:
                float target = !Powered ? 0f : Wobbly ? normalSpeed * (1f + 0.35f * Mathf.Sin(Time.time * 1.7f)) : normalSpeed; // lurching
                speed = Mathf.MoveTowards(speed, target, normalSpeed / windDownTime * Time.deltaTime);
                Spin(Wobbly ? Lurch(1f) : wheel.localPosition);
                break;
        }
        UpdateCabins();
    }

    // Side to side on a loose hinge, `amount` times as far as a plain wobble.
    Vector3 Lurch(float amount) =>
        wheelHome + new Vector3(Mathf.Sin(Time.time * 2.3f * amount) * wobble * amount, Mathf.Abs(Mathf.Sin(Time.time * 4.6f)) * wobble * 0.3f * amount, 0f);

    void Spin(Vector3 at)
    {
        wheel.localPosition = at;
        angle += speed * Time.deltaTime;
        wheel.localRotation = Quaternion.Euler(0, 0, angle);
    }

    void UpdateCabins()
    {
        if (Current < State.Rolling)
        {
            // Hang upright from evenly spaced points on the spinning rim.
            for (int i = 0; i < cabins.Count; i++)
            {
                float a = (angle + i * 360f / cabins.Count) * Mathf.Deg2Rad;
                cabins[i].position = wheel.position + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * cabinRadius * Scale + new Vector3(0, 0, -0.05f);
                // Stuck cabins rock, each to its own rhythm, settling as the payoff ends.
                float rock = Wobbly ? cabinShake : Mathf.Clamp01((shakeUntil - Time.time) / 1.5f) * cabinShake;
                cabins[i].localRotation = Quaternion.Euler(0, 0, (Mathf.PerlinNoise(Time.time * 3f, i * 1.7f) * 2f - 1f) * rock);
            }
            return;
        }

        // Flung cabins fall until they hit the ground.
        for (int i = 0; i < cabins.Count; i++)
        {
            if (cabins[i].position.y <= 0.9f * Scale && cabinVelocity[i].y <= 0) continue; // landed (a cabin hangs from its top)
            cabinVelocity[i] += Vector3.down * 15f * Time.deltaTime;
            Vector3 p = cabins[i].position + cabinVelocity[i] * Time.deltaTime;
            p.y = Mathf.Max(p.y, 0.9f * Scale);
            cabins[i].position = p;
            cabins[i].rotation = Quaternion.Euler(0, 0, cabinVelocity[i].x * -4f);
        }
    }

    // Off the hinge: the wheel comes loose, the cabins and their riders go flying.
    void Detach()
    {
        Set(State.Rolling);
        wheel.SetParent(null, true);
        foreach (var c in cabins)
        {
            c.SetParent(null, true);
            cabinVelocity.Add(new Vector3(Random.Range(-4f, 4f), Random.Range(3f, 7f), Random.Range(-1.5f, 0.5f)));
        }
        if (queue != null) queue.ThrowOutRiders(transform.position + Vector3.back * 1.5f, 3f);
        Fx.Shake(0.45f, 1f);
        Sfx.Play(Sfx.Crash);
        Sfx.Play(Sfx.Thud, 1f, 0.6f);
        ParkChaos.Add(transform.position, 12f, 9999f);
    }

    // Down to the ground, then along rollDirection, flattening people; then it tips over flat.
    void Roll()
    {
        Vector3 dir = new Vector3(rollDirection.x, 0f, rollDirection.z).normalized;
        float radius = wheelRadius * Scale;
        Vector3 p = wheel.position;
        p.y = Mathf.MoveTowards(p.y, radius, 9f * Time.deltaTime);

        if (rolled < rollDistance)
        {
            float step = rollSpeed * Time.deltaTime;
            p += dir * step;
            rolled += step;
            angle -= Mathf.Sign(dir.x) * step / radius * Mathf.Rad2Deg; // rolling without slipping
            wheel.rotation = Quaternion.Euler(0, 0, angle);
            Flatten(p, dir, radius);
            ParkChaos.Add(p, 10f, 8f);
        }
        else
        {
            // Topple: tip back until it lies on the ground.
            var flat = Quaternion.Euler(90f, 0f, angle);
            wheel.rotation = Quaternion.RotateTowards(wheel.rotation, flat, 120f * Time.deltaTime);
            p.y = Mathf.MoveTowards(p.y, 0.05f, radius * 1.4f * Time.deltaTime); // lands as it finishes tipping
            if (Quaternion.Angle(wheel.rotation, flat) < 0.5f && p.y <= 0.06f)
            {
                Set(State.Wrecked);
                Fx.Shake(0.4f, 0.8f);
                Sfx.Play(Sfx.Crash, 0.9f, 0.7f);
                Fx.Burst("Fx/puff", p, 30, new Color(0.7f, 0.65f, 0.6f), 6f, 1.2f, 1.2f);
                ParkChaos.Add(p, 12f, 9999f);
            }
        }
        wheel.position = p;
    }

    // Anyone in the wheel's path, under the rim (it's edge-on to its direction of travel, so: a thin band across it).
    static void Flatten(Vector3 center, Vector3 dir, float radius)
    {
        Vector3 side = new(-dir.z, 0f, dir.x);
        foreach (var v in Visitor.All)
        {
            if (v.Current is Visitor.State.Down or Visitor.State.Riding || !v.Look.Visible) continue;
            Vector3 d = v.transform.position - center;
            d.y = 0f;
            if (Mathf.Abs(Vector3.Dot(d, dir)) > 0.8f || Mathf.Abs(Vector3.Dot(d, side)) > radius * 0.3f) continue;
            v.KnockDown();
            Sfx.Play(Sfx.Crash, 0.6f, Random.Range(0.9f, 1.2f));
        }
    }
}
