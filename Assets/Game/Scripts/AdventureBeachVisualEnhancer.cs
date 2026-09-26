using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 『Rust & Float』白砂ビーチ＆浅瀬の情緒ビジュアル強化システム。
/// 1. 浅瀬の光の揺らめき（コースティクス・網目状の波光投影）
/// 2. 寄せては返す波打ち際の白波ライン（Shoreline Wave & Foam）
/// 3. Nikoが砂浜を歩いたときに残る愛らしい足跡（Sand Footprints & 波による洗い流し）
/// 4. 波打ち際のきらめく潮煙（Sea Spray & Mist）
/// </summary>
public class AdventureBeachVisualEnhancer : MonoBehaviour
{
    public static AdventureBeachVisualEnhancer Instance { get; private set; }

    const float WaterLevelY = 5.50f; // 海面基準水位

    // コースティクス・波マテリアル
    Material _causticsMat;
    Material _shoreWaveMat;
    Material _footprintMat;

    // 海岸メッシュ
    GameObject _causticsRoot;
    GameObject _shoreWaveRoot;
    Mesh _causticsMesh;
    Mesh _shoreWaveMesh;

    // 砂浜足跡プール
    readonly List<FootprintInstance> _activeFootprints = new List<FootprintInstance>();
    class FootprintInstance
    {
        public GameObject go;
        public MeshRenderer renderer;
        public Material material;
        public float spawnTime;
        public float lifetime;
        public float initialAlpha;
        public Vector3 position;
    }

