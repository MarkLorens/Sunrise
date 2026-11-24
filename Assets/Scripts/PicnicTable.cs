using UnityEngine;

// A picnic table in the park (Props/New/picnic_table, standing at its feet, the front edge of its picture): nobody
// walks through it (its footprint is a solid box, as the stalls'), and visitors drop by and hang out at it a while: a
// seat each side, two in front and two behind (a StandQueue in seating mode, so they come and go like a stand's line:
// Visitor.JoinStand), each staying StandQueue.stayTime before they go on with their day. Placed in the scene.
public class PicnicTable : MonoBehaviour
{
    public float depth = 1.3f; // how far back from its feet its top reaches (its footprint, front to back)

    void Start()
    {
        var spawner = FindAnyObjectByType<VisitorSpawner>();
        var art = GetComponentInChildren<SpriteRenderer>();
        if (spawner == null || art == null) return;
        float half = art.bounds.extents.x;
        Vector3 feet = transform.position;
        Vector3 middle = feet + Vector3.forward * depth * 0.5f;
        var seats = new[]
        {
            feet + new Vector3(-half * 0.45f, 0f, -0.55f), feet + new Vector3(half * 0.45f, 0f, -0.55f),           // in front
            feet + new Vector3(-half * 0.45f, 0f, depth + 0.55f), feet + new Vector3(half * 0.45f, 0f, depth + 0.55f), // behind
        };
        StandQueue.Seating("Picnic Table Seats", transform, seats, middle, spawner.ground);
    }
}
