using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Makes "SampleScene 45" from SampleScene: the same park, looked at from 45° like the 45d-version branch.
//   - a View Angle object (ViewAngle, 45): the code leans everything it makes and sets the camera's angle;
//   - the standing tilemaps (Objects, Decor) lean back 45°;
//   - every standing picture in the scene leans back 45° about its feet. Pictures drawn as one (the same depth, like
//     the Ferris wheel's base and wheel) lean together; ones spread out in depth (the queue lane's posts) each lean
//     on their own spot;
//   - their colliders move onto an upright child, so what people bump into stays the same patch of floor.
// SampleScene itself isn't touched: run this again whenever it changes to bring the 45° scene up to date
// (Sunrise > Build 45° Scene). The 45° scene is replaced, so make changes in SampleScene, not here.
public static class Scene45Builder
{
    const string Source = "Assets/Scenes/SampleScene.unity";
    public const string Target = "Assets/Scenes/SampleScene 45.unity";
    const float Lean = 45f;
    const string UprightCollider = "Collider (upright)";
    const float OnePicture = 0.5f; // parts closer together in depth than this lean as one picture

    // Left alone: the player (its look leans in code; its collider and the camera hang off it), and what isn't a
    // standing picture.
    static readonly HashSet<string> Skip = new() { "Props", "Grid", "Visitors", "Audio" };

    [MenuItem("Sunrise/Build 45° Scene")]
    static void BuildFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Build 45° Scene", "Stop Play first.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Target) != null &&
            !EditorUtility.DisplayDialog("Build 45° Scene", $"Replace {Target} with a fresh copy of SampleScene?", "Replace", "Cancel")) return;
        Debug.Log(Build());
    }

    // Returns what it did. Opens the new scene.
    public static string Build()
    {
        var log = new List<string>();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Target) != null) AssetDatabase.DeleteAsset(Target);
        if (!AssetDatabase.CopyAsset(Source, Target)) return "Couldn't copy " + Source;
        var scene = EditorSceneManager.OpenScene(Target, OpenSceneMode.Single);
        Quaternion lean = Quaternion.Euler(Lean, 0f, 0f);

        var angle = new GameObject("View Angle").AddComponent<ViewAngle>();
        angle.lean = Lean;
        angle.transform.SetSiblingIndex(0);

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root == angle.gameObject) continue;
            foreach (var map in root.GetComponentsInChildren<Tilemap>(true))
            {
                if (map.orientation != Tilemap.Orientation.Custom) continue; // the ground lies flat
                Matrix4x4 m = map.orientationMatrix;
                map.orientationMatrix = Matrix4x4.TRS(m.GetColumn(3), Quaternion.Euler(-Lean, 0f, 0f), m.lossyScale);
                log.Add("tilemap " + map.name);
            }
            if (Skip.Contains(root.name) || root.GetComponentInChildren<Light>() != null) continue;
            LeanGroup(root.transform, log);
        }

        var cam = Camera.main;
        if (cam != null) cam.transform.rotation = lean; // the angle CameraRig gives it at runtime

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Built " + Target + ": " + string.Join(", ", log);
    }

    static void LeanGroup(Transform t, List<string> log)
    {
        var sprites = new List<SpriteRenderer>();
        foreach (var r in t.GetComponentsInChildren<SpriteRenderer>(true))
            if (Standing(r.transform)) sprites.Add(r);
        if (sprites.Count == 0) return;

        // One picture if all its parts are at about the same depth (a little apart only to draw in the right order,
        // like the Ferris wheel in front of its base); otherwise each part leans on its own. It leans about the
        // bottom of its lowest part, where it stands on the floor.
        float minZ = float.MaxValue, maxZ = float.MinValue, minY = float.MaxValue, footZ = 0f;
        foreach (var r in sprites)
        {
            minZ = Mathf.Min(minZ, r.transform.position.z);
            maxZ = Mathf.Max(maxZ, r.transform.position.z);
            if (r.bounds.min.y < minY)
            {
                minY = r.bounds.min.y;
                footZ = r.transform.position.z;
            }
        }
        if (maxZ - minZ > OnePicture && t.childCount > 0)
        {
            var kids = new List<Transform>();
            foreach (Transform k in t) kids.Add(k);
            if (t.TryGetComponent(out SpriteRenderer own) && Standing(t))
            {
                // Its own picture leans without carrying its children along: they lean on their own below.
                foreach (var k in kids) k.SetParent(null, true);
                Lean1(t, own.bounds.min.y, t.position.z, log);
                foreach (var k in kids) k.SetParent(t, true);
            }
            foreach (var k in kids) LeanGroup(k, log);
            return;
        }
        Lean1(t, minY, footZ, log);
    }

    // Lean `t` back about its feet: the bottom of its picture, where it meets the floor. Its colliders go upright first.
    static void Lean1(Transform t, float bottom, float depth, List<string> log)
    {
        var boxes = new List<(BoxCollider box, Vector3 centre, Vector3 size, bool trigger)>();
        foreach (var b in t.GetComponentsInChildren<BoxCollider>(true))
        {
            if (b.transform.name == UprightCollider) continue;
            boxes.Add((b, b.transform.TransformPoint(b.center), Vector3.Scale(b.size, b.transform.lossyScale), b.isTrigger));
        }

        t.RotateAround(new Vector3(t.position.x, bottom, depth), Vector3.right, Lean);

        foreach (var (box, centre, size, trigger) in boxes)
        {
            var holder = new GameObject(UprightCollider).transform;
            holder.SetParent(box.transform, false);
            holder.SetPositionAndRotation(centre, Quaternion.identity);
            Vector3 s = holder.lossyScale;
            var upright = holder.gameObject.AddComponent<BoxCollider>();
            upright.isTrigger = trigger;
            upright.center = Vector3.zero;
            upright.size = new Vector3(size.x / s.x, size.y / s.y, size.z / s.z);
            Object.DestroyImmediate(box);
        }
        log.Add(t.name);
    }

    // A picture standing up, not one lying on the floor.
    static bool Standing(Transform t) => Mathf.Abs(Vector3.Dot(t.up, Vector3.up)) > 0.5f;
}
