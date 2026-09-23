using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 天蓋台本・クライマックス・シネマエピローグ（AdventureSanctuaryTowerManager の partial）
/// </summary>
public partial class AdventureSanctuaryTowerManager
{
    #region 5. 天蓋開放台本ビート制御

    void BeginCanopyScriptBeats()
    {
        _suppressClimax = true;
        _climaxCrisisStarted = false;
        _endingSequenceActive = true;
        _leverPulled = true;
        _scriptHoldTimer = 0f;
        ClearPendingClimax();

        var player = AdventurePlayerController.Instance
                     ?? Object.FindFirstObjectByType<AdventurePlayerController>();
        if (player != null && player.transform.position.y < 90f)
            player.ForceGroundReset();

        AdventureMusicDirector.Ensure();
        // 天蓋崩壊に入ったら壮大な天空突破テーマBGMを開始
        AdventureMusicDirector.Instance?.KeepEndingThemeActive();
        StartSkybreakWindAmbience(); // 風の音は追加で流す

        // 「空が割れた」直後から割れ目の向こうの空を見せる
        SpawnWildernessPanorama(coldCrisis: true);
        ApplySkybreakColdAtmosphere();

        // ピアノが鳴っていたら2秒かけてフェードアウト＆ダッキング解除
        var piano = AdventureAncientPianoRelic.Instance;
        if (piano != null)
            piano.FadeOutPiano(2.0f);
        else
        {
            foreach (var p in Object.FindObjectsByType<AdventureAncientPianoRelic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (p != null) p.FadeOutPiano(2.0f);
        }

        var drone = GetDrone();
        if (drone != null)
        {
            drone.ClearSpeech();
            drone.StartSkybreakNestle();
        }

        _canopyBeatIndex = 0;
        PresentCanopyBeat(0);
        Debug.Log("[RustAndFloat] 天蓋台本を Update 駆動で開始（全" + CanopyBeats.Length + "枚）");
    }

    void PresentCanopyBeat(int index)
    {
        if (index < 0 || index >= CanopyBeats.Length) return;
        var beat = CanopyBeats[index];

        SuppressAllSpeechAndBanners();
        _scriptBoardTitle = beat.Title ?? "";
        _scriptBoardSpeaker = beat.Speaker ?? "";
        _scriptBoardBody = beat.Body ?? "";
        _scriptBoardAccent = beat.Accent;
        _scriptBoardIsDive = beat.IsDive;
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
        {
            _scriptHintUi.text = beat.IsDive
                ? "【Space長押し / クリック】ダイブ！"
                : "【Space長押し / クリック】つづき";
        }

        Debug.Log($"[RustAndFloat] 台本 {index + 1}/{CanopyBeats.Length}: {beat.Title} {beat.Speaker}");
    }

    void TickCanopyScriptBeats()
    {
        // 孤児救済：シークエンス中にボードだけ残ってインデックスが死んでいる場合
        if (_canopyBeatIndex < 0)
        {
            if (_endingSequenceActive && _scriptBoardVisible && !_climaxCrisisStarted && !_epilogueTriggered)
            {
                Debug.LogWarning("[RustAndFloat] 台本インデックス喪失を検出 → Update駆動で再開");
                _canopyBeatIndex = 0;
                PresentCanopyBeat(0);
            }
            return;
        }

        float openFor = Time.unscaledTime - _scriptBoardOpenedAt;

        // 入力
        if (openFor >= 0.35f)
        {
            PollScriptBoardAdvance();

            if (IsDiveConfirmHeld())
            {
                _scriptHoldTimer += Time.unscaledDeltaTime;
                if (_scriptHoldTimer >= 0.08f)
                    _scriptBoardAdvance = true;
            }
            else
            {
                _scriptHoldTimer = 0f;
            }

            // 1枚目7秒／会話・ナレ3.5秒／ダイブ5秒
            float autoSec = _scriptBoardIsDive ? 5f : 3.0f;
            if (_canopyBeatIndex == 0)
                autoSec = 7.0f;
            else if (!_scriptBoardIsDive && _canopyBeatIndex >= 1 && _canopyBeatIndex <= 5)
                autoSec = 3.5f;
            if (openFor >= autoSec)
                _scriptBoardAdvance = true;
        }

        if (!_scriptBoardAdvance) return;

        int next = _canopyBeatIndex + 1;
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
        Debug.Log("[RustAndFloat] 天蓋台本完了 → 光の柱上昇 → クライマックスへ");
        _canopyBeatIndex = -1;
        _scriptBoardAdvance = false;
        _scriptBoardVisible = false;
        _scriptBoardIsDive = false;
        _scriptBoardTitle = "";
        _scriptBoardSpeaker = "";
        _scriptBoardBody = "";
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

        AdventureSaveManager.Instance?.SaveGame("天蓋開放・到達記録を保存しました");

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
            player.BeginSkybreakPillarAscend(new Vector3(512f, 0f, 512f), 36f, 150f);
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

        bool highEnough = player != null
            && (player.transform.position.y >= 140f || player.IsAutoGliding);
        // deadline<=0 は「高さ待ちのみ」。0を即タイムアウト扱いすると地上リセット後にエンディングが再点火する
        bool timedOut = _pendingClimaxDeadline > 0f
                        && Time.unscaledTime >= _pendingClimaxDeadline;
        if (!highEnough && !timedOut) return;

        ClearPendingClimax();
        _suppressClimax = false;
        _ignoreSavedCanopyState = false;
        if (!_isCanopyBroken)
            IsCanopyBroken = true;

        // タイムアウト時のみ高度を強制確保（通常は上昇演出の到達を活かす）
        if (player != null && player.transform.position.y < 140f)
            player.ForceSkybreakArrival(new Vector3(512f, 150f, 512f), 150f);

        Debug.Log($"[RustAndFloat] クライマックス開始 high={highEnough} timeout={timedOut}");
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

    /// <summary>天蓋破壊ボード（旧コルーチン版は未使用・互換のため残置）</summary>
    IEnumerator SkybreakFromTitleBoardRoutine()
    {
        BeginCanopyScriptBeats();
        while (_canopyBeatIndex >= 0)
            yield return null;
    }

    void SuppressAllSpeechAndBanners()
    {
        var drone = GetDrone();
        if (drone != null)
            drone.ClearSpeech();
        if (AdventureScrapHUD.Instance != null)
            AdventureScrapHUD.Instance.HideBannerImmediately();
    }

    /// <summary>台本を1枚のボードで表示し、進む入力まで待つ（吹き出しと重ねない）</summary>
    /// <param name="autoAdvanceOverride">0より大きいとき、通常の自動送り秒数の代わりに使う</param>
    IEnumerator ShowScriptBeat(string title, string speaker, string body, Color accent, bool isDive = false, float autoAdvanceOverride = -1f)
    {
        SuppressAllSpeechAndBanners();

        var player = AdventurePlayerController.Instance;
        if (player != null
            && player.transform.position.y < 90f
            && !player.IsSkybreakPillarAscending
            && !player.IsAutoGliding)
            player.ForceGroundReset();

        _scriptBoardTitle = title ?? "";
        _scriptBoardSpeaker = speaker ?? "";
        _scriptBoardBody = body ?? "";
        _scriptBoardAccent = accent;
        _scriptBoardIsDive = isDive;
        _scriptBoardAdvance = false;
        _scriptBoardVisible = true;
        _scriptBoardOpenedAt = Time.unscaledTime;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        EnsureEventSystemForUi();
        EnsureScriptBoardUI();
        ApplyScriptBoardUI();

        if (_scriptBtn != null)
        {
            var btnRt = _scriptBtn.GetComponent<RectTransform>();
            if (btnRt != null)
            {
                btnRt.sizeDelta = isDive ? new Vector2(560f, 72f) : new Vector2(520f, 56f);
                btnRt.anchoredPosition = new Vector2(0f, 16f);
            }
        }
        if (_scriptHintUi != null)
        {
            _scriptHintUi.fontSize = isDive ? 22 : 18;
            _scriptHintUi.text = isDive
                ? "【Space長押し / クリック】ダイブ！"
                : "【Space長押し / クリック】つづき";
        }

        // 最低表示
        const float minShow = 0.5f;
        while (Time.unscaledTime - _scriptBoardOpenedAt < minShow)
            yield return null;

        float holdTimer = 0f;
        float autoAfter = isDive ? 8f : 3.5f;
        if (autoAdvanceOverride > 0f)
            autoAfter = autoAdvanceOverride;
        while (!_scriptBoardAdvance)
        {
            PollScriptBoardAdvance();

            // 全台本：Space/クリック押しっぱなしで進む
            if (IsDiveConfirmHeld())
            {
                holdTimer += Time.unscaledDeltaTime;
                if (holdTimer >= 0.12f)
                    _scriptBoardAdvance = true;
            }
            else
            {
                holdTimer = 0f;
            }

            if (Time.unscaledTime - _scriptBoardOpenedAt > autoAfter)
                _scriptBoardAdvance = true;

            yield return null;
        }

        _scriptBoardAdvance = false;
        _scriptBoardVisible = false;
        _scriptBoardIsDive = false;
        _scriptBoardTitle = "";
        _scriptBoardSpeaker = "";
        _scriptBoardBody = "";
        if (_scriptUiRoot != null)
            _scriptUiRoot.SetActive(false);
        yield return null;
    }

    /// <summary>プレイヤー／UIから台本送りを直接要求</summary>
    public void NotifyScriptBoardAdvance()
    {
        if (!_scriptBoardVisible) return;
        if (_scriptRequireInputRelease) return;
        // 注油直後の最初のセリフだけ長めに守る。最終「全力で行くよ」はすぐ送れるようにする
        float minShow = 0.45f;
        if (_climaxCrisisStarted && _climaxBeatIndex == ClimaxOilSlot)
            minShow = 2.2f;
        else if (_climaxCrisisStarted && _climaxBeatIndex > ClimaxOilSlot)
            minShow = 0.85f;
        if (Time.unscaledTime - _scriptBoardOpenedAt < minShow) return;
        _scriptBoardAdvance = true;
        Debug.Log("[RustAndFloat] 台本送り入力を受け付けました");
    }

    static bool IsDiveConfirmHeld()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && (kb.spaceKey.isPressed || kb.enterKey.isPressed || kb.eKey.isPressed))
            return true;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed))
            return true;
        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (pad != null && (pad.buttonSouth.isPressed || pad.buttonWest.isPressed))
            return true;
        try
        {
            if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.E))
                return true;
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
                return true;
        }
        catch { }
        return false;
    }

    #endregion

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

    #region 10. エピローグ演出 & オートグライド

    struct FilmLine
    {
        public string Text;
        public float Hold;
        public int Act;
        public Color Color;
        public FilmLine(string text, float hold, int act, Color color)
        {
            Text = text;
            Hold = hold;
            Act = act;
            Color = color;
        }
    }

    // 関門5：映画字幕は1行ずつ・3幕（長い一括表示にしない）
    static readonly FilmLine[] EpilogueFilmLines =
    {
        new FilmLine("わぁぁ……！見て、Niko！世界はこんなに広かったんだ……！！", 4.2f, 0,
            new Color(0.55f, 0.95f, 1f, 1f)),
        new FilmLine("空が割れた。", 3.4f, 1, new Color(1f, 0.94f, 0.72f, 1f)),
        new FilmLine("箱庭の外に、凍えるほどリアルな風が吹いていた。", 4.6f, 1, new Color(1f, 0.94f, 0.72f, 1f)),
        new FilmLine("人は、最適で最短な道を進むときじゃなく、", 4.2f, 2, new Color(1f, 0.96f, 0.82f, 1f)),
        new FilmLine("寄り道をして、躓きながらも出会えた感動に", 4.4f, 2, new Color(1f, 0.96f, 0.82f, 1f)),
        new FilmLine("生きてる証を、得るんだ。", 4.0f, 2, new Color(1f, 0.96f, 0.82f, 1f)),
        new FilmLine("傷つくかもしれない自由と、", 3.2f, 3, new Color(1f, 0.92f, 0.55f, 1f)),
        new FilmLine("命の重みを取り戻した二人の旅が、", 3.2f, 3, new Color(1f, 0.92f, 0.55f, 1f)),
        new FilmLine("また始まる。—— 『Rust & Float』", 3.2f, 3, new Color(1f, 0.88f, 0.45f, 1f)),
    };

    Font ResolveEpilogueFont()
    {
        if (_epilogueFont != null)
            return _epilogueFont;
        _epilogueFont = CreateJapaneseFont(32);
        return _epilogueFont;
    }

    void BeginEpiloguePlayback()
    {
        if (_epilogueRoutine != null)
        {
            StopCoroutine(_epilogueRoutine);
            _epilogueRoutine = null;
        }

        var player = GetPlayer();
        if (player != null)
        {
            if (player.transform.position.y < 110f)
                player.ApplyLaunchUpdraft(18f, 16f);
            player.SetAutoGlideMode(true, 120f);
            player.ApplyGlideBoost(1.2f, 8f);
        }

        SetCinematicCamera(true);
        SetExplorationHudVisible(false);
        try
        {
            SpawnWildernessPanorama(coldCrisis: false);
            SoftenSkybreakColdAtmosphere();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[RustAndFloat] エピローグ背景演出: " + e.Message);
        }
        SuppressAllSpeechAndBanners();
        HideScriptBoardCompletely();
        TeardownScriptBoardUi();
        HideOilPromptUI();

        var flash = GameObject.Find("BreakthroughFlashCanvas");
        if (flash != null)
            Destroy(flash);

        RebuildCinematicLetterbox();
        if (_letterboxRoot != null)
        {
            var canvas = _letterboxRoot.GetComponent<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = 9500; // フラッシュより前面
            _letterboxRoot.SetActive(true);
        }
        if (_letterboxCg != null)
            _letterboxCg.alpha = 1f;
        _letterboxTargetAlpha = 1f;

        _epilogueStartedAt = Time.unscaledTime;
        _filmLastAct = -1;
        _filmIndex = 0;
        PrepareFilmLine(0, allowActGap: false);
        Debug.Log($"[RustAndFloat] シネマエピローグ開始（Update） lines={EpilogueFilmLines.Length} subtitle={(_filmSubtitleUi != null)}");
    }

    void PrepareFilmLine(int index, bool allowActGap)
    {
        if (index < 0 || index >= EpilogueFilmLines.Length)
        {
            FinishEpilogueFilm();
            return;
        }

        var line = EpilogueFilmLines[index];
        _filmIndex = index;
        // Hold＝放置の総時間（フェードイン＋保持＋フェードアウト）
        _filmFadeInSec = 0.35f;
        _filmFadeOutSec = 0.4f;
        float total = Mathf.Max(1.2f, line.Hold);
        _filmHoldSec = Mathf.Max(0.5f, total - _filmFadeInSec - _filmFadeOutSec);
        _filmColor = line.Color;

        if (allowActGap && line.Act != _filmLastAct && line.Act >= 1 && _filmLastAct >= 0)
        {
            ClearFilmSubtitle();
            _epilogueAct = line.Act;
            _filmPhase = 4;
            _filmPhaseAt = Time.unscaledTime;
            return;
        }

        _filmLastAct = line.Act;
        _epilogueAct = Mathf.Max(1, line.Act);
        PresentFilmLineContent(line.Text, line.Color);
        _filmPhase = 1;
        _filmPhaseAt = Time.unscaledTime;
    }

    void PresentFilmLineContent(string text, Color color)
    {
        EnsureCinematicLetterbox();
        if (_filmSubtitleUi == null)
            RebuildCinematicLetterbox();
        if (_letterboxRoot != null && !_letterboxRoot.activeSelf)
            _letterboxRoot.SetActive(true);
        if (_letterboxCg != null)
            _letterboxCg.alpha = 1f;
        _letterboxTargetAlpha = 1f;

        if (_filmSubtitleUi == null)
        {
            Debug.LogError("[RustAndFloat] FilmSubtitle UI 生成失敗");
            return;
        }
        _filmSubtitleUi.text = text ?? "";
        _filmSubtitleUi.color = new Color(color.r, color.g, color.b, 0f);
        _epilogueAlpha = 0f;
    }

    void TickEpilogueFilm()
    {
        if (!_epilogueTriggered || _showGameClearModal)
            return;
        if (_filmIndex < 0 || _filmPhase <= 0)
            return;

        var player = GetPlayer();
        KeepAutoGlide(player);
        SetExplorationHudVisible(false);
        SuppressAllSpeechAndBanners();
        _letterboxTargetAlpha = 1f;
        if (_letterboxCg != null)
            _letterboxCg.alpha = 1f;
        if (_letterboxRoot != null && !_letterboxRoot.activeSelf)
            _letterboxRoot.SetActive(true);

        float elapsed = Time.unscaledTime - _filmPhaseAt;

        // 幕あい
        if (_filmPhase == 4)
        {
            if (elapsed >= 1.15f)
            {
                var line = EpilogueFilmLines[_filmIndex];
                _filmLastAct = line.Act;
                PresentFilmLineContent(line.Text, line.Color);
                _filmPhase = 1;
                _filmPhaseAt = Time.unscaledTime;
            }
            return;
        }

        if (_filmSubtitleUi == null)
        {
            // UI欠落時は時間だけ進めて次へ
            if (elapsed >= _filmHoldSec)
                AdvanceFilmLine();
            return;
        }

        Color c = _filmColor;
        if (_filmPhase == 1)
        {
            float a = Mathf.Clamp01(elapsed / _filmFadeInSec);
            _filmSubtitleUi.color = new Color(c.r, c.g, c.b, a);
            _epilogueAlpha = a;
            if (elapsed >= _filmFadeInSec)
            {
                _filmSubtitleUi.color = c;
                _filmPhase = 2;
                _filmPhaseAt = Time.unscaledTime;
            }
        }
        else if (_filmPhase == 2)
        {
            _filmSubtitleUi.color = c;
            _epilogueAlpha = 1f;
            bool canSkip = elapsed >= 1.0f && (Time.unscaledTime - _epilogueStartedAt) >= 2.0f;
            if ((canSkip && WasFilmSkipPressed()) || elapsed >= _filmHoldSec)
            {
                _filmPhase = 3;
                _filmPhaseAt = Time.unscaledTime;
            }
        }
        else if (_filmPhase == 3)
        {
            float a = 1f - Mathf.Clamp01(elapsed / _filmFadeOutSec);
            _filmSubtitleUi.color = new Color(c.r, c.g, c.b, a);
            _epilogueAlpha = a;
            if (elapsed >= _filmFadeOutSec)
                AdvanceFilmLine();
        }
    }

    void AdvanceFilmLine()
    {
        int next = _filmIndex + 1;
        if (next >= EpilogueFilmLines.Length)
        {
            FinishEpilogueFilm();
            return;
        }
        PrepareFilmLine(next, allowActGap: true);
    }

    void FinishEpilogueFilm()
    {
        ClearFilmSubtitle();
        _filmIndex = -1;
        _filmPhase = 0;
        KeepAutoGlide(GetPlayer());
        IsGameCleared = true;
        _showGameClearModal = true;
        _epilogueAlpha = 0f;
        _letterboxTargetAlpha = 0f;
        SuppressAllSpeechAndBanners();
        AdventureMusicDirector.Ensure();
        AdventureMusicDirector.Instance?.KeepEndingThemeActive();
        Debug.Log("[RustAndFloat] シネマエピローグ完了 → クリアモーダル");
    }

    IEnumerator EpilogueSequenceRoutine()
    {
        // 互換用：Update駆動へ委譲
        BeginEpiloguePlayback();
        yield break;
    }

    IEnumerator ShowFilmSubtitleRoutine(string line, float holdSec, Color color)
    {
        yield break;
    }

    void ClearFilmSubtitle()
    {
        if (_filmSubtitleUi != null)
        {
            _filmSubtitleUi.text = "";
            var c = _filmSubtitleUi.color;
            c.a = 0f;
            _filmSubtitleUi.color = c;
        }
        _epilogueAlpha = 0f;
    }

    static bool WasFilmSkipPressed()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            return true;
        try
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                return true;
        }
        catch { }
        return false;
    }

    static void KeepAutoGlide(AdventurePlayerController player)
    {
        if (player != null && !player.IsAutoGliding)
            player.SetAutoGlideMode(true, 120f);
    }

    void SetExplorationHudVisible(bool visible)
    {
        if (visible)
            AdventureCompassHUD.Ensure();

        var compass = AdventureCompassHUD.Instance
                      ?? Object.FindFirstObjectByType<AdventureCompassHUD>(FindObjectsInactive.Include);
        if (compass != null)
            compass.gameObject.SetActive(visible);

        var scrap = AdventureScrapHUD.Instance
                    ?? Object.FindFirstObjectByType<AdventureScrapHUD>(FindObjectsInactive.Include);
        if (scrap != null)
            scrap.gameObject.SetActive(visible);

        if (_cachedGuideText == null)
        {
            foreach (var t in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include))
            {
                if (t != null && t.name == "Guide")
                {
                    _cachedGuideText = t;
                    break;
                }
            }
        }
        if (_cachedGuideText != null)
            _cachedGuideText.gameObject.SetActive(visible);
    }

    void TickCinematicLetterbox()
    {
        bool filmPlaying = _epilogueTriggered && !_showGameClearModal && _filmIndex >= 0 && _filmPhase > 0;
        bool want = filmPlaying || AdventureStoryFlow.IsPerformance;
        _letterboxTargetAlpha = want ? 1f : 0f;
        if (filmPlaying)
            _letterboxTargetAlpha = 1f;

        if (want)
        {
            SetExplorationHudVisible(false);
            EnsureCinematicLetterbox();
            if (_letterboxRoot != null && !_letterboxRoot.activeSelf)
                _letterboxRoot.SetActive(true);
            if (filmPlaying && _letterboxCg != null)
                _letterboxCg.alpha = 1f;
        }

        if (_letterboxCg != null)
        {
            float a = filmPlaying
                ? 1f
                : Mathf.MoveTowards(_letterboxCg.alpha, _letterboxTargetAlpha, Time.unscaledDeltaTime * 2.4f);
            _letterboxCg.alpha = a;
            if (_letterboxRoot != null)
            {
                bool show = a > 0.02f || _letterboxTargetAlpha > 0.02f || filmPlaying;
                if (_letterboxRoot.activeSelf != show)
                    _letterboxRoot.SetActive(show);
            }
        }
        else if (!want && _letterboxRoot != null && _letterboxRoot.activeSelf)
        {
            _letterboxRoot.SetActive(false);
        }
    }

    void RebuildCinematicLetterbox()
    {
        if (_letterboxRoot != null)
        {
            Destroy(_letterboxRoot);
            _letterboxRoot = null;
        }
        _letterboxCg = null;
        _letterboxTop = null;
        _letterboxBottom = null;
        _filmSubtitleUi = null;
        EnsureCinematicLetterbox();
    }

    void EnsureCinematicLetterbox()
    {
        if (_letterboxRoot != null)
        {
            if (_filmSubtitleUi == null)
                BuildFilmSubtitleOnLetterbox();
            return;
        }
        EnsureEventSystemForUi();

        var canvasGo = new GameObject("CinematicLetterboxCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9500;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        _letterboxCg = canvasGo.AddComponent<CanvasGroup>();
        _letterboxCg.alpha = 0f;
        _letterboxCg.blocksRaycasts = false;
        _letterboxCg.interactable = false;

        _letterboxTop = MakeLetterbar(canvasGo.transform, "Top", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 64f);
        _letterboxBottom = MakeLetterbar(canvasGo.transform, "Bottom", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), 118f);
        _letterboxRoot = canvasGo;
        BuildFilmSubtitleOnLetterbox();
    }

    void BuildFilmSubtitleOnLetterbox()
    {
        if (_letterboxRoot == null || _filmSubtitleUi != null) return;
        Font font = ResolveEpilogueFont();
        var go = new GameObject("FilmSubtitle");
        go.transform.SetParent(_letterboxRoot.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.08f, 0f);
        rt.anchorMax = new Vector2(0.92f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 36f);
        rt.sizeDelta = new Vector2(0f, 72f);
        _filmSubtitleUi = go.AddComponent<Text>();
        _filmSubtitleUi.font = font;
        _filmSubtitleUi.fontSize = 30;
        _filmSubtitleUi.alignment = TextAnchor.MiddleCenter;
        _filmSubtitleUi.color = new Color(1f, 0.94f, 0.78f, 0f);
        _filmSubtitleUi.horizontalOverflow = HorizontalWrapMode.Wrap;
        _filmSubtitleUi.verticalOverflow = VerticalWrapMode.Overflow;
        _filmSubtitleUi.raycastTarget = false;
        if (_filmSubtitleUi.font == null)
            _filmSubtitleUi.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    static Image MakeLetterbar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, anchorMin.y);
        rt.anchorMax = new Vector2(1f, anchorMax.y);
        rt.pivot = new Vector2(0.5f, anchorMin.y);
        rt.sizeDelta = new Vector2(0f, height);
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.94f);
        img.raycastTarget = false;
        return img;
    }

    static void SetCinematicCamera(bool enabled)
    {
        var cam = Camera.main;
        if (cam == null)
            cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null)
            return;
        var follow = cam.GetComponent<AdventureCameraFollow>();
        if (follow != null)
            follow.SetCinematicMode(enabled);
    }

    #endregion
}
