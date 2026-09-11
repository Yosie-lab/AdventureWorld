using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AdventureOpenRustAndFlatScene
{
    const string ScenePath = "Assets/RustAndFlat/Scenes/RustAndFlat.unity";

    [MenuItem("Adventure/Open RustAndFlat Scene (new island)")]
    public static void Open()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "再生を止めてください",
                "▶再生中はシーンを切り替えられません。■で停止してから選んでください。",
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
        Debug.Log("[Adventure] RustAndFlat（新島）シーンを開きました。");
    }
}
