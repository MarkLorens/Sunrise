using System.Collections.Generic;
using UnityEngine;

// A big standing fan. E (in anyone's body) turns its head a quarter turn clockwise: up, right, down, left (up is away
// from the camera). Whatever drifts into it goes on the way it faces: propane passes from fan to fan along a chain
// (PropaneTank) until one blows it at its target, and the smelly gutter's stink goes where its fan points (Gutter,
// Mascot). Some are guarded: the hot dog man won't let anyone touch his while he's at his stall, nor the mascot the
// locker room's.
// ponytail: one side-on sprite, flipped for left; up and down only show in the arrow at its feet until there's art.
[RequireComponent(typeof(Interactable))]
public class TankFan : MonoBehaviour, IInteraction
{
    public static readonly List<TankFan> All = new();

    public enum Dir { Up, Right, Down, Left } // clockwise, the order E turns it through
    public enum Guard { None, HotDogMan, Mascot }

    public Dir facing = Dir.Right;
    public Guard guardedBy;

    // Which way it blows, along the ground.
    public Vector3 Wind => facing switch
    {
        Dir.Up => Vector3.forward,
        Dir.Right => Vector3.right,
        Dir.Down => Vector3.back,
        _ => Vector3.left,
    };

    SpriteRenderer art;
    Transform arrow;
    float nextGust;
    bool thoughtGuarded;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
        art = ParkArt.Solid(transform, "Props/fan", 1.5f, new Color(0.6f, 0.65f, 0.7f));
        var a = ParkArt.Make("Facing", transform, "Markers/arrow", 0.9f, new Color(1f, 1f, 1f, 0.7f), ParkArt.DecalOrder + 2, true);
        arrow = a.transform.parent;
        arrow.localPosition = Vector3.up * 0.03f;
        Face();
    }

    // The art blows right; the arrow at its feet points the way it blows.
    void Face()
    {
        art.flipX = facing == Dir.Left;
        arrow.rotation = Quaternion.LookRotation(Wind); // the arrow sprite points up, which lies flat as away from the camera
        arrow.localPosition = Wind * 0.6f + Vector3.up * 0.03f;
    }

    // Is `p` in this fan's stream: ahead of it, within `reach`, and no more than `lane` off to the side?
    public bool Blows(Vector3 p, float lane, float reach)
    {
        Vector3 d = p - transform.position;
        d.y = 0f;
        float ahead = Vector3.Dot(d, Wind);
        return ahead > 0.3f && ahead <= reach && (d - Wind * ahead).magnitude <= lane;
    }

    // How far ahead `p` is along the wind.
    public float Ahead(Vector3 p) => Vector3.Dot(p - transform.position, Wind);

    void Update()
    {
        if (Time.time < nextGust) return;
        nextGust = Time.time + 0.2f;
        Gust(transform.position, Wind);
    }

    // A streak of air off a fan.
    public static void Gust(Vector3 fan, Vector3 wind)
    {
        var g = ParkArt.Make("Gust", null, "Fx/sparkle", 0.2f, new Color(1f, 1f, 1f, 0.5f), ParkArt.OverheadOrder);
        g.transform.position = fan + Vector3.up * Random.Range(0.8f, 1.4f) + wind * 0.5f;
        var bit = g.gameObject.AddComponent<FxBit>();
        bit.velocity = wind * 6f;
        bit.life = 0.6f;
    }

    public string Prompt(Visitor host) => "Turn the fan";

    public string Blocked(Visitor host) => null;

    public void Interact(Visitor host)
    {
        var guard = Guarding();
        if (guard != null)
        {
            bool mascot = guardedBy == Guard.Mascot;
            GameHud.Shout(guard.transform, mascot ? GameText.MascotGuard : GameText.HotDogGuard, 2f);
            GameHud.Refuse(transform, mascot ? GameText.MascotGuards : GameText.FanGuarded);
            if (!thoughtGuarded) GameHud.Think(mascot ? GameText.ThinkMascot : GameText.PickFanGuarded());
            thoughtGuarded = true;
            return;
        }
        facing = (Dir)(((int)facing + 1) % 4);
        Face();
        Sfx.Play(Sfx.HitWood, 0.6f, 1.2f, Captions.FanTurned);
        GameHud.Say(GameText.TurnedFan(facing.ToString().ToLowerInvariant()));
    }

    Component Guarding()
    {
        if (guardedBy == Guard.Mascot) return Mascot.GuardNear(transform.position);
        if (guardedBy != Guard.HotDogMan) return null;
        foreach (var h in HotDogMan.All)
            if (h.Guarding) return h;
        return null;
    }

    // Loading a save (SaveGame).
    public void Restore(Dir dir)
    {
        facing = dir;
        if (art != null) Face();
    }
}
