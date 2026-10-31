using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// タイムアタックのリザルト画面およびHUD表示（uGUI高精細モーダル）
/// Unity 6 (macOS) における OnGUI のフォント・文字抜け・カーソル問題を根絶し、
/// 聖域の塔モーダルと同等の最高峰クオリティで確実に描画する。
/// </summary>
public partial class AdventureGlidingTimeAttackManager
{
    private GameObject _resultCanvasGo;
    private Text _resultTitleText;
    private Text _resultDetailsText;
    private Button _resultFlyAgainBtn;
    private RectTransform _resultFlyAgainBtnRect;
    private Image _resultFlyAgainBtnImg;
    private Text _resultFlyAgainBtnText;

    private static readonly Color BtnNormalColor = new Color(0.12f, 0.52f, 0.92f, 0.96f);
    private static readonly Color BtnHoverColor  = new Color(0.25f, 0.75f, 1.0f, 1.0f);

    private void EnsureResultModalUI()
    {
        AdventurePauseMenu.EnsureEventSystemForUi();

        if (_resultCanvasGo != null && _resultCanvasGo)
        {
            _resultCanvasGo.SetActive(true);
            UpdateResultModalTexts();
            return;
        }

        Font font = ResolveUiFont();

        _resultCanvasGo = new GameObject("GlidingResultCanvas");
        _resultCanvasGo.transform.SetParent(transform, false);

        var canvas = _resultCanvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9500;

        var scaler = _resultCanvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        _resultCanvasGo.AddComponent<GraphicRaycaster>();

        // 背景遮蔽パネル（薄いダークブルー半透明）
        var backdropGo = new GameObject("Backdrop");
        backdropGo.transform.SetParent(_resultCanvasGo.transform, false);
        var backdropRt = backdropGo.AddComponent<RectTransform>();
        backdropRt.anchorMin = Vector2.zero;
        backdropRt.anchorMax = Vector2.one;
        backdropRt.offsetMin = Vector2.zero;
        backdropRt.offsetMax = Vector2.zero;
        var backdropImg = backdropGo.AddComponent<Image>();
        backdropImg.color = new Color(0.01f, 0.03f, 0.08f, 0.45f);
        backdropImg.raycastTarget = true;

        // メインパネル
        var panelGo = new GameObject("ResultPanel");
        panelGo.transform.SetParent(_resultCanvasGo.transform, false);
        var panelRt = panelGo.AddComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = new Vector2(0f, 20f);
        panelRt.sizeDelta = new Vector2(720f, 420f);

        var panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0.03f, 0.07f, 0.15f, 0.96f);
        panelImg.raycastTarget = true;

        // 金色／シアンの洗練された枠線
        var topBorder = CreateLine(panelGo.transform, "TopBorder", new Vector2(0f, 208f), new Vector2(720f, 4f),
            _isNewRecord ? new Color(1f, 0.85f, 0.2f, 0.95f) : new Color(0.2f, 0.85f, 1f, 0.9f));
        var btmBorder = CreateLine(panelGo.transform, "BtmBorder", new Vector2(0f, -208f), new Vector2(720f, 4f),
            _isNewRecord ? new Color(1f, 0.85f, 0.2f, 0.95f) : new Color(0.2f, 0.85f, 1f, 0.9f));

