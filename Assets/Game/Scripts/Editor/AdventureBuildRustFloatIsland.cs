using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;

public static class AdventureBuildRustFloatIsland
{
    const string ScenePath = "Assets/RustAndFloat/Scenes/RustAndFloat.unity";
    const string TerrainPath = "Assets/RustAndFloat/Terrain/IslandTerrain.asset";
    const string NikoPath = "Assets/Niko&Capyta/Assets/Prefabs/Niko.prefab";

    [MenuItem("Adventure/🏝️ Build 1000m Grand Sanctuary Island")]
    public static void BuildGrandFromMenu()
    {
        BuildGrandIsland();
    }

    [MenuItem("Adventure/Build RustAndFloat Island (256m)")]
    public static void BuildFromMenu()
    {
        Build();
    }

    [MenuItem("Adventure/Shape RustAndFloat North Cliff")]
    public static void ShapeNorthCliffFromMenu()
    {
        ShapeNorthCliff();
    }

    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("停止してください", "■で再生を止めてから実行してください。", "OK");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var td = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
        if (td == null)
        {
            Debug.LogError("[RustAndFloat] IslandTerrain がありません");
            return;
        }

        int res = 513;
        td.heightmapResolution = res;
        td.size = new Vector3(256f, 48f, 256f);

        float[,] h = new float[res, res];
        float cx = (res - 1) * 0.5f;
        float islandR = (res - 1) * 0.40f;
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float nx = x / (float)(res - 1);
                float nz = z / (float)(res - 1);
                float dx = (x - cx) / islandR;
                float dz = (z - cx) / islandR;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                float n1 = Mathf.PerlinNoise(x * 0.022f, z * 0.022f);
                float n2 = Mathf.PerlinNoise(x * 0.07f + 30f, z * 0.07f);

                float height;
                if (d > 1.10f)
                    height = 0.05f;
                else if (d > 0.92f)
                    height = Mathf.Lerp(0.155f, 0.05f, (d - 0.92f) / 0.18f);
                else if (d > 0.78f)
                    height = Mathf.Lerp(0.22f, 0.155f, (d - 0.78f) / 0.14f);
                else
                    height = 0.22f + n1 * 0.10f;

                float hillA = Mathf.Exp(-(Mathf.Pow((nx - 0.38f) / 0.10f, 2f) + Mathf.Pow((nz - 0.48f) / 0.10f, 2f)));
                float hillB = Mathf.Exp(-(Mathf.Pow((nx - 0.62f) / 0.09f, 2f) + Mathf.Pow((nz - 0.42f) / 0.11f, 2f)));
                if (d < 0.86f)
                    height = Mathf.Max(height, 0.24f + hillA * 0.22f, 0.23f + hillB * 0.18f);

                float ridge = Mathf.Exp(-Mathf.Pow((nx - 0.58f) / 0.085f, 2f));
                float along = Mathf.InverseLerp(0.34f, 0.74f, nz);
                if (d < 0.90f && along > 0f)
                {
                    float terrace = Mathf.Floor(along * 4f) / 4f;
                    float ramp = Mathf.Lerp(0.24f, 0.72f, terrace);
                    height = Mathf.Max(height, ramp * ridge + (1f - ridge) * height);
                }

                float cliffDx = (nx - 0.54f) / 0.16f;
                float cliffDz = (nz - 0.73f) / 0.13f;
                float cliffD = Mathf.Sqrt(cliffDx * cliffDx + cliffDz * cliffDz);
                if (cliffD < 1f && d < 0.94f)
                {
                    float peak = (1f - cliffD) * (1f - cliffD);
                    height = Mathf.Max(height, 0.28f + peak * 0.52f);
                }

                if (nz > 0.76f && nx > 0.40f && nx < 0.70f && d < 1.05f)
                {
                    float drop = Mathf.InverseLerp(0.76f, 0.88f, nz);
                    height = Mathf.Lerp(height, 0.05f, drop * drop);
                }

