using System.Collections.Generic;
using UnityEngine;

// The ride technician: on call for the Ferris wheel, actually fast asleep in his booth. Ring him (the operator does
// when the wheel loses power) and all you get is a snore. Something loud enough (the hot dog stand blowing up,
// PropaneTank) wakes him: he stumbles out, picks up the call, and goes to fiddle with the wheel's electrical box.
// Awake, he can be possessed like other staff (this script is switched off while he is); he carries a screwdriver,
// and only his fingerprint opens the wheel's platform (FingerprintArea).
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

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Awake()
    {
        look = GetComponent<CharacterSprite>();
        walker = GetComponent<GroundWalker>();
        walker.speed = 2.2f;
        if (!TryGetComponent(out body)) body = gameObject.AddComponent<Visitor>(); // loads the sprite
        body.brain = this;
        body.possessable = false;
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
        if (!IsAwake)
        {
            look.SetVisible(false); // behind the booth door
            zzz.localPosition = new Vector3(0.6f, snoreHeight + Mathf.Sin(Time.time * 2f) * 0.08f, -0.05f);
            return;
        }
        if (!called || !Box(out var box)) return;
        Vector3 spot = box.transform.position + Vector3.back * 0.9f;
        if (walker.WalkTo(spot)) return;
        look.Face(box.transform.position);
        look.pose = look.think; // fiddling with it
    }

    static bool Box(out ElectricalBox box) => (box = FindAnyObjectByType<ElectricalBox>()) != null;

    // ponytail: built-in TextMesh, swap for a Bubbles/sleep sprite once there's art for one.
    Transform Snore()
    {
        var go = new GameObject("Zzz");
        go.transform.SetParent(booth, false);
        var text = go.AddComponent<TextMesh>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = "Zzz";
        text.fontSize = 48;
        text.characterSize = 0.08f;
        text.fontStyle = FontStyle.Bold;
        text.anchor = TextAnchor.LowerLeft;
        text.color = new Color(0.75f, 0.85f, 1f);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = text.font.material;
        r.sortingOrder = ParkArt.OverheadOrder;
        return go.transform;
    }

    // Something blew up. Out he comes; if the wheel's already down he takes the call he slept through.
    public void Wake(bool quiet = false)
    {
        if (IsAwake) return;
        IsAwake = true;
        body.possessable = true;
        zzz.gameObject.SetActive(false);
        var rb = GetComponent<Rigidbody>();
        rb.position = transform.position = new Vector3(door.x, transform.position.y, door.z);
        look.SetVisible(true);
        Mischief.Complete(Mischief.Task.WakeTechnician);
        if (!quiet) GameHud.Shout(transform, GameText.TechnicianWake, 1.6f);
        foreach (var w in FerrisWheel.All)
            if (!w.Powered) Answer(quiet);
    }

    // The power-cut payoff (FerrisWheel): the camera on his booth while his phone rings, and he snores on.
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

    void Answer(bool quiet)
    {
        if (called) return;
        called = true;
        if (!quiet) Invoke(nameof(SayOnMyWay), 1.7f);
    }

    void SayOnMyWay() => GameHud.Shout(transform, GameText.TechnicianCall, 2.5f);

    // Loading a save (SaveGame).
    public void Restore()
    {
        if (Mischief.IsDone(Mischief.Task.WakeTechnician)) Wake(true);
    }
}
