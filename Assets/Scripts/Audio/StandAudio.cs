using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

// A stand's sound, on whoever runs it: the ice cream stands' jingle now and then, the balloon seller's balloons
// squeaking. The event keeps its own time in FMOD (a scatterer, or an instrument with a chance to play, in a loop),
// so this only starts it once the park's up and stops it when the seller goes (the ice cream sellers leave in the
// finale). Its caption shows while one of its sounds is actually playing and you're near enough to hear it (the
// event's 3D max distance). Added by VisitorSpawner.
public class StandAudio : MonoBehaviour
{
    Func<FMODEvents, EventReference> sound;
    string caption;

    EventInstance instance;
    Playing playing; // how many of the event's sounds are going right now, counted by FMOD's callbacks
    float hearRange;
    bool started;

    public static void Add(GameObject on, Func<FMODEvents, EventReference> sound, string caption)
    {
        var stand = on.AddComponent<StandAudio>();
        stand.sound = sound;
        stand.caption = caption;
    }

    void Update()
    {
        if (!started)
        {
            if (LoadingScreen.Busy) return; // starts with the park, once the loading screen lifts
            started = true;
            Begin();
        }
        if (playing != null && Volatile.Read(ref playing.count) > 0 && Possession.PlayerNear(transform.position, hearRange))
            ClosedCaptions.Hold(caption);
    }

    void Begin()
    {
        var events = FMODEvents.instance;
        var audio = AudioManager.instance;
        if (audio == null || events == null) return;
        var reference = sound(events);
        if (reference.IsNull) return;
        instance = audio.CreateInstance(reference);
        if (!instance.isValid()) return; // not in the built banks
        RuntimeManager.AttachInstanceToGameObject(instance, gameObject);
        instance.getDescription(out EventDescription description);
        description.getMinMaxDistance(out _, out hearRange);

        // FMOD calls back on its own thread, so the callback only touches this little counter, never the component.
        playing = new Playing();
        instance.setUserData(GCHandle.ToIntPtr(GCHandle.Alloc(playing)));
        instance.setCallback(OnSound, EVENT_CALLBACK_TYPE.SOUND_PLAYED | EVENT_CALLBACK_TYPE.SOUND_STOPPED | EVENT_CALLBACK_TYPE.DESTROYED);
        instance.start();
    }

    class Playing
    {
        public int count;
    }

    static readonly EVENT_CALLBACK OnSound = SoundCallback;

    [AOT.MonoPInvokeCallback(typeof(EVENT_CALLBACK))]
    static FMOD.RESULT SoundCallback(EVENT_CALLBACK_TYPE type, IntPtr instancePtr, IntPtr parameters)
    {
        var instance = new EventInstance(instancePtr);
        if (instance.getUserData(out IntPtr data) != FMOD.RESULT.OK || data == IntPtr.Zero) return FMOD.RESULT.OK;
        var handle = GCHandle.FromIntPtr(data);
        if (handle.Target is Playing playing)
        {
            if (type == EVENT_CALLBACK_TYPE.SOUND_PLAYED) Interlocked.Increment(ref playing.count);
            else if (type == EVENT_CALLBACK_TYPE.SOUND_STOPPED) Interlocked.Decrement(ref playing.count);
        }
        if (type == EVENT_CALLBACK_TYPE.DESTROYED) handle.Free(); // the instance is gone: nothing calls back after this
        return FMOD.RESULT.OK;
    }

    // The seller's gone (the finale) or the park's unloading: the sound stops with them.
    void OnDisable()
    {
        if (instance.isValid())
        {
            if (AudioManager.instance != null) AudioManager.instance.ReleaseInstance(instance);
            else
            {
                instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                instance.release();
            }
            instance.clearHandle();
        }
        playing = null;
        started = false; // enabled again, it starts again
    }
}
