using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The settings screen (Options), opened from the main menu or the pause menu. One row per setting (Settings): left/right arrow keys, a gamepad
// stick, or clicking the row's left or right half steps it down or up; Enter steps it up. Back (or Esc) closes it.
// Changes apply and are kept straight away.
public class SettingsMenu : MonoBehaviour
{
    public static bool IsOpen => open != null;
    static SettingsMenu open;

    Action onClose;

    public static void Open(int order, Action onClose)
    {
        if (open != null) return;
        var canvas = MainMenu.Screen("Settings Menu", order);
        open = canvas.gameObject.AddComponent<SettingsMenu>();
        open.onClose = onClose;
        open.Build(canvas);
    }

    void Build(RectTransform canvas)
    {
        // Over the main menu: the dark blue. In the park: the park, blurred and dimmed like the pause menu.
        var dim = MainMenu.Ui("Dim", canvas);
        MainMenu.Stretch(dim);
        bool inPark = Possession.Instance != null;
        MainMenu.Img(dim, null, inPark ? MainMenu.PauseShade : MainMenu.Night).raycastTarget = true;
        if (inPark) ScreenBlur.Add(canvas, Color.white);
        var column = MainMenu.Column(canvas);
        MainMenu.Title(column, "OPTIONS", 560f, 80);

        var resolutions = Settings.Resolutions();
        var first = Row(column, "Master volume", () => Percent(Settings.Volume),
            d => Nudge(() => Settings.Volume, v => Settings.Volume = v, d));
        Row(column, "Music volume", () => Percent(Settings.MusicVolume),
            d => Nudge(() => Settings.MusicVolume, v => Settings.MusicVolume = v, d));
        Row(column, "Sound effects volume", () => Percent(Settings.SfxVolume),
            d => Nudge(() => Settings.SfxVolume, v => Settings.SfxVolume = v, d));
        Row(column, "Fullscreen", () => Settings.Fullscreen ? "On" : "Off", Changed(_ => Settings.Fullscreen = !Settings.Fullscreen));
        Row(column, "Resolution", () => $"{Settings.CurrentResolution.x} x {Settings.CurrentResolution.y}",
            Changed(d => Settings.Resolution = resolutions[Wrap(Array.IndexOf(resolutions, Settings.CurrentResolution) + d, resolutions.Length)]));
        Row(column, "Graphics", () => Settings.Graphics.ToString(),
            Changed(d => Settings.Graphics = (Settings.Quality)Wrap((int)Settings.Graphics + d, 3)));
        Row(column, "Screen shake", () => Settings.ShakeNames[Settings.Shake], Changed(d => Settings.Shake = Wrap(Settings.Shake + d, 3)));
        Row(column, "Depth of field", () => Settings.DepthOfField ? "On" : "Off", Changed(_ => Settings.DepthOfField = !Settings.DepthOfField));
        MainMenu.Button(column, "Back", Close, sound: e => e.backButton); // S-06
        MainMenu.Select(first);
    }

    static int Wrap(int i, int n) => ((i % n) + n) % n;

    // Volumes go in tenths.
    static string Percent(float volume) => $"{Mathf.RoundToInt(volume * 100f)}%";

    // A volume one tenth up or down, with its sound played at the new volume: up (S-01), down (S-02), or already at
    // 0% or 100% (S-03).
    static void Nudge(Func<float> get, Action<float> set, int d)
    {
        float was = get();
        set(Mathf.Clamp01(Mathf.Round(was * 10f + d) / 10f));
        float now = get();
        if (Mathf.Approximately(now, was)) AudioManager.TryPlay(e => e.volumeLimit);
        else if (now > was) AudioManager.TryPlay(e => e.volumeUp);
        else AudioManager.TryPlay(e => e.volumeDown);
    }

    // Any other setting changing: the toggle sound (S-04).
    static Action<int> Changed(Action<int> step) => d =>
    {
        step(d);
        AudioManager.TryPlay(e => e.toggle);
    };

    static Button Row(RectTransform column, string name, Func<string> value, Action<int> step)
    {
        var button = MainMenu.Button(column, name, null, 680f);
        button.navigation = new Navigation { mode = Navigation.Mode.Vertical }; // left/right change the value instead
        var row = button.gameObject.AddComponent<OptionRow>();
        row.label = button.GetComponentInChildren<Text>();
        row.name_ = name;
        row.value = value;
        row.step = step;
        row.Refresh();
        return button;
    }

    void Update()
    {
        if (!Controls.Back) return; // Esc, or a gamepad's B
        AudioManager.TryPlay(e => e.backButton); // S-06, as if Back was clicked
        Close();
    }

    void Close()
    {
        PlayerPrefs.Save();
        open = null;
        Destroy(gameObject);
        onClose?.Invoke();
    }

    void OnDestroy()
    {
        if (open == this) open = null;
    }

    // One setting's row: "<  Name: value  >" (plain arrows: the hand-drawn font has no fancy ones).
    class OptionRow : MonoBehaviour, IMoveHandler, ISubmitHandler, IPointerClickHandler
    {
        public Text label;
        public string name_;
        public Func<string> value;
        public Action<int> step;

        public void Refresh() => label.text = $"<   {name_}: {value()}   >";

        void Step(int d)
        {
            step(d);
            Refresh();
        }

        public void OnMove(AxisEventData e)
        {
            if (e.moveDir == MoveDirection.Left) Step(-1);
            else if (e.moveDir == MoveDirection.Right) Step(1);
        }

        public void OnSubmit(BaseEventData e) => Step(1);

        public void OnPointerClick(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out Vector2 p);
            Step(p.x < 0f ? -1 : 1);
        }
    }
}
