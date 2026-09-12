using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ハワイアセット（Hawaii Beach House）の高精細モデルを使用した
/// 背の高いリアルな椰子の木（Palm Tree）ファクトリー
/// </summary>
public static class AdventurePalmFactory
{
    const string MatPath = "Assets/RustAndFlat/Materials/HawaiiPalm_URP.mat";

    static readonly string[] PalmPrefabPaths =
    {
        "Assets/Hawaii Beach House (PBR, HDRP)/Prefabs/Jungle_Plant_7.prefab",
        "Assets/Hawaii Beach House (PBR, HDRP)/Prefabs/Jungle_Plant_8.prefab",
        "Assets/Hawaii Beach House (PBR, HDRP)/Prefabs/Jungle_Plant_9.prefab",
        "Assets/Hawaii Beach House (PBR, HDRP)/Prefabs/Jungle_Plant_10.prefab"
    };

    static Material _urpMat;

    public static void ResetMaterials()
    {
        _urpMat = null;
    }

    public static Material GetMaterial()
    {
        if (_urpMat != null) return _urpMat;
#if UNITY_EDITOR
        _urpMat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
#endif
        if (_urpMat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _urpMat = new Material(shader);
        }
        return _urpMat;
    }

    public static GameObject Create(Transform parent, Vector3 pos, int seed)
    {
        var rng = new System.Random(seed);
        string prefabPath = PalmPrefabPaths[rng.Next(PalmPrefabPaths.Length)];

        GameObject palmGo = null;
#if UNITY_EDITOR
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab != null)
        {
            palmGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        }
#endif
        if (palmGo == null)
        {
            var resPrefab = Resources.Load<GameObject>(prefabPath);
            if (resPrefab != null)
                palmGo = Object.Instantiate(resPrefab, parent);
        }

        if (palmGo == null)
        {
            Debug.LogWarning("[AdventurePalmFactory] ヤシの木プレハブが見つかりません: " + prefabPath);
            return null;
        }

        palmGo.name = "Hawaii_Palm";
        palmGo.transform.position = pos;

        // 自然な傾きとランダムな向き
        float leanX = ((float)rng.NextDouble() - 0.5f) * 14f;
        float leanZ = ((float)rng.NextDouble() - 0.5f) * 14f;
        float yaw = (float)rng.NextDouble() * 360f;
        palmGo.transform.rotation = Quaternion.Euler(leanX, yaw, leanZ);

        // 背の高い堂々たるヤシの木（スケール 3.2 〜 5.2倍、高さ約12m〜20m）
        float s = 3.2f + (float)rng.NextDouble() * 2.0f;
        palmGo.transform.localScale = Vector3.one * s;

        // URP対応の美麗テクスチャマテリアルを確実に適用
        var mat = GetMaterial();
        if (mat != null)
        {
            var renderers = palmGo.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
        }

        return palmGo;
    }
}
