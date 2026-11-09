using System.IO;
using UnityEditor;
using UnityEngine;

// Turns the artist's colour-keyed art (one flat key colour per region, plus the lines) into a line layer and white
// fill layers that CharacterSprite tints: select the files, then Tools > Split Coloured Body Art.
//   body_<variant>_<pose>  -> Resources/ArtLineartTorso/<name>.PNG (line) + <name>_skin/top/bottom/shoes.png
//   hair_<n>               -> Resources/ArtLineartHead/<name>.PNG (line) + <name>_hair/hat.png
//   face_<n>[_open/closed] -> Resources/ArtLineartHead/<name>.PNG (line only)
public static class BodyArtSplitter
{
    public const string OutFolder = "Assets/Resources/ArtLineartTorso";
    public const string HeadFolder = "Assets/Resources/ArtLineartHead";

    // The artist's fill-bucket swatch; anything far from all of these (and from the line) is reported.
    static readonly (string layer, Color32 key)[] BodyKeys =
    {
        ("skin", new Color32(0xFF, 0x00, 0x00, 255)),
        ("top", new Color32(0xFF, 0xBE, 0x00, 255)),
        ("bottom", new Color32(0x00, 0x04, 0xFF, 255)),
        ("shoes", new Color32(0x00, 0xFF, 0x31, 255)),
    };
    static readonly (string layer, Color32 key)[] HairKeys =
    {
        ("hair", new Color32(0xFF, 0x00, 0x00, 255)),
        ("hat", new Color32(0x00, 0x04, 0xFF, 255)),
    };
    static readonly (string layer, Color32 key)[] NoKeys = { };

    static readonly Color32 Line = new(0x26, 0x2A, 0x2E, 255);
    const int TuckUnderLine = 4; // px each fill grows under the line, so no gap shows at anti-aliased edges

    static readonly Vector2 BodyPivot = new(0.5f, 0.045f);
    // Face/hair canvases are the top-left 442x360 of the idle body canvas; the pivot is the head's top-left corner
    // there (x 67, y 309 from the bottom), which CharacterSprite pins to the head of whatever frame the body shows.
    public static readonly Vector2 HeadPivot = new(67f / 442f, 280f / 360f);

    [MenuItem("Tools/Split Coloured Body Art")]
    static void SplitSelected()
    {
        int done = 0;
        foreach (var tex in Selection.GetFiltered<Texture2D>(SelectionMode.Assets))
            if (Split(AssetDatabase.GetAssetPath(tex))) done++;
        AssetDatabase.Refresh();
        Debug.Log($"Split Coloured Body Art: {done} file(s) split");
    }

    [MenuItem("Tools/Split Coloured Body Art", true)]
    static bool CanSplit() => Selection.GetFiltered<Texture2D>(SelectionMode.Assets).Length > 0;

    public static bool Split(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        bool head = name.StartsWith("hair_") || name.StartsWith("face_");
        var keys = name.StartsWith("hair_") ? HairKeys : name.StartsWith("face_") ? NoKeys : BodyKeys;
        string folder = head ? HeadFolder : OutFolder;
        var pivot = head ? HeadPivot : BodyPivot;
        Directory.CreateDirectory(folder);

        var src = new Texture2D(2, 2);
        src.LoadImage(File.ReadAllBytes(path)); // the file itself, so import settings don't matter
        int w = src.width, h = src.height;
        var px = src.GetPixels32();
        Object.DestroyImmediate(src);

        // Each opaque pixel goes to its nearest key colour; -1 = line (or anti-aliasing nearest the line).
        var layer = new int[px.Length];
        int unknown = 0;
        for (int i = 0; i < px.Length; i++)
        {
            layer[i] = -2; // transparent
            if (px[i].a < 128) continue;
            int best = -1, bestDist = Dist(px[i], Line);
            for (int k = 0; k < keys.Length; k++)
            {
                int d = Dist(px[i], keys[k].key);
                if (d < bestDist) { bestDist = d; best = k; }
            }
            if (bestDist > 120 && keys.Length > 0) unknown++; // faces are all line (plus eye highlights)
            layer[i] = best;
        }
        // Line layer: line pixels plus the soft outer edge, in their original colour.
        var line = new Color32[px.Length];
        for (int i = 0; i < px.Length; i++) if (layer[i] == -1 || (px[i].a > 0 && px[i].a < 128)) line[i] = px[i];
        Write(line, w, h, $"{folder}/{name}.PNG", pivot);

        if (unknown > 0) Debug.LogWarning($"{name}: {unknown} px aren't close to any key colour (stray paint?); they were guessed.");

        for (int k = 0; k < keys.Length; k++)
        {
            var mask = new bool[px.Length];
            bool any = false;
            for (int i = 0; i < px.Length; i++) any |= mask[i] = layer[i] == k;
            string outPath = $"{folder}/{name}_{keys[k].layer}.png";
            if (!any) { AssetDatabase.DeleteAsset(outPath); continue; } // e.g. hair without a hat
            for (int step = 0; step < TuckUnderLine; step++) mask = GrowIntoLine(mask, layer, w);

            var outPx = new Color32[px.Length];
            for (int i = 0; i < px.Length; i++) if (mask[i]) outPx[i] = new Color32(255, 255, 255, 255);
            Write(outPx, w, h, outPath, pivot);
        }
        return true;
    }

    static void Write(Color32[] px, int w, int h, string outPath, Vector2 pivot)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        File.WriteAllBytes(outPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        ImportLikeLineart(outPath, pivot);
    }

    static int Dist(Color32 a, Color32 b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

    static bool[] GrowIntoLine(bool[] m, int[] layer, int w)
    {
        var g = (bool[])m.Clone();
        for (int i = 0; i < m.Length; i++)
        {
            if (m[i] || layer[i] != -1) continue;
            int x = i % w;
            if ((x > 0 && m[i - 1]) || (x < w - 1 && m[i + 1]) || (i >= w && m[i - w]) || (i + w < m.Length && m[i + w])) g[i] = true;
        }
        return g;
    }

    // Same import as the lineart, so the layers line up with it exactly.
    static void ImportLikeLineart(string path, Vector2 pivot)
    {
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 750;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        var s = new TextureImporterSettings();
        imp.ReadTextureSettings(s);
        s.spriteAlignment = (int)SpriteAlignment.Custom;
        s.spritePivot = pivot;
        s.spriteMeshType = SpriteMeshType.Tight;
        imp.SetTextureSettings(s);
        imp.SaveAndReimport();
    }
}
