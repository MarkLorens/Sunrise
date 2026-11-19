using UnityEngine;

// One particle of a burst: drifts, slows, spins, shrinks and fades away.
public class FxBit : MonoBehaviour
{
    public Vector3 velocity;
    public float life = 0.5f;
    public float drag = 3f; // how fast it slows (0: drifts on at the same speed, like gas carried by a fan)

    SpriteRenderer r;
    Vector3 scale;
    Color color;
    float age;
    float spin;

    void Start()
    {
        r = GetComponent<SpriteRenderer>();
        scale = transform.localScale;
        color = r.color;
        spin = Random.Range(-360f, 360f);
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= life)
        {
            Destroy(gameObject);
            return;
        }
        float k = age / life;
        velocity *= 1f - Mathf.Min(1f, drag * Time.deltaTime);
        transform.position += velocity * Time.deltaTime;
        transform.localScale = scale * (1f - k * 0.6f);
        transform.Rotate(0f, 0f, spin * Time.deltaTime);
        r.color = new Color(color.r, color.g, color.b, color.a * (1f - k));
    }
}
