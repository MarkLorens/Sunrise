using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Ruckus in the main menu's bottom right corner (placed as in the artist's mockup), as the spirit looks in the park (SpiritSprite): its idle loop (the
// flame flickering), the body and face bobbing and the hands bobbing further, in step. The eyes are the main menu's
// own (Arts/NewMainMenu): the whites (MCEye) and the irises (MCIris) apart, so the irises follow the mouse cursor
// a little inside the whites (clipped to them), and clicking him makes him blink (the eyes swap to the
// artist's closed ones, RuckusClosedEyes, for a moment), under the eyes' outline (Outline: MCEye's dark
// strokes alone, drawn again over the irises). Every piece is drawn on the same 711 x 628 canvas,
// already in place, so they're stacked on one spot.
public class MenuRuckus : MonoBehaviour
{
    // As in the artist's mockup: big, leaning in from the bottom right corner and running off both edges. Its size in
    // the design's px (the mockup drew it 1.84x on a 2000 px wide frame), its middle from the screen's bottom right
    // corner (design px, up and right), and its tilt (clockwise).
    const float W = 711f * 1.84f * Figma.Width / 2000f, H = 628f * 1.84f * Figma.Width / 2000f;
    static readonly Vector2 FromCorner = new(22f, 330f);
    const float Tilt = 12f;
    const float Fps = 8f, BobSpeed = 2f, BodyBob = 8f, HandBob = 16f; // bobs in the drawing's px; half the park's speed
    // The irises lean toward the cursor, not onto it: at most LookMax drawing px from where they're drawn, reached once
    // the cursor is LookReach drawing px from the eyes' middle (EyesMiddle: the whites' middle, from the canvas's,
    // y up). LookEase: how quickly they catch up (per second).
    const float LookMax = 14f, LookReach = 500f, LookEase = 8f;
    static readonly Vector2 EyesMiddle = new(-91f, -93f);
    // A blink: the closed eyes for BlinkTime seconds. Clicks land on him inside the body's oval or the hands' box
    // (drawing px from the top left).
    const float BlinkTime = 0.18f;
    // Every piece is multiplied by this: muted and a little purple, to sit in the night scene (matched to the mockup).
    static readonly Color Tint = new(0.76f, 0.77f, 0.83f);
    static readonly Rect BodyBox = Rect.MinMaxRect(92f, 38f, 667f, 577f), HandsBox = Rect.MinMaxRect(28f, 430f, 654f, 577f);

    RectTransform root, face, hands, whites, irises;
    Image closed;
    float blinkFrom = -1f;
    Image body;
    Sprite[] idle;
    float px; // canvas units per drawing px

    public static void Make(RectTransform canvas)
    {
        var rt = MainMenu.Ui("Ruckus", canvas);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f); // the bottom right corner, whatever the screen's shape
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(W, H) * Figma.K;
        rt.anchoredPosition = FromCorner * Figma.K;
        rt.localRotation = Quaternion.Euler(0f, 0f, -Tilt);
        rt.gameObject.AddComponent<MenuRuckus>().Build(rt);
    }

    void Build(RectTransform rt)
    {
        root = rt;
        px = W * Figma.K / 711f;
        idle = new Sprite[4];
        for (int i = 0; i < 4; i++) idle[i] = Resources.Load<Sprite>($"MC/MC_idle_{i + 1}");
        face = Layer(rt, "Face", null).rectTransform;
        body = Layer(face, "Body", idle[0]);
        var eyes = Layer(face, "Eyes", UiArt.Get("NewMainMenu/MCEye"));
        eyes.gameObject.AddComponent<Mask>().showMaskGraphic = true; // the irises only show inside the eyes
        whites = eyes.rectTransform;
        whites.pivot = new Vector2(0.5f + EyesMiddle.x / 711f, 0.5f + EyesMiddle.y / 628f); // looks from the eyes' middle
        irises = Layer(whites, "Irises", UiArt.Get("NewMainMenu/MCIris")).rectTransform;
        Layer(whites, "Eye Outline", Outline(eyes.sprite)); // the irises tuck under it
        closed = Layer(face, "Closed Eyes", UiArt.Get("NewMainMenu/RuckusClosedEyes"));
        closed.enabled = false;
        Layer(face, "Mouth", Resources.Load<Sprite>("MC/MC_face_mouth grin"));
        hands = Layer(rt, "Hands", Resources.Load<Sprite>("MC/hands")).rectTransform;
    }

    // `eye`'s dark strokes only (its whites see-through), read off the GPU: the UI art isn't kept readable.
    static Sprite Outline(Sprite eye)
    {
        if (eye == null) return null;
        // The whole picture, not its textureRect (only the trimmed bit round the strokes): every piece is stacked on
        // the same full canvas.
        int w = eye.texture.width, h = eye.texture.height;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(eye.texture, rt);
        var was = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        RenderTexture.active = was;
        RenderTexture.ReleaseTemporary(rt);
        var px = tex.GetPixels32();
        for (int i = 0; i < px.Length; i++)
        {
            var c = px[i];
            int bright = Mathf.Max(c.r, Mathf.Max(c.g, c.b)); // the strokes are near black (< 40), the whites near white (> 200)
            c.a = (byte)(c.a * Mathf.Clamp01((160 - bright) / 80f));
            px[i] = c;
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), eye.pixelsPerUnit);
    }

    static Image Layer(RectTransform parent, string name, Sprite sprite)
    {
        var rt = MainMenu.Ui(name, parent);
        MainMenu.Stretch(rt);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.enabled = sprite != null;
        img.raycastTarget = false;
        img.color = Tint;
        return img;
    }

    // Unscaled time: the menu's running whatever the time scale.
    void Update()
    {
        if (body == null) return;
        float t = Time.unscaledTime;
        body.sprite = idle[(int)(t * Fps) % idle.Length];
        float wave = Mathf.Sin(t * BobSpeed);
        face.anchoredPosition = new Vector2(0f, wave * BodyBob * px);
        hands.anchoredPosition = new Vector2(0f, wave * HandBob * px);

        // Toward the cursor (the overlay canvas needs no camera); no mouse, they rest where they're drawn.
        Vector2 to = Vector2.zero;
        var mouse = Mouse.current;
        if (mouse != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(whites, mouse.position.ReadValue(), null, out var at))
            to = Vector2.ClampMagnitude(at / (LookReach * px), 1f) * LookMax * px;

        if (mouse != null && mouse.leftButton.wasPressedThisFrame && t - blinkFrom > BlinkTime && OnHim(mouse.position.ReadValue()))
            blinkFrom = t;
        bool shut = t - blinkFrom < BlinkTime && closed.sprite != null;
        closed.enabled = shut;
        whites.gameObject.SetActive(!shut);
        irises.anchoredPosition = Vector2.Lerp(irises.anchoredPosition, to, 1f - Mathf.Exp(-LookEase * Time.unscaledDeltaTime));
    }

    bool OnHim(Vector2 screen)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var at)) return false;
        var d = new Vector2(at.x / px + 711f / 2f, 628f / 2f - at.y / px); // drawing px from the top left
        var o = (d - BodyBox.center) / (BodyBox.size / 2f);
        return o.sqrMagnitude <= 1f || HandsBox.Contains(d);
    }
}