        // タイトル
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(panelGo.transform, false);
        var titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -24f);
        titleRt.sizeDelta = new Vector2(660f, 48f);
        _resultTitleText = titleGo.AddComponent<Text>();
        _resultTitleText.font = font;
        _resultTitleText.fontSize = 32;
        _resultTitleText.fontStyle = FontStyle.Bold;
        _resultTitleText.alignment = TextAnchor.MiddleCenter;
        _resultTitleText.color = _isNewRecord ? new Color(1f, 0.88f, 0.25f, 1f) : new Color(0.35f, 0.92f, 1f, 1f);
        _resultTitleText.supportRichText = true;
        var titleOutline = titleGo.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        titleOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // 詳細テキスト（タイム・リング数・ランク・自己ベスト）
        var detailsGo = new GameObject("Details");
        detailsGo.transform.SetParent(panelGo.transform, false);
        var detailsRt = detailsGo.AddComponent<RectTransform>();
        detailsRt.anchorMin = detailsRt.anchorMax = new Vector2(0.5f, 0.5f);
        detailsRt.pivot = new Vector2(0.5f, 0.5f);
        detailsRt.anchoredPosition = new Vector2(0f, 8f);
        detailsRt.sizeDelta = new Vector2(600f, 210f);
        _resultDetailsText = detailsGo.AddComponent<Text>();
        _resultDetailsText.font = font;
        _resultDetailsText.fontSize = 24;
        _resultDetailsText.fontStyle = FontStyle.Bold;
        _resultDetailsText.lineSpacing = 1.35f;
        _resultDetailsText.alignment = TextAnchor.MiddleCenter;
        _resultDetailsText.color = new Color(0.92f, 0.96f, 1f, 1f);
        _resultDetailsText.supportRichText = true;
        var detailsOutline = detailsGo.AddComponent<Outline>();
        detailsOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        detailsOutline.effectDistance = new Vector2(1.2f, -1.2f);

        // リトライボタン「🔄 もう一度飛ぶ [R]」
        var btnGo = new GameObject("FlyAgainBtn");
        btnGo.transform.SetParent(panelGo.transform, false);
        _resultFlyAgainBtnRect = btnGo.AddComponent<RectTransform>();
        _resultFlyAgainBtnRect.anchorMin = _resultFlyAgainBtnRect.anchorMax = new Vector2(0.5f, 0f);
        _resultFlyAgainBtnRect.pivot = new Vector2(0.5f, 0f);
        _resultFlyAgainBtnRect.anchoredPosition = new Vector2(0f, 28f);
        _resultFlyAgainBtnRect.sizeDelta = new Vector2(360f, 62f);

        _resultFlyAgainBtnImg = btnGo.AddComponent<Image>();
        _resultFlyAgainBtnImg.color = BtnNormalColor;
        _resultFlyAgainBtnImg.raycastTarget = true;

        _resultFlyAgainBtn = btnGo.AddComponent<Button>();
        _resultFlyAgainBtn.targetGraphic = _resultFlyAgainBtnImg;
        _resultFlyAgainBtn.transition = Selectable.Transition.ColorTint;
        var colors = _resultFlyAgainBtn.colors;
        colors.normalColor = BtnNormalColor;
        colors.highlightedColor = BtnHoverColor;
        colors.pressedColor = new Color(0.08f, 0.35f, 0.65f, 1f);
        colors.selectedColor = BtnNormalColor;
        _resultFlyAgainBtn.colors = colors;
        _resultFlyAgainBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        _resultFlyAgainBtn.onClick.AddListener(RetryAtStart);

        var btnTextGo = new GameObject("Label");
        btnTextGo.transform.SetParent(btnGo.transform, false);
        var btnTextRt = btnTextGo.AddComponent<RectTransform>();
        btnTextRt.anchorMin = Vector2.zero;
        btnTextRt.anchorMax = Vector2.one;
        btnTextRt.offsetMin = Vector2.zero;
        btnTextRt.offsetMax = Vector2.zero;
        _resultFlyAgainBtnText = btnTextGo.AddComponent<Text>();
        _resultFlyAgainBtnText.font = font;
        _resultFlyAgainBtnText.fontSize = 22;
        _resultFlyAgainBtnText.fontStyle = FontStyle.Bold;
        _resultFlyAgainBtnText.alignment = TextAnchor.MiddleCenter;
        _resultFlyAgainBtnText.color = Color.white;
        _resultFlyAgainBtnText.raycastTarget = false; // ★重要: テキストがクリックイベントを遮断しないように false
        var btnTextOutline = btnTextGo.AddComponent<Outline>();
        btnTextOutline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        btnTextOutline.effectDistance = new Vector2(1.2f, -1.2f);

        UpdateResultModalTexts();
    }

    /// <summary>
    /// リザルトモーダル表示中の直接入力検知（マウスクリック＆キーボード[Space]/[Enter]/[R]/[T]両対応）
    /// uGUIのEventSystemがフォーカス外れ等で不発になる場合でも、確実に100%リトライを発動する
    /// </summary>
    public void TickResultModalInput()
    {
        if (!_showResult || _resultCanvasGo == null || !_resultCanvasGo.activeInHierarchy)
            return;

        // リザルト表示中は毎フレーム確実にマウスカーソルを解放
        if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        Vector2 mousePos = ReadMouseScreenPos();

        // ボタンのホバーフィードバック
        bool isHovered = _resultFlyAgainBtnRect != null && PointerHitsButton(_resultFlyAgainBtnRect, mousePos);
        if (_resultFlyAgainBtnImg != null)
        {
            _resultFlyAgainBtnImg.color = isHovered ? BtnHoverColor : BtnNormalColor;
        }

        // 1. マウスクリック直接検知（左クリック押下時または離した瞬間の両方で確実にキャッチ）
        if (WasPointerPressedThisFrame(out Vector2 clickPos))
        {
            if (PointerHitsButton(_resultFlyAgainBtnRect, clickPos))
            {
                Debug.Log("[RustAndFloat] リザルト【もう一度飛ぶ】マウスクリック直接検知 → RetryAtStart");
                RetryAtStart();
                return;
            }
        }

        // 2. キーボード操作（Space, Enter, R, T）
        if (AdventureInputReader.SpaceOrJDown || AdventureInputReader.EnterDown 
            || AdventureInputReader.ResetDown || AdventureInputReader.TDown)
        {
            Debug.Log("[RustAndFloat] リザルト【もう一度飛ぶ】キーボード検知 → RetryAtStart");
            RetryAtStart();
            return;
        }
    }

    private static Vector2 ReadMouseScreenPos()
    {
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
            return mouse.position.ReadValue();
        try { return Input.mousePosition; } catch { return Vector2.zero; }
    }

    private static bool WasPointerPressedThisFrame(out Vector2 pointer)
    {
        pointer = Vector2.zero;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.leftButton.wasReleasedThisFrame))
        {
            pointer = mouse.position.ReadValue();
            return true;
        }
        try
        {
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0))
            {
                pointer = Input.mousePosition;
                return true;
            }
        }
        catch { }
        return false;
    }

    private static bool PointerHitsButton(RectTransform rt, Vector2 screenPos)
    {
        if (rt == null || !rt.gameObject.activeInHierarchy) return false;

        // 1. EventSystem による Raycast
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null)
        {
            var ped = new UnityEngine.EventSystems.PointerEventData(es) { position = screenPos };
            var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(ped, results);
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].gameObject == rt.gameObject || results[i].gameObject.transform.IsChildOf(rt))
                    return true;
            }
        }

        // 2. 画面座標境界矩形判定（Retinaディスプレイ・解像度スケーリング両対応）
        if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null))
            return true;
        if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos * 2f, null))
            return true;
        if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos * 0.5f, null))
            return true;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, null, out Vector2 localPoint))
        {
            if (rt.rect.Contains(localPoint))
                return true;
        }

        return false;
    }

    private void UpdateResultModalTexts()
    {
        if (_resultTitleText != null)
        {
            _resultTitleText.text = _isNewRecord
                ? AdventureLocalizationManager.Get("ta_result_new_record")
                : AdventureLocalizationManager.Get("ta_result_finish");
            _resultTitleText.color = _isNewRecord ? new Color(1f, 0.88f, 0.25f, 1f) : new Color(0.35f, 0.92f, 1f, 1f);
        }

        if (_resultDetailsText != null)
        {
            int total = courseRings.Count > 0 ? courseRings.Count : 10;
            _resultDetailsText.text = AdventureLocalizationManager.GetFormat("ta_result_details",
                _resultTimer, ringsPassedCount, total, _currentResultRank, bestTime, bestRank);
        }

        if (_resultFlyAgainBtnText != null)
        {
            _resultFlyAgainBtnText.text = AdventureLocalizationManager.Get("ta_btn_fly_again");
        }
    }

    public void HideResultModalUI()
    {
        if (_resultCanvasGo != null && _resultCanvasGo)
        {
            _resultCanvasGo.SetActive(false);
        }
    }

    public void DestroyResultModalUI()
    {
        if (_resultCanvasGo != null)
        {
            if (Application.isPlaying) Destroy(_resultCanvasGo);
            else DestroyImmediate(_resultCanvasGo);
            _resultCanvasGo = null;
        }
    }

    private static GameObject CreateLine(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        var line = new GameObject(name);
        line.transform.SetParent(parent, false);
        var rt = line.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = line.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return line;
    }

    private static Font ResolveUiFont()
    {
        string[] candidates =
        {
            "PingFang SC",
            "Hiragino Sans GB",
            "Heiti SC",
            "HiraginoSans-W3",
            "HiraginoSans-W6",
            "Hiragino Kaku Gothic ProN",
            "YuGothic",
            "Yu Gothic",
            "Apple SD Gothic Neo",
            "Microsoft YaHei",
            "SimHei",
            "Arial Unicode MS",
            "Helvetica Neue",
            "Arial"
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            try
            {
                Font font = Font.CreateDynamicFontFromOSFont(candidates[i], 24);
                if (font != null) return font;
            }
            catch { }
        }

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
               ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
