using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// A fixed-angle Cinemachine camera that stays centred on whoever you control (the spirit, or the visitor it's
// possessing) and trails them by a hair: it eases into a move and glides to a stop when you do, with no dead zone
// and no look-ahead, so the player never drifts off-centre. Optionally (framePointsOfInterest) it also eases
// anything interesting nearby into shot, Untitled Goose Game style. Spotlight hands the shot to an event for a moment,
// gliding over (easing in and out, never a lurch) and framing a spotlit person by their middle, not their feet.
// Shake is smooth noise (a short sway that settles), not a new random spot every frame.
// GameHud puts it on the main camera; it builds the Cinemachine pieces itself.
[DefaultExecutionOrder(-50)] // set up the shot before the brain reads it
public class CameraRig : MonoBehaviour
{
    public static CameraRig Instance { get; private set; }

    public Vector3 offset = new(0f, 6.5f, -10.5f); // direction and base distance from the target
    public float followDamping = 0.5f; // seconds to settle after the player stops: lower = snappier, higher = floatier

    [Header("Shake")]
    public float shakeDistance = 0.8f;  // metres of sway per unit of shake
    public float shakeFrequency = 12f;  // sways per second: lower = rolling, higher = rattly

    [Header("Framing")]
    public bool framePointsOfInterest = false; // ease nearby interactables / staff into shot (pulls the player off-centre)
    public float joinRadius = 6f;     // things closer than this ease into shot...
    public float leaveRadius = 8f;    // ...and ease out again past this (the gap stops flicker at the edge)
    public float interestWeight = 0.5f; // combined pull of everything nearby vs. the player's 1
    public float fadeTime = 0.8f;     // seconds for a point of interest to fully ease in/out
    public Vector2 dollyRange = new(-2f, 6f); // how much closer / further than `offset` the camera may go

    [Header("Zoom")]
    public float zoomTime = 0.5f; // roughly how long Zoom / EndZoom take to ease in and out: higher = slower

    [Header("Tilt")]
    public float tiltTime = 0.6f; // roughly how long Tilt / EndTilt take to ease in and out: higher = slower

    [Header("Depth of field")]
    // Diorama look: the player stays sharp and what's behind them blurs. Focus follows whoever you control (and the
    // dolly). Only behind: what's in front of the player stays sharp, because that's what the see-through hole
    // (SeeThrough.cs) opens up. Blurred too (the old Bokeh mode), its blur spread over the hole and filled it with
    // a frosted grey disc.
    public bool depthOfField = true;
    [Tooltip("World units behind the player where the blur starts with Depth of Field (Options) all the way down.")]
    public float blurStartNearest = 0f;
    [Tooltip("World units behind the player where the blur starts with Depth of Field all the way up.")]
    public float blurStartFarthest = 20f;
    [Tooltip("World units past the start where the blur is full.")]
    public float blurSpan = 10f;
    [Range(0.5f, 1.5f), Tooltip("How strong the full blur is.")]
    public float blurRadius = 1f;

    CinemachineTargetGroup group;
    CinemachinePositionComposer body;
    CinemachineCameraOffset shakeOffset;
    Transform target, spot; // spot: the point on what's spotlit now (a person's is kept on their middle)
    float spotUntil;
    // Everything spotlit: the one now easing in, earlier ones easing out, the player easing the other way. The shot is
    // their weighted average, so it glides from one to the next. Before, the spot came in at full weight and the
    // player left at full weight: the camera's damping then set off at its fastest in one frame (metres at once, a jolt
    // that read as a shake), worse coming back, when the player had faded to nothing and snapped straight back in.
    class Shot
    {
        public Transform on, point; // what was spotlit, and the point the shot uses (a person's middle, or `on` itself)
        public Visitor person;
        public float k, pullBack, lift, liftSpeed, radius; // k: how far it's eased in (0..1)
        public Vector3 last;   // where its point was last frame
        public bool standIn;   // its point is ours, standing in for a marker that's gone
    }
    readonly List<Shot> shots = new();
    Shot spotShot;
    float playerK = 1f;
    float shake, shakeRate = 1.5f; // current strength, and how much of it fades per second
    CinemachineCamera vcam;
    bool orthographic;
    float baseLens; // the scene camera's field of view (or orthographic size)
    float zoom = 1f, zoomTarget = 1f, zoomVelocity;
    CinemachineRecomposer recomposer; // turns the finished shot (Tilt) without moving the camera
    float tilt, tiltTarget, tiltVelocity;

