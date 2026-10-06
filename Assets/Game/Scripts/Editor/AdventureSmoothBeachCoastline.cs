using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 直線的・カクカク・四角い海岸線や四角い水平線・直線残骸を完全に解消し、
/// リアルで自然なゆるやかな曲線の白砂浜海岸線、滑らかな遠浅グラデーション、
/// および360度完全円盤の水平線を構築するエディタ拡張ツール。
/// </summary>
public static class AdventureSmoothBeachCoastline
{
    [MenuItem("Adventure/🏝️ Shape Natural Curved Coastline & Horizon (自然な曲線の海岸線・丸い水平線の構築)", false, 12)]
    public static void ShapeNaturalCoastlineAndHorizonMenu()
    {
        ExecuteNaturalCoastline(true);
    }

    public static void ExecuteNaturalCoastline(bool interactive)
    {
        if (EditorApplication.isPlaying)
        {
            if (interactive)
                EditorUtility.DisplayDialog("停止してください", "■で再生を止めてから実行してください。", "OK");
            return;
        }

        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != "Assets/RustAndFloat/Scenes/RustAndFloat.unity")
        {
            EditorSceneManager.OpenScene("Assets/RustAndFloat/Scenes/RustAndFloat.unity", OpenSceneMode.Single);
            activeScene = EditorSceneManager.GetActiveScene();
        }

