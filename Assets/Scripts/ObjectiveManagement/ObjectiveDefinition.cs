using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ObjectiveDefinition", menuName = "Scriptable Objects/ObjectiveDefinition")]
public class ObjectiveDefinition : ScriptableObject
{
    public string id;
    public string titleKey;
    public List<QuestDefinition> quests;
    public List<ObjectiveDefinition> prerequisites;
}

public class ObjectiveInstance
{
    public ObjectiveDefinition definition;
    public ObjectiveStatus Status;
    public int[] QuestProgress;
}

public enum ObjectiveStatus { Locked, Active, Completed }

[System.Serializable]
public class QuestDefinition
{
    public string description;
    public QuestType type;
    public string targetId;
    public int requiredCount = 1;
}
public enum QuestType { Interact, Collect, ReachArea }