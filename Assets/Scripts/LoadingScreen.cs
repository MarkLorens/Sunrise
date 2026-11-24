using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Into the park (New Game, Load Game): the screen goes the design's
// blue with the park's Ferris wheel turning in the middle ("Loading screen" in the design), Ruckus riding in one of its
// cabins, and a yellow disc over the hub counting 0..100% while the next scene loads in the background. It only lifts
// once the park's ready to play smoothly: loaded, switched to, its first frames' setting up done (the park builds its
// crowd and paths then, with the game held still meanwhile so nothing happens unseen), and a run of quick frames in a
// row. At least MinTime, so it never just flickers; at most SettleMax waiting for the frames to calm.
// The wheel is the level's own art (copies in UI Asset/Loading, a quarter size) put together as in the level: the
// base under the hub, the nine cabins hanging upright from the arms' bulbs (FerrisWheel.cabinMounts), in the same order.
// Back to the main menu (Exit to Main Menu, the finale) there's next to nothing to load (the menu's up in a tenth of a
// second), so no wheel: FadeTo just fades to black and back in on the menu.
public class LoadingScreen : MonoBehaviour
{
    static readonly Color Blue = new Color32(75, 134, 227, 255);
    static readonly Color Ink = new Color32(69, 48, 85, 255); // the art's dark outline
    const float MinTime = 0.6f, SettleMax = 4f, QuickFrame = 1f / 40f;
    const int QuickFrames = 10; // in a row, before it lifts
    const float SpinUp = 0.3f; // the wheel turning on its own before the park starts loading
    ThreadPriority? previousPriority; // the background loading's, put back once it's done

    // Layout, in the level art's own pixels (450 to a wheel unit), from the hub. The whole thing (the wheel's top, with
    // its rim ornaments, down to the base's feet) is Height design px tall, in the middle of the screen both ways: the
    // hub sits a little above the middle, since the base reaches further down than the wheel reaches up.
    const float Height = 635f;
    const float WheelTop = 1855f; // hub to the furthest of the rim's ornaments, which come round to the top as it turns
    const float ArtPx = Height / (WheelTop + BaseDrop); // design px per art px
    const float HubX = Figma.Width * 0.5f, HubY = Figma.Height * 0.5f - (BaseDrop - WheelTop) * 0.5f * ArtPx;
    static readonly Vector2 WheelSize = new(3536f, 3709f), WheelPivot = new(0.472f, 0.5532f);
    static readonly Vector2 BaseSize = new(3379f, 3709f * 1.1f), BasePivot = new(0.4922f, 0.0173f); // stretched 1.1 tall, as in the level
    const float BaseDrop = 4.83f * 450f; // hub to the base's feet
    static readonly Vector2 CabinSize = new(683f, 857f), CabinPivot = new(0.5417f, 0.8016f); // pivot: the knob it hangs from
    static readonly Vector2[] Mounts = // FerrisWheel.cabinMounts, in wheel units
    {
        new(3.2422f, 0.5511f), new(1.9222f, 2.3222f), new(0.0422f, 2.9444f), new(-1.8844f, 2.1667f), new(-2.8978f, 0.2733f),
        new(-2.7244f, -1.6667f), new(-1.0067f, -3.1467f), new(1.0956f, -2.8489f), new(2.5356f, -1.5156f),
    };
    const int RuckusCabin = 2; // the one at the top to start with
    const float Spin = 25f; // degrees per second, the way the level's wheel turns
    const float HubSize = 1020f; // art px: as big on the wheel as the design's 92 px disc

    // Ruckus (the spirit's pieces, Resources/MC, all drawn on one canvas in place) sitting in the cabin, seen through
    // its window with the bowl in front: its size and middle, in the cabin's art px.
    static readonly Vector2 RuckusSize = new(711f * 0.62f, 628f * 0.62f), RuckusAt = new(341f, 470f);
    const float RuckusFps = 8f, RuckusBob = 12f, RuckusBobSpeed = 4f; // bob: cabin art px, radians per second

    static LoadingScreen running;

    RectTransform wheel, ruckus;
    readonly RectTransform[] cabins = new RectTransform[Mounts.Length];
    Image ruckusBody;
    Sprite[] ruckusIdle;
    Text percent;
    float angle, target, shown;

    public static bool Busy => running != null;

