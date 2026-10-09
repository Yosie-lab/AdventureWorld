using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 砂浜に佇むチルな焚き火スポット（Campfire Spot）。
/// 1. 流木と丸石で組まれた焚き火炉 ＆ 流木の丸太ベンチ（Driftwood Log Bench）
/// 2. 【Eキー】で点火 ➔ 薪がパチパチとはぜる心地よいASMR音と温かな揺らめく光
/// 3. 【Eキー】で丸太に腰掛ける ➔ Nikoが腰掛け、Rustが隣にちょこんと座って一緒に海と炎を眺める
/// 4. シネマティック・チル視点カメラ ＆ Rustの心温まるつぶやき
/// 5. 移動入力（WASD / Space）で自然に立ち上がり
/// </summary>
public class AdventureBeachCampfire : MonoBehaviour
{
    public static AdventureBeachCampfire Instance { get; private set; }

    [Header("配置座標（スタート地点の白砂ビーチ：渡し板から離れた安全な海辺）")]
    public Vector3 campfirePos = new Vector3(168.0f, 6.3f, 254.0f);

    private bool _isLit = false;
    public bool IsLit => _isLit;

    private bool _isSitting = false;
    public bool IsSitting => _isSitting;

    private Transform _firePit;
    private Transform _logBench;
    private GameObject _flameRoot;
    private Light _fireLight;
    private AudioSource _fireAudio;

    // 多層パーティクルシステム
    private ParticleSystem _mainFlamePS;
    private ParticleSystem _coreFlamePS;
    private ParticleSystem _sparksParticle;
    private ParticleSystem _smokePS;

    // 熾火（おきび）炭レンダラーリスト
    private List<Renderer> _emberRenderers = new List<Renderer>();

    private Vector3 _benchSeatNiko;
    private Vector3 _benchSeatRust;
    private Vector3 _lookDirection;

    private float _chillTimer = 0f;
    private float _nextDialogueTime = 0f;
    private int _dialogueIndex = 0;

    // マテリアルキャッシュ
    private Material _woodMat;
    private Material _stoneMat;
    private Material _ashMat;
    private Material _emberMat;
    private Material _flameTongueMat;
    private Material _flameCoreMat;
    private Material _sparksMat;
    private Material _smokeMat;

