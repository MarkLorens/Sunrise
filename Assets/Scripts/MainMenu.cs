using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The main menu, the first scene in Build Settings, laid out off the artist's design (Figma: Main Menu): the Woohoo Land
// logo (UI Asset/Logo/Wohooland_logo) on the dark page and a column of words under it, the one picked in bold on the
// yellow brush stroke (UI Asset/Buttons/Button3_Active). Mouse, keyboard (arrows + Enter) and gamepad all work.
// New Game starts the park (the next scene in Build Settings) in a new save slot (there's no limit), through the
// loading screen (LoadingScreen); Load Game picks a slot (LoadGameMenu); Options opens the settings (SettingsMenu);
// Credits, the credits (CreditsMenu).
// The static builders below are shared with the pause menu (PauseMenu), the settings (SettingsMenu) and Load Game.
// Titles are in Atma (ParkArt.TitleFont), everything else in Arvo (ParkArt.Font).
public class MainMenu : MonoBehaviour
{
    public static readonly Color Paper = new Color32(14, 16, 24, 255); // the design's near-black page
    public static readonly Color Night = new(0.13f, 0.145f, 0.22f); // Load Game's and the settings' dark blue
    public static readonly Color Ink = Color.black; // as in the design
    public static readonly Color Soft = Color.white;
    // Over the blurred park: nearly dark, like the design. The project blends in linear colour, which lightens see-through
    // UI a lot compared with the design tool, hence so close to opaque.
    public static readonly Color PauseShade = new(0.025f, 0.022f, 0.045f, 0.985f);

    Button loadButton, optionsButton, creditsButton;

