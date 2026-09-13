using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// スタート地点西側の水域を「岩間をせせらぐ小川（川）」と「透き通るオアシス（池）」として徹底美化するエンハンサー
/// 高所から流れ落ちる小川、滝のしぶき、丸みを帯びた池水面、水底の砂利・丸石、岸辺の岩組み・植物、睡蓮、せせらぎ音を構築
/// </summary>
public class AdventureLakeVisualEnhancer : MonoBehaviour
{
    public static AdventureLakeVisualEnhancer Instance { get; private set; }

    const string RootName = "Lake_OasisWaterSystem";
    const float WaterLevelY = 18.15f; // 池の基準水位

    // 湖の中心座標
    static readonly Vector3 LakeCenter = new Vector3(135f, WaterLevelY, 166f);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void Ensure()
    {
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (scene == "RustAndFlat" || scene == "RustAndFloat")
            return;

        if (Instance != null) return;
        var existing = FindAnyObjectByType<AdventureLakeVisualEnhancer>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject("AdventureLakeVisualEnhancer");
        Instance = go.AddComponent<AdventureLakeVisualEnhancer>();
    }

    void Start()
    {
        BuildLakeAndStream();
    }

    public void BuildLakeAndStream()
    {
        var oldRoot = GameObject.Find(RootName);
        if (oldRoot != null)
            Destroy(oldRoot);

        var root = new GameObject(RootName);

        // 1. 高台から池へ注ぎ込む「せせらぎの小川（Stream & Cascades）」
        BuildMountainStream(root.transform);

        // 2. 角張らない自然な円形・楕円のオアシス池水面
        BuildOrganicPondSurface(root.transform);

        // 3. 水底のリアルな川底・砂利・沈み石（草原が透けて見えない処理）
        BuildRiverbedAndPondBottom(root.transform);

        // 4. 水辺の護岸（川石・岩組み・水辺の野草）
        BuildShorelineFloraAndRocks(root.transform);

        // 5. 水面に浮かぶ睡蓮（スイレンの群生）
        BuildWaterlilies(root.transform);

        // 6. 浅瀬を渡れる飛び石（Stepping Stones）
        BuildSteppingStones(root.transform);

        // 7. 太陽光のきらめき ＆ 水音（立体音響）
        BuildSparklesAndAmbience(root.transform);

        // 8. 寄り道のご褒美：オアシス池の温かい上昇気流（サーマル・コラム）
        BuildThermalUpdraft(root.transform);
    }

    // ── 1. せせらぎの小川（Stream & Cascades） ──────────────────
    void BuildMountainStream(Transform parent)
    {
        var streamGroup = new GameObject("Lake_MountainStream");
        streamGroup.transform.SetParent(parent, false);

        Material streamMat = ResolveStreamMaterial();

        // 小川の段差ステップ（スタート高台坂 X:160 から池 X:145 への流路）
        Vector3[] streamSteps = {
            new Vector3(159f, 25.5f, 163.5f), // 最上流の湧き出し岩場
            new Vector3(155f, 23.0f, 162.8f), // 中流の急流
            new Vector3(151f, 20.6f, 162.0f), // 下流の小滝段差
            new Vector3(147f, 18.5f, 161.2f)  // 池への注ぎ口（デルタ）
        };

        // 流れる水面メッシュの配置
        for (int i = 0; i < streamSteps.Length - 1; i++)
        {
            Vector3 pA = streamSteps[i];
            Vector3 pB = streamSteps[i + 1];
            Vector3 mid = (pA + pB) * 0.5f;
            Vector3 dir = (pB - pA).normalized;
            float dist = Vector3.Distance(pA, pB);

            var seg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            seg.name = "StreamSegment_" + i;
            seg.transform.SetParent(streamGroup.transform, false);
            seg.transform.position = mid + Vector3.up * 0.08f;
            seg.transform.rotation = Quaternion.LookRotation(Vector3.down, dir);
            seg.transform.localScale = new Vector3(3.2f, dist * 1.15f, 1f);

            var col = seg.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = seg.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material = streamMat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // 流速アニメーション（上流から下流へザーッと流れる）
            var anim = seg.AddComponent<StreamWaterFlowAnimator>();
            anim.speed = 1.15f;

            // 各段差の水しぶき・泡パーティクル（小さな滝の白波）
            CreateStreamSplash(streamGroup.transform, pB);
        }

        // 小川の両岸を挟む川石・岩組み
        BuildStreamRockBorders(streamGroup.transform, streamSteps);
    }

