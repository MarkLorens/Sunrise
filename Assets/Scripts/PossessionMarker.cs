using UnityEngine;

// Football-game style indicators for whoever you control:
//   1. a triangle bobbing over their head (with the grip bar: time left in this body),
//   2. a power bar while you charge a throw,
//   3. a target ring where the throw will land (red when it will connect),
//   4. a ring under their feet and a direction arrow on the ground.
// Plus a ring under whoever you could possess (grey if they resist), a small ring under what you'd grab,
// a pulse when you shout, and a ring showing how far the weak spirit can drift. GameHud adds this to the spirit.
[RequireComponent(typeof(Possession))]
public class PossessionMarker : MonoBehaviour
{
    static readonly Color Cyan = new(0.25f, 0.95f, 1f);
    static readonly Color Purple = new(0.75f, 0.45f, 1f);
    static readonly Color Grey = new(0.6f, 0.6f, 0.65f);

    Possession spirit;
    Transform triangleHolder;
    SpriteRenderer triangle, feetRing, arrow, targetRing, candidateRing, grabRing, pulseRing, leashRing;
    WorldBar powerBar, gripBar;

    void Start()
    {
        spirit = GetComponent<Possession>();
        var root = new GameObject("Possession Markers").transform;

        triangleHolder = new GameObject("Triangle").transform;
        triangleHolder.SetParent(root, false);
        triangle = ParkArt.Make("Quad", triangleHolder, "Markers/triangle", 0.4f, Cyan, ParkArt.OverheadOrder);
        powerBar = WorldBar.Create(triangleHolder, new Vector3(0f, -0.4f, 0f), 0.9f);
        gripBar = WorldBar.Create(triangleHolder, new Vector3(0f, 0.36f, 0f), 0.7f);

        feetRing = Flat("Feet Ring", root, "Markers/ring", Cyan);
        arrow = Flat("Arrow", root, "Markers/arrow", Cyan);
        targetRing = Flat("Target Ring", root, "Markers/ring", Color.white);
        candidateRing = Flat("Candidate Ring", root, "Markers/ring", Purple);
        grabRing = Flat("Grab Ring", root, "Markers/ring", Color.white);
        pulseRing = Flat("Shout Pulse", root, "Markers/ring", Color.white);
        leashRing = Flat("Leash Ring", root, "Markers/ring", Purple);
    }

    static SpriteRenderer Flat(string name, Transform parent, string sprite, Color color) =>
        ParkArt.Make(name, parent, sprite, 1f, color, ParkArt.DecalOrder + 1, true);

    void LateUpdate()
    {
        float t = Time.time;
        var host = spirit.Host;
        bool possessing = spirit.State == Possession.Mode.Possessing && host != null;
        bool floating = spirit.State == Possession.Mode.Spirit;

        // 1. triangle over whoever you control
        Vector3 head = possessing ? host.HeadPoint + Vector3.up * 0.75f : transform.position + Vector3.up * 1.6f;
        triangleHolder.position = head + Vector3.up * Mathf.Sin(t * 4f) * 0.08f;
        triangle.enabled = floating || possessing;
        triangle.color = possessing ? Cyan : Purple;

        // grip: how long you can stay in this body (blinks when nearly out)
        float grip = spirit.TimeLeft / spirit.duration;
        bool blink = grip < 0.25f && Mathf.Repeat(t * 4f, 1f) < 0.5f;
        gripBar.Set(grip, blink ? Color.white : Color.Lerp(new Color(1f, 0.3f, 0.3f), Purple, grip * 1.5f), possessing);

        // 2. power bar while charging a throw
        powerBar.Set(spirit.Charge, WorldBar.Heat(spirit.Charge), possessing && spirit.Charging);

        // how far the spirit can drift from its last body
        leashRing.enabled = floating && spirit.leashRadius > 0f;
        if (leashRing.enabled)
        {
            // Faint until you drift close to the edge.
            float reach = Flat(transform.position - spirit.LeashCenter).magnitude / spirit.leashRadius;
            leashRing.color = new Color(Purple.r, Purple.g, Purple.b, Mathf.Lerp(0.05f, 0.55f, Mathf.InverseLerp(0.5f, 1f, reach)));
            Place(leashRing, spirit.LeashCenter, -t * 10f, spirit.leashRadius * 2.1f);
        }

        // 4. ring under your feet and the facing arrow
        feetRing.enabled = arrow.enabled = possessing;
        if (possessing)
        {
            Vector3 feet = host.transform.position;
            Place(feetRing, feet, t * 40f, 1.1f + Mathf.Sin(t * 5f) * 0.04f);
            Vector3 dir = spirit.Charging ? spirit.AimDirection : host.Facing;
            var arrowHolder = arrow.transform.parent;
            arrowHolder.position = new Vector3(feet.x, 0.04f, feet.z) + dir * 1.05f;
            arrowHolder.rotation = Quaternion.LookRotation(dir, Vector3.up);
            arrowHolder.localScale = Vector3.one * 0.6f;
        }

        // 3. target ring where the throw lands: snaps onto the target and turns red when it will connect
        targetRing.enabled = possessing && spirit.Charging && spirit.Charge > 0f;
        if (targetRing.enabled)
        {
            bool locked = spirit.AimLocked;
            targetRing.color = locked ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.95f, 0.6f);
            Place(targetRing, spirit.PredictLanding(), -t * 90f, (locked ? 1.4f : 1f) + Mathf.Sin(t * 8f) * 0.08f);
        }

        // ring under whoever you could possess (grey if they resist)
        var candidate = floating ? spirit.Candidate : null;
        candidateRing.enabled = candidate != null;
        if (candidate != null)
        {
            candidateRing.color = spirit.CandidateBlocked == null ? Purple : Grey;
            Place(candidateRing, candidate.position, t * 60f, 1.2f + Mathf.Sin(t * 6f) * 0.08f);
        }

        // small ring under the thing you'd grab
        var grab = possessing ? spirit.GrabCandidate : null;
        grabRing.enabled = grab != null;
        if (grab != null) Place(grabRing, grab.Holder != null ? grab.Holder.transform.position : grab.transform.position, t * 80f, 0.8f);

        // expanding pulse when shouting
        float k = (t - spirit.ShoutedAt) / 0.45f;
        pulseRing.enabled = possessing && k < 1f;
        if (pulseRing.enabled)
        {
            Place(pulseRing, host.transform.position, 0f, Mathf.Lerp(0.5f, spirit.shoutRadius * 2f, k));
            pulseRing.color = new Color(1f, 1f, 1f, 1f - k);
        }
    }

    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

    static void Place(SpriteRenderer ring, Vector3 position, float spin, float scale)
    {
        var holder = ring.transform.parent;
        holder.SetPositionAndRotation(new Vector3(position.x, 0.04f, position.z), Quaternion.Euler(0f, spin, 0f));
        holder.localScale = Vector3.one * scale;
    }
}
