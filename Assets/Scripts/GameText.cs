// Every line the player reads about what they did, in one place, so the wording can be changed here without
// hunting through the rest of the code:
//   Pop-up (bottom left): one line for each thing the player does, and one for each step of the level done.
//   Refusals: why pressing K didn't work, shown over whatever said no.
// To reword a line, change the text between the quotes. Words in {curly brackets} are filled in by the game:
//   {who}  someone's name as the game knows them ("Visitor", "Mechanic", "Technician"...)
//   {what} a thing ("drink", "keys", "water bottle")
//   {s}    seconds
// Plain C# strings, so nothing is loaded or read at runtime; a broken line shows up as a compile error.
public static class GameText
{
    // ---------------------------------------------------------------- pop-up: things you do

    // As a spirit
    public static string Possessed(string who) => $"{Capital(Someone(who))} is possessed";

    // In someone's body
    public const string LeftBody = "You left the body";

    // The pause menu's Save Game
    public const string Saved = "Game saved";
    public const string CantSave = "Can't save right now";
    public static string Bought(string what) => $"You bought a {what}";
    public const string PressedFountain = "The fountain leaks even more";
    public const string DippedCable = "You dipped the cable in the puddle";
    public const string UnlockedBox = "You unlocked the electrical box";
    public const string CutPower = "You cut the Ferris wheel's power";
    public const string LiftedLid = "You lifted the gutter lid";
    public const string OpenedValve = "You loosened the propane valve";
    public const string SwitchedOffFan = "You switched off the hot dog man's fan";
    public static string TurnedFan(string way) => $"You turned the fan to face {way}";
    public const string ScannedFinger = "Fingerprint accepted";
    public const string LoosenedHinge = "You loosened the hinge";
    public const string RestartedWheel = "You started the Ferris wheel";
    public const string OpenedGutter = "You opened the gutter";
    public const string SparkedCable = "You sparked the broken cable";
    public const string MaxedSpeed = "You turned the Ferris wheel up to max speed";

    // "a visitor", "the mechanic", "the technician" (staff go by their job; numbers like "Reset Agent 2" are dropped).
    static string Someone(string who)
    {
        who = who.TrimEnd(' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9').ToLowerInvariant();
        return who == "visitor" ? "a visitor" : "the " + who;
    }

    static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    // ---------------------------------------------------------------- pop-up: a step of the level done
    // When a step gets done by something you just did, this line shows instead of the one above.

    public static string StepDone(Mischief.Task t) => t switch
    {
        Mischief.Task.Possess => "Visitor possessed",
        Mischief.Task.FloodPuddle => "Water fountain leaked",
        Mischief.Task.Electrocute => "Visitors electrocuted",
        Mischief.Task.LureMechanic => "Mechanic arrived",
        Mischief.Task.PossessMechanic => "Mechanic possessed",
        Mischief.Task.UnlockBox => "Electrical box unlocked",
        Mischief.Task.CutPower => "Ferris wheel power cut",
        Mischief.Task.CallTechnician => "Technician called",
        Mischief.Task.RatsOut => "Hot dog man chased off",
        Mischief.Task.StallFanOff => "Stall fan off",
        Mischief.Task.LeakPropane => "Propane leaking",
        Mischief.Task.AimFan => "Fan turned",
        Mischief.Task.GasCloud => "Hot dog stand gassed up",
        Mischief.Task.Explode => "KABOOM",
        Mischief.Task.WakeTechnician => "Technician awake",
        Mischief.Task.PossessTechnician => "Technician possessed",
        Mischief.Task.OpenPlatform => "Platform unlocked",
        Mischief.Task.LoosenHinge => "Hinge loosened",
        Mischief.Task.RestartWheel => "Ferris wheel running... sort of",
        Mischief.Task.SmellMascot => "Mascot stunk out",
        Mischief.Task.LeakLockerGas => "Propane leaking",
        Mischief.Task.AimLockerFan => "Fan turned",
        Mischief.Task.LockerGasCloud => "Locker room gassed up",
        Mischief.Task.SparkLocker => "Spark!",
        Mischief.Task.LockerFire => "Locker room on fire",
        Mischief.Task.PossessOperator => "Operator possessed",
        Mischief.Task.OpenBooth => "Booth unlocked",
        Mischief.Task.MaxSpeed => "MAX SPEED",
        Mischief.Task.WheelRolled => "The Ferris wheel is loose!",
        Mischief.Task.TutorialPossess => "Visitor possessed",
        Mischief.Task.TutorialSign => "Out of Order sign gone",
        Mischief.Task.TutorialValve => "Water pressure: MAX",
        Mischief.Task.TutorialLeave => "Left the body",
        Mischief.Task.TutorialToilet => "Toilet geyser!",
        Mischief.Task.ReachBox => "Found the electrical box",
        _ => t.ToString(),
    };