    void BuildStreamRockBorders(Transform parent, Vector3[] steps)
    {
#if UNITY_EDITOR
        var rSmall1 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Idyllic Fantasy Nature/Prefabs/Rock_Small_01.prefab");
        var rSmall2 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Idyllic Fantasy Nature/Prefabs/Rock_Small_02.prefab");
        var rMed1 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Idyllic Fantasy Nature/Prefabs/Rock_Medium_01.prefab");

        for (int i = 0; i < steps.Length; i++)
        {
            Vector3 pos = steps[i];
            // 左岸
            Vector3 leftBank = pos + new Vector3(0f, 0.15f, 1.8f);
            var rockL = Instantiate((i % 2 == 0 && rMed1 != null) ? rMed1 : rSmall1, parent);
            rockL.name = "StreamRock_L_" + i;
            rockL.transform.position = leftBank;
            rockL.transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
            rockL.transform.localScale = Vector3.one * Random.Range(0.7f, 1.1f);

            // 右岸
            Vector3 rightBank = pos + new Vector3(0f, 0.15f, -1.8f);
            var rockR = Instantiate(rSmall2 != null ? rSmall2 : rSmall1, parent);
            rockR.name = "StreamRock_R_" + i;
            rockR.transform.position = rightBank;
            rockR.transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
            rockR.transform.localScale = Vector3.one * Random.Range(0.65f, 1.05f);
        }
#endif
    }

