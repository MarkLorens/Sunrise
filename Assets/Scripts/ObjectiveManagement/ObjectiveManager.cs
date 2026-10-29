using System.Collections.Generic;
using UnityEngine;

public class ObjectiveManager : MonoBehaviour
{
    public static ObjectiveManager Instance { get; private set; }
    public List<ObjectiveDefinition> objectiveDefinitions;
    private List<ObjectiveInstance> objectiveInstances = new List<ObjectiveInstance>();
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializeObjectives();
    }
    private void InitializeObjectives()
    {
        foreach (ObjectiveDefinition objDefinition in objectiveDefinitions)
        {
            ObjectiveInstance instance = new ObjectiveInstance
            {
                definition = objDefinition,
                QuestProgress = new int[objDefinition.quests.Count],
                Status = objDefinition.prerequisites.Count == 0
                ? ObjectiveStatus.Active
                : ObjectiveStatus.Locked
            };

            objectiveInstances.Add(instance);
        }
    }
    public event System.Action<ObjectiveInstance> Completed;
    public void Report(QuestType type, string targetId)
    {
        var finished = new List<ObjectiveInstance>();
        foreach (var inst in objectiveInstances)
        {
            if (inst.Status != ObjectiveStatus.Active) continue;
            var quests = inst.definition.quests;
            for (int i = 0; i < quests.Count; i++)
                if (quests[i].type == type && quests[i].targetId == targetId && inst.QuestProgress[i] < quests[i].requiredCount)
                    inst.QuestProgress[i]++;
            if (IsDone(inst)) finished.Add(inst);
        }
        foreach (var inst in finished) Complete(inst);
    }

    bool IsDone(ObjectiveInstance inst)
    {
        for (int i = 0; i < inst.QuestProgress.Length; i++)
            if (inst.QuestProgress[i] < inst.definition.quests[i].requiredCount) return false;
        return true;
    }

    void Complete(ObjectiveInstance inst)
    {
        inst.Status = ObjectiveStatus.Completed;
        Debug.Log($"Objective completed: {inst.definition.id}");
        Completed?.Invoke(inst);

        foreach (var other in objectiveInstances)
            if (other.Status == ObjectiveStatus.Locked && other.definition.prerequisites.TrueForAll(IsCompleted))
                other.Status = ObjectiveStatus.Active;
    }

    bool IsCompleted(ObjectiveDefinition d) =>
        objectiveInstances.Exists(o => o.definition == d && o.Status == ObjectiveStatus.Completed);
}
