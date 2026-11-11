using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

// Night at the carnival, laid over the day scene at runtime (no new art, no scene changes): the sun becomes a dim
// blue moon, the ambient light drops to a dusky purple, the sky goes dark, a night grade goes over the
// post-processing, and the park's own lights come on - warm pools under every lamp post, the stalls lit, chasing
// bulbs round the Ferris wheel rim and a glow in every cabin. The wheel's bulbs race in overdrive and the whole ride
// goes dark when it breaks. A soft spirit glow follows whoever you control so you never lose yourself in the dark.
// Everything it lights is a lit sprite (SpriteLit3D), so people walking under a lamp catch its light.
// Untick the component in Play mode to compare with day: everything it changed goes back. Values are read every
// frame, so they can be tuned live. GameHud adds it at runtime; put it on a scene object instead to keep tuned values.
public class NightLighting : MonoBehaviour
{
    [Header("Sky")]
    public Color moonColor = new(0.62f, 0.66f, 1f);
    public float moonIntensity = 0.9f;
    public Color ambient = new(0.3f, 0.27f, 0.48f);
    public Color sky = new(0.04f, 0.03f, 0.09f);   // camera background where the park ends

    [Header("Fog")]
    // Fades the park into the night sky with distance, so far things lose contrast and the near ones pop.
    public bool fog = true;
    public float fogStart = 14f; // metres from the camera (it sits ~12 from the player)
    public float fogEnd = 45f;

    [Header("Grade")]
    public float exposure = -0.2f;
    [Range(-100, 100)] public float contrast = 12f;
    [Range(-100, 100)] public float saturation = -5f;
    public Color shadowTint = new(1f, 0.92f, 1.12f); // pushes the darkest tones purple
    public float bloom = 0.8f;
    [Range(0, 1)] public float vignette = 0.35f;

    [Header("Park lights")]
    public string lampSprite = "lamp"; // tiles drawn with this sprite get a light in their lantern
    public float lanternHeight = 0.87f; // where the lantern is, as a fraction of the lamp's height
    public Color lampColor = new(1f, 0.78f, 0.48f);
    public float lampIntensity = 2f;
    public float lampRange = 4f;
    public float lampCoreSize = 0.55f;   // the hot spot inside the glass
    [Range(0, 1)] public float lampCoreAlpha = 0.4f;
    public float lampHaloSize = 3.5f;    // the faint haze around it
    [Range(0, 1)] public float lampHaloAlpha = 0.07f;
    public float poolIntensity = 1.2f;   // the wide, soft pool the lamp throws on the ground around its post
    public float poolRange = 8f;
    public float poolHeight = 1.2f;      // how far above the ground it sits: higher spreads it wider and fainter
    [Range(0, 1)] public float lampFlicker = 0.08f; // how far a flame dips, as a fraction of full brightness
    public float lampFlickerSpeed = 2f;
    public Color stallColor = new(1f, 0.72f, 0.45f);
    public float stallIntensity = 3f;
    public float stallRange = 6f;

    [Header("Ferris wheel")]
    public int rimBulbs = 36;
    public Color[] bulbColors = { new(1f, 0.85f, 0.4f), new(1f, 0.45f, 0.75f), new(0.45f, 0.85f, 1f) };
    public float chaseSpeed = 4f;     // bulb steps per second (three times that in overdrive)
    public float rideIntensity = 2.5f; // the coloured wash on the platform

    [Header("Player")]
    public Color playerColor = new(0.85f, 0.7f, 1f);
    public float playerIntensity = 1.5f;
    public float playerRange = 4f;

    class Lamp
    {
        public Vector3 foot;
        public Light light, pool;
        public SpriteRenderer core, halo;
    }

    class Ride
    {
        public FerrisWheel wheel;
        public readonly List<SpriteRenderer> bulbs = new(), glows = new();
        public readonly List<Light> wash = new();
    }

    static Sprite glowSprite;
    static Material glowMaterial;

    bool built;
    Transform root;
    Light sun, player;
    readonly List<Lamp> lamps = new();
    readonly List<Light> stalls = new();
    readonly List<Ride> rides = new();
    Volume volume;
    VolumeProfile profile;
    ColorAdjustments grade;
    ShadowsMidtonesHighlights tones;
    Bloom glow;
    Vignette edges;
    Camera cam;

    // The day it replaces, put back when switched off.
    Color daySunColor;
    float daySunIntensity;
    AmbientMode dayAmbientMode;
    Color dayAmbient;
    SphericalHarmonicsL2 dayProbe;
    CameraClearFlags dayClear;
    Color dayBackground;
    bool dayFog;
    FogMode dayFogMode;
    Color dayFogColor;
    float dayFogStart, dayFogEnd;

    // Built on the first frame, once the park (wheel cabins, stalls) has set itself up.
    void LateUpdate()
    {
        if (!built) Build();
        Apply();
    }

    void OnEnable()
    {
        if (built) root.gameObject.SetActive(true);
    }

