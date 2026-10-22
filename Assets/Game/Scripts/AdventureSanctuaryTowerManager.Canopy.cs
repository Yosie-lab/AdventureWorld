using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 天蓋開放台本ビート（Opening後のレバー〜光柱上昇まで）
/// </summary>
public partial class AdventureSanctuaryTowerManager
{
    #region 5. 天蓋開放台本ビート制御

    void SetScreenScriptSubtitle(string title, string speaker, string body, Color accent, bool isPrompt = false, string hint = null)
    {
        EnsureCinematicLetterbox();
        if (_filmSubtitleUi == null)
            BuildFilmSubtitleOnLetterbox();

        if (_letterboxRoot != null && !_letterboxRoot.activeSelf)
            _letterboxRoot.SetActive(true);
        if (_letterboxCg != null)
            _letterboxCg.alpha = 1f;
        _letterboxTargetAlpha = 1f;

        if (_filmSubtitleUi == null) return;

        Font font = ResolveEpilogueFont();
        if (font != null) _filmSubtitleUi.font = font;

        string mainText = "";
        if (isPrompt)
        {
            string divePrompt = AdventureLocalizationManager.Get("canopy_dive_prompt");
            if (string.IsNullOrEmpty(divePrompt) || divePrompt == "canopy_dive_prompt")
                divePrompt = "<size=34><color=#FFD700><b>【 Space / クリック長押し 】</b></color> 光の柱へダイブ！</size>";
            mainText = divePrompt;
        }
        else if (!string.IsNullOrEmpty(speaker))
        {
            string locSpeaker = AdventureLocalizationManager.Get(speaker);
            if (!string.IsNullOrEmpty(locSpeaker)) speaker = locSpeaker;

            string locBody = AdventureLocalizationManager.Get(body);
            if (!string.IsNullOrEmpty(locBody)) body = locBody;

            string quoteL = AdventureLocalizationManager.CurrentLanguage switch
            {
                AdventureLocalizationManager.GameLanguage.English => "\"",
                AdventureLocalizationManager.GameLanguage.Spanish => "«",
                _ => "「"
            };
            string quoteR = AdventureLocalizationManager.CurrentLanguage switch
            {
                AdventureLocalizationManager.GameLanguage.English => "\"",
                AdventureLocalizationManager.GameLanguage.Spanish => "»",
                _ => "」"
            };
            string hex = ColorUtility.ToHtmlStringRGBA(accent);
            mainText = $"<color=#{hex}><b>{speaker}</b></color> {quoteL}{body}{quoteR}";
        }
        else if (!string.IsNullOrEmpty(title))
        {
            string locTitle = AdventureLocalizationManager.Get(title);
            if (!string.IsNullOrEmpty(locTitle)) title = locTitle;

            string locBody = AdventureLocalizationManager.Get(body);
            if (!string.IsNullOrEmpty(locBody)) body = locBody;

            string hex = ColorUtility.ToHtmlStringRGBA(accent);
            mainText = $"<color=#{hex}><b>✦ {title} ✦</b></color>\n{body}";
        }
        else
        {
            string locBody = AdventureLocalizationManager.Get(body);
            if (!string.IsNullOrEmpty(locBody)) body = locBody;

            string hex = ColorUtility.ToHtmlStringRGBA(accent);
            mainText = $"<color=#{hex}>{body}</color>";
        }

        if (!string.IsNullOrEmpty(hint))
        {
            string locHint = AdventureLocalizationManager.Get(hint);
            if (!string.IsNullOrEmpty(locHint)) hint = locHint;
            mainText += $"\n<size=19><color=#FFE073><b>{hint}</b></color></size>";
        }

        PrepareFontForText(font, mainText, 28);
        _filmSubtitleUi.text = mainText;
        _filmSubtitleUi.color = Color.white;
        _filmSubtitleUi.transform.localScale = Vector3.one;
    }

