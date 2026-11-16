using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// In-game pause: Esc (or a gamepad's Start) opens and closes it. The park freezes (time stops, sound pauses, the
// player's keys do nothing) behind a dimmed screen with the same buttons as the main menu (MainMenu's builders).
// GameHud puts it on the player.
public class PauseMenu : MonoBehaviour
{
    public static bool Paused { get; private set; }

    GameObject screen;

    void Awake()
    {
        Paused = false;
        var canvas = MainMenu.Screen("Pause Menu", 20); // over the HUD
        var dim = MainMenu.Ui("Dim", canvas);
        MainMenu.Stretch(dim);
        MainMenu.Img(dim, null, new Color(0.02f, 0.01f, 0.05f, 0.6f)); // also stops clicks reaching the HUD
        var column = MainMenu.Column(canvas);
        MainMenu.Title(column, "Paused", 560f, 64);
        MainMenu.Button(column, "Continue", () => SetPaused(false));
        // ponytail: Save Game and Settings do nothing yet (no save system, no settings); wire them when those exist.
        MainMenu.Button(column, "Save Game", null);
        MainMenu.Button(column, "Settings", null);
        MainMenu.Button(column, "Back to Main Menu", BackToMainMenu);
        screen = canvas.gameObject;
        screen.SetActive(false);
    }

    void Update()
    {
        bool toggle = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                      || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
        if (toggle) SetPaused(!Paused);
    }

    void SetPaused(bool on)
    {
        Paused = on;
        screen.SetActive(on);
        Time.timeScale = on ? 0f : 1f;
        AudioListener.pause = on;
        PauseFmod(on);
        if (on) MainMenu.Select(screen.GetComponentInChildren<UnityEngine.UI.Button>()); // Continue
    }

    static void PauseFmod(bool on)
    {
        try
        {
            if (FMODUnity.RuntimeManager.IsInitialized) FMODUnity.RuntimeManager.GetBus("bus:/").setPaused(on);
        }
        catch (System.Exception) { } // no FMOD banks set up: nothing of FMOD's to pause
    }

    void BackToMainMenu()
    {
        SetPaused(false);
        SceneManager.LoadScene(0); // the main menu is first in Build Settings
    }

    // Leaving the scene while paused (or stopping Play mode): don't leave the game frozen.
    void OnDestroy()
    {
        if (Paused) SetPaused(false);
        if (screen != null) Destroy(screen);
    }
}
