using UnityEngine;

// The big fan by the propane tank, blowing away from it, so any gas it leaks drifts off to nothing. Turn it round
// (E, in anyone's body) to blow at the hot dog stand instead. (The fan the hot dog man guards is StallFan.)
[RequireComponent(typeof(Interactable))]
public class TankFan : MonoBehaviour, IInteraction
{
    public PropaneTank tank;
    public Transform stall;

    public bool Aimed { get; private set; }
    // Which way it blows, along the ground.
    public Vector3 Wind
    {
        get
        {
            Vector3 d = Aimed ? stall.position - transform.position : transform.position - tank.transform.position;
            d.y = 0f;
            return d.normalized;
        }
    }

    SpriteRenderer art;
    float nextGust;

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

    public string Prompt(Visitor host) => "Turn the fan toward the hot dog stand";

    public string Blocked(Visitor host) => Aimed ? GameText.FanAimed : null;

    public void Interact(Visitor host)
    {
        Aim();
        Sfx.Play(Sfx.HitWood, 0.6f, 1.2f);
        GameHud.Say(GameText.TurnedFan);
        GameHud.Think(GameText.ThinkFanAimed);
        Mischief.Complete(Mischief.Task.AimFan);
    }

    void Aim()
    {
        Aimed = true;
        Face();
    }

    // Loading a save (SaveGame).
    public void Restore()
    {
        if (Mischief.IsDone(Mischief.Task.AimFan)) Aim();
    }
}
