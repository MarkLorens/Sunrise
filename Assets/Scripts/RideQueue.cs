using System.Collections.Generic;
using UnityEngine;

// A line of visitors waiting for a ride. The first slot is at this object's position and the line
// runs along `direction`. While the ride has power and its operator is at the post, the person at the front
// boards every `boardInterval` seconds: up the platform to `boardPoint` behind the wheel, where they get on and
// disappear, and come back out at `exit` rideTime later.
// Only visitors going about their day ride; the spirit can't (a possessed visitor leaves the line).
public class RideQueue : MonoBehaviour, IVisitorLine
{
    public static readonly List<RideQueue> All = new();

    public FerrisWheel ride;
    public RideOperator rideOperator;
    public Transform exit;
    public Transform boardPoint; // where riders walk to and get on (left empty: they get on straight from the line)
    public Vector3 direction = new(0.25f, 0f, -1f);
    public float spacing = 0.9f;
    public int capacity = 8;
    public float boardInterval = 5f;
    public float rideTime = 20f;

    readonly List<Visitor> line = new();
    readonly List<(Visitor who, float until)> riders = new();
    readonly List<Visitor> boarding = new(); // on their way up to boardPoint
    float nextBoard;

    public bool HasRoom => line.Count < capacity;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public bool Join(Visitor v)
    {
        if (!HasRoom || line.Contains(v)) return false;
        line.Add(v);
        return true;
    }

    public void Leave(Visitor v) => line.Remove(v);

    public Vector3 SlotPosition(Visitor v) => Slot(Mathf.Max(0, line.IndexOf(v)));

    public bool Faces(out Vector3 point)
    {
        point = default;
        return false; // they look whichever way they walked up
    }

    // The wheel tore off its hinge (FerrisWheel): everyone riding is flung out around `around`. They don't make it.
    public void ThrowOutRiders(Vector3 around, float spread)
    {
        foreach (var (who, _) in riders)
        {
            Vector2 r = Random.insideUnitCircle * spread;
            who.EndRide(around + new Vector3(r.x, 0f, r.y));
            who.Die();
        }
        riders.Clear();
    }

    // Walking up to get on: once there, into a cabin. Anyone scared off on the way (Visitor.SetState) isn't coming.
    void UpdateBoarding()
    {
        for (int i = boarding.Count - 1; i >= 0; i--)
        {
            var v = boarding[i];
            if (v == null || !v.Boarding) { boarding.RemoveAt(i); continue; }
            if (!v.AtBoardingSpot) continue;
            boarding.RemoveAt(i);
            v.StartRide();
            riders.Add((v, Time.time + rideTime));
        }
    }

    Vector3 Slot(int i) => transform.position + direction.normalized * spacing * i;

    void Update()
    {
        UpdateBoarding();
        // No power: riders are stuck up in their cabins and the line waits for it to come back.
        if (ride.Current != FerrisWheel.State.Running) return; // and nobody gets on a wheel at max speed

        for (int i = riders.Count - 1; i >= 0; i--)
        {
            if (Time.time < riders[i].until) continue;
            riders[i].who.EndRide(exit.position);
            riders.RemoveAt(i);
        }

        if (!rideOperator.AtPost || line.Count == 0 || Time.time < nextBoard) return;

        var front = line[0];
        Vector3 d = front.transform.position - Slot(0);
        d.y = 0;
        if (d.magnitude > 0.5f) return; // still walking up

        line.RemoveAt(0);
        if (boardPoint != null)
        {
            front.Board(boardPoint.position);
            boarding.Add(front);
        }
        else
        {
            front.StartRide();
            riders.Add((front, Time.time + rideTime));
        }
        nextBoard = Time.time + boardInterval;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        for (int i = 0; i < capacity; i++) Gizmos.DrawWireSphere(Slot(i), 0.25f);
        if (boardPoint != null) Gizmos.DrawWireCube(boardPoint.position, Vector3.one * 0.5f);
    }
}
