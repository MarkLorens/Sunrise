using System.Collections.Generic;
using UnityEngine;

// ponytail: placeholder IMGUI HUD until there's real UI. Shows the possession status,
// what E would do right now, and a short log of things people say.
public class GameHud : MonoBehaviour
{
    public string objective = "Break the Ferris wheel";

    static readonly List<(string text, float until)> messages = new();
    public static int Casualties;

    Possession possession;
    PlayerInteractor interactor;
    GUIStyle style;

    void Awake()
    {
        possession = GetComponent<Possession>();
        interactor = GetComponent<PlayerInteractor>();
        messages.Clear();
        Casualties = 0;
    }

    public static void Say(string text)
    {
        messages.Add((text, Time.time + 4f));
        if (messages.Count > 5) messages.RemoveAt(0);
    }

    void OnGUI()
    {
        style ??= new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
        messages.RemoveAll(m => Time.time > m.until);

        var host = possession.Host;
        string status;
        if (host != null)
            status = $"Possessing {host.name} — {Mathf.CeilToInt(possession.TimeLeft)}s left" +
                     (host.item != VisitorItem.None ? $" — holding {host.item}" : "") + "   [Q] leave";
        else if (possession.CooldownLeft > 0)
            status = $"Recovering… {Mathf.CeilToInt(possession.CooldownLeft)}s";
        else
            status = possession.Touching() != null ? "[Q] possess" : "Touch a visitor to possess them";

        Shadowed(new Rect(16, 12, 900, 30), $"Objective: {objective}" + (Casualties > 0 ? $"   Casualties: {Casualties}" : ""));
        Shadowed(new Rect(16, 40, 900, 30), status);

        var target = interactor.Nearest();
        if (target != null)
        {
            string blocked = target.Blocked(host);
            string line = blocked == null ? $"[E] {target.Prompt(host)}" : $"<color=#bbbbbb>{target.Prompt(host)} — {blocked}</color>";
            Shadowed(new Rect(16, Screen.height - 50, 900, 30), line);
        }

        for (int i = 0; i < messages.Count; i++)
            Shadowed(new Rect(16, 80 + i * 26, 900, 26), messages[i].text);
    }

    void Shadowed(Rect r, string text)
    {
        style.richText = true;
        var c = GUI.color;
        GUI.color = Color.black;
        GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), StripColor(text), style);
        GUI.color = c;
        GUI.Label(r, text, style);
    }

    static string StripColor(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "");
}
