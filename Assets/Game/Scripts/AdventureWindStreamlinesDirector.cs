using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 大空の可視化気流「ウィンド・ストリームライン（風の道筋）」統括マネージャー
/// 島の上空に、風の流れを視覚的に描く優美な白い風のライン（風紋パーティクル）を配置。
/// 滑空中にこの風の道に乗ると、自然に滑空速度が加速（+15〜20%）し、風を読んで飛ぶ鳥のような爽快なアビエーション体験を実現する。
/// </summary>
public class AdventureWindStreamlinesDirector : MonoBehaviour
{
    private static AdventureWindStreamlinesDirector _instance;
    public static AdventureWindStreamlinesDirector Instance => _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void AutoInitialize()
    {
        if (_instance != null) return;
        var existing = Object.FindAnyObjectByType<AdventureWindStreamlinesDirector>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("AdventureWindStreamlinesDirector");
        _instance = go.AddComponent<AdventureWindStreamlinesDirector>();
        DontDestroyOnLoad(go);
    }

    [System.Serializable]
    public class StreamRoute
    {
        public string name;
        public Vector3[] waypoints;
        public float corridorRadius = 14f;
        public float boostMultiplier = 1.18f;
        public Vector3[] smoothPoints;
    }

    private List<StreamRoute> _routes = new List<StreamRoute>();
    private Material _streamParticleMat;
    private float _lastStreamVoiceTime = -30f;
    private bool _isInAnyStream = false;

