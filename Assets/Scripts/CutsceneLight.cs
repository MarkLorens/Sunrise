using System.Collections.Generic;
using UnityEngine;

// The tutorial's spotlight look for the cutscenes' close-ups on someone (the technician in his booth, the electrician
// coming out, the operator running to the fire, the PA booth, the mascot): the park goes dark round them and they stay
// clear in their own shape with a soft glow (TutorialSpotlight, drawn under the HUD, so speech bubbles stay bright).
// On(...) says who, each with everything drawn under them (a body's pieces, a booth's picture, a Zzz); Off() fades it
// back. It leaves the tutorial's own spotlight alone while the tutorial's running.
public class CutsceneLight : MonoBehaviour
{
    const float Dim = 0.9f, FadeSpeed = 3.5f; // as the tutorial's

    static CutsceneLight instance;
    readonly List<Transform> subjects = new();
    float darkness;
    bool on;

    public static void On(params Component[] things)
    {
        var l = Get();
        if (l == null) return;
        l.subjects.Clear();
        foreach (var t in things)
            if (t != null) l.subjects.Add(t.transform);
        l.on = l.subjects.Count > 0;
    }

    public static void Off()
    {
        if (instance != null) instance.on = false;
    }

    static bool TutorialRunning
    {
        get
        {
            var t = FindAnyObjectByType<Tutorial>();
            return t != null && t.enabled;
        }
    }

    static CutsceneLight Get()
    {
        if (instance != null) return instance;
        if (TutorialRunning) return null;
        var spot = FindAnyObjectByType<TutorialSpotlight>();
        if (spot == null) spot = new GameObject("Cutscene Spotlight").AddComponent<TutorialSpotlight>();
        instance = spot.gameObject.AddComponent<CutsceneLight>();
        return instance;
    }

    void LateUpdate()
    {
        if (TutorialRunning) return; // its light, not ours
        darkness = Mathf.MoveTowards(darkness, on ? 1f : 0f, Time.unscaledDeltaTime * FadeSpeed);
        // Gathered again every frame: bodies change pieces (a face, a pose), a Zzz comes and goes.
        var lit = TutorialSpotlight.Lit;
        lit.Clear();
        if (darkness > 0f)
            foreach (var s in subjects)
                if (s != null)
                    foreach (var r in s.GetComponentsInChildren<SpriteRenderer>())
                        if (r.name != "Flat") lit.Add(r); // not shadows lying on the ground
        TutorialSpotlight.Darkness = Mathf.SmoothStep(0f, 1f, darkness) * Dim;
        if (!on && darkness <= 0f) subjects.Clear();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
