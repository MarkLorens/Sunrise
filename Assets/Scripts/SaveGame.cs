using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Save slots, as many as there are saves (save_<n>.json, n from 0), each the level's progress, not a snapshot of
// every person in it. Kept: the steps done, the puddle, the
// fountain and its mechanic, the electrical box, the wheel's power (the propane, fan, technician, platform and hinge
// all follow from the steps done), who's lying dead where, who's been carried off, and where the player
// is and whose body they're in (with what in hand). Everyone else starts fresh around the park.
// Saved from the pause menu (PauseMenu) into the slot being played (Slot), loaded from the main menu's Load Game page
// (MainMenu): Load() opens the level, and the level's PauseMenu calls ApplyPending() once everything in it has set
// itself up. Each slot also keeps what the Load Game card shows: the objective you were on, when, and a round picture
// of the park (save_<n>.png).
// Bodies are found again by name: staff by theirs ("Mechanic", "Technician"...), any free visitor for a "Visitor".
public static class SaveGame
{
    [System.Serializable]
    class Body
    {
        public string name;
        public Vector3 at;
    }

    [System.Serializable]
    class Data
    {
        public int scene;
        public List<Mischief.Task> done = new();
        public Vector3 player;
        public Body host;               // null name: floating free as the spirit
        public VisitorItem hostItem;
        public float puddle;
        public int presses;
        public bool mechanicCalled, fountainFixed, shownMechanic;
        public List<Body> dead = new();
        public List<string> gone = new(); // carried off by the clean-up crew (Visitor.CarryOff): not coming back
        public List<Fan> fans = new();  // which way every fan faces
        public string title;            // the objective you were on (the Load Game card)
        public string played;           // when it was saved (round-trip date)
    }

    // What a Load Game card shows for a slot.
    public class Info
    {
        public string title;
        public System.DateTime played;
        public Texture2D picture; // round, see-through outside; null if there isn't one
    }

    [System.Serializable]
    class Fan
    {
        public string name;
        public TankFan.Dir facing;
    }

    const string VisitorName = "Visitor";

    // Auto save: a step crossed off the objectives (Mischief.Complete) asks for one, and GameHud makes it into the slot
    // being played as soon as the game can be saved (not mid-cutscene, the tutorial or the pause menu). Any save does.
    public static bool AutoSave { get; set; }
    // The slot being played: New Game takes the first empty one, Load Game the one picked.
    public static int Slot { get; set; }

    static string PathOf(int slot) => System.IO.Path.Combine(Application.persistentDataPath, $"save_{slot}.json");
    static string PictureOf(int slot) => System.IO.Path.Combine(Application.persistentDataPath, $"save_{slot}.png");
    static Data pending;

    public static bool Has(int slot)
    {
        MoveOldSave();
        return File.Exists(PathOf(slot));
    }

    // Any slot saved in.
    public static bool Exists => Saved().Count > 0;

    // Every slot with a save in it, lowest first.
    public static List<int> Saved()
    {
        MoveOldSave();
        var slots = new List<int>();
        if (!Directory.Exists(Application.persistentDataPath)) return slots;
        foreach (var file in Directory.GetFiles(Application.persistentDataPath, "save_*.json"))
        {
            string n = System.IO.Path.GetFileNameWithoutExtension(file)["save_".Length..];
            if (int.TryParse(n, out int slot) && slot >= 0) slots.Add(slot);
        }
        slots.Sort();
        return slots;
    }

    // The lowest slot nobody's saved in: there's always one (New Game never runs out).
    public static int FirstEmpty
    {
        get
        {
            int slot = 0;
            while (Has(slot)) slot++;
            return slot;
        }
    }

    public static Info Read(int slot)
    {
        if (!Has(slot)) return null;
        var data = JsonUtility.FromJson<Data>(File.ReadAllText(PathOf(slot)));
        var info = new Info { title = data.title, played = File.GetLastWriteTime(PathOf(slot)) };
        if (System.DateTime.TryParse(data.played, null, System.Globalization.DateTimeStyles.RoundtripKind, out var when)) info.played = when;
        if (File.Exists(PictureOf(slot)))
        {
            info.picture = new Texture2D(2, 2);
            info.picture.LoadImage(File.ReadAllBytes(PictureOf(slot)));
        }
        return info;
    }

