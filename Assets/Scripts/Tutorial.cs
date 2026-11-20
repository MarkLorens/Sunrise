using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The tutorial: a new game starts with it, in the park, before level 1 (a loaded game skips it). Twelve steps, after
// the design doc's flow:
//    1 the spirit is lit up ("I'm so ready to have fun!")      7 a ring at your feet points the way to the toilet
//    2 the objectives (top left) are lit up                     8 the Out of Order sign: K takes it down
//    3 float around with W A S D                                9 the valve: K cranks it up, three times
//    4 a visitor is held still and lit up                      10 J leaves the body
//    5 J possesses them                                        11 a visitor goes in: geyser, evil laugh
//    6 "Let's complete an objective!"                          12 the events (top right) are lit up
// While a step points at something, the park goes dark except for it: someone or something in the park is lit in its
// own shape, with a soft glow of light round it (TutorialSpotlight); a piece of the HUD gets a soft rounded spotlight
// and breathes a little. A card at the bottom says what to do, and under it, on a line of their own, the keys to
// press as keycaps that light up while they're held; the spirit talks in a speech bubble. Steps you only read move on with
// Space (or Enter). Hold Tab to skip it all. Its own prank (ToiletPrank) is the objectives' first part, "Toilet
// Trouble". Every word it shows is in GameText. GameHud adds it to the spirit.
public class Tutorial : MonoBehaviour
{
    public static bool Active { get; private set; }

    const float Dim = 0.82f;       // how dark the HUD gets around the piece being pointed at
    // How dark the park gets around who or what's lit. The shade is blended in linear colour, so it looks lighter than
    // the number says: 0.82 left the park bright and purple, 0.95 was too dark.
    const float WorldDim = 0.9f;
    const float SkipHold = 1.2f;   // seconds to hold Space to skip (a tap is Next; Tab opens the objectives)
    const float FadeSpeed = 3.5f;  // the dark fades in or out over 1 / FadeSpeed seconds
    const float Corner = 63f;      // the spotlight's soft corner radius, in HUD units
    const float SpiritZoom = 0.55f; // step 1's close-up on the spirit: lower = closer (0.55 = about 1.8x bigger)
    static readonly Color Shade = Color.black;
    static readonly Color Lilac = new(0.85f, 0.72f, 1f);  // the spirit's colour, like the outline's glow
    static readonly Color PanelDark = new(0.12f, 0.08f, 0.12f, 0.9f);
    static readonly Color Ink = new(0.17f, 0.1f, 0.13f);
    static readonly Color Soft = new(0.96f, 0.92f, 0.88f);
    static readonly Color Faint = new(0.96f, 0.92f, 0.88f, 0.6f);
    // The instruction card: light, like the speech bubbles, so it stands out on the dark.
    static readonly Color CardFill = new(0.98f, 0.95f, 0.9f);
    static readonly Color CardInk = new(0.17f, 0.1f, 0.13f);
    static readonly Color CardFaint = new(0.17f, 0.1f, 0.13f, 0.55f);
    static readonly Color CardShadow = new(0f, 0f, 0f, 0.35f);


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

    RectTransform card, cardContent, cardPointer;
    Vector2 cardAt;
    bool cardPlaced;
    CanvasGroup cardGroup;
    string cardShown;
    float cardAge;
    RectTransform bubble;
    CanvasGroup bubbleGroup;
    Text bubbleText;
    string bubbleLine;
    float bubbleAge;
    Graphic[] bubbleParts;
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
        // ParkTour.Play(); // off for now; uncomment to show the Ferris wheel after the tutorial (played through or skipped)
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
        Card(GameText.TutorialMove);
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
            else if (!held.Walking && Flat(held.transform.position - spirit.transform.position) > NearEnough + 1f) Follow(held);
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
        FocusOn(prank.Picture); // the valve's drawn on the block, so light the block
        Say(GameText.TutorialValveSays);
        while (!prank.Maxed)
        {
            BodyCard(GameText.TutorialValve);
            yield return null;
        }
        yield return new WaitForSeconds(0.8f);

