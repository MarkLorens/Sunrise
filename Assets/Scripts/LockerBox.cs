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
    public Sprite openArt; // the box opened up (ElectricalBoxUnlocked), drawn like the map's electrical_box tile

    void Start()
    {
        if (!OverTile()) ParkArt.Solid(transform, "Props/electrical_box", 1.3f, new Color(0.4f, 0.55f, 0.75f));
        zap = gameObject.AddComponent<ZapFx>(); // live electrics, buzzing till it blows
        buzz = new LoopingSound(gameObject);
    }

    ZapFx zap;
    SpriteRenderer outline; // the unseen copy of the tile, for the outline (OverTile)
    bool open;
    LoopingSound buzz; // its shorted-out electrics, till it blows
    float buzzRange;   // how far off it's heard (the event's max distance): the caption shows within it

    // Blown (or loaded burnt): the bolt's gone with it, and its buzzing.
    void Update()
    {
        if (room.Burning && zap.Bolt != null) Destroy(zap.Bolt.gameObject);
        Buzz(!room.Burning);
    }

    void Buzz(bool on)
    {
        var events = FMODEvents.instance;
        if (!on || events == null)
        {
            buzz.Stop();
            return;
        }
        var sound = events.shortedElectricalBox;
        if (buzzRange <= 0f)
        {
            buzzRange = 20f; // FMOD's default, if the event can't be looked up
            if (!sound.IsNull && FMODUnity.RuntimeManager.StudioSystem.getEventByID(sound.Guid, out var description) == FMOD.RESULT.OK)
                description.getMinMaxDistance(out _, out buzzRange);
        }
        buzz.Play(sound);
        if (!sound.IsNull && Ears.Near(transform.position, buzzRange)) ClosedCaptions.Hold(Captions.ShortedBox);
    }

    void OnDisable() => buzz?.Stop();
    void OnDestroy() => buzz?.Release();

    // Drawn by the map (an electrical_box tile on or beside its spot): no picture of its own, just an unseen copy of
    // that tile's, in the tile's place, for the outline to trace (ActionOutline), so it rings the box you see.
    bool OverTile()
    {
        if (!FindTile(out var map, out var cell)) return false;
        Matrix4x4 m = ToiletPrank.TileMatrix(map, cell);
        var r = outline = new GameObject("Box Outline").AddComponent<SpriteRenderer>();
        r.transform.SetParent(transform, true);
        r.transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);
        r.transform.localScale = m.lossyScale;
        r.sprite = map.GetSprite(cell);
        var tr = map.GetComponent<TilemapRenderer>();
        if (tr != null) r.sharedMaterial = tr.sharedMaterial;
        r.forceRenderingOff = true; // the tile's the one you see
        return true;
    }

    // Its tile on the map: on or beside its spot (closed, or opened up: Open).
    bool FindTile(out Tilemap map, out Vector3Int cell)
    {
        map = null;
        cell = default;
        var spawner = FindAnyObjectByType<VisitorSpawner>();
        if (spawner == null || spawner.ground == null) return false;
        foreach (var m in GroundWalker.SolidMaps(spawner.ground))
        {
            Vector3Int at = m.WorldToCell(transform.position);
            at.z = 0;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    var c = at + new Vector3Int(dx, dy, 0);
                    var sprite = m.GetSprite(c);
                    if (sprite == null || !(sprite.name.StartsWith("electrical_box") || (openArt != null && sprite == openArt))) continue;
                    map = m;
                    cell = c;
                    return true;
                }
        }
        return false;
    }

    // The electrician's opened it up to get at the cable: drawn open from now on, its tile swapped for openArt where
    // it stands (so when the locker room burns, the open box is what burns: Scorch draws whatever's there). Sparked
    // (Interact), or the locker room alight (LockerRoom, a save loading it burnt too).
    // ponytail: only a box the map draws opens; one drawn by ParkArt.Solid (no tile) stays shut.
    public void Open()
    {
        if (open || openArt == null) return;
        open = true;
        if (outline != null) outline.sprite = openArt;
        if (!FindTile(out var map, out var cell) || map.GetSprite(cell) == openArt) return;
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.name = openArt.name;
        tile.sprite = openArt;
        if (map.GetTile(cell) is Tile old) tile.colliderType = old.colliderType;
        Matrix4x4 at = map.GetTransformMatrix(cell);
        Color color = map.GetColor(cell);
        TileFlags flags = map.GetTileFlags(cell);
        map.SetTile(cell, tile);
        map.SetTileFlags(cell, TileFlags.None);
        map.SetTransformMatrix(cell, at);
        map.SetColor(cell, color);
        map.SetTileFlags(cell, flags);
    }

    public string Prompt(Visitor host) => "Spark the broken cable";

    public string Blocked(Visitor host)
    {
        if (room.Burning) return GameText.AlreadyBurning;
        return host.HeldKind == VisitorItem.Keys ? null : GameText.NeedKeys;
    }

    public void Interact(Visitor host)
    {
        Open(); // opened up to get at the cable
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
