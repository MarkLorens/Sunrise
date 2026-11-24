#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

// Tools > Photo Mode (Editor only, never in a build): takes the main camera off the player and flies it freely, for
// screenshots. In the Game view: WASD moves, E / Q up and down, hold the right mouse button to look around, Shift
// for faster, the scroll wheel sets the speed, F12 takes a screenshot. It stays over the park: inside the ground
// tilemap's area, never lower than `minClearance` above the floor (the platform included), never above `maxHeight`.
// A dev tool, so it reads the keyboard and mouse directly rather than through Controls; the player's own input is
// blocked while it flies, so the spirit stays put. Depth of field is off while it flies (CameraRig focuses it on the
// player, not where this camera looks), and Hide HUD also hides FMOD's debug overlay.
[DefaultExecutionOrder(1000)] // after CameraRig, so nothing moves the camera after it
public class PhotoCamera : MonoBehaviour
{
    public static PhotoCamera Instance { get; private set; }

    public float speed = 6f;          // metres per second (Shift: three times that)
    public float lookSpeed = 0.15f;   // degrees per pixel of mouse movement
    public float minClearance = 0.5f; // the lowest it goes above the floor
    public float maxHeight = 30f;
    public bool freezeTime;
    public bool hideHud = true;

    CinemachineBrain brain;
    Vector3 homePosition;
    Quaternion homeRotation;
    float yaw, pitch;
    Rect area; // the park's ground, x by z
    bool blockedInput;
    float timeScaleBefore = 1f;
    bool frozen;
    readonly List<Canvas> hidden = new();
    readonly List<Behaviour> hiddenOverlays = new();
    CameraRig rig;
    bool rigDepthOfField;

    public static string Folder => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Screenshots");

    public static PhotoCamera Begin()
    {
        if (Instance != null) return Instance;
        var cam = Camera.main;
        return cam != null ? cam.gameObject.AddComponent<PhotoCamera>() : null;
    }

    public static void End()
    {
        if (Instance != null) Destroy(Instance);
    }

    // What the main camera sees at `superSize` times the Game view's resolution, into <project>/Screenshots. Returns
    // the file's path, or null. Rendered into a texture of its own (as GameHud.ParkShot does for save cards), not
    // ScreenCapture.CaptureScreenshot: its supersized capture crashed the Editor on Metal. Being the camera's own
    // picture, it has no HUD in it (screen-space canvases are drawn over the screen, not by the camera).
    public static string Capture(int superSize)
    {
        var cam = Camera.main;
        if (cam == null) return null;
        int max = Mathf.Min(SystemInfo.maxTextureSize, 8192);
        int s = Mathf.Clamp(superSize, 1, 4);
        float fit = Mathf.Min(1f, (float)max / Mathf.Max(Screen.width * s, Screen.height * s)); // stay within the GPU's limit
        int w = Mathf.Max(1, Mathf.RoundToInt(Screen.width * s * fit));
        int h = Mathf.Max(1, Mathf.RoundToInt(Screen.height * s * fit));

        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
        var old = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = old;
        var was = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = was;
        RenderTexture.ReleaseTemporary(rt);

        Directory.CreateDirectory(Folder);
        string path = Path.Combine(Folder, $"Sunrise_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Destroy(tex);
        return path;
    }

    void OnEnable()
    {
        Instance = this;
        homePosition = transform.position;
        homeRotation = transform.rotation;
        if (TryGetComponent(out brain)) brain.enabled = false; // Cinemachine would put it straight back on the player
        if (TryGetComponent(out rig))
        {
            rigDepthOfField = rig.depthOfField;
            rig.depthOfField = false;
        }
        Vector3 e = transform.eulerAngles;
        yaw = e.y;
        pitch = e.x > 180f ? e.x - 360f : e.x;
        area = ParkArea();
    }

    void OnDisable()
    {
        if (Instance == this) Instance = null;
        if (brain != null) brain.enabled = true;
        if (rig != null) rig.depthOfField = rigDepthOfField;
        transform.SetPositionAndRotation(homePosition, homeRotation); // the brain eases on from here, no jump
        Freeze(false);
        ShowHud();
        if (blockedInput) Possession.InputBlocked = false;
        blockedInput = false;
    }

    void LateUpdate()
    {
        // Every frame: a cutscene ending lets go of the input, and the HUD rebuilds bits of itself (captions, toasts).
        if (!Possession.InputBlocked)
        {
            Possession.InputBlocked = true;
            blockedInput = true;
        }
        Freeze(freezeTime);
        if (hideHud) HideHud();
        else ShowHud();

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        float dt = Time.unscaledDeltaTime; // flies the same with time frozen

        if (mouse != null)
        {
            if (mouse.rightButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue();
                yaw += d.x * lookSpeed;
                pitch = Mathf.Clamp(pitch - d.y * lookSpeed, -89f, 89f);
            }
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f) speed = Mathf.Clamp(speed * (scroll > 0f ? 1.15f : 1f / 1.15f), 0.5f, 60f);
        }
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        if (kb == null) return;
        Vector3 move = Vector3.zero;
        if (kb.wKey.isPressed) move += transform.forward;
        if (kb.sKey.isPressed) move -= transform.forward;
        if (kb.dKey.isPressed) move += transform.right;
        if (kb.aKey.isPressed) move -= transform.right;
        if (kb.eKey.isPressed) move += Vector3.up;
        if (kb.qKey.isPressed) move -= Vector3.up;
        float s = speed * (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed ? 3f : 1f);
        transform.position = Bound(transform.position + Vector3.ClampMagnitude(move, 1f) * (s * dt));

        if (kb.f12Key.wasPressedThisFrame) Debug.Log("Photo Mode: saved " + Capture(2));
    }

