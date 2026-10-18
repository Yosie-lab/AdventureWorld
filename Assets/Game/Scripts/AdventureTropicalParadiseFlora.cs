using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// 島全体に色彩豊かな南国の花畑と舞い飛ぶ蝶々を一括配置・管理するマネージャー。
/// ・西斜面〜ビーチ境界、中央タワー広場、せせらぎ池・川沿い、東の森林陽だまり、北の高台・崖上など
/// 　島全域（1000m大島）に1000株以上の色鮮やかな花々と80匹以上の蝶々を展開。
/// ・Terrainの高さデータを変更せず、正確な地表高にスナップ。
/// </summary>
public class AdventureTropicalParadiseFlora : MonoBehaviour
{
    private static AdventureTropicalParadiseFlora _instance;
    public static AdventureTropicalParadiseFlora Instance => _instance;

    public const string RootGameObjectName = "Island_Tropical_Flora_Root";
    private const string PrefabRoot = "Assets/Idyllic Fantasy Nature/Prefabs/";

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void OnEditorInit()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            RemoveExistingTropicalButterflies();
        };
    }
#endif

    // 花プレハブ一覧（多彩な色彩の南国花畑）
    private static readonly string[] FlowerPrefabs =
    {
        // レインボー花畑（極彩色）
        "FlowerMeadow_OrangePinkRedPurpleBlue.prefab",
        // 単色・混色花畑
        "FlowerMeadow_Red.prefab",
        "FlowerMeadow_Pink.prefab",
        "FlowerMeadow_Orange.prefab",
        "FlowerMeadow_Blue.prefab",
        "FlowerMeadow_Purple.prefab",
        "FlowerMeadow_White.prefab",
        "FlowerMeadow_RedPink.prefab",
        "FlowerMeadow_RedPurple.prefab",
        "FlowerMeadow_BluePurple.prefab",
        "FlowerMeadow_RedOrange.prefab",
        "FlowerMeadow_PurpleRedPink.prefab",
        // 個別花
        "Flower_Yellow.prefab",
        "Flower_White.prefab",
        "Flower_Blue_01.prefab",
        "Flower_Blue_02.prefab",
        "Flower_Orange.prefab",
        "Flower_Pink.prefab",
        "Flower_Purple.prefab",
        "Flower_Red.prefab",
        "Flower_YellowRed.prefab"
    };

    // 南国の熱帯植物・シダ・大葉
    private static readonly string[] TropicalPlantPrefabs =
    {
        "Plants.prefab",
        "Plant_01.prefab",
        "Plant_02.prefab",
        "Plant_03.prefab",
        "Plant_04.prefab",
        "Plant_05.prefab",
        "Plant_06.prefab",
        "Plant_07.prefab",
        "Plant_08.prefab"
    };

    private static GameObject[] _cachedFlowers;
    private static GameObject[] _cachedTropicalPlants;

    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = Object.FindAnyObjectByType<AdventureTropicalParadiseFlora>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("AdventureTropicalParadiseFlora");
        _instance = go.AddComponent<AdventureTropicalParadiseFlora>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        RemoveExistingTropicalButterflies();
    }

    private void Start()
    {
        RemoveExistingTropicalButterflies();
        BuildTropicalParadiseFloraIfNeeded();
    }

    /// <summary>
    /// 南国の花とともに追加された蝶々（Island_ButterfliesおよびIslandButterfly_*）の設定・オブジェクトを全て削除
    /// </summary>
    public static void RemoveExistingTropicalButterflies()
    {
        // 1. "Island_Butterflies" フォルダオブジェクトの削除
        var butterflyFolder = GameObject.Find("Island_Butterflies");
        if (butterflyFolder != null)
        {
            if (Application.isPlaying) Destroy(butterflyFolder);
            else DestroyImmediate(butterflyFolder);
        }

        // 2. ルート階層またはシーン内から IslandButterfly_* を全て削除
        var root = GameObject.Find(RootGameObjectName);
        if (root != null)
        {
            var bTransforms = new List<Transform>();
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != null && (child.name.StartsWith("IslandButterfly") || child.name == "Island_Butterflies"))
                {
                    bTransforms.Add(child);
                }
            }
            for (int i = bTransforms.Count - 1; i >= 0; i--)
            {
                if (bTransforms[i] != null)
                {
                    if (Application.isPlaying) Destroy(bTransforms[i].gameObject);
                    else DestroyImmediate(bTransforms[i].gameObject);
                }
            }
        }
    }

    private static void PreloadPrefabs()
    {
#if UNITY_EDITOR
        if (_cachedFlowers == null || _cachedFlowers.Length == 0)
        {
            _cachedFlowers = new GameObject[FlowerPrefabs.Length];
            for (int i = 0; i < FlowerPrefabs.Length; i++)
                _cachedFlowers[i] = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + FlowerPrefabs[i]);
        }
        if (_cachedTropicalPlants == null || _cachedTropicalPlants.Length == 0)
        {
            _cachedTropicalPlants = new GameObject[TropicalPlantPrefabs.Length];
            for (int i = 0; i < TropicalPlantPrefabs.Length; i++)
                _cachedTropicalPlants[i] = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + TropicalPlantPrefabs[i]);
        }
