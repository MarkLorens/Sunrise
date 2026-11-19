using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// One save slot: the level's progress, not a snapshot of every person in it. Kept: the steps done, the puddle, the
// fountain and its mechanic, the electrical box, the wheel's power (the propane, fan, technician, platform and hinge
// all follow from the steps done), who's lying dead where, and where the player
// is and whose body they're in (with what in hand). Everyone else starts fresh around the park.
// Saved from the pause menu (PauseMenu), loaded from the main menu (MainMenu): Load() opens the level, and the
// level's PauseMenu calls ApplyPending() once everything in it has set itself up.
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
        public List<Fan> fans = new();  // which way every fan faces
    }

    [System.Serializable]
    class Fan
    {
        public string name;
        public TankFan.Dir facing;
    }

    const string VisitorName = "Visitor";

    static string Path => System.IO.Path.Combine(Application.persistentDataPath, "save.json");
    static Data pending;

    public static bool Exists => File.Exists(Path);

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

    public static bool Save()
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
        };
        foreach (var f in TankFan.All) data.fans.Add(new Fan { name = f.name, facing = f.facing });
        foreach (var v in Visitor.All)
            if (v.Current == Visitor.State.Down)
                data.dead.Add(new Body { name = v.name, at = v.transform.position });
        File.WriteAllText(Path, JsonUtility.ToJson(data, true));
        return true;
    }

    // Opens the saved level; it's put back the way it was once it has loaded (ApplyPending).
    public static void Load()
    {
        if (!Exists) return;
        pending = JsonUtility.FromJson<Data>(File.ReadAllText(Path));
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
