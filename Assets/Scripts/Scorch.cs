using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Tilemaps;

// A building that's been through a fire (the hot dog stand once the propane blows: PropaneTank; the locker room once
// it's alight: LockerRoom): its own picture redrawn burnt, swapped in on the same renderer, so it keeps the same
// material, light, shadows and see-through as before.
//
// The burnt picture is worked out from the artist's one (once per picture, then kept): blotches of scorch over it,
// brown going to black soot, heavier toward the top where the fire climbs; holes burnt right through, mostly up high
// (the roof, the top edge), each ringed with soot and a glowing orange rim; and a few embers in the blackest parts.
// The embers and rims are also drawn on their own, unlit, over the building (so they glow at night), flickering; and
// thin smoke keeps curling up off it.
//
// It doesn't happen all at once: over BurnTime the scorch creeps across the building, the blotches and the top
// first, the holes last, a glowing line of fire running along its front edge as it spreads. Loading a save, it's
// simply burnt.
//
// A building that's a tile (the locker room, on See Through) is first copied into a renderer of its own where the
// tile was drawn (as ToiletPrank does with the toilet block), and the tile cleared.
public static class Scorch
{
    const int MaxSize = 768;       // the burnt picture's longest side, in pixels (the artist's are ~2000: plenty at this size)
    public const float BurnTime = 8f; // seconds for the scorch to creep over the whole building

    static readonly Color Soot = new(0.07f, 0.055f, 0.055f), Brown = new(0.3f, 0.13f, 0.06f), Ember = new(1f, 0.42f, 0.08f);
    static readonly Dictionary<Sprite, Art> made = new();

    // A picture's burnt version: as it was (from), burnt (to), the embers' glow, and when each pixel catches (at:
    // 0 first .. 255 last), with the finished sprites for a building that's simply burnt.
    internal class Art
    {
        public int w, h;
        public Vector2 pivot;
        public float ppu;
        public Color32[] from, to, glow;
        public byte[] at;
        public Sprite burnt, embers;
    }

