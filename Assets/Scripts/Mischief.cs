using System.Collections.Generic;

// The level's sequence, from the level design doc: make a scene big enough to bring the mechanic, then use his keys
// to cut the Ferris wheel's power; then blow up the hot dog stand to wake the ride technician and use him to loosen
// the wheel's hinge. Before it, on a new game, the tutorial's own little prank (Tutorial).
// Each part is a list of steps; GameHud lists the part you're on (top left) and
// ticks them off as the behaviours that make them happen report in. Nothing here drives the
// order: the world's own rules do. The list keeps it short: a step names the task that finishes it, and the tasks
// on the way there (possessing the cable man, unlocking the box...) still count, they're just not listed.
public static class Mischief
{
    // New ones go at the end: saves keep these as numbers.
    public enum Task
    {
        Possess, FloodPuddle, PossessCableMan, Electrocute, LureMechanic, PossessMechanic, UnlockBox, CutPower, CallTechnician,
        RatsOut, StallFanOff, LeakPropane, AimFan, GasCloud, Explode, WakeTechnician,
        PossessTechnician, OpenPlatform, LoosenHinge, RestartWheel,
        TutorialPossess, TutorialSign, TutorialValve, TutorialLeave, TutorialToilet,
        SmellMascot, LeakLockerGas, AimLockerFan, LockerGasCloud, SparkLocker, LockerFire,
        PossessOperator, OpenBooth, MaxSpeed, WheelRolled,
        ReachBox,
    }

    // The tutorial's steps: done for you when it's skipped, or when a saved game is loaded.
    public static readonly Task[] TutorialTasks =
        { Task.TutorialPossess, Task.TutorialSign, Task.TutorialValve, Task.TutorialLeave, Task.TutorialToilet };

    public static readonly (string title, (Task task, string label)[] steps)[] Parts =
    {
        ("Toilet Trouble", new[]
        {
            (Task.TutorialPossess, "Possess a visitor"),
            (Task.TutorialSign, "Take down the Out of Order sign"),
            (Task.TutorialValve, "Crank up the toilet's water valve"),
            (Task.TutorialLeave, "Leave the body"),
            (Task.TutorialToilet, "Wait for someone to use the toilet..."),
        }),
        // After the park tour (ParkTour). Done by getting to the box (ElectricalBox), which starts the puzzle.
        ("0. Ferris Wheel Chaos", new[]
        {
            (Task.ReachBox, "Go to the Ferris Wheel and open the electrical box"),
        }),
        ("1. A-Leak-Tricity", new[]
        {
            (Task.LureMechanic, "Find a way to lure The Mechanic."),
            (Task.CutPower, "Open the electrical box and turn off the Ferris Wheel."),
        }),
        // Shows once the cutscene of the wheel's power going out is over (GameHud waits for cutscenes).
        ("2. Flaming Hot Dogs", new[]
        {
            (Task.WakeTechnician, "Wake up The Ride Technician with a BANG!"),
            (Task.LoosenHinge, "Loosen the Ferris Wheel hinge."),
        }),
        ("3. Wobbly Wheel", new[]
        {
            (Task.RestartWheel, "Start the Ferris Wheel again."),
        }),
        ("4. Burn the Locker Room", new[]
        {
            (Task.SmellMascot, "Stink out The Mascot."),
            (Task.LockerFire, "Set the locker room on fire."),
        }),
        ("5. Max Speed", new[]
        {
            (Task.MaxSpeed, "Turn the Ferris Wheel up to max speed."),
            (Task.WheelRolled, "Watch it roll away!"),
        }),
    };

    static readonly HashSet<Task> done = new();

    public static int StepCount
    {
        get
        {
            int n = 0;
            foreach (var (_, steps) in Parts) n += steps.Length;
            return n;
        }
    }

    public static int StepsDone => done.Count;

    public static bool IsDone(Task t) => done.Contains(t);

    public static void Reset() => done.Clear();

    public static IEnumerable<Task> Done => done;

    // Loading a save (SaveGame): these steps are already done, no chime or pop-up.
    public static void Restore(IEnumerable<Task> tasks)
    {
        done.Clear();
        done.UnionWith(tasks);
        done.UnionWith(TutorialTasks); // a saved game is past the tutorial
        // ...and past getting to the box, if anything of the level's been done (saves from before it was a step).
        foreach (var t in tasks)
            if (System.Array.IndexOf(TutorialTasks, t) < 0)
            {
                done.Add(Task.ReachBox);
                break;
            }
    }

    // Skipping the tutorial: its steps count as done, with no chime or pop-up.
    public static void CompleteQuietly(IEnumerable<Task> tasks) => done.UnionWith(tasks);

    public static void Complete(Task t)
    {
        if (!done.Add(t)) return;
        Sfx.Play(Sfx.TaskDone, 0.7f);
        GameHud.StepDone(GameText.StepDone(t)); // the pop-up at the bottom left
    }
}
