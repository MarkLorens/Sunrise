using System.Collections.Generic;
using UnityEngine;

// The ride technician: on call for the Ferris wheel, actually fast asleep at his stand (behind its counter, his sleeping
// face on: Resources/Workers/worker_face_sleep). Ring him (the operator does when the wheel loses power) and all you get
// is a snore. Something loud enough (the hot dog stand blowing up, PropaneTank) wakes him, but he stays at his stand
// (out of reach: nobody can possess him there) until he picks up the call about the wheel. Then he comes out and goes to fiddle with its electrical box, where he
// can be possessed like other staff (this script is switched off while he is); he carries a screwdriver, and only
// his fingerprint opens the wheel's platform (FingerprintArea).
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public class RideTechnician : MonoBehaviour
{
    public static readonly List<RideTechnician> All = new();

    public float snoreHeight = 2.6f; // where the Zzz floats, over his booth
    public float standBack = 0.3f;   // behind his stand's counter (the Technician Post's picture), from its spot
    public float standSink = 0f;     // drawn this much lower there (world units), to sit his face under the stand's roof

    public bool IsAwake { get; private set; }

    CharacterSprite look;
    GroundWalker walker;
    Visitor body;
    Transform booth, zzz;
    Vector3 door;
    bool called; // picked up the call: off to the electrical box
    bool outside; // out of his booth
    bool napping;   // asleep on his feet (the sleep face, dozing, the Zzz): Nap

    void OnEnable() => All.Add(this);

    void OnDisable()
    {
        All.Remove(this);
        snoreSound?.Release();
    }

    void OnDestroy() => snoreSound?.Release();

    void Awake()
    {
        look = GetComponent<CharacterSprite>();
        walker = GetComponent<GroundWalker>();
        walker.speed = 2.2f;
        if (!TryGetComponent(out body)) body = gameObject.AddComponent<Visitor>(); // loads the sprite
        body.brain = this;
    }

    void Start()
    {

        // He naps behind his stand's counter (the Technician Post's sprite, in the scene), out of reach, and comes out
        // its front.
        booth = new GameObject("Technician Snore").transform;
        booth.position = transform.position;
        door = transform.position + Vector3.back * 0.9f;
        var rb = GetComponent<Rigidbody>();
        rb.position = transform.position = transform.position + Vector3.forward * standBack;
        zzz = Snore();
        body.possessable = false;
        look.Sink(standSink);
        Nap(true);
    }

    void Update()
    {
        SnoreSound(napping && !IsAwake); // dozing off again after the bang (no call yet) is silent: he's been woken
        if (!outside)
        {
            look.Face(transform.position + Vector3.back); // facing out over the counter
            if (napping) zzz.localPosition = new Vector3(0.6f, snoreHeight + Mathf.Sin(Time.time * 2f) * 0.08f, -0.05f);
            return;
        }
        if (!Box(out var box)) return;
        Vector3 spot = box.transform.position + Vector3.back * 0.9f;
        if (walker.WalkTo(spot)) return;
        look.Face(box.transform.position);
        look.pose = look.think; // fiddling with it
    }

    static bool Box(out ElectricalBox box) => (box = FindAnyObjectByType<ElectricalBox>()) != null;

    // The Zs drifting up out of his booth (SleepZFx). The point also marks where his voice comes from once he's up.
    Transform Snore()
    {
        var go = new GameObject("Zzz");
        go.transform.SetParent(booth, false);
        go.AddComponent<SleepZFx>();
        return go.transform;
    }

    void StopSnoring() => Nap(false);

    LoopingSound snoreSound; // his snoring (FMODEvents.technicianSnore), from his booth
    float snoreRange;        // how far off it's heard (the event's max distance): the caption shows within it

    // Snoring while he naps until he's first woken (FMOD spaces the snores out itself), stopped the moment he wakes.
    void SnoreSound(bool on)
    {
        var events = FMODEvents.instance;
        if (!on || events == null || events.technicianSnore.IsNull)
        {
            snoreSound?.Release(); // cut off mid-snore (Stop would let a long snore play out after he's up)
            return;
        }
        if (snoreSound == null)
        {
            snoreSound = new LoopingSound(booth.gameObject);
            snoreRange = 20f; // FMOD's default, if the event can't be looked up
            if (FMODUnity.RuntimeManager.StudioSystem.getEventByID(events.technicianSnore.Guid, out var sound) == FMOD.RESULT.OK)
                sound.getMinMaxDistance(out _, out snoreRange);
        }
        snoreSound.Play(events.technicianSnore);
        if (Ears.Near(booth.position, snoreRange)) ClosedCaptions.Hold(Captions.Snoring);
    }

    // Asleep on his feet (the sleep face, breathing and nodding off: CharacterSprite.Doze, the Zzz drifting up) or
    // awake and talking (his own face, standing still).
    void Nap(bool on)
    {
        napping = on;
        look.dozing = on;
        if (on) look.Expression("sleep", 0f);
        else look.ClearExpression();
        if (zzz != null) zzz.GetComponent<SleepZFx>().enabled = on;
    }

    // Something blew up. He yells from inside his booth; if the wheel's already down he takes the call he slept through.
    // Awake (WakeUp shows it; quiet: loading a save). If the wheel's already down, he takes the call he slept through.
    public void Wake(bool quiet = false)
    {
        if (IsAwake) return;
        IsAwake = true;
        StopSnoring(); // the point still marks where his voice comes from
        Mischief.Complete(Mischief.Task.WakeTechnician);
        foreach (var w in FerrisWheel.All)
            if (!w.Powered) Answer(true); // WakeUp has already said his lines by now
    }

    // The power-cut payoff (FerrisWheel): the camera on his booth while his phone rings, and he snores on.
    // The hot dog stand's payoff (PropaneTank): the camera on his booth while he's still snoring, the bang jolts him
    // awake, and he yells from inside. He only comes out if there's a call about the wheel to take; otherwise he grumbles
    // and stays in his booth, out of reach.
    // Where the camera looks at him in his booth: halfway down from his Zzz to his middle (on the Zzz itself the booth
    // sat low in the shot; on his middle it rode up out of the top of it).
    Transform shotPoint;
    Transform ShotPoint()
    {
        if (shotPoint == null) shotPoint = new GameObject("Technician Shot").transform;
        Vector3 middle = transform.position + Vector3.up * (GetComponent<Visitor>().HeadHeight * 0.5f);
        shotPoint.position = Vector3.Lerp(middle, zzz.position, 0.5f);
        return shotPoint;
    }

    public System.Collections.IEnumerator WakeUp()
    {
        CameraRig.Spotlight(ShotPoint(), 999f, 2f); // a little under his Zzz, so his booth sits up in the shot
        LightUp();
        yield return new WaitForSeconds(1.2f); // the camera getting there: still snoring
        Fx.Burst("Fx/puff", zzz.position + Vector3.up * 0.3f, 8, Color.white, 2f, 0.35f, 0.5f);
        StopSnoring();
        AudioManager.TryPlayOn(e => e.technicianAwake, gameObject); // jolted awake
        ClosedCaptions.Show(Captions.Startled);
        GameHud.Shout(transform, "!!", 0.8f);
        yield return new WaitForSeconds(0.6f);
        GameHud.Shout(transform, GameText.TechnicianWake, 1.8f);
        yield return new WaitForSeconds(2f);
        bool call = false;
        foreach (var w in FerrisWheel.All) call |= !w.Powered;
        if (!call)
        {
            Wake(true); // awake, staying put until someone calls
            GameHud.Shout(transform, GameText.TechnicianStaysIn, 2.5f);
            yield return new WaitForSeconds(2.7f);
            Nap(true); // and back to sleep, till the phone goes
            CutsceneLight.Off();
            yield break;
        }
        SayOnMyWay(); // the call he slept through
        yield return new WaitForSeconds(2.7f);
        Wake(true); // takes it: out he comes
        yield return new WaitForSeconds(1.5f);
        CutsceneLight.Off();
    }

    // The park dark round him, his booth and his Zzz (CutsceneLight), while the camera's on him.
    void LightUp()
    {
        var spawner = FindAnyObjectByType<VisitorSpawner>();
        CutsceneLight.On(this, spawner != null ? spawner.technicianPost : null, zzz);
    }

    public System.Collections.IEnumerator SleepThroughCall()
    {
        CameraRig.Spotlight(ShotPoint(), 999f, 2f); // his booth up in the shot
        LightUp();
        yield return new WaitForSeconds(1.2f); // the camera getting there
        GameHud.Shout(transform, GameText.PhoneRing, 1.4f);
        StartCoroutine(RingPhone(3.8f)); // rings on through his snore, till the camera leaves
        yield return new WaitForSeconds(1.6f);
        GameHud.Shout(transform, GameText.TechnicianSnore, 2f);
        yield return new WaitForSeconds(2.2f);
        CutsceneLight.Off();
    }

    // The operator ringing him (RideOperator). Asleep: nothing.
    public void Ring()
    {
        if (IsAwake) Answer(false);
    }

    // He picks up (from inside), then comes out and heads for the electrical box.
    void Answer(bool quiet)
    {
        if (called) return;
        called = true;
        Nap(false); // awake to pick up
        if (quiet)
        {
            ComeOut();
            return;
        }
        StartCoroutine(RingPhone(1.7f)); // till he picks up
        Invoke(nameof(SayOnMyWay), 1.7f);
        Invoke(nameof(ComeOut), 3.5f);
    }

    // The phone in his booth ringing (FMODEvents.phoneRing, 3D at his booth; the event loops, so it's cut off after
    // `seconds`: he picks up, or the call's over). Captioned while it rings.
    System.Collections.IEnumerator RingPhone(float seconds)
    {
        var events = FMODEvents.instance;
        var audio = AudioManager.instance;
        if (audio == null || events == null || events.phoneRing.IsNull) yield break;
        var ring = audio.CreateInstance(events.phoneRing);
        if (!ring.isValid()) yield break; // not in the built banks
        ring.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(booth.position));
        ring.start();
        for (float end = Time.time + seconds; Time.time < end;)
        {
            ClosedCaptions.Hold(Captions.PhoneRing);
            yield return null;
        }
        audio.ReleaseInstance(ring);
    }

    void SayOnMyWay() => GameHud.Shout(transform, GameText.TechnicianCall, 2.5f);

    void ComeOut()
    {
        outside = true;
        body.possessable = true; // out from behind the counter: like any other staff
        look.Sink(0f);
        var rb = GetComponent<Rigidbody>();
        rb.position = transform.position = new Vector3(door.x, transform.position.y, door.z);
        look.SetVisible(true);
        Nap(false);
        look.Expression("surprised_1", 2.5f); // still rattled from the bang
    }

    // Loading a save (SaveGame).
    public void Restore()
    {
        if (!Mischief.IsDone(Mischief.Task.WakeTechnician)) return;
        Wake(true);
        if (!outside) Nap(true); // woken by the bang, then back to sleep till the phone goes
    }
}