    // ---------------------------------------------------------------- refusals: why K didn't work

    // Possessing someone
    public static string CantBePossessed(string who) => $"{who} can't be possessed";
    public static string StillShaken(string who, int s) => $"{who} is still shaken ({s}s)";
    public static string TooWeak(int s) => $"Too weak to possess anyone ({s}s)";

    // Touching things
    public const string NeedBody = "You need a body to touch things";
    public const string HandsFull = "Your hands are full";

    // The drinking fountain
    public const string FountainFixed = "Fixed. Not a drop";
    public const string MechanicOnIt = "The mechanic's on it";
    public const string PuddleFull = "The puddle won't spread any further";

    // The live cable
    public const string CableCrackling = "Still crackling from the last zap";
    public const string CableTooFar = "The puddle's too far away for the cable to reach";

    // The Ferris wheel
    public const string OperatorOnly = "Only the operator knows how to work this";
    public const string NoPower = "No power";
    public const string PowerOff = "The power's off";
    public const string BoxLocked = "Locked. The mechanic has the keys";

    // The drinking fountain
    public const string FountainBusy = "Still gurgling";

    // Flaming hot dogs
    public const string LidOpen = "The rats are already out";
    public const string NobodyToScare = "Nobody around worth scaring";
    public const string ValveOpen = "It's hissing away already";
    public const string ValveSpent = "Nothing left in it";
    public const string FanGuarded = "The hot dog man won't let you touch his fan";

    // The technician's platform and the hinge
    public const string TechnicianOnly = "Fingerprint lock: technician only";
    public const string PlatformOpen = "It's open";
    public const string PlatformLocked = "The platform's locked: fingerprint only";
    public const string HingeNeedsTechnician = "Only the technician knows this hinge";
    public const string NeedScrewdriver = "You need a screwdriver";
    public const string HingeLoose = "Loose enough. Any looser and it falls off";
    public const string HingeFirst = "Not yet: that hinge first";
    public const string WheelWobbly = "It's running. Barely";
    public const string OperatorFingerOnly = "Fingerprint lock: operator only";
    public const string GutterOpen = "It's open. The smell's out";
    public const string MascotGuards = "The mascot won't let you near the fan";
    public const string CableSparks = "Pretty sparks. Nothing here to burn yet";
    public const string NeedKeys = "Locked. The mechanic has the keys";
    public const string AlreadyBurning = "It's already burning";
    public const string PanelNotYet = "The technician has to start the ride first";
    public const string PanelFromBooth = "You have to be in the booth to reach it";
    public const string AlreadyMaxed = "It's already flat out";
    public const string TechnicianStarts = "Only the technician can restart the wheel";

    // ---------------------------------------------------------------- speech bubble: what the spirit thinks

    // The electrical box
    public const string ThinkBoxSpirit = "This must be how I turn the Ferris Wheel off.";       // first reaching it, as a spirit
    public const string ThinkBoxLocked = "Ah that brat locked it. I'll get his attention alright!"; // after the mechanic cutscene
    public const string ThinkBoxMechanic = "There you freaking go";                              // unlocking it as the mechanic

    // The drinking fountain
    public const string ThinkFountainNear = "A leak.. I wonder if I can use this";               // first reaching it, in any form
    public static readonly string[] ThinkFountainPress =                                         // each press in a body, in order
        { "Now let's make this bigger", "A bit more", "MORE!", "That should cover it" };