    void BeginCanopyScriptBeats()
    {
        if (_canopyBeatIndex >= 0)
        {
            Debug.LogWarning("[RustAndFloat] 天蓋台本は既に進行中 — BeginCanopyScriptBeats をスキップ");
            return;
        }

        _suppressClimax = true;
        _climaxCrisisStarted = false;
        _endingSequenceActive = true;
        _leverPulled = true;
        _scriptHoldTimer = 0f;
        ClearPendingClimax();

        SetExplorationHudVisible(false);
        if (_scriptUiRoot != null)
            _scriptUiRoot.SetActive(false);

        EnsureCinematicLetterbox();
        if (_letterboxRoot != null)
        {
            _letterboxRoot.SetActive(true);
            if (_letterboxCg != null) _letterboxCg.alpha = 1f;
        }
        _letterboxTargetAlpha = 1f;

        _canopyBeatIndex = 0;
        _skyTearPlayed = false;
        _canopyBeat0TimerArmed = false;
        _canopyBeat0LockUntil = 0f;
        _scriptRequireInputRelease = true; // レバー引いた操作の余韻による即時スキップを完全防止
        PresentCanopyBeat(0);
        AdventureStoryFlow.InvalidateCache();
        Debug.Log("[RustAndFloat] 天蓋スクリーンスクリプト（シネマ字幕）を Update 駆動で開始（全" + CanopyBeats.Length + "枚）");

        StartCoroutine(DeferredCanopyEnvironmentRoutine());
    }

    /// <summary>字幕・レターボックス表示後に重い空／BGM／VFXを載せる（レバー操作の体感遅延を防ぐ）</summary>
    IEnumerator DeferredCanopyEnvironmentRoutine()
    {
        yield return null;

        var player = AdventurePlayerController.Instance
                     ?? Object.FindAnyObjectByType<AdventurePlayerController>();
        if (player != null && player.transform.position.y < 90f)
            player.ForceGroundReset();

        AdventureMusicDirector.Ensure();
        AdventureMusicDirector.Instance?.PlaySkybreakTheme(force: true);
        StartSkybreakWindAmbience();
        AdventureSoaringSeagullsManager.SilenceAllWorldSeagulls(true);
        SpawnWildernessPanorama(coldCrisis: true);
        ApplySkybreakColdAtmosphere();
        AdventureParticleSanitizer.SanitizeAllParticles();

        var piano = AdventureAncientPianoRelic.Instance ?? Object.FindAnyObjectByType<AdventureAncientPianoRelic>();
        if (piano != null)
            piano.FadeOutPiano(0.6f);

        var drone = GetDrone();
        if (drone != null)
        {
            drone.ClearSpeech();
            drone.StartSkybreakNestle();
        }
    }

    void GetLocalizedCanopyBeat(int index, out string title, out string speaker, out string body)
    {
        var beat = CanopyBeats[index];
        title = beat.Title;
        speaker = beat.Speaker;
        body = beat.Body;

        string tKey = $"canopy_{index}_title";
        string locTitle = AdventureLocalizationManager.Get(tKey);
        if (!string.IsNullOrEmpty(locTitle) && locTitle != tKey) title = locTitle;

        string sKey = $"canopy_{index}_speaker";
        string locSpeaker = AdventureLocalizationManager.Get(sKey);
        if (!string.IsNullOrEmpty(locSpeaker) && locSpeaker != sKey) speaker = locSpeaker;

        string bKey = $"canopy_{index}_body";
        string locBody = AdventureLocalizationManager.Get(bKey);
        if (!string.IsNullOrEmpty(locBody) && locBody != bKey) body = locBody;
    }

