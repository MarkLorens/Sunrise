using UnityEngine;

// A cartoon brawl: both visitors vanish into a tumbling dust cloud with POW! bursts and flailing figures popping
// out of it. When it settles one of them doesn't get up again, and their soul floats to the spirit.
// Stronger visitors are more likely to win.
public class Brawl : MonoBehaviour
{
    const float Duration = 3.6f;
    static readonly string[] pows = { "Fx/pow_bam", "Fx/pow_bonk", "Fx/pow_pow" };

    Visitor a, b;
    Transform visual;
    SpriteRenderer cloud, figure, pow;
    float age, frameTimer, popTimer, powTimer, powAge = 1f;
    int frame;
    Vector3 drift;

    public static void Begin(Visitor first, Visitor second)
    {
        if (first == null || second == null || first == second || !first.CanBeHit || !second.CanBeHit) return;
        // The spirit can't stay inside a body that's brawling: it gets thrown out.
        var spirit = Possession.Instance;
        if (spirit != null && (spirit.Host == first || spirit.Host == second))
        {
            GameHud.Say("You're thrown out of the body as the brawl begins!");
            spirit.Release();
        }
        new GameObject($"Brawl ({first.name} vs {second.name})").AddComponent<Brawl>().Init(first, second);
    }

    void Init(Visitor first, Visitor second)
    {
        a = first;
        b = second;
        Vector3 mid = (a.transform.position + b.transform.position) * 0.5f;
        transform.position = new Vector3(mid.x, 0f, mid.z);
        a.EnterFight();
        b.EnterFight();

        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);
        visual.localPosition = new Vector3(0f, 1.2f, -0.3f);
        cloud = ParkArt.Make("Cloud", visual, "Fx/brawl_cloud_0", 2.6f, Color.white, 2);
        figure = ParkArt.Make("Figure", visual, null, 1.1f, Color.white, 1);
        pow = ParkArt.Make("Pow", visual, pows[0], 1.1f, Color.white, 3);
        pow.enabled = false;

        Sfx.Punch(1f);
        Fx.Shake(0.3f);
        ParkChaos.Add(transform.position, 7f, Duration + 1f);
        GameHud.Say($"BRAWL! {a.name} vs {b.name}", "Bubbles/swords");
        Mischief.Complete(Mischief.Task.Brawl);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        // Tumble around a little, dragging both fighters along.
        drift = Vector3.Lerp(drift, new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1f, 1f)), dt * 2f);
        Vector3 next = transform.position + drift * dt;
        if (a.GetComponent<GroundWalker>().IsWalkable(next)) transform.position = next;
        Place(a, Vector3.left * 0.3f);
        Place(b, Vector3.right * 0.3f);

        // Flip-book cloud with jitter.
        if ((frameTimer -= dt) <= 0f)
        {
            frameTimer = 0.08f;
            frame = (frame + 1) % 3;
            cloud.sprite = ParkArt.Get("Fx/brawl_cloud_" + frame);
            cloud.transform.localPosition = new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(-0.08f, 0.08f), 0f);
            cloud.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-6f, 6f));
        }

        // Somebody's arms and legs popping out of the cloud.
        if ((popTimer -= dt) <= 0f)
        {
            popTimer = 0.22f;
            var who = Random.value < 0.5f ? a.Look : b.Look;
            figure.sprite = Random.value < 0.5f ? who.cheer[Random.Range(0, 2)] : who.interact;
            figure.flipX = Random.value < 0.5f;
            ParkArt.Fit(figure, 1.1f);
            Vector2 edge = Random.insideUnitCircle.normalized;
            figure.transform.localPosition = new Vector3(edge.x * 1.1f, edge.y * 0.7f - 0.3f, 0.05f);
            figure.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-35f, 35f));
        }

        // POW! BAM! BONK!
        if ((powTimer -= dt) <= 0f)
        {
            powTimer = Random.Range(0.35f, 0.6f);
            powAge = 0f;
            pow.enabled = true;
            pow.sprite = ParkArt.Get(pows[Random.Range(0, pows.Length)]);
            pow.transform.localPosition = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(0.3f, 1.1f), -0.05f);
            Sfx.Punch(0.7f);
            Fx.Shake(0.1f);
        }
        powAge += dt;
        float s = powAge < 0.1f ? powAge * 10f : Mathf.Max(0f, 1.2f - (powAge - 0.1f) * 2.5f);
        pow.transform.localScale = Vector3.one * (1.1f / pow.sprite.bounds.size.y) * s;
        if (powAge > 0.5f) pow.enabled = false;

        if (age >= Duration) Finish();
    }

    void Place(Visitor v, Vector3 offset)
    {
        var rb = v.GetComponent<Rigidbody>();
        rb.position = v.transform.position = transform.position + offset;
    }

    void Finish()
    {
        float chanceA = Mathf.Clamp(a.strength / (a.strength + b.strength), 0.15f, 0.85f);
        var winner = Random.value < chanceA ? a : b;
        var loser = winner == a ? b : a;
        Place(winner, (winner == a ? Vector3.left : Vector3.right) * 0.8f);
        Place(loser, (loser == a ? Vector3.left : Vector3.right) * 0.4f);
        winner.ExitFight(true);
        loser.ExitFight(false);
        Fx.Burst("Fx/puff", transform.position + Vector3.up, 16, new Color(0.95f, 0.9f, 0.8f), 3f, 0.8f, 0.7f);
        Sfx.Play(Sfx.Thud, 1f, 0.8f);
        GameHud.Say($"{winner.name} wins! {loser.name} is no more...", "Bubbles/skull");
        Destroy(gameObject);
    }
}
