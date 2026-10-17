using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 『Rust & Float』上昇気流（スチーム・ウィンド）マネージャー
/// 島内の要所に白い蒸気と風が勢いよく立ち昇る「スチーム・ウィンド（蒸気上昇気流噴出孔）」を配置。
/// 地上で乗るとドスンと大空へ垂直射出されて自動で滑空が開き、
/// 滑空中に突入すると高度を急上昇させて遠くまで飛べる。
/// </summary>
public class AdventureSteamVentManager : MonoBehaviour
{
    static AdventureSteamVentManager _instance;
    public static AdventureSteamVentManager Instance => _instance;

    public static void Ensure()
    {
        var existingList = FindObjectsByType<AdventureSteamVentManager>(FindObjectsInactive.Include);
        foreach (var ex in existingList)
        {
            if (ex != null && ex.gameObject != null)
            {
                if (Application.isPlaying) Destroy(ex.gameObject);
                else DestroyImmediate(ex.gameObject);
            }
        }
        _instance = null;

        var go = new GameObject("AdventureSteamVentManager");
        if (Application.isPlaying)
            DontDestroyOnLoad(go);
        _instance = go.AddComponent<AdventureSteamVentManager>();
        if (_instance.transform.Find("SteamVentsRoot") == null)
            _instance.BuildAllSteamVents();
    }

    void Awake()
    {
        _instance = this;
        if (transform.Find("SteamVentsRoot") == null)
            BuildAllSteamVents();
    }

    public void BuildAllSteamVents()
    {
        if (transform.Find("SteamVentsRoot") != null) return;

        var land = Terrain.activeTerrain ?? FindAnyObjectByType<Terrain>();
        var root = new GameObject("SteamVentsRoot");
        root.transform.SetParent(transform, false);

        // 1. 西海岸白砂ビーチ（冒険の出発点・岩場）★メガ70m級（島全体への大テイクオフ）
        CreateSteamVent(root.transform, "SteamVent_WestBeach", new Vector3(172f, 0f, 265f), 7.5f, 110f, 56.0f, land, isMega: true);

        // 2. 大草原せせらぎ池畔（低地から一気に台地へ）
        CreateSteamVent(root.transform, "SteamVent_MeadowPond", new Vector3(275f, 0f, 365f), 6.5f, 55f, 19.5f, land, isMega: false);

        // 3. 中央聖域タワー台座麓（タワー中腹・頂上へ直行）★メガ70m級（神聖なタワー天蓋をも超える絶景高度）
        CreateSteamVent(root.transform, "SteamVent_TowerFoot", new Vector3(480f, 0f, 485f), 8.5f, 115f, 56.0f, land, isMega: true);

        // 4. 東部大渓谷の谷底（キャニオン底からの脱出＆復帰）
        CreateSteamVent(root.transform, "SteamVent_CanyonFloor", new Vector3(585f, 0f, 475f), 7.0f, 55f, 20.0f, land, isMega: false);

        // 5. 大カルデラ湖畔オアシス（湖上滑空へのテイクオフ）
        CreateSteamVent(root.transform, "SteamVent_CalderaLake", new Vector3(380f, 0f, 440f), 6.5f, 52f, 19.0f, land, isMega: false);

        // 6. 北の最高峰崖下アプローチ（スカイダイブ台へのショートカット）★メガ70m級（雲海を突き抜けるウルトラハイジャンプ）
        CreateSteamVent(root.transform, "SteamVent_NorthCliffApproach", new Vector3(512f, 0f, 660f), 8.0f, 120f, 58.0f, land, isMega: true);

        // 7. 南岬シーサイドクリフ（海風パノラマビュー）
        CreateSteamVent(root.transform, "SteamVent_SouthCape", new Vector3(195f, 0f, 195f), 6.0f, 52f, 19.0f, land, isMega: false);
    }

    void CreateSteamVent(Transform parent, string name, Vector3 xzPos, float radius, float height, float launchForce, Terrain land, bool isMega = false)
    {
        float groundY = 0f;
        if (land != null)
        {
            groundY = land.SampleHeight(xzPos) + land.transform.position.y;
        }
        else
        {
            groundY = 15f;
        }

        Vector3 pos = new Vector3(xzPos.x, groundY + 0.15f, xzPos.z);

        var ventGo = new GameObject(name);
        ventGo.transform.SetParent(parent, false);
        ventGo.transform.position = pos;

        // 1. 石組みの噴気孔ベース（円形に並んだ石組み＋メガベントなら古代ルーン発光リング）
        BuildVentStoneCrater(ventGo.transform, radius * 0.7f, isMega);

        // 2. モクモク立ち昇る白い蒸気（スチーム）パーティクル
        BuildSteamParticleSystem(ventGo.transform, radius, isMega);

        // 3. 上昇気流（AdventureThermalUpdraft）コンポーネント
        var updraft = ventGo.AddComponent<AdventureThermalUpdraft>();
        updraft.isMegaVent = isMega;
        updraft.radius = radius;
        updraft.height = height;
        updraft.liftSpeed = isMega ? 42.0f : 6.5f;
        updraft.enableGroundLaunch = true;
        updraft.groundLaunchForce = launchForce;
        updraft.pullToCenter = false; // 射出後にプレイヤーが前方へ自由に前進・脱出できるようにする
    }

