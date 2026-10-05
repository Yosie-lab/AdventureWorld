using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 西の斜面（白砂ビーチから内陸大草原へと登る斜面帯）の草地・花畑・蝶々自動生成＆管理マネージャー。
/// 座礁艇（158, 275）から見上げる斜面（X: 175〜250, Z: 200〜360）に、
/// 風にそよぐ豊かな草むら、色とりどりの花畑、そして優雅に舞い飛ぶ蝶々を配置し、
/// 冒険の始まりの探索意欲と景観美を劇的に高める。
/// </summary>
public class AdventureWestSlopeFloraManager : MonoBehaviour
{
    private static AdventureWestSlopeFloraManager _instance;
    public static AdventureWestSlopeFloraManager Instance => _instance;

    public const string RootGameObjectName = "WestSlope_Flora_Root";

    private const string PrefabRoot = "Assets/Idyllic Fantasy Nature/Prefabs/";

    // 草プレハブ
    private static readonly string[] GrassPrefabs =
    {
        "Grass_01.prefab",
        "Grass_02.prefab",
        "Grass_03.prefab"
    };

    // 花プレハブ（ピンク、ホワイト、オレンジ、シアン、イエローなど多彩な野花）
    private static readonly string[] FlowerPrefabs =
    {
        "FlowerMeadow_Pink.prefab",
        "FlowerMeadow_White.prefab",
        "FlowerMeadow_Orange.prefab",
        "FlowerMeadow_BluePurple.prefab",
        "FlowerMeadow_RedPink.prefab",
        "Flower_Yellow.prefab",
        "Flower_White.prefab",
        "Flower_Blue_01.prefab",
        "Flower_Orange.prefab",
        "Flower_Pink.prefab",
        "Flower_Purple.prefab"
    };

    // 蝶プレハブ（黄色、シアン、紅色の舞い飛ぶ蝶）
    private static readonly string[] ButterflyPrefabs =
    {
        "Code Related/Butterfly_01.prefab",
        "Code Related/Butterfly_02.prefab",
        "Code Related/Butterfly_03.prefab"
    };

    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = Object.FindAnyObjectByType<AdventureWestSlopeFloraManager>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("AdventureWestSlopeFloraManager");
        _instance = go.AddComponent<AdventureWestSlopeFloraManager>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void Start()
    {
        BuildWestSlopeFloraIfNeeded();
    }

    /// <summary>西の斜面の草花・蝶々が未生成なら動的に生成・配置</summary>
    public static void BuildWestSlopeFloraIfNeeded()
    {
        var existingRoot = GameObject.Find(RootGameObjectName);
        if (existingRoot != null && existingRoot.transform.childCount > 10)
        {
            // 既に十分な数のオブジェクトが配置されていればスキップ
            return;
        }

        if (existingRoot != null)
        {
            if (Application.isPlaying)
                Destroy(existingRoot);
            else
                DestroyImmediate(existingRoot);
        }

        var root = new GameObject(RootGameObjectName);
        var land = Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();
        if (land == null) return;

        var rng = new System.Random(429);

        // 1. 草地クラスター（斜面全体に約60株の豊かな草むら）
        SpawnSlopeGrassClusters(root.transform, land, rng);

        // 2. 色とりどりの花畑（斜面沿いに約50株の野花パッチ）
        SpawnSlopeFlowerPatches(root.transform, land, rng);

        // 3. 優雅に舞う蝶々（斜面の小道や花畑の上空に12匹の蝶）
        SpawnSlopeButterflies(root.transform, land, rng);

        Debug.Log("[AdventureWestSlopeFloraManager] 🌸 西の斜面に豊かな草地・花畑・蝶々を配置しました");
    }

