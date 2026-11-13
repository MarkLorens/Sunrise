using System.Collections.Generic;
using UnityEngine;

// A flat Ferris wheel: the wheel sprite spins in its own plane and the cabins hang upright around it.
// Its power runs through the mechanic's electrical box next to it (ElectricalBox): cut it and the wheel winds down
// to a stop with whoever's riding stuck up in their cabins.
// When the scene starts it marks out its staff-only closed area (StaffArea) and puts the electrical box just outside
// it. Scale the whole object to resize the ride: radii, part offsets and the closed area are all in unscaled units
// and grow with it.
public class FerrisWheel : MonoBehaviour
{
    public static readonly List<FerrisWheel> All = new();

    public enum State { Running, Unpowered }

    public Transform wheel;
    public Sprite cabinSprite;
    public int cabinCount = 12;
    public float cabinRadius = 3.4f; // where cabins hang from, in wheel units
    public float wheelRadius = 3.5f; // outer rim, where the light bulbs go
    public float normalSpeed = 12f;  // degrees per second
    public float windDownTime = 5f;  // seconds to coast to a stop once the power's cut
    public Vector3 electricalBoxOffset = new(-3.3f, 0f, -0.6f);
    public Sprite electricalBoxArt; // drawn at its own size, pivot at its feet (the palette's props); empty = Props/electrical_box
    public Vector3 closedAreaOffset = new(0f, 0f, 0.3f);
    public Vector2 closedAreaSize = new(5.4f, 3.4f);
    public RideQueue queue;

    public State Current { get; private set; }
    public bool Powered => Current == State.Running;
    public StaffArea Area { get; private set; }
    public IReadOnlyList<Transform> Cabins => cabins;
    float Scale => transform.lossyScale.x;

    readonly List<Transform> cabins = new();
    float angle;
    float speed;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    // Before anyone's Start, so the spawner can keep visitors out of it.
    void Awake() => Area = ClosedArea();

    void Start()
    {
        speed = normalSpeed;
        Part<ElectricalBox>("Electrical Box", electricalBoxOffset, "Props/electrical_box", 1.3f, electricalBoxArt).wheel = this;
        var mat = wheel.GetComponent<SpriteRenderer>().sharedMaterial;
        for (int i = 0; i < cabinCount; i++)
        {
            var go = new GameObject($"Cabin {i + 1}");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = cabinSprite;
            sr.sharedMaterial = mat;
            ParkArt.CastShadows(sr);
            cabins.Add(go.transform);
        }
    }

    public void CutPower()
    {
        if (!Powered) return;
        Current = State.Unpowered;
        Sfx.Play(Sfx.Thud, 0.8f, 0.6f);
    }

    // Offsets are relative to the wheel's base.
    T Part<T>(string name, Vector3 offset, string sprite, float height, Sprite art = null) where T : Component
    {
        var go = new GameObject(name);
        go.SetActive(false); // so Interactable's Awake finds the rules component
        go.transform.position = transform.position + offset * Scale;
        var pic = Prop(go.transform, sprite, height, Vector3.zero);
        if (art != null)
        {
            pic.sprite = art;
            pic.transform.localScale = Vector3.one;
            pic.transform.localPosition = Vector3.zero;
        }
        var part = go.AddComponent<T>();
        go.AddComponent<Interactable>();
        go.SetActive(true);

        // Something people bump into: a box the size of its picture. Made after the scene loaded, so GroundWalker
        // is told about it.
        var box = pic.gameObject.AddComponent<BoxCollider>();
        Bounds b = pic.sprite.bounds;
        box.center = b.center;
        box.size = new Vector3(b.size.x, b.size.y, 0.2f);
        GroundWalker.AddObstacle(box);
        return part;
    }

    static SpriteRenderer Prop(Transform parent, string sprite, float height, Vector3 localFeet)
    {
        var r = ParkArt.Lit(ParkArt.Make("Sprite", parent, sprite, height, Color.white));
        r.transform.localPosition = localFeet + new Vector3(0f, height * 0.5f, 0f);
        return r;
    }

    // The staff-only ground at the wheel's foot: a painted line round it, barriers at the sides, a sign at the front.
    StaffArea ClosedArea()
    {
        var area = new GameObject("Closed Area").AddComponent<StaffArea>();
        area.transform.position = transform.position + closedAreaOffset * Scale;
        area.size = closedAreaSize * Scale;
        Vector2 h = area.size * 0.5f;

        var line = area.gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = ParkArt.Material;
        line.sortingOrder = ParkArt.DecalOrder;
        line.loop = true;
        line.widthMultiplier = 0.08f;
        line.startColor = line.endColor = new Color(1f, 0.82f, 0.15f);
        line.positionCount = 4;
        line.SetPositions(new[]
        {
            area.transform.position + new Vector3(-h.x, 0.03f, -h.y), area.transform.position + new Vector3(h.x, 0.03f, -h.y),
            area.transform.position + new Vector3(h.x, 0.03f, h.y), area.transform.position + new Vector3(-h.x, 0.03f, h.y),
        });

        Prop(area.transform, "Props/barrier", 0.6f, new Vector3(-h.x, 0f, 0f));
        Prop(area.transform, "Props/barrier", 0.6f, new Vector3(h.x, 0f, 0f));
        Prop(area.transform, "Props/staff_sign", 1.1f, new Vector3(h.x + 0.2f, 0f, -h.y - 0.1f));
        return area;
    }

    void Update()
    {
        float target = Powered ? normalSpeed : 0f;
        speed = Mathf.MoveTowards(speed, target, normalSpeed / windDownTime * Time.deltaTime);
        angle += speed * Time.deltaTime;
        wheel.localRotation = Quaternion.Euler(0, 0, angle);

        // Hang upright from evenly spaced points on the spinning rim.
        for (int i = 0; i < cabins.Count; i++)
        {
            float a = (angle + i * 360f / cabins.Count) * Mathf.Deg2Rad;
            cabins[i].position = wheel.position + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * cabinRadius * Scale + new Vector3(0, 0, -0.05f);
        }
    }
}