    // The renderers drawing `sprite`s whose name starts with one of `names`, within `radius` of `near`, burnt.
    // quiet: at once (loading a save); otherwise it spreads over BurnTime.
    public static void BurnNear(Vector3 near, float radius, bool quiet, params string[] names)
    {
        foreach (var r in Object.FindObjectsByType<SpriteRenderer>())
        {
            if (r.sprite == null || r.GetComponent<Burnt>() != null || !Named(r.sprite, names)) continue;
            if (r.name == "Stand Back") continue; // an ice cream stand's back (VisitorSpawner borrows the hot dog stand's)
            Vector3 d = r.transform.position - near;
            if (new Vector2(d.x, d.z).magnitude <= radius) Burn(r, quiet);
        }
        foreach (var map in Object.FindObjectsByType<Tilemap>())
            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                var s = map.GetSprite(cell);
                if (s == null || !Named(s, names)) continue;
                Vector3 c = map.GetCellCenterWorld(cell);
                if (new Vector2(c.x - near.x, c.z - near.z).magnitude <= radius) Burn(FromTile(map, cell, s), quiet);
            }
    }

    static bool Named(Sprite s, string[] names)
    {
        foreach (var n in names) if (s.name.StartsWith(n)) return true;
        return false;
    }

    public static void Burn(SpriteRenderer r, bool quiet)
    {
        if (r == null || r.GetComponent<Burnt>() != null) return;
        var art = Make(r.sprite);
        if (art == null) return;

        // The glow: the embers' own picture over the building, unlit, a hair nearer the camera.
        var glow = new GameObject("Embers").AddComponent<SpriteRenderer>();
        glow.transform.SetParent(r.transform, false);
        glow.transform.localPosition = new Vector3(0f, 0f, -0.01f / Mathf.Max(0.01f, r.transform.lossyScale.z));
        glow.sharedMaterial = ParkArt.Material;
        glow.sortingLayerID = r.sortingLayerID;
        glow.sortingOrder = r.sortingOrder + 1;
        r.gameObject.AddComponent<Burnt>().Set(r, glow, art, quiet);
    }

    // A copy of the tile at `cell`, drawn where and as the tilemap drew it; the tile cleared.
    static SpriteRenderer FromTile(Tilemap map, Vector3Int cell, Sprite sprite)
    {
        Matrix4x4 m = map.transform.localToWorldMatrix
                      * Matrix4x4.Translate(map.CellToLocalInterpolated(cell + map.tileAnchor))
                      * map.orientationMatrix * map.GetTransformMatrix(cell);
        var r = new GameObject(sprite.name + " (burnt)").AddComponent<SpriteRenderer>();
        r.transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);
        r.transform.localScale = m.lossyScale;
        r.sprite = sprite;
        r.color = map.GetColor(cell);
        var tr = map.GetComponent<TilemapRenderer>();
        if (tr != null)
        {
            r.sharedMaterial = tr.sharedMaterial;
            r.sortingLayerID = tr.sortingLayerID;
            r.sortingOrder = tr.sortingOrder;
            r.shadowCastingMode = tr.shadowCastingMode;
            r.receiveShadows = tr.receiveShadows;
        }
        map.SetTile(cell, null);
        return r;
    }

    // ---------------------------------------------------------------- the burnt picture

    static Art Make(Sprite src)
    {
        if (made.TryGetValue(src, out var done)) return done;
        Rect rect = src.textureRect;
        float k = Mathf.Min(1f, MaxSize / Mathf.Max(rect.width, rect.height));
        int w = Mathf.Max(1, Mathf.RoundToInt(rect.width * k)), h = Mathf.Max(1, Mathf.RoundToInt(rect.height * k));

        // The artist's pixels (any format, readable or not): the whole texture drawn down to size, then read back.
        var tex = src.texture;
        int tw = Mathf.Max(1, Mathf.RoundToInt(tex.width * k)), th = Mathf.Max(1, Mathf.RoundToInt(tex.height * k));
        var rt = RenderTexture.GetTemporary(tw, th, 0, GraphicsFormat.R8G8B8A8_SRGB);
        Graphics.Blit(tex, rt);
        var was = RenderTexture.active;
        RenderTexture.active = rt;
        var px = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        px.ReadPixels(new Rect(Mathf.Round(rect.x * k), Mathf.Round(rect.y * k), w, h), 0, 0);
        px.Apply();
        RenderTexture.active = was;
        RenderTexture.ReleaseTemporary(rt);

        var from = px.GetPixels32();
        var to = new Color32[from.Length];
        var glow = new Color32[from.Length];
        var at = new byte[from.Length];
        float seed = (src.name.GetHashCode() & 0xffff) * 0.013f;
        for (int y = 0; y < h; y++)
        {
            float v = (float)y / h;
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                Color c = from[i];
                to[i] = from[i];
                if (c.a < 0.05f) continue;
                float u = (float)x / w;
                float a = u * w / Mathf.Max(w, h), b = v * h / Mathf.Max(w, h); // square noise however the picture's shaped

                // Scorch: blotchy, heavier toward the top. Where it's heaviest (and highest) it catches first.
                float blotch = Fbm(a * 7f + 9.7f, b * 7f + seed);
                float early = Mathf.Clamp01(blotch * 0.6f + v * 0.45f);
                at[i] = (byte)(255f * Mathf.Clamp01(1f - early) * 0.9f);

                // Holes burnt through, mostly up high, and late; soot round each, then a glowing rim right at its edge.
                float hole = Fbm(a * 5f + seed, b * 5f + 3.1f) + v * 0.32f - 0.08f;
                if (hole > 0.8f)
                {
                    to[i] = new Color32(0, 0, 0, 0);
                    at[i] = (byte)Mathf.Lerp(at[i], 255f, 0.5f);
                    continue;
                }
                float rim = Mathf.InverseLerp(0.7f, 0.8f, hole);

                float scorch = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.8f, blotch + v * 0.3f));
                scorch = Mathf.Max(scorch, Mathf.InverseLerp(0.55f, 0.75f, hole)); // sooty round the holes
                Color d = Color.Lerp(c * 0.78f, Brown, scorch * 0.55f);
                d = Color.Lerp(d, Soot, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.95f, scorch)));

                // Embers: the rims, and specks in the blackest parts.
                float speck = scorch > 0.7f ? Mathf.InverseLerp(0.78f, 0.9f, Fbm(a * 40f + seed, b * 40f + 5.3f)) : 0f;
                float ember = Mathf.Max(rim * rim, speck);
                if (ember > 0f)
                {
                    d = Color.Lerp(d, Ember, ember * 0.8f);
                    glow[i] = new Color(Ember.r, Ember.g + 0.25f * ember, Ember.b, ember * c.a);
                }
                d.a = c.a;
                to[i] = d;
            }
        }

        var art = new Art
        {
            w = w, h = h, from = from, to = to, glow = glow, at = at,
            pivot = new(src.pivot.x / src.rect.width, src.pivot.y / src.rect.height),
            ppu = src.pixelsPerUnit * k,
        };
        px.SetPixels32(to);
        px.Apply();
        px.wrapMode = TextureWrapMode.Clamp;
        art.burnt = Sprite.Create(px, new Rect(0, 0, w, h), art.pivot, art.ppu, 0, SpriteMeshType.FullRect);
        art.burnt.name = src.name + " (burnt)";
        var emberTex = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp };
        emberTex.SetPixels32(glow);
        emberTex.Apply();
        art.embers = Sprite.Create(emberTex, new Rect(0, 0, w, h), art.pivot, art.ppu, 0, SpriteMeshType.FullRect);
        art.embers.name = src.name + " (embers)";
        made[src] = art;
        return art;
    }

    // Three layers of Perlin noise, 0..1.
    static float Fbm(float x, float y) =>
        (Mathf.PerlinNoise(x, y) * 0.55f + Mathf.PerlinNoise(x * 2.1f + 17f, y * 2.1f + 3f) * 0.3f
         + Mathf.PerlinNoise(x * 4.3f + 41f, y * 4.3f + 29f) * 0.15f);

    // On a burnt building: the scorch creeping over it (Spread), its embers flickering, wisps of smoke curling up off it
    // now and then.
    public class Burnt : MonoBehaviour
    {
        const float StepTime = 0.08f; // how often the spreading picture's redrawn
        const float Front = 40f;      // how wide the spreading edge is (of the 0..255 order)

        SpriteRenderer building, glow;
        Art art;
        Texture2D work, workGlow;
        Color32[] pixels, glowPixels;
        float started, nextStep, nextSmoke, phase;
        bool spreading;

        internal void Set(SpriteRenderer b, SpriteRenderer g, Art a, bool quiet)
        {
            building = b;
            glow = g;
            art = a;
            phase = Random.value * 10f;
            if (quiet)
            {
                Finish();
                return;
            }
            // Its own pictures while it spreads, redrawn as it goes.
            work = new Texture2D(a.w, a.h, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp };
            workGlow = new Texture2D(a.w, a.h, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp };
            pixels = new Color32[a.from.Length];
            glowPixels = new Color32[a.from.Length];
            building.sprite = Sprite.Create(work, new Rect(0, 0, a.w, a.h), a.pivot, a.ppu, 0, SpriteMeshType.FullRect);
            glow.sprite = Sprite.Create(workGlow, new Rect(0, 0, a.w, a.h), a.pivot, a.ppu, 0, SpriteMeshType.FullRect);
            started = Time.time;
            spreading = true;
            Spread(0f);
        }

        // At `p` (0..1 of the way through): each pixel from as it was to burnt as its turn comes, with a band of fire
        // along the edge where it's catching.
        void Spread(float p)
        {
            float reach = p * (255f + Front);
            var from = art.from;
            var to = art.to;
            var g = art.glow;
            var order = art.at;
            for (int i = 0; i < from.Length; i++)
            {
                if (from[i].a == 0)
                {
                    pixels[i] = from[i];
                    continue;
                }
                float m = (reach - order[i]) / Front;
                if (m <= 0f)
                {
                    pixels[i] = from[i];
                    glowPixels[i] = default;
                    continue;
                }
                if (m >= 1f)
                {
                    pixels[i] = to[i];
                    glowPixels[i] = g[i];
                    continue;
                }
                pixels[i] = Color32.Lerp(from[i], to[i], m);
                float edge = 1f - Mathf.Abs(m * 2f - 1f); // brightest halfway through catching
                byte fire = (byte)(edge * 220f * from[i].a / 255f);
                byte settled = (byte)(g[i].a * m);
                glowPixels[i] = fire > settled ? new Color32(255, (byte)(110 + 60 * edge), 30, fire) : new Color32(g[i].r, g[i].g, g[i].b, settled);
            }
            work.SetPixels32(pixels);
            work.Apply();
            workGlow.SetPixels32(glowPixels);
            workGlow.Apply();
        }

        // Burnt through: the finished pictures (shared), the working ones let go.
        void Finish()
        {
            spreading = false;
            building.sprite = art.burnt;
            glow.sprite = art.embers;
            if (work != null) Destroy(work);
            if (workGlow != null) Destroy(workGlow);
            work = workGlow = null;
            pixels = glowPixels = null;
        }

        void Update()
        {
            if (spreading && Time.time >= nextStep)
            {
                nextStep = Time.time + StepTime;
                float p = (Time.time - started) / BurnTime;
                if (p >= 1f) Finish();
                else Spread(p);
            }
            if (glow != null)
            {
                float f = 0.55f + 0.45f * Mathf.PerlinNoise(Time.time * 3f + phase, 0.3f);
                glow.color = new Color(1f, 1f, 1f, spreading ? 1f : f);
            }
            if (building == null || Time.time < nextSmoke) return;
            nextSmoke = Time.time + Random.Range(0.5f, 1.1f);
            Bounds bb = building.bounds;
            Vector3 at = new(Random.Range(bb.min.x, bb.max.x) * 0.8f + bb.center.x * 0.2f, Mathf.Lerp(bb.min.y, bb.max.y, Random.Range(0.6f, 0.95f)), bb.min.z - 0.1f);
            var r = ParkArt.Make("Smoke", null, "Fx/puff", Random.Range(0.7f, 1.2f), new Color(0.12f, 0.12f, 0.12f, 0.45f), ParkArt.OverheadOrder - 2);
            r.transform.position = at;
            var bit = r.gameObject.AddComponent<FxBit>();
            bit.velocity = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0.8f, 1.3f), 0f);
            bit.life = Random.Range(2f, 3f);
        }

        void OnDestroy()
        {
            if (work != null) Destroy(work);
            if (workGlow != null) Destroy(workGlow);
        }
    }
}
