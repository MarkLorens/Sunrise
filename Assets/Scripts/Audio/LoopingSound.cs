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

    public LoopingSound(GameObject owner, Rigidbody body = null)
    {
        this.owner = owner;
        this.body = body;
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
        }

        instance.getPlaybackState(out PLAYBACK_STATE state);
        if (state is PLAYBACK_STATE.STOPPED or PLAYBACK_STATE.STOPPING)
        {
            RuntimeManager.AttachInstanceToGameObject(instance, owner, body); // FMOD lets go of it every time it stops
            instance.start();
        }
    }

    public void SetParameter(string name, float value)
    {
        if (instance.isValid()) instance.setParameterByName(name, value);
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
