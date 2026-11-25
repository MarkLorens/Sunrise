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
// When the scene starts it puts the electrical box next to it. Scale the whole object to resize the ride: radii and
// part offsets are all in unscaled units and grow with it.
public class FerrisWheel : MonoBehaviour
{
    public static readonly List<FerrisWheel> All = new();

    // The finale's under way: a wheel has started tearing loose (Breaking on), for the rest of the game.
    public static bool Runaway => All.Exists(w => w.Broken);

    public enum State { Running, Unpowered, Overdrive, Breaking, Rolling, Wrecked }

    public Transform wheel;
    // One cabin per arm: its picture (pivot at the knob it hangs from) and where on the wheel's picture that arm's bulb
    // is, in wheel units from the hub with the wheel unturned. The two lists go round in the same order.
    public Sprite[] cabinSprites;
    public Vector2[] cabinMounts;
    public float cabinDrop = 1.45f; // from a cabin's knob down to the bottom of its bowl, in wheel units
    public float wheelRadius = 3.1f; // outer rim (what it rolls on), where the light bulbs go
    public float normalSpeed = 12f;  // degrees per second
    public float windDownTime = 5f;  // seconds to coast to a stop once the power's cut
    public Vector3 electricalBoxOffset = new(-3.3f, 0f, -0.6f);
    public Transform electricalBoxAt; // set: the box stands here instead of at electricalBoxOffset
    // Drawn at its own size, pivot at its feet (the palette's props). Empty: Props/electrical_box at the offset, or,
    // with electricalBoxAt set, no picture of its own (the Objects tilemap draws it there).
    public Sprite electricalBoxArt;
    public Sprite electricalBoxOpenArt; // the box once the electrician's unlocked it (ElectricalBox), drawn like electricalBoxArt
    public RideQueue queue;
    public float payoffTime = 6f;     // the camera stays on the wheel this long once the power's cut
    public float payoffPullBack = 8f;  // how much further back the camera sits to fit the whole wheel
    [Header("Finale (max speed)")]
    public Transform parkEntrance;          // where everyone runs to once the wheel's rolled; the camera goes to look
    public Transform exitGate;              // they all squeeze through here together on the way (empty: straight for it)
    public float exodusScatter = 70f;       // then scatter, up to this many degrees either side of straight out
    public Transform exodusView;            // where the camera looks meanwhile: the gate, with them pouring out through it
                                            // (empty: parkEntrance itself)
    public float entrancePullBack = 3f;     // how far back the camera sits there, to take in the stampede
    public float glideToEntrance = 7f;      // seconds drifting over from the wreck (past the closed stalls)
    public float watchExodus = 5f;          // seconds watching them run out
    public float glideHome = 3f;            // seconds back to the player
    public float finaleZoom = 0.5f;         // how far in the camera zooms on the player at the end (CameraRig.Zoom)
    public float irisCloseTime = 0.5f;      // seconds for the black to close in
    [Range(0.01f, 1f)] public float irisSoftness = 0.35f; // how soft the edge of the black is
    public float finaleHold = 3f;           // seconds on the player, gloating (their last word's up meanwhile)
    public float irisShrinkTime = 0.5f;     // then seconds for the circle to shrink away to black
    public float blackHold = 0.5f;          // and seconds of black before the main menu
    public int screamingCabins = 4;   // cabins shouting in turns (GameText.CabinScreams, or CabinScreamsWobbly) in a payoff
    public float cabinShake = 4f;     // degrees the stuck cabins rock
    public float wobble = 0.08f;      // how far the wheel lurches side to side once the hinge is loose (wheel units)
    public float overdriveSpeed = 140f; // degrees per second at max speed
    public float spinUpTime = 6f;     // seconds to get there
    public float holdTime = 2f;       // seconds the loose hinge holds at max speed
    public float breakDelay = 1.5f;   // seconds of shuddering before it tears off
    public float rollSpeed = 6f;
    public float rollDistance = 22f;
    public float rollStopDistance = 6f; // it brakes over this last bit of rollDistance
    public float stopWobbleTime = 3f;   // then rocks back and forth this many seconds, settling, before it keels over
    public Vector3 rollDirection = new(1f, 0f, -0.6f); // across the ground; it has to go mostly sideways to read as rolling
    public float veerAfter = 6f;  // it rolls straight across the screen this far (as it was facing) as it comes down
    public float veerRate = 3f;   // then curves round onto rollDirection, turning this many degrees per unit it rolls
    public float toppleGravity = 90f;  // degrees/s² it keels over with (times how far over it is): higher = falls faster
    [Range(0f, 0.6f)] public float toppleBounce = 0.25f; // how much it bounces back up off the ground (0: none)
    public int gawkers = 4;           // visitors who wander over to watch it spin flat out, right where it'll land
    public int toiletLine = 3;        // and lined up at the toilet there (VisitorSpawner's toilet line), as it spins up
    public int picnickers = 3;        // fresh visitors sat at the picnic table there as it spins up (frozen when it comes)
    public int onlookers = 3;         // and standing about there, frozen in shock when it comes
    public SpriteRenderer entranceArch; // the park's entrance arch (empty: the one drawn with Props/New/entrance)
    public Sprite moodyEntrance;      // the gate from the finale on (empty: Props/New/entrance_moody)
    public int doomedShouts = 3;      // how many of those in its way cry out (GameText.WheelDoomed) as it tears loose

