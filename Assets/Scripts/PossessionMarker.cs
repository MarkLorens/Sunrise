using UnityEngine;

// A power bar over whoever you control while you charge a throw. (What you can act on is shown by ActionOutline.)
// GameHud adds this to the spirit.
[RequireComponent(typeof(Possession))]
public class PossessionMarker : MonoBehaviour
{
    Possession spirit;
    Transform barHolder;
    WorldBar powerBar;

    void Start()
    {
        spirit = GetComponent<Possession>();
        barHolder = new GameObject("Power Bar").transform;
        powerBar = WorldBar.Create(barHolder, new Vector3(0f, -0.4f, 0f), 0.9f);
    }

    void LateUpdate()
    {
        var host = spirit.Host;
        bool possessing = spirit.State == Possession.Mode.Possessing && host != null && !spirit.Riding; // nothing to mark from the cabin
        if (possessing) barHolder.position = host.HeadPoint + Vector3.up * 0.75f;
        powerBar.Set(spirit.Charge, WorldBar.Heat(spirit.Charge), possessing && spirit.Charging);
    }
}
