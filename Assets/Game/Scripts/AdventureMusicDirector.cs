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
    AudioClip _skybreakBuildUpClip;    // 36秒ビルドアップ曲（0〜12s:神聖ブラス, 12〜24s:ブリブリベース合流, 24〜36s:ドラム加わりフル編成）
    AudioClip _skybreakDrumsOnlyClip;  // 12秒（Rust回復後：ベースが抜けてドラム＋BGMで大空へダイブ）

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
    const float AmbientThemeVolume = 0.20f;

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
        // 天空BGMの段階的ビルドアップ（36秒の1本化クリップ：0〜12s神聖ブラス、12〜24sベース合流、24〜36sドラム合流フル編成）
        if (_keepEndingThemeActive && _bgmSourceB != null && _hasSwitchedToSkybreak)
        {
            if (!_isOverdriveDropActive)
            {
                // 3周目（36秒）の終わりに達したら、24秒（フル編成の頭）へシームレスに戻してフル編成をループ！
                if (_bgmSourceB.time >= 35.92f || (!_bgmSourceB.isPlaying && _bgmSourceB.time >= 35.5f))
                {
                    _bgmSourceB.time = 24.0f;
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
        _bgmSourceB.loop = false; // 36s後はUpdateで24s（フル編成）へシームレスループ
        _bgmSourceB.time = 0f;
        _bgmSourceB.volume = SkybreakSourceVolume; // 即時1.0fで確実に鳴らす！
        _bgmSourceB.Play();
        Debug.Log("[RustAndFloat] ✦ 天空突破BGM開始：1周目（神聖ブラス＋アルペジオのみ）➔ 12sベース ➔ 24sフル編成！");
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
    /// isDrumsOnly = false：36秒の段階的ビルドアップ曲。
    ///   - 0〜12秒  ：1周目（神聖ブラスパッド＋アルペジオ＋温かいパッド和音）
    ///   - 12〜24秒：2周目（12秒の頭からブリブリベースが鳴り響き合流！）
    ///   - 24〜36秒：3周目（24秒の頭からドラム＋感動のバッハ風バイオリン主旋律が合流し完全フル編成へ！）
    /// isDrumsOnly = true ：Rust回復後（「全力で行こう！！」）。ベースとバイオリンが抜け、軽快なドラムとBGMで大空へ！
    /// </summary>
    public AudioClip GenerateSkybreakTheme(bool isDrumsOnly)
    {
        int sampleRate = 44100;
        float duration = isDrumsOnly ? 12.0f : 36.0f;
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

        // 各コードのルートベース音 (D2, D2, F#2, F#2, E2, E2, G2, G2)
        float[] bassRoots8 = new float[] { 73.42f, 73.42f, 92.50f, 92.50f, 82.41f, 82.41f, 98.00f, 98.00f };

        // (a) バッハ風の気品ある8小節バイオリン主旋律
        // 1小節目はD6 ➔ C#6 ➔ D6で美しく装飾し、8小節Gm駆け上がりから優雅に循環！
        (float start, float end, float freq)[] melodyNotes = new (float, float, float)[]
        {
            // 第1小節 (0.00〜1.50s): D - 1・2拍目D6、3拍目C#6、4拍目D6
            (0.000f, 0.750f, 1174.66f), // 1・2拍目: D6  (2拍 0.75秒、主音)
            (0.750f, 1.125f, 1108.73f), // 3拍目:   C#6 (四分音符 0.375秒、メジャー7thの切ないステップ)
            (1.125f, 1.500f, 1174.66f), // 4拍目:   D6  (四分音符 0.375秒、主音への回帰)

            // 第2小節 (1.50〜3.00s): D - 1小節目のモチーフをリフレイン（1・2拍目D6、3拍目C#6、4拍目D6）
            (1.500f, 2.250f, 1174.66f), // 1・2拍目: D6  (2拍 0.75秒、主音)
            (2.250f, 2.625f, 1108.73f), // 3拍目:   C#6 (四分音符 0.375秒、メジャー7th)
            (2.625f, 3.000f, 1174.66f), // 4拍目:   D6  (四分音符 0.375秒、主音への回帰)

            // 第3小節 (3.00〜4.50s): F#m - 胸を打つ最高嶺D6へ到達！
            (3.00f,  4.50f, 1174.66f), // D6:  最高峰の白玉ロングトーン (1.5秒)

            // 第4小節 (4.50〜6.00s): F#m - 優雅に舞い降りるステップ
            (4.50f,  5.25f, 1108.73f), // C#6: 4分音符 (0.75秒)
            (5.25f,  6.00f,  987.77f), // B5:  4分音符 (0.75秒)

            // 第5小節 (6.00〜7.50s): Em - 静けさと温もりの白玉
            (6.00f,  7.50f,  783.99f), // G5:  白玉ロングトーン (1.5秒)

            // 第6小節 (7.50〜9.00s): Em - 希望を追い求めて再び跳躍上行
            (7.50f,  8.25f,  880.00f), // A5:  4分音符 (0.75秒)
            (8.25f,  9.00f,  987.77f), // B5:  4分音符 (0.75秒)

            // 第7小節 (9.00〜10.50s): Gm - 切なく劇的なサブドミナントマイナー
            (9.00f,  9.75f,  932.33f), // Bb5: 2拍の四分音符 (0.75秒)
            (9.75f, 10.50f, 1046.50f), // C6:  2拍の四分音符 (0.75秒)

            // 第8小節 (10.50〜12.00s): Gm - 四分音符4連で小気味よく駆け上がり、1小節目頭のC#6へ美しく繋ぐ！
            (10.500f, 10.875f,  932.33f), // 拍1: Bb5 (四分音符 0.375秒、短3度)
            (10.875f, 11.250f, 1174.66f), // 拍2: D6  (四分音符 0.375秒、完全5度)
            (11.250f, 11.625f, 1318.51f), // 拍3: E6  (四分音符 0.375秒、13th)
            (11.625f, 12.000f, 1567.98f)  // 拍4: G6  (四分音符 0.375秒、オクターブ上ルート) ➔ 0.0s頭のC#6へ美しく着地！
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
            int cycle = Mathf.Clamp(Mathf.FloorToInt(t / 12.0f), 0, 2);
            float cycleT = t % 12.0f;
            int bar8 = Mathf.Clamp(Mathf.FloorToInt(cycleT / 1.5f), 0, 7);
            float bar8T = cycleT % 1.5f;
            float[] chord = chordRoots8[bar8];

            bool hasBass;
            bool hasDrums;
            bool hasViolinMelody = false; // バイオリン主旋律は一時ミュート中（復帰時は cycle >= 2 に設定）
            if (isDrumsOnly)
            {
                hasBass = false;
                hasDrums = true;
            }
            else
            {
                hasBass = (cycle >= 1);         // 12秒〜（2周目・3周目）でベース鳴動！
                hasDrums = (cycle >= 2);        // 24秒〜（3周目）でドラム合流！
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
                float gain = (!hasBass && !hasDrums) ? 0.11f : 0.08f;
                brass += s * (gain / chord.Length);
            }
            brass *= env;

            // 2. 駆け上がるキラキラしたアルペジオ
            float arpPhase = (t * 8.0f) % 1.0f;
            int arpIndex = Mathf.FloorToInt(t * 8.0f) % chord.Length;
            float arpFreq = chord[arpIndex] * 2.0f;
            float arpEnv = Mathf.Exp(-arpPhase * 5.0f);
            float arpWave = Mathf.Sin(2f * Mathf.PI * arpFreq * t) * arpEnv * 0.13f;

            // 3. 揺れのない澄み切ったピュア・バイオリン主旋律（白玉＋4分音符のバッハ風旋律）
            // 3周目（24秒〜）から開始し、8小節4連駆け上がりから1小節目へ突入！Rust回復後は消える
            float strL = 0f;
            float strR = 0f;
            {
                float mel = 0f;
                if (hasViolinMelody)
                {
                    float melFade = 0.08f; // 4分音符（0.375秒）の小気味よい輪郭を際立たせるレガート

                    for (int m = 0; m < melodyNotes.Length; m++)
                    {
                        var note = melodyNotes[m];
                        float noteDur = note.end - note.start;

                        for (int d = 0; d < 3; d++)
                        {
                            float nT = (d == 0) ? (cycleT - note.start) : ((d == 1) ? (cycleT + 12.0f - note.start) : (cycleT - 12.0f - note.start));
                            if (nT >= -melFade && nT <= noteDur + melFade)
                            {
                                float melEnv = 1f;
                                if (nT < melFade)
                                    melEnv = Mathf.SmoothStep(0f, 1f, (nT + melFade) / (melFade * 2f));
                                else if (nT > noteDur - melFade)
                                    melEnv = Mathf.SmoothStep(1f, 0f, (nT - (noteDur - melFade)) / (melFade * 2f));

                                if (melEnv > 0.001f)
                                {
                                    float f = note.freq; // 揺れのない完全なピュアピッチ（ヴィブラートなし）

                                    // 澄み切った大空に響くまっすぐで透き通るバイオリン（基音＋クリア倍音）
                                    float vPure = Mathf.Sin(2f * Mathf.PI * f * t) * 0.58f
                                                + Mathf.Sin(4f * Mathf.PI * f * t) * 0.22f
                                                + Mathf.Sin(6f * Mathf.PI * f * t) * 0.08f
                                                + Mathf.Sin(8f * Mathf.PI * f * t) * 0.03f;

                                    mel += vPure * melEnv;
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

                float strIntensity = (!hasBass && !hasDrums) ? 0.15f : (hasDrums ? 0.23f : 0.19f);
                if (isDrumsOnly) strIntensity = 0.24f;

                // バイオリン主旋律（3周目のみ合流） ＋ 温かいピュアパッド
                float combined = (mel * 0.33f + pad * 0.30f) * strIntensity;
                strL = combined * 0.98f;
                strR = combined * 1.02f;
            }

            float kick = 0f;
            float snare = 0f;
            float hat = 0f;
            float synthBass = 0f;

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

            // ブリブリベース（16分ストレート・Moog風ドライブシンセベース）
            if (hasBass)
            {
                float bRoot = bassRoots8[bar8];
                float bOct = bRoot * 2.0f;
                float stepT = (bar8T / 0.1875f) % 1.0f;
                int stepIndex = Mathf.FloorToInt(bar8T / 0.1875f) % 8;
                float bVel = (stepIndex % 2 == 0) ? 1.05f : 0.85f;
                float bEnv = Mathf.Exp(-stepT * 18.0f) * bVel;

                float filterEnv = Mathf.Exp(-stepT * 28.0f);

                float sub1 = Mathf.Sin(2f * Mathf.PI * bRoot * t) * 0.75f;
                float saw1 = Mathf.Sin(2f * Mathf.PI * bRoot * t) * 0.45f
                           - Mathf.Sin(4f * Mathf.PI * bRoot * t) * 0.25f
                           + Mathf.Sin(6f * Mathf.PI * bRoot * t) * 0.15f
                           - Mathf.Sin(8f * Mathf.PI * bRoot * t) * 0.08f;

                float sub2 = Mathf.Sin(2f * Mathf.PI * bOct * t) * 0.35f;
                float saw2 = (Mathf.Sin(2f * Mathf.PI * bOct * t) * 0.28f
                           - Mathf.Sin(4f * Mathf.PI * bOct * t) * 0.14f);

                float rawBass = (sub1 + sub2) + (saw1 + saw2) * (0.35f + filterEnv * 0.85f);
                float fatBass = (float)System.Math.Tanh(rawBass * 1.50f);
                synthBass = fatBass * bEnv * 0.54f; // 太くグルーヴィーなブリブリベース
            }

            float loopFade = 1f;
            if (t < 0.2f) loopFade = t / 0.2f;
            else if (t > duration - 0.2f) loopFade = (duration - t) / 0.2f;

            float outputGain = EndingThemeVolume / Mathf.Max(0.01f, SkybreakSourceVolume);
            float mixedL = (brass + arpWave + kick + snare + hat + synthBass + strL) * loopFade * outputGain;
            float mixedR = (brass + arpWave + kick + snare + hat + synthBass + strR) * loopFade * outputGain;
            float outL = (float)System.Math.Tanh(mixedL * 0.98f) * 0.92f;
            float outR = (float)System.Math.Tanh(mixedR * 0.98f) * 0.92f;

            samples[i * 2] = outL;
            samples[i * 2 + 1] = outR;
        }

        string clipName = isDrumsOnly ? "Music_RustFloat_Skybreak_DrumsOnly" : "Music_RustFloat_Skybreak_BuildUp";
        var clip = AudioClip.Create(clipName, totalSamples, 2, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
