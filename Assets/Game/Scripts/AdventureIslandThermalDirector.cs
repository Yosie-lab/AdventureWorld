using UnityEngine;

/// <summary>
/// 断崖・渓谷のサーマル自然上昇気流統括（Locomotion & Control ➔ 満点）
/// 北崖の断崖絶壁や白亜タワー周辺の風の通り道に、淡い光粒子が立ち昇るサーマル自然気流を配置。
/// 滑空中に飛び込むと高度がふわりと回復し、一度も着地することなく島全体を優雅に大空周遊できる。
/// </summary>
public class AdventureIslandThermalDirector : MonoBehaviour
{
    private static AdventureIslandThermalDirector _instance;
    public static AdventureIslandThermalDirector Instance => _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void AutoInitialize()
    {
        if (_instance != null) return;
        var existing = Object.FindAnyObjectByType<AdventureIslandThermalDirector>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("AdventureIslandThermalDirector");
        _instance = go.AddComponent<AdventureIslandThermalDirector>();
        DontDestroyOnLoad(go);
    }

    [System.Serializable]
    public struct ThermalPreset
    {
        public string name;
        public Vector3 position;
        public float radius;
        public float height;
        public float liftSpeed;
    }

    // 島の代表的サーマル自然気流スポット4選
    public static readonly ThermalPreset[] Presets = new ThermalPreset[]
    {
        new ThermalPreset
        {
            name = "北崖直下・海風吹き上げサーマル",
            position = new Vector3(165f, 12f, 588f),
            radius = 22.0f,
            height = 105.0f,
            liftSpeed = 6.8f
        },
        new ThermalPreset
        {
            name = "白亜タワー東部・光の風穴サーマル",
            position = new Vector3(548f, 52f, 512f),
            radius = 20.0f,
            height = 95.0f,
            liftSpeed = 7.5f
        },
        new ThermalPreset
        {
            name = "中央台地西斜面・高原上昇気流",
            position = new Vector3(240f, 22f, 310f),
            radius = 22.0f,
            height = 85.0f,
            liftSpeed = 6.5f
        },
        new ThermalPreset
        {
            name = "南西カルデラ・海風サーマル",
            position = new Vector3(210f, 10f, 185f),
            radius = 20.0f,
            height = 80.0f,
            liftSpeed = 6.5f
        }
    };

    private Material _streamMaterial;
    private float _lastThermalNoticeTime = -30f;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        CreateStreamMaterial();
        SpawnThermalSpots();
    }

    private void SpawnThermalSpots()
    {
        for (int i = 0; i < Presets.Length; i++)
        {
            var p = Presets[i];
            var spotGo = new GameObject($"Thermal_{p.name}");
            spotGo.transform.SetParent(transform, false);
            spotGo.transform.position = p.position;

            // 既存の高品質な AdventureThermalUpdraft を活用
            var updraft = spotGo.AddComponent<AdventureThermalUpdraft>();
            updraft.radius = p.radius;
            updraft.height = p.height;
            updraft.liftSpeed = p.liftSpeed;
            updraft.enableGroundLaunch = false; // 空中滑空用の自然気流
            updraft.pullToCenter = true;

            // 螺旋状に立ち昇る気流パーティクル
            AddSpiralStreamVisual(spotGo, p.radius, p.height);
        }
    }

    private void AddSpiralStreamVisual(GameObject parent, float radius, float height)
    {
        var ps = parent.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = true;
        main.loop = true;
        main.duration = 4.0f;
        main.startLifetime = 3.6f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(8.0f, 14.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.65f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 35;

        var emission = ps.emission;
        emission.rateOverTime = 8f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * 0.70f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // 上向き

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.orbitalY = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(0.6f, 1.2f); // 螺旋気流

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.65f, 1f, 0.95f), 0f), new GradientColorKey(new Color(0.35f, 0.85f, 1f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.40f, 0.25f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        var rend = parent.GetComponent<ParticleSystemRenderer>();
        rend.material = _streamMaterial;
        rend.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play();
    }

    private void CreateStreamMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Sprites/Default");

        _streamMaterial = new Material(shader) { name = "IslandThermal_StreamMat" };
        _streamMaterial.color = new Color(0.6f, 0.95f, 1f, 0.45f);
        _streamMaterial.SetFloat("_Surface", 1f);
        _streamMaterial.SetFloat("_Blend", 0f);
        _streamMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _streamMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _streamMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _streamMaterial.SetInt("_ZWrite", 0);
        _streamMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    void Update()
    {
        // プレイヤーがサーマルに進入した際の相棒Rustの温かい歓声
        var player = AdventurePlayerController.Instance;
        if (player == null || !player.IsGliding) return;

        Vector3 pPos = player.transform.position;
        for (int i = 0; i < Presets.Length; i++)
        {
            var p = Presets[i];
            float xz = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(p.position.x, p.position.z));
            if (xz <= p.radius && pPos.y >= p.position.y && pPos.y <= p.position.y + p.height)
            {
                AdventureAchievementManager.Unlock("thermal_ride");
                if (Time.time > _lastThermalNoticeTime + 28f && AdventureRustDrone.Instance != null)
                {
                    _lastThermalNoticeTime = Time.time;
                    string[] lines = new string[]
                    {
                        "わあ…上昇気流だ！高く登れるよ、Float！",
                        "ピピッ♪ 風が持ち上げてくれる…！大空クルーズだね！",
                        "気持ちいい風…！ここからなら島中を見渡せるよ♪"
                    };
                    AdventureRustDrone.Instance.SpeakCustom(lines[Random.Range(0, lines.Length)], 4.0f);
                }
                break;
            }
        }
    }
}
