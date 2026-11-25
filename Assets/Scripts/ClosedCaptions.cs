using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Closed captions (Settings.ClosedCaptions, Options > General; off by default): a line in the middle of the screen,
// low down, white words with a soft shadow like the HUD's events, for
// every sound, its words from Captions. Show is for a sound that's heard once (a crash, a chime): up for
// a couple of seconds, the same line again just keeps it up. Hold is for a sound that goes on (footsteps, the wheel's
// hum): call it every frame it's heard and the line stays, fading a moment after the calls stop. Up to MaxLines at
// once, the newest at the bottom. Its own canvas, kept across scenes, over the HUD and under the menus.
public class ClosedCaptions : MonoBehaviour
{
    const int MaxLines = 4;
    const float OnceTime = 2.4f, HeldLinger = 0.4f, Fade = 0.25f;

    class Line
    {
        public string text;
        public float until;     // shown until (unscaled time)
        public bool held;
        public RectTransform rt;
        public CanvasGroup group;
        public float age;
    }

    static ClosedCaptions instance;
    readonly List<Line> lines = new();
    RectTransform column;

    public static void Show(string text, float seconds = OnceTime)
    {
        if (string.IsNullOrEmpty(text) || !Settings.ClosedCaptions) return;
        Get()?.Add(text, seconds, false);
    }

    public static void Hold(string text)
    {
        if (string.IsNullOrEmpty(text) || !Settings.ClosedCaptions) return;
        Get()?.Add(text, HeldLinger, true);
    }

    static ClosedCaptions Get()
    {
        if (instance != null) return instance;
        if (!Application.isPlaying) return null;
        var canvas = MainMenu.Screen("Closed Captions", 18); // over the HUD (10) and the tutorial (15), under the pause menu (20)
        DontDestroyOnLoad(canvas.gameObject);
        instance = canvas.gameObject.AddComponent<ClosedCaptions>();
        canvas.GetComponent<GraphicRaycaster>().enabled = false; // never in the way of a click
        instance.column = MainMenu.Ui("Lines", canvas);
        var c = instance.column;
        c.anchorMin = c.anchorMax = c.pivot = new Vector2(0.5f, 0f);
        c.anchoredPosition = new Vector2(0f, 240f);
        var layout = c.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.LowerCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fit = c.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return instance;
    }

    void Add(string text, float seconds, bool held)
    {
        float until = Time.unscaledTime + seconds;
        foreach (var l in lines)
            if (l.text == text)
            {
                l.until = Mathf.Max(l.until, until);
                return;
            }
        if (lines.Count >= MaxLines)
        {
            Destroy(lines[0].rt.gameObject);
            lines.RemoveAt(0);
        }
        // The words alone, as wide as they are.
        var rt = MainMenu.Ui(text, column);
        var row = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        var words = MainMenu.Ui("Words", rt);
        var label = MainMenu.Label(words, text, Figma.Type.ArvoBody, Color.white, ParkArt.Font);
        var shadow = label.gameObject.AddComponent<Shadow>(); // as the HUD's events: readable over a bright park
        shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
        var group = rt.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        lines.Add(new Line { text = text, until = until, held = held, rt = rt, group = group });
    }

    void Update()
    {
        bool on = Settings.ClosedCaptions;
        if (musicPending && !LoadingScreen.Busy && Time.timeScale > 0f)
        {
            musicPending = false;
            Show(Captions.Music, 5f);
        }
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            var l = lines[i];
            l.age += Time.unscaledDeltaTime;
            float left = l.until - Time.unscaledTime;
            l.group.alpha = on ? Mathf.Min(Mathf.Clamp01(l.age / Fade), Mathf.Clamp01(left / Fade)) : 0f;
            if (left > 0f && on) continue;
            Destroy(l.rt.gameObject);
            lines.RemoveAt(i);
        }
    }

    static bool musicPending;

    // A new scene: its music gets a line once the loading screen's gone, and nothing from the last scene lingers.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= Loaded;
        SceneManager.sceneLoaded += Loaded;
    }

    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (instance != null)
        {
            foreach (var l in instance.lines) Destroy(l.rt.gameObject);
            instance.lines.Clear();
        }
        foreach (var e in FindObjectsByType<FMODUnity.StudioEventEmitter>(FindObjectsSortMode.None))
            if (!e.EventReference.IsNull && FMODUnity.RuntimeManager.GetEventDescription(e.EventReference).getPath(out string path) == FMOD.RESULT.OK && path.Contains("Music"))
            {
                musicPending = Settings.ClosedCaptions;
                if (musicPending) Get(); // its Update shows the line
                break;
            }
    }
}