        // 10. Leaving the body.
        var host = spirit.Host;
        var lastBody = host; // not the one who goes in next
        if (host != null) FocusOn(host.transform);
        Say(GameText.TutorialLeaveSays);
        Card(GameText.TutorialLeave);
        while (spirit.Host != null) yield return null;
        Mischief.Complete(Mischief.Task.TutorialLeave);
        yield return new WaitForSeconds(0.6f);

        // 11. Someone else, from a little way off, comes running for the toilet; the camera runs with them. In they
        // go, the block rumbles harder and harder, and up it goes.
        Possession.InputBlocked = true;
        Say(null);
        Card(GameText.TutorialWatch);
        Unfocus(); // the payoff in plain view: no dark, no light
        var victim = Runner(prank.Door, lastBody);
        held = victim;
        if (victim != null)
        {
            // They stop dead, it hits them, and only then do they run: a beat to see who it is.
            victim.Hold();
            CameraRig.Spotlight(victim.transform, 30f, 1.5f);
            GameHud.Shout(victim.transform, GameText.TutorialGottaGo, 2.5f);
            yield return new WaitForSeconds(1.2f);
            victim.SendTo(prank.Door, run: true);
            for (float t = 0f; victim.Walking && t < 12f; t += Time.deltaTime) yield return null;
            // Taking too long (or stuck): they're there anyway.
            if (victim.Walking) victim.GetComponent<Rigidbody>().position = victim.transform.position = prank.Door;
            victim.Hold();
            yield return new WaitForSeconds(0.6f); // at the door, a moment, then in
            victim.Look.SetVisible(false); // in they go
        }
        CameraRig.Spotlight(prank.Middle, 20f, 2.5f, 1f);
        yield return new WaitForSeconds(1f); // quiet... for now
        yield return prank.Rumble(3.5f);
        yield return prank.Geyser(victim);
        held = null; // out, soaked, and on their way
        yield return new WaitForSeconds(0.8f);
        Say(GameText.TutorialLaughSays);
        yield return new WaitForSeconds(2.2f);
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
            if (!PauseMenu.Paused && Controls.Skip) yield break; // Space or Enter
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

    // The spirit floated off again before reaching them: they come over again.
    void Follow(Visitor v)
    {
        if (BesideSpirit(v, spirit.transform.position, out Vector3 spot)) v.SendTo(spot);
    }

