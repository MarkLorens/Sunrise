using System.Collections.Generic;
using UnityEngine;

// A park guard in a holy mask: the spirit can't possess them. Patrols a route. A possessed visitor in sight
// slowly raises suspicion (faster up close, or right after they mess with something). Once spotted, the guard
// runs over and prays with a cross raised until the spirit is driven out. Leaving the body first gets you away.
// Hitting a guard (or throwing something at one) gets you spotted on the spot.
// When something terrible happens the guards go over and keep people back.
[RequireComponent(typeof(GroundWalker), typeof(CharacterSprite))]
public class Guard : MonoBehaviour
{
    public static readonly List<Guard> All = new();

    public enum State { Patrol, Watch, Chase, Pray, Rest, Investigate }

    public Vector3[] route = new Vector3[0];
    public float walkSpeed = 1.6f;
    public float runSpeed = 3.8f; // a little faster than a possessed visitor
    public float sightRange = 6f;
    public float noticeTime = 3f; // seconds of watching a calm possessed visitor before spotting them
    public float prayRange = 1.4f;
    public float prayTime = 2f;
    public Color prayerGlow = new(1f, 0.85f, 0.35f);

    public State Current { get; private set; }
    public float Suspicion { get; private set; }

    GroundWalker walker;
    CharacterSprite look;
    WorldBar suspicionBar;
    Visitor suspect;
    int routeIndex;
    float timer;
    float sparkleTimer;
    float lostFor;

    void Awake()
    {
        walker = GetComponent<GroundWalker>();
        look = GetComponent<CharacterSprite>();
        look.Load();
        walker.readKeyboard = false;
        timer = Random.Range(0f, 2f);
        suspicionBar = WorldBar.Create(transform, new Vector3(0f, look.idle.bounds.size.y + 0.2f, -0.02f), 0.7f);
    }

    void OnEnable()
    {
        All.Add(this);
        Possession.Masked.Add(transform);
    }

    void OnDisable()
    {
        All.Remove(this);
        Possession.Masked.Remove(transform);
    }

    // Whacked or hit by something thrown.
    public void HitBy(Visitor attacker)
    {
        look.Punch();
        Sfx.Punch();
        Fx.Shake(0.12f);
        var host = Possession.Instance != null ? Possession.Instance.Host : null;
        if (attacker != null && attacker == host && Current is not (State.Chase or State.Pray)) Spot(attacker);
    }

