using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// A key drawn as the artist's keycap (UI Asset/KeyboardKey): its own picture for the keys that have one (W A S D, J K,
// X Z, the arrows, Space), otherwise the blank key with the name written on it. `height` is how tall the drawn key is
// (the pictures have a wide see-through margin, left outside the layout). KeyHint lights it up while it's held
// (Space shows its pressed picture, KeySpace_fx). SetKey changes which key it shows (the HUD's prompt).
public class KeyCap : MonoBehaviour
{
    // The drawn key inside each picture: square keys 105 x 101 of a 275 px canvas, Space 202 x 102 of 272.
    const float SquareW = 105f, SquareH = 101f, SquareCanvas = 275f, SpaceW = 202f, SpaceH = 102f, SpaceCanvas = 272f;

    Image art;
    Text label;
    LayoutElement element;
    KeyHint hint;
    float height;

    public static KeyCap Make(Transform parent, string key, Key[] keys, float height, bool mouse = false)
    {
        var rt = new GameObject("Key " + key, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        var cap = rt.gameObject.AddComponent<KeyCap>();
        cap.height = height;
        cap.element = rt.gameObject.AddComponent<LayoutElement>();

        var artRt = new GameObject("Art", typeof(RectTransform)).GetComponent<RectTransform>();
        artRt.SetParent(rt, false);
        artRt.anchorMin = artRt.anchorMax = new Vector2(0.5f, 0.5f);
        cap.art = artRt.gameObject.AddComponent<Image>();
        cap.art.raycastTarget = false;

        var labelRt = new GameObject("Label", typeof(RectTransform)).GetComponent<RectTransform>();
        labelRt.SetParent(rt, false);
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(0f, height * 0.12f); // on the key's top face, above its front edge
        labelRt.offsetMax = Vector2.zero;
        cap.label = labelRt.gameObject.AddComponent<Text>();
        cap.label.font = ParkArt.Font;
        cap.label.fontStyle = FontStyle.Bold;
        cap.label.alignment = TextAnchor.MiddleCenter;
        cap.label.color = new Color32(13, 9, 11, 255);
        cap.label.raycastTarget = false;
        cap.label.horizontalOverflow = HorizontalWrapMode.Overflow;

        cap.hint = rt.gameObject.AddComponent<KeyHint>();
        cap.hint.keys = keys;
        cap.hint.leftMouse = mouse;
        cap.SetKey(key);
        return cap;
    }

    // The artist's picture for this key, if there is one.
    static string ArtFor(string key) => key.ToUpperInvariant() switch
    {
        "SPACE" => "KeyboardKey/KeySpace",
        "UP" or "↑" => "KeyboardKey/KeyUp",
        "DOWN" or "↓" => "KeyboardKey/KeyDown",
        "LEFT" or "←" => "KeyboardKey/KeyLeft",
        "RIGHT" or "→" => "KeyboardKey/KeyRight",
        var k when k.Length == 1 && UiArt.Get("KeyboardKey/Key" + k) != null => "KeyboardKey/Key" + k,
        _ => null,
    };

    public void SetKey(string key)
    {
        string name = ArtFor(key);
        bool space = name == "KeyboardKey/KeySpace";
        art.sprite = UiArt.Get(name ?? "KeyboardKey/KeyBlank");
        label.text = name == null ? key : "";
        if (hint != null)
        {
            hint.pressedSprite = space ? UiArt.Get("KeyboardKey/KeySpace_fx") : null;
            hint.idleSprite = art.sprite;
        }

        float w = space ? SpaceW : SquareW, h = space ? SpaceH : SquareH, canvas = space ? SpaceCanvas : SquareCanvas;
        float k = height / h;
        element.preferredWidth = w * k;
        element.preferredHeight = height;
        var artRt = art.rectTransform;
        artRt.sizeDelta = new Vector2(canvas, canvas) * k;
        // The drawn key sits a touch off the picture's middle (up and left): line the drawn part up with the cap.
        artRt.anchoredPosition = space ? new Vector2(7f, -4f) * k : new Vector2(2f, -4f) * k;
        label.fontSize = Mathf.RoundToInt(height * (key.Length > 2 ? 0.3f : 0.45f));
    }
}