    VolumeProfile dofProfile;
    DepthOfField dof;

    // Adds screen shake: amount = strength (0.1 = light hit, 0.3 = big impact), seconds = how long until it settles
    // (left out: stronger shakes last longer). Overlapping shakes add up and settle together.
    public static void Shake(float amount, float seconds = 0f)
    {
        var rig = Instance;
        if (seconds <= 0f) seconds = amount / 1.5f;
        amount *= Settings.ShakeStrength; // the player's Screen shake setting
        if (rig == null || amount <= 0f) return;
        float left = rig.shake / rig.shakeRate; // seconds the shake already going still has
        rig.shake = Mathf.Min(rig.shake + amount, 0.5f);
        rig.shakeRate = rig.shake / Mathf.Max(left, seconds);
    }

    // Briefly hands the shot to something else (e.g. the ferris wheel rolling away), then eases back to the player.
    // pullBack: how much further back the camera sits meanwhile, to fit something big in.
    public static void Spotlight(Transform t, float seconds, float radius = 1f, float pullBack = 0f)
    {
        var rig = Instance;
        if (rig == null || t == null) return;
        var shot = rig.shots.Find(x => x.on == t); // spotlit again while still easing out: back in it eases
        if (shot == null)
        {
            rig.group.RemoveMember(t); // it may already be in as a point of interest
            shot = new Shot { on = t, point = t, person = t.GetComponent<Visitor>() };
            if (shot.person != null)
            {
                shot.point = new GameObject("Camera Spot Aim").transform;
                shot.lift = shot.person.HeadHeight * 0.5f;
                shot.point.position = t.position + Vector3.up * shot.lift;
            }
            rig.shots.Add(shot);
            shot.radius = radius;
            shot.last = shot.point.position;
            rig.group.AddMember(shot.point, 0f, radius);
        }
        else
        {
            int m = rig.group.FindMember(shot.point);
            if (m >= 0) rig.group.Targets[m].Radius = radius;
            shot.radius = radius;
        }
        shot.pullBack = pullBack;
        rig.spotShot = shot;
        rig.spot = shot.point;
        rig.spotUntil = Time.unscaledTime + seconds;
    }

    // Hands the shot back to the player now (it eases back over fadeTime).
    public static void EndSpotlight()
    {
        if (Instance != null) Instance.spotUntil = 0f;
    }

    // Where the game hears from (Ears): `player` while nothing's spotlit; while something is, the shot's own blend of
    // the player and what's spotlit (the same weights, so the sound glides over with the picture), at `player`'s height:
    // the wheel's spot is its hub, high above where its sounds play.
    public static Vector3 Hearing(Vector3 player)
    {
        var rig = Instance;
        if (rig == null || rig.shots.Count == 0) return player;
        float total = Ease(rig.playerK);
        Vector3 sum = player * total;
        foreach (var x in rig.shots)
        {
            float w = Ease(x.k);
            Vector3 p = x.point != null ? x.point.position : x.last;
            sum += new Vector3(p.x, player.y, p.z) * w;
            total += w;
        }
        return total > 0f ? sum / total : player;
    }

    // Smoothly zooms the lens in on whatever's in shot: amount 0.6 = everything looks 1/0.6 times bigger
    // (above 1 zooms out). Stays until EndZoom, which eases back to normal.
    public static void Zoom(float amount)
    {
        if (Instance != null) Instance.zoomTarget = Mathf.Max(0.1f, amount);
    }

