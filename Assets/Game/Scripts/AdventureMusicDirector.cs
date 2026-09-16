using UnityEngine;
using System.Collections;

/// <summary>
/// 『Rust & Float』2050年世界観に寄り添うプロシージャル・アンビエントBGMディレクター。
/// 通常時はノスタルジックなフェルトピアノ＆アンビエントパッドを優しく奏で、
/// 天蓋破壊時は壮大な天空突破オーケストラル・シンセへシームレスに転調する。
/// </summary>
public class AdventureMusicDirector : MonoBehaviour
{
    public static AdventureMusicDirector Instance { get; private set; }

    AudioSource _bgmSourceA;
    AudioSource _bgmSourceB;
    AudioClip _ambientThemeClip;
    AudioClip _skybreakThemeClip;

    bool _hasSwitchedToSkybreak = false;
    /// <summary>一度天空テーマに入ったら、ニューゲーム／F9再演まで戻さない</summary>
    bool _keepEndingThemeUntilQuit = false;
    const float EndingThemeVolume = 0.78f;

    public static void Ensure()
    {
        if (Instance != null) return;
        var existing = FindAnyObjectByType<AdventureMusicDirector>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject("AdventureMusicDirector");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AdventureMusicDirector>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _bgmSourceA = gameObject.AddComponent<AudioSource>();
        _bgmSourceA.loop = true;
        _bgmSourceA.playOnAwake = false;
        _bgmSourceA.spatialBlend = 0f; // 2Dステレオ
        _bgmSourceA.volume = 0f;
        _bgmSourceA.ignoreListenerPause = true;

        _bgmSourceB = gameObject.AddComponent<AudioSource>();
        _bgmSourceB.loop = true;
        _bgmSourceB.playOnAwake = false;
        _bgmSourceB.spatialBlend = 0f;
        _bgmSourceB.volume = 0f;
        _bgmSourceB.ignoreListenerPause = true;

        GenerateMusicClips();
    }

    void Start()
    {
        if (_keepEndingThemeUntilQuit)
        {
            KeepEndingThemePlaying(restartIfNeeded: true);
            return;
        }

        if (_ambientThemeClip != null)
        {
            _bgmSourceA.clip = _ambientThemeClip;
            _bgmSourceA.Play();
            StartCoroutine(FadeVolume(_bgmSourceA, 0.30f, 3.5f));
        }
    }

    void Update()
    {
        // 天蓋破壊フラグを監視（F9再演でフラグが戻ったあとも再トリガー可）
        if (!_hasSwitchedToSkybreak && AdventureSanctuaryTowerManager.IsCanopyBroken)
            PlaySkybreakTheme(force: false);

        // シーケンス後〜アプリ終了まで天空BGMを維持（止まっていたらだけ復帰）
        if (_keepEndingThemeUntilQuit && _bgmSourceB != null)
        {
            if (!_bgmSourceB.isPlaying || _bgmSourceB.clip != _skybreakThemeClip)
                KeepEndingThemePlaying(restartIfNeeded: true);
        }
    }

    /// <summary>エンディング〜クリア後もBGMを止めない（ゲーム終了／ニューゲームまで）</summary>
    public void KeepEndingThemeUntilQuit()
    {
        _keepEndingThemeUntilQuit = true;
        _hasSwitchedToSkybreak = true;
        KeepEndingThemePlaying(restartIfNeeded: true);
    }

    void KeepEndingThemePlaying(bool restartIfNeeded)
    {
        if (_skybreakThemeClip == null)
            _skybreakThemeClip = GenerateSkybreakTheme();
        if (_skybreakThemeClip == null || _bgmSourceB == null) return;

        bool needsRestart = restartIfNeeded
            || !_bgmSourceB.isPlaying
            || _bgmSourceB.clip != _skybreakThemeClip;

        if (!needsRestart)
        {
            // 再生中なら音量だけ保証（フェード中の上書きはしない）
            if (_bgmSourceB.volume >= EndingThemeVolume * 0.5f
                && _bgmSourceB.volume < EndingThemeVolume * 0.95f)
                _bgmSourceB.volume = EndingThemeVolume;
            return;
        }

        StopAllCoroutines();
        if (_bgmSourceA != null)
        {
            _bgmSourceA.Stop();
            _bgmSourceA.volume = 0f;
        }
        _bgmSourceB.clip = _skybreakThemeClip;
        _bgmSourceB.loop = true;
        _bgmSourceB.volume = EndingThemeVolume;
        if (!_bgmSourceB.isPlaying)
            _bgmSourceB.Play();
    }

