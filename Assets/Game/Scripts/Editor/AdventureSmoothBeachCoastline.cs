using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;

/// <summary>
/// 砂浜から海（波打ち際・海底）への段差・垂直崖を解消し、
/// なだらかで美しい遠浅の海岸線へとスムーズに繋ぐエディタ拡張。
/// </summary>
public static class AdventureSmoothBeachCoastline
{
    private const string ScenePath = "Assets/RustAndFloat/Scenes/RustAndFloat.unity";
    private const string TerrainPath = "Assets/RustAndFloat/Terrain/IslandTerrain.asset";

    [MenuItem("Adventure/🏝️ Smooth Beach to Sea (砂浜から海への段差解消・スムーズ接続)", false, 12)]
    public static void SmoothBeachToSeaMenu()
    {
        ExecuteSmoothBeach(true);
    }

    public static void ExecuteSmoothBeach(bool interactive)
    {
        if (EditorApplication.isPlaying)
        {
            if (interactive)
                EditorUtility.DisplayDialog("停止してください", "■で再生を止めてから実行してください。", "OK");
            return;
        }

        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != ScenePath)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var td = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
        var land = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude)
            .FirstOrDefault(t => t.name == "LandTerrain" || t.name == "IslandTerrain");

        if (td == null || land == null)
        {
            Debug.LogError("[SmoothBeachCoastline] IslandTerrain が見つかりません。");
            return;
        }

        Undo.RecordObject(td, "Smooth Beach to Sea");

        int res = td.heightmapResolution;
        float[,] heights = td.GetHeights(0, 0, res, res);

        float islandW = td.size.x; // 1024
        float islandH = td.size.y; // 120
        float cx = (res - 1) * 0.5f;
        float cz = (res - 1) * 0.5f;
        float islandR = (res - 1) * 0.44f; // 半径約450.56m

        const float waterY = 5.5f;
        const float seaFloorY = 2.2f;
        float seaH = waterY / islandH;          // 5.5 / 120 = 0.045833
        float floorH = seaFloorY / islandH;     // 2.2 / 120 = 0.018333

        // ====================================================================
        // 1. ハイトマップの滑らかな遠浅スロープ整形
        // ====================================================================
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float wx = x / (float)(res - 1) * islandW;
                float wz = z / (float)(res - 1) * islandW;

                // 北の大滑空崖 (x: 460-564, z > 750) は意図的な急崖のため維持
                if (wz > 748f && Mathf.Abs(wx - 512f) < 58f)
                {
                    continue;
                }

                float dx = (x - cx) / islandR;
                float dz = (z - cz) / islandR;
                float d = Mathf.Sqrt(dx * dx + dz * dz);

                // 内陸（d <= 0.86）は既存の地形をそのまま維持
                if (d <= 0.86f)
                {
                    continue;
                }

                float curH = heights[z, x];
                float targetH;

                if (d >= 1.06f)
                {
                    // 沖合の安定した平坦な海底 (2.2m)
                    targetH = floorH;
                }
                else if (d >= 0.975f)
                {
                    // 波打ち際 (d=0.975, 5.5m) から浅瀬〜海底 (d=1.06, 2.2m) への滑らかな遠浅スロープ
                    // 幅約38mをかけて、傾斜約5度でなだらかに深くなる（一切の段差なし）
                    float t = Mathf.InverseLerp(0.975f, 1.06f, d);
                    // Hermite SmoothStep で波打ち際と海底の接合を連続・滑らかに
                    float s = t * t * (3f - 2f * t);
                    targetH = Mathf.Lerp(seaH, floorH, s);
                }
                else if (d >= 0.92f)
                {
                    // 砂浜渚 (d=0.92, 7.2m) から波打ち際 (d=0.975, 5.5m) への緩やかな砂浜スロープ
                    float t = Mathf.InverseLerp(0.92f, 0.975f, d);
                    float s = t * t * (3f - 2f * t);
                    targetH = Mathf.Lerp(7.2f / islandH, seaH, s);
                }
                else
                {
                    // 砂浜テラス (d=0.86〜0.92, 8.5m → 7.2m)
                    float t = Mathf.InverseLerp(0.86f, 0.92f, d);
                    float s = t * t * (3f - 2f * t);
                    targetH = Mathf.Lerp(8.5f / islandH, 7.2f / islandH, s);
                }

                // 微小な砂のテクスチャ起伏（最大数センチ）を付与して自然な砂浜感を演出
                if (d < 1.05f)
                {
                    float sandRipple = (Mathf.PerlinNoise(wx * 0.05f + 17f, wz * 0.05f + 17f) - 0.5f) * (0.08f / islandH);
                    targetH += sandRipple;
                }

                // 既存の砂浜起伏（d=0.86〜0.93）と新プロファイルを穏やかにクロスブレンド
                if (d < 0.93f)
                {
                    float blendToExisting = Mathf.InverseLerp(0.93f, 0.86f, d);
                    heights[z, x] = Mathf.Lerp(targetH, curH, blendToExisting);
                }
                else
                {
                    heights[z, x] = targetH;
                }
            }
        }

        td.SetHeights(0, 0, heights);

        // ====================================================================
        // 2. Alphamap（テクスチャ）の波打ち際・浅瀬美化
        // ====================================================================
        UpdateShorelineAlphamaps(td, land, islandW, waterY);

        land.Flush();
        EditorUtility.SetDirty(td);
        EditorSceneManager.MarkSceneDirty(activeScene);

        Debug.Log("<color=#00FFAA><b>[SmoothBeachCoastline]</b> 砂浜から海への段差を解消し、滑らかな遠浅海岸線に整形完了しました！</color>");
    }

    private static void UpdateShorelineAlphamaps(TerrainData td, Terrain land, float islandW, float waterY)
    {
        int aRes = td.alphamapResolution;
        int layerCount = td.alphamapLayers;
        if (layerCount < 3) return;

        float[,,] alphas = td.GetAlphamaps(0, 0, aRes, aRes);

        for (int z = 0; z < aRes; z++)
        {
            for (int x = 0; x < aRes; x++)
            {
                float nx = x / (float)(aRes - 1);
                float nz = z / (float)(aRes - 1);
                float wx = nx * td.size.x;
                float wz = nz * td.size.z;
                float hy = land.SampleHeight(new Vector3(wx, 0f, wz));
                float ny = td.GetInterpolatedNormal(nx, nz).y;

                // 垂直に近い急崖のみ岩肌に（北崖など）
                float steepness = 1f - Mathf.Clamp01((ny - 0.32f) / 0.15f);
                float rockW = Mathf.Clamp01(steepness * 2.0f);

                // 水面(5.5m)付近および海底浅瀬(〜3.0m)は岩肌を抑制し、全面を美しい白砂に
                if (hy < waterY + 1.5f && hy > 2.0f)
                {
                    rockW *= Mathf.Clamp01((hy - 3.5f) / 1.5f);
                }

                // 白砂ビーチ：海面(5.5m)から海底(2.2m)および陸上(8.5mまで)
                float sandW = 0f;
                if (hy < waterY + 3.0f)
                {
                    sandW = Mathf.Clamp01(1f - (hy - (waterY + 0.5f)) / 2.5f);
                }
                // 海面下はすべて白砂
                if (hy <= waterY + 0.5f)
                {
                    sandW = 1.0f;
                }

                // 残りの領域は大草原
                float grassW = Mathf.Clamp01(1f - Mathf.Max(sandW, rockW));

                float sum = grassW + sandW + rockW;
                if (sum < 0.001f)
                {
                    grassW = 1f;
                    sum = 1f;
                }

                alphas[z, x, 0] = grassW / sum; // Grass
                alphas[z, x, 1] = sandW / sum;  // Sand
                alphas[z, x, 2] = rockW / sum;  // Rock
            }
        }

        td.SetAlphamaps(0, 0, alphas);
    }

    [MenuItem("Adventure/🌿 Snap Beach Props to Terrain (砂浜の浮いた草・木・岩の接地修正)", false, 13)]
    public static void SnapBeachPropsToTerrainMenu()
    {
        SnapBeachPropsToTerrain(true);
    }

    /// <summary>
    /// 砂浜エリア（d > 0.82）にある草、低木、ヤシの木、木立、岩などの高さを現在のTerrainに密着接地させる。
    /// また、海面下（< 5.45m）に水没した陸上植物（Bush, Tree, Palm）を非アクティブ化して自然な水辺にする。
    /// </summary>
    public static void SnapBeachPropsToTerrain(bool interactive = false)
    {
        if (EditorApplication.isPlaying)
        {
            if (interactive)
                EditorUtility.DisplayDialog("停止してください", "■で再生を止めてから実行してください。", "OK");
            return;
        }

        var terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("[SmoothBeachCoastline] Terrain が見つかりません。");
            return;
        }

        var allGos = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int snappedCount = 0;
        int disabledUnderwaterCount = 0;

        foreach (var go in allGos)
        {
            if (go == null) continue;

            string name = go.name;
            // 除外対象：プレイヤー、カメラ、マネージャー、Terrain、海面、システムオブジェクト
            if (name == "Niko" || name == "Rust" || name == "OceanPlane" || name == "LandTerrain" ||
                name.Contains("Camera") || name.Contains("Manager") || name.Contains("Director") ||
                name.Contains("Boundary") || name.Contains("EventSystem") || name.Contains("HUD") ||
                name.Contains("UI") || name.Contains("Light") || name.Contains("Clouds") ||
                name.Contains("Island") || name.Contains("Audio") || name.Contains("Marker"))
                continue;

            // プロップの判定
            bool isProp = false;
            if (go.transform.parent != null &&
                (go.transform.parent.name == "Paradise" ||
                 go.transform.parent.name.Contains("Beach") ||
                 go.transform.parent.name.Contains("Rocks") ||
                 go.transform.parent.name.Contains("Props") ||
                 go.transform.parent.name.Contains("Flora") ||
                 go.transform.parent.name.Contains("Animals")))
            {
                isProp = true;
            }
            else if (go.transform.parent == null)
            {
                if (name.Contains("Palm") || name.Contains("Bush") || name.Contains("Tree") ||
                    name.Contains("Rock") || name.Contains("Stone") || name.Contains("Crab") ||
                    name.Contains("Capybara") || name.Contains("Flotsam") || name.Contains("Grass") ||
                    name.Contains("Plant") || name.Contains("Flower") || name.Contains("Reeds") ||
                    name.Contains("Cattail") || name.Contains("Waterlily"))
                {
                    isProp = true;
                }
            }

            if (!isProp) continue;

            Vector3 pos = go.transform.position;
            float dx = pos.x - 512f;
            float dz = pos.z - 512f;
            float d = Mathf.Sqrt(dx * dx + dz * dz) / 450.56f;

            // 砂浜〜海岸線エリア (d > 0.82)
            if (d > 0.82f)
            {
                float terrainY = terrain.SampleHeight(pos);

                // 海面下（5.45m未満）にある陸上植物（Bush, Tree, Palm）は非アクティブ化
                if (terrainY < 5.45f && (name.Contains("Bush") || name.Contains("Tree") || name.Contains("Palm")))
                {
                    if (go.activeSelf)
                    {
                        Undo.RecordObject(go, "Disable Underwater Plant");
                        go.SetActive(false);
                        EditorUtility.SetDirty(go);
                        disabledUnderwaterCount++;
                    }
                    continue;
                }

                // プロップ種類ごとの適正接地高さ
                float targetY = terrainY;
                if (name.Contains("Rock") || name.Contains("Stone"))
                {
                    targetY = terrainY - 0.05f; // 岩は少し土や砂に埋め込んで安定感を出す
                }
                else if (name.Contains("Flotsam"))
                {
                    targetY = terrainY + 0.02f;
                }

                if (Mathf.Abs(pos.y - targetY) > 0.015f)
                {
                    Undo.RecordObject(go.transform, "Snap Beach Prop to Ground");
                    go.transform.position = new Vector3(pos.x, targetY, pos.z);
                    EditorUtility.SetDirty(go);
                    snappedCount++;
                }
            }
        }

        var activeScene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log($"<color=#00FFAA><b>[SmoothBeachCoastline]</b> 砂浜のプロップ {snappedCount} 個をTerrainに接地させ、水没陸上植物 {disabledUnderwaterCount} 個を非アクティブ化しました！</color>");
    }
}
