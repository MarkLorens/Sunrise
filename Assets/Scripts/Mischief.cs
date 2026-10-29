using System.Collections.Generic;

// The spirit's to-do list of mischief (a nod to Untitled Goose Game). GameHud shows it on Tab.
public static class Mischief
{
    public enum Task { Possess, Swipe, ThrowHit, CrowdPanic, SlipGuard, Brawl, FeedOperator, LockToilet, Overdrive, WreckWheel, HarvestSouls }

    public static readonly (Task task, string label)[] List =
    {
        (Task.Possess, "Possess a visitor"),
        (Task.Swipe, "Swipe what a visitor is carrying"),
        (Task.ThrowHit, "Hit someone with a thrown object"),
        (Task.CrowdPanic, "Make 3 visitors panic at once"),
        (Task.SlipGuard, "Slip away from a guard who spotted you"),
        (Task.Brawl, "Start a brawl"),
        (Task.FeedOperator, "Give the operator something spicy"),
        (Task.LockToilet, "Lock someone in the toilet"),
        (Task.Overdrive, "Make the Ferris wheel spin too fast"),
        (Task.WreckWheel, "Wreck the Ferris wheel"),
        (Task.HarvestSouls, "Harvest 3 souls"),
    };

    static readonly HashSet<Task> done = new();

    public static int DoneCount => done.Count;

    public static bool IsDone(Task t) => done.Contains(t);

    public static void Reset() => done.Clear();

    public static void Complete(Task t)
    {
        if (!done.Add(t)) return;
        foreach (var (task, label) in List)
            if (task == t) GameHud.Say($"<color=#ffd75e>Mischief done:</color> {label}!", "UI/check_on");
        Sfx.Play(Sfx.TaskDone, 0.7f);
    }
}
