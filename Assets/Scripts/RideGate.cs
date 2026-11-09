using UnityEngine;

// The gate at the front of a ride's line: E here gets whoever you're possessing straight into the next cabin.
// RideQueue puts one at the head of its line.
public class RideGate : MonoBehaviour, IInteraction
{
    public RideQueue queue;

    public string Prompt(Visitor host) => "Ride the Ferris wheel";

    public string Blocked(Visitor host) => queue.BoardBlocked(host);

    public void Interact(Visitor host) => queue.Board(host);
}
