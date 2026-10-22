using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// クライマックス（Rust凍結危機・注油）
/// </summary>
public partial class AdventureSanctuaryTowerManager
{
    #region 9. クライマックス (Rust凍結危機 & 注油インタラクション)

    void BeginClimaxSequence()
    {
        if (_climaxCrisisStarted) return;
        ClearPendingClimax();
        _climaxCrisisStarted = true;
        _climaxOilInjected = false;
        _climaxOilWaiting = false;
        _oilHoldTimer = 0f;
        _climaxBeatIndex = 0;
        _scriptHoldTimer = 0f;
        _scriptRequireInputRelease = true; // 光の柱上昇の長押しが残って最初の「限界高度」を即スキップするのを防止
        _suppressClimax = false;
        _ignoreSavedCanopyState = false;

        var drone = GetDrone();
        var player = GetPlayer();

        SetCinematicCamera(true);
        EnsureCinematicLetterbox();
        if (_letterboxRoot != null)
        {
            _letterboxRoot.SetActive(true);
            if (_letterboxCg != null) _letterboxCg.alpha = 1f;
        }
        _letterboxTargetAlpha = 1f;
        SetExplorationHudVisible(false);
        if (_scriptUiRoot != null) _scriptUiRoot.SetActive(false);

        SpawnWildernessPanorama(coldCrisis: true);
        ApplySkybreakColdAtmosphere();
        AdventureParticleSanitizer.SanitizeAllParticles();

        if (player != null)
        {
            // 警告台本時点で必ず空中にいる
            if (player.transform.position.y < 140f)
                player.ForceSkybreakArrival(new Vector3(512f, 150f, 512f), 150f);
            else
                player.EndSkybreakPillarAscend();
            // オートヨーは切るが、注油前まで高度は ArmClimaxSkyHold で維持（手動 WASD は有効）
            player.SetAutoGlideMode(false);
            float holdY = Mathf.Max(player.transform.position.y, 148f);
            player.ArmClimaxSkyHold(holdY);
        }

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SuppressAllSpeechAndBanners();

        if (drone != null)
        {
            drone.StartClimaxCrisis();
            drone.ClearSpeech();
        }

        PresentClimaxBeat(0);
        AdventureStoryFlow.InvalidateCache();
        Debug.Log("[RustAndFloat] クライマックス（スクリーンスクリプト）を Update 駆動で開始");
    }

    void GetLocalizedClimaxBeat(int index, out string title, out string speaker, out string body)
    {
        var beat = ClimaxBeats[index];
        title = beat.Title;
        speaker = beat.Speaker;
        body = beat.Body;

        string tKey = $"climax_{index}_title";
        string locTitle = AdventureLocalizationManager.Get(tKey);
        if (!string.IsNullOrEmpty(locTitle) && locTitle != tKey) title = locTitle;

        string sKey = $"climax_{index}_speaker";
        string locSpeaker = AdventureLocalizationManager.Get(sKey);
        if (!string.IsNullOrEmpty(locSpeaker) && locSpeaker != sKey) speaker = locSpeaker;

        string bKey = $"climax_{index}_body";
        string locBody = AdventureLocalizationManager.Get(bKey);
        if (!string.IsNullOrEmpty(locBody) && locBody != bKey) body = locBody;
    }