    void CreateStreamSplash(Transform parent, Vector3 pos)
    {
        var pGo = new GameObject("Splash_" + pos.x);
        pGo.transform.SetParent(parent, false);
        pGo.transform.position = pos + Vector3.up * 0.1f;

        var ps = pGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 0.55f;
        main.startSpeed = 0.85f;
        main.startSize = 0.38f;
        main.startColor = new Color(1f, 1f, 1f, 0.75f); // 泡立つ白い水しぶき
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 16f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.45f;

        var rend = pGo.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.SetTexture("_BaseMap", AdventureRustDrone.GetSoftSmokeTexture());
            mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.8f));
            rend.material = mat;
        }
    }

    // ── 2. 自然な円形・楕円のオアシス池水面（四角い板の完全排除） ────
    void BuildOrganicPondSurface(Transform parent)
    {
        Material waterMat = ResolveLakeMaterial();

        // 1. メイン池の円形有機メッシュ（中心 134, 167、半径 28m の円形ディスク）
        var mainPondMesh = CreateCircleMesh(28f, 26f, 32);
        var mainPondGo = new GameObject("Lake_MainOrganicPond");
        mainPondGo.transform.SetParent(parent, false);
        mainPondGo.transform.position = LakeCenter;
        var mf = mainPondGo.AddComponent<MeshFilter>();
        mf.sharedMesh = mainPondMesh;
        var mr = mainPondGo.AddComponent<MeshRenderer>();
        mr.material = waterMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = true;
        mainPondGo.AddComponent<LakeWaterWaveAnimator>();

        // 2. 小川が注ぎ込む東の入り江（楕円メッシュ：長径 18m、短径 12m）
        var inletMesh = CreateCircleMesh(16f, 11f, 24);
        var inletGo = new GameObject("Lake_EastInletBay");
        inletGo.transform.SetParent(parent, false);
        inletGo.transform.position = new Vector3(146f, WaterLevelY - 0.01f, 162.5f);
        inletGo.transform.rotation = Quaternion.Euler(0f, -25f, 0f);
        var mfInlet = inletGo.AddComponent<MeshFilter>();
        mfInlet.sharedMesh = inletMesh;
        var mrInlet = inletGo.AddComponent<MeshRenderer>();
        mrInlet.material = waterMat;
        mrInlet.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mrInlet.receiveShadows = true;
        inletGo.AddComponent<LakeWaterWaveAnimator>();
    }

    Mesh CreateCircleMesh(float radiusX, float radiusZ, int segments)
    {
        var mesh = new Mesh();
        mesh.name = "PondCircleMesh";

        Vector3[] vertices = new Vector3[segments + 1];
        Vector2[] uvs = new Vector2[segments + 1];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < segments; i++)
        {
            float rad = (float)i / segments * Mathf.PI * 2f;
            float x = Mathf.Cos(rad) * radiusX;
            float z = Mathf.Sin(rad) * radiusZ;
            vertices[i + 1] = new Vector3(x, 0f, z);
            uvs[i + 1] = new Vector2((x / radiusX + 1f) * 0.5f, (z / radiusZ + 1f) * 0.5f);

            int tIdx = i * 3;
            triangles[tIdx] = 0;
            triangles[tIdx + 1] = i + 1;
            triangles[tIdx + 2] = (i + 1 >= segments) ? 1 : (i + 2);
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ── 3. 水底のリアルな川底・砂利・沈み石 ────────────────────
    void BuildRiverbedAndPondBottom(Transform parent)
    {
        var bedGo = new GameObject("Lake_RiverbedPebbles");
        bedGo.transform.SetParent(parent, false);

        // 水底の土台（緑の芝生を覆い隠す暗色の川砂利・湿地マテリアル）
        var bedMesh = CreateCircleMesh(26f, 24f, 24);
        var bedDisc = new GameObject("RiverbedDisc");
        bedDisc.transform.SetParent(bedGo.transform, false);
        bedDisc.transform.position = LakeCenter + Vector3.down * 1.6f; // 水面下1.6m
        var mf = bedDisc.AddComponent<MeshFilter>();
        mf.sharedMesh = bedMesh;
        var mr = bedDisc.AddComponent<MeshRenderer>();

        var bedMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        bedMat.SetColor("_BaseColor", new Color(0.24f, 0.28f, 0.26f)); // 濡れた暗い川底の泥砂利
        bedMat.SetFloat("_Smoothness", 0.65f);
        mr.material = bedMat;

        // 水底に沈む大小の丸石
#if UNITY_EDITOR
        var rSmall = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Idyllic Fantasy Nature/Prefabs/Rock_Small_03.prefab");
        if (rSmall != null)
        {
            Vector3[] bedStones = {
                new Vector3(132f, WaterLevelY - 1.4f, 165f),
                new Vector3(137f, WaterLevelY - 1.2f, 168f),
                new Vector3(135f, WaterLevelY - 1.5f, 162f),
                new Vector3(142f, WaterLevelY - 0.9f, 164f),
                new Vector3(145f, WaterLevelY - 0.6f, 163f),
                new Vector3(128f, WaterLevelY - 1.3f, 167f)
            };

            for (int i = 0; i < bedStones.Length; i++)
            {
                var stone = Instantiate(rSmall, bedGo.transform);
                stone.name = "BedStone_" + i;
                stone.transform.position = bedStones[i];
                stone.transform.rotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), 0f);
                stone.transform.localScale = new Vector3(1.4f, 0.5f, 1.4f);
            }
        }
