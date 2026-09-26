using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 夕暮れ〜夜の情緒を彩る「発光ホタル（Fireflies）」。
/// 西海岸のヤシの木やオアシスの水辺、草むら周辺にフワフワと浮遊し、
/// 柔らかな黄緑色・エメラルド・黄金の光を点滅させながら幻想的な夜の風情を演出する。
/// </summary>
public class AdventureFireflies : MonoBehaviour
{
    private static AdventureFireflies _instance;
    public static AdventureFireflies Instance => _instance;

    private ParticleSystem _fireflyPS;
    private Material _fireflyMat;

    // ホタルが舞う群生スポット（オアシス水辺、ヤシの木林、波打ち際の草むらなど）
    private readonly List<Vector3> _swarmCenters = new List<Vector3>
    {
        new Vector3(158f, 6.2f, 210f), // 西海岸ヤシの木エリア
        new Vector3(175f, 6.5f, 275f), // 白砂ビーチ中央・流木付近
        new Vector3(162f, 6.0f, 350f), // 北側ヤシの木・岩礁周辺
        new Vector3(210f, 7.8f, 220f), // 草原への入り口付近
        new Vector3(145f, 5.8f, 305f), // 渚・水辺の夜光虫連動スポット
        new Vector3(190f, 8.5f, 380f)  // オアシス木陰
    };

    const int MaxFireflies = 180;
    private ParticleSystem.Particle[] _particles;
    private Vector3[] _particleVelocities;
    private float[] _particlePhases;

    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = Object.FindFirstObjectByType<AdventureFireflies>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("AdventureFireflies");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AdventureFireflies>();
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
        BuildFireflySystem();
    }

    void BuildFireflySystem()
    {
        var psGo = new GameObject("FireflyParticles");
        psGo.transform.SetParent(transform, false);

        _fireflyPS = psGo.AddComponent<ParticleSystem>();
        var main = _fireflyPS.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = MaxFireflies;
        main.startLifetime = float.MaxValue;
        main.startSpeed = 0f;

        var emission = _fireflyPS.emission;
        emission.enabled = false;

        var shape = _fireflyPS.shape;
        shape.enabled = false;

        var rend = psGo.GetComponent<ParticleSystemRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                  ?? Shader.Find("Particles/Standard Unlit")
                  ?? Shader.Find("Mobile/Particles/Additive")
                  ?? Shader.Find("Unlit/Color");

        _fireflyMat = new Material(shader);
        _fireflyMat.color = new Color(0.75f, 1.0f, 0.35f, 0.85f);
        rend.sharedMaterial = _fireflyMat;

        _particles = new ParticleSystem.Particle[MaxFireflies];
        _particleVelocities = new Vector3[MaxFireflies];
        _particlePhases = new float[MaxFireflies];

        // 各ホタルの初期配置
        for (int i = 0; i < MaxFireflies; i++)
        {
            Vector3 center = _swarmCenters[i % _swarmCenters.Count];
            Vector3 offset = Random.insideUnitSphere * Random.Range(3.5f, 14f);
            offset.y = Mathf.Abs(offset.y) * 0.45f + 0.3f; // 地表から0.3m〜3.5mの高さを漂う

            _particles[i].position = center + offset;
            _particles[i].startSize = Random.Range(0.22f, 0.48f); // 柔らかな光粒サイズ
            _particles[i].startColor = GetFireflyColor(Random.value);

            _particleVelocities[i] = Random.insideUnitSphere * 0.4f;
            _particlePhases[i] = Random.Range(0f, Mathf.PI * 2f);
        }

        _fireflyPS.SetParticles(_particles, MaxFireflies);
    }

    Color GetFireflyColor(float roll)
    {
        if (roll < 0.55f)
            return new Color(0.70f, 1.0f, 0.25f, 1f); // 爽やかな黄緑色（日本のヘイケボタル風）
        if (roll < 0.85f)
            return new Color(0.95f, 0.90f, 0.35f, 1f); // 黄金色の温かい光
        return new Color(0.20f, 1.0f, 0.75f, 1f);       // 幻想的なエメラルドグリーン
    }

    void Update()
    {
        if (_fireflyPS == null || _particles == null) return;

        float night = AdventureDayNightDirector.NightFactor;
        float sunset = AdventureDayNightDirector.SunsetFactor;
        // 夕暮れまたは夜間に活動（昼間は光がフェードアウトして非表示）
        float activity = Mathf.Clamp01(night * 1.2f + sunset * 0.45f);

        if (_fireflyMat != null)
        {
            Color c = _fireflyMat.color;
            c.a = activity * 0.85f;
            _fireflyMat.color = c;
        }

        if (activity < 0.05f) return;

        float dt = Time.deltaTime;
        float time = Time.time;

        // ホタルのフワフワした浮遊＆点滅運動（Perlinノイズ風の有機的軌道）
        for (int i = 0; i < MaxFireflies; i++)
        {
            Vector3 center = _swarmCenters[i % _swarmCenters.Count];
            Vector3 pos = _particles[i].position;

            // 群生の中心に引き寄せられつつ、有機的に漂う
            Vector3 toCenter = center - pos;
            toCenter.y = 0f;

            _particleVelocities[i] += (toCenter.normalized * 0.15f + new Vector3(
                Mathf.Sin(time * 0.8f + _particlePhases[i]) * 0.4f,
                Mathf.Cos(time * 1.1f + _particlePhases[i] * 1.3f) * 0.25f,
                Mathf.Cos(time * 0.7f + _particlePhases[i]) * 0.4f
            )) * dt;

            // 速度制限と減衰
            _particleVelocities[i] = Vector3.ClampMagnitude(_particleVelocities[i], 0.75f);
            _particleVelocities[i] = Vector3.Lerp(_particleVelocities[i], Vector3.zero, dt * 0.35f);

            pos += _particleVelocities[i] * dt;

            // 地面より下に行かないよう補正
            if (pos.y < center.y + 0.25f)
                pos.y = center.y + 0.25f;
            if (pos.y > center.y + 4.2f)
                pos.y = center.y + 4.2f;

            _particles[i].position = pos;

            // ホタル固有の呼吸のような点滅（Glow pulse）
            float blink = Mathf.Sin(time * 2.2f + _particlePhases[i]);
            blink = Mathf.Pow(Mathf.Clamp01((blink + 0.8f) / 1.8f), 2.5f); // ふわっと光ってゆっくり消える

            Color baseCol = GetFireflyColor((float)i / MaxFireflies);
            baseCol.a = blink * activity;
            _particles[i].startColor = baseCol;
        }

        _fireflyPS.SetParticles(_particles, MaxFireflies);
    }
}
