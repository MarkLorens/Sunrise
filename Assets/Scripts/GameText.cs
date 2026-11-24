// Every line the player reads about what they did, in one place, so the wording can be changed here without
// hunting through the rest of the code:
//   Events (top right): one line for each thing the player does, and one for each step of the level done.
//   Refusals: why pressing K didn't work, shown over whatever said no.
// To reword a line, change the text between the quotes. Words in {curly brackets} are filled in by the game:
//   {who}  someone's name as the game knows them ("Visitor", "Electrician", "Technician"...)
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
    public const string Saving = "Saving...";
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
    public const string ScannedFinger = "Gate unlocked";
    // The locked gates in the fences round the technician's platform and the operator's booth (FingerprintScanner)
    public const string GateUnlock = "Unlock the gate";   // in the right body
    public const string GateLocked = "Locked gate";       // in anyone else's
    public const string LoosenedHinge = "You loosened the hinge";
    public const string RestartedWheel = "You started the Ferris wheel";
    public const string OpenedGutter = "You opened the gutter";
    public const string SparkedCable = "You sparked the broken cable";
    public const string MaxedSpeed = "You turned the Ferris wheel up to max speed";

    // "a visitor", "the electrician", "the technician" (staff go by their job; numbers like "Construction Worker 2" are
    // dropped).
    static string Someone(string who)
    {
        who = Name(who).TrimEnd(' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9').ToLowerInvariant();
        return who == "visitor" ? "a visitor" : "the " + who;
    }

    // What players call someone, from their object's name ("Reset Agent 2" is only the crew's name in the code; saves
    // find bodies by the object's name, so that stays).
    public static string Name(string who) =>
        who != null && who.StartsWith("Reset Agent") ? "Construction Worker" + who.Substring("Reset Agent".Length) : who;

    static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    // ---------------------------------------------------------------- pop-up: a step of the level done
    // When a step gets done by something you just did, this line shows instead of the one above.

    public static string StepDone(Mischief.Task t) => t switch
    {
        Mischief.Task.Possess => "Visitor possessed",
        Mischief.Task.FloodPuddle => "Water fountain leaked",
        Mischief.Task.Electrocute => "Visitors electrocuted",
        Mischief.Task.LureMechanic => "Electrician arrived",
        Mischief.Task.PossessMechanic => "Electrician possessed",
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
    public static string StillShaken(string who, int s) => $"{Name(who)} is still shaken ({s}s)";
    public static string TooWeak(int s) => $"Too weak to possess anyone ({s}s)";

    // Touching things
    public const string NeedBody = "You need a body to touch things";
    public const string HandsFull = "Your hands are full";

    // The drinking fountain
    public const string FountainFixed = "Fixed. Not a drop";
    public const string MechanicOnIt = "The electrician's on it";
    public const string CleaningUp = "Not while they're still cleaning up";
    public const string PuddleFull = "The puddle won't spread any further";

    // The live cable
    public const string CableCrackling = "Still crackling from the last zap";
    public const string CableTooFar = "The puddle's too far away for the cable to reach";

    // The Ferris wheel
    public const string OperatorOnly = "Only the operator knows how to work this";
    public const string NoPower = "No power";
    public const string PowerOff = "The power's off";
    public const string BoxLocked = "Locked. The electrician has the keys";

    // The drinking fountain
    public const string FountainBusy = "Still leaking";

    // Flaming hot dogs
    public const string LidOpen = "The rats are already out";
    public const string NobodyToScare = "Nobody around worth scaring";
    public const string ValveOpen = "It's hissing away already";
    public const string ValveSpent = "Nothing left in it";
    public const string FanGuarded = "The hot dog man won't let you touch his fan";

    // The technician's platform and the hinge
    public const string TechnicianOnly = "Locked gate: technician only";
    public const string PlatformOpen = "It's open";
    public const string PlatformLocked = "The platform's gate is locked";
    public const string HingeNeedsTechnician = "Only the technician knows this hinge";
    public const string NeedScrewdriver = "You need a screwdriver";
    public const string HingeLoose = "Loose enough. Any looser and it falls off";
    public const string HingeFirst = "Not yet: that hinge first";
    public const string WheelWobbly = "It's running. Barely";
    public const string OperatorFingerOnly = "Locked gate: operator only";
    public const string GutterOpen = "It's open. The smell's out";
    public const string SignBackShout = "Who took this sign down?! That toilet is OUT OF ORDER!";
    public const string MascotGuards = "The mascot won't let you near the fan";
    public const string CableSparks = "Pretty sparks. Nothing here to burn yet";
    public const string NeedKeys = "Locked. The electrician has the keys";
    public const string AlreadyBurning = "It's already burning";
    public const string PanelNotYet = "The technician has to start the ride first";
    public const string PanelFromBooth = "You have to be in the booth to reach it";
    public const string AlreadyMaxed = "It's already flat out";
    public const string TechnicianStarts = "Only the technician can restart the wheel";

    // ---------------------------------------------------------------- speech bubble: what the spirit thinks

    // The electrical box
    public const string ThinkBoxSpirit = "Ooh. Off switch for the big toy.";                     // first reaching it, as a spirit
    public const string ThinkBoxLocked = "Locked. Of course. Need that Electrician.";           // after the mechanic cutscene
    public const string ThinkBoxMechanic = "And we're in!";                                      // unlocking it as the mechanic

    // The drinking fountain
    // Each time Ruckus comes near before any press (leaves and returns = another line).
    public static readonly string[] ThinkFountainNear =
    {
        "A leak.. I wonder if I can use this",
        "Ooh. A leak. Useful.",
        "That puddle wants friends.",
        "Sloppy maintenance. Lucky me!",
    };
    public static string PickFountainNear() => ThinkFountainNear[UnityEngine.Random.Range(0, ThinkFountainNear.Length)];
    public static readonly string[] ThinkFountainPress =                                         // each press in a body, in order
        { "Let's flood this.", "Not enough!", "MORE!", "Dance floor's ready." };

    // The live cable (reaching it, any form)
    // Each time Ruckus comes near while the puddle doesn't reach yet (leave + return = another line).
    public static readonly string[] ThinkCableDry =
    {
        "Maybe that cable needs to go somewhere... wet",
        "Dry cable. Boring. I can fix that.",
        "A live wire? Interesting.",
    };
    public static string PickCableDry() => ThinkCableDry[UnityEngine.Random.Range(0, ThinkCableDry.Length)];
    public const string ThinkCableWet = "Hehehe. Bath time for the wire!";                      // the puddle reaches it

    // Riders stuck in the Ferris wheel's cabins once the power's cut (FerrisWheel picks at random)
    public static readonly string[] CabinScreams =
    {
        "AAAAAH!", "Let us down!", "Why did it stop?!", "MOMMY!",
        "Is it supposed to do that?!", "HELP!", "We're gonna fall!", "I want my money back!",
    };

    // Flaming hot dogs
    // Randomized pool (approach, default state): random line each enter-range while still default
    // (leave + return = another; standing still = once per visit). Same as ThinkFountainNear / ThinkCableDry.
    public static readonly string[] ThinkGutter =
    {
        "Ugh, that stinks. Something's living down there",
        "Smells like a plan.",
        "Something's scurrying. I like it.",
        "Stinky sewer pets. Perfect.",
    };
    public static string PickGutter() => ThinkGutter[UnityEngine.Random.Range(0, ThinkGutter.Length)];
    public const string ThinkRats = "And sausage boy is off! Beautiful.";                           // the rats chase him off
    // Randomized pool on refuse (each time he blocks the fan).
    public static readonly string[] ThinkFanGuarded =
    {
        "He's guarding that fan like it's his firstborn",
        "Won't let me touch it. Rude.",
        "My fan, he says. We'll see.",
    };
    public static string PickFanGuarded() => ThinkFanGuarded[UnityEngine.Random.Range(0, ThinkFanGuarded.Length)];
    // Randomized pool on open (once per play; pick varies across runs / if reopened later).
    public static readonly string[] ThinkValve =
    {
        "Smells like... opportunity",
        "Hiss. I like hiss.",
        "Perfect. Now we need a spark.",
    };
    public static string PickValve() => ThinkValve[UnityEngine.Random.Range(0, ThinkValve.Length)];
    // PropaneTank thinks this once the fan chain first carries the gas all the way.
    public static readonly string[] ThinkFanAimed =
    {
        "Who put a fan next to propane? Legend.",
        "I mean, why'd they even put this here.",
        "Blow it that way",
        "There. Now it helps me.",
    };
    public static string PickFanAimed() => ThinkFanAimed[UnityEngine.Random.Range(0, ThinkFanAimed.Length)];
    public const string ThinkGasBlownAway = "I gotta get that fan off, or no boom.";                // gas reaching the stand with its fan on
    public const string ThinkCloud = "Let's spark that fire now Sausage Boy!"; // the cloud is full
    public const string ThinkCloudGrillOn = "Hook, line, and sinker";                                // the cloud is full round his lit grill
    public const string ThinkBoom = "KABOOM. Good morning!";                                         // after the explosion
    public const string ThinkPlatform = "Only he gets through this gate. Good thing I'm him";        // unlocking it as the technician
    public const string ThinkHinge = "Lefty loosey...";                                               // loosening the hinge
    public const string ThinkWobble = "Hehehe. Structural comedy!";                                   // the wobbly restart

    public static readonly string[] CabinScreamsWobbly =
    {
        "NOW WHAT?", "You ever realize they don't have seatbelts in here?", "Why won't it stop?!", "DADDY!",
        "What are they doing down there?!", "HELP!", "*Prayer Noise*", "I'm gonna throw up!",
    };

    // Burn the locker room
    public const string ThinkMascot = "Who's the big fuzzy idiot guarding the fan?";                // the mascot refuses you
    public const string ThinkStinkGone = "The fuzzball is just as smelly, to be honest.";           // the mascot runs off
    public const string ThinkLockerCloud = "Locker room's full of gas. Now, the electrician and that cable..."; // the cloud is full
    public const string ThinkFire = "Shoddy Construction, really. Now's my chance with the operator"; // after the fire payoff
    public const string ThinkBooth = "His gate, his booth. Don't mind if I do.";                     // unlocking it as the operator
    public const string ThinkMaxSpeed = "Faster... FASTER!";                                          // maxing the wheel
    public const string ThinkRolled = "Wheely sorry folks! Come again!";                                     // the last word, after the wheel's rolled (FerrisWheel)

    // Out loud
    public const string MascotGuard = "*angry mascot noises* (Don't touch the fan!)";
    public const string MascotSilent = "...";
    public static readonly string[] MascotWarnings = { "Leave...", "You do not belong here", "Devil!" };
    public const string MascotStink = "*gagging mascot noises*";
    public const string PAFire = "ATTENTION! FIRE AT THE LOCKER ROOM! ALL STAFF TO THE LOCKER ROOM, NOW!";
    public const string OperatorFire = "FIRE?! Not the lockers, my lunch is in there!";

    // Whoever the spirit just left, coming round (Visitor.EndPossession), one at random
    public static readonly string[] ComingRound =
    {
        "H-Huh..?", "How did I get here?", "What just happened?", "Ugh, dizzy…", "Must’ve been the wind",
    };

    // Construction workers (ResetAgent) standing guard at a scene, in turns (Aftermath)
    public static readonly string[] MoveAlong =
    {
        "Move along!", "Nothing to see here, folks!", "Keep it moving!", "Please step back!",
    };

    // Visitors gossiping after each event, at random near the player (VisitorSpawner). Once an event's happened, its
    // lines join the pool for the rest of the game.
    public static readonly (Mischief.Task after, string[] lines)[] Gossip =
    {
        (Mischief.Task.TutorialToilet, new[]
        {
            "Did you see the toilet EXPLODE?!", "That poor guy... covered head to toe.",
            "I'm holding it till I get home.", "Who turned the water pressure up that high?",
        }),
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

    // The announcer at the information booth, over the PA (VisitorSpawner): like the gossip, each event adds its lines
    // for the rest of the game, and the more uneasy the park (Unease), the more often they come.
    public static readonly (Mischief.Task after, string[] lines)[] Announcements =
    {
        (Mischief.Task.TutorialToilet, new[]
        {
            "Attention guests: the restrooms are temporarily closed. We apologise for any... splashing.",
        }),
        (Mischief.Task.Electrocute, new[]
        {
            "Guests are kindly asked to stay away from the drinking fountains.",
            "Please do not dance near standing water. Thank you.",
            "The dance area is closed until further notice.",
        }),
        (Mischief.Task.CutPower, new[]
        {
            "The Ferris wheel is taking a short break. Riders, please stay seated.",
            "Our technicians are on their way. Please remain calm.",
        }),
        (Mischief.Task.Explode, new[]
        {
            "The hot dog stand is closed for the rest of the evening.",
            "Guests, please keep clear of any gas canisters.",
        }),
        (Mischief.Task.LockerFire, new[]
        {
            "Please stay away from the locker room. The fire department has been called.",
            "There is no cause for alarm. Please continue to enjoy the park.",
        }),
        (Mischief.Task.WheelRolled, new[]
        {
            "Please make your way calmly to the nearest exit.",
            "Remain calm. Everything is under control.",
            "Woohoo Land will be closing early tonight. We hope you had a magical time.",
        }),
    };

    // Visitors crowding the information booth to complain (BoothCrowd); the angry ones join once the park's very uneasy.
    public static readonly string[] Complaints =
    {
        "What's going on?!", "Do something!", "Is anyone in charge here?", "Is it safe here?",
        "Why is nobody telling us anything?", "Where's the manager?", "Hello?! Anyone?!",
    };

    public static readonly string[] AngryComplaints =
    {
        "People are DYING out there!", "Shut the park down!", "I want a refund!", "This place is cursed!",
        "Call the police!", "How is this still open?!",
    };

    // The hot dog man, out loud
    public const string HotDogGuard = "Hey! Hands off my fan!";
    public const string HotDogRats = "RATS! RAAATS!";
    public const string HotDogBack = "Okay... they're gone. Back to work. Who wants a dog?";
    public const string HotDogStove = "Let's fire up the grill!";
    public const string HotDogSmell = "Hm? What's that smell?";   // the gas fills up round his grill while it's lit
    public const string PhoneRing = "*Ring ring*";     // the technician's phone, over his booth
    public const string TechnicianSnore = "zzz...";    // sleeping through it
    public const string TechnicianWake = "WHA- WHAT WAS THAT?!";
    public const string TechnicianStaysIn = "...Ugh. Not my ride, not my problem. If it's important, they'll call.";
    public const string TechnicianCall = "Hello? The wheel's out? Yeah, yeah, on my way...";

    // After the first electrocution, one after the other
    public static readonly string[] ThinkElectrocuted = { "Uh oh, dropped the toaster again", "Perfect. That got his attention!" };

    // ---------------------------------------------------------------- tutorial (new game only)
    // Each step: what the spirit says (speech bubble) and the instruction card at the bottom. [Key] shows that key
    // as a keycap that lights up while it's held, on a line of its own under the sentence; [WASD] shows W A S D.

    // 1. The spirit
    public const string TutorialMeSays = "I woke up and chose violence… recreationally.";
    public const string TutorialMe = "This is you, Ruckus, a young evil spirit.";
    // 2. The objective (top left)
    public const string TutorialGoalSays = "Objectives! Like a recipe… for someone else's bad day.";
    public const string TutorialGoal = "Your mischief to-do list lives up here.";
    // 3. Moving
    public const string TutorialMove = "Float around with [WASD]";
    // 4. Someone to possess
    public const string TutorialVisitorSays = "Hello, stranger. Mind if I drive?";
    public const string TutorialVisitor = "Float over to that visitor.";
    // 5. Possessing
    public const string TutorialPossess = "Press [J] to possess them!";
    // 6. An objective
    public const string TutorialObjective = "Let's complete an objective!";
    // 7. To the toilet
    public const string TutorialWalk = "Walk your new body to the toilet.";
    // 8. The sign
    public const string TutorialSignSays = "Excuse me, sign. You're obstructing a public hazard.";
    public const string TutorialSign = "Press [K] to take down the sign.";
    // 9. The valve
    public const string TutorialValveSays = "Now, crank up this valve.";
    public const string TutorialWalkValve = "Walk over to the water valve.";
    public const string TutorialValve = "Press [K] until the pressure is maxed out.";
    // 10. Leaving the body (and any later exit): sometimes a line, pick one at random when it does
    public const float LeaveBodyThinkChance = 0.4f; // how often leaving triggers a thought (0–1)
    public static readonly string[] LeaveBodySays =
    {
        "Thanks for the ride! Keep the trauma!",
        "You're free! Emotionally? Debatable.",
        "I got what I needed. You can keep the rest of your day.",
        "Thanks! Try not to spiral.",
        "Bye! Don't miss me!",
        "You can have your legs back!",
    };
    public static string PickLeaveBody() => LeaveBodySays[UnityEngine.Random.Range(0, LeaveBodySays.Length)];
    public const string TutorialLeave = "Press [J] to leave the body.";
    // 11. The payoff
    public const string TutorialWatch = "Now sit back and watch...";
    public const string TutorialLaughSays = "A Royal Flush!";
    public const string TutorialGottaGo = "Gotta go, gotta go, GOTTA GO!";                       // the visitor running in
    public const string TutorialSoaked = "AAAH! IT'S EVERYWHERE!";                                  // the soaked visitor
    // 12. The events (top right)
    public const string TutorialEvent = "Everything you pull off shows up here. Have fun!";

    public const string TutorialNext = "Next";        // beside the [Space] keycap, on steps you just read
    public const string TutorialSkip = "Hold [Space] to skip tutorial";

    // After the tutorial: the park tour (ParkTour), and the banner when a part of the objectives is done
    public const string TourSays = "Alright! Opening day, baby!";

    // Visitors in the runaway Ferris wheel's path, the moment it tears loose (FerrisWheel): a few cry out before it
    // flattens them.
    public static readonly string[] WheelDoomed =
    {
        "Is that... coming this way?!",
        "Oh no. Oh no no no NO NO!",
        "Not like this... NOT LIKE THIS!",
        "Tell my mom I loved the churros!",
        "I just wanted cotton candy!",
        "Worst. Ride. EVER!",
        "Mommy, the big wheel's loose!",
    };

    // A visitor having their photo taken with the mascot (Visitor)
    public const string SayCheese = "Say cheese!";
    public static string ObjectiveCompleted(string title) => $"{title}\nCompleted"; // two lines on the cloud, in capitals

    // The toilet's sign and valve
    public const string SignPrompt = "Take down the Out of Order sign";
    public static string ValvePrompt(int turns, int of) => $"Crank up the valve ({turns}/{of})";
    public const string ValveMaxed = "It won't turn any further";

    // The end credits after the finale (EndCredits): the team in order, then the special thanks. Names and roles to
    // be filled in.
    public static readonly (string name, string role)[] EndCreditsTeam =
    {
        ("Kennard Mahib", "Game/Level Designer"), ("Amanda Dorotea", "Game/UI Designer"), ("Danu Raditya", "Artist, Illustrator"), ("Anthony Martin", "Sound Designer"), ("Aulia Agustina", "Programmer"), ("Mark Pardede", "Programmer"),
    };
    public const string EndCreditsThanks = "Special thanks to";
    public static readonly (string name, string role) EndCreditsThanked = ("Ruanth Thijssen", "Our Mentor and Supporter");
    public const string EndCreditsAndYou = "And You.";
    public const string EndCreditsThankYou = "Thank You for Playing!"; // under the studio's logo, the very end

    // The loading screen's progress, in the middle of its Ferris wheel (LoadingScreen)
    public static string LoadingPercent(int percent) => $"{percent}%";
}