    void PresentClimaxBeat(int index)
    {
        if (index < 0 || index >= ClimaxBeats.Length) return;
        var beat = ClimaxBeats[index];
        GetLocalizedClimaxBeat(index, out string title, out string speaker, out string body);

        SuppressAllSpeechAndBanners();
        _scriptBoardTitle = title ?? "";
        _scriptBoardSpeaker = speaker ?? "";
        _scriptBoardBody = body ?? "";
        _scriptBoardAccent = beat.Accent;
        _scriptBoardIsDive = false;
        _scriptBoardAdvance = false;
        _scriptBoardVisible = true;
        _scriptBoardOpenedAt = Time.unscaledTime;
        _scriptHoldTimer = 0f;
        // _scriptRequireInputRelease は BeginClimaxSequence や CompleteClimaxOil で明示設定された値を尊重（ここでの上書き解除を防止）
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        EnsureEventSystemForUi();
        // 中央ボードは出さず、スクリーンスクリプト（シネマ字幕）として映画のように表示
        if (_scriptUiRoot != null)
            _scriptUiRoot.SetActive(false);

        // 映画的な没入感を優先するため、字幕下部の操作ガイドは完全非表示
        SetScreenScriptSubtitle(title, speaker, body, beat.Accent, false, null);

        // 台本1（警告）：そばで震え始める
        // 台本2（気流が冷たい）：力なく落ちていく
        if (index == 1)
        {
            var droneFall = GetDrone();
            if (droneFall != null)
                droneFall.BeginClimaxColdFallAway();
        }

        // 台本（ピピッ！……ありがとう、Float！）：全出力セリフと同時にオーバードライブ演出
        if (index == ClimaxOilSlot + 1)
        {
            var drone = GetDrone();
            if (drone != null)
                drone.TriggerClimaxOverdrive();

            var cam = AdventureCameraFollow.Instance;
            if (cam != null)
                cam.Shake(0.65f, 0.95f);

            var player = GetPlayer();
            if (player != null)
                player.ApplyGlideBoost(1.25f, 6f);

            StopSkybreakAftershockLoop();
            AdventureSkybreakVisuals.HidePersistentSkyTearCrack();
            SpawnWildernessPanorama(coldCrisis: false);
            SoftenSkybreakColdAtmosphere();
            // 「ありがとう、Float！」からは風の音を控えめに下げてBGMとセリフを引き立てる
            SetSkybreakWindVolume(0.16f, 1.4f);
            AdventureCloudDrift.SetSkyWindAmbienceVolume(0.010f);
            StartCoroutine(AdventureSkybreakVisuals.BreakthroughFlashRoutine());
            AdventureMusicDirector.Ensure();
            AdventureMusicDirector.Instance?.TriggerSkybreakOverdriveDrop();
            _scriptRequireInputRelease = true; // 前セリフ送りやブーストのキー余韻による次セリフスキップを完全防止
            _scriptHoldTimer = 0f;
            if (_scriptHintUi != null)
                _scriptHintUi.text = "";
        }
    }

    void BeginClimaxOilWait()
    {
        _scriptBoardVisible = false;
        _scriptBoardAdvance = false;
        _scriptBoardTitle = "";
        _scriptBoardSpeaker = "";
        _scriptBoardBody = "";
        if (_scriptUiRoot != null)
            _scriptUiRoot.SetActive(false);

        _climaxOilWaiting = true;
        _climaxOilInjected = false;
        _oilHoldTimer = 0f;
        _oilWaitOpenedAt = Time.unscaledTime;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SuppressAllSpeechAndBanners();
        EnsureOilPromptUI();
        Debug.Log("[RustAndFloat] 注油フェーズ開始（E/Space/クリック長押し）");
    }

