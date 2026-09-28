using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 初代の映画的ファンタジー空（大気フォグ・ACESトーンマッピング・水色Bloom）を
/// ゲーム実行時（Play Mode）に確実に維持・適用するランタイムヘルパー。
/// </summary>
public class AdventureClassicSkyRuntime : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnSceneLoaded()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "RustAndFloat") return;

        EnsureClassicAtmosphere();
    }

    public static void EnsureClassicAtmosphere()
    {
        // 1. スカイボックス
        #if UNITY_EDITOR
        var skyMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Idyllic Fantasy Nature/Materials/Skybox/Skybox.mat");
        if (skyMat != null)
        {
            RenderSettings.skybox = skyMat;
        }
        #endif

        // 2. 大気フォグ（爽やかで透明感のあるミントブルー）
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.627451f, 1f, 0.9764706f, 1f);
        RenderSettings.fogDensity = 0.0025f; // 見晴らしを良くするためわずかに密度を軽減
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 0.96f; // 眩しさを抑えた落ち着いたシネマ的環境光
        RenderSettings.ambientSkyColor = new Color(0.65f, 0.80f, 1.0f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.88f, 0.90f, 0.82f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.42f, 0.55f, 0.35f, 1f);

        // 3. 太陽光（ギラつきを抑え、優しく照らす南国・ファンタジーの日差し）
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

        // 4. MainCamera の PostProcessing 有効化
        var cam = Camera.main;
        if (cam != null)
        {
            var addData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (addData == null)
            {
                addData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }
            if (addData != null)
            {
                addData.renderPostProcessing = true;
                addData.volumeLayerMask = ~0;
            }
        }

        // 5. PostProcessing Volume の存在保証と明るさ・ブルーム補正
        var postGo = GameObject.Find("PostProcessing");
        if (postGo == null)
        {
            postGo = new GameObject("PostProcessing");
        }
        var vol = postGo.GetComponent<Volume>();
        if (vol == null)
        {
            vol = postGo.AddComponent<Volume>();
        }
        vol.isGlobal = true;
        vol.weight = 1f;

        #if UNITY_EDITOR
        if (vol.sharedProfile == null)
        {
            var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Idyllic Fantasy Nature/Demo/Settings/Post-Processing.asset");
            if (profile != null)
            {
                vol.sharedProfile = profile;
            }
        }
        #endif

        // ポストプロセスの露出とブルームを調整して眩しさを上品にカット
        if (vol.profile != null)
        {
            if (vol.profile.TryGet<ColorAdjustments>(out var ca))
            {
                ca.postExposure.overrideState = true;
                ca.postExposure.value = -0.08f; // ギラつきのない落ち着いた露出
            }
            if (vol.profile.TryGet<Bloom>(out var bloom))
            {
                bloom.intensity.overrideState = true;
                bloom.intensity.value = 0.58f; // 眩しすぎない上品な水色ブルーム
                bloom.threshold.overrideState = true;
                bloom.threshold.value = 1.18f; // 白飛び・面発光を抑える
            }
        }
    }
}
