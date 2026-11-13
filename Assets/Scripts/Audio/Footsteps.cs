using UnityEngine;

// Footsteps that follow a character around: heard while their walk cycle is stepping, silent when they stand still.
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
        steps.Play(possessed ? events.hostFootsteps : events.npcFootsteps);
        steps.SetParameter("Pace", look.running ? 1f : 0f);
    }

    void OnDisable() => steps.Stop();
    void OnDestroy() => steps.Release();
}
