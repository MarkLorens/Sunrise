using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

// Walks on the X/Z floor with WASD/arrows, but only onto cells that have a tile on the ground tilemap.
// Gravity + the ground's collider keep it standing on the floor.
[RequireComponent(typeof(Rigidbody))]
public class GroundWalker : MonoBehaviour
{
    public Tilemap ground;
    public float speed = 5f;

    Rigidbody rb;

    void Awake() => rb = GetComponent<Rigidbody>();

    void FixedUpdate()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        Vector2 input = new Vector2(
            (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0),
            (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0));
        Vector2 v = input.normalized * speed;

        // Check each axis separately so you can slide along edges instead of sticking.
        if (!OnTile(new Vector3(v.x, 0, 0))) v.x = 0;
        if (!OnTile(new Vector3(0, 0, v.y))) v.y = 0;

        rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.y);
    }

    bool OnTile(Vector3 velocity)
    {
        Vector3Int cell = ground.WorldToCell(rb.position + velocity * Time.fixedDeltaTime);
        cell.z = 0; // height above the floor doesn't matter
        return ground.HasTile(cell);
    }
}
