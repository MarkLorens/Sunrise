using System.Collections;
using UnityEngine;

// The locker room building (on the Objects tilemap; this marks where it is). Gas it up (PropaneTank) and spark it
// (LockerBox) and it catches fire: the information booth's PA calls all staff to the fire, and the ride operator
// leaves his booth to run over (RideOperator.GoToFire), the one chance to get at him.
public class LockerRoom : MonoBehaviour
{
    public Transform paBooth;                              // where the announcement comes from
    public Vector3 rallyOffset = new(1.5f, 0f, -2.5f);     // where staff stand gawping at it, relative to this
    public float width = 3.6f;                             // how wide the flames spread

    public bool Burning { get; private set; }
    Vector3 Rally => transform.position + rallyOffset;

    float nextFlame;

    void Update()
    {
        if (!Burning || Time.time < nextFlame) return;
        nextFlame = Time.time + 0.05f;
        Vector3 at = transform.position + new Vector3(Random.Range(-0.5f, 0.5f) * width, Random.Range(0.8f, 2.6f), -0.4f);
        Rise(at, Random.Range(0.8f, 1.5f), new Color(1f, Random.Range(0.35f, 0.75f), 0.1f), 2f, 0.7f);
        if (Random.value < 0.25f) Rise(at + Vector3.up * 1.2f, 1.6f, new Color(0.15f, 0.15f, 0.15f, 0.7f), 1.2f, 2.5f); // smoke
    }

    // A flame or a puff of smoke drifting up off the building.
    static void Rise(Vector3 at, float size, Color color, float speed, float life)
    {
        var r = ParkArt.Make("Flame", null, "Fx/puff", size, color, ParkArt.OverheadOrder - 1);
        r.transform.position = at;
        var bit = r.gameObject.AddComponent<FxBit>();
        bit.velocity = new Vector3(Random.Range(-0.3f, 0.3f), speed, 0f);
        bit.life = life;
    }

    // quiet: loading a save, it's already burnt.
    void Ignite(bool quiet = false)
    {
        Burning = true;
        Scorch.BurnNear(transform.position, 2f, quiet, "lockers"); // the building's picture, burning (Scorch)
        ParkChaos.Add(transform.position, 6f, 9999f, ParkChaos.Terrifying);
    }

    // The payoff, after the fireball (PropaneTank): it burns, the PA goes off, the operator runs over.
    const float ShotHold = 5f; // seconds the camera stays on the PA booth, then on the operator, once it's there

    public IEnumerator Burn()
    {
        Ignite();
        yield return new WaitForSeconds(1f);

        if (paBooth != null)
        {
            // The booth's picture is a child, well away from the marker itself: frame the picture.
            var art = paBooth.GetComponentInChildren<SpriteRenderer>();
            CameraRig.Spotlight(art != null ? art.transform : paBooth, 999f, 2f); // the park stays lit (no CutsceneLight)
            yield return new WaitForSeconds(1.2f); // the camera getting there
            ClosedCaptions.Show(Captions.PaScreech);
            GameHud.Shout(paBooth, GameText.PAFire, ShotHold);
            yield return new WaitForSeconds(ShotHold + 0.2f);
        }

        foreach (var o in RideOperator.All)
        {
            o.GoToFire(Rally, transform.position);
            CameraRig.Spotlight(o.transform, 999f, 1.5f);
            CutsceneLight.On(o);
            GameHud.Shout(o.transform, GameText.OperatorFire, 1.2f + ShotHold);
            yield return new WaitForSeconds(1.2f + ShotHold); // the camera getting there, then the shot
            break;
        }
        CutsceneLight.Off();
        Mischief.Complete(Mischief.Task.LockerFire);
        Aftermath.Add(transform.position, 6.5f, 9999f); // just outside the fire's own panic zone
    }

    // Loading a save (SaveGame).
    public void Restore()
    {
        if (!Mischief.IsDone(Mischief.Task.LockerFire)) return;
        Ignite(true);
        foreach (var o in RideOperator.All) o.GoToFire(Rally, transform.position, true);
    }
}
