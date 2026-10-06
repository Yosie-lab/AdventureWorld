#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 楽園の島（RustAndFloat）全体に色彩豊かで美しい花々を一括配置するエディタツール。
/// 全21種類の色鮮やかな花畑パッチ（FlowerMeadow）と可憐な立ち花（Flower）を
/// 島全域および主要名所（大草原、カルデラ湖畔、せせらぎ池、中央テラス、北滑空崖など）に美しく咲かせる。
/// </summary>
public static class AdventurePlantParadiseFlowers
{
    const string ScenePath = "Assets/RustAndFloat/Scenes/RustAndFloat.unity";
    const string RootName = "Paradise";
    const string FlowersGroupName = "Flowers";
    const string PrefabDir = "Assets/Idyllic Fantasy Nature/Prefabs/";

    // ── 花畑パッチ（面として群生する花壇パッチ・全12種） ──
    static readonly string[] FlowerMeadows =
    {
        "FlowerMeadow_Pink.prefab",
        "FlowerMeadow_White.prefab",
        "FlowerMeadow_Orange.prefab",
        "FlowerMeadow_Red.prefab",
        "FlowerMeadow_RedPink.prefab",
        "FlowerMeadow_RedOrange.prefab",
        "FlowerMeadow_RedPurple.prefab",
        "FlowerMeadow_Purple.prefab",
        "FlowerMeadow_PurpleRedPink.prefab",
        "FlowerMeadow_Blue.prefab",
        "FlowerMeadow_BluePurple.prefab",
        "FlowerMeadow_OrangePinkRedPurpleBlue.prefab" // レインボーMIX
    };

    // ── 単体・アクセント立ち花（可憐に咲く立ち花・全9種） ──
    static readonly string[] SingleFlowers =
    {
        "Flower_Yellow.prefab",
        "Flower_White.prefab",
        "Flower_Blue_01.prefab",
        "Flower_Blue_02.prefab",
        "Flower_Orange.prefab",
        "Flower_Purple.prefab",
        "Flower_Pink.prefab",
        "Flower_Red.prefab",
        "Flower_YellowRed.prefab"
    };

    // ── テーマ別の配色プリセット ──
    // 1. 虹色・暖色系（大草原・スタート地点・草原池）
    static readonly string[] WarmRainbowMeadows =
    {
        "FlowerMeadow_OrangePinkRedPurpleBlue.prefab",
        "FlowerMeadow_Pink.prefab",
        "FlowerMeadow_Orange.prefab",
        "FlowerMeadow_RedPink.prefab",
        "FlowerMeadow_RedOrange.prefab",
        "FlowerMeadow_White.prefab",
        "FlowerMeadow_PurpleRedPink.prefab"
    };
    static readonly string[] WarmRainbowSingles =
    {
        "Flower_Yellow.prefab",
        "Flower_Orange.prefab",
        "Flower_Pink.prefab",
        "Flower_Red.prefab",
        "Flower_YellowRed.prefab",
        "Flower_White.prefab"
    };

    // 2. 清涼・神秘系（カルデラ湖畔・水辺・渓流・深い森）
    static readonly string[] CoolMysticMeadows =
    {
        "FlowerMeadow_Blue.prefab",
        "FlowerMeadow_BluePurple.prefab",
        "FlowerMeadow_Purple.prefab",
        "FlowerMeadow_RedPurple.prefab",
        "FlowerMeadow_White.prefab",
        "FlowerMeadow_OrangePinkRedPurpleBlue.prefab"
    };
    static readonly string[] CoolMysticSingles =
    {
        "Flower_Blue_01.prefab",
        "Flower_Blue_02.prefab",
        "Flower_Purple.prefab",
        "Flower_White.prefab",
        "Flower_Pink.prefab"
    };

    // 3. 聖なる祝祭系（中央タワーテラス・オアシス湧水池）
    static readonly string[] HolySacredMeadows =
    {
        "FlowerMeadow_White.prefab",
        "FlowerMeadow_Pink.prefab",
        "FlowerMeadow_RedPink.prefab",
        "FlowerMeadow_OrangePinkRedPurpleBlue.prefab",
        "FlowerMeadow_PurpleRedPink.prefab"
    };
    static readonly string[] HolySacredSingles =
    {
        "Flower_White.prefab",
        "Flower_Yellow.prefab",
        "Flower_Pink.prefab",
        "Flower_Blue_01.prefab"
    };