    public bool IsPlayerInWindStream => _isInAnyStream;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        CreateStreamParticleMaterial();
        SetupDefaultRoutes();
        BuildVisualStreams();
    }

    void SetupDefaultRoutes()
    {
        _routes.Clear();

        // ルート1: 西海岸エメラルド・シーコーストライン（海風が北崖へと爽快に駆け抜けるライン）
        _routes.Add(new StreamRoute
        {
            name = "西海岸シーブリーズ・ハイウェイ",
            waypoints = new Vector3[]
            {
                new Vector3(120f, 22f, 220f),
                new Vector3(125f, 28f, 320f),
                new Vector3(135f, 38f, 430f),
                new Vector3(160f, 52f, 520f),
                new Vector3(180f, 68f, 590f)
            },
            corridorRadius = 16f,
            boostMultiplier = 1.20f
        });

        // ルート2: 中央渓谷・大河スカイウェイ（大河・オアシスから白亜タワー前へ導く中央幹線）
        _routes.Add(new StreamRoute
        {
            name = "中央渓谷スカイウェイ",
            waypoints = new Vector3[]
            {
                new Vector3(260f, 32f, 250f),
                new Vector3(320f, 42f, 310f),
                new Vector3(385f, 50f, 370f),
                new Vector3(445f, 62f, 425f),
                new Vector3(505f, 75f, 485f)
            },
            corridorRadius = 15f,
            boostMultiplier = 1.18f
        });

        // ルート3: 東部原生林・スカイライン（東部高原からタワー天蓋上空へ抜ける最高高度の爽快気流）
        _routes.Add(new StreamRoute
        {
            name = "東部高空スカイハイウェイ",
            waypoints = new Vector3[]
            {
                new Vector3(660f, 95f, 620f),
                new Vector3(610f, 98f, 580f),
                new Vector3(565f, 102f, 545f),
                new Vector3(518f, 108f, 515f)
            },
            corridorRadius = 18f,
            boostMultiplier = 1.22f
        });

        // 各ウェイポイント間を Catmull-Rom スプラインで滑らかに補間
        for (int r = 0; r < _routes.Count; r++)
        {
            _routes[r].smoothPoints = GenerateSmoothSpline(_routes[r].waypoints, 12);
        }
    }

    Vector3[] GenerateSmoothSpline(Vector3[] pts, int segmentsPerSpan)
    {
        if (pts == null || pts.Length < 2) return pts;
        var list = new List<Vector3>();

        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector3 p0 = i > 0 ? pts[i - 1] : pts[i] + (pts[i] - pts[i + 1]);
            Vector3 p1 = pts[i];
            Vector3 p2 = pts[i + 1];
            Vector3 p3 = (i + 2 < pts.Length) ? pts[i + 2] : p2 + (p2 - p1);

            for (int s = 0; s < segmentsPerSpan; s++)
            {
                float t = (float)s / segmentsPerSpan;
                list.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }
        list.Add(pts[pts.Length - 1]);
        return list.ToArray();
    }

    Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    void CreateStreamParticleMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Sprites/Default");

        _streamParticleMat = new Material(shader) { name = "WindStream_Mat" };
        _streamParticleMat.color = new Color(0.92f, 0.98f, 1f, 0.38f);
        _streamParticleMat.SetFloat("_Surface", 1f);
        _streamParticleMat.SetFloat("_Blend", 0f);
        _streamParticleMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _streamParticleMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _streamParticleMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _streamParticleMat.SetInt("_ZWrite", 0);
        _streamParticleMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    void BuildVisualStreams()
    {
        for (int r = 0; r < _routes.Count; r++)
        {
            var route = _routes[r];
            var routeGo = new GameObject($"WindStream_{route.name}");
            routeGo.transform.SetParent(transform, false);

            var ps = routeGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 6.0f;
            main.startLifetime = 4.5f;
            main.startSpeed = 0.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 1.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.85f, 0.96f, 1f, 0.32f),
                new Color(1f, 1f, 1f, 0.52f)
            );
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 65;

            var emission = ps.emission;
            emission.rateOverTime = 10f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 2.5f;

            var psr = routeGo.GetComponent<ParticleSystemRenderer>();
            if (psr != null && _streamParticleMat != null)
            {
                psr.sharedMaterial = _streamParticleMat;
                psr.renderMode = ParticleSystemRenderMode.Billboard;
            }

            // ウェイポイントを巡るエミッターコントローラー
            var runner = routeGo.AddComponent<WindStreamEmitterRunner>();
            runner.points = route.smoothPoints;
            runner.ps = ps;
            ps.Play();
        }
    }

    void Update()
    {
        var player = AdventurePlayerController.Instance;
        if (player == null) return;

        bool inStream = false;
        Vector3 playerPos = player.transform.position;

        if (player.IsGliding)
        {
            for (int r = 0; r < _routes.Count; r++)
            {
                var route = _routes[r];
                if (IsPositionInRoute(playerPos, route.smoothPoints, route.corridorRadius, out Vector3 forwardDir))
                {
                    inStream = true;

                    // 風の道による自然な順風加速アシスト（進行方向が気流と合っている場合）
                    float dot = Vector3.Dot(player.transform.forward, forwardDir);
                    if (dot > 0.1f)
                    {
                        // 順風気流ブースト：気流方向へ穏やかに吸着推進
                        player.ApplyRingMagnetPull(forwardDir * (4.2f * Time.deltaTime));
                        AdventureAchievementManager.Unlock("wind_highway");

                        // 相棒Rustのリアルタイム歓声
                        if (Time.time > _lastStreamVoiceTime + 22f && AdventureRustDrone.Instance != null)
                        {
                            _lastStreamVoiceTime = Time.time;
                            string[] voices = new string[]
                            {
                                "風の道に乗ったよ！すごく速い、Float！",
                                "ピピッ♪ 大空の気流ハイウェイだ！どこまでも行けるね！",
                                "いい風をつかまえたね！気持ちいい〜〜！"
                            };
                            AdventureRustDrone.Instance.SpeakCustom(voices[Random.Range(0, voices.Length)], 4.0f);
                        }
                    }
                    break;
                }
            }
        }

        _isInAnyStream = inStream;
    }

    bool IsPositionInRoute(Vector3 pos, Vector3[] pts, float radius, out Vector3 tangent)
    {
        tangent = Vector3.forward;
        if (pts == null || pts.Length < 2) return false;

        float rSq = radius * radius;
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector3 a = pts[i];
            Vector3 b = pts[i + 1];
            Vector3 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 0.001f) continue;

            float t = Mathf.Clamp01(Vector3.Dot(pos - a, ab) / lenSq);
            Vector3 proj = a + ab * t;
            if ((pos - proj).sqrMagnitude <= rSq)
            {
                tangent = ab.normalized;
                return true;
            }
        }
        return false;
    }
}

/// <summary>
/// スプライン軌道に沿って移動しながら優美な風の軌跡を放つエミッター移動制御
/// </summary>
public class WindStreamEmitterRunner : MonoBehaviour
{
    public Vector3[] points;
    public ParticleSystem ps;
    private float _progress = 0f;
    private const float LoopSeconds = 18.0f;

    void Update()
    {
        if (points == null || points.Length < 2) return;

        _progress += Time.deltaTime / LoopSeconds;
        if (_progress > 1f) _progress -= 1f;

        float totalSpan = _progress * (points.Length - 1);
        int idx = Mathf.Clamp((int)totalSpan, 0, points.Length - 2);
        float frac = totalSpan - idx;

        transform.position = Vector3.Lerp(points[idx], points[idx + 1], frac);
    }
}
