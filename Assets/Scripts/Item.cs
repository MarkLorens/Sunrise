using UnityEngine;

// Something a visitor carries in their hand: a drink, a water bottle, the electrician's keys, the technician's
// screwdriver. Possessing someone, you can use what they hold (the keys open the electrical box and the locker box, the
// screwdriver loosens the wheel's hinge). The keys and the screwdriver aren't drawn at all (Unseen): carried all the
// same. Someone who panics or goes down lets go of
// it, and it tumbles to the ground and stays there: some land standing, some on their side (TipChance), and a drink
// or water bottle on its side leaks, a puddle spreading out from its open end over a few seconds (Leak).
[DefaultExecutionOrder(50)] // after CharacterSprite has picked this frame's pose, so it follows the hand exactly
public class Item : MonoBehaviour
{
    const float Size = 0.5f;     // lying on the ground or falling
    const float HeldSize = 0.3f; // in a hand: about one and a half hands tall

    // How big it is in a hand: HeldSize, give or take for things that read too small at that size.
    float Held => kind switch
    {
        VisitorItem.WaterBottle => HeldSize * 1.3f,
        _ => HeldSize,
    };
    const float HeldTilt = 15f;  // degrees a held thing leans in toward the holder's body
    const float Gravity = 18f;

    public VisitorItem kind;
    public Visitor Holder { get; private set; }
    public string DisplayName => kind switch
    {
        VisitorItem.Drink => "drink",
        VisitorItem.Keys => "keys",
        VisitorItem.WaterBottle => "water bottle",
        VisitorItem.Screwdriver => "screwdriver",
        _ => "thing",
    };

    SpriteRenderer sprite, shadow, spill;

    const float TipChance = 0.5f;                    // of landing on its side rather than standing
    const float SpillTime = 3f;                      // seconds for a spill to spread
    static readonly Vector2 SpillSize = new(0.7f, 1.2f); // how wide a spill ends up, smallest and biggest
    bool Leaks => kind is VisitorItem.Drink or VisitorItem.WaterBottle;
    // What runs out: cola, or water.
    Color SpillColor => kind == VisitorItem.Drink ? new Color(0.42f, 0.2f, 0.1f, 0.6f) : new Color(0.75f, 0.9f, 1f, 0.6f);

    // Carried but never drawn, in a hand or on the ground.
    bool Unseen => kind is VisitorItem.Keys or VisitorItem.Screwdriver;
    Vector3 velocity;
    bool falling;

    public static Item Create(VisitorItem kind, Vector3 position)
    {
        var go = new GameObject(kind.ToString());
        go.transform.position = position;
        var item = go.AddComponent<Item>();
        item.kind = kind;
        item.sprite = ParkArt.Lit(ParkArt.Make("Sprite", go.transform, "Items/" + kind, Size, Color.white, 1));
        if (item.sprite.sprite == null)
        {
            // ponytail: grey block until there's art in Resources/Park/Items/<kind>.png.
            item.sprite.sprite = ParkArt.Get("Markers/white");
            item.sprite.color = new Color(0.55f, 0.6f, 0.68f);
            ParkArt.Fit(item.sprite, Size * 0.6f);
        }
        item.shadow = ParkArt.Make("Shadow", null, "Markers/shadow", 0.3f, Color.white, ParkArt.DecalOrder, true);
        item.shadow.transform.parent.SetParent(go.transform, true);
        return item;
    }

    // Put in someone's hand (spawned with it, or bought at the stall).
    public void PickedUp(Visitor by, bool silent)
    {
        Holder = by;
        falling = false;
        if (!silent) ClosedCaptions.Show(Captions.PickUp);
    }

    // Let go of: it tumbles to the ground.
    public void Dropped(Vector3 at)
    {
        Holder = null;
        transform.position = at;
        velocity = Vector3.up * 2f;
        falling = true;
        ClosedCaptions.Show(Captions.DropItem);
    }

    // Eaten, poured out...
    public void Consume() => Destroy(gameObject);

