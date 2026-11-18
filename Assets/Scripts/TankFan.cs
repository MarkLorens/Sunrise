using UnityEngine;
using UnityEngine.Serialization;

// A big standing fan anyone can turn round (E, in anyone's body). It starts out blowing away from `awayFrom`; turned,
// it blows at `aimAt`. By the hot dog stand's propane tank it carries the gas to the stand (PropaneTank); by the
// locker room it carries the gas to the locker room, but the mascot won't let anyone touch that one (Mascot); and by
// the smelly gutter it carries the stink to the mascot (Gutter). (The fan the hot dog man guards is StallFan.)
[RequireComponent(typeof(Interactable))]
public class TankFan : MonoBehaviour, IInteraction
{
    public Transform awayFrom;
    [FormerlySerializedAs("stall")] public Transform aimAt;
    public string aimName = "hot dog stand";             // "Turn the fan toward the ..."
    public bool countsAsStep = true;
    public Mischief.Task task = Mischief.Task.AimFan;     // ticked off when it's turned (if countsAsStep)
    public bool mascotGuards;                              // the mascot keeps people off it while he's around

    public bool Aimed { get; private set; }
    // Which way it blows, along the ground.
    public Vector3 Wind
    {
        get
        {
            Vector3 d = Aimed ? aimAt.position - transform.position : transform.position - awayFrom.position;
            d.y = 0f;
            return d.normalized;
        }
    }

    SpriteRenderer art;
    float nextGust;
    bool thoughtGuarded;

    void Start()
    {
        art = ParkArt.Solid(transform, "Props/fan", 1.5f, new Color(0.6f, 0.65f, 0.7f));
        Face();
    }

    void Face() => art.flipX = Wind.x < 0f; // the art blows right

    void Update()
    {
        if (Time.time < nextGust) return;
        nextGust = Time.time + 0.2f;
        Gust(transform.position, Wind);
    }

    // A streak of air off a fan (StallFan too).
    public static void Gust(Vector3 fan, Vector3 wind)
    {
        var g = ParkArt.Make("Gust", null, "Fx/sparkle", 0.2f, new Color(1f, 1f, 1f, 0.5f), ParkArt.OverheadOrder);
        g.transform.position = fan + Vector3.up * Random.Range(0.8f, 1.4f) + wind * 0.5f;
        var bit = g.gameObject.AddComponent<FxBit>();
        bit.velocity = wind * 6f;
        bit.life = 0.6f;
    }

    public string Prompt(Visitor host) => $"Turn the fan toward the {aimName}";

    public string Blocked(Visitor host) => Aimed ? GameText.FanAimed : null;

    public void Interact(Visitor host)
    {
        var guard = mascotGuards ? Mascot.GuardNear(transform.position) : null;
        if (guard != null)
        {
            GameHud.Shout(guard.transform, GameText.MascotGuard, 2f);
            GameHud.Refuse(transform, GameText.MascotGuards);
            if (!thoughtGuarded) GameHud.Think(GameText.ThinkMascot);
            thoughtGuarded = true;
            return;
        }
        Aim();
        Sfx.Play(Sfx.HitWood, 0.6f, 1.2f);
        GameHud.Say(GameText.TurnedFan(aimName));
        GameHud.Think(GameText.ThinkFanAimed);
        if (countsAsStep) Mischief.Complete(task);
    }

    void Aim()
    {
        Aimed = true;
        Face();
    }

    // Loading a save (SaveGame). A fan that isn't a step of its own is turned once the step after it is done.
    public void Restore()
    {
        if (countsAsStep ? Mischief.IsDone(task) : Mischief.IsDone(Mischief.Task.SmellMascot)) Aim();
    }
}