    [MenuItem("Adventure/🌸 Plant Paradise Flowers (色彩豊かな花畑を咲かせる)")]
    public static void PlantFlowersFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("停止してください", "■ で再生を止めてから実行してください。", "OK");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var paradise = GameObject.Find(RootName);
        if (paradise == null)
        {
            paradise = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(paradise, "Create Paradise Root");
        }

        var land = Object.FindAnyObjectByType<Terrain>();
        if (land == null)
        {
            EditorUtility.DisplayDialog("エラー", "Terrain がシーンに見つかりません。", "OK");
            return;
        }

        PlantFlowers(paradise, land);
    }

    [MenuItem("Adventure/🌸 Clear Paradise Flowers (花畑を削除)")]
    public static void ClearFlowersFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("停止してください", "■ で再生を止めてから実行してください。", "OK");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var paradise = GameObject.Find(RootName);
        if (paradise != null)
        {
            var oldGroup = paradise.transform.Find(FlowersGroupName);
            if (oldGroup != null)
            {
                Undo.DestroyObjectImmediate(oldGroup.gameObject);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                Debug.Log("[RustAndFloat] 花畑を削除しました。");
            }
        }
    }

    /// <summary>花畑を一括生成・再生成する本体処理</summary>
    public static void PlantFlowers(GameObject paradise, Terrain land)
    {
        // 既存の Flowers グループがあれば一度破棄（重複防止）
        var oldGroup = paradise.transform.Find(FlowersGroupName);
        if (oldGroup != null)
        {
            Undo.DestroyObjectImmediate(oldGroup.gameObject);
        }

        var flowersRoot = new GameObject(FlowersGroupName);
        flowersRoot.transform.SetParent(paradise.transform, false);
        Undo.RegisterCreatedObjectUndo(flowersRoot, "Plant Paradise Flowers");

        var rng = new System.Random(20261006);
        int totalPlanted = 0;

        // プレハブキャッシュ
        var meadowPrefabs = LoadPrefabs(FlowerMeadows);
        var singlePrefabs = LoadPrefabs(SingleFlowers);
        var warmMeadows = LoadPrefabs(WarmRainbowMeadows);
        var warmSingles = LoadPrefabs(WarmRainbowSingles);
        var coolMeadows = LoadPrefabs(CoolMysticMeadows);
        var coolSingles = LoadPrefabs(CoolMysticSingles);
        var holyMeadows = LoadPrefabs(HolySacredMeadows);
        var holySingles = LoadPrefabs(HolySacredSingles);

        // ══════════════════════════════════════════════════════
        // 1. 主要名所のテーマ別フラワーガーデン（集中的な美化）
        // ══════════════════════════════════════════════════════

        // ① スタート地点〜西側ビーチスロープ（座礁艇の東側・登り口: 180, 280〜240, 320）
        var spawnGarden = CreateSubGroup(flowersRoot, "01_WestBeachApproach_Flowers");
        totalPlanted += PlantClusterGarden(spawnGarden, land, rng,
            center: new Vector3(205f, 0f, 295f), radius: 38f, count: 45,
            warmMeadows, warmSingles, minScale: 1.1f, maxScale: 1.8f);

        // ② 西側大草原のせせらぎ池畔フラワーガーデン（池中心 290, 320 周囲）
        var meadowPondGarden = CreateSubGroup(flowersRoot, "02_MeadowLowlandPond_Flowers");
        totalPlanted += PlantClusterGarden(meadowPondGarden, land, rng,
            center: new Vector3(290f, 0f, 320f), radius: 32f, count: 50,
            warmMeadows, warmSingles, minScale: 1.1f, maxScale: 1.9f, innerClearRadius: 16.5f);

        // ③ 大カルデラ湖畔・水辺フラワーベルト（湖中心 420, 440 周囲）
        var lakeGarden = CreateSubGroup(flowersRoot, "03_CalderaLake_ShoreFlowers");
        totalPlanted += PlantClusterGarden(lakeGarden, land, rng,
            center: new Vector3(420f, 0f, 440f), radius: 52f, count: 65,
            coolMeadows, coolSingles, minScale: 1.2f, maxScale: 2.0f, innerClearRadius: 38.5f);

        // ④ 中央タワー足元・オアシス湧水池周辺（480, 455 周囲）
        var sanctuaryGarden = CreateSubGroup(flowersRoot, "04_SanctuaryOasis_Flowers");
        totalPlanted += PlantClusterGarden(sanctuaryGarden, land, rng,
            center: new Vector3(480f, 0f, 460f), radius: 28f, count: 40,
            holyMeadows, holySingles, minScale: 1.0f, maxScale: 1.7f, innerClearRadius: 16.5f);

        // ⑤ 北の大滑空崖・絶景パノラマフラワーフィールド（512, 715 周囲）
        var cliffGarden = CreateSubGroup(flowersRoot, "05_NorthGlidingCliff_Flowers");
        totalPlanted += PlantClusterGarden(cliffGarden, land, rng,
            center: new Vector3(512f, 0f, 715f), radius: 36f, count: 45,
            coolMeadows, warmSingles, minScale: 1.1f, maxScale: 1.8f);

        // ⑥ 東の大樹海・小道と木漏れ日グレイド（600, 420 周囲）
        var forestGlade = CreateSubGroup(flowersRoot, "06_DeepForestGlade_Flowers");
        totalPlanted += PlantClusterGarden(forestGlade, land, rng,
            center: new Vector3(600f, 0f, 420f), radius: 45f, count: 40,
            coolMeadows, coolSingles, minScale: 1.1f, maxScale: 1.8f);

        // ══════════════════════════════════════════════════════
        // 2. 島全体の広域スキャッター（大草原＋全島散布：約700〜800箇所）
        // ══════════════════════════════════════════════════════
        var broadMeadow = CreateSubGroup(flowersRoot, "07_GrandIslandWide_Flowers");
        totalPlanted += PlantIslandWideScatter(broadMeadow, land, rng,
            targetCount: 750, meadowPrefabs, singlePrefabs);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log($"[RustAndFloat] 🌸 島全体に色彩豊かな花畑を咲かせました！ 合計 {totalPlanted} 個の花オブジェクトを配置しました。");
    }

    /// <summary>特定エリアを取り囲むように花畑パッチ＋立ち花を配置</summary>
    static int PlantClusterGarden(
        GameObject parent, Terrain land, System.Random rng,
        Vector3 center, float radius, int count,
        List<GameObject> meadows, List<GameObject> singles,
        float minScale, float maxScale, float innerClearRadius = 0f)
    {
        var td = land.terrainData;
        Vector3 origin = land.transform.position;
        Vector3 size = td.size;
        int planted = 0;

        for (int i = 0; i < count * 3 && planted < count; i++)
        {
            float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            float dist = innerClearRadius + (float)(rng.NextDouble() * (radius - innerClearRadius));
            Vector3 pos = center + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

            // 標高取得
            pos.y = land.SampleHeight(pos) + origin.y;

            // 水深チェック（水面5.5mより下の水中は除外）
            if (pos.y < 5.8f) continue;

            // 法線チェック（急斜面には生やさない）
            float nx = (pos.x - origin.x) / size.x;
            float nz = (pos.z - origin.z) / size.z;
            if (nx < 0f || nx > 1f || nz < 0f || nz > 1f) continue;
            Vector3 normal = td.GetInterpolatedNormal(nx, nz);
            if (normal.y < 0.55f) continue; // 傾斜約56度以下のみ

            // 70%の確率でボリューム感ある群生花畑パッチ（Meadow）、30%でアクセント立ち花（Single）
            bool isMeadow = rng.NextDouble() < 0.70;
            var list = isMeadow ? meadows : singles;
            if (list.Count == 0) continue;
            var prefab = list[rng.Next(list.Count)];

            SpawnFlowerInstance(parent, prefab, pos, normal, rng, minScale, maxScale);
            planted++;

            // さらに40%の確率で、すぐ脇（1.2〜2.5m）に相棒の小花・色違い花を寄り添わせる（自然な群生感）
            if (rng.NextDouble() < 0.40 && planted < count)
            {
                float sideAngle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float sideDist = 1.2f + (float)(rng.NextDouble() * 1.5f);
                Vector3 sidePos = pos + new Vector3(Mathf.Cos(sideAngle) * sideDist, 0f, Mathf.Sin(sideAngle) * sideDist);
                sidePos.y = land.SampleHeight(sidePos) + origin.y;
                if (sidePos.y >= 5.8f)
                {
                    var sidePrefab = singles.Count > 0 ? singles[rng.Next(singles.Count)] : prefab;
                    SpawnFlowerInstance(parent, sidePrefab, sidePos, normal, rng, minScale * 0.85f, maxScale * 0.95f);
                    planted++;
                }
            }
        }

        return planted;
    }

    /// <summary>島全体への広域バランススキャッター（大草原ゾーンを高密度化）</summary>
    static int PlantIslandWideScatter(
        GameObject parent, Terrain land, System.Random rng, int targetCount,
        List<GameObject> meadows, List<GameObject> singles)
    {
        var td = land.terrainData;
        Vector3 origin = land.transform.position;
        Vector3 size = td.size;
        Vector3 center = origin + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        float islandRadius = size.x * 0.45f; // 半径約460m
        int planted = 0;

        // グリッドサンプリング（大草原・小道・丘陵）
        float step = 5.0f;
        for (float z = 60f; z < size.z - 60f && planted < targetCount; z += step)
        {
            for (float x = 60f; x < size.x - 60f && planted < targetCount; x += step)
            {
                // ジッター乱数
                float jx = x + (float)(rng.NextDouble() * step * 0.85 - step * 0.42);
                float jz = z + (float)(rng.NextDouble() * step * 0.85 - step * 0.42);
                Vector3 pos = new Vector3(origin.x + jx, 0f, origin.z + jz);

                // 外周チェック
                float distFromCenter = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(center.x, center.z));
                if (distFromCenter > islandRadius) continue;

                // 標高取得
                pos.y = land.SampleHeight(pos) + origin.y;
                // 海面以下はスキップ
                if (pos.y < 6.2f) continue;

                // 法線チェック（なだらかな草地・丘陵地）
                float nx = (pos.x - origin.x) / size.x;
                float nz = (pos.z - origin.z) / size.z;
                if (nx < 0f || nx > 1f || nz < 0f || nz > 1f) continue;
                Vector3 normal = td.GetInterpolatedNormal(nx, nz);
                if (normal.y < 0.60f) continue;

                // ゾーニング密度制御
                // 西側大草原（x < 460, z < 650）: 花畑の天国！高頻度で配置
                bool inWestMeadow = (pos.x < 460f && pos.z < 650f);
                double pickChance = inWestMeadow ? 0.35 : 0.14; // 草原は35%、森林林床は14%
                if (rng.NextDouble() > pickChance) continue;

                // サンクチュアリ中央タワーのすぐ足元（半径12m）はレバー等のため空ける
                if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(512f, 512f)) < 12f) continue;

                // プレハブ選択（Meadow 65%, Single 35%）
                bool isMeadow = rng.NextDouble() < 0.65;
                var list = isMeadow ? meadows : singles;
                if (list.Count == 0) continue;
                var prefab = list[rng.Next(list.Count)];

                SpawnFlowerInstance(parent, prefab, pos, normal, rng, 1.0f, 1.9f);
                planted++;

                // クラスター群生（20%で隣にもう1つ）
                if (rng.NextDouble() < 0.20 && planted < targetCount)
                {
                    Vector3 subPos = pos + new Vector3((float)(rng.NextDouble() * 3.0 - 1.5), 0f, (float)(rng.NextDouble() * 3.0 - 1.5));
                    subPos.y = land.SampleHeight(subPos) + origin.y;
                    if (subPos.y >= 6.2f)
                    {
                        var subPrefab = (singles.Count > 0 && rng.NextDouble() < 0.5) ? singles[rng.Next(singles.Count)] : prefab;
                        SpawnFlowerInstance(parent, subPrefab, subPos, normal, rng, 0.9f, 1.6f);
                        planted++;
                    }
                }
            }
        }

        return planted;
    }

    /// <summary>花プレハブのインスタンスを生成して配置</summary>
    static void SpawnFlowerInstance(
        GameObject parent, GameObject prefab, Vector3 worldPos, Vector3 groundNormal,
        System.Random rng, float minScale, float maxScale)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
        if (instance == null) return;

        instance.transform.position = worldPos;

        // 地形法線に少し沿わせつつ、Y軸は360度完全ランダム回転
        float yaw = (float)(rng.NextDouble() * 360.0);
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        if (groundNormal.y < 0.98f)
        {
            Quaternion tilt = Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, groundNormal, 0.45f));
            rot = tilt * rot;
        }
        instance.transform.rotation = rot;

        // スケール
        float s = minScale + (float)(rng.NextDouble() * (maxScale - minScale));
        instance.transform.localScale = Vector3.one * s;

        // Static設定（描画パフォーマンス向上・バッチング最適化）
        instance.isStatic = true;
    }

    static GameObject CreateSubGroup(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static List<GameObject> LoadPrefabs(string[] fileNames)
    {
        var list = new List<GameObject>();
        for (int i = 0; i < fileNames.Length; i++)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + fileNames[i]);
            if (p != null) list.Add(p);
        }
        return list;
    }
}
#endif
