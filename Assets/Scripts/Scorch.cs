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
// It doesn't happen all at once. First it goes up in flames (Blaze: the grill's own fire art, all over it, springing
// up from its foot over CatchTime, a warm light flickering round it and sparks flying); then, under the flames, over
// BurnTime the scorch creeps across the building, the blotches and the top first, the holes last, a glowing line of
// fire running along its front edge as it spreads; and the flames die down, leaving the embers and the smoke. Loading
// a save, it's simply burnt.
//
// A building that's a tile (the locker room, on See Through) is first copied into a renderer of its own where the
// tile was drawn (as ToiletPrank does with the toilet block), and the tile cleared.
//
// Round a fire (BurnAround: the hot dog stand once the propane blows), the ground's scorched too, in sooty patches
// creeping out from the middle, and whatever's standing nearby (lamps, bins, trees, benches: StandingProps) burns.
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

    // flames: it goes up in flames first (Blaze), the scorch starting once they've caught; without, it just scorches
    // (what stands round a fire: BurnAround).
    public static void Burn(SpriteRenderer r, bool quiet, bool flames = true)
    {
        if (r == null || r.GetComponent<Burnt>() != null) return;
        var art = Make(r.sprite);
        if (art == null) return;
        flames &= !quiet;
        if (flames) Blaze.Light(r, art);

        // The glow: the embers' own picture over the building, unlit, a hair nearer the camera.
        var glow = new GameObject("Embers").AddComponent<SpriteRenderer>();
        glow.transform.SetParent(r.transform, false);
        glow.transform.localPosition = new Vector3(0f, 0f, -0.01f / Mathf.Max(0.01f, r.transform.lossyScale.z));
        glow.sharedMaterial = ParkArt.Material;
        glow.sortingLayerID = r.sortingLayerID;
        glow.sortingOrder = r.sortingOrder + 1;
        r.gameObject.AddComponent<Burnt>().Set(r, glow, art, quiet, flames ? Blaze.CatchTime : 0f);
    }

    // Round a fire at `center`: the ground scorched in patches out to `radius`, the nearest first, spreading over
    // BurnTime, and everything standing within `radius` burnt (the building on fire itself already is). quiet: as it
    // ends up, at once (loading a save).
    public static void BurnAround(Vector3 center, float radius, bool quiet)
    {
        foreach (var prop in StandingProps.Near(center, radius))
        {
            if (prop.picture != null && prop.picture.GetComponent<Burnt>() != null) continue;
            Burn(prop.Renderer(" (burnt)"), quiet, false);
        }

        // One big patch under the fire, the rest scattered round it; the further out, the later it catches.
        var rng = new System.Random(Mathf.RoundToInt(center.x * 7f + center.z * 13f));
        const int Patches = 8;
        for (int i = 0; i < Patches; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = i == 0 ? 0f : radius * Mathf.Lerp(0.35f, 0.85f, (float)rng.NextDouble());
            Vector3 at = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d;
            float size = i == 0 ? radius * 1.1f : Mathf.Lerp(1f, 2.2f, (float)rng.NextDouble());
            var r = ParkArt.Make("Scorched Ground", null, "Markers/shadow", 0.01f, Color.white, ParkArt.DecalOrder - 1, true);
            r.sprite = GroundScorch;
            var holder = r.transform.parent;
            holder.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            at.y = RaisedFloor.Height(at) + 0.012f;
            holder.position = at;
            holder.gameObject.AddComponent<Patch>().Set(r, size, quiet ? -1f : BurnTime * 0.8f * d / Mathf.Max(0.01f, radius));
        }
    }

    // A patch of scorched ground: soot black in the middle, burnt brown toward a ragged edge that fades out.
    static Sprite groundScorch;
    static Sprite GroundScorch
    {
        get
        {
            if (groundScorch != null) return groundScorch;
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v) + (Fbm(u * 2.5f + 4f, v * 2.5f + 9f) - 0.5f) * 0.55f; // a ragged edge
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.95f, 0.5f, r)) * 0.85f;
                    float fleck = Fbm(u * 9f + 2f, v * 9f + 6f);
                    Color c = Color.Lerp(Soot, Brown, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.9f, r)) * 0.7f);
                    c = Color.Lerp(c, Soot, Mathf.InverseLerp(0.6f, 0.75f, fleck)); // darker flecks of char
                    c.a = alpha;
                    px[y * N + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply();
            groundScorch = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N, 0, SpriteMeshType.FullRect);
            groundScorch.name = "scorched_ground";
            return groundScorch;
        }
    }

    // A patch spreading out from nothing once its turn comes (`delay`; below 0: there already), easing to a stop.
    class Patch : MonoBehaviour
    {
        const float SpreadTime = 1.6f;
        SpriteRenderer patch;
        float size, start;

        public void Set(SpriteRenderer r, float s, float delay)
        {
            patch = r;
            size = s;
            start = delay < 0f ? -100f : Time.time + delay;
            ParkArt.Fit(patch, delay < 0f ? size : 0.01f);
            if (delay < 0f) enabled = false;
        }

        void Update()
        {
            float t = Time.time - start;
            if (t < 0f) return;
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / SpreadTime), 3f);
            ParkArt.Fit(patch, Mathf.Max(0.01f, size * k));
            if (t >= SpreadTime) enabled = false;
        }
    }

    // A copy of the tile at `cell`, drawn where and as the tilemap drew it; the tile cleared. `what` goes on its name.
    internal static SpriteRenderer FromTile(Tilemap map, Vector3Int cell, Sprite sprite, string what = " (burnt)")
    {
        Matrix4x4 m = map.transform.localToWorldMatrix
                      * Matrix4x4.Translate(map.CellToLocalInterpolated(cell + map.tileAnchor))
                      * map.orientationMatrix * map.GetTransformMatrix(cell);
        var r = new GameObject(sprite.name + what).AddComponent<SpriteRenderer>();
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

    // A building going up in flames: the grill's fire art (Anim/hot_dog_fire_1, _2, cut down to the flames) all over
    // it, a row of big ones along its lower part and smaller ones higher up, each on a spot of the picture itself. They
    // spring up from its foot outward over CatchTime, flicker (the two frames, at their own pace, and a little
    // stretching), burn while the scorch creeps over it, then die down one by one. Unlit, so they glow at night; a warm
    // light flickers on everything round it meanwhile, and sparks fly. One blaze for pictures stacked on the same spot
    // (a stand's front and back).
    internal class Blaze : MonoBehaviour
    {
        public const float CatchTime = 1.2f; // the flames springing up all over it, before the scorch starts
        const float DieAt = CatchTime + BurnTime * 0.75f, DieTime = 2.5f;
        const float Fps = 7f;
        static readonly List<Blaze> burning = new();
        static Sprite[] frames;

        class Flame
        {
            public SpriteRenderer r;
            public float size, delay, die, phase;
        }

        readonly List<Flame> flames = new();
        Bounds area;
        Light lamp;
        float started, range, nextSpark;

        public static void Light(SpriteRenderer building, Art art)
        {
            Bounds b = building.bounds;
            burning.RemoveAll(x => x == null);
            foreach (var x in burning)
                if (Vector3.Distance(x.area.center, b.center) < Mathf.Max(b.extents.x, b.extents.y) * 0.5f) return; // already ablaze
            if (Frames() == null) return;
            var blaze = new GameObject("Blaze").AddComponent<Blaze>();
            blaze.Set(building, art);
            burning.Add(blaze);
        }

        // Both frames cut to where the flames are on the artist's canvas (the same spot in both), their feet at the bottom.
        static Sprite[] Frames()
        {
            if (frames != null) return frames;
            var a = ParkArt.Get("Anim/hot_dog_fire_1");
            var b = ParkArt.Get("Anim/hot_dog_fire_2");
            if (a == null || b == null) return null;
            frames = new[] { Cut(a), Cut(b) };
            return frames;
        }

        // The flames, cut out of the frame's canvas and read back (as Make reads a building), their flat bottom (where
        // they sat on the grill) faded out, so stacked on a building they rise out of it instead of standing on a line.
        static Sprite Cut(Sprite s)
        {
            // Of its 1785 x 1727 canvas, from the bottom left. The picture is its whole texture (its textureRect is only
            // the tight bit with something drawn, different in each frame), so the canvas maps straight onto it.
            var on = new Rect(361f, 82f, 485f, 323f);
            var tex = s.texture;
            float k = tex.width / s.rect.width;
            int w = Mathf.RoundToInt(on.width * k), h = Mathf.RoundToInt(on.height * k);
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, GraphicsFormat.R8G8B8A8_SRGB);
            Graphics.Blit(tex, rt);
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            var px = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp };
            px.ReadPixels(new Rect(Mathf.Round(on.x * k), Mathf.Round(on.y * k), w, h), 0, 0);
            RenderTexture.active = was;
            RenderTexture.ReleaseTemporary(rt);
            var c = px.GetPixels32();
            for (int y = 0; y < h; y++)
            {
                float fade = Mathf.SmoothStep(0f, 1f, y / (h * 0.45f)); // gone at the bottom, whole from just under halfway
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    c[i].a = (byte)(c[i].a * fade);
                }
            }
            px.SetPixels32(c);
            px.Apply();
            var cut = Sprite.Create(px, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), s.pixelsPerUnit * k, 0, SpriteMeshType.FullRect);
            cut.name = s.name + " (flames)";
            return cut;
        }

        void Set(SpriteRenderer building, Art art)
        {
            area = building.bounds;
            started = Time.time;
            float h = area.size.y, w = area.size.x;
            var cam = Camera.main;
            Vector3 toCam = cam != null ? -cam.transform.forward : Vector3.back; // along the view: no slide across the screen
            Vector3 foot = new(area.center.x, area.min.y, area.center.z);
            range = Mathf.Max(w, h);

            // Spots on the picture itself (not the see-through round it), spread across it: a row of big flames in its
            // lower part, smaller ones above.
            int across = Mathf.Clamp(Mathf.RoundToInt(w / (h * 0.32f)), 5, 11);
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < across - row * 2; i++)
                {
                    int n = across - row * 2;
                    float u0 = (float)i / n, u1 = (float)(i + 1) / n;
                    float v0 = row == 0 ? 0.02f : 0.4f, v1 = row == 0 ? 0.45f : 0.85f;
                    if (!Spot(art, u0, u1, v0, v1, out Vector2 px)) continue;
                    Vector3 local = new((px.x + 0.5f - art.pivot.x * art.w) / art.ppu, (px.y + 0.5f - art.pivot.y * art.h) / art.ppu, 0f);
                    Vector3 at = building.transform.TransformPoint(local);
                    var r = new GameObject("Flame").AddComponent<SpriteRenderer>();
                    r.transform.SetParent(transform, false);
                    // Slid straight toward the camera (as the toilet's geyser is: ToiletPrank.InFront), so the building,
                    // which writes depth, never hides them, but on screen they're still right on it; each a hair nearer.
                    r.transform.position = at + toCam * (1f + flames.Count * 0.01f);
                    r.transform.rotation = cam != null ? Quaternion.LookRotation(cam.transform.forward, Vector3.up) : Quaternion.identity;
                    r.sharedMaterial = ParkArt.Material;
                    r.sortingLayerID = building.sortingLayerID;
                    r.sortingOrder = building.sortingOrder + 2;
                    r.sprite = frames[0];
                    r.flipX = Random.value < 0.5f;
                    float size = h * (row == 0 ? Random.Range(0.42f, 0.58f) : Random.Range(0.28f, 0.42f));
                    float delay = CatchTime * 0.8f * Mathf.Clamp01(Vector3.Distance(at, foot) / Mathf.Max(0.01f, range * 0.75f));
                    flames.Add(new Flame { r = r, size = size, delay = delay, die = Random.Range(0f, 1.5f), phase = Random.value * 10f });
                    ParkArt.Fit(r, 0.01f);
                }

            // The fire's glow on everything round it.
            lamp = new GameObject("Fire Light").AddComponent<Light>();
            lamp.transform.SetParent(transform, false);
            lamp.transform.position = area.center + toCam * 1.2f;
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.55f, 0.2f);
            lamp.range = range * 1.6f;
            lamp.intensity = 0f;
            lamp.shadows = LightShadows.None;
        }

        // A random spot of the picture within (u0..u1, v0..v1) of it that isn't see-through, if there's one.
        static bool Spot(Art art, float u0, float u1, float v0, float v1, out Vector2 px)
        {
            for (int tries = 0; tries < 40; tries++)
            {
                int x = Mathf.Clamp(Mathf.FloorToInt(Random.Range(u0, u1) * art.w), 0, art.w - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt(Random.Range(v0, v1) * art.h), 0, art.h - 1);
                if (art.from[y * art.w + x].a < 200) continue;
                px = new Vector2(x, y);
                return true;
            }
            px = default;
            return false;
        }

        void Update()
        {
            float t = Time.time - started, alight = 0f;
            foreach (var f in flames)
            {
                float grow = Mathf.SmoothStep(0f, 1f, (t - f.delay) / 0.5f);
                float fade = 1f - Mathf.SmoothStep(0f, 1f, (t - DieAt - f.die) / DieTime);
                float k = grow * fade;
                alight = Mathf.Max(alight, k);
                f.r.enabled = k > 0.01f;
                if (!f.r.enabled) continue;
                f.r.sprite = frames[(int)(t * Fps + f.phase) % frames.Length];
                ParkArt.Fit(f.r, f.size * k * (1f + 0.08f * Mathf.Sin(t * 9f + f.phase)));
            }
            if (lamp != null) lamp.intensity = alight * (2.2f + 1.2f * Mathf.PerlinNoise(t * 6f, 0.5f));
            if (alight > 0.3f && Time.time >= nextSpark && flames.Count > 0)
            {
                nextSpark = Time.time + Random.Range(0.08f, 0.2f);
                var f = flames[Random.Range(0, flames.Count)];
                Fx.Burst("Fx/sparkle", f.r.transform.position + Vector3.up * f.size * 0.5f, 2, new Color(1f, 0.7f, 0.3f), 2.5f, 0.12f, 0.7f);
            }
            if (t > DieAt + DieTime + 1.6f) Destroy(gameObject); // burnt out: the embers and smoke carry on (Burnt)
        }

        void OnDestroy() => burning.Remove(this);
    }

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

        internal void Set(SpriteRenderer b, SpriteRenderer g, Art a, bool quiet, float delay = 0f)
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
            started = Time.time + delay; // the flames catching first
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
                else if (p > 0f) Spread(p);
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
