using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The HUD, built at runtime: the objectives (top left: the part of the level you're on on a yellow strip, and its
// steps as bullets on the pink panel under it; a step that gets done is struck through and stays on the list), the
// events (top right: a line for each thing the player does and each step done, the wording all in GameText), a
// prompt floating over whatever you can possess / use (and why not, when E is refused), and
// "Saving..." (bottom right) after Save Game. Titles are in Atma (ParkArt.TitleFont), the rest in Arvo.
// It also gives the main camera its rig and the spirit its outline of what it can act on.
public class GameHud : MonoBehaviour
{
    public static GameHud Instance { get; private set; }
    public static bool HideObjectives; // the objectives (top left) and the key prompts stay out of the way (ParkTour, the finale)


    static readonly Color PanelDark = new(0.12f, 0.08f, 0.12f, 0.8f);
    static readonly Color Ink = Color.black; // as in the design
    static readonly Color Soft = new(0.96f, 0.92f, 0.88f);
    static readonly List<string> pending = new();

    // A step in the objectives list. age < 0 until it's done; then a line is drawn through it and it stays put.
    // A long step wraps onto more lines (StepWrap wide at most, so it stays inside the panel), each struck
    // through on its own.
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
    LayoutElement bannerSize, stepsSize; // both ObjectiveWidth, or wider for a long title
    VerticalLayoutGroup objectivesColumn;
    // The steps drop down from under the title: click the title or press Tab to open or close them.
    LayoutElement stepsDrop;
    CanvasGroup stepsGroup;
    bool stepsOpen = true;
    float stepsShown = 1f, bannerPop = 1f;
    const float DropTime = 0.3f;
    // How far the open steps panel tucks up under the title, so the two sit together with no gap between their drawn
    // edges (both pictures have a see-through margin round the outline).
    // Sizes measured off the design: the title strip 540 x 104, the steps panel under it 11 narrower and 4 in from the
    // left, its text 24 (the title's 29), a line every 36, wrapping before StepWrap (from the bullet).
    const float StepsTuck = 11f;
    const float StepWrap = 438f;   // a line of a step (bullet and all) is no wider than this
    const float WrapStep = 36f;    // how far down each wrapped line sits from the one above
    const float ObjectiveWidth = 540f, TitleHeight = 104f, TitlePad = 34f, StepsNarrower = 11f, StepsInset = 4f;
    // The objective's name in Atma at the design's 24 (under the type scale's smallest Atma, as the design draws it);
    // the steps in Arvo Body.
    static readonly int TitleSize = Mathf.RoundToInt(32f * Figma.K), StepSize = Figma.Type.ArvoBody;
    readonly List<StepRow> stepRows = new();
    int shownPart = -2; // index into Mischief.Parts, -1 when every step is done
    const float StepHeight = 36f;
    // The events, top right: newest at the bottom, each up EventTime seconds, MaxEvents at most (the oldest goes).
    class Event
    {
        public Text text;
        public CanvasGroup group;
        public float age;
        public int frame;
    }
    readonly List<Event> events = new();
    RectTransform eventList;
    int actionFrame = -1; // the frame the last event was added by Say (a step done then takes its place)
    const float EventTime = 10f, EventFade = 0.5f;
    const int MaxEvents = 3;
    // "Saving..." bottom right, on its own canvas over the pause menu.
    CanvasGroup saving;
    Image savingSpinner;
    static Sprite[] savingFrames;
    float savingAge = SavingTime;
    const float SavingTime = 1.6f;
    const float PartHold = 1.5f; // seconds the finished part's struck-through list stays up before the next part
    // Between parts: "Objective Completed!" pops up in the middle of the screen, then the next part's list comes in.
    RectTransform puzzleBanner;
    CanvasGroup puzzleGroup;
    Text puzzleWords; // "<the part's title> COMPLETED" on the cloud
    float puzzleAge = -1f; // < 0 while it isn't showing
    const float PuzzleTime = 2.4f;
    Text gripLabel;
    GameObject gripRow;
    RectTransform prompt;
    GameObject promptCap;
    KeyCap promptKey;
    Text promptText;
    Transform refusedBy;
    bool refusedPerson;
    string refusal;
    float refusedAt = -10f;
    const float RefusalTime = 1.6f;
    RectTransform bubble;
    CanvasGroup bubbleGroup;
    Text bubbleText;
    // Spirit thoughts: priority queue (at most one waiting). Critical/Action can interrupt a lower line on screen.
    public enum ThoughtPriority { Observe = 0, Action = 1, Critical = 2 }
    struct Thought
    {
        public string line;
        public ThoughtPriority priority;
    }
    readonly List<Thought> thoughts = new();
    ThoughtPriority showingPriority;
    bool showing;
    float thoughtAge = 10f, thoughtShow;

    void Awake()
    {
        Instance = this;
        HideObjectives = false;
        spirit = GetComponent<Possession>();
        interactor = GetComponent<PlayerInteractor>();
        Mischief.Reset();
        SaveGame.AutoSave = false;
        ParkChaos.Reset();
        Aftermath.Reset();

        if (Camera.main != null && Camera.main.GetComponent<CameraRig>() == null) Camera.main.gameObject.AddComponent<CameraRig>();
        if (Camera.main != null && Camera.main.GetComponent<SeeThrough>() == null) Camera.main.gameObject.AddComponent<SeeThrough>();
        if (FindAnyObjectByType<NightLighting>() == null) new GameObject("Night Lighting").AddComponent<NightLighting>();
#if UNITY_EDITOR
        // Editor-only: F1 overlay for tuning the moon and lamp posts without leaving Play mode.
        if (FindAnyObjectByType<LightingDebugMenu>() == null)
            new GameObject("Lighting Debug Menu").AddComponent<LightingDebugMenu>();
#endif
        if (FindAnyObjectByType<ParkAnimations>() == null) new GameObject("Park Animations").AddComponent<ParkAnimations>();
        if (GetComponent<ActionOutline>() == null) gameObject.AddComponent<ActionOutline>();
        if (GetComponent<PauseMenu>() == null) gameObject.AddComponent<PauseMenu>();
        if (GetComponent<Tutorial>() == null) gameObject.AddComponent<Tutorial>();
        if (GetComponent<ParkCrowd>() == null) gameObject.AddComponent<ParkCrowd>(); // the crowd all round you
        if (GetComponent<PossessedMusic>() == null) gameObject.AddComponent<PossessedMusic>(); // the theme slows in a body
        Build();
        foreach (var line in pending) AddEvent(line);
        pending.Clear();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // For the tutorial to point at: the objectives (top left) and the events (top right).
    public RectTransform ObjectivesPanel => (RectTransform)objectives.transform;
    public RectTransform PopUp => eventList;

    // Keeps `line` up in the events while it's there (the tutorial, while you read about it).
    public void KeepPopUp(string line)
    {
        foreach (var e in events)
            if (e.text.text == line) e.age = Mathf.Min(e.age, 0.5f);
    }

    // An event (top right) for something the player just did.
    public static void Say(string text)
    {
        if (Instance == null)
        {
            pending.Add(text);
            return;
        }
        Instance.AddEvent(text);
        Instance.actionFrame = Time.frameCount;
    }

    // The event for a step that just got done. If what the player did this very frame is what did it, this line
    // takes the place of that one: one thing happened, one line.
    public static void StepDone(string text)
    {
        if (Instance == null)
        {
            pending.Add(text);
            return;
        }
        var list = Instance.events;
        if (Instance.actionFrame == Time.frameCount && list.Count > 0 && list[^1].frame == Time.frameCount) list[^1].text.text = text;
        else Instance.AddEvent(text);
        Instance.actionFrame = -1;
    }

    // "Saving..." bottom right for a moment (the pause menu's Save Game, once it's saved).
    public static void Saving()
    {
        if (Instance != null) Instance.savingAge = 0f;
    }

    // Why pressing E didn't work, floated over whatever said no for a moment (person: someone you tried to possess).
    public static void Refuse(Transform by, string why, bool person = false)
    {
        if (Instance == null || by == null || string.IsNullOrEmpty(why)) return;
        AudioManager.TryPlay(e => e.disabledAction); // G-06
        Instance.refusedBy = by;
        Instance.refusedPerson = person;
        Instance.refusal = why;
        Instance.refusedAt = Time.unscaledTime;
    }

    // A line the spirit says to itself, in a speech bubble over the player. Priority decides interrupt vs queue:
    // Observe (flavor) never stacks and yields to Action/Critical; Critical/Action can cut in over a lower line.
    // A single call may pass several lines (e.g. electrocute); those play in order without Observe-style backlog.
    public static void Think(params string[] lines) => Think(ThoughtPriority.Observe, lines);

    public static void Think(ThoughtPriority priority, params string[] lines)
    {
        if (Instance == null || lines == null) return;
        var batch = new List<string>(lines.Length);
        foreach (var line in lines)
            if (!string.IsNullOrEmpty(line)) batch.Add(line);
        if (batch.Count > 0) Instance.EnqueueThoughts(batch, priority);
    }

    void EnqueueThoughts(List<string> lines, ThoughtPriority priority)
    {
        // Higher than what's on screen: cut in on the first line; keep the rest of this batch waiting.
        if (showing && priority > showingPriority)
        {
            thoughts.Clear();
            ShowThought(lines[0], priority);
            for (int i = 1; i < lines.Count; i++)
                thoughts.Add(new Thought { line = lines[i], priority = priority });
            return;
        }

        // Observe: never stack. Newer Observe replaces an older one on screen or waiting; still yields to Action/Critical.
        if (priority == ThoughtPriority.Observe)
        {
            if (showing && showingPriority > ThoughtPriority.Observe) return;
            if (thoughts.Count > 0 && thoughts[0].priority > ThoughtPriority.Observe) return;
            if (showing && showingPriority == ThoughtPriority.Observe)
            {
                thoughts.Clear();
                ShowThought(lines[0], priority);
                return;
            }
            thoughts.Clear();
            thoughts.Add(new Thought { line = lines[0], priority = priority });
            return;
        }

        // Action/Critical: never lose to a lower waiting line; on a tie, newer batch replaces waiting.
        if (thoughts.Count > 0 && priority < thoughts[0].priority) return;
        thoughts.Clear();
        foreach (var line in lines)
            thoughts.Add(new Thought { line = line, priority = priority });
        // Cap waiting: if something is already on screen, only one slot; if idle, allow the whole short batch (max 2).
        int cap = showing ? 1 : 2;
        while (thoughts.Count > cap) thoughts.RemoveAt(thoughts.Count - 1);
    }

    void ShowThought(string line, ThoughtPriority priority)
    {
        bubbleText.text = line;
        thoughtShow = Mathf.Clamp(1.2f + line.Length * 0.05f, 2.2f, 4.5f);
        thoughtAge = 0f;
        showingPriority = priority;
        showing = true;
        AudioManager.TryPlay(e => e.chatBubble); // RU-01, as each line pops up
    }

    // Someone (or something) shouting: a speech bubble over `over` for `seconds`. Any number at once.
    // ambient: background chatter (gossip, "move along") that "Objective Completed!" doesn't wait for.
    public static void Shout(Transform over, string line, float seconds, bool ambient = false)
    {
        if (Instance == null || over == null) return;
        var b = Instantiate(Instance.bubble.gameObject, Instance.canvas).GetComponent<RectTransform>();
        b.GetComponentInChildren<Text>().text = line;
        Instance.shouts.Add(new Shouted { bubble = b, group = b.GetComponent<CanvasGroup>(), over = over, life = seconds, until = Time.unscaledTime + seconds, ambient = ambient });
    }

    // Takes down whatever `over` is saying now, so a new line replaces it instead of stacking.
    public static void Hush(Transform over)
    {
        if (Instance == null) return;
        Instance.shouts.RemoveAll(s =>
        {
            if (s.over != over) return false;
            Destroy(s.bubble.gameObject);
            return true;
        });
    }

    class Shouted
    {
        public RectTransform bubble;
        public CanvasGroup group;
        public Transform over;
        public float life, until;
        public bool ambient;
    }

    // Someone's still talking: the spirit's thoughts (shown or waiting), or a shout that isn't background chatter.
    bool Talking => thoughts.Count > 0 || bubbleGroup.alpha > 0f || shouts.Exists(s => !s.ambient);

    // How long everything's been quiet: no cutscene, no tour, the camera back on the player, nobody talking. The next
    // part (and "Objective Completed!") waits for SettleTime of it.
    float calm;
    const float SettleTime = 0.6f;

    readonly List<Shouted> shouts = new();

    // ---------------------------------------------------------------- every frame

    void Update()
    {
        UpdateObjectives();
        UpdateEvents();
        UpdateAutoSave();
        UpdateSaving();
        UpdateGrip();
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
            Vector3 screen = cam.WorldToScreenPoint(Top(s.over));
            s.group.alpha = screen.z <= 0f ? 0f : Mathf.Min(Mathf.Clamp01(age / 0.08f), Mathf.Clamp01(left / 0.15f));
            s.bubble.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, age / 0.12f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out Vector2 local);
            s.bubble.anchoredPosition = local;
        }
    }

    // Over the top of whatever's drawn there: everything showing in it and its children (a person's body and head are
    // on their Pose child, not the root), or head height over an empty marker.
    static Vector3 Top(Transform t)
    {
        Bounds? drawn = null;
        foreach (var r in t.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!r.enabled || r.sprite == null) continue;
            if (drawn is Bounds b) { b.Encapsulate(r.bounds); drawn = b; }
            else drawn = r.bounds;
        }
        return drawn is Bounds all ? new Vector3(all.center.x, all.max.y, all.center.z) : PersonTop(t);
    }

    void UpdateBubble()
    {
        if (Tutorial.CardShowing) // the tutorial's card is talking: the spirit keeps its thoughts to itself
        {
            thoughts.Clear();
            showing = false;
            thoughtAge = thoughtShow;
            bubbleGroup.alpha = 0f;
            return;
        }
        thoughtAge += Time.unscaledDeltaTime;
        if (thoughtAge >= thoughtShow)
        {
            showing = false;
            if (thoughts.Count > 0)
            {
                var next = thoughts[0];
                thoughts.RemoveAt(0);
                ShowThought(next.line, next.priority);
            }
        }
        bubbleGroup.alpha = showing
            ? Mathf.Min(Mathf.Clamp01(thoughtAge / 0.15f), Mathf.Clamp01((thoughtShow - thoughtAge) / 0.25f))
            : Mathf.MoveTowards(bubbleGroup.alpha, 0f, Time.unscaledDeltaTime / 0.25f);
        if (bubbleGroup.alpha <= 0f) return;

        // Over the host's head, or just above the little spirit.
        var host = spirit.Host;
        Vector3 over = host != null ? PersonTop(host.transform) : spirit.transform.position + Vector3.up * 0.9f;
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
        // everything has settled (calm): the cutscene and the park tour over, the camera back from its zooms and shots,
        // the dialog done. After a numbered part (0 on) the list goes,
        // "Objective Completed!" pops up, then the next part's list comes in; otherwise (and for the first list, or a
        // loaded game's) the next one just comes in.
        int part = ActivePart();
        bool quiet = !Possession.InputBlocked && !HideObjectives && !CameraRig.Busy && !Talking;
        calm = quiet ? calm + dt : 0f;
        if (part != shownPart && puzzleAge < 0f && stepRows.TrueForAll(r => r.age >= PartHold))
        {
            if (shownPart < 0) ShowPart(part);
            else if (calm >= SettleTime)
            {
                if (IsPuzzle(shownPart))
                {
                    SetPuzzleWords(PartTitle(shownPart));
                    puzzleAge = 0f;
                    AudioManager.TryPlayOverOthers(e => e.objectiveCompleted); // G-07, everything else turned down
                }
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
        var mouse = Mouse.current;
        bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame
                       && RectTransformUtility.RectangleContainsScreenPoint(objectiveBanner, mouse.position.ReadValue(), null);
        if (objectives.alpha > 0.5f && !PauseMenu.Paused && (clicked || Controls.Objectives)) // Tab
        {
            stepsOpen = !stepsOpen;
            bannerPop = 0f;
        }
        stepsShown = Mathf.MoveTowards(stepsShown, stepsOpen ? 1f : 0f, dt / DropTime);
        float eased = stepsShown * stepsShown * (3f - 2f * stepsShown);
        stepsDrop.preferredWidth = StepsInset + LayoutUtility.GetPreferredWidth(stepList);
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
        var (_, steps) = Mischief.Parts[part];
        objectiveTitle.text = PartTitle(part);
        bannerSize.preferredWidth = Mathf.Max(ObjectiveWidth, 2f * TitlePad + objectiveTitle.preferredWidth);
        stepsSize.minWidth = bannerSize.preferredWidth - StepsNarrower;
        foreach (var (task, label) in steps)
        {
            var slot = Ui(task.ToString(), stepList);
            var body = Ui("Body", slot);
            Stretch(body);
            var group = body.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            // One line each, StepWrap wide at most; the lines after the first line up under its text, past the "• ".
            var lines = Wrap(label, StepWrap - TextWidth("• ", StepSize));
            var strikes = new RectTransform[lines.Count];
            var widths = new float[lines.Count];
            float indent = 0f, widest = 0f;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = Ui("Line " + (i + 1), body);
                var text = Txt(line, i == 0 ? "• " + lines[i] : lines[i], StepSize, Ink);
                if (i == 0 && lines.Count > 1) indent = Measure(text, "• " + lines[1][0]) - Measure(text, lines[1][0].ToString());
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

    // Breaks `s` into lines no wider than `max` (in the steps' font and size), between words.
    static List<string> Wrap(string s, float max)
    {
        var lines = new List<string>();
        string line = "";
        foreach (var word in s.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length == 0) line = word;
            else if (TextWidth(line + " " + word, StepSize) <= max) line += " " + word;
            else
            {
                lines.Add(line);
                line = word;
            }
        }
        if (line.Length > 0 || lines.Count == 0) lines.Add(line);
        return lines;
    }

    static readonly TextGenerator measurer = new();

    // How wide `s` comes out in the ordinary font (Arvo) at `size`, in HUD units.
    static float TextWidth(string s, int size) => measurer.GetPreferredWidth(s, new TextGenerationSettings
    {
        font = ParkArt.Font,
        fontSize = size,
        scaleFactor = 1f,
        horizontalOverflow = HorizontalWrapMode.Overflow,
        verticalOverflow = VerticalWrapMode.Overflow,
        generationExtents = new Vector2(5000f, 500f),
        pivot = Vector2.zero,
        color = Color.white,
        lineSpacing = 1f,
        richText = false,
        textAnchor = TextAnchor.UpperLeft,
    });

    // How wide `s` comes out in `t`'s font and size, in HUD units.
    static float Measure(Text t, string s) =>
        t.cachedTextGeneratorForLayout.GetPreferredWidth(s, t.GetGenerationSettings(Vector2.zero)) / t.pixelsPerUnit;

    void AddEvent(string line)
    {
        if (events.Count >= MaxEvents)
        {
            Destroy(events[0].group.gameObject);
            events.RemoveAt(0);
        }
        var row = Ui("Event", eventList);
        var group = row.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        var text = Txt(row, line, Figma.Type.ArvoSubtitle2, Color.white, TextAnchor.MiddleRight);
        var shadow = row.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
        events.Add(new Event { text = text, group = group, frame = Time.frameCount });
    }

    // Each fades in, stays EventTime seconds, fades out and goes.
    void UpdateEvents()
    {
        for (int i = events.Count - 1; i >= 0; i--)
        {
            var e = events[i];
            e.age += Time.unscaledDeltaTime;
            e.group.alpha = Mathf.Min(Mathf.Clamp01(e.age / 0.2f), Mathf.Clamp01((EventTime - e.age) / EventFade));
            if (e.age < EventTime) continue;
            Destroy(e.group.gameObject);
            events.RemoveAt(i);
        }
    }

    // A step's been crossed off: save once nothing's in the way, with a picture of the park (no HUD) for its card.
    void UpdateAutoSave()
    {
        if (!SaveGame.AutoSave || SaveGame.Loading || !SaveGame.CanSave || Tutorial.Active || PauseMenu.Paused || Possession.InputBlocked) return;
        var shot = ParkShot();
        if (SaveGame.Save(shot)) Saving();
        if (shot != null) Destroy(shot);
    }

    // What the main camera sees, without the HUD.
    static Texture2D ParkShot()
    {
        var cam = Camera.main;
        if (cam == null) return null;
        const int w = 960, h = 540;
        var rt = RenderTexture.GetTemporary(w, h, 24);
        var old = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = old;
        var was = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = was;
        RenderTexture.ReleaseTemporary(rt);
        return tex;
    }

    void UpdateSaving()
    {
        savingAge += Time.unscaledDeltaTime;
        saving.alpha = Mathf.Min(Mathf.Clamp01(savingAge / 0.15f), Mathf.Clamp01((SavingTime - savingAge) / 0.3f));
        if (saving.alpha > 0f && savingFrames[0] != null) savingSpinner.sprite = savingFrames[(int)(Time.unscaledTime * 12f) % savingFrames.Length];
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

        if (HideObjectives) { } // a cutscene's showing: no prompts over it
        else if (refusedBy != null && Time.unscaledTime - refusedAt < RefusalTime)
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
                key = KeyBindings.Name(KeyBindings.Bind.Possess);
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
                key = blocked == null ? KeyBindings.Name(KeyBindings.Bind.Interact) : "";
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
        promptKey.SetKey(key);
        promptText.text = text;
    }

    // Where the floating prompt goes: over the head of someone you could possess, or over the top of a thing.
    static Vector3 PersonTop(Transform t) => t.position + Vector3.up * 2f;

    static Vector3 ThingTop(Transform t)
    {
        var r = t.GetComponent<SpriteRenderer>();
        return r != null ? new Vector3(t.position.x, r.bounds.max.y + 0.4f, t.position.z) : t.position + Vector3.up * 2f;
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
        BuildEvents();
        BuildSaving();
        BuildPrompt();
        BuildBubble();
        BuildPuzzleBanner();
    }

    // A part's title as the player reads it: "1. A-leak-tricity" -> "A-leak-tricity".
    static string PartTitle(int part)
    {
        string title = Mischief.Parts[part].title;
        int dot = title.IndexOf(". ");
        return dot >= 0 && dot < 3 ? title[(dot + 2)..] : title;
    }

    // The middle of the screen, a little above the player: the done part's title and "COMPLETED" on the artist's cloud.
    void BuildPuzzleBanner()
    {
        // The cloud of the artist's "OBJECTIVE COMPLETED" without its words (UI Asset/Logo/Mission_cloud, the same
        // picture), where the design (Figma: Objective completed) has it, 85% of the design's size, same middle.
        puzzleBanner = Figma.Picture(canvas, "Objective Completed", "Logo/Mission_cloud", 780.3f, 532.8f, 736.4f, 621.6f).rectTransform;
        puzzleGroup = puzzleBanner.gameObject.AddComponent<CanvasGroup>();
        puzzleGroup.alpha = 0f;
        // The words over the cloud's body (x 120..1130, y 230..830 of its 1301 x 1098 picture: its middle, a touch
        // below the picture's, where the cloud is fullest), as the art writes them:
        // yellow capitals in Atma, a thick dark line round them and a drop shadow.
        var words = Ui("Words", puzzleBanner);
        words.anchorMin = new Vector2(120f / 1301f, (1098f - 830f) / 1098f);
        words.anchorMax = new Vector2(1130f / 1301f, (1098f - 230f) / 1098f);
        words.offsetMin = words.offsetMax = Vector2.zero;
        puzzleWords = words.gameObject.AddComponent<Text>();
        puzzleWords.font = ParkArt.TitleFont;
        puzzleWords.alignment = TextAnchor.MiddleCenter;
        puzzleWords.color = new Color32(254, 208, 62, 255);
        puzzleWords.lineSpacing = 0.85f;
        puzzleWords.horizontalOverflow = HorizontalWrapMode.Overflow;
        puzzleWords.verticalOverflow = VerticalWrapMode.Overflow;
        puzzleWords.raycastTarget = false;
        var line = words.gameObject.AddComponent<Outline>();
        line.effectColor = new Color32(52, 27, 56, 255);
        line.effectDistance = new Vector2(4f, 4f);
        var shadow = words.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color32(52, 27, 56, 255);
        shadow.effectDistance = new Vector2(3f, -7f);
    }

    // "<TITLE>\nCOMPLETED" (GameText.ObjectiveCompleted), at the biggest of the title sizes that fits across the cloud.
    void SetPuzzleWords(string title)
    {
        puzzleWords.text = GameText.ObjectiveCompleted(title).ToUpperInvariant();
        float room = puzzleWords.rectTransform.rect.width;
        foreach (int size in new[] { Figma.Type.AtmaPage, Figma.Type.AtmaH1, Figma.Type.AtmaSpecial, Figma.Type.AtmaH2, Figma.Type.AtmaBody })
        {
            puzzleWords.fontSize = size;
            if (puzzleWords.preferredWidth <= room) break;
        }
    }

    // The hand-drawn speech bubble (thoughts, shouts), its tail's tip at `bubble`'s position.
    void BuildBubble()
    {
        bubble = Ui("Speech Bubble", canvas);
        Place(bubble, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(10f, 10f));
        bubbleGroup = bubble.gameObject.AddComponent<CanvasGroup>();
        bubbleGroup.alpha = 0f;
        var body = Ui("Body", bubble);
        Place(body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
        BubbleArt(body);
        Row(body, 0f, new RectOffset(BubblePad.left, BubblePad.right, BubblePad.top, BubblePad.bottom));
        Fit(body);
        bubbleText = Txt(Ui("Label", body), "", Figma.Type.ArvoSubtitle, Ink, TextAnchor.MiddleCenter);
    }

    // The speech bubble's art (UI/Bubble/dialog_box_left and _right, cut from the artist's dialog box down the middle
    // of its tail): two sliced halves side by side, so the tail keeps its shape however wide the line makes the bubble.
    // The tip is at the bottom middle of `body`; `up`: the artist's other one (dialog_box_up_*), its tail on top.
    // BubblePad / BubblePadUp: room round the words, inside the drawn line (and clear of the tail). Shared with the
    // tutorial's card.
    public static RectOffset BubblePad => new(30, 30, 22, 36); // made when asked: Unity won't build one in a static initializer
    public static RectOffset BubblePadUp => new(30, 30, 36, 22);
    const float BubbleArtScale = 3f; // the art is drawn big: a third of its pixels on screen

    public static void BubbleArt(RectTransform body, bool up = false)
    {
        string art = up ? "UI/Bubble/dialog_box_up_" : "UI/Bubble/dialog_box_";
        for (int i = 1; i >= 0; i--)
        {
            var half = Ui(i == 0 ? "Left" : "Right", body);
            half.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            half.anchorMin = new Vector2(i * 0.5f, 0f);
            half.anchorMax = new Vector2(0.5f + i * 0.5f, 1f);
            half.offsetMin = half.offsetMax = Vector2.zero;
            Img(half, art + (i == 0 ? "left" : "right"), Color.white).pixelsPerUnitMultiplier = BubbleArtScale;
            half.SetAsFirstSibling(); // behind the words
        }
    }

    // Top left: a hand-drawn yellow title strip (the part you're on, UI/Menu/main_objective) with a pink panel of its
    // steps (UI/Menu/side_objective) that drops down from under it, both ObjectiveWidth wide. Both are sliced, so their
    // drawn edges keep their shape whatever size the text makes them.
    void BuildObjectives()
    {
        var column = Ui("Objectives", canvas);
        Place(column, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -36f), Vector2.zero);
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
        Img(banner, "UI/Menu/main_objective", Color.white).pixelsPerUnitMultiplier = 1f;
        bannerSize = banner.gameObject.AddComponent<LayoutElement>();
        bannerSize.preferredWidth = ObjectiveWidth; // wider for a long title (ShowPart)
        bannerSize.preferredHeight = TitleHeight;
        var title = Ui("Label", banner);
        Stretch(title);
        title.offsetMin = new Vector2(TitlePad, 0f);
        title.offsetMax = new Vector2(-TitlePad, -6f); // a touch below the middle, as drawn
        objectiveTitle = Txt(title, "", TitleSize, Ink, TextAnchor.MiddleCenter);
        objectiveTitle.font = ParkArt.TitleFont;
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
        stepList.offsetMin = new Vector2(StepsInset, 0f); // a little in from the title's left edge
        // The picture on a child the layout skips: as the list's own Image it would ask for its full drawn size.
        var art = Ui("Art", stepList);
        Stretch(art);
        art.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        Img(art, "UI/Menu/side_objective", Color.white).pixelsPerUnitMultiplier = 1f;
        // A little more room at the top than at the bottom: its top StepsTuck hides under the title.
        Column(stepList, 0f, new RectOffset(52, 30, 32, 30));
        stepsSize = stepList.gameObject.AddComponent<LayoutElement>();
        stepsSize.minWidth = ObjectiveWidth - StepsNarrower; // a bit narrower than the title (ShowPart), not just its steps
        stepList.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        stepsGroup = stepList.gameObject.AddComponent<CanvasGroup>();
        banner.SetAsLastSibling(); // drawn over the steps tucked under it (laid out first all the same, see above)

        // "Too weak to possess", under the objectives (only while possessing is on cooldown).
        var grip = Ui("Grip", column);
        Column(grip, 0f, new RectOffset(28, 0, 12, 0));
        var pill = Ui("Pill", grip);
        Img(pill, "UI/pill", PanelDark);
        Row(pill, 10f, new RectOffset(22, 30, 8, 8)).childAlignment = TextAnchor.MiddleLeft;
        gripLabel = Txt(Ui("Label", pill), "", Figma.Type.ArvoBody, Soft);
        gripRow = grip.gameObject;
    }

    // Top right: the events, right-aligned white lines stacking down (UpdateEvents).
    void BuildEvents()
    {
        eventList = Ui("Events", canvas);
        Place(eventList, Vector2.one, Vector2.one, new Vector2(-60f, -50f), Vector2.zero);
        var column = Column(eventList, 2f, new RectOffset());
        column.childAlignment = TextAnchor.UpperRight;
        Fit(eventList);
    }

    // Bottom right, over the pause menu, as in the design (Figma: Objective completed): the artist's pink strip
    // (UI Asset/SavingIcon/AutoSave) running off the right edge, a turning icon (SavingIcon_1..8) and "Saving...".
    void BuildSaving()
    {
        var go = new GameObject("Saving", typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        var c = go.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingOrder = 30;
        Stretch((RectTransform)go.transform);
        saving = go.AddComponent<CanvasGroup>();
        saving.alpha = 0f;
        // Laid out from the screen's bottom right corner, in design px: the strip's left edge 210.8 in, its bottom
        // 29.8 up, 350 x 58.4 (most of it past the edge); the icon's middle 147.5 in, 56 up; the words from 124 in.
        var strip = Ui("Strip", (RectTransform)go.transform);
        Place(strip, Vector2.right, Vector2.zero, new Vector2(-210.8f, 29.8f) * Figma.K, new Vector2(350f, 58.4f) * Figma.K);
        strip.gameObject.AddComponent<Image>().sprite = UiArt.Get("SavingIcon/AutoSave");
        savingFrames ??= new Sprite[8];
        for (int i = 0; i < savingFrames.Length; i++) savingFrames[i] = UiArt.Get("SavingIcon/SavingIcon_" + (i + 1));
        var icon = Ui("Icon", (RectTransform)go.transform);
        Place(icon, Vector2.right, new Vector2(0.5f, 0.5f), new Vector2(-147.5f, 56f) * Figma.K, new Vector2(36.7f, 36.7f) * Figma.K);
        savingSpinner = icon.gameObject.AddComponent<Image>();
        savingSpinner.sprite = savingFrames[0];
        savingSpinner.raycastTarget = false;
        var words = Ui("Label", (RectTransform)go.transform);
        Place(words, Vector2.right, new Vector2(0f, 0.5f), new Vector2(-124f, 56f) * Figma.K, new Vector2(200f, 40f));
        Txt(words, GameText.Saving, Figma.Type.ArvoBody, new Color32(36, 23, 37, 255));
    }

    void BuildPrompt()
    {
        prompt = Ui("World Prompt", canvas);
        Place(prompt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(10f, 10f));
        var body = Ui("Body", prompt);
        Place(body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
        // Img(body, "UI/pill", new Color(0.12f, 0.08f, 0.12f, 0.88f));
        Row(body, 10f, new RectOffset(10, 22, 6, 6));
        Fit(body);
        promptKey = KeyCap.Make(body, "K", new Key[0], 40f);
        Destroy(promptKey.GetComponent<KeyHint>());
        promptCap = promptKey.gameObject;
        promptText = Txt(Ui("Label", body), "", Figma.Type.ArvoBody, Color.white);
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
        InkWeight.On(t); // as heavy as in the design
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
}
