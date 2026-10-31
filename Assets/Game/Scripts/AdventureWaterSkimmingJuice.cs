using UnityEngine;

/// <summary>
/// 『Rust & Float』水面スキミング＆白波航跡ブーストシステム（案1: JUICE極限化）
/// ・滑空中に海面（標高5.5m）または池（標高18.15m）のスレスレ（0.2〜2.4m）を低空飛行すると発動。
/// ・水面反発効果（Ground Effect）により、水没せず水面を撫でるように滑空高度を自動維持。
/// ・左右からV字型に広がる白い波飛沫（Wake Spray）と水面波紋リングを連続放出。
/// ・GC Allocゼロ化：パーティクルプールと静的エミッターによる高効率描画。
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
    public const float MinSkimAltitude = 0.10f;  // 水面からの最小高度
    public const float MaxSkimAltitude = 3.60f;  // 水面スキミング判定の上限高度（高度1〜3mで気持ちよく滑走）

    public bool IsSkimming { get; private set; }
    public float SkimIntensity { get; private set; } // 0.0〜1.0（水面に近いほど強まる）
    public float CurrentWaterSurfaceY { get; private set; }
    public float Clearance { get; private set; }

    private AdventurePlayerController _player;
    private ParticleSystem _sprayPsLeft;
    private ParticleSystem _sprayPsRight;
    private ParticleSystem _sprayMistLeft;
    private ParticleSystem _sprayMistRight;
    private ParticleSystem _wakePsCenter;
    private ParticleSystem _ripplePs;
    private AudioSource _sprayAudio;
    private AudioClip _sprayAudioClip;
    private Terrain _cachedTerrain;

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
        SetupRippleParticlePool();
        SetupSprayAudio();
    }

    void Start()
    {
        _player = AdventurePlayerController.Resolve();
        _cachedTerrain = Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();
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

        // 水面水位の特定（湖エリアか、外洋海面か。陸地の場合は除外）
        bool isOverWater;
        float waterY = ResolveWaterHeightAt(pPos, out isOverWater);
        CurrentWaterSurfaceY = waterY;

        if (!isOverWater)
        {
            StopSkimming();
            return;
        }

        float clearance = pPos.y - waterY;
        Clearance = clearance;

        // 水面との高度差が 0.10m〜MaxSkimAltitude (3.60m) の範囲内にあるか判定
        if (clearance >= MinSkimAltitude && clearance <= MaxSkimAltitude)
        {
            // 水面に近いほど強度が高まる（0.2mで1.0、3.6mで0.0）
            float t = 1.0f - Mathf.Clamp01((clearance - MinSkimAltitude) / (MaxSkimAltitude - MinSkimAltitude));
            SkimIntensity = Mathf.MoveTowards(SkimIntensity, t, Time.deltaTime * 8f);
            IsSkimming = true;
            _skimmingDuration += Time.deltaTime;

            // スキミングが1.2秒以上継続したとき、相棒Rustが歓声を上げる
            if (_skimmingDuration > 1.2f && _skimmingDuration - Time.deltaTime <= 1.2f)
            {
                var drone = AdventureRustDrone.Instance;
                if (drone != null)
                {
                    drone.TriggerFlightReaction(AdventureRustDrone.FlightReactionType.WaterSkim, SkimIntensity);
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
        SkimIntensity = Mathf.MoveTowards(SkimIntensity, 0f, Time.deltaTime * 6f);
        _skimmingDuration = 0f;
    }

    /// <summary>
    /// 座標に応じた水面水位の取得（カルデラ湖周辺か、外洋海面か。陸地上は false を返す）
    /// </summary>
    float ResolveWaterHeightAt(Vector3 pos, out bool isOverWater)
    {
        isOverWater = false;

        // 1. カルデラオアシス湖（中心 135, 166、半径約52m）
        float distToLake = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(135f, 166f));
        if (distToLake < 52f)
        {
            isOverWater = true;
            return LakeSurfaceY;
        }

        // 2. 外洋海面および西ビーチ浅瀬判定
        // 西海岸水域（X <= 195f または Z <= 330f）または島の全周境界
        if (_cachedTerrain == null) _cachedTerrain = Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();
        if (_cachedTerrain != null)
        {
            Vector3 tPos = _cachedTerrain.transform.position;
            Vector3 tSize = _cachedTerrain.terrainData.size;
            if (pos.x >= tPos.x && pos.x <= tPos.x + tSize.x && pos.z >= tPos.z && pos.z <= tPos.z + tSize.z)
            {
                float groundY = _cachedTerrain.SampleHeight(pos) + tPos.y;
                // 海面（5.50m）より低い、または波打ち際浅瀬（標高7.2m以下）、または西側浅瀬水域
                if (groundY <= OceanSurfaceY + 0.35f || pos.x < 190f || pos.z < 320f || groundY <= 7.2f)
                {
                    isOverWater = true;
                }
            }
            else
            {
                // 地形境界の外側はすべて大海原
                isOverWater = true;
            }
        }
        else
        {
            isOverWater = true;
        }

        return OceanSurfaceY;
    }

    /// <summary>
    /// 左右のV字白波スプレー、水煙ミスト、中央ウェイクパーティクルシステムの生成と配置
    /// </summary>
    void SetupSprayParticles()
    {
        var rootPsGo = new GameObject("SkimmingWakeSprayRoot");
        rootPsGo.transform.SetParent(transform, false);

        // 1. 左右の豪快なV字水しぶき（噴流角度 ±38度、斜め後方へ勢いよく飛散）
        _sprayPsLeft = CreateSprayEmitter(rootPsGo.transform, "WakeSpray_Left", new Vector3(-0.75f, -0.3f, -0.2f), -38f, isMist: false);
        _sprayPsRight = CreateSprayEmitter(rootPsGo.transform, "WakeSpray_Right", new Vector3(0.75f, -0.3f, -0.2f), 38f, isMist: false);

        // 2. 左右にふんわりと広がる白い水煙ミスト（Soft Smoke Texture適用）
        _sprayMistLeft = CreateSprayEmitter(rootPsGo.transform, "WakeMist_Left", new Vector3(-0.85f, -0.1f, -0.4f), -48f, isMist: true);
        _sprayMistRight = CreateSprayEmitter(rootPsGo.transform, "WakeMist_Right", new Vector3(0.85f, -0.1f, -0.4f), 48f, isMist: true);

        // 3. 機体直下の水面を切り裂く中央キール白波航跡
        _wakePsCenter = CreateCenterWakeEmitter(rootPsGo.transform);
    }

    ParticleSystem CreateSprayEmitter(Transform parent, string name, Vector3 localOffset, float angleY, bool isMist)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localOffset;
        go.transform.localRotation = Quaternion.Euler(18f, angleY, 0f);

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = isMist ? new ParticleSystem.MinMaxCurve(0.45f, 0.75f) : new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
        main.startSpeed = isMist ? new ParticleSystem.MinMaxCurve(5f, 11f) : new ParticleSystem.MinMaxCurve(10f, 18f);
        main.startSize = isMist ? new ParticleSystem.MinMaxCurve(0.8f, 1.8f) : new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
        main.startColor = isMist ? new Color(0.96f, 0.99f, 1.0f, 0.55f) : new Color(0.98f, 1.0f, 1.0f, 0.85f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = isMist ? 150 : 260;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = isMist ? 24f : 14f;
        shape.radius = isMist ? 0.35f : 0.18f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.88f, 0.96f, 1f), 1f) },
            new GradientAlphaKey[] {
                new GradientAlphaKey(isMist ? 0.6f : 0.9f, 0f),
                new GradientAlphaKey(isMist ? 0.4f : 0.6f, 0.4f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = grad;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var curve = new AnimationCurve();
        curve.AddKey(0f, 0.3f);
        curve.AddKey(0.4f, 1.2f);
        curve.AddKey(1f, isMist ? 2.6f : 1.6f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var rend = go.GetComponent<ParticleSystemRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader) { name = name + "_Mat" };
        Color baseCol = isMist ? new Color(0.95f, 0.99f, 1f, 0.55f) : new Color(0.98f, 1f, 1f, 0.85f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseCol);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseCol);

        var tex = AdventureRustDrone.GetSoftSmokeTexture();
        if (tex != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        }
        rend.material = mat;

        ps.Play();
        return ps;
    }

    ParticleSystem CreateCenterWakeEmitter(Transform parent)
    {
        var go = new GameObject("CenterWake");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, -0.35f, -0.1f);
        go.transform.localRotation = Quaternion.Euler(5f, 180f, 0f);

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main.startColor = new Color(1f, 1f, 1f, 0.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.5f, 0.1f, 0.8f);

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader) { name = "CenterWake_Mat" };
        var tex = AdventureRustDrone.GetSoftSmokeTexture();
        if (tex != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        }
        rend.material = mat;

        ps.Play();
        return ps;
    }

    /// <summary>
    /// 水面波紋用パーティクルプール（GC Allocゼロ化）
    /// </summary>
    void SetupRippleParticlePool()
    {
        var rippleGo = new GameObject("SkimmingRipplePool");
        rippleGo.transform.SetParent(transform, false);

        _ripplePs = rippleGo.AddComponent<ParticleSystem>();
        var main = _ripplePs.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 0.6f;
        main.startSpeed = 0f;
        main.startSize = 1.6f;
        main.startColor = new Color(0.92f, 0.98f, 1f, 0.75f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 100;

        var emission = _ripplePs.emission;
        emission.rateOverTime = 0f;

        var sizeOver = _ripplePs.sizeOverLifetime;
        sizeOver.enabled = true;
        var sCurve = new AnimationCurve();
        sCurve.AddKey(0f, 0.5f);
        sCurve.AddKey(1f, 3.5f);
        sizeOver.size = new ParticleSystem.MinMaxCurve(1f, sCurve);

        var colorOver = _ripplePs.colorOverLifetime;
        colorOver.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.85f, 0.95f, 1f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0.35f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        colorOver.color = grad;

        var rend = rippleGo.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.HorizontalBillboard; // 水平に寝た円環
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.75f));
        rend.material = mat;
    }

    /// <summary>
    /// スキミング中のパーティクル位置・放出量の動的更新（左右V字水しぶき、水煙ミスト、中央キールウェイク、水面波紋）
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

        // 1. 左右のV字水しぶき
        if (_sprayPsLeft != null)
        {
            _sprayPsLeft.transform.position = waterContactPos - playerRight * 0.75f - playerFwd * 0.25f;
            _sprayPsLeft.transform.rotation = Quaternion.LookRotation(-playerFwd - playerRight * 0.55f, Vector3.up);
            var em = _sprayPsLeft.emission;
            em.rateOverTime = active ? Mathf.Lerp(60f, 320f, SkimIntensity) : 0f;
            if (active && !_sprayPsLeft.isPlaying) _sprayPsLeft.Play();
        }

        if (_sprayPsRight != null)
        {
            _sprayPsRight.transform.position = waterContactPos + playerRight * 0.75f - playerFwd * 0.25f;
            _sprayPsRight.transform.rotation = Quaternion.LookRotation(-playerFwd + playerRight * 0.55f, Vector3.up);
            var em = _sprayPsRight.emission;
            em.rateOverTime = active ? Mathf.Lerp(60f, 320f, SkimIntensity) : 0f;
            if (active && !_sprayPsRight.isPlaying) _sprayPsRight.Play();
        }

        // 2. 左右の水煙ミスト（より外側へふんわり拡散）
        if (_sprayMistLeft != null)
        {
            _sprayMistLeft.transform.position = waterContactPos - playerRight * 0.95f - playerFwd * 0.40f;
            _sprayMistLeft.transform.rotation = Quaternion.LookRotation(-playerFwd - playerRight * 0.75f, Vector3.up);
            var em = _sprayMistLeft.emission;
            em.rateOverTime = active ? Mathf.Lerp(35f, 180f, SkimIntensity) : 0f;
            if (active && !_sprayMistLeft.isPlaying) _sprayMistLeft.Play();
        }

        if (_sprayMistRight != null)
        {
            _sprayMistRight.transform.position = waterContactPos + playerRight * 0.95f - playerFwd * 0.40f;
            _sprayMistRight.transform.rotation = Quaternion.LookRotation(-playerFwd + playerRight * 0.75f, Vector3.up);
            var em = _sprayMistRight.emission;
            em.rateOverTime = active ? Mathf.Lerp(35f, 180f, SkimIntensity) : 0f;
            if (active && !_sprayMistRight.isPlaying) _sprayMistRight.Play();
        }

        // 3. 中央キール白波航跡
        if (_wakePsCenter != null)
        {
            _wakePsCenter.transform.position = waterContactPos - playerFwd * 0.20f;
            _wakePsCenter.transform.rotation = Quaternion.LookRotation(-playerFwd, Vector3.up);
            var em = _wakePsCenter.emission;
            em.rateOverTime = active ? Mathf.Lerp(45f, 200f, SkimIntensity) : 0f;
            if (active && !_wakePsCenter.isPlaying) _wakePsCenter.Play();
        }

        // 4. 水面波紋リングの連続生成（GC Alloc ゼロ: 既存プールからEmit）
        if (active && Time.time - _lastRippleTime > 0.065f)
        {
            _lastRippleTime = Time.time;
            if (_ripplePs != null)
            {
                var emitParams = new ParticleSystem.EmitParams
                {
                    position = waterContactPos + Vector3.up * 0.02f,
                    startSize = Mathf.Lerp(1.4f, 2.8f, SkimIntensity),
                    startLifetime = 0.65f
                };
                _ripplePs.Emit(emitParams, 1);
            }
        }
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
        float targetVol = active ? Mathf.Lerp(0.20f, 0.78f, SkimIntensity) : 0f;
        float targetPitch = active ? Mathf.Lerp(1.10f, 1.48f, SkimIntensity) : 1.0f;

        _sprayAudio.volume = Mathf.MoveTowards(_sprayAudio.volume, targetVol, Time.deltaTime * 6f);
        _sprayAudio.pitch = Mathf.MoveTowards(_sprayAudio.pitch, targetPitch, Time.deltaTime * 3f);
    }

    /// <summary>
    /// 瑞々しい水しぶきと風の擦れ合い、水面を切り裂くWhoosh水流サウンドをシミュレートしたプロシージャル音源
    /// </summary>
    AudioClip SynthesizeWaterSprayRushClip()
    {
        int sampleRate = 44100;
        float duration = 1.2f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        System.Random rnd = new System.Random(88);

        // 複合フィルターによる重低音水圧＋高域水煙スプレーのシミュレーション
        float lpHydro = 0f;
        float bpSpray = 0f;
        float bpHiss = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float white = (float)(rnd.NextDouble() * 2.0 - 1.0);

            // Layer 1: 水面を割る重低音ハイドロランブル (60〜180Hz)
            lpHydro += (white - lpHydro) * 0.055f;

            // Layer 2: 豪快な水流Whoosh (800〜2200Hz)
            bpSpray += (white - bpSpray) * 0.28f;
            float midSpray = (white - bpSpray) * 0.65f;

            // Layer 3: 翼端と水煙の細やかな飛沫ヒス (2500〜5000Hz)
            bpHiss += (white - bpHiss) * 0.62f;
            float highHiss = (white - bpHiss) * 0.35f;

            // 微小な水面のうねり変調（有機的な水面の生きた息吹）
            float swell = 0.88f + 0.12f * Mathf.Sin(t * Mathf.PI * 6f);

            // シームレスループのための端部フェード
            float edgeFade = 1.0f;
            float edgeSec = 0.08f;
            if (t < edgeSec) edgeFade = t / edgeSec;
            else if (t > duration - edgeSec) edgeFade = (duration - t) / edgeSec;

            float combined = (lpHydro * 0.9f + midSpray * 0.75f + highHiss * 0.45f) * swell * edgeFade;
            samples[i] = Mathf.Clamp(combined * 0.48f, -1.0f, 1.0f);
        }

        var clip = AudioClip.Create("WaterSprayRushLoop", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
