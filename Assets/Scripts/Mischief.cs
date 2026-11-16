using System.Collections.Generic;

// The level's sequence, from the level design doc: make a scene big enough to bring the mechanic, then use his keys
// to cut the Ferris wheel's power; then blow up the hot dog stand to wake the ride technician and use him to loosen
// the wheel's hinge. Before it, on a new game, the tutorial's own little prank (Tutorial).
// Each part is a list of steps; GameHud lists the part you're on (top left) and
// ticks them off as the behaviours that make them happen report in. Nothing here drives the
// order: the world's own rules do.
public static class Mischief
{
    // New ones go at the end: saves keep these as numbers.
    public enum Task
    {
        Possess, FloodPuddle, PossessCableMan, Electrocute, LureMechanic, PossessMechanic, UnlockBox, CutPower, CallTechnician,
        RatsOut, StallFanOff, LeakPropane, AimFan, GasCloud, Explode, WakeTechnician,
        PossessTechnician, OpenPlatform, LoosenHinge, RestartWheel,
        TutorialPossess, TutorialSign, TutorialValve, TutorialLeave, TutorialToilet,
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
        ("1. A-leak-tricity", new[]
        {
            (Task.Possess, "Possess a visitor"),
            (Task.FloodPuddle, "Flood the crowd at the speaker with the leaky fountain"),
            (Task.PossessCableMan, "Possess the cable man"),
            (Task.Electrocute, "Dip the broken speaker cable in the puddle"),
            (Task.LureMechanic, "The mechanic comes to fix the fountain"),
        }),
        ("2. Lights out", new[]
        {
            (Task.PossessMechanic, "Possess the mechanic (he has the keys)"),
            (Task.UnlockBox, "Unlock the Ferris wheel's electrical box"),
            (Task.CutPower, "Cut the Ferris wheel's power"),
            (Task.CallTechnician, "The operator calls the ride technician..."),
        }),
        ("3. Flaming hot dogs", new[]
        {
            (Task.RatsOut, "Lift the stinky gutter lid to chase off the hot dog man"),
            (Task.StallFanOff, "Switch off the hot dog man's fan"),
            (Task.LeakPropane, "Loosen the propane tank's valve"),
            (Task.AimFan, "Turn the tank's fan toward the hot dog stand"),
            (Task.GasCloud, "Let the propane cloud up around the stand"),
            (Task.Explode, "The hot dog man comes back and lights his stove..."),
            (Task.WakeTechnician, "The ride technician wakes up and takes the call"),
        }),
        ("4. Loosen the Ferris wheel", new[]
        {
            (Task.PossessTechnician, "Possess the ride technician"),
            (Task.OpenPlatform, "Open the wheel's platform with his fingerprint"),
            (Task.LoosenHinge, "Loosen the leg hinge with his screwdriver"),
            (Task.RestartWheel, "Start the Ferris wheel again at the electrical box"),
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
