#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 天蓋開放〜エンディング演出を、毎回フル周回せずに確認するためのデバッグメニュー
/// </summary>
public static class AdventureDebugCanopyOpening
{
    const string ScenePath = "Assets/RustAndFloat/Scenes/RustAndFloat.unity";
    static bool _pendingJump;

    [MenuItem("Adventure/▶ Jump to 11 Scraps (パーツ11個から確認)")]
    public static void JumpFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorUtility.DisplayDialog(
                        "パーツ11個から確認",
                        "RustAndFloat シーンを開いて Play し、\nパーツ11/12・天蓋未開放から開始します（警告UIは出しません）。よろしいですか？",
                        "開始", "キャンセル"))
                    return;
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            _pendingJump = true;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.isPlaying = true;
            return;
        }

        TriggerJump();
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !_pendingJump)
            return;

        _pendingJump = false;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.delayCall += () =>
        {
            EditorApplication.delayCall += TriggerJump;
        };
    }

    static void TriggerJump()
    {
        var tower = AdventureSanctuaryTowerManager.Instance
                    ?? Object.FindFirstObjectByType<AdventureSanctuaryTowerManager>();
        if (tower == null)
        {
            Debug.LogWarning("[RustAndFloat] AdventureSanctuaryTowerManager がまだありません。数秒待って F9 を押してください。");
            EditorUtility.DisplayDialog(
                "パーツ11個から確認",
                "タワー管理者がまだ生成されていません。\nPlay開始後に Game ビューで【F9】を押してください。",
                "OK");
            return;
        }

        tower.DebugJumpToCanopyOpening();
        Debug.Log("[RustAndFloat] パーツ11/12からデバッグ起動しました（再実行は F9）");
    }
}
#endif