    // Nikoの足跡追跡用
    Vector3 _lastNikoPos;
    float _footstepDistanceCounter = 0f;
    bool _isNextLeftFoot = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        Ensure();
    }

    public static void Ensure()
    {
        if (Instance != null) return;
        var existing = Object.FindFirstObjectByType<AdventureBeachVisualEnhancer>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject("AdventureBeachVisualEnhancer");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AdventureBeachVisualEnhancer>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // 以前のミストパーティクル（白いスクエア拡大の原因）が存在すれば即時破棄
        var oldMist = GameObject.Find("Beach_ShoreMistVFX");
        if (oldMist != null) Destroy(oldMist);

        SetupMaterials();
        BuildCoastMeshes();

        var player = AdventurePlayerController.Instance;
        if (player != null)
            _lastNikoPos = player.transform.position;
    }

    void SetupMaterials()
    {
        var unlitShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                       ?? Shader.Find("Universal Render Pipeline/Unlit")
                       ?? Shader.Find("Particles/Standard Unlit")
                       ?? Shader.Find("Mobile/Particles/Alpha Blended");

        // 1. コースティクスマテリアル（加算ブレンドで白砂に透き通る太陽光の網目を投影）
        _causticsMat = new Material(unlitShader);
        _causticsMat.name = "Beach_Caustics_Mat";
        _causticsMat.mainTexture = GenerateCausticsTexture(256);
        _causticsMat.color = new Color(0.65f, 0.95f, 1.0f, 0.42f);
        // 加算半透明ブレンド
        _causticsMat.SetFloat("_Surface", 1f); // Transparent
        _causticsMat.SetFloat("_Blend", 1f);   // Additive
        _causticsMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _causticsMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        _causticsMat.SetInt("_ZWrite", 0);
        _causticsMat.renderQueue = 3050; // 水面(3000)より直上、地形より前面

        // 2. 波打ち際白波マテリアル（半透明アルファブレンド）
        _shoreWaveMat = new Material(unlitShader);
        _shoreWaveMat.name = "Beach_ShoreWave_Mat";
        _shoreWaveMat.mainTexture = GenerateShoreFoamTexture(256);
        _shoreWaveMat.color = new Color(1f, 1f, 1f, 0.75f);
        _shoreWaveMat.SetFloat("_Surface", 1f);
        _shoreWaveMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _shoreWaveMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _shoreWaveMat.SetInt("_ZWrite", 0);
        _shoreWaveMat.renderQueue = 3060;

        // 3. 足跡マテリアル（砂のくぼみ・影の乗算風半透明）
        _footprintMat = new Material(unlitShader);
        _footprintMat.name = "Beach_Footprint_Mat";
        _footprintMat.mainTexture = GenerateFootprintTexture(128);
        _footprintMat.color = new Color(0.38f, 0.32f, 0.22f, 0.45f);
        _footprintMat.SetFloat("_Surface", 1f);
        _footprintMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _footprintMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _footprintMat.SetInt("_ZWrite", 0);
        _footprintMat.renderQueue = 2950; // 地形直上、水面より下
    }

    /// <summary>
    /// 西側白砂海岸線の地形を二分探索で自動スキャンし、
    /// 水深0m〜2.2mに沿う「浅瀬コースティクスリボン」と「白波フォームリボン」をプロシージャル生成
    /// </summary>
    void BuildCoastMeshes()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        // 西側海岸線（Z: 165m 〜 425m）を細かくサンプリング（全65セグメント）
        float zMin = 165f;
        float zMax = 425f;
        int stepCount = 65;
        float zStep = (zMax - zMin) / stepCount;

        List<Vector3> causticsDeepVerts = new List<Vector3>();
        List<Vector3> causticsMidVerts = new List<Vector3>();
        List<Vector3> causticsShallowVerts = new List<Vector3>();

        List<Vector3> shoreSeaVerts = new List<Vector3>();
        List<Vector3> shoreSandVerts = new List<Vector3>();

        for (int i = 0; i <= stepCount; i++)
        {
            float z = zMin + i * zStep;

            // 二分探索で標高が指定高さになるX座標を高精度特定
            float xDeep = FindXAtTerrainHeight(terrain, z, WaterLevelY - 1.80f, 90f, 240f);    // 水深1.8m（深海側）
            float xMid = FindXAtTerrainHeight(terrain, z, WaterLevelY - 0.70f, 90f, 240f);     // 水深0.7m（浅瀬中央）
            float xShallow = FindXAtTerrainHeight(terrain, z, WaterLevelY - 0.05f, 90f, 240f); // 汀線（水深0m）
            float xSand = FindXAtTerrainHeight(terrain, z, WaterLevelY + 0.45f, 90f, 240f);    // 砂浜（満ち潮到達点）

            // 1. コースティクス用（水深 1.8m 〜 0.05m）
            // 地形表面からわずかに 0.03m 浮かせてZファイティングを防止
            float yDeep = terrain.SampleHeight(new Vector3(xDeep, 0f, z)) + 0.04f;
            float yMid = terrain.SampleHeight(new Vector3(xMid, 0f, z)) + 0.04f;
            float yShallow = terrain.SampleHeight(new Vector3(xShallow, 0f, z)) + 0.04f;

            causticsDeepVerts.Add(new Vector3(xDeep, yDeep, z));
            causticsMidVerts.Add(new Vector3(xMid, yMid, z));
            causticsShallowVerts.Add(new Vector3(xShallow, yShallow, z));

            // 2. 白波フォーム用（水深 0.35m 〜 砂浜標高+0.45m）
            float xWaveSea = FindXAtTerrainHeight(terrain, z, WaterLevelY - 0.35f, 90f, 240f);
            float yWaveSea = Mathf.Max(WaterLevelY - 0.02f, terrain.SampleHeight(new Vector3(xWaveSea, 0f, z)) + 0.03f);
            float ySand = terrain.SampleHeight(new Vector3(xSand, 0f, z)) + 0.04f;

            shoreSeaVerts.Add(new Vector3(xWaveSea, yWaveSea, z));
            shoreSandVerts.Add(new Vector3(xSand, ySand, z));
        }

        // コースティクスメッシュの組み立て（2帯クワッドストリップ）
        BuildCausticsMesh(causticsDeepVerts, causticsMidVerts, causticsShallowVerts);

        // 白波フォームメッシュの組み立て
        BuildShoreWaveMesh(shoreSeaVerts, shoreSandVerts);
    }

    void BuildCausticsMesh(List<Vector3> deep, List<Vector3> mid, List<Vector3> shallow)
    {
        _causticsRoot = new GameObject("Beach_WaterCaustics");
        _causticsRoot.transform.SetParent(transform, false);

        var mf = _causticsRoot.AddComponent<MeshFilter>();
        var mr = _causticsRoot.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _causticsMat;

        _causticsMesh = new Mesh();
        _causticsMesh.name = "Beach_Caustics_Mesh";

        int n = deep.Count;
        Vector3[] verts = new Vector3[n * 3];
        Vector2[] uvs = new Vector2[n * 3];
        Color[] colors = new Color[n * 3];
        List<int> tris = new List<int>();

        for (int i = 0; i < n; i++)
        {
            float v = i * 0.45f;
            verts[i * 3 + 0] = deep[i];
            verts[i * 3 + 1] = mid[i];
            verts[i * 3 + 2] = shallow[i];

            uvs[i * 3 + 0] = new Vector2(0f, v);
            uvs[i * 3 + 1] = new Vector2(0.5f, v);
            uvs[i * 3 + 2] = new Vector2(1f, v);

            // 頂点カラー：深海側と渚側でフェード、中央（浅瀬）で最大輝度、端部は滑らかにフェード
            float edgeFade = Mathf.Sin((float)i / (n - 1) * Mathf.PI);
            colors[i * 3 + 0] = new Color(1f, 1f, 1f, 0.08f * edgeFade);
            colors[i * 3 + 1] = new Color(1f, 1f, 1f, 1.0f * edgeFade);
            colors[i * 3 + 2] = new Color(1f, 1f, 1f, 0.30f * edgeFade);

            if (i < n - 1)
            {
                int r0 = i * 3;
                int r1 = (i + 1) * 3;

                // Strip 1: deep -> mid
                tris.Add(r0 + 0); tris.Add(r1 + 0); tris.Add(r0 + 1);
                tris.Add(r1 + 0); tris.Add(r1 + 1); tris.Add(r0 + 1);

                // Strip 2: mid -> shallow
                tris.Add(r0 + 1); tris.Add(r1 + 1); tris.Add(r0 + 2);
                tris.Add(r1 + 1); tris.Add(r1 + 2); tris.Add(r0 + 2);
            }
        }

        _causticsMesh.vertices = verts;
        _causticsMesh.uv = uvs;
        _causticsMesh.colors = colors;
        _causticsMesh.triangles = tris.ToArray();
        _causticsMesh.RecalculateNormals();
        _causticsMesh.RecalculateBounds();
        mf.sharedMesh = _causticsMesh;
    }

    void BuildShoreWaveMesh(List<Vector3> sea, List<Vector3> sand)
    {
        _shoreWaveRoot = new GameObject("Beach_ShorelineWave");
        _shoreWaveRoot.transform.SetParent(transform, false);

        var mf = _shoreWaveRoot.AddComponent<MeshFilter>();
        var mr = _shoreWaveRoot.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _shoreWaveMat;

        _shoreWaveMesh = new Mesh();
        _shoreWaveMesh.name = "Beach_ShoreWave_Mesh";

        int n = sea.Count;
        Vector3[] verts = new Vector3[n * 2];
        Vector2[] uvs = new Vector2[n * 2];
        Color[] colors = new Color[n * 2];
        List<int> tris = new List<int>();

        for (int i = 0; i < n; i++)
        {
            float v = i * 0.35f;
            verts[i * 2 + 0] = sea[i];
            verts[i * 2 + 1] = sand[i];

            uvs[i * 2 + 0] = new Vector2(0f, v);
            uvs[i * 2 + 1] = new Vector2(1f, v);

            // 南北の端部は滑らかにフェードアウト
            float edgeFade = Mathf.Sin((float)i / (n - 1) * Mathf.PI);
            colors[i * 2 + 0] = new Color(1f, 1f, 1f, 0.02f * edgeFade); // 海側フェードアウト
            colors[i * 2 + 1] = new Color(1f, 1f, 1f, 0.85f * edgeFade); // 砂浜側の波頭（白い泡）

            if (i < n - 1)
            {
                int r0 = i * 2;
                int r1 = (i + 1) * 2;
                tris.Add(r0 + 0); tris.Add(r1 + 0); tris.Add(r0 + 1);
                tris.Add(r1 + 0); tris.Add(r1 + 1); tris.Add(r0 + 1);
            }
        }

        _shoreWaveMesh.vertices = verts;
        _shoreWaveMesh.uv = uvs;
        _shoreWaveMesh.colors = colors;
        _shoreWaveMesh.triangles = tris.ToArray();
        _shoreWaveMesh.RecalculateNormals();
        _shoreWaveMesh.RecalculateBounds();
        mf.sharedMesh = _shoreWaveMesh;
    }

    /// <summary>指定Z座標において、地形標高がtargetYに最も近くなるX座標を二分探索</summary>
    float FindXAtTerrainHeight(Terrain terrain, float z, float targetY, float minX, float maxX)
    {
        float low = minX;
        float high = maxX;
        // 西海岸はXが大きいほど内陸（標高が高い）
        for (int iter = 0; iter < 18; iter++)
        {
            float mid = (low + high) * 0.5f;
            float h = terrain.SampleHeight(new Vector3(mid, 0f, z));
            if (h < targetY)
                low = mid;
            else
                high = mid;
        }
        return (low + high) * 0.5f;
    }



    void Update()
    {
        var strayMist = GameObject.Find("Beach_ShoreMistVFX");
        if (strayMist != null) Destroy(strayMist);

        UpdateWaterAnimations();
        UpdateNikoFootprints();
        CleanExpiredFootprints();
    }

    /// <summary>コースティクス光の揺らぎと波打ち際の寄せては返すアニメーション（夕暮れ反射・夜光虫連動）</summary>
    void UpdateWaterAnimations()
    {
        float t = Time.time;
        float night = AdventureDayNightDirector.NightFactor;
        float sunset = AdventureDayNightDirector.SunsetFactor;

        // 1. コースティクスのUVスクロール（2方向ブレンド感）
        if (_causticsMat != null)
        {
            Vector2 offset = new Vector2(
                Mathf.Repeat(t * 0.045f, 1f),
                Mathf.Repeat(t * 0.028f, 1f)
            );
            _causticsMat.mainTextureOffset = offset;

            // 太陽光の微かなゆらめき輝度変化（夜は月光で微かに透き通り、昼は輝く）
            float glow = 0.38f + 0.08f * Mathf.Sin(t * 1.8f) + 0.04f * Mathf.Cos(t * 2.7f);
            Color causticsBase = Color.Lerp(
                new Color(0.65f, 0.95f, 1.0f),
                new Color(1.0f, 0.80f, 0.55f),
                sunset * 0.6f
            );
            causticsBase = Color.Lerp(causticsBase, new Color(0.25f, 0.50f, 0.85f), night * 0.8f);
            glow *= Mathf.Lerp(1.0f, 0.45f, night);
            _causticsMat.color = new Color(causticsBase.r, causticsBase.g, causticsBase.b, glow);
        }

        // 2. 波打ち際の寄せては返す白波＆夜光虫（周期 約4.2秒）
        if (_shoreWaveMat != null)
        {
            // 寄せる波（急）と引く波（ゆるやか）の非線形ウェーブ
            float wavePhase = (t % 4.2f) / 4.2f;
            float surge = Mathf.SmoothStep(0f, 1f, Mathf.Sin(wavePhase * Mathf.PI));

            // UVオフセットで波が砂浜に押し寄せて引く
            _shoreWaveMat.mainTextureOffset = new Vector2(surge * 0.65f, Mathf.Repeat(t * 0.015f, 1f));

            // 昼は純白、夕暮れは茜色、夜は神秘的な「夜光虫（ネオンシアンの幻想発光）」！
            Color dayWaveColor = new Color(1f, 1f, 1f);
            Color sunsetWaveColor = new Color(1.0f, 0.82f, 0.70f);
            Color bioluminescentColor = new Color(0.18f, 0.95f, 1.0f) * 1.6f; // 夜光虫の青白いネオン発光

            Color curWaveColor = Color.Lerp(dayWaveColor, sunsetWaveColor, sunset);
            curWaveColor = Color.Lerp(curWaveColor, bioluminescentColor, night);

            // 満ちたときに白く/青白く際立ち、引くときに透き通る
            float waveAlpha = Mathf.Lerp(0.15f, 0.85f, surge);
            _shoreWaveMat.color = new Color(curWaveColor.r, curWaveColor.g, curWaveColor.b, waveAlpha);
        }
    }

    /// <summary>Nikoが白砂ビーチを歩いたときの愛らしい足跡生成</summary>
    void UpdateNikoFootprints()
    {
        var player = AdventurePlayerController.Instance;
        if (player == null) return;

        Vector3 curPos = player.transform.position;
        bool isGrounded = player.IsGrounded;
        bool isGliding = player.IsGliding;

        if (!isGrounded || isGliding)
        {
            _lastNikoPos = curPos;
            return;
        }

        Vector3 moveDelta = curPos - _lastNikoPos;
        moveDelta.y = 0f;
        float dist = moveDelta.magnitude;
        _footstepDistanceCounter += dist;
        _lastNikoPos = curPos;

        // 約0.48m歩くごとに片足ずつ足跡スタンプ
        if (_footstepDistanceCounter >= 0.48f)
        {
            _footstepDistanceCounter = 0f;

            // 白砂ビーチエリア判定（標高 5.70m〜7.5m、西側海岸）
            if (CheckIsOnSandBeach(curPos))
            {
                SpawnFootprint(curPos, player.transform.forward, player.transform.right, _isNextLeftFoot);
                _isNextLeftFoot = !_isNextLeftFoot;
            }
        }
    }

    /// <summary>白砂ビーチ領域かどうかの判定</summary>
    public static bool CheckIsOnSandBeach(Vector3 pos)
    {
        // 西側海岸の座標範囲（X: 132〜205, Z: 165〜425, Y: 5.68m〜7.6m）
        if (pos.x >= 132f && pos.x <= 205f && pos.z >= 165f && pos.z <= 425f)
        {
            return pos.y >= 5.68f && pos.y <= 7.6f;
        }
        return false;
    }

    void SpawnFootprint(Vector3 centerPos, Vector3 forward, Vector3 right, bool isLeft)
    {
        // 左右の足の横オフセット（約11cm）
        Vector3 footOffset = (isLeft ? -right : right) * 0.11f;
        Vector3 footPos = centerPos + footOffset;

        var terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            float terrainH = terrain.SampleHeight(footPos);
            footPos.y = terrainH + 0.018f; // 地形表面直上にぴったり配置
        }

        // 足跡用クワッド
        var fpGo = new GameObject("SandFootprint");
        fpGo.transform.SetParent(transform, true);
        fpGo.transform.position = footPos;

        // 歩行進行方向を向く（わずかに左右でハの字に傾ける）
        float angleOffset = isLeft ? -5f : 5f;
        Quaternion rot = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(90f, angleOffset, 0f);
        fpGo.transform.rotation = rot;
        fpGo.transform.localScale = new Vector3(0.18f, 0.28f, 1f); // 楕円の小さな足跡

        var mf = fpGo.AddComponent<MeshFilter>();
        mf.sharedMesh = GetQuadMesh();

        var mr = fpGo.AddComponent<MeshRenderer>();
        var instMat = new Material(_footprintMat);
        mr.sharedMaterial = instMat;

        // 波打ち際に近いほど波ですぐ消える（約3〜7秒）
        float distToWater = Mathf.Max(0f, footPos.y - WaterLevelY);
        float lifetime = Mathf.Lerp(3.2f, 7.5f, Mathf.Clamp01(distToWater / 0.8f));

        var fp = new FootprintInstance
        {
            go = fpGo,
            renderer = mr,
            material = instMat,
            spawnTime = Time.time,
            lifetime = lifetime,
            initialAlpha = 0.46f,
            position = footPos
        };

        _activeFootprints.Add(fp);

        // 夜間（NightFactor > 0.25f）なら、足元から神秘的な青白い夜光虫がポワンと舞い散る
        if (AdventureDayNightDirector.NightFactor > 0.25f)
        {
            SpawnBioluminescentStep(footPos);
        }

        // 最大プール数（36個）を超えたら最も古いものをフェードアウト
        if (_activeFootprints.Count > 36)
        {
            _activeFootprints[0].lifetime = 0.1f;
        }
    }

    /// <summary>夜間に砂浜を踏みしめたときの夜光虫の青白いきらめき飛沫</summary>
    void SpawnBioluminescentStep(Vector3 footPos)
    {
        var bioGo = new GameObject("BioluminescentStep");
        bioGo.transform.SetParent(transform, true);
        bioGo.transform.position = footPos + Vector3.up * 0.05f;

        var ps = bioGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = Random.Range(1.2f, 1.8f);
        main.startSpeed = Random.Range(0.2f, 0.65f);
        main.startSize = Random.Range(0.08f, 0.16f);
        main.startColor = new Color(0.20f, 0.95f, 1.0f, 0.85f); // 鮮やかな夜光虫シアン
        main.maxParticles = 8;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 4, 7) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.18f;

        var rend = bioGo.GetComponent<ParticleSystemRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                  ?? Shader.Find("Particles/Standard Unlit")
                  ?? Shader.Find("Unlit/Color");
        rend.sharedMaterial = new Material(shader) { color = new Color(0.25f, 0.98f, 1.0f, 0.9f) };

        Destroy(bioGo, 2.2f);
    }

    void CleanExpiredFootprints()
    {
        float now = Time.time;
        for (int i = _activeFootprints.Count - 1; i >= 0; i--)
        {
            var fp = _activeFootprints[i];
            float elapsed = now - fp.spawnTime;
            float progress = elapsed / fp.lifetime;

            if (progress >= 1f)
            {
                if (fp.go != null) Destroy(fp.go);
                if (fp.material != null) Destroy(fp.material);
                _activeFootprints.RemoveAt(i);
            }
            else
            {
                // 後半にかけてふんわりと砂に溶けるようにフェードアウト
                float alpha = fp.initialAlpha * Mathf.Clamp01(1f - progress);
                if (fp.material != null)
                {
                    Color c = fp.material.color;
                    c.a = alpha;
                    fp.material.color = c;
                }
            }
        }
    }

    static Mesh _quadMesh;
    static Mesh GetQuadMesh()
    {
        if (_quadMesh != null) return _quadMesh;

        _quadMesh = new Mesh();
        _quadMesh.name = "Footprint_Quad";
        _quadMesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };
        _quadMesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        _quadMesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
        _quadMesh.RecalculateNormals();
        return _quadMesh;
    }

    // ═══════════════════════════════════════════════════════════════════
    // プロシージャルテクスチャ生成（コースティクス・白波泡・足跡）
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>太陽光が水面で屈折して生まれる有機的なコースティクス網目テクスチャ</summary>
    static Texture2D GenerateCausticsTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        Color[] colors = new Color[size * size];

        // 疑似Voronoiセルネットワーク
        int seedPoints = 14;
        Vector2[] points = new Vector2[seedPoints];
        Random.InitState(42);
        for (int i = 0; i < seedPoints; i++)
        {
            points[i] = new Vector2(Random.value, Random.value);
        }

        for (int y = 0; y < size; y++)
        {
            float v = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;
                Vector2 uv = new Vector2(u, v);

                // 最近傍2点との距離比（Voronoiエッジ検出）
                float d1 = float.MaxValue;
                float d2 = float.MaxValue;

                for (int i = 0; i < seedPoints; i++)
                {
                    // トーラス状のシームレス境界距離
                    float dx = Mathf.Abs(uv.x - points[i].x);
                    if (dx > 0.5f) dx = 1f - dx;
                    float dy = Mathf.Abs(uv.y - points[i].y);
                    if (dy > 0.5f) dy = 1f - dy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    if (d < d1)
                    {
                        d2 = d1;
                        d1 = d;
                    }
                    else if (d < d2)
                    {
                        d2 = d;
                    }
                }

                // セルの境界線（網目）部分が鋭く光る
                float edge = d2 - d1;
                float caustics = Mathf.Pow(Mathf.Clamp01(1f - edge * 4.8f), 3.2f);

                // 微細な波紋ノイズブレンド
                float ripple = Mathf.Sin(u * 28f + Mathf.Cos(v * 24f)) * 0.5f + 0.5f;
                caustics = Mathf.Clamp01(caustics * 1.35f + ripple * 0.18f);

                colors[y * size + x] = new Color(1f, 1f, 1f, caustics);
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        return tex;
    }

    /// <summary>波打ち際のふんわりとした白波・泡立ちテクスチャ</summary>
    static Texture2D GenerateShoreFoamTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        Color[] colors = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            float v = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;

                // 多重ノイズによる微細な泡構造
                float n1 = Mathf.PerlinNoise(u * 14f, v * 8f);
                float n2 = Mathf.PerlinNoise(u * 28f + 5.2f, v * 18f + 3.1f);
                float foam = Mathf.Clamp01(n1 * 0.65f + n2 * 0.35f);

                // 波頭（uが1に近いほど砂浜側で濃密）
                float edgeGradient = Mathf.SmoothStep(0f, 1f, u);
                foam = Mathf.Pow(foam, 1.4f) * edgeGradient;

                colors[y * size + x] = new Color(1f, 1f, 1f, foam);
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        return tex;
    }

    /// <summary>砂浜に押された愛らしい小さな足跡（かかと＋つま先）テクスチャ</summary>
    static Texture2D GenerateFootprintTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] colors = new Color[size * size];
        Vector2 centerHeel = new Vector2(0.5f, 0.32f);
        Vector2 centerToe = new Vector2(0.5f, 0.66f);

        for (int y = 0; y < size; y++)
        {
            float v = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;

                // かかと楕円
                float dxH = (u - centerHeel.x) / 0.22f;
                float dyH = (v - centerHeel.y) / 0.24f;
                float distH = dxH * dxH + dyH * dyH;

                // つま先楕円
                float dxT = (u - centerToe.x) / 0.26f;
                float dyT = (v - centerToe.y) / 0.26f;
                float distT = dxT * dxT + dyT * dyT;

                float alphaH = Mathf.Clamp01(1f - Mathf.Sqrt(distH));
                float alphaT = Mathf.Clamp01(1f - Mathf.Sqrt(distT));
                float alpha = Mathf.Max(alphaH, alphaT);

                // 周囲を滑らかにフェード
                alpha = Mathf.SmoothStep(0f, 1f, alpha);

                colors[y * size + x] = new Color(0.35f, 0.28f, 0.18f, alpha);
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        return tex;
    }
}
