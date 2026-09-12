using UnityEngine;

/// <summary>
/// 島の外周ビーチ・海岸線に近づいた時に、リアルな潮騒・寄せては返す波音を臨場感たっぷりに再生するマネージャー
/// シーン起動時に自動生成され、プレイヤーの海抜高度・海岸との距離に応じてスムーズにフェードイン/アウトする
/// </summary>
public class AdventureBeachWavesManager : MonoBehaviour
{
    static AdventureBeachWavesManager _instance;
    public static AdventureBeachWavesManager Instance => _instance;

    AudioClip _waveClip;
    AudioSource _waveSourceA;
    AudioSource _waveSourceB;
    Transform _playerTransform;

    [Header("Wave Volume")]
    [Range(0f, 1f)] public float masterVolume = 0.55f;

    const float IslandCenterX = 512f;
    const float IslandCenterZ = 512f;
    const float BeachRadiusMin = 345f; // 内陸からビーチに近づき始める半径
    const float BeachRadiusMax = 445f; // 白砂ビーチのピーク半径

    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = FindObjectOfType<AdventureBeachWavesManager>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("ParadiseBeachWavesManager");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AdventureBeachWavesManager>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        LoadAudioClip();
        SetupAudioSources();
    }

    void LoadAudioClip()
    {
#if UNITY_EDITOR
        _waveClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RustAndFlat/Audio/Ambience/ocean_waves_grand.wav");
        if (_waveClip == null)
            _waveClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/AudioFiles/03_amb/watershore_amb.wav");
#endif
    }

    void SetupAudioSources()
    {
        // 2系統の音源を時間差（17秒ずらし）でクロスブレンドし、寄せては引く波の立体感とうねりを再現
        _waveSourceA = gameObject.AddComponent<AudioSource>();
        _waveSourceA.clip = _waveClip;
        _waveSourceA.loop = true;
        _waveSourceA.spatialBlend = 0.0f; // 2Dステレオで耳全体を心地よく包み込む
        _waveSourceA.volume = 0f;
        _waveSourceA.playOnAwake = false;

        _waveSourceB = gameObject.AddComponent<AudioSource>();
        _waveSourceB.clip = _waveClip;
        _waveSourceB.loop = true;
        _waveSourceB.spatialBlend = 0.0f;
        _waveSourceB.volume = 0f;
        _waveSourceB.playOnAwake = false;

        if (_waveClip != null)
        {
            _waveSourceA.time = 0f;
            _waveSourceA.Play();

            // Bは半周期ずらして再生開始
            _waveSourceB.time = Mathf.Repeat(16.5f, _waveClip.length);
            _waveSourceB.Play();
        }
    }

    void Update()
    {
        if (_playerTransform == null)
        {
            var player = FindObjectOfType<AdventurePlayerController>();
            if (player != null)
                _playerTransform = player.transform;
            else
                return;
        }

        if (_waveClip == null)
        {
            LoadAudioClip();
            if (_waveClip != null && _waveSourceA != null)
            {
                _waveSourceA.clip = _waveClip;
                _waveSourceB.clip = _waveClip;
                _waveSourceA.Play();
                _waveSourceB.Play();
            }
            return;
        }

        Vector3 pos = _playerTransform.position;

        // 1. 半径ベースのビーチ接近度（島の中心 512, 512 からの水平距離）
        float dx = pos.x - IslandCenterX;
        float dz = pos.z - IslandCenterZ;
        float radius = Mathf.Sqrt(dx * dx + dz * dz);
        float radiusFactor = Mathf.InverseLerp(BeachRadiusMin, BeachRadiusMax, radius);

        // 2. 高度ベースの海岸接近度（海抜 5.5m〜10m のビーチテラス）
        float y = pos.y;
        float heightFactor = 0f;
        if (y <= 7.5f)
        {
            heightFactor = 1.0f; // 水辺・波打ち際
        }
        else if (y <= 13.0f)
        {
            heightFactor = Mathf.InverseLerp(13.0f, 7.5f, y); // ビーチ斜面
        }

        // 半径または低高度のどちらかで海岸に近ければ波音が聞こえる
        float targetIntensity = Mathf.Max(radiusFactor, heightFactor);

        // 波の音量をほんの少しだけ引き上げ（masterVolume = 0.55f）
        float targetVolA = targetIntensity * masterVolume;
        float targetVolB = targetIntensity * (masterVolume * 0.85f);

        // スムーズな音量フェード
        float dt = Time.deltaTime;
        _waveSourceA.volume = Mathf.MoveTowards(_waveSourceA.volume, targetVolA, dt * 0.4f);
        _waveSourceB.volume = Mathf.MoveTowards(_waveSourceB.volume, targetVolB, dt * 0.4f);
    }
}
