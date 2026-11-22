using System;
using UnityEngine;

// The hand-drawn "boil" on the park's sprites (the sprite doodle effect, Palletes/Shaders/Doodle.hlsl): every
// outline is redrawn a few times a second, each time a little off. One place to set it for each group:
//   Floor:          the ground tilemap (its own material, SpriteLit2.5 Ground)
//   Props & decor:  the Objects, See Through and Decor tilemaps, the Ferris wheel, stalls, signs... (SpriteLit2.5,
//                   SpriteLit2.5 SeeThrough, SpriteLit2.5Sway) and props made in code (ParkArt.LitMaterial)
//   People:         visitors and staff (ParkArt.CharacterMaterial)
//   Main character: the spirit (ParkArt.SpiritMaterial)
// Strength is how far an outline may wander, in world units across the screen (0 = still); Per Second is how many
// times a second it's redrawn. Changes show straight away, in Play mode and in the Scene view. Lives on the scene's
// "Doodle" object.
[ExecuteAlways]
public class DoodleSettings : MonoBehaviour
{
    [Serializable]
    public struct Doodle
    {
        [Range(0f, 0.1f)] public float strength;
        [Range(1f, 12f)] public float perSecond;

        public Doodle(float strength, float perSecond)
        {
            this.strength = strength;
            this.perSecond = perSecond;
        }
    }

    public Doodle floor = new(0.03f, 6f);
    public Doodle propsAndDecor = new(0.012f, 4f);
    public Doodle people = new(0.015f, 4f);
    public Doodle mainCharacter = new(0.015f, 4f);

    [Header("Materials (set up already)")]
    public Material[] floorMaterials = new Material[0];
    public Material[] propMaterials = new Material[0];

    static readonly int StrengthId = Shader.PropertyToID("_DoodleStrength");
    static readonly int PerSecondId = Shader.PropertyToID("_DoodlePerSecond");

    Doodle shownFloor, shownProps, shownPeople, shownSpirit;
    bool applied;

    void OnEnable() => applied = false;
    void OnValidate() => applied = false;

    void Update()
    {
        // The materials people and props made in code use only exist while playing.
        bool playing = Application.isPlaying;
        if (applied && shownFloor.Equals(floor) && shownProps.Equals(propsAndDecor) && shownPeople.Equals(people) && shownSpirit.Equals(mainCharacter)) return;
        foreach (var m in floorMaterials) Set(m, floor);
        foreach (var m in propMaterials) Set(m, propsAndDecor);
        if (playing)
        {
            Set(ParkArt.LitMaterial, propsAndDecor);
            Set(ParkArt.CharacterMaterial, people);
            Set(ParkArt.SpiritMaterial, mainCharacter);
        }
        shownFloor = floor;
        shownProps = propsAndDecor;
        shownPeople = people;
        shownSpirit = mainCharacter;
        applied = true;
    }

    static void Set(Material m, Doodle d)
    {
        if (m == null) return;
        m.SetFloat(StrengthId, d.strength);
        m.SetFloat(PerSecondId, d.perSecond);
    }
}
