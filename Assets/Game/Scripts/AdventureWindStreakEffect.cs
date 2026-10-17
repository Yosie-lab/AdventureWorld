using UnityEngine;

/// <summary>
/// 70mメガジャンプ・急上昇気流・滑空ブースト時に画面に現れる速度線（ウィンドストリーク）エフェクト
/// カメラ視界の周囲を疾走する白い風の流線パーティクルを制御し、圧倒的なスピード感と爽快感を演出
/// </summary>
[DisallowMultipleComponent]
public class AdventureWindStreakEffect : MonoBehaviour
{
    private ParticleSystem _streakPs;
    private ParticleSystem.EmissionModule _emission;
    private ParticleSystem.VelocityOverLifetimeModule _velocityModule;
    private AdventurePlayerController _player;
    private Camera _cam;

    private static AdventureWindStreakEffect _instance;
    public static AdventureWindStreakEffect Instance => _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        var cam = Camera.main;
        if (cam != null && cam.GetComponent<AdventureWindStreakEffect>() == null)
        {
            cam.gameObject.AddComponent<AdventureWindStreakEffect>();
        }
    }

    void Awake()
    {
        _instance = this;
        _cam = GetComponent<Camera>();
        CreateStreakParticles();
    }

    void Start()
    {
        _player = AdventurePlayerController.Resolve();
    }

    void Update()
    {
        if (_player == null)
        {
            _player = AdventurePlayerController.Resolve();
            if (_player == null) return;
        }

        float intensity = _player.SpeedIntensity;
        bool isAscending = _player.IsUpdraftAscending;

        // 速度が乗っている時（intensity > 0.18f）のみウィンドストリークを放出
        if (intensity > 0.18f && !_player.IsGrounded)
        {
            float targetRate = Mathf.Lerp(12f, 75f, (intensity - 0.18f) / 0.82f);
            if (isAscending) targetRate *= 1.35f; // メガジャンプ急上昇時はさらに増量

            _emission.rateOverTime = targetRate;

            // 急上昇中は下方向へ激しく流し、水平滑空中は手前方向（カメラ後方）へ流す
            if (isAscending)
            {
                _velocityModule.y = new ParticleSystem.MinMaxCurve(-38.0f * intensity, -22.0f * intensity);
                _velocityModule.z = new ParticleSystem.MinMaxCurve(-12.0f, -4.0f);
            }
            else
            {
                _velocityModule.y = new ParticleSystem.MinMaxCurve(-6.0f, 2.0f);
                _velocityModule.z = new ParticleSystem.MinMaxCurve(-35.0f * intensity, -18.0f * intensity);
            }
        }
        else
        {
            _emission.rateOverTime = 0f;
        }
    }

    void CreateStreakParticles()
    {
        var psGo = new GameObject("WindStreakParticles");
        psGo.transform.SetParent(transform, false);
        psGo.transform.localPosition = new Vector3(0f, 0f, 1.8f); // カメラの前方1.8mに配置
        psGo.transform.localRotation = Quaternion.identity;

        _streakPs = psGo.AddComponent<ParticleSystem>();
        var main = _streakPs.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.20f, 0.38f); // 瞬時に流れて消える
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 6.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
        main.startColor = new Color(1.0f, 1.0f, 1.0f, 0.38f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = true;

        _emission = _streakPs.emission;
        _emission.rateOverTime = 0f;

        // 画面の四隅・周辺ドーナツ状から発生
        var shape = _streakPs.shape;
        shape.shapeType = ParticleSystemShapeType.Donut;
        shape.radius = 1.6f;
        shape.donutRadius = 0.5f;

        // 速度によるパーティクルの流線化（Stretched Billboard）
        var rend = psGo.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.cameraVelocityScale = 0f;
            rend.velocityScale = 0.18f;
            rend.lengthScale = 4.2f; // 疾走感のある長い白線

#if UNITY_EDITOR
            var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/RustAndFloat/Materials/UpdraftWind_Mat.mat");
            if (mat != null) rend.sharedMaterial = mat;
#endif
            if (rend.sharedMaterial == null)
            {
                var s = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                rend.material = new Material(s);
            }
        }

        // Velocity Over Lifetime
        _velocityModule = _streakPs.velocityOverLifetime;
        _velocityModule.enabled = true;
        _velocityModule.space = ParticleSystemSimulationSpace.Local;

        // Size over Lifetime（フェードアウト）
        var sol = _streakPs.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.4f);
        sizeCurve.AddKey(0.25f, 1.0f);
        sizeCurve.AddKey(1f, 0.1f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Color over Lifetime（透明度フェードイン・アウト）
        var col = _streakPs.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.85f, 1.0f, 0.98f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.42f, 0.2f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;
    }
}
