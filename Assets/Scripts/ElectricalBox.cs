using System.Collections;
using UnityEngine;

// The Ferris wheel's electrical box. It's the mechanic's, and it's locked: only his keys open it. Once it's open,
// flip the switch and the wheel loses power. Only the ride technician can start it again.
// The first time someone else tries it, a short cutscene shows who has the keys: the camera pans to the mechanic
// fixing a flickering lamp by the staff door, follows him back behind the staff door, then returns to the player.
[RequireComponent(typeof(Interactable))]
// It plays its own sound (IOwnSound): the box opening when it's unlocked or the power's brought back, the menu click
// for the locked-box cutscene.
public class ElectricalBox : MonoBehaviour, IInteraction, ITried, IOwnSound
{
    public FerrisWheel wheel;
    public float tinkerTime = 4f; // seconds the mechanic works on the lamp
    [HideInInspector] public Sprite openArt; // its picture once unlocked (FerrisWheel.electricalBoxOpenArt)

    public bool Unlocked { get; private set; }

    bool shownMechanic, thoughtAsSpirit;

    public bool ShownMechanic => shownMechanic;

    // Loading a save (SaveGame). Unlocked comes from the steps done.
    public void Restore(bool shown)
    {
        shownMechanic = shown;
        Unlocked = Mischief.IsDone(Mischief.Task.UnlockBox);
        if (Unlocked) ShowOpen();
        thoughtAsSpirit = shown || Unlocked || Mischief.IsDone(Mischief.Task.PossessMechanic);
    }

    // The spirit floats up to it for the first time: it works out what this is for.
    void Update()
    {
        if (thoughtAsSpirit || Unlocked || !wheel.Powered || Possession.Instance == null) return;
        if (Possession.Instance.State != Possession.Mode.Spirit || !Possession.PlayerNear(transform.position, GetComponent<Interactable>().range)) return;
        thoughtAsSpirit = true;
        GameHud.Think(GameHud.ThoughtPriority.Action, GameText.ThinkBoxSpirit);
    }

    // Opening Night's step: someone you're in has tried to open it (K, let through or refused), once the tutorial's
    // over. Floating up to it as a spirit doesn't count.
    public void Tried(Visitor host)
    {
        if (!Mischief.IsDone(Mischief.Task.ReachBox) && !Tutorial.Active) Mischief.Complete(Mischief.Task.ReachBox);
    }

    public string Prompt(Visitor host) => !wheel.Powered ? "Start the Ferris wheel" : Unlocked ? "Cut the Ferris wheel's power" : "Unlock the electrician's electrical box";

    public string Blocked(Visitor host)
    {
        if (wheel.Wobbly) return GameText.WheelWobbly;
        if (!wheel.Powered)
        {
            if (host.brain is not RideTechnician) return GameText.TechnicianStarts;
            return Mischief.IsDone(Mischief.Task.LoosenHinge) ? null : GameText.HingeFirst;
        }
        if (!Unlocked && host.HeldKind != VisitorItem.Keys) return IdleMechanic() != null ? null : GameText.BoxLocked;
        return null;
    }

    public void Interact(Visitor host)
    {
        if (!wheel.Powered)
        {
            OpenBox();
            wheel.Restart();
            ClosedCaptions.Show(Captions.PowerBackOn);
            GameHud.Say(GameText.RestartedWheel);
            Mischief.Complete(Mischief.Task.RestartWheel);
            return;
        }
        if (!Unlocked && host.HeldKind != VisitorItem.Keys)
        {
            AudioManager.TryPlay(e => e.menuClick); // G-03
            StartCoroutine(ShowMechanic(IdleMechanic()));
            return;
        }
        if (!Unlocked)
        {
            OpenBox(); // the electrician opens it up, ready to cut the power
            Unlocked = true;
            ShowOpen();
            ClosedCaptions.Show(Captions.BoxUnlocked);
            if (host.brain is Mechanic) GameHud.Think(GameHud.ThoughtPriority.Action, GameText.ThinkBoxMechanic);
            GameHud.Say(GameText.UnlockedBox);
            Mischief.Complete(Mischief.Task.UnlockBox);
            return;
        }
        Fx.Burst("Fx/sparkle", transform.position + Vector3.up * 0.8f, 12, new Color(1f, 0.9f, 0.4f), 3f, 0.3f, 0.4f);
        wheel.CutPower();
        GameHud.Say(GameText.CutPower);
        Mischief.Complete(Mischief.Task.CutPower);
        Aftermath.Add(wheel.transform.position + Vector3.back * 3f, 3f, 90f); // the crowd stares up at the stuck cabins
    }

