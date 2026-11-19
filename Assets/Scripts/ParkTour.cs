using System;
using System.Collections;
using UnityEngine;

// After the tutorial (played through or skipped; a loaded game has neither): the camera takes in the park a spot at
// a time (bottom right, top right, top left, bottom left, a few seconds each), then the Ferris wheel and its
// electrical box, and comes back to the spirit, who says what's next. It isn't part of the tutorial: it can't be
// skipped, and the objectives (top left) stay hidden until it's over. The player can't move meanwhile.
// The camera follows one invisible point (CameraRig.Spotlight) that glides from stop to stop.
public class ParkTour : MonoBehaviour
{
    const float SpotHold = 2.5f;     // seconds the camera rests on each spot
    const float SpotMove = 2.5f;     // seconds to glide from one stop to the next
    const float SpotPullBack = 3f;   // how much further back than usual the camera sits on the spots

    // The park's spots, picked out in FerrisWheelLevel (world positions, on the ground), in the order they're shown.
    static readonly Vector3[] Spots =
    {
        new(9.5f, 0f, -11f),   // bottom right: the toilet block and the ice cream stand, the benches between
        new(12f, 0f, 5.5f),    // top right: the speakers and their crowd
        new(-16f, 0f, 1f),     // top left: the hot dog stand and the two ice cream stands
        new(-12f, 0f, -12.5f), // bottom left: the locker room and the balloon stand
    };

    Possession spirit;
    Transform point;
    float pullBack;

    public static void Play()
    {
        if (Possession.Instance == null) return;
        // Straight away, so nothing slips in between the tutorial ending and the tour starting.
        Possession.InputBlocked = true;
        GameHud.HideObjectives = true;
        new GameObject("Park Tour").AddComponent<ParkTour>();
    }

    IEnumerator Start()
    {
        spirit = Possession.Instance;
        point = new GameObject("Park Tour Point").transform;
        point.SetParent(transform, false);
        At(spirit.Focus.position, 0f);

        foreach (var spot in Spots)
        {
            yield return Glide(() => spot, SpotPullBack, SpotMove);
            yield return new WaitForSeconds(SpotHold);
        }

        var wheel = FindAnyObjectByType<FerrisWheel>();
        if (wheel != null)
        {
            yield return Glide(() => wheel.wheel.position, wheel.payoffPullBack, 3f);
            yield return new WaitForSeconds(2.5f);
            var box = FindAnyObjectByType<ElectricalBox>();
            if (box != null)
            {
                var picture = box.GetComponentInChildren<Renderer>();
                yield return Glide(() => picture != null ? picture.bounds.center : box.transform.position, 0f, 2.5f);
                yield return new WaitForSeconds(2f);
            }
        }

        // Home, and the spirit has a plan.
        yield return Glide(() => spirit.Focus.position, 0f, 3f);
        CameraRig.EndSpotlight();
        GameHud.Think(GameText.TourSays);
        yield return new WaitForSeconds(4.2f); // read to the end before anything else pops up
        Destroy(gameObject); // the player moves again, and the objectives come back
    }

    // Finished, or gone mid-tour (the scene's unloading): never leaves the player stuck.
    void OnDestroy()
    {
        Possession.InputBlocked = false;
        GameHud.HideObjectives = false;
    }

    // From where the point is now to `to` (followed, in case it moves), easing in and out.
    IEnumerator Glide(Func<Vector3> to, float toPullBack, float seconds)
    {
        Vector3 from = point.position;
        float fromPullBack = pullBack;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            At(Vector3.Lerp(from, to(), k), Mathf.Lerp(fromPullBack, toPullBack, k));
            yield return null;
        }
        At(to(), toPullBack);
    }

    void At(Vector3 at, float back)
    {
        point.position = at;
        pullBack = back;
        CameraRig.Spotlight(point, 999f, 1f, back);
    }
}
