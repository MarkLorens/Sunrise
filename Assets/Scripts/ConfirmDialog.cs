using System;
using UnityEngine;

// A Yes / No question on the blue panel (UI/Menu/bg_main), over whatever's behind it blurred and darker: Load Game's
// Delete Slot, the pause menu's Save Game over another slot. No is picked first; Esc means No. It closes itself and
// then tells `answer` (true for Yes).
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

        ScreenBlur.Add(canvas, new Color(0.3f, 0.3f, 0.35f)); // what's behind, out of focus and darker
        var shade = MainMenu.Ui("Shade", canvas);
        MainMenu.Stretch(shade);
        MainMenu.Img(shade, null, new Color(0f, 0f, 0.02f, 0.45f)).raycastTarget = true;

        // Measured off the design (Delete Slot): the title 39 in Atma, the question in Arvo, Yes and No side by side.
        var panel = MainMenu.Ui("Panel", canvas);
        Place(panel, Vector2.zero, new Vector2(580f, 400f));
        MainMenu.Img(panel, "UI/Menu/bg_main", Color.white).pixelsPerUnitMultiplier = 0.8f;
        MainMenu.Label(Place(MainMenu.Ui("Title", panel), new Vector2(0f, 70f), new Vector2(500f, 70f)), title, 39, Color.white, ParkArt.TitleFont);
        MainMenu.Label(Place(MainMenu.Ui("Question", panel), new Vector2(0f, 8f), new Vector2(500f, 40f)), question, 30, Color.white, ParkArt.Font);
        var yes = MainMenu.Pill(panel, "Yes", () => open.Close(true), 210f, 58f);
        Place((RectTransform)yes.transform, new Vector2(-122f, -82f), yes.GetComponent<RectTransform>().sizeDelta);
        var no = MainMenu.Pill(panel, "No", () => open.Close(false), 210f, 58f);
        Place((RectTransform)no.transform, new Vector2(122f, -82f), no.GetComponent<RectTransform>().sizeDelta);
        yes.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnRight = no };
        no.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnLeft = yes };
        MainMenu.Select(no); // the safe answer first
    }

    static RectTransform Place(RectTransform rt, Vector2 at, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = at;
        rt.sizeDelta = size;
        return rt;
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
