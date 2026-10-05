using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 楽園ファンタジー空（大気フォグ・楽園日光・水色Bloom）を
/// Play Mode に確実に適用するランタイムヘルパー。
/// エディタ拡張 AdventureRestoreClassicSkyTool もここの定数を参照する。
/// </summary>
public class AdventureClassicSkyRuntime : MonoBehaviour
{
    // ─── ライティング定数 ────────────────────────────────────────
    public const string SkyboxPath        = "Assets/Idyllic Fantasy Nature/Materials/Skybox/Skybox.mat";
    public const string PostProfilePath   = "Assets/Idyllic Fantasy Nature/Demo/Settings/Post-Processing.asset";

    // 大気フォグ
    public static readonly Color  FogColor          = new Color(0.627451f, 1f, 0.9764706f, 1f);
    public const  float           FogDensity         = 0.0010f; // 霞みを抑えて遠景の海・空を澄み渡らせる

    // 環境光（Trilightモードで影が黒く沈むのを防止し、白砂・草原の照り返しを明るく表現）
    public const  float           AmbientIntensity   = 1.35f;
    public static readonly Color  AmbientSkyColor    = new Color(0.72f, 0.86f, 1.0f, 1f);     // 青空の反射光
    public static readonly Color  AmbientEquator     = new Color(0.92f, 0.94f, 0.88f, 1f);    // 水平線の柔らかい光
    public static readonly Color  AmbientGround      = new Color(0.55f, 0.68f, 0.48f, 1f);    // 白砂・草原の照り返し

    // 太陽光
    public static readonly Color  SunColor           = new Color(1f, 0.98f, 0.90f, 1f);
    public const  float           SunIntensity       = 1.65f; // 暖かく明るい南国の陽光

    // ポストプロセス
    public const  float           PostExposure       = 0.35f; // 全体の露出を底上げして明るく爽やかな画面に
    public const  float           Contrast           = -4f;
    public const  float           BloomIntensity     = 0.28f;
    public const  float           BloomThreshold     = 1.18f;
    // ─────────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnSceneLoaded()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "RustAndFloat") return;
        EnsureClassicAtmosphere();
    }

    /// <summary>ランタイム・エディタ両方から呼べる大気一括適用エントリポイント。</summary>
    public static void EnsureClassicAtmosphere()
    {
        ApplySkybox();
        ApplyFogAndAmbient();
        ApplySunLight();
        EnsurePostProcessingVolume();
        AdventureCloudDrift.EnsureCloudSystem();
    }

    // ─── サブメソッド ─────────────────────────────────────────────

    private static void ApplySkybox()
    {
#if UNITY_EDITOR
        var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
        if (mat != null) RenderSettings.skybox = mat;
#endif
    }

    private static void ApplyFogAndAmbient()
    {
        RenderSettings.fog              = true;
        RenderSettings.fogMode          = FogMode.ExponentialSquared;
        RenderSettings.fogColor         = FogColor;
        RenderSettings.fogDensity       = FogDensity;
        RenderSettings.ambientMode      = AmbientMode.Trilight;
        RenderSettings.ambientIntensity = AmbientIntensity;
        RenderSettings.ambientSkyColor  = AmbientSkyColor;
        RenderSettings.ambientEquatorColor = AmbientEquator;
        RenderSettings.ambientGroundColor  = AmbientGround;
    }

    private static void ApplySunLight()
    {
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional) continue;
            RenderSettings.sun = l;
            l.color     = SunColor;
            l.intensity = SunIntensity;
            break;
        }
    }

    private static void EnsurePostProcessingVolume()
    {
        // Volume オブジェクトを取得または生成
        var postGo = GameObject.Find("PostProcessing") ?? new GameObject("PostProcessing");
        var vol    = postGo.GetComponent<Volume>() ?? postGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.weight   = 1f;

        // エディタ時のみプロファイルを割り当て
#if UNITY_EDITOR
        if (vol.sharedProfile == null)
        {
            var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);
            if (profile != null) vol.sharedProfile = profile;
        }
#endif

        // カメラの PostProcessing 有効化
        var cam = Camera.main;
        if (cam != null)
        {
            var camData = cam.GetComponent<UniversalAdditionalCameraData>()
                       ?? cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.volumeLayerMask      = ~0;
        }

        // ポストプロセスパラメータの上書き
        if (vol.profile == null) return;
        if (vol.profile.TryGet<ColorAdjustments>(out var ca))
        {
            ca.postExposure.Override(PostExposure);
            ca.contrast.Override(Contrast);
        }
        if (vol.profile.TryGet<Bloom>(out var bloom))
        {
            bloom.intensity.Override(BloomIntensity);
            bloom.threshold.Override(BloomThreshold);
        }
    }
}