    void Update()
    {
        var host = Possession.Instance != null ? Possession.Instance.Host : null;
        switch (Current)
        {
            case State.Chase:
                Chase(host);
                break;

            case State.Pray:
                Pray(host);
                break;

            default:
                if (Watch(host)) break;
                if (Current == State.Rest) Rest();
                else if (!Investigate()) Patrol();
                break;
        }
        suspicionBar.Set(Suspicion, Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.25f, 0.2f), Suspicion), Current == State.Watch && Suspicion > 0.02f);
    }

    // ---------------------------------------------------------------- noticing

    // Returns true while the guard has stopped to stare at someone.
    bool Watch(Visitor host)
    {
        if (Current == State.Rest || host == null || !CanSee(host))
        {
            Suspicion = Mathf.Max(0f, Suspicion - Time.deltaTime * 0.3f);
            if (Current == State.Watch)
            {
                Current = State.Patrol;
                look.HideBubble();
            }
            return false;
        }

        float rate = 1f / noticeTime;
        if (Time.time - Possession.Instance.LastLoudTime < 2f) rate *= 4f; // caught them shouting, hitting, swiping, fiddling
        if (Flat(host.transform.position - transform.position).magnitude < 3f) rate *= 1.5f;
        Suspicion += rate * Time.deltaTime;

        if (Current != State.Watch)
        {
            // "huh?" - stop and look
            Current = State.Watch;
            look.Show("question", 999f, true);
            Sfx.Play(Sfx.Huh, 0.4f);
        }
        Stop();
        look.Face(host.transform.position);
        if (Suspicion >= 1f) Spot(host);
        return true;
    }

    bool CanSee(Visitor host)
    {
        Vector3 to = Flat(host.transform.position - transform.position);
        float d = to.magnitude;
        if (d > sightRange) return false;
        // Up close they notice either way; further off only what's in front of them.
        return d < 2.5f || (look.FacingLeft ? to.x < 0.5f : to.x > -0.5f);
    }

    void Spot(Visitor host)
    {
        suspect = host;
        Suspicion = 1f;
        lostFor = 0f;
        Current = State.Chase;
        look.Show("Spotted", 999f, true);
        look.Cheer(0.5f);
        Sfx.Play(Sfx.Spotted, 0.8f);
        Fx.Shake(0.1f);
        GameHud.Say($"{name} spotted something wrong with {host.name}! Get out of that body!", "Icons/Spotted");
    }

    // ---------------------------------------------------------------- chasing and praying

    void Chase(Visitor host)
    {
        if (host != suspect)
        {
            Lose();
            return;
        }
        Vector3 to = Flat(suspect.transform.position - transform.position);
        float d = to.magnitude;
        lostFor = d > sightRange * 1.6f ? lostFor + Time.deltaTime : 0f;
        if (lostFor > 2.5f)
        {
            Lose();
            return;
        }
        if (d < prayRange)
        {
            Current = State.Pray;
            timer = 0f;
            look.Show("Cross", 999f, true);
            GameHud.Say($"{name} raises a cross at {suspect.name} and starts to pray!", "UI/cross");
            return;
        }
        Move(to / d, runSpeed, true);
    }

    void Pray(Visitor host)
    {
        if (host != suspect)
        {
            Lose();
            return;
        }
        Vector3 to = Flat(suspect.transform.position - transform.position);
        float d = to.magnitude;
        if (d > prayRange + 2f)
        {
            // They got away mid-prayer: after them.
            Current = State.Chase;
            look.pose = null;
            look.Show("Spotted", 999f, true);
            return;
        }

        // Keep the cross up and shuffle after them.
        if (d > prayRange * 0.8f) Move(to / d, walkSpeed, false);
        else Stop();
        look.pose = look.interact;
        look.Face(suspect.transform.position);
        suspect.Look.SetTint(Color.Lerp(suspect.possessedTint, prayerGlow, Mathf.PingPong(Time.time * 4f, 1f)));
        if ((sparkleTimer -= Time.deltaTime) <= 0f)
        {
            sparkleTimer = 0.08f;
            Fx.Burst("Fx/sparkle", suspect.HeadPoint, 3, prayerGlow, 1.5f, 0.3f, 0.5f);
        }

        timer += Time.deltaTime;
        if (timer < prayTime) return;

        var victim = suspect;
        suspect = null;
        Suspicion = 0f;
        StartRest(2f, "heart");
        Fx.Burst("Fx/sparkle", victim.HeadPoint, 40, prayerGlow, 5f, 0.4f, 0.7f);
        Fx.Shake(0.25f);
        GameHud.Say($"{name}'s prayer drives you out of {victim.name}!", "UI/cross");
        Possession.Instance.Eject();
    }

    // The spirit left the body (or got away): the guard is left wondering.
    void Lose()
    {
        bool ejected = Possession.Instance != null && Time.time - Possession.Instance.LastEjectedAt < 0.5f;
        suspect = null;
        Suspicion = 0f;
        StartRest(2f, "question");
        if (ejected) return; // another guard got them
        GameHud.Say($"{name} lost track of you.");
        Mischief.Complete(Mischief.Task.SlipGuard);
    }

    void StartRest(float seconds, string icon)
    {
        Current = State.Rest;
        timer = seconds;
        look.pose = null;
        if (icon != null) look.Show(icon, seconds);
        else look.HideBubble();
        Stop();
    }

    void Rest()
    {
        Stop();
        if ((timer -= Time.deltaTime) > 0f) return;
        Current = State.Patrol;
        timer = 0f;
    }

    // ---------------------------------------------------------------- patrolling

    // Go and stand at the edge of nearby chaos ("move along, nothing to see here").
    bool Investigate()
    {
        if (!ParkChaos.Nearest(transform.position, 25f, ParkChaos.Terrifying, out Vector3 center, out float radius))
        {
            if (Current == State.Investigate) Current = State.Patrol;
            return false;
        }
        Current = State.Investigate;
        Vector3 from = Flat(transform.position - center);
        Vector3 stand = center + (from.sqrMagnitude > 0.01f ? from.normalized : Vector3.back) * Mathf.Min(radius * 0.6f, 4f);
        Vector3 to = Flat(stand - transform.position);
        if (to.magnitude > 0.5f) Move(to.normalized, walkSpeed * 1.5f, false);
        else
        {
            Stop();
            look.Face(center);
        }
        return true;
    }

    void Patrol()
    {
        Current = State.Patrol;
        if (route.Length == 0)
        {
            Stop();
            return;
        }
        timer -= Time.deltaTime;
        if (timer > 0f)
        {
            Stop(); // pause and look around
            return;
        }
        Vector3 to = Flat(route[routeIndex] - transform.position);
        // Arrived, or stuck against an edge long enough: on to the next point.
        if (to.magnitude < 0.4f || timer < -20f)
        {
            routeIndex = (routeIndex + 1) % route.Length;
            timer = Random.Range(1.5f, 3f);
            return;
        }
        Move(to.normalized, walkSpeed, false);
    }

    void Move(Vector3 dir, float speed, bool running)
    {
        walker.speed = speed;
        walker.move = new Vector2(dir.x, dir.z);
        look.running = running;
    }

    void Stop()
    {
        walker.move = Vector2.zero;
        look.running = false;
    }

    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
}