#endif
    }

    // ── 4. 岸辺の岩組み ＆ 水辺の植物 ────────────────────────
    void BuildShorelineFloraAndRocks(Transform parent)
    {
        var shoreGo = new GameObject("Lake_ShorelineDecor");
        shoreGo.transform.SetParent(parent, false);

#if UNITY_EDITOR
        var rMed1 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Idyllic Fantasy Nature/Prefabs/Rock_Medium_01.prefab");
        var rMed2 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Idyllic Fantasy Nature/Prefabs/Rock_Medium_02.prefab");
        var rSmall1 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Idyllic Fantasy Nature/Prefabs/Rock_Small_01.prefab");
        var plant1 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Idyllic Fantasy Nature/Prefabs/Plant_01.prefab");
        var plant2 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Idyllic Fantasy Nature/Prefabs/Plant_02.prefab");

        // 水際に沿って自然に並ぶ岩組み（汀線の演出）
        Vector3[] shoreRocks = {
            new Vector3(147f, WaterLevelY + 0.35f, 168f),
            new Vector3(144f, WaterLevelY + 0.30f, 174f),
            new Vector3(138f, WaterLevelY + 0.25f, 179f),
            new Vector3(131f, WaterLevelY + 0.30f, 178f),
            new Vector3(123f, WaterLevelY + 0.35f, 173f),
            new Vector3(120f, WaterLevelY + 0.40f, 165f),
            new Vector3(122f, WaterLevelY + 0.35f, 158f),
            new Vector3(129f, WaterLevelY + 0.30f, 154f),
            new Vector3(138f, WaterLevelY + 0.25f, 153f),
            new Vector3(145f, WaterLevelY + 0.30f, 156f)
        };

        for (int i = 0; i < shoreRocks.Length; i++)
        {
            var rockPrefab = (i % 2 == 0 && rMed1 != null) ? rMed1 : (rMed2 != null ? rMed2 : rSmall1);
            if (rockPrefab != null)
            {
                var r = Instantiate(rockPrefab, shoreGo.transform);
                r.name = "ShoreRock_" + i;
                r.transform.position = shoreRocks[i];
                r.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
                r.transform.localScale = Vector3.one * Random.Range(0.85f, 1.25f);
            }

            // 岩の隙間に生える水辺の野草
            var pPrefab = (i % 2 == 0 && plant1 != null) ? plant1 : plant2;
            if (pPrefab != null)
            {
                var p = Instantiate(pPrefab, shoreGo.transform);
                p.name = "ShorePlant_" + i;
                p.transform.position = shoreRocks[i] + new Vector3(Random.Range(-0.8f, 0.8f), 0.1f, Random.Range(-0.8f, 0.8f));
                p.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                p.transform.localScale = Vector3.one * Random.Range(0.8f, 1.3f);
            }
        }
#endif
    }

    // ── 5. 水面に浮かぶ睡蓮（スイレンの群生） ─────────────────
    void BuildWaterlilies(Transform parent)
    {
#if UNITY_EDITOR
        var lily1 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Idyllic Fantasy Nature/Prefabs/Waterlily_01.prefab");
        var lily2 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Idyllic Fantasy Nature/Prefabs/Waterlily_02.prefab");

        Vector3[] clusters = {
            new Vector3(141f, WaterLevelY + 0.02f, 166f),
            new Vector3(143f, WaterLevelY + 0.02f, 169f),
            new Vector3(137f, WaterLevelY + 0.02f, 172f),
            new Vector3(131f, WaterLevelY + 0.02f, 162f),
            new Vector3(128f, WaterLevelY + 0.02f, 169f),
            new Vector3(135f, WaterLevelY + 0.02f, 158f),
            new Vector3(144f, WaterLevelY + 0.02f, 159f)
        };

        for (int i = 0; i < clusters.Length; i++)
        {
            var prefab = (i % 2 == 0 && lily1 != null) ? lily1 : lily2;
            if (prefab == null) continue;

            var l = Instantiate(prefab, parent);
            l.name = "Lake_Waterlily_" + i;
            l.transform.position = clusters[i];
            l.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            l.transform.localScale = Vector3.one * Random.Range(0.9f, 1.3f);
        }
#endif
    }

    // ── 6. 浅瀬を渡れる飛び石（Stepping Stones） ───────────────
    void BuildSteppingStones(Transform parent)
    {
        Vector3[] stones = {
            new Vector3(149f, WaterLevelY + 0.16f, 161.5f),
            new Vector3(146f, WaterLevelY + 0.19f, 163.0f),
            new Vector3(143f, WaterLevelY + 0.22f, 164.8f),
            new Vector3(140f, WaterLevelY + 0.18f, 166.5f),
            new Vector3(137f, WaterLevelY + 0.20f, 168.2f)
        };

        var stoneMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        stoneMat.SetColor("_BaseColor", new Color(0.42f, 0.46f, 0.42f));
        stoneMat.SetFloat("_Smoothness", 0.38f);

        for (int i = 0; i < stones.Length; i++)
        {
            var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stone.name = "Lake_SteppingStone_" + i;
            stone.transform.SetParent(parent, false);
            stone.transform.position = stones[i];
            stone.transform.localScale = new Vector3(1.75f, 0.60f, 1.65f);
            stone.transform.rotation = Quaternion.Euler(Random.Range(-5f, 5f), Random.Range(0f, 360f), Random.Range(-5f, 5f));

            var rend = stone.GetComponent<Renderer>();
            if (rend != null) rend.material = stoneMat;
        }
    }

    // ── 7. 太陽光のきらめき ＆ 水音（立体音響） ─────────────────
    void BuildSparklesAndAmbience(Transform parent)
    {
        // 1. 水面の太陽光きらめき
        var pGo = new GameObject("Lake_SunlightSparkles");
        pGo.transform.SetParent(parent, false);
        pGo.transform.position = LakeCenter + Vector3.up * 0.12f;

        var ps = pGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 2.4f;
        main.startSpeed = 0.06f;
        main.startSize = 0.32f;
        main.startColor = new Color(1.0f, 0.96f, 0.82f, 0.90f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 42f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 24f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        var rend = pGo.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.SetTexture("_BaseMap", AdventureRustDrone.GetSoftSmokeTexture());
            mat.SetColor("_BaseColor", new Color(1.0f, 0.95f, 0.75f, 0.9f));
            rend.material = mat;
        }

        // 2. 清流・せせらぎ音（小川上流部）
        var streamAudioGo = new GameObject("Lake_StreamAudio");
        streamAudioGo.transform.SetParent(parent, false);
        streamAudioGo.transform.position = new Vector3(154f, 22f, 163f);
        var audioStream = streamAudioGo.AddComponent<AudioSource>();
        audioStream.spatialBlend = 0.90f;
        audioStream.minDistance = 5f;
        audioStream.maxDistance = 38f;
        audioStream.volume = 0.45f;
        audioStream.loop = true;
        audioStream.playOnAwake = true;

        // 3. 静かな湖面音（池中央部）
        var lakeAudioGo = new GameObject("Lake_PondAudio");
        lakeAudioGo.transform.SetParent(parent, false);
        lakeAudioGo.transform.position = LakeCenter;
        var audioLake = lakeAudioGo.AddComponent<AudioSource>();
        audioLake.spatialBlend = 0.85f;
        audioLake.minDistance = 8f;
        audioLake.maxDistance = 50f;
        audioLake.volume = 0.38f;
        audioLake.loop = true;
        audioLake.playOnAwake = true;

#if UNITY_EDITOR
        var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/AudioFiles/03_amb/watershore_amb.wav");
        if (clip != null)
        {
            audioStream.clip = clip;
            audioStream.pitch = 1.15f; // せせらぎの軽やかな高音
            audioStream.Play();

            audioLake.clip = clip;
            audioLake.pitch = 0.90f; // 穏やかな低音
            audioLake.Play();
        }
#endif
    }

    Material ResolveLakeMaterial()
    {
#if UNITY_EDITOR
        var sourceMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Idyllic Fantasy Nature/Demo/Materials/Lake.mat");
        if (sourceMat != null)
            return new Material(sourceMat);
#endif
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(shader);
        m.SetColor("_BaseColor", new Color(0.12f, 0.65f, 0.78f, 0.72f));
        m.SetFloat("_Smoothness", 0.95f);
        m.SetFloat("_Metallic", 0.08f);
        m.SetFloat("_Surface", 1);
        m.SetFloat("_Blend", 0);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.renderQueue = 3000;
        return m;
    }

    Material ResolveStreamMaterial()
    {
#if UNITY_EDITOR
        var sourceMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Water/WaterStream.mat");
        if (sourceMat != null)
            return new Material(sourceMat);
#endif
        return ResolveLakeMaterial();
    }

    // ── 8. 寄り道のご褒美：オアシス池の温かい上昇気流（サーマル・コラム） ────
    void BuildThermalUpdraft(Transform parent)
    {
        var ventGo = new GameObject("Lake_ThermalUpdraftVent");
        ventGo.transform.SetParent(parent, false);
        ventGo.transform.position = LakeCenter + Vector3.up * 0.3f; // 池中央

        // 立ち昇る風と光のサーマルパーティクル
        var ps = ventGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 3.2f;
        main.startSpeed = 4.8f;
        main.startSize = 0.45f;
        main.startColor = new Color(0.35f, 0.95f, 0.90f, 0.75f); // 澄んだシアンと光の風
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 28f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 4.5f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // 真上へ噴出

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.orbitalZ = 1.8f; // らせん状に美しく舞い上がる

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.4f, 0.98f, 0.9f), 0f), new GradientColorKey(new Color(1f, 0.92f, 0.5f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.2f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        var rend = ventGo.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.SetTexture("_BaseMap", AdventureRustDrone.GetSoftSmokeTexture());
            rend.material = mat;
        }

        // 上昇気流トリガーコンポーネント
        ventGo.AddComponent<OasisThermalVent>();
    }
}

