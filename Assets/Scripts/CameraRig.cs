using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

// Untitled Goose Game style "dynamic framing" on Cinemachine: a fixed-angle camera frames whoever you control
// (the spirit, or the visitor it's possessing) plus anything interesting nearby (interactables, mechanics, ride
// operators), dollying out to fit them and easing them in and out of shot. Shakes on big hits.
// While the host rides the Ferris wheel a second camera sits in their cabin and looks out over the park, rising
// and falling with the wheel; WASD turns the rider's head. The brain blends between the two.
// GameHud puts it on the main camera; it builds the Cinemachine pieces itself.
[DefaultExecutionOrder(-50)] // place the cabin camera after the wheel moves, before the brain reads it
public class CameraRig : MonoBehaviour
{
    public static CameraRig Instance { get; private set; }

    public Vector3 offset = new(0f, 10f, -12f); // direction and base distance from the target
    public float lookAhead = 0.4f;            // seconds of look-ahead in the direction of travel

    [Header("Framing")]
    public float joinRadius = 6f;     // things closer than this ease into shot...
    public float leaveRadius = 8f;    // ...and ease out again past this (the gap stops flicker at the edge)
    public float interestWeight = 0.5f; // combined pull of everything nearby vs. the player's 1
    public float fadeTime = 0.8f;     // seconds for a point of interest to fully ease in/out
    public Vector2 dollyRange = new(-2f, 6f); // how much closer / further than `offset` the camera may go

    [Header("Riding")]
    public float rideFov = 85f;
    public float rideTilt = 25f;     // degrees looking down when you get in
    public float lookYaw = 100f;     // how far you can turn your head either way
    public Vector2 lookPitch = new(-5f, 70f);
    public float lookSpeed = 80f;    // degrees per second
    public Vector3 seat = new(0f, -0.2f, -2f);   // eye position from the cabin's hook: a step back toward the park, for a wider view
    public float rideBlend = 1.2f;   // seconds to swoop into / out of the cabin

    CinemachineTargetGroup group;
    CinemachineCameraOffset shakeOffset, rideShakeOffset;
    CinemachineCamera rideCam;
    Transform target, spot;
    float spotUntil;
    float shake;

    Transform cabin;
    Vector2 look;    // yaw, pitch

    // Adds screen shake (0.1 = light hit, 0.3 = big impact). Decays on its own.
    public static void Shake(float amount)
    {
        if (Instance != null) Instance.shake = Mathf.Min(Instance.shake + amount, 0.5f);
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

        group = new GameObject("Camera Target Group").AddComponent<CinemachineTargetGroup>();

        var go = new GameObject("Framing Camera");
        go.transform.rotation = Quaternion.LookRotation(-offset, Vector3.up); // fixed angle: no aim component
        var cam = go.AddComponent<CinemachineCamera>();
        cam.Lens = lens;
        cam.Follow = group.transform;

        var body = go.AddComponent<CinemachinePositionComposer>();
        body.CameraDistance = offset.magnitude;
        body.Damping = new Vector3(0.3f, 0.3f, 0.6f);
        body.Lookahead = new LookaheadSettings { Enabled = true, Time = lookAhead, Smoothing = 8f, IgnoreY = true };
        body.Composition.DeadZone = new ScreenComposerSettings.DeadZoneSettings { Enabled = true, Size = new Vector2(0.08f, 0.08f) };

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
    }

    void OnDestroy()
    {
        if (group != null) Destroy(group.gameObject);
        if (shakeOffset != null) Destroy(shakeOffset.gameObject);
        if (rideCam != null) Destroy(rideCam.gameObject);
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
        Follow(spirit != null ? spirit.Focus : null);

        shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 1.5f);
        Vector3 jolt = shake > 0f ? (Vector3)Random.insideUnitCircle * shake * 0.5f : Vector3.zero;
        shakeOffset.Offset = rideShakeOffset.Offset = jolt;
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

    // Points of interest ease in (weight up) when they come within joinRadius and ease out past leaveRadius.
    void UpdateInterest()
    {
        Vector3 here = target.position;
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
            bool inRange = InRange(m.Object, here);
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
