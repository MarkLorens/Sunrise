using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

// The toilet block after its geyser (ToiletPrank): poop all over it, and puddles of it on the ground round it.
//
// The block: its own picture redrawn splattered, swapped in on the same renderer (as Scorch does a burnt building),
// so it keeps the same material, night light, shadows and see-through. Worked out from the artist's picture: splats
// round the hole in the roof (not in it: the hole stays dark), thinning out over the roof, each a lobed blob with a few
// droplets round it, a darker rim like the art's ink, and now and then a wet shine; smaller splashes on the walls and
// doors; drips running off the roof's front edge down the walls; specks everywhere. Only on the block itself.
// It isn't there all at once: the splats land one after another while the geyser gushes (SplatTime), and each drip
// runs down after its splat's landed.
//
// The puddles: the artist's puddle (Fx/poop_puddle, recoloured from the fountain's water puddle), a few round the
// block's front, each spreading out and easing to a stop.
public static class PoopStain
{
    const int MaxSize = 768; // the splattered picture's longest side, in pixels
    public const float SplatTime = 2.6f; // seconds for all the splats to land
    const float DripTime = 1.2f;         // a drip running down the wall

    static readonly Color Dark = new(0.36f, 0.21f, 0.08f), Mid = new(0.56f, 0.35f, 0.14f);
    static readonly Color Ink = new(0.24f, 0.13f, 0.06f), Shine = new(0.78f, 0.58f, 0.33f);
    // How much of the stain covers what's under it: see-through, so the roof's own hatching and lines show through and
    // it sits on the building rather than pasted over it; thicker in a splat's middle than at its edge.
    const float Thin = 0.55f, Thick = 0.8f, RimCover = 0.7f;
    static readonly Dictionary<Sprite, Art> made = new();

    class Art
    {
        public int w, h;
        public Vector2 pivot;
        public float ppu;
        public Color32[] from, to;
        public int[] order; // the stained pixels, in the order they're hit
        public float[] at;  // when each pixel's hit (seconds), per pixel
        public Sprite done;
    }

    // `block` splattered. hole: the roof's hole, as a share of its picture (0..1 across, 0..1 up from the bottom).
    // quiet: all at once.
    public static void Splatter(SpriteRenderer block, Vector2 hole, bool quiet = false)
    {
        if (block == null || block.sprite == null || block.GetComponent<Stained>() != null) return;
        var art = Make(block.sprite, hole);
        if (art != null) block.gameObject.AddComponent<Stained>().Set(block, art, quiet);
    }

