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

    // For any script: plays one of FMODEvents' sounds if it's set up. False if it isn't (no slot, no audio system):
    // then it's quiet. No position = a 2D sound (menus, the HUD).
    //   AudioManager.TryPlay(e => e.disabledAction);
    public static bool TryPlay(System.Func<FMODEvents, EventReference> pick, Vector3? at = null)
    {
        var events = FMODEvents.instance;
        if (instance == null || events == null) return false;
        EventReference sound = pick(events);
        if (sound.IsNull) return false;
        instance.PlayOneShot(sound, at ?? Vector3.zero);
        return true;
    }

    // Like TryPlay (2D), with one of the event's labeled parameters set first, to pick which way it sounds:
    //   AudioManager.TryPlayLabeled(e => e.objectiveList, "Objective List", "Shown");
    public static bool TryPlayLabeled(System.Func<FMODEvents, EventReference> pick, string parameter, string label)
    {
        var events = FMODEvents.instance;
        if (instance == null || events == null) return false;
        EventReference sound = pick(events);
        if (sound.IsNull) return false;
        EventInstance once;
        try
        {
            once = RuntimeManager.CreateInstance(sound);
        }
        catch (EventNotFoundException)
        {
            if (instance.missing.Add(sound.Guid)) Debug.LogWarning($"[FMOD] Event not in the banks (rebuild them in FMOD Studio?): {sound}");
            return false;
        }
        once.setParameterByNameWithLabel(parameter, label);
        once.start();
        once.release(); // FMOD frees it once it's done playing
        return true;
    }

    // Like TryPlay, but the sound follows `on` while it plays (a visitor screaming as they run off).
    public static bool TryPlayOn(System.Func<FMODEvents, EventReference> pick, GameObject on)
    {
        var events = FMODEvents.instance;
        if (instance == null || events == null || on == null) return false;
        EventReference sound = pick(events);
        if (sound.IsNull) return false;
        RuntimeManager.PlayOneShotAttached(sound, on); // an event missing from the banks only logs a warning
        return true;
    }

    // Like TryPlay, but every other sound is turned down to OthersVolume while this one plays, then back up
    // (the "Objective Completed!" pop-up).
    public static bool TryPlayOverOthers(System.Func<FMODEvents, EventReference> pick)
    {
        var events = FMODEvents.instance;
        if (instance == null || events == null) return false;
        EventReference sound = pick(events);
        if (sound.IsNull) return false;
        return instance.PlayOverOthers(sound);
    }

    private readonly List<EventInstance> eventInstances = new();
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

    // Leaving a scene (the loading screen: Exit to Main Menu, New Game, Load Game): everything the game was playing
    // stops dead, the music and every park sound (the Music and SFX groups), one-shots too, so nothing from the
    // park carries on into the main menu. The menus' own group (UI) is left alone so the click that got here finishes,
    // apart from "Objective Completed!".
    public void StopGameplaySounds()
    {
        StopMusic(now: true);
        MusicOff = false; // the finale never turns it back on itself; the next scene's music starts at full
        musicLevel = 1f;
        AudioBuses.MusicLevel(1f);
        if (overOthers.isValid()) overOthers.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        AudioBuses.StopAll(AudioBuses.Music);
        AudioBuses.StopAll(AudioBuses.Sfx);
    }

    // Fades out, or `now`: cut dead (starting a game from the main menu, so the New / Load Game sound stands alone).
    public void StopMusic(bool now = false)
    {
        if (!music.isValid()) return;
        eventInstances.Remove(music);
        music.stop(now ? FMOD.Studio.STOP_MODE.IMMEDIATE : FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        music.release(); // FMOD frees it once the fade-out is done
        music.clearHandle();
        musicPlaying = default;
    }

    // ---------------------------------------------------------------- pause menu

    private readonly List<EventInstance> pausedByMenu = new();

    // The pause menu: every FMOD sound playing right now freezes where it is, whatever group it goes through (music,
    // the park, the menus, even events routed straight to Master), and picks up again on Continue. Sounds started
    // after this, like the pause menu's own, play as normal.
    public void PauseAll(bool paused)
    {
        if (!paused)
        {
            foreach (EventInstance eventInstance in pausedByMenu)
            {
                if (eventInstance.isValid()) eventInstance.setPaused(false);
            }
            pausedByMenu.Clear();
            return;
        }
        if (pausedByMenu.Count > 0 || !RuntimeManager.IsInitialized) return; // already paused
        if (RuntimeManager.StudioSystem.getBankList(out Bank[] banks) != FMOD.RESULT.OK) return;
        foreach (Bank bank in banks)
        {
            if (bank.getEventList(out EventDescription[] events) != FMOD.RESULT.OK) continue;
            foreach (EventDescription description in events)
            {
                if (description.getInstanceList(out EventInstance[] instances) != FMOD.RESULT.OK) continue;
                foreach (EventInstance eventInstance in instances)
                {
                    eventInstance.getPaused(out bool alreadyPaused);
                    if (!alreadyPaused && eventInstance.setPaused(true) == FMOD.RESULT.OK) pausedByMenu.Add(eventInstance);
                }
            }
        }
    }

    // ---------------------------------------------------------------- turning everything else down

    private const float OthersVolume = 0.25f; // 75% quieter
    private const float DuckFade = 0.25f;     // seconds to go down, and to come back up
    private EventInstance overOthers;          // the sound everything else makes way for
    private float duckLevel = 1f;              // what the Music and SFX groups are multiplied by right now

    // A puzzle's payoff (the camera's shot of it): the music fades out, and back in once the camera heads home.
    public static bool MusicOff;
    private const float MusicFade = 0.5f;      // seconds to fade out, and back in
    private float musicLevel = 1f;             // what the Music group is multiplied by right now

    // A 2D one-shot that everything else makes way for. FMOD frees it once it has finished.
    public bool PlayOverOthers(EventReference sound)
    {
        EventInstance eventInstance;
        try
        {
            eventInstance = RuntimeManager.CreateInstance(sound);
        }
        catch (EventNotFoundException)
        {
            if (missing.Add(sound.Guid)) Debug.LogWarning($"[FMOD] Event not in the banks (rebuild them in FMOD Studio?): {sound}");
            return false;
        }
        eventInstance.start();
        eventInstance.release();
        overOthers = eventInstance;
        return true;
    }

    bool OverOthersPlaying()
    {
        if (!overOthers.isValid()) return false; // finished and freed
        overOthers.getPlaybackState(out PLAYBACK_STATE state);
        return state != PLAYBACK_STATE.STOPPED;
    }

    // While it plays, the Music and SFX groups (everything in the park, the music, the crowd) ease down to
    // OthersVolume and back up after: the groups' volume, not each sound's, so nothing that sets a sound's own volume
    // (AudioMix, EmitterVolume) undoes it, and sounds that start meanwhile are down too. It's on the UI group itself.
    private void Update()
    {
        float music = MusicOff ? 0f : 1f;
        if (!Mathf.Approximately(musicLevel, music))
        {
            musicLevel = Mathf.MoveTowards(musicLevel, music, Time.unscaledDeltaTime / MusicFade);
            AudioBuses.MusicLevel(musicLevel);
        }

        float target = OverOthersPlaying() ? OthersVolume : 1f;
        if (Mathf.Approximately(duckLevel, target)) return;
        duckLevel = Mathf.MoveTowards(duckLevel, target, Time.unscaledDeltaTime * (1f - OthersVolume) / DuckFade);
        AudioBuses.Duck(duckLevel);
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
    }

    private void OnDestroy()
    {
        if (instance != this) return; // a duplicate going away
        CleanUp();
        instance = null;
    }
}
