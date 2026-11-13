using UnityEngine;

// Something a visitor carries in their hand: a drink, a water bottle, the mechanic's keys. Possessing someone, you
// can use what they hold (the mechanic's keys open the electrical box). Someone who panics or goes down lets go of
// it, and it tumbles to the ground and stays there.
[DefaultExecutionOrder(50)] // after CharacterSprite has picked this frame's pose, so it follows the hand exactly
public class Item : MonoBehaviour
{
    const float Size = 0.5f;     // lying on the ground or falling
    const float HeldSize = 0.3f; // in a hand: about one and a half hands tall
    const float HeldTilt = 15f;  // degrees a held thing leans in toward the holder's body
    const float Gravity = 18f;

    public VisitorItem kind;
    public Visitor Holder { get; private set; }
    public string DisplayName => kind switch
    {
        VisitorItem.Drink => "drink",
        VisitorItem.Keys => "keys",
        VisitorItem.WaterBottle => "water bottle",
        _ => "thing",
    };

    SpriteRenderer sprite, shadow;

    // What the HUD's inventory box shows (a grey block until the item has art).
    public Sprite Icon => sprite.sprite;
    public Color IconColor => sprite.color;
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
        if (!silent) Sfx.Play(Sfx.Grab, 0.6f);
    }

    // Let go of: it tumbles to the ground.
    public void Dropped(Vector3 at)
    {
        Holder = null;
        transform.position = at;
        velocity = Vector3.up * 2f;
        falling = true;
        Sfx.Play(Sfx.Drop, 0.5f);
    }

    // Eaten, poured out...
    public void Consume() => Destroy(gameObject);

    void LateUpdate()
    {
        var shadowHolder = shadow.transform.parent;
        if (Holder != null)
        {
            // In the hand, drawn in front of it (and of the whole body), sticking up out of the fist and tipped a little
            // in toward the body, so it reads as held. It swings with the hand on every walk frame (hidden while the
            // holder rides).
            transform.localScale = Vector3.one * (HeldSize / Size);
            var tilt = Quaternion.Euler(0f, 0f, Holder.Look.FacingLeft ? HeldTilt : -HeldTilt);
            transform.SetPositionAndRotation(Holder.CarryPoint + tilt * Vector3.up * (HeldSize * 0.3f) - Holder.transform.forward * 0.02f, tilt);
            sprite.enabled = Holder.Look.Visible;
            shadow.enabled = false;
            return;
        }
        transform.localScale = Vector3.one;
        sprite.enabled = shadow.enabled = true;
        shadowHolder.SetPositionAndRotation(new Vector3(transform.position.x, 0.02f, transform.position.z), Quaternion.identity);
        if (!falling) return;

        velocity.y -= Gravity * Time.deltaTime;
        transform.position += velocity * Time.deltaTime;
        transform.Rotate(0f, 0f, -velocity.x * 60f * Time.deltaTime);

        if (transform.position.y <= Size * 0.5f && velocity.y < 0f) Land();
    }

    void Land()
    {
        transform.position = new Vector3(transform.position.x, Size * 0.5f, transform.position.z);
        transform.rotation = Quaternion.identity;
        velocity = Vector3.zero;
        falling = false;
    }
}
