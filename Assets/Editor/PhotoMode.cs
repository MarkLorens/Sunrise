using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Photo Mode: in Play mode, flies the camera freely around the park for screenshots (PhotoCamera), with the
// HUD hidden and time frozen if you like. Shots go to <project>/Screenshots.
public class PhotoMode : EditorWindow
{
    int superSize = 2;

    [MenuItem("Tools/Photo Mode")]
    static void Open() => GetWindow<PhotoMode>("Photo Mode");

    void OnInspectorUpdate() => Repaint(); // the speed follows the scroll wheel

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Click the Game view first. WASD to move, E / Q up and down, hold the right mouse button to look, " +
                                "Shift for faster, scroll to set the speed, F12 for a quick screenshot. The camera stays over the park.",
                                MessageType.None);
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play mode first.", MessageType.Info);
            return;
        }

        var cam = PhotoCamera.Instance;
        bool on = GUILayout.Toggle(cam != null, cam != null ? "Flying: click to give the camera back" : "Fly the camera", "Button", GUILayout.Height(28));
        if (on && cam == null) cam = PhotoCamera.Begin();
        else if (!on && cam != null)
        {
            PhotoCamera.End();
            cam = null;
        }

        if (cam != null)
        {
            cam.hideHud = EditorGUILayout.Toggle("Hide HUD", cam.hideHud);
            cam.freezeTime = EditorGUILayout.Toggle("Freeze time", cam.freezeTime);
            cam.speed = EditorGUILayout.Slider("Speed", cam.speed, 0.5f, 60f);
            cam.minClearance = EditorGUILayout.Slider("Lowest above floor", cam.minClearance, 0.1f, 5f);
            cam.maxHeight = EditorGUILayout.Slider("Highest", cam.maxHeight, 2f, 100f);
        }

        EditorGUILayout.Space();
        superSize = EditorGUILayout.IntSlider("Resolution ×", superSize, 1, 4);
        if (GUILayout.Button("Take screenshot", GUILayout.Height(24)))
        {
            int size = superSize;
            // Rendered after this window has drawn, not in the middle of its OnGUI.
            EditorApplication.delayCall += () =>
            {
                if (!Application.isPlaying) return;
                string path = PhotoCamera.Capture(size);
                ShowNotification(new GUIContent(path != null ? "Saved " + Path.GetFileName(path) : "No camera to shoot"));
            };
        }
        if (GUILayout.Button("Open screenshots folder"))
        {
            Directory.CreateDirectory(PhotoCamera.Folder);
            EditorUtility.RevealInFinder(PhotoCamera.Folder);
        }
    }
}