    // The live cable (reaching it, any form)
    public const string ThinkCableDry = "Maybe that cable needs to go somewhere... wet";          // the puddle doesn't reach it yet
    public const string ThinkCableWet = "Hehehe let's get buzzed";                               // the puddle reaches it

    // Riders stuck in the Ferris wheel's cabins once the power's cut (FerrisWheel picks at random)
    public static readonly string[] CabinScreams =
    {
        "AAAAAH!", "Let us down!", "Why did it stop?!", "MOMMY!",
        "Is it supposed to do that?!", "HELP!", "We're gonna fall!", "I want my money back!",
    };

    // Flaming hot dogs
    public const string ThinkGutter = "Ugh, that stinks. Something's living down there";           // first reaching the gutter, any form
    public const string ThinkRats = "Run, sausage boy, run!";                                       // the rats chase him off
    public const string ThinkFanGuarded = "He's guarding that fan like it's his firstborn";        // refused by the hot dog man
    public const string ThinkValve = "Smells like... opportunity";                                   // opening the valve
    public const string ThinkFanAimed = "I mean, why'd they even put this here.";                                          // turning the tank's fan
    public const string ThinkGasBlownAway = "His fan keeps blowing the gas away. That has to go";   // gas reaching the stand with its fan on
    public const string ThinkCloud = "And now. We wait...";            // the cloud is full
    public const string ThinkBoom = "Well, THAT woke someone up";                                    // after the explosion
    public const string ThinkPlatform = "Only his fingers open this. Good thing I have them";        // scanning as the technician
    public const string ThinkHinge = "Lefty loosey...";                                               // loosening the hinge
    public const string ThinkWobble = "Hehe. Enjoy the ride";                                         // the wobbly restart

    public static readonly string[] CabinScreamsWobbly =
    {
        "NOW WHAT?", "You ever realize they don't have seatbelts in here?", "Why won't it stop?!", "DADDY!",
        "What are they doing down there?!", "HELP!", "*Prayer Noise*", "I'm gonna throw up!",
    };

    // Burn the locker room
    public const string ThinkMascot = "Who's the big fuzzy idiot guarding the fan?";                // the mascot refuses you
    public const string ThinkStinkGone = "The fuzzball is just as smelly, to be honest.";           // the mascot runs off
    public const string ThinkLockerCloud = "Locker room's full of gas. Now, the mechanic and that cable..."; // the cloud is full
    public const string ThinkFire = "Shoddy Construction, really. Now's my chance with the operator"; // after the fire payoff
    public const string ThinkBooth = "His own finger. How trusting";                                  // scanning as the operator
    public const string ThinkMaxSpeed = "Faster... FASTER!";                                          // maxing the wheel
    public const string ThinkRolled = "Fare-thee-wheel... Hehehe";                                    // after the wheel's rolled

    // Out loud
    public const string MascotGuard = "*angry mascot noises* (Don't touch the fan!)";
    public const string MascotStink = "*gagging mascot noises*";
    public const string PAFire = "ATTENTION! FIRE AT THE LOCKER ROOM! ALL STAFF TO THE LOCKER ROOM, NOW!";
    public const string OperatorFire = "FIRE?! Not the lockers, my lunch is in there!";

    // Reset Agents standing guard at a scene, in turns (Aftermath, ResetAgent)
    public static readonly string[] MoveAlong =
    {
        "Move along!", "Nothing to see here, folks!", "Keep it moving!", "Please step back!",
    };

    // Visitors gossiping after each event, at random near the player (VisitorSpawner). Once an event's happened, its
    // lines join the pool for the rest of the game.
    public static readonly (Mischief.Task after, string[] lines)[] Gossip =
    {
        (Mischief.Task.Electrocute, new[]
        {
            "Did you see what happened at the dance spot?", "Don't go near the drinking fountains.",
            "Those people were just dancing...", "I'm not standing in any puddles today.",
        }),
        (Mischief.Task.CutPower, new[]
        {
            "The Ferris wheel just stopped with people on it!", "I can hear them screaming up there.",
            "Glad I skipped the Ferris wheel.",
        }),
        (Mischief.Task.Explode, new[]
        {
            "The hot dog stand EXPLODED!", "I'm never eating a hot dog again.", "Was that a gas leak?",
        }),
        (Mischief.Task.LockerFire, new[]
        {
            "The locker room's on fire!", "Is this park cursed or something?", "Where's the fire department?",
        }),
        (Mischief.Task.WheelRolled, new[]
        {
            "THE WHEEL JUST ROLLED AWAY!", "I want my money back!", "We need to get out of here!",
        }),
    };

