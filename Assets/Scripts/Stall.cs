using System.Collections.Generic;
using UnityEngine;

// A food stall. Visitors with empty hands wander over and buy whatever it sells;
// a possessed visitor can buy one too (E) - handy when the puzzle needs another drink or a spicy hotdog.
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
        customer.Look.Show("heart", 1.2f);
    }

    string Goods => sells switch
    {
        VisitorItem.SpicyHotdog => "a spicy hotdog",
        VisitorItem.Drink => "a drink",
        _ => sells.ToString(),
    };

    public string Prompt(Visitor host) => $"Buy {Goods}";

    public string Blocked(Visitor host) => host.Held != null ? "Your hands are full" : null;

    public void Interact(Visitor host)
    {
        Sell(host);
        GameHud.Say($"{host.name} buys {Goods}.");
    }
}
