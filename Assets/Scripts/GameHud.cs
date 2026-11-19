using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The HUD, built at runtime in the style of My project's: the objectives (top left: the part of the level you're on
// and its steps; a step that gets done is struck through and stays on the list), a pop-up line for each thing the
// player does and each step done (bottom left; the wording is all in GameText), a prompt floating over whatever you
// can possess / use (and why not, when E is refused), and the intro banner.
// It also gives the main camera its rig and the spirit its outline of what it can act on.
public class GameHud : MonoBehaviour
{
    public static GameHud Instance { get; private set; }
    public static bool HideObjectives; // the objectives (top left) stay out of the way (the park tour, ParkTour)

    public string objective = "Cut the Ferris wheel's power";
    public Sprite spiritIcon; // the intro banner's icon (falls back to UI/spirit)

    static readonly Color PanelDark = new(0.12f, 0.08f, 0.12f, 0.8f);
    static readonly Color Ink = new(0.17f, 0.1f, 0.13f);
    static readonly Color Soft = new(0.96f, 0.92f, 0.88f);
    static readonly List<string> pending = new();
    static Font handFont;
    static Font HandFont => handFont != null ? handFont : handFont = Resources.Load<Font>("Park/Fonts/LoveYaLikeASister");

    // A step in the objectives list. age < 0 until it's done; then a line is drawn through it and it stays put.
    // A long step wraps onto more lines (StepChars at most per line), each struck through on its own.
    class StepRow
    {
        public Mischief.Task task;
        public LayoutElement slot;
        public RectTransform body;
        public RectTransform[] strikes; // one per line
        public float[] widths;          // each line's width
        public CanvasGroup group;
        public float age = -1f, appear;
    }

    Possession spirit;
    PlayerInteractor interactor;
    RectTransform canvas;
    CanvasGroup objectives;
    Text objectiveTitle;
    RectTransform objectiveBanner, stepList;
    LayoutElement bannerSize; // the title's width follows its text
    VerticalLayoutGroup objectivesColumn;
    // The steps drop down from under the title: click the title or press Tab to open or close them.
    LayoutElement stepsDrop;
    CanvasGroup stepsGroup;
    bool stepsOpen = true;
    float stepsShown = 1f, bannerPop = 1f;
    const float DropTime = 0.3f;
    // How far the open steps panel tucks up under the title, so the two sit together with no gap between their drawn
    // edges (both pictures have a see-through margin round the outline).
    const float StepsTuck = 28f;
    const int StepChars = 30;      // a step longer than this wraps onto the next line
    const float WrapStep = 30f;    // how far down each wrapped line sits from the one above
    const float TitlePadLeft = 46f, TitlePadRight = 50f, TitleMinWidth = 220f, StepsMinWidth = 240f;
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
    // Between parts: "Objective Completed!" pops up in the middle of the screen, then the next part's list comes in.
    RectTransform puzzleBanner;
    CanvasGroup puzzleGroup;
    float puzzleAge = -1f; // < 0 while it isn't showing
    const float PuzzleTime = 2.4f;
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
    RectTransform bubble;
    CanvasGroup bubbleGroup;
    Text bubbleText;
    readonly List<string> thoughts = new();
    float thoughtAge = 10f, thoughtShow;

