using UnityEngine;

// The fingerprint lock on the Ferris wheel's platform (WheelPlatform). Only the ride technician's finger opens it.
[RequireComponent(typeof(Interactable))]
public class FingerprintScanner : MonoBehaviour, IInteraction
{
    public WheelPlatform platform;

    SpriteRenderer screen;

    void Start() => screen = ParkArt.Solid(transform, "Props/fingerprint_scanner", 1.2f, new Color(0.3f, 0.35f, 0.45f));

    static bool IsTechnician(Visitor host) => host != null && host.brain is RideTechnician;

    public string Prompt(Visitor host) => IsTechnician(host) ? "Scan your fingerprint" : "Fingerprint lock";

    public string Blocked(Visitor host)
    {
        if (platform.Open) return GameText.PlatformOpen;
        return IsTechnician(host) ? null : GameText.TechnicianOnly;
    }

    public void Interact(Visitor host) => Unlock(false);

    void Unlock(bool quiet)
    {
        platform.Open = true;
        if (quiet) return;
        Sfx.Play(Sfx.TaskDone, 0.4f, 1.6f);
        Fx.Burst("Fx/sparkle", transform.position + Vector3.up * 1.1f, 8, new Color(0.4f, 1f, 0.6f), 2f, 0.2f, 0.4f);
        GameHud.Say(GameText.ScannedFinger);
        GameHud.Think(GameText.ThinkPlatform);
        Mischief.Complete(Mischief.Task.OpenPlatform);
    }

    // Loading a save (SaveGame).
    public void Restore()
    {
        if (Mischief.IsDone(Mischief.Task.OpenPlatform)) Unlock(true);
    }
}
