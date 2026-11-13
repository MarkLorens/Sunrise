using System.Collections.Generic;
using UnityEngine;

// A loudspeaker playing music. Visitors like to stand around in front of it for a while (Visitor picks it as
// somewhere to go until the crowd is big enough). The one with livePlug on has a broken, live plug sticking out of
// it (LiveCable), sparking.
public class Speaker : MonoBehaviour
{
    public static readonly List<Speaker> All = new();

    public Vector3 crowdOffset = new(0f, 0f, -1.6f); // where the crowd stands, relative to the speaker
    public float crowdRadius = 1.6f;
    public int crowdSize = 6;
    public bool livePlug = true; // its plug is the broken, live one (LiveCable)
    public Vector3 cableEndOffset = new(0.8f, 0.83f, -0.12f); // the plug in the sprite, relative to the speaker
    public DrinkingFountain fountain; // the puddle the cable can reach

    public Vector3 CrowdCenter => new(transform.position.x + crowdOffset.x, 0f, transform.position.z + crowdOffset.z);

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
        if (!livePlug) return;
        var end = new GameObject("Live Cable");
        end.SetActive(false); // so Interactable's Awake finds the rules component
        end.transform.position = transform.position + cableEndOffset;
        var cable = end.AddComponent<LiveCable>();
        cable.speaker = this;
        cable.fountain = fountain;
        end.AddComponent<Interactable>().range = 1.6f;
        end.SetActive(true);
    }

    // A speaker whose crowd isn't full yet, if any.
    public static Speaker Wanting()
    {
        foreach (var s in All)
            if (s.Listeners() < s.crowdSize) return s;
        return null;
    }

    // The speaker this spot is in the crowd of, if any.
    public static Speaker Near(Vector3 p)
    {
        foreach (var s in All)
            if (s.InCrowd(p)) return s;
        return null;
    }

    // A random spot in the crowd that isn't inside a lamp, bench or prop.
    public Vector3 ListenSpot()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector2 r = Random.insideUnitCircle * crowdRadius;
            Vector3 p = CrowdCenter + new Vector3(r.x, 0f, r.y);
            if (!GroundWalker.NearObstacle(p, 0.35f)) return p;
        }
        return CrowdCenter;
    }

    bool InCrowd(Vector3 p)
    {
        Vector3 d = p - CrowdCenter;
        d.y = 0f;
        return d.magnitude <= crowdRadius + 0.5f;
    }

    int Listeners()
    {
        int n = 0;
        foreach (var v in Visitor.All)
            if (v.brain == null && v.Current is Visitor.State.Idle or Visitor.State.Wander && InCrowd(v.transform.position)) n++;
        return n;
    }
}
