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
    AudioClip _skybreakIntroClip;
    AudioClip _skybreakOverdriveClip;

    bool _hasSwitchedToSkybreak = false;
    bool _isOverdriveDropActive = false;
    /// <summary>エンディング進行中のみ天空BGMを維持（クリア後の自由探索では解除）</summary>
    bool _keepEndingThemeActive = false;
    /// <summary>エンディング後に探索曲へ戻したら、天蓋開放済みでも天空曲へ再切替しない</summary>
    bool _preferAmbientAfterEnding = false;
    bool _isFadingA = false;
    const float EndingThemeVolume = 1.00f;
    // AudioSource.volume は 1 で頭打ちになる。1 を超える分は生成波形で足す。
    float SkybreakSourceVolume => Mathf.Min(1f, EndingThemeVolume);
    const float AmbientThemeVolume = 0.30f;

    /// <summary>外部（古代ピアノ等の環境スポット）からのダッキング要求度 (0.0 = 通常音量, 1.0 = 最大ダッキング)</summary>
    public float spotDuckingFactor = 0f;

    /// <summary>スポットダッキング係数の設定 (0.0〜1.0)</summary>
    public void SetSpotDucking(float factor)
    {
        spotDuckingFactor = Mathf.Clamp01(factor);
    }

    public static void Ensure()
    {
        if (Instance != null) return;
        var existing = Object.FindFirstObjectByType<AdventureMusicDirector>();
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
        // クリア後の自由探索中に再入したら、天空曲を残さず探索曲へ
        if (ShouldUseExplorationThemeOnBoot())
        {
            RestoreExplorationTheme();
            return;
        }

        if (_keepEndingThemeActive)
        {
            KeepEndingThemePlaying(restartIfNeeded: true);
            return;
        }

        if (_ambientThemeClip != null)
        {
            _bgmSourceA.clip = _ambientThemeClip;
            _bgmSourceA.Play();
            StartCoroutine(FadeVolume(_bgmSourceA, AmbientThemeVolume, 3.5f));
        }
    }

    void Update()
    {
        // エンディング進行中だけ天空BGMを維持（止まっていたら復帰）
        if (_keepEndingThemeActive && _bgmSourceB != null)
        {
            if (!_bgmSourceB.isPlaying || _bgmSourceB.clip != _skybreakThemeClip)
                KeepEndingThemePlaying(restartIfNeeded: true);
            return;
        }

        // クリア後の自由探索で天空曲が残っていたら探索曲へ戻す
        if (!_preferAmbientAfterEnding && AdventureSanctuaryTowerManager.IsGameCleared
            && ShouldUseExplorationThemeOnBoot())
        {
            RestoreExplorationTheme();
            return;
        }

        // 通常探索時：スポットダッキング係数（古代ピアノ接近等）に応じて音量をスムーズに調整
        if (!_keepEndingThemeActive && !_isFadingA && _bgmSourceA != null && _bgmSourceA.isPlaying)
        {
            float targetVol = Mathf.Lerp(AmbientThemeVolume, AmbientThemeVolume * 0.20f, spotDuckingFactor);
            _bgmSourceA.volume = Mathf.MoveTowards(_bgmSourceA.volume, targetVol, Time.deltaTime * 0.45f);
        }

        if (_preferAmbientAfterEnding)
            return;
    }

    static bool ShouldUseExplorationThemeOnBoot() => AdventureStoryFlow.ShouldUseExplorationTheme;

    /// <summary>エンディング進行中：天空BGMを維持</summary>
    public void KeepEndingThemeActive()
    {
        _preferAmbientAfterEnding = false;
        _keepEndingThemeActive = true;
        _hasSwitchedToSkybreak = true;
        KeepEndingThemePlaying(restartIfNeeded: true);
    }

    /// <summary>互換：旧名。エンディング進行中の天空BGM維持</summary>
    public void KeepEndingThemeUntilQuit() => KeepEndingThemeActive();

    /// <summary>エンディング終了後の自由探索：探索アンビエントへ戻す</summary>
    public void RestoreExplorationTheme()
    {
        _keepEndingThemeActive = false;
        _preferAmbientAfterEnding = true;
        _hasSwitchedToSkybreak = false;
        _isOverdriveDropActive = false;
        FadeOutSkybreakAndPlayAmbient(skyFade: 1.4f, ambientFade: 2.0f, stopSkyImmediate: false);
    }

    void KeepEndingThemePlaying(bool restartIfNeeded)
    {
        if (_skybreakThemeClip == null)
            _skybreakThemeClip = _isOverdriveDropActive ? _skybreakOverdriveClip : _skybreakIntroClip;
        if (_skybreakThemeClip == null)
            _skybreakThemeClip = GenerateSkybreakTheme(withRhythmAndBass: _isOverdriveDropActive);
        if (_skybreakThemeClip == null || _bgmSourceB == null) return;

        bool needsRestart = restartIfNeeded
            || !_bgmSourceB.isPlaying
            || _bgmSourceB.clip != _skybreakThemeClip;

        if (!needsRestart)
        {
            if (_bgmSourceB.volume >= SkybreakSourceVolume * 0.5f
                && _bgmSourceB.volume < SkybreakSourceVolume * 0.95f)
                _bgmSourceB.volume = SkybreakSourceVolume;
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
        _bgmSourceB.volume = SkybreakSourceVolume;
        if (!_bgmSourceB.isPlaying)
            _bgmSourceB.Play();
    }

    /// <summary>F9再演／ニューゲームで天蓋前に戻すとき探索曲へ戻す</summary>
    public void ResetSkybreakMusicState()
    {
        _keepEndingThemeActive = false;
        _preferAmbientAfterEnding = false;
        _hasSwitchedToSkybreak = false;
        _isOverdriveDropActive = false;
        _skybreakThemeClip = _skybreakIntroClip;
        FadeOutSkybreakAndPlayAmbient(skyFade: 0f, ambientFade: 1.2f, stopSkyImmediate: true);
    }

    void FadeOutSkybreakAndPlayAmbient(float skyFade, float ambientFade, bool stopSkyImmediate)
    {
        StopAllCoroutines();

        if (_bgmSourceB != null)
        {
            if (stopSkyImmediate || skyFade <= 0f)
            {
                _bgmSourceB.Stop();
                _bgmSourceB.volume = 0f;
            }
            else
            {
                StartCoroutine(FadeVolume(_bgmSourceB, 0f, skyFade));
            }
        }

        if (_ambientThemeClip == null)
            _ambientThemeClip = GenerateAmbientTheme();
        if (_bgmSourceA == null || _ambientThemeClip == null)
            return;

        _bgmSourceA.clip = _ambientThemeClip;
        _bgmSourceA.loop = true;
        if (!_bgmSourceA.isPlaying)
            _bgmSourceA.Play();
        StartCoroutine(FadeVolume(_bgmSourceA, AmbientThemeVolume, ambientFade));
    }

    /// <summary>天空突破BGMへ切替（初期はベースなし神聖版）。force=true で再演時も必ず再生</summary>
    public void PlaySkybreakTheme(bool force = false)
    {
        if (!force && _hasSwitchedToSkybreak) return;
        if (force || _skybreakIntroClip == null)
            _skybreakIntroClip = GenerateSkybreakTheme(withRhythmAndBass: false);
        if (force || _skybreakOverdriveClip == null)
            _skybreakOverdriveClip = GenerateSkybreakTheme(withRhythmAndBass: true);

        _isOverdriveDropActive = false;
        _skybreakThemeClip = _skybreakIntroClip;

        _preferAmbientAfterEnding = false;
        _hasSwitchedToSkybreak = true;
        _keepEndingThemeActive = true;
        TriggerSkybreakMusic();
    }

    /// <summary>
    /// オーバードライブ突入時（「全力で行こう！！」）：
    /// 現在の再生位置を寸分違わず引き継いで、シンセベース＆ビート解禁版へシームレスにドロップ切り替え
    /// </summary>
    public void TriggerSkybreakOverdriveDrop()
    {
        _isOverdriveDropActive = true;
        if (_skybreakOverdriveClip == null)
            _skybreakOverdriveClip = GenerateSkybreakTheme(withRhythmAndBass: true);

        _skybreakThemeClip = _skybreakOverdriveClip;

        if (_bgmSourceB != null)
        {
            float currentTime = _bgmSourceB.time;
            _bgmSourceB.clip = _skybreakOverdriveClip;
            _bgmSourceB.time = currentTime % _skybreakOverdriveClip.length;
            if (!_bgmSourceB.isPlaying)
                _bgmSourceB.Play();
            _bgmSourceB.volume = SkybreakSourceVolume;
            Debug.Log($"[RustAndFloat] ✦ オーバードライブBGMベース解禁ドロップ発動 (time: {currentTime:F2}s)");
        }
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
        StartCoroutine(FadeVolume(_bgmSourceB, SkybreakSourceVolume, 1.0f));
    }

    IEnumerator FadeVolume(AudioSource src, float targetVol, float duration)
    {
        if (src == null) yield break;
        if (src == _bgmSourceA) _isFadingA = true;
        float startVol = src.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // エンディング維持中に天空曲を消すフェードは中断
            if (_keepEndingThemeActive && src == _bgmSourceB && targetVol <= 0f)
            {
                if (src == _bgmSourceA) _isFadingA = false;
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(startVol, targetVol, elapsed / duration);
            yield return null;
        }
        src.volume = targetVol;
        if (targetVol <= 0f && !(_keepEndingThemeActive && src == _bgmSourceB))
            src.Stop();
        if (src == _bgmSourceA) _isFadingA = false;
    }

    void GenerateMusicClips()
    {
        _ambientThemeClip = GenerateAmbientTheme();
        _skybreakIntroClip = GenerateSkybreakTheme(withRhythmAndBass: false);
        _skybreakOverdriveClip = GenerateSkybreakTheme(withRhythmAndBass: true);
        _skybreakThemeClip = _skybreakIntroClip;
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
    /// 天蓋崩壊＆天空ダイブ時の壮大な開放ファンファーレ。
    /// withRhythmAndBass=false：天蓋開放〜凍結危機用の神聖で重厚なブラスパッドのみ（コミカル音なし）。
    /// withRhythmAndBass=true ：オーバードライブ解禁用のタイトなキック・スネア＆モータウンシンセベース。
    /// </summary>
    AudioClip GenerateSkybreakTheme(bool withRhythmAndBass)
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

        // 各コードのルートベース音 (D2, F#2, E2, G2)
        float[] bassRoots = new float[] { 73.42f, 92.50f, 82.41f, 98.00f };

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            int bar = Mathf.Clamp(Mathf.FloorToInt(t / 3.0f), 0, 3);
            float barT = t % 3.0f;
            float[] chord = chordRoots[bar];

            // 1. パワフルで神秘的なシネマティック・ブラスパッド
            float brass = 0f;
            float env = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(barT / 0.4f)) * Mathf.SmoothStep(1f, 0.5f, Mathf.Clamp01(barT / 3.0f));
            for (int k = 0; k < chord.Length; k++)
            {
                float freq = chord[k];
                float s = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.45f
                        + Mathf.Sin(4f * Mathf.PI * freq * t) * 0.25f
                        + Mathf.Sin(6f * Mathf.PI * freq * t) * 0.15f;
                float gain = withRhythmAndBass ? 0.08f : 0.11f;
                brass += s * (gain / chord.Length);
            }
            brass *= env;

            // 2. 駆け上がるキラキラしたアルペジオ（前半・後半ともに美しく鳴り響く）
            float arpPhase = (t * 8.0f) % 1.0f;
            int arpIndex = Mathf.FloorToInt(t * 8.0f) % chord.Length;
            float arpFreq = chord[arpIndex] * 2.0f;
            float arpEnv = Mathf.Exp(-arpPhase * 5.0f);
            float arpWave = Mathf.Sin(2f * Mathf.PI * arpFreq * t) * arpEnv * 0.13f;

            float kick = 0f;
            float snare = 0f;
            float synthBass = 0f;

            // 後半（Rustオーバードライブ後）：16分ストレート・ドライブベースとドラム隊が合流
            if (withRhythmAndBass)
            {
                // ドラム用ノイズ
                uint seed = (uint)(i * 1973 + 9277);
                seed = (seed ^ 61) ^ (seed >> 16);
                seed *= 9;
                seed = seed ^ (seed >> 4);
                seed *= 0x27d4eb2d;
                seed = seed ^ (seed >> 15);
                float noise = ((seed & 0xFFFF) / 32768.0f) - 1.0f;

                // (a) タイトな4つ打ちキック
                float beatT = barT % 0.75f;
                if (beatT < 0.15f)
                {
                    float kPitch = Mathf.Lerp(128f, 44f, Mathf.Clamp01(beatT / 0.08f));
                    float kEnv = Mathf.Exp(-beatT * 25f);
                    kick = Mathf.Sin(2f * Mathf.PI * kPitch * beatT) * kEnv * 0.24f;
                }

                // (b) 2拍・4拍スネア
                float snareT = -1f;
                if (barT >= 0.75f && barT < 1.15f) snareT = barT - 0.75f;
                else if (barT >= 2.25f && barT < 2.65f) snareT = barT - 2.25f;
                if (snareT >= 0f)
                {
                    float sEnv = Mathf.Exp(-snareT * 22f);
                    float sTone = Mathf.Sin(2f * Mathf.PI * 180f * snareT) * 0.35f;
                    snare = (noise * 0.60f + sTone) * sEnv * 0.16f;
                }

                // (c) Moog風ブリブリ・アナログシンセベース（ルート重低音 ＋ オクターブ上レイヤーの分厚い16分ドライブ）
                float bRoot = bassRoots[bar];
                float bOct = bRoot * 2.0f; // オクターブ上の音
                float stepT = (barT / 0.1875f) % 1.0f;
                int stepIndex = Mathf.FloorToInt(barT / 0.1875f) % 16;
                // 拍頭(0,4,8,12)に重み、8分裏(2,6,10,14)にアクセント、間(1,3,5...)は軽快パルス
                float bVel = (stepIndex % 4 == 0) ? 1.10f : ((stepIndex % 2 == 0) ? 0.90f : 0.75f);
                float bEnv = Mathf.Exp(-stepT * 18.0f) * bVel;

                // フィルターエンベロープ（アタック時に一瞬「ブリッ！」と開き急速に閉じる）
                float filterEnv = Mathf.Exp(-stepT * 28.0f);

                // 1. ルート重低音オシレーター（腹に響くサブロー ＋ 太いノコギリ倍音）
                float sub1 = Mathf.Sin(2f * Mathf.PI * bRoot * t) * 0.75f;
                float saw1 = Mathf.Sin(2f * Mathf.PI * bRoot * t) * 0.45f
                           - Mathf.Sin(4f * Mathf.PI * bRoot * t) * 0.25f
                           + Mathf.Sin(6f * Mathf.PI * bRoot * t) * 0.15f
                           - Mathf.Sin(8f * Mathf.PI * bRoot * t) * 0.08f;

                // 2. オクターブ上オシレーター（音程の輪郭・抜けと分厚いユニゾン感）
                float sub2 = Mathf.Sin(2f * Mathf.PI * bOct * t) * 0.35f;
                float saw2 = (Mathf.Sin(2f * Mathf.PI * bOct * t) * 0.28f
                           - Mathf.Sin(4f * Mathf.PI * bOct * t) * 0.14f);

                // フィルター変調（アタックの瞬間に倍音が強く鳴り、独特の「ブリブリ」した粘りを生む）
                float rawBass = (sub1 + sub2) + (saw1 + saw2) * (0.35f + filterEnv * 0.85f);
                // アナログサチュレーション（温かい歪みで図太さを極大化）
                float fatBass = (float)System.Math.Tanh(rawBass * 1.50f);
                synthBass = fatBass * bEnv * 0.26f;
            }

            float loopFade = 1f;
            if (t < 0.2f) loopFade = t / 0.2f;
            else if (t > duration - 0.2f) loopFade = (duration - t) / 0.2f;

            float outputGain = EndingThemeVolume / Mathf.Max(0.01f, SkybreakSourceVolume);
            float mixed = (brass + arpWave + kick + snare + synthBass) * loopFade * outputGain;
            float mono = Mathf.Clamp(mixed, -0.92f, 0.92f);

            // 前半は広大なステレオ感、後半はベースがセンターに定位するようステレオを配置
            if (!withRhythmAndBass)
            {
                samples[i * 2] = mono * 0.95f;
                samples[i * 2 + 1] = mono * 1.05f;
            }
            else
            {
                samples[i * 2] = mono * 0.99f;
                samples[i * 2 + 1] = mono * 1.01f;
            }
        }

        string clipName = withRhythmAndBass ? "Music_RustFloat_Skybreak_Overdrive" : "Music_RustFloat_Skybreak_Intro";
        var clip = AudioClip.Create(clipName, totalSamples, 2, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
