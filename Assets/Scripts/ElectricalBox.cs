using UnityEngine;

// The Ferris wheel's electrical box. It's the mechanic's, and it's locked: only his keys open it. Once it's open,
// flip the switch and the wheel loses power.
[RequireComponent(typeof(Interactable))]
public class ElectricalBox : MonoBehaviour, IInteraction
{
    public FerrisWheel wheel;

    public bool Unlocked { get; private set; }

    public string Prompt(Visitor host) => Unlocked ? "Cut the Ferris wheel's power" : "Unlock the mechanic's electrical box";

    public string Blocked(Visitor host)
    {
        if (!wheel.Powered) return GameText.PowerOff;
        if (!Unlocked && host.HeldKind != VisitorItem.Keys) return GameText.BoxLocked;
        return null;
    }

    public void Interact(Visitor host)
    {
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
}