    /// <summary>草地クラスターの生成（X: 175〜250, Z: 200〜360）</summary>
    private static void SpawnSlopeGrassClusters(Transform root, Terrain land, System.Random rng)
    {
        var grassGroup = new GameObject("GrassClusters");
        grassGroup.transform.SetParent(root, false);

        // 西の斜面の主要エリア（メインスロープ、南西斜面、北西スロープ、尾根沿い）
        Vector3[] slopeAnchors =
        {
            // メイン登り口スロープ（座礁艇正面〜大草原）
            new Vector3(182f, 0f, 275f),
            new Vector3(192f, 0f, 270f),
            new Vector3(198f, 0f, 285f),
            new Vector3(205f, 0f, 278f),
            new Vector3(215f, 0f, 282f),
            new Vector3(225f, 0f, 275f),
            new Vector3(235f, 0f, 285f),

            // 南西アプローチ斜面（南砂浜・川沿い）
            new Vector3(188f, 0f, 235f),
            new Vector3(195f, 0f, 248f),
            new Vector3(202f, 0f, 225f),
            new Vector3(210f, 0f, 240f),
            new Vector3(218f, 0f, 215f),
            new Vector3(225f, 0f, 230f),
            new Vector3(235f, 0f, 245f),

            // 北西テラス・木道沿い斜面
            new Vector3(178f, 0f, 315f),
            new Vector3(185f, 0f, 330f),
            new Vector3(195f, 0f, 310f),
            new Vector3(202f, 0f, 345f),
            new Vector3(212f, 0f, 325f),
            new Vector3(220f, 0f, 350f),
            new Vector3(230f, 0f, 335f),

            // 尾根・大草原接続部（登りきった見晴らし台）
            new Vector3(242f, 0f, 265f),
            new Vector3(245f, 0f, 290f),
            new Vector3(248f, 0f, 315f),
            new Vector3(250f, 0f, 340f),
        };

        for (int i = 0; i < slopeAnchors.Length; i++)
        {
            Vector3 anchor = slopeAnchors[i];
            // 各アンカーの周囲に2〜4個の草を密生クラスタ化
            int countInCluster = rng.Next(2, 4);
            for (int c = 0; c < countInCluster; c++)
            {
                Vector3 pos = anchor + new Vector3((float)(rng.NextDouble() * 7.0 - 3.5), 0f, (float)(rng.NextDouble() * 7.0 - 3.5));
                float h = land.SampleHeight(pos) + land.transform.position.y;
                if (h < 6.2f) continue; // 汀線ギリギリは避ける
                pos.y = h;

                string pName = GrassPrefabs[rng.Next(GrassPrefabs.Length)];
                var prefab = LoadPrefab(pName);
                if (prefab == null) continue;

                var go = InstantiateObject(prefab, grassGroup.transform, pos, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                float scale = 1.4f + (float)rng.NextDouble() * 1.0f;
                go.transform.localScale = Vector3.one * scale;
            }
        }
    }

    /// <summary>花畑パッチの生成（X: 175〜250, Z: 200〜360）</summary>
    private static void SpawnSlopeFlowerPatches(Transform root, Terrain land, System.Random rng)
    {
        var flowerGroup = new GameObject("FlowerPatches");
        flowerGroup.transform.SetParent(root, false);

        Vector3[] flowerAnchors =
        {
            // メインスロープの両脇（草花が道を彩る）
            new Vector3(185f, 0f, 280f),
            new Vector3(190f, 0f, 265f),
            new Vector3(198f, 0f, 275f),
            new Vector3(205f, 0f, 288f),
            new Vector3(212f, 0f, 272f),
            new Vector3(220f, 0f, 285f),
            new Vector3(228f, 0f, 278f),
            new Vector3(236f, 0f, 292f),

            // 南西斜面（陽だまりの野花畑）
            new Vector3(190f, 0f, 240f),
            new Vector3(198f, 0f, 252f),
            new Vector3(205f, 0f, 235f),
            new Vector3(215f, 0f, 250f),
            new Vector3(222f, 0f, 225f),
            new Vector3(230f, 0f, 238f),
            new Vector3(238f, 0f, 255f),

            // 北西斜面（段々池・木道周辺の花壇風パッチ）
            new Vector3(180f, 0f, 320f),
            new Vector3(188f, 0f, 338f),
            new Vector3(198f, 0f, 325f),
            new Vector3(208f, 0f, 340f),
            new Vector3(215f, 0f, 318f),
            new Vector3(225f, 0f, 345f),
            new Vector3(235f, 0f, 330f),

            // 尾根・高原境界（咲き誇る花の群生）
            new Vector3(240f, 0f, 275f),
            new Vector3(245f, 0f, 305f),
            new Vector3(248f, 0f, 330f),
        };

        for (int i = 0; i < flowerAnchors.Length; i++)
        {
            Vector3 anchor = flowerAnchors[i];
            int countInPatch = rng.Next(2, 4);
            for (int p = 0; p < countInPatch; p++)
            {
                Vector3 pos = anchor + new Vector3((float)(rng.NextDouble() * 5.0 - 2.5), 0f, (float)(rng.NextDouble() * 5.0 - 2.5));
                float h = land.SampleHeight(pos) + land.transform.position.y;
                if (h < 6.4f) continue;
                pos.y = h;

                string pName = FlowerPrefabs[rng.Next(FlowerPrefabs.Length)];
                var prefab = LoadPrefab(pName);
                if (prefab == null) continue;

                var go = InstantiateObject(prefab, flowerGroup.transform, pos, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                float scale = 1.1f + (float)rng.NextDouble() * 0.8f;
                go.transform.localScale = Vector3.one * scale;
            }
        }
    }

    /// <summary>優雅に舞う蝶々の配置（X: 180〜245, Z: 215〜350）</summary>
    private static void SpawnSlopeButterflies(Transform root, Terrain land, System.Random rng)
    {
        var butterflyGroup = new GameObject("Butterflies");
        butterflyGroup.transform.SetParent(root, false);

        // 斜面の小道や花畑の上空に点在する蝶々のホーム位置（全12箇所）
        Vector3[] butterflyHomes =
        {
            // メインスロープ沿い（プレイヤーの視界に入る斜面中央）
            new Vector3(188f, 0f, 275f),
            new Vector3(202f, 0f, 282f),
            new Vector3(218f, 0f, 275f),
            new Vector3(232f, 0f, 285f),

            // 南西斜面・小川アプローチ
            new Vector3(195f, 0f, 240f),
            new Vector3(212f, 0f, 230f),
            new Vector3(228f, 0f, 245f),
            new Vector3(205f, 0f, 215f),

            // 北西斜面・木道・段々池アプローチ
            new Vector3(185f, 0f, 325f),
            new Vector3(205f, 0f, 335f),
            new Vector3(222f, 0f, 320f),
            new Vector3(235f, 0f, 345f),
        };

        for (int i = 0; i < butterflyHomes.Length; i++)
        {
            Vector3 pos = butterflyHomes[i];
            float groundY = land.SampleHeight(pos) + land.transform.position.y;
            // 地面から 1.4m〜2.2m 上空をふわりと舞う
            pos.y = groundY + 1.6f + (float)rng.NextDouble() * 0.6f;

            string bName = ButterflyPrefabs[i % ButterflyPrefabs.Length];
            var prefab = LoadPrefab(bName);
            if (prefab == null) continue;

            var go = InstantiateObject(prefab, butterflyGroup.transform, pos, Quaternion.identity);
            go.name = $"SlopeButterfly_{i + 1:D2}";
            go.transform.localScale = Vector3.one * 10f; // Idyllic Butterfly 既定スケール

            // 余分なスポーンスクリプトを除去し、最新の自律飛翔コンポーネントを適用
            var bSpawn = go.GetComponent<IdyllicFantasyNature.ButterflySpawn>();
            if (bSpawn != null)
            {
                if (Application.isPlaying) Destroy(bSpawn);
                else DestroyImmediate(bSpawn);
            }

            var anim = go.GetComponent<Animator>();
            if (anim != null) anim.enabled = true;

            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.SetActive(true);

            var drift = go.GetComponent<AdventureButterflyDrift>() ?? go.AddComponent<AdventureButterflyDrift>();
            drift.radius = 4.0f + (float)rng.NextDouble() * 2.5f;
            drift.speed = 0.65f + (float)rng.NextDouble() * 0.35f;
            drift.bob = 0.55f + (float)rng.NextDouble() * 0.3f;
        }
    }

    private static GameObject LoadPrefab(string relativePath)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + relativePath);
#else
        return null;
#endif
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
    [MenuItem("Adventure/🌸 Decorate West Slope (西の斜面の草花と蝶々の一括配置)", false, 15)]
    public static void EditorDecorateWestSlope()
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

        BuildWestSlopeFloraIfNeeded();

        var newRoot = GameObject.Find(RootGameObjectName);
        if (newRoot != null)
        {
            Undo.RegisterCreatedObjectUndo(newRoot, "Decorate West Slope Flora");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }

        Debug.Log("🌸 【WestSlope】西の斜面に豊かな草地・花畑・蝶々の配置が完了しました！");
    }
#endif
}
