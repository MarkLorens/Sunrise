using UnityEngine;

// The sound guy who looks after the speaker. He stands by its broken cable scratching his head: the only one who
// knows how to handle a live wire (LiveCable). His Visitor body can be possessed like anyone's (this script is
// switched off while it is); on release he wanders back to his spot.
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public class CableMan : MonoBehaviour
{
    GroundWalker walker;
    CharacterSprite look;
    Vector3 post;

    void Awake()
    {
        walker = GetComponent<GroundWalker>();
        look = GetComponent<CharacterSprite>();
        if (!TryGetComponent(out Visitor body)) body = gameObject.AddComponent<Visitor>(); // loads the sprite
        body.brain = this;
        walker.speed = 2f;
        post = transform.position;
    }

    void Update() => look.pose = walker.WalkTo(post) ? null : look.think;
}
