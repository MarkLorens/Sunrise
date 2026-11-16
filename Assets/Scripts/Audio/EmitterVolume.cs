using UnityEngine;
using FMODUnity;

// A volume slider for the Studio Event Emitters on this object (FMOD's emitter has none of its own).
// 1 = as loud as it was mixed in FMOD Studio, 0 = silent. It can be dragged while playing to hear the difference;
// changes made in Play mode aren't kept, so set the same number again afterwards and save the scene.
[RequireComponent(typeof(StudioEventEmitter))]
public class EmitterVolume : MonoBehaviour
{
    [Range(0f, 1f)] public float volume = 1f;

    StudioEventEmitter[] emitters;

    void Awake() => emitters = GetComponents<StudioEventEmitter>();

    void Update()
    {
        foreach (var e in emitters)
            if (e.EventInstance.isValid()) e.EventInstance.setVolume(volume);
    }
}
