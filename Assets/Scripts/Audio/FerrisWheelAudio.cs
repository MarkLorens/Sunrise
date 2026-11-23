using UnityEngine;
using FMODUnity;

// The Ferris wheel's sounds. While it runs, its motor (the Mechanical emitter on the wheel in the scene) and its
// hydraulics hum. When the power's cut the motor finishes its loop and winds down, the hum stops, the power goes off with a clunk, and from then on the stuck
// cabins creak. The events are set on FMODEvents; an empty slot is just silent. Added by FerrisWheel.
[RequireComponent(typeof(FerrisWheel))]
public class FerrisWheelAudio : MonoBehaviour
{
    FerrisWheel ride;
    LoopingSound hydraulic, creak;
    StudioEventEmitter[] motors;
    bool wasPowered = true;

    void Awake()
    {
        ride = GetComponent<FerrisWheel>();
        motors = GetComponents<StudioEventEmitter>();
        hydraulic = new LoopingSound(gameObject);
        creak = new LoopingSound(gameObject);
    }

    void Update()
    {
        var events = FMODEvents.instance;
        if (events == null) return;
        if (ride.Powered)
        {
            if (!wasPowered) PowerOn();
            hydraulic.Play(events.ferrisHydraulic);
            SetHinge(ride.Wobbly);
            if (ride.Wobbly) creak.Play(events.ferrisCabinCreak); // a loosened hinge: it creaks all the way round
            else creak.Stop();
        }
        else
        {
            if (wasPowered) PowerOff(events);
            hydraulic.Stop();
            creak.Play(events.ferrisCabinCreak);
        }
        wasPowered = ride.Powered;

        // Closed captions while you're near enough to hear it (its 3D sounds fade out further off).
        if (!Possession.PlayerNear(transform.position, HearRange)) return;
        if (ride.Powered) ClosedCaptions.Hold(Captions.WheelHum);
        if (!ride.Powered || ride.Wobbly) ClosedCaptions.Hold(Captions.CabinCreak);
        if (ride.Powered && ride.Wobbly) ClosedCaptions.Hold(Captions.WheelWobble);
    }

    // The motor event's "Hinge" parameter: Loose (0) once the wheel's running on the loosened hinge (FerrisWheel.Wobbly),
    // which brings in its wobbly noises, otherwise Intact (1). Set every frame while it runs, so it also reaches an
    // instance that was only just made (starting again, or loading a save that's already wobbly).
    const string HingeParameter = "Hinge";

    void SetHinge(bool loose)
    {
        float value = loose ? 0f : 1f;
        hydraulic.SetParameter(HingeParameter, value);
        foreach (var motor in motors) motor.SetParameter(HingeParameter, value);
    }

    const float HearRange = 14f;

    // The motor event loops while its "Power" parameter is On. Setting it Off lets the playhead leave the loop
    // region the next time it reaches the end and play the wind-down tail, after which the event ends by itself.
    const string PowerParameter = "Power";

    void PowerOn()
    {
        foreach (var motor in GetComponents<StudioEventEmitter>())
        {
            motor.SetParameter(PowerParameter, 1f); // the emitter reuses its instance, which still has Power Off
            motor.Play();
        }
    }

    void PowerOff(FMODEvents events)
    {
        foreach (var motor in GetComponents<StudioEventEmitter>()) motor.SetParameter(PowerParameter, 0f);
        if (AudioManager.instance != null) AudioManager.instance.PlayOneShot(events.ferrisPowerOff, transform.position);
        ClosedCaptions.Show(Captions.PowerOff);
    }

    // Loading a save with the power already cut (FerrisWheel.RestoreUnpowered): the motor just goes quiet, with no
    // power-off clunk and no wind-down. The stuck cabins still creak.
    public void RestoreUnpowered()
    {
        foreach (var motor in GetComponents<StudioEventEmitter>()) motor.Stop();
        hydraulic.Stop();
        wasPowered = false; // so Update doesn't treat it as the power being cut just now
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