    /// <summary>F9再演／ニューゲームで天蓋前に戻すときだけBGMを探索曲へ戻す</summary>
    public void ResetSkybreakMusicState()
    {
        _keepEndingThemeUntilQuit = false;
        _hasSwitchedToSkybreak = false;
        StopAllCoroutines();
        if (_bgmSourceB != null)
        {
            _bgmSourceB.Stop();
            _bgmSourceB.volume = 0f;
        }
        if (_bgmSourceA != null && _ambientThemeClip != null)
        {
            _bgmSourceA.clip = _ambientThemeClip;
            _bgmSourceA.loop = true;
            if (!_bgmSourceA.isPlaying)
                _bgmSourceA.Play();
            StartCoroutine(FadeVolume(_bgmSourceA, 0.30f, 1.2f));
        }
    }

    /// <summary>天空突破BGMへ切替。force=true で再演時も必ず再生（クリップ再生成）</summary>
    public void PlaySkybreakTheme(bool force = false)
    {
        if (!force && _hasSwitchedToSkybreak) return;
        if (force || _skybreakThemeClip == null)
        {
            _skybreakThemeClip = GenerateSkybreakTheme();
            if (_skybreakThemeClip == null) return;
        }

        _hasSwitchedToSkybreak = true;
        _keepEndingThemeUntilQuit = true;
        TriggerSkybreakMusic();
    }

    void TriggerSkybreakMusic()
    {
        if (_skybreakThemeClip == null) return;

        StopAllCoroutines();
        if (_bgmSourceA != null && _bgmSourceA.isPlaying)
            StartCoroutine(FadeVolume(_bgmSourceA, 0f, 1.2f));

        _bgmSourceB.clip = _skybreakThemeClip;
        _bgmSourceB.loop = true;
        _bgmSourceB.time = 0f;
        _bgmSourceB.volume = 0f;
        _bgmSourceB.Play();
        StartCoroutine(FadeVolume(_bgmSourceB, EndingThemeVolume, 1.0f));
    }

