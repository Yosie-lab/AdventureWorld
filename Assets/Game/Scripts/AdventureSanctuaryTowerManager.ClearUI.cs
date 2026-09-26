using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 聖域の塔（Sanctuary Tower）のゲームクリアモーダルUI・入力判定・リプレイ／大空ダイブ処理
/// </summary>
public partial class AdventureSanctuaryTowerManager
{
    #region ゲームクリアモーダルUI

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

        var kb = UnityEngine.InputSystem.Keyboard.current;
        bool spacePressed = kb != null && (kb.spaceKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame);
        bool nPressed = kb != null && kb.nKey.wasPressedThisFrame;
        bool closePressed = kb != null && (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame);
        try
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J)) spacePressed = true;
            if (Input.GetKeyDown(KeyCode.N)) nPressed = true;
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape)) closePressed = true;
        }
        catch { }

        if (spacePressed)
            RelaunchIntoSky();
        else if (nPressed)
            StartNewGameFromClearModal();
        else if (closePressed)
            CloseGameClearModalForFreeExplore();

        if (WasPointerPressedThisFrame(out Vector2 pointer))
        {
            if (PointerHits(_clearDiveBtn, pointer))
                RelaunchIntoSky();
            else if (PointerHits(_clearNewBtn, pointer))
                StartNewGameFromClearModal();
            else if (PointerHits(_clearCloseBtn, pointer))
                CloseGameClearModalForFreeExplore();
        }
    }

    static bool WasPointerPressedThisFrame(out Vector2 pointer)
    {
        pointer = Vector2.zero;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
            pointer = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
                return true;
        }
        try
        {
            if (Input.GetMouseButtonDown(0))
            {
                pointer = Input.mousePosition;
                return true;
            }
        }
        catch { }
        return false;
    }

    static bool PointerHits(RectTransform rt, Vector2 screenPos)
    {
        if (rt == null || !rt.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null);
    }

    void SetGameClearModalVisible(bool visible)
    {
        bool wasVisible = _showGameClearModal;
        _showGameClearModal = visible;
        if (visible)
        {
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

    void EnsureGameClearModalUI()
    {
        EnsureEventSystemForUi();
        if (_clearUiRoot != null)
        {
            _clearUiRoot.SetActive(true);
            if (_clearDiveBtn == null)
                _clearDiveBtn = _clearUiRoot.transform.Find("Panel/DiveBtn") as RectTransform;
            if (_clearNewBtn == null)
                _clearNewBtn = _clearUiRoot.transform.Find("Panel/NewBtn") as RectTransform;
            if (_clearCloseBtn == null)
                _clearCloseBtn = _clearUiRoot.transform.Find("Panel/CloseBtn") as RectTransform;
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
            "「ありがとう、Niko。僕たちの翼で、どこまでも行こう……！」\n\n" +
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
        rt.sizeDelta = new Vector2(280f, 56f);
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(onClick);
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
        _showGameClearModal = false;
        HideGameClearModalUI();
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
