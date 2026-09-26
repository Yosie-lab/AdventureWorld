using UnityEngine;

/// <summary>
/// 夜空に浮かび上がる満天の星空・天の川・きらめく星々。
/// 昼は青空に隠れ、夕暮れから夜にかけて無数の星々がスーッと現れ、
/// カメラを見上げると頭上一面に広がる幻想的な宇宙の夜空を創り出す。
/// </summary>
public class AdventureStarrySky : MonoBehaviour
{
    private static AdventureStarrySky _instance;
    public static AdventureStarrySky Instance => _instance;

    private Material _starMaterial;
    private ParticleSystem _starsParticleSystem;
    private Transform _moonObj;
    private Camera _cam;

    const int StarCount = 650; // 満天の星の数

    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = Object.FindFirstObjectByType<AdventureStarrySky>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("AdventureStarrySky");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AdventureStarrySky>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    void Start()
    {
        BuildStarryDome();
        BuildMoon();
    }

    void BuildStarryDome()
    {
        var psGo = new GameObject("StarDomeParticles");
        psGo.transform.SetParent(transform, false);

        _starsParticleSystem = psGo.AddComponent<ParticleSystem>();
        var main = _starsParticleSystem.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = StarCount;
        main.startLifetime = float.MaxValue;
        main.startSpeed = 0f;

        var emission = _starsParticleSystem.emission;
        emission.enabled = false; // 初期配置のみ

        var shape = _starsParticleSystem.shape;
        shape.enabled = false;

        var rend = psGo.GetComponent<ParticleSystemRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                  ?? Shader.Find("Particles/Standard Unlit")
                  ?? Shader.Find("Unlit/Color");
        _starMaterial = new Material(shader);
        _starMaterial.color = Color.white;
        rend.sharedMaterial = _starMaterial;

        // 星々の配置（半球面上に均等かつ天の川の密度濃淡をつけて配置）
        var particles = new ParticleSystem.Particle[StarCount];
        for (int i = 0; i < StarCount; i++)
        {
            // 上半球（Elevation 8度〜88度）にランダム配置
            float u = Random.value;
            float v = Random.value;
            float theta = u * Mathf.PI * 2f; // 方位角
            float phi = Mathf.Lerp(0.14f, Mathf.PI * 0.48f, v); // 仰角

            // 天の川（Milky Way）の帯状密度アップ
            float milkyBand = Mathf.Abs(Mathf.Sin(theta * 1.5f + phi));
            if (i < 200 && milkyBand < 0.35f)
            {
                phi = Mathf.Lerp(phi, Mathf.PI * 0.35f, 0.45f);
            }

            float r = 450f;
            Vector3 pos = new Vector3(
                Mathf.Cos(theta) * Mathf.Cos(phi) * r,
                Mathf.Sin(phi) * r,
                Mathf.Sin(theta) * Mathf.Cos(phi) * r
            );

            // 星のサイズ（微細な星から一等星まで）
            float starSize = Random.value < 0.08f ? Random.Range(3.2f, 4.8f) : Random.Range(1.2f, 2.4f);

            // 星の色温度（青白い星、温かい黄金色の星、純白の星）
            Color starCol;
            float colRoll = Random.value;
            if (colRoll < 0.25f)
                starCol = new Color(0.75f, 0.88f, 1.0f); // シリウス風の青白
            else if (colRoll < 0.45f)
                starCol = new Color(1.0f, 0.92f, 0.70f); // カペラ風の温かい黄金
            else
                starCol = new Color(0.95f, 0.98f, 1.0f); // 純白

            particles[i].position = pos;
            particles[i].startSize = starSize;
            particles[i].startColor = starCol;
        }

        _starsParticleSystem.SetParticles(particles, StarCount);
    }

    void BuildMoon()
    {
        // 東の空に浮かぶ優しい月
        var moonGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        moonGo.name = "Moon";
        moonGo.transform.SetParent(transform, false);
        moonGo.transform.localPosition = new Vector3(280f, 220f, 160f);
        moonGo.transform.localScale = Vector3.one * 32f;
        Destroy(moonGo.GetComponent<Collider>());

        var mr = moonGo.GetComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
        mat.color = new Color(1.0f, 0.96f, 0.88f); // 柔らかな月光色
        mr.sharedMaterial = mat;
        _moonObj = moonGo.transform;
    }

    void LateUpdate()
    {
        if (_cam == null)
            _cam = Camera.main;

        if (_cam != null)
        {
            // 星空ドームをカメラ位置に追従（無限遠の背景として描画）
            transform.position = _cam.transform.position;
        }

        // 時間帯マネージャーから夜の深さ（NightFactor: 0.0=昼、1.0=夜）を取得
        float night = AdventureDayNightDirector.NightFactor;

        // 星々のきらめき＆夜空のフェードイン
        if (_starMaterial != null)
        {
            // 昼は完全透明、夕暮れから夜にかけてスーッと浮き上がる
            float alpha = Mathf.Clamp01((night - 0.25f) / 0.65f);
            // 微細な瞬き（Twinkle）
            float twinkle = 0.92f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.08f;
            Color c = Color.white * (alpha * twinkle);
            c.a = alpha;
            _starMaterial.color = c;
        }

        // 月のフェードイン＆輝き
        if (_moonObj != null)
        {
            float moonAlpha = Mathf.Clamp01((night - 0.15f) / 0.60f);
            var mr = _moonObj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.enabled = moonAlpha > 0.01f;
                Color mc = new Color(1.0f, 0.96f, 0.88f) * (0.4f + moonAlpha * 0.8f);
                mr.sharedMaterial.color = mc;
            }
        }
    }
}
