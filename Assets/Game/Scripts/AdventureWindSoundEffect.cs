using UnityEngine;

/// <summary>
/// 70mメガジャンプ・急上昇気流・高速滑空・ブースト時にプレイヤーの耳元でダイナミックに唸るリアルタイム風切り音演出
/// 速度（SpeedIntensity）や上昇状態に応じて音量とピッチが有機的に変化し、圧倒的な疾走感と高低差の臨場感を創出
/// </summary>
[DisallowMultipleComponent]
public class AdventureWindSoundEffect : MonoBehaviour
{
    private AudioSource _audio;
    private AdventurePlayerController _player;
    private static AdventureWindSoundEffect _instance;

    public static AdventureWindSoundEffect Instance => _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        var player = AdventurePlayerController.Resolve();
        if (player != null && player.GetComponent<AdventureWindSoundEffect>() == null)
        {
            player.gameObject.AddComponent<AdventureWindSoundEffect>();
        }
    }

    void Awake()
    {
        _instance = this;
        _player = GetComponent<AdventurePlayerController>();
        SetupAudio();
    }

    void Start()
    {
        if (_player == null)
            _player = AdventurePlayerController.Resolve();
    }

    private float _cachedSeVol = 1f;
    private float _lastSeVolCheckTime = -10f;

    void Update()
    {
        if (_player == null)
        {
            _player = AdventurePlayerController.Resolve();
            if (_player == null) return;
        }

        if (Time.unscaledTime - _lastSeVolCheckTime > 0.5f)
        {
            _lastSeVolCheckTime = Time.unscaledTime;
            _cachedSeVol = PlayerPrefs.GetFloat("Adventure_SeVolume", 1f);
        }
        float seVol = _cachedSeVol;
        float intensity = _player.SpeedIntensity;
        bool isGrounded = _player.IsGrounded;
        bool isAscending = _player.IsUpdraftAscending;
        bool isGliding = _player.IsGliding;

        // タワー崩壊後の上昇（光の柱上昇）および天蓋崩壊〜クライマックス〜エピローグ演出中は追加風切り音を完全無効化
        bool isTowerAscendOrClimax = _player.IsSkybreakPillarAscending
            || AdventureSanctuaryTowerManager.IsCanopyBroken
            || (AdventureSanctuaryTowerManager.Instance != null && AdventureSanctuaryTowerManager.Instance.IsCanopyToEpilogueSequence)
            || AdventureStoryFlow.KeepsAirborne(_player);

        // 有効なフライト・急上昇状態（滑空、またはメガジャンプ急上昇、または高高度急降下）
        bool isValidWindFlight = !isGrounded && (isGliding || isAscending || intensity > 0.40f);

        // 接地中、森の木の間などの通常歩行中、または非フライト時は瞬時に完全無音化
        if (isTowerAscendOrClimax || !isValidWindFlight || intensity < 0.12f)
        {
            _audio.volume = Mathf.MoveTowards(_audio.volume, 0f, Time.deltaTime * 10.0f);
            if (_audio.volume <= 0.001f && _audio.isPlaying)
                _audio.Stop();
            return;
        }

        // 空中での速度に応じた音量計算（メガジャンプ時は最大 0.85）
        float targetVolume = Mathf.Lerp(0.08f, isAscending ? 0.85f : 0.65f, (intensity - 0.12f) / 0.88f) * seVol;
        _audio.volume = Mathf.MoveTowards(_audio.volume, targetVolume, Time.deltaTime * (isAscending ? 4.5f : 2.5f));

        // 速度に応じたピッチ変化（通常0.92 → 最高速1.38で風が甲高く切り裂く）
        float targetPitch = Mathf.Lerp(0.92f, isAscending ? 1.38f : 1.25f, intensity);
        _audio.pitch = Mathf.MoveTowards(_audio.pitch, targetPitch, Time.deltaTime * 2.0f);

        if (!_audio.isPlaying && _audio.volume > 0.01f)
            _audio.Play();
        else if (_audio.isPlaying && _audio.volume <= 0.001f && isGrounded)
            _audio.Stop();
    }

    void SetupAudio()
    {
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.spatialBlend = 0f; // 2Dステレオ（プレイヤーの耳元で全方位から包み込む風圧）
        _audio.loop = true;
        _audio.playOnAwake = false;
        _audio.volume = 0f;
        _audio.pitch = 1.0f;
        _audio.clip = SynthesizeContinuousWindClip();
        _audio.Play();
    }

    /// <summary>重低音の風圧ランブルと高域の風切り音を含む高音質シームレスループ風音のプロシージャル合成</summary>
    static AudioClip SynthesizeContinuousWindClip()
    {
        const int rate = 44100;
        float duration = 3.0f; // 3秒のシームレスループ
        int count = (int)(rate * duration);
        float[] data = new float[count];
        var rng = new System.Random(8888);

        float prevLow = 0f;
        float prevMid = 0f;
        float prevHigh = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float rawNoise = (float)(rng.NextDouble() * 2.0 - 1.0);

            // 1. 低周波の風圧・重低音ランブル（大気のうねり）
            prevLow = Mathf.Lerp(prevLow, rawNoise, 0.045f);
            float lowRumble = prevLow * 1.4f;

            // 2. 中域の安定したジェット気流
            prevMid = Mathf.Lerp(prevMid, rawNoise, 0.18f);
            float midWind = (prevMid - prevLow) * 0.95f;

            // 3. 高域の爽快な風切り音（ヒューッという高音）
            prevHigh = Mathf.Lerp(prevHigh, rawNoise, 0.48f);
            float highWhoosh = (prevHigh - prevMid) * 0.65f;

            // わずかな息遣いのようなうねり（LFO）
            float lfo = 1.0f + 0.18f * Mathf.Sin(t * Mathf.PI * 2f * 1.33f) + 0.12f * Mathf.Sin(t * Mathf.PI * 2f * 2.85f);

            data[i] = Mathf.Clamp((lowRumble * 0.75f + midWind * 0.85f + highWhoosh * 0.90f) * lfo * 0.45f, -1f, 1f);
        }

        // ループ端点のクロスフェード（プチノイズ防止）
        int fadeLen = (int)(rate * 0.05f);
        for (int i = 0; i < fadeLen; i++)
        {
            float factor = i / (float)fadeLen;
            data[i] = Mathf.Lerp(data[count - fadeLen + i], data[i], factor);
        }

        var clip = AudioClip.Create("ContinuousWindWhoosh", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