    public static void Delete(int slot)
    {
        if (File.Exists(PathOf(slot))) File.Delete(PathOf(slot));
        if (File.Exists(PictureOf(slot))) File.Delete(PictureOf(slot));
    }

    // The one save from before there were slots becomes slot 1's.
    static void MoveOldSave()
    {
        string old = System.IO.Path.Combine(Application.persistentDataPath, "save.json");
        if (File.Exists(old) && !File.Exists(PathOf(0))) File.Move(old, PathOf(0));
    }

    // A saved game is being put back (from Load Game until the level has set itself up).
    public static bool Loading => pending != null;

    // Not mid-swoop or mid-cutscene: those don't come back sensibly.
    public static bool CanSave
    {
        get
        {
            var p = Possession.Instance;
            return p != null && !Possession.InCutscene && p.State != Possession.Mode.Diving
                   && (p.Host == null || p.Host.Current == Visitor.State.Possessed);
        }
    }

    // Into the slot being played. `screen`: the park as it looked (the pause menu's shot of it), for the card's picture.
    public static bool Save(Texture2D screen = null)
    {
        if (!CanSave) return false;
        var p = Possession.Instance;
        var fountain = Object.FindAnyObjectByType<DrinkingFountain>();
        var box = Object.FindAnyObjectByType<ElectricalBox>();
        var data = new Data
        {
            scene = SceneManager.GetActiveScene().buildIndex,
            done = new List<Mischief.Task>(Mischief.Done),
            player = p.Focus.position,
            host = p.Host != null ? new Body { name = p.Host.name, at = p.Host.transform.position } : null,
            hostItem = p.Host != null ? p.Host.HeldKind : VisitorItem.None,
            puddle = fountain != null ? fountain.Target : 0f,
            presses = fountain != null ? fountain.Presses : 0,
            mechanicCalled = fountain != null && fountain.MechanicCalled,
            fountainFixed = fountain != null && fountain.Fixed,
            shownMechanic = box != null && box.ShownMechanic,
            title = ObjectiveTitle(),
            played = System.DateTime.Now.ToString("o"),
        };
        foreach (var f in TankFan.All) data.fans.Add(new Fan { name = f.name, facing = f.facing });
        foreach (var v in Object.FindObjectsByType<Visitor>(FindObjectsInactive.Include))
            if (v.CarriedOff) data.gone.Add(v.name);
        foreach (var v in Visitor.All)
            if (v.Current == Visitor.State.Down)
                data.dead.Add(new Body { name = v.name, at = v.transform.position });
        File.WriteAllText(PathOf(Slot), JsonUtility.ToJson(data, true));
        AutoSave = false;
        if (screen != null) File.WriteAllBytes(PictureOf(Slot), RoundPicture(screen, 256).EncodeToPNG());
        else if (File.Exists(PictureOf(Slot))) File.Delete(PictureOf(Slot)); // not an older game's picture
        return true;
    }

    // The part of the level you're on, as the objectives show it ("1. A-Leak-Tricity" -> "A-Leak-Tricity"); the last
    // one once they're all done.
    static string ObjectiveTitle()
    {
        var parts = Mischief.Parts;
        int part = parts.Length - 1;
        for (int p = parts.Length - 1; p >= 0; p--)
            foreach (var (task, _) in parts[p].steps)
                if (!Mischief.IsDone(task)) part = p;
        string title = parts[part].title;
        int dot = title.IndexOf(". ");
        return dot >= 0 && dot < 3 ? title[(dot + 2)..] : title;
    }

