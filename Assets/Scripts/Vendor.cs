using UnityEngine;

// A stand's seller (made by VisitorSpawner from the stand tiles). They keep to their spot and face the front.
// Inside a stand (the ice cream sellers, between its front tile and back layer) they're out of reach: they can't be
// possessed while in there, and they never leave. Beside one (the balloon sellers) they're possessable like anyone;
// this script is switched off while they are, and on release they walk back to their spot.
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public class Vendor : MonoBehaviour
{
    public bool inside;

    GroundWalker walker;
    CharacterSprite look;
    Vector3 spot;

    void Awake()
    {
        walker = GetComponent<GroundWalker>();
        look = GetComponent<CharacterSprite>();
        if (!TryGetComponent(out Visitor body)) body = gameObject.AddComponent<Visitor>(); // loads the sprite
        body.brain = this;
        walker.speed = 2f;
        spot = transform.position;
    }

    void Start()
    {
        if (inside) GetComponent<Visitor>().possessable = false;
    }

    void Update()
    {
        if (walker.WalkTo(spot)) return;
        look.Face(spot + Vector3.back);
    }
}