    void TickClimaxSequence()
    {
        if (!_climaxCrisisStarted || _epilogueTriggered || _suppressClimax)
            return;

        if (_climaxOilWaiting)
        {
            TickClimaxOilHold();
            return;
        }

        // 最終セリフ後に完全に放置された場合の保険（12秒）
        if (_climaxOilInjected
            && _climaxBeatIndex >= ClimaxBeats.Length - 1
            && !_epilogueTriggered)
        {
            float finalOpen = Time.unscaledTime - _scriptBoardOpenedAt;
            if (finalOpen >= 12.0f)
            {
                Debug.LogWarning("[RustAndFloat] 最終セリフタイムアウト → エピローグ強制開始");
                FinishClimaxSequence();
                return;
            }
        }

        if (_climaxBeatIndex < 0 || !_scriptBoardVisible)
            return;

        bool isRevivalBeat = (_climaxBeatIndex == ClimaxOilSlot); // beat 3: 「……あ……温かい油が……心臓部に……」
        float openFor = Time.unscaledTime - _scriptBoardOpenedAt;

        float rawAuto = (_climaxBeatIndex >= 0 && _climaxBeatIndex < ClimaxBeats.Length)
            ? ClimaxBeats[_climaxBeatIndex].AutoAdvanceSeconds
            : 4.5f;
        float autoSec = isRevivalBeat
            ? ClimaxRevivalBeatHoldSec
            : AdventureSpeechBoardLayout.ScaleScriptAutoAdvance(rawAuto, this);

        // 蘇生セリフは9秒フル表示（短縮倍率・手動送りなし）
        if (isRevivalBeat)
        {
            _scriptBoardAdvance = false;
            if (openFor < autoSec)
                return;
            _scriptBoardAdvance = true;
            Debug.Log($"[RustAndFloat] クライマックス蘇生セリフ自動送り: {openFor:F1}s");
        }
        else
        {
            float minHoldOpen = 0.35f;
            if (openFor < minHoldOpen)
            {
                _scriptBoardAdvance = false;
                return;
            }

            if (!_scriptRequireInputRelease)
                PollScriptBoardAdvance();

            if (openFor >= autoSec)
            {
                _scriptBoardAdvance = true;
                Debug.Log($"[RustAndFloat] クライマックス自動送り発動: beat={_climaxBeatIndex} (経過={openFor:F1}s)");
            }
        }

        if (!_scriptBoardAdvance) return;

        // 次のセリフへ進む瞬間に入力フラグとホールドタイマーをリセット
        _scriptHoldTimer = 0f;
        _scriptBoardAdvance = false;
        _scriptRequireInputRelease = true; // キーを一度離すまで次のセリフ送りを待つガードを確実にセット

        int next = _climaxBeatIndex + 1;

        // index 0,1,2 のあと（next==3）で注油へ
        if (next == ClimaxOilSlot && !_climaxOilInjected)
        {
            BeginClimaxOilWait();
            return;
        }

        if (next >= ClimaxBeats.Length)
        {
            FinishClimaxSequence();
            return;
        }

        _climaxBeatIndex = next;
        Debug.Log($"[RustAndFloat] クライマックス台本進行: beat={next}");
        PresentClimaxBeat(next);
    }


    void HideScriptBoardCompletely()
    {
        _scriptBoardVisible = false;
        _scriptBoardAdvance = false;
        _scriptBoardIsDive = false;
        _scriptBoardTitle = "";
        _scriptBoardSpeaker = "";
        _scriptBoardBody = "";
        if (_scriptUiRoot != null)
            _scriptUiRoot.SetActive(false);
        else
            DestroyOrphanEndingScriptBoardCanvas();

        if (_oilUiRoot != null && !IsClimaxOilPromptActive)
            _oilUiRoot.SetActive(false);
    }

    void TickClimaxOilHold()
    {
        if (!_climaxOilWaiting || _climaxOilInjected) return;

        bool holding = AdventureInputReader.OilHoldButtons;
        if (holding)
        {
            if (_oilHoldFrame != Time.frameCount)
            {
                _oilHoldFrame = Time.frameCount;
                _oilHoldTimer += Time.unscaledDeltaTime;
            }
        }
        else
        {
            if (_oilHoldFrame != Time.frameCount)
            {
                _oilHoldTimer = Mathf.Max(0f, _oilHoldTimer - Time.unscaledDeltaTime * 0.75f);
            }
        }

        if (Time.unscaledTime - _oilWaitOpenedAt >= 10f)
            _oilHoldTimer = OilHoldRequired;

        RefreshOilPromptUI();

        if (_oilHoldTimer >= OilHoldRequired)
            CompleteClimaxOil();
    }

    /// <summary>外部／プレイヤーから注油ホールドを加算（同一フレームでの多重加算を防止）</summary>
    public void NotifyOilHold(float dt)
    {
        if (!IsClimaxOilPromptActive || _climaxOilInjected) return;
        if (_oilHoldFrame == Time.frameCount) return;
        _oilHoldFrame = Time.frameCount;
        _oilHoldTimer += Mathf.Max(0f, dt);
        if (_oilHoldTimer >= OilHoldRequired)
            CompleteClimaxOil();
    }

    void CompleteClimaxOil()
    {
        if (_climaxOilInjected) return;
        _climaxOilInjected = true;
        _climaxOilWaiting = false;
        _oilHoldTimer = OilHoldRequired;
        HideOilPromptUI();

        var drone = GetDrone();
        if (drone != null)
            drone.StartClimaxPetAndOil();

        var cam = AdventureCameraFollow.Instance;
        if (cam != null)
            cam.Shake(0.35f, 0.55f);

        StartCoroutine(ClimaxOilWarmGlowRoutine());

        // 注油完了：極寒の雷雲・冷気を解き、暖かな日光と黄金の祝福光芒・色彩豊かな景色を展開
        SpawnWildernessPanorama(coldCrisis: false);
        SoftenSkybreakColdAtmosphere();

        _climaxBeatIndex = ClimaxOilSlot;
        _scriptBoardAdvance = false;
        _scriptHoldTimer = 0f;
        _scriptBoardOpenedAt = Time.unscaledTime;
        _scriptRequireInputRelease = true; // 注油長押しを一度離すまで蘇生セリフの送りをガード
        PresentClimaxBeat(ClimaxOilSlot);
        Debug.Log("[RustAndFloat] 注油完了 → 蘇生台本3（温かい油が…）");
    }

