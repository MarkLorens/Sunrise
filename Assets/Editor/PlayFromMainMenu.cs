using UnityEditor;
using UnityEditor.SceneManagement;

// The Editor's Play button always starts from the main menu (the first scene in Build Settings), whichever scene
// is open, like a build does. Leaving Play mode puts you back in the scene you were editing.
[InitializeOnLoad]
static class PlayFromMainMenu
{
    static PlayFromMainMenu()
    {
        var scenes = EditorBuildSettings.scenes;
        EditorSceneManager.playModeStartScene = scenes.Length > 0 ? AssetDatabase.LoadAssetAtPath<SceneAsset>(scenes[0].path) : null;
    }
}
