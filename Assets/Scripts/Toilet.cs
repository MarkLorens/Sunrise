using UnityEngine;

// Portable toilet. Whoever holds the toilet key can lock someone inside to keep them out of the way longer.
[RequireComponent(typeof(Interactable))]
public class Toilet : MonoBehaviour, IInteraction
{
    public float lockTime = 30f;
    public Vector3 doorOffset = new(0f, 0f, -0.6f);
    public Sprite lockedIcon;

    public RideOperator Occupant { get; private set; }
    public bool Locked => Time.time < lockedUntil;
    public Vector3 Door => transform.position + doorOffset;

    float lockedUntil;
    SpriteRenderer lockIcon;

    void Start()
    {
        var go = new GameObject("Lock Icon");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 2.7f, -0.01f);
        go.transform.localScale = Vector3.one * 0.5f;
        lockIcon = go.AddComponent<SpriteRenderer>();
        lockIcon.sprite = lockedIcon;
        lockIcon.sharedMaterial = GetComponent<SpriteRenderer>().sharedMaterial;
        lockIcon.enabled = false;
    }

    void Update() => lockIcon.enabled = Locked;

    public void Enter(RideOperator who) => Occupant = who;
    public void Exit() => Occupant = null;

    public string Prompt(Visitor host) =>
        host != null && host.item == VisitorItem.ToiletKey ? "Lock the toilet door" : Occupant != null ? "Toilet (occupied)" : "Toilet";

    public string Blocked(Visitor host)
    {
        if (host.item != VisitorItem.ToiletKey) return Occupant != null ? "It locks from the outside... with a key" : "Nothing to do here";
        if (Occupant == null) return "Nobody's inside to lock in";
        if (Locked) return "Already locked";
        return null;
    }

    public void Interact(Visitor host)
    {
        host.TakeItem(); // key stays in the lock
        lockedUntil = Time.time + lockTime;
        GameHud.Say("*click*  Operator (inside): ...Hello? Is someone out there?");
    }
}
