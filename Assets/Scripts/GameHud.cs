using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The HUD, built at runtime in the style of My project's: the objectives (top left: the part of the level you're on
// and its steps; a step that gets done is struck through and stays on the list), a pop-up line for each thing the
// player does and each step done (bottom left; the wording is all in GameText), a prompt floating over whatever you
// can possess / use (and why not, when E is refused), and the intro banner. While you're in someone, an
// inventory box (bottom right) shows what they're holding.
// It also gives the main camera its rig and the spirit its outline of what it can act on.
public class GameHud : MonoBehaviour
{
    public static GameHud Instance { get; private set; }

    public string objective = "Cut the Ferris wheel's power";

    static readonly Color PanelDark = new(0.12f, 0.08f, 0.12f, 0.8f);
    static readonly Color Ink = new(0.17f, 0.1f, 0.13f);
    static readonly Color Soft = new(0.96f, 0.92f, 0.88f);
    static readonly Color Brush = new(0.1f, 0.07f, 0.1f, 0.9f);
    static readonly Color StepInk = new(0.97f, 0.93f, 0.86f);
    static readonly List<string> pending = new();
    static Font handFont;
    static Font HandFont => handFont != null ? handFont : handFont = Resources.Load<Font>("Park/Fonts/LoveYaLikeASister");

    // A step in the objectives list. age < 0 until it's done; then a line is drawn through it and it stays put.
    class StepRow
    {
        public Mischief.Task task;
        public LayoutElement slot;
        public RectTransform body, strike;
        public CanvasGroup group;
        public float width, age = -1f, appear;
    }

    Possession spirit;
    PlayerInteractor interactor;
    RectTransform canvas;
    CanvasGroup objectives;
    Text objectiveTitle;
    RectTransform stepList;
    readonly List<StepRow> stepRows = new();
    int shownPart = -2; // index into Mischief.Parts, -1 when every step is done
    RectTransform doneBody;
    CanvasGroup doneGroup;
    Text doneText;
    float doneAge = DoneTime, doneShow = DoneTime;
    readonly List<string> doneQueue = new();
    int actionFrame = -1; // the frame the last line in doneQueue was queued by Say (a step done then takes its place)
    const float DoneTime = 3.6f, DoneMin = 1.4f, DoneFade = 0.4f, StepHeight = 38f;
    const float PartHold = 1.5f; // seconds the finished part's struck-through list stays up before the next part
    Text gripLabel;
    GameObject gripRow;
    RectTransform prompt;
    GameObject promptCap;
    Text promptKey, promptText;
    Transform refusedBy;
    bool refusedPerson;
    string refusal;
    float refusedAt = -10f;
    const float RefusalTime = 1.6f;
    CanvasGroup intro;
    float introAge;
    CanvasGroup inventory;
    Image inventoryItem;

