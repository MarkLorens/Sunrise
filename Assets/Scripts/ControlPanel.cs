using UnityEngine;

// The Ferris wheel's control panel, in the operator's booth (FingerprintArea). Only the operator works it, from inside
// the booth. Once the technician has started the wheel again on its loosened hinge (ElectricalBox), turn it up to max
// speed and it shakes itself off the hinge (FerrisWheel.MaxSpeed).
[RequireComponent(typeof(Interactable))]
public class ControlPanel : MonoBehaviour, IInteraction
{
    public FerrisWheel wheel;
    public RideOperator rideOperator;
    public SpriteRenderer sparks;
    public FingerprintArea booth;

    void Start() => sparks.enabled = false;

    public string Prompt(Visitor host) => host != null && host.brain is RideOperator ? "Turn the speed all the way up" : "Control panel";

    public string Blocked(Visitor host)
    {
        if (host.brain is not RideOperator) return GameText.OperatorOnly;
        if (booth != null && !booth.Contains(host.transform.position)) return GameText.PanelFromBooth;
        if (!wheel.Powered) return GameText.NoPower;
        if (wheel.Current != FerrisWheel.State.Running) return GameText.AlreadyMaxed;
        if (!wheel.Wobbly) return GameText.PanelNotYet;
        return null;
    }

    public void Interact(Visitor host)
    {
        sparks.enabled = true;
        ClosedCaptions.Show(Captions.ControlPanel);
        GameHud.Say(GameText.MaxedSpeed);
        GameHud.Think(GameHud.ThoughtPriority.Action, GameText.ThinkMaxSpeed);
        Mischief.Complete(Mischief.Task.MaxSpeed);
        wheel.MaxSpeed();
    }
}
