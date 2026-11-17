using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

// The tutorial's prank, at the park's toilet block (the "bathroom" tile): an Out of Order sign in front of the door,
// and a water valve on a pipe beside it. In a body (E): take the sign down (ToiletSign), crank the valve all the way
// up (ToiletValve), and the next visitor who goes in comes back out in a geyser. Made by Tutorial, on a new game only.
// ponytail: placeholder blocks until there's art at Resources/Park/Props/toilet_sign.png and toilet_valve.png.
public class ToiletPrank : MonoBehaviour
{
    public const int Turns = 3; // cranks to max the pressure

    static readonly Color Water = new(0.45f, 0.75f, 1f);
    static readonly Color SignRed = new(0.85f, 0.28f, 0.24f);
    static readonly Color PipeGrey = new(0.55f, 0.6f, 0.68f);

    public Transform Sign { get; private set; }
    public Transform Valve { get; private set; }
    public Vector3 Door { get; private set; }          // where a visitor stands to go in
    public Bounds Block { get; private set; }          // the toilet block's picture, for the tutorial to light up
    public Transform Middle { get; private set; }      // its middle, for the camera
    public Transform Picture { get; private set; }     // a copy of its tile that never draws, for the tutorial's light
    public bool SignDown { get; private set; }
    public int Pressure { get; private set; }
    public bool Maxed => Pressure >= Turns;

    Transform wheel;
    float wheelAngle, wheelShown;

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

        Sign = Part<ToiletSign>("Out Of Order Sign", Door + new Vector3(0f, 0f, 0.1f), "Props/toilet_sign", 1.1f, SignRed).transform;
        if (ParkArt.Get("Props/toilet_sign") == null) Label(Sign, "OUT OF\nORDER", 1.1f);

        Valve = Part<ToiletValve>("Water Valve", feet + new Vector3(size.x * 0.5f + 0.45f, 0f, -0.5f), "Props/toilet_valve", 1f, PipeGrey).transform;
        if (ParkArt.Get("Props/toilet_valve") == null)
        {
            // A red handwheel on top of the pipe, with a spoke so you can see it turn.
            wheel = new GameObject("Wheel").transform;
            wheel.SetParent(Valve, false);
            wheel.localPosition = new Vector3(0f, 1.05f, -0.06f);
            ParkArt.Make("Rim", wheel, "Markers/ring", 0.5f, SignRed, 1);
            var spoke = ParkArt.Make("Spoke", wheel, "Markers/white", 0.42f, SignRed, 1);
            Vector3 s = spoke.sprite.bounds.size;
            spoke.transform.localScale = new Vector3(0.07f / s.x, 0.42f / s.y, 1f);
        }
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
        Picture = r.transform;
    }

    // Something you use with E, standing on the floor at `at`.
    T Part<T>(string name, Vector3 at, string sprite, float height, Color standIn) where T : MonoBehaviour
    {
        var go = new GameObject(name);
        go.SetActive(false); // so Interactable's Awake finds the rules component
        go.transform.SetParent(transform, true);
        go.transform.position = at;
        ParkArt.Solid(go.transform, sprite, height, standIn);
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

    void Update()
    {
        if (wheel == null) return;
        wheelShown = Mathf.MoveTowards(wheelShown, wheelAngle, 400f * Time.deltaTime);
        wheel.localRotation = Quaternion.Euler(0f, 0f, -wheelShown);
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
        wheelAngle += 120f;
        Sfx.Play(Sfx.HitWood, 0.6f, 1f + 0.15f * Pressure);
        Fx.Burst("Fx/sparkle", Valve.position + Vector3.up * 1.1f, 3 + Pressure * 2, Water, 1.5f + Pressure * 0.5f, 0.2f, 0.4f);
        Fx.Shake(0.04f * Pressure);
        if (!Maxed) return;
        Fx.Burst("Fx/puff", Valve.position + Vector3.up * 1.1f, 10, Water, 3f, 0.4f, 0.6f);
        Mischief.Complete(Mischief.Task.TutorialValve);
    }

    // The payoff: water bursts out of the toilet, and whoever was in there runs out soaked.
    public IEnumerator Geyser(Visitor victim)
    {
        Sfx.Play(Sfx.Crash, 1f, 1.4f);
        Fx.Shake(0.35f, 0.5f);
        for (int i = 0; i < 6; i++)
        {
            Fx.Burst("Fx/puff", Block.center + Vector3.up * (i * 0.35f), 14, Water, 4.5f, 0.55f, 0.9f);
            Fx.Burst("Fx/sparkle", Door + Vector3.up * 0.4f, 10, Water, 5f, 0.3f, 0.6f);
            if (i == 1 && victim != null)
            {
                victim.Look.SetVisible(true);
                victim.Free();
                victim.Panic(Block.center, 5f);
                GameHud.Shout(victim.transform, GameText.TutorialSoaked, 2.5f);
            }
            yield return new WaitForSeconds(0.2f);
        }
        ParkChaos.Add(Door, 3.5f, 3f, ParkChaos.Scary);
        Mischief.Complete(Mischief.Task.TutorialToilet);
    }
}
