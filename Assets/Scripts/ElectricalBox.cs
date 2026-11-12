using System.Collections;
using UnityEngine;

// The Ferris wheel's electrical box. It's the mechanic's, and it's locked: only his keys open it. Once it's open,
// flip the switch and the wheel loses power.
// The first time someone else tries it, a short cutscene shows who has the keys: the camera pans to the mechanic
// fixing a flickering lamp by the staff door, follows him back behind the staff door, then returns to the player.
[RequireComponent(typeof(Interactable))]
public class ElectricalBox : MonoBehaviour, IInteraction
{
    public FerrisWheel wheel;
    public float tinkerTime = 4f; // seconds the mechanic works on the lamp

    public bool Unlocked { get; private set; }

    bool shownMechanic;

    public string Prompt(Visitor host) => Unlocked ? "Cut the Ferris wheel's power" : "Unlock the mechanic's electrical box";

    public string Blocked(Visitor host)
    {
        if (!wheel.Powered) return GameText.PowerOff;
        if (!Unlocked && host.HeldKind != VisitorItem.Keys) return IdleMechanic() != null ? null : GameText.BoxLocked;
        return null;
    }

    public void Interact(Visitor host)
    {
        if (!Unlocked && host.HeldKind != VisitorItem.Keys)
        {
            StartCoroutine(ShowMechanic(IdleMechanic()));
            return;
        }
        if (!Unlocked)
        {
            Unlocked = true;
            Sfx.Play(Sfx.HitWood, 0.7f, 1.5f);
            GameHud.Say(GameText.UnlockedBox);
            Mischief.Complete(Mischief.Task.UnlockBox);
            return;
        }
        Fx.Burst("Fx/sparkle", transform.position + Vector3.up * 0.8f, 12, new Color(1f, 0.9f, 0.4f), 3f, 0.3f, 0.4f);
        wheel.CutPower();
        GameHud.Say(GameText.CutPower);
        Mischief.Complete(Mischief.Task.CutPower);
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

        // Fixes the lamp, then walks back behind the staff door. Timed out in case something pulls him away.
        float giveUp = Time.time + tinkerTime + 25f;
        while (m.Current != StaffWorker.State.Inside && Time.time < giveUp) yield return null;
        lights.Sputtering = -1;

        CameraRig.EndSpotlight();
        yield return new WaitForSeconds(1f); // the camera easing back
        Possession.InputBlocked = false;
        GameHud.Refuse(transform, GameText.BoxLocked);
    }

    // A clear spot next to the lamp post to stand and work, preferably with nobody in the way (the cable man's nearby).
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
