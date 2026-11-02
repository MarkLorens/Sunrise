using System.Collections.Generic;
using UnityEngine;

// The Ferris wheel's closed area: the marked-off ground at the foot of the wheel with the support hinge and the gear
// box. Staff come and go as they like. Visitors never wander in, and while staff on duty are nearby (not possessed,
// not off breaking up a fight) a possessed visitor can't either: they're stopped at the line, and one caught inside
// is sent back out. With nobody watching, a possessed visitor can sneak in.
public class StaffArea : MonoBehaviour
{
    public static readonly List<StaffArea> All = new();

    public Vector2 size = new(5.4f, 3.4f); // x by z, centred on this object
    public float watchRadius = 6f;          // staff this close to the middle keep an eye on it

    float nextWarning;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public bool Contains(Vector3 p)
    {
        Vector3 d = p - transform.position;
        return Mathf.Abs(d.x) <= size.x * 0.5f && Mathf.Abs(d.z) <= size.y * 0.5f;
    }

    // The staff member on duty keeping an eye on it, if any. Breaking up a fight, they have their hands full.
    public Component Watcher()
    {
        foreach (var o in RideOperator.All)
            if (!o.BreakingUpFight && Near(o)) return o;
        foreach (var m in Mechanic.All)
            if (!m.BreakingUpFight && Near(m)) return m;
        return null;
    }

    bool Near(Component c)
    {
        Vector3 d = c.transform.position - transform.position;
        d.y = 0;
        return d.magnitude < watchRadius;
    }

    // Can this person step from `from` to `to`? Only visitors are kept out; staff and the spirit go where they like.
    public static bool Allows(Visitor v, Vector3 from, Vector3 to)
    {
        if (v == null || v.brain != null) return true;
        foreach (var a in All)
        {
            if (!a.Contains(to) || a.Contains(from)) continue;
            if (v.Current != Visitor.State.Possessed) return false;
            var watcher = a.Watcher();
            if (watcher == null) continue;
            a.Warn(watcher, "Hey! Staff only back there!");
            return false;
        }
        return true;
    }

    void Warn(Component watcher, string line)
    {
        if (Time.time < nextWarning) return;
        nextWarning = Time.time + 3f;
        GameHud.Say($"{watcher.name}: {line}");
        if (watcher.TryGetComponent(out CharacterSprite look))
        {
            look.Face(transform.position);
            look.Show("exclaim", 1.5f);
        }
    }

    // A possessed visitor caught inside when staff come back: out they go, to the front of the area.
    void Update()
    {
        var spirit = Possession.Instance;
        var host = spirit != null ? spirit.Host : null;
        if (host == null || host.brain != null || !Contains(host.transform.position)) return;
        var watcher = Watcher();
        if (watcher == null) return;
        nextWarning = 0f;
        Warn(watcher, "Hey! What are you doing back there? Out!");
        Vector3 p = host.transform.position;
        p.z = transform.position.z - size.y * 0.5f - 0.5f;
        var rb = host.GetComponent<Rigidbody>();
        rb.position = host.transform.position = p;
    }
}