    void PresentCanopyBeat(int index)
    {
        if (index < 0 || index >= CanopyBeats.Length) return;
        var beat = CanopyBeats[index];
        GetLocalizedCanopyBeat(index, out string title, out string speaker, out string body);

        SuppressAllSpeechAndBanners();
        _scriptBoardTitle = title ?? "";
        _scriptBoardSpeaker = speaker ?? "";
        _scriptBoardBody = body ?? "";
        _scriptBoardAccent = beat.Accent;
        _scriptBoardIsDive = beat.IsDive;
        _scriptBoardAdvance = false;
        _scriptBoardVisible = true;
        _scriptBoardOpenedAt = Time.unscaledTime;
        if (index == 0)
        {
            _canopyBeat0TimerArmed = false;
            _canopyBeat0LockUntil = 0f;
        }
        _scriptHoldTimer = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        EnsureEventSystemForUi();
        // 中央ボードは出さず、スクリーンスクリプト（シネマ字幕）として映画のように表示
        if (_scriptUiRoot != null)
            _scriptUiRoot.SetActive(false);

        SetScreenScriptSubtitle(title, speaker, body, beat.Accent, beat.IsDive);

        // 「空が……割れるよ」で天空裂開シーン
        if (index == 2 && !_skyTearPlayed)
        {
            _skyTearPlayed = true;
            if (GameObject.Find("SkybreakEffect") == null)
                SpawnSkybreakCracks(new Vector3(512f, 150f, 512f));
            StartCoroutine(AdventureSkybreakVisuals.PlaySkyTearOpenRoutine());
            Debug.Log("[RustAndFloat] 天空裂開シーン開始（7.0秒演出）");
        }
        else if (index >= 3)
        {
            // 「ありがとうRust…！」以降、光の柱へ飛び込む（ダイブ完了）まで、中規模の稲妻と地震を持続ループ
            StartSkybreakAftershockLoop();
        }

        Debug.Log($"[RustAndFloat] スクリーンスクリプト {index + 1}/{CanopyBeats.Length}: {beat.Title} {beat.Speaker}");
    }

    Coroutine _skybreakAftershockRoutine;

    void StartSkybreakAftershockLoop()
    {
        if (_skybreakAftershockRoutine != null) return;
        _skybreakAftershockRoutine = StartCoroutine(SkybreakAftershockLoopRoutine());
        Debug.Log("[RustAndFloat] 天蓋崩壊余震・稲妻ループ開始（光の柱ダイブまで継続）");
    }

    void StopSkybreakAftershockLoop()
    {
        if (_skybreakAftershockRoutine != null)
        {
            StopCoroutine(_skybreakAftershockRoutine);
            _skybreakAftershockRoutine = null;
            Debug.Log("[RustAndFloat] 天蓋崩壊余震・稲妻ループ停止");
        }
    }

    bool IsCanopyBeat0SubtitleVisible()
    {
        if (!_scriptBoardVisible || _canopyBeatIndex != 0)
            return false;
        if (_letterboxRoot == null || !_letterboxRoot.activeSelf)
            return false;
        if (_letterboxCg != null && _letterboxCg.alpha < 0.35f)
            return false;
        if (_filmSubtitleUi == null || string.IsNullOrEmpty(_filmSubtitleUi.text))
            return false;
        return _filmSubtitleUi.color.a >= 0.35f;
    }

    void ArmCanopyBeat0AutoTimer()
    {
        float autoSec = CanopyBeats[0].AutoAdvanceSeconds;
        _canopyBeat0TimerArmed = true;
        _canopyBeat0VisibleStreak = 2;
        _canopyBeat0LockUntil = Time.unscaledTime + autoSec;
    }

    void TryArmCanopyBeat0Timer()
    {
        if (_canopyBeatIndex != 0 || _canopyBeat0TimerArmed)
            return;
        if (!IsCanopyBeat0SubtitleVisible())
            return;
        ArmCanopyBeat0AutoTimer();
    }

    void RefreshCanopyBeat0Subtitle()
    {
        if (_canopyBeatIndex != 0)
            return;
        var beat = CanopyBeats[0];
        GetLocalizedCanopyBeat(0, out string title, out string speaker, out string body);
        SetScreenScriptSubtitle(title, speaker, body, beat.Accent, beat.IsDive);
    }

