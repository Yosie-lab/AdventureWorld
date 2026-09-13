#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;

public static class AdventureDressRustFloatParadise
{
    const string ScenePath = "Assets/RustAndFloat/Scenes/RustAndFloat.unity";
    const string RootName = "Paradise";
    const string PrefabRoot = "Assets/Idyllic Fantasy Nature/Prefabs/";

    static readonly string[] Trees =
    {
        "BlossomTree_01.prefab",
        "BlossomTree_03.prefab",
        "BlossomTree_05.prefab",
        "WillowTree_01_Green.prefab",
        "WillowTree_02_Green.prefab",
        "WillowTree_01_Pink.prefab",
        "WillowTree_03_Pink.prefab",
        "WillowTree_05_Pink.prefab",
        "BroadleafTree_01_Green.prefab",
        "BroadleafTree_04_Green.prefab",
        "BroadleafTree_02_Green.prefab",
        "BroadleafTree_01_Red.prefab",
        "BroadleafTree_03_Purple.prefab"
    };

    static readonly string[] DenseDarkGreenTrees =
    {
        "Fir_01.prefab",
        "Fir_02.prefab",
        "Fir_03.prefab",
        "Fir_04.prefab",
        "Fir_05.prefab",
        "BroadleafTree_01_Green.prefab",
        "BroadleafTree_02_Green.prefab",
        "BroadleafTree_03_Green.prefab",
        "BroadleafTree_04_Green.prefab",
        "BroadleafTree_05_Green.prefab",
        "WillowTree_01_Green.prefab",
        "WillowTree_02_Green.prefab",
        "WillowTree_03_Green.prefab",
        "WillowTree_04_Green.prefab",
        "WillowTree_05_Green.prefab"
    };

    static readonly string[] Grasses =
    {
        "Grass_01.prefab",
        "Grass_02.prefab",
        "Grass_03.prefab"
    };

    static readonly string[] Bushes =
    {
        "Bush_01_01.prefab",
        "Bush_01_02.prefab",
        "Bush_02_01.prefab",
        "Bush_03_01.prefab"
    };

    static readonly string[] Flowers =
    {
        "FlowerMeadow_Pink.prefab",
        "FlowerMeadow_White.prefab",
        "FlowerMeadow_Orange.prefab",
        "FlowerMeadow_RedPink.prefab",
        "FlowerMeadow_BluePurple.prefab",
        "FlowerMeadow_RedOrange.prefab",
        "FlowerMeadow_PurpleRedPink.prefab",
        "FlowerMeadow_OrangePinkRedPurpleBlue.prefab",
        "Flower_Yellow.prefab",
        "Flower_White.prefab",
        "Flower_Blue_01.prefab",
        "Flower_Orange.prefab",
        "Flower_Purple.prefab"
    };

    static readonly string[] Rocks =
    {
        "Rock_Small_01.prefab",
        "Rock_Small_02.prefab",
        "Rock_Medium_01.prefab",
        "Stone_Medium_01.prefab"
    };

    static readonly string[] GorgeRocks =
    {
        "Stone_Big_01.prefab",
        "Stone_Big_02.prefab",
        "Stone_Big_03.prefab",
        "Rock_Medium_01.prefab",
        "Stone_Medium_01.prefab",
        "Rock_Small_01.prefab",
        "Rock_Small_02.prefab"
    };

    static readonly string[] ShorePlants =
    {
        "Reeds_01.prefab",
        "Reeds_02.prefab",
        "Waterlily_01.prefab",
        "Cattail_01.prefab",
        "Cattail_02.prefab"
    };

    [MenuItem("Adventure/Dress RustAndFloat Paradise")]
    public static void DressFromMenu()
    {
        Dress();
    }

