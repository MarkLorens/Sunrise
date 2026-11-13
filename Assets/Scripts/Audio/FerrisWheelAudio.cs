using UnityEngine;
using FMODUnity;

// The Ferris wheel's sounds. While it runs, its motor (the Mechanical emitter on the wheel in the scene) and its
// hydraulics hum. When the power's cut the hum stops, the power goes off with a clunk, and from then on the stuck
// cabins creak. The events are set on FMODEvents; an empty slot is just silent. Added by FerrisWheel.
[RequireComponent(typeof(FerrisWheel))]
public class FerrisWheelAudio : MonoBehaviour
{
    FerrisWheel ride;
    LoopingSound hydraulic, creak;
    bool wasPowered = true;

    void Awake()
    {
        ride = GetComponent<FerrisWheel>();
        hydraulic = new LoopingSound(gameObject);
        creak = new LoopingSound(gameObject);
    }

    void Update()
    {
        var events = FMODEvents.instance;
        if (events == null) return;
        if (ride.Powered)
        {
            hydraulic.Play(events.ferrisHydraulic);
            creak.Stop();
        }
        else
        {
            if (wasPowered) PowerOff(events);
            hydraulic.Stop();
            creak.Play(events.ferrisCabinCreak);
        }
        wasPowered = ride.Powered;
    }

    void PowerOff(FMODEvents events)
    {
        foreach (var motor in GetComponents<StudioEventEmitter>()) motor.Stop();
        if (AudioManager.instance != null) AudioManager.instance.PlayOneShot(events.ferrisPowerOff, transform.position);
    }

    void OnDisable()
    {
        hydraulic.Stop();
        creak.Stop();
    }

    void OnDestroy()
    {
        hydraulic.Release();
        creak.Release();
    }
}
