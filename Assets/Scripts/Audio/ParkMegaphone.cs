using System.Collections.Generic;
using UnityEngine;

// One of the megaphones up on the park's corners. The theme comes out of these (ParkMusic).
// MusicFx, added in the scene, thumps the box and puffs the notes. These are not the ground
// speakers visitors stand in front of (Speaker) — those are a separate prop.
public class ParkMegaphone : MonoBehaviour
{
    public static readonly List<ParkMegaphone> All = new();

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);
}
