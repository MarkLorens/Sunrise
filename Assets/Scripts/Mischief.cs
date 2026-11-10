using System.Collections.Generic;

// The level's sequence, from the level design doc: make a scene big enough to bring the mechanic, then use his keys
// to cut the Ferris wheel's power. Each part is a list of steps; GameHud shows them on Tab and ticks them off as
// the behaviours that make them happen report in. Nothing here drives the order: the world's own rules do.
public static class Mischief
{
    public enum Task { Possess, FloodPuddle, PossessCableMan, Electrocute, LureMechanic, PossessMechanic, UnlockBox, CutPower, CallTechnician }

    public static readonly (string title, (Task task, string label)[] steps)[] Parts =
    {
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

    public static void Complete(Task t)
    {
        if (!done.Add(t)) return;
        Sfx.Play(Sfx.TaskDone, 0.7f);
        foreach (var (_, steps) in Parts)
            foreach (var (task, label) in steps)
                if (task == t)
                {
                    GameHud.Say($"<color=#ffd75e>Step done:</color> {label}", "UI/check_on");
                    return;
                }
    }
}
