using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The tutorial: a new game starts with it, in the park, before level 1 (a loaded game skips it). Twelve steps, after
// the design doc's flow:
//    1 the spirit is lit up ("I'm so ready to have fun!")      7 a ring at your feet points the way to the toilet
//    2 the objectives (top left) are lit up                     8 the Out of Order sign: E takes it down
//    3 float around with the arrow keys                         9 the valve: E cranks it up, three times
//    4 a visitor is held still and lit up                      10 Q leaves the body
//    5 E possesses them                                        11 a visitor goes in: geyser, evil laugh
//    6 "Let's complete an objective!"                          12 the pop-up (bottom left) is lit up
// While a step points at something, the park goes dark except for it: someone or something in the park is lit in its
// own shape, with a soft glow of light round it (TutorialSpotlight); a piece of the HUD gets a soft rounded spotlight
// and breathes a little. A card at the bottom says what to do, with the keys to press as
// keycaps that light up while they're held; the spirit talks in a speech bubble. Steps you only read move on with
// Space (or Enter). Hold Tab to skip it all. Its own prank (ToiletPrank) is the objectives' first part, "Toilet
// Trouble". Every word it shows is in GameText. GameHud adds it to the spirit.
public class Tutorial : MonoBehaviour
{
    public static bool Active { get; private set; }

    const float Dim = 0.82f;       // how dark the HUD gets around the piece being pointed at
    // How dark the park gets around who or what's lit. The shade is blended in linear colour, so it looks lighter than
    // the number says: 0.82 left the park bright and purple, 0.95 was too dark.
    const float WorldDim = 0.9f;
    const float SkipHold = 1.2f;   // seconds to hold Tab to skip
    const float FadeSpeed = 3.5f;  // the dark fades in or out over 1 / FadeSpeed seconds
    const float Corner = 63f;      // the spotlight's soft corner radius, in HUD units
    const float SpiritZoom = 0.55f; // step 1's close-up on the spirit: lower = closer (0.55 = about 1.8x bigger)
    static readonly Color Shade = Color.black;
    static readonly Color Lilac = new(0.85f, 0.72f, 1f);  // the spirit's colour, like the outline's glow
    static readonly Color PanelDark = new(0.12f, 0.08f, 0.12f, 0.9f);
    static readonly Color Ink = new(0.17f, 0.1f, 0.13f);
    static readonly Color Soft = new(0.96f, 0.92f, 0.88f);
    static readonly Color Faint = new(0.96f, 0.92f, 0.88f, 0.6f);

    static Font hand;
    static Font HandFont => hand != null ? hand : hand = Resources.Load<Font>("Park/Fonts/LoveYaLikeASister") ?? ParkArt.Font;

    Possession spirit;
    ToiletPrank prank;
    Visitor held;                 // someone the tutorial is holding still or walking somewhere
    Coroutine flow;
    bool ending;

    Canvas canvasComponent;
    RectTransform canvas;
    CanvasGroup overlay;          // the dim round a piece of the HUD
    RectTransform hole;
    readonly List<Transform> lighting = new(); // who or what in the park is lit
    // What the next step wants lit. Between steps the dark fades out first (the park goes bright), then the next
    // spotlight fades in on its own.
    readonly List<Transform> wantLighting = new();
    Func<Rect?> wantFocus;
    RectTransform wantBreathing;
    bool switching;
    RectTransform breathing;      // the piece of the HUD being pointed at, pulsing gently
    Vector3 breathingScale;
    readonly RectTransform[] shades = new RectTransform[4];
    Func<Rect?> focus;            // the screen rect to light up, if any
    float dim, worldDim;
    Rect box;
    bool hasLit;

    RectTransform card, cardContent;
    CanvasGroup cardGroup;
    string cardShown;
    float cardAge;
    RectTransform bubble;
    CanvasGroup bubbleGroup;
    Text bubbleText;
    string bubbleLine;
    float bubbleAge;
    CanvasGroup skip;
    Image skipFill;
    float skipHeld;
    Transform guide, guideArrow;

