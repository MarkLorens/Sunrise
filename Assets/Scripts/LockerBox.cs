using UnityEngine;
using UnityEngine.Tilemaps;

// The locker room's electrical box, its broken cable inside. It's the mechanic's: his keys open it (anyone holding
// them). Spark the cable while the locker room's full of propane (PropaneTank) and up it goes.
// It plays its own sound (IOwnSound): the menu click when the spark sets it off, only the "can't" sound when there's
// no propane to catch.
[RequireComponent(typeof(Interactable))]
public class LockerBox : MonoBehaviour, IInteraction, IOwnSound
{
    public LockerRoom room;

    void Start()
    {
        if (!OverTile()) ParkArt.Solid(transform, "Props/electrical_box", 1.3f, new Color(0.4f, 0.55f, 0.75f));
        zap = gameObject.AddComponent<ZapFx>(); // live electrics, buzzing till it blows
    }

    ZapFx zap;

    // Blown (or loaded burnt): the bolt's gone with it.
    void Update()
    {
        if (room.Burning && zap.Bolt != null) Destroy(zap.Bolt.gameObject);
    }

    // Drawn by the map (an electrical_box tile on or beside its spot): no picture of its own, just an unseen copy of
    // that tile's, in the tile's place, for the outline to trace (ActionOutline), so it rings the box you see.
    bool OverTile()
    {
        var spawner = FindAnyObjectByType<VisitorSpawner>();
        if (spawner == null || spawner.ground == null) return false;
        foreach (var map in GroundWalker.SolidMaps(spawner.ground))
        {
            Vector3Int at = map.WorldToCell(transform.position);
            at.z = 0;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    var cell = at + new Vector3Int(dx, dy, 0);
                    var sprite = map.GetSprite(cell);
                    if (sprite == null || !sprite.name.StartsWith("electrical_box")) continue;
                    Matrix4x4 m = ToiletPrank.TileMatrix(map, cell);
                    var r = new GameObject("Box Outline").AddComponent<SpriteRenderer>();
                    r.transform.SetParent(transform, true);
                    r.transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);
                    r.transform.localScale = m.lossyScale;
                    r.sprite = sprite;
                    var tr = map.GetComponent<TilemapRenderer>();
                    if (tr != null) r.sharedMaterial = tr.sharedMaterial;
                    r.forceRenderingOff = true; // the tile's the one you see
                    return true;
                }
        }
        return false;
    }

    public string Prompt(Visitor host) => "Spark the broken cable";

    public string Blocked(Visitor host)
    {
        if (room.Burning) return GameText.AlreadyBurning;
        return host.HeldKind == VisitorItem.Keys ? null : GameText.NeedKeys;
    }

    public void Interact(Visitor host)
    {
        ClosedCaptions.Show(Captions.CableSpark);
        Fx.Burst("Fx/sparkle", transform.position + Vector3.up * 0.8f, 14, new Color(1f, 0.95f, 0.4f), 3f, 0.3f, 0.4f);
        var tank = PropaneTank.ReadyNear(room.transform.position, 1f);
        if (tank == null)
        {
            GameHud.Refuse(transform, GameText.CableSparks); // G-06, no click
            return;
        }
        AudioManager.TryPlay(e => e.menuClick); // G-03: it went through
        GameHud.Say(GameText.SparkedCable);
        Mischief.Complete(Mischief.Task.SparkLocker);
        tank.Explode();
    }
}
