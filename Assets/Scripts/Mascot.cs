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
        GameHud.Think(GameHud.ThoughtPriority.Action, GameText.ThinkStinkGone);
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

    // The spirit tries to possess him (Possession.TryPossess): it never works. Usually he just says "...", each new
    // one replacing the last; now and then (warnChance) he warns it off instead, the camera closing in on the two of
    // them while he says it, and after that he pays the spirit no more mind.
    public float warnChance = 0.1f;
    public float warnTime = 2f;
    public float warnZoom = 0.5f;
    public float warnVignette = 0.6f; // how dark the edges get meanwhile
    bool warned;

    public bool Warned => warned;
    public bool Teasable => Guarding && !warned;

    public void Tease()
    {
        if (!Teasable) return;
        GameHud.Hush(transform);
        if (Random.value >= warnChance)
        {
            GameHud.Shout(transform, GameText.MascotSilent, 1.5f, true);
            return;
        }
        warned = true;
        GameHud.Shout(transform, GameText.MascotWarnings[Random.Range(0, GameText.MascotWarnings.Length)], warnTime);
        StartCoroutine(CloseUp());
    }

    // The camera on the spot between him and the spirit, zoomed in, the screen going dark around them, for as long as
    // he's talking.
    System.Collections.IEnumerator CloseUp()
    {
        var spirit = Possession.Instance.transform;
        var between = new GameObject("Mascot Close-Up").transform;
        between.position = (transform.position + spirit.position) * 0.5f;
        CameraRig.Spotlight(between, warnTime, 1.5f);
        CameraRig.Zoom(warnZoom);

        // A dark ring closing in round them, over the night's own vignette (NightLighting, priority 10).
        var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
        var ring = profile.Add<UnityEngine.Rendering.Universal.Vignette>();
        ring.intensity.Override(warnVignette);
        ring.smoothness.Override(0.6f);
        ring.color.Override(Color.black);
        var volume = between.gameObject.AddComponent<UnityEngine.Rendering.Volume>();
        volume.isGlobal = true;
        volume.priority = 30;
        volume.sharedProfile = profile;
        volume.weight = 0f;

        const float fade = 0.35f;
        var cam = Camera.main;
        for (float t = 0f; t < warnTime + fade; t += Time.unscaledDeltaTime)
        {
            between.position = (transform.position + spirit.position) * 0.5f;
            if (cam != null) ring.center.Override((Vector2)cam.WorldToViewportPoint(between.position));
            volume.weight = Mathf.Min(t / fade, (warnTime + fade - t) / fade); // in fast, out as he finishes
            if (t >= warnTime) CameraRig.EndZoom();
            yield return null;
        }
        CameraRig.EndZoom();
        CameraRig.EndSpotlight();
        yield return null; // let the rig let go of it first
        yield return null;
        Destroy(profile);
        Destroy(between.gameObject);
    }

    // Loading a save (SaveGame).
    public void Restore(bool warnedSpirit)
    {
        warned = warnedSpirit;
        if (Mischief.IsDone(Mischief.Task.SmellMascot)) Leave(true);
    }
}