/// <summary>
/// 小川の水流を高速スクロールさせ、躍動感あるせせらぎを演出するコンポーネント
/// </summary>
public class StreamWaterFlowAnimator : MonoBehaviour
{
    public float speed = 1.0f;
    Renderer _rend;
    Material _mat;
    Vector2 _offset;

    void Start()
    {
        _rend = GetComponent<Renderer>();
        if (_rend != null)
            _mat = _rend.material;
    }

    void Update()
    {
        if (_mat == null) return;
        _offset.y += Time.deltaTime * speed;

        if (_mat.HasProperty("_BaseMap"))
            _mat.SetTextureOffset("_BaseMap", _offset);
        if (_mat.HasProperty("_BumpMap"))
            _mat.SetTextureOffset("_BumpMap", _offset);
    }
}

/// <summary>
/// 水面のテクスチャオフセットを滑らかにスクロールさせ、光の反射・波紋をアニメーションさせるコンポーネント
/// </summary>
public class LakeWaterWaveAnimator : MonoBehaviour
{
    Renderer _rend;
    Material _mat;
    Vector2 _offset;

    void Start()
    {
        _rend = GetComponent<Renderer>();
        if (_rend != null)
            _mat = _rend.material;
    }

    void Update()
    {
        if (_mat == null) return;
        float speed = 0.045f;
        _offset.x += Time.deltaTime * speed;
        _offset.y += Time.deltaTime * (speed * 0.7f);

        if (_mat.HasProperty("_BaseMap"))
            _mat.SetTextureOffset("_BaseMap", _offset);
        if (_mat.HasProperty("_BumpMap"))
            _mat.SetTextureOffset("_BumpMap", _offset);
    }
}

