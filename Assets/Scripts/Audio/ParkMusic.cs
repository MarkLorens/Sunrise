using UnityEngine;
using FMOD.Studio;
using FMODUnity;

// The park's theme (the level, not the main menu). One play of the song, sitting on whichever
// park megaphone you're nearest, so it pans from that horn.
// Four copies of the same track would pile up and flange against each other. A broadcast reads
// as the horn closest to you.
// Distances are from you (the listener's attenuation object: the player) to that horn.
// Inside minDistance it's as loud as `volume`; it fades out and is gone at maxDistance.
public class ParkMusic : MonoBehaviour
{
    [Range(0f, 1f)] public float volume = 0.68f;
    public float minDistance = 2f;   // full volume, standing at a megaphone
    public float maxDistance = 30f;  // gone past here. Tune on the Audio object.
    public float switchMargin = 2f;  // metres a new horn must be closer by before the song jumps to it

    EventInstance music;
    ParkMegaphone from;
    Transform ear;

    void Start()
    {
        var listener = FindAnyObjectByType<StudioListener>();
        if (listener != null)
            ear = listener.AttenuationObject != null ? listener.AttenuationObject.transform : listener.transform;

        var events = FMODEvents.instance;
        if (events == null || events.parkMusic.IsNull || AudioManager.instance == null) return;

        // FMOD parks a new 3D event at an inaudible nowhere-point until set3DAttributes. Don't start
        // it until a horn has actually taken it, or it stays silent.
        if (Nearest() == null) return;
        music = AudioManager.instance.CreateInstance(events.parkMusic);
        Apply();
        music.start();
    }

    void Update()
    {
        if (!music.isValid())
        {
            Start();
            return;
        }
        Apply();
    }

    void Apply()
    {
        Place();
        music.setVolume(volume);
        music.setProperty(EVENT_PROPERTY.MINIMUM_DISTANCE, minDistance);
        music.setProperty(EVENT_PROPERTY.MAXIMUM_DISTANCE, Mathf.Max(minDistance + 0.01f, maxDistance));
    }

    // On the nearest horn. It stays put until another is clearly closer, so it doesn't flicker
    // on the line between two of them. No slide across the park: that would pass the song
    // right over you and spike the volume.
    void Place()
    {
        var horn = Nearest();
        if (horn == null) return;
        from = horn;
        music.set3DAttributes(horn.transform.To3DAttributes());
    }

    ParkMegaphone Nearest()
    {
        Vector3 at = ear != null ? ear.position : transform.position;
        ParkMegaphone best = null;
        float bestD = float.MaxValue;
        float currentD = float.MaxValue;
        foreach (var horn in ParkMegaphone.All)
        {
            float d = Vector3.Distance(at, horn.transform.position);
            if (d < bestD)
            {
                bestD = d;
                best = horn;
            }
            if (horn == from) currentD = d;
        }
        if (from != null && best != from && bestD + switchMargin >= currentD) return from;
        return best;
    }

    void OnDestroy()
    {
        if (!music.isValid()) return;
        if (AudioManager.instance != null) AudioManager.instance.ReleaseInstance(music);
        else
        {
            music.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            music.release();
        }
        music.clearHandle();
    }
}
