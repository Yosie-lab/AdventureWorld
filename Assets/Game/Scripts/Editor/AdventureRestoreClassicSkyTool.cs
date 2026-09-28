using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// RustAndFloat の楽園ライティング設定をエディタから適用するツール。
/// ライティング定数・適用ロジックはランタイム側 AdventureClassicSkyRuntime に一元管理し、
/// このクラスはエディタ固有の処理（Undo・保存・メニュー）のみを担う。
/// </summary>
public static class AdventureRestoreClassicSkyTool
{
    // ─── エディタ自動適用 ─────────────────────────────────────────

    [InitializeOnLoadMethod]
    private static void OnEditorLoad()
    {
        EditorApplication.delayCall += () =>
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.isLoaded && scene.path.Contains("RustAndFloat"))
                ApplyClassicSkySettings(save: false);
        };
    }

    // ─── メニューコマンド ─────────────────────────────────────────

    [MenuItem("Adventure/🌤️ 初代映画的ファンタジー空＆雲を完全復元 (Restore Classic Cinematic Sky)", priority = 205)]
    public static void RestoreClassicSkyAndClouds() => ApplyClassicSkySettings(save: true);

    [MenuItem("Adventure/🌅 イントロ・遭難目覚まし演出をテスト再生 (Play Awakening Intro)", priority = 210)]
    public static void PlayAwakeningIntroMenu()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("情報",
                "Play（再生）中に実行してください。\nゲーム中にこのメニューを押すと、いつでもイントロ目覚まし演出をテスト再生できます。", "OK");
            return;
        }
        AdventurePrologueDrama.Ensure();
        var drama = AdventurePrologueDrama.Instance;
        if (drama != null)
        {
            drama.PlayAwakeningIntro(force: true);
            Debug.Log("[AdventureRestoreClassicSky] 🌅 イントロ目覚まし演出を強制再生しました。");
        }
        else
        {
            Debug.LogWarning("[AdventureRestoreClassicSky] ⚠️ AdventurePrologueDrama が見つかりませんでした。");
        }
    }

    [MenuItem("Adventure/🔄 完全ニューゲーム（セーブ消去して最初から）", priority = 211)]
    public static void ResetToNewGameMenu()
    {
        if (!EditorUtility.DisplayDialog("確認",
            "セーブデータおよび探索進捗を完全消去し、最初から（オープニング・遭難目覚まし演出）やり直しますか？",
            "リセットする", "キャンセル")) return;

        var saveMgr = Object.FindAnyObjectByType<AdventureSaveManager>();
        if (saveMgr != null)
        {
            saveMgr.ResetToNewGame();
        }
        else
        {
            string savePath = System.IO.Path.Combine(Application.persistentDataPath, "rust_and_float_save.json");
            if (System.IO.File.Exists(savePath))        System.IO.File.Delete(savePath);
            if (System.IO.File.Exists(savePath + ".bak")) System.IO.File.Delete(savePath + ".bak");
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }
        Debug.Log("🔄 セーブデータを完全初期化しました。Play（再生）すると最初からオープニングと目覚まし演出が再生されます。");
    }

    // ─── コアロジック ─────────────────────────────────────────────

    /// <summary>
    /// ライティング・PostProcessing・雲を楽園設定に復元する。
    /// ロジックは AdventureClassicSkyRuntime.EnsureClassicAtmosphere() に委譲する。
    /// </summary>
    public static void ApplyClassicSkySettings(bool save = true)
    {
        // スカイボックス（エディタ専用パス）
        var skyMat = AssetDatabase.LoadAssetAtPath<Material>(AdventureClassicSkyRuntime.SkyboxPath);
        if (skyMat != null)
            RenderSettings.skybox = skyMat;
        else
            Debug.LogWarning($"[AdventureRestoreClassicSky] ⚠️ スカイボックスが見つかりません: {AdventureClassicSkyRuntime.SkyboxPath}");

        // ライティング・PostProcessing 適用（ランタイムと共通ロジック）
        AdventureClassicSkyRuntime.EnsureClassicAtmosphere();

        // ふんわり雲システムの保証
        AdventureCloudDrift.EnsureCloudSystem();

        // シーンの変更フラグ＆保存
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.isLoaded) return;
        EditorSceneManager.MarkSceneDirty(scene);
        if (save) EditorSceneManager.SaveScene(scene);

        Debug.Log("🎉 【楽園ファンタジー空＆雲】ライティングを完全復元しました！");
    }
}
