using UnityEngine;

/// <summary>
/// 水面リップル波紋＆水鳥スプラッシュジュース（Water Surface Ripple & Splash Engine）。
/// Floatの浅瀬歩行・着水時、および相棒Rustの水面スキミング（低空飛行）時に
/// 心地よい同心円リップル（波紋リング）と水しぶきパーティクルをプロシージャル展開する。
/// </summary>
public class AdventureWaterRippleManager : MonoBehaviour
{
    private static AdventureWaterRippleManager _instance;
    public static AdventureWaterRippleManager Instance => Ensure();

    private ParticleSystem _ripplePs;
    private ParticleSystem _splashPs;
    private float _playerRippleTimer = 0f;
    private float _rustRippleTimer = 0f;

    public const float DefaultWaterLevel = 5.5f;

    public static AdventureWaterRippleManager Ensure()
    {
        if (_instance != null) return _instance;
        _instance = FindAnyObjectByType<AdventureWaterRippleManager>();
        if (_instance != null) return _instance;

        var go = new GameObject("AdventureWaterRippleManager");
        _instance = go.AddComponent<AdventureWaterRippleManager>();
        DontDestroyOnLoad(go);
        return _instance;
    }

    private void Awake()
    {
        if (_instance == null) _instance = this;
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        SetupParticleSystems();
    }

    private void Update()
    {
        float waterY = GetWaterSurfaceY();

        TickPlayerWaterRipples(waterY);
        TickRustWaterSkimming(waterY);
    }

    /// <summary>水面Y座標の取得（島バウンダリ設定準拠）</summary>
    public static float GetWaterSurfaceY()
    {
        var bounds = AdventureIslandBoundary.Instance;
        if (bounds != null) return bounds.waterLevel;
        return DefaultWaterLevel;
    }

    /// <summary>Float（プレイヤー）の水面波紋＆スプラッシュ</summary>
    private void TickPlayerWaterRipples(float waterY)
    {
        var player = AdventurePlayerController.Instance;
        if (player == null) return;

        Vector3 pPos = player.transform.position;
        float diffY = pPos.y - waterY;

        // 水面から下0.8m（浅瀬）〜上0.35m（波打ち際）の範囲
        bool isInWaterZone = diffY >= -0.85f && diffY <= 0.40f;
        if (!isInWaterZone) return;

        float hSpeed = player.HorizontalSpeed;
        if (hSpeed < 0.25f) return;

        _playerRippleTimer -= Time.deltaTime;
        float interval = Mathf.Lerp(0.38f, 0.16f, Mathf.Clamp01(hSpeed / 10.0f));

        if (_playerRippleTimer <= 0f)
        {
            _playerRippleTimer = interval;
            SpawnRipple(new Vector3(pPos.x, waterY + 0.02f, pPos.z), Mathf.Lerp(0.6f, 1.25f, hSpeed / 9f));

            if (hSpeed > 3.0f)
            {
                SpawnSplash(new Vector3(pPos.x, waterY + 0.05f, pPos.z), player.transform.forward, hSpeed);
            }
        }
    }

    /// <summary>相棒Rustの水面スキミング（低空飛行時の真下の水煙・波紋）</summary>
    private void TickRustWaterSkimming(float waterY)
    {
        var rust = AdventureRustDrone.Instance;
        if (rust == null) return;

        Vector3 rPos = rust.transform.position;
        float heightAboveWater = rPos.y - waterY;

        // 水面から 0.2m 〜 2.0m 以内の低空飛行中
        if (heightAboveWater > 0.1f && heightAboveWater < 1.85f)
        {
            _rustRippleTimer -= Time.deltaTime;
            float interval = Mathf.Lerp(0.12f, 0.28f, heightAboveWater / 1.85f);

            if (_rustRippleTimer <= 0f)
            {
                _rustRippleTimer = interval;
                float scale = Mathf.Lerp(1.1f, 0.5f, heightAboveWater / 1.85f);
                SpawnRipple(new Vector3(rPos.x, waterY + 0.02f, rPos.z), scale);
            }
        }
    }

