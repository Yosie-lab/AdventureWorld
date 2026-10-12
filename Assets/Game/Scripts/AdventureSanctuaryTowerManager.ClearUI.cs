using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 聖域の塔（Sanctuary Tower）のゲームクリアモーダルUI・入力判定・リプレイ／大空ダイブ処理
/// </summary>
public partial class AdventureSanctuaryTowerManager
{
    #region ゲームクリアモーダルUI

    float _clearModalOpenedAt = 0f;
    bool _clearModalRequireRelease = false;

    void TickGameClearModal()
    {
        if (!_showGameClearModal)
        {
            HideGameClearModalUI();
            return;
        }

        HideScriptBoardCompletely();
        TeardownScriptBoardUi();
        HideOilPromptUI();
        EnsureGameClearModalUI();
        if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // 開いた直後の誤爆防止（エピローグ字幕送りSpaceの突き抜け防止）
        float elapsedSinceOpen = Time.unscaledTime - _clearModalOpenedAt;
        if (_clearModalRequireRelease)
        {
            if (!AdventureInputReader.SpaceOrJHeld)
                _clearModalRequireRelease = false;
        }

        // AdventureInputReader に委譲（新旧InputSystem統合）
        bool nPressed = false;
        var kb = AdventureInputReader.Keyboard;
        if (kb != null && kb.nKey.wasPressedThisFrame) nPressed = true;
        try { if (Input.GetKeyDown(KeyCode.N)) nPressed = true; } catch { }

        bool spacePressed = !_clearModalRequireRelease && elapsedSinceOpen >= 0.4f && AdventureInputReader.SpaceOrJDown;
        bool closePressed = elapsedSinceOpen >= 0.3f && (AdventureInputReader.InteractDown || AdventureInputReader.EscapeDown);

        // ★キーボード操作判定（N＝はじめから、Space＝大空へ、E/Esc＝閉じる）
        if (nPressed)
        {
            Debug.Log("[RustAndFloat] キーボード【N】検知 → StartNewGameFromClearModal 実行");
            StartNewGameFromClearModal();
            return;
        }
        if (spacePressed)
        {
            Debug.Log("[RustAndFloat] キーボード【Space】検知 → RelaunchIntoSky 実行");
            RelaunchIntoSky();
            return;
        }
        if (closePressed)
        {
            Debug.Log("[RustAndFloat] キーボード【E/Esc】検知 → CloseGameClearModalForFreeExplore 実行");
            CloseGameClearModalForFreeExplore();
            return;
        }
    }

    void SetGameClearModalVisible(bool visible)
    {
        bool wasVisible = _showGameClearModal;
        _showGameClearModal = visible;
        if (visible)
        {
            _clearModalOpenedAt = Time.unscaledTime;
            _clearModalRequireRelease = true;
            // 古いインスタンスが残っている場合はクリーンに再構築
            DestroyClearModalUI();
            EnsureGameClearModalUI();
            if (!wasVisible)
            {
                PlayGameClearTriumphChime();
            }
        }
        else
        {
            HideGameClearModalUI();
        }
    }

