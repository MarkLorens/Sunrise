using System.Collections.Generic;
using UnityEngine;

// Something a visitor carries over their head: a spicy hotdog, a drink, the toilet key. A possessed visitor can
// grab it (even right out of someone's hands), drop it or throw it. A thrown item hits people (and they get furious
// at the thrower); one that lands in the Ferris wheel's gears jams it. Whoever owned it chases down whoever took it.
public class Item : MonoBehaviour
{
    public static readonly List<Item> All = new();

    const float Size = 0.5f;
    public const float Gravity = 18f;

    public VisitorItem kind;
    public Visitor Owner { get; set; }
    public Visitor Holder { get; private set; }
    public Visitor LastHolder { get; private set; }
    public bool InFlight { get; private set; }
    public string DisplayName => kind switch
    {
        VisitorItem.SpicyHotdog => "spicy hotdog",
        VisitorItem.ToiletKey => "toilet key",
        VisitorItem.Drink => "drink",
        VisitorItem.Screwdriver => "screwdriver",
        _ => "thing",
    };
    public float HitPower => kind == VisitorItem.Drink ? 1f : 0.75f; // a full cup to the face is the worst

    SpriteRenderer sprite, shadow;
    Vector3 velocity;
    bool thrown;

    public static Item Create(VisitorItem kind, Vector3 position, Visitor owner)
    {
        var go = new GameObject(kind.ToString());
        go.transform.position = position;
        var item = go.AddComponent<Item>();
        item.kind = kind;
        item.Owner = owner;
        item.sprite = ParkArt.Make("Sprite", go.transform, "Items/" + kind, Size, Color.white, 1);
        if (item.sprite.sprite == null)
        {
            // ponytail: grey block until there's art in Resources/Park/Items/<kind>.png (the screwdriver has none yet).
            item.sprite.sprite = ParkArt.Get("Markers/white");
            item.sprite.color = new Color(0.55f, 0.6f, 0.68f);
            ParkArt.Fit(item.sprite, Size * 0.6f);
        }
        item.shadow = ParkArt.Make("Shadow", null, "Markers/shadow", 0.3f, Color.white, ParkArt.DecalOrder, true);
        item.shadow.transform.parent.SetParent(go.transform, true);
        return item;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public void PickedUp(Visitor by, bool silent)
    {
        Holder = by;
        LastHolder = by;
        InFlight = thrown = false;
        if (!silent) Sfx.Play(Sfx.Grab, 0.6f);
    }

    // Let go of without throwing: it tumbles to the ground.
    public void Dropped(Vector3 at)
    {
        Holder = null;
        transform.position = at;
        velocity = Vector3.up * 2f;
        InFlight = true;
        thrown = false;
        Sfx.Play(Sfx.Drop, 0.5f);
    }

    public void Thrown(Visitor by, Vector3 from, Vector3 v)
    {
        Holder = null;
        LastHolder = by;
        transform.position = from;
        velocity = v;
        InFlight = thrown = true;
    }

    // Eaten, poured out, left in a lock...
    public void Consume() => Destroy(gameObject);

    void LateUpdate()
    {
        var shadowHolder = shadow.transform.parent;
        if (Holder != null)
        {
            // Carried over the head with a little wobble (hidden while the holder rides or brawls).
            transform.position = Holder.CarryPoint + Vector3.up * Mathf.Sin(Time.time * 6f) * 0.04f;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 3f) * 8f);
            sprite.enabled = Holder.Look.Visible;
            shadow.enabled = false;
            return;
        }
        sprite.enabled = shadow.enabled = true;
        shadowHolder.SetPositionAndRotation(new Vector3(transform.position.x, 0.02f, transform.position.z), Quaternion.identity);
        if (!InFlight) return;

        velocity.y -= Gravity * Time.deltaTime;
        transform.position += velocity * Time.deltaTime;
        transform.Rotate(0f, 0f, -velocity.x * 60f * Time.deltaTime);

        if (thrown && (HitSomeone() || JamWheel())) return;
        if (transform.position.y <= Size * 0.5f && velocity.y < 0f) Land();
    }

    bool HitSomeone()
    {
        Vector3 p = transform.position;
        foreach (var v in Visitor.All)
        {
            if (v == LastHolder || !v.CanBeHit || !Touches(v.transform.position, v.HeadHeight, p)) continue;
            v.GetHit(LastHolder, HitPower, velocity);
            Bounce(p);
            if (LastHolder != null && LastHolder.Current == Visitor.State.Possessed) Mischief.Complete(Mischief.Task.ThrowHit);
            return true;
        }
        return false;
    }

    static bool Touches(Vector3 feet, float height, Vector3 p)
    {
        Vector3 d = p - feet;
        return new Vector2(d.x, d.z).magnitude < 0.55f && p.y < height + 0.2f;
    }

    void Bounce(Vector3 at)
    {
        Sfx.Play(Sfx.HitWood, 0.9f);
        Fx.Burst("Fx/sparkle", at, 12, new Color(1f, 0.95f, 0.5f), 3f, 0.35f, 0.45f);
        thrown = false;
        velocity = new Vector3(-velocity.x * 0.25f, 3f, -velocity.z * 0.25f);
    }

    bool JamWheel()
    {
        foreach (var w in FerrisWheel.All)
        {
            if (!w.InGears(transform.position)) continue;
            w.Jam(this);
            return true;
        }
        return false;
    }

    void Land()
    {
        transform.position = new Vector3(transform.position.x, Size * 0.5f, transform.position.z);
        transform.rotation = Quaternion.identity;
        velocity = Vector3.zero;
        if (thrown) Sfx.Play(Sfx.HitSoft, 0.4f, 1.2f);
        InFlight = thrown = false;
    }
}
