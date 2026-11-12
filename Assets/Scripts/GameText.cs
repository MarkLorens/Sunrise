// Every line the player reads about what they did, in one place, so the wording can be changed here without
// hunting through the rest of the code:
//   Pop-up (bottom left): one line for each thing the player does, and one for each step of the level done.
//   Refusals: why pressing E didn't work, shown over whatever said no.
// To reword a line, change the text between the quotes. Words in {curly brackets} are filled in by the game:
//   {who}  someone's name as the game knows them ("Visitor", "Mechanic", "Cable Man"...)
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
    public static string PickedUp(string what) => $"You picked up the {what}";
    public static string Threw(string what) => $"You threw the {what}";
    public static string Dropped(string what) => $"You dropped the {what}";
    public static string Bought(string what) => $"You bought a {what}";
    public static string PickedFight(string who) => $"You picked a fight with {Someone(who)}";
    public static string ToldToMoveAlong(string who) => $"You told {Someone(who)} to move along";
    public const string PressedFountain = "The fountain leaks even more";
    public const string DippedCable = "You dipped the cable in the puddle";
    public const string UnlockedBox = "You unlocked the electrical box";
    public const string CutPower = "You cut the Ferris wheel's power";
    public const string BoardedRide = "You got on the Ferris wheel";

    // "a visitor", "the mechanic", "the cable man" (staff go by their job; numbers like "Reset Agent 2" are dropped).
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
        Mischief.Task.PossessCableMan => "Cable man possessed",
        Mischief.Task.Electrocute => "Visitors electrocuted",
        Mischief.Task.LureMechanic => "Mechanic arrived",
        Mischief.Task.PossessMechanic => "Mechanic possessed",
        Mischief.Task.UnlockBox => "Electrical box unlocked",
        Mischief.Task.CutPower => "Ferris wheel power cut",
        Mischief.Task.CallTechnician => "Technician called",
        _ => t.ToString(),
    };

    // ---------------------------------------------------------------- refusals: why E didn't work

    // Possessing someone
    public static string CantBePossessed(string who) => $"{who} can't be possessed";
    public static string StillShaken(string who, int s) => $"{who} is still shaken ({s}s)";
    public static string TooWeak(int s) => $"Too weak to possess anyone ({s}s)";

    // Touching things
    public const string NeedBody = "You need a body to touch things";
    public const string HandsFull = "Your hands are full";
    public static string Busy(string who) => $"{who} is busy";
    public const string CantFight = "You're in no state to fight";

    // The drinking fountain
    public const string FountainFixed = "Fixed. Not a drop";
    public const string MechanicOnIt = "The mechanic's on it";
    public const string PuddleFull = "The puddle won't spread any further";

    // The live cable
    public const string CableLive = "It's live! Only the cable man knows how to handle it";
    public const string CableCrackling = "Still crackling from the last zap";
    public const string CableTooFar = "The puddle's too far away for the cable to reach";

    // The Ferris wheel
    public const string StaffCantRide = "Staff can't ride while they're on duty";
    public const string RideStopped = "The ride's stopped";
    public const string NobodyRunningRide = "Nobody's running the ride";
    public const string OperatorOnly = "Only the operator knows how to work this";
    public const string NoPower = "No power";
    public const string PowerOff = "The power's off";
    public const string BoxLocked = "Locked. The mechanic has the keys";
}
