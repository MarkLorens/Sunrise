using UnityEngine;
using FMODUnity;
using FMOD.Studio;

// The park's crowd: Park Crowd (FMODEvents.parkCrowd) plays the whole time you're in the park, on the player, where
// the game hears from (the listener's attenuation object). Its two crowd loops sound all round you; its scatterers
// (shouts and laughs, screams and gasps) spring up around you. Two of its parameters follow the visitors near you:
//   Crowd: how many there are (0 = nobody within hearRadius, 1 = about fullCrowd of them right next to you).
//   Panic: how many of them are running scared (State.Panic), plus how close something frightening is (ParkChaos).
// Each visitor counts for more the closer they are, so people walking in and out of range fade the sound rather than
// step it; and both numbers ease toward where they're headed (panic rising quickly and dying away slowly), so the
// sound never jumps. GameHud puts it on the player.
public class ParkCrowd : MonoBehaviour
{
    [Header("Who counts")]
    [Tooltip("Metres: visitors further than this from the player aren't heard.")]
    public float hearRadius = 12f;
    [Tooltip("Visitors right next to you (fewer further away) for Crowd to reach 1.")]
    public float fullCrowd = 8f;
    [Tooltip("Panicking visitors right next to you (fewer further away) for Panic to reach 1.")]
    public float fullPanic = 3f;
    [Tooltip("Metres: a frightening place (ParkChaos) whose edge is this close adds to Panic, up to chaosPanic when you're in it.")]
    public float chaosRange = 10f;
    [Range(0f, 1f)] public float chaosPanic = 0.35f;

    [Header("Easing (per second)")]
    public float crowdSpeed = 0.4f;  // ~2.5 s from empty to packed
    public float panicRise = 1.5f;   // screams come in fast...
    public float panicFall = 0.2f;   // ...and die away over ~5 s

    [Header("Sound")]
    [Tooltip("Metres: the event's 3D range (full volume within the first, quietest past the second). Covers the scatterers' furthest sounds.")]
    public Vector2 range = new(1f, 30f);

    const float SampleEvery = 0.2f; // counting the visitors five times a second is plenty

    EventInstance instance;
    float crowd, panic, crowdTarget, panicTarget, nextSample;
    bool started;

    void Update()
    {
        if (!started)
        {
            if (LoadingScreen.Busy) return; // starts with the park, once the loading screen lifts
            started = true;
            Sample();
            crowd = crowdTarget; // no swell up from nothing at the start
            panic = panicTarget;
            Begin();
        }
        if (!instance.isValid()) return;

        if (Time.time >= nextSample) Sample(); // game time: paused, it holds
        float dt = Time.deltaTime;
        crowd = Mathf.MoveTowards(crowd, crowdTarget, crowdSpeed * dt);
        panic = Mathf.MoveTowards(panic, panicTarget, (panicTarget > panic ? panicRise : panicFall) * dt);
        instance.setParameterByName("Crowd", crowd);
        instance.setParameterByName("Panic", panic);
    }

    void Begin()
    {
        var events = FMODEvents.instance;
        var audio = AudioManager.instance;
        if (audio == null || events == null || events.parkCrowd.IsNull) return;
        instance = audio.CreateInstance(events.parkCrowd);
        if (!instance.isValid()) return; // not in the built banks
        RuntimeManager.AttachInstanceToGameObject(instance, gameObject, GetComponent<Rigidbody>());
        instance.setProperty(EVENT_PROPERTY.MINIMUM_DISTANCE, range.x);
        instance.setProperty(EVENT_PROPERTY.MAXIMUM_DISTANCE, Mathf.Max(range.x, range.y));
        instance.setParameterByName("Crowd", crowd, true); // straight there, not eased (FMOD's own seek speed)
        instance.setParameterByName("Panic", panic, true);
        instance.start();
    }

    // Where Crowd and Panic are headed: every visitor in earshot, weighted by how close they are.
    void Sample()
    {
        nextSample = Time.time + SampleEvery;
        Vector3 me = transform.position;
        float people = 0f, scared = 0f;
        foreach (var v in Visitor.All)
        {
            if (v == null || !v.Look.Visible) continue; // inside the toilet, say
            Vector3 d = v.transform.position - me;
            d.y = 0f;
            float near = 1f - d.magnitude / hearRadius;
            if (near <= 0f) continue;
            people += near;
            if (v.Current == Visitor.State.Panic) scared += near;
        }

        float chaos = 0f;
        if (ParkChaos.Nearest(me, chaosRange, ParkChaos.Scary, out Vector3 center, out float radius))
        {
            Vector3 d = me - center;
            d.y = 0f;
            float gap = Mathf.Max(0f, d.magnitude - radius); // 0 inside it
            chaos = chaosPanic * (1f - gap / chaosRange);
        }

        crowdTarget = Mathf.Clamp01(people / fullCrowd);
        panicTarget = Mathf.Clamp01(scared / fullPanic + chaos);
    }

    void OnDestroy()
    {
        if (!instance.isValid()) return;
        if (AudioManager.instance != null) AudioManager.instance.ReleaseInstance(instance);
        else
        {
            instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            instance.release();
        }
        instance.clearHandle();
    }
}
