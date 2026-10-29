using UnityEngine;

// The soul of someone who didn't survive the chaos: drifts up, then streaks to the spirit and is harvested.
public class SoulWisp : MonoBehaviour
{
    static readonly Color Mint = new(0.5f, 1f, 0.9f);

    SpriteRenderer sprite;
    Vector3 velocity = Vector3.up * 2.5f;
    float age;

    public static void Spawn(Vector3 position)
    {
        var go = new GameObject("Soul");
        go.transform.position = position;
        var wisp = go.AddComponent<SoulWisp>();
        wisp.sprite = ParkArt.Make("Sprite", go.transform, "Fx/soul", 0.6f, Color.white, ParkArt.OverheadOrder);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        var spirit = Possession.Instance;
        if (age < 1.2f || spirit == null || spirit.Focus == null)
        {
            // Rise and wobble.
            velocity = Vector3.Lerp(velocity, Vector3.up * 0.4f, dt * 2f);
            transform.position += (velocity + new Vector3(Mathf.Sin(age * 6f), 0f, 0f) * 0.5f) * dt;
        }
        else
        {
            Vector3 to = spirit.SpiritPoint - transform.position;
            float speed = 4f + (age - 1.2f) * 14f;
            transform.position += Vector3.ClampMagnitude(to.normalized * speed * dt, to.magnitude);
            if (to.magnitude < 0.3f)
            {
                Sfx.Play(Sfx.Soul, 0.9f, 1.3f);
                Fx.Burst("Fx/sparkle", transform.position, 20, Mint, 3f, 0.35f, 0.5f);
                GameHud.AddSoul();
                Destroy(gameObject);
                return;
            }
        }
        sprite.transform.localScale = Vector3.one * (0.6f / sprite.sprite.bounds.size.y) * (1f + Mathf.Sin(age * 10f) * 0.1f);
        if (Random.value < dt * 20f) Fx.Burst("Fx/sparkle", transform.position, 1, new Color(Mint.r, Mint.g, Mint.b, 0.8f), 0.3f, 0.2f, 0.5f);
    }
}