    [MenuItem("Adventure/📍 Reset Spawn to Meadow Plains (大草原のせせらぎ平原)")]
    public static void ResetSpawnToMeadowPlains()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("停止してください", "■で再生を止めてから実行してください。", "OK");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var land = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude)
            .FirstOrDefault(t => t.name == "LandTerrain" || t.name == "IslandTerrain");
        if (land == null)
        {
            Debug.LogError("[RustAndFloat] Terrain がありません");
            return;
        }

        EnsureSafeSpawnPosition(land, true);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("<color=#00FFAA><b>[RustAndFloat]</b> スポーン地点を大草原のせせらぎ池平原（265, 15.2, 330）へ再設定しました！</color>");
    }

    static void EnsureSafeSpawnPosition(Terrain land, bool isGrand)
    {
        var player = Object.FindObjectsByType<AdventurePlayerController>(FindObjectsInactive.Exclude).FirstOrDefault();
        if (player == null) return;

        // 大草原のせせらぎ池のほとり（平坦で広大な緑の野原、標高約15m）
        Vector3 safeSpawn = isGrand ? new Vector3(265f, 0f, 330f) : new Vector3(138f, 0f, 176f);
        float terrainH = land.SampleHeight(safeSpawn);
        safeSpawn.y = terrainH + 0.15f;

        Quaternion rot = isGrand ? Quaternion.Euler(0f, 55f, 0f) : Quaternion.identity;
        player.transform.SetPositionAndRotation(safeSpawn, rot);
        player.spawnPosition = safeSpawn;

        if (player.GetComponent<AdventureNikoFootsteps>() == null)
            player.gameObject.AddComponent<AdventureNikoFootsteps>();

        EditorUtility.SetDirty(player.gameObject);

        var cam = Camera.main ?? Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude).FirstOrDefault();
        if (cam != null)
        {
            Vector3 camOffset = isGrand ? new Vector3(-5.5f, 3.2f, -5.5f) : new Vector3(0f, 4.5f, -10f);
            cam.transform.position = safeSpawn + camOffset;
            cam.transform.LookAt(safeSpawn + Vector3.up * 1.4f);
            EditorUtility.SetDirty(cam.gameObject);
        }
    }

    [MenuItem("Adventure/🧹 Remove White Box Particles")]
    public static void RemoveWhiteBoxes()
    {
        int count = 0;
        var allVfx = Object.FindObjectsByType<UnityEngine.VFX.VisualEffect>(FindObjectsInactive.Include);
        foreach (var v in allVfx)
        {
            if (v != null && v.gameObject != null)
            {
                Object.DestroyImmediate(v.gameObject);
                count++;
            }
        }
        var allGo = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (var go in allGo)
        {
            if (go != null && (go.name.Contains("RoundingBirds") || go.name.Contains("VFX_Insect") || go.name.Contains("VFX_Rounding")))
            {
                Object.DestroyImmediate(go);
                count++;
            }
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#00FFAA><b>[RustAndFloat]</b> 空中を飛ぶ白い四角の原因だったVFX {count} 個を完全に削除しました！</color>");
    }

    public static void Dress()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("停止してください", "■で再生を止めてから実行してください。", "OK");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var land = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude)
            .FirstOrDefault(t => t.name == "LandTerrain" || t.name == "IslandTerrain");
        if (land == null || land.terrainData == null)
        {
            Debug.LogError("[RustAndFloat] Terrain がありません");
            return;
        }

        var old = GameObject.Find(RootName);
        if (old != null)
            Object.DestroyImmediate(old);

        var lookout = GameObject.Find("CliffLookout") ?? GameObject.Find("SpawnMarker");
        if (lookout != null)
            Object.DestroyImmediate(lookout);

        // 白い四角いボックスとして表示崩れを起こす壊れたVFX（鳥・虫）を一括クリーンアップ
        var allVfx = Object.FindObjectsByType<UnityEngine.VFX.VisualEffect>(FindObjectsInactive.Include);
        foreach (var v in allVfx)
        {
            if (v != null && v.gameObject != null && (v.name.Contains("Birds") || v.name.Contains("Insect") || v.name.Contains("Rounding")))
                Object.DestroyImmediate(v.gameObject);
        }

        var root = new GameObject(RootName);
        var rng = new System.Random(2050);
        float water = 5.5f;

        // 島サイズを自動検出（256m or 1024m）
        Vector3 tSize = land.terrainData.size;
        bool isGrand = tSize.x > 500f;
        Vector3 center = land.transform.position + new Vector3(tSize.x * 0.5f, 0f, tSize.z * 0.5f);

        BrightenScene();

        if (isGrand)
        {
            // ── 1024m Grand Island 用 ──
            // サンクチュアリ中央(512,512)半径80m と 北崖(512,720)周辺を空ける
            Vector3 sanctuary = new Vector3(512f, 0f, 512f);
            Vector3 northCliff = new Vector3(512f, 0f, 720f);

            // 木（東部〜北部に広大無辺な深緑の大樹海、西側には絵になる草原木立：2,200本）
            ScatterGrand(root, land, rng, Trees, 2200, sanctuary, 75f, northCliff, 55f,
                1.1f, 2.4f, water + 2f, isTree: true);
            // 風にそよぐ野原の草（主要アクセント。島全体はTerrainDetailGrassで軽快に一面描画）
            ScatterGrand(root, land, rng, Grasses, 300, sanctuary, 35f, northCliff, 35f,
                1.5f, 3.2f, water + 1.2f, isTree: false);
            // 豊かな下草・灌木（大樹海の林床や草原の起伏を彩る）
            ScatterGrand(root, land, rng, Bushes, 400, sanctuary, 40f, northCliff, 40f,
                0.9f, 1.7f, water + 1.4f, isTree: false);
            // 色とりどりの花畑・花（小道や池の周囲のアクセント花畑）
            ScatterGrand(root, land, rng, Flowers, 300, sanctuary, 35f, northCliff, 35f,
                1.1f, 2.4f, water + 1.2f, isTree: false);
            // 岸辺の岩と水辺植物
            PlaceShoreGrand(root, land, rng, water);
            // 標高差を下る激しい「渓流（東の深林大渓流）」の巨石群・飛び石・苔岩・水辺植物デコレーション
            PlaceMountainGorgeProps(root, land, rng);
            // シンボルツリー（島の各エリアに大木）
            PlaceHeroTreesGrand(root, land);
            // ハワイの背の高いリアル大ヤシの木（白砂ビーチ沿いに160本）
            PlacePalmsGrand(root, land, rng, water);
            // 蝶（森や花畑の各所に36箇所）
            PlaceButterfliesGrand(root, land, rng);
            // 動物たち（カピバラ、犬、猫）
            PlaceAnimalsGrand(root, land, rng);
            // 鳥のさえずり・空の旋回鳥
            PlaceBirdsAndAmbienceGrand(root, land, rng);
            // 3つの池（オアシス池・カルデラ湖・草原池）と小川の睡蓮・蓮の葉・葦・飛び石
            PlaceWaterFloraAndProps(root, land, rng);
            // 池や小川のカエル（36匹、ピョンピョン跳ねる）
            PlaceFrogs(root, land, rng);
            // 白砂ビーチのカニ（35匹、カサカサ横歩き）
            PlaceCrabs(root, land, rng, water);
            // 水辺や草原を飛ぶトンボ（28匹、ホバリング飛行）
            PlaceDragonflies(root, land, rng);
            // 濃い緑の木が密集した原生林（3大森林）と、状況に合わせた蝉時雨＆木の幹に止まるリアルな蝉
            PlaceDenseDarkGreenForestsAndCicadas(root, land, rng);
            // 大草原の草むらの虫の声（エンマコオロギ）＆夏の田舎道アンビエンス
            PlaceMeadowInsectsAndAmbience(root, land, rng);
            // 抜けるように澄んだ青空に浮かぶとても気持ちの良い白い雲群
            PlaceParadiseClouds(root, rng);
            // 白砂ビーチ沿いに寄せては返すリアルな波の音（3D立体音響 24箇所）
            PlaceBeachWavesGrand(root, land, rng, water);
        }
        else
        {
            // ── 旧256m島用（後方互換） ──
            Vector3 spawn = new Vector3(138f, 0f, 176f);
            Scatter256(root, land, rng, Trees, 48, spawn, 11f, 0.82f, 1.2f, 7.2f);
            Scatter256(root, land, rng, Bushes, 90, spawn, 7f, 0.75f, 1.1f, 6.8f);
            Scatter256(root, land, rng, Flowers, 70, spawn, 5f, 0.7f, 1.3f, 6.6f);
            PlaceShore256(root, land, rng, water, spawn);
            PlaceHeroTrees256(root, land);
            PlacePalms256(root, land, rng, water, spawn);
            PlaceButterflies256(root, land, rng);
        }

        // スポーン地点が崖の中腹にならないよう、安全な平坦展望テラスへ確実に救出・補正
        EnsureSafeSpawnPosition(land, isGrand);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[RustAndFloat] 楽園の植生・動物・鳥を置きました。件数=" + root.transform.childCount
            + " 島サイズ=" + tSize.x + "m");
    }

    // ══════════════════════════════════════════
    //  1024m Grand Island 用メソッド群
    // ══════════════════════════════════════════

    /// <summary>1024m島用の広域スキャッター配置（高密度）</summary>
    static void ScatterGrand(
        GameObject root, Terrain land, System.Random rng, string[] paths,
        int maxCount, Vector3 sanctuaryCenter, float sanctuaryClear,
        Vector3 cliffCenter, float cliffClear,
        float minScale, float maxScale, float minHeight,
        bool isTree = false)
    {
        var td = land.terrainData;
        Vector3 origin = land.transform.position;
        Vector3 size = td.size;
        Vector3 center = origin + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        float islandR = size.x * 0.45f; // 島の半径≈460m
        var spots = new System.Collections.Generic.List<Vector3>();

        // 木は3.6mグリッドで深林サンプリング、草・花・灌木は1.8mの超細密グリッドで島全域から高密度サンプリング
        float step = isTree ? 3.6f : 1.8f;
        for (float z = 50f; z < size.z - 50f; z += step)
        {
            for (float x = 50f; x < size.x - 50f; x += step)
            {
                float jx = x + (float)(rng.NextDouble() * (step * 0.8) - (step * 0.4));
                float jz = z + (float)(rng.NextDouble() * (step * 0.8) - (step * 0.4));
                Vector3 p = new Vector3(origin.x + jx, 0f, origin.z + jz);
                p.y = land.SampleHeight(p) + origin.y;

                // 水面以下はスキップ
                if (p.y < minHeight) continue;

                // 島の外周はスキップ
                float dCenter = Horizontal(p, center);
                if (dCenter > islandR) continue;

                // サンクチュアリ中央を空ける
                if (Horizontal(p, sanctuaryCenter) < sanctuaryClear) continue;

                // 北崖を空ける
                if (Horizontal(p, cliffCenter) < cliffClear) continue;

                // ── ゾーニング：西側大草原 vs 東部・北部大樹海 ──
                bool inWestMeadow = (p.x < 460f && p.z < 650f);
                if (isTree)
                {
                    // 西側大草原ゾーン：地平線まで突き抜ける草原の開放感を保つため、木は爽やかな一本杉や木立（4%のみ）
                    if (inWestMeadow && rng.NextDouble() > 0.04)
                        continue;

                    // 東部〜北東部〜北部（x >= 460f または z >= 650f）：
                    // 「森林をもっと広く深く」するため、間引かず高密度に林立！
                }

                // 垂直崖（n.y < 0.38）以外はすべて草花・木を配置可能！
                float nx = (p.x - origin.x) / size.x;
                float nz = (p.z - origin.z) / size.z;
                Vector3 n = td.GetInterpolatedNormal(nx, nz);
                if (n.y < 0.38f) continue;

                spots.Add(p);

                // 大樹海ゾーン（東部・北部）では天蓋が重なり合う深い森（密林クラスター）を自動形成
                if (isTree && !inWestMeadow && rng.NextDouble() < 0.70)
                {
                    Vector3 tc = p + new Vector3((float)(rng.NextDouble() * 5.0 - 2.5), 0f, (float)(rng.NextDouble() * 5.0 - 2.5));
                    tc.y = land.SampleHeight(tc) + origin.y;
                    spots.Add(tc);
                }

                // 草原・森林共通で草むら・花畑の高密度クラスター（密集群生）を形成
                if (!isTree && rng.NextDouble() < 0.85)
                {
                    Vector3 c1 = p + new Vector3((float)(rng.NextDouble() * 3.0 - 1.5), 0f, (float)(rng.NextDouble() * 3.0 - 1.5));
                    c1.y = land.SampleHeight(c1) + origin.y;
                    spots.Add(c1);

                    if (rng.NextDouble() < 0.60)
                    {
                        Vector3 c2 = p + new Vector3((float)(rng.NextDouble() * 4.0 - 2.0), 0f, (float)(rng.NextDouble() * 4.0 - 2.0));
                        c2.y = land.SampleHeight(c2) + origin.y;
                        spots.Add(c2);
                    }
                }
            }
        }

        // シャッフル
        for (int i = spots.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (spots[i], spots[j]) = (spots[j], spots[i]);
        }

        int take = Mathf.Min(maxCount, spots.Count);
        for (int i = 0; i < take; i++)
        {
            string prefabPath;
            float sMin = minScale;
            float sMax = maxScale;

            if (isTree)
            {
                // 東部・北部の森林ゾーンでは深緑の巨木（モミの木 Fir 01〜05、深緑広葉樹）を70%の高頻度でブレンド
                bool isDeepForest = (spots[i].x >= 460f || spots[i].z >= 650f);
                if (isDeepForest && rng.NextDouble() < 0.70)
                {
                    prefabPath = DenseDarkGreenTrees[rng.Next(DenseDarkGreenTrees.Length)];
                    sMin = 1.3f;
                    sMax = 2.5f; // 見上げるような大樹海の巨木
                }
                else
                {
                    prefabPath = paths[rng.Next(paths.Length)];
                }
            }
            else
            {
                prefabPath = paths[rng.Next(paths.Length)];
            }

            Place(root, prefabPath, spots[i], rng, sMin, sMax);
        }
    }

    /// <summary>1024m島の海岸沿いに岩と水辺植物を配置</summary>
    static void PlaceShoreGrand(GameObject root, Terrain land, System.Random rng, float water)
    {
        var td = land.terrainData;
        Vector3 origin = land.transform.position;
        Vector3 size = td.size;
        Vector3 center = origin + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        float islandR = size.x * 0.43f;

        // 岩（海岸線を一周、220個）
        int rocks = 0;
        for (int i = 0; i < 500 && rocks < 220; i++)
        {
            float ang = (float)(i / 500.0 * Mathf.PI * 2.0 + rng.NextDouble() * 0.05);
            float rad = islandR - 15f + (float)rng.NextDouble() * 35f;
            Vector3 p = center + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
            p.y = land.SampleHeight(p) + origin.y;
            if (p.y < water - 0.5f || p.y > water + 6f) continue;
            Place(root, Rocks[rng.Next(Rocks.Length)], p, rng, 0.85f, 1.8f);
            rocks++;
        }

        // 水辺植物（ヨシ・スイレン・ガマ）
        int plants = 0;
        for (int i = 0; i < 400 && plants < 140; i++)
        {
            float ang = (float)(i / 400.0 * Mathf.PI * 2.0 + rng.NextDouble() * 0.08);
            float rad = islandR - 8f + (float)rng.NextDouble() * 25f;
            Vector3 p = center + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
            p.y = land.SampleHeight(p) + origin.y;
            if (p.y < water - 0.3f || p.y > water + 4f) continue;
            Place(root, ShorePlants[rng.Next(ShorePlants.Length)], p, rng, 0.9f, 1.4f);
            plants++;
        }

        // カルデラ池周り（x=420, z=440）にも水辺植物
        Vector3 lakeCenter = new Vector3(420f, 0f, 440f);
        for (int i = 0; i < 36; i++)
        {
            float ang = i / 36f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.15f;
            float rad = 30f + (float)rng.NextDouble() * 12f;
            Vector3 p = lakeCenter + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
            p.y = land.SampleHeight(p) + origin.y;
            if (p.y < water - 0.3f) continue;
            Place(root, ShorePlants[rng.Next(ShorePlants.Length)], p, rng, 0.9f, 1.35f);
        }

        // 川沿い（x=440→120）にも岩と水辺植物
        for (float wx = 140f; wx < 420f; wx += 15f)
        {
            float riverT = Mathf.Clamp01((512f - wx) / 360f);
            float riverZ = Mathf.Lerp(440f, 160f, riverT) + Mathf.Sin(wx * 0.035f) * 24f;
            Vector3 rp = new Vector3(wx + (float)(rng.NextDouble() * 10 - 5), 0f, riverZ + (float)(rng.NextDouble() * 10 - 5));
            rp.y = land.SampleHeight(rp) + origin.y;
            if (rp.y > water - 0.2f && rp.y < 30f)
            {
                Place(root, ShorePlants[rng.Next(ShorePlants.Length)], rp, rng, 0.8f, 1.2f);
                if (rng.NextDouble() > 0.5)
                    Place(root, Rocks[rng.Next(Rocks.Length)], rp + new Vector3(2f, 0f, 2f), rng, 0.7f, 1.4f);
            }
        }
    }

    /// <summary>1024m島の各エリアにシンボルツリーを配置</summary>
    static void PlaceHeroTreesGrand(GameObject root, Terrain land)
    {
        Vector3 origin = land.transform.position;
        Vector3[] spots =
        {
            new Vector3(350f, 0f, 350f),  // 南西
            new Vector3(680f, 0f, 300f),  // 南東
            new Vector3(280f, 0f, 550f),  // 西
            new Vector3(740f, 0f, 520f),  // 東
            new Vector3(380f, 0f, 660f),  // 北西
            new Vector3(640f, 0f, 620f),  // 北東
            new Vector3(512f, 0f, 360f),  // 南中央
            new Vector3(380f, 0f, 480f),  // カルデラ池の近く
            new Vector3(450f, 0f, 380f),  // 川の上流
            new Vector3(720f, 0f, 680f),  // 東山岳の展望台
        };
        string[] heroes =
        {
            "BlossomTree_01.prefab", "WillowTree_01_Pink.prefab",
            "BlossomTree_03.prefab", "WillowTree_02_Green.prefab",
            "BlossomTree_05.prefab", "WillowTree_03_Pink.prefab",
            "BroadleafTree_01_Red.prefab", "WillowTree_05_Pink.prefab"
        };
        var rng = new System.Random(7);
        for (int i = 0; i < spots.Length; i++)
        {
            Vector3 p = spots[i];
            p.y = land.SampleHeight(p) + origin.y;
            if (p.y < 7f) continue;
            Place(root, heroes[i % heroes.Length], p, rng, 1.4f, 1.8f);
        }
    }

    /// <summary>1024m島の海岸線にヤシの木を配置（150本）</summary>
    static void PlacePalmsGrand(GameObject root, Terrain land, System.Random rng, float water)
    {
        Vector3 origin = land.transform.position;
        Vector3 size = land.terrainData.size;
        Vector3 center = origin + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        float islandR = size.x * 0.41f;
        AdventurePalmFactory.ResetMaterials();

        // 海岸線を一周するヤシ
        int n = 0;
        for (int i = 0; i < 350 && n < 130; i++)
        {
            float ang = i / 130f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.15f;
            float rad = islandR - 25f + (float)rng.NextDouble() * 35f;
            Vector3 p = center + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
            p.y = land.SampleHeight(p) + origin.y;
            if (p.y < water + 0.5f || p.y > water + 14f) continue;
            // 北崖エリアはスキップ
            if (Mathf.Abs(p.x - 512f) < 55f && p.z > 700f) continue;
            AdventurePalmFactory.Create(root.transform, p, rng.Next());
            n++;
        }

        // 内陸・オアシス・川沿いのアクセントヤシ
        Vector3[] inland =
        {
            new Vector3(400f, 0f, 380f), new Vector3(410f, 0f, 390f),
            new Vector3(600f, 0f, 400f), new Vector3(615f, 0f, 410f),
            new Vector3(350f, 0f, 500f), new Vector3(340f, 0f, 510f),
            new Vector3(650f, 0f, 550f), new Vector3(660f, 0f, 540f),
            new Vector3(450f, 0f, 600f), new Vector3(440f, 0f, 610f),
            new Vector3(300f, 0f, 320f), new Vector3(260f, 0f, 260f),
        };
        foreach (var e in inland)
        {
            Vector3 p = e;
            p.y = land.SampleHeight(p) + origin.y;
            if (p.y > water + 1f)
                AdventurePalmFactory.Create(root.transform, p, rng.Next());
        }
    }

    /// <summary>1024m島の森林・花畑に蝶を配置（36箇所）</summary>
    static void PlaceButterfliesGrand(GameObject root, Terrain land, System.Random rng)
    {
        string[] flies =
        {
            "Code Related/Butterfly_01.prefab",
            "Code Related/Butterfly_02.prefab",
            "Code Related/Butterfly_03.prefab"
        };
        Vector3 origin = land.transform.position;
        Vector3[] homes =
        {
            new Vector3(350f, 0f, 380f), new Vector3(600f, 0f, 350f), new Vector3(300f, 0f, 500f),
            new Vector3(680f, 0f, 480f), new Vector3(400f, 0f, 620f), new Vector3(550f, 0f, 400f),
            new Vector3(450f, 0f, 500f), new Vector3(380f, 0f, 350f), new Vector3(620f, 0f, 550f),
            new Vector3(500f, 0f, 650f), new Vector3(350f, 0f, 600f), new Vector3(650f, 0f, 400f),
            new Vector3(420f, 0f, 450f), new Vector3(440f, 0f, 430f), new Vector3(410f, 0f, 460f),
            new Vector3(280f, 0f, 400f), new Vector3(320f, 0f, 450f), new Vector3(580f, 0f, 480f),
            new Vector3(620f, 0f, 450f), new Vector3(520f, 0f, 420f), new Vector3(480f, 0f, 430f),
            new Vector3(360f, 0f, 540f), new Vector3(390f, 0f, 570f), new Vector3(630f, 0f, 600f),
            new Vector3(670f, 0f, 580f), new Vector3(480f, 0f, 620f), new Vector3(540f, 0f, 620f),
            new Vector3(300f, 0f, 300f), new Vector3(250f, 0f, 250f), new Vector3(700f, 0f, 350f),
            new Vector3(720f, 0f, 420f), new Vector3(240f, 0f, 500f), new Vector3(750f, 0f, 550f),
            new Vector3(450f, 0f, 320f), new Vector3(580f, 0f, 320f), new Vector3(512f, 0f, 660f)
        };
        foreach (var h in homes)
        {
            Vector3 p = h;
            p.y = land.SampleHeight(p) + origin.y + 1.8f;
            if (p.y < 8f) continue;
            PlaceButterfly(root, flies[rng.Next(flies.Length)], p, rng);
        }
    }

    /// <summary>楽園の動物たち（カピバラ、犬、猫）を配置</summary>
    static void PlaceAnimalsGrand(GameObject root, Terrain land, System.Random rng)
    {
        var animalRoot = new GameObject("Animals");
        animalRoot.transform.SetParent(root.transform, false);

        Vector3 origin = land.transform.position;

        // 1. カピバラ（Capyta）カルデラ湖畔や水辺、草地で群れる
        var capytaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Niko&Capyta/Assets/Prefabs/Capyta.prefab");
        if (capytaPrefab != null)
        {
            // カルデラ湖畔の家族（6頭）
            Vector3 lakeCenter = new Vector3(420f, 0f, 440f);
            for (int i = 0; i < 6; i++)
            {
                float ang = (float)(i / 6.0 * Mathf.PI * 0.8 + 0.2 + rng.NextDouble() * 0.15);
                float rad = 31f + (float)rng.NextDouble() * 6f;
                Vector3 p = lakeCenter + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                p.y = land.SampleHeight(p) + origin.y;
                var c = (GameObject)PrefabUtility.InstantiatePrefab(capytaPrefab, animalRoot.transform);
                c.name = "Capyta_Lake_" + i;
                c.transform.position = p;
                c.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                c.transform.localScale = Vector3.one * (0.9f + (float)rng.NextDouble() * 0.35f);
            }

            // 南西の川辺の群れ（5頭）
            Vector3[] riverSpots =
            {
                new Vector3(320f, 0f, 340f), new Vector3(315f, 0f, 335f),
                new Vector3(260f, 0f, 280f), new Vector3(255f, 0f, 275f),
                new Vector3(210f, 0f, 220f)
            };
            for (int i = 0; i < riverSpots.Length; i++)
            {
                Vector3 p = riverSpots[i];
                p.y = land.SampleHeight(p) + origin.y;
                var c = (GameObject)PrefabUtility.InstantiatePrefab(capytaPrefab, animalRoot.transform);
                c.name = "Capyta_River_" + i;
                c.transform.position = p;
                c.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                c.transform.localScale = Vector3.one * (0.85f + (float)rng.NextDouble() * 0.3f);
            }

            // 草原・木陰ののんびりカピバラ（7頭）
            Vector3[] meadowSpots =
            {
                new Vector3(370f, 0f, 520f), new Vector3(375f, 0f, 523f),
                new Vector3(580f, 0f, 380f), new Vector3(585f, 0f, 385f),
                new Vector3(640f, 0f, 480f), new Vector3(460f, 0f, 580f),
                new Vector3(512f, 0f, 430f)
            };
            for (int i = 0; i < meadowSpots.Length; i++)
            {
                Vector3 p = meadowSpots[i];
                p.y = land.SampleHeight(p) + origin.y;
                var c = (GameObject)PrefabUtility.InstantiatePrefab(capytaPrefab, animalRoot.transform);
                c.name = "Capyta_Meadow_" + i;
                c.transform.position = p;
                c.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                c.transform.localScale = Vector3.one * (0.9f + (float)rng.NextDouble() * 0.3f);
            }
        }

        // 2. 猫（SM_CartoonAnimal_Cat）白亜遺跡の周りや日当たりの良い岩場
        var catPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolyOne/Cartoon Dog, Cat/Prefab/SM_CartoonAnimal_Cat.prefab");
        if (catPrefab != null)
        {
            Vector3[] catSpots =
            {
                new Vector3(518f, 0f, 475f), new Vector3(506f, 0f, 475f),
                new Vector3(535f, 0f, 512f), new Vector3(488f, 0f, 512f),
                new Vector3(512f, 0f, 545f), new Vector3(430f, 0f, 470f),
                new Vector3(600f, 0f, 450f), new Vector3(350f, 0f, 450f),
                new Vector3(480f, 0f, 660f), new Vector3(540f, 0f, 660f)
            };
            for (int i = 0; i < catSpots.Length; i++)
            {
                Vector3 p = catSpots[i];
                p.y = land.SampleHeight(p) + origin.y;
                var cat = (GameObject)PrefabUtility.InstantiatePrefab(catPrefab, animalRoot.transform);
                cat.name = "Cat_" + i;
                cat.transform.position = p;
                cat.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                cat.transform.localScale = Vector3.one * (0.8f + (float)rng.NextDouble() * 0.25f);
            }
        }

        // 3. 犬（SM_CartoonAnimal_Dog）草原や小道
        var dogPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolyOne/Cartoon Dog, Cat/Prefab/SM_CartoonAnimal_Dog.prefab");
        if (dogPrefab != null)
        {
            Vector3[] dogSpots =
            {
                new Vector3(512f, 0f, 460f), new Vector3(525f, 0f, 450f),
                new Vector3(450f, 0f, 480f), new Vector3(570f, 0f, 480f),
                new Vector3(380f, 0f, 380f), new Vector3(620f, 0f, 520f),
                new Vector3(460f, 0f, 650f), new Vector3(320f, 0f, 550f),
                new Vector3(680f, 0f, 400f), new Vector3(280f, 0f, 300f)
            };
            for (int i = 0; i < dogSpots.Length; i++)
            {
                Vector3 p = dogSpots[i];
                p.y = land.SampleHeight(p) + origin.y;
                var dog = (GameObject)PrefabUtility.InstantiatePrefab(dogPrefab, animalRoot.transform);
                dog.name = "Dog_" + i;
                dog.transform.position = p;
                dog.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                dog.transform.localScale = Vector3.one * (0.85f + (float)rng.NextDouble() * 0.25f);
            }
        }
    }

    /// <summary>森の小鳥・海岸のカモメの3D立体環境音を配置（白い四角化する壊れたVFXは排除）</summary>
    static void PlaceBirdsAndAmbienceGrand(GameObject root, Terrain land, System.Random rng)
    {
        var birdRoot = new GameObject("BirdsAndAmbience");
        birdRoot.transform.SetParent(root.transform, false);

        Vector3 origin = land.transform.position;

        // 1. 森の小鳥のさえずり（3D AudioSource）
        var forestClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/AudioFiles/03_amb/birdforest_amb.wav");
        if (forestClip != null)
        {
            Vector3[] forestSoundSpots =
            {
                new Vector3(420f, 0f, 480f), new Vector3(580f, 0f, 450f),
                new Vector3(360f, 0f, 400f), new Vector3(640f, 0f, 520f),
                new Vector3(460f, 0f, 560f), new Vector3(500f, 0f, 630f),
                new Vector3(350f, 0f, 580f), new Vector3(650f, 0f, 380f)
            };
            for (int i = 0; i < forestSoundSpots.Length; i++)
            {
                Vector3 p = forestSoundSpots[i];
                p.y = land.SampleHeight(p) + origin.y + 4f;
                var sndGo = new GameObject("BirdSound_Forest_" + i);
                sndGo.transform.SetParent(birdRoot.transform, false);
                sndGo.transform.position = p;

                var src = sndGo.AddComponent<AudioSource>();
                src.clip = forestClip;
                src.loop = true;
                src.playOnAwake = true;
                src.spatialBlend = 1.0f; // 完全3Dサウンド
                src.minDistance = 12f;
                src.maxDistance = 55f;
                src.volume = 0.5f;
                src.rolloffMode = AudioRolloffMode.Linear;
            }
        }

        // 3. 海岸線のカモメの声（3D AudioSource）
        var seagullClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/AudioFiles/06_birds/seagulls_1.wav");
        if (seagullClip != null)
        {
            Vector3[] seaSoundSpots =
            {
                new Vector3(512f, 8f, 120f), // 南海岸
                new Vector3(140f, 8f, 512f), // 西海岸
                new Vector3(880f, 8f, 512f), // 東海岸
                new Vector3(512f, 15f, 790f), // 北崖下海面
                new Vector3(240f, 8f, 240f), // 南西の川河口
                new Vector3(780f, 8f, 260f), // 南東ビーチ
            };
            for (int i = 0; i < seaSoundSpots.Length; i++)
            {
                var sndGo = new GameObject("SeagullSound_" + i);
                sndGo.transform.SetParent(birdRoot.transform, false);
                sndGo.transform.position = seaSoundSpots[i];

                var src = sndGo.AddComponent<AudioSource>();
                src.clip = seagullClip;
                src.loop = true;
                src.playOnAwake = true;
                src.spatialBlend = 1.0f;
                src.minDistance = 18f;
                src.maxDistance = 80f;
                src.volume = 0.55f;
                src.rolloffMode = AudioRolloffMode.Linear;
            }
        }
    }

    // ══════════════════════════════════════════
    //  旧256m島用メソッド群（後方互換）
    // ══════════════════════════════════════════

    static void Scatter256(
        GameObject root, Terrain land, System.Random rng, string[] paths,
        int maxCount, Vector3 spawn, float spawnClear,
        float minScale, float maxScale, float minLandAboveWater)
    {
        var td = land.terrainData;
        Vector3 origin = land.transform.position;
        Vector3 size = td.size;
        Vector3 center = origin + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        var spots = new System.Collections.Generic.List<Vector3>();

        for (float z = 28f; z < size.z - 28f; z += 6.5f)
        {
            for (float x = 28f; x < size.x - 28f; x += 6.5f)
            {
                float jx = x + (float)(rng.NextDouble() * 4.5 - 2.25);
                float jz = z + (float)(rng.NextDouble() * 4.5 - 2.25);
                Vector3 p = new Vector3(origin.x + jx, 0f, origin.z + jz);
                p.y = land.SampleHeight(p) + origin.y;
                if (p.y < minLandAboveWater) continue;
                float dCenter = Horizontal(p, center);
                if (dCenter > 102f || dCenter < 14f) continue;
                if (Horizontal(p, spawn) < spawnClear) continue;
                if (p.z > 184f) continue;
                Vector3 n = td.GetInterpolatedNormal((p.x - origin.x) / size.x, (p.z - origin.z) / size.z);
                if (n.y < 0.72f) continue;
                spots.Add(p);
            }
        }
        for (int i = spots.Count - 1; i > 0; i--)
        { int j = rng.Next(i + 1); (spots[i], spots[j]) = (spots[j], spots[i]); }
        int take = Mathf.Min(maxCount, spots.Count);
        for (int i = 0; i < take; i++)
            Place(root, paths[rng.Next(paths.Length)], spots[i], rng, minScale, maxScale);
    }

    static void PlaceShore256(GameObject root, Terrain land, System.Random rng, float water, Vector3 spawn)
    {
        var td = land.terrainData;
        Vector3 origin = land.transform.position;
        Vector3 size = td.size;
        Vector3 center = origin + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        int rocks = 0;
        for (int i = 0; i < 90 && rocks < 36; i++)
        {
            float ang = (float)(i / 90f * Mathf.PI * 2f + rng.NextDouble() * 0.08);
            float rad = 96f + (float)rng.NextDouble() * 12f;
            Vector3 p = center + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
            p.y = land.SampleHeight(p) + origin.y;
            if (p.y < water - 0.4f || p.y > water + 4.5f) continue;
            if (Horizontal(p, spawn) < 10f) continue;
            if (p.z > 188f) continue;
            Place(root, Rocks[rng.Next(Rocks.Length)], p, rng, 0.7f, 1.35f);
            rocks++;
        }
        int plants = 0;
        for (int i = 0; i < 80 && plants < 28; i++)
        {
            float ang = (float)(i / 80f * Mathf.PI * 2f + rng.NextDouble() * 0.1);
            float rad = 100f + (float)rng.NextDouble() * 8f;
            Vector3 p = center + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
            p.y = land.SampleHeight(p) + origin.y;
            if (p.y < water - 0.2f || p.y > water + 3.2f) continue;
            if (p.z > 188f) continue;
            Place(root, ShorePlants[rng.Next(ShorePlants.Length)], p, rng, 0.85f, 1.2f);
            plants++;
        }
    }

    static void PlaceHeroTrees256(GameObject root, Terrain land)
    {
        Vector3 origin = land.transform.position;
        Vector3[] spots = { new Vector3(98f,0f,122f), new Vector3(158f,0f,108f), new Vector3(118f,0f,148f) };
        string[] heroes = { "BlossomTree_01.prefab", "WillowTree_01_Pink.prefab", "BlossomTree_03.prefab" };
        var rng = new System.Random(7);
        for (int i = 0; i < spots.Length; i++)
        { Vector3 p = spots[i]; p.y = land.SampleHeight(p) + origin.y; Place(root, heroes[i], p, rng, 1.15f, 1.35f); }
    }

    static void PlacePalms256(GameObject root, Terrain land, System.Random rng, float water, Vector3 spawn)
    {
        Vector3 origin = land.transform.position;
        Vector3 size = land.terrainData.size;
        Vector3 center = origin + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        AdventurePalmFactory.ResetMaterials();
        int n = 0;
        for (int i = 0; i < 40 && n < 14; i++)
        {
            float ang = i / 14f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.2f;
            float rad = 86f + (float)rng.NextDouble() * 10f;
            Vector3 p = center + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
            p.y = land.SampleHeight(p) + origin.y;
            if (p.y < water + 0.4f || p.y > water + 8f) continue;
            if (Horizontal(p, spawn) < 14f || p.z > 182f) continue;
            AdventurePalmFactory.Create(root.transform, p, rng.Next());
            n++;
        }
        Vector3[] inland = { new Vector3(128f,0f,158f), new Vector3(152f,0f,160f), new Vector3(116f,0f,154f) };
        foreach (var e in inland)
        { Vector3 p = e; p.y = land.SampleHeight(p) + origin.y; AdventurePalmFactory.Create(root.transform, p, rng.Next()); }
    }

    static void PlaceButterflies256(GameObject root, Terrain land, System.Random rng)
    {
        string[] flies = { "Code Related/Butterfly_01.prefab", "Code Related/Butterfly_02.prefab", "Code Related/Butterfly_03.prefab" };
        Vector3[] homes = { new Vector3(118f,0f,140f), new Vector3(148f,0f,132f), new Vector3(108f,0f,118f),
            new Vector3(132f,0f,150f), new Vector3(156f,0f,148f), new Vector3(124f,0f,108f) };
        foreach (var h in homes)
        { Vector3 p = h; p.y = land.SampleHeight(p) + land.transform.position.y + 1.6f;
            PlaceButterfly(root, flies[rng.Next(flies.Length)], p, rng); }
    }

    // ══════════════════════════════════════════
    //  共通ユーティリティ
    // ══════════════════════════════════════════

    static void BrightenScene()
    {
        // 抜けるように澄んだ鮮やかな青空スカイボックスを作成・適用
        var skyShader = Shader.Find("RustAndFloat/ClearBlueSky");
        if (skyShader != null)
        {
            const string skyMatPath = "Assets/RustAndFloat/Materials/ClearBlueSky.mat";
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>(skyMatPath);
            if (skyMat == null)
            {
                skyMat = new Material(skyShader);
                AssetDatabase.CreateAsset(skyMat, skyMatPath);
            }
            else
            {
                skyMat.shader = skyShader;
            }

            // 吸い込まれるような深く鮮やかな青空カラー
            skyMat.SetColor("_TopColor", new Color(0.01f, 0.24f, 0.85f, 1f));     // 天頂の深い群青
            skyMat.SetColor("_MidColor", new Color(0.05f, 0.48f, 0.98f, 1f));     // 鮮やかなアズールブルー
            skyMat.SetColor("_HorizonColor", new Color(0.40f, 0.75f, 0.98f, 1f));  // 地平線の澄んだシアンブルー
            skyMat.SetColor("_GroundColor", new Color(0.22f, 0.58f, 0.90f, 1f));   // 海面のサファイア
            skyMat.SetColor("_SunColor", new Color(1.0f, 0.98f, 0.90f, 1f));      // 温かな太陽
            skyMat.SetFloat("_SunSize", 0.035f);
            skyMat.SetFloat("_SunGlow", 2.6f);
            skyMat.SetFloat("_Exponent", 0.65f);
            skyMat.SetFloat("_HorizonOffset", 0.01f);
            EditorUtility.SetDirty(skyMat);
            RenderSettings.skybox = skyMat;
        }

        var sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude)
            .FirstOrDefault(l => l.type == LightType.Directional);
        if (sun != null)
        {
            // 心地よい快晴の夏の日差し（南東から、仰角50度）
            sun.transform.rotation = Quaternion.Euler(50f, 140f, 0f);
            sun.intensity = 1.8f;
            sun.color = new Color(1f, 0.98f, 0.92f);
            sun.shadows = LightShadows.Soft;
        }

        // アンビエントライト（青空と緑の草原の反射光）
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.65f, 0.82f, 1.0f);     // 青空の光
        RenderSettings.ambientEquatorColor = new Color(0.92f, 0.93f, 0.85f); // 水平線の柔らかな光
        RenderSettings.ambientGroundColor = new Color(0.45f, 0.62f, 0.35f);  // 緑の草原の照り返し
        RenderSettings.ambientIntensity = 1.3f;
        RenderSettings.fog = false;

        // メインカメラのクリアフラグをSkyboxに
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.Skybox;
        }
    }

    /// <summary>空に浮かぶとても気持ちの良いふんわりとした白い雲群を生成</summary>
    static void PlaceParadiseClouds(GameObject root, System.Random rng)
    {
        var cloudsRoot = new GameObject("ParadiseClouds");
        cloudsRoot.transform.SetParent(root.transform, false);

        // 雲マテリアルのロードまたは作成
        var cloudShader = Shader.Find("RustAndFloat/FluffyCloud");
        Material cloudMat = null;
        if (cloudShader != null)
        {
            const string cloudMatPath = "Assets/RustAndFloat/Materials/FluffyCloud.mat";
            cloudMat = AssetDatabase.LoadAssetAtPath<Material>(cloudMatPath);
            if (cloudMat == null)
            {
                cloudMat = new Material(cloudShader);
                cloudMat.SetColor("_BaseColor", new Color(0.96f, 0.98f, 1.0f, 0.94f));
                cloudMat.SetColor("_ShadowColor", new Color(0.78f, 0.86f, 0.96f, 0.90f));
                cloudMat.SetColor("_RimColor", new Color(1.0f, 1.0f, 1.0f, 1.0f));
                cloudMat.SetFloat("_RimPower", 2.2f);
                AssetDatabase.CreateAsset(cloudMat, cloudMatPath);
            }
        }
        if (cloudMat == null)
        {
            cloudMat = MakeColorMat(Color.white, 0.1f, 0.5f);
        }

        // 18個のふんわりとした立体雲クラスターを空（高度110m〜240m）に散布
        const int cloudCount = 18;
        for (int i = 0; i < cloudCount; i++)
        {
            var clusterGo = new GameObject("CloudCluster_" + i);
            clusterGo.transform.SetParent(cloudsRoot.transform, false);

            // 島の広がり（1000m）全体の上空に散布
            float cx = (float)(rng.NextDouble() * 1100.0 - 50.0);
            float cz = (float)(rng.NextDouble() * 1100.0 - 50.0);
            float cy = 115f + (float)(rng.NextDouble() * 110.0); // 高度115m〜225m
            clusterGo.transform.position = new Vector3(cx, cy, cz);

            // 各雲クラスターは6〜9個の重なり合う球体で、ぽっかりとした入道雲・夏雲を形成
            int puffCount = 6 + rng.Next(4);
            float baseScale = 24f + (float)rng.NextDouble() * 26f; // クラスター全体のスケール（24m〜50m）
            for (int p = 0; p < puffCount; p++)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puff.name = "Puff_" + p;
                puff.transform.SetParent(clusterGo.transform, false);

                // 中央ほど大きく、周囲に広がる雲のモコモコ感
                float px = (float)(rng.NextDouble() * baseScale * 1.4 - baseScale * 0.7);
                float pz = (float)(rng.NextDouble() * baseScale * 1.0 - baseScale * 0.5);
                float py = (float)(rng.NextDouble() * baseScale * 0.45 - baseScale * 0.1);
                puff.transform.localPosition = new Vector3(px, py, pz);

                // 扁平・丸みを帯びたスケール
                float sx = baseScale * (0.55f + (float)rng.NextDouble() * 0.55f);
                float sy = baseScale * (0.35f + (float)rng.NextDouble() * 0.45f);
                float sz = baseScale * (0.55f + (float)rng.NextDouble() * 0.55f);
                puff.transform.localScale = new Vector3(sx, sy, sz);

                puff.GetComponent<Renderer>().sharedMaterial = cloudMat;
                Object.DestroyImmediate(puff.GetComponent<Collider>());
            }

            // 風に乗ってゆっくり空を漂うアニメーション
            clusterGo.AddComponent<AdventureCloudDrift>();
        }

        // 上空の爽やかな風の音（skywind_1.wav）
        var windClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/AudioFiles/03_amb/skywind_1.wav");
        if (windClip != null)
        {
            var windGo = new GameObject("SkyWindAmbience");
            windGo.transform.SetParent(cloudsRoot.transform, false);
            windGo.transform.position = new Vector3(512f, 120f, 512f);
            var src = windGo.AddComponent<AudioSource>();
            src.clip = windClip;
            src.loop = true;
            src.playOnAwake = true;
            src.spatialBlend = 0.6f; // 上空の薄い気配に
            src.volume = 0.03f; // 耳障りにならないよう極めて控えめに
        }
    }

    static void PlaceButterfly(GameObject root, string file, Vector3 pos, System.Random rng)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + file);
        if (prefab == null) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 10f;
        var bSpawn = go.GetComponent<IdyllicFantasyNature.ButterflySpawn>();
        if (bSpawn != null) Object.DestroyImmediate(bSpawn);
        var anim = go.GetComponent<Animator>();
        if (anim != null) anim.enabled = true;
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.SetActive(true);
        go.AddComponent<AdventureButterflyDrift>();
    }

    static void Place(GameObject root, string file, Vector3 pos, System.Random rng, float minScale, float maxScale)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + file);
        if (prefab == null)
        {
            Debug.LogWarning("[RustAndFloat] missing " + file);
            return;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
        float s = minScale + (float)rng.NextDouble() * (maxScale - minScale);
        go.transform.localScale = Vector3.one * s;
    }

    static float Horizontal(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ══════════════════════════════════════════
    //  カエル・カニ・トンボ・セミ・昆虫
    // ══════════════════════════════════════════

    /// <summary>3つの池（オアシス池・カルデラ湖・草原池）と小川（上流・本流）に睡蓮・蓮の葉・葦・ガマ・飛び石を美しく配置</summary>
    static void PlaceWaterFloraAndProps(GameObject root, Terrain land, System.Random rng)
    {
        var waterRoot = new GameObject("WaterFloraAndProps");
        waterRoot.transform.SetParent(root.transform, false);

        string[] waterLilies = {
            "Waterlily_01.prefab",
            "Waterlily_02.prefab"
        };
        string[] lilyPads = {
            "LilyPads_01.prefab",
            "LilyPads_02.prefab",
            "LilyPads_03.prefab"
        };
        string[] reeds = {
            "Reeds_01.prefab",
            "Reeds_02.prefab",
            "Reeds_03.prefab",
            "Cattail_01.prefab",
            "Cattail_02.prefab"
        };
        string[] stones = {
            "Stones_01.prefab",
            "Stones_02.prefab",
            "Stone_Big_01.prefab",
            "Stone_Medium_01.prefab"
        };

        // 1. 中央台地足元のオアシス湧水池 (480, 48.2, 455, 半径16m)
        Vector3 oasisC = new Vector3(480f, 48.25f, 455f);
        for (int i = 0; i < 14; i++)
        {
            float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float r = (float)(rng.NextDouble() * 12f);
            Vector3 p = oasisC + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
            Place(waterRoot, lilyPads[rng.Next(lilyPads.Length)], p, rng, 0.9f, 1.4f);
            if (i < 8)
                Place(waterRoot, waterLilies[rng.Next(waterLilies.Length)], p + new Vector3(0.4f, 0f, 0.4f), rng, 0.8f, 1.3f);
        }
        for (int i = 0; i < 28; i++)
        {
            float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float r = 14f + (float)(rng.NextDouble() * 5f);
            Vector3 p = new Vector3(oasisC.x + Mathf.Cos(ang) * r, 0f, oasisC.z + Mathf.Sin(ang) * r);
            p.y = land.SampleHeight(p) + land.transform.position.y;
            Place(waterRoot, reeds[rng.Next(reeds.Length)], p, rng, 0.9f, 1.4f);
            if (i % 3 == 0)
                Place(waterRoot, stones[rng.Next(stones.Length)], p, rng, 0.8f, 1.5f);
        }

        // 2. 大カルデラ湖 (420, 25.5, 440, 半径38m)
        Vector3 lakeC = new Vector3(420f, 25.55f, 440f);
        for (int i = 0; i < 30; i++)
        {
            float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float r = (float)(rng.NextDouble() * 32f);
            Vector3 p = lakeC + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
            Place(waterRoot, lilyPads[rng.Next(lilyPads.Length)], p, rng, 1.0f, 1.6f);
            if (i < 18)
                Place(waterRoot, waterLilies[rng.Next(waterLilies.Length)], p + new Vector3(0.4f, 0f, 0.4f), rng, 0.9f, 1.5f);
        }
        for (int i = 0; i < 45; i++)
        {
            float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float r = 35f + (float)(rng.NextDouble() * 9f);
            Vector3 p = new Vector3(lakeC.x + Mathf.Cos(ang) * r, 0f, lakeC.z + Mathf.Sin(ang) * r);
            p.y = land.SampleHeight(p) + land.transform.position.y;
            Place(waterRoot, reeds[rng.Next(reeds.Length)], p, rng, 0.9f, 1.5f);
            if (i % 3 == 0)
                Place(waterRoot, stones[rng.Next(stones.Length)], p, rng, 1.0f, 2.0f);
        }

        // 3. 大草原の憩いのせせらぎ池 (290, 14.5, 320, 半径16m)
        Vector3 pondC = new Vector3(290f, 14.55f, 320f);
        for (int i = 0; i < 16; i++)
        {
            float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float r = (float)(rng.NextDouble() * 12f);
            Vector3 p = pondC + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
            Place(waterRoot, lilyPads[rng.Next(lilyPads.Length)], p, rng, 0.9f, 1.4f);
            if (i < 9)
                Place(waterRoot, waterLilies[rng.Next(waterLilies.Length)], p + new Vector3(0.4f, 0f, 0.4f), rng, 0.85f, 1.35f);
        }
        for (int i = 0; i < 28; i++)
        {
            float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float r = 14f + (float)(rng.NextDouble() * 5f);
            Vector3 p = new Vector3(pondC.x + Mathf.Cos(ang) * r, 0f, pondC.z + Mathf.Sin(ang) * r);
            p.y = land.SampleHeight(p) + land.transform.position.y;
            Place(waterRoot, reeds[rng.Next(reeds.Length)], p, rng, 0.9f, 1.4f);
            if (i % 3 == 0)
                Place(waterRoot, stones[rng.Next(stones.Length)], p, rng, 0.8f, 1.5f);
        }

        // 4. 小川沿いの葦と岩、飛び石（Stepping Stones）
        for (int i = 0; i < 35; i++)
        {
            float t = (float)i / 35f;
            float wx = Mathf.Lerp(410f, 165f, t);
            float wz = Mathf.Lerp(435f, 185f, t) + Mathf.Sin(wx * 0.025f) * 22f;
            float side = (rng.NextDouble() > 0.5 ? 1f : -1f) * (7f + (float)rng.NextDouble() * 6f);
            Vector3 p = new Vector3(wx + side, 0f, wz + side);
            p.y = land.SampleHeight(p) + land.transform.position.y;
            Place(waterRoot, reeds[rng.Next(reeds.Length)], p, rng, 0.85f, 1.4f);
            if (i % 4 == 0)
                Place(waterRoot, stones[rng.Next(stones.Length)], p, rng, 0.8f, 1.6f);
        }

        // 飛び石（川を歩いて渡れるポイントを2箇所に設置！）
        float[] crossT = { 0.35f, 0.68f };
        foreach (var ct in crossT)
        {
            float cx = Mathf.Lerp(410f, 165f, ct);
            float cz = Mathf.Lerp(435f, 185f, ct) + Mathf.Sin(cx * 0.025f) * 22f;
            float cy = Mathf.Lerp(25.0f, 5.8f, ct);
            for (int s = -2; s <= 2; s++)
            {
                Vector3 stoneP = new Vector3(cx + s * 2.5f, cy + 0.15f, cz);
                Place(waterRoot, "Stone_Big_01.prefab", stoneP, rng, 0.85f, 1.1f);
            }
        }
    }

    /// <summary>激しい高低差を下る「東の深林大渓流」沿いに巨石群・飛び石・苔岩・水辺植物・カジカガエルの美声を配置</summary>
    static void PlaceMountainGorgeProps(GameObject root, Terrain land, System.Random rng)
    {
        var gorgeRoot = new GameObject("MountainGorgeProps");
        gorgeRoot.transform.SetParent(root.transform, false);

        Vector3 origin = land.transform.position;

        // 1. 東の深林大渓流（x: 715, z: 570 〜 x: 425, z: 440、標高差約15m）沿いに巨石・岩壁・川底の岩を大量配置（約380個）
        const int steps = 90;
        for (int i = 0; i < steps; i++)
        {
            float t = (float)i / (steps - 1);
            float wx = Mathf.Lerp(715f, 425f, t);
            float wz = Mathf.Lerp(570f, 440f, t) + Mathf.Sin(wx * 0.035f) * 16f;

            // 川の中央・浅瀬・飛び石
            for (int r = 0; r < 2; r++)
            {
                float offsetCenter = (float)(rng.NextDouble() * 8.0 - 4.0);
                Vector3 cp = new Vector3(wx + offsetCenter * 0.4f, 0f, wz + offsetCenter);
                cp.y = land.SampleHeight(cp) + origin.y;
                Place(gorgeRoot, GorgeRocks[rng.Next(GorgeRocks.Length)], cp, rng, 0.9f, 2.2f);
            }

            // 両岸の急斜面・岩壁（Boulders）
            for (int side = -1; side <= 1; side += 2)
            {
                float bankDist = side * (5.5f + (float)rng.NextDouble() * 7.0f);
                Vector3 bp = new Vector3(wx + bankDist * 0.3f, 0f, wz + bankDist);
                bp.y = land.SampleHeight(bp) + origin.y;
                Place(gorgeRoot, GorgeRocks[rng.Next(GorgeRocks.Length)], bp, rng, 1.2f, 2.8f);

                // 岸辺の葦・シダ・ガマ
                if (rng.NextDouble() < 0.65)
                {
                    Vector3 plantP = bp + new Vector3((float)(rng.NextDouble() * 2.0 - 1.0), 0f, (float)(rng.NextDouble() * 2.0 - 1.0));
                    plantP.y = land.SampleHeight(plantP) + origin.y;
                    Place(gorgeRoot, ShorePlants[rng.Next(ShorePlants.Length)], plantP, rng, 0.85f, 1.5f);
                }
            }
        }

        // 2. 上流急流（オアシス池〜カルデラ湖）の岩場（70個）
        for (int i = 0; i < 25; i++)
        {
            float t = (float)i / 25f;
            float wx = Mathf.Lerp(475f, 425f, t);
            float wz = Mathf.Lerp(455f, 440f, t);
            float off = (float)(rng.NextDouble() * 10.0 - 5.0);
            Vector3 p = new Vector3(wx + off * 0.3f, 0f, wz + off);
            p.y = land.SampleHeight(p) + origin.y;
            Place(gorgeRoot, GorgeRocks[rng.Next(GorgeRocks.Length)], p, rng, 1.0f, 2.4f);
        }

        // 3. 渓流の要所にカジカガエルの美声を配置（岩陰から響く清流の鳴き声 4箇所）
        var kajikaClip = LoadAudioByGuid("6e4321ae4e14748bea428b93e3fdfcc9"); // 01カジカガエル.3.aif
        if (kajikaClip != null)
        {
            Vector3[] gorgeKajikaSpots =
            {
                new Vector3(650f, 0f, 545f), // 渓流上流
                new Vector3(570f, 0f, 500f), // 渓流中流滝壺
                new Vector3(490f, 0f, 465f), // 渓流下流合流部
                new Vector3(440f, 0f, 445f)  // カルデラ湖流入部
            };
            for (int i = 0; i < gorgeKajikaSpots.Length; i++)
            {
                Vector3 p = gorgeKajikaSpots[i];
                p.y = land.SampleHeight(p) + origin.y + 0.5f;

                var sGo = new GameObject("GorgeKajika_" + i);
                sGo.transform.SetParent(gorgeRoot.transform, false);
                sGo.transform.position = p;

                var src = sGo.AddComponent<AudioSource>();
                src.clip = kajikaClip;
                src.loop = true;
                src.playOnAwake = true;
                src.spatialBlend = 1.0f;
                src.minDistance = 6.0f;
                src.maxDistance = 38.0f;
                src.volume = 0.75f;
                src.rolloffMode = AudioRolloffMode.Linear;
            }
        }
    }

    /// <summary>3つの池や小川沿いにピョンピョン跳ねるカエルを配置（36匹）</summary>
    static void PlaceFrogs(GameObject root, Terrain land, System.Random rng)
    {
        var frogRoot = new GameObject("Frogs");
        frogRoot.transform.SetParent(root.transform, false);

        var frogMat = MakeColorMat(new Color(0.25f, 0.72f, 0.22f), 0.1f, 0.4f);
        var eyeMat = MakeColorMat(Color.black, 0.2f, 0.8f);

        Vector3 origin = land.transform.position;

        // 1. オアシス湧水池周辺（8匹）
        Vector3 oasisCenter = new Vector3(480f, 0f, 455f);
        for (int i = 0; i < 8; i++)
        {
            float ang = i / 8f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.2f;
            float rad = 13f + (float)rng.NextDouble() * 4f;
            Vector3 p = oasisCenter + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
            p.y = land.SampleHeight(p) + origin.y + 0.1f;
            CreateFrog(frogRoot.transform, p, frogMat, eyeMat, rng);
        }

        // 2. カルデラ池の岩場・水草周辺（14匹）
        Vector3 lakeCenter = new Vector3(420f, 0f, 440f);
        for (int i = 0; i < 14; i++)
        {
            float ang = i / 14f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.2f;
            float rad = 32f + (float)rng.NextDouble() * 8f;
            Vector3 p = lakeCenter + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
            p.y = land.SampleHeight(p) + origin.y + 0.1f;
            CreateFrog(frogRoot.transform, p, frogMat, eyeMat, rng);
        }

        // 3. 草原池周辺（6匹）
        Vector3 pondCenter = new Vector3(290f, 0f, 320f);
        for (int i = 0; i < 6; i++)
        {
            float ang = i / 6f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.2f;
            float rad = 13f + (float)rng.NextDouble() * 4f;
            Vector3 p = pondCenter + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
            p.y = land.SampleHeight(p) + origin.y + 0.1f;
            CreateFrog(frogRoot.transform, p, frogMat, eyeMat, rng);
        }

        // 4. 小川（420→160）の両岸・飛び石周辺（8匹）
        for (int i = 0; i < 8; i++)
        {
            float t = (i + 0.5f) / 8f;
            float wx = Mathf.Lerp(410f, 175f, t) + (float)(rng.NextDouble() * 12 - 6);
            float wz = Mathf.Lerp(430f, 190f, t) + Mathf.Sin(wx * 0.025f) * 22f + (float)(rng.NextDouble() * 10 - 5);
            Vector3 p = new Vector3(wx, 0f, wz);
            p.y = land.SampleHeight(p) + origin.y + 0.1f;
            CreateFrog(frogRoot.transform, p, frogMat, eyeMat, rng);
        }

        // ── 状況に応じた水辺の音響（虫の声フォルダより） ──
        // A. カエルの大合唱（大カルデラ湖 4箇所、草原池 1箇所）
        var frogChorusClip = LoadAudioByGuid("8dc5cef668d894bdfb296b86aae333a0"); // カエルの大合唱.mp3
        if (frogChorusClip != null)
        {
            Vector3[] chorusSpots =
            {
                new Vector3(420f, 25.6f, 440f), // カルデラ湖中心
                new Vector3(395f, 25.6f, 435f), // カルデラ湖西岸
                new Vector3(445f, 25.6f, 445f), // カルデラ湖東岸
                new Vector3(420f, 25.6f, 465f), // カルデラ湖北岸
                new Vector3(290f, 14.6f, 320f), // 大草原池
            };
            for (int i = 0; i < chorusSpots.Length; i++)
            {
                var sGo = new GameObject("FrogChorus_" + i);
                sGo.transform.SetParent(frogRoot.transform, false);
                sGo.transform.position = chorusSpots[i];
                var src = sGo.AddComponent<AudioSource>();
                src.clip = frogChorusClip;
                src.loop = true;
                src.playOnAwake = true;
                src.spatialBlend = 1.0f;
                src.minDistance = 7f;
                src.maxDistance = 45f;
                src.volume = 0.70f;
                src.rolloffMode = AudioRolloffMode.Linear;
            }
        }

        // B. カジカガエルの澄んだ美声（オアシス湧水池 2箇所、小川の飛び石 2箇所）
        var kajikaClip = LoadAudioByGuid("6e4321ae4e14748bea428b93e3fdfcc9"); // 01カジカガエル.3.aif
        if (kajikaClip != null)
        {
            Vector3[] kajikaSpots =
            {
                new Vector3(480f, 48.3f, 455f), // オアシス湧水池
                new Vector3(460f, 38.0f, 448f), // 上流急流の岩場
                new Vector3(340f, 19.5f, 365f), // 本流上流飛び石
                new Vector3(230f, 10.0f, 245f), // 本流下流飛び石
            };
            for (int i = 0; i < kajikaSpots.Length; i++)
            {
                var sGo = new GameObject("KajikaFrog_" + i);
                sGo.transform.SetParent(frogRoot.transform, false);
                sGo.transform.position = kajikaSpots[i];
                var src = sGo.AddComponent<AudioSource>();
                src.clip = kajikaClip;
                src.loop = true;
                src.playOnAwake = true;
                src.spatialBlend = 1.0f;
                src.minDistance = 5f;
                src.maxDistance = 35f;
                src.volume = 0.75f;
                src.rolloffMode = AudioRolloffMode.Linear;
            }
        }

        // C. 渓流のせせらぎ音（上流急流、本流小川）
        var streamClip = LoadAudioByGuid("c0a1ddb8b5fb84692b7a28e5b0df3d12"); // 渓流.mp3
        if (streamClip != null)
        {
            Vector3[] streamSpots =
            {
                new Vector3(465f, 40.0f, 450f), // 上流急流
                new Vector3(360f, 21.0f, 380f), // 本流中流
                new Vector3(250f, 11.5f, 270f), // 本流下流
                new Vector3(180f, 6.5f, 200f)   // 河口
            };
            for (int i = 0; i < streamSpots.Length; i++)
            {
                var sGo = new GameObject("StreamAudio_" + i);
                sGo.transform.SetParent(frogRoot.transform, false);
                sGo.transform.position = streamSpots[i];
                var src = sGo.AddComponent<AudioSource>();
                src.clip = streamClip;
                src.loop = true;
                src.playOnAwake = true;
                src.spatialBlend = 1.0f;
                src.minDistance = 8f;
                src.maxDistance = 36f;
                src.volume = 0.25f;
                src.rolloffMode = AudioRolloffMode.Linear;
            }
        }
    }

    static void CreateFrog(Transform parent, Vector3 pos, Material bodyMat, Material eyeMat, System.Random rng)
    {
        var frog = new GameObject("Frog");
        frog.transform.SetParent(parent, false);
        frog.transform.position = pos;
        frog.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

        // 胴体
        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "Body";
        body.transform.SetParent(frog.transform, false);
        body.transform.localScale = new Vector3(0.32f, 0.22f, 0.38f);
        body.transform.localPosition = new Vector3(0f, 0.11f, 0f);
        body.GetComponent<Renderer>().sharedMaterial = bodyMat;
        Object.DestroyImmediate(body.GetComponent<Collider>());

        // 左右の目
        for (int side = -1; side <= 1; side += 2)
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "Eye";
            eye.transform.SetParent(body.transform, false);
            eye.transform.localScale = new Vector3(0.32f, 0.38f, 0.32f);
            eye.transform.localPosition = new Vector3(side * 0.38f, 0.45f, 0.25f);
            eye.GetComponent<Renderer>().sharedMaterial = eyeMat;
            Object.DestroyImmediate(eye.GetComponent<Collider>());
        }

        // 跳ねるアニメーションスクリプト
        frog.AddComponent<FrogHop>();
        float s = 0.8f + (float)rng.NextDouble() * 0.45f;
        frog.transform.localScale = Vector3.one * s;
    }

    /// <summary>白砂ビーチにカサカサ横歩きするカニを配置（35匹）</summary>
    static void PlaceCrabs(GameObject root, Terrain land, System.Random rng, float water)
    {
        var crabRoot = new GameObject("Crabs");
        crabRoot.transform.SetParent(root.transform, false);

        var crabMat = MakeColorMat(new Color(0.88f, 0.28f, 0.18f), 0.15f, 0.5f); // 鮮やかな赤橙
        var eyeMat = MakeColorMat(Color.black, 0.2f, 0.8f);

        Vector3 origin = land.transform.position;
        Vector3 size = land.terrainData.size;
        Vector3 center = origin + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        float islandR = size.x * 0.44f; // ビーチゾーン

        int placed = 0;
        for (int i = 0; i < 200 && placed < 35; i++)
        {
            float ang = (float)(i / 35.0 * Mathf.PI * 2.0 + rng.NextDouble() * 0.15);
            float rad = islandR - 15f + (float)rng.NextDouble() * 30f;
            Vector3 p = center + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
            p.y = land.SampleHeight(p) + origin.y;

            // 砂浜の高さ（6.0m〜7.8m）
            if (p.y < water + 0.4f || p.y > water + 2.8f) continue;

            CreateCrab(crabRoot.transform, p, crabMat, eyeMat, rng);
            placed++;
        }
    }

    static void CreateCrab(Transform parent, Vector3 pos, Material shellMat, Material eyeMat, System.Random rng)
    {
        var crab = new GameObject("Crab");
        crab.transform.SetParent(parent, false);
        crab.transform.position = pos;
        crab.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

        // 甲羅（平たい楕円球）
        var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shell.name = "Shell";
        shell.transform.SetParent(crab.transform, false);
        shell.transform.localScale = new Vector3(0.42f, 0.16f, 0.32f);
        shell.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        shell.GetComponent<Renderer>().sharedMaterial = shellMat;
        Object.DestroyImmediate(shell.GetComponent<Collider>());

        // 左右のハサミ
        for (int side = -1; side <= 1; side += 2)
        {
            var claw = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            claw.name = side < 0 ? "LeftClaw" : "RightClaw";
            claw.transform.SetParent(crab.transform, false);
            claw.transform.localScale = new Vector3(0.18f, 0.12f, 0.22f);
            claw.transform.localPosition = new Vector3(side * 0.32f, 0.11f, 0.24f);
            claw.GetComponent<Renderer>().sharedMaterial = shellMat;
            Object.DestroyImmediate(claw.GetComponent<Collider>());
        }

        // 左右の目
        for (int side = -1; side <= 1; side += 2)
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "Eye";
            eye.transform.SetParent(shell.transform, false);
            eye.transform.localScale = new Vector3(0.18f, 0.45f, 0.18f);
            eye.transform.localPosition = new Vector3(side * 0.28f, 0.5f, 0.32f);
            eye.GetComponent<Renderer>().sharedMaterial = eyeMat;
            Object.DestroyImmediate(eye.GetComponent<Collider>());
        }

        // カニの横歩きスクリプト
        crab.AddComponent<CrabWander>();
        float s = 0.85f + (float)rng.NextDouble() * 0.4f;
        crab.transform.localScale = Vector3.one * s;
    }

    /// <summary>小川・3つの池・大草原の上をホバリング飛行するトンボを配置（28匹）</summary>
    static void PlaceDragonflies(GameObject root, Terrain land, System.Random rng)
    {
        var dfRoot = new GameObject("Dragonflies");
        dfRoot.transform.SetParent(root.transform, false);

        var redMat = MakeColorMat(new Color(0.85f, 0.2f, 0.15f), 0.1f, 0.6f); // 赤とんぼ
        var blueMat = MakeColorMat(new Color(0.2f, 0.6f, 0.95f), 0.1f, 0.6f); // イトトンボ・シオカラ
        var wingMat = MakeTransparentMat(new Color(0.9f, 0.95f, 1f, 0.45f));

        Vector3 origin = land.transform.position;

        // 1. オアシス湧水池の上（6匹）
        Vector3 oasisCenter = new Vector3(480f, 0f, 455f);
        for (int i = 0; i < 6; i++)
        {
            float ang = i / 6f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.2f;
            float rad = 5f + (float)rng.NextDouble() * 10f;
            Vector3 p = oasisCenter + new Vector3(Mathf.Cos(ang) * rad, 49.2f + (float)rng.NextDouble() * 1.2f, Mathf.Sin(ang) * rad);
            CreateDragonfly(dfRoot.transform, p, (i % 2 == 0) ? redMat : blueMat, wingMat, rng);
        }

        // 2. カルデラ池の上（8匹）
        Vector3 lakeCenter = new Vector3(420f, 0f, 440f);
        for (int i = 0; i < 8; i++)
        {
            float ang = i / 8f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.2f;
            float rad = 15f + (float)rng.NextDouble() * 20f;
            Vector3 p = lakeCenter + new Vector3(Mathf.Cos(ang) * rad, 26.5f + (float)rng.NextDouble() * 1.5f, Mathf.Sin(ang) * rad);
            CreateDragonfly(dfRoot.transform, p, (i % 2 == 0) ? redMat : blueMat, wingMat, rng);
        }

        // 3. 草原池の上（6匹）
        Vector3 pondCenter = new Vector3(290f, 0f, 320f);
        for (int i = 0; i < 6; i++)
        {
            float ang = i / 6f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.2f;
            float rad = 5f + (float)rng.NextDouble() * 10f;
            Vector3 p = pondCenter + new Vector3(Mathf.Cos(ang) * rad, 15.2f + (float)rng.NextDouble() * 1.2f, Mathf.Sin(ang) * rad);
            CreateDragonfly(dfRoot.transform, p, blueMat, wingMat, rng);
        }

        // 4. 小川の上（8匹）
        for (int i = 0; i < 8; i++)
        {
            float t = (i + 0.5f) / 8f;
            float wx = Mathf.Lerp(410f, 180f, t);
            float wz = Mathf.Lerp(430f, 190f, t) + Mathf.Sin(wx * 0.025f) * 22f;
            Vector3 p = new Vector3(wx, land.SampleHeight(new Vector3(wx, 0f, wz)) + origin.y + 1.2f, wz);
            CreateDragonfly(dfRoot.transform, p, redMat, wingMat, rng);
        }
    }

    static void CreateDragonfly(Transform parent, Vector3 pos, Material bodyMat, Material wingMat, System.Random rng)
    {
        var df = new GameObject("Dragonfly");
        df.transform.SetParent(parent, false);
        df.transform.position = pos;
        df.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

        // 細長い胴体
        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Body";
        body.transform.SetParent(df.transform, false);
        body.transform.localScale = new Vector3(0.06f, 0.28f, 0.06f);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.GetComponent<Renderer>().sharedMaterial = bodyMat;
        Object.DestroyImmediate(body.GetComponent<Collider>());

        // 翅（Wings）
        var wings = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wings.name = "Wings";
        wings.transform.SetParent(df.transform, false);
        wings.transform.localScale = new Vector3(0.72f, 0.015f, 0.16f);
        wings.transform.localPosition = new Vector3(0f, 0.04f, 0.12f);
        wings.GetComponent<Renderer>().sharedMaterial = wingMat;
        Object.DestroyImmediate(wings.GetComponent<Collider>());

        df.AddComponent<DragonflyFlight>();
        df.transform.localScale = Vector3.one * (0.8f + (float)rng.NextDouble() * 0.35f);
    }

    /// <summary>濃い緑の木が超密集した3箇所の原生林・木立と、そこに降り注ぐ蝉時雨（3D音響）＆木の幹に止まる蝉を配置</summary>
    static void PlaceDenseDarkGreenForestsAndCicadas(GameObject root, Terrain land, System.Random rng)
    {
        var forestRoot = new GameObject("DenseDarkGreenForests");
        forestRoot.transform.SetParent(root.transform, false);

        var cicadaRoot = new GameObject("Cicadas");
        cicadaRoot.transform.SetParent(forestRoot.transform, false);

        Vector3 origin = land.transform.position;

        // 状況に合わせた蝉・山林の音響アセット（Assets/虫の声 フォルダより）
        var minminForestClip = LoadAudioByGuid("4b23982718f724f16a272255800a0627"); // ミンミンゼミが鳴く雑木林.mp3
        var minminSoloClip   = LoadAudioByGuid("c601104b5268144caa388626af6ca859"); // ミンミンゼミの鳴き声.mp3
        var abura1Clip       = LoadAudioByGuid("34410a54164814bf8a8f4a7d77860ef8"); // アブラゼミの鳴き声1.mp3
        var abura2Clip       = LoadAudioByGuid("3d0cca8c1c72e4fffad939870946572f"); // アブラゼミの鳴き声2.mp3
        var tsukutsuku1Clip  = LoadAudioByGuid("04414348975ce42ff8a09f8cd0456553"); // ツクツクボウシの鳴き声1.mp3
        var tsukutsuku2Clip  = LoadAudioByGuid("84878c2a08660415d9a04f37a3afd3a3"); // ツクツクボウシの鳴き声2.mp3
        var niiniiClip       = LoadAudioByGuid("b776ba0223f7e4804a05983f635e4111"); // ニイニイゼミの鳴き声.mp3
        var higurashiClip    = LoadAudioByGuid("3eab796eb867c4ba4bedd1683b7d1b0e"); // ヒグラシの鳴き声.mp3
        var summerMtn1Clip   = LoadAudioByGuid("afb9961f99a1c4352b1123411781dbf1"); // 夏の山1.mp3
        var summerMtn2Clip   = LoadAudioByGuid("e1006d715f47447a69812e93d31ce7bf"); // 夏の山2.mp3
        var summerCountryClip = LoadAudioByGuid("8c374d87fd6ad45eaa8a6a3ef43bad28"); // 夏の田舎道.mp3
        var cricketClip      = LoadAudioByGuid("9113c8ddf15ff4ca288de8176160dbb9"); // エンマコオロギの鳴き声.mp3

        // 濃い緑の木が密集する8大森林・木立ゾーン（島全体を包み込む壮大な蝉時雨）
        // 1. 東丘陵の深緑原生林 (ミンミンゼミとアブラゼミの大合唱)
        // 2. 北崖奥の深緑古樹林 (夏の山の深緑にニイニイゼミ・アブラゼミが鳴く)
        // 3. カルデラ湖東岸の深緑木立 (水辺の木陰にヒグラシとツクツクボウシ)
        // 4. 東部樹海南側スロープ (アブラゼミとツクツクボウシの盛夏大樹海)
        // 5. 北東樹海高地 (ミンミンゼミとニイニイゼミの梢)
        // 6. 東の深林大渓流沿い (水音と涼やかなヒグラシ・ミンミンゼミの共演)
        // 7. 中央台地足元・オアシス湧水池の木立 (ツクツクボウシとヒグラシ)
        // 8. 草原〜湖畔アプローチ木立 (スタート地点近くから心地よく聞こえる夏の蝉)
        // 9. スタート地点・せせらぎ池西岸木立 (足元から聞こえるツクツクボウシとヒグラシ)
        // 10. 西側ビーチテラス・海辺木立 (波音と調和するミンミンゼミとヒグラシ)
        // 11. 南西草原・小川沿い木立 (せせらぎとアブラゼミ・夏の田舎道)
        (Vector3 center, float radius, int treeCount, int cicadaCount, AudioClip forestAmbience, AudioClip[] treeClips)[] groves =
        {
            (new Vector3(680f, 0f, 530f), 75f, 130, 12, minminForestClip, new[] { minminSoloClip, abura1Clip, abura2Clip }),
            (new Vector3(490f, 0f, 680f), 65f, 100, 10, summerMtn1Clip ?? summerMtn2Clip, new[] { niiniiClip, abura1Clip, summerMtn2Clip }),
            (new Vector3(440f, 0f, 480f), 55f, 80, 8, higurashiClip, new[] { tsukutsuku1Clip, tsukutsuku2Clip, higurashiClip }),
            (new Vector3(720f, 0f, 380f), 70f, 110, 10, minminForestClip ?? summerMtn2Clip, new[] { abura1Clip, abura2Clip, tsukutsuku1Clip }),
            (new Vector3(620f, 0f, 640f), 65f, 90, 8, minminForestClip, new[] { minminSoloClip, niiniiClip, abura1Clip }),
            (new Vector3(580f, 0f, 460f), 55f, 80, 8, higurashiClip, new[] { higurashiClip, minminSoloClip, tsukutsuku2Clip }),
            (new Vector3(480f, 0f, 430f), 45f, 60, 7, summerMtn1Clip, new[] { tsukutsuku1Clip, tsukutsuku2Clip, higurashiClip }),
            (new Vector3(360f, 0f, 380f), 50f, 70, 7, minminForestClip, new[] { minminSoloClip, abura1Clip, higurashiClip }),
            (new Vector3(250f, 0f, 340f), 35f, 35, 6, higurashiClip, new[] { tsukutsuku1Clip, higurashiClip, minminSoloClip }),
            (new Vector3(190f, 0f, 290f), 40f, 40, 6, minminForestClip, new[] { minminSoloClip, higurashiClip, abura1Clip }),
            (new Vector3(280f, 0f, 220f), 40f, 40, 6, summerCountryClip ?? summerMtn1Clip, new[] { abura1Clip, tsukutsuku2Clip, cricketClip ?? higurashiClip })
        };

        // 蝉モデル用マテリアル
        var bodyMat = MakeColorMat(new Color(0.12f, 0.10f, 0.08f), 0.4f, 0.7f); // 光沢のある黒褐色
        var eyeMat = MakeColorMat(new Color(0.45f, 0.12f, 0.08f), 0.1f, 0.8f);  // 赤褐色複眼
        var wingMat = MakeTransparentMat(new Color(0.70f, 0.65f, 0.45f, 0.45f)); // 半透明の琥珀色の翅

        int totalCicadas = 0;
        int totalAudio = 0;

        for (int g = 0; g < groves.Length; g++)
        {
            var grove = groves[g];
            var groveGo = new GameObject("DenseGrove_" + (g + 1));
            groveGo.transform.SetParent(forestRoot.transform, false);

            var treePositions = new System.Collections.Generic.List<Vector3>();

            // 1. 濃い緑の木（Firや深緑広葉樹）を超高密度配置（木と木の距離約2.0〜3.5m）
            int placedTrees = 0;
            for (int attempt = 0; attempt < grove.treeCount * 3 && placedTrees < grove.treeCount; attempt++)
            {
                float r = (float)(Mathf.Sqrt((float)rng.NextDouble()) * grove.radius);
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                Vector3 p = grove.center + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                p.y = land.SampleHeight(p) + origin.y;

                if (p.y < 7.0f) continue;

                // 他の木と極端に重ならないように（最小1.8m間隔）
                bool tooClose = false;
                for (int t = 0; t < treePositions.Count; t++)
                {
                    if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(treePositions[t].x, treePositions[t].z)) < 1.8f)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                treePositions.Add(p);
                string prefabPath = DenseDarkGreenTrees[rng.Next(DenseDarkGreenTrees.Length)];
                float scale = (float)(1.1 + rng.NextDouble() * 0.7); // 1.1〜1.8倍の雄大な深緑の木
                Place(groveGo, prefabPath, p, rng, scale, scale);
                placedTrees++;

                // 木の足元に濃い緑の灌木（下草）を配置
                if (rng.NextDouble() < 0.25)
                {
                    Vector3 bushP = p + new Vector3((float)(rng.NextDouble() * 2.0 - 1.0), 0f, (float)(rng.NextDouble() * 2.0 - 1.0));
                    bushP.y = land.SampleHeight(bushP) + origin.y;
                    Place(groveGo, Bushes[rng.Next(Bushes.Length)], bushP, rng, 0.8f, 1.3f);
                }
            }

            // 2. 森林全体の広域アンビエンス音（中心部に配置）
            if (grove.forestAmbience != null)
            {
                var ambGo = new GameObject("ForestAmbience_G" + (g + 1));
                ambGo.transform.SetParent(cicadaRoot.transform, false);
                Vector3 ambP = grove.center;
                ambP.y = land.SampleHeight(ambP) + origin.y + 7.0f;
                ambGo.transform.position = ambP;

                var src = ambGo.AddComponent<AudioSource>();
                src.clip = grove.forestAmbience;
                src.loop = true;
                src.playOnAwake = true;
                src.spatialBlend = 1.0f;
                src.minDistance = 15.0f;
                src.maxDistance = grove.radius * 1.8f;
                src.volume = 0.75f;
                src.rolloffMode = AudioRolloffMode.Linear;
                totalAudio++;
            }

            // 3. 梢から降り注ぐ個々の蝉の鳴き声（3D立体音響 5〜8箇所）
            var validTreeClips = grove.treeClips?.Where(c => c != null).ToArray();
            if (validTreeClips != null && validTreeClips.Length > 0)
            {
                int audioCount = Mathf.Max(5, grove.cicadaCount / 2 + 2);
                for (int a = 0; a < audioCount; a++)
                {
                    Vector3 ap = grove.center + (a == 0 ? Vector3.zero :
                        new Vector3((float)(rng.NextDouble() * grove.radius * 1.3 - grove.radius * 0.65), 0f,
                                    (float)(rng.NextDouble() * grove.radius * 1.3 - grove.radius * 0.65)));
                    ap.y = land.SampleHeight(ap) + origin.y + 5.5f;

                    var sndGo = new GameObject("CicadaSound_G" + (g + 1) + "_" + a);
                    sndGo.transform.SetParent(cicadaRoot.transform, false);
                    sndGo.transform.position = ap;

                    var src = sndGo.AddComponent<AudioSource>();
                    src.clip = validTreeClips[rng.Next(validTreeClips.Length)];
                    src.loop = true;
                    src.playOnAwake = true;
                    src.spatialBlend = 1.0f; // 3D音響
                    src.minDistance = 6.0f;
                    src.maxDistance = 45.0f;
                    src.volume = 0.85f;
                    src.rolloffMode = AudioRolloffMode.Linear;
                    totalAudio++;
                }
            }

            // 4. 木の幹にピタッと止まる蝉の3Dモデル（各密集林に指定数）
            int cicadasPlaced = 0;
            for (int i = 0; i < treePositions.Count && cicadasPlaced < grove.cicadaCount; i++)
            {
                if (rng.NextDouble() > 0.40) continue;

                Vector3 treeP = treePositions[i];
                float trunkHeight = 1.8f + (float)rng.NextDouble() * 1.6f; // 地上1.8m〜3.4m（見つけやすい高さ）
                float trunkAngle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float trunkRadius = 0.35f + (float)rng.NextDouble() * 0.15f; // 木の幹の表面
                Vector3 cPos = treeP + new Vector3(Mathf.Cos(trunkAngle) * trunkRadius, trunkHeight, Mathf.Sin(trunkAngle) * trunkRadius);

                // 木の幹の外側を向き、頭を上に向ける回転
                Vector3 outward = (cPos - (treeP + Vector3.up * trunkHeight)).normalized;
                Quaternion cRot = Quaternion.LookRotation(outward, Vector3.up);

                CreateCicadaModel(cicadaRoot, cPos, cRot, bodyMat, eyeMat, wingMat, rng);
                cicadasPlaced++;
                totalCicadas++;
            }
        }

        Debug.Log($"[RustAndFloat] 濃い緑の木の森林・蝉ゾーン8箇所を生成しました。木={forestRoot.GetComponentsInChildren<Transform>().Length}本, 蝉={totalCicadas}匹, 蝉音響={totalAudio}箇所");
    }

    /// <summary>広大な大草原の草むら・花畑にエンマコオロギの鳴き声と、夏の田舎道アンビエンスを配置</summary>
    static void PlaceMeadowInsectsAndAmbience(GameObject root, Terrain land, System.Random rng)
    {
        var meadowRoot = new GameObject("MeadowInsects");
        meadowRoot.transform.SetParent(root.transform, false);

        Vector3 origin = land.transform.position;
        var cricketClip = LoadAudioByGuid("9113c8ddf15ff4ca288de8176160dbb9"); // エンマコオロギの鳴き声.mp3
        var countryRoadClip = LoadAudioByGuid("8c374d87fd6ad45eaa8a6a3ef43bad28"); // 夏の田舎道.mp3

        // 1. エンマコオロギ（草むらの中からコロコロと心地よく響く 18箇所）
        if (cricketClip != null)
        {
            Vector3[] cricketSpots =
            {
                new Vector3(220f, 0f, 260f),
                new Vector3(250f, 0f, 380f),
                new Vector3(310f, 0f, 230f),
                new Vector3(340f, 0f, 340f),
                new Vector3(280f, 0f, 440f),
                new Vector3(230f, 0f, 500f),
                new Vector3(350f, 0f, 560f),
                new Vector3(410f, 0f, 330f),
                new Vector3(450f, 0f, 370f),
                new Vector3(380f, 0f, 260f),
                new Vector3(200f, 0f, 420f),
                new Vector3(260f, 0f, 580f),
                new Vector3(490f, 0f, 360f),
                new Vector3(530f, 0f, 420f),
                new Vector3(600f, 0f, 380f),
                new Vector3(640f, 0f, 450f),
                new Vector3(570f, 0f, 620f),
                new Vector3(430f, 0f, 600f)
            };
            for (int i = 0; i < cricketSpots.Length; i++)
            {
                Vector3 p = cricketSpots[i];
                p.y = land.SampleHeight(p) + origin.y + 0.35f; // 草の根元付近

                var sGo = new GameObject("Cricket_" + i);
                sGo.transform.SetParent(meadowRoot.transform, false);
                sGo.transform.position = p;

                var src = sGo.AddComponent<AudioSource>();
                src.clip = cricketClip;
                src.loop = true;
                src.playOnAwake = true;
                src.spatialBlend = 1.0f; // 3D音響
                src.minDistance = 3.5f;
                src.maxDistance = 22.0f;
                src.volume = 0.60f;
                src.rolloffMode = AudioRolloffMode.Linear;
            }
        }

        // 2. 夏の田舎道（大草原を渡る風と虫の気配 5箇所）
        if (countryRoadClip != null)
        {
            Vector3[] roadSpots =
            {
                new Vector3(280f, 0f, 300f),
                new Vector3(360f, 0f, 360f),
                new Vector3(440f, 0f, 420f),
                new Vector3(320f, 0f, 480f),
                new Vector3(250f, 0f, 350f)
            };
            for (int i = 0; i < roadSpots.Length; i++)
            {
                Vector3 p = roadSpots[i];
                p.y = land.SampleHeight(p) + origin.y + 2.0f;

                var sGo = new GameObject("CountryRoadAmbience_" + i);
                sGo.transform.SetParent(meadowRoot.transform, false);
                sGo.transform.position = p;

                var src = sGo.AddComponent<AudioSource>();
                src.clip = countryRoadClip;
                src.loop = true;
                src.playOnAwake = true;
                src.spatialBlend = 1.0f;
                src.minDistance = 6.0f;
                src.maxDistance = 25.0f;
                src.volume = 0.10f; // 風擦過音を大幅に抑える
                src.rolloffMode = AudioRolloffMode.Linear;
            }
        }
    }

    /// <summary>白砂ビーチ沿い（外周約1000m、海抜5.5m〜8.5m）に寄せては返すリアルな波の音（3D立体音響 24箇所）を配置</summary>
    static void PlaceBeachWavesGrand(GameObject root, Terrain land, System.Random rng, float water)
    {
        var waveRoot = new GameObject("BeachWaves");
        waveRoot.transform.SetParent(root.transform, false);

        var td = land.terrainData;
        Vector3 origin = land.transform.position;
        Vector3 center = origin + new Vector3(td.size.x * 0.5f, 0f, td.size.z * 0.5f);
        float islandR = td.size.x * 0.44f; // 半径約450mの海岸線

        var waveClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RustAndFloat/Audio/Ambience/ocean_waves_grand.wav");
        if (waveClip == null)
            waveClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/AudioFiles/03_amb/watershore_amb.wav");
        if (waveClip == null) return;

        // 島の外周白砂ビーチに沿って均等に24箇所の3D波音源を配置
        const int waveCount = 24;
        for (int i = 0; i < waveCount; i++)
        {
            float ang = (float)(i / (double)waveCount * Mathf.PI * 2.0);
            // 砂浜の波打ち際（半径440m〜455m）
            float r = islandR + (float)(rng.NextDouble() * 10.0 - 5.0);
            Vector3 p = center + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
            p.y = water + 0.35f; // 波打ち際の水面高さ（約5.85m）

            var waveGo = new GameObject("BeachWave_" + i);
            waveGo.transform.SetParent(waveRoot.transform, false);
            waveGo.transform.position = p;

            var src = waveGo.AddComponent<AudioSource>();
            src.clip = waveClip;
            src.loop = true;
            src.playOnAwake = true;
            src.spatialBlend = 1.0f; // リアルな3Dサラウンド
            src.minDistance = 14.0f;
            src.maxDistance = 55.0f; // 砂浜に近づくと心地よく波音が包み込み、内陸には響かない
            src.volume = 0.68f;
            src.rolloffMode = AudioRolloffMode.Linear;

            // 各音源ごとに再生時間をずらし、寄せては引く波のリズムを自然にブレンド
            if (waveClip.length > 1f)
            {
                src.time = (float)(rng.NextDouble() * (waveClip.length - 0.5f));
            }
        }

        Debug.Log($"[RustAndFloat] 砂浜の波打ち際に波の音（3D AudioSource）{waveCount}箇所を配置しました。");
    }

    /// <summary>GUIDから確実にAudioClipをロードするヘルパー（macOSの日本語濁点NFC/NFD差異を回避）</summary>
    static AudioClip LoadAudioByGuid(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    /// <summary>木の幹に止まるリアルな蝉の3Dモデルを生成</summary>
    static void CreateCicadaModel(GameObject parent, Vector3 pos, Quaternion rot,
        Material bodyMat, Material eyeMat, Material wingMat, System.Random rng)
    {
        var cicadaGo = new GameObject("Cicada");
        cicadaGo.transform.SetParent(parent.transform, false);
        cicadaGo.transform.SetPositionAndRotation(pos, rot);

        // 胴体（胸部・腹部）
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(cicadaGo.transform, false);
        body.transform.localPosition = new Vector3(0f, 0f, 0.02f);
        body.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
        body.transform.localScale = new Vector3(0.05f, 0.11f, 0.045f);
        body.GetComponent<Renderer>().sharedMaterial = bodyMat;
        Object.DestroyImmediate(body.GetComponent<Collider>());

        // 頭部
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(cicadaGo.transform, false);
        head.transform.localPosition = new Vector3(0f, 0.09f, 0.025f);
        head.transform.localScale = new Vector3(0.052f, 0.038f, 0.045f);
        head.GetComponent<Renderer>().sharedMaterial = bodyMat;
        Object.DestroyImmediate(head.GetComponent<Collider>());

        // 複眼（左右）
        for (int side = -1; side <= 1; side += 2)
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = side < 0 ? "LeftEye" : "RightEye";
            eye.transform.SetParent(head.transform, false);
            eye.transform.localPosition = new Vector3(side * 0.45f, 0.15f, 0.2f);
            eye.transform.localScale = new Vector3(0.38f, 0.38f, 0.38f);
            eye.GetComponent<Renderer>().sharedMaterial = eyeMat;
            Object.DestroyImmediate(eye.GetComponent<Collider>());
        }

        // 背中にたたまれた半透明の翅（Wings）
        var wings = GameObject.CreatePrimitive(PrimitiveType.Quad);
        wings.name = "Wings";
        wings.transform.SetParent(cicadaGo.transform, false);
        wings.transform.localPosition = new Vector3(0f, 0.01f, 0.048f);
        wings.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
        wings.transform.localScale = new Vector3(0.075f, 0.15f, 1f);
        wings.GetComponent<Renderer>().sharedMaterial = wingMat;
        Object.DestroyImmediate(wings.GetComponent<Collider>());

        // アニメーションコンポーネント（翅の微振動と呼吸）
        cicadaGo.AddComponent<AdventureCicadaVibrate>();
        float scale = 1.0f + (float)(rng.NextDouble() * 0.35 - 0.15); // 約0.85〜1.2倍
        cicadaGo.transform.localScale = Vector3.one * scale;
    }

    static Material MakeColorMat(Color color, float metallic, float smoothness)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }

    static Material MakeTransparentMat(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);
        mat.SetFloat("_Surface", 1); // Transparent
        mat.SetFloat("_Blend", 0);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return mat;
    }
}
#endif
