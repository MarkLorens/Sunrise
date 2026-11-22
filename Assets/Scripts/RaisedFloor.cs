using System.Collections.Generic;
using UnityEngine;

// A floor people walk up onto (the Ferris wheel's platform, stairs and all). On load its mesh is sampled once into a
// height map and the temporary collider thrown away, so it never shoves anyone or counts as an obstacle. GroundWalker
// stands people at Height and won't let them step up or down more than a stair (GroundWalker.MaxStep): the stairs are
// the way up, the sheer sides aren't. Everywhere off it the floor is at 0.
[DefaultExecutionOrder(-100)] // mapped before anyone wakes up and asks how high they're standing
[RequireComponent(typeof(MeshFilter))]
public class RaisedFloor : MonoBehaviour
{
    const float Cell = 0.25f;
    static readonly List<RaisedFloor> All = new();

    Rect area;
    int width, depth;
    float[] heights;

    void Awake()
    {
        var col = gameObject.AddComponent<MeshCollider>();
        col.sharedMesh = GetComponent<MeshFilter>().sharedMesh;
        Physics.SyncTransforms();
        Bounds b = col.bounds;
        area = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
        width = Mathf.CeilToInt(area.width / Cell);
        depth = Mathf.CeilToInt(area.height / Cell);
        heights = new float[width * depth];
        float top = b.max.y + 1f;
        for (int z = 0; z < depth; z++)
            for (int x = 0; x < width; x++)
            {
                var ray = new Ray(new Vector3(area.xMin + (x + 0.5f) * Cell, top, area.yMin + (z + 0.5f) * Cell), Vector3.down);
                heights[z * width + x] = col.Raycast(ray, out RaycastHit hit, top - b.min.y + 1f) ? Mathf.Max(0f, hit.point.y) : 0f;
            }
        DestroyImmediate(col); // gone before GroundWalker collects the scene's obstacles
        All.Add(this);
        NavGrid.Invalidate();
    }

    void OnDestroy()
    {
        All.Remove(this);
        NavGrid.Invalidate();
    }

    // How high the floor is here.
    public static float Height(Vector3 p)
    {
        float y = 0f;
        foreach (var f in All)
        {
            int x = Mathf.FloorToInt((p.x - f.area.xMin) / Cell), z = Mathf.FloorToInt((p.z - f.area.yMin) / Cell);
            if (x >= 0 && z >= 0 && x < f.width && z < f.depth) y = Mathf.Max(y, f.heights[z * f.width + x]);
        }
        return y;
    }
}
