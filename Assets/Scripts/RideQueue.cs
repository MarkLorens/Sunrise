using System.Collections.Generic;
using UnityEngine;

// A line of visitors waiting for a ride. The first slot is at this object's position and the line
// runs along `direction`. While the ride runs normally and its operator is at the post,
// the person at the front boards every `boardInterval` seconds and comes back out at `exit` later.
public class RideQueue : MonoBehaviour
{
    public static readonly List<RideQueue> All = new();

    public FerrisWheel ride;
    public RideOperator rideOperator;
    public Transform exit;
    public Vector3 direction = new(0.25f, 0f, -1f);
    public float spacing = 0.9f;
    public int capacity = 8;
    public float boardInterval = 5f;
    public float rideTime = 20f;

    readonly List<Visitor> line = new();
    readonly List<(Visitor who, float until)> riders = new();
    float nextBoard;

    public bool HasRoom => line.Count < capacity;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public bool Join(Visitor v)
    {
        if (!HasRoom || ride.IsBroken || line.Contains(v)) return false;
        line.Add(v);
        return true;
    }

    public void Leave(Visitor v) => line.Remove(v);

    public Vector3 SlotPosition(Visitor v) => Slot(Mathf.Max(0, line.IndexOf(v)));

    Vector3 Slot(int i) => transform.position + direction.normalized * spacing * i;

    void Update()
    {
        for (int i = riders.Count - 1; i >= 0; i--)
        {
            if (Time.time < riders[i].until) continue;
            riders[i].who.EndRide(exit.position);
            riders.RemoveAt(i);
        }

        // Nobody waits in line for a wrecked ride.
        if (ride.IsBroken)
        {
            foreach (var v in line.ToArray()) v.Panic(ride.transform.position, 6f);
            return;
        }

        bool boarding = ride.IsRunningNormally && rideOperator.AtPost;
        if (!boarding || line.Count == 0 || Time.time < nextBoard) return;

        var front = line[0];
        Vector3 d = front.transform.position - Slot(0);
        d.y = 0;
        if (d.magnitude > 0.5f) return; // still walking up

        line.RemoveAt(0);
        front.StartRide();
        riders.Add((front, Time.time + rideTime));
        nextBoard = Time.time + boardInterval;
    }

    // Everyone in a cabin tumbles out at once (the wheel broke loose or jammed). Some don't survive the fall.
    public void ThrowOutRiders(Vector3 around, float spread, float deathChance)
    {
        foreach (var (who, _) in riders)
        {
            Vector2 r = Random.insideUnitCircle * spread;
            who.EndRide(around + new Vector3(r.x, 0, r.y));
            if (Random.value < deathChance) who.Die();
        }
        riders.Clear();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        for (int i = 0; i < capacity; i++) Gizmos.DrawWireSphere(Slot(i), 0.25f);
    }
}
