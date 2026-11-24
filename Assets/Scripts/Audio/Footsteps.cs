using UnityEngine;
using FMODUnity;

// Footsteps that follow a character around: heard while they walk (waddle), silent when they stand still.
// Visitors and staff use FMODEvents.npcFootsteps, and FMODEvents.npcRun while they run; the body the player is
// possessing uses FMODEvents.hostFootsteps. How far each is heard is the event's own min/max distance in FMOD Studio
// (Run's is short, so a crowd bolting far off isn't heard).
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
        // Running on their own (panicking, or the tutorial's "Gotta go!" dash): the running sound, if it's set up.
        bool run = !possessed && look.running && !events.npcRun.IsNull;
        steps.Play(possessed ? HostSteps(events) : run ? events.npcRun : events.npcFootsteps);
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