    // ---------------------------------------------------------------- starting and stopping

    void Start()
    {
        spirit = GetComponent<Possession>();
        if (!SaveGame.Loading) prank = ToiletPrank.Create();
        if (prank == null)
        {
            // Loading a saved game (or a park with no toilet): straight on to level 1.
            Mischief.CompleteQuietly(Mischief.TutorialTasks);
            enabled = false;
            return;
        }
        Build();
        Active = true;
        flow = StartCoroutine(Run());
    }

    void OnDestroy()
    {
        if (Active) Active = false;
        TutorialSpotlight.Darkness = 0f;
        TutorialSpotlight.Lit.Clear();
    }

    void Finish()
    {
        Active = false;
        ending = true;
        Possession.InputBlocked = false;
        if (held != null)
        {
            held.Look.SetVisible(true); // skipped while they were in the toilet
            held.Free();
        }
        held = null;
        Unfocus();
        Say(null);
        ShowGuide(false);
        CameraRig.EndSpotlight();
        CameraRig.EndZoom(); // skipped on step 1
    }

    void Skip()
    {
        if (flow != null) StopCoroutine(flow);
        Mischief.CompleteQuietly(Mischief.TutorialTasks);
        if (prank != null) Destroy(prank.gameObject);
        Finish();
    }

    // ---------------------------------------------------------------- the steps

    IEnumerator Run()
    {
        Possession.InputBlocked = true;
        yield return new WaitForSeconds(1f); // the park settles and the camera finds the spirit

        // 1. The spirit: the camera eases in close on it.
        FocusOn(spirit.transform);
        CameraRig.Zoom(SpiritZoom);
        Say(GameText.TutorialMeSays);
        Card(GameText.TutorialMe, next: true);
        yield return Next();

        // 2. The objectives (the camera eases back out).
        CameraRig.EndZoom();
        FocusOn(GameHud.Instance.ObjectivesPanel, 18f);
        Say(GameText.TutorialGoalSays);
        Card(GameText.TutorialGoal, next: true);
        yield return Next();

        // 3. Floating around: lit keycaps show which arrow you're pressing.
        Unfocus();
        Say(null);
        Possession.InputBlocked = false;
        Card(GameText.TutorialMove, GameText.TutorialMoveAlso);
        Vector3 last = spirit.transform.position;
        float floated = 0f;
        while (floated < 5f)
        {
            yield return null;
            Vector3 now = spirit.transform.position;
            floated += new Vector2(now.x - last.x, now.z - last.z).magnitude;
            last = now;
        }
        yield return new WaitForSeconds(0.3f);

        // 4. Someone to possess: the nearest visitor stops where they are.
        Card(GameText.TutorialVisitor);
        Say(GameText.TutorialVisitorSays);
        while (spirit.Host == null)
        {
            if (held == null || !held.CanBePossessed) HoldNearest();
            if (held != null && spirit.Candidate == held.transform) break;
            yield return null;
        }

        // 5. Possessing them (any visitor will do).
        Say(null);
        Card(GameText.TutorialPossess);
        while (spirit.Host == null || spirit.Host.brain != null) yield return null;
        if (held != null) held.Free();
        held = null;
        Unfocus();
        yield return new WaitForSeconds(0.8f);

        // 6. An objective.
        Possession.InputBlocked = true;
        FocusOn(GameHud.Instance.ObjectivesPanel, 18f);
        Card(GameText.TutorialObjective, next: true);
        yield return Next();
        Possession.InputBlocked = false;

        // 7. To the toilet: a ring at your feet, its arrow pointing the way.
        Unfocus();
        while (spirit.Host == null || Flat(spirit.Host.transform.position - prank.Sign.position) > 2.2f)
        {
            ShowGuide(spirit.Host != null);
            BodyCard(GameText.TutorialWalk);
            yield return null;
        }
        ShowGuide(false);

        // 8. The sign.
        FocusOn(prank.Sign);
        Say(GameText.TutorialSignSays);
        while (!prank.SignDown)
        {
            BodyCard(GameText.TutorialSign);
            yield return null;
        }
        yield return new WaitForSeconds(0.6f);

        // 9. The valve.
        FocusOn(prank.Valve);
        Say(GameText.TutorialValveSays);
        while (!prank.Maxed)
        {
            BodyCard(GameText.TutorialValve);
            yield return null;
        }
        yield return new WaitForSeconds(0.8f);

        // 10. Leaving the body.
        var host = spirit.Host;
        if (host != null) FocusOn(host.transform);
        Say(GameText.TutorialLeaveSays);
        Card(GameText.TutorialLeave);
        while (spirit.Host != null) yield return null;
        Mischief.Complete(Mischief.Task.TutorialLeave);
        yield return new WaitForSeconds(0.6f);

        // 11. Someone goes in...
        Possession.InputBlocked = true;
        Say(null);
        Card(GameText.TutorialWatch);
        CameraRig.Spotlight(prank.Middle, 14f, 2.5f, 1f);
        var victim = Nearest(prank.Door, null);
        held = victim;
        Unfocus(); // the payoff in plain view: no dark, no light
        if (victim != null)
        {
            victim.SendTo(prank.Door);
            for (float t = 0f; victim.Walking && t < 12f; t += Time.deltaTime) yield return null;
            // Taking too long (or stuck): they're there anyway.
            if (victim.Walking) victim.GetComponent<Rigidbody>().position = victim.transform.position = prank.Door;
            victim.Hold();
            victim.Look.SetVisible(false); // in they go
            yield return new WaitForSeconds(1.3f);
        }
        yield return prank.Geyser(victim);
        held = null; // out, soaked, and on their way
        yield return new WaitForSeconds(0.3f);
        Say(GameText.TutorialLaughSays);
        yield return new WaitForSeconds(1.6f);
        CameraRig.EndSpotlight();

        // 12. The pop-up, holding the geyser's line while you read.
        string line = GameText.StepDone(Mischief.Task.TutorialToilet);
        FocusOn(GameHud.Instance.PopUp, 18f);
        Card(GameText.TutorialEvent, next: true);
        GameHud.Instance.KeepPopUp(line);
        yield return Next(() => GameHud.Instance.KeepPopUp(line));

        Finish();
    }