    // The hot dog man, out loud
    public const string HotDogGuard = "Hey! Hands off my fan!";
    public const string HotDogRats = "RATS! RAAATS!";
    public const string HotDogBack = "Okay... they're gone. Back to work. Who wants a dog?";
    public const string HotDogStove = "Let's fire up the grill!";
    public const string PhoneRing = "*Ring ring*";     // the technician's phone, over his booth
    public const string TechnicianSnore = "zzz...";    // sleeping through it
    public const string TechnicianWake = "WHA- WHAT WAS THAT?!";
    public const string TechnicianCall = "Hello? The wheel's out? Yeah, yeah, on my way...";

    // After the first electrocution, one after the other
    public static readonly string[] ThinkElectrocuted = { "Uh oh, dropped the toaster again", "Anyways, that should get the brat's attention" };

    // ---------------------------------------------------------------- tutorial (new game only)
    // Each step: what the spirit says (speech bubble) and the instruction card at the bottom. [Key] shows that key
    // as a keycap that lights up while it's held, on a line of its own under the sentence; [WASD] shows W A S D.

    // 1. The spirit
    public const string TutorialMeSays = "I'm so ready to have fun!";
    public const string TutorialMe = "This little evil spirit is you.";
    // 2. The objective (top left)
    public const string TutorialGoalSays = "Ah! This is what I should do!";
    public const string TutorialGoal = "Your mischief to-do list lives up here.";
    // 3. Moving
    public const string TutorialMove = "Float around with [WASD]";
    // 4. Someone to possess
    public const string TutorialVisitorSays = "I want to possess someone.";
    public const string TutorialVisitor = "Float over to that visitor.";
    // 5. Possessing
    public const string TutorialPossess = "Press [J] to possess them!";
    // 6. An objective
    public const string TutorialObjective = "Let's complete an objective!";
    // 7. To the toilet
    public const string TutorialWalk = "Walk your new body to the toilet.";
    // 8. The sign
    public const string TutorialSignSays = "Hihihi, let's remove this.";
    public const string TutorialSign = "Press [K] to take down the sign.";
    // 9. The valve
    public const string TutorialValveSays = "Now, crank up this valve.";
    public const string TutorialValve = "Press [K] until the pressure is maxed out.";
    // 10. Leaving the body
    public const string TutorialLeaveSays = "Alright, this guy is useless to me now.";
    public const string TutorialLeave = "Press [J] to leave the body.";
    // 11. The payoff
    public const string TutorialWatch = "Now sit back and watch...";
    public const string TutorialLaughSays = "MWAHAHAHA!";
    public const string TutorialGottaGo = "Gotta go, gotta go, GOTTA GO!";                       // the visitor running in
    public const string TutorialSoaked = "AAAH! IT'S EVERYWHERE!";                                  // the soaked visitor
    // 12. The pop-up (bottom left)
    public const string TutorialEvent = "Everything you pull off shows up down here. Have fun!";

    public const string TutorialNext = "Next";        // beside the [Space] keycap, on steps you just read
    public const string TutorialSkip = "Hold [Space] to skip tutorial";

    // After the tutorial: the park tour (ParkTour), and the banner when a part of the objectives is done
    public const string TourSays = "Let's get the show started with that ferris wheel";
    public const string ObjectiveCompleted = "Objective Completed!";

    // The toilet's sign and valve
    public const string SignPrompt = "Take down the Out of Order sign";
    public static string ValvePrompt(int turns, int of) => $"Crank up the valve ({turns}/{of})";
    public const string ValveMaxed = "It won't turn any further";
}
