using UnityEngine;
using FMODUnity;
using FMOD.Studio;

// A looping sound that follows one object and gets switched on and off, like footsteps while someone walks.
// Call Play every frame it should be heard and Stop when it shouldn't; it makes the FMOD instance once and reuses it.
// If the event isn't set to loop in FMOD, Play starts it again when it runs out.
public class LoopingSound
{
    readonly GameObject owner;
    readonly Rigidbody body;
    EventInstance instance;
    FMOD.GUID loaded; // which event `instance` is
    Vector2? range; // (full volume up to, silent past) in metres from the player; null = the event's own from FMOD Studio

    public LoopingSound(GameObject owner, Rigidbody body = null)
    {
        this.owner = owner;
        this.body = body;
    }

    // How far away it can be heard: full volume within `fullVolumeWithin` metres, fading out to silent at `silentPast`.
    // Overrides the event's 3D min/max distance from FMOD Studio (its default is 1 to 20).
    public void SetRange(float fullVolumeWithin, float silentPast)
    {
        range = new Vector2(fullVolumeWithin, Mathf.Max(fullVolumeWithin, silentPast));
        ApplyRange();
    }

    void ApplyRange()
    {
        if (range == null || !instance.isValid()) return;
        instance.setProperty(EVENT_PROPERTY.MINIMUM_DISTANCE, range.Value.x);
        instance.setProperty(EVENT_PROPERTY.MAXIMUM_DISTANCE, range.Value.y);
    }

    public void Play(EventReference sound)
    {
        if (sound.IsNull || AudioManager.instance == null)
        {
            Stop();
            return;
        }
        if (instance.isValid() && loaded != sound.Guid) Release(); // switched to a different sound
        if (!instance.isValid())
        {
            instance = AudioManager.instance.CreateInstance(sound);
            loaded = sound.Guid;
            if (!instance.isValid()) return; // not in the built banks
            ApplyRange();
        }

        instance.getPlaybackState(out PLAYBACK_STATE state);
        if (state is PLAYBACK_STATE.STOPPED or PLAYBACK_STATE.STOPPING)
        {
            RuntimeManager.AttachInstanceToGameObject(instance, owner, body); // FMOD lets go of it every time it stops
            instance.start();
        }
    }

    // instant: jump straight there, skipping the parameter's seek modulator in FMOD (if it has one).
    public void SetParameter(string name, float value, bool instant = false)
    {
        if (instance.isValid()) instance.setParameterByName(name, value, instant);
    }

    // Where a parameter actually is right now (partway there while its seek modulator moves it); -1 if not playing.
    public float GetParameter(string name)
    {
        if (!instance.isValid() || instance.getParameterByName(name, out _, out float now) != FMOD.RESULT.OK) return -1f;
        return now;
    }

    public void Stop()
    {
        if (instance.isValid()) instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
    }

    // Done with it for good (the object is going away).
    public void Release()
    {
        if (!instance.isValid()) return;
        if (AudioManager.instance != null) AudioManager.instance.ReleaseInstance(instance);
        else
        {
            instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            instance.release();
        }
        instance.clearHandle();
    }
}
