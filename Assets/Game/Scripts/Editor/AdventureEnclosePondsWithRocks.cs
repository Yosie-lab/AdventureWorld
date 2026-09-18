using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 2つの池（オアシス湧水池 SanctuarySpringPond / 草原せせらぎ池 MeadowLowlandPond）の岸辺を
/// 天然岩と水草（睡蓮）で囲い込み、自然な池として美化するエディタ拡張。
/// </summary>
public static class AdventureEnclosePondsWithRocks
{
    private static readonly string[] BigRockPaths = new string[]
    {
        "Assets/Idyllic Fantasy Nature/Prefabs/Stone_Big_01.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Stone_Big_02.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Stone_Big_03.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Rock_Big_01.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Rock_Big_02.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Rock_Big_03.prefab"
    };

    private static readonly string[] MediumRockPaths = new string[]
    {
        "Assets/Idyllic Fantasy Nature/Prefabs/Stone_Medium_01.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Stone_Medium_02.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Stone_Medium_03.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Rock_Medium_01.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Rock_Medium_02.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Rock_Medium_03.prefab"
    };

    private static readonly string[] SmallRockPaths = new string[]
    {
        "Assets/Idyllic Fantasy Nature/Prefabs/Rock_Small_01.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Rock_Small_02.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Rock_Small_03.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Stones_01.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Stones_02.prefab"
    };

    private static readonly string[] WaterlilyPaths = new string[]
    {
        "Assets/Idyllic Fantasy Nature/Prefabs/Waterlily_01.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Waterlily_02.prefab"
    };

    [MenuItem("Adventure/🪨 Enclose Ponds With Natural Rocks", false, 20)]
    public static void EnclosePondsMenu()
    {
        ExecuteEnclosePonds(true);
    }

    public static void ExecuteBatch()
    {
        ExecuteEnclosePonds(false);
    }