    // カメラチル制御用
    private Vector3 _origCamOffset;
    private bool _hasOverriddenCamera = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void AutoEnsure()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName == "RustAndFlat" || sceneName == "RustAndFloat")
        {
            Ensure();
        }
    }

    public static void Ensure()
    {
        if (Instance != null && Instance.gameObject != null) return;
        var existing = Object.FindAnyObjectByType<AdventureBeachCampfire>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject("AdventureBeachCampfire");
        Instance = go.AddComponent<AdventureBeachCampfire>();
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
        InitMaterials();
        AlignToTerrain();
        BuildCampfireStructure();
    }

    void InitMaterials()
    {
        var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        _woodMat = new Material(litShader);
        _woodMat.SetColor("_BaseColor", new Color(0.38f, 0.28f, 0.18f)); // 乾いた流木色
        _woodMat.SetFloat("_Smoothness", 0.18f);

        _stoneMat = new Material(litShader);
        _stoneMat.SetColor("_BaseColor", new Color(0.48f, 0.46f, 0.44f)); // 海岸の丸石
        _stoneMat.SetFloat("_Smoothness", 0.32f);

        _ashMat = new Material(litShader);
        _ashMat.SetColor("_BaseColor", new Color(0.18f, 0.16f, 0.15f)); // 炭・灰
        _ashMat.SetFloat("_Smoothness", 0.10f);

        // 赤熱する熾火（おきび）マテリアル（Emissive）
        _emberMat = new Material(litShader);
        _emberMat.SetColor("_BaseColor", new Color(0.14f, 0.10f, 0.08f));
        _emberMat.EnableKeyword("_EMISSION");
        _emberMat.SetColor("_EmissionColor", new Color(2.4f, 0.55f, 0.08f));
        _emberMat.SetFloat("_Smoothness", 0.15f);

        // プロシージャルテクスチャの生成
        var flameTex = CreateProceduralFlameTexture();
        var smokeTex = CreateProceduralSmokeTexture();
        var sparkTex = CreateProceduralSparkTexture();

        // パーティクル用マテリアル
        _flameTongueMat = CreateParticleMaterial(flameTex, isAdditive: true);
        _flameCoreMat = CreateParticleMaterial(flameTex, isAdditive: true);
        _sparksMat = CreateParticleMaterial(sparkTex, isAdditive: true);
        _smokeMat = CreateParticleMaterial(smokeTex, isAdditive: false);
    }

    Texture2D CreateProceduralFlameTexture()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            float ny = y / (float)size;
            for (int x = 0; x < size; x++)
            {
                float nx = (x / (float)size - 0.5f) * 2f;
                float widthAtY = Mathf.Max(0.001f, (1f - ny * 0.88f) * Mathf.Sin(Mathf.Clamp01(ny + 0.12f) * Mathf.PI));
                float dist = Mathf.Abs(nx) / widthAtY;

                if (dist >= 1f || ny < 0.04f || ny > 0.96f)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                float core = Mathf.Clamp01(1f - dist);
                core = Mathf.Pow(core, 1.8f);

                Color col;
                if (core > 0.65f)
                    col = Color.Lerp(new Color(1f, 0.95f, 0.75f, 1f), Color.white, (core - 0.65f) / 0.35f);
                else if (core > 0.25f)
                    col = Color.Lerp(new Color(1f, 0.45f, 0.08f, 0.95f), new Color(1f, 0.92f, 0.45f, 1f), (core - 0.25f) / 0.4f);
                else
                    col = Color.Lerp(new Color(0.85f, 0.15f, 0.02f, 0f), new Color(1f, 0.42f, 0.08f, 0.9f), core / 0.25f);

                float edgeFade = Mathf.Sin(ny * Mathf.PI);
                col.a *= edgeFade;
                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();
        return tex;
    }

    Texture2D CreateProceduralSmokeTexture()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxR = size * 0.48f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / maxR;
                if (d >= 1f)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else
                {
                    float a = Mathf.SmoothStep(1f, 0f, d);
                    a = Mathf.Pow(a, 2.2f) * 0.65f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
        }
        tex.Apply();
        return tex;
    }

    Texture2D CreateProceduralSparkTexture()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxR = size * 0.45f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / maxR;
                if (d >= 1f)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else
                {
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.5f);
                    tex.SetPixel(x, y, new Color(1f, 0.9f, 0.6f, a));
                }
            }
        }
        tex.Apply();
        return tex;
    }

    Material CreateParticleMaterial(Texture2D tex, bool isAdditive)
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                  ?? Shader.Find("Particles/Standard Unlit")
                  ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader);
        if (tex != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        }

        if (isAdditive)
        {
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 1);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3100;
        }
        else
        {
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
        }
        return mat;
    }

    void AlignToTerrain()
    {
        var land = Terrain.activeTerrain ?? FindAnyObjectByType<Terrain>();
        if (land != null)
        {
            float groundY = land.SampleHeight(campfirePos) + land.transform.position.y;
            campfirePos.y = groundY;
        }
        transform.position = campfirePos;
    }

    void BuildCampfireStructure()
    {
        // 1. 焚き火炉（FirePit）
        var pitGo = new GameObject("FirePit");
        pitGo.transform.SetParent(transform, false);
        pitGo.transform.localPosition = Vector3.zero;
        _firePit = pitGo.transform;

        // 丸石サークル（15個の大きめの丸石が円形に並ぶ：直径約2.5m）
        int stoneCount = 15;
        float pitRadius = 1.25f;
        for (int i = 0; i < stoneCount; i++)
        {
            float rad = (i / (float)stoneCount) * Mathf.PI * 2f;
            Vector3 sPos = new Vector3(Mathf.Cos(rad) * pitRadius, 0.12f, Mathf.Sin(rad) * pitRadius);
            var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stone.name = $"PitStone_{i}";
            stone.transform.SetParent(_firePit, false);
            stone.transform.localPosition = sPos;
            stone.transform.localScale = new Vector3(0.48f, 0.34f, 0.48f);
            stone.transform.rotation = Random.rotation;

            var mr = stone.GetComponent<MeshRenderer>();
            if (mr != null) mr.material = _stoneMat;
            var col = stone.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        // 中央の炭・灰
        var ash = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ash.name = "CampfireAsh";
        ash.transform.SetParent(_firePit, false);
        ash.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        ash.transform.localScale = new Vector3(1.8f, 0.05f, 1.8f);
        var ashMr = ash.GetComponent<MeshRenderer>();
        if (ashMr != null) ashMr.material = _ashMat;
        var ashCol = ash.GetComponent<Collider>();
        if (ashCol != null) Destroy(ashCol);

        // 灰の上に敷き詰められた赤熱する熾火（おきび・Glowing Embers）
        _emberRenderers.Clear();
        int emberCount = 14;
        for (int i = 0; i < emberCount; i++)
        {
            float r = Random.Range(0.12f, 0.68f);
            float a = Random.Range(0f, Mathf.PI * 2f);
            var eb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eb.name = $"EmberCoal_{i}";
            eb.transform.SetParent(_firePit, false);
            eb.transform.localPosition = new Vector3(Mathf.Cos(a) * r, 0.06f + Random.Range(0f, 0.06f), Mathf.Sin(a) * r);
            eb.transform.localScale = new Vector3(Random.Range(0.20f, 0.32f), Random.Range(0.10f, 0.16f), Random.Range(0.20f, 0.32f));
            eb.transform.rotation = Random.rotation;

            var emr = eb.GetComponent<MeshRenderer>();
            if (emr != null)
            {
                emr.material = _emberMat;
                _emberRenderers.Add(emr);
            }
            var col = eb.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        // 円錐状に組まれた薪（8本の立派な流木）
        int logCount = 8;
        for (int i = 0; i < logCount; i++)
        {
            float angle = i * (360f / logCount) + 12f;
            float rad = angle * Mathf.Deg2Rad;
            var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = $"FireLog_{i}";
            log.transform.SetParent(_firePit, false);
            log.transform.localPosition = new Vector3(Mathf.Cos(rad) * 0.46f, 0.30f, Mathf.Sin(rad) * 0.46f);
            log.transform.localScale = new Vector3(0.18f, 0.58f, 0.18f);
            log.transform.rotation = Quaternion.Euler(38f, -angle + 90f, 0f);

            var lmr = log.GetComponent<MeshRenderer>();
            if (lmr != null) lmr.material = _woodMat;
            var lcol = log.GetComponent<Collider>();
            if (lcol != null) Destroy(lcol);
        }

        // 2. リアル多層パーティクル炎＆演出エフェクト
        _flameRoot = new GameObject("FlameVisuals");
        _flameRoot.transform.SetParent(_firePit, false);
        _flameRoot.transform.localPosition = new Vector3(0f, 0.18f, 0f);

        // レイヤー1: メインの炎の舌（有機的に揺らぎ舞い上がる炎）
        _mainFlamePS = CreateParticleEmitter(_flameRoot, "MainFlames", _flameTongueMat);
        SetupMainFlamePS(_mainFlamePS);

        // レイヤー2: 炎の白熱コア（薪の内側から吹き出す高温の炎芯）
        _coreFlamePS = CreateParticleEmitter(_flameRoot, "CoreFlames", _flameCoreMat);
        SetupCoreFlamePS(_coreFlamePS);

        // レイヤー3: 浮遊する火の粉（気流に乗って夜空へ螺旋を描いて昇る）
        _sparksParticle = CreateParticleEmitter(_flameRoot, "FloatingSparks", _sparksMat);
        SetupSparksPS(_sparksParticle);

        // レイヤー4: 立ち上る薄い煙（夜空・夕焼けに溶けていく柔らかな煙）
        _smokePS = CreateParticleEmitter(_flameRoot, "CampfireSmoke", _smokeMat);
        SetupSmokePS(_smokePS);

        // 焚き火ライト（より暖かく広範囲を有機的に照らす）
        var ltGo = new GameObject("CampfireLight");
        ltGo.transform.SetParent(_flameRoot.transform, false);
        ltGo.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        _fireLight = ltGo.AddComponent<Light>();
        _fireLight.type = LightType.Point;
        _fireLight.color = new Color(1.0f, 0.62f, 0.22f);
        _fireLight.intensity = 5.2f;
        _fireLight.range = 19.0f;

        // 焚き火ASMRパチパチ音（8秒シームレス高音質ASMR）
        _fireAudio = _flameRoot.AddComponent<AudioSource>();
        _fireAudio.spatialBlend = 0.85f;
        _fireAudio.minDistance = 3.0f;
        _fireAudio.maxDistance = 30f;
        _fireAudio.rolloffMode = AudioRolloffMode.Linear;
        _fireAudio.loop = true;
        _fireAudio.clip = CreateCracklingAudioClip();
        _fireAudio.volume = 0.72f;

        _flameRoot.SetActive(false);

        // 3. 流木丸太ベンチ（Driftwood Log Bench）
        var benchGo = new GameObject("DriftwoodLogBench");
        benchGo.transform.SetParent(transform, false);
        Vector3 benchOffset = new Vector3(-0.55f, 0.28f, -2.15f);
        benchGo.transform.localPosition = benchOffset;
        _logBench = benchGo.transform;

        // 丸太本体（長さ約3.5m、直径0.58mのどっしりとした流木ベンチ）
        var logMesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        logMesh.name = "BenchLogMesh";
        logMesh.transform.SetParent(_logBench, false);
        logMesh.transform.localPosition = Vector3.zero;
        logMesh.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        logMesh.transform.localScale = new Vector3(0.58f, 1.75f, 0.58f);
        var bmr = logMesh.GetComponent<MeshRenderer>();
        if (bmr != null) bmr.material = _woodMat;

        // 座席位置の定義
        _lookDirection = (transform.position - _logBench.position).normalized;
        _lookDirection.y = 0f;
        _lookDirection = Quaternion.Euler(0f, -15f, 0f) * _lookDirection;

        Vector3 benchWorld = _logBench.position;
        Vector3 right = Vector3.Cross(Vector3.up, _lookDirection).normalized;

        // Nikoの座席（丸太の中央やや左）
        _benchSeatNiko = benchWorld - right * 0.36f + Vector3.up * 0.46f;

        // Rustの座席（Nikoのすぐ右隣 0.72m）
        _benchSeatRust = benchWorld + right * 0.50f + Vector3.up * 0.48f;
    }

    ParticleSystem CreateParticleEmitter(GameObject parentGo, string name, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parentGo.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var psr = go.GetComponent<ParticleSystemRenderer>();
        if (psr != null)
        {
            psr.material = mat;
            psr.sortMode = ParticleSystemSortMode.Distance;
            psr.alignment = ParticleSystemRenderSpace.View;
        }
        return ps;
    }

    void SetupMainFlamePS(ParticleSystem ps)
    {
        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.70f, 1.15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 2.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 0.85f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.rateOverTime = 34f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.22f;
        shape.position = new Vector3(0f, 0.1f, 0f);
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.55f);
        sizeCurve.AddKey(0.35f, 1.0f);
        sizeCurve.AddKey(0.75f, 0.65f);
        sizeCurve.AddKey(1f, 0.15f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.88f, 0.45f), 0.0f),
                new GradientColorKey(new Color(1f, 0.50f, 0.10f), 0.35f),
                new GradientColorKey(new Color(0.95f, 0.22f, 0.04f), 0.75f),
                new GradientColorKey(new Color(0.3f, 0.08f, 0.05f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.2f, 0.0f),
                new GradientAlphaKey(0.85f, 0.2f),
                new GradientAlphaKey(0.6f, 0.7f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        col.color = grad;
    }

    void SetupCoreFlamePS(ParticleSystem ps)
    {
        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.40f, 0.60f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.52f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 50;

        var emission = ps.emission;
        emission.rateOverTime = 22f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.14f;
        shape.position = new Vector3(0f, 0.15f, 0f);
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.7f);
        sizeCurve.AddKey(0.4f, 1.0f);
        sizeCurve.AddKey(1f, 0.2f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0.0f),
                new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0.4f),
                new GradientColorKey(new Color(1f, 0.65f, 0.2f), 0.8f),
                new GradientColorKey(new Color(0.9f, 0.3f, 0.05f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.4f, 0.0f),
                new GradientAlphaKey(0.95f, 0.25f),
                new GradientAlphaKey(0.4f, 0.8f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        col.color = grad;
    }

    void SetupSparksPS(ParticleSystem ps)
    {
        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.8f, 4.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 1.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.10f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 75;

        var emission = ps.emission;
        emission.rateOverTime = 24f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14f;
        shape.radius = 0.32f;
        shape.position = new Vector3(0f, 0.25f, 0f);
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.55f;
        noise.frequency = 0.65f;
        noise.scrollSpeed = 0.6f;
        noise.damping = true;

        var force = ps.forceOverLifetime;
        force.enabled = true;
        force.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.25f);
        force.y = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        force.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        force.space = ParticleSystemSimulationSpace.World;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.9f, 0.45f), 0.0f),
                new GradientColorKey(new Color(1f, 0.65f, 0.20f), 0.5f),
                new GradientColorKey(new Color(0.9f, 0.35f, 0.08f), 0.85f),
                new GradientColorKey(new Color(0.5f, 0.15f, 0.05f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.8f, 0.0f),
                new GradientAlphaKey(1.0f, 0.2f),
                new GradientAlphaKey(0.7f, 0.7f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        col.color = grad;
    }

    void SetupSmokePS(ParticleSystem ps)
    {
        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.8f, 5.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.65f, 1.05f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.40f, 0.65f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;

        var emission = ps.emission;
        emission.rateOverTime = 8.5f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = 0.25f;
        shape.position = new Vector3(0f, 0.65f, 0f);
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.5f);
        sizeCurve.AddKey(0.4f, 1.2f);
        sizeCurve.AddKey(1f, 2.5f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.4f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.40f, 0.38f, 0.42f), 0.0f),
                new GradientColorKey(new Color(0.48f, 0.48f, 0.52f), 0.5f),
                new GradientColorKey(new Color(0.55f, 0.55f, 0.58f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.0f, 0.0f),
                new GradientAlphaKey(0.14f, 0.2f),
                new GradientAlphaKey(0.10f, 0.6f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        col.color = grad;
    }

    /// <summary>薪がはぜる極上ASMRの8秒シームレス高音質プロシージャル音響合成</summary>
    AudioClip CreateCracklingAudioClip()
    {
        int sampleRate = 44100;
        int lengthSamples = sampleRate * 8; // 8秒間のシームレス長尺ループ
        float[] samples = new float[lengthSamples];

        System.Random rng = new System.Random(777);

        // 1. 低周波の温かな燃焼ハミング（Deep Flame Rumble & Air Breath）
        float rumblePhase1 = 0f;
        float rumblePhase2 = 0f;
        float rumbleFreq1 = 82f;
        float rumbleFreq2 = 128f;

        for (int i = 0; i < lengthSamples; i++)
        {
            float t = i / (float)sampleRate;
            float mod = 1.0f + 0.15f * Mathf.Sin(t * 1.8f);

            rumblePhase1 += 2f * Mathf.PI * rumbleFreq1 * mod / sampleRate;
            rumblePhase2 += 2f * Mathf.PI * rumbleFreq2 * mod / sampleRate;

            float rumble = (Mathf.Sin(rumblePhase1) * 0.05f + Mathf.Sin(rumblePhase2) * 0.035f);
            samples[i] = rumble;
        }

        // 2. 連続的な微細チリチリ音（Micro Sizzle / Crackle Texture）
        int microCount = (int)(lengthSamples * 0.012f);
        for (int m = 0; m < microCount; m++)
        {
            int startIdx = rng.Next(lengthSamples);
            float amp = (float)(rng.NextDouble() * 0.045f + 0.015f);
            int dur = rng.Next(20, 60);
            for (int j = 0; j < dur && startIdx + j < lengthSamples; j++)
            {
                float env = 1f - (j / (float)dur);
                samples[startIdx + j] += ((float)rng.NextDouble() * 2f - 1f) * amp * env;
            }
        }

        // 3. 本物の薪のはぜる快音（Resonant Snaps & Pops）
        int popCount = 58;
        for (int p = 0; p < popCount; p++)
        {
            int startIdx = rng.Next(lengthSamples);

            bool isHighSnap = rng.NextDouble() < 0.72;
            float popFreq = isHighSnap ? (float)(rng.NextDouble() * 1400f + 1200f) : (float)(rng.NextDouble() * 400f + 450f);
            float popAmp = isHighSnap ? (float)(rng.NextDouble() * 0.45f + 0.30f) : (float)(rng.NextDouble() * 0.55f + 0.25f);
            float decayMs = isHighSnap ? (float)(rng.NextDouble() * 18f + 16f) : (float)(rng.NextDouble() * 28f + 25f);
            int decaySamples = Mathf.RoundToInt((decayMs / 1000f) * sampleRate);

            for (int j = 0; j < decaySamples; j++)
            {
                int idx = (startIdx + j) % lengthSamples;
                float tj = j / (float)sampleRate;
                float env = Mathf.Exp(-j * 4.5f / decaySamples);
                float sine = Mathf.Sin(2f * Mathf.PI * popFreq * tj);
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.25f;

                samples[idx] += (sine * 0.75f + noise) * popAmp * env;
            }

            // たまに「パパチンッ！」と2連打ではぜるバースト演出
            if (rng.NextDouble() < 0.28)
            {
                int burstOffset = rng.Next(1800, 6000);
                int burstIdx = (startIdx + burstOffset) % lengthSamples;
                float bFreq = popFreq * (float)(rng.NextDouble() * 0.4f + 0.8f);
                float bAmp = popAmp * 0.7f;
                int bDecay = (int)(decaySamples * 0.85f);
                for (int j = 0; j < bDecay; j++)
                {
                    int idx = (burstIdx + j) % lengthSamples;
                    float tj = j / (float)sampleRate;
                    float env = Mathf.Exp(-j * 4.5f / bDecay);
                    float sine = Mathf.Sin(2f * Mathf.PI * bFreq * tj);
                    samples[idx] += sine * bAmp * env;
                }
            }
        }

        // 4. 端部のシームレス・コサインクロスフェード & ウォームリミッティング
        int fadeLen = (int)(sampleRate * 0.15f);
        for (int i = 0; i < fadeLen; i++)
        {
            float t = i / (float)fadeLen;
            float w1 = 0.5f * (1f - Mathf.Cos(t * Mathf.PI));
            float blend = samples[i] * w1 + samples[lengthSamples - fadeLen + i] * (1f - w1);
            samples[i] = blend;
            samples[lengthSamples - fadeLen + i] = blend;
        }

        for (int i = 0; i < lengthSamples; i++)
        {
            float s = samples[i];
            samples[i] = Mathf.Clamp(Mathf.Sin(s * 1.25f) * 0.80f, -0.92f, 0.92f);
        }

        var clip = AudioClip.Create("RealCampfireASMR_Loop", lengthSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    void Update()
    {
        var player = AdventurePlayerController.Instance;
        if (player == null) return;

        // 炎の多重周波数フリッカー ＆ 熾火（おきび）の呼吸パルス
        if (_isLit)
        {
            if (_fireLight != null)
            {
                float fHigh = Mathf.PerlinNoise(Time.time * 15f, 0.0f) * 0.75f;
                float fMid = Mathf.PerlinNoise(Time.time * 3.8f, 7.3f) * 1.25f;
                float fBreathe = Mathf.Sin(Time.time * 1.4f) * 0.55f;
                _fireLight.intensity = 4.4f + fHigh + fMid + fBreathe;

                float lx = Mathf.Sin(Time.time * 5.2f) * 0.07f;
                float lz = Mathf.Cos(Time.time * 4.4f) * 0.07f;
                _fireLight.transform.localPosition = new Vector3(lx, 0.65f + fHigh * 0.05f, lz);
            }

            if (_emberMat != null)
            {
                float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 1.8f) + 0.15f * Mathf.PerlinNoise(Time.time * 2.5f, 0f);
                Color emberCol = new Color(2.4f * pulse, 0.55f * pulse, 0.08f * pulse);
                _emberMat.SetColor("_EmissionColor", emberCol);
            }
        }

        Vector3 pPos = player.transform.position;
        float xzDistToFire = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(transform.position.x, transform.position.z));
        float yDistToFire = Mathf.Abs(pPos.y - transform.position.y);
        bool inFireZone = xzDistToFire < 4.8f && yDistToFire < 3.2f;

        float xzDistToBench = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(_benchSeatNiko.x, _benchSeatNiko.z));
        float yDistToBench = Mathf.Abs(pPos.y - _benchSeatNiko.y);
        bool inBenchZone = xzDistToBench < 3.8f && yDistToBench < 3.2f;

        bool interactPressed = CheckInteractInput();

        if (_isSitting)
        {
            UpdateSittingState(player);
        }
        else
        {
            // 未点火時：焚き火に近づいて【E】または左クリックで点火
            if (!_isLit && inFireZone)
            {
                if (interactPressed)
                {
                    IgniteCampfire();
                }
            }
            // 点火後（またはベンチ付近）：【E】または左クリックで腰掛ける
            else if (inBenchZone)
            {
                if (interactPressed)
                {
                    SitDown(player);
                }
            }
        }
    }

    /// <summary>Eキー、左クリック、ゲームパッドのアクションボタンを包括的に判定</summary>
    bool CheckInteractInput()
    {
        return AdventureInputReader.InteractDown || AdventureInputReader.MouseLeftDown;
    }

    public void IgniteCampfire()
    {
        if (_isLit) return;
        _isLit = true;

        if (_flameRoot != null)
        {
            _flameRoot.SetActive(true);
            if (_mainFlamePS != null) _mainFlamePS.Play();
            if (_coreFlamePS != null) _coreFlamePS.Play();
            if (_sparksParticle != null) _sparksParticle.Play();
            if (_smokePS != null) _smokePS.Play();
        }

        if (_fireAudio != null)
            _fireAudio.Play();

        // 点火時のRustのリアクション
        var drone = AdventureRustDrone.Instance;
        if (drone != null)
        {
            drone.SpeakCustom("わぁ……！火が点いた！すごくあったかいね……！", 4.0f);
        }
    }

    public void SitDown(AdventurePlayerController player)
    {
        if (_isSitting) return;
        _isSitting = true;
        _chillTimer = 0f;
        _nextDialogueTime = 5.0f; // 5秒後に最初のチル台詞
        _dialogueIndex = 0;

        // もし火が点いていなければ自動的に点火
        if (!_isLit)
        {
            IgniteCampfire();
        }

        // 1. Nikoを丸太の上に腰掛けさせる
        player.enabled = false;
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.transform.position = _benchSeatNiko;
        player.transform.rotation = Quaternion.LookRotation(_lookDirection, Vector3.up);

        // 2. Rustを隣の座席に呼んでちょこんと座らせる
        var drone = AdventureRustDrone.Instance;
        if (drone != null)
        {
            drone.SetCampfireChill(true, _benchSeatRust, _lookDirection);
        }

        // 3. カメラをシネマティック・チル視点へ
        var camFollow = Camera.main != null ? Camera.main.GetComponent<AdventureCameraFollow>() : null;
        if (camFollow != null)
        {
            camFollow.SetCinematicMode(true);
            _hasOverriddenCamera = true;
        }
    }

    public void StandUp(AdventurePlayerController player)
    {
        if (!_isSitting) return;
        _isSitting = false;

        // 1. Nikoの操作を復帰
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = true;
        player.enabled = true;

        // ベンチの前に少し進んだ位置に立たせる
        player.transform.position = _benchSeatNiko + _lookDirection * 0.85f;

        // 2. Rustを通常追従に復帰
        var drone = AdventureRustDrone.Instance;
        if (drone != null)
        {
            drone.SetCampfireChill(false);
            drone.SpeakCustom("よいしょ！さぁ、冒険を続けよう！", 3.0f);
        }

        // 3. カメラを通常モードへ復帰
        if (_hasOverriddenCamera)
        {
            var camFollow = Camera.main != null ? Camera.main.GetComponent<AdventureCameraFollow>() : null;
            if (camFollow != null)
            {
                camFollow.SetCinematicMode(false);
            }
            _hasOverriddenCamera = false;
        }
    }

    void UpdateSittingState(AdventurePlayerController player)
    {
        _chillTimer += Time.deltaTime;

        // Nikoの座り位置を固定（アニメーターのアイドル再生）
        player.transform.position = _benchSeatNiko;
        player.transform.rotation = Quaternion.LookRotation(_lookDirection, Vector3.up);

        // 立ち上がり判定（移動キー・ジャンプキー・Eキー・クリック）
        bool movePressed = AdventureInputReader.HasAnyMove || AdventureInputReader.SpaceOrJDown;

        // 座り始め0.5秒以降に立ち上がりを受け付ける
        if (_chillTimer > 0.5f && (movePressed || CheckInteractInput()))
        {
            StandUp(player);
            return;
        }

        // Rustの心温まるチルな呟き（定期的にぽつりぽつりと話す）
        if (_chillTimer >= _nextDialogueTime)
        {
            var drone = AdventureRustDrone.Instance;
            if (drone != null)
            {
                string[] dialogues = {
                    "……あったかいね、Niko。",
                    "パチパチって、なんだか落ち着く音だね……",
                    "波の音がきれいに聴こえるよ……",
                    "ずっとこうして、海を眺めていたいな……",
                    "ふぅ……少し休んでいこう……",
                    "Nikoと一緒にいると、心があったかくなるよ。"
                };

                string msg = dialogues[_dialogueIndex % dialogues.Length];
                drone.SpeakCustom(msg, 5.0f);
                _dialogueIndex++;
                _nextDialogueTime = _chillTimer + Random.Range(16f, 24f);
            }
        }
    }

    void OnGUI()
    {
        var player = AdventurePlayerController.Instance;
        if (player == null) return;

        Vector3 pPos = player.transform.position;
        float xzDistToFire = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(transform.position.x, transform.position.z));
        float yDistToFire = Mathf.Abs(pPos.y - transform.position.y);
        bool inFireZone = xzDistToFire < 4.2f && yDistToFire < 2.8f;

        float xzDistToBench = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(_benchSeatNiko.x, _benchSeatNiko.z));
        float yDistToBench = Mathf.Abs(pPos.y - _benchSeatNiko.y);
        bool inBenchZone = xzDistToBench < 3.2f && yDistToBench < 2.8f;

        var skin = GUI.skin;
        var style = new GUIStyle(skin.label);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 20;
        style.normal.textColor = Color.white;

        // ドロップシャドウ
        var shadow = new GUIStyle(style);
        shadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f);

        if (_isSitting)
        {
            // 座っている時の控えめなプロンプト
            string sitPrompt = "【 W / A / S / D 】または【 Space 】立ち上がる";
            float w = 500f;
            float h = 40f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - 90f;

            GUI.Label(new Rect(x + 1, y + 1, w, h), sitPrompt, shadow);
            GUI.Label(new Rect(x, y, w, h), sitPrompt, style);
        }
        else
        {
            if (!_isLit && inFireZone)
            {
                string prompt = "🔥 【E / クリック】焚き火に火を点ける";
                float w = 420f;
                float h = 40f;
                float x = (Screen.width - w) * 0.5f;
                float y = Screen.height * 0.65f;

                GUI.Label(new Rect(x + 1, y + 1, w, h), prompt, shadow);
                GUI.Label(new Rect(x, y, w, h), prompt, style);
            }
            else if (inBenchZone)
            {
                string prompt = "🪵 【E / クリック】丸太に腰掛ける（海と炎を眺める）";
                float w = 480f;
                float h = 40f;
                float x = (Screen.width - w) * 0.5f;
                float y = Screen.height * 0.65f;

                GUI.Label(new Rect(x + 1, y + 1, w, h), prompt, shadow);
                GUI.Label(new Rect(x, y, w, h), prompt, style);
            }
        }
    }

}
