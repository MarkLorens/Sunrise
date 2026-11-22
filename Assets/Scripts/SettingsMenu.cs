using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Options, from the main menu or the pause menu, laid out off the artist's four design pages (Figma: Options -
// General / Display / Audio / Game Controls): "OPTIONS" over the page background (UI Asset/PageBG), four tabs
// (Tab_Active for the page showing, Tab_Idle for the others), and the page's rows under them.
// A row is picked by the mouse or up/down (its words go bold and yellow, its arrows/slider yellow too); left/right
// changes it. Up from the first row reaches the tabs, where left/right turns the page; clicking a tab does too. Esc
// closes it.
// ponytail: only the settings the game already has do anything (window mode, graphics, screen shake, resolution,
// depth of field, master volume: Settings). Language, closed captions, vertical sync, anti-aliasing, music and sound
// volume and the controls are the design's pictures for now.
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

    // The design's own values for what the game doesn't have yet: shown and changeable, but they do nothing.
    static bool captions = true, vsync, antiAliasing;
    static int music = 80, sfx = 30;

    Action onClose;
    RectTransform canvas, page;
    int tab;
    readonly List<Button> tabButtons = new();
    readonly List<Row> rows = new();
    ScrollRect scroll;
    int openedFrame;

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

        // Idle tabs first, the open one last so it overlaps them.
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < 4; i++)
            {
                bool on = i == t;
                if (on != (pass == 1)) continue;
                float x = TabX[t][i];
                var art = Figma.Picture(page, "Tab " + TabNames[i], on ? "Tab_Active" : "Tab_Idle",
                    on ? x + 2f : x, on ? 250.3f : 222.2f, on ? 243.5f : 197f, on ? 159f : 105.5f);
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
                    float y = lines.Length == 1 ? (on ? 257.5f : 221f) : on ? 218f + 45f * l : 201f + 30f * l;
                    Figma.Words(page, lines[l], x, y, on ? Figma.Type.AtmaH1 : Figma.Type.ArvoSubtitle, Yellow, on ? ParkArt.TitleFont : ParkArt.Font);
                }
            }

        switch (t)
        {
            case 0: General(); break;
            case 1: Display(); break;
            case 2: Audio(); break;
            default: firstControl = null; Controls(); break;
        }
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
        }
    }

    // ---------------------------------------------------------------- the pages

    void General()
    {
        ArrowRow(0, 146f, "Language", 650f, 862.5f, 756f, () => "English", _ => { });
        ArrowRow(1, 146f, "Closed Caption", 650f, 862.5f, 756f, () => captions ? "ON" : "OFF", _ => captions = !captions);
    }

    void Display()
    {
        var resolutions = Settings.Resolutions();
        PillRow(0, "Window Mode", new[] { "Windowed", "Fullscreen" }, () => Settings.Fullscreen ? 1 : 0, i => Settings.Fullscreen = i == 1);
        PillRow(1, "Graphic", new[] { "Low", "Medium", "High" }, () => (int)Settings.Graphics, i => Settings.Graphics = (Settings.Quality)i);
        PillRow(2, "Screen Shake", new[] { "Full", "Half", "Off" }, () => 2 - Settings.Shake, i => Settings.Shake = 2 - i);
        ArrowRow(3, 152f, "Resolution", 574f, 786.4f, 679.5f, () => $"{Settings.CurrentResolution.x} x {Settings.CurrentResolution.y}",
            d => Settings.Resolution = resolutions[Wrap(Array.IndexOf(resolutions, Settings.CurrentResolution) + d, resolutions.Length)]);
        ArrowRow(4, 152f, "Depth of field", 574f, 786.4f, 679.5f, () => Settings.DepthOfField ? "ON" : "OFF", _ => Settings.DepthOfField = !Settings.DepthOfField);
        ArrowRow(5, 152f, "Vertical Sync", 574f, 786.4f, 679.5f, () => vsync ? "ON" : "OFF", _ => vsync = !vsync);
        ArrowRow(6, 152f, "Anti-Aliasing", 574f, 786.4f, 679.5f, () => antiAliasing ? "ON" : "OFF", _ => antiAliasing = !antiAliasing);
    }

    void Audio()
    {
        SliderRow(0, "Master Volume", () => Mathf.Round(Settings.Volume * 100f), d => Settings.Volume = Mathf.Clamp01(Mathf.Round(Settings.Volume * 10f + d) / 10f));
        SliderRow(1, "Background Music", () => music, d => music = Mathf.Clamp(music + d * 10, 0, 100));
        SliderRow(2, "SFX", () => sfx, d => sfx = Mathf.Clamp(sfx + d * 10, 0, 100));
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
        r.step = step;
    }

    // The pills, left to right, as the design spaces them; the one that's set is the yellow one.
    static readonly float[] PillX = { 676f, 985f, 1290.5f };

    void PillRow(int index, string name, string[] options, Func<int> current, Action<int> set)
    {
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
            Figma.Words(page, options[i], PillX[i] + 6f, y - 4.5f, Figma.Type.ArvoBody, Ink, ParkArt.Font);
        }
        r.options = options;
        r.current = current;
        r.step = d => set(Mathf.Clamp(current() + d, 0, options.Length - 1));
    }

    void SliderRow(int index, string name, Func<float> amount, Action<int> step)
    {
        var r = NewRow(index, 146f, name);
        float y = RowY + RowStep * index;
        r.bar = Figma.Picture(page, "Bar", "Volume/VolumeBar_Idle", 891.5f, y, 422f, 46.9f);
        r.knob = Figma.Picture(page, "Knob", "Volume/VolumeSelecter_Idle", 0f, y, 24.7f, 54f);
        r.value = Figma.Words(page, "", 1174f, y, Figma.Type.ArvoSubtitle, Ink, ParkArt.Font);
        r.amount = amount;
        r.step = step;
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
                selectOnDown = i < rows.Count - 1 ? rows[i + 1].button : null,
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

    // ---------------------------------------------------------------- game controls (pictures only, scrolling)

    void Controls()
    {
        // Everything under the tabs scrolls (the design's page is taller than the screen); the scroll bar on the right.
        var view = MainMenu.Ui("View", page);
        // From just under the tabs (ContentTop, measured from the screen's middle like everything else) to the bottom.
        view.anchorMin = Vector2.zero;
        view.anchorMax = new Vector2(1f, 0.5f);
        view.offsetMin = Vector2.zero;
        view.offsetMax = new Vector2(0f, (Figma.Height * 0.5f - ContentTop) * Figma.K);
        view.gameObject.AddComponent<RectMask2D>();
        var viewImg = view.gameObject.AddComponent<Image>();
        viewImg.color = Color.clear; // catches the scroll wheel

        var content = MainMenu.Ui("Content", view);
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(Figma.Width * Figma.K, (1298f - ContentTop + 40f) * Figma.K);

        scroll = page.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = view;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        // The two schemes.
        At(content, "Card_active", 401.5f, 553f, 654f, 262f);
        At(content, "Card_idle", 1083.5f, 550f, 665f, 266f);
        At(content, "Title_Selected", 244.5f, 407f, 371f, 72.6f);
        At(content, "Title_Idle", 937f, 408f, 378f, 74f);
        AtWords(content, "Control A", 236.5f, 406.5f, Figma.Type.ArvoTitle, Ink, FontStyle.Bold);
        AtWords(content, "Control B", 928f, 406.5f, Figma.Type.ArvoTitle, Ink, FontStyle.Bold);
        foreach (var (key, x, y) in new[] { ("KeyW", 241f, 480.5f), ("KeyA", 169f, 544.5f), ("KeyS", 241f, 544.5f), ("KeyD", 313f, 544.5f),
                     ("KeyJ", 452f, 520.5f), ("KeyK", 605f, 520.5f), ("KeyUp", 928f, 480.5f), ("KeyLeft", 856f, 544.5f), ("KeyDown", 928f, 544.5f),
                     ("KeyRight", 1000f, 544.5f), ("KeyZ", 1139f, 520.5f), ("KeyX", 1292f, 520.5f) })
            At(content, "KeyboardKey/" + key, x, y, 141f, 141f);
        foreach (var (words, x) in new[] { ("Movement", 239f), ("Interact", 607f), ("Movement", 925f), ("Interact", 1293.5f) })
            AtWords(content, words, x, 615.5f, Figma.Type.ArvoSubtitle, Ink);
        foreach (var x in new[] { 453f, 1139.5f })
        {
            AtWords(content, "Possess /", x, 603f, Figma.Type.ArvoSubtitle, Ink);
            AtWords(content, "Leave Body", x, 629.5f, Figma.Type.ArvoSubtitle, Ink);
        }

        // Custom controls: what each does, and its key on a pill (the first one picked, as in the design).
        var title = AtWords(content, "Custom Controls", 0f, 741.5f, Figma.Type.ArvoSelection, Color.white, FontStyle.Bold);
        LeftIn(title, 124f);
        string[,] leftCol = { { "Left", "A" }, { "Right", "D" }, { "Up", "W" }, { "Down", "S" }, { "Possess / Leave Body", "J" }, { "Interact", "K" } };
        string[,] rightCol = { { "Expand Objective", "TAB" }, { "Skip", "SPACE" }, { "Pause", "ESC" } };
        var pills = new List<Button>();
        for (int i = 0; i < 6; i++) pills.Add(KeyRow(content, leftCol[i, 0], leftCol[i, 1], 154f, 640.5f, 795.5f + 70f * i));
        for (int i = 0; i < 3; i++) pills.Add(KeyRow(content, rightCol[i, 0], rightCol[i, 1], 836f, 1261.5f, 796.5f + 70f * i));
        for (int i = 0; i < pills.Count; i++)
        {
            bool leftSide = i < 6;
            int top = leftSide ? 0 : 6, count = leftSide ? 6 : 3;
            pills[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = i > top ? pills[i - 1] : tabButtons[^1],
                selectOnDown = i < top + count - 1 ? pills[i + 1] : null,
                selectOnLeft = leftSide ? null : pills[i - 6],
                selectOnRight = leftSide && i + 6 < pills.Count ? pills[i + 6] : null,
            };
            pills[i].gameObject.AddComponent<KeepInView>().menu = this;
        }
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

    Button firstControl;

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
        pill.GetComponentInChildren<Text>().fontSize = Figma.Type.ArvoBody;
        return pill;
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
        if (Time.frameCount != openedFrame && global::Controls.Back) Close();
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
}
