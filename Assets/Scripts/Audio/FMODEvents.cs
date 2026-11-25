using UnityEngine;
using FMODUnity;

// Every FMOD event the scripts play, in one place, grouped by the FMOD project's event folders (the IDs in the
// tooltips are the sound list's Sound IDs; slots with no FMOD event yet sit with the folder they belong to).
// Lives on the AudioSystem prefab (Assets/Resources) next to AudioManager: open the prefab and drag the events in
// from the Inspector. An empty slot just means that sound doesn't play yet.
public class FMODEvents : MonoBehaviour
{
    [field: Header("A-leak-tricity (AL)")]
    [field: Tooltip("AL-01: music from each boombox speaker (3D; restarted when it ends if it doesn't loop).")]
    [field: SerializeField] public EventReference speakerMusic { get; private set; }
    [field: Tooltip("AL-06: everyone standing in the puddle gets electrocuted (plays where the cable hit the water).")]
    [field: SerializeField] public EventReference electrocution { get; private set; }
    [field: Tooltip("An electrocuted visitor toppling over once the zap's done with them (Visitor.Collapse; a one-shot, 3D where they fall; once for everyone dropping at the same moment).")]
    [field: SerializeField] public EventReference npcFall { get; private set; }
    [field: Tooltip("The leaky fountain's faucet dripping into its puddle, the whole game (3D at the fountain, loops).")]
    [field: SerializeField] public EventReference faucetDrip { get; private set; }
    [field: Tooltip("AL-02: pressing the leaky fountain's button.")]
    [field: SerializeField] public EventReference fountainPress { get; private set; }
    [field: Tooltip("AL-05: dipping the live cable in the puddle.")]
    [field: SerializeField] public EventReference cableDip { get; private set; }
    [field: Tooltip("LO-01: unlocking the mechanic's electrical box.")]
    [field: SerializeField] public EventReference unlockElectricalBox { get; private set; }
    [field: Tooltip("Opening the Ferris wheel's electrical box to cut its power or start it again (a one-shot, 3D at the box).")]
    [field: SerializeField] public EventReference openElectricalBox { get; private set; }
    [field: Tooltip("LO-02: cutting the Ferris wheel's power, or bringing it back (button noises; FerrisWheelAudio, 3D at the wheel, with the power going off or on).")]
    [field: SerializeField] public EventReference cutPower { get; private set; }

    [field: Header("Ferris Wheel")]
    [field: Tooltip("LO-03: the Ferris wheel turning. Hums while it runs (its motor is the Mechanical emitter on the wheel itself).")]
    [field: SerializeField] public EventReference ferrisHydraulic { get; private set; }
    [field: Tooltip("The wheel's machinery shutting down once the power's cut.")]
    [field: SerializeField] public EventReference ferrisPowerOff { get; private set; }
    [field: Tooltip("The stuck cabins creaking once the wheel has stopped, or all the way round with a loosened hinge.")]
    [field: SerializeField] public EventReference ferrisCabinCreak { get; private set; }
    [field: Tooltip("Unlocking a staff gate (FingerprintScanner): the technician's left gate to the hinge, the operator's right gate to the control panel (a one-shot, 3D at the gate).")]
    [field: SerializeField] public EventReference unlockGate { get; private set; }

