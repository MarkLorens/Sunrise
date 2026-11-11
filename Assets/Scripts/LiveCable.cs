using UnityEngine;

// The frayed live end of the speaker's broken power cable, sparking on the ground. Only the cable man knows how to
// handle it: in his body, pick it up and dip it in the drinking fountain's puddle if it reaches.
[RequireComponent(typeof(Interactable))]
public class LiveCable : MonoBehaviour, IInteraction
{
    public Speaker speaker;
    public DrinkingFountain fountain;
    public float reach = 1.5f;    // how far past its end the cable stretches
    public float recharge = 5f;   // before it can zap again

    static readonly Color Spark = new(1f, 0.95f, 0.4f);

    LineRenderer line;
    Vector3 dippedAt;
    float dipUntil, readyAt, nextSpark;

    void Start()
    {
        line = gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = ParkArt.Material;
        line.sortingOrder = ParkArt.DecalOrder + 1;
        line.widthMultiplier = 0.07f;
        line.startColor = line.endColor = new Color(0.08f, 0.08f, 0.1f);
        line.positionCount = 2;
    }

    void Update()
    {
        bool dipped = Time.time < dipUntil;
        Vector3 end = dipped ? dippedAt : transform.position;
        line.SetPosition(0, speaker.transform.position + Vector3.up * 0.05f);
        line.SetPosition(1, end + Vector3.up * 0.05f);
        if (Time.time < nextSpark) return;
        nextSpark = Time.time + Random.Range(0.6f, 1.4f);
        Fx.Burst("Fx/sparkle", ParkArt.Above(end, 0.15f), dipped ? 6 : 3, Spark, 1.5f, 0.2f, 0.3f);
    }

    static bool IsCableMan(Visitor host) => host != null && host.brain is CableMan;

    public string Prompt(Visitor host) => IsCableMan(host) ? "Dip the live cable in the puddle" : "Broken speaker cable";

    public string Blocked(Visitor host)
    {
        if (!IsCableMan(host)) return "It's live! Only the cable man knows how to handle it";
        if (Time.time < readyAt) return "Still crackling from the last zap";
        if (fountain.Radius <= 0f || fountain.Gap(transform.position) > reach) return "The puddle's too far away for the cable to reach";
        return null;
    }

    public void Interact(Visitor host)
    {
        host.Look.Gesture(host.Look.interact, 1f);
        Vector3 toPuddle = fountain.PuddleCenter - transform.position;
        toPuddle.y = 0f;
        float into = Mathf.Max(0f, toPuddle.magnitude - fountain.Radius) + 0.3f;
        dippedAt = transform.position + toPuddle.normalized * Mathf.Min(into, toPuddle.magnitude);
        dipUntil = Time.time + 1.5f;
        readyAt = Time.time + recharge;
        fountain.Electrify(dippedAt);
    }
}
