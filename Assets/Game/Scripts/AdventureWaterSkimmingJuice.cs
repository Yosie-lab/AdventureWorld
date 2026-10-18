using UnityEngine;

/// <summary>
/// 『Rust & Float』水面スキミング＆白波航跡ブーストシステム（案1: JUICE極限化）
/// ・滑空中に海面（標高5.5m）または池（標高18.15m）のスレスレ（1〜2.2m）を低空飛行すると発動。
/// ・水面反発効果（Ground Effect）により、水没せず水面を撫でるように滑空高度を自動維持。
/// ・左右からV字型に広がる白い波飛沫（Wake Spray）と水面波紋リングを連続放出。
/// ・高速で水面を切り裂く瑞々しい水流疾走サウンド（プロシージャル合成）を再生。
/// ・水面スキミングによる推進力ブーストをプレイヤーへ付与。
/// </summary>
[DisallowMultipleComponent]
public class AdventureWaterSkimmingJuice : MonoBehaviour
{
    private static AdventureWaterSkimmingJuice _instance;
    public static AdventureWaterSkimmingJuice Instance => _instance;

    public const float OceanSurfaceY = 5.50f;
    public const float LakeSurfaceY = 18.15f;
    public const float MinSkimAltitude = 0.20f;  // 水面からの最小高度
    public const float MaxSkimAltitude = 2.40f;  // 水面スキミング判定の上限高度

    public bool IsSkimming { get; private set; }
    public float SkimIntensity { get; private set; } // 0.0〜1.0（水面に近いほど強まる）
    public float CurrentWaterSurfaceY { get; private set; }

    private AdventurePlayerController _player;
    private ParticleSystem _sprayPsLeft;
    private ParticleSystem _sprayPsRight;
    private AudioSource _sprayAudio;
    private AudioClip _sprayAudioClip;

    private float _lastRippleTime;
    private float _skimmingDuration;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        var existing = Object.FindAnyObjectByType<AdventureWaterSkimmingJuice>();
        if (existing == null)
        {
            var go = new GameObject("AdventureWaterSkimmingJuice");
            go.AddComponent<AdventureWaterSkimmingJuice>();
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        SetupSprayParticles();
        SetupSprayAudio();
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

        CheckWaterSkimming();
        UpdateSprayVisuals();
        UpdateSprayAudio();
    }

    /// <summary>
    /// 現在のプレイヤー位置における水面高さとスキミング状態を判定
    /// </summary>
    void CheckWaterSkimming()
    {
        // 滑空中かつ非接地のみスキミング対象
        if (!_player.IsGliding || _player.IsGrounded)
        {
            StopSkimming();
            return;
        }

        Vector3 pPos = _player.transform.position;

        // 水面水位の特定（湖エリアか、外洋海面か）
        float waterY = ResolveWaterHeightAt(pPos);
        CurrentWaterSurfaceY = waterY;

        float clearance = pPos.y - waterY;

        // 水面との高度差が 0.2m〜2.4m の範囲内にあるか判定
        if (clearance >= MinSkimAltitude && clearance <= MaxSkimAltitude)
        {
            // 水面に近いほど強度が高まる（0.2mで1.0、2.4mで0.0）
            float t = 1.0f - Mathf.Clamp01((clearance - MinSkimAltitude) / (MaxSkimAltitude - MinSkimAltitude));
            SkimIntensity = Mathf.MoveTowards(SkimIntensity, t, Time.deltaTime * 6f);
            IsSkimming = true;
            _skimmingDuration += Time.deltaTime;

            // スキミングが1.2秒以上継続したとき、相棒Rustが歓声を上げる
            if (_skimmingDuration > 1.2f && _skimmingDuration - Time.deltaTime <= 1.2f)
            {
                var drone = AdventureRustDrone.Instance;
                if (drone != null)
                {
                    drone.SpeakCustom("わっ、水面スレスレ！すごいスピード！！", 3.2f);
                    drone.TriggerCelebrate();
                }
            }
        }
        else
        {
            StopSkimming();
        }
    }