    IEnumerator SkybreakAftershockLoopRoutine()
    {
        while (_canopyBeatIndex >= 3 && _endingSequenceActive)
        {
            // 最初（0.7〜0.85f）ほどではないが、迫力ある中規模の地震（0.30f〜0.42f）と空の稲妻・雷鳴
            float shakeAmp = Random.Range(0.30f, 0.42f);
            float vol = Random.Range(0.45f, 0.60f);
            yield return StartCoroutine(AdventureSkybreakVisuals.PlaySkybreakLightningAndEarthquakeRoutine(shakeIntensity: shakeAmp, volume: vol));
            yield return new WaitForSecondsRealtime(Random.Range(1.2f, 2.0f));
        }
        _skybreakAftershockRoutine = null;
    }

    void TickCanopyScriptBeats()
    {
        // 孤児救済：シークエンス中にボードだけ残ってインデックスが死んでいる場合
        if (_canopyBeatIndex < 0)
        {
            if (_endingSequenceActive && _scriptBoardVisible && !_climaxCrisisStarted && !_epilogueTriggered)
            {
                Debug.LogWarning("[RustAndFloat] 台本インデックス喪失を検出 → 字幕のみ復旧（第1幕タイマーは維持）");
                _canopyBeatIndex = 0;
                if (!_canopyBeat0TimerArmed)
                    ArmCanopyBeat0AutoTimer();
                RefreshCanopyBeat0Subtitle();
            }
            return;
        }

        float openFor = Time.unscaledTime - _scriptBoardOpenedAt;

        if (_canopyBeatIndex == 0)
            TryArmCanopyBeat0Timer();

        // 字幕自己修復：他処理（リセット・発話クリア等）で字幕が消されても、表示中のビートを毎フレーム保証する
        if (_scriptBoardVisible && !_scriptBoardIsDive && _canopyBeatIndex < CanopyBeats.Length
            && (_filmSubtitleUi == null || string.IsNullOrEmpty(_filmSubtitleUi.text) || _filmSubtitleUi.color.a < 0.99f))
        {
            if (_canopyBeatIndex == 0)
                RefreshCanopyBeat0Subtitle();
            else
            {
                var cur = CanopyBeats[_canopyBeatIndex];
                SetScreenScriptSubtitle(cur.Title, cur.Speaker, cur.Body, cur.Accent, cur.IsDive);
            }
        }

        // 各ビートの定義データから自動送り秒数を取得（1枚目2.0s／2・3枚目5.2s／Float・Rust 4.2s／ダイブ6.0s）
        float rawAuto = (_canopyBeatIndex >= 0 && _canopyBeatIndex < CanopyBeats.Length)
            ? CanopyBeats[_canopyBeatIndex].AutoAdvanceSeconds
            : 3.5f;
        float autoSec = AdventureSpeechBoardLayout.ScaleScriptAutoAdvance(rawAuto, this);

        if (_canopyBeatIndex == 0)
        {
            if (!IsCanopyBeat0SubtitleVisible())
            {
                RefreshCanopyBeat0Subtitle();
                return;
            }

            if (!_canopyBeat0TimerArmed)
                ArmCanopyBeat0AutoTimer();

            if (Time.unscaledTime < _canopyBeat0LockUntil)
                return;

            _scriptBoardAdvance = true;
        }
        else
        {
            float minShow = AdventureSpeechBoardLayout.ScriptManualMinShowSec;
            if (openFor < minShow)
            {
                _scriptBoardAdvance = false;
                return;
            }

            if (!_scriptRequireInputRelease && !_scriptBoardIsDive)
                PollScriptBoardAdvance();

            if (_scriptBoardIsDive)
            {
                if (IsDiveConfirmHeld())
                {
                    _scriptHoldTimer += Time.unscaledDeltaTime;
                    if (_filmSubtitleUi != null)
                    {
                        Font f = ResolveEpilogueFont();
                        if (f != null && _filmSubtitleUi.font != f) _filmSubtitleUi.font = f;
                        string divingText = AdventureLocalizationManager.Get("canopy_diving_prompt");
                        _filmSubtitleUi.text = divingText;
                        PrepareFontForText(f, divingText, 34, FontStyle.Bold);
                    }
                    if (_scriptHoldTimer >= 0.35f)
                        _scriptBoardAdvance = true;
                }
                else
                {
                    _scriptHoldTimer = 0f;
                    if (_filmSubtitleUi != null)
                    {
                        Font f = ResolveEpilogueFont();
                        if (f != null && _filmSubtitleUi.font != f) _filmSubtitleUi.font = f;
                        string diveText = AdventureLocalizationManager.Get("canopy_dive_prompt");
                        _filmSubtitleUi.text = diveText;
                        PrepareFontForText(f, diveText, 34, FontStyle.Bold);
                    }
                }
            }

            if (openFor >= autoSec)
                _scriptBoardAdvance = true;
        }

        if (!_scriptBoardAdvance) return;

        _scriptBoardAdvance = false;
        _scriptHoldTimer = 0f;
        _scriptRequireInputRelease = true; // セリフ送り時の突き抜けを完全防止（キーを一度離すまで次を受け付けない）

        int next = _canopyBeatIndex + 1;
        if (_canopyBeatIndex == 0)
        {
            float shown = _canopyBeat0TimerArmed
                ? Time.unscaledTime - (_canopyBeat0LockUntil - CanopyBeats[0].AutoAdvanceSeconds)
                : 0f;
            Debug.Log("[RustAndFloat] 台本ビート送り: 0 → 1（第1幕表示=" + shown.ToString("F2") + "s / 規定=" + CanopyBeats[0].AutoAdvanceSeconds.ToString("F1") + "s / armed=" + _canopyBeat0TimerArmed + "）");
            _canopyBeat0TimerArmed = false;
            _canopyBeat0VisibleStreak = 0;
        }
        else
            Debug.Log($"[RustAndFloat] 台本ビート送り: {_canopyBeatIndex} → {next}（表示={openFor:F2}s / 自動={autoSec:F1}s）");
        if (next >= CanopyBeats.Length)
        {
            FinishCanopyScriptBeats();
            return;
        }

        _canopyBeatIndex = next;
        PresentCanopyBeat(next);
    }