    // A walkable spot right beside the spirit, close enough to possess them from (Possession.touchRange is 1.2),
    // for a visitor to come and stand on.
    static bool BesideSpirit(Visitor v, Vector3 me, out Vector3 spot)
    {
        var walker = v.GetComponent<GroundWalker>();
        foreach (var side in new[] { new Vector3(0.9f, 0f, -0.2f), new Vector3(-0.9f, 0f, -0.2f), new Vector3(0f, 0f, -0.9f), new Vector3(0f, 0f, 0.9f) })
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

    // Who runs for the toilet: not `not` (the body just left), and someone a little way off, so you see them come
    // (the nearest between 6 and 16 away; failing that, the nearest at all).
    static Visitor Runner(Vector3 door, Visitor not)
    {
        Visitor best = null, any = null;
        float bestDistance = float.MaxValue, anyDistance = float.MaxValue;
        foreach (var v in Visitor.All)
        {
            if (v == not || v.brain != null || !v.CanBePossessed) continue;
            if (v.Current is not (Visitor.State.Idle or Visitor.State.Wander or Visitor.State.Stare or Visitor.State.Confused)) continue;
            float d = Flat(v.transform.position - door);
            if (d < anyDistance) { anyDistance = d; any = v; }
            if (d >= 9f && d <= 18f && d < bestDistance) { bestDistance = d; best = v; }
        }
        return best != null ? best : any;
    }

    // ---------------------------------------------------------------- every frame

    void Update()
    {
        if (!Active && !ending) return;
        float dt = Time.unscaledDeltaTime;

        // Hold Space to skip.
        bool holding = Active && !PauseMenu.Paused && Controls.SkipHeld;
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

    // What to do, at the bottom: the sentence, then the keys to press on a line of their own. [WASD], [J], [K]...
    // in the sentence show there as keycaps that light up while they're held (and as plain bold letters in the
    // sentence); `next` adds [Space] Next for steps you only read.
    void Card(string line, bool next = false)
    {
        string key = line + "|" + next;
        if (key == cardShown) return;
        cardShown = key;
        cardAge = 0f;
        foreach (Transform child in cardContent) Destroy(child.gameObject);

        var sentence = new List<string>();
        var keys = new List<string>();
        foreach (var (text, isKey) in Split(line))
        {
            if (!isKey)
            {
                if (text.Length > 0) sentence.Add(text);
                continue;
            }
            keys.Add(text);
            sentence.Add($"<b>{text}</b>");
        }
        Words(cardContent, string.Join(" ", sentence), 34, CardInk);

        if (keys.Count == 0 && !next) return;
        var row = Row(Ui("Keys", cardContent), 12f);
        foreach (var k in keys)
        {
            if (k == "WASD") Wasd(row);
            else KeyCap(row, k, Keys(k), 52f);
        }
        if (!next) return;
        KeyCap(row, "Space", new[] { Key.Space, Key.Enter, Key.NumpadEnter }, 44f);
        Words(row, GameText.TutorialNext + " ›", 28, CardFaint);
    }

    // The card sits by whatever's lit (under it, or over it when there's no room below), its little pointer aimed at
    // it, and glides along as the camera moves. With nothing lit it waits at the bottom of the screen.
    void UpdateCard(float dt)
    {
        cardAge += dt;
        bool show = Active && cardShown != null;
        cardGroup.alpha = Mathf.MoveTowards(cardGroup.alpha, show ? 1f : 0f, dt * 5f);

        Vector2 half = canvas.rect.size * 0.5f, size = card.rect.size;
        const float Gap = 34f, Margin = 24f;
        Vector2 want = new(0f, -half.y + 120f + size.y * 0.5f);
        int pointer = 0; // +1: the card's under it, pointing up; -1: over it, pointing down
        Rect? lit = Active && !switching && (worldDim > 0.5f || dim > 0.5f) ? LitRect() : null;
        if (lit.HasValue)
        {
            Rect t = lit.Value;
            float x = Mathf.Clamp(t.center.x, -half.x + Margin + size.x * 0.5f, half.x - Margin - size.x * 0.5f);
            float below = t.yMin - Gap - size.y * 0.5f, above = t.yMax + Gap + size.y * 0.5f;
            if (below - size.y * 0.5f >= -half.y + Margin) { want = new Vector2(x, below); pointer = 1; }
            else if (above + size.y * 0.5f <= half.y - Margin) { want = new Vector2(x, above); pointer = -1; }
            else want = new Vector2(Mathf.Clamp(t.xMax + Gap + size.x * 0.5f, -half.x + Margin + size.x * 0.5f, half.x - Margin - size.x * 0.5f),
                                    Mathf.Clamp(t.center.y, -half.y + Margin + size.y * 0.5f, half.y - Margin - size.y * 0.5f));
        }
        if (!cardPlaced || cardGroup.alpha <= 0.01f) cardAt = want; // appears in place, then glides
        cardAt = Vector2.Lerp(cardAt, want, 1f - Mathf.Exp(-10f * dt));
        cardPlaced = true;

        // Each new card pops in from a little lower.
        float pop = 1f - Mathf.Pow(1f - Mathf.Clamp01(cardAge / 0.3f), 3f);
        card.anchoredPosition = cardAt + new Vector2(0f, -(1f - pop) * 24f);
        card.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, pop);

        cardPointer.gameObject.SetActive(pointer != 0 && lit.HasValue);
        if (pointer == 0 || !lit.HasValue) return;
        float px = Mathf.Clamp(lit.Value.center.x - cardAt.x, -size.x * 0.5f + 30f, size.x * 0.5f - 30f);
        cardPointer.anchoredPosition = new Vector2(px, pointer * (size.y * 0.5f));
    }

    // What's lit, in HUD units centred like the canvas: the HUD piece's box, or round the lit sprites on screen.
    Rect? LitRect()
    {
        float s = canvasComponent.scaleFactor;
        Vector2 half = canvas.rect.size * 0.5f;
        Rect? screen = focus?.Invoke();
        if (!screen.HasValue && TutorialSpotlight.Lit.Count > 0)
        {
            var cam = Camera.main;
            if (cam == null) return null;
            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            foreach (var r in TutorialSpotlight.Lit)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                Bounds b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = cam.WorldToScreenPoint(corner);
                    if (p.z <= 0f) continue;
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
            }
            if (min.x > max.x) return null;
            screen = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        if (!screen.HasValue) return null;
        Rect r2 = screen.Value;
        return Rect.MinMaxRect(r2.xMin / s - half.x, r2.yMin / s - half.y, r2.xMax / s - half.x, r2.yMax / s - half.y);
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
        "J" => new[] { Key.J },
        "K" => new[] { Key.K },
        "Tab" => new[] { Key.Tab },
        "Space" => new[] { Key.Space },
        _ => Enum.TryParse(name, true, out Key k) ? new[] { k } : new Key[0],
    };

    // W A S D as on a keyboard (W over A, S, D), each lighting up while it's held.
    void Wasd(RectTransform parent)
    {
        var block = Ui("WASD", parent);
        var column = block.gameObject.AddComponent<VerticalLayoutGroup>();
        column.spacing = 4f;
        column.childAlignment = TextAnchor.MiddleCenter;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = column.childForceExpandHeight = false;
        var top = Row(Ui("Top", block), 2f);
        var bottom = Row(Ui("Bottom", block), 2f);
        KeyArt(top, "W", Key.W, 54f);
        KeyArt(bottom, "A", Key.A, 54f);
        KeyArt(bottom, "S", Key.S, 54f);
        KeyArt(bottom, "D", Key.D, 54f);
    }

    // A key drawn with its letter (UI/Buttons/key_w...), lighting up while it's held like the other keycaps.
    static RectTransform KeyArt(RectTransform parent, string name, Key key, float height)
    {
        var cap = Ui("Key " + name, parent);
        var img = Img(cap, "UI/Buttons/key_" + name.ToLowerInvariant(), Color.white);
        img.preserveAspect = true;
        var element = cap.gameObject.AddComponent<LayoutElement>();
        Vector2 size = img.sprite != null ? img.sprite.rect.size : Vector2.one;
        element.preferredWidth = height * size.x / size.y;
        element.preferredHeight = height;
        cap.gameObject.AddComponent<KeyHint>().keys = new[] { key };
        return cap;
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
        // The dark on the park is drawn before any HUD, so the bubble darkens itself as much, as if it were under it.
        bubbleParts ??= bubble.GetComponentsInChildren<Graphic>(true);
        float shade = 1f - TutorialSpotlight.Darkness;
        foreach (var g in bubbleParts) g.canvasRenderer.SetColor(new Color(shade, shade, shade, 1f));
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

        // The card: a light face with a soft shadow and a pointer at what's lit (placed every frame, UpdateCard).
        card = Ui("Card", canvas);
        Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        cardGroup = card.gameObject.AddComponent<CanvasGroup>();
        cardGroup.alpha = 0f;
        Row(card, 0f);
        Fit(card);
        var shadow = Ui("Shadow", card);
        shadow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        Stretch(shadow);
        shadow.offsetMin = new Vector2(4f, -8f);
        shadow.offsetMax = new Vector2(4f, -8f);
        Img(shadow, "UI/pill", CardShadow);
        cardPointer = Ui("Pointer", card);
        cardPointer.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        Place(cardPointer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
        cardPointer.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Img(cardPointer, "Markers/white", CardFill);
        var face = Ui("Face", card);
        Img(face, "UI/pill", CardFill);
        Row(face, 0f, new RectOffset(40, 40, 18, 18));
        cardContent = Ui("Content", face);
        var lines = cardContent.gameObject.AddComponent<VerticalLayoutGroup>(); // the sentence, then the keys under it
        lines.spacing = 12f;
        lines.childAlignment = TextAnchor.MiddleCenter;
        lines.childControlWidth = lines.childControlHeight = true;
        lines.childForceExpandWidth = lines.childForceExpandHeight = false;

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
        bubbleText.font = ParkArt.Font;
        // Keep its size steady while the line types out: the full line sizes it, invisibly.
        body.gameObject.AddComponent<BubbleSizer>().Init(bubbleText, () => bubbleLine);
        bubble.SetAsFirstSibling(); // under the dark overlay: what's lit stays the brightest thing

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
        t.font = ParkArt.Font;
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
