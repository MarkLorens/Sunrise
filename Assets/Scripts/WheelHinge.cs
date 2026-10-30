using UnityEngine;

// Where the Ferris wheel's support bolts to the frame. With a screwdriver (the mechanic's) the screws can be backed
// out: the wheel keeps turning for now, but run it at full speed and the support gives way.
[RequireComponent(typeof(Interactable))]
public class WheelHinge : MonoBehaviour, IInteraction
{
    public FerrisWheel wheel;

    SpriteRenderer sprite;
    Color tight;

    void Start()
    {
        sprite = GetComponentInChildren<SpriteRenderer>();
        tight = sprite.color;
    }

    void Update() => sprite.color = wheel.Loosened ? new Color(0.85f, 0.45f, 0.3f) : tight;

    public string Prompt(Visitor host) =>
        host != null && host.HeldKind == VisitorItem.Screwdriver ? "Loosen the support screws" : "Ferris wheel support";

    public string Blocked(Visitor host)
    {
        if (wheel.IsBroken) return "Nothing left to loosen";
        if (wheel.Loosened) return "The screws are already loose";
        if (host.HeldKind != VisitorItem.Screwdriver) return "Bolted tight. You'd need a screwdriver";
        return null;
    }

    public void Interact(Visitor host)
    {
        wheel.Loosen();
        Sfx.Play(Sfx.Grab, 0.8f, 0.7f);
        GameHud.Say("*squeak squeak* The support wobbles. It won't take much load now...");
        Mischief.Complete(Mischief.Task.LoosenHinge);
    }
}
