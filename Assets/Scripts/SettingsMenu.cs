using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Options, from the main menu or the pause menu, laid out off the artist's four design pages (Figma: Options -
// General / Display / Audio / Game Controls): "OPTIONS" over the page background (UI Asset/PageBG), four tabs
// (Tab_Active for the page showing, Tab_Idle for the others), and the page's rows under them.
// A row is picked by the mouse or up/down (its words go bold and yellow, its arrows/slider yellow too); left/right
// changes it, and so does the mouse: clicking an arrow or a pill, or pressing anywhere on a volume bar (drag to slide). Up from the first row reaches the tabs, where left/right turns the page; clicking a tab does too. Esc
// asks Back.
// Changes show straight away, but they're only kept with Apply Changes (bottom right, with Reset and Back, placed as
// Credits' Back): Reset puts every setting back to how the game first starts; Back with changes not applied asks
// "DISCARD CHANGES" first (No stays, Yes puts them back and leaves).
// What works: window mode, graphics, screen shake, resolution, depth of field, master volume, closed captions
// (Settings) and the game controls (KeyBindings). Language is English only. Everything else works too: window mode,
// graphics, screen shake, resolution (the screen's own, Settings.Resolutions), depth of field, vertical sync,
// anti-aliasing, and the master, music and SFX volumes (Settings; AudioMix).
public class SettingsMenu : MonoBehaviour
{
    public static bool IsOpen => open != null;
    static SettingsMenu open;

    static readonly string[] TabNames = { "General", "Display", "Audio", "Game\nControl" };
    // Each page's tabs (where their words' middles are, across), as the design draws them: the open one is wider.
    static readonly float[][] TabX =
    {
        new[] { 188.5f, 398f, 579.5f, 766f },
        new[] { 170f, 376.5f, 582.5f, 766f },
        new[] { 170f, 352f, 557.5f, 766f },
        new[] { 168f, 350f, 534.5f, 742f },
    };
    static readonly Color Yellow = new Color32(254, 208, 62, 255), Ink = new Color32(13, 9, 11, 255);
    const float RowY = 392f, RowStep = 70f, ContentTop = 330f;
    const float ActiveLift = 16f; // the open tab sits this much higher than the design draws it (its top under the header)


    Action onClose;
    RectTransform canvas, page;
    int tab;
    readonly List<Button> tabButtons = new();
    readonly List<Row> rows = new();
    ScrollRect scroll;
    int openedFrame, order, confirmClosedFrame = -1;
    State kept; // as it was when Options opened, or last applied
    Button resetButton, applyButton, backButton;

    // One line of a page: its label and whatever shows its value (arrows and words, pills, or a slider).
    class Row
    {
        public Button button;
        public Text label;
        public Text value;
        public Image left, right;          // arrow rows
        public Image bar, knob;             // slider rows
        public Image[] pills; public string[] options; // pill rows
        public Func<int> current;           // pill rows: which pill's on
        public Func<string> shown;          // arrow rows: the value's words
        public Func<float> amount;          // slider rows: 0..100
        public Action<int> step;
    }

    public static void Open(int order, Action onClose)
    {
        if (open != null) return;
        var canvas = MainMenu.Screen("Options", order);
        open = canvas.gameObject.AddComponent<SettingsMenu>();
        open.onClose = onClose;
        open.canvas = canvas;
        open.openedFrame = Time.frameCount;
        open.order = order;
        open.kept = State.Now();
        open.Build();
    }

    void Build()
    {
        var bgRt = MainMenu.Ui("Page", canvas);
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
        var bg = bgRt.gameObject.AddComponent<Image>();
        bg.sprite = UiArt.Get("PageBG");
        bg.raycastTarget = true; // nothing behind it gets clicked
        var fit = bgRt.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = 2268f / 1479f;
        Figma.Words(canvas, "OPTIONS", 304.5f, 111.5f, Figma.Type.AtmaPage, Color.white, ParkArt.TitleFont);
        ShowTab(0);
    }

    // ---------------------------------------------------------------- tabs

    void ShowTab(int t)
    {
        tab = t;
        if (page != null) Destroy(page.gameObject);
        page = MainMenu.Ui("Tab " + t, canvas);
        MainMenu.Stretch(page);
        tabButtons.Clear();
        rows.Clear();
        scroll = null;
        firstControl = cards[0] = cards[1] = null;
        controlsContent = null;
        pills.Clear();
        pillBinds.Clear();

        // Idle tabs first, the open one last so it overlaps them.
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < 4; i++)
            {
                bool on = i == t;
                if (on != (pass == 1)) continue;
                float x = TabX[t][i];
                var art = Figma.Picture(page, "Tab " + TabNames[i], on ? "Tab_Active" : "Tab_Idle",
                    on ? x + 2f : x, on ? 250.3f - ActiveLift : 222.2f, on ? 243.5f : 197f, on ? 159f : 105.5f);
                art.raycastTarget = true;
                var b = art.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                int which = i;
                b.onClick.AddListener(() => { if (which != tab) ShowTab(which); });
                art.gameObject.AddComponent<MainMenu.HoverSelects>();
                art.gameObject.AddComponent<TabKeys>().menu = this;
                tabButtons.Add(b);
                string[] lines = TabNames[i].Split('\n');
                for (int l = 0; l < lines.Length; l++)
                {
                    float y = lines.Length == 1 ? (on ? 257.5f - ActiveLift : 221f) : on ? 218f - ActiveLift + 45f * l : 201f + 30f * l;
                    Figma.Words(page, lines[l], x, y, on ? Figma.Type.AtmaH1 : Figma.Type.ArvoSubtitle, on ? Yellow : Color.white, on ? ParkArt.TitleFont : ParkArt.Font);
                }
            }

