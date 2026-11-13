using UnityEngine;

// Keeps whoever you control visible when a tree, lamp or stall stands between them and the camera.
// This half only measures: every frame it tells all shaders (as global values) where the player is and how big
// the see-through hole should be. The sprite shader (SpriteLit3D, via Palletes/Shaders/SeeThrough.hlsl) reads
// these and dithers away the pixels of anything in front of the player inside that circle.
// GameHud puts it on the main camera, next to CameraRig.
[DefaultExecutionOrder(100)] // after CameraRig and the Cinemachine brain have placed the camera this frame
public class SeeThrough : MonoBehaviour
{
    [Tooltip("Radius of the see-through hole around the player, in world units.")]
    public float radius = 2f;
    [Range(0f, 1f), Tooltip("How much of the radius is a soft, dithered edge (0 = hard edge).")]
    public float softness = 0.8f;
    [Tooltip("Seconds for the hole to open or close.")]
    public float fadeTime = 0.25f;

    static readonly int PosId = Shader.PropertyToID("_SeeThroughPos");           // xyz: player's middle, w: radius
    static readonly int SoftnessId = Shader.PropertyToID("_SeeThroughSoftness"); // fraction of the radius
    static readonly int StrengthId = Shader.PropertyToID("_SeeThroughStrength"); // 0 = off, 1 = full hole
    static readonly int FrameId = Shader.PropertyToID("_SeeThroughFrame");       // moves the dither every frame

    float strength;

    // Off when nothing is set up yet, so shaders never act on stale values.
    void OnDisable() => Shader.SetGlobalFloat(StrengthId, 0f);

    void LateUpdate()
    {
        var spirit = Possession.Instance;
        Transform target = spirit != null ? spirit.Focus : null;
        bool wanted = target != null;
        strength = Mathf.MoveTowards(strength, wanted ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime));
        Shader.SetGlobalFloat(StrengthId, strength);
        Shader.SetGlobalFloat(FrameId, Time.frameCount & 63);
        if (target == null) return;

        Vector3 middle = Middle(target);
        Shader.SetGlobalVector(PosId, new Vector4(middle.x, middle.y, middle.z, radius));
        Shader.SetGlobalFloat(SoftnessId, softness);
    }

    // The middle of the player's picture.
    static Vector3 Middle(Transform t) =>
        t.TryGetComponent(out SpriteRenderer r) && r.sprite != null ? r.bounds.center : t.position + Vector3.up * 0.8f;
}
