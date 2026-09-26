// 全ての池・小川の水面質感を高品質URP水面シェーダーへ刷新するエディタ拡張
// メニュー: Adventure → 💧 Beautify All Ponds & Streams Water
// 処理内容:
//   1. PondWater_URP.mat の自動ロード
//   2. 各池（SanctuarySpringPond, CalderaLake, MeadowLowlandPond）の水面マテリアルを差し替え
//   3. 小川・段々池セグメントにも適用
//   4. 水底の Terrain AlphaMap に泥・土肌・砂地をペイント
//   5. 浅瀬に葦・ガマ・睡蓮を配置

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public static class AdventureBeautifyAllPondsWater
{
    // 水面マテリアルのパス
    const string PondMatPath = "Assets/RustAndFloat/Materials/PondWater_URP.mat";

    // 浅瀬植物プレハブのパス
    const string ReedsPath_1   = "Assets/Idyllic Fantasy Nature/Prefabs/Reeds_01.prefab";
    const string ReedsPath_2   = "Assets/Idyllic Fantasy Nature/Prefabs/Reeds_02.prefab";
    const string ReedsPath_3   = "Assets/Idyllic Fantasy Nature/Prefabs/Reeds_03.prefab";
    const string CattailPath_1 = "Assets/Idyllic Fantasy Nature/Prefabs/Cattail_01.prefab";
    const string CattailPath_2 = "Assets/Idyllic Fantasy Nature/Prefabs/Cattail_02.prefab";
    const string CattailPath_3 = "Assets/Idyllic Fantasy Nature/Prefabs/Cattail_03.prefab";
    const string LilyPath_1    = "Assets/Idyllic Fantasy Nature/Prefabs/Waterlily_01.prefab";
    const string LilyPath_2    = "Assets/Idyllic Fantasy Nature/Prefabs/Waterlily_02.prefab";

    // 各池の定義
    struct PondDef
    {
        public string   name;
        public Vector3  center;
        public float    radius;
        public float    waterY;
    }

    static readonly PondDef[] Ponds = new PondDef[]
    {
        new PondDef { name = "SanctuarySpringPond", center = new Vector3(480f, 48.2f, 455f), radius = 16f, waterY = 48.2f },
        new PondDef { name = "CalderaLake",         center = new Vector3(420f, 25.5f, 440f), radius = 38f, waterY = 25.5f },
        new PondDef { name = "MeadowLowlandPond",   center = new Vector3(290f, 14.5f, 320f), radius = 16f, waterY = 14.5f },
    };

    [MenuItem("Adventure/💧 Beautify All Ponds & Streams Water (全池・小川の水面質感・水底・水草の美化)")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[BeautifyPonds] 現在Play Mode中です。Play Modeを自動停止し、停止後に処理を実行します...");
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.isPlaying = false;
            return;
        }

        ExecuteBeautify();
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.delayCall += () =>
            {
                Debug.Log("[BeautifyPonds] Edit Modeに移行しました。美化処理を実行します。");
                ExecuteBeautify();
            };
        }
    }


    static void ExecuteBeautify()
    {
        // ─── 1. 水面マテリアルのロード ───
        var pondMat = AssetDatabase.LoadAssetAtPath<Material>(PondMatPath);
        if (pondMat == null)
        {
            Debug.LogError("[BeautifyPonds] PondWater_URP.mat が見つかりません: " + PondMatPath);
            return;
        }

        // ─── 2. 各池の水面メッシュをフラットディスクに差し替え＆マテリアル適用 ───
        foreach (var p in Ponds)
        {
            SetupPondWaterSurface(p, pondMat);
        }

        // ─── 3. 小川・渓流・段々池のセグメントにも適用 ───
        ApplyMaterialToChildren("UpperParadiseStream",    pondMat);
        ApplyMaterialToChildren("ParadiseRiver",          pondMat);
        ApplyMaterialToChildren("EasternMountainTorrent", pondMat);
        ApplyMaterialToChildren("BeachStepPondsRoot",     pondMat); // 段々池

        // ─── 4. 水底の Terrain AlphaMap に土肌をペイント ───
        PaintPondBottoms();

        // ─── 5. 浅瀬に水草・葦・ガマ・睡蓮を配置 ───
        PlaceWaterPlants();

        // ─── 6. シーン保存 ───
        var activeScene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();

        Debug.Log("<color=#00FFCC><b>[BeautifyPonds]</b> 全ての池・小川の水面を高品質シェーダーに刷新し、フラット水面ディスク・水底土肌ペイント・水草配置が完了しました！シーン保存完了。</color>");
    }

    // ─── 池の水面オブジェクトをフラットディスク化してマテリアル適用 ───
    static void SetupPondWaterSurface(PondDef pond, Material mat)
    {
        var go = GameObject.Find(pond.name);
        if (go == null)
        {
            Debug.LogWarning("[BeautifyPonds] '" + pond.name + "' が見つかりません（スキップ）");
            return;
        }

        Undo.RecordObject(go, "Beautify Water Mesh: " + pond.name);

        // コライダーを削除（水面は通り抜け可能にする、またはトリガー）
        var col = go.GetComponent<Collider>();
        if (col != null)
        {
            Object.DestroyImmediate(col);
        }

        // 上面のみの円形フラットディスクメッシュを生成して割り当て
        var mf = go.GetComponent<MeshFilter>();
        if (mf != null)
        {
            // スケールを(1, 1, 1)にリセットし、メッシュ自体を半径 pond.radius で作成
            go.transform.localScale = Vector3.one;
            go.transform.position = new Vector3(pond.center.x, pond.waterY, pond.center.z);

            var discMesh = CreateCircularFlatMesh(pond.radius, 64);
            discMesh.name = pond.name + "_FlatDiscMesh";
            mf.sharedMesh = discMesh;
        }

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false; // 水面自体の影落とし・受影を切って透明感を保つ
        }
        Debug.Log("[BeautifyPonds] '" + pond.name + "' をフラット水面ディスクに更新（半径=" + pond.radius + "m）");
    }

    // ─── 半径R、segments分割の円形フラットメッシュ生成 ───
    static Mesh CreateCircularFlatMesh(float radius, int segments)
    {
        var mesh = new Mesh();
        var vertices = new Vector3[segments + 1];
        var uvs = new Vector2[segments + 1];
        var normals = new Vector3[segments + 1];
        var triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);
        normals[0] = Vector3.up;

        float angleStep = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float rad = (i * angleStep) * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            vertices[i + 1] = new Vector3(cos * radius, 0f, sin * radius);
            uvs[i + 1] = new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f);
            normals[i + 1] = Vector3.up;
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            triangles[i * 3 + 0] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = next + 1;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    // ─── 子オブジェクトの全 MeshRenderer に新マテリアルを適用 ───
    static void ApplyMaterialToChildren(string rootName, Material mat)
    {
        var root = GameObject.Find(rootName);
        if (root == null) return;

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            Undo.RecordObject(r, "Beautify Water: " + r.name);
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        Debug.Log("[BeautifyPonds] '" + rootName + "' の水面マテリアルを刷新（" + renderers.Length + "個）");
    }

    // ─── 各池の水底 Terrain に泥・土肌をペイント ───
    static void PaintPondBottoms()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("[BeautifyPonds] アクティブなTerrainが見つかりません（水底ペイントをスキップ）");
            return;
        }

        var td     = terrain.terrainData;
        var layers = td.terrainLayers;
        if (layers == null || layers.Length < 2)
        {
            Debug.LogWarning("[BeautifyPonds] Terrainレイヤーが不足しています（水底ペイントをスキップ）");
            return;
        }

        int aRes       = td.alphamapResolution;
        int layerCount = layers.Length;
        float[,,] alpha = td.GetAlphamaps(0, 0, aRes, aRes);

        // 各池の水底をペイント
        foreach (var p in Ponds)
        {
            PaintCircleBottom(td, alpha, aRes, layerCount, p.center, p.radius, p.waterY);
        }

        // 小川水路の水底ペイント
        PaintRiverBottom(td, alpha, aRes, layerCount);

        td.SetAlphamaps(0, 0, alpha);
        Debug.Log("[BeautifyPonds] 水底の土肌ペイント完了");
    }

    // ─── 円形の水底をペイント（sand=index1 を強調） ───
    static void PaintCircleBottom(TerrainData td, float[,,] alpha,
        int aRes, int layerCount, Vector3 center, float radius, float waterY)
    {
        float deepRadius   = radius * 0.70f; // 深場の範囲
        float shallowOuter = radius * 1.00f; // 浅瀬の外縁

        for (int az = 0; az < aRes; az++)
        {
            for (int ax = 0; ax < aRes; ax++)
            {
                float nx = (float)ax / (aRes - 1);
                float nz = (float)az / (aRes - 1);
                float wx = nx * td.size.x;
                float wz = nz * td.size.z;

                float dist = Vector2.Distance(new Vector2(wx, wz), new Vector2(center.x, center.z));

                if (dist < shallowOuter)
                {
                    // 中心ほど深い泥（sand強め）、外縁は浅瀬砂地
                    float depthT   = Mathf.Clamp01(1f - dist / deepRadius);
                    float shallowT = 1f - Mathf.Clamp01((shallowOuter - dist) / (shallowOuter - deepRadius));
                    float paintStr = Mathf.Clamp01(Mathf.Max(depthT * 0.55f, shallowT * 0.80f));

                    if (layerCount > 1) // sand = index 1
                    {
                        float currentSand = alpha[az, ax, 1];
                        float newSand     = Mathf.Lerp(currentSand, 0.90f, paintStr);
                        float delta       = newSand - currentSand;
                        alpha[az, ax, 1] = newSand;

                        // grassを削って差分を調整
                        float grassReduce = Mathf.Min(alpha[az, ax, 0], delta);
                        alpha[az, ax, 0]  = Mathf.Max(0f, alpha[az, ax, 0] - grassReduce);
                    }

                    // 合計を正規化
                    float sum = 0f;
                    for (int l = 0; l < layerCount; l++) sum += alpha[az, ax, l];
                    if (sum > 0.001f)
                        for (int l = 0; l < layerCount; l++) alpha[az, ax, l] /= sum;
                }
            }
        }
    }

    // ─── 小川水路の水底ペイント ───
    static void PaintRiverBottom(TerrainData td, float[,,] alpha, int aRes, int layerCount)
    {
        if (layerCount < 2) return;

        for (int az = 0; az < aRes; az++)
        {
            for (int ax = 0; ax < aRes; ax++)
            {
                float nx = (float)ax / (aRes - 1);
                float nz = (float)az / (aRes - 1);
                float wx = nx * td.size.x;
                float wz = nz * td.size.z;

                // カルデラ湖(420, 440)→草原池(290, 320)→海(160, 180) の水路
                float riverT = Mathf.Clamp01((420f - wx) / 260f);
                if (riverT > 0f && riverT < 1f)
                {
                    float riverZ   = Mathf.Lerp(440f, 180f, riverT) + Mathf.Sin(wx * 0.025f) * 22f;
                    float distRiver = Mathf.Abs(wz - riverZ);
                    if (distRiver < 14f && wx < 420f && wx > 165f)
                    {
                        float rStr    = Mathf.Clamp01(1f - distRiver / 14f) * 0.70f;
                        float newSand = Mathf.Lerp(alpha[az, ax, 1], 0.85f, rStr);
                        float delta   = newSand - alpha[az, ax, 1];
                        alpha[az, ax, 1] = newSand;
                        alpha[az, ax, 0] = Mathf.Max(0f, alpha[az, ax, 0] - delta);

                        float sum = 0f;
                        for (int l = 0; l < layerCount; l++) sum += alpha[az, ax, l];
                        if (sum > 0.001f)
                            for (int l = 0; l < layerCount; l++) alpha[az, ax, l] /= sum;
                    }
                }
            }
        }
    }

    // ─── 浅瀬に葦・ガマ・睡蓮を配置 ───
    static void PlaceWaterPlants()
    {
        var prefabs = new List<GameObject>();
        string[] paths = new string[]
        {
            ReedsPath_1, ReedsPath_2, ReedsPath_3,
            CattailPath_1, CattailPath_2, CattailPath_3,
            LilyPath_1, LilyPath_2
        };
        foreach (var p in paths)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (pf != null) prefabs.Add(pf);
        }
        if (prefabs.Count == 0)
        {
            Debug.LogWarning("[BeautifyPonds] 水草プレハブが見つかりません（スキップ）");
            return;
        }

        foreach (var p in Ponds)
        {
            PlacePlantsAroundPond(p, prefabs);
        }
        Debug.Log("[BeautifyPonds] 浅瀬の水草・葦・睡蓮の配置が完了しました");
    }

    // ─── 1つの池の浅瀬に水草を配置 ───
    static void PlacePlantsAroundPond(PondDef pond, List<GameObject> prefabs)
    {
        // 既存の水草ルートを削除して再配置
        string rootName = "PondPlants_" + pond.name;
        var old = GameObject.Find(rootName);
        if (old != null) Object.DestroyImmediate(old);

        var root = new GameObject(rootName);
        var terrain = Terrain.activeTerrain;

        // カルデラ湖は広いのでより多く
        int plantCount = (pond.radius > 30f) ? 36 : 18;
        var rng = new System.Random(pond.name.GetHashCode());

        for (int i = 0; i < plantCount; i++)
        {
            float angle  = (float)(rng.NextDouble() * 360.0);
            float isPond = (float)rng.NextDouble();
            float dist;
            GameObject prefab;
            float baseY;

            if (isPond < 0.35f)
            {
                // 水面に浮かぶ睡蓮（水面中央部）
                dist   = (float)(rng.NextDouble() * pond.radius * 0.55f + pond.radius * 0.08f);
                int lilyStart = Mathf.Min(6, prefabs.Count - 1);
                prefab = prefabs[lilyStart + rng.Next(0, prefabs.Count - lilyStart)];
                baseY  = pond.waterY + 0.04f; // 水面直上
            }
            else
            {
                // 岸辺の葦・ガマ（浅瀬〜水際）
                dist   = (float)(rng.NextDouble() * pond.radius * 0.28f + pond.radius * 0.68f);
                prefab = prefabs[rng.Next(0, Mathf.Min(6, prefabs.Count))];
                baseY  = pond.waterY - 0.1f; // わずかに水中から生える
            }

            float rad = angle * Mathf.Deg2Rad;
            float wx  = pond.center.x + Mathf.Cos(rad) * dist;
            float wz  = pond.center.z + Mathf.Sin(rad) * dist;

            // 葦・ガマは地形高さから生やす
            float wy;
            if (terrain != null && isPond >= 0.35f)
            {
                float th = terrain.SampleHeight(new Vector3(wx, 0f, wz));
                wy = Mathf.Max(th, baseY);
            }
            else
            {
                wy = baseY;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(root.transform, true);
            go.transform.position = new Vector3(wx, wy, wz);

            // ランダムな向きとサイズバリエーション
            float yRot  = (float)(rng.NextDouble() * 360.0);
            float scale = 0.70f + (float)(rng.NextDouble() * 0.45f);
            go.transform.rotation   = Quaternion.Euler(0f, yRot, 0f);
            go.transform.localScale = Vector3.one * scale;
        }

        Debug.Log("[BeautifyPonds] '" + pond.name + "' に水草 " + plantCount + " 個を配置");
    }
}
