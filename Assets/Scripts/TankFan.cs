using System.Collections.Generic;
using UnityEngine;

// A big standing fan. E (in anyone's body) turns its head a quarter turn clockwise: up, right, down, left (up is away
// from the camera). Whatever drifts into it goes on the way it faces: propane passes from fan to fan along a chain
// (PropaneTank) until one blows it at its target, and the smelly gutter's stink goes where its fan points (Gutter,
// Mascot). Some are guarded: the hot dog man won't let anyone touch his while he's at his stall, nor the mascot the
// locker room's.
// Drawn from whichever side it shows the camera (Resources/Park/Props/Fan): its front when it blows toward the camera,
// its back when it blows away, its side (drawn blowing left, flipped for right) otherwise. Its blades are always going
// round: on the front they spin about the hub, the artist's two drawings swapped as they go so the strokes shimmer; on
// the side the two side drawings are swapped. From the back there are none to see.
// It plays its own sound (IOwnSound): its head turning when it turns, only the "can't" sound when it's guarded.
[RequireComponent(typeof(Interactable))]
public class TankFan : MonoBehaviour, IInteraction, IOwnSound
{
    public static readonly List<TankFan> All = new();

    public enum Dir { Up, Right, Down, Left } // clockwise, the order E turns it through
    public enum Guard { None, HotDogMan, Mascot }

    public Dir facing = Dir.Right;
    public Guard guardedBy;

    // Which way it blows, along the ground.
    public Vector3 Wind => facing switch
    {
        Dir.Up => Vector3.forward,
        Dir.Right => Vector3.right,
        Dir.Down => Vector3.back,
        _ => Vector3.left,
    };

    const float Height = 1.5f;     // what it's first fitted to (ParkArt.Solid); then ArtScale and ArtRaise place it
    const float ArtScale = 0.2f;   // its pictures' size (the same for every view, so they match)
    const float ArtRaise = 1.14f;  // their middle above its feet, so it stands on the ground
    const float Spin = 900f;       // degrees a second, the front's blades
    const float FrameTime = 0.07f; // seconds each blade drawing shows
    // The front blades' hub on the front picture, from its middle (its pixels at 100 a unit; the blades' pictures are
    // cut square round their own hub). A hair in front of the fan.
    static readonly Vector3 FrontHub = new(-0.05f, 2.95f, -0.01f);

    SpriteRenderer art, blades;
    BoxCollider box;
    float spin, frameClock;
    bool secondFrame;
    float nextGust;
    [System.NonSerialized] public bool stinky; // the smelly gutter's fan (Gutter): its gusts are the gutter's stink
    bool thoughtGuarded;

    void OnEnable() => All.Add(this);
    void OnDisable()
    {
        All.Remove(this);
        hum?.Release();
        humAt = -1f;
    }

    void Start()
    {
        art = ParkArt.Solid(transform, Picture(), Height, new Color(0.6f, 0.65f, 0.7f));
        box = art.GetComponent<BoxCollider>();
        blades = ParkArt.Lit(ParkArt.Make("Blades", art.transform, null, 1f, Color.white, art.sortingOrder + 1));
        Face();
    }

    string Picture() => facing switch
    {
        Dir.Down => "Props/Fan/fan_front", // blowing at the camera
        Dir.Up => "Props/Fan/fan_back",
        _ => "Props/Fan/fan_side",
    };

    // Its picture for the way it faces (the side's drawn blowing left).
    void Face()
    {
        art.sprite = ParkArt.Get(Picture());
        if (art.TryGetComponent(out Scorch.Burnt burnt)) burnt.Redraw(art.sprite); // burnt stays burnt, whichever side it shows
        // The same size whatever the fan's own scale in the scene (some are 2): ArtScale and ArtRaise are as seen.
        float own = Mathf.Max(0.01f, transform.lossyScale.y);
        art.transform.localScale = Vector3.one * (ArtScale / own);
        art.transform.localPosition = new Vector3(0f, ArtRaise / own, 0f);
        // Flipped by its scale (the lit sprite shader ignores flipX), blades and all.
        if (facing == Dir.Right) art.transform.localScale = Vector3.Scale(art.transform.localScale, new Vector3(-1f, 1f, 1f));
        if (box != null && art.sprite != null)
        {
            Bounds b = art.sprite.bounds;
            box.center = b.center;
            box.size = new Vector3(b.size.x, b.size.y, 0.2f);
        }
        TurnBlades();
    }

