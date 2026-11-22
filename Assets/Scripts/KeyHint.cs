using UnityEngine;
using UnityEngine.InputSystem;

// Lights up a keycap while its key (or mouse button) is held, so new players can see which button does what: it
// dips a little, and either swaps to its pressed picture (pressedSprite: Space) or takes on pressedColor.
public class KeyHint : MonoBehaviour
{
    public Key[] keys = new Key[0];
    public bool leftMouse;
    public Color pressedColor = new(1f, 0.55f, 0.7f);
    public Sprite pressedSprite;
    public Sprite idleSprite; // its own picture, when pressedSprite swaps it (KeyCap sets both)

    UnityEngine.UI.Image cap;
    Color normalColor;
    Sprite normalSprite;

    void Awake()
    {
        cap = GetComponent<UnityEngine.UI.Image>();
        if (cap == null) cap = GetComponentInChildren<UnityEngine.UI.Image>(); // a KeyCap: its picture is on a child
        normalColor = cap.color;
    }

    void Update()
    {
        bool down = false;
        var keyboard = Keyboard.current;
        if (keyboard != null)
            foreach (var key in keys)
                down |= keyboard[key].isPressed;
        if (leftMouse && Mouse.current != null) down |= Mouse.current.leftButton.isPressed;
        if (pressedSprite != null)
        {
            if (idleSprite != null) normalSprite = idleSprite;
            else if (cap.sprite != pressedSprite && cap.sprite != null) normalSprite = cap.sprite;
            cap.sprite = down || normalSprite == null ? pressedSprite : normalSprite;
        }
        else cap.color = down ? pressedColor : normalColor;
        transform.localScale = Vector3.one * (down ? 0.9f : 1f);
    }
}