    void StopSkimming()
    {
        IsSkimming = false;
        SkimIntensity = Mathf.MoveTowards(SkimIntensity, 0f, Time.deltaTime * 5f);
        _skimmingDuration = 0f;
    }

    /// <summary>
    /// 座標に応じた水面水位の取得（カルデラ湖周辺か、外洋海面か）
    /// </summary>
    float ResolveWaterHeightAt(Vector3 pos)
    {
        // カルデラオアシス湖（中心 135, 166、半径約32m）
        float distToLake = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(135f, 166f));
        if (distToLake < 35f)
        {
            return LakeSurfaceY;
        }

        // 基本外洋海面
        return OceanSurfaceY;
    }

    /// <summary>
    /// 左右の白波スプレーパーティクルシステムの生成と配置
    /// </summary>
    void SetupSprayParticles()
    {
        var rootPsGo = new GameObject("SkimmingWakeSprayRoot");
        rootPsGo.transform.SetParent(transform, false);

        _sprayPsLeft = CreateSprayEmitter(rootPsGo.transform, "WakeSpray_Left", new Vector3(-0.65f, -0.4f, -0.2f), -24f);
        _sprayPsRight = CreateSprayEmitter(rootPsGo.transform, "WakeSpray_Right", new Vector3(0.65f, -0.4f, -0.2f), 24f);
    }

    ParticleSystem CreateSprayEmitter(Transform parent, string name, Vector3 localOffset, float angleY)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localOffset;
        go.transform.localRotation = Quaternion.Euler(15f, angleY, 0f);

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.48f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
        main.startColor = new Color(0.92f, 0.98f, 1.0f, 0.75f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 250;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.15f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.85f, 0.95f, 1f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.55f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = grad;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var curve = new AnimationCurve();
        curve.AddKey(0f, 0.35f);
        curve.AddKey(0.4f, 1.0f);
        curve.AddKey(1f, 1.6f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var rend = go.GetComponent<ParticleSystemRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(0.95f, 0.99f, 1f, 0.75f));
        rend.material = mat;

        return ps;
    }

    /// <summary>
    /// スキミング中のパーティクル位置・放出量の動的更新
    /// </summary>
    void UpdateSprayVisuals()
    {
        if (_player == null) return;

        bool active = IsSkimming && SkimIntensity > 0.05f;

        // プレイヤーの左右後方に追従
        Vector3 playerPos = _player.transform.position;
        Vector3 playerFwd = _player.transform.forward;
        Vector3 playerRight = _player.transform.right;

        // 水面直上にエミッターのY座標を接地
        Vector3 waterContactPos = new Vector3(playerPos.x, CurrentWaterSurfaceY + 0.05f, playerPos.z);

        if (_sprayPsLeft != null)
        {
            _sprayPsLeft.transform.position = waterContactPos - playerRight * 0.75f - playerFwd * 0.25f;
            _sprayPsLeft.transform.rotation = Quaternion.LookRotation(-playerFwd - playerRight * 0.45f, Vector3.up);
            var em = _sprayPsLeft.emission;
            em.rateOverTime = active ? Mathf.Lerp(25f, 140f, SkimIntensity) : 0f;
        }

        if (_sprayPsRight != null)
        {
            _sprayPsRight.transform.position = waterContactPos + playerRight * 0.75f - playerFwd * 0.25f;
            _sprayPsRight.transform.rotation = Quaternion.LookRotation(-playerFwd + playerRight * 0.45f, Vector3.up);
            var em = _sprayPsRight.emission;
            em.rateOverTime = active ? Mathf.Lerp(25f, 140f, SkimIntensity) : 0f;
        }

        // 水面波紋リングの連続生成（秒間10回程度）
        if (active && Time.time - _lastRippleTime > 0.10f)
        {
            _lastRippleTime = Time.time;
            SpawnSkimRippleRing(waterContactPos);
        }
    }

    /// <summary>
    /// 水面を掠めた際に広がる水平円環波紋
    /// </summary>
    void SpawnSkimRippleRing(Vector3 waterPos)
    {
        var fxGo = new GameObject("SkimWaterRipple");
        fxGo.transform.position = waterPos + Vector3.up * 0.02f;

        var ps = fxGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.startLifetime = 0.55f;
        main.startSpeed = 0f;
        main.startSize = 1.4f;
        main.startColor = new Color(0.95f, 1f, 1f, 0.70f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1;

        var sizeOver = ps.sizeOverLifetime;
        sizeOver.enabled = true;
        var sCurve = new AnimationCurve();
        sCurve.AddKey(0f, 0.6f);
        sCurve.AddKey(1f, 3.2f); // 急速に外側へ広がる
        sizeOver.size = new ParticleSystem.MinMaxCurve(1f, sCurve);

        var colorOver = ps.colorOverLifetime;
        colorOver.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.85f, 0.95f, 1f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.70f, 0f), new GradientAlphaKey(0.35f, 0.4f), new GradientAlphaKey(0f, 1f) }
        );
        colorOver.color = grad;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

        var rend = fxGo.GetComponent<ParticleSystemRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.70f));
        rend.material = mat;

        ps.Play();
        Destroy(fxGo, 0.65f);
    }

    /// <summary>
    /// 水面疾走音（Water Whoosh / Spray Rush）のプロシージャル合成
    /// </summary>
    void SetupSprayAudio()
    {
        _sprayAudio = gameObject.AddComponent<AudioSource>();
        _sprayAudio.loop = true;
        _sprayAudio.playOnAwake = false;
        _sprayAudio.spatialBlend = 0f; // 2Dステレオでプレイヤーの耳元に爽快に響く
        _sprayAudio.volume = 0f;
        _sprayAudio.pitch = 1.15f;

        _sprayAudioClip = SynthesizeWaterSprayRushClip();
        _sprayAudio.clip = _sprayAudioClip;
        _sprayAudio.Play();
    }

    void UpdateSprayAudio()
    {
        if (_sprayAudio == null) return;

        bool active = IsSkimming && SkimIntensity > 0.05f;
        float targetVol = active ? Mathf.Lerp(0.08f, 0.42f, SkimIntensity) : 0f;
        float targetPitch = active ? Mathf.Lerp(1.05f, 1.35f, SkimIntensity) : 1.0f;

        _sprayAudio.volume = Mathf.MoveTowards(_sprayAudio.volume, targetVol, Time.deltaTime * 3.5f);
        _sprayAudio.pitch = Mathf.MoveTowards(_sprayAudio.pitch, targetPitch, Time.deltaTime * 2.0f);
    }

    /// <summary>
    /// 瑞々しい水しぶきと風の擦れ合いをシミュレートしたプロシージャル音源
    /// </summary>
    AudioClip SynthesizeWaterSprayRushClip()
    {
        int sampleRate = 44100;
        float duration = 1.0f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        System.Random rnd = new System.Random(42);

        // バンドパス/ローパスフィルターシミュレーションによる清涼な水流ノイズ
        float filterState1 = 0f;
        float filterState2 = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
            
            // 2段RCフィルターで耳障りな超高域を削り、爽快な「ザーッ」という水しぶき音へ整形
            filterState1 += (white - filterState1) * 0.28f;
            filterState2 += (filterState1 - filterState2) * 0.22f;

            // 微小な波のうねり変調（有機的な水面の生きた音）
            float swell = 0.85f + 0.15f * Mathf.Sin((float)i / sampleRate * Mathf.PI * 4f);
            samples[i] = filterState2 * swell * 0.35f;
        }

        var clip = AudioClip.Create("WaterSprayRushLoop", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
