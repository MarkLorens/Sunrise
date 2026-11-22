using System;
using UnityEngine;

// A Yes / No question on the artist's confirmation card (UI Asset/ConfirmationCard), over whatever's behind it blurred
// and dark: Load Game's Delete Slot, the pause menu's Save Game over another slot. Laid out off the design (Figma:
// Saving game - overwrite): the title in Atma, the question in Arvo, Yes and No pills side by side. No is picked first;
// Esc means No. It closes itself and then tells `answer` (true for Yes).
public class ConfirmDialog : MonoBehaviour
{
    public static bool IsOpen => open != null;
    static ConfirmDialog open;

    Action<bool> answer;

    public static void Open(int order, string title, string question, Action<bool> answer)
    {
        if (open != null) return;
        var canvas = MainMenu.Screen(title, order);
        open = canvas.gameObject.AddComponent<ConfirmDialog>();
        open.answer = answer;

        ScreenBlur.Add(canvas, Color.white); // what's behind, out of focus
        var shade = MainMenu.Ui("Shade", canvas);
        MainMenu.Stretch(shade);
        MainMenu.Img(shade, null, MainMenu.PauseShade).raycastTarget = true; // and dark, like the pause menu's

        Figma.Picture(canvas, "Card", "ConfirmationCard", 765.8f, 470.3f, 535f, 388f);
        Figma.Words(canvas, title, 769.5f, 412f, Figma.Type.AtmaH2, Color.white, ParkArt.TitleFont);
        Figma.Words(canvas, question, 769f, 468f, Figma.Type.ArvoSubtitle, Color.white, ParkArt.Font);
        var yes = MainMenu.Pill(canvas, "Yes", () => open.Close(true));
        Figma.Place((RectTransform)yes.transform, 654.5f, 545f, 211f, 63f);
        var no = MainMenu.Pill(canvas, "No", () => open.Close(false));
        Figma.Place((RectTransform)no.transform, 874f, 542f, 214f, 64f);
        yes.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnRight = no };
        no.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnLeft = yes };
        MainMenu.Select(no); // the safe answer first
    }

    void Update()
    {
        if (Controls.Back) Close(false);
    }

    void Close(bool yes)
    {
        if (open != this) return;
        open = null;
        Destroy(gameObject);
        answer?.Invoke(yes);
    }

    void OnDestroy()
    {
        if (open == this) open = null;
    }
}
