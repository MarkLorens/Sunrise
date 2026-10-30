using UnityEngine;

// A small bar floating in the world (anger, throw power, possession grip),
// like the stamina/power bars over players in football games.
public class WorldBar : MonoBehaviour
{
    const float Height = 0.13f;

    SpriteRenderer back, fill;
    float width;

    public static WorldBar Create(Transform parent, Vector3 localPosition, float width = 0.8f)
    {
        var go = new GameObject("Bar");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        var bar = go.AddComponent<WorldBar>();
        bar.width = width;
        bar.back = ParkArt.Make("Back", go.transform, "Markers/white", Height, new Color(0.1f, 0.06f, 0.1f, 0.8f), ParkArt.OverheadOrder);
        bar.fill = ParkArt.Make("Fill", go.transform, "Markers/white", Height * 0.6f, Color.white, ParkArt.OverheadOrder + 1);
        Size(bar.back, width + 0.06f, Height);
        bar.Set(0f, Color.white, false);
        return bar;
    }

    // Green -> yellow -> red, for anger or power.
    public static Color Heat(float t) =>
        t < 0.5f ? Color.Lerp(new Color(0.45f, 0.9f, 0.4f), new Color(1f, 0.85f, 0.2f), t * 2f)
                 : Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.25f, 0.2f), (t - 0.5f) * 2f);

    public void Set(float value, Color color, bool visible)
    {
        back.enabled = fill.enabled = visible;
        if (!visible) return;
        value = Mathf.Clamp01(value);
        fill.color = color;
        Size(fill, width * value, Height * 0.6f);
        fill.transform.localPosition = new Vector3(-width * (1f - value) * 0.5f, 0f, -0.005f);
    }

    static void Size(SpriteRenderer r, float w, float h)
    {
        Vector3 s = r.sprite.bounds.size;
        r.transform.localScale = new Vector3(Mathf.Max(0.0001f, w / s.x), h / s.y, 1f);
    }
}