    public static void Load(int buildIndex)
    {
        if (running != null) return;
        if (AudioManager.instance != null) AudioManager.instance.StopGameplaySounds(); // nothing carries over
        var canvas = MainMenu.Screen("Loading Screen", 1000); // over everything
        DontDestroyOnLoad(canvas.gameObject);
        var bg = MainMenu.Ui("Blue", canvas);
        MainMenu.Stretch(bg);
        MainMenu.Img(bg, null, Blue).raycastTarget = true; // nothing underneath takes clicks meanwhile
        running = canvas.gameObject.AddComponent<LoadingScreen>();
        running.Build(canvas);
        running.StartCoroutine(running.Go(buildIndex));
    }

    const float FadeTime = 0.3f; // each way, for FadeTo

    // To a scene with next to nothing to load (the main menu): a quick fade to black, the scene switched behind it,
    // then a fade back in.
    public static void FadeTo(int buildIndex)
    {
        if (running != null) return;
        if (AudioManager.instance != null) AudioManager.instance.StopGameplaySounds(); // nothing carries over
        var canvas = MainMenu.Screen("Scene Fade", 1000); // over everything
        DontDestroyOnLoad(canvas.gameObject);
        var black = MainMenu.Ui("Black", canvas);
        MainMenu.Stretch(black);
        MainMenu.Img(black, null, Color.black).raycastTarget = true; // nothing underneath takes clicks meanwhile
        var group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        running = canvas.gameObject.AddComponent<LoadingScreen>();
        running.StartCoroutine(running.Fade(buildIndex, group));
    }

