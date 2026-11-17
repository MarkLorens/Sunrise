using UnityEngine;

// The fingerprint lock on a FingerprintArea: only its owner's finger (the technician's, the operator's) opens it.
[RequireComponent(typeof(Interactable))]
public class FingerprintScanner : MonoBehaviour, IInteraction
{
    public FingerprintArea platform;
    public Mischief.Task task = Mischief.Task.OpenPlatform; // the step it ticks off

    void Start() => ParkArt.Solid(transform, "Props/fingerprint_scanner", 1.2f, new Color(0.3f, 0.35f, 0.45f));

    public string Prompt(Visitor host) => platform.IsOwner(host) ? "Scan your fingerprint" : "Fingerprint lock";

    public string Blocked(Visitor host)
    {
        if (platform.Open) return GameText.PlatformOpen;
        if (platform.IsOwner(host)) return null;
        return platform.owner == FingerprintArea.Staff.Technician ? GameText.TechnicianOnly : GameText.OperatorFingerOnly;
    }

    public void Interact(Visitor host) => Unlock(false);

    void Unlock(bool quiet)
    {
        platform.Open = true;
        if (quiet) return;
        Sfx.Play(Sfx.TaskDone, 0.4f, 1.6f);
        Fx.Burst("Fx/sparkle", transform.position + Vector3.up * 1.1f, 8, new Color(0.4f, 1f, 0.6f), 2f, 0.2f, 0.4f);
        GameHud.Say(GameText.ScannedFinger);
        GameHud.Think(platform.owner == FingerprintArea.Staff.Technician ? GameText.ThinkPlatform : GameText.ThinkBooth);
        Mischief.Complete(task);
    }

    // Loading a save (SaveGame).
    public void Restore()
    {
        if (Mischief.IsDone(task)) Unlock(true);
    }
}
