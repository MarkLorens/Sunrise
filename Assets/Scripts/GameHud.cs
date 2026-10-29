using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The HUD, built at runtime in the style of My project's: a counters panel (souls, mischief, time left in the body,
// exorcisms), a context-sensitive controls bar whose keycaps light up while held, a prompt floating over whatever
// you can possess / grab / use, the intro banner, a feed of what's happening, the win banner, the to-do list of
// mischief on Tab (the game pauses while it's open) and the BANISHED card.
// It also gives the main camera its rig and the spirit its markers.
public class GameHud : MonoBehaviour
{
    public static GameHud Instance { get; private set; }
    public static int Casualties;
    public static int Souls { get; private set; }
    public const int SoulsToWin = 3;

    public string objective = "Break the Ferris wheel";
    public int panicCrowdSize = 3; // this many visitors panicking at once ticks off the panic mischief

    static readonly Color PanelDark = new(0.12f, 0.08f, 0.12f, 0.8f);
    static readonly Color Ink = new(0.17f, 0.1f, 0.13f);
    static readonly Color Soft = new(0.96f, 0.92f, 0.88f);
    static readonly Color Mint = new(0.6f, 1f, 0.92f);
    static readonly List<(string text, string icon)> pending = new();

    class Toast
    {
        public RectTransform rect, body;
        public CanvasGroup group;
        public float age;
    }

    Possession spirit;
    PlayerInteractor interactor;
    RectTransform canvas;
    Text soulsLabel, mischiefLabel, gripLabel, exorcisedLabel;
    GameObject gripRow;
    GameObject controlsBar, floatHint, possessHint, walkHint, grabHint, whackHint, throwHint, dropHint, shoutHint, useHint, leaveHint;
    RectTransform prompt;
    GameObject promptCap;
    Text promptKey, promptText;
    CanvasGroup intro;
    float introAge;
    RectTransform feed, toastTemplate;
    readonly List<Toast> toasts = new();
    CanvasGroup winBanner;
    float winAge = -1f;
    CanvasGroup listScreen;
    RectTransform listPage;
    readonly List<(Mischief.Task task, Image box, Text label)> rows = new();
    bool listOpen;
    float listFade;
    CanvasGroup flash, banishCard;
    GameObject banishRetry;

    void Awake()
    {
        Instance = this;
        spirit = GetComponent<Possession>();
        interactor = GetComponent<PlayerInteractor>();
        Casualties = 0;
        Souls = 0;
        Mischief.Reset();
        ParkChaos.Reset();

        if (Camera.main != null && Camera.main.GetComponent<CameraRig>() == null) Camera.main.gameObject.AddComponent<CameraRig>();
        if (GetComponent<PossessionMarker>() == null) gameObject.AddComponent<PossessionMarker>();
        Build();
        foreach (var (text, icon) in pending) Push(text, icon);
        pending.Clear();
    }

    void OnDestroy()
    {
        if (listOpen) Time.timeScale = 1f;
        if (Instance == this) Instance = null;
    }

    // A line in the event feed, with an optional icon (a sprite path for ParkArt.Get).
    public static void Say(string text, string icon = null)
    {
        if (Instance != null) Instance.Push(text, icon);
        else pending.Add((text, icon));
    }

    public static void AddSoul()
    {
        Souls++;
        Say($"Soul harvested! ({Souls}/{SoulsToWin})", "Fx/soul");
        if (Souls != SoulsToWin) return;
        Mischief.Complete(Mischief.Task.HarvestSouls);
        if (Instance != null) Instance.winAge = 0f;
    }

    // Driven by the banish cutscene: white-out, then the card.
    public void SetBanish(float flashAlpha, float cardAlpha, float cardScale, bool retry)
    {
        flash.alpha = flashAlpha;
        banishCard.alpha = cardAlpha;
        banishCard.transform.localScale = Vector3.one * cardScale;
        banishRetry.SetActive(retry);
    }

    // ---------------------------------------------------------------- every frame

