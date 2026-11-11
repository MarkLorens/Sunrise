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
        if (!wheel.Powered) return "The power's off";
        if (!Unlocked && host.HeldKind != VisitorItem.Keys) return "Locked. The mechanic has the keys";
        return null;
    }

    public void Interact(Visitor host)
    {
        if (!Unlocked)
        {
            Unlocked = true;
            Sfx.Play(Sfx.HitWood, 0.7f, 1.5f);
            GameHud.Say("*click* The electrical box swings open.");
            Mischief.Complete(Mischief.Task.UnlockBox);
            return;
        }
        Fx.Burst("Fx/sparkle", ParkArt.Above(transform.position, 0.8f), 12, new Color(1f, 0.9f, 0.4f), 3f, 0.3f, 0.4f);
        wheel.CutPower();
        Mischief.Complete(Mischief.Task.CutPower);
    }
}
