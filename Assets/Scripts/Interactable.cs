using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Puzzle objects implement this next to an Interactable to decide what pressing E does, and for whom.
// `host` is the visitor the spirit is inside, or null when it's floating around on its own.
public interface IInteraction
{
    string Prompt(Visitor host);
    string Blocked(Visitor host); // null = allowed, otherwise the reason shown to the player
    void Interact(Visitor host);
}

// Anything the player can press E on. Simple ones hook up onInteract in the Inspector;
// puzzle pieces add an IInteraction component for rules that depend on who's interacting.
public class Interactable : MonoBehaviour
{
    // Every active interactable, so the player can find them without physics queries.
    public static readonly List<Interactable> All = new();

    public float range = 2f;
    public string prompt = "Interact";
    public bool needsBody = true; // the spirit can't touch the world; it has to be inside someone
    public UnityEvent onInteract;

    IInteraction rules;

    void Awake() => rules = GetComponent<IInteraction>();
    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public string Prompt(Visitor host) => rules?.Prompt(host) ?? prompt;

    public string Blocked(Visitor host)
    {
        if (needsBody && host == null) return "You need a body to touch things";
        return rules?.Blocked(host);
    }

    public void Interact(Visitor host)
    {
        Debug.Log($"interacted with {name}");
        rules?.Interact(host);
        onInteract.Invoke();
    }
}
