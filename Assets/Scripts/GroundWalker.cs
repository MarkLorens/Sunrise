using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

// Walks on the X/Z floor, but only onto cells that have a tile on the ground tilemap.
// Driven by WASD/arrows when readKeyboard is on, otherwise by whatever sets `move` (e.g. visitor AI).
// Gravity + the ground's collider keep it standing on the floor.
[RequireComponent(typeof(Rigidbody))]
public class GroundWalker : MonoBehaviour
{
    public Tilemap ground;
    public float speed = 5f;
    public bool readKeyboard = true;
    [HideInInspector] public Vector2 move; // x = world X, y = world Z; used when readKeyboard is off

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        ground.CompressBounds();
        var b = ground.localBounds;
        var floor = ground.GetComponent<BoxCollider>();
        floor.center = new Vector3(b.center.x, b.center.y, 0.5f);
        floor.size = new Vector3(b.size.x, b.size.y, 1f);
    }

    void FixedUpdate()
    {
        Vector2 input = readKeyboard ? KeyboardInput() : move;
        Vector2 v = Vector2.ClampMagnitude(input, 1f) * speed;

        // Check each axis separately so you can slide along edges instead of sticking.
        if (!OnTile(new Vector3(v.x, 0, 0))) v.x = 0;
        if (!OnTile(new Vector3(0, 0, v.y))) v.y = 0;

        rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.y);
    }

    static Vector2 KeyboardInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return Vector2.zero;
        return new Vector2(
            (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0),
            (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0)).normalized;
    }

    public bool IsWalkable(Vector3 worldPos)
    {
        Vector3Int cell = ground.WorldToCell(worldPos);
        cell.z = 0; // height above the floor doesn't matter
        return ground.HasTile(cell);
    }

    bool OnTile(Vector3 velocity) => IsWalkable(rb.position + velocity * Time.fixedDeltaTime);
}