    public static void EndZoom() => Zoom(1f);

    // Smoothly tips the camera up by `degrees` (negative tips it down), turning on the spot: it stays where it is and
    // just looks up, at something shooting up into the sky. Stays until EndTilt, which eases back to the usual angle.
    public static void Tilt(float degrees)
    {
        if (Instance != null) Instance.tiltTarget = degrees;
    }

    public static void EndTilt() => Tilt(0f);

    // Something else has the shot (a spotlight, a zoom) or the camera's still easing back to the player after one.
    public static bool Busy
    {
        get
        {
            var rig = Instance;
            if (rig == null) return false;
            if (rig.spot != null || rig.zoomTarget != 1f || rig.zoom != 1f || rig.tiltTarget != 0f || rig.tilt != 0f) return true;
            int me = rig.target != null ? rig.group.FindMember(rig.target) : -1;
            return me >= 0 && rig.group.Targets[me].Weight < 1f;
        }
    }

    // Temporal anti-aliasing (Options > Display > Anti-Aliasing, Settings.AntiAliasing): blends the last few frames.
    // Besides smoothing edges, it turns the see-through hole's moving dither (SeeThrough.hlsl) into a smooth fade
    // instead of a visible dot pattern. Off, the picture is sharper but the edges jagged and the hole dotted.
    public void ApplyAntiAliasing()
    {
        var urp = GetComponent<Camera>().GetUniversalAdditionalCameraData();
        if (!Settings.AntiAliasing)
        {
            urp.antialiasing = AntialiasingMode.None;
            return;
        }
        urp.antialiasing = AntialiasingMode.TemporalAntiAliasing;
        urp.antialiasingQuality = AntialiasingQuality.High;
        // Average more frames (blend) and clamp the history less tightly, so the dither noise melts into a smooth
        // fade. Push these further and fast movement starts to leave faint trails (ghosting).
        var taa = urp.taaSettings;
        taa.baseBlendFactor = 0.95f;     // default 0.9
        taa.varianceClampScale = 1.5f;   // default 0.9
        urp.taaSettings = taa;
    }

    void Awake()
    {
        Instance = this;
        // The scene has the camera riding on the spirit; Cinemachine drives it now.
        transform.SetParent(null, true);
        transform.localScale = Vector3.one;

        if (!TryGetComponent(out CinemachineBrain brain)) brain = gameObject.AddComponent<CinemachineBrain>();
        brain.IgnoreTimeScale = true; // keeps moving through hit-stop / slow-mo like the old rig
        // Every frame, after everyone's moved: all the bodies are interpolated, so they move smoothly every frame. Left
        // on SmartUpdate it now and then did the shot on a physics step instead, and the camera held still for a frame
        // and then caught up: a hitch.
        brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
        var lens = LensSettings.FromCamera(GetComponent<Camera>());
        orthographic = GetComponent<Camera>().orthographic;
        baseLens = orthographic ? lens.OrthographicSize : lens.FieldOfView;

        ApplyAntiAliasing();

        group =new GameObject("Camera Target Group").AddComponent<CinemachineTargetGroup>();
        // The weighted average of who's in shot (not the middle of their bounds), so easing the weights glides it.
        group.PositionMode = CinemachineTargetGroup.PositionModes.GroupAverage;

        var go = new GameObject("Framing Camera");
        go.transform.rotation = Quaternion.LookRotation(-offset, Vector3.up); // fixed angle: no aim component
        var cam = vcam = go.AddComponent<CinemachineCamera>();
        cam.Lens = lens;
        cam.Follow = group.transform;

        // Centred, no dead zone or look-ahead: only the damping makes it trail (set every frame in LateUpdate).
        body = go.AddComponent<CinemachinePositionComposer>();
        body.CameraDistance = offset.magnitude;

        var framing = go.AddComponent<CinemachineGroupFraming>();
        framing.SizeAdjustment = CinemachineGroupFraming.SizeAdjustmentModes.DollyOnly;
        framing.FramingSize = 0.65f;
        framing.DollyRange = dollyRange;
        framing.Damping = 2f;

        shakeOffset = go.AddComponent<CinemachineCameraOffset>();

        // Tilt: turns the camera after everything else has placed it. Every field set by hand: its defaults are only
        // filled in when it's added in the editor, and left at 0 the zoom and the follow would go to nothing.
        recomposer = go.AddComponent<CinemachineRecomposer>();
        recomposer.ApplyAfter = CinemachineCore.Stage.Finalize;
        recomposer.Tilt = recomposer.Pan = recomposer.Dutch = 0f;
        recomposer.ZoomScale = 1f;
        recomposer.FollowAttachment = recomposer.LookAtAttachment = 1f;

        dofProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        dof = dofProfile.Add<DepthOfField>();
        dof.mode.Override(DepthOfFieldMode.Gaussian); // far blur only (Bokeh blurs in front of the focus too)
        dof.highQualitySampling.Override(true);
        var vol = new GameObject("Depth Of Field").AddComponent<Volume>();
        vol.transform.SetParent(go.transform, false);
        vol.isGlobal = true;
        vol.priority = 20;
        vol.sharedProfile = dofProfile;
    }