    [field: Header("Flaming Hot Dogs (HD)")]
    [field: Tooltip("The phone in the ride technician's booth ringing (3D at his booth, loops until he picks up or the call ends).")]
    [field: SerializeField] public EventReference phoneRing { get; private set; }
    [field: Tooltip("The ride technician jolted awake by the hot dog stand blowing up (a one-shot, 3D, following him).")]
    [field: SerializeField] public EventReference technicianAwake { get; private set; }
    [field: Tooltip("Gas hissing out of a loosened propane valve, until it blows (3D at the tank, loops).")]
    [field: SerializeField] public EventReference propaneLeak { get; private set; }
    [field: Tooltip("Turning a propane tank's valve, right before its gas starts leaking (a one-shot, 3D at the tank).")]
    [field: SerializeField] public EventReference propaneValve { get; private set; }
    [field: Tooltip("The hot dog stand blowing up once the hot dog man fires up his grill in the gas (PropaneTank; a one-shot, 3D at the stand). Not the locker room's.")]
    [field: SerializeField] public EventReference hotDogExplosion { get; private set; }
    [field: Tooltip("The hot dog man lighting his grill again, back at his stall after the rats (HotDogMan; a one-shot, 3D at the grill). With the gas round the stand, it's the spark.")]
    [field: SerializeField] public EventReference grillStartUp { get; private set; }
    [field: Tooltip("The ride technician loosening the Ferris wheel's leg hinge with his screwdriver (WheelHinge; a one-shot, 3D at the hinge).")]
    [field: SerializeField] public EventReference loosenHinge { get; private set; }
    [field: Tooltip("The shorted-out electrical box by the lockers buzzing and zapping, until the locker room goes up (LockerBox; 3D at the box, loops).")]
    [field: SerializeField] public EventReference shortedElectricalBox { get; private set; }
    [field: Tooltip("Sparking the broken cable in the locker room's electrical box, whether or not there's propane to catch (LockerBox; a one-shot, 3D at the box).")]
    [field: SerializeField] public EventReference sparkCable { get; private set; }
    [field: Tooltip("The locker room blowing up once the electrician's spark from the broken cable lights its gas (PropaneTank; a one-shot, 3D at the lockers).")]
    [field: SerializeField] public EventReference lockersExplosion { get; private set; }
    [field: Tooltip("Each cooling fan whirring (TankFan: 3D at the fan, loops). Its Fan Direction parameter (South 0, West 1, North 2, East 3, optionally South again at 4) is set to the way the fan faces; a seek modulator on it sweeps the pan when the fan's turned.")]
    [field: SerializeField] public EventReference fan { get; private set; }
    [field: Tooltip("Turning a cooling fan's head a quarter turn (TankFan; a one-shot, 3D at the fan). Empty = the menu click.")]
    [field: SerializeField] public EventReference fanTurn { get; private set; }
    [field: Tooltip("A gutter lid sliding open or dropping shut: the rats' gutter by the hot dog stand, and the smelly one by the mascot (a one-shot, 3D at the gutter).")]
    [field: SerializeField] public EventReference gutterLid { get; private set; }
    [field: Tooltip("The rats squeaking from the moment the lid's open until they're all back down (3D, follows the pack, loops).")]
    [field: SerializeField] public EventReference ratSqueaks { get; private set; }

    [field: Header("Max Speed on Control Panel (MS)")]
    [field: Tooltip("The control panel set to max speed: the lever and the motor revving up (ControlPanel; a one-shot, 3D at the panel). From then on the wheel's own event (Park/Ferris Wheel, the Mechanical emitter) runs wild with its Wheel Speed parameter, 0 at its normal speed up to 1 flat out (FerrisWheelAudio).")]
    [field: SerializeField] public EventReference ferrisMaxSpeed { get; private set; }

    [field: Header("A Wheely Bad Day (the finale)")]
    [field: Tooltip("The wheel shuddering on its hinge at max speed, just before it tears off (FerrisWheel; a one-shot, 3D at the wheel, about 1.5 s).")]
    [field: SerializeField] public EventReference wheelBreaking { get; private set; }
    [field: Tooltip("The wheel tearing off its hinge (FerrisWheel.Detach; a one-shot, 3D at the wheel). The wheel's own hum stops dead under it.")]
    [field: SerializeField] public EventReference wheelCrash { get; private set; }
    [field: Tooltip("One flung cabin hitting the ground (FerrisWheel; a one-shot, 3D where it lands, once per cabin).")]
    [field: SerializeField] public EventReference cabinCrash { get; private set; }
    [field: Tooltip("The runaway wheel rolling (FerrisWheelAudio: 3D, follows the wheel, from tearing off until it starts to topple). Its Roll Speed parameter is 1 flat out, down to 0 as it rocks to a stop.")]
    [field: SerializeField] public EventReference wheelRolling { get; private set; }
    [field: Tooltip("The wheel rocking back and forth where it stopped, before it keels over (FerrisWheel; a one-shot, 3D at the wheel, about 3 s).")]
    [field: SerializeField] public EventReference wheelRocking { get; private set; }
    [field: Tooltip("The wheel keeling over (FerrisWheel; a one-shot, 3D at the wheel, about 3 s until it slams down).")]
    [field: SerializeField] public EventReference wheelTopple { get; private set; }
    [field: Tooltip("The wheel slamming down flat, its bounce about 0.6 s later, and the debris (FerrisWheel; a one-shot, 3D where it lands).")]
    [field: SerializeField] public EventReference wheelSmash { get; private set; }
    [field: Tooltip("A building or prop squashed flat (StandingProps.Squash; a one-shot, 3D at it, once a frame however many go at once).")]
    [field: SerializeField] public EventReference propCrunch { get; private set; }
    [field: Tooltip("A visitor squashed flat (Visitor.Squash; a one-shot, 3D at them, once a frame however many go at once).")]
    [field: SerializeField] public EventReference npcSquished { get; private set; }