    void OnDisable()
    {
        if (!built) return;
        if (sun != null)
        {
            sun.color = daySunColor;
            sun.intensity = daySunIntensity;
        }
        RenderSettings.ambientMode = dayAmbientMode;
        RenderSettings.ambientLight = dayAmbient;
        RenderSettings.ambientProbe = dayProbe;
        RenderSettings.fog = dayFog;
        RenderSettings.fogMode = dayFogMode;
        RenderSettings.fogColor = dayFogColor;
        RenderSettings.fogStartDistance = dayFogStart;
        RenderSettings.fogEndDistance = dayFogEnd;
        if (cam != null)
        {
            cam.clearFlags = dayClear;
            cam.backgroundColor = dayBackground;
        }
        if (root != null) root.gameObject.SetActive(false);
        foreach (var r in rides)
        {
            foreach (var b in r.bulbs) if (b != null) b.enabled = false;
            foreach (var g in r.glows) if (g != null) g.enabled = false;
        }
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
        if (profile != null) Destroy(profile);
        foreach (var r in rides)
        {
            foreach (var b in r.bulbs) if (b != null) Destroy(b.gameObject);
            foreach (var g in r.glows) if (g != null) Destroy(g.gameObject);
        }
    }

    void Build()
    {
        built = true;
        root = new GameObject("Night Lights").transform;

        sun = RenderSettings.sun;
        if (sun == null)
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
        if (sun != null)
        {
            daySunColor = sun.color;
            daySunIntensity = sun.intensity;
        }
        dayAmbientMode = RenderSettings.ambientMode;
        dayAmbient = RenderSettings.ambientLight;
        dayProbe = RenderSettings.ambientProbe;
        dayFog = RenderSettings.fog;
        dayFogMode = RenderSettings.fogMode;
        dayFogColor = RenderSettings.fogColor;
        dayFogStart = RenderSettings.fogStartDistance;
        dayFogEnd = RenderSettings.fogEndDistance;
        cam = Camera.main;
        if (cam != null)
        {
            dayClear = cam.clearFlags;
            dayBackground = cam.backgroundColor;
        }

        BuildGrade();

        foreach (var map in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                var s = map.GetSprite(cell);
                if (s == null || s.name != lampSprite) continue;
                // Props stand upright on their cell with their feet at its centre.
                Vector3 foot = map.GetCellCenterWorld(cell);
                Vector3 lantern = ParkArt.Above(foot, s.bounds.max.y * map.transform.lossyScale.y * lanternHeight);
                // Well in front of the lantern, so it lights the lamp and the people near it without burning the glass white.
                lamps.Add(new Lamp
                {
                    foot = foot,
                    light = PointLight("Lamp Light", root, lantern + Vector3.back * 1.2f),
                    pool = PointLight("Lamp Pool", root, foot),
                    core = Glow("Lamp Core", root, lantern + Vector3.back * 0.05f, lampCoreSize, lampColor, lampCoreAlpha),
                    halo = Glow("Lamp Halo", root, lantern + Vector3.back * 0.04f, lampHaloSize, lampColor, lampHaloAlpha),
                });
            }

        foreach (var s in Stall.All)
            stalls.Add(PointLight("Stall Light", root, s.transform.position + new Vector3(0f, 2.2f, -1.5f)));

        foreach (var w in FerrisWheel.All) rides.Add(LightUp(w));

        player = PointLight("Spirit Glow", root, Vector3.zero);
    }

    // Bulbs chase round the rim (in the wheel's own plane, so they spin with it), every cabin glows, and two
    // coloured lights wash over the platform.
    Ride LightUp(FerrisWheel w)
    {
        var ride = new Ride { wheel = w };
        float r = w.wheelRadius * 0.94f; // on the outer truss, just inside the cabin hooks
        for (int i = 0; i < rimBulbs; i++)
        {
            float a = i * Mathf.PI * 2f / rimBulbs;
            ride.bulbs.Add(Glow("Rim Bulb", w.wheel, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, -0.02f), 0.4f, Bulb(i), 1f));
        }
        ride.glows.Add(Glow("Hub Glow", w.wheel, new Vector3(0f, 0f, -0.03f), 1.4f, bulbColors[0], 0.7f));
        for (int i = 0; i < w.Cabins.Count; i++)
            ride.glows.Add(Glow("Cabin Glow", w.Cabins[i], new Vector3(0f, -0.52f, -0.03f), 0.9f, Bulb(i), 0.6f)); // in the window

