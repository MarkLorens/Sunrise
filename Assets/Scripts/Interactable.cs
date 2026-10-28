using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Anything the player can press E on. Hook up what happens through onInteract in the Inspector.
public class Interactable : MonoBehaviour
{
    // Every active interactable, so the player can find them without physics queries.
    public static readonly List<Interactable> All = new();

    public float range = 2f;
    public UnityEvent onInteract;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public void Interact()
    {
        Debug.Log($"interacted with {name}");
        onInteract.Invoke();
    }
}