    // Is `p` in this fan's stream: ahead of it, within `reach`, and no more than `lane` off to the side?
    public bool Blows(Vector3 p, float lane, float reach)
    {
        Vector3 d = p - transform.position;
        d.y = 0f;
        float ahead = Vector3.Dot(d, Wind);
        return ahead > 0.3f && ahead <= reach && (d - Wind * ahead).magnitude <= lane;
    }

    // How far ahead `p` is along the wind.
    public float Ahead(Vector3 p) => Vector3.Dot(p - transform.position, Wind);

    void Update()
    {
        TurnBlades();
        Hum();
        if (Time.time < nextGust) return;
        nextGust = Time.time + 0.2f;
        if (stinky) StinkGust(); else Gust(transform.position, Wind);
    }

    // ---------------------------------------------------------------- its hum

    // Its whirring (FMODEvents.fan, 3D at the fan, loops), panned by which way it faces: the event's "Fan Direction"
    // parameter, South 0, West 1, North 2, East 3 (each turn clockwise is one more), which FMOD's seek modulator
    // sweeps to its new value. East to South would sweep back the long way round, through North and West: if the
    // parameter goes up to 4 (South again) it sweeps on to 4 and is quietly put back to 0 once there; otherwise it
    // jumps straight to South.
    const string DirectionParameter = "Fan Direction";
    LoopingSound hum;
    float humRange;     // how far off it's heard (the event's max distance): the caption shows within it
    float humTop = 3f;  // the parameter's maximum in FMOD
    float humAt = -1f;  // the value last sent (-1: not yet, so the next one is sent straight there, no sweep)

    static float DirectionValue(Dir d) => d switch
    {
        Dir.Down => 0f,  // South
        Dir.Left => 1f,  // West
        Dir.Up => 2f,    // North
        _ => 3f,         // East
    };

    void Hum()
    {
        var events = FMODEvents.instance;
        if (events == null || events.fan.IsNull)
        {
            hum?.Stop();
            return;
        }
        if (hum == null)
        {
            hum = new LoopingSound(gameObject);
            humRange = 20f; // FMOD's default, if the event can't be looked up
            if (FMODUnity.RuntimeManager.StudioSystem.getEventByID(events.fan.Guid, out var sound) == FMOD.RESULT.OK)
            {
                sound.getMinMaxDistance(out _, out humRange);
                if (sound.getParameterDescriptionByName(DirectionParameter, out var p) == FMOD.RESULT.OK) humTop = p.maximum;
            }
        }
        hum.Play(events.fan);

        float want = DirectionValue(facing);
        if (humAt < 0f)
            Point(want, true); // just started (or loaded): already facing its way
        else if (humAt == 4f)
        {
            if (want != 0f) // turned on again before the last sweep finished
            {
                Point(0f, true);
                Point(want, false);
            }
            else if (hum.GetParameter(DirectionParameter) >= 3.99f) Point(0f, true); // there: back to 0, sounding the same
        }
        else if (want != humAt)
        {
            if (humAt == 3f && want == 0f) Point(humTop >= 4f ? 4f : 0f, humTop < 4f); // East to South
            else Point(want, false);
        }
        if (Ears.Near(transform.position, humRange)) ClosedCaptions.Hold(Captions.FanWhirring);
    }

    void Point(float value, bool instant)
    {
        hum.SetParameter(DirectionParameter, value, instant);
        humAt = value;
    }

