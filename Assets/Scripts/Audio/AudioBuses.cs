using FMODUnity;
using UnityEngine;

// The FMOD mixer's groups, so volume settings and the pause menu can reach them:
//   Music: background music (lobby, Ruckus, possessed).
//   SFX:   everything that happens in the park (footsteps, zaps, screams, the wheel, the speaker).
//   UI:    menus, the pause menu and settings, which keep playing while the game is paused.
// A group missing from the built banks is skipped (the call says false), so nothing breaks before the banks are rebuilt.
public static class AudioBuses
{
    public const string Master = "bus:/";
    public const string Music = "bus:/Music";
    public const string Sfx = "bus:/SFX";
    public const string Ui = "bus:/UI";

    // The built banks have the Music and SFX groups, so the volume sliders go on them (Settings.ApplyVolume).
    public static bool HasGroups =>
        RuntimeManager.IsInitialized
        && RuntimeManager.StudioSystem.getBus(Music, out _) == FMOD.RESULT.OK
        && RuntimeManager.StudioSystem.getBus(Sfx, out _) == FMOD.RESULT.OK;

    // Each group's volume as the sliders set it (Settings), before any turning down (Duck).
    static readonly System.Collections.Generic.Dictionary<string, float> setting = new();
    static float duck = 1f;

    public static bool SetVolume(string path, float volume)
    {
        setting[path] = volume;
        return Apply(path);
    }

    // "Objective Completed!" (AudioManager.PlayOverOthers): the Music and SFX groups at `level` (0..1) of their
    // slider's volume, so the pop-up's sound (UI) stands out; 1 puts them back.
    public static void Duck(float level)
    {
        if (Mathf.Approximately(level, duck)) return;
        duck = level;
        Apply(Music);
        Apply(Sfx);
    }

    static bool Apply(string path)
    {
        float volume = setting.TryGetValue(path, out float v) ? v : 1f;
        if (path == Music || path == Sfx) volume *= duck;
        try
        {
            return RuntimeManager.GetBus(path).setVolume(volume) == FMOD.RESULT.OK; // starts FMOD up if it isn't yet
        }
        catch (System.Exception) { return false; } // no such group, or no FMOD banks set up
    }

    // Every sound going through this group stops dead (leaving a scene: AudioManager.StopGameplaySounds).
    public static bool StopAll(string path)
    {
        try
        {
            return RuntimeManager.IsInitialized && RuntimeManager.GetBus(path).stopAllEvents(FMOD.Studio.STOP_MODE.IMMEDIATE) == FMOD.RESULT.OK;
        }
        catch (System.Exception) { return false; }
    }

    public static bool SetPaused(string path, bool paused)
    {
        try
        {
            return RuntimeManager.IsInitialized && RuntimeManager.GetBus(path).setPaused(paused) == FMOD.RESULT.OK;
        }
        catch (System.Exception) { return false; }
    }

    // The pause menu: the park's music and sounds stop, the menus' own sounds don't. Banks without the groups yet:
    // everything pauses, as before.
    public static void PauseGame(bool paused)
    {
        bool music = SetPaused(Music, paused);
        bool sfx = SetPaused(Sfx, paused);
        if (!music && !sfx) SetPaused(Master, paused);
    }
}
