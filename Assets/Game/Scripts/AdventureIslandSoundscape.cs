using UnityEngine;

/// <summary>
/// 『Rust & Float』海辺の潮騒・風・鳥のさえずり（環境立体音響）システム。
/// 砂浜に近づくと心地よい波の引き波音が聞こえ、森では木々のざわめきと鳥の声が自然にフェードインする。
/// AdventureBiomeAmbienceManager と完全協調し、プレイヤーの現在地に基づいた高品質な環境音響を提供する。
/// </summary>
public class AdventureIslandSoundscape : MonoBehaviour
{
    private static AdventureIslandSoundscape _instance;
    public static AdventureIslandSoundscape Instance => _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = Object.FindAnyObjectByType<AdventureIslandSoundscape>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("AdventureIslandSoundscape");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AdventureIslandSoundscape>();
    }

    private AdventurePlayerController _player;
    private AudioSource _surfAudio;
    private AudioSource _forestAudio;
    private AudioSource _birdSpatialAudio;

    private float _nextBirdChirpTimer = 4.0f;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        SetupSoundscapeSources();
    }

    void SetupSoundscapeSources()
    {
        // 1. 砂浜の引き波音（心地よい打ち寄せ＆引き波のサラサラ音）
        var surfGo = new GameObject("BeachSurfAudio");
        surfGo.transform.SetParent(transform, false);
        _surfAudio = surfGo.AddComponent<AudioSource>();
        _surfAudio.loop = true;
        _surfAudio.spatialBlend = 0f;
        _surfAudio.volume = 0f;
        _surfAudio.clip = SynthesizeSurfWaveClip();
        _surfAudio.Play();

        // 2. 樹海の木々のざわめき（こずえを抜ける心地よい葉擦れ風）
        var forestGo = new GameObject("ForestCanopyAudio");
        forestGo.transform.SetParent(transform, false);
        _forestAudio = forestGo.AddComponent<AudioSource>();
        _forestAudio.loop = true;
        _forestAudio.spatialBlend = 0f;
        _forestAudio.volume = 0f;
        _forestAudio.clip = SynthesizeCanopyRustleClip();
        _forestAudio.Play();

        // 3. 空間小鳥さえずり用
        var birdGo = new GameObject("ForestBirdAudio");
        birdGo.transform.SetParent(transform, false);
        _birdSpatialAudio = birdGo.AddComponent<AudioSource>();
        _birdSpatialAudio.loop = false;
        _birdSpatialAudio.spatialBlend = 0.45f;
    }

    void Update()
    {
        if (_player == null)
        {
            _player = AdventurePlayerController.Resolve();
            if (_player == null) return;
        }

        Vector3 pPos = _player.transform.position;
        float seScale = PlayerPrefs.GetFloat("Adventure_SeVolume", 1.0f);

        // ── 砂浜（Coast/Beach）接近度判定 ──
        // 西ビーチ（168, 6.3, 254）または外洋海岸線（Y < 12m または 中心から320m以上）
        float distToBeach = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(168f, 254f));
        float beachProximity = 0f;
        if (pPos.y < 12.5f)
        {
            beachProximity = Mathf.Clamp01(1.0f - (pPos.y - 5.5f) / 7.0f);
        }
        if (distToBeach < 90f)
        {
            beachProximity = Mathf.Max(beachProximity, Mathf.Clamp01(1.0f - distToBeach / 90f));
        }

        // ── 樹海（Forest）接近度判定 ──
        // 東部樹海エリア（X: 380〜800m, Z: 350〜750m）
        float forestProximity = 0f;
        if (pPos.x > 360f && pPos.y < 48f)
        {
            forestProximity = Mathf.Clamp01((pPos.x - 360f) / 90f);
        }

        // 音量の滑らかなフェードイン／アウト（Lerp）
        float targetSurfVol = beachProximity * 0.38f * seScale;
        float targetForestVol = forestProximity * 0.32f * seScale;

        if (_surfAudio != null)
            _surfAudio.volume = Mathf.MoveTowards(_surfAudio.volume, targetSurfVol, Time.deltaTime * 0.45f);

        if (_forestAudio != null)
            _forestAudio.volume = Mathf.MoveTowards(_forestAudio.volume, targetForestVol, Time.deltaTime * 0.45f);

        // ── 森での小鳥の自然なさえずり ──
        if (forestProximity > 0.25f)
        {
            _nextBirdChirpTimer -= Time.deltaTime;
            if (_nextBirdChirpTimer <= 0f)
            {
                _nextBirdChirpTimer = Random.Range(6.0f, 13.0f);
                PlayGentleBirdChirp(pPos, forestProximity * seScale);
            }
        }
    }

    void PlayGentleBirdChirp(Vector3 playerPos, float volScale)
    {
        if (_birdSpatialAudio == null) return;

        // プレイヤーの周囲ランダムな木立の位置から鳴く
        Vector3 birdPos = playerPos + Random.insideUnitSphere * 14f + Vector3.up * 4f;
        _birdSpatialAudio.transform.position = birdPos;
        _birdSpatialAudio.pitch = Random.Range(0.95f, 1.08f);
        _birdSpatialAudio.volume = Random.Range(0.18f, 0.28f) * volScale;

        var clip = SynthesizeSingleBirdChirpClip();
        if (clip != null)
        {
            _birdSpatialAudio.PlayOneShot(clip);
        }
    }

    /// <summary>
    /// 砂浜の打ち寄せと心地よい引き波（Surf Wash & Recede）のプロシージャル合成
    /// </summary>
    static AudioClip SynthesizeSurfWaveClip()
    {
        int rate = 44100;
        float dur = 6.0f; // 6秒周期の自然な波の呼吸
        int count = (int)(rate * dur);
        float[] data = new float[count];

        System.Random rnd = new System.Random(107);
        float lp = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float white = (float)(rnd.NextDouble() * 2.0 - 1.0);

            // ローパスで柔らかな「ザー…」という波音へ
            lp += (white - lp) * 0.08f;

            // 打ち寄せ（2秒）と長い引き波（4秒）のエンベロープ
            float cycle = (t % dur) / dur;
            float waveEnv = (cycle < 0.35f)
                ? Mathf.Sin((cycle / 0.35f) * Mathf.PI * 0.5f)
                : Mathf.Pow(Mathf.Cos(((cycle - 0.35f) / 0.65f) * Mathf.PI * 0.5f), 1.5f);

            data[i] = lp * waveEnv * 0.35f;
        }

        var clip = AudioClip.Create("IslandSurfWaveLoop", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// 樹海の梢を揺らすサラサラとした木々のざわめき（Canopy Rustle）のプロシージャル合成
    /// </summary>
    static AudioClip SynthesizeCanopyRustleClip()
    {
        int rate = 44100;
        float dur = 4.0f;
        int count = (int)(rate * dur);
        float[] data = new float[count];

        System.Random rnd = new System.Random(219);
        float bp = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float white = (float)(rnd.NextDouble() * 2.0 - 1.0);

            // バンドパスで葉擦れのサラサラした周波数帯（1.2k〜3.5kHz）を抽出
            bp += (white - bp) * 0.22f;
            float rustle = (white - bp) * 0.35f;

            // 風のゆらぎ
            float breeze = 0.75f + 0.25f * Mathf.Sin(t * Mathf.PI * 1.5f);

            data[i] = rustle * breeze * 0.28f;
        }

        var clip = AudioClip.Create("IslandCanopyRustleLoop", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// 澄んだ小鳥の愛らしいさえずり（ピピピッ、チチチ…）のプロシージャル合成
    /// </summary>
    static AudioClip SynthesizeSingleBirdChirpClip()
    {
        int rate = 44100;
        float dur = 0.45f;
        int count = (int)(rate * dur);
        float[] data = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            // 2.8kHz〜4.2kHzのさえずり周波数変調
            float freq = 3200f + 900f * Mathf.Sin(t * 85f);
            float env = Mathf.Sin((t / dur) * Mathf.PI);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.22f;
        }

        var clip = AudioClip.Create("SingleBirdChirp", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

