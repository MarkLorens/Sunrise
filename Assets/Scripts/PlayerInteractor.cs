using UnityEngine;
using UnityEngine.InputSystem;

// Pressing E as a spirit possesses whoever you're touching. Inside someone, E interacts with the nearest
// Interactable whose range reaches the player, on behalf of whoever the spirit is possessing.
public class PlayerInteractor : MonoBehaviour
{
    Possession possession;

    public Visitor Host => possession ? possession.Host : null;

    void Awake() => possession = GetComponent<Possession>();

    void Update()
    {
        var kb = Keyboard.current;
        if (Possession.InputBlocked || kb == null || !kb.eKey.wasPressedThisFrame) return;
        if (possession.State == Possession.Mode.Diving) return;
        if (Host == null && possession.TryPossess()) return;

        var target = Nearest();
        if (target == null) return;

        string blocked = target.Blocked(Host);
        if (blocked != null)
        {
            GameHud.Say(blocked);
            return;
        }
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