    void Awake()
    {
        Instance = this;
        spirit = GetComponent<Possession>();
        interactor = GetComponent<PlayerInteractor>();
        Mischief.Reset();
        ParkChaos.Reset();

        if (Camera.main != null && Camera.main.GetComponent<CameraRig>() == null) Camera.main.gameObject.AddComponent<CameraRig>();
        if (Camera.main != null && Camera.main.GetComponent<SeeThrough>() == null) Camera.main.gameObject.AddComponent<SeeThrough>();
        if (FindAnyObjectByType<NightLighting>() == null) new GameObject("Night Lighting").AddComponent<NightLighting>();
        if (GetComponent<ActionOutline>() == null) gameObject.AddComponent<ActionOutline>();
        Build();
        doneQueue.AddRange(pending);
        pending.Clear();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // A line in the bottom-left pop-up for something the player just did. One at a time: later ones wait their turn.
    public static void Say(string text)
    {
        if (Instance == null)
        {
            pending.Add(text);
            return;
        }
        Instance.doneQueue.Add(text);
        Instance.actionFrame = Time.frameCount;
    }

    // The pop-up line for a step that just got done. If what the player did this very frame is what did it, this
    // line takes the place of that one: one thing happened, one line.
    public static void StepDone(string text)
    {
        if (Instance == null)
        {
            pending.Add(text);
            return;
        }
        var q = Instance.doneQueue;
        if (Instance.actionFrame == Time.frameCount && q.Count > 0) q[q.Count - 1] = text;
        else q.Add(text);
        Instance.actionFrame = -1;
    }

    // Why pressing E didn't work, floated over whatever said no for a moment (person: someone you tried to possess).
    public static void Refuse(Transform by, string why, bool person = false)
    {
        if (Instance == null || by == null || string.IsNullOrEmpty(why)) return;
        Instance.refusedBy = by;
        Instance.refusedPerson = person;
        Instance.refusal = why;
        Instance.refusedAt = Time.unscaledTime;
    }

    // ---------------------------------------------------------------- every frame

    void Update()
    {
        UpdateObjectives();
        UpdateStepPopup();
        UpdateGrip();
        UpdatePrompt();
        UpdateInventory();
        UpdateBanners();
    }

    // The part you're on: the first one with a step still to do (-1 once they're all done).
    static int ActivePart()
    {
        for (int p = 0; p < Mischief.Parts.Length; p++)
            foreach (var (task, _) in Mischief.Parts[p].steps)
                if (!Mischief.IsDone(task)) return p;
        return -1;
    }

    void UpdateObjectives()
    {
        float dt = Time.unscaledDeltaTime;
        // Move on to the next part once every step of this one has been struck through and stayed up a moment.
        int part = ActivePart();
        if (part != shownPart && stepRows.TrueForAll(r => r.age >= PartHold)) ShowPart(part);
        objectives.alpha = Mathf.MoveTowards(objectives.alpha, shownPart >= 0 ? 1f : 0f, dt * 3f);

        foreach (var r in stepRows)
        {
            if (r.age < 0f)
            {
                // New rows ease in from the left.
                r.appear = Mathf.Min(1f, r.appear + dt / 0.3f);
                r.group.alpha = r.appear;
                r.body.anchoredPosition = new Vector2((r.appear - 1f) * 40f, 0f);
                if (!Mischief.IsDone(r.task)) continue;
                r.age = 0f;
                r.group.alpha = 1f;
                r.body.anchoredPosition = Vector2.zero;
            }
            // Done: a line is drawn through it, and it stays on the list.
            r.age += dt;
            float strike = 1f - Mathf.Pow(1f - Mathf.Clamp01(r.age / 0.35f), 3f);
            r.strike.sizeDelta = new Vector2(r.width * strike, 3f);
        }
    }

    void ShowPart(int part)
    {
        shownPart = part;
        if (part < 0) return; // all done: the panel fades away, last list and all
        foreach (var r in stepRows) Destroy(r.slot.gameObject);
        stepRows.Clear();
        var (title, steps) = Mischief.Parts[part];
        int dot = title.IndexOf(". ");
        objectiveTitle.text = dot >= 0 && dot < 3 ? title[(dot + 2)..] : title; // "1. A-leak-tricity" -> "A-leak-tricity"
        foreach (var (task, label) in steps)
        {
            var slot = Ui(task.ToString(), stepList);
            var body = Ui("Body", slot);
            Stretch(body);
            var group = body.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            var text = Txt(body, "› " + label, 26, StepInk);
            text.font = HandFont;
            var strike = Ui("Strike", body);
            Place(strike, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -1f), new Vector2(0f, 3f));
            Img(strike, null, StepInk);
            var element = slot.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = text.preferredWidth;
            element.preferredHeight = StepHeight;
            stepRows.Add(new StepRow { task = task, slot = element, body = body, strike = strike, group = group, width = text.preferredWidth });
        }
    }

