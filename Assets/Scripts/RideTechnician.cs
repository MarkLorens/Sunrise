using System.Collections.Generic;
using UnityEngine;

// The ride technician: on call for the Ferris wheel, actually fast asleep. Ring him (the operator does when the
// wheel loses power) and all you get is a snore.
// ponytail: can't be woken or possessed yet; that's the next part of the sequence.
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public class RideTechnician : MonoBehaviour
{
    public static readonly List<RideTechnician> All = new();

    CharacterSprite look;
    Transform zzz;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Awake()
    {
        look = GetComponent<CharacterSprite>();
        if (!TryGetComponent(out Visitor body)) body = gameObject.AddComponent<Visitor>(); // loads the sprite
        body.brain = this;
        body.possessable = false;
        zzz = Snore();
    }

    void Update()
    {
        // Dozing on his feet: fallDown is a mid-air tumble (and what the dead lie in), so it read as a corpse.
        look.pose = look.idle;
        zzz.localPosition = new Vector3(0.25f, look.idle.bounds.size.y - 0.25f + Mathf.Sin(Time.time * 2f) * 0.08f, -0.03f);
        zzz.localScale = new Vector3(Mathf.Sign(transform.localScale.x), 1f, 1f); // stay readable when he faces left
    }

    // ponytail: built-in TextMesh, swap for a Bubbles/sleep sprite once there's art for one.
    Transform Snore()
    {
        var go = new GameObject("Zzz");
        go.transform.SetParent(transform, false);
        var text = go.AddComponent<TextMesh>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = "Zzz";
        text.fontSize = 48;
        text.characterSize = 0.06f;
        text.fontStyle = FontStyle.Bold;
        text.anchor = TextAnchor.LowerLeft;
        text.color = new Color(0.75f, 0.85f, 1f);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = text.font.material;
        r.sortingOrder = ParkArt.OverheadOrder;
        return go.transform;
    }

    public void Ring()
    {
        // Asleep: he doesn't pick up.
    }
}
