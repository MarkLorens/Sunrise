using UnityEngine;
using UnityEngine.InputSystem;

// Pressing E interacts with the nearest Interactable whose range reaches the player.
public class PlayerInteractor : MonoBehaviour
{
    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.eKey.wasPressedThisFrame) return;

        var target = Nearest();
        if (target != null) target.Interact();
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