    void UpdateStepPopup()
    {
        doneAge += Time.unscaledDeltaTime;
        // More lines waiting: the one up now gives way sooner, once it's been up long enough to read.
        if (doneQueue.Count > 0) doneShow = Mathf.Min(doneShow, Mathf.Max(doneAge + DoneFade, DoneMin));
        if (doneAge >= doneShow && doneQueue.Count > 0)
        {
            doneText.text = doneQueue[0];
            doneQueue.RemoveAt(0);
            doneAge = 0f;
            doneShow = DoneTime;
        }
        // Slides in, stays a few seconds, fades.
        float fadeIn = Mathf.Clamp01(doneAge / 0.15f);
        doneGroup.alpha = Mathf.Min(fadeIn, Mathf.Clamp01((doneShow - doneAge) / DoneFade));
        doneBody.anchoredPosition = new Vector2((fadeIn - 1f) * 60f, 0f);
    }

    void UpdateGrip()
    {
        string grip = "";
        if (spirit.State == Possession.Mode.Spirit && spirit.CooldownLeft > 0f)
            grip = $"<color=#ffb38a>Too weak to possess  <b>{Mathf.CeilToInt(spirit.CooldownLeft)}s</b></color>";
        if (gripRow.activeSelf != grip.Length > 0) gripRow.SetActive(grip.Length > 0);
        gripLabel.text = grip;
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

        if (refusedBy != null && Time.unscaledTime - refusedAt < RefusalTime)
        {
            // E was just refused: say why, over whatever said no.
            anchor = refusedPerson ? PersonTop(refusedBy) : ThingTop(refusedBy);
            text = $"<color=#bbbbbb>{refusal}</color>";
        }
        else if (spirit.State == Possession.Mode.Spirit && spirit.Candidate != null)
        {
            anchor = PersonTop(spirit.Candidate);
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
                anchor = ThingTop(target.transform);
                string blocked = target.Blocked(host);
                key = blocked == null ? "E" : "";
                text = blocked == null ? target.Prompt(host) : $"<color=#bbbbbb>{target.Prompt(host)}: {blocked}</color>";
            }
        }