    IEnumerator ClimaxOilWarmGlowRoutine()
    {
        var glowGo = new GameObject("ClimaxOilWarmGlowCanvas");
        var canvas = glowGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5300;
        var scaler = glowGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);

        var imgGo = new GameObject("Glow");
        imgGo.transform.SetParent(glowGo.transform, false);
        var imgRt = imgGo.AddComponent<RectTransform>();
        imgRt.anchorMin = Vector2.zero;
        imgRt.anchorMax = Vector2.one;
        imgRt.sizeDelta = Vector2.zero;
        var img = imgGo.AddComponent<Image>();
        img.color = new Color(1f, 0.88f, 0.45f, 0f);
        img.raycastTarget = false;

        float t = 0f;
        const float peakTime = 0.22f;
        while (t < peakTime)
        {
            t += Time.unscaledDeltaTime;
            img.color = new Color(1f, 0.88f, 0.45f, Mathf.Clamp01(t / peakTime) * 0.45f);
            yield return null;
        }
        t = 0f;
        const float fadeTime = 0.65f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / fadeTime);
            img.color = new Color(1f, 0.85f, 0.35f, a * 0.45f);
            yield return null;
        }
        Destroy(glowGo);
    }


    void RefreshClimaxOilFilmSubtitle()
    {
        if (!IsClimaxOilPromptActive) return;

        EnsureCinematicLetterbox();
        if (_filmSubtitleUi == null)
            BuildFilmSubtitleOnLetterbox();
        if (_filmSubtitleUi == null) return;

        if (_letterboxRoot != null && !_letterboxRoot.activeSelf)
            _letterboxRoot.SetActive(true);
        if (_letterboxCg != null)
            _letterboxCg.alpha = 1f;
        _letterboxTargetAlpha = 1f;

        Font font = ResolveEpilogueFont();
        if (font != null)
            _filmSubtitleUi.font = font;

        string title = AdventureLocalizationManager.Get("climax_oil_title");
        if (string.IsNullOrEmpty(title) || title == "climax_oil_title")
            title = SkyLimitWarning;
        string promptLead = AdventureLocalizationManager.Get("climax_oil_prompt");
        if (string.IsNullOrEmpty(promptLead) || promptLead == "climax_oil_prompt")
            promptLead = "常備油を心臓部へ";
        string promptKeys = AdventureLocalizationManager.Get("climax_oil_prompt_keys");
        if (string.IsNullOrEmpty(promptKeys) || promptKeys == "climax_oil_prompt_keys")
            promptKeys = "【E / Space / 長押し】";

        const string whiteHex = "FFFFFF";
        const string yellowHex = "FFD700";
        string mainText =
            $"<color=#{whiteHex}><b>{title}</b></color>\n" +
            $"<color=#{whiteHex}><b>{promptLead}</b></color> <color=#{yellowHex}><b>{promptKeys}</b></color>";

        _filmSubtitleUi.supportRichText = true;
        _filmSubtitleUi.fontSize = AdventureSpeechBoardLayout.CinematicFilmSubtitleSize;
        PrepareFontForText(font, title + promptLead + promptKeys, AdventureSpeechBoardLayout.CinematicFilmSubtitleSize, FontStyle.Bold);
        if (_filmSubtitleUi.gameObject != null && !_filmSubtitleUi.gameObject.activeSelf)
            _filmSubtitleUi.gameObject.SetActive(true);
        _filmSubtitleUi.text = mainText;
        _filmSubtitleUi.color = Color.white;
        _filmSubtitleUi.transform.localScale = Vector3.one;
    }

    void EnsureOilPromptUI()
    {
        EnsureEventSystemForUi();
        if (_oilUiRoot != null)
        {
            _oilUiRoot.SetActive(true);
            RefreshOilPromptUI();
            return;
        }

        var canvasGo = new GameObject("ClimaxOilPromptCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9600; // レターボックス(9500)より手前に確実に表示
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        canvasGo.AddComponent<GraphicRaycaster>();

        var panelGo = new GameObject("GaugePanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelGo.AddComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0f);
        panelRt.pivot = new Vector2(0.5f, 0f);
        panelRt.anchoredPosition = new Vector2(0f, 148f);
        panelRt.sizeDelta = new Vector2(560f, 12f);

        var gaugeBgGo = new GameObject("GaugeBg");
        gaugeBgGo.transform.SetParent(panelGo.transform, false);
        var gaugeBgRt = gaugeBgGo.AddComponent<RectTransform>();
        gaugeBgRt.anchorMin = Vector2.zero;
        gaugeBgRt.anchorMax = Vector2.one;
        gaugeBgRt.offsetMin = Vector2.zero;
        gaugeBgRt.offsetMax = Vector2.zero;
        var gaugeBgImg = gaugeBgGo.AddComponent<Image>();
        gaugeBgImg.color = new Color(0.06f, 0.10f, 0.18f, 0.55f);
        gaugeBgImg.raycastTarget = false;

        var gaugeFillGo = new GameObject("GaugeFill");
        gaugeFillGo.transform.SetParent(gaugeBgGo.transform, false);
        var gaugeFillRt = gaugeFillGo.AddComponent<RectTransform>();
        gaugeFillRt.anchorMin = new Vector2(0f, 0f);
        gaugeFillRt.anchorMax = new Vector2(0f, 1f);
        gaugeFillRt.pivot = new Vector2(0f, 0.5f);
        gaugeFillRt.anchoredPosition = Vector2.zero;
        gaugeFillRt.sizeDelta = new Vector2(0f, 0f);
        _oilGaugeFill = gaugeFillGo.AddComponent<Image>();
        _oilGaugeFill.color = new Color(1f, 0.82f, 0.22f, 1f);
        _oilGaugeFill.raycastTarget = false;

        _oilUiRoot = canvasGo;
        RefreshOilPromptUI();
    }

    void RefreshOilPromptUI()
    {
        if (_oilUiRoot == null) return;
        _oilUiRoot.SetActive(IsClimaxOilPromptActive);
        RefreshClimaxOilFilmSubtitle();

        if (_oilGaugeFill != null)
        {
            float ratio = Mathf.Clamp01(_oilHoldTimer / OilHoldRequired);
            var fillRt = _oilGaugeFill.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(ratio, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            Color coldColor = new Color(0.45f, 0.85f, 1f, 1f);
            Color warmColor = new Color(1f, 0.82f, 0.22f, 1f);
            _oilGaugeFill.color = Color.Lerp(coldColor, warmColor, ratio);
        }
    }

    void HideOilPromptUI()
    {
        if (_oilUiRoot != null)
        {
            _oilUiRoot.SetActive(false);
            Destroy(_oilUiRoot);
            _oilUiRoot = null;
        }
        _oilGaugeFill = null;
    }

    void FinishClimaxSequence()
    {
        // クリアモーダル表示済みなら二重起動しない。未再生なら再起動可
        if (_showGameClearModal)
        {
            HideScriptBoardCompletely();
            return;
        }

        _climaxBeatIndex = -1;
        _scriptRequireInputRelease = false;
        HideScriptBoardCompletely();
        TeardownScriptBoardUi();
        HideOilPromptUI();

        Time.timeScale = 1f;
        var player = GetPlayer();
        if (player != null)
        {
            player.ApplyGlideBoost(2.0f, 18f);
            if (player.transform.position.y < 155f)
                player.ApplyUpdraft(22f);
        }

        _epilogueTriggered = true;
        _epilogueAct = 1;
        _epilogueAlpha = 1f;
        _climaxCrisisStarted = false;
        AdventureMusicDirector.Ensure();
        AdventureMusicDirector.Instance?.KeepEndingThemeActive();
        Debug.Log("[RustAndFloat] クライマックス完了 → エピローグ開始（Update駆動）");
        AdventureStoryFlow.InvalidateCache();
        BeginEpiloguePlayback();
    }

    #endregion

}
