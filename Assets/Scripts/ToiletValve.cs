using UnityEngine;

// The water valve beside the toilet (ToiletPrank): in a body, each K cranks the pressure up a notch, with the
// Water Valve sound (so no menu click: IOwnSound).
[RequireComponent(typeof(Interactable))]
public class ToiletValve : MonoBehaviour, IInteraction, IOwnSound
{
    ToiletPrank prank;

    void Awake() => prank = GetComponentInParent<ToiletPrank>();

    public string Prompt(Visitor host) => GameText.ValvePrompt(prank.Pressure, ToiletPrank.Turns);

    public string Blocked(Visitor host) => prank.Maxed ? GameText.ValveMaxed : null;

    public void Interact(Visitor host) => prank.Crank();
}
