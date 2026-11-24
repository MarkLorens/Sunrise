using UnityEngine;
using FMODUnity;

// Where the game hears from. How loud each 3D sound is goes by how far it is from here (it's the FMOD listener's
// attenuation object); left and right still come from the camera, where the listener itself is. It sits on whoever
// you control, except while something's spotlit (CameraRig.Spotlight): then it glides over with the shot, at the same
// pace, so a payoff is heard from where it's happening rather than from wherever you were standing, and glides back
// with it. Captions for sounds that go on (Near) and the park's crowd (ParkCrowd) go by it too. GameHud makes it.
public class Ears : MonoBehaviour
{
    static Ears instance;
    StudioListener listener;

    // Where it is now (on the player when it isn't made yet).
    public static Vector3 Position =>
        instance != null ? instance.transform.position
        : Possession.Instance != null ? Possession.Instance.transform.position : Vector3.zero;

    public static GameObject Object => instance != null ? instance.gameObject : null;

    // Is `p` within `range` of where the game hears from, along the floor? For a sound's caption: shown while it's heard.
    public static bool Near(Vector3 p, float range)
    {
        Vector3 d = Position - p;
        d.y = 0f;
        return d.magnitude <= range;
    }

    void Awake()
    {
        instance = this;
        Place();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // After the camera has worked out this frame's shot (CameraRig runs first).
    void LateUpdate() => Place();

    void Place()
    {
        var spirit = Possession.Instance;
        if (spirit == null) return;
        // The spirit's own object: it follows the body it's in, at the height the game has always heard from.
        transform.position = CameraRig.Hearing(spirit.transform.position);

        if (listener == null) listener = FindAnyObjectByType<StudioListener>();
        if (listener != null && listener.AttenuationObject != gameObject) listener.AttenuationObject = gameObject;
    }
}
