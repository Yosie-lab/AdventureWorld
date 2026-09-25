using UnityEngine;

/// <summary>
/// niko専用のかわいいトコトコ足音コンポーネント
/// 2Dダイレクト音響（減衰なし）でプレイヤーの耳元にクリアに届き、
/// 歩行・ダッシュの速度に応じて愛らしいリズムで左右の足を交互に奏でる
/// </summary>
public class AdventureNikoFootsteps : MonoBehaviour
{
    CharacterController _cc;
    AdventurePlayerController _player;
    AudioSource _audioSource;

    // 左右のトコトコ足音クリップ
    AudioClip _stepL;
    AudioClip _stepR;

    float _stepTimer = 0f;
    int _stepCount = 0;
    Vector3 _lastPos;

    [Header("Volume & Tuning")]
    [Range(0f, 1f)] public float volume = 0.22f;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _player = GetComponent<AdventurePlayerController>();

        // 足音専用の2D AudioSourceをセットアップ
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0.0f; // 2D音響（カメラ距離に影響されずクリアに聴こえる）
        _audioSource.volume = 1.0f;
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;

        _lastPos = transform.position;
        LoadClips();
    }

    void Start()
    {
        if (_stepL == null || _stepR == null)
            LoadClips();
    }

    void LoadClips()
    {
#if UNITY_EDITOR
        _stepL = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RustAndFloat/Audio/Footsteps/niko_step_L.wav");
        _stepR = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RustAndFloat/Audio/Footsteps/niko_step_R.wav");
#endif

        // フォールバック合成音（万一アセットが未読み込みでも確実に発音を保証）
        if (_stepL == null) _stepL = SynthesizeCuteStep("NikoStep_L", 520f, 360f);
        if (_stepR == null) _stepR = SynthesizeCuteStep("NikoStep_R", 640f, 440f);
    }

    void Update()
    {
        if (_audioSource == null) return;

        // 接地＆滑空判定（空中や滑空時は自然に消音）
        bool isGrounded = (_player != null) ? _player.IsGrounded : (_cc != null && _cc.isGrounded);
        bool isGliding = (_player != null && _player.IsGliding);

        if (!isGrounded || isGliding)
        {
            _stepTimer = 0.15f;
            _lastPos = transform.position;
            return;
        }

        // 実移動速度の計算（水平方向のみ）
        Vector3 curPos = transform.position;
        Vector3 diff = curPos - _lastPos;
        diff.y = 0f;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float speed = diff.magnitude / dt;
        _lastPos = curPos;

        // 移動中判定（歩行: 1.5〜4.2m/s、走行: 7.8m/s、微小な動きでも反応するよう閾値0.25f）
        if (speed > 0.25f)
        {
            // 速度に応じたステップ間隔（歩行: 約0.30秒、走行: 約0.20秒）
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

        // 左右で交互に鳴るかわいいトコトコ音
        AudioClip clip = isLeft ? _stepL : _stepR;

        if (clip != null)
        {
            // 速度に応じた音量スケーリング（歩行時は優しく約0.16、ダッシュ時は約0.22）
            float speedFactor = Mathf.Lerp(0.75f, 1.0f, Mathf.InverseLerp(1.5f, 7.5f, speed));
            float vol = speedFactor * volume;

            // わずかなピッチ揺らぎで機械的な反復感を解消
            _audioSource.pitch = Random.Range(0.97f, 1.03f);
            _audioSource.PlayOneShot(clip, vol);
        }
    }

    /// <summary>小ジャンプ・踏み切り時の軽快なポップホップ音</summary>
    public void PlayJumpSound()
    {
        if (_audioSource == null) return;
        AudioClip clip = _stepR ?? _stepL;
        if (clip != null)
        {
            _audioSource.pitch = 1.35f;
            _audioSource.PlayOneShot(clip, volume * 1.35f);
        }
    }

    /// <summary>フォールバック用の愛らしいポップトコトコ音波形合成</summary>
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