    // Over the park's ground, above its floor, under the ceiling.
    Vector3 Bound(Vector3 p)
    {
        p.x = Mathf.Clamp(p.x, area.xMin, area.xMax);
        p.z = Mathf.Clamp(p.z, area.yMin, area.yMax);
        p.y = Mathf.Clamp(p.y, RaisedFloor.Height(p) + minClearance, Mathf.Max(maxHeight, minClearance));
        return p;
    }

    // The ground tilemap's tiles, in world x and z (the walkers' ground, or a tilemap called Ground).
    static Rect ParkArea()
    {
        var walker = FindAnyObjectByType<GroundWalker>();
        Tilemap ground = walker != null ? walker.ground : null;
        if (ground == null)
            foreach (var t in FindObjectsByType<Tilemap>())
                if (t.name == "Ground") { ground = t; break; }
        if (ground == null) return new Rect(-1000f, -1000f, 2000f, 2000f);
        BoundsInt b = ground.cellBounds;
        Vector3 a = ground.CellToWorld(b.min), c = ground.CellToWorld(b.max);
        return Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.z, c.z), Mathf.Max(a.x, c.x), Mathf.Max(a.z, c.z));
    }

    void Freeze(bool on)
    {
        if (on == frozen) return;
        frozen = on;
        if (on)
        {
            timeScaleBefore = Time.timeScale;
            Time.timeScale = 0f;
        }
        else Time.timeScale = timeScaleBefore;
    }

    void HideHud()
    {
        foreach (var c in FindObjectsByType<Canvas>())
            if (c.enabled && c.isRootCanvas && c.renderMode != RenderMode.WorldSpace)
            {
                c.enabled = false;
                hidden.Add(c);
            }
        // FMOD's debug overlay: drawn by its own OnGUI, not a canvas, on an object hidden from the hierarchy (so
        // FindObjectsByType doesn't see it).
        foreach (var o in Resources.FindObjectsOfTypeAll<FMODUnity.FMODRuntimeManagerOnGUIHelper>())
            if (o.enabled && !UnityEditor.EditorUtility.IsPersistent(o))
            {
                o.enabled = false;
                hiddenOverlays.Add(o);
            }
    }

    void ShowHud()
    {
        foreach (var c in hidden)
            if (c != null) c.enabled = true;
        hidden.Clear();
        foreach (var o in hiddenOverlays)
            if (o != null) o.enabled = true;
        hiddenOverlays.Clear();
    }
}
#endif
