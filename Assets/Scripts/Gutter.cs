using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A stinky gutter by the hot dog stand. Lift its lid (E, in anyone's body) and rats pour out, make straight for the
// hot dog man and chase him off his stand (HotDogMan.Scare), then scurry back down and the lid drops shut.
// A smellOnly one (by the locker room) has no rats: opened, it stays open and stinks, and its fan (TankFan) carries the
// stink wherever it blows: at the mascot, once it's turned (Mascot).
[RequireComponent(typeof(Interactable))]
public class Gutter : MonoBehaviour, IInteraction
{
    public int rats = 5;
    public float ratSpeed = 5f;
    public float chaseTime = 4f; // seconds the rats keep after him before going home
    public float size = 1f;
    public bool smellOnly;
    public TankFan fan; // smellOnly: carries the stink

    // Open and reeking (smellOnly).
    public bool Stinking => smellOnly && open;

    static readonly Color Stink = new(0.6f, 0.75f, 0.3f, 0.6f);

    SpriteRenderer lid, hole;
    bool open, nearGutter;
    float nextStink;

    void Start()
    {
        hole = ParkArt.Make("Hole", transform, "Props/gutter_hole", size, Color.white, ParkArt.DecalOrder + 1, true);
        lid = ParkArt.Make("Lid", transform, "Props/gutter_lid", size, Color.white, ParkArt.DecalOrder + 2, true);
        hole.transform.parent.localPosition = lid.transform.parent.localPosition = Vector3.up * 0.02f;
        hole.enabled = false;
    }

    void Update()
    {
        // Randomized pool while lid is still shut (default). Open = no more gutter thoughts here.
        bool inRange = !open && Possession.PlayerNear(transform.position, GetComponent<Interactable>().range);
        if (inRange && !nearGutter) GameHud.Think(GameText.PickGutter());
        nearGutter = inRange;
        if (Time.time < nextStink) return;
        nextStink = Time.time + (Stinking ? 0.2f : 0.8f);
        if (!Stinking || fan == null)
        {
            Fx.Burst("Fx/puff", transform.position + Vector3.up * 0.3f, 1, Stink, 0.5f, 0.35f, 1.4f);
            return;
        }
        // Open: a stream of stink along the fan's wind.
        var w = ParkArt.Make("Stink", null, "Fx/puff", 0.6f, Stink, ParkArt.OverheadOrder);
        w.transform.position = transform.position + Vector3.up * 0.5f;
        var bit = w.gameObject.AddComponent<FxBit>();
        bit.velocity = fan.Wind * 3f + Vector3.up * 0.3f;
        bit.life = 2f;
    }

    static HotDogMan Target()
    {
        foreach (var h in HotDogMan.All)
            if (h.Guarding) return h;
        return null;
    }

    public string Prompt(Visitor host) => smellOnly ? "Open the smelly gutter" : "Lift the stinky gutter lid";

    public string Blocked(Visitor host)
    {
        if (open) return smellOnly ? GameText.GutterOpen : GameText.LidOpen;
        if (!smellOnly && Target() == null) return GameText.NobodyToScare;
        return null;
    }

    public void Interact(Visitor host)
    {
        if (smellOnly)
        {
            Open();
            Sfx.Play(Sfx.HitWood, 0.6f, 0.7f);
            Fx.Burst("Fx/puff", transform.position + Vector3.up * 0.3f, 8, Stink, 2f, 0.5f, 1f);
            GameHud.Say(GameText.OpenedGutter);
            return;
        }
        GameHud.Say(GameText.LiftedLid);
        StartCoroutine(Rats(Target()));
    }

    IEnumerator Rats(HotDogMan man)
    {
        open = true;
        lid.transform.parent.localPosition = new Vector3(0.7f * size, 0.03f, -0.2f); // slid aside
        hole.enabled = true;
        Sfx.Play(Sfx.HitWood, 0.6f, 0.7f);
        Fx.Burst("Fx/puff", transform.position + Vector3.up * 0.3f, 8, Stink, 2f, 0.5f, 1f);
        ParkChaos.Add(transform.position, 4f, 8f, ParkChaos.Scary); // kids don't like rats

        var pack = new List<Transform>();
        for (int i = 0; i < rats; i++)
        {
            var r = ParkArt.Lit(ParkArt.Make("Rat", null, "Props/rat", 0.35f, Color.white));
            r.transform.position = transform.position + new Vector3(Random.Range(-0.3f, 0.3f), 0.17f, Random.Range(-0.3f, 0.3f));
            pack.Add(r.transform);
        }

        bool scared = false;
        float chaseEnds = float.MaxValue;
        while (Time.time < chaseEnds)
        {
            for (int i = 0; i < pack.Count; i++)
            {
                // Each rat heads for a spot around his feet, so they swarm rather than stack.
                float a = i * Mathf.PI * 2f / pack.Count + Time.time * 3f;
                Vector3 goal = man.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.6f;
                Run(pack[i], goal, scared ? 0.9f : 1f);
                if (!scared && Flat(pack[i].position - man.transform.position) < 1.2f)
                {
                    scared = true;
                    man.Scare(transform.position);
                    Mischief.Complete(Mischief.Task.RatsOut);
                    GameHud.Think(GameText.ThinkRats);
                    chaseEnds = Time.time + chaseTime;
                }
            }
            yield return null;
        }

        // Home again.
        while (pack.Count > 0)
        {
            for (int i = pack.Count - 1; i >= 0; i--)
            {
                Run(pack[i], transform.position, 1f);
                if (Flat(pack[i].position - transform.position) > 0.2f) continue;
                Destroy(pack[i].gameObject);
                pack.RemoveAt(i);
            }
            yield return null;
        }
        lid.transform.parent.localPosition = Vector3.up * 0.02f;
        hole.enabled = false;
        Sfx.Play(Sfx.HitWood, 0.5f, 0.8f);
        open = false;
    }

    void Run(Transform rat, Vector3 goal, float speedScale)
    {
        Vector3 d = goal - rat.position;
        d.y = 0f;
        rat.position += Vector3.ClampMagnitude(d, ratSpeed * speedScale * Time.deltaTime);
        if (Mathf.Abs(d.x) > 0.05f) rat.GetComponent<SpriteRenderer>().flipX = d.x < 0f; // art faces right
    }

    static float Flat(Vector3 d) => new Vector2(d.x, d.z).magnitude;

    void Open()
    {
        open = true;
        lid.transform.parent.localPosition = new Vector3(0.7f * size, 0.03f, -0.2f); // slid aside
        hole.enabled = true;
    }

    // Loading a save (SaveGame): a smellOnly one that did its job stays open.
    public void Restore()
    {
        if (smellOnly && Mischief.IsDone(Mischief.Task.SmellMascot)) Open();
    }
}
