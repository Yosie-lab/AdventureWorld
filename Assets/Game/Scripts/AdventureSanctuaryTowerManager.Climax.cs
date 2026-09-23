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
        _suppressClimax = false;
        _ignoreSavedCanopyState = false;

        var drone = GetDrone();
        var player = GetPlayer();

        SetCinematicCamera(true);
        SetExplorationHudVisible(false);
        SpawnWildernessPanorama(coldCrisis: true);
        ApplySkybreakColdAtmosphere();

        if (player != null)
        {
            // 警告台本時点で必ず空中にいる
            if (player.transform.position.y < 140f)
                player.ForceSkybreakArrival(new Vector3(512f, 150f, 512f), 150f);
            else
            {
                player.EndSkybreakPillarAscend();
                player.SetAutoGlideMode(true, Mathf.Clamp(player.transform.position.y, 140f, 160f));
            }
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
        Debug.Log("[RustAndFloat] クライマックスを Update 駆動で開始");
    }

    void PresentClimaxBeat(int index)
    {
        if (index < 0 || index >= ClimaxBeats.Length) return;
        var beat = ClimaxBeats[index];
        SuppressAllSpeechAndBanners();
        _scriptBoardTitle = beat.Title ?? "";
        _scriptBoardSpeaker = beat.Speaker ?? "";
        _scriptBoardBody = beat.Body ?? "";
        _scriptBoardAccent = beat.Accent;
        _scriptBoardIsDive = false;
        _scriptBoardAdvance = false;
        _scriptBoardVisible = true;
        _scriptBoardOpenedAt = Time.unscaledTime;
        _scriptHoldTimer = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;
        EnsureEventSystemForUi();
        EnsureScriptBoardUI();
        ApplyScriptBoardUI();
        if (_scriptHintUi != null)
            _scriptHintUi.text = "【Space長押し / クリック】つづき";

        // 台本1（警告）：そばで震え始める
        // 台本2（気流が冷たい）：力なく落ちていく
        if (index == 1)
        {
            var droneFall = GetDrone();
            if (droneFall != null)
                droneFall.BeginClimaxColdFallAway();
        }

        // 台本12：全出力セリフと同時にオーバードライブ演出
        if (index == ClimaxOilSlot + 1)
        {
            var drone = GetDrone();
            if (drone != null)
                drone.TriggerClimaxOverdrive();
            SpawnWildernessPanorama(coldCrisis: false);
            SoftenSkybreakColdAtmosphere();
            StartCoroutine(AdventureSkybreakVisuals.BreakthroughFlashRoutine());
            // 押しっぱなしで即スキップされないよう、一瞬だけ離し待ち
            _scriptRequireInputRelease = true;
            _scriptHoldTimer = 0f;
            if (_scriptHintUi != null)
                _scriptHintUi.text = "【Space / クリック】大空へ";
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

        // 最終セリフ後に台本が消えた／進まない場合でもエピローグへ強制遷移
        if (_climaxOilInjected
            && _climaxBeatIndex >= ClimaxBeats.Length - 1
            && !_epilogueTriggered)
        {
            float finalOpen = Time.unscaledTime - _scriptBoardOpenedAt;
            if (finalOpen >= 4.5f || (!_scriptBoardVisible && finalOpen >= 0.35f))
            {
                Debug.LogWarning("[RustAndFloat] 最終セリフ詰まり検知 → エピローグ強制開始");
                FinishClimaxSequence();
                return;
            }
        }

        if (_climaxBeatIndex < 0 || !_scriptBoardVisible)
            return;

        bool postOilBeat = _climaxBeatIndex >= ClimaxOilSlot;
        float openFor = Time.unscaledTime - _scriptBoardOpenedAt;

        // 注油完了直後：Space/E が押されたままだと「温かい油」が即スキップされる
        // ※押しっぱなし中は手動送りだけ抑止。自動送り／最終保険は必ず通す（エピローグ詰まり防止）
        if (_scriptRequireInputRelease)
        {
            if (!IsDiveConfirmHeld())
            {
                _scriptRequireInputRelease = false;
                _scriptHoldTimer = 0f;
            }
            else
            {
                _scriptHoldTimer = 0f;
                bool finalWhileHeld = _climaxBeatIndex >= ClimaxBeats.Length - 1;
                float heldOpen = openFor;
                float heldAuto = finalWhileHeld
                    ? 4.2f
                    : GetScriptBeatAutoAdvanceSeconds(_scriptBoardBody, postOilBeat);
                if (heldOpen >= heldAuto || (finalWhileHeld && heldOpen >= 5.5f))
                {
                    _scriptRequireInputRelease = false;
                    _scriptBoardAdvance = true;
                }
                else
                    return;
            }
        }

        // 注油後の最初のセリフは最低2.2秒見せる。最終「全力」は0.85秒で送り可
        float minHoldOpen = 0.35f;
        if (postOilBeat && _climaxBeatIndex == ClimaxOilSlot)
            minHoldOpen = 2.2f;
        else if (postOilBeat && _climaxBeatIndex > ClimaxOilSlot)
            minHoldOpen = 0.85f;
        if (openFor >= minHoldOpen)
        {
            PollScriptBoardAdvance();
            if (IsDiveConfirmHeld())
            {
                _scriptHoldTimer += Time.unscaledDeltaTime;
                if (_scriptHoldTimer >= 0.18f)
                    _scriptBoardAdvance = true;
            }
            else _scriptHoldTimer = 0f;

            bool finalBeat = _climaxBeatIndex >= ClimaxBeats.Length - 1;
            float autoSec = finalBeat
                ? 4.2f
                : GetScriptBeatAutoAdvanceSeconds(_scriptBoardBody, postOilBeat);
            if (openFor >= autoSec)
                _scriptBoardAdvance = true;

            // 最終セリフ：入力が取れなくても必ずエピローグへ（保険）
            if (finalBeat && openFor >= 5.5f)
                _scriptBoardAdvance = true;
        }

        if (!_scriptBoardAdvance) return;

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
        PresentClimaxBeat(next);
    }

    static float GetScriptBeatAutoAdvanceSeconds(string body, bool postOil)
    {
        int len = string.IsNullOrEmpty(body) ? 0 : body.Length;
        if (postOil)
            return Mathf.Clamp(8f + len * 0.14f, 9f, 20f);
        return Mathf.Clamp(3.4f + len * 0.07f, 3f, 12f);
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

        var orphanBoard = GameObject.Find("EndingScriptBoardCanvas");
        if (orphanBoard != null)
            orphanBoard.SetActive(false);

        if (!IsClimaxOilPromptActive)
        {
            var oilCanvas = GameObject.Find("ClimaxOilPromptCanvas");
            if (oilCanvas != null)
                oilCanvas.SetActive(false);
        }
    }

    void TickClimaxOilHold()
    {
        if (!_climaxOilWaiting || _climaxOilInjected) return;

        bool holding = IsDiveConfirmHeld();
        if (holding)
            _oilHoldTimer += Time.unscaledDeltaTime;
        else
            _oilHoldTimer = Mathf.Max(0f, _oilHoldTimer - Time.unscaledDeltaTime * 1.1f);

        if (Time.unscaledTime - _oilWaitOpenedAt >= 8f)
            _oilHoldTimer = OilHoldRequired;

        RefreshOilPromptUI();

        if (_oilHoldTimer >= OilHoldRequired)
            CompleteClimaxOil();
    }

    /// <summary>外部／プレイヤーから注油ホールドを加算</summary>
    public void NotifyOilHold(float dt)
    {
        if (!IsClimaxOilPromptActive) return;
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

        // 注油後：極寒の気配を少し緩め、解放の金色へ寄せる
        SoftenSkybreakColdAtmosphere();

        _climaxBeatIndex = ClimaxOilSlot;
        _climaxPostOilPhase = 0;
        _climaxPostOilUntil = 0f;
        _scriptBoardAdvance = false;
        _scriptHoldTimer = 0f;
        // 注油ゲージを満たした押しっぱなしが、そのまま台本送りにならないようにする
        _scriptRequireInputRelease = true;
        PresentClimaxBeat(ClimaxOilSlot);
        Debug.Log("[RustAndFloat] 注油完了 → 台本11（蘇生セリフ）");
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

        Font font = ResolveUiFont();
        var canvasGo = new GameObject("ClimaxOilPromptCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5200;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        canvasGo.AddComponent<GraphicRaycaster>();

        // 上下レターボックス
        CreateOilBar(canvasGo.transform, true);
        CreateOilBar(canvasGo.transform, false);

        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelGo.AddComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(860f, 320f);
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0.02f, 0.05f, 0.10f, 0.96f);
        panelImg.raycastTarget = true;
        var panelBtn = panelGo.AddComponent<Button>();
        panelBtn.targetGraphic = panelImg;
        panelBtn.transition = Selectable.Transition.None;
        // 押し続け判定は Update 側。ここでは見た目用

        _oilTitleUi = MakeScriptText(panelGo.transform, "OilTitle", new Vector2(0f, -24f), new Vector2(0.5f, 1f), new Vector2(800f, 40f), 30, TextAnchor.MiddleCenter, font);
        _oilTitleUi.color = new Color(1f, 0.55f, 0.45f, 1f);
        _oilTitleUi.text = SkyLimitWarning;
        PrepareFontForText(font, _oilTitleUi.text, 30, FontStyle.Bold);

        _oilPromptUi = MakeScriptText(panelGo.transform, "OilPrompt", new Vector2(0f, 10f), new Vector2(0.5f, 0.5f), new Vector2(780f, 140f), 26, TextAnchor.MiddleCenter, font);
        _oilPromptUi.color = new Color(1f, 0.92f, 0.4f, 1f);
        _oilPromptUi.text = "✦ エネルギー注入 ✦\n【E / Space / クリック長押し】\nゲージを満タンにして油をさす";
        PrepareFontForText(font, _oilPromptUi.text, 26);

        var gaugeBgGo = new GameObject("GaugeBg");
        gaugeBgGo.transform.SetParent(panelGo.transform, false);
        var gaugeBgRt = gaugeBgGo.AddComponent<RectTransform>();
        gaugeBgRt.anchorMin = gaugeBgRt.anchorMax = new Vector2(0.5f, 0f);
        gaugeBgRt.anchoredPosition = new Vector2(0f, 36f);
        gaugeBgRt.sizeDelta = new Vector2(640f, 28f);
        var gaugeBgImg = gaugeBgGo.AddComponent<Image>();
        gaugeBgImg.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);

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

        var holdGo = new GameObject("HoldBtn");
        holdGo.transform.SetParent(canvasGo.transform, false);
        var holdRt = holdGo.AddComponent<RectTransform>();
        holdRt.anchorMin = holdRt.anchorMax = new Vector2(0.5f, 0f);
        holdRt.anchoredPosition = new Vector2(0f, 88f);
        holdRt.sizeDelta = new Vector2(640f, 64f);
        var holdImg = holdGo.AddComponent<Image>();
        holdImg.color = new Color(0.95f, 0.75f, 0.2f, 0.95f);
        _oilHoldBtn = holdGo.AddComponent<Button>();
        _oilHoldBtn.targetGraphic = holdImg;
        _oilHoldBtn.transition = Selectable.Transition.None;
        _oilHoldLabelUi = MakeScriptText(holdGo.transform, "HoldLabel", Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(600f, 56f), 26, TextAnchor.MiddleCenter, font);
        _oilHoldLabelUi.color = new Color(0.12f, 0.08f, 0.02f, 1f);
        _oilHoldLabelUi.text = "【押し続け】Rustに油をさす";
        PrepareFontForText(font, _oilHoldLabelUi.text, 26, FontStyle.Bold);

        _oilUiRoot = canvasGo;
        RefreshOilPromptUI();
    }

    static void CreateOilBar(Transform parent, bool top)
    {
        var go = new GameObject(top ? "LetterTop" : "LetterBottom");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        if (top)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
        }
        else
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
        }
        rt.sizeDelta = new Vector2(0f, 90f);
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.02f, 0.04f, 0.08f, 0.92f);
        img.raycastTarget = false;
    }

    void RefreshOilPromptUI()
    {
        if (_oilUiRoot == null) return;
        _oilUiRoot.SetActive(IsClimaxOilPromptActive);
        if (_oilGaugeFill != null)
        {
            float ratio = Mathf.Clamp01(_oilHoldTimer / OilHoldRequired);
            var parent = _oilGaugeFill.transform.parent as RectTransform;
            float w = parent != null ? parent.sizeDelta.x : 640f;
            _oilGaugeFill.rectTransform.sizeDelta = new Vector2(w * ratio, 0f);
        }
    }

    void HideOilPromptUI()
    {
        if (_oilUiRoot != null)
            _oilUiRoot.SetActive(false);
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
        _climaxOverdriveCinematicUntil = 0f;
        _climaxPostOilPhase = 0;
        _climaxPostOilUntil = 0f;
        _scriptRequireInputRelease = false;
        HideScriptBoardCompletely();
        TeardownScriptBoardUi();
        HideOilPromptUI();

        Time.timeScale = 1f;
        var player = GetPlayer();
        if (player != null)
        {
            player.SetAutoGlideMode(false);
            player.ApplyGlideBoost(3.2f, 75f);
            player.ApplyUpdraft(28f);
        }

        _epilogueTriggered = true;
        _epilogueAct = 1;
        _epilogueAlpha = 1f;
        _climaxCrisisStarted = false;
        AdventureMusicDirector.Ensure();
        AdventureMusicDirector.Instance?.KeepEndingThemeActive();
        Debug.Log("[RustAndFloat] クライマックス完了 → エピローグ開始（Update駆動）");
        BeginEpiloguePlayback();
    }

    #endregion

}
