using UnityEngine;

// A stand's seller (made by VisitorSpawner from the stand tiles). They keep to their spot and face the front.
// Inside a stand (the ice cream sellers, between its front tile and back layer) they're out of reach: they can't be
// possessed while in there, and they never leave, pottering from side to side behind the counter (paceHalfWidth) in
// the stall's solid box (GroundWalker.Block, ignoreObstacles). Beside one (the balloon sellers) they're possessable like anyone;
// this script is switched off while they are, and on release they walk back to their spot.
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public class Vendor : MonoBehaviour
{
    public bool inside;
    public float paceHalfWidth; // inside: how far either side of their spot they wander behind the counter

    GroundWalker walker;
    CharacterSprite look;
    Vector3 spot, pacingTo;
    float nextPace;

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
        if (!inside) return;
        GetComponent<Visitor>().possessable = false;
        walker.ignoreObstacles = true;
        walker.speed = 1.2f; // an unhurried shuffle
        pacingTo = spot;
    }

    void Update()
    {
        if (inside)
        {
            Pace();
            return;
        }
        if (walker.WalkTo(spot)) return;
        look.Face(spot + Vector3.back);
    }

    // Over to a spot along the counter, a pause there, then another (straight along it: nothing's in the way inside).
    void Pace()
    {
        Vector3 to = pacingTo - transform.position;
        to.y = 0f;
        if (to.magnitude > 0.08f)
        {
            walker.move = new Vector2(to.x, to.z).normalized;
            return;
        }
        walker.move = Vector2.zero;
        if (Time.time < nextPace) return;
        nextPace = Time.time + Random.Range(2f, 5f);
        pacingTo = spot + Vector3.right * Random.Range(-paceHalfWidth, paceHalfWidth);
    }
}
