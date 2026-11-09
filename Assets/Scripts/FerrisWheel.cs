using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// A flat Ferris wheel: the wheel sprite spins in its own plane and the cabins hang upright around it.
// Two ways to wreck it:
//   1. The puzzle: loosen the support (WheelHinge), then run the ride at full speed (ControlPanel). After `loadTime`
//      at full speed the support gives way, the wheel rolls across the park flattening whoever's in the way, then
//      topples flat onto the ground.
//   2. Run it at full speed, then get something long and sturdy into the gear box at its base (GearBox): it jams to a
//      sudden stop and flings the cabins and riders out.
// Either way the level's objective is done (GameHud's banner): panic within `chaosRadius` for `chaosDuration`, police
// tape around the wreck after `policeDelay`, then once the chaos is over it's closed for `rebuildTime` and comes back
// good as new, ready to be broken the other way.
// When the scene starts it marks out its staff-only closed area (StaffArea) with the support hinge and gear box
// inside, and puts the electrical box just outside it. Scale the whole object to resize the ride: radii, part offsets
// and the closed area are all in unscaled units and grow with it.
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
    public float cameraFollowTime = 3f; // seconds the camera follows the wheel once it breaks loose
    public float chaosRadius = 10f;
    public float chaosDuration = 300f; // people keep screaming and running near the wreck for 5 minutes
    public float panicTime = 8f;
    public float gearRadius = 1.6f;   // around the gear box: where a thrown item lands in the gears
    [Range(0, 1)] public float riderDeathChance = 0.6f;
    public float loadTime = 6f;       // seconds a loosened support holds at full speed
    public float policeDelay = 10f;
    public float policeLineRadius = 5f;
    public float rebuildTime = 600f;
    public Vector3 electricalBoxOffset = new(-3.3f, 0f, -0.6f);
    public Vector3 hingeOffset = new(-1.4f, 0f, -1f);
    public Vector3 gearBoxOffset = new(0.5f, 0f, -0.8f);
    public Vector3 closedAreaOffset = new(0f, 0f, 0.3f);
    public Vector2 closedAreaSize = new(5.4f, 3.4f);
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
    public StaffArea Area { get; private set; }
    public IReadOnlyList<Transform> Cabins => cabins;
    float Scale => transform.lossyScale.x;

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

    // Method 2: something lands in the gears. The wheel stops dead and the cabins, riders and all, fly off the rim.
    public void Jam(Item item)
    {
        if (IsBroken) return;
        item.Consume();
        Current = State.Jammed;
        stateTime = 0f;
        brokeAt = Time.time;
        float fling = speed * Mathf.Deg2Rad * cabinRadius * Scale; // how fast the rim was going
        for (int i = 0; i < cabins.Count; i++)
        {
            float a = (angle + i * 360f / cabins.Count) * Mathf.Deg2Rad;
            cabins[i].SetParent(null, true);
            cabinVelocity.Add(new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f) * fling + new Vector3(0f, 2f, Random.Range(-1.5f, 0.5f)));
        }
        speed = 0f;
        if (queue != null) queue.ThrowOutRiders(transform.position + new Vector3(0, 0, -1.5f), 3.5f, riderDeathChance);
        Chaos(transform.position);
        ParkChaos.Add(transform.position, chaosRadius, chaosDuration);
        Sfx.Play(Sfx.Crash, 1f, 0.6f);
        Fx.Burst("Fx/puff", transform.position + Vector3.up, 20, new Color(0.9f, 0.85f, 0.8f), 3f, 0.9f, 0.8f);
        Fx.Shake(0.4f);
        GameHud.Say($"CRUNCH! The {item.DisplayName} jams the gears. The wheel stops dead and the cabins fly off!", "Bubbles/exclaim");
        Mischief.Complete(Mischief.Task.WheelJammed);
        GameHud.ObjectiveComplete("The gears jammed and the cabins flew off!");
    }

    // Before anyone's Start, so the spawner can keep visitors out of it.
    void Awake() => Area = ClosedArea();

    void Start()
    {
        wheelHome = wheel.localPosition;
        wheelParent = wheel.parent;
        speed = normalSpeed;
        rebuildBar = WorldBar.Create(transform, new Vector3(0f, 1.6f, -0.3f), 2.5f);
        Part<ElectricalBox>("Electrical Box", electricalBoxOffset, "Props/electrical_box", 1.3f).wheel = this;
        Part<WheelHinge>("Support Hinge", hingeOffset, "Props/support_hinge", 0.9f).wheel = this;
        Part<GearBox>("Gear Box", gearBoxOffset, "Props/gear_box", 1f).wheel = this;
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

    // The cabin at the bottom of the wheel, where riders get on. Null once it's broken.
    public Transform BoardingCabin()
    {
        if (IsBroken) return null;
        Transform best = null;
        foreach (var c in cabins)
            if (best == null || c.position.y < best.position.y) best = c;
        return best;
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

    // Offsets are relative to the wheel's base.
    T Part<T>(string name, Vector3 offset, string sprite, float height) where T : Component
    {
        var go = new GameObject(name);
        go.SetActive(false); // so Interactable's Awake finds the rules component
        go.transform.position = transform.position + offset * Scale;
        Prop(go.transform, sprite, height, Vector3.zero);
        var part = go.AddComponent<T>();
        go.AddComponent<Interactable>();
        go.SetActive(true);
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
        CameraRig.Spotlight(wheel, cameraFollowTime, wheelRadius);
        Chaos(transform.position);
        ParkChaos.Add(transform.position, chaosRadius, chaosDuration);
        Sfx.Play(Sfx.Crash);
        GameHud.Say("THE FERRIS WHEEL BROKE LOOSE!");
    }

    void Roll()
    {
        // Drop to the ground first, then roll left across the park.
        Vector3 p = wheel.position;
        float radius = wheelRadius * Scale;
        p.y = Mathf.MoveTowards(p.y, radius, 9f * Time.deltaTime);
        Vector3 next = p + Vector3.left * rollSpeed * Time.deltaTime;
        bool onGround = OnGround(next + Vector3.left * radius);

        if (rolled < rollDistance && onGround)
        {
            p = next;
            rolled += rollSpeed * Time.deltaTime;
            angle += rollSpeed / radius * Mathf.Rad2Deg * Time.deltaTime; // rolling without slipping
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
                Mischief.Complete(Mischief.Task.WheelRolled);
                GameHud.ObjectiveComplete("The wheel broke loose and rolled over the park!");
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
            if (Mathf.Abs(d.x) < wheelRadius * Scale * 0.8f && Mathf.Abs(d.z) < 0.8f)
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
        if (Current < State.Rolling)
        {
            // Hang upright from evenly spaced points on the spinning rim.
            for (int i = 0; i < cabins.Count; i++)
            {
                float a = (angle + i * 360f / cabins.Count) * Mathf.Deg2Rad;
                cabins[i].position = wheel.position + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * cabinRadius * Scale + new Vector3(0, 0, -0.05f);
            }
            return;
        }

        // Flung cabins fall until they hit the ground.
        for (int i = 0; i < cabins.Count; i++)
        {
            if (cabins[i].position.y <= 0.9f * Scale && cabinVelocity[i].y <= 0) continue; // landed (cabin hangs from its top)
            cabinVelocity[i] += Vector3.down * 15f * Time.deltaTime;
            Vector3 p = cabins[i].position + cabinVelocity[i] * Time.deltaTime;
            p.y = Mathf.Max(p.y, 0.9f * Scale);
            cabins[i].position = p;
            cabins[i].rotation = Quaternion.Euler(0, 0, cabinVelocity[i].x * -4f);
        }
    }
}