    void Update()
    {
        var kb = Keyboard.current;
        bool toggle = kb != null && (kb.tabKey.wasPressedThisFrame || (listOpen && kb.escapeKey.wasPressedThisFrame));
        if (toggle && !spirit.Banished)
        {
            listOpen = !listOpen;
            Time.timeScale = listOpen ? 0f : 1f;
            Possession.InputBlocked = listOpen;
            if (listOpen) RefreshList();
        }

        int panicking = 0;
        foreach (var v in Visitor.All)
            if (v.Current == Visitor.State.Panic) panicking++;
        if (panicking >= panicCrowdSize && !Mischief.IsDone(Mischief.Task.CrowdPanic))
        {
            Say("The crowd is in a panic!", "Icons/Scream");
            Mischief.Complete(Mischief.Task.CrowdPanic);
        }

        UpdateCounters();
        UpdateControls();
        UpdatePrompt();
        UpdateFeed();
        UpdateBanners();
    }

    void UpdateCounters()
    {
        soulsLabel.text = $"Souls  <b>{Souls}</b> / {SoulsToWin}";
        mischiefLabel.text = $"Mischief  <b>{Mischief.DoneCount}</b> / {Mischief.List.Length}";
        exorcisedLabel.text = $"Exorcised  <b>{spirit.Ejections}</b> / {spirit.maxEjections}";

        string grip = "";
        if (spirit.Host != null)
        {
            int left = Mathf.CeilToInt(spirit.TimeLeft);
            grip = $"Possession  <color={(left <= 5 ? "#ff8a7a" : "#c9b6ff")}><b>{left}s</b></color>";
        }
        else if (spirit.State == Possession.Mode.Spirit && spirit.CooldownLeft > 0f)
            grip = $"<color=#ffb38a>Too weak to possess  <b>{Mathf.CeilToInt(spirit.CooldownLeft)}s</b></color>";
        if (gripRow.activeSelf != grip.Length > 0) gripRow.SetActive(grip.Length > 0);
        gripLabel.text = grip;
    }

    void UpdateControls()
    {
        bool isSpirit = spirit.Host == null;
        bool holding = !isSpirit && spirit.Host.Held != null;
        Set(controlsBar, !spirit.Banished);
        Set(floatHint, isSpirit && !spirit.Banished);
        Set(possessHint, isSpirit && !spirit.Banished);
        Set(walkHint, !isSpirit);
        Set(grabHint, !isSpirit && !holding);
        Set(whackHint, holding);
        Set(throwHint, holding);
        Set(dropHint, holding);
        Set(shoutHint, !isSpirit);
        Set(useHint, !isSpirit && interactor.Nearest() != null);
        Set(leaveHint, !isSpirit);
    }

    static void Set(GameObject go, bool on)
    {
        if (go.activeSelf != on) go.SetActive(on);
    }

