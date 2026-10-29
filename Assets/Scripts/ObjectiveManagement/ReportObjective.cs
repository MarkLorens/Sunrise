using UnityEngine;

public class ReportObjective : MonoBehaviour
{
    public QuestType type = QuestType.Interact;
    public string targetId;
    bool reported;

    public void Report()
    {
        if (reported) return;
        reported = true;
        ObjectiveManager.Instance.Report(type, targetId);
    }
}
