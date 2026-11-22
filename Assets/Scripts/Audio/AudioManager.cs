using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

// Plays FMOD sounds for the scripts and keeps track of every looping sound it made, so they all stop when the scene
// ends instead of hanging around. Lives on the AudioSystem prefab (Resources) next to FMODEvents: it's made once when
// the game starts, from the main menu or straight into a level, and stays for every scene after.
public class AudioManager : MonoBehaviour
{
    public static AudioManager instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Spawn()
    {
        if (instance != null) return;
        var prefab = Resources.Load<GameObject>("AudioSystem");
        if (prefab != null) Instantiate(prefab).name = prefab.name;
    }

    // For any script: plays one of FMODEvents' sounds if it's set up. False if it isn't (no slot, no audio system),
    // so the caller can fall back to its old sound. No position = a 2D sound (menus, the HUD).
    //   if (!AudioManager.TryPlay(e => e.disabledAction)) Sfx.Play(Sfx.Refuse);
    public static bool TryPlay(System.Func<FMODEvents, EventReference> pick, Vector3? at = null)
    {
        var events = FMODEvents.instance;
        if (instance == null || events == null) return false;
        EventReference sound = pick(events);
        if (sound.IsNull) return false;
        instance.PlayOneShot(sound, at ?? Vector3.zero);
        return true;
    }

    private readonly List<EventInstance> eventInstances = new();
    private readonly List<StudioEventEmitter> eventEmitters = new();
    private readonly HashSet<FMOD.GUID> missing = new(); // events not in the built banks: warned about once

    private EventInstance music;
    private FMOD.GUID musicPlaying;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject); // an extra one (say, dropped into a scene): the first one keeps going
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // A fire-and-forget sound at a point in the world (a crash, a yelp).
    public void PlayOneShot(EventReference sound, Vector3 worldPos)
    {
        if (sound.IsNull) return;
        RuntimeManager.PlayOneShot(sound, worldPos); // an event missing from the banks only logs a warning
    }

    // A fire-and-forget sound that isn't anywhere in the world: menu clicks, the pause menu, the HUD (2D events).
    public void PlayOneShot(EventReference sound)
    {
        PlayOneShot(sound, Vector3.zero);
    }

    // A sound you start and stop yourself (loops). Hand it back with ReleaseInstance when you're done with it.
    // An event that isn't in the built banks gives an empty instance (check isValid) instead of an error.
    public EventInstance CreateInstance(EventReference sound)
    {
        EventInstance eventInstance;
        try
        {
            eventInstance = RuntimeManager.CreateInstance(sound);
        }
        catch (EventNotFoundException)
        {
            if (missing.Add(sound.Guid)) Debug.LogWarning($"[FMOD] Event not in the banks (rebuild them in FMOD Studio?): {sound}");
            return default;
        }
        eventInstances.Add(eventInstance);
        return eventInstance;
    }

    public void ReleaseInstance(EventInstance eventInstance)
    {
        eventInstances.Remove(eventInstance);
        eventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        eventInstance.release();
    }

    // ---------------------------------------------------------------- music

    // One music track at a time (lobby, Ruckus, possessed). Asking for a different one fades the old one out
    // and starts the new one; asking for the one already playing does nothing. An empty slot stops the music.
    // The audio system outlives scenes, so whoever starts music stops it when they go (MainMenu does).
    public void PlayMusic(EventReference track)
    {
        if (track.IsNull)
        {
            StopMusic();
            return;
        }
        if (music.isValid() && musicPlaying == track.Guid) return;
        StopMusic();
        music = CreateInstance(track);
        if (!music.isValid()) return;
        musicPlaying = track.Guid;
        music.start();
    }

    public void StopMusic()
    {
        if (!music.isValid()) return;
        eventInstances.Remove(music);
        music.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        music.release(); // FMOD frees it once the fade-out is done
        music.clearHandle();
        musicPlaying = default;
    }

    // ---------------------------------------------------------------- emitters

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
        StopMusic(); // fades out
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
        if (instance != this) return; // a duplicate going away
        CleanUp();
        instance = null;
    }
}
