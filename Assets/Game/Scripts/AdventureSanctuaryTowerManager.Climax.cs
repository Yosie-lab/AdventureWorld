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
        Debug.Log("[RustAndFloat] クライマックス（スクリーンスクリプト）を Update 駆動で開始");
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
        _scriptRequireInputRelease = true; // 各セリフ開始時はキーを一度離すのを待つ（連打・長押し多重スキップ完全防止）
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        EnsureEventSystemForUi();
        // 中央ボードは出さず、スクリーンスクリプト（シネマ字幕）として映画のように表示
        if (_scriptUiRoot != null)
            _scriptUiRoot.SetActive(false);

        string hint = (index >= ClimaxBeats.Length - 1)
            ? "【 Space / クリック 】大空へ！"
            : "【 Space / クリック 】つづき";
        SetScreenScriptSubtitle(beat.Title, beat.Speaker, beat.Body, beat.Accent, false, hint);

        // 台本1（警告）：そばで震え始める
        // 台本2（気流が冷たい）：力なく落ちていく
        if (index == 1)
        {
            var droneFall = GetDrone();
            if (droneFall != null)
                droneFall.BeginClimaxColdFallAway();
        }

        // 台本（ピピッ！……ありがとう、Niko！）：全出力セリフと同時にオーバードライブ演出
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

            SpawnWildernessPanorama(coldCrisis: false);
            SoftenSkybreakColdAtmosphere();
            // 「ありがとう、Niko！」からは風の音を控えめ（0.45f -> 0.18f）に下げてBGMとセリフを引き立てる
            SetSkybreakWindVolume(0.18f, 1.4f);
            StartCoroutine(AdventureSkybreakVisuals.BreakthroughFlashRoutine());
            AdventureMusicDirector.Ensure();
            AdventureMusicDirector.Instance?.TriggerSkybreakOverdriveDrop();
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
        ClearFilmSubtitle();
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

        bool postOilBeat = _climaxBeatIndex >= ClimaxOilSlot;
        float openFor = Time.unscaledTime - _scriptBoardOpenedAt;

        // セリフ表示直後の0.25秒デバウンス（直前の連打による誤スキップ防止）
        const float minHoldOpen = 0.25f;
        if (openFor >= minHoldOpen)
        {
            // 単発押し（Down）でセリフを1つ進める（Space / クリック / E / Enter）
            // ※キーを離していれば押した瞬間に小気味よく即座に進む！
            if (AdventureInputReader.DialogAdvanceDown || AdventureInputReader.MouseLeftDown ||
                AdventureInputReader.SpaceDown || AdventureInputReader.InteractDown || AdventureInputReader.EnterDown)
            {
                _scriptBoardAdvance = true;
                Debug.Log($"[RustAndFloat] クライマックス手動送り入力検知: beat={_climaxBeatIndex}");
            }

            // 何も押さなくても自然に次のセリフへ進む自動送り時間（放置シネマ：5.5〜6.5秒）
            bool finalBeat = _climaxBeatIndex >= ClimaxBeats.Length - 1;
            float autoSec = finalBeat ? 6.5f : (postOilBeat ? 5.5f : 5.0f);

            if (openFor >= autoSec)
            {
                _scriptBoardAdvance = true;
                Debug.Log($"[RustAndFloat] クライマックス自動送り発動: beat={_climaxBeatIndex} (経過={openFor:F1}s)");
            }
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
        Debug.Log($"[RustAndFloat] クライマックス台本進行: beat={next}");
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
        AdventureParticleSanitizer.SanitizeAllParticles();

        _climaxBeatIndex = ClimaxOilSlot;
        _scriptBoardAdvance = false;
        _scriptHoldTimer = 0f;
        _scriptBoardOpenedAt = Time.unscaledTime;
        // 注油ゲージを満たした押しっぱなしが、そのまま台本送りにならないようにする
        _scriptRequireInputRelease = true;
        PresentClimaxBeat(ClimaxOilSlot);
        Debug.Log("[RustAndFloat] 注油完了 → 台本11（蘇生セリフ）");
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
        canvas.sortingOrder = 9600; // レターボックス(9500)より手前に確実に表示
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        canvasGo.AddComponent<GraphicRaycaster>();

        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelGo.AddComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0f); // 画面下部アンカー
        panelRt.pivot = new Vector2(0.5f, 0f);
        panelRt.anchoredPosition = new Vector2(0f, 48f); // 画面下部に配置し、中央〜上部の2人の飛行アクションを完全にクリアに見せる
        panelRt.sizeDelta = new Vector2(760f, 210f);
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0.01f, 0.03f, 0.08f, 0.22f); // 大幅に透明化（22%）：背後のRustの動き・空・稲妻がしっかり透ける
        panelImg.raycastTarget = true;
        var panelBtn = panelGo.AddComponent<Button>();
        panelBtn.targetGraphic = panelImg;
        panelBtn.transition = Selectable.Transition.None;

        // 繊細な氷晶ゴールド枠線（半透明でも境界が綺麗に際立つ）
        var borderOutline = panelGo.AddComponent<Outline>();
        borderOutline.effectColor = new Color(0.75f, 0.88f, 1f, 0.35f);
        borderOutline.effectDistance = new Vector2(1f, -1f);

        // 左側アクセントバー（シースルーに馴染む細ライン）
        var accGo = new GameObject("Accent");
        accGo.transform.SetParent(panelGo.transform, false);
        var accRt = accGo.AddComponent<RectTransform>();
        accRt.anchorMin = new Vector2(0f, 0f);
        accRt.anchorMax = new Vector2(0f, 1f);
        accRt.pivot = new Vector2(0f, 0.5f);
        accRt.sizeDelta = new Vector2(4f, 0f);
        accRt.anchoredPosition = Vector2.zero;
        var accImg = accGo.AddComponent<Image>();
        accImg.color = new Color(1f, 0.85f, 0.35f, 0.75f);
        accImg.raycastTarget = false;

        _oilTitleUi = MakeScriptText(panelGo.transform, "OilTitle", new Vector2(0f, -12f), new Vector2(0.5f, 1f), new Vector2(720f, 32f), 24, TextAnchor.MiddleCenter, font);
        _oilTitleUi.color = new Color(1f, 0.88f, 0.40f, 1f);
        _oilTitleUi.text = SkyLimitWarning;
        PrepareFontForText(font, _oilTitleUi.text, 24, FontStyle.Bold);

        _oilPromptUi = MakeScriptText(panelGo.transform, "OilPrompt", new Vector2(0f, -46f), new Vector2(0.5f, 1f), new Vector2(720f, 54f), 19, TextAnchor.MiddleCenter, font);
        _oilPromptUi.color = new Color(1f, 0.96f, 0.88f, 1f);
        _oilPromptUi.text = "極寒の気流でRustのギアが凍りつく……！\n集めた常備油を心臓部へ注ぎ込め！\n【 E / Space / クリック長押し 】";
        _oilPromptUi.lineSpacing = 1.18f;
        PrepareFontForText(font, _oilPromptUi.text, 19);

        var gaugeBgGo = new GameObject("GaugeBg");
        gaugeBgGo.transform.SetParent(panelGo.transform, false);
        var gaugeBgRt = gaugeBgGo.AddComponent<RectTransform>();
        gaugeBgRt.anchorMin = gaugeBgRt.anchorMax = new Vector2(0.5f, 0f);
        gaugeBgRt.pivot = new Vector2(0.5f, 0f);
        gaugeBgRt.anchoredPosition = new Vector2(0f, 58f);
        gaugeBgRt.sizeDelta = new Vector2(600f, 18f);
        var gaugeBgImg = gaugeBgGo.AddComponent<Image>();
        gaugeBgImg.color = new Color(0.06f, 0.10f, 0.18f, 0.45f); // 半透明背景

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
        holdGo.transform.SetParent(panelGo.transform, false);
        var holdRt = holdGo.AddComponent<RectTransform>();
        holdRt.anchorMin = holdRt.anchorMax = new Vector2(0.5f, 0f);
        holdRt.pivot = new Vector2(0.5f, 0f);
        holdRt.anchoredPosition = new Vector2(0f, 12f);
        holdRt.sizeDelta = new Vector2(600f, 40f);
        var holdImg = holdGo.AddComponent<Image>();
        holdImg.color = new Color(0.95f, 0.78f, 0.25f, 0.82f);
        _oilHoldBtn = holdGo.AddComponent<Button>();
        _oilHoldBtn.targetGraphic = holdImg;
        _oilHoldBtn.transition = Selectable.Transition.None;
        _oilHoldLabelUi = MakeScriptText(holdGo.transform, "HoldLabel", Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(560f, 36f), 19, TextAnchor.MiddleCenter, font);
        _oilHoldLabelUi.color = new Color(0.12f, 0.08f, 0.02f, 1f);
        _oilHoldLabelUi.text = "【長押しで注油】Rustを温める";
        PrepareFontForText(font, _oilHoldLabelUi.text, 19, FontStyle.Bold);

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

            // 進行度に応じて冷たい青白から温かい琥珀・黄金へ
            Color coldColor = new Color(0.45f, 0.85f, 1f, 1f);
            Color warmColor = new Color(1f, 0.82f, 0.22f, 1f);
            _oilGaugeFill.color = Color.Lerp(coldColor, warmColor, ratio);

            // 注油中のパルス脈動演出
            bool holding = IsDiveConfirmHeld();
            if (_oilHoldBtn != null)
            {
                if (holding && ratio > 0.01f)
                {
                    float pulse = 1f + Mathf.Sin(Time.unscaledTime * 14f) * 0.025f;
                    _oilHoldBtn.transform.localScale = new Vector3(pulse, pulse, 1f);
                }
                else
                {
                    _oilHoldBtn.transform.localScale = Vector3.one;
                }
            }
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
        var canvas = GameObject.Find("ClimaxOilPromptCanvas");
        if (canvas != null)
            Destroy(canvas);
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
            if (player.transform.position.y < 118f)
                player.ApplyUpdraft(16f);
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