    // Its door swinging open: the electrician opening it up (right before he cuts the power), or the technician
    // opening it to start the wheel again.
    void OpenBox()
    {
        ClosedCaptions.Show(Captions.BoxOpened);
        AudioManager.TryPlay(e => e.openElectricalBox, transform.position);
    }

    // Unlocked: drawn open from now on (its picture, not the map's: FerrisWheel gives it one of its own).
    void ShowOpen()
    {
        if (openArt == null) return;
        foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
            if (r.sprite != null && r.sprite.name.StartsWith("electrical_box")) r.sprite = openArt;
    }

    // The mechanic, if the cutscene hasn't played yet and he's behind the staff door (out and about, you can see him).
    Mechanic IdleMechanic()
    {
        if (shownMechanic || NightLighting.Instance == null) return null;
        foreach (var m in Mechanic.All)
            if (m.Current == StaffWorker.State.Inside) return m;
        return null;
    }

    IEnumerator ShowMechanic(Mechanic m)
    {
        shownMechanic = true;
        var lights = NightLighting.Instance;
        // He's behind the staff door, so this is the lamp right by it.
        if (!lights.NearestLamp(m.transform.position, out int lamp, out Vector3 post) || !StandBy(m, post, out Vector3 spot))
        {
            GameHud.Refuse(transform, GameText.BoxLocked);
            yield break;
        }

        Possession.InputBlocked = true;
        var rb = m.GetComponent<Rigidbody>();
        rb.position = m.transform.position = spot;
        lights.Sputtering = lamp;
        m.Take(new StaffWorker.Job { spot = spot, at = post, seconds = tinkerTime, done = () => lights.Sputtering = -1 });
        CameraRig.Spotlight(m.transform, 999f);
        CutsceneLight.On(m); // the park dark round him

        // Fixes the lamp, then walks back behind the staff door. Timed out in case something pulls him away.
        float giveUp = Time.time + tinkerTime + 25f;
        while (m.Current != StaffWorker.State.Inside && Time.time < giveUp) yield return null;
        lights.Sputtering = -1;

        CutsceneLight.Off();
        CameraRig.EndSpotlight();
        yield return new WaitForSeconds(1f); // the camera easing back
        Possession.InputBlocked = false;
        GameHud.Refuse(transform, GameText.BoxLocked);
        GameHud.Think(GameHud.ThoughtPriority.Action, GameText.ThinkBoxLocked);
    }

    // A clear spot next to the lamp post to stand and work, preferably with nobody in the way.
    static bool StandBy(Mechanic m, Vector3 post, out Vector3 spot)
    {
        var walker = m.GetComponent<GroundWalker>();
        var dirs = new[] { Vector3.left, Vector3.right, Vector3.back, Vector3.forward };
        for (int pass = 0; pass < 2; pass++)
            foreach (var dir in dirs)
            {
                spot = post + dir * 0.9f;
                if (walker.IsWalkable(spot) && !GroundWalker.NearObstacle(spot, 0.35f) && (pass == 1 || !Crowded(m, spot))) return true;
            }
        spot = default;
        return false;
    }

    static bool Crowded(Mechanic m, Vector3 p)
    {
        foreach (var v in Visitor.All)
        {
            Vector3 d = v.transform.position - p;
            if (v.gameObject != m.gameObject && v.Look.Visible && new Vector2(d.x, d.z).magnitude < 1.5f) return true;
        }
        return false;
    }
}