    void UpdatePrompt()
    {
        Vector3? anchor = null;
        string key = "", text = "";
        var host = spirit.Host;

        if (spirit.State == Possession.Mode.Spirit && spirit.Candidate != null)
        {
            anchor = spirit.Candidate.position + Vector3.up * 2.6f;
            if (spirit.CandidateBlocked == null)
            {
                key = "E";
                text = "Possess " + spirit.CandidateName;
            }
            else text = spirit.CandidateBlocked;
        }
        else if (host != null)
        {
            var target = interactor.Nearest();
            if (target != null)
            {
                var r = target.GetComponent<SpriteRenderer>();
                anchor = r != null ? new Vector3(target.transform.position.x, r.bounds.max.y + 0.4f, target.transform.position.z) : target.transform.position + Vector3.up * 2f;
                string blocked = target.Blocked(host);
                key = blocked == null ? "E" : "";
                text = blocked == null ? target.Prompt(host) : $"<color=#bbbbbb>{target.Prompt(host)}: {blocked}</color>";
            }
            else if (spirit.GrabCandidate != null)
            {
                var g = spirit.GrabCandidate;
                anchor = (g.Holder != null ? g.Holder.HeadPoint + Vector3.up * 0.9f : g.transform.position + Vector3.up * 0.8f);
                key = "J";
                text = g.Holder != null ? $"Swipe {g.Holder.name}'s {g.DisplayName}"
                    : g.Owner != null && g.Owner != host ? $"Grab {g.Owner.name}'s {g.DisplayName}" : "Grab the " + g.DisplayName;
            }
        }

        var cam = Camera.main;
        Vector3 screen = anchor.HasValue && cam != null ? cam.WorldToScreenPoint(anchor.Value) : Vector3.back;
        bool show = anchor.HasValue && screen.z > 0f && !listOpen;
        Set(prompt.gameObject, show);
        if (!show) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out Vector2 local);
        prompt.anchoredPosition = local;
        Set(promptCap, key.Length > 0);
        promptKey.text = key;
        promptText.text = text;
    }

    void UpdateFeed()
    {
        for (int i = toasts.Count - 1; i >= 0; i--)
        {
            var t = toasts[i];
            t.age += Time.unscaledDeltaTime;
            float fadeIn = Mathf.Clamp01(t.age / 0.15f);
            t.group.alpha = Mathf.Min(fadeIn, Mathf.Clamp01((3.6f - t.age) / 0.4f));
            t.body.anchoredPosition = new Vector2((1f - fadeIn) * 60f, 0f);
            if (t.age < 3.6f) continue;
            Destroy(t.rect.gameObject);
            toasts.RemoveAt(i);
        }
    }

    void UpdateBanners()
    {
        float dt = Time.unscaledDeltaTime;
        introAge += dt;
        if (spirit.Host != null && introAge < 9f) introAge = 9f; // got the idea
        intro.alpha = Mathf.Min(Mathf.Clamp01((introAge - 0.5f) * 2f), Mathf.Clamp01((10f - introAge) * 2f));

        if (winAge >= 0f)
        {
            winAge += dt;
            winBanner.alpha = winAge < 7f ? Mathf.Clamp01(winAge * 2f) : Mathf.Clamp01(1f - (winAge - 7f));
            winBanner.transform.localScale = Vector3.one * (1f + Mathf.Max(0f, 0.3f - winAge));
        }

        listFade = Mathf.MoveTowards(listFade, listOpen ? 1f : 0f, dt * 8f);
        listScreen.alpha = listFade;
        // The page slides up and tilts in like a notepad being pulled out.
        listPage.anchoredPosition = new Vector2(0f, Mathf.Lerp(-120f, 0f, listFade));
        listPage.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-6f, -2f, listFade));
    }

    void RefreshList()
    {
        foreach (var (task, box, label) in rows)
        {
            bool done = Mischief.IsDone(task);
            box.sprite = ParkArt.Get(done ? "UI/check_on" : "UI/check_off");
            label.color = done ? new Color(0.55f, 0.5f, 0.48f) : Ink;
        }
    }

    void Push(string text, string icon)
    {
        var rect = Instantiate(toastTemplate, feed);
        rect.gameObject.SetActive(true);
        var body = (RectTransform)rect.GetChild(0);
        body.GetComponentInChildren<Text>().text = text;
        var iconImage = body.GetChild(0).GetComponent<Image>();
        iconImage.sprite = ParkArt.Get(icon);
        iconImage.gameObject.SetActive(iconImage.sprite != null);
        toasts.Add(new Toast { rect = rect, body = body, group = rect.GetComponent<CanvasGroup>() });
        while (toasts.Count > 5)
        {
            Destroy(toasts[0].rect.gameObject);
            toasts.RemoveAt(0);
        }
    }

    // ---------------------------------------------------------------- building the canvas

    void Build()
    {
        var go = new GameObject("HUD", typeof(RectTransform));
        canvas = (RectTransform)go.transform;
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 10;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        BuildCounters();
        BuildControls();
        BuildPrompt();
        BuildIntro();
        BuildFeed();
        BuildWinBanner();
        BuildList();
        BuildBanish();
    }

    void BuildCounters()
    {
        var panel = Ui("Counters", canvas);
        Place(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -26f), Vector2.zero);
        Img(panel, "UI/pill", PanelDark);
        var column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(22, 30, 12, 12);
        column.spacing = 4f;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = column.childForceExpandHeight = false;
        Fit(panel);

        var souls = Ui("Souls", panel);
        Row(souls, 10f).childAlignment = TextAnchor.MiddleLeft;
        Icon(souls, "UI/soul", 40f);
        soulsLabel = Txt(Ui("Label", souls), "Souls", 30, Mint);

        var mischief = Ui("Mischief", panel);
        Row(mischief, 10f).childAlignment = TextAnchor.MiddleLeft;
        mischiefLabel = Txt(Ui("Label", mischief), "Mischief", 24, Soft);
        KeyCap(mischief, "Tab", new[] { Key.Tab }, false, 36f);

        var grip = Ui("Grip", panel);
        Row(grip, 10f).childAlignment = TextAnchor.MiddleLeft;
        gripLabel = Txt(Ui("Label", grip), "", 24, Soft);
        gripRow = grip.gameObject;

        var exorcised = Ui("Exorcised", panel);
        Row(exorcised, 10f).childAlignment = TextAnchor.MiddleLeft;
        Icon(exorcised, "UI/cross", 32f);
        exorcisedLabel = Txt(Ui("Label", exorcised), "Exorcised", 24, Soft);
    }

    void BuildControls()
    {
        var bar = Ui("Controls Bar", canvas);
        Place(bar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), Vector2.zero);
        Img(bar, "UI/pill", PanelDark);
        Row(bar, 34f, new RectOffset(32, 32, 12, 12));
        Fit(bar);
        controlsBar = bar.gameObject;

        GameObject Group(string name, string label, params (string text, Key[] keys, bool mouse)[] caps)
        {
            var group = Ui(name, bar);
            Row(group, 7f);
            foreach (var (text, keys, mouse) in caps) KeyCap(group, text, keys, mouse);
            Txt(Ui("Label", group), label, 25, Soft);
            return group.gameObject;
        }

        var wasd = new (string, Key[], bool)[]
        {
            ("W", new[] { Key.W, Key.UpArrow }, false), ("A", new[] { Key.A, Key.LeftArrow }, false),
            ("S", new[] { Key.S, Key.DownArrow }, false), ("D", new[] { Key.D, Key.RightArrow }, false),
        };
        floatHint = Group("Float", "Float", wasd);
        possessHint = Group("Possess", "Possess", ("E", new[] { Key.E }, false));
        walkHint = Group("Walk", "Walk", wasd);
        grabHint = Group("Grab", "Grab", ("J", new[] { Key.J }, true));
        whackHint = Group("Whack", "Tap: Whack", ("J", new[] { Key.J }, true));
        throwHint = Group("Throw", "Hold: Throw", ("J", new[] { Key.J }, true));
        dropHint = Group("Drop", "Drop", ("K", new[] { Key.K }, false));
        shoutHint = Group("Shout", "Shout", ("Space", new[] { Key.Space }, false));
        useHint = Group("Use", "Use", ("E", new[] { Key.E }, false));
        leaveHint = Group("Leave", "Leave body", ("Q", new[] { Key.Q }, false));
    }

    void BuildPrompt()
    {
        prompt = Ui("World Prompt", canvas);
        Place(prompt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(10f, 10f));
        var body = Ui("Body", prompt);
        Place(body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
        Img(body, "UI/pill", new Color(0.12f, 0.08f, 0.12f, 0.88f));
        Row(body, 10f, new RectOffset(10, 22, 6, 6));
        Fit(body);
        var cap = KeyCap(body, "E", new Key[0], false, 40f);
        Destroy(cap.GetComponent<KeyHint>());
        promptCap = cap.gameObject;
        promptKey = cap.GetComponentInChildren<Text>();
        promptText = Txt(Ui("Label", body), "", 26, Color.white);
    }

    void BuildIntro()
    {
        var banner = Ui("Intro", canvas);
        Place(banner, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), Vector2.zero);
        Img(banner, "UI/pill", PanelDark);
        Row(banner, 14f, new RectOffset(26, 34, 10, 10));
        Fit(banner);
        intro = banner.gameObject.AddComponent<CanvasGroup>();
        intro.alpha = 0f;
        Icon(banner, "UI/spirit", 46f);
        Txt(Ui("Text", banner), $"You are an evil spirit loose in a theme park. <b>{objective}</b> and spread chaos, but beware the masked guards!", 26, new Color(0.98f, 0.9f, 1f));
    }

    void BuildFeed()
    {
        feed = Ui("Event Feed", canvas);
        Place(feed, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 120f), new Vector2(900f, 420f));
        var column = feed.gameObject.AddComponent<VerticalLayoutGroup>();
        column.childAlignment = TextAnchor.LowerRight;
        column.spacing = 8f;
        column.childControlWidth = column.childControlHeight = false;
        column.childForceExpandWidth = column.childForceExpandHeight = false;

        toastTemplate = Ui("Entry", feed);
        toastTemplate.sizeDelta = new Vector2(900f, 52f);
        toastTemplate.gameObject.AddComponent<CanvasGroup>();
        var body = Ui("Body", toastTemplate);
        body.anchorMin = new Vector2(1f, 0f);
        body.anchorMax = new Vector2(1f, 1f);
        body.pivot = new Vector2(1f, 0.5f);
        body.anchoredPosition = Vector2.zero;
        body.sizeDelta = Vector2.zero;
        Img(body, "UI/pill", new Color(0.12f, 0.08f, 0.12f, 0.88f));
        Row(body, 10f, new RectOffset(12, 24, 5, 5));
        body.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        Icon(body, "UI/soul", 40f);
        Txt(Ui("Label", body), "Event", 24, Color.white);
        toastTemplate.gameObject.SetActive(false);
    }

    void BuildWinBanner()
    {
        var banner = Ui("Win Banner", canvas);
        Place(banner, new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Img(banner, "UI/pill", new Color(0.18f, 0.08f, 0.26f, 0.9f));
        var column = banner.gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(60, 60, 22, 26);
        column.spacing = 4f;
        column.childAlignment = TextAnchor.MiddleCenter;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = column.childForceExpandHeight = false;
        Fit(banner);
        winBanner = banner.gameObject.AddComponent<CanvasGroup>();
        winBanner.alpha = 0f;
        Txt(Ui("Title", banner), "The park trembles...", 64, Mint, TextAnchor.MiddleCenter, true);
        Txt(Ui("Subtitle", banner), $"{SoulsToWin} souls harvested. Keep causing mischief!", 30, Soft, TextAnchor.MiddleCenter);
    }

    void BuildList()
    {
        var screen = Ui("Mischief List", canvas);
        Stretch(screen);
        listScreen = screen.gameObject.AddComponent<CanvasGroup>();
        listScreen.alpha = 0f;
        var dim = Ui("Dim", screen);
        Stretch(dim);
        Img(dim, null, new Color(0.05f, 0.03f, 0.06f, 0.45f));

        listPage = Ui("Page", screen);
        float height = 190f + Mischief.List.Length * 50f;
        Place(listPage, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, height));
        Img(listPage, "UI/paper", Color.white).pixelsPerUnitMultiplier = 0.5f;

        var title = Ui("Title", listPage);
        Place(title, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(600f, 64f));
        Txt(title, "Mischief List", 50, Ink, TextAnchor.MiddleLeft, true);

        var list = Ui("Rows", listPage);
        Place(list, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(590f, Mischief.List.Length * 50f));
        var column = list.gameObject.AddComponent<VerticalLayoutGroup>();
        column.spacing = 10f;
        column.childAlignment = TextAnchor.UpperLeft;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        foreach (var (task, label) in Mischief.List)
        {
            var row = Ui(task.ToString(), list);
            Row(row, 16f).childAlignment = TextAnchor.MiddleLeft;
            var box = Icon(row, "UI/check_off", 40f);
            rows.Add((task, box, Txt(Ui("Label", row), label, 28, Ink)));
        }

        var footer = Ui("Footer", listPage);
        Place(footer, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(600f, 36f));
        Txt(footer, "Tab to close", 22, new Color(0.45f, 0.38f, 0.36f), TextAnchor.MiddleRight);
    }

    void BuildBanish()
    {
        var white = Ui("Flash", canvas);
        Stretch(white);
        Img(white, null, Color.white);
        flash = white.gameObject.AddComponent<CanvasGroup>();
        flash.alpha = 0f;

        var card = Ui("Banished", canvas);
        Place(card, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Img(card, "UI/pill", new Color(0.12f, 0.06f, 0.14f, 0.92f));
        var column = card.gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(70, 70, 30, 34);
        column.spacing = 10f;
        column.childAlignment = TextAnchor.MiddleCenter;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = column.childForceExpandHeight = false;
        Fit(card);
        banishCard = card.gameObject.AddComponent<CanvasGroup>();
        banishCard.alpha = 0f;
        Icon(card, "UI/cross", 90f);
        Txt(Ui("Title", card), "BANISHED", 80, new Color(1f, 0.85f, 0.45f), TextAnchor.MiddleCenter, true);
        Txt(Ui("Text", card), $"Exorcised {spirit.maxEjections} times. The guards' prayers cast you out of the park.", 30, Soft, TextAnchor.MiddleCenter);
        var retry = Txt(Ui("Retry", card), "Press  <b>R</b>  to try again", 26, new Color(0.8f, 0.75f, 0.85f), TextAnchor.MiddleCenter);
        banishRetry = retry.gameObject;
        banishRetry.SetActive(false);
    }

    // ---------------------------------------------------------------- UI helpers

    static RectTransform Ui(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Image Img(RectTransform rt, string sprite, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = ParkArt.Get(sprite);
        img.color = color;
        img.raycastTarget = false;
        if (img.sprite != null && img.sprite.border != Vector4.zero)
        {
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1.3f;
        }
        return img;
    }

    static Image Icon(RectTransform parent, string sprite, float size)
    {
        var img = Img(Ui("Icon", parent), sprite, Color.white);
        img.preserveAspect = true;
        var le = img.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = le.preferredHeight = size;
        return img;
    }

    static Text Txt(RectTransform rt, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, bool bold = false)
    {
        var t = rt.gameObject.AddComponent<Text>();
        t.font = ParkArt.Font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        if (bold) t.fontStyle = FontStyle.Bold;
        t.text = text;
        return t;
    }

    static HorizontalLayoutGroup Row(RectTransform rt, float spacing, RectOffset padding = null)
    {
        var row = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = spacing;
        row.padding = padding ?? new RectOffset();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        return row;
    }

    static void Fit(RectTransform rt)
    {
        var fit = rt.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    static RectTransform KeyCap(RectTransform parent, string text, Key[] keys, bool mouse, float height = 46f)
    {
        var cap = Ui("Key " + text, parent);
        var img = Img(cap, "UI/key", Color.white);
        img.pixelsPerUnitMultiplier = 1f;
        var element = cap.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = Mathf.Max(height, text.Length * 15f + 26f);
        element.preferredHeight = height;
        var hint = cap.gameObject.AddComponent<KeyHint>();
        hint.keys = keys;
        hint.leftMouse = mouse;
        var label = Ui("Label", cap);
        Stretch(label);
        label.offsetMin = new Vector2(0f, 6f);
        Txt(label, text, Mathf.RoundToInt(height * 0.45f), Ink, TextAnchor.MiddleCenter, true);
        return cap;
    }
}
