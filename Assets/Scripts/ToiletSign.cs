using UnityEngine;

// The Out of Order sign in front of the toilet (ToiletPrank): in a body, E takes it down.
[RequireComponent(typeof(Interactable))]
public class ToiletSign : MonoBehaviour, IInteraction
{
    ToiletPrank prank;

    void Awake() => prank = GetComponentInParent<ToiletPrank>();

    public string Prompt(Visitor host) => GameText.SignPrompt;

    public string Blocked(Visitor host) => null;

    public void Interact(Visitor host) => prank.TakeDownSign();
}