    IEnumerator Fade(int buildIndex, CanvasGroup group)
    {
        Time.timeScale = 0f; // the park held still as it fades
        for (float t = 0f; t < FadeTime; t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f)) // a hitch never skips it
        {
            group.alpha = t / FadeTime;
            yield return null;
        }
        group.alpha = 1f;
        var op = SceneManager.LoadSceneAsync(buildIndex);
        while (!op.isDone) yield return null;
        Time.timeScale = 1f;
        yield return null; // the menu builds itself in its first frames: still under the black
        yield return null;
        for (float t = 0f; t < FadeTime; t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f)) // a hitch never skips it
        {
            group.alpha = 1f - t / FadeTime;
            yield return null;
        }
        Destroy(gameObject);
    }

    void Build(RectTransform canvas)
    {
        var rig = Figma.Place(MainMenu.Ui("Ferris Wheel", canvas), HubX, HubY, 0f, 0f);
        Part(rig, "Base", "Loading/Base", BaseSize, BasePivot, new Vector2(0f, -BaseDrop));
        wheel = Part(rig, "Wheel", "Loading/Wheel", WheelSize, WheelPivot, Vector2.zero);
        for (int i = 0; i < Mounts.Length; i++)
        {
            var cabin = MainMenu.Ui($"Cabin {i + 1}", rig);
            cabin.sizeDelta = Scaled(CabinSize);
            cabin.pivot = CabinPivot;
            if (i == RuckusCabin) Ruckus(cabin); // behind the cabin's picture: in it, not on it
            Stretched(cabin, $"Loading/Cabin{i + 1}");
            cabins[i] = cabin;
        }
        PlaceCabins();

        var hub = Part(rig, "Hub", "Loading/Hub", new Vector2(HubSize, HubSize), new Vector2(0.5f, 0.5f), Vector2.zero);
        percent = MainMenu.Label(MainMenu.Ui("Percent", hub), GameText.LoadingPercent(0), Figma.Type.ArvoTitle, Ink, ParkArt.Font);
        percent.fontStyle = FontStyle.Bold;
        MainMenu.Stretch(percent.rectTransform);
    }

    // Ruckus as the spirit looks in the park: body (its idle loop), eyes, grin and hands stacked on one spot.
    void Ruckus(RectTransform cabin)
    {
        ruckus = MainMenu.Ui("Ruckus", cabin);
        ruckus.anchorMin = ruckus.anchorMax = Vector2.zero;
        ruckus.sizeDelta = Scaled(RuckusSize);
        ruckus.anchoredPosition = Scaled(new Vector2(RuckusAt.x, CabinSize.y - RuckusAt.y));
        ruckusIdle = new Sprite[4];
        for (int i = 0; i < 4; i++) ruckusIdle[i] = Resources.Load<Sprite>($"MC/MC_idle_{i + 1}");
        ruckusBody = Piece(ruckus, ruckusIdle[0]);
        Piece(ruckus, Resources.Load<Sprite>("MC/MC_face_eyes center"));
        Piece(ruckus, Resources.Load<Sprite>("MC/MC_face_mouth grin"));
        Piece(ruckus, Resources.Load<Sprite>("MC/hands"));
    }

    static Image Piece(RectTransform parent, Sprite sprite)
    {
        var img = Stretched(parent, null);
        img.sprite = sprite;
        return img;
    }

    static Image Stretched(RectTransform parent, string art)
    {
        var rt = MainMenu.Ui(art ?? "Piece", parent);
        MainMenu.Stretch(rt);
        var img = rt.gameObject.AddComponent<Image>();
        if (art != null) img.sprite = UiArt.Get(art);
        img.raycastTarget = false;
        return img;
    }

    // A picture of `size` art px with its `pivot` at `at` (art px from the hub).
    static RectTransform Part(RectTransform rig, string name, string art, Vector2 size, Vector2 pivot, Vector2 at)
    {
        var rt = MainMenu.Ui(name, rig);
        rt.sizeDelta = Scaled(size);
        rt.pivot = pivot;
        rt.anchoredPosition = Scaled(at);
        Stretched(rt, art);
        return rt;
    }

    static Vector2 Scaled(Vector2 artPx) => artPx * (ArtPx * Figma.K);

    // Each cabin hangs upright from its arm's bulb, wherever the wheel's turned to.
    void PlaceCabins()
    {
        var turn = Quaternion.Euler(0f, 0f, angle);
        for (int i = 0; i < cabins.Length; i++)
            cabins[i].anchoredPosition = Scaled(turn * (Mounts[i] * 450f));
    }

    // Unscaled time: the game's held still (time scale 0) while the park sets up.
    void Update()
    {
        if (wheel == null) return; // FadeTo: just the black
        float dt = Time.unscaledDeltaTime;
        angle += Spin * Mathf.Min(dt, 1f / 30f); // a frame the loading held up never makes it jump round

        wheel.localRotation = Quaternion.Euler(0f, 0f, angle);
        PlaceCabins();

        float t = Time.unscaledTime;
        ruckusBody.sprite = ruckusIdle[(int)(t * RuckusFps) % ruckusIdle.Length];
        ruckus.anchoredPosition = Scaled(new Vector2(RuckusAt.x, CabinSize.y - RuckusAt.y + Mathf.Sin(t * RuckusBobSpeed) * RuckusBob));

        // The count runs up smoothly to where the loading is, never back.
        shown = Mathf.MoveTowards(shown, target, dt * 1.5f);
        percent.text = GameText.LoadingPercent(Mathf.FloorToInt(shown * 100f + 0.001f));
    }

    // How far along it is: reading the park in (to 70%), switching to it (80%), its first frames settling (to 100%).
    void Reached(float k) => target = Mathf.Max(target, Mathf.Clamp01(k));

    IEnumerator Go(int buildIndex)
    {
        float start = Time.unscaledTime;
        yield return new WaitForSecondsRealtime(SpinUp); // seen turning before the loading starts
        previousPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = ThreadPriority.Low; // the loading takes less of each frame: the wheel keeps turning
        var op = SceneManager.LoadSceneAsync(buildIndex);
        op.allowSceneActivation = false;
        while (op.progress < 0.9f) // read in the background, the wheel still turning
        {
            Reached(op.progress / 0.9f * 0.7f);
            yield return null;
        }
        Reached(0.7f);
        yield return CharacterSprite.Preload(); // the crowd's art read in now, between turns of the wheel, not as the park starts
        Time.timeScale = 0f; // the new scene wakes up held still: no tutorial, no crowd wandering off unseen
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        Reached(0.8f);

        // Its first frames do the heavy setting up (spawning, the walking grid, lighting): wait for them to be over.
        int quick = 0;
        float settleFrom = Time.unscaledTime;
        while (quick < QuickFrames && Time.unscaledTime - settleFrom < SettleMax)
        {
            yield return null;
            quick = Time.unscaledDeltaTime <= QuickFrame ? quick + 1 : 0;
            Reached(0.8f + 0.2f * Mathf.Max((float)quick / QuickFrames, (Time.unscaledTime - settleFrom) / SettleMax));
        }
        Reached(1f);
        while (Time.unscaledTime - start < MinTime || shown < 1f) yield return null;
        yield return new WaitForSecondsRealtime(0.25f); // a moment on 100%
        Time.timeScale = 1f;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (running != this) return;
        running = null;
        Time.timeScale = 1f; // never leaves the game frozen
        if (previousPriority.HasValue) Application.backgroundLoadingPriority = previousPriority.Value;
    }
}
