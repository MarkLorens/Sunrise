using System;
using System.Collections;
using UnityEngine;

// OFF FOR NOW: nothing plays it (Tutorial.Finish's call is commented out). Kept in case we want the shot again.
// After the tutorial (played through or skipped; a loaded game has neither): the camera glides over to the Ferris
// wheel, holds on it, and comes back to the spirit, who says what's next. It isn't part of the tutorial: it can't be
// skipped, and the objectives (top left) stay hidden until it's over. The player can't move meanwhile.
// The camera follows one invisible point (CameraRig.Spotlight) that glides from stop to stop.
public class ParkTour : MonoBehaviour
{
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

        var wheel = FindAnyObjectByType<FerrisWheel>();
        if (wheel != null)
        {
            yield return Glide(() => wheel.wheel.position, wheel.payoffPullBack, 3f);
            yield return new WaitForSeconds(2.5f);
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
