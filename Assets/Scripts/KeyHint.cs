using UnityEngine;
using UnityEngine.InputSystem;

// A keycap that answers its key: while the key (or mouse button) is held it dips a little and, if it has one, shows its
// pressed picture (pressedSprite: Space's KeySpace_fx). `blink` makes it press itself on a beat, a quick click every
// BlinkTime seconds, so it catches the eye (the tutorial card's Next key).
public class KeyHint : MonoBehaviour
{
    public const float BlinkTime = 0.9f, ClickTime = 0.18f;

    public Key[] keys = new Key[0];
    public bool leftMouse;
    public bool blink;
    public Sprite pressedSprite;
    public Sprite idleSprite; // its own picture, when pressedSprite swaps it (KeyCap sets both)

    UnityEngine.UI.Image cap;
    Sprite normalSprite;

    void Awake()
    {
        cap = GetComponent<UnityEngine.UI.Image>();
        if (cap == null) cap = GetComponentInChildren<UnityEngine.UI.Image>(); // a KeyCap: its picture is on a child
    }

    void Update()
    {
        bool down = false;
        var keyboard = Keyboard.current;
        if (keyboard != null)
            foreach (var key in keys)
                down |= keyboard[key].isPressed;
        if (leftMouse && Mouse.current != null) down |= Mouse.current.leftButton.isPressed;
        if (blink) down |= Mathf.Repeat(Time.unscaledTime, BlinkTime) < ClickTime;

        if (pressedSprite != null)
        {
            if (idleSprite != null) normalSprite = idleSprite;
            else if (cap.sprite != pressedSprite && cap.sprite != null) normalSprite = cap.sprite;
            cap.sprite = down || normalSprite == null ? pressedSprite : normalSprite;
        }
        transform.localScale = Vector3.one * (down ? 0.9f : 1f);
    }
}
