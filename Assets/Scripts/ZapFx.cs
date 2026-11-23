using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

// Live electrics on an electrical box (the Ferris wheel's and the locker room's), always: the lightning bolt over the
// box (Resources/Park/Fx/zap, the artist's) crackles where the box's picture used to have it drawn (the palette's
// electrical_box tile, Arts/ParkPropsNew, has had that drawn one taken out). The box is drawn by that tile on the
// Objects tilemap, so the bolt finds the tile next to it; a box with only its own picture gets it over its lid.
public class ZapFx : MonoBehaviour
{
    // Where the drawn bolt was in the tile's picture, from its feet (the sprite's pivot), and how tall, in the
    // picture's own units (its pixels / pixels per unit): measured off electrical_box.PNG.
    static readonly Vector2 BoltInTile = new(70.5f / 450f, 763.5f / 450f);
    const float BoltHeightInTile = 295f / 450f;
    const float TilePixels = 450f; // the tile picture's pixels per unit: how it's recognised on a sprite

    IEnumerator Start()
    {
        yield return null; // the box's own picture is made in its own Start
        if (!FromTile(out Vector3 at, out float height))
        {
            var box = GetComponentInChildren<SpriteRenderer>();
            if (box == null) yield break;
            if (box.sprite != null && Mathf.Approximately(box.sprite.pixelsPerUnit, TilePixels))
            {
                // The tile's own picture on a sprite (the Ferris wheel's box): the bolt where it was drawn, as on the tiles.
                at = box.transform.TransformPoint(new Vector3(BoltInTile.x, BoltInTile.y, 0f));
                height = BoltHeightInTile * box.transform.lossyScale.y;
            }
            else
            {
                Bounds b = box.bounds;
                at = new Vector3(b.center.x, b.max.y + 0.15f, b.center.z); // low over the lid
                height = 0.6f;
            }
        }
        at.z -= 0.1f; // just in front of the box
        ElectricBolt.Make("Box Bolt", null, at, height, 0f).Blink = false; // always lit, just shaking
    }

    // The electrical_box tile standing where this box is, and where its bolt goes.
    bool FromTile(out Vector3 at, out float height)
    {
        at = default;
        height = 0f;
        float best = 1.5f;
        foreach (var map in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                var s = map.GetSprite(cell);
                if (s == null || !s.name.StartsWith("electrical_box")) continue;
                Vector3 c = map.GetCellCenterWorld(cell);
                float d = new Vector2(c.x - transform.position.x, c.z - transform.position.z).magnitude;
                if (d > best) continue;
                best = d;
                // As the tilemap draws it (like ToiletPrank's copy of the toilet block).
                Matrix4x4 m = map.transform.localToWorldMatrix
                              * Matrix4x4.Translate(map.CellToLocalInterpolated(cell + map.tileAnchor))
                              * map.orientationMatrix * map.GetTransformMatrix(cell);
                at = m.MultiplyPoint3x4(new Vector3(BoltInTile.x, BoltInTile.y, 0f));
                height = BoltHeightInTile * m.lossyScale.y;
            }
        return height > 0f;
    }
}

// A crackling lightning bolt (Resources/Park/Fx/zap): twitching a little every frame, and every so often surging (a
// jolt bigger and a few sparks). Blink: also flickers off for a blink now and then (the boxes' bolts don't, they only
// shake). Anchor moves it; Intensity > 1 crackles harder.
public class ElectricBolt : MonoBehaviour
{
    public Vector3 Anchor;
    public float Intensity = 1f;
    public float Tilt;
    public bool Blink = true;

    static readonly Color Spark = new(1f, 0.95f, 0.45f);
    SpriteRenderer r;
    Vector3 size;
    float twitch; // how far it may shift off its spot: a few percent of its height
    float stateUntil, nextSurge, surge;
    bool on = true;

    public static ElectricBolt Make(string name, Transform parent, Vector3 at, float height, float tilt)
    {
        var sr = ParkArt.Make(name, parent, "Fx/zap", height, Color.white, ParkArt.OverheadOrder);
        sr.transform.position = at;
        var bolt = sr.gameObject.AddComponent<ElectricBolt>();
        bolt.Anchor = at;
        bolt.Tilt = tilt;
        return bolt;
    }

    void Start()
    {
        r = GetComponent<SpriteRenderer>();
        size = transform.localScale;
        twitch = r.bounds.size.y * 0.04f;
        nextSurge = Time.time + Random.Range(0.6f, 1.8f);
    }

    void Update()
    {
        // On for a while, off for a blink (more often the harder it crackles).
        if (!Blink) on = true;
        else if (Time.time >= stateUntil)
        {
            on = !on || Random.value < 0.15f;
            stateUntil = Time.time + (on ? Random.Range(0.07f, 0.35f) / Intensity : Random.Range(0.03f, 0.1f));
        }
        if (Time.time >= nextSurge)
        {
            nextSurge = Time.time + Random.Range(0.6f, 1.8f) / Intensity;
            surge = 1f;
            on = true;
            Fx.Burst("Fx/sparkle", Anchor, Mathf.RoundToInt(Random.Range(2, 4) * Intensity), Spark, 1.4f, 0.12f, 0.25f);
        }
        surge = Mathf.MoveTowards(surge, 0f, Time.deltaTime * 6f);

        r.enabled = on;
        if (!on) return;
        // The twitch: a hair off its spot, a few degrees, a touch bigger or smaller every frame; bigger in a surge.
        transform.position = Anchor + new Vector3(Random.Range(-twitch, twitch), Random.Range(-twitch, twitch), 0f);
        transform.localRotation = Quaternion.Euler(0f, 0f, Tilt + Random.Range(-6f, 6f));
        transform.localScale = size * (1f + Random.Range(-0.05f, 0.05f) + surge * 0.25f);
    }
}
