using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;

public static class AdventureBuildRustFloatIsland
{
    const string ScenePath = "Assets/RustAndFlat/Scenes/RustAndFlat.unity";
    const string TerrainPath = "Assets/RustAndFlat/Terrain/IslandTerrain.asset";
    const string NikoPath = "Assets/Niko&Capyta/Assets/Prefabs/Niko.prefab";

    [MenuItem("Adventure/Build RustAndFloat Island")]
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
        var sand = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/RustAndFlat/Terrain/SandLayer.terrainlayer");
        var grass = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Idyllic Fantasy Nature/Terrain Layer/Grass_Layer.terrainlayer");
        var rock = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Idyllic Fantasy Nature/Terrain Layer/Rock_Layer.terrainlayer");
        if (sand == null || grass == null || rock == null)
            return;

        td.terrainLayers = new[] { sand, grass, rock };
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
                float hx = td.GetInterpolatedNormal(nx, nz).y;
                float steep = 1f - Mathf.Clamp01((hx - 0.72f) / 0.28f);
                float sandW = Mathf.Clamp01(1f - (hy - (waterY + 1.2f)) / 3.5f);
                float rockW = Mathf.Clamp01(steep * 1.4f + Mathf.InverseLerp(24f, 34f, hy) * 0.65f);
                float grassW = Mathf.Max((1f - sandW) * (1f - rockW), (1f - sandW) * 0.25f);
                float sum = sandW + grassW + rockW;
                if (sum < 0.001f)
                {
                    sandW = 1f;
                    sum = 1f;
                }
                a[z, x, 0] = sandW / sum;
                a[z, x, 1] = grassW / sum;
                a[z, x, 2] = rockW / sum;
            }
        }
        td.SetAlphamaps(0, 0, a);
    }
}
