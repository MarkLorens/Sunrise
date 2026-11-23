#if UNITY_EDITOR
using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Editor-only panel for tuning night lighting while the game is running.
// F1 shows or hides it. Sliders write NightLighting's public fields; that component
// reapplies them every frame, so the park updates without a restart.
//
// A canvas, not an IMGUI window. Play mode here uses the new Input System only, and
// OnGUI never hears those mouse events, so IMGUI sliders draw but will not move.
// The clickable menus (MainMenu.Screen) are canvases driven by InputSystemUIInputModule.
// Each group is its own method so a later section (fog, grade, stalls, the wheel)
// is another build call, not a rewrite.
// The whole file is stripped from player builds. It lives next to the other gameplay
// scripts (not under Editor/) so GameHud can add it at runtime.
public class LightingDebugMenu : MonoBehaviour
{
    const float RowHeight = 48f;

    bool open;
    GameObject root;
    GameObject panel;
    GameObject hint;
    GameObject wheelRoot;
    RectTransform wheelDisc;
    RectTransform wheelMarker;
    Text wheelTitle;
    BoundColor wheelTarget;
    Text copyLabel;
    float copiedUntil;
    RectTransform list;
    static Sprite white;
    static Sprite wheelSprite;

    void Start() => Build();

    void Update()
    {
        if (copyLabel != null && copiedUntil > 0f && Time.unscaledTime >= copiedUntil)
        {
            copyLabel.text = "Copy values";
            copiedUntil = 0f;
        }
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.f1Key.wasPressedThisFrame || panel == null) return;
        open = !open;
        panel.SetActive(open);
        hint.SetActive(!open);
        if (!open && wheelRoot != null) wheelRoot.SetActive(false);
    }

    void LateUpdate()
    {
        if (wheelRoot != null && wheelRoot.activeSelf) PlaceMarker();
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root);
    }

    void Build()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        else if (FindAnyObjectByType<InputSystemUIInputModule>() == null && EventSystem.current != null)
            EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();

        var canvasGo = new GameObject("Lighting Debug", typeof(RectTransform));
        root = canvasGo;
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40; // above the HUD (10), the tutorial (15) and the pause menu (20)
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var hintLabel = Label(canvasGo.transform, "F1  lighting", 22, TextAnchor.LowerLeft);
        hint = hintLabel.gameObject;
        var hintRt = (RectTransform)hint.transform;
        hintRt.anchorMin = hintRt.anchorMax = new Vector2(0f, 0f);
        hintRt.pivot = new Vector2(0f, 0f);
        hintRt.anchoredPosition = new Vector2(16f, 12f);
        hintRt.sizeDelta = new Vector2(280f, 36f);

        panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(canvasGo.transform, false);
        var panelRt = (RectTransform)panel.transform;
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = new Vector2(1f / 3f, 1f);
        panelRt.offsetMin = panelRt.offsetMax = Vector2.zero;
        var bg = panel.AddComponent<Image>();
        bg.sprite = White();
        bg.color = new Color(0.1f, 0.07f, 0.12f, 0.94f);

        var column = panel.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(22, 22, 18, 18);
        column.spacing = 8f;
        column.childAlignment = TextAnchor.UpperLeft;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;

        var title = Label(panel.transform, "Night lighting", 34, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 46f;
        Label(panel.transform, "F1 hides this", 20, TextAnchor.MiddleLeft)
            .gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;

        var scrollGo = new GameObject("Scroll", typeof(RectTransform));
        scrollGo.transform.SetParent(panel.transform, false);
        var scrollLe = scrollGo.AddComponent<LayoutElement>();
        scrollLe.flexibleHeight = 1f;
        scrollLe.minHeight = 80f;
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 36f;

        var viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(scrollGo.transform, false);
        Stretch((RectTransform)viewport.transform);
        var hit = viewport.AddComponent<Image>();
        hit.sprite = White();
        hit.color = new Color(0f, 0f, 0f, 0.01f); // a target for the wheel over the gaps between rows
        viewport.AddComponent<RectMask2D>();

        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewport.transform, false);
        list = (RectTransform)contentGo.transform;
        list.anchorMin = new Vector2(0f, 1f);
        list.anchorMax = new Vector2(1f, 1f);
        list.pivot = new Vector2(0.5f, 1f);
        list.anchoredPosition = Vector2.zero;
        list.sizeDelta = Vector2.zero;
        var rows = contentGo.AddComponent<VerticalLayoutGroup>();
        rows.spacing = 4f;
        rows.childAlignment = TextAnchor.UpperLeft;
        rows.childControlWidth = rows.childControlHeight = true;
        rows.childForceExpandWidth = true;
        rows.childForceExpandHeight = false;
        var fit = contentGo.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = (RectTransform)viewport.transform;
        scroll.content = list;

        var night = NightLighting.Instance;
        if (night == null)
            Label(list, "Night lighting is not in this scene.", 22, TextAnchor.MiddleLeft);
        else
        {
            BuildMoon(night);
            BuildGlow(night);
            BuildLamps(night);
        }

        copyLabel = MakeButton(panel.transform, "Copy values", () =>
        {
            if (NightLighting.Instance == null) return;
            // On the clipboard, so it can be pasted into chat and written back into NightLighting later.
            GUIUtility.systemCopyBuffer = Values(NightLighting.Instance);
            copyLabel.text = "Copied";
            copiedUntil = Time.unscaledTime + 1.5f;
        });

        BuildWheel(canvasGo.transform);
        panel.SetActive(false);
    }

    void BuildMoon(NightLighting night)
    {
        Heading("Moon");
        ColorBlock("Color", () => night.moonColor, c => night.moonColor = c);
        SliderRow("Intensity", night.moonIntensity, 0f, 3f, v => night.moonIntensity = v);
    }

    void BuildGlow(NightLighting night)
    {
        Gap(10f);
        Heading("Glow");
        SliderRow("Softness", night.softness, 0f, 1f, v => night.softness = v);
    }

    void BuildLamps(NightLighting night)
    {
        Gap(10f);
        Heading("Lamp posts");
        var colors = night.lampColors;
        if (colors != null)
            for (int i = 0; i < colors.Length; i++)
            {
                int index = i;
                ColorBlock("Color " + (index + 1), () => night.lampColors[index], c => night.lampColors[index] = c);
            }

        SliderRow("Intensity", night.lampIntensity, 0f, 8f, v => night.lampIntensity = v);
        SliderRow("Range", night.lampRange, 0f, 20f, v => night.lampRange = v);
        SliderRow("Core size", night.lampCoreSize, 0f, 4f, v => night.lampCoreSize = v);
        SliderRow("Core alpha", night.lampCoreAlpha, 0f, 1f, v => night.lampCoreAlpha = v);
        SliderRow("Halo size", night.lampHaloSize, 0f, 12f, v => night.lampHaloSize = v);
        SliderRow("Halo alpha", night.lampHaloAlpha, 0f, 1f, v => night.lampHaloAlpha = v);
        SliderRow("Pool intensity", night.poolIntensity, 0f, 6f, v => night.poolIntensity = v);
        SliderRow("Pool range", night.poolRange, 0f, 30f, v => night.poolRange = v);
        SliderRow("Pool height", night.poolHeight, 0f, 8f, v => night.poolHeight = v);
        SliderRow("Flicker", night.lampFlicker, 0f, 1f, v => night.lampFlicker = v);
        SliderRow("Flicker speed", night.lampFlickerSpeed, 0f, 10f, v => night.lampFlickerSpeed = v);
    }

    void Heading(string text)
    {
        var t = Label(list, text, 26, TextAnchor.MiddleLeft);
        t.fontStyle = FontStyle.Bold;
        t.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;
    }

    void Gap(float height)
    {
        var go = new GameObject("Gap", typeof(RectTransform));
        go.transform.SetParent(list, false);
        go.AddComponent<LayoutElement>().preferredHeight = height;
    }

    void ColorBlock(string label, Func<Color> get, Action<Color> set)
    {
        var bound = new BoundColor { name = label, get = get, set = set };
        var row = Row(list, 44f);
        var hit = row.gameObject.AddComponent<Image>();
        hit.sprite = White();
        hit.color = new Color(1f, 1f, 1f, 0.08f);
        var button = row.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => ShowWheel(bound));

        var title = Label(row, label, 24, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var swatchGo = new GameObject("Swatch", typeof(RectTransform));
        swatchGo.transform.SetParent(row, false);
        var swatch = swatchGo.AddComponent<Image>();
        swatch.sprite = White();
        swatch.raycastTarget = false;
        var current = get();
        swatch.color = new Color(current.r, current.g, current.b, 1f);
        var swatchLe = swatchGo.AddComponent<LayoutElement>();
        swatchLe.preferredWidth = 56f;
        swatchLe.preferredHeight = 28f;
        bound.swatch = swatch;

        void Channel(string name, Func<Color, float> read, Func<Color, float, Color> write, Action<Slider, Text> keep)
        {
            var (slider, readout) = SliderRow(name, read(get()), 0f, 1f, v =>
            {
                var next = write(get(), v);
                set(next);
                swatch.color = new Color(next.r, next.g, next.b, 1f);
            });
            keep(slider, readout);
        }
        Channel("Red", c => c.r, (c, v) => { c.r = v; return c; }, (s, t) => { bound.red = s; bound.redText = t; });
        Channel("Green", c => c.g, (c, v) => { c.g = v; return c; }, (s, t) => { bound.green = s; bound.greenText = t; });
        Channel("Blue", c => c.b, (c, v) => { c.b = v; return c; }, (s, t) => { bound.blue = s; bound.blueText = t; });
    }

    (Slider slider, Text readout) SliderRow(string name, float value, float min, float max, Action<float> set)
    {
        var row = Row(list, RowHeight);
        var nameText = Label(row, name, 22, TextAnchor.MiddleLeft);
        var nameLe = nameText.gameObject.AddComponent<LayoutElement>();
        nameLe.preferredWidth = 190f;
        nameLe.flexibleWidth = 0f;

        var slider = MakeSlider(row, value, min, max);
        var sliderLe = slider.gameObject.AddComponent<LayoutElement>();
        sliderLe.flexibleWidth = 1f;
        sliderLe.minWidth = 80f;

        var readout = Label(row, Format(value), 22, TextAnchor.MiddleRight);
        readout.gameObject.AddComponent<LayoutElement>().preferredWidth = 72f;
        slider.onValueChanged.AddListener(v =>
        {
            set(v);
            readout.text = Format(v);
        });
        return (slider, readout);
    }

    // Sits just to the right of the panel. Clicking a color row opens it for that color.
    void BuildWheel(Transform canvas)
    {
        wheelRoot = new GameObject("Color Wheel", typeof(RectTransform));
        wheelRoot.transform.SetParent(canvas, false);
        var rt = (RectTransform)wheelRoot.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f / 3f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(16f, 0f);
        rt.sizeDelta = new Vector2(320f, 400f);
        var bg = wheelRoot.AddComponent<Image>();
        bg.sprite = White();
        bg.color = new Color(0.1f, 0.07f, 0.12f, 0.94f);
        var column = wheelRoot.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(18, 18, 16, 16);
        column.spacing = 10f;
        column.childAlignment = TextAnchor.UpperCenter;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = column.childForceExpandHeight = false;

        wheelTitle = Label(wheelRoot.transform, "Color", 26, TextAnchor.MiddleCenter);
        wheelTitle.fontStyle = FontStyle.Bold;
        wheelTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;

        var discGo = new GameObject("Disc", typeof(RectTransform));
        discGo.transform.SetParent(wheelRoot.transform, false);
        var discLe = discGo.AddComponent<LayoutElement>();
        discLe.preferredWidth = discLe.preferredHeight = 276f;
        wheelDisc = (RectTransform)discGo.transform;
        var disc = discGo.AddComponent<Image>();
        disc.sprite = WheelSprite();
        disc.preserveAspect = true;
        var input = discGo.AddComponent<WheelInput>();
        input.menu = this;

        var marker = new GameObject("Marker", typeof(RectTransform));
        marker.transform.SetParent(wheelDisc, false);
        wheelMarker = (RectTransform)marker.transform;
        wheelMarker.anchorMin = wheelMarker.anchorMax = new Vector2(0.5f, 0.5f);
        wheelMarker.sizeDelta = new Vector2(22f, 22f);
        var ring = marker.AddComponent<Image>();
        ring.sprite = White();
        ring.color = new Color(0.05f, 0.04f, 0.06f);
        ring.raycastTarget = false;
        var dotGo = new GameObject("Dot", typeof(RectTransform));
        dotGo.transform.SetParent(marker.transform, false);
        Stretch((RectTransform)dotGo.transform);
        ((RectTransform)dotGo.transform).offsetMin = new Vector2(4f, 4f);
        ((RectTransform)dotGo.transform).offsetMax = new Vector2(-4f, -4f);
        var dot = dotGo.AddComponent<Image>();
        dot.sprite = White();
        dot.color = Color.white;
        dot.raycastTarget = false;

        var note = Label(wheelRoot.transform, "Drag the wheel. The sliders follow.", 18, TextAnchor.MiddleCenter);
        note.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;
        wheelRoot.SetActive(false);
    }

    void ShowWheel(BoundColor bound)
    {
        if (wheelTarget == bound && wheelRoot.activeSelf)
        {
            wheelRoot.SetActive(false);
            wheelTarget = null;
            return;
        }
        wheelTarget = bound;
        wheelTitle.text = bound.name;
        wheelRoot.SetActive(true);
        PlaceMarker();
    }

    // Hue around the disc, saturation out from the middle. Brightness stays whatever the
    // sliders already had, except pure black, which becomes a visible color so the drag shows.
    internal void PickWheel(PointerEventData eventData)
    {
        if (wheelTarget == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                wheelDisc, eventData.position, eventData.pressEventCamera, out var local))
            return;
        float radius = wheelDisc.rect.width * 0.5f;
        if (radius < 1f) return;
        float x = local.x / radius;
        float y = local.y / radius;
        float sat = Mathf.Sqrt(x * x + y * y);
        if (sat > 1f)
        {
            x /= sat;
            y /= sat;
            sat = 1f;
        }
        float hue = Mathf.Atan2(y, x) / (Mathf.PI * 2f);
        if (hue < 0f) hue += 1f;

        var prev = wheelTarget.get();
        Color.RGBToHSV(prev, out _, out _, out float value);
        if (value < 0.02f) value = 1f;
        var next = Color.HSVToRGB(hue, sat, value);
        next.a = prev.a;
        wheelTarget.set(next);
        wheelTarget.swatch.color = new Color(next.r, next.g, next.b, 1f);
        SetChannel(wheelTarget.red, wheelTarget.redText, next.r);
        SetChannel(wheelTarget.green, wheelTarget.greenText, next.g);
        SetChannel(wheelTarget.blue, wheelTarget.blueText, next.b);
    }

    static void SetChannel(Slider slider, Text readout, float value)
    {
        slider.SetValueWithoutNotify(value);
        readout.text = Format(value);
    }

    void PlaceMarker()
    {
        if (wheelTarget == null || wheelDisc == null) return;
        Color.RGBToHSV(wheelTarget.get(), out float hue, out float sat, out _);
        float radius = wheelDisc.rect.width * 0.5f * sat;
        float ang = hue * Mathf.PI * 2f;
        wheelMarker.anchoredPosition = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
    }

    static Sprite WheelSprite()
    {
        if (wheelSprite != null) return wheelSprite;
        const int n = 256;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            name = "Lighting Color Wheel",
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
        };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f;
                float dy = (y + 0.5f) / n * 2f - 1f;
                float sat = Mathf.Sqrt(dx * dx + dy * dy);
                if (sat > 1f)
                {
                    px[y * n + x] = new Color32(0, 0, 0, 0);
                    continue;
                }
                float hue = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f);
                if (hue < 0f) hue += 1f;
                var c = Color.HSVToRGB(hue, sat, 1f);
                px[y * n + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), 255);
            }
        tex.SetPixels32(px);
        tex.Apply();
        wheelSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        wheelSprite.hideFlags = HideFlags.HideAndDontSave;
        return wheelSprite;
    }

    class BoundColor
    {
        public string name;
        public Func<Color> get;
        public Action<Color> set;
        public Image swatch;
        public Slider red, green, blue;
        public Text redText, greenText, blueText;
    }

    class WheelInput : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public LightingDebugMenu menu;
        public void OnPointerDown(PointerEventData eventData) => menu.PickWheel(eventData);
        public void OnDrag(PointerEventData eventData) => menu.PickWheel(eventData);
    }

    static RectTransform Row(Transform parent, float height)
    {
        var go = new GameObject("Row", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<LayoutElement>().preferredHeight = height;
        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(0, 0, 6, 6);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        return (RectTransform)go.transform;
    }

    // Track, fill and handle. Navigation is off so a selected slider does not eat WASD or the arrows.
    static Slider MakeSlider(Transform parent, float value, float min, float max)
    {
        var go = new GameObject("Slider", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var track = go.AddComponent<Image>();
        track.sprite = White();
        track.color = new Color(1f, 1f, 1f, 0.18f);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        Stretch((RectTransform)fillArea.transform);
        ((RectTransform)fillArea.transform).offsetMin = new Vector2(6f, 6f);
        ((RectTransform)fillArea.transform).offsetMax = new Vector2(-6f, -6f);
        var fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(fillArea.transform, false);
        Stretch((RectTransform)fill.transform);
        var fillImage = fill.AddComponent<Image>();
        fillImage.sprite = White();
        fillImage.color = new Color(1f, 0.72f, 0.42f);
        fillImage.raycastTarget = false;

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        Stretch((RectTransform)handleArea.transform);
        ((RectTransform)handleArea.transform).offsetMin = new Vector2(6f, 0f);
        ((RectTransform)handleArea.transform).offsetMax = new Vector2(-6f, 0f);
        var handle = new GameObject("Handle", typeof(RectTransform));
        handle.transform.SetParent(handleArea.transform, false);
        var handleRt = (RectTransform)handle.transform;
        handleRt.anchorMin = new Vector2(0f, 0f);
        handleRt.anchorMax = new Vector2(0f, 1f);
        handleRt.sizeDelta = new Vector2(16f, 0f);
        var handleImage = handle.AddComponent<Image>();
        handleImage.sprite = White();
        handleImage.color = new Color(0.98f, 0.95f, 0.9f);

        var slider = go.AddComponent<Slider>();
        slider.fillRect = (RectTransform)fill.transform;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        return slider;
    }

    static Text MakeButton(Transform parent, string label, Action onClick)
    {
        var go = new GameObject(label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<LayoutElement>().preferredHeight = 52f;
        var image = go.AddComponent<Image>();
        image.sprite = White();
        image.color = new Color(0.95f, 0.78f, 0.45f);
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => onClick());
        var text = Label(go.transform, label, 24, TextAnchor.MiddleCenter);
        text.color = new Color(0.12f, 0.08f, 0.1f);
        Stretch((RectTransform)text.transform);
        return text;
    }

    static Text Label(Transform parent, string text, int size, TextAnchor align)
    {
        var go = new GameObject(text, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = ParkArt.Font != null ? ParkArt.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.color = new Color(0.96f, 0.92f, 0.88f);
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Sprite White()
    {
        if (white != null) return white;
        var tex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        white.hideFlags = HideFlags.HideAndDontSave;
        return white;
    }

    static string Format(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    // Field assignments in the same shape as NightLighting. Copied to the clipboard so they can be
    // pasted back and written into the script after Play mode throws the runtime object away.
    static string Values(NightLighting night)
    {
        var s = new StringBuilder();
        s.AppendLine("Night lighting values. Paste these into NightLighting as the new defaults.");
        s.Append("moonColor = ").Append(Col(night.moonColor)).AppendLine(";");
        Line(s, "moonIntensity", night.moonIntensity);
        Line(s, "softness", night.softness);
        s.Append("lampColors = new[] { ");
        var colors = night.lampColors;
        if (colors != null)
            for (int i = 0; i < colors.Length; i++)
            {
                if (i > 0) s.Append(", ");
                s.Append(Col(colors[i]));
            }
        s.AppendLine(" };");
        Line(s, "lampIntensity", night.lampIntensity);
        Line(s, "lampRange", night.lampRange);
        Line(s, "lampCoreSize", night.lampCoreSize);
        Line(s, "lampCoreAlpha", night.lampCoreAlpha);
        Line(s, "lampHaloSize", night.lampHaloSize);
        Line(s, "lampHaloAlpha", night.lampHaloAlpha);
        Line(s, "poolIntensity", night.poolIntensity);
        Line(s, "poolRange", night.poolRange);
        Line(s, "poolHeight", night.poolHeight);
        Line(s, "lampFlicker", night.lampFlicker);
        Line(s, "lampFlickerSpeed", night.lampFlickerSpeed);
        return s.ToString();
    }

    static void Line(StringBuilder s, string name, float value) =>
        s.Append(name).Append(" = ").Append(Num(value)).AppendLine("f;");

    static string Col(Color c)
    {
        var culture = CultureInfo.InvariantCulture;
        if (c.a >= 0.999f)
            return string.Format(culture, "new({0}f, {1}f, {2}f)", Num(c.r), Num(c.g), Num(c.b));
        return string.Format(culture, "new({0}f, {1}f, {2}f, {3}f)", Num(c.r), Num(c.g), Num(c.b), Num(c.a));
    }

    static string Num(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
#endif