/// <summary>
/// 寄り道したプレイヤーを温かい上昇気流（サーマル）でふわりと大空へ舞い上がらせるコンポーネント
/// </summary>
public class OasisThermalVent : MonoBehaviour
{
    float _lastLiftTime = -10f;
    float _lastCheerTime = -20f;

    void Update()
    {
        var player = AdventurePlayerController.Instance;
        if (player == null) return;

        Vector3 diff = player.transform.position - transform.position;
        float hDist = new Vector2(diff.x, diff.z).magnitude;
        float vDist = diff.y; // 気流の高さ（0m〜16m）

        // 半径5.5m、高度0〜16mのサーマルコラム
        if (hDist < 5.5f && vDist >= -0.5f && vDist < 16f)
        {
            if (Time.time - _lastLiftTime > 0.25f)
            {
                _lastLiftTime = Time.time;
                player.ApplyGlideBoost(1.4f, 3.0f, Vector3.up);
                player.ApplyUpdraft(6.8f);

                // 相棒Rustの歓喜
                if (Time.time - _lastCheerTime > 12f)
                {
                    _lastCheerTime = Time.time;
                    var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
                    if (drone != null)
                    {
                        drone.SpeakCustom("わぁっ！温かい上昇気流だ！風に乗って行こう、Niko！", 4.5f);
                    }
                }
            }
        }
    }
}

