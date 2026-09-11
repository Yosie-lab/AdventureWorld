#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;

public static class AdventureDressRustFloatParadise
{
    const string ScenePath = "Assets/RustAndFlat/Scenes/RustAndFlat.unity";
    const string RootName = "Paradise";
    const string PrefabRoot = "Assets/Idyllic Fantasy Nature/Prefabs/";

    static readonly string[] Trees =
    {
        "BlossomTree_01.prefab",
        "BlossomTree_03.prefab",
        "WillowTree_01_Green.prefab",
        "WillowTree_02_Green.prefab",
        "WillowTree_01_Pink.prefab",
        "BroadleafTree_01_Green.prefab",
        "BroadleafTree_04_Green.prefab",
        "BroadleafTree_02_Green.prefab"
    };

    static readonly string[] Bushes =
    {
        "Bush_01_01.prefab",
        "Bush_02_01.prefab",
        "Bush_03_01.prefab"
    };

    static readonly string[] Flowers =
    {
        "FlowerMeadow_Pink.prefab",
        "FlowerMeadow_White.prefab",
        "FlowerMeadow_Orange.prefab",
        "FlowerMeadow_RedPink.prefab",
        "Flower_Yellow.prefab",
        "Flower_White.prefab"
    };

    static readonly string[] Rocks =
    {
        "Rock_Small_01.prefab",
        "Rock_Small_02.prefab",
        "Rock_Medium_01.prefab",
        "Stone_Medium_01.prefab"
    };

    static readonly string[] ShorePlants =
    {
        "Reeds_01.prefab",
        "Reeds_02.prefab",
        "Waterlily_01.prefab"
    };

    [MenuItem("Adventure/Dress RustAndFloat Paradise")]
    public static void DressFromMenu()
    {
        Dress();
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

        var root = new GameObject(RootName);
        var rng = new System.Random(2050);
        Vector3 spawn = new Vector3(138f, 0f, 176f);
        float water = 5.5f;

        BrightenScene();
        Scatter(root, land, rng, Trees, 48, spawn, 11f, 0.82f, 1.2f, 7.2f);
        Scatter(root, land, rng, Bushes, 90, spawn, 7f, 0.75f, 1.1f, 6.8f);
        Scatter(root, land, rng, Flowers, 70, spawn, 5f, 0.7f, 1.3f, 6.6f);
        PlaceShore(root, land, rng, water, spawn);
        PlaceHeroTrees(root, land);
        PlacePalms(root, land, rng, water, spawn);
        PlaceButterflies(root, land, rng);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[RustAndFloat] 楽園の植生を置きました。件数=" + root.transform.childCount);
    }

