using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// A flat Ferris wheel: the wheel sprite spins in its own plane and the cabins hang upright around it.
// Two ways to wreck it:
//   1. The puzzle: loosen the support (WheelHinge), then run the ride at full speed (ControlPanel). After `loadTime`
//      at full speed the support gives way, the wheel rolls across the park flattening whoever's in the way, then
//      topples flat onto the ground.
//   2. Something thrown into the gears at its base jams it to a sudden stop and flings the riders out.
// Either way: panic within `chaosRadius` for `chaosDuration`, police tape around the wreck after `policeDelay`,
// then once the chaos is over it's closed for `rebuildTime` and comes back good as new.
// It also puts its electrical box and support hinge next to itself when the scene starts.
public class FerrisWheel : MonoBehaviour
{
    public static readonly List<FerrisWheel> All = new();

    public enum State { Running, Overdrive, Breaking, Rolling, Wrecked, Jammed }

    public Transform wheel;
    public Tilemap ground;
    public Sprite cabinSprite;
    public int cabinCount = 12;
    public float cabinRadius = 3.4f; // where cabins hang from, in wheel units
    public float wheelRadius = 3.5f; // outer rim, for rolling and hitting people
    public float normalSpeed = 12f;  // degrees per second
    public float overdriveSpeed = 140f;
    public float spinUpTime = 4f;
    public float breakDelay = 1.5f;
    public float rollSpeed = 6f;
    public float rollDistance = 30f;
    public float chaosRadius = 10f;
    public float chaosDuration = 300f; // people keep screaming and running near the wreck for 5 minutes
    public float panicTime = 8f;
    public float gearRadius = 1.6f;   // around the base: where a thrown item jams the gears
    [Range(0, 1)] public float riderDeathChance = 0.6f;
    public float loadTime = 6f;       // seconds a loosened support holds at full speed
    public float policeDelay = 10f;
    public float policeLineRadius = 5f;
    public float rebuildTime = 600f;
    public Vector3 electricalBoxOffset = new(2.2f, 0f, -1.4f);
    public Vector3 hingeOffset = new(-1.4f, 0f, -1f);
    public RideQueue queue;

    public State Current { get; private set; }
    public bool IsRunningNormally => Current == State.Running;
    public bool IsOverdrive => Current == State.Overdrive;
    public bool IsBroken => Current is State.Breaking or State.Rolling or State.Wrecked or State.Jammed;
    public bool Loosened { get; private set; }
    public bool PanelFried { get; private set; }
    // Broken and still frightening: people scream, staff keep them back.
    public bool InChaos => IsBroken && Time.time - brokeAt < chaosDuration;
    public Vector3 WreckPoint => new(wheel.position.x, 0f, wheel.position.z);

    readonly List<Transform> cabins = new();
    readonly List<Vector3> cabinVelocity = new();
    Vector3 wheelHome;
    float angle;
    float speed;
    float stateTime;
    float rolled;
    float nextPanic;
    float nextPuff;
    float strain;    // seconds at full speed on a loosened support
    float brokeAt;
    bool rebuilding;
    Transform wheelParent;
    GameObject policeLine;
    WorldBar rebuildBar;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public bool InGears(Vector3 p)
    {
        Vector3 d = p - transform.position;
        return !IsBroken && new Vector2(d.x, d.z).magnitude < gearRadius && p.y < 1.5f;
    }

    // Method 2: something lands in the gears. The wheel stops dead and the riders are flung out of their cabins.
    public void Jam(Item item)
    {
        if (IsBroken) return;
        item.Consume();
        Current = State.Jammed;
        stateTime = 0f;
        brokeAt = Time.time;
        speed = 0f;
        if (queue != null) queue.ThrowOutRiders(transform.position + new Vector3(0, 0, -1.5f), 3.5f, riderDeathChance);
        Chaos(transform.position);
        ParkChaos.Add(transform.position, chaosRadius, chaosDuration);
        Sfx.Play(Sfx.Crash, 1f, 0.6f);
        Fx.Burst("Fx/puff", transform.position + Vector3.up, 20, new Color(0.9f, 0.85f, 0.8f), 3f, 0.9f, 0.8f);
        Fx.Shake(0.4f);
        GameHud.Say($"CRUNCH! The {item.DisplayName} jams the gears. The wheel stops dead and the riders are flung out!", "Bubbles/exclaim");
        Mischief.Complete(Mischief.Task.WreckWheel);
    }

    void Start()
    {
        wheelHome = wheel.localPosition;
        wheelParent = wheel.parent;
        speed = normalSpeed;
        rebuildBar = WorldBar.Create(transform, new Vector3(0f, 1.6f, -0.3f), 2.5f);
        Part<ElectricalBox>("Electrical Box", electricalBoxOffset, 1.1f, new Color(0.35f, 0.45f, 0.6f)).wheel = this;
        Part<WheelHinge>("Support Hinge", hingeOffset, 0.5f, new Color(0.5f, 0.5f, 0.55f)).wheel = this;
        var mat = wheel.GetComponent<SpriteRenderer>().sharedMaterial;
        for (int i = 0; i < cabinCount; i++)
        {
            var go = new GameObject($"Cabin {i + 1}");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = cabinSprite;
            sr.sharedMaterial = mat;
            cabins.Add(go.transform);
        }
    }

