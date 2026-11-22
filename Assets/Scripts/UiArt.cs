using System.Collections.Generic;
using UnityEngine;

// The artist's UI pictures (Assets/UI Asset, kept where the artist put them, outside Resources): a list of them by
// their path in that folder ("Logo/Wohooland_logo", "Buttons/Button1_Idle"...), in Resources/UiArt so the game can
// find them at runtime. Tools > Build UI Art List remakes it (and imports the pictures as UI sprites) after the artist
// adds or changes some.
public class UiArt : ScriptableObject
{
    public List<string> names = new();
    public List<Sprite> sprites = new();

    static Dictionary<string, Sprite> byName;

    public static Sprite Get(string name)
    {
        if (byName == null)
        {
            byName = new Dictionary<string, Sprite>();
            var list = Resources.Load<UiArt>("UiArt");
            if (list != null)
                for (int i = 0; i < list.names.Count; i++) byName[list.names[i]] = list.sprites[i];
        }
        return byName.TryGetValue(name, out var s) ? s : null;
    }

    // Play mode starts without reloading scripts here: look the list up afresh each time.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Forget() => byName = null;
}
