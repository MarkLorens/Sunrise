using UnityEngine;
using FMODUnity;

// Every FMOD event the scripts play, in one place. Lives on the Audio object next to AudioManager: drag the events in
// from the Inspector. An empty slot just means that sound doesn't play yet.
public class FMODEvents : MonoBehaviour
{
    [field: Header("Player")]
    [field: Tooltip("The spirit drifting around. Plays only while the spirit is moving.")]
    [field: SerializeField] public EventReference spiritHover { get; private set; }
    [field: Tooltip("Footsteps of the body the player is possessing.")]
    [field: SerializeField] public EventReference hostFootsteps { get; private set; }

    [field: Header("Visitors")]
    [field: Tooltip("Visitors and staff walking on their own (3D, optional Pace parameter: 0 walk, 1 run).")]
    [field: SerializeField] public EventReference npcFootsteps { get; private set; }

    public static FMODEvents instance { get; private set; }

    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("Found more than one FMODEvents in the scene.");
        }
        instance = this;
    }
}