    void Awake()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Paper;
        }
        var canvas = Screen("Main Menu", 0);
        // The logo (its picture's see-through margin and all), and the column of words, both as placed in the design.
        // 80% of the design's size, its bottom edge where the design has it (just over the words).
        Figma.Picture(canvas, "Logo", "Logo/Wohooland_logo", 778f, 378.2f, 921.3f, 754.4f);

        var column = Column(canvas);
        column.anchoredPosition = new Vector2(0f, (Figma.Height * 0.5f - 715.2f) * Figma.K);
        var first = Button(column, "New Game", NewGame, sound: e => e.newOrLoadGame);
        loadButton = Button(column, "Load Game", OpenLoad);
        loadButton.interactable = SaveGame.Exists; // greyed out with no save yet
        optionsButton = Button(column, "Options", OpenOptions);
        creditsButton = Button(column, "Credits", OpenCredits);
        Button(column, "Exit", Quit);
        Select(first);
        if (AudioManager.instance != null && FMODEvents.instance != null)
            AudioManager.instance.PlayMusic(FMODEvents.instance.lobbyMusic); // G-01
    }

    // Leaving for the park (or quitting): the lobby music fades out.
    void OnDestroy()
    {
        if (AudioManager.instance != null) AudioManager.instance.StopMusic();
    }

    void NewGame() => StartIn(SaveGame.FirstEmpty); // a slot of its own, however many saves there are

    // New Game or a Load Game slot picked: the lobby music stops dead as "Loading...." comes up, leaving the
    // New / Load Game sound on its own.
    public static void CutLobbyMusic()
    {
        if (AudioManager.instance != null) AudioManager.instance.StopMusic(now: true);
    }

    // A new game in `slot` (Save Game in the pause menu saves into it).
    public static void StartIn(int slot)
    {
        SaveGame.Slot = slot;
        CutLobbyMusic();
        LoadingScreen.Load(SceneManager.GetActiveScene().buildIndex + 1);
    }

    void OpenLoad() => LoadGameMenu.Open(5, () =>
    {
        loadButton.interactable = SaveGame.Exists;
        Select(loadButton.interactable ? loadButton : optionsButton);
    });

    void OpenOptions() => SettingsMenu.Open(5, () => Select(optionsButton));

    void OpenCredits() => CreditsMenu.Open(5, () => Select(creditsButton));

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------------------------------------------------------- menu builders (main, pause, settings, load)

    // A full-screen canvas drawn over everything below `order` (the HUD is 10), with an EventSystem to drive it.
    public static RectTransform Screen(string name, int order)
    {
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var go = new GameObject(name, typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return (RectTransform)go.transform;
    }

    // A centred column that sizes itself to what's in it.
    public static RectTransform Column(RectTransform parent)
    {
        var column = Ui("Column", parent);
        column.anchorMin = column.anchorMax = column.pivot = new Vector2(0.5f, 0.5f);
        var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 0f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fit = column.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return column;
    }

    // A title in Atma ("PAUSED"), with some room under it.
    public static void Title(RectTransform column, string text, float width, int size, Color? color = null)
    {
        var title = Ui("Title", column);
        Size(title, width, size * 1.3f);
        Stretch((RectTransform)Label(Ui("Label", title), text, size, color ?? Soft, ParkArt.TitleFont).transform);
        Size(Ui("Gap", column), 10f, 30f);
    }

    // A word to pick: `ink` on its own, bold and dark on the yellow brush stroke while hovered or picked with the keys.
    // `sound` is what clicking it plays: the usual click (G-03) unless given, e.g. e => e.backButton.
    // Sizes off the design (Main Menu): a word every 49.7 design px, Arvo 28; the stroke 418.6 x 77.8, a touch low.
    public static Button Button(RectTransform parent, string text, UnityAction onClick, float width = 430f, Color? ink = null,
        System.Func<FMODEvents, FMODUnity.EventReference> sound = null)
    {
        var rt = Ui(text, parent);
        Size(rt, width, 49.7f * Figma.K);
        var stroke = Ui("Stroke", rt);
        stroke.anchorMin = stroke.anchorMax = stroke.pivot = new Vector2(0.5f, 0.5f);
        stroke.anchoredPosition = new Vector2(0f, -3.6f * Figma.K);
        stroke.sizeDelta = new Vector2(418.6f, 77.8f) * Figma.K;
        var img = stroke.gameObject.AddComponent<Image>();
        img.sprite = UiArt.Get("Buttons/Button3_Active");
        img.raycastTarget = false;
        var hit = rt.gameObject.AddComponent<Image>(); // the whole row is clickable, see-through
        hit.color = Color.clear;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;
        OnClick(button, onClick, sound);

        var label = Label(Ui("Label", rt), text, Figma.Type.ArvoSubtitle, ink ?? Soft, ParkArt.Font);
        Stretch((RectTransform)label.transform);
        var look = rt.gameObject.AddComponent<MenuWord>();
        look.label = label;
        look.button = button;
        look.stroke = img;
        look.ink = ink ?? Soft;
        return button;
    }

    // A small hand-drawn pill (Delete Slot, Back, Cancel, Yes, No): light blue (UI Asset/Buttons/Button1_Idle), yellow
    // while hovered or picked (Button1_Active), as in the design.
    // `sound` is what clicking it plays: the usual click (G-03) unless given, e.g. e => e.backButton.
    public static Button Pill(RectTransform parent, string text, UnityAction onClick, float width = 210f, float height = 58f,
        System.Func<FMODEvents, FMODUnity.EventReference> sound = null)
    {
        var rt = Ui(text, parent);
        rt.sizeDelta = new Vector2(width, height);
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = UiArt.Get("Buttons/Button1_Idle");
        img.raycastTarget = true; // clickable
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        button.transition = Selectable.Transition.SpriteSwap;
        var swap = button.spriteState;
        swap.highlightedSprite = swap.selectedSprite = swap.pressedSprite = UiArt.Get("Buttons/Button1_Active");
        button.spriteState = swap;
        OnClick(button, onClick, sound);
        var label = Label(Ui("Label", rt), text, Figma.Type.ArvoSubtitle, Ink, ParkArt.Font);
        Stretch((RectTransform)label.transform);
        ((RectTransform)label.transform).offsetMin = new Vector2(0f, 3f);
        rt.gameObject.AddComponent<HoverSelects>();
        return button;
    }

    // The click sound (G-03 unless given), then what the button does: before it opens a scene or screen.
    static void OnClick(Button button, UnityAction onClick, System.Func<FMODEvents, FMODUnity.EventReference> sound)
    {
        if (onClick == null) return;
        var click = sound ?? (e => e.menuClick);
        button.onClick.AddListener(() => AudioManager.TryPlay(click));
        button.onClick.AddListener(onClick);
    }

    // Picks a button for the player (a screen opening): no hover sound, they didn't move to it.
    public static void Select(Selectable s)
    {
        quietSelect = true;
        EventSystem.current?.SetSelectedGameObject(s != null ? s.gameObject : null);
        quietSelect = false;
    }

    static bool quietSelect;

    // ---------------------------------------------------------------- UI helpers

    public static RectTransform Ui(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Size(RectTransform rt, float w, float h)
    {
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = w;
        le.preferredHeight = h;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    public static Image Img(RectTransform rt, string sprite, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = ParkArt.Get(sprite);
        img.color = color;
        img.raycastTarget = false;
        if (img.sprite != null && img.sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
        return img;
    }

    public static Text Label(RectTransform rt, string text, int size, Color color, Font font)
    {
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    // Hovering something selects it, so mouse and keys share one highlight.
    // Moving the highlight onto it, with the mouse or the keys, plays the hover sound (G-02), unless it's `quiet`.
    public class HoverSelects : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        public bool quiet; // no hover sound (Options' tabs: they only sound when clicked)

        public void OnPointerEnter(PointerEventData e)
        {
            if (TryGetComponent(out Selectable s) && s.interactable) EventSystem.current?.SetSelectedGameObject(gameObject);
        }

        public void OnSelect(BaseEventData e)
        {
            if (!quietSelect && !quiet) AudioManager.TryPlay(ev => ev.menuHover);
        }
    }

    // A word in a menu: the brush stroke shows under the one picked, which goes dark on it; greyed out when it can't
    // be picked.
    class MenuWord : HoverSelects
    {
        public Text label;
        public Button button;
        public Image stroke;
        public Color ink;

        void Update()
        {
            bool picked = button.interactable && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            stroke.enabled = picked;
            label.color = !button.interactable ? new Color(ink.r, ink.g, ink.b, 0.3f) : picked ? Ink : ink;
            label.fontStyle = picked ? FontStyle.Bold : FontStyle.Normal;
        }
    }
}
