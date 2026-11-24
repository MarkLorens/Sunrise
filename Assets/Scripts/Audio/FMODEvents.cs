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
    [field: Tooltip("AL-03: the leaky fountain dripping, until the mechanic fixes it (3D, loops).")]
    [field: SerializeField] public EventReference waterLeak { get; private set; }
    [field: Tooltip("AL-02: pressing the leaky fountain's button.")]
    [field: SerializeField] public EventReference fountainPress { get; private set; }
    [field: Tooltip("AL-05: dipping the live cable in the puddle.")]
    [field: SerializeField] public EventReference cableDip { get; private set; }
    [field: Tooltip("LO-01: unlocking the mechanic's electrical box.")]
    [field: SerializeField] public EventReference unlockElectricalBox { get; private set; }
    [field: Tooltip("LO-02: cutting the Ferris wheel's power (button noises).")]
    [field: SerializeField] public EventReference cutPower { get; private set; }

    [field: Header("Ferris Wheel")]
    [field: Tooltip("LO-03: the Ferris wheel turning. Hums while it runs (its motor is the Mechanical emitter on the wheel itself).")]
    [field: SerializeField] public EventReference ferrisHydraulic { get; private set; }
    [field: Tooltip("The wheel's machinery shutting down once the power's cut.")]
    [field: SerializeField] public EventReference ferrisPowerOff { get; private set; }
    [field: Tooltip("The stuck cabins creaking once the wheel has stopped, or all the way round with a loosened hinge.")]
    [field: SerializeField] public EventReference ferrisCabinCreak { get; private set; }

    [field: Header("Max Speed on Control Panel (MS)")]
    [field: Tooltip("The control panel set to max speed.")]
    [field: SerializeField] public EventReference ferrisMaxSpeed { get; private set; }

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
    [field: Tooltip("NPC-02: surprised or bothered.")]
    [field: SerializeField] public EventReference npcSurprised { get; private set; }
    [field: Tooltip("Visitors and staff walking on their own (3D, optional Pace parameter: 0 walk, 1 run).")]
    [field: SerializeField] public EventReference npcFootsteps { get; private set; }

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
    [field: Tooltip("Starting a game: New Game on the main menu, or picking a slot in Load Game.")]
    [field: SerializeField] public EventReference newOrLoadGame { get; private set; }
    [field: Tooltip("G-07: \"Objective Completed!\" pops up after a part is done. Everything else is turned down while it plays.")]
    [field: SerializeField] public EventReference objectiveCompleted { get; private set; }
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
