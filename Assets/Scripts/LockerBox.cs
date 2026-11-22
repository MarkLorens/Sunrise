using UnityEngine;

// The locker room's electrical box, its broken cable inside. It's the mechanic's: his keys open it (anyone holding
// them). Spark the cable while the locker room's full of propane (PropaneTank) and up it goes.
[RequireComponent(typeof(Interactable))]
public class LockerBox : MonoBehaviour, IInteraction
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
        Sfx.Play(Sfx.Crash, 0.5f, 1.8f);
        Fx.Burst("Fx/sparkle", transform.position + Vector3.up * 0.8f, 14, new Color(1f, 0.95f, 0.4f), 3f, 0.3f, 0.4f);
        var tank = PropaneTank.ReadyNear(room.transform.position, 1f);
        if (tank == null)
        {
            GameHud.Refuse(transform, GameText.CableSparks);
            return;
        }
        GameHud.Say(GameText.SparkedCable);
        Mischief.Complete(Mischief.Task.SparkLocker);
        tank.Explode();
    }
}