    void Awake()
    {
        Instance = this;
        HideObjectives = false;
        spirit = GetComponent<Possession>();
        interactor = GetComponent<PlayerInteractor>();
        Mischief.Reset();
        ParkChaos.Reset();
        Aftermath.Reset();

        if (Camera.main != null && Camera.main.GetComponent<CameraRig>() == null) Camera.main.gameObject.AddComponent<CameraRig>();
        if (Camera.main != null && Camera.main.GetComponent<SeeThrough>() == null) Camera.main.gameObject.AddComponent<SeeThrough>();
        if (FindAnyObjectByType<NightLighting>() == null) new GameObject("Night Lighting").AddComponent<NightLighting>();
        if (GetComponent<ActionOutline>() == null) gameObject.AddComponent<ActionOutline>();
        if (GetComponent<PauseMenu>() == null) gameObject.AddComponent<PauseMenu>();
        if (GetComponent<Tutorial>() == null) gameObject.AddComponent<Tutorial>();
        Build();
        doneQueue.AddRange(pending);
        pending.Clear();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // For the tutorial to point at: the objectives (top left) and the pop-up line (bottom left).
    public RectTransform ObjectivesPanel => (RectTransform)objectives.transform;
    public RectTransform PopUp => doneBody;

    // Keeps `line` up in the pop-up while it's the one showing (the tutorial, while you read about it).
    public void KeepPopUp(string line)
    {
        if (doneText.text != line || doneQueue.Count > 0) return;
        doneAge = Mathf.Min(doneAge, 0.5f);
        doneShow = DoneTime;
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

    // A line the spirit says to itself, in a speech bubble over the player. One at a time: later ones wait their turn.
    public static void Think(params string[] lines)
    {
        if (Instance != null) Instance.thoughts.AddRange(lines);
    }

    // Someone (or something) shouting: a speech bubble over `over` for `seconds`. Any number at once.
    public static void Shout(Transform over, string line, float seconds)
    {
        if (Instance == null || over == null) return;
        var b = Instantiate(Instance.bubble.gameObject, Instance.canvas).GetComponent<RectTransform>();
        b.GetComponentInChildren<Text>().text = line;
        Instance.shouts.Add(new Shouted { bubble = b, group = b.GetComponent<CanvasGroup>(), over = over, life = seconds, until = Time.unscaledTime + seconds });
    }

    class Shouted
    {
        public RectTransform bubble;
        public CanvasGroup group;
        public Transform over;
        public float life, until;
    }

    readonly List<Shouted> shouts = new();

    // ---------------------------------------------------------------- every frame

    void Update()
    {
        UpdateObjectives();
        UpdateStepPopup();
        UpdateGrip();
        UpdateBanners();
    }

    // Anything pinned over something in the world waits for the camera to move this frame: placed in Update it used
    // last frame's camera and lagged a frame behind, stuttering as the camera followed the player.
    void OnEnable() => Unity.Cinemachine.CinemachineCore.CameraUpdatedEvent.AddListener(FollowCamera);
    void OnDisable() => Unity.Cinemachine.CinemachineCore.CameraUpdatedEvent.RemoveListener(FollowCamera);

    void FollowCamera(Unity.Cinemachine.CinemachineBrain brain)
    {
        if (brain.OutputCamera != Camera.main) return;
        UpdatePrompt();
        UpdateBubble();
        UpdateShouts();
    }

    // Pop in, hang over the top of whatever's shouting, fade out.
    void UpdateShouts()
    {
        var cam = Camera.main;
        for (int i = shouts.Count - 1; i >= 0; i--)
        {
            var s = shouts[i];
            float left = s.until - Time.unscaledTime, age = s.life - left;
            if (left <= 0f || s.over == null || cam == null)
            {
                Destroy(s.bubble.gameObject);
                shouts.RemoveAt(i);
                continue;
            }
            var r = s.over.GetComponent<Renderer>();
            Vector3 top = r != null ? new Vector3(r.bounds.center.x, r.bounds.max.y, r.bounds.center.z) : s.over.position;
            Vector3 screen = cam.WorldToScreenPoint(top);
            s.group.alpha = screen.z <= 0f ? 0f : Mathf.Min(Mathf.Clamp01(age / 0.08f), Mathf.Clamp01(left / 0.15f));
            s.bubble.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, age / 0.12f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out Vector2 local);
            s.bubble.anchoredPosition = local;
        }
    }

    void UpdateBubble()
    {
        thoughtAge += Time.unscaledDeltaTime;
        if (thoughtAge >= thoughtShow && thoughts.Count > 0)
        {
            bubbleText.text = thoughts[0];
            thoughtShow = Mathf.Clamp(1.2f + thoughts[0].Length * 0.05f, 2.2f, 4.5f); // long enough to read
            thoughts.RemoveAt(0);
            thoughtAge = 0f;
        }
        bubbleGroup.alpha = Mathf.Min(Mathf.Clamp01(thoughtAge / 0.15f), Mathf.Clamp01((thoughtShow - thoughtAge) / 0.25f));
        if (bubbleGroup.alpha <= 0f) return;

        // Over the host's head, or just above the little spirit.
        var host = spirit.Host;
        Vector3 over = host != null ? PersonTop(host.transform) : spirit.transform.position + Vector3.up * 1.4f;
        var cam = Camera.main;
        Vector3 screen = cam != null ? cam.WorldToScreenPoint(over) : Vector3.back;
        if (screen.z <= 0f) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out Vector2 local);
        bubble.anchoredPosition = local;
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
        // Move on to the next part once every step of this one has been struck through and stayed up a moment, and
        // any cutscene is over (the player can move) and the park tour too. After a numbered part (0 on) the list goes,
        // "Objective Completed!" pops up, then the next part's list comes in; otherwise (and for the first list, or a
        // loaded game's) the next one just comes in.
        int part = ActivePart();
        if (part != shownPart && puzzleAge < 0f && stepRows.TrueForAll(r => r.age >= PartHold))
        {
            if (shownPart < 0) ShowPart(part);
            else if (!Possession.InputBlocked && !HideObjectives)
            {
                if (IsPuzzle(shownPart)) puzzleAge = 0f;
                else ShowPart(part);
            }
        }
        UpdatePuzzleBanner(dt, part);
        bool show = shownPart >= 0 && puzzleAge < 0f && !HideObjectives;
        objectives.alpha = Mathf.MoveTowards(objectives.alpha, show ? 1f : 0f, dt * 3f);
        UpdateDrop(dt);

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
            // Done: a line is drawn through it (through every line of it), and it stays on the list.
            r.age += dt;
            float strike = 1f - Mathf.Pow(1f - Mathf.Clamp01(r.age / 0.35f), 3f);
            for (int i = 0; i < r.strikes.Length; i++) r.strikes[i].sizeDelta = new Vector2(r.widths[i] * strike, 3f);
        }
    }

    // The numbered parts ("0. Ferris Wheel Chaos", "1. A-Leak-Tricity"...): every part but the tutorial's.
    static bool IsPuzzle(int part)
    {
        string title = Mischief.Parts[part].title;
        int dot = title.IndexOf(". ");
        return dot > 0 && int.TryParse(title[..dot], out int n) && n >= 0;
    }

    // Pops in (overshooting a little), stays, fades; then the next part.
    void UpdatePuzzleBanner(float dt, int part)
    {
        if (puzzleAge < 0f)
        {
            puzzleGroup.alpha = 0f;
            return;
        }
        puzzleAge += dt;
        float pop = Mathf.Clamp01(puzzleAge / 0.35f) - 1f;
        puzzleBanner.localScale = Vector3.one * (1f + 2.7f * pop * pop * pop + 1.7f * pop * pop); // ease out, overshooting
        puzzleGroup.alpha = Mathf.Min(Mathf.Clamp01(puzzleAge / 0.15f), Mathf.Clamp01((PuzzleTime - puzzleAge) / 0.4f));
        if (puzzleAge < PuzzleTime) return;
        puzzleAge = -1f;
        puzzleGroup.alpha = 0f;
        ShowPart(part);
    }

    // Click the title (or press Tab) and the steps slide down out from under it, or back up. The title gives a
    // little squash as it's pressed.
    void UpdateDrop(float dt)
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame
                       && RectTransformUtility.RectangleContainsScreenPoint(objectiveBanner, mouse.position.ReadValue(), null);
        if (objectives.alpha > 0.5f && !PauseMenu.Paused && (clicked || (kb != null && kb.tabKey.wasPressedThisFrame)))
        {
            stepsOpen = !stepsOpen;
            bannerPop = 0f;
        }
        stepsShown = Mathf.MoveTowards(stepsShown, stepsOpen ? 1f : 0f, dt / DropTime);
        float eased = stepsShown * stepsShown * (3f - 2f * stepsShown);
        stepsDrop.preferredWidth = LayoutUtility.GetPreferredWidth(stepList);
        stepsDrop.preferredHeight = LayoutUtility.GetPreferredHeight(stepList) * eased;
        stepsGroup.alpha = eased;
        objectivesColumn.spacing = -StepsTuck * eased; // closed, nothing tucks under the title
        bannerPop = Mathf.Min(1f, bannerPop + dt / 0.25f);
        objectiveBanner.localScale = Vector3.one * (1f - 0.06f * Mathf.Sin(bannerPop * Mathf.PI));
    }

    void ShowPart(int part)
    {
        shownPart = part;
        if (part < 0) return; // all done: the panel fades away, last list and all
        stepsOpen = true; // a new part's steps always drop down, even if the last ones were closed
        foreach (var r in stepRows) Destroy(r.slot.gameObject);
        stepRows.Clear();
        var (title, steps) = Mischief.Parts[part];
        int dot = title.IndexOf(". ");
        objectiveTitle.text = dot >= 0 && dot < 3 ? title[(dot + 2)..] : title; // "1. A-leak-tricity" -> "A-leak-tricity"
        bannerSize.preferredWidth = Mathf.Max(TitleMinWidth, TitlePadLeft + objectiveTitle.preferredWidth + TitlePadRight);
        foreach (var (task, label) in steps)
        {
            var slot = Ui(task.ToString(), stepList);
            var body = Ui("Body", slot);
            Stretch(body);
            var group = body.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            // One line each, StepChars at most; the lines after the first line up under its text, past the "› ".
            var lines = Wrap(label, StepChars);
            var strikes = new RectTransform[lines.Count];
            var widths = new float[lines.Count];
            float indent = 0f, widest = 0f;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = Ui("Line " + (i + 1), body);
                var text = Txt(line, i == 0 ? "› " + lines[i] : lines[i], 26, Ink);
                text.font = HandFont;
                if (i == 0 && lines.Count > 1) indent = Measure(text, "› " + lines[1][0]) - Measure(text, lines[1][0].ToString());
                float x = i == 0 ? 0f : indent;
                Place(line, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -i * WrapStep), new Vector2(text.preferredWidth, StepHeight));
                var strike = Ui("Strike", line);
                Place(strike, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -1f), new Vector2(0f, 3f));
                Img(strike, null, Ink);
                strikes[i] = strike;
                widths[i] = text.preferredWidth;
                widest = Mathf.Max(widest, x + text.preferredWidth);
            }

            var element = slot.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = widest;
            element.preferredHeight = StepHeight + (lines.Count - 1) * WrapStep;
            stepRows.Add(new StepRow { task = task, slot = element, body = body, strikes = strikes, widths = widths, group = group });
        }
    }

    // Breaks `s` into lines of at most `max` characters, between words (a word longer than that is cut).
    static List<string> Wrap(string s, int max)
    {
        var lines = new List<string>();
        string line = "";
        foreach (var word in s.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
        {
            string w = word;
            while (w.Length > max)
            {
                if (line.Length > 0) lines.Add(line);
                lines.Add(w[..max]);
                line = "";
                w = w[max..];
            }
            if (line.Length == 0) line = w;
            else if (line.Length + 1 + w.Length <= max) line += " " + w;
            else
            {
                lines.Add(line);
                line = w;
            }
        }
        if (line.Length > 0 || lines.Count == 0) lines.Add(line);
        return lines;
    }

    // How wide `s` comes out in `t`'s font and size, in HUD units.
    static float Measure(Text t, string s) =>
        t.cachedTextGeneratorForLayout.GetPreferredWidth(s, t.GetGenerationSettings(Vector2.zero)) / t.pixelsPerUnit;

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
                key = "J";
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
                key = blocked == null ? "K" : "";
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

    void UpdateBanners()
    {
        if (intro == null) return; // the intro banner's switched off (BuildIntro)
        float dt = Time.unscaledDeltaTime;
        introAge += dt;
        if (spirit.Host != null && introAge < 9f) introAge = 9f; // got the idea
        if (Tutorial.Active) introAge = 0f; // the intro banner waits for the tutorial, then plays
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
        BuildBubble();
        BuildPuzzleBanner();
    }

    // The middle of the screen, a little above the player: the objectives' title art, bigger, with the words.
    void BuildPuzzleBanner()
    {
        puzzleBanner = Ui("Objective Completed", canvas);
        Img(puzzleBanner, "UI/Buttons/objective_main", Color.white).pixelsPerUnitMultiplier = 2f;
        var label = Ui("Label", puzzleBanner);
        Stretch(label);
        label.offsetMin = new Vector2(0f, 6f);
        var text = Txt(label, GameText.ObjectiveCompleted, 64, Ink, TextAnchor.MiddleCenter, true);
        text.font = HandFont;
        Place(puzzleBanner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(text.preferredWidth + 2f * 90f, 150f));
        puzzleGroup = puzzleBanner.gameObject.AddComponent<CanvasGroup>();
        puzzleGroup.alpha = 0f;
    }

    // A light speech bubble with a little tail, its point at `bubble`'s position.
    void BuildBubble()
    {
        bubble = Ui("Speech Bubble", canvas);
        Place(bubble, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(10f, 10f));
        bubbleGroup = bubble.gameObject.AddComponent<CanvasGroup>();
        bubbleGroup.alpha = 0f;
        var tail = Ui("Tail", bubble);
        Place(tail, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(18f, 18f));
        tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Img(tail, null, Soft);
        var body = Ui("Body", bubble);
        Place(body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), Vector2.zero);
        Img(body, "UI/pill", Soft);
        Row(body, 0f, new RectOffset(26, 26, 10, 12));
        Fit(body);
        bubbleText = Txt(Ui("Label", body), "", 30, Ink, TextAnchor.MiddleCenter);
        bubbleText.font = HandFont;
    }

    // Top left: a hand-drawn title (the part you're on, UI/Buttons/objective_main) with a yellow panel of its steps
    // (UI/Buttons/objective_steps) that drops down from under it. Both are sliced, so their drawn edges keep their
    // shape whatever size the text makes them.
    void BuildObjectives()
    {
        var column = Ui("Objectives", canvas);
        Place(column, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -40f), Vector2.zero);
        Column(column, 0f, new RectOffset());
        Fit(column);

        var panel = Ui("Panel", column);
        // The steps tuck up under the title (negative spacing, see UpdateDrop), so the title has to draw over them: it's
        // the last child, and the column lays its children out last to first to keep it on top of the list.
        objectivesColumn = Column(panel, 0f, new RectOffset());
        objectivesColumn.reverseArrangement = true;
        objectives = panel.gameObject.AddComponent<CanvasGroup>();
        objectives.alpha = 0f;

        var banner = Ui("Title", panel);
        Img(banner, "UI/Buttons/objective_main", Color.white).pixelsPerUnitMultiplier = 3.2f; // edges drawn ~50 wide
        bannerSize = banner.gameObject.AddComponent<LayoutElement>();
        bannerSize.preferredWidth = TitleMinWidth; // fitted to the title's text in ShowPart
        bannerSize.preferredHeight = 92f;
        var title = Ui("Label", banner);
        Stretch(title);
        title.offsetMin = new Vector2(TitlePadLeft, 4f);
        objectiveTitle = Txt(title, "", 38, Ink, TextAnchor.MiddleLeft, true);
        objectiveTitle.font = HandFont;
        objectiveBanner = banner;

        // The steps hang from the bottom of a box that grows and shrinks (and clips them), so they slide out from
        // under the title.
        var drop = Ui("Drop", panel);
        drop.gameObject.AddComponent<RectMask2D>();
        stepsDrop = drop.gameObject.AddComponent<LayoutElement>();
        stepList = Ui("Steps", drop);
        stepList.anchorMin = Vector2.zero;
        stepList.anchorMax = new Vector2(1f, 0f);
        stepList.pivot = new Vector2(0.5f, 0f);
        stepList.anchoredPosition = stepList.sizeDelta = Vector2.zero;
        // The picture on a child the layout skips: as the list's own Image it would ask for its full drawn size.
        var art = Ui("Art", stepList);
        Stretch(art);
        art.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        Img(art, "UI/Buttons/objective_steps", Color.white).pixelsPerUnitMultiplier = 5f; // corners ~56 wide
        // A little more room at the top than at the bottom: its top StepsTuck hides under the title.
        Column(stepList, 4f, new RectOffset(48, 44, 36, 34));
        stepList.gameObject.AddComponent<LayoutElement>().minWidth = StepsMinWidth; // otherwise as wide as its steps
        stepList.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        stepsGroup = stepList.gameObject.AddComponent<CanvasGroup>();
        banner.SetAsLastSibling(); // drawn over the steps tucked under it (laid out first all the same, see above)

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
        var cap = KeyCap(body, "K", new Key[0], false, 40f);
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
        var icon = Icon(banner, "UI/spirit", 46f);
        if (spiritIcon != null) icon.sprite = spiritIcon;
        Txt(Ui("Text", banner), $"You are an evil spirit loose in a theme park. <b>{objective}</b>.", 26, new Color(0.98f, 0.9f, 1f));
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
