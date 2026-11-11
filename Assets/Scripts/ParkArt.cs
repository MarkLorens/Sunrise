using System.Collections.Generic;
using UnityEngine;

// Runtime art for bubbles, markers, effects, items and the HUD: Resources/Park (2D art from My project,
// Kenney packs, CC0) plus the older Resources/Icons. Things made here are unlit by default (bubbles, markers,
// effects); props standing in the world call Lit() so they're shaded and cast shadows like the scene's sprites.
public static class ParkArt
{
    public const int DecalOrder = -40;  // lying on the ground: above the ground tiles (-50), under anything standing
    public const int OverheadOrder = 5; // bubbles, bars and markers drawn over people

    // Every standing card leans back this far (X axis), so it faces the camera straight on. Colliders stay upright.
    public static readonly Quaternion Card = Quaternion.Euler(45f, 0f, 0f);

    // "Up" along a standing card: rising 1 also means moving 1 away from the camera (Y and Z together, like the
    // tutorial's gravity rule), so things placed above a card stay on it instead of drifting toward the camera.
    public static readonly Vector3 CardUp = Card * Vector3.up;

    // The point `height` up a standing card from its feet.
    public static Vector3 Above(Vector3 feet, float height) => feet + CardUp * height;

    static readonly Dictionary<string, Sprite> sprites = new();
    static Material material, litMaterial, characterMaterial;
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

    // The same lit sprite shader for people (visitors, staff, the spirit), with the see-through hole switched off:
    // a person standing between you and the camera stays whole; only scenery opens up (SeeThrough.cs).
    public static Material CharacterMaterial
    {
        get
        {
            if (characterMaterial == null)
            {
                characterMaterial = new Material(LitMaterial) { name = "Park Character Sprite" };
                characterMaterial.SetFloat(SeeThroughCutId, 0f);
            }
            return characterMaterial;
        }
    }
    static readonly int SeeThroughCutId = Shader.PropertyToID("_SeeThroughCut");

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

    // A prop standing on `parent`'s position, `height` tall, that people walk around. Until there's art at
    // Resources/Park/<sprite> it's a block of `standIn` colour.
    public static SpriteRenderer Solid(Transform parent, string sprite, float height, Color standIn)
    {
        var r = Lit(Make("Sprite", parent, sprite, height, Color.white));
        if (r.sprite == null)
        {
            // ponytail: coloured block until the prop is drawn.
            r.sprite = Get("Markers/white");
            r.color = standIn;
            Vector3 size = r.sprite.bounds.size;
            r.transform.localScale = new Vector3(height * 0.7f / size.x, height / size.y, 1f); // the marker isn't square
        }
        // Lean the card 45° about its feet, whatever the parent's own rotation, so its bottom edge stays on the floor.
        Quaternion lean = Quaternion.Inverse(parent.rotation) * Card;
        r.transform.localRotation = lean;
        r.transform.localPosition = lean * new Vector3(0f, height * 0.5f, 0f);
        // The collider stays upright on its own child (one that leaned with the art would cover the wrong patch of floor).
        var holder = new GameObject("Collider (upright)");
        holder.transform.SetParent(parent, false);
        var box = holder.AddComponent<BoxCollider>();
        Bounds b = r.sprite.bounds;
        Vector3 s = r.transform.localScale;
        box.center = new Vector3(b.center.x * s.x, height * 0.5f + b.center.y * s.y, 0f);
        box.size = new Vector3(b.size.x * s.x, b.size.y * s.y, 0.2f);
        GroundWalker.AddObstacle(box);
        return r;
    }

    public static void Fit(SpriteRenderer r, float height)
    {
        if (r.sprite == null) return;
        r.transform.localScale = Vector3.one * (height / r.sprite.bounds.size.y);
    }
}
