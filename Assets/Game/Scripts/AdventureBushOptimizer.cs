using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 『Rust & Float』スタートからエンドまでの全体パフォーマンスを最適化する統合オプティマイザー。
/// 1. Idyllic Fantasy Nature の低木に付随する 3,403個の LeafTrigger（PhysX衝突＆パーティクル負荷）を一括無効化。
/// 2. 6,000個以上の草花（Flower, Grass, Plant）の不要なシャドウキャスト（影落とし）をオフにし、シャドウパス負荷を激減。
/// 3. URPの影描画距離（ShadowDistance 2,000m）を 140m へ適正化し、遠景の無駄なシャドウマップ描画を完全に排除。
/// 4. QualitySettings.lodBias（2.5）を 1.15 へ適正化し、遠方のポリゴン数・ドローコールを大幅カット。
/// </summary>
public static class AdventureBushOptimizer
{
    private static bool _initialized = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void Initialize()
    {
        ApplyAllOptimizations();
        SceneManager.sceneLoaded += (scene, mode) => ApplyAllOptimizations();
    }

    public static void ApplyAllOptimizations()
    {
        OptimizeAllBushTriggers();
        OptimizeVegetationShadows();
        OptimizeRenderingSettings();
        _initialized = true;
    }

    /// <summary>3,403個の低木トリガーコライダーおよびパーティクルを一括無効化</summary>
    public static void OptimizeAllBushTriggers()
    {
        var triggers = Object.FindObjectsByType<IdyllicFantasyNature.BushLeafTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (triggers == null || triggers.Length == 0) return;

        int optimizedCount = 0;
        for (int i = 0; i < triggers.Length; i++)
        {
            var t = triggers[i];
            if (t == null) continue;

            t.enabled = false;
            var col = t.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            var ps = t.GetComponentInChildren<ParticleSystem>(true);
            if (ps != null) ps.gameObject.SetActive(false);

            optimizedCount++;
        }

        Debug.Log($"[AdventureBushOptimizer] 🌿 低木トリガー {optimizedCount} 個を一括最適化しました（PhysX＆パーティクル負荷カット）");
    }

    /// <summary>草花・小石・水草（約6,000個）のシャドウキャストをオフにし、シャドウ描画パス負荷を大幅削減</summary>
    public static void OptimizeVegetationShadows()
    {
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (renderers == null || renderers.Length == 0) return;

        int turnedOff = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            string n = r.gameObject.name;
            if (n.StartsWith("Flower") || n.StartsWith("Grass") || n.StartsWith("Plant") ||
                n.StartsWith("Reeds") || n.StartsWith("Waterlily") || n.StartsWith("Cattail"))
            {
                if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    turnedOff++;
                }
            }
        }

        Debug.Log($"[AdventureBushOptimizer] 🌸 草花 {turnedOff} 個のシャドウキャストを最適化（影描画パスを削減）");
    }

    /// <summary>URPシャドウ描画距離とLODバイアスを適正化</summary>
    public static void OptimizeRenderingSettings()
    {
        // 1. URP Shadow Distance（2,000m → 140m）
        var urpAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset != null)
        {
            if (urpAsset.shadowDistance > 150f)
            {
                urpAsset.shadowDistance = 140f;
                Debug.Log("[AdventureBushOptimizer] ☀️ URP ShadowDistance を 140m に最適化しました");
            }
        }

        // 2. QualitySettings.lodBias（2.5 → 1.15）
        if (QualitySettings.lodBias > 1.25f)
        {
            QualitySettings.lodBias = 1.15f;
            Debug.Log("[AdventureBushOptimizer] 📐 QualitySettings.lodBias を 1.15 に適正化しました");
        }
    }
}
