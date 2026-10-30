using UnityEngine;

// The Ferris wheel's electrical box. Does nothing on its own, but bang on it a few times and the racket brings the
// mechanic over to check on it.
[RequireComponent(typeof(Interactable))]
public class ElectricalBox : MonoBehaviour, IInteraction
{
    public FerrisWheel wheel;
    public int hitsToLure = 3;

    int hits;

    public string Prompt(Visitor host) => "Bang on the electrical box";

    public string Blocked(Visitor host)
    {
        if (wheel.IsBroken) return "It's dead";
        if (Mechanic.Busy) return "The mechanic's already on his way";
        return null;
    }

    public void Interact(Visitor host)
    {
        hits++;
        Sfx.Play(Sfx.HitWood, 0.9f, Random.Range(0.8f, 1.1f));
        Fx.Burst("Fx/sparkle", transform.position + Vector3.up * 0.8f, 8, new Color(1f, 0.9f, 0.4f), 3f, 0.3f, 0.4f);
        Fx.Shake(0.08f);
        ParkChaos.Add(transform.position, 3f, 0.5f, ParkChaos.Scary);
        if (hits < hitsToLure)
        {
            GameHud.Say("*CLANG!*");
            return;
        }
        hits = 0;
        if (Mechanic.Lure(transform.position)) Mischief.Complete(Mischief.Task.LureMechanic);
        else GameHud.Say("*CLANG!* ...nobody comes.");
    }
}
