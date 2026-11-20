using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

// The tutorial's prank, at the park's toilet block (the "bathroom" tile): an Out of Order sign in front of the door,
// and a water valve on a pipe beside it. In a body (K): take the sign down (ToiletSign), crank the valve all the way
// up (ToiletValve), and the next visitor who goes in comes back out in a geyser, sending everyone nearby running.
// The block's left broken (Props/toilet_broken), and a Reset Agent comes and puts up a caution sign
// (Props/toilet_sign_caution), for good. Made by Tutorial, on a new game only.
// The valve is drawn into the block's own tile (its left side), so the valve part is just an invisible spot to press K.
public class ToiletPrank : MonoBehaviour
{
    public const int Turns = 3; // cranks to max the pressure

    static readonly Color Water = new(0.45f, 0.75f, 1f);
    // The geyser: browns, from light splash to dark lumps.
    // Light enough to read at night (the puff picture is soft and see-through, so it comes out darker than this).
    static readonly Color[] Poop = { new(0.78f, 0.52f, 0.26f), new(0.62f, 0.4f, 0.18f), new(0.88f, 0.66f, 0.38f), new(0.5f, 0.3f, 0.12f) };
    static readonly Color SignRed = new(0.85f, 0.28f, 0.24f);

    public Transform Sign { get; private set; }
    public Transform Valve { get; private set; }
    public Vector3 Door { get; private set; }          // where a visitor stands to go in
    public Bounds Block { get; private set; }          // the toilet block's picture, for the tutorial to light up
    public Transform Middle { get; private set; }      // its middle, for the camera
    public Transform Picture { get; private set; }     // a copy of its tile that never draws, for the tutorial's light
    public bool SignDown { get; private set; }
    public bool Erupted { get; private set; }
    public float geyserHeight = 6f;                    // the drawn geyser (Fx/poop_geyser_1, _2), out of the roof          // the geyser went off (not just skipped past: gossip's about it)
    public float panicRadius = 8f;                     // everyone this close runs from the geyser
    public float panicTime = 6f;
    public int Pressure { get; private set; }
    public bool Maxed => Pressure >= Turns;

    Vector3 handle; // the drawn valve's handle, where the crank's splashes come out
    // The block's tile, put back after the copy has stood in for it while it rumbles.
    Tilemap tiles;
    Vector3Int cell;
    TileBase tile;
    Matrix4x4 tileMatrix;
    Vector3 pictureAt;

    // Finds the toilet block and sets the prank up at it; null if this park has none.
    public static ToiletPrank Create()
    {
        foreach (var map in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                var s = map.GetSprite(cell);
                if (s == null || !s.name.StartsWith("bathroom")) continue;
                var prank = new GameObject("Toilet Prank").AddComponent<ToiletPrank>();
                // Props stand upright on their cell with their feet at its centre.
                prank.Build(map.GetCellCenterWorld(cell), s.bounds.size * map.transform.lossyScale.x);
                prank.Copy(map, cell, s);
                return prank;
            }
        return null;
    }

    void Build(Vector3 feet, Vector2 size)
    {
        transform.position = feet;
        Block = new Bounds(feet + new Vector3(0f, size.y * 0.5f, 0f), new Vector3(size.x, size.y, 0.4f));
        Middle = new GameObject("Toilet Middle").transform;
        Middle.SetParent(transform, false);
        Middle.position = Block.center;
        // Just in front of the block's door (people bump into its front edge), and the sign right in their way.
        Door = feet + new Vector3(0f, 0f, -0.85f);

        Sign = Part<ToiletSign>("Out Of Order Sign", Door + new Vector3(0.35f, 1f, 0.8f), "Props/toilet_sign", 1.1f, SignRed).transform;
        if (ParkArt.Get("Props/toilet_sign") == null) Label(Sign, "OUT OF\nORDER", 1.1f);

        // The pipe and valve drawn at the tile's left, ~15% in from its edge, handle ~23% up (bathroom.PNG).
        Valve = Part<ToiletValve>("Water Valve", feet + new Vector3(-size.x * 0.35f, 0f, -0.5f), null, 0f, default).transform;
        handle = new Vector3(Valve.position.x, feet.y + size.y * 0.23f, feet.z - 0.1f);
    }