        var land = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude)
            .FirstOrDefault(t => t.name == "LandTerrain" || t.name == "IslandTerrain");

        if (land == null)
        {
            Debug.LogError("[NaturalCoastline] LandTerrain または IslandTerrain が見つかりません。");
            return;
        }

        TerrainData td = land.terrainData;
        Undo.RecordObject(td, "Shape Natural Coastline & Organic Reef");

        int hRes = td.heightmapResolution;
        float[,] heights = td.GetHeights(0, 0, hRes, hRes);

        float islandH = td.size.y; // 600m
        float waterY = 5.5f;       // 海面海抜
        float seaH = waterY / islandH;

        float cX = td.size.x * 0.5f; // 512m
        float cZ = td.size.z * 0.5f; // 512m

        // ====================================================================
        // 1. 地形ハイトマップの自然曲線化（島全体を取り囲む優美なオーバル形状）
        // ====================================================================
        for (int z = 0; z < hRes; z++)
        {
            for (int x = 0; x < hRes; x++)
            {
                float wx = (x / (float)(hRes - 1)) * td.size.x;
                float wz = (z / (float)(hRes - 1)) * td.size.z;

                float dx = wx - cX;
                float dz = wz - cZ;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);

                // 内陸コア（dist <= 260m）は完全に維持
                if (dist <= 260f) continue;

                float angle = Mathf.Atan2(dz, dx);
                float deg = angle * Mathf.Rad2Deg;

                // ------------------------------------------------------------
                // 均一で優美な「自然にゆるやかなオーバル島」の輪郭
                // 局所的な四角い台形突出を一切排し、どこから見ても自然な弧を描く
                // ------------------------------------------------------------
                float cosA = Mathf.Cos(angle);
                float sinA = Mathf.Sin(angle);

                // 1) 島全体のオーバル形状（長軸・短軸の穏やかな調和）
                float oval = Mathf.Cos(angle * 2f - 0.4f) * 12f;
                // 2) 広域マクロうねり（波長約200m〜300mの自然な有機的カーブ）
                float wMacro = (Mathf.PerlinNoise(cosA * 0.6f + 25.3f, sinA * 0.6f + 71.8f) - 0.5f) * 12f;

                // 北岬（angle ~= 90 deg）の大滑空崖（雄大な張り出し）
                float nAngleNorm = Mathf.DeltaAngle(deg, 90f) / 45f;
                float northWeight = 0f;
                if (Mathf.Abs(nAngleNorm) < 1f)
                {
                    float t = 1f - nAngleNorm * nAngleNorm;
                    northWeight = t * t * (3f - 2f * t);
                }
                float northBoost = northWeight * 30f;

                // 広域三日月湾の揺らぎ（波長250m）
                float wCove = (Mathf.PerlinNoise(cosA * 1.0f + 14.2f, sinA * 1.0f + 48.7f) - 0.5f) * 10f;

                // 目標海岸線半径（波打ち際 5.5m）: 通常370m〜395mの均一で優美な曲線輪郭
                float rCoast = 375f + oval + wMacro + northBoost + wCove;

                // ------------------------------------------------------------
                // 珊瑚礁・浅瀬の有機的な幅 W_reef(angle)
                // ------------------------------------------------------------
                float wReefNoise = Mathf.Sin(2f * angle + 0.8f) * 3f + (Mathf.PerlinNoise(cosA * 1.5f + 5.5f, sinA * 1.5f + 8.8f) - 0.5f) * 5f;
                float baseReef = Mathf.Lerp(32f, 22f, northWeight);
                float wReef = baseReef + wReefNoise; // 浅瀬ラグーンの幅: 約22m〜36m

                // 海岸線からの幾何学的相対距離 (正: 沖合海側, 負: 陸地側)
                float delta = dist - rCoast;

                float curH = heights[z, x];
                float targetH;

                // 深海底標高: 0.05m / islandH（水深約5.45m＝完全な深海色）
                float deepFloorH = 0.05f / islandH;
                // 浅瀬海底標高: 3.0m / islandH（水深2.5m＝透明なエメラルドグリーン）
                float shallowFloorH = 3.0f / islandH;

                // ドロップオフゾーン幅（なだらかな水深グラデーション）
                float dropOffWidth = Mathf.Lerp(48f, 36f, northWeight);

                if (delta > wReef + dropOffWidth)
                {
                    // 沖合・完全深海（水深5.45m）
                    targetH = deepFloorH;
                }
                else if (delta > wReef)
                {
                    // 浅瀬から深海へのドロップオフゾーン:
                    // 5次エルミート (SmootherStep) で極端な急変・段差感を完全に解消
                    float t = (delta - wReef) / dropOffWidth;
                    float s = t * t * t * (t * (t * 6f - 15f) + 10f); // SmootherStep
                    targetH = Mathf.Lerp(shallowFloorH, deepFloorH, s);
                }
                else if (delta > 0f)
                {
                    // 波打ち際 (delta=0, 5.5m) から浅瀬海底 (delta=wReef, 3.0m) への滑らかな遠浅スロープ
                    float t = delta / wReef;
                    float s = t * t * (3f - 2f * t);
                    targetH = Mathf.Lerp(seaH, shallowFloorH, s);
                }
                else if (delta >= -16f)
                {
                    // 波打ち際・渚スロープ (delta=-16m〜0m, 6.2m → 5.5m): 傾斜わずか4%の極めて平坦で滑らかな渚
                    // ポリゴングリッドの急な折れ曲がり（カクカク）を完全に防ぐ
                    float t = (-delta) / 16f;
                    float s = t * t * (3f - 2f * t);
                    targetH = Mathf.Lerp(seaH, 6.2f / islandH, s);
                }
                else if (delta >= -42f)
                {
                    // 砂浜テラス (delta=-42m〜-16m, 7.6m → 6.2m): 心地よく広大な平坦白砂浜
                    float t = (-delta - 16f) / 26f;
                    float s = t * t * (3f - 2f * t);
                    targetH = Mathf.Lerp(6.2f / islandH, 7.6f / islandH, s);
                }
                else if (delta >= -68f)
                {
                    // 砂浜から内陸草原への緩やかなスロープ (delta=-68m〜-42m, 9.5m → 7.6m)
                    float t = (-delta - 42f) / 26f;
                    float s = t * t * (3f - 2f * t);
                    targetH = Mathf.Lerp(7.6f / islandH, 9.5f / islandH, s);
                }
                else
                {
                    // 砂浜テラスから内陸の草原への緩やかな接続
                    float t = Mathf.Clamp01((-delta - 68f) / 32f);
                    float s = t * t * (3f - 2f * t);
                    targetH = Mathf.Lerp(9.5f / islandH, curH, s);
                }

                // 北岬の陸地側（北の大滑空崖、標高 > 5.5m、delta <= 0f）：
                if (northWeight > 0.05f && delta <= 0f && curH > seaH)
                {
                    float distToCoast = -delta;
                    if (distToCoast > 20f)
                    {
                        targetH = Mathf.Max(curH, targetH);
                    }
                    else
                    {
                        float blendT = Mathf.Clamp01(distToCoast / 20f);
                        float smoothB = blendT * blendT * (3f - 2f * blendT);
                        targetH = Mathf.Lerp(targetH, curH, smoothB);
                    }
                }

                // 内陸（中心から260m以内）は既存の地形（高原・池・タワー・遺跡）を100%維持
                if (dist <= 260f)
                {
                    // 何もしない（curH を維持）
                }
                else if (dist <= 295f)
                {
                    // 内陸境界での滑らかなブレンド
                    float blend = Mathf.InverseLerp(260f, 295f, dist);
                    float sb = blend * blend * (3f - 2f * blend);
                    heights[z, x] = Mathf.Lerp(curH, targetH, sb);
                }
                else
                {
                    heights[z, x] = targetH;
                }
            }
        }

        // ====================================================================
        // 1.5 波打ち際（海岸線帯 dist: 340m〜420m）のハイトマップ平滑化フィルタ
        // ポリゴングリッドによる微細な角張りを完全に消去し、シルクのように滑らかな曲面を形成
        // ====================================================================
        float[,] smoothedHeights = (float[,])heights.Clone();
        for (int z = 1; z < hRes - 1; z++)
        {
            for (int x = 1; x < hRes - 1; x++)
            {
                float wx = (x / (float)(hRes - 1)) * td.size.x;
                float wz = (z / (float)(hRes - 1)) * td.size.z;
                float dx = wx - cX;
                float dz = wz - cZ;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);

                // 海岸線付近のみ3x3平滑化
                if (dist >= 340f && dist <= 420f)
                {
                    float sum = heights[z - 1, x - 1] * 0.0625f + heights[z - 1, x] * 0.125f + heights[z - 1, x + 1] * 0.0625f +
                                heights[z,     x - 1] * 0.125f  + heights[z,     x] * 0.25f   + heights[z,     x + 1] * 0.125f +
                                heights[z + 1, x - 1] * 0.0625f + heights[z + 1, x] * 0.125f + heights[z + 1, x + 1] * 0.0625f;
                    smoothedHeights[z, x] = sum;
                }
            }
        }
        td.SetHeights(0, 0, smoothedHeights);

        // ====================================================================
        // 2. Alphamap（テクスチャ）の幾何学的極座標グラデーション美化
        // 等高線のジグザグ折れ線を完全追放し、数学的に滑らかな三日月曲線の白砂浜を実現
        // ====================================================================
        UpdateShorelineAlphamaps(td, land, waterY, cX, cZ);

        land.Flush();
        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssetIfDirty(td);

        // ====================================================================
        // 3. 水平線（Curved Ocean Horizon）の構築と海面スケール拡張
        // ====================================================================
        EnsureCurvedOceanHorizon();

        // ====================================================================
        // 4. Nikoスポーン地点を美しい南西白砂浜テラスに設定
        // ====================================================================
        UpdateNikoSpawnPosition(td, land);

        // ====================================================================
        // 5. 砂浜プロップの接地と水没オブジェクトの適正化
        // ====================================================================
        SnapBeachPropsToTerrain(interactive);

        // ====================================================================
        // 6. 川の最下流残骸（RiverSeg_17〜19）の無効化確認
        // ====================================================================
        DisableRiverMouthSegments();

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=#00FFAA><b>[NaturalCoastline]</b> 直線的な海岸線・南端残骸を解消し、リアルな自然曲線海岸線＆丸い水平線の構築が完了しました！</color>");
    }

    private static void DisableRiverMouthSegments()
    {
        // 砂浜の上に露出していた河口水面プレーン（RiverSeg_12〜19）をすべて無効化！
        // これにより白砂浜を横切る四角い板・直線枠を完全に根絶
        for (int seg = 12; seg <= 19; seg++)
        {
            var go = GameObject.Find("RiverSeg_" + seg);
            if (go != null && go.activeSelf)
            {
                Undo.RecordObject(go, "Disable River Mouth Segment " + seg);
                go.SetActive(false);
                EditorUtility.SetDirty(go);
            }
        }
    }

    private static void UpdateNikoSpawnPosition(TerrainData td, Terrain land)
    {
        // 南西ビーチの心地よい白砂浜テラス（X: 205, Z: 295）
        // 標高約7.2mの乾いた白砂の上。目の前に優美な三日月海岸線とエメラルドグリーンの海が広がる
        var startPoint = GameObject.Find("StartPoint");
        if (startPoint != null)
        {
            Undo.RecordObject(startPoint.transform, "Update StartPoint to Beach Terrace");
            float y = land.SampleHeight(new Vector3(205f, 0f, 295f)) + 0.1f;
            startPoint.transform.position = new Vector3(205f, y, 295f);
            startPoint.transform.rotation = Quaternion.Euler(0f, -135f, 0f);
            EditorUtility.SetDirty(startPoint);
        }

        // Nikoキャラクター本体
        var niko = GameObject.Find("Niko");
        if (niko != null)
        {
            Undo.RecordObject(niko.transform, "Update Niko to Beach Terrace");
            float y = land.SampleHeight(new Vector3(205f, 0f, 295f)) + 0.1f;
            niko.transform.position = new Vector3(205f, y, 295f);
            niko.transform.rotation = Quaternion.Euler(0f, -135f, 0f);
            EditorUtility.SetDirty(niko);
        }
    }

    private static void UpdateShorelineAlphamaps(TerrainData td, Terrain land, float waterY, float cX, float cZ)
    {
        if (td.alphamapResolution < 1024)
        {
            td.alphamapResolution = 1024;
        }

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
                float ny = td.GetInterpolatedNormal(nx, nz).y;

                // 垂直に近い急崖のみ岩肌に（内陸の北崖など）
                float steepness = 1f - Mathf.Clamp01((ny - 0.32f) / 0.15f);
                float rockW = Mathf.Clamp01(steepness * 2.0f);

                // 極座標計算
                float dx = wx - cX;
                float dz = wz - cZ;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                float angle = Mathf.Atan2(dz, dx);
                float deg = angle * Mathf.Rad2Deg;

                // 海岸線半径 rCoast
                float cosA = Mathf.Cos(angle);
                float sinA = Mathf.Sin(angle);
                float oval = Mathf.Cos(angle * 2f - 0.4f) * 12f;
                float wMacro = (Mathf.PerlinNoise(cosA * 0.6f + 25.3f, sinA * 0.6f + 71.8f) - 0.5f) * 12f;

                float nAngleNorm = Mathf.DeltaAngle(deg, 90f) / 45f;
                float northWeight = 0f;
                if (Mathf.Abs(nAngleNorm) < 1f)
                {
                    float t = 1f - nAngleNorm * nAngleNorm;
                    northWeight = t * t * (3f - 2f * t);
                }
                float northBoost = northWeight * 30f;
                float wCove = (Mathf.PerlinNoise(cosA * 1.0f + 14.2f, sinA * 1.0f + 48.7f) - 0.5f) * 10f;
                float rCoast = 375f + oval + wMacro + northBoost + wCove;

                // 幾何学的海岸線相対距離 (正: 海側, 負: 陸地側)
                float delta = dist - rCoast;

                float sandW = 0f;
                // 砂浜と草原の境界に、雄大で自然な三日月曲線の揺らぎ（波長約120m）
                float beachWave = (Mathf.PerlinNoise(wx * 0.012f + 42.1f, wz * 0.012f + 67.8f) - 0.5f) * 4.5f;

                // 海側から波打ち際、そして砂浜テラス全域（delta >= -36m）：
                // 等高線ではなく幾何学的極座標距離で塗るため、カクカクの直線や段差が原理的に一切出ない！
                if (delta >= -36f)
                {
                    sandW = 1.0f;
                    rockW = 0f; // 海中および波打ち際・砂浜にはグレーの岩肌を出さない
                }
                else if (delta >= -58f)
                {
                    // 砂浜テラスから草原への移行帯（幅22m）:
                    // 5次エルミート SmootherStep で絹のように滑らかに白砂から大草原へ溶け込む
                    float t = Mathf.Clamp01((-delta - 36f + beachWave) / 22f);
                    float s = t * t * t * (t * (t * 6f - 15f) + 10f); // SmootherStep
                    sandW = 1.0f - s;
                    rockW *= (1f - sandW);
                }
                else
                {
                    // 完全な内陸草原
                    sandW = 0f;
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

    /// <summary>
    /// 海面 OceanPlane のスケールを拡張し、円形の大洋水平線メッシュを構築
    /// </summary>
    public static void EnsureCurvedOceanHorizon()
    {
        var oceanGo = GameObject.Find("OceanPlane");
        if (oceanGo != null)
        {
            oceanGo.transform.position = new Vector3(512f, 5.5f, 512f);
            oceanGo.transform.localScale = new Vector3(1200f, 1f, 1200f);

            var horizonMgr = oceanGo.GetComponent<AdventureOceanHorizonManager>();
            if (horizonMgr == null)
            {
                horizonMgr = oceanGo.AddComponent<AdventureOceanHorizonManager>();
            }
            horizonMgr.EnsureCurvedHorizon();
            EditorUtility.SetDirty(oceanGo);

            // 海面マテリアルの水深グラデーションを滑らかに更新（極端な急変を解消）
            var rend = oceanGo.GetComponent<Renderer>();
            if (rend != null && rend.sharedMaterial != null)
            {
                var mat = rend.sharedMaterial;
                Undo.RecordObject(mat, "Smooth Ocean Depth Gradient");
                mat.SetFloat("_Depth_Distance", 2.5f);
                mat.SetFloat("_Depth", 5.5f);
                mat.SetFloat("_Distance", 24.0f);
                mat.SetFloat("_Coast_Opacity", 0.002f);
                mat.SetFloat("_CoastOpacity", 0.004f);
                mat.SetFloat("_EdgeFade", 0.95f);
                mat.SetFloat("_Normal_Strength", 0.55f);
                mat.SetFloat("_Water_Speed", 0.55f);
                mat.SetFloat("_Wave_Strength", 0.42f);
                mat.SetFloat("_Smoothness", 0.98f);
                mat.SetColor("_Shallow_Color", new Color(0.18f, 0.88f, 0.94f, 0.38f));
                mat.SetColor("_Deep_Color", new Color(0.04f, 0.48f, 0.82f, 0.80f));
                mat.SetColor("_Surface_Color", new Color(0.15f, 0.72f, 0.88f, 0.55f));
                mat.SetColor("_SpecColor", new Color(1.0f, 1.0f, 1.0f, 1.0f));
                EditorUtility.SetDirty(mat);
            }
        }

        // シーン内の全カメラの farClipPlane を 12,000m に拡張
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (cam.farClipPlane < 8000f)
            {
                Undo.RecordObject(cam, "Extend Camera FarClip");
                cam.farClipPlane = 12000f;
                EditorUtility.SetDirty(cam);
            }
        }
    }

    /// <summary>
    /// 砂浜エリアにある草、低木、ヤシの木、木立、岩などの高さを現在のTerrainに密着接地させる。
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
            var land = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude)
                .FirstOrDefault(t => t.name == "LandTerrain" || t.name == "IslandTerrain");
            terrain = land;
        }
        if (terrain == null) return;

        var allGos = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
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
                name.Contains("Island") || name.Contains("Audio") || name.Contains("Marker") ||
                name.Contains("Horizon"))
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
            float dist = Mathf.Sqrt(dx * dx + dz * dz);

            // 砂浜〜海岸線エリア (dist > 300m)
            if (dist > 300f)
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
                    EditorUtility.SetDirty(go.transform);
                    snappedCount++;
                }
            }
        }

        Debug.Log($"<color=#00FFAA><b>[NaturalCoastline]</b> 砂浜のプロップ {snappedCount} 個をTerrainに接地させ、水没陸上植物 {disabledUnderwaterCount} 個を非アクティブ化しました！</color>");
    }
}
