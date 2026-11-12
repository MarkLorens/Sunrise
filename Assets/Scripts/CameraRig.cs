using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// A fixed-angle Cinemachine camera that stays centred on whoever you control (the spirit, or the visitor it's
// possessing) and trails them by a hair: it eases into a move and glides to a stop when you do, with no dead zone
// and no look-ahead, so the player never drifts off-centre. Optionally (framePointsOfInterest) it also eases
// anything interesting nearby into shot, Untitled Goose Game style. Spotlight hands the shot to an event for a moment.
// Shake is smooth noise (a short sway that settles), not a new random spot every frame.
// While the host rides the Ferris wheel a second camera sits in their cabin and looks out over the park, rising
// and falling with the wheel; WASD turns the rider's head. The brain blends between the two.
// GameHud puts it on the main camera; it builds the Cinemachine pieces itself.
[DefaultExecutionOrder(-50)] // place the cabin camera after the wheel moves, before the brain reads it
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

    [Header("Depth of field")]
    // Tilt-shift look: the player stays sharp, the ground in front and behind blurs, so the park reads as a diorama.
    // Focus follows whoever you control (and the dolly). Blur grows with focal length and shrinks with aperture.
    public bool depthOfField = true;
    [Range(1, 300)] public float focalLength = 120f; // mm
    [Range(1, 32)] public float aperture = 4f;       // f-stop

    [Header("Riding")]
    public float rideFov = 85f;
    public float rideTilt = 25f;     // degrees looking down when you get in
    public float lookYaw = 100f;     // how far you can turn your head either way
    public Vector2 lookPitch = new(-5f, 70f);
    public float lookSpeed = 80f;    // degrees per second
    public Vector3 seat = new(0f, -0.2f, -2f);   // eye position from the cabin's hook: a step back toward the park, for a wider view
    public float rideBlend = 1.2f;   // seconds to swoop into / out of the cabin

    CinemachineTargetGroup group;
    CinemachinePositionComposer body;
    CinemachineCameraOffset shakeOffset, rideShakeOffset;
    CinemachineCamera rideCam;
    Transform target, spot;
    float spotUntil;
    float shake, shakeRate = 1.5f; // current strength, and how much of it fades per second

    Transform cabin;
    Vector2 look;    // yaw, pitch

    VolumeProfile dofProfile;
    DepthOfField dof;

    // Adds screen shake: amount = strength (0.1 = light hit, 0.3 = big impact), seconds = how long until it settles
    // (left out: stronger shakes last longer). Overlapping shakes add up and settle together.
    public static void Shake(float amount, float seconds = 0f)
    {
        var rig = Instance;
        if (rig == null || amount <= 0f) return;
        if (seconds <= 0f) seconds = amount / 1.5f;
        float left = rig.shake / rig.shakeRate; // seconds the shake already going still has
        rig.shake = Mathf.Min(rig.shake + amount, 0.5f);
        rig.shakeRate = rig.shake / Mathf.Max(left, seconds);
    }

    // Briefly hands the shot to something else (e.g. the ferris wheel rolling away), then eases back to the player.
    public static void Spotlight(Transform t, float seconds, float radius = 1f)
    {
        var rig = Instance;
        if (rig == null || t == null) return;
        if (rig.spot != null) rig.group.RemoveMember(rig.spot);
        rig.group.RemoveMember(t); // it may already be in as a point of interest
        rig.group.AddMember(t, 1f, radius);
        rig.spot = t;
        rig.spotUntil = Time.unscaledTime + seconds;
    }

    // Hands the shot back to the player now (it eases back over fadeTime).
    public static void EndSpotlight()
    {
        if (Instance != null) Instance.spotUntil = 0f;
    }

    void Awake()
    {
        Instance = this;
        // The scene has the camera riding on the spirit; Cinemachine drives it now.
        transform.SetParent(null, true);
        transform.localScale = Vector3.one;

        if (!TryGetComponent(out CinemachineBrain brain)) brain = gameObject.AddComponent<CinemachineBrain>();
        brain.IgnoreTimeScale = true; // keeps moving through hit-stop / slow-mo like the old rig
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, rideBlend);
        var lens = LensSettings.FromCamera(GetComponent<Camera>());

        // Temporal anti-aliasing: blends the last few frames. Besides smoothing edges, it turns the see-through hole's
        // moving dither (SeeThrough.hlsl) into a smooth fade instead of a visible dot pattern.
        var urp = GetComponent<Camera>().GetUniversalAdditionalCameraData();
        urp.antialiasing = AntialiasingMode.TemporalAntiAliasing;
        urp.antialiasingQuality = AntialiasingQuality.High;
        // Average more frames (blend) and clamp the history less tightly, so the dither noise melts into a smooth
        // fade. Push these further and fast movement starts to leave faint trails (ghosting).
        var taa = urp.taaSettings;
        taa.baseBlendFactor = 0.95f;     // default 0.9
        taa.varianceClampScale = 1.5f;   // default 0.9
        urp.taaSettings = taa;

        group =new GameObject("Camera Target Group").AddComponent<CinemachineTargetGroup>();

        var go = new GameObject("Framing Camera");
        go.transform.rotation = Quaternion.LookRotation(-offset, Vector3.up); // fixed angle: no aim component
        var cam = go.AddComponent<CinemachineCamera>();
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

        // Placed by hand each frame (no body/aim), switched on only while riding so it takes over the shot.
        var ride = new GameObject("Cabin Camera");
        rideCam = ride.AddComponent<CinemachineCamera>();
        lens.FieldOfView = rideFov;
        rideCam.Lens = lens;
        rideShakeOffset = ride.AddComponent<CinemachineCameraOffset>();
        rideCam.enabled = false;

        dofProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        dof = dofProfile.Add<DepthOfField>();
        dof.mode.Override(DepthOfFieldMode.Bokeh);
        var vol = new GameObject("Depth Of Field").AddComponent<Volume>();
        vol.transform.SetParent(go.transform, false);
        vol.isGlobal = true;
        vol.priority = 20;
        vol.sharedProfile = dofProfile;
    }

    void OnDestroy()
    {
        if (group != null) Destroy(group.gameObject);
        if (shakeOffset != null) Destroy(shakeOffset.gameObject);
        if (rideCam != null) Destroy(rideCam.gameObject);
        if (dofProfile != null) Destroy(dofProfile);
    }

    void LateUpdate()
    {
        var spirit = Possession.Instance;
        var ride = spirit != null && spirit.Riding ? spirit.Host.Cabin : null;
        if (ride != cabin)
        {
            cabin = ride;
            look = new Vector2(0f, rideTilt);
            rideCam.enabled = cabin != null; // the most recently enabled camera goes live; the brain blends
        }
        if (cabin != null) Ride();
        body.Damping = Vector3.one * followDamping; // same on every axis, so moving up/down the screen feels like left/right
        Follow(spirit != null ? spirit.Focus : null);
        Focus();

        // Smooth noise, so the camera sways and settles instead of buzzing; wider than tall, like a hit's knock.
        shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * shakeRate);
        float t = Time.unscaledTime * shakeFrequency;
        var sway = new Vector3(Mathf.PerlinNoise(t, 0.3f) * 2f - 1f, (Mathf.PerlinNoise(t, 7.7f) * 2f - 1f) * 0.6f, 0f);
        shakeOffset.Offset = rideShakeOffset.Offset = sway * (shake * shakeDistance);
    }

    // Keeps the framing camera's group up to date (also while riding, so stepping out lands on a sensible shot).
    void Follow(Transform focus)
    {
        if (focus != target)
        {
            if (target != null) group.RemoveMember(target);
            target = focus;
            if (target != null) group.AddMember(target, 1f, 1f);
        }
        if (spot != null && Time.unscaledTime >= spotUntil)
        {
            group.RemoveMember(spot);
            spot = null;
        }
        if (target != null)
        {
            // The player fades out of the shot while something is spotlit, and back in afterwards.
            var me = group.Targets[group.FindMember(target)];
            me.Weight = Mathf.MoveTowards(me.Weight, spot != null ? 0f : 1f, Time.unscaledDeltaTime / fadeTime);
            UpdateInterest();
        }
    }

    // Focus on the player (or whatever's spotlit) along the view axis; off in the cabin, where you look out over the whole park.
    void Focus()
    {
        dof.active = depthOfField && cabin == null && target != null;
        if (!dof.active) return;
        var cam = transform; // the brain places the main camera
        var on = spot != null ? spot : target;
        dof.focusDistance.Override(Mathf.Max(0.5f, Vector3.Dot(on.position - cam.position, cam.forward)));
        dof.focalLength.Override(focalLength);
        dof.aperture.Override(aperture);
    }

    // In the cabin, facing out over the park (the wheel stands at the back of it).
    void Ride()
    {
        float dt = Time.unscaledDeltaTime;
        var kb = Keyboard.current;
        if (!Possession.InputBlocked && kb != null)
        {
            float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
            look.x = Mathf.Clamp(look.x + x * lookSpeed * dt, -lookYaw, lookYaw);
            look.y = Mathf.Clamp(look.y - y * lookSpeed * dt, lookPitch.x, lookPitch.y);
        }

        rideCam.transform.SetPositionAndRotation(cabin.position + seat * cabin.lossyScale.y, Quaternion.Euler(look.y, 180f + look.x, 0f));
    }

    // Points of interest ease in (weight up) when they come within joinRadius and ease out past leaveRadius
    // (or all of them, when framePointsOfInterest is switched off).
    void UpdateInterest()
    {
        Vector3 here = target.position;
        if (framePointsOfInterest)
            foreach (var t in Interesting())
            {
                if (t == target || t == spot || t.IsChildOf(target)) continue;
                if (group.FindMember(t) < 0 && Vector3.Distance(here, t.position) < joinRadius) group.AddMember(t, 0f, 0.8f);
            }

        int inRangeCount = 0;
        foreach (var m in group.Targets) if (m.Object != target && m.Object != spot && InRange(m.Object, here)) inRangeCount++;
        // Split the pull between them, so a cluster of ride parts doesn't drag the camera off the player.
        float each = interestWeight / Mathf.Max(1, inRangeCount);
        float step = Time.unscaledDeltaTime / fadeTime;

        // Walk backwards so removing doesn't skip anyone.
        for (int i = group.Targets.Count - 1; i >= 0; i--)
        {
            var m = group.Targets[i];
            if (m.Object == target || m.Object == spot) continue;
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