    void LateUpdate()
    {
        if (Unseen)
        {
            sprite.enabled = shadow.enabled = false;
            return;
        }
        var shadowHolder = shadow.transform.parent;
        if (Holder != null)
        {
            // In the hand, drawn in front of it (and of the whole body), centred on the hand and tipped a little in
            // toward the body, so it reads as held. It swings with the hand on every walk frame (hidden while the
            // holder rides). (Lifting it by 0.3 x its size, to stick up out of the fist, sat it above the hand.)
            float held = Held;
            transform.localScale = Vector3.one * (held / Size);
            var tilt = Quaternion.Euler(0f, 0f, Holder.Look.FacingLeft ? HeldTilt : -HeldTilt);
            transform.SetPositionAndRotation(Holder.CarryPoint - Holder.transform.forward * 0.02f, tilt);
            sprite.enabled = Holder.Look.Visible;
            shadow.enabled = false;
            return;
        }
        transform.localScale = Vector3.one;
        sprite.enabled = shadow.enabled = true;
        shadowHolder.SetPositionAndRotation(new Vector3(transform.position.x, RaisedFloor.Height(transform.position) + 0.02f, transform.position.z), Quaternion.identity);
        if (!falling) return;

        velocity.y -= Gravity * Time.deltaTime;
        transform.position += velocity * Time.deltaTime;
        transform.Rotate(0f, 0f, -velocity.x * 60f * Time.deltaTime);

        if (transform.position.y <= RaisedFloor.Height(transform.position) + Size * 0.5f && velocity.y < 0f) Land();
    }

    void Land()
    {
        velocity = Vector3.zero;
        falling = false;
        float ground = RaisedFloor.Height(transform.position);
        if (Random.value >= TipChance)
        {
            // Standing.
            transform.rotation = Quaternion.identity;
            transform.position = new Vector3(transform.position.x, ground + Size * 0.5f, transform.position.z);
            return;
        }
        // On its side, top to the left or the right, lying as low as it's wide.
        float side = Random.value < 0.5f ? 1f : -1f;
        transform.rotation = Quaternion.identity;
        float halfWide = sprite.bounds.size.x * 0.5f;
        transform.rotation = Quaternion.Euler(0f, 0f, side * 90f);
        transform.position = new Vector3(transform.position.x, ground + halfWide, transform.position.z);
        if (Leaks) StartCoroutine(Leak(-side, ground));
    }

    // A puddle spreading out from its open end (toward `toward` across the screen), easing to a stop.
    System.Collections.IEnumerator Leak(float toward, float ground)
    {
        Vector3 mouth = transform.position + Vector3.right * toward * Size * 0.45f;
        var fountain = FindAnyObjectByType<DrinkingFountain>();
        spill = ParkArt.Make("Spill", null, "Markers/shadow", 0.01f, SpillColor, ParkArt.DecalOrder, true);
        if (fountain != null && fountain.puddleArt != null)
        {
            spill.sprite = fountain.puddleArt; // the artist's puddle, tinted to whatever's running out
            spill.color = kind == VisitorItem.Drink ? new Color(0.75f, 0.45f, 0.3f, 0.85f) : new Color(1f, 1f, 1f, 0.7f);
        }
        var holder = spill.transform.parent;
        holder.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f); // not the same blob every time
        float size = Random.Range(SpillSize.x, SpillSize.y);
        for (float t = 0f; t < SpillTime; t += Time.deltaTime)
        {
            if (spill == null) yield break;
            float k = 1f - Mathf.Pow(1f - t / SpillTime, 3f); // fast at first, slowing to a stop
            ParkArt.Fit(spill, Mathf.Max(0.01f, size * k));
            // Its middle creeps out from the mouth as it grows, so it stays round the opening.
            holder.position = new Vector3(mouth.x + toward * size * 0.4f * k, ground + 0.015f, mouth.z);
            yield return null;
        }
    }

    void OnDestroy()
    {
        if (spill != null) Destroy(spill.transform.parent.gameObject);
    }
}
