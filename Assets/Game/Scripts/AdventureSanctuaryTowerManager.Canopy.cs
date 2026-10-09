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
            mainText = "<size=30><color=#FFD700><b>【 Space / クリック長押し 】</b></color> 空の裂け目へダイブ！</size>";
        }
        else if (!string.IsNullOrEmpty(speaker))
        {
            string hex = ColorUtility.ToHtmlStringRGBA(accent);
            mainText = $"<color=#{hex}><b>{speaker}</b></color> 「{body}」";
        }
        else if (!string.IsNullOrEmpty(title))
        {
            string hex = ColorUtility.ToHtmlStringRGBA(accent);
            mainText = $"<color=#{hex}><b>✦ {title} ✦</b></color>\n{body}";
        }
        else
        {
            string hex = ColorUtility.ToHtmlStringRGBA(accent);
            mainText = $"<color=#{hex}>{body}</color>";
        }

        if (!string.IsNullOrEmpty(hint))
        {
            mainText += $"\n<size=19><color=#FFE073><b>{hint}</b></color></size>";
        }

        _filmSubtitleUi.text = mainText;
        _filmSubtitleUi.color = Color.white;
        _filmSubtitleUi.transform.localScale = Vector3.one;
    }

    void BeginCanopyScriptBeats()
    {
        _suppressClimax = true;
        _climaxCrisisStarted = false;
        _endingSequenceActive = true;
        _leverPulled = true;
        _scriptHoldTimer = 0f;
        ClearPendingClimax();

        var player = AdventurePlayerController.Instance
                     ?? Object.FindAnyObjectByType<AdventurePlayerController>();
        if (player != null && player.transform.position.y < 90f)
            player.ForceGroundReset();

        AdventureMusicDirector.Ensure();
        // 天蓋崩壊に入ったら壮大な天空突破テーマBGMを1周目（神聖ブラス＋アルペジオ）から確実に開始
        AdventureMusicDirector.Instance?.PlaySkybreakTheme(force: true);
        StartSkybreakWindAmbience(); // 風の音は追加で流す

        // 天蓋崩壊中はウミネコ（カモメ）音声を小さい声も含めて完全に強制沈黙
        AdventureSoaringSeagullsManager.SilenceAllWorldSeagulls(true);

        // 「空が割れた」直後から割れ目の向こうの空を見せる
        SpawnWildernessPanorama(coldCrisis: true);
        ApplySkybreakColdAtmosphere();

        EnsureCinematicLetterbox();
        if (_letterboxRoot != null)
        {
            _letterboxRoot.SetActive(true);
            if (_letterboxCg != null) _letterboxCg.alpha = 1f;
        }
        _letterboxTargetAlpha = 1f;
        SetExplorationHudVisible(false);

        // 中央ボードは出さずに完全スクリーンスクリプト（シネマ字幕）で進行
        if (_scriptUiRoot != null)
            _scriptUiRoot.SetActive(false);

        // ピアノが鳴っていたら2秒かけてフェードアウト＆ダッキング解除
        var piano = AdventureAncientPianoRelic.Instance ?? Object.FindAnyObjectByType<AdventureAncientPianoRelic>();
        if (piano != null)
            piano.FadeOutPiano(2.0f);

        var drone = GetDrone();
        if (drone != null)
        {
            drone.ClearSpeech();
            drone.StartSkybreakNestle();
        }

        _canopyBeatIndex = 0;
        _skyTearPlayed = false;
        PresentCanopyBeat(0);
        Debug.Log("[RustAndFloat] 天蓋スクリーンスクリプト（シネマ字幕）を Update 駆動で開始（全" + CanopyBeats.Length + "枚）");
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
        // 中央ボードは出さず、スクリーンスクリプト（シネマ字幕）として映画のように表示
        if (_scriptUiRoot != null)
            _scriptUiRoot.SetActive(false);

        SetScreenScriptSubtitle(beat.Title, beat.Speaker, beat.Body, beat.Accent, beat.IsDive);

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
                if (_scriptBoardIsDive && _filmSubtitleUi != null)
                {
                    _filmSubtitleUi.text = "<size=30><color=#FFE073><b>【 光の柱へダイブ中……！ 】</b></color></size>";
                }
                if (_scriptHoldTimer >= 0.08f)
                    _scriptBoardAdvance = true;
            }
            else
            {
                _scriptHoldTimer = 0f;
            }

            // 各ビートの定義データから自動送り秒数を取得（1枚目7.5s／2枚目4.5s／3枚目7.0s／会話3.8s／ダイブ6.0s）
            float autoSec = (_canopyBeatIndex >= 0 && _canopyBeatIndex < CanopyBeats.Length)
                ? CanopyBeats[_canopyBeatIndex].AutoAdvanceSeconds
                : (_scriptBoardIsDive ? 6.0f : 3.5f);
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
        Debug.Log("[RustAndFloat] 天蓋スクリーンスクリプト完了 → 光の柱上昇 → クライマックスへ");
        _canopyBeatIndex = -1;
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
        if (_scriptRequireInputRelease) return;
        if (_climaxCrisisStarted) return; // クライマックス中はTickClimaxSequenceが一元管理
        float minShow = 0.45f;
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