    /// <summary>石組みの噴気孔クレーター（古代の風孔・自然に地面に馴染む低層デザイン）</summary>
    void BuildVentStoneCrater(Transform parent, float ringRadius, bool isMega)
    {
        var craterRoot = new GameObject("CraterRim");
        craterRoot.transform.SetParent(parent, false);
        craterRoot.transform.localPosition = Vector3.zero;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var rockMat = new Material(shader);
        rockMat.color = isMega ? new Color(0.28f, 0.32f, 0.36f) : new Color(0.35f, 0.36f, 0.34f);
        rockMat.SetFloat("_Smoothness", 0.15f);

        // 円形に並ぶ自然な小石（薄く低層にして地面になじませる）
        int stoneCount = isMega ? 14 : 10;
        for (int i = 0; i < stoneCount; i++)
        {
            float angle = (i / (float)stoneCount) * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * ringRadius, 0.08f, Mathf.Sin(angle) * ringRadius);

            var stone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stone.name = $"RimStone_{i}";
            stone.transform.SetParent(craterRoot.transform, false);
            stone.transform.localPosition = offset;
            stone.transform.localRotation = Quaternion.Euler(Random.Range(-5f, 5f), angle * Mathf.Rad2Deg, Random.Range(-5f, 5f));
            stone.transform.localScale = new Vector3(ringRadius * 0.32f, 0.09f, ringRadius * 0.28f);

            var mr = stone.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = rockMat;

            // コライダーは不要（引っかかり防止）
            var col = stone.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
        }

        // 中心部の通気孔ホール
        var hole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hole.name = "VentHole";
        hole.transform.SetParent(craterRoot.transform, false);
        hole.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        hole.transform.localScale = new Vector3(ringRadius * 1.05f, 0.04f, ringRadius * 1.05f);
        var holeMat = new Material(shader);
        holeMat.color = isMega ? new Color(0.08f, 0.16f, 0.20f) : new Color(0.10f, 0.12f, 0.13f);
        var holeMr = hole.GetComponent<MeshRenderer>();
        if (holeMr != null) holeMr.sharedMaterial = holeMat;
        var holeCol = hole.GetComponent<Collider>();
        if (holeCol != null)
        {
            if (Application.isPlaying) Destroy(holeCol);
            else DestroyImmediate(holeCol);
        }

        // メガベント専用：古代のシアン発光エネルギーリング（ひと目で超上昇気流とわかる特別感）
        if (isMega)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "MegaEnergyRune";
            ring.transform.SetParent(craterRoot.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            ring.transform.localScale = new Vector3(ringRadius * 0.85f, 0.02f, ringRadius * 0.85f);

            var runeMat = new Material(shader);
            Color runeColor = new Color(0.15f, 0.95f, 0.88f);
            runeMat.color = runeColor;
            runeMat.EnableKeyword("_EMISSION");
            runeMat.SetColor("_EmissionColor", runeColor * 2.8f);
            var ringMr = ring.GetComponent<MeshRenderer>();
            if (ringMr != null) ringMr.sharedMaterial = runeMat;

            var rCol = ring.GetComponent<Collider>();
            if (rCol != null)
            {
                if (Application.isPlaying) Destroy(rCol);
                else DestroyImmediate(rCol);
            }
        }
    }

    /// <summary>立ち昇る自然で上品なスチーム（柔らかな蒸気）＆風のパーティクル</summary>
    void BuildSteamParticleSystem(Transform parent, float radius, bool isMega)
    {
        var steamGo = new GameObject("SteamFx");
        steamGo.transform.SetParent(parent, false);
        steamGo.transform.localPosition = new Vector3(0f, 0.2f, 0f);

        var ps = steamGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = isMega ? 3.4f : 2.4f;
        main.startSpeed = isMega ? new ParticleSystem.MinMaxCurve(14f, 24f) : new ParticleSystem.MinMaxCurve(6.5f, 11f); // メガは遥か上空まで噴出
        main.startSize = isMega ? new ParticleSystem.MinMaxCurve(0.4f, 1.1f) : new ParticleSystem.MinMaxCurve(0.28f, 0.65f);
        main.startColor = isMega ? new Color(0.92f, 0.99f, 1.0f, 0.28f) : new Color(0.96f, 0.99f, 1.0f, 0.16f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = isMega ? -0.12f : -0.06f; // 上昇気流
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = isMega ? 38f : 22f; // メガはリッチにモクモク噴出

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * (isMega ? 0.42f : 0.32f);
        shape.rotation = new Vector3(-90f, 0f, 0f); // 真上へ噴出


        // 上昇に伴う穏やかなふんわり拡散
        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.6f),
            new Keyframe(0.35f, 1.0f),
            new Keyframe(1f, 1.4f)
        );
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // 自然なフェードイン＆フェードアウト
        var colOverLife = ps.colorOverLifetime;
        colOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.85f, 0.96f, 1f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.18f, 0.15f), new GradientAlphaKey(0.08f, 0.65f), new GradientAlphaKey(0f, 1f) }
        );
        colOverLife.color = grad;

        // ランダムな自転（ふわふわとゆらぐ回転）
        var rotOverLife = ps.rotationOverLifetime;
        rotOverLife.enabled = true;
        rotOverLife.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);

        // ノイズ（自然なゆらぎ）
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.38f;
        noise.frequency = 0.18f;
        noise.scrollSpeed = 0.25f;

        var rend = steamGo.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            // WhiteSmoke（完全透過シェーダー）を使用し、四角いエッジを完全排除
            var shader = Shader.Find("RustAndFloat/WhiteSmoke");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            var smokeTex = AdventureRustDrone.GetSoftSmokeTexture();
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", smokeTex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", smokeTex);
            mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.35f));

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f); // Alpha
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3100;

            rend.material = mat;
        }
    }
}