    // A few puddles of it on the ground in front of the block (`door`: the floor at its door; `block`: its picture),
    // spreading out one after another.
    public static void Puddles(Vector3 door, Bounds block)
    {
        float half = block.extents.x;
        // Along its foot, just out from the walls, and one by each corner (from the block's own plane: the door spot
        // is where people stand, a step out in front).
        var spots = new[]
        {
            new Vector3(-0.3f * half, 0f, -0.45f), new Vector3(0.45f * half, 0f, -0.8f), new Vector3(-0.8f * half, 0f, -0.3f),
            new Vector3(0.9f * half, 0f, -0.35f), new Vector3(0.05f * half, 0f, -1.15f),
        };
        var sizes = new[] { 1.15f, 0.9f, 0.65f, 0.7f, 0.5f };
        Vector3 front = new(block.center.x, door.y, block.center.z);
        for (int i = 0; i < spots.Length; i++)
        {
            var r = ParkArt.Make("Poop Puddle", null, "Fx/poop_puddle", 0.01f, Color.white, ParkArt.DecalOrder, true);
            if (r.sprite == null) r.sprite = ParkArt.Get("Markers/shadow"); // the art's missing: a brown blob
            // A touch darker than drawn: on the lit ground it otherwise glowed orange.
            r.color = r.sprite.name == "poop_puddle" ? new Color(0.72f, 0.72f, 0.72f, 0.92f) : new Color(Mid.r, Mid.g, Mid.b, 0.9f);
            var holder = r.transform.parent;
            holder.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f); // not the same blob every time
            Vector3 at = front + spots[i] + new Vector3(Random.Range(-0.2f, 0.2f), 0f, Random.Range(-0.15f, 0.15f));
            at.y = RaisedFloor.Height(at) + 0.015f;
            holder.position = at;
            holder.gameObject.AddComponent<Spreading>().Set(r, sizes[i] * Random.Range(0.85f, 1.15f), 0.3f + i * 0.35f);
        }
    }

    // ---------------------------------------------------------------- the splattered picture

    static Art Make(Sprite src, Vector2 hole)
    {
        if (made.TryGetValue(src, out var done)) return done;
        Rect rect = src.textureRect;
        float k = Mathf.Min(1f, MaxSize / Mathf.Max(rect.width, rect.height));
        int w = Mathf.Max(1, Mathf.RoundToInt(rect.width * k)), h = Mathf.Max(1, Mathf.RoundToInt(rect.height * k));

        // The artist's pixels (readable or not): the whole texture drawn down to size, then read back (as Scorch).
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

        var p = new Painter(w, h, px.GetPixels32(), src.name.GetHashCode());
        p.Paint(new Vector2(hole.x * w, hole.y * h));
        var art = new Art
        {
            w = w, h = h, from = p.from, to = p.Colour(), at = p.at, order = p.Order(),
            pivot = new(src.pivot.x / src.rect.width, src.pivot.y / src.rect.height),
            ppu = src.pixelsPerUnit * k,
        };
        px.SetPixels32(art.to);
        px.Apply();
        px.wrapMode = TextureWrapMode.Clamp;
        art.done = Sprite.Create(px, new Rect(0, 0, w, h), art.pivot, art.ppu, 0, SpriteMeshType.FullRect);
        art.done.name = src.name + " (splattered)";
        made[src] = art;
        return art;
    }

    // Draws the splats into a coverage mask (in the picture's pixels, y up), each pixel with the time it's hit.
    class Painter
    {
        readonly int w, h;
        public readonly Color32[] from;
        public readonly float[] at;
        readonly float[] shine;
        readonly System.Random rng;

        public Painter(int w, int h, Color32[] from, int seed)
        {
            this.w = w;
            this.h = h;
            this.from = from;
            at = new float[w * h];
            shine = new float[w * h];
            for (int i = 0; i < at.Length; i++) at[i] = float.MaxValue;
            rng = new System.Random(seed);
        }

        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        float Gauss() => Mathf.Sqrt(-2f * Mathf.Log(Mathf.Max(1e-6f, (float)rng.NextDouble()))) * Mathf.Cos(2f * Mathf.PI * (float)rng.NextDouble());
        bool Solid(float x, float y) => x >= 0 && y >= 0 && x < w && y < h && from[(int)y * w + (int)x].a >= 128;

        void Hit(int x, int y, float t)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int i = y * w + x;
            if (from[i].a < 128) return; // only on the block
            if (t < at[i]) at[i] = t;
        }

        void Dot(float cx, float cy, float r, float t)
        {
            for (int y = (int)(cy - r - 1); y <= (int)(cy + r + 1); y++)
                for (int x = (int)(cx - r - 1); x <= (int)(cx + r + 1); x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) < r * r) Hit(x, y, t);
        }

        // A lobed blob (its edge wobbling round it), a few droplets round it, sometimes a wet shine up and to the left.
        void Splat(float cx, float cy, float r, bool wet, float t)
        {
            float s = Rand(0f, 10f);
            for (int y = (int)(cy - r * 1.6f); y <= (int)(cy + r * 1.6f); y++)
                for (int x = (int)(cx - r * 1.6f); x <= (int)(cx + r * 1.6f); x++)
                {
                    float dx = x - cx, dy = y - cy, d = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
                    float lobes = 0.5f + 0.25f * Mathf.Sin(3f * a + s) + 0.15f * Mathf.Sin(5f * a + s * 1.7f) + 0.1f * Mathf.Sin(9f * a + s * 2.3f);
                    if (d >= r * (0.7f + 0.6f * lobes)) continue;
                    Hit(x, y, t);
                    if (!wet || x < 0 || y < 0 || x >= w || y >= h) continue;
                    float g = Mathf.Sqrt((dx + r * 0.3f) * (dx + r * 0.3f) + (dy - r * 0.3f) * (dy - r * 0.3f)) / (r * 0.35f);
                    if (g < 1f) shine[y * w + x] = Mathf.Max(shine[y * w + x], (1f - g) * 0.6f);
                }
            int drops = rng.Next(2, 6);
            for (int i = 0; i < drops; i++)
            {
                float a = Rand(0f, 2f * Mathf.PI), dd = r * Rand(1.3f, 2.1f);
                Dot(cx + Mathf.Cos(a) * dd, cy + Mathf.Sin(a) * dd, r * Rand(0.12f, 0.25f), t);
            }
        }

        // Running down from (x, top): thinning a little, wavering, with a drop at the bottom.
        void Drip(float x, float top, float length, float width, float t)
        {
            for (int k = 0; k < (int)length; k++)
            {
                float ww = width * (1f - 0.35f * k / length), xx = x + Mathf.Sin(k * 0.05f + x) * 1.5f;
                float when = t + k / length * DripTime;
                for (int xi = (int)(xx - ww); xi <= (int)(xx + ww); xi++) Hit(xi, (int)(top - k), when);
            }
            Dot(x, top - length, width * 1.5f, t + DripTime);
        }

        public void Paint(Vector2 hole)
        {
            // Round the hole's rim, thinning out over the roof (more of it below the hole, down the roof's slope).
            for (int i = 0; i < 40; i++)
            {
                float a = Rand(0f, 2f * Mathf.PI), ring = 1f + Mathf.Abs(Gauss()) * 1.6f;
                float cx = hole.x + Mathf.Cos(a) * ring * 0.15f * w;
                float cy = hole.y + Mathf.Sin(a) * ring * 0.07f * h - (ring - 1f) * 0.06f * h;
                if (!Solid(cx, cy)) continue;
                float r = Mathf.Max(4f, 0.045f * w * Mathf.Exp(-(ring - 1f) / 1.6f)) * Rand(0.6f, 1.2f);
                Splat(cx, cy, r, rng.NextDouble() < 0.18, SplatTime * Mathf.Clamp01((ring - 1f) / 3f * 0.7f + Rand(0f, 0.3f)));
            }
            // Splashed down onto the walls and doors.
            for (int i = 0; i < 14; i++)
            {
                float cx = Rand(0.28f, 0.9f) * w, cy = (1f - Rand(0.55f, 0.92f)) * h;
                if (Solid(cx, cy)) Splat(cx, cy, Rand(0.008f, 0.022f) * w, false, SplatTime * Rand(0.4f, 1f));
            }
            // Off the roof's front edge and down the walls.
            for (int i = 0; i < 11; i++)
            {
                float x = Rand(0.3f, 0.88f) * w, top = (1f - Rand(0.5f, 0.56f)) * h;
                if (!Solid(x, top)) continue;
                float t = SplatTime * Rand(0.35f, 0.75f);
                Dot(x, top, Rand(5f, 9f), t);
                Drip(x, top, Rand(0.06f, 0.28f) * h, Rand(2f, 3.6f), t);
            }
            // Specks all over.
            for (int i = 0; i < 90; i++)
            {
                float cx = hole.x + Gauss() * 0.3f * w, cy = hole.y - 0.12f * h + Gauss() * 0.25f * h;
                if (Solid(cx, cy)) Dot(cx, cy, Rand(1.5f, 3.5f), SplatTime * Rand(0f, 1f));
            }
        }

        bool Stained(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && at[y * w + x] < float.MaxValue;

        static readonly Vector2Int[] Ring =
            { new(4, 0), new(-4, 0), new(0, 4), new(0, -4), new(3, 3), new(-3, 3), new(3, -3), new(-3, -3) };

        // The splattered picture: blotchy browns (noise, not a pattern), laid over the picture see-through (thicker in
        // the middle of a splat), a softer darker rim at each splat's edge, now and then a faint shine.
        public Color32[] Colour()
        {
            var to = (Color32[])from.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (at[i] == float.MaxValue) continue;
                    bool rim = false;
                    for (int dy = -2; dy <= 2 && !rim; dy += 2)
                        for (int dx = -2; dx <= 2 && !rim; dx += 2)
                            if (!Stained(x + dx, y + dy)) rim = true;
                    // How far in from the edge: the share of points round it (4 px out) that are stained too.
                    int inside = 0;
                    for (int k = 0; k < 8; k++)
                        if (Stained(x + Ring[k].x, y + Ring[k].y)) inside++;
                    float vary = Mathf.PerlinNoise(x * 0.07f + 3.1f, y * 0.07f + 7.7f) * 0.6f + Mathf.PerlinNoise(x * 0.2f + 11f, y * 0.2f + 5f) * 0.4f;
                    float cover = Mathf.Lerp(Thin, Thick, inside / 8f) * (0.9f + 0.2f * (Mathf.PerlinNoise(x * 0.11f + 21f, y * 0.11f + 2f) - 0.5f));
                    Color c = Color.Lerp(Dark, Mid, vary);
                    c = Color.Lerp(c, Shine, shine[i] * 0.6f);
                    if (rim)
                    {
                        c = Ink;
                        cover = RimCover;
                    }
                    Color was = from[i];
                    c = Color.Lerp(was, c, cover);
                    c.a = was.a;
                    to[i] = c;
                }
            return to;
        }

        // The stained pixels, in the order they're hit.
        public int[] Order()
        {
            var list = new List<int>();
            for (int i = 0; i < at.Length; i++) if (at[i] < float.MaxValue) list.Add(i);
            list.Sort((a, b) => at[a].CompareTo(at[b]));
            return list.ToArray();
        }
    }

    // On the block: the splats landing as their turn comes, then the finished picture.
    class Stained : MonoBehaviour
    {
        const float StepTime = 0.05f; // how often the picture's redrawn while it's being hit

        SpriteRenderer block;
        Art art;
        Texture2D work;
        Color32[] pixels;
        int next; // the next pixel in `order` to land
        float started, nextStep;

        public void Set(SpriteRenderer b, Art a, bool quiet)
        {
            block = b;
            art = a;
            if (quiet)
            {
                block.sprite = art.done;
                enabled = false;
                return;
            }
            work = new Texture2D(a.w, a.h, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp };
            pixels = (Color32[])a.from.Clone();
            work.SetPixels32(pixels);
            work.Apply();
            block.sprite = Sprite.Create(work, new Rect(0, 0, a.w, a.h), a.pivot, a.ppu, 0, SpriteMeshType.FullRect);
            started = Time.time;
        }

        void Update()
        {
            if (art == null || Time.time < nextStep) return;
            nextStep = Time.time + StepTime;
            float now = Time.time - started;
            var order = art.order;
            bool changed = false;
            while (next < order.Length && art.at[order[next]] <= now)
            {
                int i = order[next++];
                pixels[i] = art.to[i];
                changed = true;
            }
            if (next >= order.Length)
            {
                // All landed: the finished picture (shared), the working one let go.
                block.sprite = art.done;
                Destroy(work);
                work = null;
                pixels = null;
                enabled = false;
                return;
            }
            if (!changed) return;
            work.SetPixels32(pixels);
            work.Apply();
        }

        void OnDestroy()
        {
            if (work != null) Destroy(work);
        }
    }

    // A puddle spreading out after `delay`, fast at first and easing to a stop.
    class Spreading : MonoBehaviour
    {
        const float SpreadTime = 1.4f;
        SpriteRenderer puddle;
        float size, start;

        public void Set(SpriteRenderer r, float s, float delay)
        {
            puddle = r;
            size = s;
            start = Time.time + delay;
        }

        void Update()
        {
            if (puddle == null) return;
            float t = Time.time - start;
            if (t < 0f) return;
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / SpreadTime), 3f);
            ParkArt.Fit(puddle, Mathf.Max(0.01f, size * k));
            if (t >= SpreadTime) enabled = false;
        }
    }
}