    [field: Header("Music and Ambiance (MA)")]
    [field: Tooltip("G-04: the park's theme, from the theme song speakers. Slows and drops in pitch while possessing someone (its global Possessed parameter, PossessedMusic).")]
    [field: SerializeField] public EventReference ruckusMusic { get; private set; }
    [field: Tooltip("G-01: music on the main menu.")]
    [field: SerializeField] public EventReference lobbyMusic { get; private set; }
    [field: Tooltip("The park's crowd, all the time in the park (ParkCrowd: 3D on the player, its Crowd and Panic parameters set from the visitors nearby).")]
    [field: SerializeField] public EventReference parkCrowd { get; private set; }
    [field: Tooltip("Each ice cream stand's jingle, now and then (StandAudio: 3D on the seller; FMOD decides when it plays).")]
    [field: SerializeField] public EventReference iceCreamStall { get; private set; }
    [field: Tooltip("The balloon seller's balloons squeaking, now and then (StandAudio: 3D on the seller; FMOD decides when it plays).")]
    [field: SerializeField] public EventReference balloonSeller { get; private set; }

    [field: Header("NPC")]
    [field: Tooltip("NPC-03: curious.")]
    [field: SerializeField] public EventReference npcCurious { get; private set; }
    [field: Tooltip("Visitors running: panicking, or the tutorial's \"Gotta go!\" dash to the toilet (3D, follows them, started again while they keep running). Empty = npcFootsteps.")]
    [field: SerializeField] public EventReference npcRun { get; private set; }
    [field: Tooltip("NPC-01: a visitor screaming as they start to panic and run (Visitor.Panic; one of several at random, following them).")]
    [field: SerializeField] public EventReference npcScream { get; private set; }
    [field: Tooltip("The riders stuck in the Ferris wheel's cabins screaming during its payoffs (power cut, wobbly), once per shout bubble (a one-shot, 3D, following the cabin). Empty or not in the banks = NPC-01.")]
    [field: SerializeField] public EventReference cabinScream { get; private set; }
    [field: Tooltip("The mascot finally warning the spirit off after its tries to possess him (Mascot.Tease's close-up; a one-shot, 3D, following him).")]
    [field: SerializeField] public EventReference mascotEasterEgg { get; private set; }
    [field: Tooltip("The mascot gagging on the smelly gutter's stink as he runs off, leaving the fan unguarded (Mascot; a one-shot, 3D, following him).")]
    [field: SerializeField] public EventReference mascotGagging { get; private set; }
    [field: Tooltip("NPC-02: surprised or bothered.")]
    [field: SerializeField] public EventReference npcSurprised { get; private set; }
    [field: Tooltip("Visitors and staff walking on their own (3D, optional Pace parameter: 0 walk, 1 run).")]
    [field: SerializeField] public EventReference npcFootsteps { get; private set; }
    [field: Tooltip("The announcer's garbled PA voice, once per announcement bubble (a one-shot, 3D, following the announcer).")]
    [field: SerializeField] public EventReference paAnnouncer { get; private set; }
    [field: Tooltip("The ride technician snoring at his booth while he naps (3D at his booth; FMOD spaces the snores out, it runs until he wakes).")]
    [field: SerializeField] public EventReference technicianSnore { get; private set; }
    [field: Tooltip("The construction workers' barriers going up round a scene after an incident, or round a job (ConstructionTape; a one-shot, 3D in the middle of the ring).")]
    [field: SerializeField] public EventReference constructionSign { get; private set; }

