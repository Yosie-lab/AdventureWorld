using UnityEngine;

/// <summary>
/// 『Rust & Float』バイオーム別・空間立体アンビエンス音響システム。
/// 島の地形・標高・エリアに応じて環境音をシームレスにクロスフェード：
/// - Coast（砂浜・波打ち際）: 潮騒と寄せては返す波音
/// - Meadow（大草原）: 草を撫でるサラサラ風と微細な虫の音
/// - Forest（東部大樹海・オアシス）: こずえのざわめきと時折さえずる澄んだ小鳥の声
/// - Highland（中央タワー・北崖）: 澄んだ高空のピューという風音と天蓋共鳴
/// - SkyGliding（滑空時）: 速度とダイブに呼応するリアルタイム気流風切り音
/// </summary>
public class AdventureBiomeAmbienceManager : MonoBehaviour
{
    public static AdventureBiomeAmbienceManager Instance { get; private set; }

    public enum BiomeType
    {
        Coast,      // 砂浜・波打ち際（標高 < 12m、半径 > 340m）
        Meadow,     // 西側大草原（標高 12〜38m、西側 X < 450m）
        Forest,     // 東部樹海・カルデラ湖畔・オアシス（X > 400m、Z 350〜650m、池周辺）
        Highland,   // 中央タワー・北崖（標高 > 54m）
    }

    [Header("Current Status")]
    public BiomeType currentBiome = BiomeType.Coast;
    public float currentGlidingFactor = 0f;

    // 各バイオーム用の環境ループ音源
    AudioSource _meadowWindSource;
    AudioSource _forestRustleSource;
    AudioSource _highlandWindSource;
    AudioSource _glidingWindSource;
    AudioSource _birdChirpSource;

    // 合成オーディオクリップ
    AudioClip _meadowWindClip;
    AudioClip _forestRustleClip;
    AudioClip _highlandWindClip;
    AudioClip _glidingWindClip;
    AudioClip[] _birdChirpClips;

    // 音量目標値と現在値（スムーズなクロスフェード用）
    float _targetMeadowVol = 0f;
    float _targetForestVol = 0f;
    float _targetHighlandVol = 0f;
    float _targetGlidingVol = 0f;

    float _curMeadowVol = 0f;
    float _curForestVol = 0f;
    float _curHighlandVol = 0f;
    float _curGlidingVol = 0f;

    float _nextBirdChirpTime = 0f;

