using System.Collections.Generic;
using UnityEngine;

// The ride technician: on call for the Ferris wheel, actually fast asleep in his booth. Ring him (the operator does
// when the wheel loses power) and all you get is a snore. Something loud enough (the hot dog stand blowing up,
// PropaneTank) wakes him, but he stays shut in his booth (out of sight, so out of reach: nobody can possess him)
// until he picks up the call about the wheel. Then he comes out and goes to fiddle with its electrical box, where he
// can be possessed like other staff (this script is switched off while he is); he carries a screwdriver, and only
// his fingerprint opens the wheel's platform (FingerprintArea).
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public class RideTechnician : MonoBehaviour
{
    public static readonly List<RideTechnician> All = new();

    public float snoreHeight = 2.6f; // where the Zzz floats, over his booth

    public bool IsAwake { get; private set; }

    CharacterSprite look;
    GroundWalker walker;
    Visitor body;
    Transform booth, zzz;
    Vector3 door;
    bool called; // picked up the call: off to the electrical box
    bool outside; // out of his booth

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

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

        // He naps inside his booth (the Technician Post's sprite, in the scene) and comes out its door.
        booth = new GameObject("Technician Snore").transform;
        booth.position = transform.position;
        door = transform.position + Vector3.back * 0.9f;
        zzz = Snore();
        look.SetVisible(false);
    }

    void Update()
    {
        if (!outside)
        {
            look.SetVisible(false); // behind the booth door
            if (!IsAwake) zzz.localPosition = new Vector3(0.6f, snoreHeight + Mathf.Sin(Time.time * 2f) * 0.08f, -0.05f);
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

    void StopSnoring()
    {
        if (zzz != null) zzz.GetComponent<SleepZFx>().enabled = false;
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
    public System.Collections.IEnumerator WakeUp()
    {
        CameraRig.Spotlight(zzz, 999f, 2f);
        yield return new WaitForSeconds(1.2f); // the camera getting there: still snoring
        Fx.Burst("Fx/puff", zzz.position + Vector3.up * 0.3f, 8, Color.white, 2f, 0.35f, 0.5f);
        StopSnoring();
        Sfx.Play(Sfx.Huh, 0.8f);
        GameHud.Shout(zzz, "!!", 0.8f);
        yield return Jolt(0.6f);
        GameHud.Shout(zzz, GameText.TechnicianWake, 1.8f);
        yield return new WaitForSeconds(2f);
        bool call = false;
        foreach (var w in FerrisWheel.All) call |= !w.Powered;
        if (!call)
        {
            Wake(true); // awake, staying put until someone calls
            GameHud.Shout(zzz, GameText.TechnicianStaysIn, 2.5f);
            yield return new WaitForSeconds(2.7f);
            yield break;
        }
        SayOnMyWay(); // the call he slept through
        yield return new WaitForSeconds(2.7f);
        Wake(true); // takes it: out he comes
        yield return new WaitForSeconds(1.5f);
    }

    // His booth (the Technician Post's picture) rattles, him tumbling out of bed inside.
    System.Collections.IEnumerator Jolt(float seconds)
    {
        var spawner = FindAnyObjectByType<VisitorSpawner>();
        var post = spawner != null ? spawner.technicianPost : null;
        Vector3 home = post != null ? post.position : Vector3.zero;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            if (post != null) post.position = home + new Vector3(Random.Range(-0.06f, 0.06f), 0f, 0f);
            yield return null;
        }
        if (post != null) post.position = home;
    }

    public System.Collections.IEnumerator SleepThroughCall()
    {
        CameraRig.Spotlight(zzz, 999f, 2f);
        yield return new WaitForSeconds(1.2f); // the camera getting there
        GameHud.Shout(zzz, GameText.PhoneRing, 1.4f);
        yield return new WaitForSeconds(1.6f);
        GameHud.Shout(zzz, GameText.TechnicianSnore, 2f);
        yield return new WaitForSeconds(2.2f);
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
        if (quiet)
        {
            ComeOut();
            return;
        }
        Invoke(nameof(SayOnMyWay), 1.7f);
        Invoke(nameof(ComeOut), 3.5f);
    }

    void SayOnMyWay() => GameHud.Shout(zzz, GameText.TechnicianCall, 2.5f);

    void ComeOut()
    {
        outside = true;
        var rb = GetComponent<Rigidbody>();
        rb.position = transform.position = new Vector3(door.x, transform.position.y, door.z);
        look.SetVisible(true);
        look.Expression("surprised_1", 2.5f); // still rattled from the bang
    }

    // Loading a save (SaveGame).
    public void Restore()
    {
        if (Mischief.IsDone(Mischief.Task.WakeTechnician)) Wake(true);
    }
}
