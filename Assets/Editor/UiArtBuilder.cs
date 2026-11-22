using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Build UI Art List: imports every picture in Assets/UI Asset as an uncompressed UI sprite and lists them in
// Resources/UiArt (UiArt), by their path in that folder without the extension.
public static class UiArtBuilder
{
    const string Folder = "Assets/UI Asset";
    const string ListPath = "Assets/Resources/UiArt.asset";

    [MenuItem("Tools/Build UI Art List")]
    public static void Build()
    {
        var list = AssetDatabase.LoadAssetAtPath<UiArt>(ListPath);
        if (list == null)
        {
            list = ScriptableObject.CreateInstance<UiArt>();
            AssetDatabase.CreateAsset(list, ListPath);
        }
        list.names.Clear();
        list.sprites.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            bool big = false;
            if (imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single
                || imp.textureCompression != TextureImporterCompression.Uncompressed || imp.mipmapEnabled)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                big = true;
            }
            imp.GetSourceTextureWidthAndHeight(out int w, out int h);
            if (Mathf.Max(w, h) > imp.maxTextureSize) { imp.maxTextureSize = 4096; big = true; } // the page background is 2268 wide
            if (big) imp.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) continue;
            string name = path.Substring(Folder.Length + 1);
            name = Path.ChangeExtension(name, null).Replace('\\', '/');
            list.names.Add(name);
            list.sprites.Add(sprite);
        }
        EditorUtility.SetDirty(list);
        AssetDatabase.SaveAssets();
        Debug.Log($"UI Art List: {list.names.Count} pictures");
    }
}