    // The toilet block is a tile, drawn with the rest of its tilemap: this copy sits exactly where the tile is drawn, so
    // the tutorial's light (which lights sprites in their own shape) has a shape to light. It never draws itself.
    void Copy(Tilemap map, Vector3Int cell, Sprite sprite)
    {
        Matrix4x4 m = map.transform.localToWorldMatrix
                      * Matrix4x4.Translate(map.CellToLocalInterpolated(cell + map.tileAnchor))
                      * map.orientationMatrix * map.GetTransformMatrix(cell);
        var r = new GameObject("Toilet Picture").AddComponent<SpriteRenderer>();
        r.transform.SetParent(transform, true);
        r.transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);
        r.transform.localScale = m.lossyScale;
        r.sprite = sprite;
        r.forceRenderingOff = true;
        // Drawn like the tile when it stands in for it (Rumble).
        var tr = map.GetComponent<TilemapRenderer>();
        if (tr != null)
        {
            r.sharedMaterial = tr.sharedMaterial;
            r.sortingLayerID = tr.sortingLayerID;
            r.sortingOrder = tr.sortingOrder;
            r.shadowCastingMode = tr.shadowCastingMode;
            r.receiveShadows = tr.receiveShadows;
        }
        Picture = r.transform;
        pictureAt = r.transform.localPosition;
        tiles = map;
        this.cell = cell;
        tile = map.GetTile(cell);
        tileMatrix = map.GetTransformMatrix(cell);
    }

    // The block shakes harder and harder for `seconds` (someone's in there): the tile steps aside and its copy, which
    // can move, shakes in its place; the camera rattles too.
    public IEnumerator Rumble(float seconds)
    {
        var picture = Picture.GetComponent<SpriteRenderer>();
        picture.forceRenderingOff = false;
        if (tiles != null) tiles.SetTile(cell, null);
        float next = 0f;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = t / seconds;
            float amount = Mathf.Lerp(0.02f, 0.09f, k * k);
            Picture.localPosition = pictureAt + new Vector3(Random.Range(-amount, amount), Random.Range(0f, amount), 0f);
            if (t >= next)
            {
                next = t + Mathf.Lerp(0.35f, 0.12f, k);
                Fx.Shake(Mathf.Lerp(0.05f, 0.18f, k));
                Sfx.Play(Sfx.HitSoft, Mathf.Lerp(0.25f, 0.6f, k), Random.Range(0.6f, 0.8f));
            }
            yield return null;
        }
    }

    // Back to the tile, standing still.
    void Settle()
    {
        Picture.localPosition = pictureAt;
        Picture.GetComponent<SpriteRenderer>().forceRenderingOff = true;
        if (tiles == null || tile == null || tiles.GetTile(cell) != null) return;
        tiles.SetTile(cell, tile);
        tiles.SetTransformMatrix(cell, tileMatrix);
    }

    void OnDestroy()
    {
        if (Picture != null && !broken) Settle(); // skipped mid-rumble: the toilet comes back
    }

    // Blown open: the copy stays in the tile's place, showing the broken block, for good.
    bool broken;

    void Break()
    {
        var art = ParkArt.Get("Props/toilet_broken");
        if (art == null || Picture == null) return;
        broken = true;
        var r = Picture.GetComponent<SpriteRenderer>();
        r.sprite = art; // cut and imported like the tile's own picture, so it lands right where the tile was
        r.forceRenderingOff = false;
        Picture.localPosition = pictureAt;
        if (tiles != null) tiles.SetTile(cell, null);
    }

    // Something you use with K, standing on the floor at `at`. No sprite: just the spot (it's drawn in the tile).
    T Part<T>(string name, Vector3 at, string sprite, float height, Color standIn) where T : MonoBehaviour
    {
        var go = new GameObject(name);
        go.SetActive(false); // so Interactable's Awake finds the rules component
        go.transform.SetParent(transform, true);
        go.transform.position = at;
        if (sprite != null) ParkArt.Solid(go.transform, sprite, height, standIn);
        var part = go.AddComponent<T>();
        go.AddComponent<Interactable>().range = 1.8f;
        go.SetActive(true);
        return part;
    }

    // ponytail: built-in TextMesh on the placeholder sign; the drawn sign will carry its own words.
    static void Label(Transform on, string text, float height)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(on, false);
        go.transform.localPosition = new Vector3(0f, height * 0.55f, -0.12f);
        var t = go.AddComponent<TextMesh>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = text;
        t.fontSize = 48;
        t.characterSize = 0.035f;
        t.fontStyle = FontStyle.Bold;
        t.anchor = TextAnchor.MiddleCenter;
        t.alignment = TextAlignment.Center;
        t.color = Color.white;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = t.font.material;
        r.sortingOrder = 2;
    }

    public void TakeDownSign()
    {
        if (SignDown) return;
        SignDown = true;
        Sfx.Play(Sfx.Grab, 0.7f);
        Fx.Burst("Fx/puff", Sign.position + Vector3.up * 0.6f, 8, Color.white, 2f, 0.35f, 0.5f);
        Sign.gameObject.SetActive(false); // its collider goes with it: the door's clear
        Mischief.Complete(Mischief.Task.TutorialSign);
    }

    public void Crank()
    {
        if (Maxed) return;
        Pressure++;
        Sfx.Play(Sfx.HitWood, 0.6f, 1f + 0.15f * Pressure);
        Fx.Burst("Fx/sparkle", handle, 3 + Pressure * 2, Water, 1.5f + Pressure * 0.5f, 0.2f, 0.4f);
        Fx.Shake(0.04f * Pressure);
        if (!Maxed) return;
        Fx.Burst("Fx/puff", handle, 10, Water, 3f, 0.4f, 0.6f);
        Mischief.Complete(Mischief.Task.TutorialValve);
    }

    // The payoff: a geyser of poop bursts out of the toilet, and whoever was in there runs out covered in it. The
    // spray is soft and a little blurry (big, see-through puffs over smaller ones), so it reads without being gross.
    public IEnumerator Geyser(Visitor victim)
    {
        Settle();
        Sfx.Play(Sfx.Crash, 1f, 0.9f);
        Fx.Shake(0.45f, 0.6f);
        // Out of the roof and up, in front of the block's picture (level with it, the puffs would hide behind it).
        Vector3 roof = new(Block.center.x, Block.max.y, Door.z);
        var frames = new[] { ParkArt.Get("Fx/poop_geyser_1"), ParkArt.Get("Fx/poop_geyser_2") };
        SpriteRenderer spout = null;
        if (frames[0] != null)
        {
            spout = ParkArt.Make("Geyser", null, "Fx/poop_geyser_1", geyserHeight, Color.white, ParkArt.OverheadOrder - 1);
            spout.transform.position = roof - Vector3.up * 0.6f; // its foot down in the roof
        }
        Break();
        for (int i = 0; i < 10; i++)
        {
            if (spout != null)
            {
                spout.sprite = frames[i % 2] ?? frames[0];
                ParkArt.Fit(spout, geyserHeight * Mathf.Min(1f, 0.4f + i * 0.2f)); // shoots up, then gushes
            }
            Vector3 up = roof + Vector3.up * (i % 7 * 0.55f);
            var c = Poop[i % Poop.Length];
            Fx.Burst("Fx/puff", up, 10, new Color(c.r, c.g, c.b, 0.45f), 3f, 2.6f, 1.8f);  // the blur
            Fx.Burst("Fx/puff", up, 16, c, 4.5f, 1.2f, 1.5f);
            Fx.Burst("Fx/puff", Door + Vector3.up * 0.4f, 8, Poop[(i + 1) % Poop.Length], 4f, 0.7f, 1.2f); // splats
            if (i == 3)
            {
                Erupted = true;
                ParkChaos.Add(Door, panicRadius, panicTime); // everybody nearby runs
            }
            if (i == 3 && victim != null)
            {
                victim.Look.SetVisible(true);
                victim.Free();
                victim.Panic(Block.center, 5f);
                GameHud.Shout(victim.transform, GameText.TutorialSoaked, 2.5f);
            }
            yield return new WaitForSeconds(0.35f);
        }
        if (spout != null) Destroy(spout.gameObject);
        Mischief.Complete(Mischief.Task.TutorialToilet);

        // Clean-up comes to put the sign back.
        ResetAgent.Call(new StaffWorker.Job
        {
            spot = Door + new Vector3(0.8f, 0f, -0.6f),
            at = Sign.position,
            seconds = 2f,
            shout = GameText.SignBackShout,
            done = PutSignBack,
        });
    }

    // Out of order again, and nobody's taking it down this time.
    void PutSignBack()
    {
        var caution = ParkArt.Get("Props/toilet_sign_caution");
        var picture = Sign.GetComponentInChildren<SpriteRenderer>(true);
        if (caution != null && picture != null)
        {
            picture.sprite = caution;
            ParkArt.Fit(picture, 1.1f);
        }
        Sign.gameObject.SetActive(true);
        Sign.GetComponent<Interactable>().enabled = false;
        Sfx.Play(Sfx.HitWood, 0.6f, 1.1f);
        Fx.Burst("Fx/puff", Sign.position + Vector3.up * 0.6f, 8, Color.white, 2f, 0.35f, 0.5f);
    }
}
