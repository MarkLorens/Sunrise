using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Between the main menu and the park, both ways (New Game, Load Game, Exit to Main Menu): the screen goes the design's
// blue with "Loading...." in the middle while the next scene loads in the background, and only lifts once it's ready
// to play smoothly: loaded, switched to, its first frames' setting up done (the park builds its crowd and paths then,
// with the game held still meanwhile so nothing happens unseen), and a run of quick frames in a row. At least MinTime,
// so it never just flickers; at most SettleMax waiting for the frames to calm. ponytail: plain blue for now, the
// design's Ferris wheel comes later.
public class LoadingScreen : MonoBehaviour
{
    static readonly Color Blue = new Color32(75, 134, 227, 255);
    const float MinTime = 0.6f, SettleMax = 4f, QuickFrame = 1f / 40f;
    const int QuickFrames = 10; // in a row, before it lifts
    static LoadingScreen running;

    public static bool Busy => running != null;

    public static void Load(int buildIndex)
    {
        if (running != null) return;
        var canvas = MainMenu.Screen("Loading Screen", 1000); // over everything
        DontDestroyOnLoad(canvas.gameObject);
        var bg = MainMenu.Ui("Blue", canvas);
        MainMenu.Stretch(bg);
        MainMenu.Img(bg, null, Blue).raycastTarget = true; // nothing underneath takes clicks meanwhile
        var label = MainMenu.Ui("Loading", canvas);
        MainMenu.Stretch(label);
        MainMenu.Label(label, "Loading....", Figma.Type.AtmaPage, Color.white, ParkArt.TitleFont);
        running = canvas.gameObject.AddComponent<LoadingScreen>();
        running.StartCoroutine(running.Go(buildIndex));
    }

    IEnumerator Go(int buildIndex)
    {
        yield return null; // drawn before the loading starts
        float start = Time.unscaledTime;
        var op = SceneManager.LoadSceneAsync(buildIndex);
        op.allowSceneActivation = false;
        while (op.progress < 0.9f) yield return null; // read in the background, the blue screen still drawing
        Time.timeScale = 0f; // the new scene wakes up held still: no tutorial, no crowd wandering off unseen
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        // Its first frames do the heavy setting up (spawning, the walking grid, lighting): wait for them to be over.
        int quick = 0;
        float settleFrom = Time.unscaledTime;
        while (quick < QuickFrames && Time.unscaledTime - settleFrom < SettleMax)
        {
            yield return null;
            quick = Time.unscaledDeltaTime <= QuickFrame ? quick + 1 : 0;
        }
        while (Time.unscaledTime - start < MinTime) yield return null;
        Time.timeScale = 1f;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (running != this) return;
        running = null;
        Time.timeScale = 1f; // never leaves the game frozen
    }
}
