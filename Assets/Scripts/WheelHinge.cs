using UnityEngine;

// The support hinge on the Ferris wheel's leg, up on its platform (WheelPlatform). The technician can loosen it with
// his screwdriver; the wheel doesn't notice until it's started again (FerrisWheel.Restart).
[RequireComponent(typeof(Interactable))]
public class WheelHinge : MonoBehaviour, IInteraction
{
    public WheelPlatform platform;

    public bool Loose { get; private set; }

    SpriteRenderer art;

    void Start() => art = ParkArt.Solid(transform, "Props/support_hinge", 1f, new Color(0.5f, 0.52f, 0.56f));

    void Update()
    {
        if (Loose) art.transform.localRotation = Quaternion.Euler(0f, 0f, 8f + Mathf.Sin(Time.time * 9f) * 1.5f); // rattling
    }

    public string Prompt(Visitor host) => "Loosen the hinge";

    public string Blocked(Visitor host)
    {
        if (Loose) return GameText.HingeLoose;
        if (host.brain is not RideTechnician || !platform.Open) return GameText.HingeNeedsTechnician;
        if (host.HeldKind != VisitorItem.Screwdriver) return GameText.NeedScrewdriver;
        return null;
    }

    public void Interact(Visitor host)
    {
        Loosen();
        Sfx.Play(Sfx.HitWood, 0.7f, 0.6f);
        Fx.Burst("Fx/sparkle", transform.position + Vector3.up * 0.6f, 6, new Color(0.85f, 0.85f, 0.9f), 2f, 0.15f, 0.3f);
        GameHud.Say(GameText.LoosenedHinge);
        GameHud.Think(GameText.ThinkHinge);
        Mischief.Complete(Mischief.Task.LoosenHinge);
    }

    void Loosen() => Loose = true;

    // Loading a save (SaveGame).
    public void Restore()
    {
        if (Mischief.IsDone(Mischief.Task.LoosenHinge)) Loosen();
    }
}
