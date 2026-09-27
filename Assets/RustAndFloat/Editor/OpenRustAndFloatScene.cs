using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class OpenRustAndFloatScene
{
    const string ScenePath = "Assets/RustAndFloat/Scenes/RustAndFloat.unity";

    [MenuItem("RustAndFloat/Open Main Scene")]
    public static void Open()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Stop Play Mode", "■で再生を止めてから開いてください。", "OK");
            return;
        }
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
        {
            EditorUtility.DisplayDialog("Missing", ScenePath, "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }
}
