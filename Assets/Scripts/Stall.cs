using System.Collections.Generic;
using UnityEngine;

// A drinks stall. Visitors with empty hands wander over and buy whatever it sells;
// a possessed visitor can buy one too (E) - handy when the puzzle needs another drink.
[RequireComponent(typeof(Interactable))]
public class Stall : MonoBehaviour, IInteraction
{
    public static readonly List<Stall> All = new();

    public VisitorItem sells = VisitorItem.Drink;
    public Vector3 servePoint = new(0f, 0f, -1.1f); // where customers stand, relative to the stall

    public Vector3 ServePoint => transform.position + servePoint;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public void Sell(Visitor customer)
    {
        if (customer.Held != null) return;
        var item = Item.Create(sells, ServePoint + Vector3.up, customer);
        customer.GrabItem(item);
    }

    string Goods => sells switch
    {
        VisitorItem.Drink => "a drink",
        _ => sells.ToString(),
    };

    public string Prompt(Visitor host) => $"Buy {Goods}";

    public string Blocked(Visitor host) => host.Held != null ? GameText.HandsFull : null;

    public void Interact(Visitor host)
    {
        Sell(host);
        if (host.Held != null) GameHud.Say(GameText.Bought(host.Held.DisplayName));
    }
}
