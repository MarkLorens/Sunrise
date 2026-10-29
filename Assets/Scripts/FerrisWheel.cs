using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// A flat Ferris wheel: the wheel sprite spins in its own plane and the cabins hang upright around it.
// Sabotage pushes it into overdrive; a short circuit while in overdrive breaks the wheel loose,
// it rolls across the park flattening whoever's in the way, then topples flat onto the ground.
public class FerrisWheel : MonoBehaviour
{
    public enum State { Running, Overdrive, Breaking, Rolling, Wrecked }

    public Transform wheel;
    public Tilemap ground;
    public Sprite cabinSprite;
    public int cabinCount = 12;
    public float cabinRadius = 3.4f; // where cabins hang from, in wheel units
    public float wheelRadius = 3.5f; // outer rim, for rolling and hitting people
    public float normalSpeed = 12f;  // degrees per second
    public float overdriveSpeed = 140f;
    public float spinUpTime = 4f;
    public float breakDelay = 1.5f;
    public float rollSpeed = 6f;
    public float rollDistance = 30f;
    public float chaosRadius = 10f;
    public float panicTime = 8f;
    public RideQueue queue;

    public State Current { get; private set; }
    public bool IsRunningNormally => Current == State.Running;
    public bool IsOverdrive => Current == State.Overdrive;
    public bool IsBroken => Current >= State.Breaking;

    readonly List<Transform> cabins = new();
    readonly List<Vector3> cabinVelocity = new();
    Vector3 wheelHome;
    float angle;
    float speed;
    float stateTime;
    float rolled;
    float nextPanic;

    void Start()
    {
        wheelHome = wheel.localPosition;
        speed = normalSpeed;
        var mat = wheel.GetComponent<SpriteRenderer>().sharedMaterial;
        for (int i = 0; i < cabinCount; i++)
        {
            var go = new GameObject($"Cabin {i + 1}");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = cabinSprite;
            sr.sharedMaterial = mat;
            cabins.Add(go.transform);
        }
    }

    public void Overdrive()
    {
        if (Current != State.Running) return;
        Current = State.Overdrive;
        stateTime = 0f;
    }

    public void ResetToNormal()
    {
        if (Current != State.Overdrive) return;
        Current = State.Running;
    }

    public void BreakLoose()
    {
        if (Current != State.Overdrive) return;
        Current = State.Breaking;
        stateTime = 0f;
    }

    void Update()
    {
        stateTime += Time.deltaTime;
        switch (Current)
        {
            case State.Running:
                speed = Mathf.MoveTowards(speed, normalSpeed, overdriveSpeed / spinUpTime * Time.deltaTime);
                wheel.localPosition = wheelHome;
                Spin();
                break;

            case State.Overdrive:
                speed = Mathf.MoveTowards(speed, overdriveSpeed, overdriveSpeed / spinUpTime * Time.deltaTime);
                Shake(0.05f * speed / overdriveSpeed);
                Spin();
                break;

            case State.Breaking:
                Shake(0.25f);
                Spin();
                if (stateTime >= breakDelay) Detach();
                break;

            case State.Rolling:
                Roll();
                break;
        }
        UpdateCabins();
    }

    void Spin()
    {
        angle += speed * Time.deltaTime;
        wheel.localRotation = Quaternion.Euler(0, 0, angle);
    }

    void Shake(float amount)
    {
        wheel.localPosition = wheelHome + (Vector3)(Random.insideUnitCircle * amount);
    }

    void Detach()
    {
        Current = State.Rolling;
        stateTime = 0f;
        wheel.SetParent(null, true);

        // Cabins and the people in them get flung off.
        for (int i = 0; i < cabins.Count; i++)
        {
            cabins[i].SetParent(null, true);
            cabinVelocity.Add(new Vector3(Random.Range(-4f, 4f), Random.Range(3f, 7f), Random.Range(-1.5f, 0.5f)));
        }
        if (queue != null) queue.ThrowOutRiders(transform.position + new Vector3(0, 0, -1.5f), 3f);
        Chaos(transform.position);
        GameHud.Say("THE FERRIS WHEEL BROKE LOOSE!");
    }

    void Roll()
    {
        // Drop to the ground first, then roll left across the park.
        Vector3 p = wheel.position;
        p.y = Mathf.MoveTowards(p.y, wheelRadius, 9f * Time.deltaTime);
        Vector3 next = p + Vector3.left * rollSpeed * Time.deltaTime;
        bool onGround = OnGround(next + Vector3.left * wheelRadius);

        if (rolled < rollDistance && onGround)
        {
            p = next;
            rolled += rollSpeed * Time.deltaTime;
            angle += rollSpeed / wheelRadius * Mathf.Rad2Deg * Time.deltaTime; // rolling without slipping
            wheel.localRotation = Quaternion.Euler(0, 0, angle);
            FlattenPeople(p);
            if (Time.time >= nextPanic)
            {
                Chaos(p);
                nextPanic = Time.time + 0.5f;
            }
        }
        else
        {
            // Topple over: tip the flat wheel back until it lies on the ground like a decal.
            wheel.rotation = Quaternion.RotateTowards(wheel.rotation, Quaternion.Euler(90f, 0f, angle), 120f * Time.deltaTime);
            p.y = Mathf.MoveTowards(p.y, 0.02f, 3f * Time.deltaTime);
            if (Quaternion.Angle(wheel.rotation, Quaternion.Euler(90f, 0f, angle)) < 0.5f)
            {
                Current = State.Wrecked;
                GameHud.Say("The Ferris wheel is wrecked. Closed for repairs!");
            }
        }
        wheel.position = p;
    }

    bool OnGround(Vector3 p)
    {
        Vector3Int cell = ground.WorldToCell(p);
        cell.z = 0;
        return ground.HasTile(cell);
    }

    void FlattenPeople(Vector3 center)
    {
        foreach (var v in Visitor.All)
        {
            if (v.Current is Visitor.State.Down or Visitor.State.Riding) continue;
            Vector3 d = v.transform.position - center;
            if (Mathf.Abs(d.x) < wheelRadius * 0.8f && Mathf.Abs(d.z) < 0.8f)
            {
                v.KnockDown();
                GameHud.Casualties++;
            }
        }
    }

    void Chaos(Vector3 at)
    {
        foreach (var v in Visitor.All)
        {
            Vector3 d = v.transform.position - at;
            d.y = 0;
            if (d.magnitude <= chaosRadius) v.Panic(at, panicTime);
        }
    }

    void UpdateCabins()
    {
        if (Current < State.Rolling)
        {
            // Hang upright from evenly spaced points on the spinning rim.
            for (int i = 0; i < cabins.Count; i++)
            {
                float a = (angle + i * 360f / cabins.Count) * Mathf.Deg2Rad;
                cabins[i].position = wheel.position + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * cabinRadius + new Vector3(0, 0, -0.05f);
            }
            return;
        }

        // Flung cabins fall until they hit the ground.
        for (int i = 0; i < cabins.Count; i++)
        {
            if (cabins[i].position.y <= 0.9f && cabinVelocity[i].y <= 0) continue; // landed (cabin hangs from its top)
            cabinVelocity[i] += Vector3.down * 15f * Time.deltaTime;
            Vector3 p = cabins[i].position + cabinVelocity[i] * Time.deltaTime;
            p.y = Mathf.Max(p.y, 0.9f);
            cabins[i].position = p;
            cabins[i].rotation = Quaternion.Euler(0, 0, cabinVelocity[i].x * -4f);
        }
    }
}