    [field: Header("Ruckus (RU)")]
    [field: Tooltip("RU-01: Ruckus's thought bubble pops up.")]
    [field: SerializeField] public EventReference chatBubble { get; private set; }
    [field: Tooltip("RU-02: the spirit drifting around. Plays the whole time it's out of a body; its Roaming parameter is 1 while it moves, 0 while it stands still.")]
    [field: SerializeField] public EventReference spiritHover { get; private set; }
    [field: Tooltip("RU-03: diving into someone's body.")]
    [field: SerializeField] public EventReference possess { get; private set; }
    [field: Tooltip("RU-04: leaving a body.")]
    [field: SerializeField] public EventReference release { get; private set; }
    [field: Tooltip("RU-05: footsteps of a possessed visitor, and of any special character below whose slot is empty.")]
    [field: SerializeField] public EventReference hostFootsteps { get; private set; }
    [field: Tooltip("RU-07: footsteps while possessing the Mechanic. Empty = RU-05.")]
    [field: SerializeField] public EventReference mechanicFootsteps { get; private set; }
    [field: Tooltip("RU-06: footsteps while possessing the Cable Man. Empty = RU-05.")]
    [field: SerializeField] public EventReference cableManFootsteps { get; private set; }
    [field: Tooltip("RU-08: footsteps while possessing the Hot Dog Man. Empty = RU-05.")]
    [field: SerializeField] public EventReference hotDogManFootsteps { get; private set; }
    [field: Tooltip("RU-09: footsteps while possessing the Ride Technician. Empty = RU-05.")]
    [field: SerializeField] public EventReference technicianFootsteps { get; private set; }

    [field: Header("Toilet Trouble (TT)")]
    [field: Tooltip("TT-06: the toilet geyser.")]
    [field: SerializeField] public EventReference toiletGeyser { get; private set; }
    [field: Tooltip("TT-04: cranking up the valve (3 variations).")]
    [field: SerializeField] public EventReference valveCrank { get; private set; }
    [field: Tooltip("TT-01: the camera zooms into Ruckus: cheeky sounds, Ruckus laughs.")]
    [field: SerializeField] public EventReference tutorialZoomIn { get; private set; }
    [field: Tooltip("TT-03: taking down the Out of Order sign.")]
    [field: SerializeField] public EventReference outOfOrderSign { get; private set; }
    [field: Tooltip("TT-05: an NPC goes into the closet.")]
    [field: SerializeField] public EventReference closetEnter { get; private set; }

    [field: Header("User Interface (UI)")]
    [field: Tooltip("S-06: the Back button (or Esc) in the settings.")]
    [field: SerializeField] public EventReference backButton { get; private set; }
    [field: Tooltip("Yes in Load Game's Delete Slot: the save is deleted.")]
    [field: SerializeField] public EventReference deleteSaveFile { get; private set; }
    [field: Tooltip("G-06: trying something you can't do right now.")]
    [field: SerializeField] public EventReference disabledAction { get; private set; }
    [field: Tooltip("A menu option that can't change: Language in Options (English is the only one).")]
    [field: SerializeField] public EventReference disabledMenu { get; private set; }
    [field: Tooltip("TT-02: holding Space to skip the tutorial (a filling-up sound; cut off if let go early).")]
    [field: SerializeField] public EventReference skipTutorialHold { get; private set; }
    [field: Tooltip("G-03: picking a menu item.")]
    [field: SerializeField] public EventReference menuClick { get; private set; }
    [field: Tooltip("G-02: moving between menu items.")]
    [field: SerializeField] public EventReference menuHover { get; private set; }
    [field: Tooltip("S-04: changing a setting (a volume up or down, On/Off, resolution, graphics, screen shake).")]
    [field: SerializeField] public EventReference toggle { get; private set; }
    [field: Tooltip("A volume slider in Options moving: while it's dragged (each time its number changes, not too often), and each tenth with the keys. Empty = the keys play the toggle sound.")]
    [field: SerializeField] public EventReference volumeSlider { get; private set; }
    [field: Tooltip("Starting a game: New Game on the main menu, or picking a slot in Load Game.")]
    [field: SerializeField] public EventReference newOrLoadGame { get; private set; }
    [field: Tooltip("G-07: \"Objective Completed!\" pops up after a part is done. Everything else is turned down while it plays.")]
    [field: SerializeField] public EventReference objectiveCompleted { get; private set; }
    [field: Tooltip("A step on the objectives list getting crossed out.")]
    [field: SerializeField] public EventReference taskDone { get; private set; }
    [field: Tooltip("Clicking the objectives title (or its key) to fold the list up or drop it down. Its Objective List parameter is set to how the list was when clicked: Shown plays the lower collapse sound, Hidden the expand one.")]
    [field: SerializeField] public EventReference objectiveList { get; private set; }
    [field: Tooltip("S-07: the pause menu's Save Game button.")]
    [field: SerializeField] public EventReference saveButton { get; private set; }
    [field: Tooltip("S-05: Esc opens the pause menu.")]
    [field: SerializeField] public EventReference pauseMenu { get; private set; }

    public static FMODEvents instance { get; private set; }

    private void Awake()
    {
        if (instance != null && instance != this) return; // an extra AudioSystem: AudioManager removes it
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