    /// <summary>波紋リングの発生</summary>
    public void SpawnRipple(Vector3 worldPos, float scale = 1.0f)
    {
        if (_ripplePs == null) return;

        var emitParams = new ParticleSystem.EmitParams
        {
            position = worldPos,
            startSize = 0.45f * scale,
            startLifetime = 0.95f,
            startColor = new Color(0.85f, 0.96f, 1.0f, 0.65f)
        };
        _ripplePs.Emit(emitParams, 1);
    }

    /// <summary>水しぶきスプラッシュの発生</summary>
    public void SpawnSplash(Vector3 worldPos, Vector3 forward, float speed)
    {
        if (_splashPs == null) return;

        int count = Mathf.Clamp((int)(speed * 1.5f), 3, 12);
        for (int i = 0; i < count; i++)
        {
            Vector3 spread = (forward + Random.insideUnitSphere * 0.45f + Vector3.up * 0.65f).normalized;
            var emitParams = new ParticleSystem.EmitParams
            {
                position = worldPos + Random.insideUnitSphere * 0.15f,
                velocity = spread * Random.Range(1.8f, 3.8f),
                startSize = Random.Range(0.08f, 0.18f),
                startLifetime = Random.Range(0.35f, 0.65f),
                startColor = new Color(0.92f, 0.98f, 1.0f, 0.75f)
            };
            _splashPs.Emit(emitParams, 1);
        }
    }

    private void SetupParticleSystems()
    {
        // 1. 同心円リップル（波紋リング）パーティクル
        var rGo = new GameObject("WaterRipple_Emitter");
        rGo.transform.SetParent(transform, false);
        _ripplePs = rGo.AddComponent<ParticleSystem>();
        var rMain = _ripplePs.main;
        rMain.loop = false;
        rMain.playOnAwake = false;
        rMain.simulationSpace = ParticleSystemSimulationSpace.World;
        rMain.startSpeed = 0f;
        rMain.startLifetime = 0.95f;
        rMain.startSize = 0.5f;

        // 時間経過でリングが大きく広がるカーブ
        var sizeOverLifetime = _ripplePs.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.4f);
        sizeCurve.AddKey(0.4f, 1.8f);
        sizeCurve.AddKey(1f, 3.2f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // 時間経過でフェードアウト
        var colOverLifetime = _ripplePs.colorOverLifetime;
        colOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.65f, 0.92f, 1f), 0f), new GradientColorKey(new Color(0.85f, 1f, 0.95f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0.45f, 0.4f), new GradientAlphaKey(0f, 1f) }
        );
        colOverLifetime.color = grad;

        var rEmission = _ripplePs.emission;
        rEmission.rateOverTime = 0f;

        var rShape = _ripplePs.shape;
        rShape.enabled = false;

        var rPsr = rGo.GetComponent<ParticleSystemRenderer>();
        rPsr.renderMode = ParticleSystemRenderMode.HorizontalBillboard; // 水平（水面にペタッと平らに広がる）
        var pShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
        Material pMat = pShader != null ? new Material(pShader) : null;
        if (pMat != null) rPsr.material = pMat;

        // 2. 水しぶきスプラッシュパーティクル
        var sGo = new GameObject("WaterSplash_Emitter");
        sGo.transform.SetParent(transform, false);
        _splashPs = sGo.AddComponent<ParticleSystem>();
        var sMain = _splashPs.main;
        sMain.loop = false;
        sMain.playOnAwake = false;
        sMain.simulationSpace = ParticleSystemSimulationSpace.World;
        sMain.gravityModifier = 1.8f; // 重力で自然に落下

        var sEmission = _splashPs.emission;
        sEmission.rateOverTime = 0f;

        var sShape = _splashPs.shape;
        sShape.enabled = false;

        var sPsr = sGo.GetComponent<ParticleSystemRenderer>();
        sPsr.renderMode = ParticleSystemRenderMode.Billboard;
        if (pMat != null) sPsr.material = pMat;
    }
}
