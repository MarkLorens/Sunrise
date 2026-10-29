using UnityEngine;

public class WinCondition : MonoBehaviour
{
    public ObjectiveDefinition winObjective;

    void Start() => ObjectiveManager.Instance.Completed += OnCompleted;
    void OnDestroy()
    {
        if (ObjectiveManager.Instance)
            ObjectiveManager.Instance.Completed -= OnCompleted;
    }

    bool won;

    void OnCompleted(ObjectiveInstance inst)
    {
        if (inst.definition != winObjective) return;
        Debug.Log("You win!");
        won = true;
    }

    // Temporary sprint gag: big CONGRATS overlay. Delete this method (and `won`) to remove it.
    void OnGUI()
    {
        if (!won) return;
        var style = new GUIStyle(GUI.skin.label) { fontSize = Screen.height / 6, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "CONGRATS", style);
    }
}
