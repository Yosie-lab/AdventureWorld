using UnityEngine;

/// <summary>
/// niko専用のかわいいトコトコ足音コンポーネント
/// 2Dダイレクト音響（距離減衰なし）で耳元にクリアに届き、
/// 草原・砂浜・水辺それぞれの地面に合わせて愛らしい「トコトコ」「サクッ」「ピチャッ」音を奏でる
/// </summary>
public class AdventureNikoFootsteps : MonoBehaviour
{
    CharacterController _cc;
    AdventurePlayerController _player;
    AudioSource _audioSource;

    // 草地・通常地面用
    AudioClip _stepL;
    AudioClip _stepR;

    // 砂浜用
    AudioClip _sandStepL;
    AudioClip _sandStepR;

    // 浅瀬・波打ち際用
    AudioClip _waterStepL;
    AudioClip _waterStepR;

    float _stepTimer = 0f;
    int _stepCount = 0;
    Vector3 _lastPos;

    [Header("Volume")]
    [Range(0f, 1f)] public float volume = 0.22f;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _player = GetComponent<AdventurePlayerController>();

        // 足音専用のAudioSourceを用意
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0.0f; // 2D音響：カメラ距離に関係なくクリアに届く
        _audioSource.volume = 1.0f;
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;

        _lastPos = transform.position;
        LoadClips();
    }

    void Start()
    {
        if (_stepL == null)
            LoadClips();
    }

    void LoadClips()
    {
#if UNITY_EDITOR
        _stepL = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RustAndFlat/Audio/Footsteps/niko_step_L.wav");
        _stepR = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RustAndFlat/Audio/Footsteps/niko_step_R.wav");
        _sandStepL = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RustAndFlat/Audio/Footsteps/niko_step_sand_L.wav");
        _sandStepR = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RustAndFlat/Audio/Footsteps/niko_step_sand_R.wav");
        _waterStepL = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RustAndFlat/Audio/Footsteps/niko_step_water_L.wav");
        _waterStepR = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RustAndFlat/Audio/Footsteps/niko_step_water_R.wav");
#endif

        // フォールバック合成音
        if (_stepL == null) _stepL = SynthesizeCuteStep("NikoStep_L", 500f, 340f);
        if (_stepR == null) _stepR = SynthesizeCuteStep("NikoStep_R", 620f, 420f);
        if (_sandStepL == null) _sandStepL = _stepL;
        if (_sandStepR == null) _sandStepR = _stepR;
        if (_waterStepL == null) _waterStepL = _stepL;
        if (_waterStepR == null) _waterStepR = _stepR;
    }

    void Update()
    {
        if (_audioSource == null) return;

        // 接地＆滑空判定
        bool isGrounded = (_player != null) ? _player.IsGrounded : (_cc != null && _cc.isGrounded);
        bool isGliding = (_player != null && _player.IsGliding);

        if (!isGrounded || isGliding)
        {
            _stepTimer = 0.15f;
            _lastPos = transform.position;
            return;
        }

        // 実移動速度の計算（水平方向）
        Vector3 curPos = transform.position;
        Vector3 diff = curPos - _lastPos;
        diff.y = 0f;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float speed = diff.magnitude / dt;
        _lastPos = curPos;

        // 移動中判定（歩行: 1.5〜4.2m/s、走行: 7.8m/s、微小な動きでも反応するよう閾値0.25f）
        if (speed > 0.25f)
        {
            float stepInterval = Mathf.Lerp(0.30f, 0.20f, Mathf.InverseLerp(1.5f, 7.5f, speed));
            _stepTimer += Time.deltaTime;

            if (_stepTimer >= stepInterval)
            {
                _stepTimer = 0f;
                PlayStep(speed);
            }
        }
        else
        {
            // 次の歩き出しですぐに1歩目が鳴るよう待機
            _stepTimer = 0.22f;
        }
    }

    void PlayStep(float speed)
    {
        _stepCount++;
        bool isLeft = (_stepCount % 2 == 0);

        // 水辺や海、砂浜、草原すべて共通の愛らしいトコトコ音
        AudioClip clip = isLeft ? _stepL : _stepR;

        if (clip != null)
        {
            // 速度に応じた音量（歩行時: 約0.16、ダッシュ時: 約0.22）
            float speedFactor = Mathf.Lerp(0.75f, 1.0f, Mathf.InverseLerp(1.5f, 7.5f, speed));
            float vol = speedFactor * volume;

            // わずかなピッチ揺らぎで機械感をなくす
            _audioSource.pitch = Random.Range(0.97f, 1.03f);
            _audioSource.PlayOneShot(clip, vol);
        }
    }

    static AudioClip SynthesizeCuteStep(string name, float startFreq, float endFreq)
    {
        const int rate = 44100;
        float duration = 0.12f;
        int count = (int)(rate * duration);
        float[] data = new float[count];
        float phase = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float p = t / duration;
            float freq = Mathf.Lerp(startFreq, endFreq, Mathf.Sqrt(p));
            phase += 2f * Mathf.PI * freq / rate;

            float env = (t < 0.004f) ? (t / 0.004f) : Mathf.Exp(-(t - 0.004f) * 45f) * (1f - p);
            float wave = Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 2f);
            data[i] = Mathf.Clamp(wave * env * 0.9f, -1f, 1f);
        }

        var ac = AudioClip.Create(name, count, 1, rate, false);
        ac.SetData(data, 0);
        return ac;
    }
}
