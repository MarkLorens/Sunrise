using UnityEngine;
using UnityEngine.UI;

// Laying UI out straight from the design (the artist's Figma frames, 1512 x 982 px): every size and position below
// is in the design's own pixels, measured off the exported frames, and turned into canvas units here. Our canvases
// scale with the screen (1920 x 1080, matching width and height half and half: MainMenu.Screen), which makes a design
// pixel K canvas units; at the design's own screen size it lands pixel for pixel.
public static class Figma
{
    public const float Width = 1512f, Height = 982f;
    // The canvas scale at 1512 x 982: 2 ^ mean(log2(1512 / 1920), log2(982 / 1080)) = 0.846.
    public const float K = 1f / 0.846f;

    // `rt` centred at (x, y) in the design (from its top left), `w` x `h` design px.
    public static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2((x - Width * 0.5f) * K, (Height * 0.5f - y) * K);
        rt.sizeDelta = new Vector2(w * K, h * K);
        return rt;
    }

    // An artist's picture (UiArt), centred at (x, y), w x h design px.
    public static Image Picture(RectTransform parent, string name, string art, float x, float y, float w, float h)
    {
        var rt = MainMenu.Ui(name, parent);
        Place(rt, x, y, w, h);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = UiArt.Get(art);
        img.raycastTarget = false;
        return img;
    }

    // A line of text centred at (x, y); `size` is the canvas font size (solved so the text comes out as wide as the
    // design's).
    public static Text Words(RectTransform parent, string text, float x, float y, int size, Color color, Font font, FontStyle style = FontStyle.Normal)
    {
        var rt = MainMenu.Ui(text, parent);
        Place(rt, x, y, 1200f, size * 2f / K);
        var t = MainMenu.Label(rt, text, size, color, font);
        t.fontStyle = style;
        return t;
    }

    // The artist's type scale (design sizes, turned into canvas font sizes): Atma for titles, Arvo for the rest.
    public static class Type
    {
        public static readonly int AtmaPage = S(96), AtmaH1 = S(48), AtmaSpecial = S(40), AtmaH2 = S(36), AtmaBody = S(32);
        public static readonly int ArvoTitle = S(32), ArvoSelection = S(24), ArvoSubtitle = S(24), ArvoBody = S(20), ArvoSubtitle2 = S(16);
        static int S(float px) => Mathf.RoundToInt(px * K);
    }

    public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
}
