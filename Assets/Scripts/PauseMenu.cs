using UnityEngine;
using UnityEngine.SceneManagement;

// In-game pause: Esc (or a gamepad's Start) opens and closes it. The park freezes (time stops, sound pauses, the
// player's keys do nothing) behind "PAUSED" and the same kind of menu as the main menu's (MainMenu's builders), over
// the park blurred and dimmed (ScreenBlur). Save Game asks which slot (SaveSlotMenu) and saves with the blur's sharp
// shot of the park for the Load Game card; GameHud shows "Saving...". GameHud puts it on the player.
public class PauseMenu : MonoBehaviour
{
    public static bool Paused { get; private set; }

    GameObject screen;
    ScreenBlur blur;
    RectTransform column;
    UnityEngine.UI.Button settingsButton, saveButton;
    int menuClosedFrame = -1; // a screen on top of this one (settings, save slots) closed: its Esc isn't this one's

    void Awake()
    {
        Paused = false;
        var canvas = MainMenu.Screen("Pause Menu", 20); // over the HUD
        var dim = MainMenu.Ui("Dim", canvas);
        MainMenu.Stretch(dim);
        MainMenu.Img(dim, null, MainMenu.PauseShade).raycastTarget = true; // also stops clicks reaching the HUD
        blur = ScreenBlur.Add(canvas, Color.white);
        column = MainMenu.Column(canvas);
        MainMenu.Title(column, "PAUSED", 560f, 80);
        MainMenu.Button(column, "Continue", () => SetPaused(false));
        saveButton = MainMenu.Button(column, "Save Game", Save);
        settingsButton = MainMenu.Button(column, "Options", OpenSettings);
        MainMenu.Button(column, "Exit to Main Menu", BackToMainMenu);
        screen = canvas.gameObject;
        screen.SetActive(false);
    }

    // Coming from Load Game: put the park back the way it was saved, once everything in it has set itself up.
    System.Collections.IEnumerator Start()
    {
        yield return null;
        SaveGame.ApplyPending();
    }

    void Update()
    {
        // Esc in the settings or the save slots goes back to this one, not out of the pause.
        if (SettingsMenu.IsOpen || SaveSlotMenu.IsOpen || ConfirmDialog.IsOpen || Time.frameCount == menuClosedFrame) return;
        if (Controls.Pause) SetPaused(!Paused); // Esc, or a gamepad's Start
    }

    // The slots pop up in place of the menu (the park stays blurred behind).
    void Save()
    {
        if (!SaveGame.CanSave)
        {
            GameHud.Say(GameText.CantSave);
            return;
        }
        column.gameObject.SetActive(false);
        SaveSlotMenu.Open(21, blur.Shot, saved =>
        {
            menuClosedFrame = Time.frameCount;
            column.gameObject.SetActive(true);
            MainMenu.Select(saveButton);
            if (saved == true) GameHud.Saving();
            else if (saved == false) GameHud.Say(GameText.CantSave);
        });
    }

    // The settings screen takes the pause menu's place until Back.
    void OpenSettings()
    {
        screen.SetActive(false);
        SettingsMenu.Open(20, () =>
        {
            menuClosedFrame = Time.frameCount;
            screen.SetActive(true);
            MainMenu.Select(settingsButton);
        });
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