        switch (t)
        {
            case 0: General(); break;
            case 1: Display(); break;
            case 2: Audio(); break;
            default: Controls(); break;
        }
        Buttons();
        Wire();
        MainMenu.Select(rows.Count > 0 ? rows[0].button : firstControl != null ? firstControl : tabButtons[^1]);
    }

    class TabKeys : MonoBehaviour, IMoveHandler
    {
        public SettingsMenu menu;
        public void OnMove(AxisEventData e)
        {
            if (e.moveDir == MoveDirection.Left && menu.tab > 0) menu.ShowTab(menu.tab - 1);
            else if (e.moveDir == MoveDirection.Right && menu.tab < 3) menu.ShowTab(menu.tab + 1);
            else if (e.moveDir == MoveDirection.Down && menu.rows.Count > 0) MainMenu.Select(menu.rows[0].button);
            else if (e.moveDir == MoveDirection.Down && menu.cards[0] != null) MainMenu.Select(menu.cards[0]);
        }
    }

    // ---------------------------------------------------------------- the pages

    void General()
    {
        ArrowRow(0, 146f, "Language", 650f, 862.5f, 756f, () => "English", _ => { });
        ArrowRow(1, 146f, "Closed Caption", 650f, 862.5f, 756f, () => Settings.ClosedCaptions ? "ON" : "OFF", _ => Settings.ClosedCaptions = !Settings.ClosedCaptions);
    }

    void Display()
    {
        PillRow(0, "Window Mode", new[] { "Windowed", "Fullscreen" }, () => Settings.Fullscreen ? 1 : 0, i => Settings.Fullscreen = i == 1);
        PillRow(1, "Graphic", new[] { "Low", "Medium", "High" }, () => (int)Settings.Graphics, i => Settings.Graphics = (Settings.Quality)i);
        PillRow(2, "Screen Shake", new[] { "Full", "Half", "Off" }, () => 2 - Settings.Shake, i => Settings.Shake = 2 - i);
        ArrowRow(3, 152f, "Resolution", 574f, 786.4f, 679.5f, () => $"{Settings.CurrentResolution.x} x {Settings.CurrentResolution.y}",
            d => Settings.Resolution = Settings.StepResolution(d));
        ArrowRow(4, 152f, "Depth of field", 574f, 786.4f, 679.5f, () => Settings.DepthOfField ? "ON" : "OFF", _ => Settings.DepthOfField = !Settings.DepthOfField);
        ArrowRow(5, 152f, "Vertical Sync", 574f, 786.4f, 679.5f, () => Settings.VSync ? "ON" : "OFF", _ => Settings.VSync = !Settings.VSync);
        ArrowRow(6, 152f, "Anti-Aliasing", 574f, 786.4f, 679.5f, () => Settings.AntiAliasing ? "ON" : "OFF", _ => Settings.AntiAliasing = !Settings.AntiAliasing);
    }

    void Audio()
    {
        SliderRow(0, "Master Volume", () => Mathf.Round(Settings.Volume * 100f), v => Settings.Volume = v / 100f);
        SliderRow(1, "Background Music", () => Mathf.Round(Settings.MusicVolume * 100f), v => Settings.MusicVolume = v / 100f);
        SliderRow(2, "SFX", () => Mathf.Round(Settings.SfxVolume * 100f), v => Settings.SfxVolume = v / 100f);
    }

    static int Wrap(int i, int n) => ((i % n) + n) % n;

    // ---------------------------------------------------------------- rows

    Row NewRow(int index, float labelX, string name)
    {
        float y = RowY + RowStep * index;
        var r = new Row();
        var hit = MainMenu.Ui(name, page);
        Figma.Place(hit, 790f, y, 1320f, RowStep); // the whole line is the row
        var img = hit.gameObject.AddComponent<Image>();
        img.color = Color.clear;
        r.button = hit.gameObject.AddComponent<Button>();
        r.button.targetGraphic = img;
        r.button.transition = Selectable.Transition.None;
        hit.gameObject.AddComponent<MainMenu.HoverSelects>();
        hit.gameObject.AddComponent<RowKeys>().row = r;
        r.label = Left(page, name, labelX, y, Figma.Type.ArvoSubtitle);
        rows.Add(r);
        return r;
    }

    void ArrowRow(int index, float labelX, string name, float leftX, float rightX, float valueX, Func<string> shown, Action<int> step)
    {
        var r = NewRow(index, labelX, name);
        float y = RowY + RowStep * index;
        r.left = Figma.Picture(page, "Left", "Buttons/Arrows/LeftArrow_Idle", leftX, y + 1.5f, 47f, 47f);
        r.right = Figma.Picture(page, "Right", "Buttons/Arrows/RightArrow_Idle", rightX, y, 47f, 47f);
        r.value = Figma.Words(page, shown(), valueX, y, Figma.Type.ArvoSubtitle, Ink, ParkArt.Font);
        r.shown = shown;
        r.step = d =>
        {
            string was = shown();
            step(d);
            if (shown() != was) AudioManager.TryPlay(e => e.toggle); // S-04
        };
        ArrowClick(r.left, r, -1);
        ArrowClick(r.right, r, 1);
    }

    // A click on an arrow steps its row, as left / right does.
    void ArrowClick(Image arrow, Row r, int d)
    {
        arrow.raycastTarget = true;
        var b = arrow.gameObject.AddComponent<Button>();
        b.targetGraphic = arrow;
        b.transition = Selectable.Transition.None;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.onClick.AddListener(() =>
        {
            r.step?.Invoke(d);
            MainMenu.Select(r.button);
            Refresh();
        });
        arrow.gameObject.AddComponent<PicksRow>().row = r.button;
    }

    // An arrow or pill picks its row the moment the mouse goes down on it. (They're buttons that can't be picked
    // themselves, and pressing one would otherwise leave nothing picked until the click: the menu would jump to the
    // first row and back, a flicker.)
    class PicksRow : MonoBehaviour, IPointerDownHandler
    {
        public Button row;
        public void OnPointerDown(PointerEventData e) => MainMenu.Select(row);
    }

    // The pills, left to right, as the design spaces them; the one that's set is the yellow one.
    static readonly float[] PillX = { 676f, 985f, 1290.5f };

    void PillRow(int index, string name, string[] options, Func<int> current, Action<int> set)
    {
        set = Toggled(current, set);
        var r = NewRow(index, 152f, name);
        float y = RowY + RowStep * index - 4.5f;
        r.pills = new Image[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            r.pills[i] = Figma.Picture(page, options[i], "Buttons/Button2_Idle", PillX[i], y, 286f, 61f);
            r.pills[i].raycastTarget = true;
            int which = i;
            var b = r.pills[i].gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => { set(which); Refresh(); MainMenu.Select(r.button); });
            r.pills[i].gameObject.AddComponent<PicksRow>().row = r.button;
            Figma.Words(page, options[i], PillX[i] + 6f, y - 4.5f, Figma.Type.ArvoBody, Ink, ParkArt.Font);
        }
        r.options = options;
        r.current = current;
        r.step = d => set(Mathf.Clamp(current() + d, 0, options.Length - 1));
    }

    // A setting changing to another of its choices: the toggle sound (S-04).
    static Action<int> Toggled(Func<int> current, Action<int> set) => i =>
    {
        int was = current();
        set(i);
        if (current() != was) AudioManager.TryPlay(e => e.toggle);
    };

    // `set` takes 0..100; left / right go to the next ten.
    void SliderRow(int index, string name, Func<float> amount, Action<float> set)
    {
        var r = NewRow(index, 146f, name);
        float y = RowY + RowStep * index;
        r.bar = Figma.Picture(page, "Bar", "Volume/VolumeBar_Idle", 891.5f, y, 422f, 46.9f);
        r.knob = Figma.Picture(page, "Knob", "Volume/VolumeSelecter_Idle", 0f, y, 24.7f, 54f);
        r.value = Figma.Words(page, "", 1174f, y, Figma.Type.ArvoSubtitle, Ink, ParkArt.Font);
        r.amount = amount;
        r.step = d =>
        {
            // A tenth up or down, its sound at the new volume: up (S-01), down (S-02), or already at 0 or 100 (S-03).
            float was = amount();
            set(Mathf.Clamp(Mathf.Round(was / 10f + d) * 10f, 0f, 100f));
            float now = amount();
            if (Mathf.Approximately(now, was)) AudioManager.TryPlay(e => e.volumeLimit);
            else if (now > was) AudioManager.TryPlay(e => e.volumeUp);
            else AudioManager.TryPlay(e => e.volumeDown);
        };
        foreach (var img in new[] { r.bar, r.knob })
        {
            img.raycastTarget = true;
            var drag = img.gameObject.AddComponent<SliderDrag>();
            drag.menu = this;
            drag.row = r;
            drag.set = set;
        }
    }

    // Pressing on a volume bar (or its knob) puts the knob there, to the nearest whole number; dragging slides it.
    class SliderDrag : MonoBehaviour, IPointerDownHandler, IDragHandler, IInitializePotentialDragHandler
    {
        public SettingsMenu menu;
        public Row row;
        public Action<float> set;

        public void OnPointerDown(PointerEventData e) => Move(e);
        public void OnDrag(PointerEventData e) => Move(e);
        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false; // follows at once

        void Move(PointerEventData e)
        {
            var page = menu.page;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(page, e.position, e.pressEventCamera, out var local)) return;
            float x = (local.x - page.rect.center.x) / Figma.K + Figma.Width * 0.5f; // in design px, as the knob's placed
            set(Mathf.Clamp(Mathf.Round((x - 700f) / 3.86f), 0f, 100f));
            MainMenu.Select(row.button);
            menu.Refresh();
        }
    }

    // A label starting at x (left-aligned), its middle at y.
    static Text Left(RectTransform parent, string text, float x, float y, int size)
    {
        var rt = MainMenu.Ui(text, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2((x - Figma.Width * 0.5f) * Figma.K, (Figma.Height * 0.5f - y) * Figma.K);
        rt.sizeDelta = new Vector2(600f, size * 2f);
        var t = MainMenu.Label(rt, text, size, Ink, ParkArt.Font);
        t.alignment = TextAnchor.MiddleLeft;
        return t;
    }

    class RowKeys : MonoBehaviour, IMoveHandler, ISubmitHandler
    {
        public Row row;
        public void OnMove(AxisEventData e)
        {
            if (e.moveDir == MoveDirection.Left) row.step?.Invoke(-1);
            else if (e.moveDir == MoveDirection.Right) row.step?.Invoke(1);
            else return;
            open?.Refresh();
        }
        public void OnSubmit(BaseEventData e)
        {
            row.step?.Invoke(1);
            open?.Refresh();
        }
    }

    // Up/down between rows; up from the first row to the open tab.
    void Wire()
    {
        for (int i = 0; i < rows.Count; i++)
            rows[i].button.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = i > 0 ? rows[i - 1].button : tabButtons[^1],
                selectOnDown = i < rows.Count - 1 ? rows[i + 1].button : applyButton,
            };
        foreach (var b in tabButtons) b.navigation = new Navigation { mode = Navigation.Mode.None }; // TabKeys
        Refresh();
    }

    // Every row's words and pictures for its value, and the picked one in yellow.
    void Refresh()
    {
        var picked = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        foreach (var r in rows)
        {
            bool on = picked == r.button.gameObject;
            r.label.color = on ? Yellow : Ink;
            r.label.fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
            if (r.shown != null)
            {
                r.value.text = r.shown();
                r.value.color = on ? Yellow : Ink;
                r.value.fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
                r.left.sprite = UiArt.Get(on ? "Buttons/Arrows/LeftArrow_Active" : "Buttons/Arrows/LeftArrow_Idle");
                r.right.sprite = UiArt.Get(on ? "Buttons/Arrows/RightArrow_Active" : "Buttons/Arrows/RightArrow_Idle");
            }
            if (r.pills != null)
                for (int i = 0; i < r.pills.Length; i++)
                    r.pills[i].sprite = UiArt.Get(i == r.current() ? "Buttons/Button2_Active" : "Buttons/Button2_Idle");
            if (r.amount != null)
            {
                float v = r.amount();
                r.value.text = Mathf.RoundToInt(v).ToString();
                r.value.color = on ? Yellow : Ink;
                r.value.fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
                r.bar.sprite = UiArt.Get(on ? "Volume/VolumeBar_Active" : "Volume/VolumeBar_Idle");
                r.knob.sprite = UiArt.Get(on ? "Volume/VolumeSelecter_Active" : "Volume/VolumeSelecter_Idle");
                var k = r.knob.rectTransform;
                // Along the bar's line, as the design places 50 (its middle at 700 + 3.86 per step).
                k.anchoredPosition = new Vector2((700f + v * 3.86f - Figma.Width * 0.5f) * Figma.K, k.anchoredPosition.y);
            }
        }
    }

    // ---------------------------------------------------------------- Reset, Apply Changes, Back

    // Bottom right, Back where Credits has it and the other two to its left, in the same light blue pills.
    void Buttons()
    {
        resetButton = BottomPill("Reset", 819.5f, 213f, Reset);
        applyButton = BottomPill("Apply Changes", 1072f, 260f, Apply);
        backButton = BottomPill("Back", 1324.5f, 213f, Back, e => e.backButton); // S-06
        // Up from them: the page's last row (or last key on Game Control, or the tab); left / right along them.
        Selectable leftUp = rows.Count > 0 ? rows[^1].button : pills.Count > 0 ? pills[LeftBinds.Length - 1] : tabButtons[^1];
        Selectable rightUp = rows.Count > 0 ? rows[^1].button : pills.Count > 0 ? pills[^1] : tabButtons[^1];
        resetButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = leftUp, selectOnRight = applyButton };
        applyButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = rightUp, selectOnLeft = resetButton, selectOnRight = backButton };
        backButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = rightUp, selectOnLeft = applyButton };
        // Down from the bottom of each column of keys.
        foreach (var i in new[] { LeftBinds.Length - 1, pills.Count - 1 })
            if (i >= 0 && i < pills.Count)
            {
                var nav = pills[i].navigation;
                nav.selectOnDown = applyButton;
                pills[i].navigation = nav;
            }
    }

    Button BottomPill(string words, float x, float w, UnityEngine.Events.UnityAction onClick,
        Func<FMODEvents, FMODUnity.EventReference> sound = null)
    {
        var b = MainMenu.Pill(page, words, onClick, sound: sound);
        Figma.Place((RectTransform)b.transform, x, 897.5f, w, 63f);
        b.GetComponent<Image>().sprite = UiArt.Get("Buttons/Button2_Idle");
        var swap = b.spriteState;
        swap.highlightedSprite = swap.selectedSprite = swap.pressedSprite = UiArt.Get("Buttons/Button2_Active");
        b.spriteState = swap;
        b.GetComponentInChildren<Text>().fontSize = Figma.Type.ArvoBody;
        return b;
    }

    // Every setting back to how the game first starts (not kept until applied).
    void Reset()
    {
        if (listening >= 0) return;
        State.Defaults().Restore();
        var at = controlsContent != null ? controlsContent.anchoredPosition : Vector2.zero;
        ShowTab(tab);
        if (controlsContent != null) controlsContent.anchoredPosition = at;
        MainMenu.Select(resetButton);
    }

    void Apply()
    {
        if (listening >= 0) return;
        kept = State.Now();
        PlayerPrefs.Save();
    }

    // Leaves, asking first if there are changes not applied: Yes puts them back as they were kept.
    void Back()
    {
        if (listening >= 0) return;
        if (kept.Same(State.Now()))
        {
            Close();
            return;
        }
        ConfirmDialog.Open(order + 1, "DISCARD CHANGES", "Are you sure?", yes =>
        {
            confirmClosedFrame = Time.frameCount;
            if (yes)
            {
                kept.Restore();
                Close();
            }
            else MainMenu.Select(backButton);
        });
    }

    // Everything Options changes: to tell whether anything's changed since it was kept, and to put it back.
    class State
    {
        public bool fullscreen, dof, captions, vsync, antiAliasing;
        public int quality, shake;
        public float volume, music, sfx;
        public Vector2Int resolution;
        public Key[] keys;

        public static State Now() => new()
        {
            fullscreen = Settings.Fullscreen, dof = Settings.DepthOfField, captions = Settings.ClosedCaptions,
            vsync = Settings.VSync, antiAliasing = Settings.AntiAliasing, quality = (int)Settings.Graphics,
            shake = Settings.Shake, music = Settings.MusicVolume, sfx = Settings.SfxVolume, volume = Settings.Volume,
            resolution = Settings.Resolution, keys = KeyBindings.Current(),
        };

        // How the game first starts (Settings' and KeyBindings' own defaults).
        public static State Defaults() => new()
        {
            fullscreen = true, dof = true, captions = false, vsync = true, antiAliasing = true,
            quality = (int)Settings.Quality.High, shake = 2, music = 1f, sfx = 1f, volume = 1f,
            resolution = Vector2Int.zero, keys = KeyBindings.Defaults(),
        };

        public bool Same(State o) =>
            fullscreen == o.fullscreen && dof == o.dof && captions == o.captions && vsync == o.vsync &&
            antiAliasing == o.antiAliasing && quality == o.quality && shake == o.shake &&
            Mathf.Approximately(music, o.music) && Mathf.Approximately(sfx, o.sfx) && Mathf.Approximately(volume, o.volume) && resolution == o.resolution &&
            System.Linq.Enumerable.SequenceEqual(keys, o.keys);

        // Sets what differs (so the screen's only set again if it changed).
        public void Restore()
        {
            var now = Now();
            if (now.fullscreen != fullscreen) Settings.Fullscreen = fullscreen;
            if (now.resolution != resolution) Settings.Resolution = resolution;
            if (now.dof != dof) Settings.DepthOfField = dof;
            if (now.captions != captions) Settings.ClosedCaptions = captions;
            if (now.quality != quality) Settings.Graphics = (Settings.Quality)quality;
            if (now.shake != shake) Settings.Shake = shake;
            if (!Mathf.Approximately(now.volume, volume)) Settings.Volume = volume;
            if (now.vsync != vsync) Settings.VSync = vsync;
            if (now.antiAliasing != antiAliasing) Settings.AntiAliasing = antiAliasing;
            if (!Mathf.Approximately(now.music, music)) Settings.MusicVolume = music;
            if (!Mathf.Approximately(now.sfx, sfx)) Settings.SfxVolume = sfx;
            if (!System.Linq.Enumerable.SequenceEqual(now.keys, keys)) KeyBindings.SetAll(keys);
        }
    }

    // ---------------------------------------------------------------- game controls (scrolling)

    // The two ready-made schemes (KeyBindings), each as the design draws it: Control A's on the left, B's on the right.
    // Their keycaps: (picture, x, y).
    static readonly (string art, float x, float y)[][] SchemeKeys =
    {
        new[] { ("KeyW", 241f, 480.5f), ("KeyA", 169f, 544.5f), ("KeyS", 241f, 544.5f), ("KeyD", 313f, 544.5f), ("KeyJ", 452f, 520.5f), ("KeyK", 605f, 520.5f) },
        new[] { ("KeyUp", 928f, 480.5f), ("KeyLeft", 856f, 544.5f), ("KeyDown", 928f, 544.5f), ("KeyRight", 1000f, 544.5f), ("KeyZ", 1139f, 520.5f), ("KeyX", 1292f, 520.5f) },
    };
    static readonly float[] CardX = { 401.5f, 1083.5f }, TitleX = { 244.5f, 937f }, NameX = { 236.5f, 928f };
    static readonly float[] MoveX = { 239f, 925f }, InteractX = { 607f, 1293.5f }, PossessX = { 453f, 1139.5f };

    // The custom controls: what each does and the key it's on, a pill per job (left column, then right).
    static readonly (KeyBindings.Bind bind, string what)[] LeftBinds =
    {
        (KeyBindings.Bind.Left, "Left"), (KeyBindings.Bind.Right, "Right"), (KeyBindings.Bind.Up, "Up"), (KeyBindings.Bind.Down, "Down"),
        (KeyBindings.Bind.Possess, "Possess / Leave Body"), (KeyBindings.Bind.Interact, "Interact"),
    };
    static readonly (KeyBindings.Bind bind, string what)[] RightBinds =
    {
        (KeyBindings.Bind.Objectives, "Expand Objective"), (KeyBindings.Bind.Skip, "Skip"), (KeyBindings.Bind.Pause, "Pause"),
    };

    Button firstControl;
    readonly Button[] cards = new Button[2];
    readonly List<Button> pills = new();
    readonly List<KeyBindings.Bind> pillBinds = new();
    RectTransform controlsContent;
    // The pill waiting for its new key (-1: none), and the frames it started and stopped waiting.
    int listening = -1, listenFrame, listenEndFrame = -10;

    void Controls()
    {
        // Everything under the tabs scrolls (the design's page is taller than the screen); the scroll bar on the right.
        var view = MainMenu.Ui("View", page);
        // From just under the tabs (ContentTop, measured from the screen's middle like everything else) to just above
        // the buttons at the bottom.
        view.anchorMin = new Vector2(0f, 0.5f);
        view.anchorMax = new Vector2(1f, 0.5f);
        view.offsetMin = new Vector2(0f, (Figma.Height * 0.5f - 856f) * Figma.K); // ends above Reset / Apply / Back
        view.offsetMax = new Vector2(0f, (Figma.Height * 0.5f - ContentTop) * Figma.K);
        view.gameObject.AddComponent<RectMask2D>();
        var viewImg = view.gameObject.AddComponent<Image>();
        viewImg.color = Color.clear; // catches the scroll wheel

        var content = MainMenu.Ui("Content", view);
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(Figma.Width * Figma.K, (1298f - ContentTop + 40f) * Figma.K);
        controlsContent = content;

        scroll = page.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = view;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        // The two schemes: the one in use on the bright card under the picked title. Keys changed away from both
        // (custom): neither is picked, and both go dark.
        int scheme = KeyBindings.Scheme;
        for (int s = 0; s < 2; s++) SchemeCard(content, s, s == scheme, scheme < 0);

        // Custom controls: what each does, and its key on a pill. Picking a pill waits for the key to put it on.
        var title = AtWords(content, "Custom Controls", 0f, 741.5f, Figma.Type.ArvoSelection, Color.white, FontStyle.Bold);
        LeftIn(title, 124f);
        for (int i = 0; i < LeftBinds.Length; i++)
            AddPill(KeyRow(content, LeftBinds[i].what, KeyBindings.Words(LeftBinds[i].bind), 154f, 640.5f, 795.5f + 70f * i), LeftBinds[i].bind);
        for (int i = 0; i < RightBinds.Length; i++)
            AddPill(KeyRow(content, RightBinds[i].what, KeyBindings.Words(RightBinds[i].bind), 836f, 1261.5f, 796.5f + 70f * i), RightBinds[i].bind);
        int leftCount = LeftBinds.Length;
        for (int i = 0; i < pills.Count; i++)
        {
            bool leftSide = i < leftCount;
            int top = leftSide ? 0 : leftCount, count = leftSide ? leftCount : RightBinds.Length;
            pills[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = i > top ? pills[i - 1] : cards[leftSide ? 0 : 1],
                selectOnDown = i < top + count - 1 ? pills[i + 1] : null,
                selectOnLeft = leftSide ? null : pills[i - leftCount],
                selectOnRight = leftSide && i + leftCount < pills.Count ? pills[i + leftCount] : null,
            };
        }
        for (int s = 0; s < 2; s++)
            cards[s].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = tabButtons[^1],
                selectOnDown = pills[s == 0 ? 0 : leftCount],
                selectOnLeft = s == 1 ? cards[0] : null,
                selectOnRight = s == 0 ? cards[1] : null,
            };
        firstControl = pills[0];

        // The scroll bar (the design's, fixed on the right), driving the list.
        var barRt = MainMenu.Ui("Scroll Bar", page);
        Figma.Place(barRt, 1462f, 589.5f, 56f, 738f);
        var barImg = barRt.gameObject.AddComponent<Image>();
        barImg.sprite = UiArt.Get("Scroll/ScrollBar");
        // The thumb slides along the track as drawn (ScrollBar.png: x 17..55, y 18..1072 of 86 x 1136), so at the top
        // or bottom of the list it sits right at the track's end.
        var area = MainMenu.Ui("Area", barRt);
        area.anchorMin = new Vector2(17f / 86f, (1136f - 1072f) / 1136f);
        area.anchorMax = new Vector2(55f / 86f, (1136f - 18f) / 1136f);
        area.offsetMin = area.offsetMax = Vector2.zero;
        var handle = MainMenu.Ui("Thumb", area);
        handle.offsetMin = handle.offsetMax = Vector2.zero;
        var hit = handle.gameObject.AddComponent<Image>(); // the thumb's own box, see-through, for dragging
        hit.color = Color.clear;
        // Its picture (Scroll.png: the thumb drawn at x 39..78, y 62..269 of 122 x 311) spills out round the box so
        // the drawn thumb fills it exactly.
        var art = MainMenu.Ui("Art", handle);
        art.anchorMin = new Vector2(-39f / 39f, -(311f - 269f) / 207f);
        art.anchorMax = new Vector2(1f + (122f - 78f) / 39f, 1f + 62f / 207f);
        art.offsetMin = art.offsetMax = Vector2.zero;
        var artImg = art.gameObject.AddComponent<Image>();
        artImg.sprite = UiArt.Get("Scroll/Scroll");
        artImg.raycastTarget = false;
        var bar = barRt.gameObject.AddComponent<Scrollbar>();
        bar.direction = Scrollbar.Direction.BottomToTop; // ScrollRect's way round: the thumb at the top shows the top
        bar.handleRect = handle;
        bar.targetGraphic = hit;
        bar.navigation = new Navigation { mode = Navigation.Mode.None };
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
    }

    // One scheme's card: the card and title pictures (bright and "selected" when it's the one in use), its name, its
    // keycaps and what they do. The whole card is one button that switches to it; picked, it grows a touch.
    void SchemeCard(RectTransform content, int s, bool on, bool dark)
    {
        var section = MainMenu.Ui(s == 0 ? "Control A" : "Control B", content);
        MainMenu.Stretch(section);
        // Grows round the card's middle (design y 527.5, between the title's top and the card's bottom).
        section.pivot = new Vector2(CardX[s] / Figma.Width, 1f - (527.5f - ContentTop) / (1298f - ContentTop + 40f));

        if (on) At(section, "Card_active", CardX[s], 553f, 654f, 262f);
        else At(section, "Card_idle", CardX[s], 550f, 665f, 266f);
        if (on) At(section, "Title_Selected", TitleX[s], 407f, 371f, 72.6f);
        else At(section, "Title_Idle", TitleX[s], 408f, 378f, 74f);
        AtWords(section, s == 0 ? "Control A" : "Control B", NameX[s], 406.5f, Figma.Type.ArvoTitle, Ink, FontStyle.Bold);
        foreach (var (key, x, y) in SchemeKeys[s])
            At(section, "KeyboardKey/" + key, x, y, 141f, 141f);
        AtWords(section, "Movement", MoveX[s], 615.5f, Figma.Type.ArvoSubtitle, Ink);
        AtWords(section, "Interact", InteractX[s], 615.5f, Figma.Type.ArvoSubtitle, Ink);
        AtWords(section, "Possess /", PossessX[s], 603f, Figma.Type.ArvoSubtitle, Ink);
        AtWords(section, "Leave Body", PossessX[s], 629.5f, Figma.Type.ArvoSubtitle, Ink);

        if (dark)
            foreach (var g in section.GetComponentsInChildren<Graphic>())
                g.color = new Color(g.color.r * 0.45f, g.color.g * 0.45f, g.color.b * 0.45f, g.color.a);

        // See-through, over the title and the card.
        var hitRt = MainMenu.Ui("Pick", section);
        Figma.Place(hitRt, CardX[s], 527.5f, 670f, 320f);
        Top(hitRt, CardX[s], 527.5f);
        var hit = hitRt.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        hit.raycastTarget = true;
        var b = hit.gameObject.AddComponent<Button>();
        b.targetGraphic = hit;
        b.transition = Selectable.Transition.None;
        int which = s;
        b.onClick.AddListener(() => PickScheme(which));
        hit.gameObject.AddComponent<MainMenu.HoverSelects>();
        hit.gameObject.AddComponent<KeepInView>().menu = this;
        hit.gameObject.AddComponent<Grows>().target = section;
        cards[s] = b;
    }

    class Grows : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        public RectTransform target;
        public void OnSelect(BaseEventData e) => target.localScale = Vector3.one * 1.04f;
        public void OnDeselect(BaseEventData e) => target.localScale = Vector3.one;
    }

    void PickScheme(int s)
    {
        if (listening >= 0) return;
        KeyBindings.UseScheme(s);
        RebuildControls(() => cards[s]);
    }

    void AddPill(Button pill, KeyBindings.Bind bind)
    {
        int i = pills.Count;
        pills.Add(pill);
        pillBinds.Add(bind);
        pill.onClick.AddListener(() => Listen(i));
        pill.gameObject.AddComponent<KeepInView>().menu = this;
    }

    // A pill picked: it waits for the next key pressed, which goes on that job (KeyBindings.Set: if another job had
    // it, the two swap). Esc stops waiting and leaves it as it was, unless it's Pause's own pill.
    void Listen(int i)
    {
        if (listening >= 0 || Time.frameCount <= listenEndFrame + 1) return; // the key just put on isn't a pick too
        listening = i;
        listenFrame = Time.frameCount;
        PillText(pills[i], "PRESS A KEY");
        // The key pressed goes to the pill, not to moving about the menu (or picking or closing).
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
    }

    void Listening()
    {
        if (Time.frameCount == listenFrame) return;
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb != null)
            foreach (var k in kb.allKeys)
            {
                if (k == null || k.keyCode == Key.None || !k.wasPressedThisFrame) continue;
                var bind = pillBinds[listening];
                if (k.keyCode != Key.Escape || bind == KeyBindings.Bind.Pause) KeyBindings.Set(bind, k.keyCode);
                StopListening();
                return;
            }
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) StopListening(); // a click elsewhere: never mind
    }

    void StopListening()
    {
        int i = listening;
        listening = -1;
        listenEndFrame = Time.frameCount;
        RebuildControls(() => pills[i]);
    }

    // The page again after a change (the cards, the dark, the pills' keys), kept where it was scrolled to and on what
    // was picked.
    void RebuildControls(Func<Selectable> pick)
    {
        var at = controlsContent != null ? controlsContent.anchoredPosition : Vector2.zero;
        ShowTab(3);
        controlsContent.anchoredPosition = at;
        MainMenu.Select(pick());
    }

    // In the scrolling content: placed by design px, its y from the page's top (the content starts at ContentTop).
    static Image At(RectTransform content, string art, float x, float y, float w, float h)
    {
        var img = Figma.Picture(content, art, art, x, y, w, h);
        Top(img.rectTransform, x, y);
        return img;
    }

    static Text AtWords(RectTransform content, string words, float x, float y, int size, Color color, FontStyle style = FontStyle.Normal)
    {
        var t = Figma.Words(content, words, x, y, size, color, ParkArt.Font, style);
        Top(t.rectTransform, x, y);
        return t;
    }

    static void Top(RectTransform rt, float x, float y)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2((x - Figma.Width * 0.5f) * Figma.K, -(y - ContentTop) * Figma.K);
    }

    // Left-aligned from x instead of centred.
    static void LeftIn(Text t, float x)
    {
        var rt = t.rectTransform;
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2((x - Figma.Width * 0.5f) * Figma.K, rt.anchoredPosition.y);
        t.alignment = TextAnchor.MiddleLeft;
    }

    Button KeyRow(RectTransform content, string what, string key, float labelX, float pillX, float y)
    {
        LeftIn(AtWords(content, what, 0f, y, Figma.Type.ArvoSubtitle, Ink), labelX);
        var pill = MainMenu.Pill(content, key, null);
        var rt = (RectTransform)pill.transform;
        Figma.Place(rt, pillX, y + 0.5f, 174f, 47f);
        Top(rt, pillX, y + 0.5f);
        PillText(pill, key);
        return pill;
    }

    // A key's name on its pill: a long one ("RIGHT ARROW") a size smaller so it fits.
    static void PillText(Button pill, string words)
    {
        var t = pill.GetComponentInChildren<Text>();
        t.text = words;
        t.fontSize = words.Length > 7 ? Figma.Type.ArvoSubtitle2 : Figma.Type.ArvoBody;
    }

    // A picked control scrolled into view.
    class KeepInView : MonoBehaviour, ISelectHandler
    {
        public SettingsMenu menu;
        public void OnSelect(BaseEventData e)
        {
            var s = menu.scroll;
            if (s == null) return;
            var view = s.viewport;
            var me = (RectTransform)transform;
            Vector3[] c = new Vector3[4], v = new Vector3[4];
            me.GetWorldCorners(c);
            view.GetWorldCorners(v);
            float shift = 0f;
            if (c[0].y < v[0].y) shift = v[0].y - c[0].y + 20f;      // below: move the list up
            else if (c[1].y > v[1].y) shift = v[1].y - c[1].y - 20f; // above: down
            if (shift != 0f) s.content.anchoredPosition += new Vector2(0f, shift / s.content.lossyScale.y);
        }
    }

    // ---------------------------------------------------------------- every frame

    GameObject lastPicked;

    void Update()
    {
        var es = EventSystem.current;
        if (listening >= 0)
        {
            Listening();
            return;
        }
        if (es != null && !es.sendNavigationEvents && Time.frameCount > listenEndFrame) es.sendNavigationEvents = true;
        if (ConfirmDialog.IsOpen || Time.frameCount == confirmClosedFrame) return;
        var picked = es != null ? es.currentSelectedGameObject : null;
        if (picked == null && es != null)
        {
            picked = rows.Count > 0 ? rows[0].button.gameObject : firstControl != null ? firstControl.gameObject : null;
            es.SetSelectedGameObject(picked); // clicked on nothing
        }
        if (picked != lastPicked)
        {
            lastPicked = picked;
            Refresh();
        }
        if (Time.frameCount != openedFrame && Time.frameCount > listenEndFrame && global::Controls.Back)
        {
            AudioManager.TryPlay(e => e.backButton); // S-06, as if Back was clicked
            Back();
        }
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
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = true;
    }
}
