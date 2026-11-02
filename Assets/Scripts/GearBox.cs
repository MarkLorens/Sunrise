using System.Collections.Generic;
using UnityEngine;

// The cover over the Ferris wheel's drive gears, at the foot of the wheel. On its own it just keeps the wheel turning.
// Get something long and sturdy (a water bottle) into it while the wheel is at full speed and the gears jam: the
// wheel stops dead and the cabins fly off. Throw it in (hold J) or shove it in up close (E). It sits in the ride's
// closed area, so with staff watching nobody gets close enough: they have to be busy elsewhere first (a fight draws
// them off).
[RequireComponent(typeof(Interactable))]
public class GearBox : MonoBehaviour, IInteraction
{
    public static readonly List<GearBox> All = new();

    public FerrisWheel wheel;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    // Did a thrown thing at `p` land in the gears?
    public bool Catches(Vector3 p)
    {
        Vector3 d = p - transform.position;
        return !wheel.IsBroken && new Vector2(d.x, d.z).magnitude < wheel.gearRadius && p.y < 1.5f;
    }

    // Why `item` from `by` won't jam the gears right now; null = it will.
    public string Refusal(Item item, Visitor by)
    {
        if (wheel.IsBroken) return "The gears have already stopped for good";
        var watcher = by != null && by.brain != null ? null : wheel.Area.Watcher(); // staff don't stop other staff
        if (watcher != null) return $"{watcher.name} is keeping an eye on the gears";
        if (item == null) return "The gears grind away under the cover. Something long and sturdy could jam them";
        if (!item.Sturdy) return $"The {item.DisplayName} would just get chewed up. It needs something long and sturdy";
        if (!wheel.IsOverdrive) return "At this speed it'd just get spat back out. The wheel needs to be spinning much faster";
        return null;
    }

    public void Jam(Item item)
    {
        Mischief.Complete(Mischief.Task.JamGears);
        wheel.Jam(item);
    }

    public string Prompt(Visitor host) =>
        host != null && host.Held != null && host.Held.Sturdy ? $"Jam the {host.Held.DisplayName} into the gears" : "Gear box";

    public string Blocked(Visitor host) => Refusal(host.Held, host);

    public void Interact(Visitor host)
    {
        var item = host.Held;
        host.LoseHeld();
        Jam(item);
    }
}