    readonly List<Visitor> doomed = new();
    Vector3 rollStart;

    public State Current { get; private set; }
    public bool Powered => Current != State.Unpowered;
    public bool Broken => Current >= State.Breaking;
    public bool Wobbly { get; private set; }
    // For its sounds (FerrisWheelAudio): how far it's got spinning up at max speed, 0 at its normal speed to 1 flat out
    // (0 the rest of the time); torn off its hinge; rolling (not yet keeling over), and how fast, 1 flat out to 0 still.
    public float Overdrive => Current is State.Overdrive or State.Breaking ? Mathf.InverseLerp(normalSpeed, overdriveSpeed, speed) : 0f;
    public bool TornOff => Current >= State.Rolling;
    public bool Rolling => Current == State.Rolling && !toppling;
    public float RollSpeed => Rolling ? Mathf.Clamp01(rollVelocity / rollSpeed) : 0f;
    public IReadOnlyList<Transform> Cabins => cabins;
    float Scale => transform.lossyScale.x;

    readonly List<Transform> cabins = new();
    float angle;
    float speed;
    float shakeUntil;
    Vector3 wheelHome;
    float stateTime, rolled, wobbled; // wobbled: seconds rocking at the end of the roll
    bool toppling, landed;   // keeling over (Roll), and hit the ground once
    float tilt, tiltSpeed;   // how far over it's gone (0 upright, 90 flat) and how fast, degrees
    float yaw;               // which way it's facing as it rolls (PathAt), degrees: 0 side-on to the camera
    Vector3 pathFrom;        // where it tore loose (on the ground)
    Vector3 hinge;           // the bottom of its rim, where it keels over from
    float rollVelocity;      // how fast it's going along its path, units a second
    readonly List<Vector3> cabinVelocity = new();
    readonly List<bool> cabinDown = new(); // each flung cabin, once it's hit the ground

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
        speed = normalSpeed;
        wheelHome = wheel.localPosition;
        if (!TryGetComponent(out FerrisWheelAudio _)) gameObject.AddComponent<FerrisWheelAudio>();
        var box = Part<ElectricalBox>("Electrical Box", electricalBoxOffset, "Props/electrical_box", 1.3f, electricalBoxArt, electricalBoxAt);
        box.gameObject.AddComponent<ZapFx>(); // live electrics, always buzzing
        box.wheel = this;
        box.openArt = electricalBoxOpenArt;
        var mat = wheel.GetComponent<SpriteRenderer>().sharedMaterial;
        for (int i = 0; i < cabinMounts.Length; i++)
        {
            var go = new GameObject($"Cabin {i + 1}");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = cabinSprites[i % cabinSprites.Length];
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
        if (TryGetComponent(out FerrisWheelAudio audio)) audio.RestoreUnpowered();
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
        AudioManager.MusicOff = true;
        CameraRig.Spotlight(wheel, 999f, (wheelRadius + cabinDrop) * Scale, payoffPullBack); // down to the lowest cabin
        ScreamIn(GameText.CabinScreamsWobbly);
        yield return new WaitForSeconds(1.5f);
        Fx.Shake(0.15f, payoffTime - 1.5f);
        yield return new WaitForSeconds(payoffTime - 1.5f);
        CameraRig.EndSpotlight();
        AudioManager.MusicOff = false;
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
        AudioManager.MusicOff = true;
        CameraRig.Spotlight(wheel, 999f, (wheelRadius + cabinDrop) * Scale, payoffPullBack); // down to the lowest cabin
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
        AudioManager.MusicOff = false;
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
            // Where the cabin is as the scream starts, held there: a bubble riding a cabin whipping round a wheel at
            // full tilt (lurching, rocking, shaking loose) can't be read. Shout puts it 2 up from a bare marker.
            var r = cabin.GetComponentInChildren<SpriteRenderer>();
            Bounds b = r != null ? r.bounds : new Bounds(cabin.position, Vector3.zero);
            var at = new GameObject("Scream").transform;
            at.position = new Vector3(b.center.x, b.max.y - 2f, b.center.z);
            GameHud.Shout(at, line, show);
            // Heard too: the cabin's riders' scream (NPC-01 until the cabin's own is in the banks), carrying from
            // the spotlit wheel (AudioManager.TryPlayScared).
            if (!AudioManager.TryPlayScared(e => e.cabinScream, cabin.gameObject)) AudioManager.TryPlayScared(e => e.npcScream, cabin.gameObject);
            ClosedCaptions.Show(Captions.CabinScreams);
            yield return new WaitForSeconds(show);
            Destroy(at.gameObject);
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
        if (at != null && art == null)
        {
            pic.enabled = false; // the Objects tilemap draws it (and its footprint is what people bump into)
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
        // Its feet on the ground, wherever its picture's pivot is (the middle for most, the bottom for the barriers).
        float pivot = r.sprite != null ? r.sprite.pivot.y / r.sprite.rect.height : 0.5f;
        r.transform.localPosition = localFeet + new Vector3(0f, height * pivot, 0f);
        return r;
    }

    // ---------------------------------------------------------------- max speed (ControlPanel)

    public void MaxSpeed()
    {
        if (Current != State.Running) return;
        Set(State.Overdrive);
        StartCoroutine(MaxSpeedPayoff());
        GatherGawkers();
    }

    void Set(State s)
    {
        Current = s;
        stateTime = 0f;
    }

    // The finale, and the end of the game for now. The camera stays on the wheel the whole way: spinning up, tearing off,
    // rolling, toppling. Then everyone still standing, staff and all, runs for the park's entrance (Visitor.Flee) while
    // the camera drifts over there past the closed stalls and the stampede, watches them go, and comes back to the
    // player: zoomed in, the screen closing to black round them like an old cartoon's iris, the spirit's last word.
    // Then the main menu.
    IEnumerator MaxSpeedPayoff()
    {
        GoMoody(); // out of shot: the camera's on the wheel
        Possession.InputBlocked = true;
        // Out of the operator, as the camera goes to the wheel: the ending's iris closes on Ruckus themself.
        if (Possession.Instance != null) Possession.Instance.Release();
        AudioManager.MusicOff = true;
        CameraRig.Spotlight(wheel, 999f, (wheelRadius + cabinDrop) * Scale, payoffPullBack); // down to the lowest cabin
        ScreamIn(GameText.CabinScreamsWobbly);
        while (Current != State.Wrecked) yield return null;
        yield return new WaitForSeconds(2f);
        Mischief.Complete(Mischief.Task.WheelRolled);
        Aftermath.Add(new Vector3(wheel.position.x, 0f, wheel.position.z), wheelRadius * Scale + 1f, 9999f);

        GameHud.HideObjectives = true; // the rest is the ending: no objectives or prompts over it
        Vector3 exit = parkEntrance != null ? parkEntrance.position : wheel.position;
        foreach (var v in new List<Visitor>(Visitor.All)) v.Flee(exit, exitGate != null ? exitGate.position + (exit - exitGate.position).normalized * 1f : null, exodusScatter);

        var shot = new GameObject("Finale Shot").transform;
        shot.position = wheel.position;
        float pull = payoffPullBack;
        IEnumerator Glide(System.Func<Vector3> to, float toPull, float seconds)
        {
            Vector3 from = shot.position;
            float fromPull = pull;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                shot.position = Vector3.Lerp(from, to(), k);
                pull = Mathf.Lerp(fromPull, toPull, k);
                CameraRig.Spotlight(shot, 999f, 1f, pull);
                yield return null;
            }
        }
        var spirit = Possession.Instance;
        Vector3 view = exodusView != null ? exodusView.position : exit;
        yield return Glide(() => view, entrancePullBack, glideToEntrance);
        yield return new WaitForSeconds(watchExodus);
        yield return Glide(() => spirit.Focus.position, 0f, glideHome);
        CameraRig.EndSpotlight();
        Destroy(shot.gameObject);

        // The iris: zoomed right in, black closing in round the player while they gloat.
        CameraRig.Zoom(finaleZoom);
        var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
        var iris = profile.Add<UnityEngine.Rendering.Universal.Vignette>();
        iris.color.Override(Color.black);
        iris.intensity.Override(1f);
        iris.smoothness.Override(irisSoftness);
        iris.rounded.Override(true);
        var volume = new GameObject("Finale Iris").AddComponent<UnityEngine.Rendering.Volume>();
        volume.isGlobal = true;
        volume.priority = 40; // over the night's own vignette (NightLighting)
        volume.sharedProfile = profile;
        volume.weight = 0f;
        var dark = profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(); // for the very last of the light
        dark.colorFilter.Override(Color.white);
        var cam = Camera.main;
        var look = spirit.GetComponent<SpiritSprite>();
        GameHud.Think(GameHud.ThoughtPriority.Critical, GameText.ThinkRolled);
        for (float t = 0f; t < finaleHold + irisShrinkTime; t += Time.deltaTime)
        {
            // On the middle of Ruckus as drawn (bobbing with them); a person's middle is about 1 up from their feet.
            Vector3 middle = look != null && look.HasBody ? look.Middle : spirit.Focus.position + Vector3.up;
            if (cam != null) iris.center.Override((Vector2)cam.WorldToViewportPoint(middle));
            volume.weight = Mathf.Clamp01(t / irisCloseTime);
            // Their gloat said, the circle shrinks away to nothing: its edge widening into the middle while what's
            // left of the picture fades out, black by the end.
            float shut = Mathf.Clamp01((t - finaleHold) / irisShrinkTime);
            iris.smoothness.Override(Mathf.Lerp(irisSoftness, 1f, shut));
            dark.colorFilter.Override(Color.Lerp(Color.white, Color.black, shut * shut));
            yield return null;
        }
        yield return new WaitForSeconds(blackHold);
        EndCredits.Play(); // then the main menu
    }

