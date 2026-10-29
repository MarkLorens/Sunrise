using System.Collections.Generic;
using UnityEngine;

// Fire-and-forget sound effects from Resources/Audio (Kenney Interface & Impact Sounds, CC0).
public static class Sfx
{
    public const string Possess = "glitch_001";
    public const string Leave = "glitch_002";
    public const string Huh = "question_001";
    public const string Spotted = "error_004";
    public const string Refuse = "error_004";
    public const string Exorcised = "bong_001";
    public const string Soul = "bong_001";
    public const string TaskDone = "confirmation_001";
    public const string Crash = "impactPunch_heavy_000";
    public const string HitWood = "impactWood_medium_000";
    public const string HitSoft = "impactSoft_medium_000";
    public const string Thud = "impactSoft_heavy_000";
    public const string Grab = "pluck_001";
    public const string Drop = "drop_002";

    static readonly string[] punches = { "impactPunch_heavy_000", "impactPunch_medium_000", "impactPunch_medium_001" };
    static readonly Dictionary<string, AudioClip> cache = new();

    public static void Punch(float volume = 0.8f) => Play(punches[Random.Range(0, punches.Length)], volume, Random.Range(0.9f, 1.1f));

    public static void Play(string name, float volume = 1f, float pitch = 1f)
    {
        if (!cache.TryGetValue(name, out var clip)) cache[name] = clip = Resources.Load<AudioClip>("Audio/" + name);
        if (clip == null) return;

        var go = new GameObject("Sfx " + name);
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.spatialBlend = 0f;
        source.Play();
        Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
    }
}
