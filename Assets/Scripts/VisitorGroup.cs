using System.Collections.Generic;
using UnityEngine;

// A few visitors who came to the park together (2-4, made by VisitorSpawner). The first is the leader: it goes about
// its day like any visitor, except that groups never queue for rides or go to listen to the speakers. The rest follow
// it (Visitor.Follow), each in its own slot beside or behind it, stopping when it stops and facing the way it faces.
// Anyone possessed, killed or taken over by a cutscene drops out; if the leader goes, the next one leads, and a group
// of one is just a visitor again.
public class VisitorGroup
{
    const float Side = 0.9f;  // how far beside the leader the first two walk
    const float Back = 0.35f; // and how far behind

    readonly List<Visitor> members = new();

    public Visitor Leader => members.Count > 0 ? members[0] : null;

    public void Add(Visitor v)
    {
        members.Add(v);
        v.Group = this;
    }

    public void Leave(Visitor v)
    {
        if (!members.Remove(v)) return;
        v.Group = null;
        if (members.Count == 1) Leave(members[0]);
    }

    // Where this follower walks: left of the leader, right of it, then behind it, turning as the leader turns.
    public Vector3 Slot(Visitor v)
    {
        var lead = Leader;
        Vector3 f = lead.Facing;
        Vector3 side = new(f.z, 0f, -f.x);
        Vector3 offset = members.IndexOf(v) switch
        {
            1 => side * Side - f * Back,
            2 => -side * Side - f * Back,
            _ => -f * (Back + 0.8f),
        };
        return lead.transform.position + offset;
    }
}
