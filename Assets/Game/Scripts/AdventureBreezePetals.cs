using UnityEngine;

/// <summary>
/// 風に舞う花びら・木漏れ日綿毛エフェクト（World Atmosphere ➔ 満点）
/// 西斜面や中央高原の花畑、オアシスの木立ちの上空を通過した際、
/// 淡い桜色の花びらや琥珀色の綿毛微粒子が風に乗って舞い、ジブリ作品のような瑞々しい空気感を演出。
/// </summary>
public class AdventureBreezePetals : MonoBehaviour
{
    private static AdventureBreezePetals _instance;
    public static AdventureBreezePetals Instance => _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void AutoInitialize()
    {
        if (_instance != null) return;
        var existing = Object.FindAnyObjectByType<AdventureBreezePetals>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("AdventureBreezePetals");
        _instance = go.AddComponent<AdventureBreezePetals>();
        DontDestroyOnLoad(go);
    }

    private Transform _playerTransform;
    private ParticleSystem _petalParticleSystem;
    private ParticleSystem _pollenParticleSystem;
    private Texture2D _petalTexture;
    private Texture2D _pollenTexture;
    private Material _petalMaterial;
    private Material _pollenMaterial;

    // 花畑密集エリアのアンカー中心群（西斜面〜高原〜オアシス）
    private static readonly Vector3[] FlowerMeadowCenters = new Vector3[]
    {
        new Vector3(210f, 0f, 260f), // 西斜面南
        new Vector3(205f, 0f, 325f), // 西斜面北
        new Vector3(290f, 0f, 280f), // 中央高原
        new Vector3(295f, 0f, 350f), // せせらぎ池
        new Vector3(474f, 0f, 460f), // オアシス木陰
        new Vector3(300f, 0f, 400f)  // 北部高台
    };

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        CreateProceduralTextures();
        SetupParticleSystems();
    }

    void Start()
    {
        FindPlayer();
    }

    void FindPlayer()
    {
        if (_playerTransform == null)
        {
            var p = GameObject.Find("Niko") ?? GameObject.FindGameObjectWithTag("Player");
            if (p != null) _playerTransform = p.transform;
        }
    }

    void Update()
    {
        if (_playerTransform == null)
        {
            FindPlayer();
            if (_playerTransform == null) return;
        }

        Vector3 playerPos = _playerTransform.position;
        transform.position = playerPos;

        // 花畑からの最短距離を判定
        float minFlowerDist = float.MaxValue;
        for (int i = 0; i < FlowerMeadowCenters.Length; i++)
        {
            Vector3 center = FlowerMeadowCenters[i];
            float dist = Vector2.Distance(new Vector2(playerPos.x, playerPos.z), new Vector2(center.x, center.z));
            if (dist < minFlowerDist) minFlowerDist = dist;
        }

        // 花畑が近いほど（< 60m）花びら・綿毛の放出レートが上昇
        float flowerProximity = Mathf.Clamp01(1f - (minFlowerDist / 55f));

        // 海抜チェック（海面スレスレや沖合では控えめに）
        bool isOverIsland = playerPos.y > 6.0f && playerPos.x > 140f && playerPos.x < 580f && playerPos.z > 140f && playerPos.z < 620f;
        // 基本はごくたまにふわりと舞う程度（0.4枚/秒）、花畑の上でも最大3枚/秒程度の上品な量に制限
        float baseEmission = isOverIsland ? 0.45f : 0.08f;

        if (_petalParticleSystem != null)
        {
            var em = _petalParticleSystem.emission;
            em.rateOverTime = baseEmission + flowerProximity * 2.8f;
        }

        if (_pollenParticleSystem != null)
        {
            var em = _pollenParticleSystem.emission;
            em.rateOverTime = (baseEmission * 0.6f) + flowerProximity * 2.0f;
        }
    }

    private void SetupParticleSystems()
    {
        // 1. 花びら（桜色・淡いコーラルピンク）パーティクル
        var petalGo = new GameObject("Petals_PS");
        petalGo.transform.SetParent(transform, false);
        _petalParticleSystem = petalGo.AddComponent<ParticleSystem>();
        ConfigurePetalsPS(_petalParticleSystem, _petalMaterial);

        // 2. 琥珀色・金色の木漏れ日綿毛（Pollen）パーティクル
        var pollenGo = new GameObject("Pollen_PS");
        pollenGo.transform.SetParent(transform, false);
        _pollenParticleSystem = pollenGo.AddComponent<ParticleSystem>();
        ConfigurePollenPS(_pollenParticleSystem, _pollenMaterial);
    }

    private void ConfigurePetalsPS(ParticleSystem ps, Material mat)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = true;
        main.loop = true;
        main.duration = 4.0f;
        main.startLifetime = 3.8f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 22; // 画面全体で最大22枚に制限し、視界を塞がず上品に舞う

        // 多彩な花びらカラー（ピンク・白・黄色）のランダムサンプリング
        var colorGrad = new Gradient();
        colorGrad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.62f, 0.76f), 0.00f),    // 1. 可憐な桜色・ローズピンク
                new GradientColorKey(new Color(0.98f, 0.99f, 1.00f), 0.35f),  // 2. 清らかなシルキーホワイト（白）
                new GradientColorKey(new Color(1.00f, 0.91f, 0.26f), 0.70f),  // 3. 陽だまりの菜の花イエロー（黄）
                new GradientColorKey(new Color(1.00f, 0.80f, 0.50f), 1.00f),  // 4. 温かなアプリコットクリーム
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        );
        main.startColor = new ParticleSystem.MinMaxGradient(colorGrad);

        var emission = ps.emission;
        emission.rateOverTime = 1.2f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(14f, 6f, 14f);
        shape.position = new Vector3(0f, 1.5f, 4f); // プレイヤーの少し前方上空

        // 風による揺らぎ
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.6f, 1.8f); // 島のそよ風（東〜北東向き）
        vel.y = new ParticleSystem.MinMaxCurve(-0.4f, 0.2f);
        vel.z = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);

        // 回転アニメーション
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-1.8f, 1.8f);

        // フェードイン・アウト（RGBは白のままでstartColorのピンク・白・黄色を100%発色）
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.90f, 0.22f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.material = mat;
        rend.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play();
    }

    private void ConfigurePollenPS(ParticleSystem ps, Material mat)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = true;
        main.loop = true;
        main.duration = 3.5f;
        main.startLifetime = 3.2f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 16;

        var emission = ps.emission;
        emission.rateOverTime = 1.0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 8.0f;
        shape.position = new Vector3(0f, 1.0f, 2f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.92f, 0.45f), 0f), new GradientColorKey(new Color(1f, 0.75f, 0.3f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.material = mat;
        rend.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play();
    }

    private void CreateProceduralTextures()
    {
        int size = 64;

        // 1. 花びらテクスチャ（純白アルファマスクの滑らかなオーバル花弁）
        _petalTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        _petalTexture.filterMode = FilterMode.Bilinear;
        Color transparent = new Color(1f, 1f, 1f, 0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size - 0.5f;
                float v = (float)y / size - 0.5f;

                // 楕円花弁（縦長で基部が少し細い涙型）
                float shapeFactor = 1.0f - v * 0.4f;
                float dist = (u * u) / (0.16f * shapeFactor) + (v * v) / 0.18f;

                if (dist <= 1.0f)
                {
                    float alpha = Mathf.Clamp01((1.0f - dist) * 4.5f);
                    _petalTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    _petalTexture.SetPixel(x, y, transparent);
                }
            }
        }
        _petalTexture.Apply();

        // 2. 綿毛・花粉テクスチャ（ソフトグロー円）
        _pollenTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        _pollenTexture.filterMode = FilterMode.Bilinear;
        Color gold = new Color(1f, 0.90f, 0.45f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (float)x / size - 0.5f;
                float dy = (float)y / size - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                if (d <= 1.0f)
                {
                    float alpha = Mathf.Pow(1.0f - d, 2.0f);
                    _pollenTexture.SetPixel(x, y, new Color(gold.r, gold.g, gold.b, alpha));
                }
                else
                {
                    _pollenTexture.SetPixel(x, y, transparent);
                }
            }
        }
        _pollenTexture.Apply();

        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Sprites/Default");

        _petalMaterial = new Material(shader) { name = "BreezePetal_Mat" };
        _petalMaterial.mainTexture = _petalTexture;
        SetupMaterial(_petalMaterial);

        _pollenMaterial = new Material(shader) { name = "BreezePollen_Mat" };
        _pollenMaterial.mainTexture = _pollenTexture;
        SetupMaterial(_pollenMaterial);
    }

    private void SetupMaterial(Material mat)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}