    public static void ExecuteEnclosePonds(bool interactive)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.Log("[AdventureEnclosePonds] Playモードを停止してEditモードで実行します...");
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.isPlaying = false;
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.isLoaded || string.IsNullOrEmpty(scene.path))
        {
            scene = EditorSceneManager.OpenScene("Assets/RustAndFloat/Scenes/RustAndFloat.unity", OpenSceneMode.Single);
        }

        // プレハブのロード確認
        var bigRocks = LoadPrefabs(BigRockPaths);
        var medRocks = LoadPrefabs(MediumRockPaths);
        var smallRocks = LoadPrefabs(SmallRockPaths);
        var waterlilies = LoadPrefabs(WaterlilyPaths);

        if (bigRocks.Count == 0 && medRocks.Count == 0)
        {
            Debug.LogError("[AdventureEnclosePonds] 岩プレハブが見つかりませんでした。");
            return;
        }

        Terrain terrain = Terrain.activeTerrain;

        // 1. オアシス湧水池 (480, 48.2, 455)
        EncloseSinglePond(
            pondName: "SanctuarySpringPond",
            holderName: "SanctuarySpringPond_Rocks",
            defaultCenter: new Vector3(480f, 48.2f, 455f),
            waterRadius: 16.0f,
            waterY: 48.2f,
            bigRocks: bigRocks,
            medRocks: medRocks,
            smallRocks: smallRocks,
            waterlilies: waterlilies,
            terrain: terrain,
            seed: 42
        );

        // 2. 草原せせらぎ池 (290, 14.5, 320)
        EncloseSinglePond(
            pondName: "MeadowLowlandPond",
            holderName: "MeadowLowlandPond_Rocks",
            defaultCenter: new Vector3(290f, 14.5f, 320f),
            waterRadius: 16.0f,
            waterY: 14.5f,
            bigRocks: bigRocks,
            medRocks: medRocks,
            smallRocks: smallRocks,
            waterlilies: waterlilies,
            terrain: terrain,
            seed: 108
        );

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("<color=#00FFAA><b>[AdventureEnclosePonds]</b> 2つの池（オアシス湧水池・草原せせらぎ池）の岩組み美化が完了し、シーンを正常に保存しました！</color>");

        if (interactive)
        {
            EditorUtility.DisplayDialog("岩組み美化完了", "2つの池の周囲に天然岩と睡蓮を配置し、シーンを保存しました！", "OK");
        }
    }

    private static void EncloseSinglePond(
        string pondName,
        string holderName,
        Vector3 defaultCenter,
        float waterRadius,
        float waterY,
        List<GameObject> bigRocks,
        List<GameObject> medRocks,
        List<GameObject> smallRocks,
        List<GameObject> waterlilies,
        Terrain terrain,
        int seed)
    {
        Random.InitState(seed);

        GameObject pondGo = GameObject.Find(pondName);
        Vector3 center = pondGo != null ? pondGo.transform.position : defaultCenter;
        center.y = waterY;

        // 既存の岩ホルダーをクリーンアップ
        GameObject existingHolder = GameObject.Find(holderName);
        if (existingHolder != null)
        {
            Undo.DestroyObjectImmediate(existingHolder);
        }

        GameObject holder = new GameObject(holderName);
        holder.transform.position = center;
        Undo.RegisterCreatedObjectUndo(holder, "Create " + holderName);

        // A. 主岩盤リング（境界線：半径 15.3m〜16.7m）
        int mainCount = 48;
        for (int i = 0; i < mainCount; i++)
        {
            float angle = (i * 360f / mainCount) + Random.Range(-2.5f, 2.5f);
            float rad = angle * Mathf.Deg2Rad;
            float dist = waterRadius + Random.Range(-0.7f, 0.7f);

            Vector3 pos = center + new Vector3(Mathf.Cos(rad) * dist, 0f, Mathf.Sin(rad) * dist);
            float groundY = GetGroundHeight(pos, terrain, waterY);
            // 水面と地面のうち高い方に合わせ、さらに岩が水面境界を完全に覆うよう高さを微調整
            pos.y = Mathf.Max(waterY - 0.25f, groundY - 0.2f);

            var prefabList = (i % 2 == 0 && bigRocks.Count > 0) ? bigRocks : medRocks;
            GameObject prefab = prefabList[Random.Range(0, prefabList.Count)];
            GameObject rock = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
            rock.transform.position = pos;
            rock.transform.rotation = Quaternion.Euler(
                Random.Range(-8f, 8f),
                Random.Range(0f, 360f),
                Random.Range(-8f, 8f)
            );
            float scale = Random.Range(1.2f, 1.85f);
            rock.transform.localScale = Vector3.one * scale;
        }

        // B. 浅瀬・内側リング（半径 14.3m〜15.3m）
        int innerCount = 28;
        for (int i = 0; i < innerCount; i++)
        {
            float angle = (i * 360f / innerCount) + Random.Range(-4f, 4f);
            float rad = angle * Mathf.Deg2Rad;
            float dist = waterRadius - Random.Range(0.7f, 1.7f);

            Vector3 pos = center + new Vector3(Mathf.Cos(rad) * dist, 0f, Mathf.Sin(rad) * dist);
            float groundY = GetGroundHeight(pos, terrain, waterY);
            pos.y = Mathf.Min(waterY + 0.1f, Mathf.Max(waterY - 0.4f, groundY));

            var prefabList = medRocks.Count > 0 ? medRocks : smallRocks;
            GameObject prefab = prefabList[Random.Range(0, prefabList.Count)];
            GameObject rock = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
            rock.transform.position = pos;
            rock.transform.rotation = Quaternion.Euler(
                Random.Range(-10f, 10f),
                Random.Range(0f, 360f),
                Random.Range(-10f, 10f)
            );
            float scale = Random.Range(0.85f, 1.35f);
            rock.transform.localScale = Vector3.one * scale;
        }

        // C. 外縁リング（半径 16.8m〜18.8m）
        int outerCount = 24;
        for (int i = 0; i < outerCount; i++)
        {
            float angle = (i * 360f / outerCount) + Random.Range(-6f, 6f);
            float rad = angle * Mathf.Deg2Rad;
            float dist = waterRadius + Random.Range(0.8f, 2.8f);

            Vector3 pos = center + new Vector3(Mathf.Cos(rad) * dist, 0f, Mathf.Sin(rad) * dist);
            float groundY = GetGroundHeight(pos, terrain, waterY);
            pos.y = groundY - 0.15f;

            var prefabList = smallRocks.Count > 0 ? smallRocks : medRocks;
            GameObject prefab = prefabList[Random.Range(0, prefabList.Count)];
            GameObject rock = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
            rock.transform.position = pos;
            rock.transform.rotation = Quaternion.Euler(
                Random.Range(-12f, 12f),
                Random.Range(0f, 360f),
                Random.Range(-12f, 12f)
            );
            float scale = Random.Range(0.7f, 1.25f);
            rock.transform.localScale = Vector3.one * scale;
        }

        // D. 睡蓮（Waterlily）の配置（水面に浮かぶ 5〜7 箇所）
        if (waterlilies.Count > 0)
        {
            int lilyCount = Random.Range(5, 8);
            for (int i = 0; i < lilyCount; i++)
            {
                float angle = Random.Range(0f, 360f);
                float rad = angle * Mathf.Deg2Rad;
                float dist = Random.Range(6.0f, waterRadius - 2.5f);

                Vector3 pos = center + new Vector3(Mathf.Cos(rad) * dist, 0.02f, Mathf.Sin(rad) * dist);
                GameObject prefab = waterlilies[Random.Range(0, waterlilies.Count)];
                GameObject lily = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
                lily.transform.position = pos;
                lily.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                lily.transform.localScale = Vector3.one * Random.Range(0.9f, 1.4f);
            }
        }
    }

    private static float GetGroundHeight(Vector3 pos, Terrain terrain, float fallbackY)
    {
        if (terrain != null)
        {
            return terrain.SampleHeight(pos) + terrain.transform.position.y;
        }
        return fallbackY;
    }

    private static List<GameObject> LoadPrefabs(string[] paths)
    {
        var list = new List<GameObject>();
        foreach (var path in paths)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (p != null)
            {
                list.Add(p);
            }
        }
        return list;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.delayCall += () => ExecuteEnclosePonds(false);
        }
    }
}
