using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Everything standing in the park that isn't a person: the buildings, trees, lamps and benches drawn on the Objects and
// See Through tilemaps, and the props with pictures of their own (stalls, signs, bins...). For whatever happens to the
// park round them: the runaway Ferris wheel squashing them (Squash), a fire scorching them (Scorch.BurnAround).
// Not: people (and the spirit), anything lying flat on the ground, the ground itself, effects (drawn unlit), dropped
// things (Item).
public static class StandingProps
{
    // One of them: a tile (map, cell) until something needs it as a picture of its own (Renderer), or a picture.
    public class Prop
    {
        public Tilemap map;
        public Vector3Int cell;
        public SpriteRenderer picture;
        public Vector3 at;   // where it stands (its feet)
        public float half;   // half its width
        public string Name => picture != null ? picture.sprite.name : map.GetSprite(cell).name;

        // Its picture; a tile is copied into one of its own where it was drawn (and the tile cleared).
        public SpriteRenderer Renderer(string what)
        {
            if (picture == null && map != null && map.GetSprite(cell) != null)
                picture = Scorch.FromTile(map, cell, map.GetSprite(cell), what);
            return picture;
        }
    }

    static readonly string[] Maps = { "Objects", "See Through" };

    // Everything standing within `radius` of `near` (flat distance, to its feet), except what `skip` turns down.
    public static List<Prop> Near(Vector3 near, float radius, System.Func<Transform, bool> skip = null)
    {
        var found = new List<Prop>();
        foreach (var map in Object.FindObjectsByType<Tilemap>())
        {
            if (System.Array.IndexOf(Maps, map.name) < 0) continue;
            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                var s = map.GetSprite(cell);
                if (s == null) continue;
                Vector3 c = map.GetCellCenterWorld(cell);
                if (Flat(c - near) > radius) continue;
                found.Add(new Prop { map = map, cell = cell, at = c, half = s.bounds.extents.x * map.transform.lossyScale.x });
            }
        }
        var pictures = new HashSet<SpriteRenderer>();
        foreach (var r in Object.FindObjectsByType<SpriteRenderer>())
            if (Standing(r) && (skip == null || !skip(r.transform)) && Flat(r.transform.position - near) <= radius) pictures.Add(r);
        foreach (var r in pictures)
        {
            // One picture inside another (a burnt building's glow): it goes with its parent.
            bool inside = false;
            for (var p = r.transform.parent; p != null && !inside; p = p.parent)
                inside = p.TryGetComponent(out SpriteRenderer pr) && pictures.Contains(pr);
            if (!inside) found.Add(new Prop { picture = r, at = r.transform.position, half = r.bounds.extents.x });
        }
        return found;
    }

    static bool Standing(SpriteRenderer r)
    {
        if (r == null || !r.enabled || r.sprite == null || !r.gameObject.activeInHierarchy) return false;
        if (r.sortingOrder <= ParkArt.DecalOrder || r.name == "Flat" || r.name == "Embers") return false; // lying on the ground
        if (r.GetComponentInParent<Visitor>() != null || r.GetComponentInParent<Possession>() != null) return false; // people
        if (r.GetComponentInParent<FxBit>() != null || r.GetComponentInParent<Item>() != null) return false; // effects, dropped things
        // Effects, glows, markers and bubbles are drawn unlit (ParkArt.Material); props stand in the park's light.
        if (r.sharedMaterial == ParkArt.Material) return false;
        return true;
    }

    public static float Flat(Vector3 d) => new Vector2(d.x, d.z).magnitude;

    // ---------------------------------------------------------------- squashed flat

    // Flattened by something huge (the runaway Ferris wheel), like the people it rolls over (Visitor.Squash): slammed
    // down to a squat FlatHeight of its height and FlatWidth as wide in a blink, then boinging back up to it like
    // jelly, its feet staying put. A puff of dust as it goes.
    public static void Squash(Prop prop)
    {
        var r = prop.Renderer(" (squashed)");
        if (r == null || r.GetComponent<Squashed>() != null) return;
        float tall = r.bounds.size.y, wide = r.bounds.size.x;
        r.gameObject.AddComponent<Squashed>();
        Fx.Burst("Fx/puff", prop.at + Vector3.up * 0.3f, 16, new Color(0.7f, 0.65f, 0.6f), 4f, 0.8f, 0.9f);
        DizzyStars.Over(null, prop.at + Vector3.up * (tall * 0.32f + 0.4f), Mathf.Min(wide, 3f) * 0.7f); // seeing stars
        // The crunch: once a frame, however many go flat together (the wheel landing on a row of them).
        if (crunchedFrame != Time.frameCount)
        {
            crunchedFrame = Time.frameCount;
            AudioManager.TryPlay(e => e.propCrunch, prop.at);
            ClosedCaptions.Show(Captions.Flattened);
        }
    }

    static int crunchedFrame = -1;

    class Squashed : MonoBehaviour
    {
        // As the people (CharacterSprite.Pancake), a touch less wide: buildings stand close together.
        const float SlamTime = 0.15f, WobbleTime = 0.9f, FlatWidth = 1.3f, FlatHeight = 0.32f, Boing = 0.4f;

        Vector3 scale, position;
        float since, feet; // feet: how far its feet are below its position, in its own (unscaled) units

        void Start()
        {
            scale = transform.localScale;
            position = transform.localPosition;
            since = Time.time;
            var sprite = GetComponent<SpriteRenderer>().sprite;
            feet = sprite.pivot.y / sprite.pixelsPerUnit; // the picture's bottom, below where it stands
        }

        void Update()
        {
            float t = Time.time - since, sx, sy;
            if (t < SlamTime)
            {
                float k = t / SlamTime;
                sy = Mathf.Lerp(1f, FlatHeight * 0.6f, k); // slammed a little past it...
                sx = Mathf.Lerp(1f, FlatWidth, k);
            }
            else
            {
                float w = Mathf.Clamp01((t - SlamTime) / WobbleTime);
                float jelly = Mathf.Sin(w * Mathf.PI * 4f) * (1f - w) * Boing; // ...and boinging back up to it, cartoon-big
                sx = FlatWidth * (1f - jelly * 0.5f);
                sy = FlatHeight * (1f + jelly);
                if (w >= 1f) enabled = false;
            }
            transform.localScale = new Vector3(scale.x * sx, scale.y * sy, scale.z);
            // Its feet stay on the ground (a picture whose pivot is above its bottom would otherwise float up off it).
            transform.localPosition = position - transform.localRotation * Vector3.up * (feet * scale.y * (1f - sy));
        }
    }
}