    static void Scatter(
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
                if (p.y < minLandAboveWater)
                    continue;

                float dCenter = Horizontal(p, center);
                if (dCenter > 102f || dCenter < 14f)
                    continue;
                if (Horizontal(p, spawn) < spawnClear)
                    continue;
                if (p.z > 184f)
                    continue;

                Vector3 n = td.GetInterpolatedNormal((p.x - origin.x) / size.x, (p.z - origin.z) / size.z);
                if (n.y < 0.72f)
                    continue;
                spots.Add(p);
            }
        }

        for (int i = spots.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (spots[i], spots[j]) = (spots[j], spots[i]);
        }

        int take = Mathf.Min(maxCount, spots.Count);
        for (int i = 0; i < take; i++)
            Place(root, paths[rng.Next(paths.Length)], spots[i], rng, minScale, maxScale);
    }

    static void PlaceShore(GameObject root, Terrain land, System.Random rng, float water, Vector3 spawn)
    {
        var td = land.terrainData;
        Vector3 origin = land.transform.position;
        Vector3 size = td.size;
        Vector3 center = origin + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        int rocks = 0;
        int plants = 0;

        for (int i = 0; i < 90 && rocks < 36; i++)
        {
            float ang = (float)(i / 90f * Mathf.PI * 2f + rng.NextDouble() * 0.08);
            float rad = 96f + (float)rng.NextDouble() * 12f;
            Vector3 p = center + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
            p.y = land.SampleHeight(p) + origin.y;
            if (p.y < water - 0.4f || p.y > water + 4.5f)
                continue;
            if (Horizontal(p, spawn) < 10f)
                continue;
            if (p.z > 188f)
                continue;
            Place(root, Rocks[rng.Next(Rocks.Length)], p, rng, 0.7f, 1.35f);
            rocks++;
        }

        for (int i = 0; i < 80 && plants < 28; i++)
        {
            float ang = (float)(i / 80f * Mathf.PI * 2f + rng.NextDouble() * 0.1);
            float rad = 100f + (float)rng.NextDouble() * 8f;
            Vector3 p = center + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
            p.y = land.SampleHeight(p) + origin.y;
            if (p.y < water - 0.2f || p.y > water + 3.2f)
                continue;
            if (p.z > 188f)
                continue;
            Place(root, ShorePlants[rng.Next(ShorePlants.Length)], p, rng, 0.85f, 1.2f);
            plants++;
        }
    }

    static void PlaceHeroTrees(GameObject root, Terrain land)
    {
        Vector3 origin = land.transform.position;
        Vector3[] spots =
        {
            new Vector3(98f, 0f, 122f),
            new Vector3(158f, 0f, 108f),
            new Vector3(118f, 0f, 148f)
        };
        string[] heroes = { "BlossomTree_01.prefab", "WillowTree_01_Pink.prefab", "BlossomTree_03.prefab" };
        var rng = new System.Random(7);
        for (int i = 0; i < spots.Length; i++)
        {
            Vector3 p = spots[i];
            p.y = land.SampleHeight(p) + origin.y;
            Place(root, heroes[i], p, rng, 1.15f, 1.35f);
        }
    }

    static void BrightenScene()
    {
        var sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude)
            .FirstOrDefault(l => l.type == LightType.Directional);
        if (sun != null)
        {
            sun.intensity = 1.75f;
            sun.color = new Color(1f, 0.97f, 0.86f);
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.78f, 0.9f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.95f, 0.9f, 0.72f);
        RenderSettings.ambientGroundColor = new Color(0.55f, 0.62f, 0.4f);
        RenderSettings.ambientIntensity = 1.25f;
        RenderSettings.fog = false;
    }

    static void PlacePalms(GameObject root, Terrain land, System.Random rng, float water, Vector3 spawn)
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
            if (p.y < water + 0.4f || p.y > water + 8f)
                continue;
            if (Horizontal(p, spawn) < 14f || p.z > 182f)
                continue;
            AdventurePalmFactory.Create(root.transform, p, rng.Next());
            n++;
        }

        Vector3[] inland =
        {
            new Vector3(128f, 0f, 158f),
            new Vector3(152f, 0f, 160f),
            new Vector3(116f, 0f, 154f)
        };
        foreach (var e in inland)
        {
            Vector3 p = e;
            p.y = land.SampleHeight(p) + origin.y;
            AdventurePalmFactory.Create(root.transform, p, rng.Next());
        }
    }

    static void PlaceButterflies(GameObject root, Terrain land, System.Random rng)
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
            new Vector3(118f, 0f, 140f),
            new Vector3(148f, 0f, 132f),
            new Vector3(108f, 0f, 118f),
            new Vector3(132f, 0f, 150f),
            new Vector3(156f, 0f, 148f),
            new Vector3(124f, 0f, 108f)
        };
        foreach (var h in homes)
        {
            Vector3 p = h;
            p.y = land.SampleHeight(p) + origin.y + 1.6f;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + flies[rng.Next(flies.Length)]);
            if (prefab == null)
                continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            go.transform.position = p;
            go.transform.localScale = Vector3.one * 10f;
            var spawn = go.GetComponent<IdyllicFantasyNature.ButterflySpawn>();
            if (spawn != null)
                Object.DestroyImmediate(spawn);
            var anim = go.GetComponent<Animator>();
            if (anim != null)
                anim.enabled = true;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.SetActive(true);
            go.AddComponent<AdventureButterflyDrift>();
        }
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
}
#endif
