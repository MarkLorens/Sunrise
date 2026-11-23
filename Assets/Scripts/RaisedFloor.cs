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
    public const float Cell = 0.25f; // how finely the floor is mapped
    public const float Ground = 0.05f; // floor lower than this is the ground, not the platform
    static readonly List<RaisedFloor> All = new();

    Rect area;
    int width, depth;
    float[] heights;
    bool[] railX, railZ; // a railing between a square and the one to its right (X) / behind it (Z)

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
        Railings();
        All.Add(this);
        NavGrid.Invalidate();
    }

    void OnDestroy()
    {
        All.Remove(this);
        NavGrid.Invalidate();
    }

    // Stairs cut into the deck: their last steps sit only a stair below the deck beside them, so nothing in the heights
    // stops someone stepping off sideways onto the deck halfway up. Railings go along each side of a flight: wherever
    // stairs meet the deck along an edge that runs past more than one step. The top of the flight, where the last step
    // meets the deck along one step only, stays open.
    void Railings()
    {
        float deck = 0f;
        foreach (float h in heights) deck = Mathf.Max(deck, h);
        bool Stair(int x, int z) => x >= 0 && z >= 0 && x < width && z < depth && heights[z * width + x] > Ground && heights[z * width + x] < deck - Ground;
        bool Deck(int x, int z) => x >= 0 && z >= 0 && x < width && z < depth && heights[z * width + x] >= deck - Ground;
        railX = new bool[width * depth];
        railZ = new bool[width * depth];
        for (int z = 0; z < depth; z++)
            for (int x = 0; x < width; x++)
            {
                // Between (x,z) and (x+1,z): the stairs on one side, the deck on the other, the run along z.
                railX[z * width + x] = Side(x, z, 1, 0, 0, 1);
                railZ[z * width + x] = Side(x, z, 0, 1, 1, 0);
            }

        // The edge from (x,z) to (x+dx,z+dz) between stairs and deck, and how much the steps along it (run ux,uz) vary.
        bool Side(int x, int z, int dx, int dz, int ux, int uz)
        {
            bool Edge(int i) => Stair(x + ux * i, z + uz * i) && Deck(x + ux * i + dx, z + uz * i + dz)
                             || Deck(x + ux * i, z + uz * i) && Stair(x + ux * i + dx, z + uz * i + dz);
            float Step(int i) => Stair(x + ux * i, z + uz * i) ? heights[(z + uz * i) * width + x + ux * i] : heights[(z + uz * i + dz) * width + x + ux * i + dx];
            if (!Edge(0)) return false;
            float low = Step(0), high = low;
            for (int i = 1; Edge(i); i++) { low = Mathf.Min(low, Step(i)); high = Mathf.Max(high, Step(i)); }
            for (int i = -1; Edge(i); i--) { low = Mathf.Min(low, Step(i)); high = Mathf.Max(high, Step(i)); }
            return high - low > Ground;
        }
    }

    // Would stepping from a to b (next to each other: one step of someone walking) go through a railing?
    public static bool Railed(Vector3 a, Vector3 b)
    {
        foreach (var f in All)
        {
            int ax = Mathf.FloorToInt((a.x - f.area.xMin) / Cell), az = Mathf.FloorToInt((a.z - f.area.yMin) / Cell);
            int bx = Mathf.FloorToInt((b.x - f.area.xMin) / Cell), bz = Mathf.FloorToInt((b.z - f.area.yMin) / Cell);
            if (ax == bx && az == bz) continue;
            if (Mathf.Min(ax, bx) < 0 || Mathf.Min(az, bz) < 0 || Mathf.Max(ax, bx) >= f.width || Mathf.Max(az, bz) >= f.depth) continue;
            int x = Mathf.Min(ax, bx), z = Mathf.Min(az, bz);
            if (ax != bx && (f.railX[az * f.width + x] || f.railX[bz * f.width + x])) return true;
            if (az != bz && (f.railZ[z * f.width + ax] || f.railZ[z * f.width + bx])) return true;
        }
        return false;
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
