using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 天蓋開放台本ビート（Opening後のレバー〜光柱上昇まで）
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
        _skyTearPlayed = false;
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

        // 「空が……割れるよ」で天空裂開シーン
        if (index == 2 && !_skyTearPlayed)
        {
            _skyTearPlayed = true;
            if (GameObject.Find("SkybreakEffect") == null)
                SpawnSkybreakCracks(new Vector3(512f, 150f, 512f));
            StartCoroutine(AdventureSkybreakVisuals.PlaySkyTearOpenRoutine());
            Debug.Log("[RustAndFloat] 天空裂開シーン開始（7.0秒演出）");
        }
        else if (index >= 3 && index <= 5)
        {
            // 後続セリフ（ありがとうRust〜光の柱へ）中も空の裂け目からパルス・閃光を走らせる
            if (index == 4)
            {
                // 「あれが本物の空だ……！」：セリフ中ずっと3回連続で激しく持続（3.2秒間）
                StartCoroutine(AdventureSkybreakVisuals.PlaySkyTearMiniPulseRoutine(shakeIntensity: 0.35f, pulses: 3, totalDuration: 3.2f));
            }
            else if (index == 5)
            {
                // 「タワー中央の光の柱へ…」：2回持続パルス（2.2秒間）
                StartCoroutine(AdventureSkybreakVisuals.PlaySkyTearMiniPulseRoutine(shakeIntensity: 0.30f, pulses: 2, totalDuration: 2.2f));
            }
            else
            {
                // 「ありがとうRust…！」：1回パルス
                StartCoroutine(AdventureSkybreakVisuals.PlaySkyTearMiniPulseRoutine(shakeIntensity: 0.22f, pulses: 1, totalDuration: 0.45f));
            }
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

            // 1枚目7秒／「空が割れるよ」7秒／会話3.8秒／ナレ3.5秒／ダイブ5秒
            float autoSec = _scriptBoardIsDive ? 5f : 3.0f;
            if (_canopyBeatIndex == 0)
                autoSec = 7.0f;
            else if (_canopyBeatIndex == 2)
                autoSec = 7.0f;
            else if (_canopyBeatIndex == 3 || _canopyBeatIndex == 4)
                autoSec = 3.8f;
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

}
