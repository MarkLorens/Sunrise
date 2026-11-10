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

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Awake()
    {
        look = GetComponent<CharacterSprite>();
        if (!TryGetComponent(out Visitor body)) body = gameObject.AddComponent<Visitor>(); // loads the sprite
        body.brain = this;
        body.possessable = false;
    }

    void Update() => look.pose = look.fallDown;

    public void Ring()
    {
        look.Show("question", 3f);
        GameHud.Say($"{name}: Zzz... *snore*... five more minutes...");
    }
}
