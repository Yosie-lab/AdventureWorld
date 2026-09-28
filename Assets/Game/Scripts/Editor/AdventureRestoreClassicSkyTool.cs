using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 昔の島（AdventureWorld / 初代RustAndFloat）の美しい空（太陽光・大気フォグ・ふんわり漂う雲）を
/// 現在の RustAndFloat シーンに完全復元するエディタ拡張ツール。
/// </summary>
public static class AdventureRestoreClassicSkyTool
{
    private const string CLASSIC_SKYBOX_PATH = "Assets/Idyllic Fantasy Nature/Materials/Skybox/Skybox.mat";
    private const string CLASSIC_POST_PROCESSING_PATH = "Assets/Idyllic Fantasy Nature/Demo/Settings/Post-Processing.asset";

    [InitializeOnLoadMethod]
    private static void OnEditorLoad()
    {
        EditorApplication.delayCall += () =>
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.isLoaded && activeScene.path.Contains("RustAndFloat"))
            {
                ApplyClassicSkySettings(save: false);
            }
        };
    }

    [MenuItem("Adventure/🌤️ 初代映画的ファンタジー空＆雲を完全復元 (Restore Classic Cinematic Sky)", priority = 205)]
    public static void RestoreClassicSkyAndClouds()
    {
        ApplyClassicSkySettings(save: true);
    }

    public static void ApplyClassicSkySettings(bool save = true)
    {
        // 1. スカイボックスの復元
        var skyMat = AssetDatabase.LoadAssetAtPath<Material>(CLASSIC_SKYBOX_PATH);
        if (skyMat != null)
        {
            RenderSettings.skybox = skyMat;
            Debug.Log($"[AdventureRestoreClassicSky] ✅ スカイボックスを設定: {CLASSIC_SKYBOX_PATH}");
        }
        else
        {
            Debug.LogWarning($"[AdventureRestoreClassicSky] ⚠️ スカイボックスマテリアルが見つかりませんでした: {CLASSIC_SKYBOX_PATH}");
        }

        // 2. 大気フォグの復元（ミントブルーの爽やかな大気感）
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.627451f, 1f, 0.9764706f, 1f);
        RenderSettings.fogDensity = 0.0025f; // 見晴らしを良くするため密度を微調整
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 0.96f; // 眩しさを抑えた落ち着いたシネマ的環境光
        RenderSettings.ambientSkyColor = new Color(0.65f, 0.80f, 1.0f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.88f, 0.90f, 0.82f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.42f, 0.55f, 0.35f, 1f);

        // 3. 太陽光ライト（Directional Light）の映画的トーン連動（ギラつきを抑えた南国日差し）
        var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in lights)
        {
            if (l.type == LightType.Directional)
            {
                RenderSettings.sun = l;
                l.color = new Color(1f, 0.97f, 0.88f, 1f);
                l.intensity = 1.00f; // 白飛びせず澄んだ島並みを照らす適正輝度
                break;
            }
        }

        // 4. PostProcessing Volume の復元（ACESトーンマッピング、水色Bloom、被写界深度、色調補正）
        var postGo = GameObject.Find("PostProcessing");
        if (postGo == null)
        {
            postGo = new GameObject("PostProcessing");
            Undo.RegisterCreatedObjectUndo(postGo, "Create PostProcessing Volume");
        }
        var volume = postGo.GetComponent<UnityEngine.Rendering.Volume>();
        if (volume == null)
        {
            volume = postGo.AddComponent<UnityEngine.Rendering.Volume>();
        }
        volume.isGlobal = true;
        volume.weight = 1f;

        var postProfile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(CLASSIC_POST_PROCESSING_PATH);
        if (postProfile != null)
        {
            volume.sharedProfile = postProfile;
            Debug.Log($"[AdventureRestoreClassicSky] ✅ 初代Post-Processingプロファイルを割り当て: {CLASSIC_POST_PROCESSING_PATH}");
        }

        // ポストプロセスの露出とブルームを調整して眩しさを上品にカット
        if (volume.profile != null)
        {
            if (volume.profile.TryGet<UnityEngine.Rendering.Universal.ColorAdjustments>(out var ca))
            {
                ca.postExposure.overrideState = true;
                ca.postExposure.value = -0.08f; // ギラつきのない落ち着いた露出
            }
            if (volume.profile.TryGet<UnityEngine.Rendering.Universal.Bloom>(out var bloom))
            {
                bloom.intensity.overrideState = true;
                bloom.intensity.value = 0.58f; // 眩しすぎない上品な水色ブルーム
                bloom.threshold.overrideState = true;
                bloom.threshold.value = 1.18f; // 白飛び・面発光を抑える
            }
        }

        // 5. Main Camera の PostProcessing を有効化
        var cam = Camera.main;
        if (cam != null)
        {
            var addCamData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (addCamData == null)
            {
                addCamData = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            }
            if (addCamData != null)
            {
                addCamData.renderPostProcessing = true;
                addCamData.volumeLayerMask = ~0; // 全Volumeレイヤーを反映
                Debug.Log("[AdventureRestoreClassicSky] ✅ MainCamera の PostProcessing 描画を有効化しました。");
            }
        }

        // 6. ふんわり雲システム（ParadiseClouds / RustFloat_Clouds）の生成・整備
        AdventureCloudDrift.EnsureCloudSystem();

        // 7. シーンの変更を保存
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.isLoaded)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            if (save)
            {
                EditorSceneManager.SaveScene(activeScene);
            }
        }

        Debug.Log("🎉 【初代映画的ファンタジー空＆雲】PostProcessing（ACES・水色Bloom）と大気フォグを完全復元しました！");
    }

    [MenuItem("Adventure/🌅 イントロ・遭難目覚まし演出をテスト再生 (Play Awakening Intro)", priority = 210)]
    public static void PlayAwakeningIntroMenu()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("情報", "Play（再生）中に実行してください。\nゲーム中にこのメニューを押すと、いつでもイントロ目覚まし演出をテスト再生できます。", "OK");
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
        if (EditorUtility.DisplayDialog("確認", "セーブデータおよび探索進捗を完全消去し、最初から（オープニング・遭難目覚まし演出）やり直しますか？", "リセットする", "キャンセル"))
        {
            var saveMgr = Object.FindAnyObjectByType<AdventureSaveManager>();
            if (saveMgr != null)
            {
                saveMgr.ResetToNewGame();
            }
            else
            {
                // セーブファイルの直接削除
                string savePath = System.IO.Path.Combine(Application.persistentDataPath, "rust_and_float_save.json");
                if (System.IO.File.Exists(savePath)) System.IO.File.Delete(savePath);
                if (System.IO.File.Exists(savePath + ".bak")) System.IO.File.Delete(savePath + ".bak");
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
            }
            Debug.Log("🔄 セーブデータを完全初期化しました。Play（再生）すると最初からオープニングと目覚まし演出が再生されます。");
        }
    }
}
