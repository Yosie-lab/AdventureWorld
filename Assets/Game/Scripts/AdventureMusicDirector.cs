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
    AudioClip _skybreakBuildUpClip;    // 96秒ビルドアップ曲（0〜12s:神聖ブラス, 12〜24s:ドラム合流, 24〜84s:3〜7周目トランペット主旋律, 84〜96s:8周目以降メロディーミュート、ベース音なし）
    AudioClip _skybreakDrumsOnlyClip;  // 12秒（Rust回復後：軽快なドラム＋爽快BGMで大空へダイブ、メロディーなし）

    bool _hasSwitchedToSkybreak = false;
    bool _isOverdriveDropActive = false;
    /// <summary>エンディング進行中のみ天空BGMを維持（クリア後の自由探索では解除）</summary>
    bool _keepEndingThemeActive = false;
    /// <summary>エンディング後に探索曲へ戻したら、天蓋開放済みでも天空曲へ再切替しない</summary>
    bool _preferAmbientAfterEnding = false;
    bool _isFadingA = false;
    const float EndingThemeVolume = 1.35f;
    // AudioSource.volume は 1 で頭打ちになる。1 を超える分は生成波形で足す。
    float SkybreakSourceVolume => Mathf.Min(1f, EndingThemeVolume);
    const float AmbientThemeVolume = 0.14f;

    /// <summary>外部（古代ピアノ等の環境スポット）からのダッキング要求度 (0.0 = 通常音量, 1.0 = 最大ダッキング)</summary>
    public float spotDuckingFactor = 0f;

    /// <summary>マスターBGMボリュームスケール (0.0〜1.0)</summary>
    public static float MasterBgmVolumeScale
    {
        get => PlayerPrefs.GetFloat("Adventure_BgmVolume", 1.0f);
        set
        {
            float clamped = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat("Adventure_BgmVolume", clamped);
            PlayerPrefs.Save();
            Instance?.ApplyMasterVolumeScale();
        }
    }

    public void ApplyMasterVolumeScale()
    {
        float scale = MasterBgmVolumeScale;
        if (_bgmSourceA != null)
            _bgmSourceA.volume = AmbientThemeVolume * scale * (1f - spotDuckingFactor * 0.85f);
        if (_bgmSourceB != null)
            _bgmSourceB.volume = SkybreakSourceVolume * scale;
    }

    /// <summary>スポットダッキング係数の設定 (0.0〜1.0)</summary>
    public void SetSpotDucking(float factor)
    {
        spotDuckingFactor = Mathf.Clamp01(factor);
        ApplyMasterVolumeScale();
    }

    public static void Ensure()
    {
        if (Instance != null) return;
        var existing = Object.FindAnyObjectByType<AdventureMusicDirector>();
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
        // 天空BGMの段階的ビルドアップ（96秒の1本化クリップ：0〜12sブラス、12〜24sドラム、24〜84sトランペット、84〜96sメロディーミュート、ベースなし）
        if (_keepEndingThemeActive && _bgmSourceB != null && _hasSwitchedToSkybreak)
        {
            if (!_isOverdriveDropActive)
            {
                // 8周目（96秒）の終わりに達したら、84秒（8周目の頭：トランペットをカットしたフルインスト編成）へシームレスに戻してループ！
                if (_bgmSourceB.time >= 95.92f || (!_bgmSourceB.isPlaying && _bgmSourceB.time >= 95.5f))
                {
                    _bgmSourceB.time = 84.0f;
                    if (!_bgmSourceB.isPlaying)
                        _bgmSourceB.Play();
                }
            }
            else
            {
                // オーバードライブ中（12秒ループ）
                if (!_bgmSourceB.isPlaying && _skybreakDrumsOnlyClip != null)
                {
                    _bgmSourceB.clip = _skybreakDrumsOnlyClip;
                    _bgmSourceB.loop = true;
                    _bgmSourceB.time = 0f;
                    _bgmSourceB.Play();
                }
            }
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

    /// <summary>エンディング進行中：天空BGMを維持（再生中なら絶対に巻き戻さない）</summary>
    public void KeepEndingThemeActive()
    {
        _preferAmbientAfterEnding = false;
        _keepEndingThemeActive = true;
        _hasSwitchedToSkybreak = true;

        // すでに天空BGMが正常に再生中の場合は、一切巻き戻さずそのまま継続
        if (_bgmSourceB != null && _bgmSourceB.isPlaying)
        {
            return;
        }

        // 停止していた場合のみ開始
        PlaySkybreakTheme(force: true);
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
        // 既存の再生中状態を守り、停止時のみ再起動
        if (_bgmSourceB != null && _bgmSourceB.isPlaying && !restartIfNeeded)
            return;

        PlaySkybreakTheme(force: true);
    }

    /// <summary>F9再演／ニューゲームで天蓋前に戻すとき探索曲へ戻す</summary>
    public void ResetSkybreakMusicState()
    {
        _keepEndingThemeActive = false;
        _preferAmbientAfterEnding = false;
        _hasSwitchedToSkybreak = false;
        _isOverdriveDropActive = false;
        _skybreakThemeClip = _skybreakBuildUpClip;
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

    /// <summary>天空突破BGMへ切替（36秒ビルドアップ曲を開始）。force=true で再演時も必ず再生</summary>
    public void PlaySkybreakTheme(bool force = false)
    {
        if (!force && _hasSwitchedToSkybreak && _bgmSourceB != null && _bgmSourceB.isPlaying) return;
        if (force || _skybreakBuildUpClip == null)
            _skybreakBuildUpClip = GenerateSkybreakTheme(isDrumsOnly: false);
        if (force || _skybreakDrumsOnlyClip == null)
            _skybreakDrumsOnlyClip = GenerateSkybreakTheme(isDrumsOnly: true);

        _isOverdriveDropActive = false;
        _skybreakThemeClip = _skybreakBuildUpClip;

        _preferAmbientAfterEnding = false;
        _hasSwitchedToSkybreak = true;
        _keepEndingThemeActive = true;
        TriggerSkybreakMusic();
    }

    /// <summary>
    /// オーバードライブ突入時（Rust回復・「全力で行こう！！」）：
    /// 今までと同じタイミングで、ベースが抜けてドラムとBGMの疾走滑空モードへシームレスに切り替え！
    /// </summary>
    public void TriggerSkybreakOverdriveDrop()
    {
        _isOverdriveDropActive = true;

        if (_skybreakDrumsOnlyClip == null)
            _skybreakDrumsOnlyClip = GenerateSkybreakTheme(isDrumsOnly: true);

        _skybreakThemeClip = _skybreakDrumsOnlyClip;

        if (_bgmSourceB != null)
        {
            float currentTime = _bgmSourceB.time % 12.0f;
            _bgmSourceB.clip = _skybreakDrumsOnlyClip;
            _bgmSourceB.time = currentTime;
            _bgmSourceB.loop = true;
            if (!_bgmSourceB.isPlaying)
                _bgmSourceB.Play();
            _bgmSourceB.volume = SkybreakSourceVolume;
            Debug.Log($"[RustAndFloat] ✦ Rust回復オーバードライブ：ベースが抜けてドラム＋BGMで大空へ！ (time: {currentTime:F2}s)");
        }
    }

    void TriggerSkybreakMusic()
    {
        if (_skybreakThemeClip == null) return;

        StopAllCoroutines();
        if (_bgmSourceA != null && _bgmSourceA.isPlaying)
            StartCoroutine(FadeVolume(_bgmSourceA, 0f, 1.2f));

        _bgmSourceB.clip = _skybreakThemeClip;
        _bgmSourceB.loop = false; // 96s後はUpdateで84s（8周目：トランペットカット済みフルインスト）へシームレスループ
        _bgmSourceB.time = 0f;
        _bgmSourceB.volume = SkybreakSourceVolume; // 即時1.0fで確実に鳴らす！
        _bgmSourceB.Play();
        Debug.Log("[RustAndFloat] ✦ 天空突破BGM開始：1周目ブラス ➔ 12sドラム ➔ 24〜84sトランペット ➔ 84s以降メロディーミュート（ベースなし）！");
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
        _skybreakBuildUpClip = GenerateSkybreakTheme(isDrumsOnly: false);
        _skybreakDrumsOnlyClip = GenerateSkybreakTheme(isDrumsOnly: true);
        _skybreakThemeClip = _skybreakBuildUpClip;
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
                pad += s * (0.030f / chord.Length);
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
                    mel += wave * env * 0.075f;
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
    /// isDrumsOnly = false：96秒の段階的ビルドアップ曲（8周構成）。
    ///   - 0〜12秒  ：1周目（神聖ブラスパッド＋アルペジオ＋温かいパッド和音）
    ///   - 12〜24秒：2周目（12秒の頭からドラムが合流！）
    ///   - 24〜84秒：3〜7周目（24秒の頭から哀愁のトランペット主旋律が合流し感動的にリフレイン！）
    ///   - 84〜96秒：8周目（84秒からメロディーはミュートされ、ドラム＋ブラスパッドのインスト編成でループ継続）
    /// isDrumsOnly = true ：Rust回復後（「全力で行こう！！」）。ベースとトランペットが抜け、軽快なドラムと爽快BGMで大空へ！
    /// </summary>
    public AudioClip GenerateSkybreakTheme(bool isDrumsOnly)
    {
        int sampleRate = 44100;
        float duration = isDrumsOnly ? 12.0f : 96.0f;
        int totalSamples = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[totalSamples * 2];

        // 壮大な8小節進行 (1小節 = 1.5秒、計12秒ループ)
        // 1〜2小節: D (3.0s) - 美しく堂々と始まる
        // 3〜4小節: F#m (3.0s) - 最高嶺の感動ロングトーン
        // 5〜6小節: Em (3.0s) - 希望を追い求めて跳躍
        // 7〜8小節: Gm (3.0s) - 2小節続く切なく劇的なサブドミナントマイナー！四分音符で1小節目へ！
        float[][] chordRoots8 = new float[][]
        {
            new float[] { 146.83f, 220.00f, 293.66f, 369.99f, 440.00f }, // 1: D
            new float[] { 146.83f, 220.00f, 293.66f, 369.99f, 440.00f }, // 2: D
            new float[] { 185.00f, 220.00f, 277.18f, 369.99f, 554.37f }, // 3: F#m
            new float[] { 185.00f, 220.00f, 277.18f, 369.99f, 554.37f }, // 4: F#m
            new float[] { 164.81f, 196.00f, 246.94f, 329.63f, 493.88f }, // 5: Em
            new float[] { 164.81f, 196.00f, 246.94f, 329.63f, 493.88f }, // 6: Em
            new float[] { 196.00f, 233.08f, 293.66f, 392.00f, 466.16f }, // 7: Gm
            new float[] { 196.00f, 233.08f, 293.66f, 392.00f, 466.16f }  // 8: Gm (Aへの移行なし、Gmを維持)
        };



        // (a) 星屑のように澄み渡るセレステ（Celesta / 鐘・鉄琴調）主旋律（全32拍・8小節・計12.0秒）
        // F#5拍 ➔ F#1拍 ➔ F1拍 ➔ F#1拍 ➔ A7拍 ➔ A1拍 ➔ G5拍 ➔ B1拍 ➔ E1拍 ➔ G1拍 ➔ A#4拍 ➔ A2拍 ➔ G2拍
        const float beatSec = 0.375f; // 1拍 = 0.375秒 (BPM 160)
        (float start, float end, float freq)[] celestaNotes = new (float, float, float)[]
        {
            // 第1〜2小節 (Dコード / 0.00〜3.00s): F# 5拍 + F# 1拍 + F 1拍 + F# 1拍
            (0.000f * beatSec,  5.000f * beatSec, 369.99f), // F#4 (5拍 = 1.875s) 澄んだ鐘のロングトーン
            (5.000f * beatSec,  6.000f * beatSec, 369.99f), // F#4 (1拍 = 0.375s) 軽やかに再打鍵
            (6.000f * beatSec,  7.000f * beatSec, 349.23f), // F4  (1拍 = 0.375s) 切ない経過音
            (7.000f * beatSec,  8.000f * beatSec, 369.99f), // F#4 (1拍 = 0.375s)

            // 第3〜4小節 (F#mコード / 3.00〜6.00s): A 7拍 + A 1拍
            (8.000f * beatSec, 15.000f * beatSec, 440.00f), // A4  (7拍 = 2.625s) 心を打つ切ない長音
            (15.000f * beatSec, 16.000f * beatSec, 440.00f), // A4  (1拍 = 0.375s)

            // 第5〜6小節 (Emコード / 6.00〜9.00s): G 5拍 + B 1拍 + E 1拍 + G 1拍
            (16.000f * beatSec, 21.000f * beatSec, 392.00f), // G4  (5拍 = 1.875s) 温かな叙情
            (21.000f * beatSec, 22.000f * beatSec, 246.94f), // B3  (1拍 = 0.375s) 落ち着いたトーンへ
            (22.000f * beatSec, 23.000f * beatSec, 329.63f), // E4  (1拍 = 0.375s)
            (23.000f * beatSec, 24.000f * beatSec, 392.00f), // G4  (1拍 = 0.375s)

            // 第7〜8小節 (Gmコード / 9.00〜12.00s): A# 4拍 + A 2拍 + G 2拍
            (24.000f * beatSec, 28.000f * beatSec, 466.16f), // A#4 (4拍 = 1.500s) 劇的な短3度
            (28.000f * beatSec, 30.000f * beatSec, 440.00f), // A4  (2拍 = 0.750s)
            (30.000f * beatSec, 32.000f * beatSec, 392.00f)  // G4  (2拍 = 0.750s) 終止から冒頭F#へ美しく循環
        };

        // (b) メロディーを澄んだ響きで包み込む8小節白玉ハーモニー（揺れのないピュア和音）
        float[][] padChords8 = new float[][]
        {
            new float[] { 146.83f, 220.00f, 369.99f }, // 1: D
            new float[] { 146.83f, 220.00f, 369.99f }, // 2: D
            new float[] { 138.59f, 220.00f, 369.99f }, // 3: F#m
            new float[] { 138.59f, 220.00f, 369.99f }, // 4: F#m
            new float[] { 164.81f, 246.94f, 392.00f }, // 5: Em
            new float[] { 164.81f, 246.94f, 392.00f }, // 6: Em
            new float[] { 196.00f, 233.08f, 392.00f }, // 7: Gm
            new float[] { 196.00f, 233.08f, 392.00f }  // 8: Gm
        };

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            int cycle = Mathf.Clamp(Mathf.FloorToInt(t / 12.0f), 0, 7);
            float cycleT = t % 12.0f;
            int bar8 = Mathf.Clamp(Mathf.FloorToInt(cycleT / 1.5f), 0, 7);
            float bar8T = cycleT % 1.5f;
            float[] chord = chordRoots8[bar8];

            bool hasDrums;
            bool hasCelesta;
            if (isDrumsOnly)
            {
                hasDrums = true;
                hasCelesta = false; // 大空滑空時・オーバードライブ後もメロディーは無し
            }
            else
            {
                hasDrums = (cycle >= 1);                        // 12秒〜（2周目以降）でドラム合流！
                hasCelesta = (cycle >= 2 && cycle < 7);         // 24〜84秒（3〜7周目）でセレステ主旋律！8周目（84秒〜）からはミュート！
            }

            // 1. パワフルで神秘的なシネマティック・ブラスパッド
            float brass = 0f;
            float env = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(bar8T / 0.3f)) * Mathf.SmoothStep(1f, 0.6f, Mathf.Clamp01(bar8T / 1.5f));
            for (int k = 0; k < chord.Length; k++)
            {
                float freq = chord[k];
                float s = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.45f
                        + Mathf.Sin(4f * Mathf.PI * freq * t) * 0.25f
                        + Mathf.Sin(6f * Mathf.PI * freq * t) * 0.15f;
                float gain = (!hasDrums) ? 0.17f : 0.11f;
                brass += s * (gain / chord.Length);
            }
            brass *= env;

            // 2. 駆け上がるキラキラしたアルペジオ（音量を10%削り、セレステの主旋律を美しく引き立てる）
            float arpPhase = (t * 8.0f) % 1.0f;
            int arpIndex = Mathf.FloorToInt(t * 8.0f) % chord.Length;
            float arpFreq = chord[arpIndex] * 2.0f;
            float arpEnv = Mathf.Exp(-arpPhase * 5.0f);
            float arpWave = Mathf.Sin(2f * Mathf.PI * arpFreq * t) * arpEnv * 0.171f;

            // 3. 星屑のように澄み渡るセレステ（Celesta / 鐘・鉄琴）主旋律 ＋ 専用ステレオシマーリバーブ ＋ 温かいコードパッド
            float strL = 0f;
            float strR = 0f;
            {
                float celestaL = 0f;
                float celestaR = 0f;

                if (hasCelesta)
                {
                    for (int m = 0; m < celestaNotes.Length; m++)
                    {
                        var note = celestaNotes[m];
                        float noteDur = note.end - note.start;

                        for (int d = 0; d < 3; d++)
                        {
                            float nT = (d == 0) ? (cycleT - note.start) : ((d == 1) ? (cycleT + 12.0f - note.start) : (cycleT - 12.0f - note.start));
                            if (nT >= 0f && nT <= noteDur + 0.6f) // 余韻・残響のため少し長めに再生判定
                            {
                                float f = note.freq * 2.0f; // セレステの澄んだ音域（1オクターブ上）

                                // 瞬時のクリスタル打鍵アタック（0.002s）
                                float cAttack = Mathf.Clamp01(nT / 0.002f);
                                float cDecayFund = Mathf.Exp(-nT * 1.5f);     // 基音の澄んだ余韻
                                float cDecayHigh = Mathf.Exp(-nT * 3.6f);     // 高音キラキラ成分の余韻
                                float cDecaySparkle = Mathf.Exp(-nT * 5.0f);  // 超高音星屑のきらめき

                                // キラキラ感を大幅強化したクリスタル・セレステ波形
                                // 澄んだベル基音 ＋ 1オクターブ上 ＋ 2オクターブ上星屑 ＋ チャイム倍音
                                float cWave = (Mathf.Sin(2f * Mathf.PI * (f * 0.5f) * t) * 0.05f * cDecayFund)
                                            + (Mathf.Sin(2f * Mathf.PI * f * t) * 0.46f * cDecayFund)
                                            + (Mathf.Sin(2f * Mathf.PI * (f * 2.0f) * t) * 0.45f * cDecayHigh)
                                            + (Mathf.Sin(2f * Mathf.PI * (f * 4.0f) * t) * 0.28f * cDecaySparkle)
                                            + (Mathf.Sin(2f * Mathf.PI * (f * 3.01f) * t) * 0.20f * cDecayHigh)
                                            + (Mathf.Sin(2f * Mathf.PI * (f * 5.02f) * t) * 0.14f * cDecaySparkle);

                                if (nT <= noteDur)
                                {
                                    float directSig = cWave * cAttack;
                                    celestaL += directSig * 0.95f;
                                    celestaR += directSig * 1.05f;
                                }

                                // --- セレステ専用の空間ステレオリバーブ／シマー反射音 ---
                                // 左右にふわっと広がりながら星空へ消え入る4段ピンポン空間残響
                                float[] delayTimes = { 0.094f, 0.188f, 0.282f, 0.375f };
                                float[] delayGains = { 0.45f, 0.32f, 0.22f, 0.14f };
                                float[] panL = { 0.25f, 0.85f, 0.30f, 0.78f };
                                float[] panR = { 0.85f, 0.25f, 0.78f, 0.30f };

                                for (int rv = 0; rv < 4; rv++)
                                {
                                    float rT = nT - delayTimes[rv];
                                    if (rT >= 0f)
                                    {
                                        float rEnv = Mathf.Clamp01(rT / 0.004f) * Mathf.Exp(-rT * 2.2f) * delayGains[rv];
                                        // 反射音は高域倍音ときらめきを強調したシマーリバーブ
                                        float rWave = (Mathf.Sin(2f * Mathf.PI * f * t) * 0.38f)
                                                    + (Mathf.Sin(2f * Mathf.PI * (f * 2.0f) * t) * 0.52f)
                                                    + (Mathf.Sin(2f * Mathf.PI * (f * 4.0f) * t) * 0.26f);
                                        float rSig = rWave * rEnv;
                                        celestaL += rSig * panL[rv];
                                        celestaR += rSig * panR[rv];
                                    }
                                }
                            }
                        }
                    }
                }

                // (b) 小節境界でふんわりと移り変わる揺れのない8小節白玉コードパッド
                float pad = 0f;
                float padFade = 0.35f;

                for (int b = 0; b < 8; b++)
                {
                    float bStart = b * 1.5f;
                    for (int pd = 0; pd < 3; pd++)
                    {
                        float pT = (pd == 0) ? (cycleT - bStart) : ((pd == 1) ? (cycleT + 12.0f - bStart) : (cycleT - 12.0f - bStart));
                        if (pT >= -padFade && pT <= 1.5f + padFade)
                        {
                            float pEnv = 1f;
                            if (pT < padFade)
                                pEnv = Mathf.SmoothStep(0f, 1f, (pT + padFade) / (padFade * 2f));
                            else if (pT > 1.5f - padFade)
                                pEnv = Mathf.SmoothStep(1f, 0f, (pT - (1.5f - padFade)) / (padFade * 2f));

                            if (pEnv > 0.001f)
                            {
                                float[] pNotes = padChords8[b];
                                for (int pn = 0; pn < pNotes.Length; pn++)
                                {
                                    float pf = pNotes[pn];
                                    float ps = Mathf.Sin(2f * Mathf.PI * pf * t) * 0.38f
                                             + Mathf.Sin(4f * Mathf.PI * pf * t) * 0.16f;

                                    pad += ps * pEnv * (1.0f / pNotes.Length);
                                }
                            }
                        }
                    }
                }

                // セレステの音量バランス（30%削減して耳に優しく、上品に大空を彩る音量）
                float celestaGain = ((cycle == 0) ? 0.38f : 0.46f) * 0.336f;
                if (isDrumsOnly) celestaGain = 0.48f * 0.336f;

                // 左右に美しく広がるステレオ定位（セレステの透明感が大空全体を包み込む）
                float combinedL = celestaL * celestaGain + pad * 0.18f * 0.98f;
                float combinedR = celestaR * celestaGain + pad * 0.18f * 1.02f;
                strL = combinedL;
                strR = combinedR;
            }

            float kick = 0f;
            float snare = 0f;
            float hat = 0f;

            // ドラム隊
            if (hasDrums)
            {
                uint seed = (uint)(i * 1973 + 9277);
                seed = (seed ^ 61) ^ (seed >> 16);
                seed *= 9;
                seed = seed ^ (seed >> 4);
                seed *= 0x27d4eb2d;
                seed = seed ^ (seed >> 15);
                float noise = ((seed & 0xFFFF) / 32768.0f) - 1.0f;

                float beatT = bar8T % 0.75f;
                if (beatT < 0.15f)
                {
                    float kPitch = Mathf.Lerp(128f, 44f, Mathf.Clamp01(beatT / 0.08f));
                    float kEnv = Mathf.Exp(-beatT * 25f);
                    kick = Mathf.Sin(2f * Mathf.PI * kPitch * beatT) * kEnv * 0.32f;
                }

                float snareT = -1f;
                if (bar8T >= 0.375f && bar8T < 0.65f) snareT = bar8T - 0.375f;
                else if (bar8T >= 1.125f && bar8T < 1.40f) snareT = bar8T - 1.125f;
                // 第8小節の末尾（11.8125s）：1小節目の頭へ小気味よく雪崩れ込むスネアフィル！
                else if (bar8 == 7 && bar8T >= 1.3125f && bar8T < 1.48f) snareT = bar8T - 1.3125f;

                if (snareT >= 0f)
                {
                    float sEnv = Mathf.Exp(-snareT * 24f);
                    float sTone = Mathf.Sin(2f * Mathf.PI * 190f * snareT) * 0.35f;
                    snare = (noise * 0.65f + sTone) * sEnv * 0.24f;
                }

                float hatT = bar8T % 0.1875f;
                if (hatT < 0.06f)
                {
                    float hEnv = Mathf.Exp(-hatT * 55f);
                    hat = noise * hEnv * 0.09f;
                }
            }

            float loopFade = 1f;
            if (t < 0.2f) loopFade = t / 0.2f;
            else if (t > duration - 0.2f) loopFade = (duration - t) / 0.2f;

            float outputGain = EndingThemeVolume / Mathf.Max(0.01f, SkybreakSourceVolume);
            float mixedL = (brass + arpWave + kick + snare + hat + strL) * loopFade * outputGain;
            float mixedR = (brass + arpWave + kick + snare + hat + strR) * loopFade * outputGain;
            float outL = (float)System.Math.Tanh(mixedL * 0.98f) * 0.92f;
            float outR = (float)System.Math.Tanh(mixedR * 0.98f) * 0.92f;

            samples[i * 2] = outL;
            samples[i * 2 + 1] = outR;
        }

        // 全体に広大で神秘的な深みのあるステレオリバーブを付与（セレステの響きと大空の残響を最大化）
        ApplyLightReverb(samples, totalSamples, sampleRate, 0.44f);

        string clipName = isDrumsOnly ? "Music_RustFloat_Skybreak_DrumsOnly" : "Music_RustFloat_Skybreak_BuildUp";
        var clip = AudioClip.Create(clipName, totalSamples, 2, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// 全体に広大で豊かな深みのあるステレオリバーブを付与するSchroederリバーブ
    /// 左右独立長尺コムフィルタ＋直列オールパス拡散＋2パス循環（ループ端シームレス化）
    /// </summary>
    private static void ApplyLightReverb(float[] samples, int totalSamples, int sampleRate, float wetMix = 0.44f)
    {
        if (samples == null || totalSamples <= 0) return;

        // コムフィルタのディレイ長（大空間・大聖堂・大空スケールのディレイ時間）
        int[] dL = new int[]
        {
            Mathf.RoundToInt(0.0437f * sampleRate),
            Mathf.RoundToInt(0.0541f * sampleRate),
            Mathf.RoundToInt(0.0613f * sampleRate),
            Mathf.RoundToInt(0.0671f * sampleRate)
        };
        int[] dR = new int[]
        {
            Mathf.RoundToInt(0.0459f * sampleRate),
            Mathf.RoundToInt(0.0527f * sampleRate),
            Mathf.RoundToInt(0.0631f * sampleRate),
            Mathf.RoundToInt(0.0699f * sampleRate)
        };

        float[][] combL = new float[4][];
        float[][] combR = new float[4][];
        for (int k = 0; k < 4; k++)
        {
            combL[k] = new float[dL[k]];
            combR[k] = new float[dR[k]];
        }
        int[] idxL = new int[4];
        int[] idxR = new int[4];
        float[] filterL = new float[4];
        float[] filterR = new float[4];

        int apLenL1 = Mathf.RoundToInt(0.0071f * sampleRate);
        int apLenL2 = Mathf.RoundToInt(0.0024f * sampleRate);
        int apLenR1 = Mathf.RoundToInt(0.0079f * sampleRate);
        int apLenR2 = Mathf.RoundToInt(0.0027f * sampleRate);

        float[] apBufL1 = new float[apLenL1];
        float[] apBufL2 = new float[apLenL2];
        float[] apBufR1 = new float[apLenR1];
        float[] apBufR2 = new float[apLenR2];
        int apIdxL1 = 0, apIdxL2 = 0, apIdxR1 = 0, apIdxR2 = 0;

        const float feedback = 0.85f;
        const float damp = 0.18f;
        const float apGain = 0.5f;

        float dryMix = 1.0f - wetMix * 0.30f;

        // 2パス実行: 1パス目はディレイバッファのウォームアップ、2パス目で出力適用（ループ境界のシームレス化）
        for (int pass = 0; pass < 2; pass++)
        {
            bool write = (pass == 1);
            for (int i = 0; i < totalSamples; i++)
            {
                float inL = samples[i * 2];
                float inR = samples[i * 2 + 1];

                // 4並列コムフィルタ (Left)
                float outCombL = 0f;
                for (int k = 0; k < 4; k++)
                {
                    int idx = idxL[k];
                    float delayed = combL[k][idx];
                    filterL[k] = delayed * (1f - damp) + filterL[k] * damp;
                    combL[k][idx] = inL + filterL[k] * feedback;
                    outCombL += delayed;
                    idxL[k] = (idx + 1) % dL[k];
                }
                outCombL *= 0.25f;

                // 4並列コムフィルタ (Right)
                float outCombR = 0f;
                for (int k = 0; k < 4; k++)
                {
                    int idx = idxR[k];
                    float delayed = combR[k][idx];
                    filterR[k] = delayed * (1f - damp) + filterR[k] * damp;
                    combR[k][idx] = inR + filterR[k] * feedback;
                    outCombR += delayed;
                    idxR[k] = (idx + 1) % dR[k];
                }
                outCombR *= 0.25f;

                // 2直列オールパスフィルタ (Left)
                float apL1Out = -apGain * outCombL + apBufL1[apIdxL1];
                apBufL1[apIdxL1] = outCombL + apGain * apL1Out;
                apIdxL1 = (apIdxL1 + 1) % apLenL1;

                float apL2Out = -apGain * apL1Out + apBufL2[apIdxL2];
                apBufL2[apIdxL2] = apL1Out + apGain * apL2Out;
                apIdxL2 = (apIdxL2 + 1) % apLenL2;

                // 2直列オールパスフィルタ (Right)
                float apR1Out = -apGain * outCombR + apBufR1[apIdxR1];
                apBufR1[apIdxR1] = outCombR + apGain * apR1Out;
                apIdxR1 = (apIdxR1 + 1) % apLenR1;

                float apR2Out = -apGain * apR1Out + apBufR2[apIdxR2];
                apBufR2[apIdxR2] = apR1Out + apGain * apR2Out;
                apIdxR2 = (apIdxR2 + 1) % apLenR2;

                if (write)
                {
                    float finalL = inL * dryMix + apL2Out * wetMix;
                    float finalR = inR * dryMix + apR2Out * wetMix;
                    samples[i * 2] = (float)System.Math.Tanh(finalL);
                    samples[i * 2 + 1] = (float)System.Math.Tanh(finalR);
                }
            }
        }
    }
}