    public void Overdrive()
    {
        if (Current != State.Running) return;
        Current = State.Overdrive;
        stateTime = 0f;
    }

    // The operator turning it back down. Not possible once the panel's fried.
    public void ResetToNormal()
    {
        if (Current != State.Overdrive || PanelFried) return;
        Current = State.Running;
    }

    public void Loosen() => Loosened = true;

    public void FryPanel() => PanelFried = true;

    void BreakLoose()
    {
        Current = State.Breaking;
        stateTime = 0f;
        brokeAt = Time.time;
        GameHud.Say("*CREAK* The loosened support is buckling!");
    }

    // ponytail: placeholder blocks from Markers/white until there's art; offsets are relative to the wheel's base.
    T Part<T>(string name, Vector3 offset, float height, Color color) where T : Component
    {
        var go = new GameObject(name);
        go.SetActive(false); // so Interactable's Awake finds the rules component
        go.transform.position = transform.position + offset;
        var sprite = ParkArt.Make("Sprite", go.transform, "Markers/white", height, color);
        sprite.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
        var part = go.AddComponent<T>();
        go.AddComponent<Interactable>();
        go.SetActive(true);
        return part;
    }

    void Update()
    {
        stateTime += Time.deltaTime;
        switch (Current)
        {
            case State.Running:
                speed = Mathf.MoveTowards(speed, normalSpeed, overdriveSpeed / spinUpTime * Time.deltaTime);
                wheel.localPosition = wheelHome;
                Spin();
                break;

            case State.Overdrive:
                speed = Mathf.MoveTowards(speed, overdriveSpeed, overdriveSpeed / spinUpTime * Time.deltaTime);
                Shake((Loosened ? 0.15f : 0.05f) * speed / overdriveSpeed);
                Spin();
                if (Loosened && speed >= overdriveSpeed * 0.95f) strain += Time.deltaTime;
                if (strain >= loadTime) BreakLoose();
                break;

            case State.Breaking:
                Shake(0.25f);
                Spin();
                if (stateTime >= breakDelay) Detach();
                break;

            case State.Rolling:
                Roll();
                break;

            case State.Jammed:
                // Shudders, grinds and smokes for a while after the sudden stop.
                Shake(Mathf.Max(0f, 0.3f - stateTime * 0.1f));
                if (stateTime < 8f && Time.time >= nextPuff)
                {
                    nextPuff = Time.time + 0.3f;
                    Fx.Burst("Fx/puff", transform.position + Vector3.up * 0.6f, 2, new Color(0.5f, 0.5f, 0.5f, 0.7f), 1f, 0.6f, 1f);
                }
                Aftermath();
                break;

            case State.Wrecked:
                Aftermath();
                break;
        }
        UpdateCabins();
    }

    // Police tape while the chaos lasts, then closed for rebuilding with a progress bar, then open again.
    void Aftermath()
    {
        float t = Time.time - brokeAt;
        if (t < chaosDuration)
        {
            if (policeLine == null && t >= policeDelay)
            {
                policeLine = PoliceLine(WreckPoint, policeLineRadius);
                GameHud.Say("The police tape off the area around the wreck.");
            }
            return;
        }
        if (!rebuilding)
        {
            rebuilding = true;
            if (policeLine != null) Destroy(policeLine);
            GameHud.Say($"The police tape comes down. The Ferris wheel is closed for rebuilding ({rebuildTime / 60f:0} min).");
        }
        rebuildBar.Set(Mathf.InverseLerp(chaosDuration, chaosDuration + rebuildTime, t), new Color(0.6f, 1f, 0.92f), true);
        if (t >= chaosDuration + rebuildTime) Rebuild();
    }

