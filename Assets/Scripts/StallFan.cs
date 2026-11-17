using System.Collections.Generic;
using UnityEngine;

// The hot dog man's fan by his stand, cooling the line. While it's on it blows any propane straight back out of the
// stand (PropaneTank), so it has to be switched off (E, in anyone's body): not while he's there to see it, so the
// rats have to chase him off first (Gutter).
[RequireComponent(typeof(Interactable))]
public class StallFan : MonoBehaviour, IInteraction
{
    public static readonly List<StallFan> All = new();

    public Vector3 blow = Vector3.back; // which way it blows, along the ground: at the line

    public bool On { get; private set; } = true;

    SpriteRenderer art;
    float nextGust;
    bool thoughtGuarded;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
        art = ParkArt.Solid(transform, "Props/fan", 1.5f, new Color(0.6f, 0.65f, 0.7f));
        art.flipX = blow.x < 0f; // the art blows right
    }

    void Update()
    {
        if (!On || Time.time < nextGust) return;
        nextGust = Time.time + 0.2f;
        Vector3 w = blow;
        w.y = 0f;
        TankFan.Gust(transform.position, w.normalized);
    }

    static HotDogMan Guard()
    {
        foreach (var h in HotDogMan.All)
            if (h.Guarding) return h;
        return null;
    }

    public string Prompt(Visitor host) => "Switch off the hot dog man's fan";

    public string Blocked(Visitor host) => On ? null : GameText.FanOff;

    public void Interact(Visitor host)
    {
        var guard = Guard();
        if (guard != null)
        {
            GameHud.Shout(guard.transform, GameText.HotDogGuard, 2f);
            GameHud.Refuse(transform, GameText.FanGuarded);
            if (!thoughtGuarded) GameHud.Think(GameText.ThinkFanGuarded);
            thoughtGuarded = true;
            return;
        }
        On = false;
        Sfx.Play(Sfx.HitWood, 0.5f, 1.4f);
        GameHud.Say(GameText.SwitchedOffFan);
        GameHud.Think(GameText.ThinkStallFanOff);
        Mischief.Complete(Mischief.Task.StallFanOff);
    }

    // Loading a save (SaveGame).
    public void Restore()
    {
        if (Mischief.IsDone(Mischief.Task.StallFanOff)) On = false;
    }
}