    // The gate changes to its moody picture (moodyEntrance) as the wheel spins up, while the camera's on the wheel: by
    // the time it gets to the gate for the stampede, the gate's already like that.
    void GoMoody()
    {
        var arch = entranceArch;
        if (arch == null)
            foreach (var r in FindObjectsByType<SpriteRenderer>())
                if (r.sprite != null && r.sprite.name == "entrance")
                {
                    arch = r;
                    break;
                }
        var art = moodyEntrance != null ? moodyEntrance : ParkArt.Get("Props/New/entrance_moody");
        if (arch == null || art == null || arch.sprite == art) return;
        arch.transform.localScale *= arch.sprite.bounds.size.x / art.bounds.size.x; // as wide as before
        arch.sprite = art;
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
                    ClosedCaptions.Show(Captions.WheelBreaking);
                    AudioManager.TryPlay(e => e.wheelBreaking, wheel.position);
                    StartCoroutine(BraceDoomed());
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
        angle -= speed * Time.deltaTime; // clockwise, as seen from the camera: the way it rolls off (rollDirection, to the right)
        wheel.localRotation = Quaternion.Euler(0, 0, angle);
    }

    void UpdateCabins()
    {
        if (Current < State.Rolling)
        {
            // Hang upright from the arms' bulbs as the wheel turns.
            for (int i = 0; i < cabins.Count; i++)
            {
                cabins[i].position = wheel.TransformPoint(cabinMounts[i]) + new Vector3(0, 0, -0.05f);
                // Stuck cabins rock, each to its own rhythm, settling as the payoff ends.
                float rock = Wobbly ? cabinShake : Mathf.Clamp01((shakeUntil - Time.time) / 1.5f) * cabinShake;
                cabins[i].localRotation = Quaternion.Euler(0, 0, (Mathf.PerlinNoise(Time.time * 3f, i * 1.7f) * 2f - 1f) * rock);
            }
            return;
        }

        // Flung cabins fall until they hit the ground.
        for (int i = 0; i < cabins.Count; i++)
        {
            float landed = RaisedFloor.Height(cabins[i].position) + cabinDrop * Scale; // a cabin hangs from its knob
            if (cabins[i].position.y <= landed && cabinVelocity[i].y <= 0) continue;
            cabinVelocity[i] += Vector3.down * 15f * Time.deltaTime;
            Vector3 p = cabins[i].position + cabinVelocity[i] * Time.deltaTime;
            p.y = Mathf.Max(p.y, landed);
            cabins[i].position = p;
            cabins[i].rotation = Quaternion.Euler(0, 0, cabinVelocity[i].x * -4f);
            if (p.y <= landed && cabinVelocity[i].y < 0f && !cabinDown[i])
            {
                cabinDown[i] = true; // crashing down, once (not a low cabin that's only just being flung up)
                AudioManager.TryPlay(e => e.cabinCrash, p);
                ClosedCaptions.Show(Captions.CabinsCrash);
            }
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
            cabinDown.Add(false);
        }
        if (queue != null) queue.ThrowOutRiders(transform.position + Vector3.back * 1.5f, 3f);
        // Everything standing anywhere it'll roll over or come to lie on (not its own parts).
        Vector3 from = pathFrom = new(wheel.position.x, 0f, wheel.position.z);
        standing = StandingProps.Near(from + PathAt(rollDistance, out _) * 0.5f, rollDistance * 0.5f + wheelRadius * Scale + 2f,
            t => t.IsChildOf(transform) || t.IsChildOf(wheel) || cabins.Exists(c => t.IsChildOf(c)));
        Fx.Shake(0.45f, 1f);
        ClosedCaptions.Show(Captions.WheelCollapse);
        AudioManager.TryPlay(e => e.wheelCrash, wheel.position);
        ParkChaos.Add(transform.position, 12f, 9999f);
    }

