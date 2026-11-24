using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Build UI Art List: imports every picture in Assets/UI Asset as an uncompressed UI sprite and lists them in
// Resources/UiArt (UiArt), by their path in that folder without the extension. The new main menu's art
// (Assets/Arts/NewMainMenu) is listed too, as "NewMainMenu/<name>".
public static class UiArtBuilder
{
    static readonly (string folder, string prefix)[] Folders = { ("Assets/UI Asset", ""), ("Assets/Arts/NewMainMenu", "NewMainMenu/") };
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
        foreach (var (folder, prefix) in Folders)
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
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
            if (Mathf.Max(w, h) > imp.maxTextureSize) { imp.maxTextureSize = 4096; big = true; } // the page background is 2268 wide, the main menu's 3024
            if (big) imp.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) continue;
            string name = path.Substring(folder.Length + 1);
            name = Path.ChangeExtension(name, null).Replace('\\', '/');
            list.names.Add(prefix + name);
            list.sprites.Add(sprite);
        }
        EditorUtility.SetDirty(list);
        AssetDatabase.SaveAssets();
        Debug.Log($"UI Art List: {list.names.Count} pictures");
    }
}