    // 基準音量（BGMを邪魔しないオーガニックなレベル）
    const float BaseMeadowVol = 0.16f;
    const float BaseForestVol = 0.18f;
    const float BaseHighlandVol = 0.22f;
    const float BaseGlidingVol = 0.32f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        Ensure();
    }

    public static void Ensure()
    {
        if (Instance != null) return;
        var existing = Object.FindFirstObjectByType<AdventureBiomeAmbienceManager>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject("AdventureBiomeAmbienceManager");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AdventureBiomeAmbienceManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        SynthesizeAmbienceClips();
        SetupAudioSources();
    }

    void Start()
    {
        _nextBirdChirpTime = Time.time + Random.Range(3.5f, 7.0f);
    }

    void SetupAudioSources()
    {
        _meadowWindSource = CreateLoopSource("MeadowWind", _meadowWindClip);
        _forestRustleSource = CreateLoopSource("ForestRustle", _forestRustleClip);
        _highlandWindSource = CreateLoopSource("HighlandWind", _highlandWindClip);
        _glidingWindSource = CreateLoopSource("GlidingWind", _glidingWindClip);

        var birdGo = new GameObject("BirdChirpSource");
        birdGo.transform.SetParent(transform, false);
        _birdChirpSource = birdGo.AddComponent<AudioSource>();
        _birdChirpSource.playOnAwake = false;
        _birdChirpSource.spatialBlend = 0.35f;
        _birdChirpSource.loop = false;
    }

    AudioSource CreateLoopSource(string name, AudioClip clip)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.volume = 0f;
        src.spatialBlend = 0.0f; // 2D全方位アンビエント
        src.playOnAwake = false;
        src.Play();
        return src;
    }

    void Update()
    {
        var player = AdventurePlayerController.Instance;
        if (player == null) return;

        Vector3 pos = player.transform.position;
        bool isGliding = player.IsGliding;

        // 1. 現在のバイオーム判定
        currentBiome = EvaluateBiome(pos);

        // 2. 滑空風量判定（滑空速度や高度・ブーストに応じた気流風）
        float targetGlideFactor = isGliding ? 1.0f : 0.0f;
        if (isGliding && player.IsBoostActive) targetGlideFactor = 1.5f;
        currentGlidingFactor = Mathf.MoveTowards(currentGlidingFactor, targetGlideFactor, Time.deltaTime * 3.0f);

        // 3. 各バイオームの目標音量計算
        float seScale = PlayerPrefs.GetFloat("Adventure_SeVolume", 1.0f);

        _targetMeadowVol = (currentBiome == BiomeType.Meadow) ? (BaseMeadowVol * seScale) : 0f;
        _targetForestVol = (currentBiome == BiomeType.Forest) ? (BaseForestVol * seScale) : 0f;
        _targetHighlandVol = (currentBiome == BiomeType.Highland) ? (BaseHighlandVol * seScale) : 0f;
        _targetGlidingVol = BaseGlidingVol * currentGlidingFactor * seScale;

        // 滑空時は地上の環境音を少しダッキング（風の疾走感を際立たせる）
        if (currentGlidingFactor > 0.2f)
        {
            float groundDucking = 1f - Mathf.Clamp01(currentGlidingFactor * 0.6f);
            _targetMeadowVol *= groundDucking;
            _targetForestVol *= groundDucking;
            _targetHighlandVol *= groundDucking;
        }

        // 4. スムーズな音量フェード（時定数約2.0秒）
        float fadeSpeed = Time.deltaTime * 0.85f;
        _curMeadowVol = Mathf.MoveTowards(_curMeadowVol, _targetMeadowVol, fadeSpeed);
        _curForestVol = Mathf.MoveTowards(_curForestVol, _targetForestVol, fadeSpeed);
        _curHighlandVol = Mathf.MoveTowards(_curHighlandVol, _targetHighlandVol, fadeSpeed);
        _curGlidingVol = Mathf.MoveTowards(_curGlidingVol, _targetGlidingVol, Time.deltaTime * 2.5f);

        if (_meadowWindSource != null) _meadowWindSource.volume = _curMeadowVol;
        if (_forestRustleSource != null) _forestRustleSource.volume = _curForestVol;
        if (_highlandWindSource != null) _highlandWindSource.volume = _curHighlandVol;
        if (_glidingWindSource != null)
        {
            _glidingWindSource.volume = _curGlidingVol;
            // ダイブ急降下時は風切り音のピッチがフワッと上がる
            _glidingWindSource.pitch = 0.95f + currentGlidingFactor * 0.25f;
        }

        // 5. 森の小鳥の自律さえずり（Forestバイオーム滞在時）
        if (currentBiome == BiomeType.Forest && _curForestVol > 0.08f)
        {
            if (Time.time >= _nextBirdChirpTime)
            {
                _nextBirdChirpTime = Time.time + Random.Range(5.0f, 10.5f);
                PlayRandomBirdChirp(pos, seScale);
            }
        }
    }

    BiomeType EvaluateBiome(Vector3 pos)
    {
        // 高地・中央タワー（標高54m以上、またはタワー白大理石テラス周辺）
        if (pos.y >= 54.0f)
            return BiomeType.Highland;

        // 海岸線（波打ち際・砂浜：標高12m以下、または島中心(512,512)から半径340m以上の外周海域）
        float distFromCenter = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(512f, 512f));
        if (pos.y <= 11.5f || distFromCenter >= 345f)
            return BiomeType.Coast;

        // 東部樹海・カルデラ湖畔・湧水オアシス（X > 430m、カルデラ湖やオアシス池・森のテラス）
        if (pos.x >= 430f || (pos.z >= 420f && pos.z <= 620f && pos.x >= 380f))
            return BiomeType.Forest;

        // それ以外（西側大草原・焚き火キャンプ跡から草原の古木・大河）
        return BiomeType.Meadow;
    }

    void PlayRandomBirdChirp(Vector3 playerPos, float seScale)
    {
        if (_birdChirpClips == null || _birdChirpClips.Length == 0 || _birdChirpSource == null) return;

        var clip = _birdChirpClips[Random.Range(0, _birdChirpClips.Length)];
        if (clip != null)
        {
            // プレイヤーの周囲ランダムな木々の梢から聴こえるようにパンとピッチを調整
            _birdChirpSource.pitch = Random.Range(0.96f, 1.05f);
            _birdChirpSource.volume = Random.Range(0.14f, 0.22f) * seScale;
            _birdChirpSource.PlayOneShot(clip);
        }
    }

    #region Procedural Ambience Synthesis
    void SynthesizeAmbienceClips()
    {
        _meadowWindClip = SynthesizeMeadowWind();
        _forestRustleClip = SynthesizeForestRustle();
        _highlandWindClip = SynthesizeHighlandWind();
        _glidingWindClip = SynthesizeGlidingWind();

        _birdChirpClips = new AudioClip[]
        {
            SynthesizeBirdChirp("BirdChirp_1", 2800f, 3400f, 0.28f),
            SynthesizeBirdChirp("BirdChirp_2", 3200f, 2600f, 0.22f),
            SynthesizeBirdChirp("BirdChirp_3", 3000f, 3800f, 0.35f)
        };
    }

    /// <summary>草原の風：乾いた草を撫でるサラサラとした風（低〜中域のオーガニックなゆらぎ）</summary>
    AudioClip SynthesizeMeadowWind()
    {
        const int rate = 44100;
        const float duration = 8.0f;
        int count = (int)(rate * duration);
        float[] samples = new float[count];

        float filterVal = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            // ピンクノイズ生成
            float white = Random.value * 2f - 1f;
            filterVal = Mathf.Lerp(filterVal, white, 0.055f);

            // 風のうねり（1.8秒〜3.5秒周期で優しく強弱が変化）
            float swell = Mathf.Sin(t * 1.4f) * 0.25f + Mathf.Sin(t * 0.7f) * 0.35f + 0.55f;
            // 草擦れの微細な高域成分
            float grassRustle = (Random.value * 2f - 1f) * 0.08f * Mathf.Clamp01(filterVal);

            samples[i] = (filterVal * swell + grassRustle) * 0.42f;
        }

        var clip = AudioClip.Create("MeadowWindLoop", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>森のこずえ：木々の葉がザワザワと擦れ合う深みのあるアンビエンス</summary>
    AudioClip SynthesizeForestRustle()
    {
        const int rate = 44100;
        const float duration = 8.0f;
        int count = (int)(rate * duration);
        float[] samples = new float[count];

        float lowFilter = 0f;
        float midFilter = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float white = Random.value * 2f - 1f;

            // 2段階バンドパスで木々のざわめき（重厚な低域風＋葉擦れのサワサワ音）
            lowFilter = Mathf.Lerp(lowFilter, white, 0.035f);
            midFilter = Mathf.Lerp(midFilter, white - lowFilter, 0.12f);

            float rustleSwell = Mathf.Sin(t * 1.8f) * 0.3f + Mathf.Sin(t * 0.9f) * 0.35f + 0.5f;
            samples[i] = (lowFilter * 0.6f + midFilter * 0.45f) * rustleSwell * 0.48f;
        }

        var clip = AudioClip.Create("ForestRustleLoop", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>高地の風：標高60mのタワーテラス・崖に吹き抜ける澄んだ高空風</summary>
    AudioClip SynthesizeHighlandWind()
    {
        const int rate = 44100;
        const float duration = 8.0f;
        int count = (int)(rate * duration);
        float[] samples = new float[count];

        float highFilter = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float white = Random.value * 2f - 1f;
            highFilter = Mathf.Lerp(highFilter, white, 0.085f);

            // 高空特有のピューという澄んだ風の波長
            float whistle = Mathf.Sin(2f * Mathf.PI * (580f + Mathf.Sin(t * 1.2f) * 80f) * t) * 0.06f;
            float swell = Mathf.Sin(t * 1.1f) * 0.35f + 0.65f;

            samples[i] = (highFilter * swell + whistle) * 0.45f;
        }

        var clip = AudioClip.Create("HighlandWindLoop", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>滑空の気流：翼を切り裂く爽快なゴーッという疾走風切り音</summary>
    AudioClip SynthesizeGlidingWind()
    {
        const int rate = 44100;
        const float duration = 4.0f;
        int count = (int)(rate * duration);
        float[] samples = new float[count];

        float glideFilter = 0f;
        for (int i = 0; i < count; i++)
        {
            float white = Random.value * 2f - 1f;
            // 疾走感のある広帯域風切りノイズ
            glideFilter = Mathf.Lerp(glideFilter, white, 0.18f);
            samples[i] = glideFilter * 0.55f;
        }

        var clip = AudioClip.Create("GlidingWindLoop", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>澄んだ森の小鳥のさえずり（ピピ、チッチッ…）</summary>
    static AudioClip SynthesizeBirdChirp(string name, float startFreq, float endFreq, float duration)
    {
        const int rate = 44100;
        int count = (int)(rate * duration);
        float[] samples = new float[count];
        float phase = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float p = t / duration;

            // 周波数変調（小鳥の愛らしい上下スラー）
            float freq = Mathf.Lerp(startFreq, endFreq, p) + Mathf.Sin(p * Mathf.PI * 4f) * 250f;
            phase += 2f * Mathf.PI * freq / rate;

            // ベル型のエンベロープ（柔らかいアタックとディケイ）
            float env = Mathf.Sin(p * Mathf.PI);
            float wave = Mathf.Sin(phase) + Mathf.Sin(phase * 2f) * 0.2f;

            samples[i] = Mathf.Clamp(wave * env * 0.75f, -1f, 1f);
        }

        var clip = AudioClip.Create(name, count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
    #endregion
}
