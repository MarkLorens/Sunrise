using UnityEngine;

// One-shot sprite bursts (sparkles, puffs, purple possession swirls) and screen shake.
public static class Fx
{
    public static readonly Color Gold = new(1f, 0.85f, 0.35f);
    public static readonly Color Spirit = new(0.75f, 0.45f, 1f);

    public static void Burst(string sprite, Vector3 at, int count, Color color, float speed, float size, float life)
    {
        for (int i = 0; i < count; i++)
        {
            var r = ParkArt.Make("Fx", null, sprite, size * Random.Range(0.6f, 1.2f), color, ParkArt.OverheadOrder);
            r.transform.position = at;
            var bit = r.gameObject.AddComponent<FxBit>();
            Vector3 dir = Random.onUnitSphere;
            dir.z *= 0.3f; // mostly across the screen, where it reads
            bit.velocity = dir * speed * Random.Range(0.5f, 1f);
            bit.life = life * Random.Range(0.7f, 1.2f);
        }
    }

    public static void Shake(float amount) => CameraRig.Shake(amount);
}