        Vector3 foot = new(w.wheel.position.x, 0f, w.wheel.position.z);
        float scale = w.transform.lossyScale.x;
        for (int side = -1; side <= 1; side += 2)
        {
            var l = PointLight("Ride Light", root, foot + new Vector3(side * 2.6f * scale, 1.5f, -1.8f));
            l.color = bulbColors[side < 0 ? 1 : 2];
            ride.wash.Add(l);
        }
        return ride;
    }

    Color Bulb(int i) => bulbColors.Length > 0 ? bulbColors[i % bulbColors.Length] : Color.white;

    void BuildGrade()
    {
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        // Only what's set below is overridden; everything else comes from the scene's day volume.
        grade = profile.Add<ColorAdjustments>();
        tones = profile.Add<ShadowsMidtonesHighlights>();
        glow = profile.Add<Bloom>();
        edges = profile.Add<Vignette>();
        var go = new GameObject("Night Grade");
        go.transform.SetParent(root, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10; // above the scene's volume
        volume.sharedProfile = profile;
    }

    // Every frame, so the values above can be tuned while playing.
    void Apply()
    {
        if (sun != null)
        {
            sun.color = moonColor;
            sun.intensity = moonIntensity;
        }
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambient;
        var probe = new SphericalHarmonicsL2();
        probe.AddAmbientLight(ambient.linear);
        RenderSettings.ambientProbe = probe;
        RenderSettings.fog = fog;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = sky; // same as the background, so the far edge melts into the sky
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = sky;
        }

        grade.postExposure.Override(exposure);
        grade.contrast.Override(contrast);
        grade.saturation.Override(saturation);
        tones.shadows.Override(new Vector4(shadowTint.r, shadowTint.g, shadowTint.b, 0f));
        glow.threshold.Override(0.9f);
        glow.intensity.Override(bloom);
        glow.scatter.Override(0.7f);
        edges.intensity.Override(vignette);
        edges.smoothness.Override(0.45f);
        edges.color.Override(new Color(0.03f, 0.01f, 0.08f));

        for (int i = 0; i < lamps.Count; i++)
        {
            // Each lamp on its own slow noise, so they waver like flames rather than pulse together.
            float f = 1f - lampFlicker * Mathf.PerlinNoise(Time.time * lampFlickerSpeed, i * 7.31f);
            var l = lamps[i];
            Set(l.light, lampColor, lampIntensity * f, lampRange);
            Set(l.pool, lampColor, poolIntensity * f, poolRange);
            l.pool.transform.position = l.foot + new Vector3(0f, poolHeight, -0.3f);
            Tint(l.core, lampColor, lampCoreAlpha * f, lampCoreSize);
            Tint(l.halo, lampColor, lampHaloAlpha * f, lampHaloSize);
        }
        foreach (var l in stalls) Set(l, stallColor, stallIntensity, stallRange);

        foreach (var ride in rides)
        {
            bool on = ride.wheel != null && ride.wheel.Powered;
            int step = (int)(Time.time * chaseSpeed);
            for (int i = 0; i < ride.bulbs.Count; i++)
            {
                var b = ride.bulbs[i];
                if (b == null) continue;
                b.enabled = on;
                var c = b.color;
                c.a = (i + step) % 3 == 0 ? 1f : 0.35f; // every third bulb lit, marching round
                b.color = c;
            }
            foreach (var g in ride.glows) if (g != null) g.enabled = on;
            foreach (var l in ride.wash)
            {
                l.enabled = on;
                l.intensity = rideIntensity;
            }
        }

        var focus = Possession.Instance != null ? Possession.Instance.Focus : null;
        player.enabled = focus != null;
        if (focus != null) player.transform.position = focus.position + new Vector3(0f, 1.8f, -1.3f);
        Set(player, playerColor, playerIntensity, playerRange);
    }

    static void Set(Light l, Color c, float intensity, float range)
    {
        l.color = c;
        l.intensity = intensity;
        l.range = range;
    }

    static void Tint(SpriteRenderer r, Color c, float alpha, float size)
    {
        c.a = alpha;
        r.color = c;
        r.transform.localScale = Vector3.one * size;
    }

    static Light PointLight(string name, Transform parent, Vector3 position)
    {
        var l = new GameObject(name).AddComponent<Light>();
        l.transform.SetParent(parent, false);
        l.transform.position = position;
        l.type = LightType.Point;
        l.shadows = LightShadows.None; // dozens of these: shadows stay with the moon
        return l;
    }

    // A soft round glow, bright enough (HDR) for the bloom to pick up.
    static SpriteRenderer Glow(string name, Transform parent, Vector3 localPosition, float size, Color color, float alpha)
    {
        var r = new GameObject(name).AddComponent<SpriteRenderer>();
        r.transform.SetParent(parent, false);
        r.transform.localPosition = localPosition;
        r.transform.rotation = ParkArt.Card;
        r.transform.localScale = Vector3.one * size;
        r.sprite = GlowSprite();
        r.sharedMaterial = GlowMaterial();
        r.sortingOrder = ParkArt.OverheadOrder - 1;
        color.a = alpha;
        r.color = color;
        return r;
    }

    static Material GlowMaterial()
    {
        if (glowMaterial != null) return glowMaterial;
        glowMaterial = new Material(ParkArt.Material) { name = "Night Glow" };
        glowMaterial.SetColor("_Color", new Color(2.5f, 2.5f, 2.5f, 1f)); // the sprite shader multiplies this in
        return glowMaterial;
    }

    // One unit across, white, fading out from a bright centre.
    static Sprite GlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Night Glow", wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                float a = t * t * (3f - 2f * t);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }
}