    // The middle of `screen`, square, `size` px, see-through outside a circle (a soft edge a pixel or two wide).
    static Texture2D RoundPicture(Texture2D screen, int size)
    {
        float side = Mathf.Min(screen.width, screen.height);
        var rt = RenderTexture.GetTemporary(size, size, 0);
        Graphics.Blit(screen, rt, new Vector2(side / screen.width, side / screen.height),
            new Vector2((1f - side / screen.width) * 0.5f, (1f - side / screen.height) * 0.5f));
        var was = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        RenderTexture.active = was;
        RenderTexture.ReleaseTemporary(rt);
        var px = tex.GetPixels32();
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - r, y + 0.5f - r).magnitude;
                px[y * size + x].a = (byte)(Mathf.Clamp01(r - d) * 255f);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // Opens a saved level; it's put back the way it was once it has loaded (ApplyPending). From now on that's the
    // slot being played.
    public static void Load(int slot)
    {
        if (!Has(slot)) return;
        Slot = slot;
        pending = JsonUtility.FromJson<Data>(File.ReadAllText(PathOf(slot)));
        SceneManager.LoadScene(pending.scene);
    }

    public static void ApplyPending()
    {
        var data = pending;
        pending = null;
        if (data == null) return;

        Mischief.Restore(data.done);
        foreach (var w in FerrisWheel.All)
            if (Mischief.IsDone(Mischief.Task.RestartWheel)) w.RestoreWobbly();
            else if (Mischief.IsDone(Mischief.Task.CutPower)) w.RestoreUnpowered();
        var fountain = Object.FindAnyObjectByType<DrinkingFountain>();
        if (fountain != null) fountain.Restore(data.puddle, data.presses, data.mechanicCalled, data.fountainFixed);
        var box = Object.FindAnyObjectByType<ElectricalBox>();
        if (box != null) box.Restore(data.shownMechanic);
        // Flaming hot dogs and the hinge: all of it follows from the steps done.
        foreach (var t in RideTechnician.All) t.Restore();
        foreach (var t in Object.FindObjectsByType<PropaneTank>()) t.Restore();
        foreach (var saved in data.fans)
            foreach (var f in TankFan.All)
                if (f.name == saved.name) f.Restore(saved.facing);
        foreach (var s in Object.FindObjectsByType<FingerprintScanner>()) s.Restore();
        foreach (var h in Object.FindObjectsByType<WheelHinge>()) h.Restore();
        foreach (var g in Object.FindObjectsByType<Gutter>()) g.Restore();
        foreach (var m in Mascot.All) m.Restore();
        foreach (var r in Object.FindObjectsByType<LockerRoom>()) r.Restore();
        // ponytail: a save after the wheel has rolled loads it standing, wobbly. Nothing comes after it yet.

        var used = new HashSet<Visitor>();
        foreach (var name in data.gone)
            Find(name, used)?.CarryOff();
        foreach (var d in data.dead)
        {
            var v = Find(d.name, used);
            if (v == null) continue;
            Place(v, d.at);
            v.Die();
        }
        ParkChaos.Reset(); // nobody panics at bodies that have been lying there all along

        Visitor host = data.host != null && !string.IsNullOrEmpty(data.host.name) ? Find(data.host.name, used) : null;
        if (host != null)
        {
            Place(host, data.host.at);
            host.Look.SetVisible(true); // staff may still be "behind the staff door"
            if (host.HeldKind != data.hostItem)
            {
                host.TakeItem(); // whatever the fresh body happened to be carrying
                if (data.hostItem != VisitorItem.None) host.GrabItem(Item.Create(data.hostItem, host.CarryPoint), true);
            }
        }
        Possession.Instance.Restore(data.player, host);
    }

    // Staff by name; for a visitor, any free one (not taken, not on the ride, not already down).
    static Visitor Find(string name, HashSet<Visitor> used)
    {
        foreach (var v in Visitor.All)
        {
            if (used.Contains(v) || v.Current is Visitor.State.Down or Visitor.State.Riding) continue;
            if (name == VisitorName ? v.brain == null && v.name == VisitorName : v.name == name)
            {
                used.Add(v);
                return v;
            }
        }
        return null;
    }

    static void Place(Visitor v, Vector3 at)
    {
        at.y = v.transform.position.y;
        v.transform.position = at;
        if (v.TryGetComponent(out Rigidbody rb)) rb.position = at;
    }
}
