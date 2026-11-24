// Every closed caption's words, in one place to change the wording (Options > General > Closed Caption shows them:
// ClosedCaptions). Each sound the game plays has one, shown where the sound is played (ClosedCaptions.Show for
// one-shots, ClosedCaptions.Hold for sounds that go on, like the Ferris wheel's hum).
// A new sound needs a line here too. Footsteps and the spirit's drifting hum have none: they're there all the time.
public static class Captions
{
    // ---------------------------------------------------------------- music
    public const string Music = "[♪ Upbeat carnival music]";

    // ---------------------------------------------------------------- the spirit
    public const string Possess = "[Ghostly whoosh]";
    public const string Leave = "[Spirit slips out with a whoosh]";
    public const string Refused = "[Error buzz]";

    // ---------------------------------------------------------------- objectives and things in hand
    public const string StepDone = "[Success chime]";
    public const string PickUp = "[Picks something up]";
    public const string DropItem = "[Something clatters to the ground]";

    // ---------------------------------------------------------------- the toilet prank (tutorial)
    public const string SignOff = "[Sign rips off]";
    public const string SignBack = "[Sign clatters back]";
    public const string Valve = "[Valve squeaks]";
    public const string ToiletRumble = "[Toilet rumbling]";
    public const string ToiletGeyser = "[Toilet bursts with a WHOOSH]";

    // ---------------------------------------------------------------- the drinking fountain and the live cable
    public const string FountainPress = "[Water sputters]";
    public const string FaucetDrip = "[Water dripping]";             // the leaky fountain, while you're within earshot
    public const string Electrocution = "[Loud electric crackle]";
    public const string FountainFixed = "[Wrench clanks]";

    // ---------------------------------------------------------------- the Ferris wheel and its box
    public const string BoxUnlocked = "[Lock clicks open]";
    public const string PowerBackOn = "[Power switch clunks on]";
    public const string PowerOff = "[Power cuts out]";
    public const string WheelHum = "[Ferris wheel machinery hums]";  // while it runs, when you're near
    public const string CabinCreak = "[Cabins creaking]";             // stuck, or wobbly, when you're near
    public const string WheelWobble = "[Wheel wobbles with springy boings]"; // running on the loosened hinge, when you're near
    public const string ControlPanel = "[Control panel sparks]";
    public const string HingeLoose = "[Hinge creaks loose]";
    public const string WheelBreaking = "[Wheel groans loudly]";
    public const string WheelCollapse = "[Huge crash]";
    public const string WheelWrecked = "[Wheel smashes down]";
    public const string Flattened = "[Crunch]";

    // ---------------------------------------------------------------- the stands
    public const string IceCreamJingle = "[Ice cream jingle plays]";  // now and then, when you're near
    public const string Balloons = "[Balloons squeak]";               // now and then, when you're near

    // ---------------------------------------------------------------- the hot dog stand, the gutter, the fans
    public const string GutterOpen = "[Gutter lid scrapes open]";
    public const string GutterShut = "[Gutter lid clangs shut]";
    public const string FanTurned = "[Fan clunks round]";
    public const string HotDogManHop = "[Hot dog man scrambles]";
    public const string GasLeak = "[Gas hissing]";
    public const string Explosion = "[Massive explosion]";

    // ---------------------------------------------------------------- the locker room and staff
    public const string CableSpark = "[Cable sparks]";
    public const string PaScreech = "[PA system screeches]";
    public const string PaAnnouncement = "[Garbled PA announcement]";
    public const string Scanner = "[Gate lock clicks open]";
    public const string StaffTapping = "[Tapping and clanking]";
    public const string Startled = "[Startled grunt]";
    public const string PhoneRing = "[Phone ringing]";               // the technician's booth, while it rings
    public const string Snoring = "[Snoring]";                       // the technician napping, when you're near
}