    // Down to the ground, then along its path (PathAt), flattening people; then it tips over flat.
    void Roll()
    {
        float radius = wheelRadius * Scale;
        Vector3 p = wheel.position;
        float floor = RaisedFloor.Height(p); // the platform, or the ground once it's rolled off

        if (wobbled < stopWobbleTime)
        {
            p.y = Mathf.MoveTowards(p.y, floor + radius, 9f * Time.deltaTime); // down onto its rim
            float step;
            float left = rollDistance - rolled;
            float crawl = rollSpeed * 0.45f; // what it's down to as it reaches the end, and rocks past it with
            if (left > 0f && wobbled == 0f) // (once it's rocking, rocking back behind the end doesn't count)
            {
                // Flat out, then braking steadily over the last rollStopDistance.
                float v = left > rollStopDistance ? rollSpeed
                    : Mathf.Sqrt(crawl * crawl + (rollSpeed * rollSpeed - crawl * crawl) * left / rollStopDistance);
                step = Mathf.Min(v * Time.deltaTime, left);
            }
            else
            {
                // Rocks back and forth about where it stopped, each swing smaller, till it's still.
                if (wobbled == 0f)
                {
                    AudioManager.TryPlay(e => e.wheelRocking, p);
                    ClosedCaptions.Show(Captions.WheelRocking);
                }
                wobbled += Time.deltaTime;
                const float swing = 3.5f; // radians a second: about a swing and back every 1.8 s
                float rock = crawl / swing * Mathf.Exp(-3f * wobbled / stopWobbleTime) * Mathf.Sin(swing * wobbled);
                step = rock - (rolled - rollDistance);
            }
            rolled += step;
            rollVelocity = Time.deltaTime > 0f ? Mathf.Abs(step) / Time.deltaTime : 0f;
            Vector3 at = pathFrom + PathAt(rolled, out yaw); // facing the way it's going
            p.x = at.x;
            p.z = at.z;
            Vector3 dir = Heading(yaw);
            // Rolling without slipping: clockwise going right, so it carries on the way it was spinning, flat out.
            angle -= Mathf.Sign(rollDirection.x) * step / radius * Mathf.Rad2Deg;
            wheel.rotation = Quaternion.Euler(0, yaw, angle);
            Flatten(p, dir, radius);
            SquashAlong(p, dir, radius);
            ParkChaos.Add(p, 10f, 8f);
        }
        else
        {
            // Topple: keels over backwards on the bottom of its rim, which stays put on the ground. Slow at first, then
            // faster the further over it is, like a felled tree; it slams down, bounces once and lies flat.
            if (!toppling)
            {
                toppling = true;
                rollVelocity = 0f;
                hinge = new Vector3(p.x, floor, p.z);
                tiltSpeed = 4f; // the nudge that starts it going: barely leaning at first
                AudioManager.TryPlay(e => e.wheelTopple, p);
                ClosedCaptions.Show(Captions.WheelToppling);
            }
            tiltSpeed += toppleGravity * Mathf.Sin(Mathf.Max(tilt, 3f) * Mathf.Deg2Rad) * Time.deltaTime;
            tilt += tiltSpeed * Time.deltaTime;
            if (tilt >= 90f)
            {
                tilt = 90f;
                if (!landed)
                {
                    landed = true;
                    Vector3 down = hinge + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0.05f, radius);
                    Fx.Shake(0.4f, 0.8f);
                    ClosedCaptions.Show(Captions.WheelWrecked);
                    AudioManager.TryPlay(e => e.wheelSmash, down); // its bounce is in the sound, about 0.6 s on
                    Fx.Burst("Fx/puff", down, 30, new Color(0.7f, 0.65f, 0.6f), 6f, 1.2f, 1.2f);
                    SquashUnder(down, radius);
                    ParkChaos.Add(down, 12f, 9999f);
                }
                tiltSpeed *= -toppleBounce;
                if (tiltSpeed > -20f) Set(State.Wrecked); // too little left to bounce
            }
            float t = tilt * Mathf.Deg2Rad;
            p = hinge + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, radius * Mathf.Cos(t) + 0.05f * Mathf.Sin(t), radius * Mathf.Sin(t));
            wheel.rotation = Quaternion.Euler(tilt, yaw, angle); // tips over backwards, off its turned face
        }
        wheel.position = p;
    }

    // Anyone in the wheel's path, under the rim (it's edge-on to its direction of travel, so: a thin band across it).
    // Visitors go flat as pancakes (Visitor.Squash); staff, and the body the spirit's in, just go down as before.
    static void Flatten(Vector3 center, Vector3 dir, float radius)
    {
        Vector3 side = new(-dir.z, 0f, dir.x);
        foreach (var v in Visitor.All.ToArray())
        {
            if (v.Current is Visitor.State.Down or Visitor.State.Riding || !v.Look.Visible) continue;
            Vector3 d = v.transform.position - center;
            d.y = 0f;
            if (Mathf.Abs(Vector3.Dot(d, dir)) > 0.8f || Mathf.Abs(Vector3.Dot(d, side)) > radius * 0.3f) continue;
            if (Crushable(v)) v.Squash();
            else v.KnockDown(); // the spirit's body: knocked out, not flattened
            ClosedCaptions.Show(Captions.Flattened);
        }
    }

    // Buildings, trees, lamps... (StandingProps) it rolls over go flat too, like the people (StandingProps.Squash), as
    // it passes: within the same thin band under its rim, a building's width counting.
    List<StandingProps.Prop> standing; // what's left standing anywhere it'll go
    void SquashAlong(Vector3 center, Vector3 dir, float radius)
    {
        if (standing == null) return;
        Vector3 side = new(-dir.z, 0f, dir.x);
        for (int i = standing.Count - 1; i >= 0; i--)
        {
            var s = standing[i];
            Vector3 d = s.at - center;
            d.y = 0f;
            if (Mathf.Abs(Vector3.Dot(d, dir)) > 0.8f + s.half * 0.5f || Mathf.Abs(Vector3.Dot(d, side)) > radius * 0.3f + s.half) continue;
            StandingProps.Squash(s);
            standing.RemoveAt(i);
        }
    }

    // A visitor going about their day (not staff, not the body the spirit's in).
    static bool Squashable(Visitor v) =>
        v.brain == null && (Possession.Instance == null || Possession.Instance.Host != v) && v.Current != Visitor.State.Possessed;

    // Anyone it rolls over or lands on goes flat, staff too: everyone but the body the spirit's in.
    static bool Crushable(Visitor v) =>
        (Possession.Instance == null || Possession.Instance.Host != v) && v.Current != Visitor.State.Possessed;

    // ---------------------------------------------------------------- the runaway wheel's victims (MaxSpeedPayoff)

    // Where it'll come to rest, lying flat: rollDistance along its path from where it tears loose.
    Vector3 RestPoint(Vector3 from) => from + PathAt(rollDistance, out _);

    // `along` units into its roll: how far it's got from where it set off (on the ground), and which way it's facing
    // (yaw, degrees). Straight across the screen at first, side-on to the camera as it was, then after veerAfter
    // curving round onto rollDirection. It never turns past side-on, so the picture's never seen from behind.
    Vector3 PathAt(float along, out float heading)
    {
        float s = Mathf.Sign(rollDirection.x);
        float turnTo = Mathf.Atan2(-rollDirection.z * s, rollDirection.x * s) * Mathf.Rad2Deg;
        const float ds = 0.25f;
        Vector3 at = Vector3.zero;
        for (float d = 0f; d < along; d += ds)
        {
            float step = Mathf.Min(ds, along - d);
            at += Heading(Mathf.MoveTowards(0f, turnTo, Mathf.Max(0f, d + step * 0.5f - veerAfter) * veerRate)) * step;
        }
        heading = Mathf.MoveTowards(0f, turnTo, Mathf.Max(0f, along - veerAfter) * veerRate);
        return at;
    }

    // The way along the ground it rolls when facing yaw (to the right, or the left if rollDirection goes left).
    Vector3 Heading(float yaw) => Mathf.Sign(rollDirection.x) * new Vector3(Mathf.Cos(yaw * Mathf.Deg2Rad), 0f, -Mathf.Sin(yaw * Mathf.Deg2Rad));

    // As it spins up, a few visitors nearby wander over to watch, right where it'll end up. They stay put. It lands in
    // a corner of the park, so they may come from a way off (GawkerReach), hurrying from past GawkerWalk.
    const float GawkerReach = 32f, GawkerWalk = 14f;
    void GatherGawkers()
    {
        Vector3 rest = RestPoint(new Vector3(wheel.position.x, 0f, wheel.position.z));
        float radius = wheelRadius * Scale;
        LineUpAtToilet(rest, radius);
        var near = new List<Visitor>();
        foreach (var v in Visitor.All)
            if (Squashable(v) && v.Look.Visible && v.Current is Visitor.State.Idle or Visitor.State.Wander or Visitor.State.Stare)
                near.Add(v);
        near.Sort((a, b) => Flat(a.transform.position - rest).CompareTo(Flat(b.transform.position - rest)));
        for (int i = 0; i < near.Count && doomed.Count < gawkers; i++)
        {
            var v = near[i];
            float far = Flat(v.transform.position - rest);
            if (far > GawkerReach) break;
            var walker = v.GetComponent<GroundWalker>();
            for (int tries = 0; tries < 8; tries++)
            {
                Vector2 r = Random.insideUnitCircle * radius * 0.6f;
                Vector3 spot = rest + new Vector3(r.x, 0f, r.y);
                // Ground to stand on, clear of props (the route there goes round whatever's in the way).
                Vector3Int cell = walker.ground.WorldToCell(spot);
                cell.z = 0;
                if (!GroundWalker.InPark(walker.ground, cell) || GroundWalker.NearObstacle(spot, 0.35f)) continue;
                v.SendTo(spot, run: far > GawkerWalk); // from further off, hurrying, to be there in time
                doomed.Add(v);
                break;
            }
        }
        CrowdTheCorner(rest, radius);
    }

    // Where it lands, busy: fresh visitors sat at the picnic table there and standing about, every one of them frozen
    // to the spot when it comes (doomed: BraceDoomed holds them, staring, panic-faced).
    void CrowdTheCorner(Vector3 rest, float radius)
    {
        foreach (var table in StandQueue.All.ToArray())
        {
            if (!table.IsSeating || Flat(table.transform.position - rest) > radius + 4f) continue;
            for (int i = 0; i < picnickers && table.HasRoom; i++) doomed.Add(Seat(table, Fresh(table.transform.position)));
        }
        var ground = FindAnyObjectByType<VisitorSpawner>().ground;
        for (int i = 0, tries = 0; i < onlookers && tries < 40; tries++)
        {
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(radius * 0.7f, radius + 2.5f);
            Vector3 spot = rest + new Vector3(r.x, 0f, r.y);
            Vector3Int cell = ground.WorldToCell(spot);
            cell.z = 0;
            if (!GroundWalker.InPark(ground, cell) || GroundWalker.NearObstacle(spot, 0.4f)) continue;
            var v = Fresh(spot);
            if (v == null) break;
            v.Hold(); // standing there, watching it spin
            doomed.Add(v);
            i++;
        }
        doomed.RemoveAll(v => v == null);
    }

    // A new visitor (VisitorSpawner's kind) standing at `at`.
    static Visitor Fresh(Vector3 at)
    {
        var spawner = FindAnyObjectByType<VisitorSpawner>();
        return spawner != null ? spawner.SpawnVisitor(at) : null;
    }

    // `v` in `line` (a toilet's, or a picnic table's seats), already there.
    static Visitor Seat(StandQueue line, Visitor v)
    {
        if (v == null || !line.Join(v)) return v;
        v.EnterQueue(line);
        v.transform.position = line.SlotPosition(v);
        return v;
    }

    static float Flat(Vector3 d) => new Vector2(d.x, d.z).magnitude;

    // The toilet where it'll land has a line (toiletLine long) when it does: nobody comes out any more, and the
    // nearest people about make it up to toiletLine, walking over to join it.
    void LineUpAtToilet(Vector3 rest, float radius)
    {
        foreach (var line in StandQueue.All.ToArray())
        {
            if (!line.name.StartsWith("Toilet") || Flat(line.transform.position - rest) > radius + 2f) continue;
            line.serving = () => false;
            var near = new List<Visitor>();
            foreach (var v in Visitor.All)
                if (Squashable(v) && v.Look.Visible && v.Group == null && v.Current is Visitor.State.Idle or Visitor.State.Wander)
                    near.Add(v);
            Vector3 at = line.transform.position;
            // Fresh visitors already stood in it, then the nearest about make up any shortfall.
            for (int i = line.Waiting; i < toiletLine && line.HasRoom; i++) Seat(line, Fresh(at));
            near.Sort((a, b) => Flat(a.transform.position - at).CompareTo(Flat(b.transform.position - at)));
            for (int i = 0; i < near.Count && line.Waiting < toiletLine; i++)
            {
                if (!line.Join(near[i])) break;
                near[i].EnterQueue(line);
            }
        }
    }

    // It tears loose: everyone in its way (on its path, or where it'll lie) freezes, staring at it, panic-faced, and a
    // few cry out. Then it comes.
    IEnumerator BraceDoomed()
    {
        rollStart = new Vector3(wheel.position.x, 0f, wheel.position.z);
        Vector3 rest = RestPoint(rollStart);
        float radius = wheelRadius * Scale;
        foreach (var v in Visitor.All)
        {
            if (!Squashable(v) || !v.Look.Visible || v.Current is Visitor.State.Down or Visitor.State.Riding || doomed.Contains(v)) continue;
            Vector3 d = v.transform.position - rollStart;
            d.y = 0f;
            bool onPath = Flat(d) <= radius * 0.3f + 0.5f; // where it sets off, then every unit along its path
            for (float a = 1f; a <= rollDistance && !onPath; a += 1f)
                onPath = Flat(d - PathAt(a, out _)) <= radius * 0.3f + 0.5f;
            if (onPath || Flat(v.transform.position - rest) <= radius) doomed.Add(v);
        }
        doomed.RemoveAll(v => v == null || v.Current is Visitor.State.Down or Visitor.State.Riding or Visitor.State.Possessed);
        foreach (var v in doomed)
        {
            v.Hold(); // frozen to the spot
            v.Look.panicFace = true;
            v.Look.Face(wheel.position);
        }
        // A few cry out, one after another, each something different.
        var lines = new List<string>(GameText.WheelDoomed);
        var shouters = new List<Visitor>(doomed);
        shouters.Sort((a, b) => Flat(a.transform.position - rollStart).CompareTo(Flat(b.transform.position - rollStart)));
        for (int i = 0; i < shouters.Count && i < doomedShouts && lines.Count > 0; i++)
        {
            var v = shouters[i];
            if (v.Current == Visitor.State.Down) continue;
            int pick = Random.Range(0, lines.Count);
            GameHud.Shout(v.transform, lines[pick], 2.4f);
            if (AudioManager.TryPlayScared(e => e.npcScream, v.gameObject)) ClosedCaptions.Show(Captions.DoomedScreams);
            lines.RemoveAt(pick);
            yield return new WaitForSeconds(0.45f);
        }
    }

    // Lying flat at last: anyone under it is squashed; whoever it missed comes to and runs.
    void SquashUnder(Vector3 center, float radius)
    {
        if (standing != null)
            foreach (var s in standing)
                if (Flat(s.at - center) <= radius * 0.95f + s.half * 0.3f) StandingProps.Squash(s); // and everything it lands on
        standing = null;
        foreach (var v in Visitor.All.ToArray())
        {
            if (!Crushable(v) || !v.Look.Visible || v.Current is Visitor.State.Down or Visitor.State.Riding) continue;
            if (Flat(v.transform.position - center) > radius * 0.95f) continue;
            v.Squash();
            ClosedCaptions.Show(Captions.Flattened);
        }
        foreach (var v in doomed)
        {
            if (v == null || v.Current == Visitor.State.Down) continue;
            v.Free();
            v.Panic(center, 4f);
        }
        doomed.Clear();
    }
}
