using UnityEngine;

// How on edge the whole park is, 0 (an ordinary evening) to 1 (it's all gone horribly wrong). Unlike ParkChaos, which
// scares people near a scene for a while, this only ever rises: it's worked out from what has happened tonight, the
// big events (Mischief tasks) and everyone who's died, so a loaded game is exactly as uneasy as when it was saved.
// ParkCrowd keeps the crowd's panic from dropping below it; the announcer (VisitorSpawner) speaks up more often.
public static class Unease
{
    // Each event, once it's happened, and each death so far.
    static readonly (Mischief.Task task, float weight)[] Events =
    {
        (Mischief.Task.TutorialToilet, 0.05f), // only if someone saw it (ToiletPrank.Erupted)
        (Mischief.Task.Electrocute, 0.15f),
        (Mischief.Task.CutPower, 0.1f),
        (Mischief.Task.Explode, 0.15f),
        (Mischief.Task.LockerFire, 0.15f),
        (Mischief.Task.WheelRolled, 0.3f),
    };
    const float PerDeath = 0.04f;
    const float Recount = 1f; // seconds: deaths are counted afresh this often

    static float level, countedAt = float.NegativeInfinity;

    public static float Level
    {
        get
        {
            if (Time.time - countedAt < Recount && Time.time >= countedAt) return level;
            countedAt = Time.time;
            float sum = 0f;
            var prank = Object.FindAnyObjectByType<ToiletPrank>();
            foreach (var (task, weight) in Events)
                // The tutorial counts as done when it's skipped or a game's loaded: only a geyser someone saw counts.
                if (Mischief.IsDone(task) && (task != Mischief.Task.TutorialToilet || prank != null && prank.Erupted))
                    sum += weight;
            foreach (var v in Object.FindObjectsByType<Visitor>(FindObjectsInactive.Include))
                if (v.CarriedOff || v.Current == Visitor.State.Down) sum += PerDeath; // the carried-off ones died too
            return level = Mathf.Clamp01(sum);
        }
    }
}
