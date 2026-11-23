using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using T = Mischief.Task;

// Tools > Puzzle Skipper: in Play mode, starts the park at any part of the objectives (Mischief.Parts), everything
// before it done, as if loaded from a save (SaveGame.DebugStartAt). Nothing is saved during such a run.
public class PuzzleSkipper : EditorWindow
{
    // What each part of the objectives finishes, hidden steps included, in order: starting at part n means all of
    // parts 0..n-1 here are done. The tutorial's done at any of them. A task added to the level goes in its part.
    static readonly T[][] Finishes =
    {
        new[] { T.TutorialPossess, T.TutorialSign, T.TutorialValve, T.TutorialLeave, T.TutorialToilet },
        new[] { T.ReachBox },
        new[] { T.Possess, T.FloodPuddle, T.Electrocute, T.LureMechanic, T.PossessMechanic, T.UnlockBox, T.CutPower, T.CallTechnician },
        new[] { T.RatsOut, T.LeakPropane, T.AimFan, T.GasCloud, T.Explode, T.WakeTechnician, T.PossessTechnician, T.OpenPlatform, T.LoosenHinge },
        new[] { T.RestartWheel },
        new[] { T.SmellMascot, T.LeakLockerGas, T.AimLockerFan, T.LockerGasCloud, T.SparkLocker, T.LockerFire },
        new[] { T.PossessOperator, T.OpenBooth, T.MaxSpeed, T.WheelRolled },
    };

    [MenuItem("Tools/Puzzle Skipper")]
    static void Open() => GetWindow<PuzzleSkipper>("Puzzle Skipper");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Reloads the park at the start of a puzzle, with everything before it done. Nothing is saved during the run.", MessageType.None);
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play mode first.", MessageType.Info);
            return;
        }
        var parts = Mischief.Parts;
        for (int p = 1; p < parts.Length; p++) // 0 is the tutorial: a New Game
            if (GUILayout.Button("Start at " + parts[p].title)) StartAt(p);
        if (SaveGame.DebugRun) EditorGUILayout.LabelField("Debug run: saving is off.", EditorStyles.miniLabel);
    }

    static void StartAt(int part)
    {
        var done = new List<T>();
        for (int p = 0; p < part && p < Finishes.Length; p++) done.AddRange(Finishes[p]);
        SaveGame.DebugStartAt(done, shownMechanic: part >= 2);
    }
}
