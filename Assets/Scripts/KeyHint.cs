using UnityEngine;
using UnityEngine.InputSystem;

// Lights up a keycap in the controls bar while its key (or mouse button) is held,
// so new players can see which button does what.
public class KeyHint : MonoBehaviour
{
    public Key[] keys = new Key[0];
    public bool leftMouse;
    public Color pressedColor = new(1f, 0.55f, 0.7f);

    UnityEngine.UI.Image cap;
    Color normalColor;

    void Awake()
    {
        cap = GetComponent<UnityEngine.UI.Image>();
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
        cap.color = down ? pressedColor : normalColor;
        transform.localScale = Vector3.one * (down ? 0.9f : 1f);
    }
}
