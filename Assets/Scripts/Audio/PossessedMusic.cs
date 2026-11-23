using UnityEngine;
using FMODUnity;

// The park's theme (BGM (Ruckus), from the theme song speakers) slows down and drops in pitch while you're in someone's
// body, and comes back up when you leave it: FMOD's global Possessed parameter (0 = floating as Ruckus, 1 = possessing),
// which the event's clip pitch and Pitch Shifter follow. It glides there rather than jumping, so the song keeps going
// and sounds like a record slowing down and speeding back up. Global, so every speaker follows the one value.
// GameHud puts it on the player.
public class PossessedMusic : MonoBehaviour
{
    public const string Parameter = "Possessed";

    [Tooltip("Seconds to glide all the way down when you possess someone.")]
    public float intoBody = 0.6f;
    [Tooltip("Seconds to glide all the way back up when you leave the body.")]
    public float outOfBody = 1f;

    Possession spirit;
    float value = -1f; // what FMOD has; -1: not set yet

    void Awake() => spirit = GetComponent<Possession>();

    void Update()
    {
        float target = spirit != null && spirit.State == Possession.Mode.Possessing ? 1f : 0f;
        float now = value < 0f
            ? target // the park starting (say, a loaded game already in someone): straight there
            : Mathf.MoveTowards(value, target, Time.deltaTime / Mathf.Max(0.01f, target > value ? intoBody : outOfBody));
        if (Mathf.Approximately(now, value)) return;
        value = now;
        Set(value); // game time: paused, it holds where it is
    }

    // Global parameters outlast the scene: leaving the park puts the theme back to normal.
    void OnDestroy() => Set(0f);

    static void Set(float v)
    {
        if (RuntimeManager.IsInitialized) RuntimeManager.StudioSystem.setParameterByName(Parameter, v);
    }
}
