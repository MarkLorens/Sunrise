using UnityEngine;

// The lock on a FingerprintArea (it was a fingerprint scanner; now the area's locked gate): only its owner (the
// technician, the operator) can unlock it.
// Drawn as the area's metal gate (Props/New/metal_gate_closed, padlocked) in the gap in its front fence; unlocked, it
// stands open (metal_gate_opened). The spot to use it is where the scanner was.
[RequireComponent(typeof(Interactable))]
public class FingerprintScanner : MonoBehaviour, IInteraction
{
    public FingerprintArea platform;
    public Mischief.Task task = Mischief.Task.OpenPlatform; // the step it ticks off

    SpriteRenderer gate;

    // In its section of the area's front fence, as wide as the fence's other sections (FingerprintArea.GateSection).
    void Start()
    {
        Vector3 at = transform.position;
        float width = 1.2f;
        if (platform != null) platform.GateSection(out at, out width, out _);
        gate = ParkArt.Lit(ParkArt.Make("Gate", null, "Props/New/metal_gate_closed", 1f, Color.white));
        ParkArt.Fit(gate, width * gate.sprite.bounds.size.y / Mathf.Max(0.01f, gate.sprite.bounds.size.x));
        gate.transform.SetParent(transform, true);
        gate.transform.position = at;
        if (platform != null && platform.Open) ShowOpen();
    }

    void ShowOpen()
    {
        var open = ParkArt.Get("Props/New/metal_gate_opened");
        if (gate != null && open != null) gate.sprite = open; // the same canvas as the closed one: it opens in place
    }

    public string Prompt(Visitor host) => platform.IsOwner(host) ? GameText.GateUnlock : GameText.GateLocked;

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
        ShowOpen();
        if (quiet) return;
        ClosedCaptions.Show(Captions.Scanner);
        Fx.Burst("Fx/sparkle", transform.position + Vector3.up * 1.1f, 8, new Color(0.4f, 1f, 0.6f), 2f, 0.2f, 0.4f);
        GameHud.Say(GameText.ScannedFinger);
        GameHud.Think(GameHud.ThoughtPriority.Action, platform.owner == FingerprintArea.Staff.Technician ? GameText.ThinkPlatform : GameText.ThinkBooth);
        Mischief.Complete(task);
    }

    // Loading a save (SaveGame).
    public void Restore()
    {
        if (Mischief.IsDone(task)) Unlock(true);
    }
}