#endif
    }

    /// <summary>
    /// 島全体に南国の花々と熱帯植物を一括配置（蝶々の生成は除外）
    /// </summary>
    public static void BuildTropicalParadiseFloraIfNeeded()
    {
        RemoveExistingTropicalButterflies();

        var existingRoot = GameObject.Find(RootGameObjectName);
        if (existingRoot != null && existingRoot.transform.childCount > 10)
        {
            // 既に十分な花畑が存在する場合はスキップ
            return;
        }

        PreloadPrefabs();

        if (existingRoot != null)
        {
            if (Application.isPlaying) Destroy(existingRoot);
            else DestroyImmediate(existingRoot);
        }

        var root = new GameObject(RootGameObjectName);
        var land = Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();
        if (land == null) return;

        var rng = new System.Random(777);

        // 1. 各エリアごとの花畑パッチ中心点（全島網羅・約120拠点）
        List<Vector3> patchCenters = GenerateIslandWidePatchCenters();

        var flowerFolder = new GameObject("Island_FlowerMeadows");
        flowerFolder.transform.SetParent(root.transform, false);

        var tropicalFolder = new GameObject("Island_TropicalPlants");
        tropicalFolder.transform.SetParent(root.transform, false);

        int totalFlowers = 0;
        int totalTropicalPlants = 0;

        for (int i = 0; i < patchCenters.Count; i++)
        {
            Vector3 center = patchCenters[i];
            float groundH = land.SampleHeight(center) + land.transform.position.y;
            // 海中（6.2m未満）や異常高所・断崖はスキップ
            if (groundH < 6.2f || groundH > 180f) continue;

            // 地形の傾斜判定（40度以上の断崖絶壁には咲かせない）
            Vector3 norm = land.terrainData.GetInterpolatedNormal(
                (center.x - land.transform.position.x) / land.terrainData.size.x,
                (center.z - land.transform.position.z) / land.terrainData.size.z);
            if (Vector3.Angle(norm, Vector3.up) > 38f) continue;

            // 各拠点ごとに 6〜12 株の花畑・植物をダイナミックに群生（広大な花畑カーペット化）
            int flowerCount = rng.Next(7, 13);
            for (int f = 0; f < flowerCount; f++)
            {
                float rx = (float)(rng.NextDouble() * 18.0 - 9.0);
                float rz = (float)(rng.NextDouble() * 18.0 - 9.0);
                Vector3 pos = center + new Vector3(rx, 0f, rz);
                float h = land.SampleHeight(pos) + land.transform.position.y;
                if (h < 6.0f) continue;
                pos.y = h;

                // 85%の確率で多彩な花畑、15%の確率で南国シダ/大葉植物
                if (rng.NextDouble() < 0.85)
                {
                    var prefab = GetRandomFlowerPrefab(rng);
                    if (prefab == null) continue;

                    var go = InstantiateObject(prefab, flowerFolder.transform, pos, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                    // 上空からも地上からも華やかに映えるスケール（2.8m〜5.2mの広がり）
                    float scale = 2.8f + (float)rng.NextDouble() * 2.4f;
                    go.transform.localScale = Vector3.one * scale;
                    totalFlowers++;
                }
                else
                {
                    var prefab = GetRandomTropicalPlantPrefab(rng);
                    if (prefab == null) continue;

                    var go = InstantiateObject(prefab, tropicalFolder.transform, pos, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                    float scale = 2.0f + (float)rng.NextDouble() * 1.5f;
                    go.transform.localScale = Vector3.one * scale;
                    totalTropicalPlants++;
                }
            }
        }

        Debug.Log($"🌺 【Tropical Paradise Flora】島全体に色彩豊かな南国の花（{totalFlowers}株）および南国植物（{totalTropicalPlants}株）の配置が完了しました！");
    }

    /// <summary>
    /// 島全体（1000m大島、半径約420m）を網羅する花畑パッチ中心点を生成
    /// </summary>
    private static List<Vector3> GenerateIslandWidePatchCenters()
    {
        var list = new List<Vector3>();

        // A. 中央タワー広場〜大草原（X: 430〜570, Z: 430〜570）
        for (float x = 430; x <= 570; x += 22)
        {
            for (float z = 430; z <= 570; z += 22)
            {
                // タワー直下（半径15m）は除外
                if (Vector2.Distance(new Vector2(x, z), new Vector2(500, 500)) < 16f) continue;
                list.Add(new Vector3(x, 0, z));
            }
        }

        // B. 西の斜面〜白砂ビーチ境界（X: 170〜350, Z: 180〜440）
        for (float x = 170; x <= 350; x += 22)
        {
            for (float z = 180; z <= 440; z += 22)
            {
                list.Add(new Vector3(x, 0, z));
            }
        }

        // C. 南のせせらぎ池・小川・渓谷沿い（X: 260〜540, Z: 180〜440）
        for (float x = 260; x <= 540; x += 24)
        {
            for (float z = 180; z <= 440; z += 24)
            {
                list.Add(new Vector3(x, 0, z));
            }
        }

        // D. 東の森林・高原・木陰（X: 550〜800, Z: 240〜700）
        for (float x = 550; x <= 800; x += 26)
        {
            for (float z = 240; z <= 700; z += 26)
            {
                list.Add(new Vector3(x, 0, z));
            }
        }

        // E. 北の滑空崖下〜高山尾根（X: 360〜660, Z: 600〜800）
        for (float x = 360; x <= 660; x += 25)
        {
            for (float z = 600; z <= 800; z += 25)
            {
                list.Add(new Vector3(x, 0, z));
            }
        }

        // F. 外周の海岸テラス・グリーンベルト（半径350m〜390mの円環帯・内周と外周の2重）
        Vector2 center = new Vector2(500, 500);
        for (int i = 0; i < 48; i++)
        {
            float angle = i * (Mathf.PI * 2f / 48f);
            float r1 = 350f;
            float px1 = center.x + Mathf.Cos(angle) * r1;
            float pz1 = center.y + Mathf.Sin(angle) * r1;
            list.Add(new Vector3(px1, 0, pz1));

            float r2 = 385f;
            float px2 = center.x + Mathf.Cos(angle) * r2;
            float pz2 = center.y + Mathf.Sin(angle) * r2;
            list.Add(new Vector3(px2, 0, pz2));
        }

        return list;
    }

    private static GameObject GetRandomFlowerPrefab(System.Random rng)
    {
        if (_cachedFlowers == null || _cachedFlowers.Length == 0) PreloadPrefabs();
        if (_cachedFlowers == null || _cachedFlowers.Length == 0) return null;
        return _cachedFlowers[rng.Next(_cachedFlowers.Length)];
    }

    private static GameObject GetRandomTropicalPlantPrefab(System.Random rng)
    {
        if (_cachedTropicalPlants == null || _cachedTropicalPlants.Length == 0) PreloadPrefabs();
        if (_cachedTropicalPlants == null || _cachedTropicalPlants.Length == 0) return null;
        return _cachedTropicalPlants[rng.Next(_cachedTropicalPlants.Length)];
    }

    private static GameObject InstantiateObject(GameObject prefab, Transform parent, Vector3 pos, Quaternion rot)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = pos;
            instance.transform.rotation = rot;
            return instance;
        }
#endif
        return Object.Instantiate(prefab, pos, rot, parent);
    }

#if UNITY_EDITOR
    [MenuItem("Adventure/🌺 Island Wide Tropical Flora (島全体に南国の花＆熱帯植物を一括配置)", false, 16)]
    public static void EditorDecorateIslandTropicalFlora()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("停止してください", "■で再生を止めてから実行してください。", "OK");
            return;
        }

        var land = Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();
        if (land == null)
        {
            EditorUtility.DisplayDialog("エラー", "Terrainが見つかりません。", "OK");
            return;
        }

        var existingRoot = GameObject.Find(RootGameObjectName);
        if (existingRoot != null)
        {
            Undo.DestroyObjectImmediate(existingRoot);
        }

        RemoveExistingTropicalButterflies();
        BuildTropicalParadiseFloraIfNeeded();

        var newRoot = GameObject.Find(RootGameObjectName);
        if (newRoot != null)
        {
            Undo.RegisterCreatedObjectUndo(newRoot, "Decorate Island Tropical Flora");
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        Debug.Log("🌺 【Tropical Paradise Flora】シーン保存完了！島全体に南国の花々と熱帯植物が美しく咲き誇りました！");
    }
#endif
}
