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

    [MenuItem("Adventure/🌤️ 昔の空と雲を完全復元 (Classic Sky & Clouds)", priority = 205)]
    public static void RestoreClassicSkyAndClouds()
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
        RenderSettings.fogDensity = 0.003f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;

        // 3. 太陽光ライト（Directional Light）の連動
        var lights = Object.FindObjectsByType<Light>();
        foreach (var l in lights)
        {
            if (l.type == LightType.Directional)
            {
                RenderSettings.sun = l;
                break;
            }
        }

        // 4. ふんわり雲システム（ParadiseClouds / RustFloat_Clouds）の生成・整備
        AdventureCloudDrift.EnsureCloudSystem();

        // 5. シーンの変更を保存可能状態にする
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("🎉 昔の美しい空（プロシージャル太陽光・大気フォグ）とふんわり雲システムを復元しました！");
    }
}
