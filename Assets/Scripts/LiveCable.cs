using UnityEngine;

// The live plug sticking out of the speaker's sprite, sparking, a lightning bolt crackling over its prongs
// (ElectricBolt). In anyone's body, pull it out and dip it in the drinking fountain's puddle if it reaches (a cord is
// drawn while it's in): the bolt jumps to the wet end and crackles harder.
[RequireComponent(typeof(Interactable))]
public class LiveCable : MonoBehaviour, IInteraction
{
    public Speaker speaker;
    public DrinkingFountain fountain;
    public float reach = 1.5f;    // how far past its end the cable stretches
    public float recharge = 5f;   // before it can zap again

    static readonly Color Spark = new(1f, 0.95f, 0.4f);
    // Over the prongs (which stick out up and to the right of this point), leaning a touch.
    static readonly Vector3 BoltAt = new(0.2f, 0.48f, -0.05f);
    const float BoltTilt = -8f, BoltHeight = 0.42f;
    ElectricBolt bolt;

    LineRenderer line;
    Vector3 dippedAt;
    float dipUntil, readyAt, nextSpark;
    bool thoughtDry, thoughtWet;

    bool Reaches => fountain.Radius > 0f && fountain.Gap(transform.position) <= reach;

    // Coming up to it (in any form): a hint while the puddle's too far, glee once it reaches. Each said once.
    void Think()
    {
        if (thoughtWet || !Possession.PlayerNear(transform.position, GetComponent<Interactable>().range)) return;
        if (Reaches)
        {
            thoughtWet = true;
            GameHud.Think(GameText.ThinkCableWet);
        }
        else if (!thoughtDry)
        {
            thoughtDry = true;
            GameHud.Think(GameText.ThinkCableDry);
        }
    }

    void Start()
    {
        line = gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = ParkArt.Material;
        line.sortingOrder = ParkArt.DecalOrder + 1;
        line.widthMultiplier = 0.07f;
        line.startColor = line.endColor = new Color(0.08f, 0.08f, 0.1f);
        line.positionCount = 2;
        bolt = ElectricBolt.Make("Plug Bolt", null, transform.position + BoltAt, BoltHeight, BoltTilt);
    }

    void Update()
    {
        Think();
        bool dipped = Time.time < dipUntil;
        Vector3 end = dipped ? dippedAt : transform.position;
        line.enabled = dipped; // the plug's own cord is part of the speaker's art
        line.SetPosition(0, transform.position);
        line.SetPosition(1, end + Vector3.up * 0.05f);
        if (bolt != null)
        {
            bolt.Anchor = (dipped ? end + new Vector3(-0.2f, 0f, 0f) : transform.position) + BoltAt;
            bolt.Intensity = dipped ? 2.2f : 1f;
        }
        if (Time.time < nextSpark) return;
        nextSpark = Time.time + Random.Range(0.6f, 1.4f);
        Fx.Burst("Fx/sparkle", end + Vector3.up * 0.15f, dipped ? 6 : 3, Spark, 1.5f, 0.2f, 0.3f);
    }

    public string Prompt(Visitor host) => "Dip the live cable in the puddle";

    public string Blocked(Visitor host)
    {
        if (Time.time < readyAt) return GameText.CableCrackling;
        if (!Reaches) return GameText.CableTooFar;
        return null;
    }

    public void Interact(Visitor host)
    {
        host.Look.Gesture(host.Look.interact, 1f);
        Vector3 toPuddle = fountain.PuddleCenter - transform.position;
        toPuddle.y = 0f;
        float into = Mathf.Max(0f, toPuddle.magnitude - fountain.Radius) + 0.3f;
        dippedAt = transform.position + toPuddle.normalized * Mathf.Min(into, toPuddle.magnitude);
        dippedAt.y = 0f; // the plug's up on the speaker; the dipped end lies in the water
        dipUntil = Time.time + 1.5f;
        readyAt = Time.time + recharge;
        GameHud.Say(GameText.DippedCable);
        fountain.Electrify(dippedAt);
    }
}