    void FinishCanopyScriptBeats()
    {
        Debug.Log("[RustAndFloat] 天蓋スクリーンスクリプト完了 → 光の柱上昇 → クライマックスへ");
        _canopyBeatIndex = -1;
        _canopyBeat0LockUntil = 0f;
        _canopyBeat0TimerArmed = false;
        _canopyBeat0VisibleStreak = 0;
        _scriptBoardAdvance = false;
        _scriptBoardVisible = false;
        _scriptBoardIsDive = false;
        _scriptBoardTitle = "";
        _scriptBoardSpeaker = "";
        _scriptBoardBody = "";
        StopSkybreakAftershockLoop();
        ClearFilmSubtitle();
        if (_scriptUiRoot != null)
            _scriptUiRoot.SetActive(false);

        _endingSequenceActive = false;
        // レバー再入禁止を維持（false にすると Space 長押しで台本が最初へ巻き戻る）
        _leverPulled = true;
        _leverPullLockUntil = Time.unscaledTime + 3600f;
        _suppressClimax = false;
        _climaxOilInjected = false;
        // クライマックスは柱上昇完了後に開始（ここでは立てない）
        _climaxCrisisStarted = false;
        _ignoreSavedCanopyState = false;
        IsCanopyBroken = true;

        // 上昇演出を優先。最大4.5秒でクライマックスへ必ず接続（後半途切れ防止）
        ArmPendingClimax(failsafeSeconds: 4.5f);

        var drone = GetDrone();
        if (drone != null)
            drone.StartSkybreakNestle();

        BuildSkybreakHyperUpdraft(new Vector3(512f, 62f, 512f));

        var player = GetPlayer();
        if (player != null)
        {
            player.PrepareSkybreakPillarAscend();
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            float startY = Mathf.Max(player.transform.position.y, TerraceTopY + 1f);
            player.transform.position = new Vector3(512f, startY + 2.5f, 512f);
            if (cc != null) cc.enabled = true;
            player.BeginSkybreakPillarAscend(new Vector3(512f, 0f, 512f), 38f, 156f);
        }
    }

