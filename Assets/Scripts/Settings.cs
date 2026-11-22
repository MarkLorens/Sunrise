using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The player's settings, kept between sessions in PlayerPrefs and applied as soon as the game starts. The settings
// screen (SettingsMenu) changes them; the camera reads ShakeStrength and DepthOfField itself (CameraRig).
// Graphics quality changes the render pipeline asset in place; in the Editor its values are put back when Play mode
// ends, so the asset in the project never keeps them.
public static class Settings
{
    public enum Quality { Low, Medium, High }

    public static readonly string[] ShakeNames = { "Off", "Half", "Full" };

    public static float Volume
    {
        get => PlayerPrefs.GetFloat("volume", 1f);
        set { PlayerPrefs.SetFloat("volume", Mathf.Clamp01(value)); ApplyVolume(); }
    }

    // Background music only (FMOD's Music group), on top of Volume.
    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat("musicVolume", 1f);
        set { PlayerPrefs.SetFloat("musicVolume", Mathf.Clamp01(value)); ApplyVolume(); }
    }

    // Sound effects and menu sounds (FMOD's SFX and UI groups, and the old Sfx clips), on top of Volume.
    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat("sfxVolume", 1f);
        set { PlayerPrefs.SetFloat("sfxVolume", Mathf.Clamp01(value)); ApplyVolume(); }
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt("fullscreen", 1) == 1;
        set { PlayerPrefs.SetInt("fullscreen", value ? 1 : 0); ApplyScreen(); }
    }

    // Width x height; 0 = whatever the game started at.
    public static Vector2Int Resolution
    {
        get => new(PlayerPrefs.GetInt("resWidth", 0), PlayerPrefs.GetInt("resHeight", 0));
        set { PlayerPrefs.SetInt("resWidth", value.x); PlayerPrefs.SetInt("resHeight", value.y); ApplyScreen(); }
    }

    public static Quality Graphics
    {
        get => (Quality)PlayerPrefs.GetInt("quality", (int)Quality.High);
        set { PlayerPrefs.SetInt("quality", (int)value); ApplyGraphics(); }
    }

    // 0 off, 1 half, 2 full.
    public static int Shake
    {
        get => PlayerPrefs.GetInt("shake", 2);
        set => PlayerPrefs.SetInt("shake", Mathf.Clamp(value, 0, 2));
    }

    public static float ShakeStrength => Shake * 0.5f;

    public static bool DepthOfField
    {
        get => PlayerPrefs.GetInt("dof", 1) == 1;
        set => PlayerPrefs.SetInt("dof", value ? 1 : 0);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplyAll()
    {
        ApplyVolume();
        ApplyScreen();
        ApplyGraphics();
        Application.quitting += RestoreGraphics;
    }

    // ---------------------------------------------------------------- volume

    static void ApplyVolume()
    {
        AudioListener.volume = Volume * SfxVolume; // the old Sfx clips
        AudioBuses.SetVolume(AudioBuses.Master, Volume);
        AudioBuses.SetVolume(AudioBuses.Music, MusicVolume);
        AudioBuses.SetVolume(AudioBuses.Sfx, SfxVolume);
        AudioBuses.SetVolume(AudioBuses.Ui, SfxVolume);
    }

    // ---------------------------------------------------------------- screen

    // The screen's own resolutions, one per size, smallest first.
    public static Vector2Int[] Resolutions()
    {
        var list = new System.Collections.Generic.List<Vector2Int>();
        foreach (var r in UnityEngine.Screen.resolutions)
        {
            var size = new Vector2Int(r.width, r.height);
            if (!list.Contains(size)) list.Add(size);
        }
        if (list.Count == 0) list.Add(new Vector2Int(UnityEngine.Screen.width, UnityEngine.Screen.height));
        return list.ToArray();
    }

    public static Vector2Int CurrentResolution =>
        Resolution.x > 0 ? Resolution : new Vector2Int(UnityEngine.Screen.width, UnityEngine.Screen.height);

    static void ApplyScreen()
    {
        if (Application.isEditor) return; // the Game view sets its own size
        var mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        var size = CurrentResolution;
        UnityEngine.Screen.SetResolution(size.x, size.y, mode);
    }

    // ---------------------------------------------------------------- graphics

    // High is the project's own setup. Lower levels trade the expensive bits found in the lag review: the
    // screen-space ambient occlusion, shadow-map size and cascades, and the render resolution.
    struct Look
    {
        public bool ao;
        public int shadowRes, cascades;
        public float shadowDistance, renderScale;
    }

    static bool saved;
    static Look original;

    static UniversalRenderPipelineAsset Pipeline => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

    static void ApplyGraphics()
    {
        var urp = Pipeline;
        if (urp == null) return;
        if (!saved)
        {
            original = Read(urp);
            saved = true;
        }
        var look = original;
        switch (Graphics)
        {
            case Quality.Medium:
                look.shadowRes = Mathf.Min(original.shadowRes, 1024);
                look.cascades = Mathf.Min(original.cascades, 2);
                look.renderScale = Mathf.Min(original.renderScale, 0.85f);
                break;
            case Quality.Low:
                look.ao = false;
                look.shadowRes = Mathf.Min(original.shadowRes, 1024);
                look.cascades = 1;
                look.shadowDistance = Mathf.Min(original.shadowDistance, 30f);
                look.renderScale = Mathf.Min(original.renderScale, 0.7f);
                break;
        }
        Write(urp, look);
    }

    static void RestoreGraphics()
    {
        if (saved && Pipeline != null) Write(Pipeline, original);
    }

    static Look Read(UniversalRenderPipelineAsset urp) => new()
    {
        ao = AmbientOcclusion(urp)?.isActive ?? false,
        shadowRes = urp.mainLightShadowmapResolution,
        cascades = urp.shadowCascadeCount,
        shadowDistance = urp.shadowDistance,
        renderScale = urp.renderScale,
    };

    static void Write(UniversalRenderPipelineAsset urp, Look look)
    {
        AmbientOcclusion(urp)?.SetActive(look.ao);
        urp.mainLightShadowmapResolution = look.shadowRes;
        urp.shadowCascadeCount = look.cascades;
        urp.shadowDistance = look.shadowDistance;
        urp.renderScale = look.renderScale;
    }

    static ScriptableRendererFeature AmbientOcclusion(UniversalRenderPipelineAsset urp)
    {
        foreach (var data in urp.rendererDataList)
            if (data != null)
                foreach (var f in data.rendererFeatures)
                    if (f is ScreenSpaceAmbientOcclusion) return f;
        return null;
    }
}
