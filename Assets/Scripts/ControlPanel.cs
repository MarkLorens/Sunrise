using UnityEngine;

// The Ferris wheel's control panel. With the operator gone: mash buttons to send the wheel into overdrive,
// then pour a drink over the panel while it's overdriven to short it out and break the wheel loose.
[RequireComponent(typeof(Interactable))]
public class ControlPanel : MonoBehaviour, IInteraction
{
    public FerrisWheel wheel;
    public RideOperator rideOperator;
    public SpriteRenderer sparks;

    void Update()
    {
        // Crackles while the wheel is being pushed past its limits.
        bool crackling = wheel.IsOverdrive ? Random.value < 0.08f : wheel.Current == FerrisWheel.State.Breaking;
        sparks.enabled = crackling && Random.value < 0.6f;
        if (sparks.enabled) sparks.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
    }

    public string Prompt(Visitor host)
    {
        if (wheel.IsOverdrive && host != null && host.HeldKind == VisitorItem.Drink) return "Pour the drink over the panel";
        return "Mash random buttons";
    }

    public string Blocked(Visitor host)
    {
        if (wheel.IsBroken) return "Nothing left to break";
        if (rideOperator.WatchingPanel) return "The operator is watching the panel";
        if (wheel.IsOverdrive && host.HeldKind != VisitorItem.Drink) return "It's already going haywire... something wet might finish it off";
        return null;
    }

    public void Interact(Visitor host)
    {
        if (wheel.IsOverdrive)
        {
            host.TakeItem();
            wheel.BreakLoose();
            GameHud.Say("*BZZZT!* The panel short-circuits!");
        }
        else
        {
            wheel.Overdrive();
            GameHud.Say("The wheel starts spinning faster... and faster...");
            Mischief.Complete(Mischief.Task.Overdrive);
        }
    }
}
