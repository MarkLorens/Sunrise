using System.Collections.Generic;
using UnityEngine;

// Runtime art for bubbles, markers, effects, items and the HUD: Resources/Park (2D art from My project,
// Kenney packs, CC0) plus the older Resources/Icons. Things made here are unlit by default (bubbles, markers,
// effects); props standing in the world call Lit() so they're shaded and cast shadows like the scene's sprites.
public static class ParkArt
{
    public const int DecalOrder = -40;  // lying on the ground: above the ground tiles (-50), under anything standing
    public const int OverheadOrder = 5; // bubbles, bars and markers drawn over people

    static readonly Dictionary<string, Sprite> sprites = new();
    static Material material, litMaterial;
    static Font font;

    // Same shader as Palletes/Shaders/SpriteLit2.5 (lit, alpha clip, casts shadows, tinted by SpriteRenderer.color).
    public static Material LitMaterial
    {
        get
        {
            if (litMaterial == null) litMaterial = new Material(Shader.Find("Shader Graphs/SpriteLit3D")) { name = "Park Lit Sprite" };
            return litMaterial;
        }
    }

    // A standing sprite: lit, casts and receives shadows.
    public static SpriteRenderer Lit(SpriteRenderer r)
    {
        r.sharedMaterial = LitMaterial;
        CastShadows(r);
        return r;
    }

    public static void CastShadows(Renderer r)
    {
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        r.receiveShadows = true;
    }

    // Unlit sprite material that honours SpriteRenderer.color (for markers, bubbles, tints).
    public static Material Material
    {
        get
        {
            if (material == null) material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")) { name = "Park Unlit Sprite" };
            return material;
        }
    }

    public static Font Font => font != null ? font : font = Resources.Load<Font>("Park/Fonts/Fredoka");

    // Resources/Park/<path>, or Resources/<path> for the older icons.
    public static Sprite Get(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (!sprites.TryGetValue(path, out var s))
        {
            s = Resources.Load<Sprite>("Park/" + path);
            if (s == null) s = Resources.Load<Sprite>(path);
            sprites[path] = s;
        }
        return s;
    }

    // A child sprite `height` units tall in its parent's space. Flat ones lie on the ground on a child of the
    // returned renderer's parent ("holder"), so the holder can be moved and spun around the vertical axis.
    public static SpriteRenderer Make(string name, Transform parent, string sprite, float height, Color color, int order = 0, bool flat = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        if (flat)
        {
            var quad = new GameObject("Flat");
            quad.transform.SetParent(go.transform, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go = quad;
        }
        var r = go.AddComponent<SpriteRenderer>();
        r.sharedMaterial = Material;
        r.sprite = Get(sprite);
        r.color = color;
        r.sortingOrder = order;
        Fit(r, height);
        return r;
    }

    public static void Fit(SpriteRenderer r, float height)
    {
        if (r.sprite == null) return;
        r.transform.localScale = Vector3.one * (height / r.sprite.bounds.size.y);
    }
}