    void OnDestroy()
    {
        if (group != null) Destroy(group.gameObject);
        if (aim != null) Destroy(aim.gameObject);
        foreach (var x in shots)
            if (x.person != null && x.point != null) Destroy(x.point.gameObject);
        if (shakeOffset != null) Destroy(shakeOffset.gameObject);
        if (dofProfile != null) Destroy(dofProfile);
    }

    void LateUpdate()
    {
        var spirit = Possession.Instance;
        body.Damping = Vector3.one * followDamping; // same on every axis, so moving up/down the screen feels like left/right
        Follow(Aim(spirit));
        // Further back for something big spotlit, eased with its weight (Cinemachine doesn't damp distance changes).
        float pullBack = 0f;
        foreach (var x in shots) pullBack += Ease(x.k) * x.pullBack;
        body.CameraDistance = offset.magnitude + pullBack;
        // The group placed now, from where everyone is this frame, before the brain reads it (its own LateUpdate may
        // come after the brain's).
        group.DoUpdate();
        Focus();

        // Smooth noise, so the camera sways and settles instead of buzzing; wider than tall, like a hit's knock.
        shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * shakeRate);
        float t = Time.unscaledTime * shakeFrequency;
        var sway = new Vector3(Mathf.PerlinNoise(t, 0.3f) * 2f - 1f, (Mathf.PerlinNoise(t, 7.7f) * 2f - 1f) * 0.6f, 0f);
        shakeOffset.Offset = sway * (shake * shakeDistance);