    IEnumerator FadeVolume(AudioSource src, float targetVol, float duration)
    {
        if (src == null) yield break;
        float startVol = src.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // 終了BGMロック中に探索曲へ戻すフェードは中断
            if (_keepEndingThemeUntilQuit && src == _bgmSourceB && targetVol <= 0f)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(startVol, targetVol, elapsed / duration);
            yield return null;
        }
        src.volume = targetVol;
        if (targetVol <= 0f && !(_keepEndingThemeUntilQuit && src == _bgmSourceB))
            src.Stop();
    }

    void GenerateMusicClips()
    {
        _ambientThemeClip = GenerateAmbientTheme();
        _skybreakThemeClip = GenerateSkybreakTheme();
    }

    /// <summary>
    /// ノスタルジックなローファイ・ピアノ＆温かいシンセパッド（約16秒ループ、心地よいCmaj9-Am9-Fmaj7-G6進行）
    /// </summary>
    AudioClip GenerateAmbientTheme()
    {
        int sampleRate = 44100;
        float duration = 16.0f;
        int totalSamples = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[totalSamples * 2]; // Stereo

        // 4小節のコード進行 (BPM = 60, 1コード = 4秒)
        // 0-4s: Cmaj9 (C3, G3, B3, E4, D5)
        // 4-8s: Am9   (A2, E3, G3, C4, B4)
        // 8-12s: Fmaj7 (F2, C3, E3, A3, G4)
        // 12-16s: G6add9(G2, D3, G3, B3, E4)

        float[][] chordRoots = new float[][]
        {
            new float[] { 130.81f, 196.00f, 246.94f, 329.63f, 587.33f }, // Cmaj9
            new float[] { 110.00f, 164.81f, 196.00f, 261.63f, 493.88f }, // Am9
            new float[] { 87.31f,  130.81f, 164.81f, 220.00f, 392.00f }, // Fmaj7
            new float[] { 98.00f,  146.83f, 196.00f, 246.94f, 329.63f }  // G6
        };

        // メロディの音程とタイミング (秒, 周波数, 音量)
        var melodyNotes = new (float time, float freq, float vel)[]
        {
            (0.5f, 523.25f, 0.45f),  // C5
            (1.8f, 587.33f, 0.40f),  // D5
            (2.8f, 659.25f, 0.50f),  // E5
            (4.5f, 587.33f, 0.38f),  // D5
            (5.8f, 493.88f, 0.42f),  // B4
            (7.0f, 440.00f, 0.35f),  // A4
            (8.5f, 523.25f, 0.42f),  // C5
            (9.8f, 659.25f, 0.48f),  // E5
            (11.2f, 783.99f, 0.52f), // G5
            (12.5f, 659.25f, 0.40f), // E5
            (13.8f, 587.33f, 0.38f), // D5
            (14.8f, 493.88f, 0.35f)  // B4
        };

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            int bar = Mathf.Clamp(Mathf.FloorToInt(t / 4.0f), 0, 3);
            float barT = t % 4.0f;
            float[] chord = chordRoots[bar];

            // 1. パッド（温かい持続和音：サイン波＋三角波のブレンド）
            float pad = 0f;
            float padEnv = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(barT / 0.8f)) * Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((barT - 3.2f) / 0.8f));
            for (int k = 0; k < chord.Length; k++)
            {
                float freq = chord[k];
                float s = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f + Mathf.Sin(4f * Mathf.PI * freq * t) * 0.15f;
                pad += s * (0.045f / chord.Length);
            }
            pad *= padEnv;

            // 2. メロディ（優しいフェルトピアノ／ベル音）
            float mel = 0f;
            for (int m = 0; m < melodyNotes.Length; m++)
            {
                var note = melodyNotes[m];
                float noteT = t - note.time;
                if (noteT >= 0f && noteT < 2.5f)
                {
                    // ピアノ風のアタックと指数減衰
                    float env = Mathf.Exp(-noteT * 2.8f) * note.vel;
                    float wave = Mathf.Sin(2f * Mathf.PI * note.freq * noteT)
                               + 0.35f * Mathf.Sin(4f * Mathf.PI * note.freq * noteT)
                               + 0.12f * Mathf.Sin(6f * Mathf.PI * note.freq * noteT);
                    mel += wave * env * 0.14f;
                }
            }

            // ループの端のシームレス・クロスフェード
            float loopFade = 1f;
            if (t < 0.2f) loopFade = t / 0.2f;
            else if (t > duration - 0.2f) loopFade = (duration - t) / 0.2f;

            float mono = (pad + mel) * loopFade;
            samples[i * 2] = mono * 0.95f;     // L
            samples[i * 2 + 1] = mono * 1.05f; // R (わずかなステレオ感)
        }

        var clip = AudioClip.Create("Music_RustFloat_Ambient", totalSamples, 2, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// 天蓋崩壊＆天空ダイブ時の壮大な開放ファンファーレ（約12秒ループ、D Majorの力強い高揚感）
    /// </summary>
    AudioClip GenerateSkybreakTheme()
    {
        int sampleRate = 44100;
        float duration = 12.0f;
        int totalSamples = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[totalSamples * 2];

        // 壮大なシンセ＆ブラス進行 (D -> F#m -> Em -> Gm)
        float[][] chordRoots = new float[][]
        {
            new float[] { 146.83f, 220.00f, 293.66f, 369.99f, 440.00f }, // D
            new float[] { 185.00f, 220.00f, 277.18f, 369.99f, 554.37f }, // F#m
            new float[] { 164.81f, 196.00f, 246.94f, 329.63f, 493.88f }, // Em
            new float[] { 196.00f, 233.08f, 293.66f, 392.00f, 466.16f }  // Gm
        };

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            int bar = Mathf.Clamp(Mathf.FloorToInt(t / 3.0f), 0, 3);
            float barT = t % 3.0f;
            float[] chord = chordRoots[bar];

            // パワフルなブラスシンセパッド
            float brass = 0f;
            float env = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(barT / 0.4f)) * Mathf.SmoothStep(1f, 0.4f, Mathf.Clamp01(barT / 3.0f));
            for (int k = 0; k < chord.Length; k++)
            {
                float freq = chord[k];
                // 矩形波とノコギリ波のブラス倍音
                float s = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.45f
                        + Mathf.Sin(4f * Mathf.PI * freq * t) * 0.25f
                        + Mathf.Sin(6f * Mathf.PI * freq * t) * 0.15f;
                brass += s * (0.11f / chord.Length);
            }
            brass *= env;

            // 駆け上がるキラキラしたアルペジオ
            float arpPhase = (t * 8.0f) % 1.0f;
            int arpIndex = Mathf.FloorToInt(t * 8.0f) % chord.Length;
            float arpFreq = chord[arpIndex] * 2.0f;
            float arpEnv = Mathf.Exp(-arpPhase * 5.0f);
            float arpWave = Mathf.Sin(2f * Mathf.PI * arpFreq * t) * arpEnv * 0.14f;

            float loopFade = 1f;
            if (t < 0.2f) loopFade = t / 0.2f;
            else if (t > duration - 0.2f) loopFade = (duration - t) / 0.2f;

            float mono = (brass + arpWave) * loopFade;
            samples[i * 2] = mono;
            samples[i * 2 + 1] = mono;
        }

        var clip = AudioClip.Create("Music_RustFloat_Skybreak", totalSamples, 2, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