                height += (n2 - 0.5f) * 0.01f;
                h[z, x] = Mathf.Clamp01(height);
            }
        }
        td.SetHeights(0, 0, h);
        EditorUtility.SetDirty(td);

        var land = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude).FirstOrDefault();
        if (land == null)
        {
            Debug.LogError("[RustAndFloat] Terrain オブジェクトがありません");
            return;
        }

        land.name = "LandTerrain";
        land.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        land.terrainData = td;
        land.Flush();
        PaintLayers(td, land);
        PaintGrassDetails(td, land);

        var water = GameObject.Find("OceanPlane");
        if (water != null)
        {
            water.transform.position = new Vector3(128f, 5.5f, 128f);
            water.transform.localScale = new Vector3(42f, 1f, 42f);
            var col = water.GetComponent<Collider>();
            if (col != null)
                Object.DestroyImmediate(col);
        }

        if (Object.FindObjectsByType<AdventureRustFloatIsland>(FindObjectsInactive.Exclude).Length == 0)
            new GameObject("RustFloatIsland").AddComponent<AdventureRustFloatIsland>();

        var player = Object.FindObjectsByType<AdventurePlayerController>(FindObjectsInactive.Exclude).FirstOrDefault();
        GameObject nikoGo = player != null ? player.gameObject : null;
        if (nikoGo == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NikoPath);
            if (prefab == null)
            {
                Debug.LogError("[RustAndFloat] Niko prefab がありません");
                return;
            }
            nikoGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            nikoGo.name = "Niko";
        }

        if (nikoGo.GetComponent<CharacterController>() == null)
        {
            var cc = nikoGo.AddComponent<CharacterController>();
            cc.height = 1.55f;
            cc.radius = 0.28f;
            cc.center = new Vector3(0f, 0.78f, 0f);
            cc.slopeLimit = 55f;
            cc.stepOffset = 0.4f;
        }

        player = nikoGo.GetComponent<AdventurePlayerController>();
        if (player == null)
            player = nikoGo.AddComponent<AdventurePlayerController>();
        player.canGlide = true;
        player.jumpHeight = 1.4f;

        Vector3 spawn = new Vector3(138f, 0f, 184f);
        spawn.y = land.SampleHeight(spawn) + 0.12f;
        nikoGo.transform.SetPositionAndRotation(spawn, Quaternion.identity);
        player.spawnPosition = spawn;

        var cam = Camera.main;
        if (cam == null)
            cam = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude).FirstOrDefault();
        if (cam != null)
        {
            var follow = cam.GetComponent<AdventureCameraFollow>();
            if (follow == null)
                follow = cam.gameObject.AddComponent<AdventureCameraFollow>();
            follow.target = nikoGo.transform;
            player.cameraPivot = cam.transform;
            cam.transform.position = spawn + new Vector3(0f, 4f, -8f);
            cam.transform.LookAt(spawn + Vector3.up * 1.5f);
        }

        var marker = GameObject.Find("SpawnMarker") ?? GameObject.Find("CliffLookout");
        if (marker != null)
            Object.DestroyImmediate(marker);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[RustAndFloat] 島を作成しました。スポーン=" + spawn + " 崖高さ=" + land.SampleHeight(new Vector3(138f, 0f, 186f)));
    }

    /// <summary>
    /// 島全体は作り直さず、北の見晴らし台だけ滑空用にする。
    /// 南から歩ける坂、広い平坦、北端のほぼ垂直な落ち。
    /// </summary>
    public static void ShapeNorthCliff()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("停止してください", "■で再生を止めてから実行してください。", "OK");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var td = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
        var land = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude)
            .FirstOrDefault(t => t.name == "LandTerrain" || t.name == "IslandTerrain");
        if (td == null || land == null)
        {
            Debug.LogError("[RustAndFloat] IslandTerrain がありません");
            return;
        }

        int res = td.heightmapResolution;
        float[,] h = td.GetHeights(0, 0, res, res);
        float inv = 1f / (res - 1);
        const float size = 256f;
        const float peakY = 48f;
        const float padH = 32.8f / peakY;
        const float seaH = 0.05f;
        const float lookX = 138f;
        const float padZ0 = 166f;
        const float padZ1 = 186f;
        const float approachZ0 = 138f;
        const float dropMeters = 1.6f;
        const float padHalf = 14f;
        const float sideBlend = 10f;

        for (int z = 0; z < res; z++)
        {
            float wz = z * inv * size;
            if (wz < 128f || wz > 214f)
                continue;
            for (int x = 0; x < res; x++)
            {
                float wx = x * inv * size;
                float absDx = Mathf.Abs(wx - lookX);
                if (absDx > padHalf + sideBlend + 4f)
                    continue;

                float lat = Mathf.InverseLerp(padHalf + sideBlend, padHalf, absDx);
                if (lat <= 0f)
                    continue;

                float cur = h[z, x];
                float want = cur;

                if (wz >= padZ0 && wz <= padZ1)
                {
                    want = padH;
                }
                else if (wz > padZ1)
                {
                    float t = Mathf.Clamp01((wz - padZ1) / dropMeters);
                    t = t * t * (3f - 2f * t);
                    t = t * t;
                    want = Mathf.Lerp(padH, seaH, t);
                }
                else if (wz >= approachZ0)
                {
                    float a = Mathf.InverseLerp(approachZ0, padZ0, wz);
                    a = a * a * (3f - 2f * a);
                    want = Mathf.Lerp(cur, padH, a);
                    if (want < cur)
                        want = cur;
                }

                h[z, x] = Mathf.Clamp01(Mathf.Lerp(cur, want, lat));
            }
        }

        td.SetHeights(0, 0, h);
        land.terrainData = td;
        land.Flush();
        PaintLayers(td, land);
        EditorUtility.SetDirty(td);

        ClearGlidePathFoliage();

        var player = Object.FindObjectsByType<AdventurePlayerController>(FindObjectsInactive.Exclude).FirstOrDefault();
        if (player != null)
        {
            Vector3 spawn = new Vector3(138f, 0f, 176f);
            spawn.y = land.SampleHeight(spawn) + 0.12f;
            player.transform.SetPositionAndRotation(spawn, Quaternion.Euler(0f, 0f, 0f));
            player.spawnPosition = spawn;
            EditorUtility.SetDirty(player);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[RustAndFloat] 北崖を滑空向きに整形。スポーン=" + land.SampleHeight(new Vector3(138f, 0f, 176f)).ToString("F1")
            + " 唇=" + land.SampleHeight(new Vector3(138f, 0f, 186f)).ToString("F1")
            + " 直下=" + land.SampleHeight(new Vector3(138f, 0f, 188f)).ToString("F1"));
    }

    static void ClearGlidePathFoliage()
    {
        var paradise = GameObject.Find("Paradise");
        if (paradise == null)
            return;

        var victims = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in paradise.transform)
        {
            Vector3 p = child.position;
            if (p.x < 118f || p.x > 158f || p.z < 182f || p.z > 210f)
                continue;
            victims.Add(child.gameObject);
        }
        for (int i = 0; i < victims.Count; i++)
            Object.DestroyImmediate(victims[i]);
    }

    static void PaintLayers(TerrainData td, Terrain land)
    {
        var sand = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/RustAndFloat/Terrain/SandLayer.terrainlayer");
        var grass = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Idyllic Fantasy Nature/Terrain Layer/Grass_Layer.terrainlayer");
        var rock = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Idyllic Fantasy Nature/Terrain Layer/Rock_Layer.terrainlayer");
        if (sand == null || grass == null || rock == null)
            return;

        // grass を index 0 (ベース) にすることで、島全体が鮮やかな緑の草原に！
        td.terrainLayers = new[] { grass, sand, rock };
        int aRes = 512;
        td.alphamapResolution = aRes;
        float[,,] a = new float[aRes, aRes, 3];
        const float waterY = 5.5f;

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

                // 垂直に近い急崖（ny < 0.32、傾斜角約71度以上）のみ岩肌に。なだらかな丘陵斜面はすべて緑の草原に！
                float steepness = 1f - Mathf.Clamp01((ny - 0.32f) / 0.15f);
                float rockW = Mathf.Clamp01(steepness * 2.0f);

                // 白砂ビーチ：海面(5.5m)から波打ち際・海岸線(8.5mまで)
                float sandW = 0f;
                if (hy < waterY + 3.0f)
                {
                    sandW = Mathf.Clamp01(1f - (hy - (waterY + 0.5f)) / 2.5f);
                }

                // 残りの全領域（丘陵、台地、平原、スロープ）は見渡す限りの鮮やかな緑の大草原！
                float grassW = Mathf.Clamp01(1f - Mathf.Max(sandW, rockW));

                float sum = grassW + sandW + rockW;
                if (sum < 0.001f)
                {
                    grassW = 1f;
                    sum = 1f;
                }

                a[z, x, 0] = grassW / sum; // grass
                a[z, x, 1] = sandW / sum;  // sand
                a[z, x, 2] = rockW / sum;  // rock
            }
        }
        td.SetAlphamaps(0, 0, a);
    }

    /// <summary>何十万本もの風にそよぐ草をTerrainのDetailLayerとして島一面・大草原に高密度生成</summary>
    static void PaintGrassDetails(TerrainData td, Terrain land)
    {
        var t1 = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Idyllic Fantasy Nature/Textures/Grass/Grass_01.png");
        var t2 = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Idyllic Fantasy Nature/Textures/Grass/Grass_02.png");
        var t3 = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Idyllic Fantasy Nature/Textures/Grass/Grass_03.png");
        if (t1 == null || t2 == null || t3 == null)
            return;

        var dpList = new System.Collections.Generic.List<DetailPrototype>();
        Texture2D[] texs = { t1, t2, t3 };
        for (int i = 0; i < texs.Length; i++)
        {
            var dp = new DetailPrototype();
            dp.prototypeTexture = texs[i];
            dp.renderMode = DetailRenderMode.GrassBillboard;
            dp.minWidth = 2.4f;
            dp.maxWidth = 4.8f;
            dp.minHeight = 2.2f;
            dp.maxHeight = 4.2f;
            dp.healthyColor = new Color(0.85f, 0.98f, 0.55f);
            dp.dryColor = new Color(0.70f, 0.85f, 0.40f);
            dpList.Add(dp);
        }
        td.detailPrototypes = dpList.ToArray();

        // 風でそよぐ草原の設定
        td.wavingGrassStrength = 0.65f;
        td.wavingGrassSpeed = 0.50f;
        td.wavingGrassAmount = 0.60f;
        td.wavingGrassTint = new Color(0.75f, 0.92f, 0.45f);

        int dRes = 512;
        td.SetDetailResolution(dRes, 16);
        land.detailObjectDistance = 500f; // 500m先まで見渡す限りの地平線に草を描画！
        land.detailObjectDensity = 1.0f;

        const float waterY = 5.5f;
        for (int l = 0; l < texs.Length; l++)
        {
            int[,] map = new int[dRes, dRes];
            for (int z = 0; z < dRes; z++)
            {
                for (int x = 0; x < dRes; x++)
                {
                    float nx = x / (float)(dRes - 1);
                    float nz = z / (float)(dRes - 1);
                    float wx = nx * td.size.x;
                    float wz = nz * td.size.z;
                    float hy = land.SampleHeight(new Vector3(wx, 0f, wz));
                    float steep = td.GetInterpolatedNormal(nx, nz).y;

                    // 海水面より上（8m以上）、垂直崖でない（steep > 0.35）場所島一面に超高密度に草を生やす！
                    if (hy > waterY + 2.5f && steep > 0.35f)
                    {
                        // 中央タワー広場周辺は少し低密度に
                        float dCenter = Vector2.Distance(new Vector2(wx, wz), new Vector2(512f, 512f));
                        if (dCenter < 35f)
                        {
                            map[z, x] = 0;
                        }
                        else
                        {
                            // 島全体の丘陵・平原すべてを最大密度の大草原に！
                            map[z, x] = (l == 0) ? 22 : ((l == 1) ? 18 : 15);
                        }
                    }
                }
            }
            td.SetDetailLayer(0, 0, l, map);
        }
    }

    /// <summary>
    /// 約1000m四方の広大な『サンクチュアリ・ゼロ：隔離区域』島を生成
    /// 中央白亜遺跡タワー台地、カルデラ池、南西への渓谷・川、大滑空北崖、外周白砂ビーチ
    /// </summary>
    public static void BuildGrandIsland()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("停止してください", "■で再生を止めてから実行してください。", "OK");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var td = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
        if (td == null)
        {
            Debug.LogError("[RustAndFloat] IslandTerrain がありません");
            return;
        }

        int res = 1025;
        td.heightmapResolution = res;
        const float islandW = 1024f;
        const float islandH = 120f;
        td.size = new Vector3(islandW, islandH, islandW);

        float[,] h = new float[res, res];
        float cx = (res - 1) * 0.5f;
        float cz = (res - 1) * 0.5f;
        float islandR = (res - 1) * 0.44f; // 半径約450m
        const float seaH = 5.5f / islandH;

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float dx = (x - cx) / islandR;
                float dz = (z - cz) / islandR;
                float d = Mathf.Sqrt(dx * dx + dz * dz);

                float wx = x / (float)(res - 1) * islandW;
                float wz = z / (float)(res - 1) * islandW;

                // 複数オクターブのパーリンノイズで豊かな山・丘・谷を形成
                float nBase = Mathf.PerlinNoise(wx * 0.004f, wz * 0.004f) * 0.24f; // 大きなうねり
                float nRidge = Mathf.PerlinNoise(wx * 0.009f + 120f, wz * 0.009f + 70f) * 0.14f; // 中規模の丘・尾根
                float nDetail = Mathf.PerlinNoise(wx * 0.025f + 40f, wz * 0.025f + 40f) * 0.035f; // 細かい起伏

                float height = seaH * 0.4f; // デフォルトは海中

                if (d <= 1.0f)
                {
                    // 砂浜から島内部への自然な立ち上がり（d=0.84〜0.96に広大な白砂ビーチテラスを確保）
                    float beachProfile;
                    if (d > 0.96f)
                    {
                        // 海から波打ち際（5.5m〜6.2m）
                        beachProfile = Mathf.Lerp(seaH * 0.9f, 6.2f / islandH, Mathf.InverseLerp(1.0f, 0.96f, d));
                    }
                    else if (d > 0.84f)
                    {
                        // 広大な白砂ビーチ（6.2m〜8.5m、幅約50mのなだらかな砂浜）
                        float tBeach = Mathf.InverseLerp(0.96f, 0.84f, d);
                        beachProfile = Mathf.Lerp(6.2f / islandH, 8.5f / islandH, tBeach);
                    }
                    else
                    {
                        // 砂浜から内陸の丘陵・高原へ
                        float tInland = Mathf.InverseLerp(0.84f, 0.0f, d);
                        beachProfile = Mathf.Lerp(8.5f / islandH, 0.28f, Mathf.Pow(tInland, 0.7f));
                    }

                    height = beachProfile + (d < 0.86f ? (nBase + nRidge + nDetail) : (nDetail * 0.3f));

                    // 東部〜北東部：険しい岩山をなくし、広大でなだらかな「緑の丘陵大草原（Rolling Green Hills）」へ（標高22m〜35m）
                    // プレイヤーがどこまでも歩いて駆け抜けられる心地よい起伏
                    float eastHillDist = Vector2.Distance(new Vector2(wx, wz), new Vector2(720f, 580f));
                    if (eastHillDist < 180f)
                    {
                        float tHill = Mathf.Clamp01((180f - eastHillDist) / 180f);
                        float hillShape = Mathf.PerlinNoise(wx * 0.008f + 80f, wz * 0.008f + 80f) * 0.08f;
                        height += tHill * (0.10f + hillShape); // 穏やかな丘陵スロープ
                    }

                    // 島全域の広大な緑の大草原・野原（x: 140〜880, z: 150〜720）標高14m〜32m
                    // 視界を遮る急峻な壁を抑え、なだらかに波打つ心地よい緑の野原スロープを全面に形成
                    if (wx > 140f && wx < 880f && wz > 150f && wz < 720f)
                    {
                        float meadowShape = Mathf.PerlinNoise(wx * 0.006f + 45f, wz * 0.006f + 45f) * 0.05f;
                        height += meadowShape * 0.5f;
                    }

                    // 1. 中央白亜台地 (x=512, z=512, 半径75m、標高約62m = 0.517)
                    float distCenter = Vector2.Distance(new Vector2(wx, wz), new Vector2(512f, 512f));
                    if (distCenter < 80f)
                    {
                        float tPlateau = Mathf.Clamp01((80f - distCenter) / 22f);
                        float wantPlateau = 62f / islandH;
                        height = Mathf.Lerp(height, wantPlateau, tPlateau);
                    }

                    // 2. 北の大滑空崖 (x: 470-554, z: 660-760、標高約92m = 0.767)
                    float cliffDist = Vector2.Distance(new Vector2(wx, wz), new Vector2(512f, 720f));
                    if (cliffDist < 100f)
                    {
                        float tCliff = Mathf.Clamp01((100f - cliffDist) / 45f);
                        height = Mathf.Max(height, Mathf.Lerp(height, 92f / islandH, tCliff));

                        // 北端 (z > 750) は海面下へ垂直急落
                        if (wz > 750f && Mathf.Abs(wx - 512f) < 55f)
                        {
                            float drop = Mathf.Clamp01((wz - 750f) / 12f);
                            height = Mathf.Lerp(height, seaH * 0.5f, drop * drop);
                        }
                    }

                    // 3. 中央台地足元のオアシス湧水池 (x=480, z=455, 半径16m, 水面48.5m)
                    float distOasis = Vector2.Distance(new Vector2(wx, wz), new Vector2(480f, 455f));
                    if (distOasis < 22f)
                    {
                        float tOasis = Mathf.Clamp01((22f - distOasis) / 10f);
                        float oasisBottom = 46.0f / islandH;
                        height = Mathf.Lerp(height, oasisBottom, tOasis);
                    }

                    // 4. オアシス池からカルデラ湖へ注ぐ上流小川の掘り込み
                    float upStreamT = Mathf.Clamp01((480f - wx) / 60f);
                    if (upStreamT > 0f && upStreamT < 1f && wz > 430f && wz < 465f)
                    {
                        float streamZ = Mathf.Lerp(455f, 440f, upStreamT);
                        float dStream = Mathf.Abs(wz - streamZ);
                        if (dStream < 10f)
                        {
                            float sCut = Mathf.Clamp01(1f - dStream / 10f);
                            float sTargetH = Mathf.Lerp(47.5f / islandH, 25.0f / islandH, upStreamT);
                            height = Mathf.Lerp(height, sTargetH, sCut * 0.85f);
                        }
                    }

                    // 5. 大カルデラ湖 (x=420, z=440, 半径38m、標高約25.5mの広大な湖)
                    float distLake = Vector2.Distance(new Vector2(wx, wz), new Vector2(420f, 440f));
                    if (distLake < 75f)
                    {
                        if (distLake < 40f)
                        {
                            // 湖底
                            float tLake = Mathf.Clamp01((40f - distLake) / 16f);
                            float lakeBottom = 22.5f / islandH;
                            height = Mathf.Lerp(height, lakeBottom, tLake);
                        }
                        else
                        {
                            // 外輪山の尾根
                            float rimT = Mathf.Sin((distLake - 40f) / 35f * Mathf.PI);
                            height += rimT * 0.07f;
                        }
                    }

                    // 6. 大草原の憩いのせせらぎ池 (x=290, z=320, 半径16m, 水面14.5m)
                    float distMeadowPond = Vector2.Distance(new Vector2(wx, wz), new Vector2(290f, 320f));
                    if (distMeadowPond < 22f)
                    {
                        float tPond = Mathf.Clamp01((22f - distMeadowPond) / 10f);
                        float pondBottom = 12.0f / islandH;
                        height = Mathf.Lerp(height, pondBottom, tPond);
                    }

                    // 7. カルデラ湖から大草原の池を経て南西の海へ抜ける小川・大河（水路の掘り込み）
                    float riverT = Mathf.Clamp01((420f - wx) / 260f);
                    if (riverT > 0f && riverT < 1f)
                    {
                        // カルデラ湖(420, 440)から草原池(290, 320)付近を蛇行して海(160, 180)へ
                        float riverZ = Mathf.Lerp(440f, 180f, riverT) + Mathf.Sin(wx * 0.025f) * 22f;
                        float distRiver = Mathf.Abs(wz - riverZ);
                        if (distRiver < 18f && wx < 420f && wx > 160f)
                        {
                            float rCut = Mathf.Clamp01(1f - distRiver / 18f);
                            // 湖(25.5m)から海(5.5m)へなだらかに下る川底
                            float rTargetH = Mathf.Lerp(24.5f / islandH, 5.0f / islandH, riverT);
                            height = Mathf.Lerp(height, rTargetH, rCut * 0.85f);
                        }
                    }

                    // 8. 東の深林大渓流（標高40mの東部高地原生林からカルデラ湖25.5mへ急流で下るV字谷渓谷）
                    float torrentT = Mathf.Clamp01((720f - wx) / 300f);
                    if (torrentT > 0f && torrentT < 1f && wx > 420f && wx < 720f)
                    {
                        float torrentZ = Mathf.Lerp(570f, 440f, torrentT) + Mathf.Sin(wx * 0.035f) * 16f;
                        float distTorrent = Mathf.Abs(wz - torrentZ);
                        if (distTorrent < 16f)
                        {
                            float tCut = Mathf.Clamp01(1f - distTorrent / 16f);
                            // 高地(40m)から湖(25m)へ激しく下る岩の渓谷川底
                            float tTargetH = Mathf.Lerp(39.0f / islandH, 24.8f / islandH, torrentT);
                            height = Mathf.Lerp(height, tTargetH, tCut * 0.88f);
                        }
                    }
                }

                h[z, x] = Mathf.Clamp01(height);
            }
        }

        td.SetHeights(0, 0, h);
        EditorUtility.SetDirty(td);

        var land = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude).FirstOrDefault();
        if (land == null)
        {
            Debug.LogError("[RustAndFloat] Terrain がありません");
            return;
        }

        land.name = "LandTerrain";
        land.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        land.terrainData = td;
        land.Flush();
        PaintLayers(td, land);
        PaintGrassDetails(td, land);

        // 広大な海面 OceanPlane (中心 512, 5.5, 512, スケール 160)
        var water = GameObject.Find("OceanPlane");
        if (water != null)
        {
            water.transform.position = new Vector3(512f, 5.5f, 512f);
            water.transform.localScale = new Vector3(160f, 1f, 160f);
            var col = water.GetComponent<Collider>();
            if (col != null)
                Object.DestroyImmediate(col);
        }

        // 3つの池（オアシス湧水池・大カルデラ湖・草原池）と小川（上流急流・本流大河）の水面メッシュと3Dせせらぎ音
        EnsureAllWaterBodies();

        // 中央タワー・白亜の遺跡の床プレースホルダー
        EnsureSanctuaryTower(new Vector3(512f, 62f, 512f));

        // nikoの配置（中央白亜台地の見晴らしテラス、標高約62.5m）
        var player = Object.FindObjectsByType<AdventurePlayerController>(FindObjectsInactive.Exclude).FirstOrDefault();
        GameObject nikoGo = player != null ? player.gameObject : null;
        if (nikoGo == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NikoPath);
            if (prefab != null)
            {
                nikoGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                nikoGo.name = "Niko";
            }
        }

        if (nikoGo != null)
        {
            // 大草原のせせらぎ池のほとり（x=265, z=330, 標高約15m）の広大な緑の平原にスポーン
            Vector3 spawn = new Vector3(265f, 0f, 330f);
            float terrainH = land.SampleHeight(spawn);
            spawn.y = terrainH + 0.15f;
            nikoGo.transform.SetPositionAndRotation(spawn, Quaternion.Euler(0f, 55f, 0f));

            player = nikoGo.GetComponent<AdventurePlayerController>();
            if (player != null)
            {
                player.spawnPosition = spawn;
                player.canGlide = true;
            }

            if (nikoGo.GetComponent<AdventureNikoFootsteps>() == null)
                nikoGo.AddComponent<AdventureNikoFootsteps>();

            var cam = Camera.main ?? Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude).FirstOrDefault();
            if (cam != null)
            {
                var follow = cam.GetComponent<AdventureCameraFollow>() ?? cam.gameObject.AddComponent<AdventureCameraFollow>();
                follow.target = nikoGo.transform;
                cam.transform.position = spawn + new Vector3(-5.5f, 3.2f, -5.5f);
                cam.transform.LookAt(spawn + Vector3.up * 1.4f);
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("<color=#00FFAA><b>[RustAndFloat]</b> 1000m規模の『サンクチュアリ・ゼロ』広大島を構築しました！中央タワー・池・川・大滑空北崖を生成完了。</color>");
    }

    static void EnsureLakeWater(string name, Vector3 center, float radius)
    {
        var lake = GameObject.Find(name);
        if (lake == null)
        {
            lake = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lake.name = name;
        }

        lake.transform.position = center;
        lake.transform.localScale = new Vector3(radius * 2f, 0.08f, radius * 2f);

        var col = lake.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        var ocean = GameObject.Find("OceanPlane");
        if (ocean != null)
        {
            var rend = lake.GetComponent<Renderer>();
            var oceanRend = ocean.GetComponent<Renderer>();
            if (rend != null && oceanRend != null)
                rend.sharedMaterial = oceanRend.sharedMaterial;
        }

        // 3D環境音
        var streamClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/AudioFiles/03_amb/watershore_amb.wav");
        if (streamClip != null && lake.GetComponent<AudioSource>() == null)
        {
            var src = lake.AddComponent<AudioSource>();
            src.clip = streamClip;
            src.loop = true;
            src.playOnAwake = true;
            src.spatialBlend = 1.0f;
            src.minDistance = 6f;
            src.maxDistance = radius * 1.8f;
            src.volume = 0.20f;
            src.rolloffMode = AudioRolloffMode.Linear;
        }
    }

    static void EnsureSanctuaryTower(Vector3 center)
    {
        var root = GameObject.Find("SanctuaryZero_Tower");
        if (root == null)
            root = new GameObject("SanctuaryZero_Tower");

        root.transform.position = center;

        // 1. 白亜の円形広場基壇（直径70m）
        var podium = root.transform.Find("WhiteMarblePodium")?.gameObject;
        if (podium == null)
        {
            podium = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            podium.name = "WhiteMarblePodium";
            podium.transform.SetParent(root.transform, false);
            var cap = podium.GetComponent<CapsuleCollider>();
            if (cap != null) Object.DestroyImmediate(cap);
            if (podium.GetComponent<MeshCollider>() == null)
                podium.AddComponent<MeshCollider>();
        }
        podium.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        podium.transform.localScale = new Vector3(70f, 0.6f, 70f);

        // 2. ワイヤーフレーム発光予定のグリッド床（中央部・直径35m）
        var gridFloor = root.transform.Find("SanctuaryGridFloor")?.gameObject;
        if (gridFloor == null)
        {
            gridFloor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            gridFloor.name = "SanctuaryGridFloor";
            gridFloor.transform.SetParent(root.transform, false);
            var cap = gridFloor.GetComponent<CapsuleCollider>();
            if (cap != null) Object.DestroyImmediate(cap);
            if (gridFloor.GetComponent<MeshCollider>() == null)
                gridFloor.AddComponent<MeshCollider>();
        }
        gridFloor.transform.localPosition = new Vector3(0f, 0.72f, 0f);
        gridFloor.transform.localScale = new Vector3(36f, 0.1f, 36f);

        // 3. 中央の白亜オベリスク／監視タワー
        var towerPillar = root.transform.Find("CentralMonolith")?.gameObject;
        if (towerPillar == null)
        {
            towerPillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            towerPillar.name = "CentralMonolith";
            towerPillar.transform.SetParent(root.transform, false);
        }
        towerPillar.transform.localPosition = new Vector3(0f, 25f, 0f);
        towerPillar.transform.localScale = new Vector3(8f, 50f, 8f);
    }

    /// <summary>全池（オアシス池、カルデラ湖、草原池）と小川（上流・本流）の水面メッシュとせせらぎ音を生成</summary>
    static void EnsureAllWaterBodies()
    {
        // 1. 中央台地足元のオアシス湧水池 (x=480, y=48.2m, z=455, 半径16m)
        EnsureLakeWater("SanctuarySpringPond", new Vector3(480f, 48.2f, 455f), 16f);

        // 2. 大カルデラ湖 (x=420, y=25.5m, z=440, 半径38m)
        EnsureLakeWater("CalderaLake", new Vector3(420f, 25.5f, 440f), 38f);

        // 3. 大草原の憩いのせせらぎ池 (x=290, y=14.5m, z=320, 半径16m)
        EnsureLakeWater("MeadowLowlandPond", new Vector3(290f, 14.5f, 320f), 16f);

        var ocean = GameObject.Find("OceanPlane");
        Material waterMat = null;
        if (ocean != null)
        {
            var r = ocean.GetComponent<Renderer>();
            if (r != null) waterMat = r.sharedMaterial;
        }

        var streamClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/AudioFiles/03_amb/watershore_amb.wav");

        // 4. 上流小川: オアシス湧水池(480, 455) から カルデラ湖(420, 440) へ下るせせらぎ
        var upRoot = GameObject.Find("UpperParadiseStream");
        if (upRoot != null) Object.DestroyImmediate(upRoot);
        upRoot = new GameObject("UpperParadiseStream");

        const int upSegs = 9;
        for (int i = 0; i < upSegs; i++)
        {
            float t = (float)i / (upSegs - 1);
            float wx = Mathf.Lerp(475f, 425f, t);
            float wz = Mathf.Lerp(455f, 440f, t);
            float wy = Mathf.Lerp(48.0f, 25.6f, t);

            var seg = GameObject.CreatePrimitive(PrimitiveType.Plane);
            seg.name = "UpSeg_" + i;
            seg.transform.SetParent(upRoot.transform, false);
            seg.transform.position = new Vector3(wx, wy, wz);

            float nextT = Mathf.Min(1f, t + 0.12f);
            float nextX = Mathf.Lerp(475f, 425f, nextT);
            float nextZ = Mathf.Lerp(455f, 440f, nextT);
            Vector3 fwd = new Vector3(nextX - wx, 0f, nextZ - wz).normalized;
            if (fwd != Vector3.zero) seg.transform.rotation = Quaternion.LookRotation(fwd);

            seg.transform.localScale = new Vector3(0.9f, 1f, 1.2f); // 幅9m x 長さ12m

            var col = seg.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            if (waterMat != null)
            {
                var rend = seg.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = waterMat;
            }

            if (i % 3 == 0 && streamClip != null)
            {
                var sndGo = new GameObject("UpSound_" + i);
                sndGo.transform.SetParent(seg.transform, false);
                sndGo.transform.localPosition = Vector3.zero;
                var src = sndGo.AddComponent<AudioSource>();
                src.clip = streamClip;
                src.loop = true;
                src.playOnAwake = true;
                src.spatialBlend = 1.0f;
                src.minDistance = 6f;
                src.maxDistance = 25f;
                src.volume = 0.22f;
                src.rolloffMode = AudioRolloffMode.Linear;
            }
        }

        // 5. 本流大河: カルデラ湖(420, 440)から草原池(290, 320)を経て海(160, 180)へ下る小川
        var riverRoot = GameObject.Find("ParadiseRiver");
        if (riverRoot != null) Object.DestroyImmediate(riverRoot);
        riverRoot = new GameObject("ParadiseRiver");

        const int segs = 20;
        for (int i = 0; i < segs; i++)
        {
            float t = (float)i / (segs - 1);
            float wx = Mathf.Lerp(415f, 162f, t);
            float wz = Mathf.Lerp(438f, 182f, t) + Mathf.Sin(wx * 0.025f) * 22f;
            float wy = Mathf.Lerp(25.2f, 5.6f, t); // 湖面から海面へなだらかに下る

            var seg = GameObject.CreatePrimitive(PrimitiveType.Plane);
            seg.name = "RiverSeg_" + i;
            seg.transform.SetParent(riverRoot.transform, false);
            seg.transform.position = new Vector3(wx, wy, wz);

            float nextT = Mathf.Min(1f, t + 0.06f);
            float nextX = Mathf.Lerp(415f, 162f, nextT);
            float nextZ = Mathf.Lerp(438f, 182f, nextT) + Mathf.Sin(nextX * 0.025f) * 22f;
            Vector3 forward = new Vector3(nextX - wx, 0f, nextZ - wz).normalized;
            if (forward != Vector3.zero)
                seg.transform.rotation = Quaternion.LookRotation(forward);

            seg.transform.localScale = new Vector3(1.6f, 1f, 2.0f); // 幅16m x 長さ20m

            var col = seg.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            if (waterMat != null)
            {
                var rend = seg.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = waterMat;
            }

            // 川のせせらぎ音（3セグメントごとに配置）
            if (i % 3 == 0 && streamClip != null)
            {
                var sndGo = new GameObject("RiverSound_" + i);
                sndGo.transform.SetParent(seg.transform, false);
                sndGo.transform.localPosition = Vector3.zero;

                var src = sndGo.AddComponent<AudioSource>();
                src.clip = streamClip;
                src.loop = true;
                src.playOnAwake = true;
                src.spatialBlend = 1.0f;
                src.minDistance = 8f;
                src.maxDistance = 45f;
                src.volume = 0.25f;
                src.rolloffMode = AudioRolloffMode.Linear;
            }
        }

        // 6. 東の深林大渓流（標高40mの森林高台からカルデラ湖25.5mへ急流で下る段々水面メッシュ）
        var torrentRoot = GameObject.Find("EasternMountainTorrent");
        if (torrentRoot != null) Object.DestroyImmediate(torrentRoot);
        torrentRoot = new GameObject("EasternMountainTorrent");

        const int tSegs = 24;
        for (int i = 0; i < tSegs; i++)
        {
            float t = (float)i / (tSegs - 1);
            float wx = Mathf.Lerp(715f, 425f, t);
            float wz = Mathf.Lerp(570f, 440f, t) + Mathf.Sin(wx * 0.035f) * 16f;
            float wy = Mathf.Lerp(39.5f, 25.5f, t); // 標高40mから25.5mへ激しく下る

            var seg = GameObject.CreatePrimitive(PrimitiveType.Plane);
            seg.name = "TorrentSeg_" + i;
            seg.transform.SetParent(torrentRoot.transform, false);
            seg.transform.position = new Vector3(wx, wy, wz);

            float nextT = Mathf.Min(1f, t + 0.05f);
            float nextX = Mathf.Lerp(715f, 425f, nextT);
            float nextZ = Mathf.Lerp(570f, 440f, nextT) + Mathf.Sin(nextX * 0.035f) * 16f;
            Vector3 forward = new Vector3(nextX - wx, 0f, nextZ - wz).normalized;
            if (forward != Vector3.zero)
                seg.transform.rotation = Quaternion.LookRotation(forward);

            seg.transform.localScale = new Vector3(1.2f, 1f, 1.6f); // 幅12m x 長さ16m

            var col = seg.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            if (waterMat != null)
            {
                var rend = seg.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = waterMat;
            }

            if (i % 3 == 0 && streamClip != null)
            {
                var sndGo = new GameObject("TorrentSound_" + i);
                sndGo.transform.SetParent(seg.transform, false);
                sndGo.transform.localPosition = Vector3.zero;

                var src = sndGo.AddComponent<AudioSource>();
                src.clip = streamClip;
                src.loop = true;
                src.playOnAwake = true;
                src.spatialBlend = 1.0f;
                src.minDistance = 8f;
                src.maxDistance = 40f;
                src.volume = 0.25f;
                src.rolloffMode = AudioRolloffMode.Linear;
            }
        }
    }
}