    // Round they go: the front's spun about the hub, the side's two drawings swapped; none from the back.
    void TurnBlades()
    {
        if (blades == null) return;
        frameClock += Time.deltaTime;
        if (frameClock >= FrameTime)
        {
            frameClock = 0f;
            secondFrame = !secondFrame;
        }
        bool front = facing == Dir.Down, side = facing is Dir.Left or Dir.Right;
        blades.enabled = front || side;
        if (front)
        {
            spin = (spin + Spin * Time.deltaTime) % 360f;
            blades.sprite = ParkArt.Get(secondFrame ? "Props/Fan/fan_front_blade_b" : "Props/Fan/fan_front_blade_a");
            blades.transform.localPosition = FrontHub;
            blades.transform.localRotation = Quaternion.Euler(0f, 0f, -spin); // clockwise
        }
        else if (side)
        {
            blades.sprite = ParkArt.Get(secondFrame ? "Props/Fan/fan_side_blade_b" : "Props/Fan/fan_side_blade_a");
            blades.transform.localPosition = new Vector3(0f, 0f, -0.01f); // cut the same as the side picture
            blades.transform.localRotation = Quaternion.identity;
        }
    }

    // A streak of air off a fan.
    public static void Gust(Vector3 fan, Vector3 wind)
    {
        var g = ParkArt.Make("Gust", null, "Fx/sparkle", 0.2f, new Color(1f, 1f, 1f, 0.5f), ParkArt.OverheadOrder);
        g.transform.position = fan + Vector3.up * Random.Range(0.8f, 1.4f) + wind * 0.5f;
        var bit = g.gameObject.AddComponent<FxBit>();
        bit.velocity = wind * 6f;
        bit.life = 0.6f;
    }

    // A puff of the gutter's stink off the smelly gutter's fan: the gutter's own gas art, brown.
    void StinkGust()
    {
        var g = ParkArt.Make("Gust", null, Gutter.StinkArt, Random.Range(0.3f, 0.7f), Gutter.Stink, ParkArt.OverheadOrder);
        g.transform.position = transform.position + Vector3.up * Random.Range(0.8f, 1.4f) + Wind * 0.5f;
        var bit = g.gameObject.AddComponent<FxBit>();
        bit.velocity = Wind * 6f;
        bit.life = 0.6f;
        bit.growth = 0.25f; // spreading a little as it goes, as the propane's wisps
    }

    public string Prompt(Visitor host) => "Turn the fan";

    public string Blocked(Visitor host) => null;

    public void Interact(Visitor host)
    {
        var guard = Guarding();
        if (guard != null)
        {
            bool mascot = guardedBy == Guard.Mascot;
            GameHud.Shout(guard.transform, mascot ? GameText.MascotGuard : GameText.HotDogGuard, 2f);
            GameHud.Refuse(transform, mascot ? GameText.MascotGuards : GameText.FanGuarded); // G-06, no click
            if (!thoughtGuarded) GameHud.Think(mascot ? GameText.ThinkMascot : GameText.PickFanGuarded());
            thoughtGuarded = true;
            return;
        }
        // Its head turning (3D at the fan); the menu click (G-03) until that sound's set up.
        if (!AudioManager.TryPlay(e => e.fanTurn, transform.position)) AudioManager.TryPlay(e => e.menuClick);
        facing = (Dir)(((int)facing + 1) % 4);
        Face();
        ClosedCaptions.Show(Captions.FanTurned);
        GameHud.Say(GameText.TurnedFan(facing.ToString().ToLowerInvariant()));
    }

    Component Guarding()
    {
        if (guardedBy == Guard.Mascot) return Mascot.GuardNear(transform.position);
        if (guardedBy != Guard.HotDogMan) return null;
        foreach (var h in HotDogMan.All)
            if (h.Guarding) return h;
        return null;
    }

    // Loading a save (SaveGame).
    public void Restore(Dir dir)
    {
        facing = dir;
        humAt = -1f; // straight to its saved direction, no sweep
        if (art != null) Face();
    }
}
