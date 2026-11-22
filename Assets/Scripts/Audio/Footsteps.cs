using UnityEngine;
using FMODUnity;

// Footsteps that follow a character around: heard while they walk (waddle), silent when they stand still.
// Visitors and staff use FMODEvents.npcFootsteps; the body the player is possessing uses FMODEvents.hostFootsteps.
// Added automatically by CharacterSprite.
[RequireComponent(typeof(CharacterSprite))]
public class Footsteps : MonoBehaviour
{
    CharacterSprite look;
    Visitor visitor;
    LoopingSound steps;

    void Awake()
    {
        look = GetComponent<CharacterSprite>();
        visitor = GetComponent<Visitor>();
        steps = new LoopingSound(gameObject, GetComponent<Rigidbody>());
    }

    void LateUpdate()
    {
        var events = FMODEvents.instance;
        if (events == null || !look.Stepping)
        {
            steps.Stop();
            return;
        }

        var possession = Possession.Instance;
        bool possessed = visitor != null && possession != null && possession.Host == visitor;
        steps.Play(possessed ? HostSteps(events) : events.npcFootsteps);
        steps.SetParameter("Pace", look.running ? 1f : 0f);
    }

    // The player walking in this body: the special characters have their own steps (RU-06 to RU-09), everyone else
    // and any of theirs not set up yet the possessed visitor's (RU-05).
    EventReference HostSteps(FMODEvents events)
    {
        EventReference own = visitor.brain switch
        {
            Mechanic => events.mechanicFootsteps,
            HotDogMan => events.hotDogManFootsteps,
            RideTechnician => events.technicianFootsteps,
            _ => default,
        };
        return own.IsNull ? events.hostFootsteps : own;
    }

    void OnDisable() => steps.Stop();
    void OnDestroy() => steps.Release();
}
