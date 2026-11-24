using UnityEngine;

// K uses the nearest Interactable whose range reaches the player (a ride part, the stall...), on behalf of whoever
// the spirit is possessing. As a spirit there's nothing it can touch: K just says so. (Possessing is J: Possession.)
public class PlayerInteractor : MonoBehaviour
{
    Possession possession;

    public Visitor Host => possession ? possession.Host : null;

    void Awake() => possession = GetComponent<Possession>();

    void Update()
    {
        if (Possession.InputBlocked || !Controls.Use) return; // K
        if (possession.State == Possession.Mode.Diving) return;

        var target = Nearest();
        if (target == null) return;

        target.Tried(Host);
        string blocked = target.Blocked(Host);
        if (blocked != null)
        {
            GameHud.Refuse(target.transform, blocked); // over whatever said no
            return;
        }
        // G-03: K went through (a refusal gets G-06 instead), unless it makes a sound of its own (IOwnSound).
        if (!target.OwnSound) AudioManager.TryPlay(e => e.menuClick);
        if (Host != null) Host.Look.Gesture(Host.Look.interact);
        target.Interact(Host);
    }

    // ponytail: linear scan over every interactable; fine for hundreds, swap to a spatial lookup if it ever shows up in the profiler.
    public Interactable Nearest()
    {
        Interactable best = null;
        float bestDist = float.MaxValue;
        foreach (var i in Interactable.All)
        {
            if (Host != null && i.gameObject == Host.gameObject) continue; // yourself
            Vector3 d = i.transform.position - transform.position;
            d.y = 0; // distance along the floor only, so sprite height/pivot doesn't count
            float dist = d.magnitude;
            if (dist <= i.range && dist < bestDist)
            {
                best = i;
                bestDist = dist;
            }
        }
        return best;
    }
}
