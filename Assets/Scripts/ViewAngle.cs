using UnityEngine;

// How this scene is looked at. SampleScene has none: pictures stand upright and the camera keeps its own angle.
// The 45° scene ("SampleScene 45") has one set to 45, like the 45d-version branch: every standing picture leans back
// 45° to face a camera looking down at 45°. It runs before everything else, so people, props and the camera are
// built at the scene's angle.
[DefaultExecutionOrder(-1000)]
public class ViewAngle : MonoBehaviour
{
    public static ViewAngle Instance { get; private set; }

    [Tooltip("Degrees the standing pictures lean back (the camera looks down at the same angle).")]
    public float lean = 45f;
    [Tooltip("Direction and base distance from the camera's target (CameraRig.offset). Looks down at 45°, 10.5 away: a little closer than SampleScene's 12.35.")]
    public Vector3 cameraOffset = new(0f, 7.42f, -7.42f);
    [Tooltip("How much closer / further than the offset the camera may go (CameraRig.dollyRange).")]
    public Vector2 dollyRange = new(-2f, 4f);

    void Awake()
    {
        Instance = this;
        ParkArt.SetLean(lean);
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        ParkArt.SetLean(0f);
    }
}
