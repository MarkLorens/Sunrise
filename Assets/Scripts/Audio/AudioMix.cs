using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

// Options > Audio's Background Music and SFX sliders (Settings.MusicVolume, SfxVolume) on the FMOD sounds, for banks
// built before the Music / SFX groups (AudioBuses, which does it once they're there). Those banks have no buses for them (every event goes to Master, which is Master Volume), so this does it here: each frame,
// after the frame's sounds have started, every playing event's volume is set to its share, music events (a path with
// "Music" in it) to MusicVolume and the rest to SfxVolume. (The event's volume, not its channel group's: FMOD Studio's
// own faders drive that.) An event with a volume of its own (EmitterVolume) hands it to OwnVolume each frame instead of
// setting it, and gets the two multiplied. Kept across scenes.
public class AudioMix : MonoBehaviour
{
    readonly Dictionary<FMOD.GUID, bool> isMusic = new();
    static readonly Dictionary<System.IntPtr, float> own = new();

    // A volume of an event's own (0..1) for this frame, multiplied with its slider's.
    public static void OwnVolume(EventInstance instance, float volume) => own[instance.handle] = volume;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Make()
    {
        if (FindAnyObjectByType<AudioMix>() != null) return;
        var go = new GameObject("Audio Mix");
        DontDestroyOnLoad(go);
        go.AddComponent<AudioMix>();
    }

    void LateUpdate()
    {
        if (!RuntimeManager.IsInitialized) return;
        if (RuntimeManager.StudioSystem.getBankList(out Bank[] banks) != FMOD.RESULT.OK) return;
        // Banks with the Music / SFX groups (AudioBuses) already have the sliders on them: only events' own volumes here.
        bool groups = AudioBuses.HasGroups;
        float music = groups ? 1f : Settings.MusicVolume, sfx = groups ? 1f : Settings.SfxVolume;
        foreach (var bank in banks)
        {
            if (bank.getEventList(out EventDescription[] events) != FMOD.RESULT.OK) continue;
            foreach (var e in events)
            {
                if (e.getInstanceCount(out int count) != FMOD.RESULT.OK || count == 0) continue;
                if (e.getInstanceList(out EventInstance[] instances) != FMOD.RESULT.OK) continue;
                float volume = Music(e) ? music : sfx;
                foreach (var instance in instances)
                    instance.setVolume(volume * (own.TryGetValue(instance.handle, out float mine) ? mine : 1f));
            }
        }
        own.Clear();
    }

    bool Music(EventDescription e)
    {
        e.getID(out FMOD.GUID id);
        if (!isMusic.TryGetValue(id, out bool music))
        {
            e.getPath(out string path);
            isMusic[id] = music = path != null && path.IndexOf("Music", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
        return music;
    }
}
