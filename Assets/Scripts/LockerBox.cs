using UnityEngine;

// The locker room's electrical box, its broken cable inside. It's the mechanic's: his keys open it (anyone holding
// them). Spark the cable while the locker room's full of propane (PropaneTank) and up it goes.
// It plays its own sound (IOwnSound): the menu click when the spark sets it off, only the "can't" sound when there's
// no propane to catch.
[RequireComponent(typeof(Interactable))]
public class LockerBox : MonoBehaviour, IInteraction, IOwnSound
{
    public LockerRoom room;

    void Start()
    {
        ParkArt.Solid(transform, "Props/electrical_box", 1.3f, new Color(0.4f, 0.55f, 0.75f));
        gameObject.AddComponent<ZapFx>(); // live electrics, always buzzing
    }

    public string Prompt(Visitor host) => "Spark the broken cable";

    public string Blocked(Visitor host)
    {
        if (room.Burning) return GameText.AlreadyBurning;
        return host.HeldKind == VisitorItem.Keys ? null : GameText.NeedKeys;
    }

    public void Interact(Visitor host)
    {
        ClosedCaptions.Show(Captions.CableSpark);
        Fx.Burst("Fx/sparkle", transform.position + Vector3.up * 0.8f, 14, new Color(1f, 0.95f, 0.4f), 3f, 0.3f, 0.4f);
        var tank = PropaneTank.ReadyNear(room.transform.position, 1f);
        if (tank == null)
        {
            GameHud.Refuse(transform, GameText.CableSparks); // G-06, no click
            return;
        }
        AudioManager.TryPlay(e => e.menuClick); // G-03: it went through
        GameHud.Say(GameText.SparkedCable);
        Mischief.Complete(Mischief.Task.SparkLocker);
        tank.Explode();
    }
}
