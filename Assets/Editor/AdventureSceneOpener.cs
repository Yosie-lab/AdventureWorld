#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

public static class AdventureSceneOpener
{
    [MenuItem("Adventure/🏝️ 1. Open Island Scene (島を開く) %&o")]
    public static void OpenIslandScene()
    {
        // 変更の保存を確認しつつ、AdventureWorldシーンを開く
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene("Assets/Scenes/AdventureWorld.unity");
        }
    }
}
#endif
