using UnityEngine;
using FMODUnity;

// The Ferris wheel's sounds. While it runs, its motor (the Mechanical emitter on the wheel in the scene) and its
// hydraulics hum. When the power's cut the motor finishes its loop and winds down, the hum stops, the power goes off with a clunk, and from then on the stuck
// cabins creak. Turned up to max speed, the motor runs wild (its Wheel Speed parameter); once the wheel tears off its
// hinge all of that stops dead and the runaway wheel rumbles as it rolls. The events are set on FMODEvents; an empty
// slot is just silent. Added by FerrisWheel.
[RequireComponent(typeof(FerrisWheel))]
public class FerrisWheelAudio : MonoBehaviour
{
    FerrisWheel ride;
    LoopingSound hydraulic, creak, rolling;
    StudioEventEmitter[] motors;
    bool wasPowered = true;
    bool tornOff;

    void Awake()
    {
        ride = GetComponent<FerrisWheel>();
        motors = GetComponents<StudioEventEmitter>();
        hydraulic = new LoopingSound(gameObject);
        creak = new LoopingSound(gameObject);
        rolling = new LoopingSound(ride.wheel.gameObject); // the wheel itself, which rolls off without the rest
    }

    void Update()
    {
        var events = FMODEvents.instance;
        if (events == null) return;
        if (ride.TornOff)
        {
            if (!tornOff) TearOff();
            Roll(events);
            return;
        }
        if (ride.Powered)
        {
            if (!wasPowered) PowerOn(events);
            hydraulic.Play(events.ferrisHydraulic);
            SetHinge(ride.Wobbly);
            SetWheelSpeed(ride.Overdrive);
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
        if (!Ears.Near(transform.position, HearRange)) return;
        if (ride.Powered) ClosedCaptions.Hold(Captions.WheelHum);
        if (!ride.Powered || ride.Wobbly) ClosedCaptions.Hold(Captions.CabinCreak);
        if (ride.Powered && ride.Wobbly) ClosedCaptions.Hold(Captions.WheelWobble);
        if (ride.Overdrive > 0f) ClosedCaptions.Hold(Captions.WheelOverdrive);
    }

    // The motor event's "Wheel Speed" parameter: 0 at its normal speed, up to 1 flat out at max speed
    // (FerrisWheel.Overdrive), which brings in its overdrive layers. Only on the motor: the hydraulic slot plays the
    // same event, and the overdrive shouldn't come in twice.
    const string WheelSpeedParameter = "Wheel Speed";

    void SetWheelSpeed(float value)
    {
        foreach (var motor in motors) motor.SetParameter(WheelSpeedParameter, value);
    }

    // Torn off its hinge (FerrisWheel.Detach): there's no motor any more. Everything it was making stops dead, under
    // the crash.
    void TearOff()
    {
        tornOff = true;
        foreach (var motor in motors)
        {
            bool fade = motor.AllowFadeout;
            motor.AllowFadeout = false;
            motor.Stop();
            motor.AllowFadeout = fade;
        }
        hydraulic.Stop(now: true);
        creak.Stop(now: true);
    }

    // The runaway wheel rumbling along, its "Roll Speed" parameter 1 flat out down to 0 as it rocks to a stop, until it
    // starts to keel over (FerrisWheel.Rolling).
    const string RollSpeedParameter = "Roll Speed";

    void Roll(FMODEvents events)
    {
        if (!ride.Rolling)
        {
            rolling.Stop();
            return;
        }
        rolling.Play(events.wheelRolling);
        rolling.SetParameter(RollSpeedParameter, ride.RollSpeed);
        if (Ears.Near(ride.wheel.position, HearRange)) ClosedCaptions.Hold(Captions.WheelRolling);
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

    // LO-02: the box's power buttons, cutting the power or bringing it back (as the camera takes in the wheel).
    void PowerSwitch(FMODEvents events)
    {
        if (AudioManager.instance != null && !events.cutPower.IsNull) AudioManager.instance.PlayOneShot(events.cutPower, transform.position);
    }

    void PowerOn(FMODEvents events)
    {
        PowerSwitch(events); // its caption: ElectricalBox's "power switch clunks on"
        foreach (var motor in GetComponents<StudioEventEmitter>())
        {
            motor.SetParameter(PowerParameter, 1f); // the emitter reuses its instance, which still has Power Off
            motor.Play();
        }
    }

    void PowerOff(FMODEvents events)
    {
        foreach (var motor in GetComponents<StudioEventEmitter>()) motor.SetParameter(PowerParameter, 0f);
        PowerSwitch(events);
        ClosedCaptions.Show(Captions.PowerSwitchOff);
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
        rolling.Stop();
    }

    void OnDestroy()
    {
        hydraulic.Release();
        creak.Release();
        rolling.Release();
    }
}
