using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

// Plays FMOD sounds for the scripts and keeps track of every looping sound it made, so they all stop when the scene
// ends instead of hanging around. Lives on the Audio object next to FMODEvents.
public class AudioManager : MonoBehaviour
{
    public static AudioManager instance { get; private set; }

    private readonly List<EventInstance> eventInstances = new();
    private readonly List<StudioEventEmitter> eventEmitters = new();

    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("Found more than one AudioManager in the scene.");
        }
        instance = this;
    }

    // A fire-and-forget sound at a point in the world (a click, a crash, a yelp).
    public void PlayOneShot(EventReference sound, Vector3 worldPos)
    {
        if (sound.IsNull) return;
        RuntimeManager.PlayOneShot(sound, worldPos);
    }

    // A sound you start and stop yourself (loops). Hand it back with ReleaseInstance when you're done with it.
    public EventInstance CreateInstance(EventReference sound)
    {
        EventInstance eventInstance = RuntimeManager.CreateInstance(sound);
        eventInstances.Add(eventInstance);
        return eventInstance;
    }

    public void ReleaseInstance(EventInstance eventInstance)
    {
        eventInstances.Remove(eventInstance);
        eventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        eventInstance.release();
    }

    // Points a Studio Event Emitter already on `emitterObject` at `sound`, and stops it with everything else at the end.
    public StudioEventEmitter InitializeEventEmitter(EventReference sound, GameObject emitterObject)
    {
        StudioEventEmitter emitter = emitterObject.GetComponent<StudioEventEmitter>();
        emitter.EventReference = sound;
        eventEmitters.Add(emitter);
        return emitter;
    }

    private void CleanUp()
    {
        foreach (EventInstance eventInstance in eventInstances)
        {
            eventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            eventInstance.release();
        }
        eventInstances.Clear();
        foreach (StudioEventEmitter emitter in eventEmitters)
        {
            if (emitter != null) emitter.Stop();
        }
        eventEmitters.Clear();
    }

    private void OnDestroy()
    {
        CleanUp();
        if (instance == this) instance = null;
    }
}
