using UnityEngine;

// The Ferris wheel's control panel: the operator's post. Nothing to do with it for now, it's just there.
[RequireComponent(typeof(Interactable))]
public class ControlPanel : MonoBehaviour, IInteraction
{
    public FerrisWheel wheel;
    public RideOperator rideOperator;
    public SpriteRenderer sparks;

    void Start() => sparks.enabled = false;

    public string Prompt(Visitor host) => "Control panel";

    public string Blocked(Visitor host) => wheel.Powered ? "Only the operator knows how to work this" : "No power";

    public void Interact(Visitor host) { }
}
