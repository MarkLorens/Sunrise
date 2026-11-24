using System;
using UnityEngine;
using UnityEngine.UI;

// Credits, from the main menu, laid out off the artist's design (Figma: Credits): the page background (UI Asset/PageBG,
// filling the screen), "CREDITS", the studio's logo on its card (ImageCard, Logo/Max2_Studio) with the mentor under it,
// and the team two by two, each role on a pink strip (HeaderCredits) over the name. Back (the light blue pill,
// Buttons/Button2_Idle, its yellow version while picked) or Esc closes it.
public class CreditsMenu : MonoBehaviour
{
    static CreditsMenu open;
    public static bool IsOpen => open != null;

    static readonly Color Name = new Color32(254, 208, 62, 255), RoleInk = new Color32(13, 9, 11, 255);
    static readonly int NameSize = Figma.Type.AtmaSpecial, RoleSize = Figma.Type.ArvoSubtitle;

    // Role, name (a second line under it if any), where the role's strip sits and where the name's middle is, in the
    // design.
    static readonly (string role, string name, string name2, float x, float y, float nameY)[] Team =
    {
        ("GAME DESIGN", "KENNARD (KEAN)", "MAHIB", 817f, 324f, 389f),
        ("GAME ENGINEER", "MARK (MAX)", "PARDEDE", 1246.5f, 324f, 389f),
        ("GAME ARTIST", "DANU RADITYA", null, 817f, 561f, 622f),
        ("GAME ENGINEER", "AULIA AGUSTINA", null, 1246.5f, 561f, 622f),
        ("UI ARTIST / UX DESIGN", "AMANDA DOROTEA", null, 817f, 738f, 805f),
        ("SOUND DESIGNER", "ANTHONY MARTIN", null, 1246.5f, 738f, 805f),
    };

    Action onClose;
    int openedFrame;

    public static void Open(int order, Action onClose)
    {
        if (open != null) return;
        var canvas = MainMenu.Screen("Credits", order);
        open = canvas.gameObject.AddComponent<CreditsMenu>();
        open.onClose = onClose;
        open.openedFrame = Time.frameCount;
        open.Build(canvas);
    }

    void Build(RectTransform canvas)
    {
        // The page fills the screen whatever its shape (cropped rather than squashed).
        var page = MainMenu.Ui("Page", canvas);
        page.anchorMin = page.anchorMax = page.pivot = new Vector2(0.5f, 0.5f);
        var bg = page.gameObject.AddComponent<Image>();
        bg.sprite = UiArt.Get("PageBG");
        bg.raycastTarget = true; // nothing behind it gets clicked
        var fit = page.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = 2268f / 1479f;

        Figma.Words(canvas, "CREDITS", 295f, 111f, Figma.Type.AtmaPage, Color.white, ParkArt.TitleFont);

        Figma.Picture(canvas, "Card", "ImageCard", 343f, 475f, 484.6f, 476f);
        Figma.Picture(canvas, "Studio", "Logo/Max2_Studio", 351.5f, 497.8f, 534.8f, 519.5f).preserveAspect = true; // its own shape, not the box's
        Figma.Picture(canvas, "Mentor Strip", "Buttons/Button3_Active", 350.5f, 747.5f, 270f, 56.3f);
        Figma.Words(canvas, "CONSULTANT / MENTOR", 347.5f, 747.5f, Figma.Type.ArvoSubtitle2, RoleInk, ParkArt.Font);
        Figma.Words(canvas, "RUANTH THIJSSEN", 347.5f, 805.5f, Figma.Type.AtmaBody, Name, ParkArt.TitleFont);

        foreach (var (role, name, name2, x, y, nameY) in Team)
        {
            Figma.Picture(canvas, role, "HeaderCredits", x, y, 352f, 80f);
            Figma.Words(canvas, role, x, y - 4f, RoleSize, RoleInk, ParkArt.Font);
            Figma.Words(canvas, name, x, nameY, NameSize, Name, ParkArt.TitleFont);
            if (name2 != null) Figma.Words(canvas, name2, x, nameY + 51f, NameSize, Name, ParkArt.TitleFont);
        }

        var back = MainMenu.Pill(canvas, "Back", Close);
        Figma.Place((RectTransform)back.transform, 1324.5f, 897.5f, 213f, 63f);
        var art = back.GetComponent<Image>();
        art.sprite = UiArt.Get("Buttons/Button2_Idle");
        var swap = back.spriteState;
        swap.highlightedSprite = swap.selectedSprite = swap.pressedSprite = UiArt.Get("Buttons/Button2_Active");
        back.spriteState = swap;
        back.GetComponentInChildren<Text>().fontSize = Figma.Type.ArvoBody;
        this.back = back;
        MainMenu.Select(null); // as in the design: nothing picked until the mouse goes over Back or a key's pressed
    }

    Button back;

    void Update()
    {
        if (Time.frameCount != openedFrame && Controls.Back) Close();
        var es = UnityEngine.EventSystems.EventSystem.current;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        var pad = UnityEngine.InputSystem.Gamepad.current;
        bool key = (kb != null && kb.anyKey.wasPressedThisFrame) || (pad != null && pad.dpad.ReadValue() != Vector2.zero);
        if (es != null && es.currentSelectedGameObject == null && key) MainMenu.Select(back); // keys go to Back
    }

    void Close()
    {
        Destroy(gameObject);
        onClose?.Invoke();
    }

    void OnDestroy()
    {
        if (open == this) open = null;
    }
}
