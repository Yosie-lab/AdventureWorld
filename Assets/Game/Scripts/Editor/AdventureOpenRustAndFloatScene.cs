using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AdventureOpenRustAndFloatScene
{
    const string ScenePath = "Assets/RustAndFloat/Scenes/RustAndFloat.unity";

    [MenuItem("Adventure/Open RustAndFloat Scene (1000m Grand Sanctuary)")]
    public static void Open()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "再生を止めてください",
                "▶再生中はシーンを切り替えられません。\n上の■（停止）を押してから、もう一度このメニューを選んでください。",
                "OK");
            return;
        }

        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
        {
            EditorUtility.DisplayDialog("見つかりません", ScenePath + " がありません。", "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("[Adventure] RustAndFloat シーンを開きました。");
    }
}