        var cam = Camera.main;
        Vector3 screen = anchor.HasValue && cam != null ? cam.WorldToScreenPoint(anchor.Value) : Vector3.back;
        bool show = anchor.HasValue && screen.z > 0f;
        Set(prompt.gameObject, show);
        if (!show) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out Vector2 local);
        prompt.anchoredPosition = local;
        Set(promptCap, key.Length > 0);
        promptKey.text = key;
        promptText.text = text;
    }

    // Where the floating prompt goes: over the head of someone you could possess, or over the top of a thing.
    static Vector3 PersonTop(Transform t) => t.position + Vector3.up * 2.6f;

    static Vector3 ThingTop(Transform t)
    {
        var r = t.GetComponent<SpriteRenderer>();
        return r != null ? new Vector3(t.position.x, r.bounds.max.y + 0.4f, t.position.z) : t.position + Vector3.up * 2f;
    }

    // Shown while you're in someone: empty, or the item in their hand.
    void UpdateInventory()
    {
        var host = spirit.Host;
        bool show = host != null;
        inventory.alpha = Mathf.MoveTowards(inventory.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        var held = show ? host.Held : null;
        Set(inventoryItem.gameObject, held != null);
        if (held == null) return;
        inventoryItem.sprite = held.Icon;
        inventoryItem.color = held.IconColor;
    }

    void UpdateBanners()
    {
        float dt = Time.unscaledDeltaTime;
        introAge += dt;
        if (spirit.Host != null && introAge < 9f) introAge = 9f; // got the idea
        intro.alpha = Mathf.Min(Mathf.Clamp01((introAge - 0.5f) * 2f), Mathf.Clamp01((10f - introAge) * 2f));
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

        BuildObjectives();
        BuildStepPopup();
        BuildPrompt();
        BuildIntro();
        BuildInventory();
    }

    // Top left: a brush-stroke title (the part you're on) over a dim panel of its steps.
    void BuildObjectives()
    {
        var column = Ui("Objectives", canvas);
        Place(column, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -40f), Vector2.zero);
        Column(column, 0f, new RectOffset());
        Fit(column);

        var panel = Ui("Panel", column);
        Column(panel, 0f, new RectOffset());
        objectives = panel.gameObject.AddComponent<CanvasGroup>();
        objectives.alpha = 0f;

        var banner = Ui("Title", panel);
        Img(banner, "UI/objective_banner", Brush);
        var size = banner.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = 560f;
        size.preferredHeight = 75f;
        var title = Ui("Label", banner);
        Stretch(title);
        title.offsetMin = new Vector2(36f, 0f);
        objectiveTitle = Txt(title, "", 38, Color.white, TextAnchor.MiddleLeft, true);
        objectiveTitle.font = HandFont;

        stepList = Ui("Steps", panel);
        Img(stepList, null, new Color(0.1f, 0.07f, 0.1f, 0.35f));
        Column(stepList, 4f, new RectOffset(36, 30, 12, 14));
        stepList.gameObject.AddComponent<LayoutElement>().minWidth = 520f;

        // "Too weak to possess", under the objectives (only while possessing is on cooldown).
        var grip = Ui("Grip", column);
        Column(grip, 0f, new RectOffset(28, 0, 12, 0));
        var pill = Ui("Pill", grip);
        Img(pill, "UI/pill", PanelDark);
        Row(pill, 10f, new RectOffset(22, 30, 8, 8)).childAlignment = TextAnchor.MiddleLeft;
        gripLabel = Txt(Ui("Label", pill), "", 24, Soft);
        gripRow = grip.gameObject;
    }

    // Bottom left: a dim strip with one line, shown when a step gets done.
    void BuildStepPopup()
    {
        var popup = Ui("Step Done", canvas);
        Place(popup, Vector2.zero, Vector2.zero, new Vector2(0f, 120f), new Vector2(560f, 58f));
        doneGroup = popup.gameObject.AddComponent<CanvasGroup>();
        doneGroup.alpha = 0f;
        doneBody = Ui("Body", popup);
        doneBody.anchorMin = Vector2.zero;
        doneBody.anchorMax = new Vector2(0f, 1f);
        doneBody.pivot = new Vector2(0f, 0.5f);
        doneBody.anchoredPosition = Vector2.zero;
        doneBody.sizeDelta = Vector2.zero;
        Img(doneBody, null, new Color(0.1f, 0.07f, 0.1f, 0.55f));
        Row(doneBody, 0f, new RectOffset(36, 44, 8, 8)).childAlignment = TextAnchor.MiddleLeft;
        doneBody.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        doneText = Txt(Ui("Label", doneBody), "", 28, Color.white);
        doneText.font = HandFont;
        doneText.gameObject.AddComponent<LayoutElement>().minWidth = 440f;
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
        Txt(Ui("Text", banner), $"You are an evil spirit loose in a theme park. <b>{objective}</b>.", 26, new Color(0.98f, 0.9f, 1f));
    }

    // Bottom right: a light box with a dark border, the held item in the middle.
    void BuildInventory()
    {
        var box = Ui("Inventory", canvas);
        Place(box, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(170f, 170f));
        inventory = box.gameObject.AddComponent<CanvasGroup>();
        inventory.alpha = 0f;
        Img(box, "UI/pill", new Color(Ink.r, Ink.g, Ink.b, 0.92f)).pixelsPerUnitMultiplier = 2.5f;
        var inside = Ui("Inside", box);
        Stretch(inside);
        inside.offsetMin = new Vector2(7f, 7f);
        inside.offsetMax = new Vector2(-7f, -7f);
        Img(inside, "UI/pill", new Color(0.93f, 0.92f, 0.95f, 0.88f)).pixelsPerUnitMultiplier = 2.8f;
        var item = Ui("Item", inside);
        Stretch(item);
        item.offsetMin = new Vector2(26f, 22f);
        item.offsetMax = new Vector2(-26f, -22f);
        inventoryItem = Img(item, null, Color.white);
        inventoryItem.preserveAspect = true;
        item.gameObject.SetActive(false);
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

    static VerticalLayoutGroup Column(RectTransform rt, float spacing, RectOffset padding)
    {
        var column = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        column.spacing = spacing;
        column.padding = padding;
        column.childAlignment = TextAnchor.UpperLeft;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = column.childForceExpandHeight = false;
        return column;
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
