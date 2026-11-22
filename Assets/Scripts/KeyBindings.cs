using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Which keyboard key does what (Options > Game Control). Two ready-made schemes, Control A (W A S D, J possess,
// K interact) and Control B (the arrows, Z possess, X interact), and any key changed one by one on top (custom).
// Expand Objective, Skip and Pause are the same in both. Kept in PlayerPrefs and written over the project's input
// actions (Assets/InputSystem_Actions, which Controls reads), so the whole game follows them; the gamepad bindings are
// left alone. Anything that names a key to the player (the tutorial's cards, the prompt over what you can use) asks
// here (Name, Token) instead of writing "J" or "K" itself.
public static class KeyBindings
{
    public enum Bind { Left, Right, Up, Down, Possess, Interact, Objectives, Skip, Pause }

    public static readonly Bind[] All = (Bind[])Enum.GetValues(typeof(Bind));
    // The ones a scheme sets; the rest are shared.
    public static readonly Bind[] SchemeBinds = { Bind.Left, Bind.Right, Bind.Up, Bind.Down, Bind.Possess, Bind.Interact };

    static readonly Dictionary<Bind, Key> SchemeA = new()
    {
        { Bind.Left, Key.A }, { Bind.Right, Key.D }, { Bind.Up, Key.W }, { Bind.Down, Key.S },
        { Bind.Possess, Key.J }, { Bind.Interact, Key.K },
    };
    static readonly Dictionary<Bind, Key> SchemeB = new()
    {
        { Bind.Left, Key.LeftArrow }, { Bind.Right, Key.RightArrow }, { Bind.Up, Key.UpArrow }, { Bind.Down, Key.DownArrow },
        { Bind.Possess, Key.Z }, { Bind.Interact, Key.X },
    };
    static readonly Dictionary<Bind, Key> Shared = new() { { Bind.Objectives, Key.Tab }, { Bind.Skip, Key.Space }, { Bind.Pause, Key.Escape } };

    static Dictionary<Bind, Key> keys;

    public static Key Get(Bind b)
    {
        Load();
        return keys[b];
    }

    // Changes one key. If another job already had it, that one takes this one's old key (a swap), so no key does two
    // things and nothing's left without one.
    public static void Set(Bind b, Key key)
    {
        Load();
        Key old = keys[b];
        foreach (var other in All)
            if (other != b && keys[other] == key) keys[other] = old;
        keys[b] = key;
        Save();
    }

    // 0: Control A, 1: Control B.
    public static void UseScheme(int scheme)
    {
        Load();
        foreach (var kv in scheme == 0 ? SchemeA : SchemeB) keys[kv.Key] = kv.Value;
        foreach (var kv in Shared) keys[kv.Key] = kv.Value;
        Save();
    }

    // Every job's key, in All's order: to keep and put back (Options' unsaved changes), or the first-play keys.
    public static Key[] Current()
    {
        Load();
        return Array.ConvertAll(All, b => keys[b]);
    }

    public static Key[] Defaults() => Array.ConvertAll(All, b => SchemeA.TryGetValue(b, out var k) ? k : Shared[b]);

    public static void SetAll(Key[] list)
    {
        Load();
        for (int i = 0; i < All.Length && i < list.Length; i++) keys[All[i]] = list[i];
        Save();
    }

    // Which scheme the keys are, or -1 when they've been changed away from both (custom).
    public static int Scheme
    {
        get
        {
            Load();
            foreach (var kv in Shared) if (keys[kv.Key] != kv.Value) return -1;
            if (Matches(SchemeA)) return 0;
            if (Matches(SchemeB)) return 1;
            return -1;
        }
    }

    static bool Matches(Dictionary<Bind, Key> scheme)
    {
        foreach (var kv in scheme) if (keys[kv.Key] != kv.Value) return false;
        return true;
    }

    // ---------------------------------------------------------------- what to call a key

    // A key as it's written on a keycap or a pill: "A", "←", "SPACE", "ESC".
    public static string Name(Key key) => key switch
    {
        Key.LeftArrow => "←",
        Key.RightArrow => "→",
        Key.UpArrow => "↑",
        Key.DownArrow => "↓",
        Key.Escape => "ESC",
        Key.Space => "SPACE",
        Key.Tab => "TAB",
        Key.Enter => "ENTER",
        Key.Backspace => "BKSP",
        Key.LeftShift => "L-SHIFT",
        Key.RightShift => "R-SHIFT",
        Key.LeftCtrl => "L-CTRL",
        Key.RightCtrl => "R-CTRL",
        Key.LeftAlt => "L-ALT",
        Key.RightAlt => "R-ALT",
        >= Key.Digit1 and <= Key.Digit0 => key == Key.Digit0 ? "0" : ((int)key - (int)Key.Digit1 + 1).ToString(),
        _ => key.ToString().ToUpperInvariant(),
    };

    public static string Name(Bind b) => Name(Get(b));

    // A key written in a sentence or on a pill (the game's fonts have no arrows): "A", "LEFT ARROW", "SPACE".
    public static string Words(Key key) => key switch
    {
        Key.LeftArrow => "LEFT ARROW",
        Key.RightArrow => "RIGHT ARROW",
        Key.UpArrow => "UP ARROW",
        Key.DownArrow => "DOWN ARROW",
        _ => Name(key),
    };

    public static string Words(Bind b) => Words(Get(b));

    // A [token] in the game's text (GameText): [J] is possess, [K] interact, [WASD] moving, [Tab], [Space], [Esc].
    // Gives what to write in the sentence and the keys to draw (and light up) for it.
    public static (string words, Key[] keys) Token(string token)
    {
        switch (token.ToUpperInvariant())
        {
            case "J": return (Words(Bind.Possess), new[] { Get(Bind.Possess) });
            case "K": return (Words(Bind.Interact), new[] { Get(Bind.Interact) });
            case "TAB": return (Words(Bind.Objectives), new[] { Get(Bind.Objectives) });
            case "SPACE": return (Words(Bind.Skip), new[] { Get(Bind.Skip) });
            case "ESC": return (Words(Bind.Pause), new[] { Get(Bind.Pause) });
            case "WASD":
                var move = new[] { Get(Bind.Up), Get(Bind.Left), Get(Bind.Down), Get(Bind.Right) };
                bool arrows = move[0] == Key.UpArrow && move[1] == Key.LeftArrow && move[2] == Key.DownArrow && move[3] == Key.RightArrow;
                if (arrows) return ("the arrow keys", move);
                var names = Array.ConvertAll(move, k => Words(k));
                return (Array.TrueForAll(names, n => n.Length == 1) ? string.Concat(names) : string.Join(", ", names), move);
            default:
                return Enum.TryParse(token, true, out Key k) ? (Words(k), new[] { k }) : (token, new Key[0]);
        }
    }

    // ---------------------------------------------------------------- kept, and written over the input actions

    static void Load()
    {
        if (keys != null) return;
        keys = new Dictionary<Bind, Key>();
        foreach (var kv in SchemeA) keys[kv.Key] = kv.Value;
        foreach (var kv in Shared) keys[kv.Key] = kv.Value;
        foreach (var b in All)
            if (Enum.TryParse(PlayerPrefs.GetString("key." + b, ""), out Key saved) && saved != Key.None) keys[b] = saved;
    }

    static void Save()
    {
        foreach (var b in All) PlayerPrefs.SetString("key." + b, keys[b].ToString());
        PlayerPrefs.Save();
        Apply();
    }

    // Writes the keys over the project's actions: Player/Move's four directions (each has two keyboard parts, the
    // second one following the first), Player/Possess, Use, Objectives, Pause and Skip's first keyboard binding.
    public static void Apply()
    {
        Load();
        var actions = InputSystem.actions;
        if (actions == null) return;
        var move = actions.FindAction("Player/Move");
        if (move != null)
            for (int i = 0; i < move.bindings.Count; i++)
            {
                var b = move.bindings[i];
                if (!b.isPartOfComposite || !b.path.StartsWith("<Keyboard>")) continue;
                Bind? dir = b.name switch { "up" => Bind.Up, "down" => Bind.Down, "left" => Bind.Left, "right" => Bind.Right, _ => null };
                if (dir.HasValue) move.ApplyBindingOverride(i, Path(keys[dir.Value]));
            }
        One(actions.FindAction("Player/Possess"), Bind.Possess);
        One(actions.FindAction("Player/Use"), Bind.Interact);
        One(actions.FindAction("Player/Objectives"), Bind.Objectives);
        One(actions.FindAction("Player/Pause"), Bind.Pause);
        One(actions.FindAction("Player/Skip"), Bind.Skip);
    }

    static void One(InputAction action, Bind b)
    {
        if (action == null) return;
        for (int i = 0; i < action.bindings.Count; i++)
            if (action.bindings[i].path.StartsWith("<Keyboard>"))
            {
                action.ApplyBindingOverride(i, Path(keys[b]));
                return; // the first one: Skip keeps Enter as a second
            }
    }

    // <Keyboard>/leftArrow for Key.LeftArrow (the control's name is the key's, first letter small).
    static string Path(Key key)
    {
        string n = key.ToString();
        return "<Keyboard>/" + char.ToLowerInvariant(n[0]) + n.Substring(1);
    }

    // Every play starts from what was kept (scripts aren't reloaded between plays here).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Startup()
    {
        keys = null;
        Apply();
    }
}
