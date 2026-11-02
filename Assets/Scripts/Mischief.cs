using System.Collections.Generic;

// The level's objective, from the level design doc: break the Ferris wheel (primary) and find every way to do it
// (secondary). Each way is a list of steps; GameHud shows them on Tab and ticks them off as they happen.
public static class Mischief
{
    public enum Task { Possess, LureMechanic, PossessMechanic, SendAway, LoosenHinge, PossessOperator, Overdrive, FryPanel, PickFight, JamGears, WheelRolled, WheelJammed }

    public static readonly (string title, (Task task, string label)[] steps)[] Methods =
    {
        ("Method 1: Loosen the support", new[]
        {
            (Task.Possess, "Possess a visitor"),
            (Task.LureMechanic, "Bang on the electrical box until the mechanic comes"),
            (Task.PossessMechanic, "Possess the mechanic"),
            (Task.SendAway, "As the mechanic, send the visitor away"),
            (Task.LoosenHinge, "Walk into the closed area and loosen the support screws"),
            (Task.PossessOperator, "Get close to the operator and possess him"),
            (Task.Overdrive, "Start the ride at full speed"),
            (Task.FryPanel, "Optional: pour a drink over the control panel"),
            (Task.WheelRolled, "The wheel breaks loose and rolls away"),
        }),
        ("Method 2: Jam the gears", new[]
        {
            (Task.Possess, "Possess a visitor and queue up close to the operator"),
            (Task.PossessOperator, "Possess the operator"),
            (Task.Overdrive, "Start the ride at full speed, then walk him away"),
            (Task.FryPanel, "Pour a drink over the control panel"),
            (Task.PickFight, "Pick a fight with another visitor to draw the staff away"),
            (Task.JamGears, "Sneak into the closed area and throw a water bottle into the gear box"),
            (Task.WheelJammed, "The wheel stops dead and the cabins fly off"),
        }),
    };

    public static readonly Task[] Endings = { Task.WheelRolled, Task.WheelJammed };

    static readonly HashSet<Task> done = new();

    public static int MethodsFound => (IsDone(Task.WheelRolled) ? 1 : 0) + (IsDone(Task.WheelJammed) ? 1 : 0);

    public static bool IsDone(Task t) => done.Contains(t);

    public static void Reset() => done.Clear();

    public static void Complete(Task t)
    {
        if (!done.Add(t)) return;
        Sfx.Play(Sfx.TaskDone, 0.7f);
        if (System.Array.IndexOf(Endings, t) >= 0) return; // the wheel breaking gets its own banner
        foreach (var (_, steps) in Methods)
            foreach (var (task, label) in steps)
                if (task == t)
                {
                    GameHud.Say($"<color=#ffd75e>Step done:</color> {label}", "UI/check_on");
                    return;
                }
    }
}
