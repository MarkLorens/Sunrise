using UnityEngine;

// The Ferris wheel's control panel. Only the operator knows how to work it: possess him and push the ride to full
// speed. Back at his post he'd turn it down again, unless someone fries the panel with a drink while it's maxed out.
// A loosened support (WheelHinge) won't hold the wheel at full speed for long.
[RequireComponent(typeof(Interactable))]
public class ControlPanel : MonoBehaviour, IInteraction
{
    public FerrisWheel wheel;
    public RideOperator rideOperator;
    public SpriteRenderer sparks;

    void Update()
    {
        // Crackles while the wheel is being pushed past its limits, constantly once fried.
        bool crackling = wheel.IsOverdrive ? Random.value < (wheel.PanelFried ? 0.3f : 0.08f) : wheel.Current == FerrisWheel.State.Breaking;
        sparks.enabled = crackling && Random.value < 0.6f;
        if (sparks.enabled) sparks.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
    }

    static bool IsOperator(Visitor host) => host != null && host.brain is RideOperator;

    public string Prompt(Visitor host)
    {
        if (IsOperator(host)) return "Start the ride at full speed";
        if (wheel.IsOverdrive && host != null && host.HeldKind == VisitorItem.Drink) return "Pour the drink over the panel";
        return "Control panel";
    }

    public string Blocked(Visitor host)
    {
        if (wheel.IsBroken) return "Nothing left to break";
        if (wheel.PanelFried) return "It's fried. Stuck at full speed";
        if (IsOperator(host)) return wheel.IsOverdrive ? "It's already at full speed" : null;
        if (rideOperator.WatchingPanel) return "The operator is watching the panel";
        if (!wheel.IsOverdrive) return "Only the operator knows how to work this";
        if (host.HeldKind != VisitorItem.Drink) return "It's going haywire... something wet might fry it for good";
        return null;
    }

    public void Interact(Visitor host)
    {
        if (IsOperator(host))
        {
            wheel.Overdrive();
            GameHud.Say("The wheel starts spinning faster... and faster...");
            Mischief.Complete(Mischief.Task.Overdrive);
            return;
        }
        host.TakeItem();
        wheel.FryPanel();
        GameHud.Say("*BZZZT!* The panel fries. The wheel is stuck at full speed!");
        Mischief.Complete(Mischief.Task.FryPanel);
    }
}
