using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The main menu, the first scene in Build Settings. Built at runtime in the HUD's style (GameHud): the title on a
// brush stroke over the night sky, and a column of buttons. Mouse, keyboard (arrows + Enter) and gamepad all work.
// New Game loads the next scene in Build Settings (the park), so renaming that scene doesn't break it.
// The static builders below are shared with the pause menu (PauseMenu).
public class MainMenu : MonoBehaviour
{
    static readonly Color Sky = new(0.04f, 0.03f, 0.09f);
    static readonly Color Brush = new(0.1f, 0.07f, 0.1f, 0.9f);
    static readonly Color ButtonDark = new(0.12f, 0.08f, 0.12f, 0.88f);
    static readonly Color Soft = new(0.96f, 0.92f, 0.88f);
    static readonly Color Ink = new(0.17f, 0.1f, 0.13f);

    static Font handFont;
    static Font HandFont => handFont != null ? handFont : handFont = Resources.Load<Font>("Park/Fonts/LoveYaLikeASister") ?? ParkArt.Font;

    void Awake()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Sky;
        }
        var column = Column(Screen("Main Menu", 0));
        Title(column, Application.productName.Replace('_', ' '), 900f, 84);
        var first = Button(column, "New Game", NewGame);
        Button(column, "Load Game", SaveGame.Load).interactable = SaveGame.Exists; // greyed out with no save yet
        // ponytail: Credits does nothing yet (no credits screen); wire it when there is one.
        Button(column, "Credits", null);
        Button(column, "Quit Game", Quit);
        Select(first);
    }

    void NewGame() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------------------------------------------------------- menu builders (main menu and pause menu)

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
        layout.spacing = 22f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fit = column.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return column;
    }

    // A title on a brush stroke, with some room under it.
    public static void Title(RectTransform column, string text, float width, int size)
    {
        var title = Ui("Title", column);
        Img(title, "UI/objective_banner", Brush);
        Size(title, width, size * 1.8f);
        Stretch((RectTransform)Label(Ui("Label", title), text, size, Color.white).transform);
        Size(Ui("Gap", column), 10f, 40f);
    }

    // A rounded button: dark with light text, light with dark text while hovered or picked with the keys.
    public static Button Button(RectTransform parent, string text, UnityAction onClick)
    {
        var rt = Ui(text, parent);
        var img = Img(rt, "UI/pill", Color.white);
        Size(rt, 420f, 84f);
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        var colors = button.colors;
        colors.normalColor = ButtonDark;
        colors.highlightedColor = colors.selectedColor = Soft; // hovered (which selects it) or picked with the keys
        colors.pressedColor = new Color(0.85f, 0.8f, 0.76f);
        colors.disabledColor = new Color(ButtonDark.r, ButtonDark.g, ButtonDark.b, 0.45f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        if (onClick != null) button.onClick.AddListener(onClick);

        var label = Label(Ui("Label", rt), text, 40, Soft);
        Stretch((RectTransform)label.transform);
        var ink = rt.gameObject.AddComponent<MenuButtonInk>();
        ink.label = label;
        ink.button = button;
        return button;
    }

    public static void Select(Selectable s) => EventSystem.current?.SetSelectedGameObject(s.gameObject);

    // ---------------------------------------------------------------- UI helpers

    public static RectTransform Ui(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Size(RectTransform rt, float w, float h)
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
        if (img.sprite != null && img.sprite.border != Vector4.zero)
        {
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1.3f;
        }
        return img;
    }

    static Text Label(RectTransform rt, string text, int size, Color color)
    {
        var t = rt.gameObject.AddComponent<Text>();
        t.font = HandFont;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    // Hovering a button selects it, so mouse and keys share one highlight; its label goes dark on the light pill.
    class MenuButtonInk : MonoBehaviour, IPointerEnterHandler
    {
        public Text label;
        public Button button;

        public void OnPointerEnter(PointerEventData e)
        {
            if (button.interactable) EventSystem.current?.SetSelectedGameObject(gameObject);
        }

        void Update() => label.color =
            !button.interactable ? new Color(Soft.r, Soft.g, Soft.b, 0.35f)
            : EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject ? Ink : Soft;
    }
}