        Lens();
        Pitch();
    }

    // Eases the camera's angle toward tiltTarget (Tilt / EndTilt), starting and stopping gently. Only the view turns
    // (the recomposer works on the finished shot), so the camera stays put and the framing isn't thrown off.
    void Pitch()
    {
        if (tilt == tiltTarget) return;
        tilt = Mathf.SmoothDamp(tilt, tiltTarget, ref tiltVelocity, tiltTime, Mathf.Infinity, Time.unscaledDeltaTime);
        if (Mathf.Abs(tilt - tiltTarget) < 0.01f) tilt = tiltTarget;
        recomposer.Tilt = -tilt; // the recomposer's positive tilt looks down
    }

    // Eases the lens toward zoomTarget (Zoom / EndZoom), starting and stopping gently.
    void Lens()
    {
        if (zoom == zoomTarget) return;
        zoom = Mathf.SmoothDamp(zoom, zoomTarget, ref zoomVelocity, zoomTime, Mathf.Infinity, Time.unscaledDeltaTime);
        if (Mathf.Abs(zoom - zoomTarget) < 0.001f) zoom = zoomTarget;
        var lens = vcam.Lens;
        if (orthographic) lens.OrthographicSize = baseLens * zoom;
        // The view's height shrinks by `zoom` (not the angle), so 0.5 really makes things twice as big.
        else lens.FieldOfView = 2f * Mathf.Atan(Mathf.Tan(baseLens * 0.5f * Mathf.Deg2Rad) * zoom) * Mathf.Rad2Deg;
        vcam.Lens = lens;
    }

    // What the camera centres on: the middle of whoever you control, not their feet. Someone you're in stands on their
    // feet (their pivot), so the point is lifted by half their height; the spirit's own pivot is already its middle.
    // Eased, so stepping into or out of someone doesn't jolt the shot.
    Transform Aim(Possession spirit)
    {
        var focus = spirit != null ? spirit.Focus : null;
        if (focus == null) return null;
        if (aim == null) aim = new GameObject("Camera Aim").transform;
        aimed = focus;
        var host = spirit.Host;
        float lift = host != null ? host.HeadHeight * 0.5f : 0f;
        centreLift = Mathf.SmoothDamp(centreLift, lift, ref centreLiftSpeed, followDamping, Mathf.Infinity, Time.unscaledDeltaTime);
        // Their height eased on its own: stairs and the platform lift people a step at a time, and followed as it is
        // that bumped the shot up (or down) on every step. Eased, it glides.
        Vector3 at = focus.position;
        if (!aimHeightSet) aimHeight = at.y;
        aimHeightSet = true;
        aimHeight = Mathf.SmoothDamp(aimHeight, at.y, ref aimHeightSpeed, HeightSmoothing, Mathf.Infinity, Time.unscaledDeltaTime);
        aim.position = new Vector3(at.x, aimHeight + centreLift, at.z);
        return aim;
    }

    Transform aim, aimed; // the point, and whose middle it is
    const float HeightSmoothing = 0.3f; // seconds the shot takes to settle on a new height (a stair, the platform)
    float aimHeight, aimHeightSpeed;
    bool aimHeightSet;
    float centreLift, centreLiftSpeed;

    // Keeps the framing camera's group up to date.
    void Follow(Transform focus)
    {
        if (focus != target)
        {
            if (target != null) group.RemoveMember(target);
            target = focus;
            if (target != null) group.AddMember(target, Ease(playerK), 1f);
        }
        if (spot != null && Time.unscaledTime >= spotUntil)
        {
            spot = null;
            spotShot = null;
        }

        // The spotlit one eases in, earlier ones out (gone once they're out), and the player the other way: all at
        // the same pace, so the weights always add up to the same and the shot glides between them.
        float step = Time.unscaledDeltaTime / fadeTime;
        for (int i = shots.Count - 1; i >= 0; i--)
        {
            var x = shots[i];
            if (x.point == null || x.on == null)
            {
                // Its owner destroyed it (a cutscene's own marker, a person gone): a stand-in where it last was, easing
                // out like any other (dropped at once, its weight left the shot in a frame: a lurch).
                if (x.person != null && x.point != null) Destroy(x.point.gameObject);
                group.Targets.RemoveAll(g => g.Object == null || g.Object == x.point);
                var standIn = new GameObject("Camera Spot (gone)").transform;
                standIn.position = x.last;
                x.point = x.on = standIn;
                x.person = null;
                x.standIn = true;
                group.AddMember(standIn, Ease(x.k), x.radius);
                if (x == spotShot) { spotShot = null; spot = null; }
            }
            x.k = Mathf.MoveTowards(x.k, x == spotShot ? 1f : 0f, step);
            if (x.person != null)
            {
                x.lift = Mathf.SmoothDamp(x.lift, x.person.HeadHeight * 0.5f, ref x.liftSpeed, followDamping, Mathf.Infinity, Time.unscaledDeltaTime);
                x.point.position = x.on.position + Vector3.up * x.lift;
            }
            int m = group.FindMember(x.point);
            if (m >= 0) group.Targets[m].Weight = Ease(x.k);
            x.last = x.point.position;
            if (x.k <= 0f && x != spotShot)
            {
                group.RemoveMember(x.point);
                if (x.person != null || x.standIn) Destroy(x.point.gameObject);
                shots.RemoveAt(i);
            }
        }
        playerK = Mathf.MoveTowards(playerK, spot != null ? 0f : 1f, step);

        if (target != null)
        {
            // The player fades out of the shot while something is spotlit, and back in afterwards.
            group.Targets[group.FindMember(target)].Weight = Ease(playerK);
            UpdateInterest();
        }
    }

    // Starts and ends gently: a weight easing in or out of the shot.
    static float Ease(float k) => Mathf.SmoothStep(0f, 1f, k);

    bool IsShot(Transform t)
    {
        foreach (var x in shots)
            if (x.point == t || x.on == t) return true;
        return false;
    }

    // Focus on the player (or whatever's spotlit) along the view axis.
    void Focus()
    {
        dof.active = depthOfField && target != null;
        if (!dof.active) return;
        var cam = transform; // the brain places the main camera
        var on = spot != null ? spot : target;
        float focus = Mathf.Max(0.5f, Vector3.Dot(on.position - cam.position, cam.forward));
        float start = focus + Mathf.Lerp(blurStartNearest, blurStartFarthest, Settings.DepthOfField);
        dof.gaussianStart.Override(start);
        dof.gaussianEnd.Override(start + Mathf.Max(blurSpan, 0.1f));
        dof.gaussianMaxRadius.Override(blurRadius);
    }

    // Points of interest ease in (weight up) when they come within joinRadius and ease out past leaveRadius
    // (or all of them, when framePointsOfInterest is switched off).
    void UpdateInterest()
    {
        Vector3 here = target.position;
        if (framePointsOfInterest)
            foreach (var t in Interesting())
            {
                if (t == target || IsShot(t) || (aimed != null && t.IsChildOf(aimed))) continue; // not the player themselves
                if (group.FindMember(t) < 0 && Vector3.Distance(here, t.position) < joinRadius) group.AddMember(t, 0f, 0.8f);
            }

        int inRangeCount = 0;
        foreach (var m in group.Targets) if (m.Object != target && !IsShot(m.Object) && InRange(m.Object, here)) inRangeCount++;
        // Split the pull between them, so a cluster of ride parts doesn't drag the camera off the player.
        float each = interestWeight / Mathf.Max(1, inRangeCount);
        float step = Time.unscaledDeltaTime / fadeTime;

        // Walk backwards so removing doesn't skip anyone.
        for (int i = group.Targets.Count - 1; i >= 0; i--)
        {
            var m = group.Targets[i];
            if (m.Object == target || IsShot(m.Object)) continue;
            bool inRange = framePointsOfInterest && InRange(m.Object, here);
            m.Weight = Mathf.MoveTowards(m.Weight, inRange && spot == null ? each : 0f, step * each);
            if (m.Weight <= 0f && !inRange) group.Targets.RemoveAt(i);
        }
    }

    bool InRange(Transform t, Vector3 here) =>
        t != null && t.gameObject.activeInHierarchy && Vector3.Distance(here, t.position) < leaveRadius;

    // ponytail: scans the existing registries every frame; fine for a park this size, use a spatial query if
    // there are ever hundreds of these.
    static IEnumerable<Transform> Interesting()
    {
        foreach (var x in Interactable.All) if (x != null) yield return x.transform;
        foreach (var x in Mechanic.All) if (x != null) yield return x.transform;
        foreach (var x in RideOperator.All) if (x != null) yield return x.transform;
    }
}