    // Space or Enter moves on (not while paused).
    IEnumerator Next(Action meanwhile = null)
    {
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            meanwhile?.Invoke();
            yield return null;
        }
        while (true)
        {
            meanwhile?.Invoke();
            var kb = Keyboard.current;
            if (kb != null && !PauseMenu.Paused && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) yield break;
            yield return null;
        }
    }

    // The card for a step done in a body, or a reminder to get into one first.
    void BodyCard(string line) => Card(spirit.Host != null ? line : GameText.TutorialPossess);

    // The visitor nearest the spirit stops and gets the spotlight.
    // The visitor nearest the spirit stops (or, if nobody's close, walks over to stand beside it) and gets the light.
    void HoldNearest()
    {
        if (held != null) held.Free();
        Vector3 me = spirit.transform.position;
        held = Nearest(me, spirit.Host);
        if (held == null) return;
        if (Flat(held.transform.position - me) > NearEnough && BesideSpirit(held, me, out Vector3 spot)) held.SendTo(spot);
        else held.Hold();
        FocusOn(held.transform);
    }

    const float NearEnough = 3.5f; // closer than this, a visitor just stops where they are

    // A walkable spot a step to one side of the spirit, for a visitor to come and stand on.
    static bool BesideSpirit(Visitor v, Vector3 me, out Vector3 spot)
    {
        var walker = v.GetComponent<GroundWalker>();
        foreach (var side in new[] { new Vector3(1.6f, 0f, -0.3f), new Vector3(-1.6f, 0f, -0.3f), new Vector3(0f, 0f, -1.4f), new Vector3(0f, 0f, 1.4f) })
        {
            spot = new Vector3(me.x, v.transform.position.y, me.z) + side;
            if (walker == null || walker.IsWalkable(spot)) return true;
        }
        spot = default;
        return false;
    }

    static Visitor Nearest(Vector3 to, Visitor not)
    {
        Visitor best = null;
        float bestDistance = float.MaxValue;
        foreach (var v in Visitor.All)
        {
            if (v == not || v.brain != null || !v.CanBePossessed) continue;
            if (v.Current is not (Visitor.State.Idle or Visitor.State.Wander or Visitor.State.Stare or Visitor.State.Confused)) continue;
            float d = Flat(v.transform.position - to);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = v;
            }
        }
        return best;
    }

    static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    // ---------------------------------------------------------------- every frame

    void Update()
    {
        if (!Active && !ending) return;
        float dt = Time.unscaledDeltaTime;

        // Hold Tab to skip.
        var kb = Keyboard.current;
        bool holding = Active && !PauseMenu.Paused && kb != null && kb.tabKey.isPressed;
        skipHeld = holding ? skipHeld + dt : Mathf.MoveTowards(skipHeld, 0f, dt * 3f);
        skipFill.fillAmount = skipHeld / SkipHold;
        skip.alpha = Mathf.MoveTowards(skip.alpha, Active ? 1f : 0f, dt * 4f);
        if (Active && skipHeld >= SkipHold) Skip();

        UpdateGuide();
    }

    void LateUpdate()
    {
        if (canvas == null) return;
        float dt = Time.unscaledDeltaTime;
        UpdateSpotlight(dt);
        UpdateCard(dt);
        UpdateBubble(dt);

        // Once it's over and everything has faded, the overlay goes.
        if (ending && dim <= 0f && worldDim <= 0f && !switching && cardGroup.alpha <= 0f && skip.alpha <= 0f && bubbleGroup.alpha <= 0f)
        {
            Destroy(canvas.gameObject);
            enabled = false;
        }
    }

    // ---------------------------------------------------------------- the spotlight

    void Unfocus() => Want(null, null);

    // A piece of the HUD pulses gently while it's pointed at (and goes back to its size after).
    void Breathe(RectTransform ui)
    {
        if (breathing == ui) return;
        if (breathing != null) breathing.localScale = breathingScale;
        breathing = ui;
        if (ui != null) breathingScale = ui.localScale;
    }

    // Light someone or something in the park, in its own shape.
    void FocusOn(params Transform[] things)
    {
        var list = new List<Transform>();
        foreach (var t in things) if (t != null) list.Add(t);
        Want(list, null);
    }

    // A soft rounded spotlight on a piece of the HUD.
    void FocusOn(RectTransform ui, float pad)
    {
        if (ui == wantBreathing && wantFocus != null) return; // already on it
        Want(null, ui, () =>
        {
            if (ui == null) return null;
            var corners = new Vector3[4];
            ui.GetWorldCorners(corners); // an overlay canvas: these are screen pixels
            float s = canvasComponent.scaleFactor * pad;
            return Rect.MinMaxRect(corners[0].x - s, corners[0].y - s, corners[2].x + s, corners[2].y + s);
        });
    }

    void Want(List<Transform> things, RectTransform ui, Func<Rect?> rect = null)
    {
        bool same = ui == wantBreathing && (things?.Count ?? 0) == wantLighting.Count;
        if (same && things != null)
            for (int i = 0; i < things.Count; i++) same &= things[i] == wantLighting[i];
        if (same && (rect == null) == (wantFocus == null)) return; // already on it: no flicker
        wantLighting.Clear();
        if (things != null) wantLighting.AddRange(things);
        wantBreathing = ui;
        wantFocus = rect;
        switching = true;
    }

    // Who or what is lit, as sprites for TutorialSpotlight (gathered once per spotlight, not every frame).
    void Light(List<Transform> things)
    {
        lighting.Clear();
        lighting.AddRange(things);
        var lit = TutorialSpotlight.Lit;
        lit.Clear();
        foreach (var t in lighting)
            if (t != null)
                foreach (var r in t.GetComponentsInChildren<SpriteRenderer>(true))
                    if (r.transform.name != "Flat") lit.Add(r); // not shadows lying flat
    }

    void UpdateSpotlight(float dt)
    {
        // Between steps: fade the dark out (the park goes bright), then swap in the next spotlight.
        if (switching)
        {
            worldDim = Mathf.MoveTowards(worldDim, 0f, dt * FadeSpeed);
            dim = Mathf.MoveTowards(dim, 0f, dt * FadeSpeed);
            if (worldDim <= 0f && dim <= 0f)
            {
                Light(wantLighting);
                focus = wantFocus;
                Breathe(wantBreathing);
                hasLit = false;
                switching = false;
            }
        }

        // Someone or something in the park: dark everywhere but its shape (TutorialSpotlight, under the HUD).
        if (!switching) worldDim = Mathf.MoveTowards(worldDim, Active && lighting.Count > 0 ? 1f : 0f, dt * FadeSpeed);
        TutorialSpotlight.Darkness = Mathf.SmoothStep(0f, 1f, worldDim) * WorldDim;

        // A piece of the HUD: the overlay, dark everywhere but a soft rounded box.
        Rect? target = Active ? focus?.Invoke() : null;
        if (!switching) dim = Mathf.MoveTowards(dim, target.HasValue ? 1f : 0f, dt * FadeSpeed);
        overlay.alpha = Mathf.SmoothStep(0f, 1f, dim) * Dim;
        if (breathing != null)
            breathing.localScale = breathingScale * (1f + 0.025f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f)));
        if (!target.HasValue && !hasLit) return;

        // In HUD units, centred like the canvas.
        Vector2 size = canvas.rect.size;
        float s = canvasComponent.scaleFactor;
        if (target.HasValue)
        {
            Rect r = target.Value;
            var want = Rect.MinMaxRect(r.xMin / s - size.x * 0.5f, r.yMin / s - size.y * 0.5f, r.xMax / s - size.x * 0.5f, r.yMax / s - size.y * 0.5f);
            // At least big enough for its soft corners.
            if (want.width < Corner * 2f) want = new Rect(want.center.x - Corner, want.y, Corner * 2f, want.height);
            if (want.height < Corner * 2f) want = new Rect(want.x, want.center.y - Corner, want.width, Corner * 2f);
            if (!hasLit || dim <= 0.01f) box = want; // appears in place, then glides from one thing to the next
            float k = 1f - Mathf.Exp(-10f * dt);
            box = Rect.MinMaxRect(Mathf.Lerp(box.xMin, want.xMin, k), Mathf.Lerp(box.yMin, want.yMin, k),
                Mathf.Lerp(box.xMax, want.xMax, k), Mathf.Lerp(box.yMax, want.yMax, k));
            hasLit = true;
        }

        float left = -size.x * 0.5f, right = size.x * 0.5f, bottom = -size.y * 0.5f, top = size.y * 0.5f;
        Box(hole, box);
        Box(shades[0], Rect.MinMaxRect(left - 10f, box.yMax, right + 10f, top + 10f));        // above
        Box(shades[1], Rect.MinMaxRect(left - 10f, bottom - 10f, right + 10f, box.yMin));     // below
        Box(shades[2], Rect.MinMaxRect(left - 10f, box.yMin, box.xMin, box.yMax));            // left
        Box(shades[3], Rect.MinMaxRect(box.xMax, box.yMin, right + 10f, box.yMax));           // right
    }

    static void Box(RectTransform rt, Rect r)
    {
        rt.anchoredPosition = r.center;
        rt.sizeDelta = new Vector2(Mathf.Max(0f, r.width), Mathf.Max(0f, r.height));
    }

    // ---------------------------------------------------------------- the card

    // What to do, at the bottom. [Arrows], [E], [Q], [Space]... show as keycaps.
    void Card(string line, string aside = null, bool next = false)
    {
        string key = line + "|" + aside + "|" + next;
        if (key == cardShown) return;
        cardShown = key;
        cardAge = 0f;
        foreach (Transform child in cardContent) Destroy(child.gameObject);

        foreach (var (text, isKey) in Split(line))
        {
            if (!isKey) Words(cardContent, text, 34, Soft);
            else if (text == "Arrows") ArrowKeys(cardContent);
            else KeyCap(cardContent, text, Keys(text), 52f);
        }
        if (aside != null) Words(cardContent, aside, 26, Faint);
        if (next)
        {
            var gap = Ui("Gap", cardContent);
            gap.gameObject.AddComponent<LayoutElement>().preferredWidth = 18f;
            KeyCap(cardContent, "Space", new[] { Key.Space, Key.Enter, Key.NumpadEnter }, 44f);
            Words(cardContent, GameText.TutorialNext + " ›", 28, Faint);
        }
    }

    void UpdateCard(float dt)
    {
        cardAge += dt;
        bool show = Active && cardShown != null;
        cardGroup.alpha = Mathf.MoveTowards(cardGroup.alpha, show ? 1f : 0f, dt * 5f);
        // Each new card pops in from a little lower.
        float pop = 1f - Mathf.Pow(1f - Mathf.Clamp01(cardAge / 0.3f), 3f);
        card.anchoredPosition = new Vector2(0f, 120f - (1f - pop) * 24f);
        card.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, pop);
    }

    static IEnumerable<(string text, bool isKey)> Split(string line)
    {
        int at = 0;
        while (at < line.Length)
        {
            int open = line.IndexOf('[', at);
            int close = open >= 0 ? line.IndexOf(']', open) : -1;
            if (open < 0 || close < 0)
            {
                yield return (line[at..].Trim(), false);
                yield break;
            }
            if (open > at) yield return (line[at..open].Trim(), false);
            yield return (line[(open + 1)..close], true);
            at = close + 1;
        }
    }

    static Key[] Keys(string name) => name switch
    {
        "E" => new[] { Key.E },
        "Q" => new[] { Key.Q },
        "Tab" => new[] { Key.Tab },
        "Space" => new[] { Key.Space },
        _ => Enum.TryParse(name, true, out Key k) ? new[] { k } : new Key[0],
    };

    // The four arrow keys as on a keyboard (up over left, down, right), each lighting up while held (WASD too).
    void ArrowKeys(RectTransform parent)
    {
        var block = Ui("Arrows", parent);
        var column = block.gameObject.AddComponent<VerticalLayoutGroup>();
        column.spacing = 4f;
        column.childAlignment = TextAnchor.MiddleCenter;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = column.childForceExpandHeight = false;
        var upRow = Row(Ui("Top", block), 4f);
        var downRow = Row(Ui("Bottom", block), 4f);
        ArrowCap(upRow, 0f, Key.UpArrow, Key.W);
        ArrowCap(downRow, 90f, Key.LeftArrow, Key.A);
        ArrowCap(downRow, 180f, Key.DownArrow, Key.S);
        ArrowCap(downRow, -90f, Key.RightArrow, Key.D);
    }

    void ArrowCap(RectTransform row, float angle, params Key[] keys)
    {
        var cap = KeyCap(row, "", keys, 46f);
        var arrow = Ui("Arrow", cap);
        Place(arrow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(22f, 22f));
        arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
        Img(arrow, "Markers/arrow", Ink).preserveAspect = true;
    }

    // ---------------------------------------------------------------- the speech bubble

    // The spirit (or whoever it's in) says something, typed out; null hides the bubble.
    void Say(string line)
    {
        if (line == bubbleLine) return;
        bubbleLine = line;
        bubbleAge = 0f;
    }

    void UpdateBubble(float dt)
    {
        bubbleAge += dt;
        bool show = Active && !string.IsNullOrEmpty(bubbleLine);
        bubbleGroup.alpha = Mathf.MoveTowards(bubbleGroup.alpha, show ? 1f : 0f, dt * 6f);
        if (show) bubbleText.text = bubbleLine[..Mathf.Min(bubbleLine.Length, Mathf.FloorToInt(bubbleAge * 45f))];
        if (bubbleGroup.alpha <= 0f) return;
        var cam = Camera.main;
        if (cam == null) return;
        var host = spirit.Host;
        Vector3 over = host != null ? host.transform.position + Vector3.up * 2.6f : spirit.transform.position + Vector3.up * 1.4f;
        Vector3 screen = cam.WorldToScreenPoint(over);
        if (screen.z <= 0f) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out Vector2 local);
        bubble.anchoredPosition = local;
        bubble.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(bubbleAge / 0.12f));
    }

    // ---------------------------------------------------------------- the ring at your feet

    void ShowGuide(bool on)
    {
        if (!on)
        {
            if (guide != null) Destroy(guide.gameObject);
            guide = null;
            return;
        }
        if (guide != null) return;
        guide = new GameObject("Tutorial Guide").transform;
        ParkArt.Make("Ring", guide, "Markers/ring", 1.3f, Lilac, ParkArt.DecalOrder + 2, true);
        guideArrow = new GameObject("Arrow Holder").transform;
        guideArrow.SetParent(guide, false);
        var arrow = ParkArt.Make("Arrow", guideArrow, "Markers/arrow", 0.55f, Lilac, ParkArt.DecalOrder + 2, true);
        arrow.transform.parent.localPosition = new Vector3(0f, 0f, 1.05f); // ahead of the ring, pointing out of it
    }

    void UpdateGuide()
    {
        if (guide == null || spirit.Host == null || prank == null) return;
        Vector3 feet = spirit.Host.transform.position;
        guide.position = new Vector3(feet.x, 0.04f, feet.z);
        Vector3 to = prank.Sign.position - feet;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f) guideArrow.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        guide.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.time * 5f));
    }

    // ---------------------------------------------------------------- building the overlay

    void Build()
    {
        var go = new GameObject("Tutorial", typeof(RectTransform));
        canvas = (RectTransform)go.transform;
        canvasComponent = go.AddComponent<Canvas>();
        canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasComponent.sortingOrder = 15; // over the HUD (10), under the pause menu (20)
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // The dim: a soft-edged hole, and four shades round it.
        var dimRoot = Ui("Dim", canvas);
        Stretch(dimRoot);
        overlay = dimRoot.gameObject.AddComponent<CanvasGroup>();
        overlay.alpha = 0f;
        hole = Centred(Ui("Spotlight", dimRoot));
        Sliced(hole, HoleSprite, Shade);
        gameObject.AddComponent<TutorialSpotlight>(); // the light on someone or something in the park
        for (int i = 0; i < shades.Length; i++)
        {
            shades[i] = Centred(Ui("Shade", dimRoot));
            Img(shades[i], null, Shade);
        }

        // The card at the bottom.
        card = Ui("Card", canvas);
        Place(card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), Vector2.zero);
        cardGroup = card.gameObject.AddComponent<CanvasGroup>();
        cardGroup.alpha = 0f;
        Img(card, "UI/pill", PanelDark);
        Row(card, 0f, new RectOffset(40, 40, 16, 16));
        Fit(card);
        cardContent = Row(Ui("Content", card), 14f);

        // The speech bubble, like the HUD's.
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
        // Keep its size steady while the line types out: the full line sizes it, invisibly.
        body.gameObject.AddComponent<BubbleSizer>().Init(bubbleText, () => bubbleLine);

        // Hold Tab to skip, bottom right.
        var skipRoot = Ui("Skip", canvas);
        Place(skipRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 36f), Vector2.zero);
        skip = skipRoot.gameObject.AddComponent<CanvasGroup>();
        skip.alpha = 0f;
        Img(skipRoot, "UI/pill", PanelDark);
        Row(skipRoot, 10f, new RectOffset(18, 24, 8, 8));
        Fit(skipRoot);
        var (before, keyName, after) = SplitOne(GameText.TutorialSkip);
        if (before.Length > 0) Words(skipRoot, before, 22, Faint);
        var cap = KeyCap(skipRoot, keyName, Keys(keyName), 38f);
        var fill = Ui("Hold", cap);
        Stretch(fill);
        skipFill = Img(fill, "UI/key", new Color(1f, 0.55f, 0.7f, 0.85f));
        skipFill.type = Image.Type.Filled;
        skipFill.fillMethod = Image.FillMethod.Horizontal;
        skipFill.fillAmount = 0f;
        fill.SetAsFirstSibling();
        if (after.Length > 0) Words(skipRoot, after, 22, Faint);
    }

    static (string before, string key, string after) SplitOne(string line)
    {
        int open = line.IndexOf('['), close = line.IndexOf(']');
        if (open < 0 || close < open) return (line, "Tab", "");
        return (line[..open].Trim(), line[(open + 1)..close], line[(close + 1)..].Trim());
    }

    // A soft-edged hole: clear in the middle, dark towards the edges (sliced, so it's a rounded box of any size).
    static Sprite holeSprite;
    static Sprite HoleSprite => holeSprite != null ? holeSprite : holeSprite = RoundSprite("Tutorial Hole", 128, d => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d)));

    static Sprite RoundSprite(string name, int n, Func<float, float> alpha)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f - n * 0.5f) / (n * 0.5f), dy = (y + 0.5f - n * 0.5f) / (n * 0.5f);
                pixels[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(Mathf.Sqrt(dx * dx + dy * dy))) * 255f));
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        float edge = n * 0.5f - 1f;
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(edge, edge, edge, edge));
    }

    static Image Sliced(RectTransform rt, Sprite sprite, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        img.pixelsPerUnitMultiplier = 63f / Corner;
        return img;
    }

    static RectTransform Centred(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    // ---------------------------------------------------------------- UI helpers (as in GameHud)

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

    static Text Txt(RectTransform rt, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft)
    {
        var t = rt.gameObject.AddComponent<Text>();
        t.font = HandFont;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    static void Words(RectTransform parent, string text, int size, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;
        Txt(Ui("Words", parent), text, size, color);
    }

    static RectTransform Row(RectTransform rt, float spacing, RectOffset padding = null)
    {
        var row = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = spacing;
        row.padding = padding ?? new RectOffset();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        return rt;
    }

    static void Fit(RectTransform rt)
    {
        var fit = rt.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    // A keycap (the HUD's key art) that lights up while one of its keys is held.
    static RectTransform KeyCap(RectTransform parent, string text, Key[] keys, float height)
    {
        var cap = Ui("Key " + text, parent);
        var img = Img(cap, "UI/key", Color.white);
        img.pixelsPerUnitMultiplier = 1f;
        var element = cap.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = Mathf.Max(height, text.Length * 15f + 26f);
        element.preferredHeight = height;
        cap.gameObject.AddComponent<KeyHint>().keys = keys;
        if (text.Length > 0)
        {
            var label = Ui("Label", cap);
            Stretch(label);
            label.offsetMin = new Vector2(0f, 6f);
            var t = Txt(label, text, Mathf.RoundToInt(height * 0.45f), Ink, TextAnchor.MiddleCenter);
            t.font = ParkArt.Font;
            t.fontStyle = FontStyle.Bold;
        }
        return cap;
    }

    // Sizes the speech bubble to its whole line from the first letter, so it doesn't grow as the line types out.
    class BubbleSizer : MonoBehaviour, ILayoutElement
    {
        Text text;
        Func<string> line;
        public void Init(Text t, Func<string> full)
        {
            text = t;
            line = full;
        }
        float Measure(bool width)
        {
            if (text == null) return -1f;
            var settings = text.GetGenerationSettings(Vector2.zero);
            string s = line() ?? "";
            return width ? text.cachedTextGeneratorForLayout.GetPreferredWidth(s, settings) / text.pixelsPerUnit + 52f
                         : text.cachedTextGeneratorForLayout.GetPreferredHeight(s, settings) / text.pixelsPerUnit + 22f;
        }
        public void CalculateLayoutInputHorizontal() { }
        public void CalculateLayoutInputVertical() { }
        public float minWidth => -1f;
        public float preferredWidth => Measure(true);
        public float flexibleWidth => -1f;
        public float minHeight => -1f;
        public float preferredHeight => Measure(false);
        public float flexibleHeight => -1f;
        public float maxWidth => -1f;
        public float maxHeight => -1f;
        public int layoutPriority => 2;
    }
}
