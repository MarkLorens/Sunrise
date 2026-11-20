using System.Collections.Generic;
using UnityEngine;

// The park mascot, standing in front of the locker room's propane fan and not letting anyone touch it (TankFan). He
// can't be possessed: he's all costume. Blow the smelly gutter's stink at him (Gutter, its fan turned his way) and he
// gags and runs off for good.
// While guarding he shows off, never straying far from his post: he walks a little way to one side (paceDistance),
// stands for idleTime, does a little hop into a pose (mascot_pose_1, then _2, in turn), holds it rocking on the spot
// for poseTime, then walks to the other side and does it again. Running off he's in his panic pose.
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public class Mascot : MonoBehaviour
{
    public static readonly List<Mascot> All = new();

    public float guardRadius = 6f;  // how far from him a fan counts as his
    public float stinkTime = 2f;    // seconds of stink before he's had enough
    public float runDistance = 18f;
    public float paceDistance = 1.5f; // how far he walks to each side of his post
    public float idleTime = 1.5f;     // seconds standing still before he hops into a pose
    public float poseTime = 2f;       // seconds in each pose
    public float hopTime = 0.35f, hopHeight = 0.3f;

    public bool Guarding => !gone;

    GroundWalker walker;
    CharacterSprite look;
    Vector3 post, away;
    bool gone, leaving;
    float stink;
    enum Show { Walking, Standing, Hopping, Posing }
    Show show;
    float showUntil, hopStart;
    int nextPose, side = -1; // -1 = left of his post, 1 = right
    Rigidbody rb;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Awake()
    {
        walker = GetComponent<GroundWalker>();
        look = GetComponent<CharacterSprite>();
        if (!TryGetComponent(out Visitor body)) body = gameObject.AddComponent<Visitor>(); // loads the sprite
        body.brain = this;
        body.possessable = false;
        rb = GetComponent<Rigidbody>();
        walker.speed = 2f;
        post = transform.position;
    }

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
        ShowOff();

        // The stink reaches him: a smellOnly gutter that's open, its fan blowing his way.
        stink = Smelly() ? stink + Time.deltaTime : 0f;
        if (stink < stinkTime) return;
        Leave(false);
    }

    // Walk to one side (idle art), stand, hop, pose; then the same on the other side with the other pose.
    void ShowOff()
    {
        switch (show)
        {
            case Show.Walking:
                if (walker.WalkTo(Side())) return;
                show = Show.Standing;
                showUntil = Time.time + idleTime;
                return;

            case Show.Standing:
                walker.move = Vector2.zero;
                if (Time.time < showUntil) return;
                if (look.poses.Length == 0) { NextSide(); return; }
                show = Show.Hopping;
                hopStart = Time.time;
                return;

            case Show.Hopping:
                float k = Mathf.Clamp01((Time.time - hopStart) / hopTime);
                Vector3 p = transform.position;
                Place(new Vector3(p.x, post.y + Mathf.Sin(k * Mathf.PI) * hopHeight, p.z));
                if (k < 1f) return;
                look.pose = look.poses[nextPose]; // landed: strike the pose
                nextPose = (nextPose + 1) % look.poses.Length;
                look.dance = true;
                show = Show.Posing;
                showUntil = Time.time + poseTime;
                return;

            case Show.Posing:
                if (Time.time < showUntil) return;
                NextSide();
                return;
        }
    }

    void NextSide()
    {
        look.pose = null;
        look.dance = false;
        side = -side;
        show = Show.Walking;
    }

    // A spot paceDistance to his side of the post (left or right on screen); his post if it's blocked.
    Vector3 Side()
    {
        Vector3 spot = post + Vector3.right * side * paceDistance;
        return walker.IsWalkable(spot) ? spot : post;
    }

    void Place(Vector3 p) => rb.position = transform.position = p;

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
        look.pose = null; // done showing off
        look.dance = false;
        if (show == Show.Hopping) Place(new Vector3(transform.position.x, post.y, transform.position.z)); // back on the ground
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
        look.panicBody = true; // mascot_panic
    }

    // Loading a save (SaveGame).
    public void Restore()
    {
        if (Mischief.IsDone(Mischief.Task.SmellMascot)) Leave(true);
    }
}