    void DestroyClearModalUI()
    {
        if (_clearUiRoot != null)
        {
            Destroy(_clearUiRoot);
            _clearUiRoot = null;
            _clearDiveBtn = null;
            _clearNewBtn = null;
            _clearCloseBtn = null;
        }
        var orphans = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < orphans.Length; i++)
        {
            if (orphans[i] != null && orphans[i].name == "GameClearModalCanvas")
                Destroy(orphans[i]);
        }
    }

    void EnsureGameClearModalUI()
    {
        EnsureEventSystemForUi();
        if (_clearUiRoot != null)
        {
            _clearUiRoot.SetActive(true);
            return;
        }

        Font font = ResolveUiFont();
        var canvasGo = new GameObject("GameClearModalCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6000;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        canvasGo.AddComponent<GraphicRaycaster>();

        var dimGo = new GameObject("Dim");
        dimGo.transform.SetParent(canvasGo.transform, false);
        var dimRt = dimGo.AddComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        var dimImg = dimGo.AddComponent<Image>();
        dimImg.color = new Color(0.01f, 0.02f, 0.05f, 0.55f);
        dimImg.raycastTarget = true;

        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelGo.AddComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(980f, 560f);
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0.02f, 0.05f, 0.10f, 0.96f);

        var title = MakeScriptText(panelGo.transform, "Title", new Vector2(0f, -28f), new Vector2(0.5f, 1f), new Vector2(900f, 48f), 34, TextAnchor.MiddleCenter, font);
        title.color = new Color(1f, 0.88f, 0.4f, 1f);
        title.fontStyle = FontStyle.Bold;
        title.text = "✦ 『Rust & Float』 GAME CLEAR ✦";
        PrepareFontForText(font, title.text, 34, FontStyle.Bold);

        var body = MakeScriptText(panelGo.transform, "Body", new Vector2(0f, 20f), new Vector2(0.5f, 0.5f), new Vector2(880f, 280f), 24, TextAnchor.UpperCenter, font);
        body.color = new Color(0.9f, 0.95f, 1f, 1f);
        body.text =
            "天蓋の檻を打ち破り、二人は蒼い風が吹く空へ羽ばたいた。\n\n" +
            "✦ 漂着古代パーツ回収： 12 / 12\n" +
            "✦ 相棒Rust： 二段ジャンプ・超滑空・探知ソナー\n\n" +
            "「ありがとう、Float。僕たちの翼で、どこまでも行こう……！」\n\n" +
            "【Space】大空へ　【N】はじめから　【E / Esc】閉じる";
        PrepareFontForText(font, body.text, 24);

        _clearDiveBtn = MakeClearModalButton(panelGo.transform, "DiveBtn", new Vector2(-300f, 36f), new Color(0.20f, 0.75f, 0.95f, 0.95f),
            "【Space】大空へダイブ", font, RelaunchIntoSky);
        _clearNewBtn = MakeClearModalButton(panelGo.transform, "NewBtn", new Vector2(0f, 36f), new Color(0.35f, 0.82f, 0.55f, 0.95f),
            "【N】はじめから", font, StartNewGameFromClearModal);
        _clearCloseBtn = MakeClearModalButton(panelGo.transform, "CloseBtn", new Vector2(300f, 36f), new Color(0.25f, 0.35f, 0.45f, 0.95f),
            "【E】閉じる", font, CloseGameClearModalForFreeExplore);

        _clearUiRoot = canvasGo;
    }

    RectTransform MakeClearModalButton(Transform parent, string name, Vector2 anchoredPos, Color color, string label, Font font, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(280f, 58f);
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;
        var colors = btn.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.35f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
        colors.selectedColor = color;
        btn.colors = colors;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        btn.onClick.AddListener(onClick);
        var forwarder = go.AddComponent<ClearModalButtonClickForwarder>();
        forwarder.Init(onClick);
        var text = MakeScriptText(go.transform, "Label", Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(260f, 48f), 20, TextAnchor.MiddleCenter, font);
        text.color = Color.white;
        text.fontStyle = FontStyle.Bold;
        text.text = label;
        text.raycastTarget = false;
        PrepareFontForText(font, label, 20, FontStyle.Bold);
        return rt;
    }

    void HideGameClearModalUI()
    {
        if (_clearUiRoot != null)
            _clearUiRoot.SetActive(false);
        var orphan = GameObject.Find("GameClearModalCanvas");
        if (orphan != null && orphan != _clearUiRoot)
            orphan.SetActive(false);
    }

    bool ConsumeClearModalAction()
    {
        if (_clearModalActionFrame == Time.frameCount) return false;
        _clearModalActionFrame = Time.frameCount;
        return true;
    }

    void StartNewGameFromClearModal()
    {
        if (!ConsumeClearModalAction()) return;
        Debug.Log("[RustAndFloat] ★★★ StartNewGameFromClearModal 実行開始 ★★★");
        _showGameClearModal = false;
        HideGameClearModalUI();

        // エピローグ・クライマックス・エンディングのランタイム状態をゼロクリア
        _epilogueTriggered = false;
        _endingSequenceActive = false;
        _canopyBeatIndex = -1;
        _climaxCrisisStarted = false;
        _climaxOilInjected = false;
        _climaxOilWaiting = false;
        _filmIndex = -1;
        _filmPhase = 0;
        AdventureStoryFlow.ResetFlowToNewGame();

        var player = GetPlayer();
        if (player != null)
        {
            player.SetAutoGlideMode(false);
            player.ForceGroundReset();
        }
        SetCinematicCamera(false);
        var drone = GetDrone();
        if (drone != null)
        {
            drone.StopSkybreakNestle();
            drone.ResetClimaxState();
        }
        AdventureSaveManager.Ensure();
        AdventureSaveManager.Instance?.ResetToNewGame();
    }

    void CloseGameClearModalForFreeExplore()
    {
        if (!ConsumeClearModalAction()) return;
        Debug.Log("[RustAndFloat] ★ CloseGameClearModalForFreeExplore 実行（自由探索へ復帰）");
        _showGameClearModal = false;
        HideGameClearModalUI();
        AdventureMusicDirector.Ensure();
        AdventureMusicDirector.Instance?.RestoreExplorationTheme();
        var player = GetPlayer();
        if (player != null)
            player.SetAutoGlideMode(false);
        SetCinematicCamera(false);
        SetExplorationHudVisible(true);
        var drone = GetDrone();
        if (drone != null)
        {
            drone.StopSkybreakNestle();
            drone.ResetClimaxState();
        }
    }

    /// <summary>ゲームクリアリザルト画面を直接開く</summary>
    public void OpenGameClearModal()
    {
        SetGameClearModalVisible(true);
    }

    /// <summary>クリア後に何度でも大空へ飛び立てるリダイブ処理</summary>
    public void RelaunchIntoSky()
    {
        if (!ConsumeClearModalAction()) return;
        Debug.Log("[RustAndFloat] ★ RelaunchIntoSky 実行（大空へダイブ）");
        _showGameClearModal = false;
        HideGameClearModalUI();
        AdventureMusicDirector.Ensure();
        AdventureMusicDirector.Instance?.RestoreExplorationTheme();
        var player = GetPlayer();
        if (player != null)
        {
            // タワー上空の光柱へワープし、大空へ打ち上げ＆スーパー滑空
            player.SetAutoGlideMode(false);
            player.transform.position = new Vector3(512f, 110f, 512f);
            player.ApplyLaunchUpdraft(25f, 25f);
            player.ApplyGlideBoost(3.0f, 65f);
            player.SetAutoGlideMode(true, 120f);
        }
        SetCinematicCamera(true);
        var drone = GetDrone();
        if (drone != null)
        {
            drone.SpeakCustom("いっくよー！大空へダイブ！！", 5.0f);
        }
    }

    #endregion
}

/// <summary>
/// クリアモーダルの各ボタン専用クリックフォワーダー。
/// uGUIのEventSystemから直接ポインタクリックを受け取り、割り当てられたコールバックを確実に実行する。
/// 自前座標計算によるボタン誤ヒット・誤爆を完全防止。
/// </summary>
public class ClearModalButtonClickForwarder : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
{
    private UnityEngine.Events.UnityAction _callback;

    public void Init(UnityEngine.Events.UnityAction callback)
    {
        _callback = callback;
    }

    public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
    {
        _callback?.Invoke();
    }
}
