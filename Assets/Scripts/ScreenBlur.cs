using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// A blurred picture of whatever was on screen behind a menu (the paused park, Load Game behind its delete question).
// Each time it's switched on it hides its own canvas for a frame, takes the screen as it is, and shows it shrunk down
// and stretched back up (soft, like out of focus), tinted. Shot keeps the sharp picture (Save Game's card picture).
[RequireComponent(typeof(RawImage))]
public class ScreenBlur : MonoBehaviour
{
    public Texture2D Shot { get; private set; }

    RawImage image;
    RenderTexture blurred;
    Canvas canvas;

    // Fills `parent`, under everything else in it.
    public static ScreenBlur Add(RectTransform parent, Color tint)
    {
        var rt = MainMenu.Ui("Blur", parent);
        MainMenu.Stretch(rt);
        rt.SetAsFirstSibling();
        var img = rt.gameObject.AddComponent<RawImage>();
        img.color = tint;
        return rt.gameObject.AddComponent<ScreenBlur>();
    }

    void Awake() => image = GetComponent<RawImage>();

    void OnEnable() => StartCoroutine(Take());

    // Switched off mid-shot: don't leave the menu hidden.
    void OnDisable()
    {
        if (canvas != null) canvas.enabled = true;
    }

    IEnumerator Take()
    {
        canvas = GetComponentInParent<Canvas>().rootCanvas;
        canvas.enabled = false;
        image.enabled = false;
        yield return new WaitForEndOfFrame();
        Free();
        Shot = ScreenCapture.CaptureScreenshotAsTexture();
        canvas.enabled = true;

        // Halve it down to a 16th, then back up to a quarter: each step's bilinear filtering softens it a little more.
        RenderTexture src = null;
        int w = Shot.width, h = Shot.height;
        for (int step = 0; step < 6; step++)
        {
            int k = step < 4 ? 1 << (step + 1) : 1 << (7 - step); // 2, 4, 8, 16, then 8, 4
            var dst = RenderTexture.GetTemporary(Mathf.Max(1, w / k), Mathf.Max(1, h / k), 0);
            dst.filterMode = FilterMode.Bilinear;
            if (src == null) Graphics.Blit(Shot, dst);
            else
            {
                Graphics.Blit(src, dst);
                RenderTexture.ReleaseTemporary(src);
            }
            src = dst;
        }
        blurred = new RenderTexture(src.width, src.height, 0) { filterMode = FilterMode.Bilinear };
        Graphics.Blit(src, blurred);
        RenderTexture.ReleaseTemporary(src);
        image.texture = blurred;
        image.enabled = true;
    }

    void Free()
    {
        if (Shot != null) Destroy(Shot);
        if (blurred != null) Destroy(blurred);
        Shot = null;
        blurred = null;
    }

    void OnDestroy() => Free();
}