    /// <summary>光の柱到達：上昇完了からクライマックスへ</summary>
    public void NotifyPillarAscendComplete()
    {
        if (_climaxCrisisStarted || _epilogueTriggered) return;
        // 高度到達待ち。0秒＝無期限タイムアウトではなく「高さ条件のみ」
        ArmPendingClimax(failsafeSeconds: 8f);
        _suppressClimax = false;
        _ignoreSavedCanopyState = false;
        if (!_isCanopyBroken)
            IsCanopyBroken = true;

        var player = GetPlayer();
        TryStartPendingClimax(player);
    }

    void TryStartPendingClimax(AdventurePlayerController player)
    {
        if (!_pendingClimaxAfterCanopy) return;
        if (_climaxCrisisStarted || _epilogueTriggered) return;
        if (_canopyBeatIndex >= 0 || _endingSequenceActive) return;

        // 光の柱上昇中は割れ目手前で止まったままクライマックスに入らない
        bool highEnough = player != null
                          && !player.IsSkybreakPillarAscending
                          && player.transform.position.y >= 147f;
        // deadline<=0 は「高さ待ちのみ」。0を即タイムアウト扱いすると地上リセット後にエンディングが再点火する
        bool timedOut = _pendingClimaxDeadline > 0f
                        && Time.unscaledTime >= _pendingClimaxDeadline;
        if (!highEnough && !timedOut) return;

        ClearPendingClimax();
        _suppressClimax = false;
        _ignoreSavedCanopyState = false;
        if (!_isCanopyBroken)
            IsCanopyBroken = true;

        const float climaxSkyY = 156f;
        if (player != null && player.transform.position.y < climaxSkyY - 2f)
            player.ForceSkybreakArrival(new Vector3(512f, climaxSkyY, 512f), climaxSkyY);

        Debug.Log($"[RustAndFloat] クライマックス開始 high={highEnough} timeout={timedOut}");
        AdventureStoryFlow.InvalidateCache();
        BeginClimaxSequence();
    }

    void ClearPendingClimax()
    {
        _pendingClimaxAfterCanopy = false;
        _pendingClimaxDeadline = 0f;
    }

    void ArmPendingClimax(float failsafeSeconds)
    {
        _pendingClimaxAfterCanopy = true;
        _pendingClimaxDeadline = failsafeSeconds <= 0f
            ? 0f // 0 = タイムアウトなし（高さ条件のみ）
            : Time.unscaledTime + failsafeSeconds;
    }

    void SuppressAllSpeechAndBanners()
    {
        var drone = GetDrone();
        if (drone != null)
            drone.ClearSpeech();
        if (AdventureScrapHUD.Instance != null)
            AdventureScrapHUD.Instance.HideBannerImmediately();
    }

    /// <summary>プレイヤー／UIから台本送りを直接要求</summary>
    public void NotifyScriptBoardAdvance()
    {
        if (!_scriptBoardVisible) return;
        if (_canopyBeatIndex == 0) return;
        if (_climaxCrisisStarted) return;
        if (_scriptBoardIsDive) return;
        if (PollManualScriptAdvance(minShowSec: AdventureSpeechBoardLayout.ScriptManualMinShowSec, blockCanopyBeat0: true))
            Debug.Log("[RustAndFloat] 台本送り入力を受け付けました");
    }

    static bool IsDiveConfirmHeld()
    {
        bool touch = false;
        try { touch = Input.touchSupported && Input.touchCount > 0; } catch { }
        return AdventureInputReader.OilHoldButtons
            || AdventureInputReader.MouseRightHeld
            || AdventureInputReader.SpaceOrJHeld
            || touch;
    }

    #endregion

}