    static GameObject PoliceLine(Vector3 center, float radius)
    {
        const int points = 40;
        var go = new GameObject("Police Line");
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = ParkArt.Material;
        line.loop = true;
        line.widthMultiplier = 0.12f;
        line.startColor = line.endColor = new Color(1f, 0.85f, 0.1f);
        line.positionCount = points;
        for (int i = 0; i < points; i++)
        {
            float a = i * Mathf.PI * 2f / points;
            line.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, 0.7f, Mathf.Sin(a) * radius));
        }
        return go;
    }

    void Rebuild()
    {
        wheel.SetParent(wheelParent, true);
        wheel.localPosition = wheelHome;
        wheel.localRotation = Quaternion.identity;
        foreach (var c in cabins)
        {
            c.SetParent(transform, true);
            c.rotation = Quaternion.identity;
        }
        cabinVelocity.Clear();
        Current = State.Running;
        stateTime = angle = speed = rolled = strain = 0f;
        Loosened = PanelFried = rebuilding = false;
        policeLine = null;
        rebuildBar.Set(0f, Color.white, false);
        GameHud.Say("The Ferris wheel is rebuilt and open again!");
    }

    void Spin()
    {
        angle += speed * Time.deltaTime;
        wheel.localRotation = Quaternion.Euler(0, 0, angle);
    }

    void Shake(float amount)
    {
        wheel.localPosition = wheelHome + (Vector3)(Random.insideUnitCircle * amount);
    }

    void Detach()
    {
        Current = State.Rolling;
        stateTime = 0f;
        wheel.SetParent(null, true);

        // Cabins and the people in them get flung off.
        for (int i = 0; i < cabins.Count; i++)
        {
            cabins[i].SetParent(null, true);
            cabinVelocity.Add(new Vector3(Random.Range(-4f, 4f), Random.Range(3f, 7f), Random.Range(-1.5f, 0.5f)));
        }
        if (queue != null) queue.ThrowOutRiders(transform.position + new Vector3(0, 0, -1.5f), 3f, riderDeathChance);
        Fx.Shake(0.4f);
        Chaos(transform.position);
        ParkChaos.Add(transform.position, chaosRadius, chaosDuration);
        Sfx.Play(Sfx.Crash);
        GameHud.Say("THE FERRIS WHEEL BROKE LOOSE!");
    }

    void Roll()
    {
        // Drop to the ground first, then roll left across the park.
        Vector3 p = wheel.position;
        p.y = Mathf.MoveTowards(p.y, wheelRadius, 9f * Time.deltaTime);
        Vector3 next = p + Vector3.left * rollSpeed * Time.deltaTime;
        bool onGround = OnGround(next + Vector3.left * wheelRadius);

        if (rolled < rollDistance && onGround)
        {
            p = next;
            rolled += rollSpeed * Time.deltaTime;
            angle += rollSpeed / wheelRadius * Mathf.Rad2Deg * Time.deltaTime; // rolling without slipping
            wheel.localRotation = Quaternion.Euler(0, 0, angle);
            FlattenPeople(p);
            if (Time.time >= nextPanic)
            {
                Chaos(p);
                nextPanic = Time.time + 0.5f;
            }
        }
        else
        {
            // Topple over: tip the flat wheel back until it lies on the ground like a decal.
            wheel.rotation = Quaternion.RotateTowards(wheel.rotation, Quaternion.Euler(90f, 0f, angle), 120f * Time.deltaTime);
            p.y = Mathf.MoveTowards(p.y, 0.02f, 3f * Time.deltaTime);
            if (Quaternion.Angle(wheel.rotation, Quaternion.Euler(90f, 0f, angle)) < 0.5f)
            {
                Current = State.Wrecked;
                ParkChaos.Add(p, chaosRadius, chaosDuration);
                Sfx.Play(Sfx.Crash, 0.8f, 0.7f);
                GameHud.Say("The Ferris wheel is wrecked. Closed for repairs!");
                Mischief.Complete(Mischief.Task.WreckWheel);
            }
        }
        wheel.position = p;
    }

    bool OnGround(Vector3 p)
    {
        Vector3Int cell = ground.WorldToCell(p);
        cell.z = 0;
        return ground.HasTile(cell);
    }

    void FlattenPeople(Vector3 center)
    {
        foreach (var v in Visitor.All)
        {
            if (v.Current is Visitor.State.Down or Visitor.State.Riding) continue;
            Vector3 d = v.transform.position - center;
            if (Mathf.Abs(d.x) < wheelRadius * 0.8f && Mathf.Abs(d.z) < 0.8f)
            {
                v.KnockDown();
                Sfx.Play(Sfx.Crash, 0.6f, Random.Range(0.9f, 1.2f));
            }
        }
    }

    void Chaos(Vector3 at)
    {
        foreach (var v in Visitor.All)
        {
            Vector3 d = v.transform.position - at;
            d.y = 0;
            if (d.magnitude <= chaosRadius) v.Panic(at, panicTime);
        }
    }

    void UpdateCabins()
    {
        if (Current is < State.Rolling or State.Jammed)
        {
            // Hang upright from evenly spaced points on the spinning rim.
            for (int i = 0; i < cabins.Count; i++)
            {
                float a = (angle + i * 360f / cabins.Count) * Mathf.Deg2Rad;
                cabins[i].position = wheel.position + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * cabinRadius + new Vector3(0, 0, -0.05f);
            }
            return;
        }

        // Flung cabins fall until they hit the ground.
        for (int i = 0; i < cabins.Count; i++)
        {
            if (cabins[i].position.y <= 0.9f && cabinVelocity[i].y <= 0) continue; // landed (cabin hangs from its top)
            cabinVelocity[i] += Vector3.down * 15f * Time.deltaTime;
            Vector3 p = cabins[i].position + cabinVelocity[i] * Time.deltaTime;
            p.y = Mathf.Max(p.y, 0.9f);
            cabins[i].position = p;
            cabins[i].rotation = Quaternion.Euler(0, 0, cabinVelocity[i].x * -4f);
        }
    }
}
