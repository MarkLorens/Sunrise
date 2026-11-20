using UnityEngine;
using UnityEngine.InputSystem;

// The player's controls, read from the project's input actions (Project Settings > Input System Package > Project-wide
// Actions, which is Assets/InputSystem_Actions). Change a key there, not in code:
//   Player/Move (WASD, arrows, gamepad stick), Player/Possess (J), Player/Use (K), Player/Objectives (Tab),
//   Player/Pause (Esc, gamepad Start), Player/Skip (Space, Enter: the tutorial), UI/Cancel (Esc: back out of a menu).
// The key caps drawn on screen (KeyHint) are pictures of the keys, not bindings.
public static class Controls
{
    static InputAction move, possess, use, objectives, pause, skip, back;

    // Play mode starts without reloading scripts here, so look the actions up afresh each time.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Forget() => move = possess = use = objectives = pause = skip = back = null;

    public static Vector2 Move => Vector2.ClampMagnitude(Get(ref move, "Player/Move")?.ReadValue<Vector2>() ?? Vector2.zero, 1f);
    public static bool Possess => Pressed(ref possess, "Player/Possess");
    public static bool Use => Pressed(ref use, "Player/Use");
    public static bool Objectives => Pressed(ref objectives, "Player/Objectives");
    public static bool Pause => Pressed(ref pause, "Player/Pause");
    public static bool Skip => Pressed(ref skip, "Player/Skip");
    public static bool SkipHeld => Get(ref skip, "Player/Skip")?.IsPressed() ?? false;
    public static bool Back => Pressed(ref back, "UI/Cancel");

    static bool Pressed(ref InputAction a, string name) => Get(ref a, name)?.WasPressedThisFrame() ?? false;

    static InputAction Get(ref InputAction a, string name)
    {
        if (a == null && InputSystem.actions != null)
        {
            a = InputSystem.actions.FindAction(name);
            if (a != null && !a.enabled) a.Enable();
        }
        return a;
    }
}
