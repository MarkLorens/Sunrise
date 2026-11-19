using System.Collections.Generic;
using UnityEngine;

// The park mascot, standing in front of the locker room's propane fan and not letting anyone touch it (TankFan). He
// can't be possessed: he's all costume. Blow the smelly gutter's stink at him (Gutter, its fan turned his way) and he
// gags and runs off for good.
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public class Mascot : MonoBehaviour
{
    public static readonly List<Mascot> All = new();

    public float guardRadius = 6f;  // how far from him a fan counts as his
    public float stinkTime = 2f;    // seconds of stink before he's had enough
    public float runDistance = 18f;

    public bool Guarding => !gone;

    GroundWalker walker;
    CharacterSprite look;
    Vector3 post, away;
    bool gone, leaving;
    float stink;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Awake()
    {
        walker = GetComponent<GroundWalker>();
        look = GetComponent<CharacterSprite>();
        if (!TryGetComponent(out Visitor body)) body = gameObject.AddComponent<Visitor>(); // loads the sprite
        body.brain = this;
        body.possessable = false;
        walker.speed = 2f;
        post = transform.position;
    }

    void Start() => look.SetTint(new Color(1f, 0.75f, 0.9f)); // ponytail: a tinted staff body until there's mascot art

    // A mascot guarding something near `p`, if any.
    public static Mascot GuardNear(Vector3 p)
    {
        foreach (var m in All)
        {
            Vector3 d = m.transform.position - p;
            if (m.Guarding && new Vector2(d.x, d.z).magnitude <= m.guardRadius) return m;
        }
        return null;
    }

    void Update()
    {
        if (leaving)
        {
            if (walker.WalkTo(away)) return;
            leaving = false;
            look.SetVisible(false); // gone home to wash his costume
            enabled = false;
            return;
        }
        if (gone) return;
        walker.WalkTo(post);

        // The stink reaches him: a smellOnly gutter that's open, its fan blowing his way.
        stink = Smelly() ? stink + Time.deltaTime : 0f;
        if (stink < stinkTime) return;
        Leave(false);
    }

    bool Smelly()
    {
        foreach (var g in FindObjectsByType<Gutter>())
            if (g.Stinking && g.fan != null && g.fan.Blows(transform.position, 1.5f, 12f))
                return true;
        return false;
    }

    void Leave(bool quiet)
    {
        gone = true;
        Mischief.Complete(Mischief.Task.SmellMascot);
        if (quiet)
        {
            look.SetVisible(false);
            enabled = false;
            return;
        }
        GameHud.Shout(transform, GameText.MascotStink, 2f);
        GameHud.Think(GameText.ThinkStinkGone);
        away = post;
        for (int i = 0; i < 12; i++)
        {
            Vector3 spot = post + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * runDistance;
            if (walker.IsWalkable(spot)) { away = spot; break; }
        }
        leaving = true;
        walker.speed = 4f;
        look.running = true;
    }

    // Loading a save (SaveGame).
    public void Restore()
    {
        if (Mischief.IsDone(Mischief.Task.SmellMascot)) Leave(true);
    }
}
