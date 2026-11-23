using System.Collections.Generic;
using UnityEngine;

// Once enough has gone wrong (Unease from startAt), visitors gather in front of the information booth to complain,
// more of them the more uneasy the park (minCrowd at startAt up to maxCrowd at 1). They come one at a time, the nearest
// free visitor first, each to a random spot round the booth, not too close to anyone else there, the ring widening as
// the crowd grows (a mob, not a queue); now and then one of them says something
// (GameText.Complaints, harsher from angryAt). VisitorSpawner sets it up on the booth with the announcer.
public class BoothCrowd : MonoBehaviour
{
    public SpriteRenderer booth;               // the booth's picture: the crowd stands in front of it
    [Range(0f, 1f)] public float startAt = 0.25f;
    [Range(0f, 1f)] public float angryAt = 0.6f;
    public int minCrowd = 2, maxCrowd = 14;
    public Vector2 joinEvery = new(3f, 6f);     // seconds between newcomers
    public Vector2 complainEvery = new(3f, 6f); // seconds between complaints
    public float giveUpAfter = 40f;             // seconds to get there before someone's let go (stuck on the way)

    public float ringGap = 0.9f;  // how far out from the booth's edge the first of them stand
    public float spread = 0.25f;  // how much further out each newcomer may end up
    public float elbowRoom = 0.7f; // never nearer than this to anyone else in the crowd

    readonly List<(Visitor who, Vector3 spot, float since)> crowd = new();
    float nextJoin, nextComplaint;
    string lastLine;

    void Update()
    {
        if (booth == null) return;
        float unease = Unease.Level;
        if (unease < startAt) return;
        if (Unease.Mayhem) // past complaining: everyone's running (Visitor)
        {
            foreach (var m in crowd) if (m.who != null) m.who.StopProtesting();
            crowd.Clear();
            return;
        }

        crowd.RemoveAll(m => m.who == null || m.who.CarriedOff || m.who.Current is Visitor.State.Down or Visitor.State.Riding
                             || !m.who.AtProtest && Time.time - m.since > giveUpAfter && Stop(m.who));
        int wanted = Mathf.RoundToInt(Mathf.Lerp(minCrowd, maxCrowd, Mathf.InverseLerp(startAt, 1f, unease)));
        if (crowd.Count < wanted && Time.time >= nextJoin)
        {
            nextJoin = Time.time + Random.Range(joinEvery.x, joinEvery.y);
            Recruit();
        }
        if (Time.time >= nextComplaint)
        {
            nextComplaint = Time.time + Random.Range(complainEvery.x, complainEvery.y);
            Complain(unease >= angryAt);
        }
    }

    static bool Stop(Visitor v)
    {
        v.StopProtesting();
        return true;
    }

    // The nearest visitor going about their day, to the first free spot they can stand on.
    void Recruit()
    {
        Vector3 front = new(booth.bounds.center.x, 0f, booth.bounds.min.z);
        Visitor best = null;
        float bestDistance = float.MaxValue;
        foreach (var v in Visitor.All)
        {
            if (v.brain != null || v.Group != null || v.Protesting || v.Boarding || !v.CanBePossessed
                || v.Current is not (Visitor.State.Idle or Visitor.State.Wander)) continue;
            float d = (v.transform.position - front).sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = v; }
        }
        if (best == null) return;
        var walker = best.GetComponent<GroundWalker>();
        Bounds b = booth.bounds;
        Vector3 middle = new(b.center.x, 0f, b.min.z);
        float reach = b.extents.x + ringGap + crowd.Count * spread; // round the booth, further out the bigger the crowd
        for (int i = 0; i < 20; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(b.extents.x + ringGap * 0.6f, reach); // anywhere from up close to the crowd's edge
            Vector3 spot = middle + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r * 0.7f); // flatter front to back, like the booth
            if (crowd.Exists(m => (m.spot - spot).sqrMagnitude < elbowRoom * elbowRoom) || !walker.IsWalkable(spot)) continue;
            best.Protest(spot, middle);
            crowd.Add((best, spot, Time.time));
            return;
        }
    }

    void Complain(bool angry)
    {
        var there = crowd.FindAll(m => m.who.AtProtest);
        if (there.Count == 0) return;
        var lines = new List<string>(GameText.Complaints);
        if (angry) lines.AddRange(GameText.AngryComplaints);
        if (lines.Count > 1) lines.Remove(lastLine);
        lastLine = lines[Random.Range(0, lines.Count)];
        var who = there[Random.Range(0, there.Count)].who;
        GameHud.Hush(who.transform);
        GameHud.Shout(who.transform, lastLine, 2.5f, true);
    }
}
