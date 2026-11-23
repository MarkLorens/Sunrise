using UnityEngine;
using UnityEngine.Tilemaps;

// The park's looping prop animations, set up once the level's loaded (GameHud adds it):
// - every drinking fountain, the scene's and any on a tilemap: its water running, Resources/Park/Anim/drinking_fountain_1..4
//   in turn (FountainFps);
// - the hot dog stand: its grill's flames flickering between Resources/Park/Anim/hot_dog_fire_1 and _2, drawn on the
//   stand's own canvas (the artist's FIRE GUIDE), just behind its front and in front of the hot dog man.
// Found by their pictures' names, so a fountain or stand added later gets it too.
public class ParkAnimations : MonoBehaviour
{
    const float FountainFps = 3.5f; // frames a second: a calm trickle
    const string FountainSprite = "drinking_fountain", StandFront = "hot_dog_stand_front";

    // Each hot dog stand's grill flames, so the hot dog man can put them out and light them again (HotDogMan).
    public static readonly System.Collections.Generic.List<SpriteRenderer> Grills = new();

    void Start()
    {
        Grills.Clear(); // the last level's
        var fountain = Frames("Park/Anim/drinking_fountain_", 4);
        var fire = Frames("Park/Anim/hot_dog_fire_", 2);

        foreach (var r in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            if (r.sprite == null) continue;
            if (r.sprite.name == FountainSprite && fountain != null)
                r.gameObject.AddComponent<SpriteFlipbook>().Play(fountain, FountainFps, 0f);
            else if (r.sprite.name == StandFront && fire != null)
                Flames(r, fire);
        }

        if (fountain == null) return;
        foreach (var map in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                var s = map.GetSprite(cell);
                if (s == null || s.name != FountainSprite) continue;
                var tile = ScriptableObject.CreateInstance<FlipbookTile>();
                tile.frames = fountain;
                tile.fps = FountainFps;
                tile.color = map.GetColor(cell);
                tile.matrix = map.GetTransformMatrix(cell);
                map.SetTile(cell, tile);
            }
    }

    // The flames sit exactly over the stand's front (same canvas, pivot and size), a hair further back: behind the
    // front's frame, in front of whoever's inside. Unlit, so they glow at night.
    static void Flames(SpriteRenderer front, Sprite[] frames)
    {
        var r = new GameObject("Grill Fire").AddComponent<SpriteRenderer>();
        r.transform.SetParent(front.transform.parent, false);
        r.transform.localPosition = front.transform.localPosition;
        r.transform.localRotation = front.transform.localRotation;
        r.transform.localScale = front.transform.localScale;
        r.transform.position += Vector3.forward * 0.03f; // the hot dog man stands 0.06 behind the front
        r.sharedMaterial = ParkArt.Material;
        r.sortingOrder = front.sortingOrder;
        r.sprite = frames[0];
        r.gameObject.AddComponent<SpriteFlipbook>().Play(frames, 8f, 0.35f);
        Grills.Add(r);
    }

    static Sprite[] Frames(string path, int n)
    {
        var frames = new Sprite[n];
        for (int i = 0; i < n; i++)
            if ((frames[i] = Resources.Load<Sprite>(path + (i + 1))) == null) return null;
        return frames;
    }
}

// Cycles a SpriteRenderer through frames. jitter: each frame lasts a little more or less (0.35 = up to 35%), for
// things like flames that shouldn't tick like a clock.
public class SpriteFlipbook : MonoBehaviour
{
    Sprite[] frames;
    float fps, jitter, next;
    int at;
    SpriteRenderer r;

    public void Play(Sprite[] frames, float fps, float jitter)
    {
        this.frames = frames;
        this.fps = fps;
        this.jitter = jitter;
        r = GetComponent<SpriteRenderer>();
        at = Random.Range(0, frames.Length); // neighbours don't run in step
        r.sprite = frames[at];
        next = Time.time + Hold();
    }

    float Hold() => 1f / fps * (1f + Random.Range(-jitter, jitter));

    void Update()
    {
        if (frames == null || Time.time < next) return;
        at = (at + 1) % frames.Length;
        r.sprite = frames[at];
        next = Time.time + Hold();
    }
}

// A tile that plays frames in place (the Tilemap animates it), keeping the cell's own placement and tint.
public class FlipbookTile : TileBase
{
    public Sprite[] frames;
    public float fps = 7f;
    public Color color = Color.white;
    public Matrix4x4 matrix = Matrix4x4.identity;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData data)
    {
        data.sprite = frames[0];
        data.color = color;
        data.transform = matrix;
        data.flags = TileFlags.LockTransform | TileFlags.LockColor;
        data.colliderType = Tile.ColliderType.None;
    }

    public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData data)
    {
        data.animatedSprites = frames;
        data.animationSpeed = fps;
        data.animationStartTime = Random.value * frames.Length / fps;
        return true;
    }
}
