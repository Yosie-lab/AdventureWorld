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

    private Button _resultResumeBtn;
    private RectTransform _resultResumeBtnRect;
    private Image _resultResumeBtnImg;
    private Text _resultResumeBtnText;

    private static readonly Color BtnNormalColor       = new Color(0.12f, 0.52f, 0.92f, 0.96f);
    private static readonly Color BtnHoverColor        = new Color(0.25f, 0.75f, 1.0f, 1.0f);
    private static readonly Color ResumeBtnNormalColor = new Color(0.12f, 0.62f, 0.40f, 0.96f);
    private static readonly Color ResumeBtnHoverColor  = new Color(0.22f, 0.82f, 0.52f, 1.0f);

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

        // パネル横幅を少し広げて2つのボタンを美しく配置
        panelRt.sizeDelta = new Vector2(760f, 430f);

        // 金色／シアンの洗練された枠線
        var topBorder = CreateLine(panelGo.transform, "TopBorder", new Vector2(0f, 213f), new Vector2(760f, 4f),
            _isNewRecord ? new Color(1f, 0.85f, 0.2f, 0.95f) : new Color(0.2f, 0.85f, 1f, 0.9f));
        var btmBorder = CreateLine(panelGo.transform, "BtmBorder", new Vector2(0f, -213f), new Vector2(760f, 4f),
            _isNewRecord ? new Color(1f, 0.85f, 0.2f, 0.95f) : new Color(0.2f, 0.85f, 1f, 0.9f));

        // タイトル
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(panelGo.transform, false);
        var titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -24f);
        titleRt.sizeDelta = new Vector2(700f, 48f);
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
        detailsRt.anchoredPosition = new Vector2(0f, 14f);
        detailsRt.sizeDelta = new Vector2(660f, 210f);
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

        // 左ボタン: リトライ「🔄 もう一度飛ぶ [R]」
        var flyBtnGo = new GameObject("FlyAgainBtn");
        flyBtnGo.transform.SetParent(panelGo.transform, false);
        _resultFlyAgainBtnRect = flyBtnGo.AddComponent<RectTransform>();
        _resultFlyAgainBtnRect.anchorMin = _resultFlyAgainBtnRect.anchorMax = new Vector2(0.5f, 0f);
        _resultFlyAgainBtnRect.pivot = new Vector2(0.5f, 0f);
        _resultFlyAgainBtnRect.anchoredPosition = new Vector2(-175f, 26f);
        _resultFlyAgainBtnRect.sizeDelta = new Vector2(320f, 58f);

        _resultFlyAgainBtnImg = flyBtnGo.AddComponent<Image>();
        _resultFlyAgainBtnImg.color = BtnNormalColor;
        _resultFlyAgainBtnImg.raycastTarget = true;

        _resultFlyAgainBtn = flyBtnGo.AddComponent<Button>();
        _resultFlyAgainBtn.targetGraphic = _resultFlyAgainBtnImg;
        _resultFlyAgainBtn.transition = Selectable.Transition.ColorTint;
        var flyColors = _resultFlyAgainBtn.colors;
        flyColors.normalColor = BtnNormalColor;
        flyColors.highlightedColor = BtnHoverColor;
        flyColors.pressedColor = new Color(0.08f, 0.35f, 0.65f, 1f);
        flyColors.selectedColor = BtnNormalColor;
        _resultFlyAgainBtn.colors = flyColors;
        _resultFlyAgainBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        _resultFlyAgainBtn.onClick.AddListener(RetryAtStart);

        var flyBtnTextGo = new GameObject("Label");
        flyBtnTextGo.transform.SetParent(flyBtnGo.transform, false);
        var flyBtnTextRt = flyBtnTextGo.AddComponent<RectTransform>();
        flyBtnTextRt.anchorMin = Vector2.zero;
        flyBtnTextRt.anchorMax = Vector2.one;
        flyBtnTextRt.offsetMin = Vector2.zero;
        flyBtnTextRt.offsetMax = Vector2.zero;
        _resultFlyAgainBtnText = flyBtnTextGo.AddComponent<Text>();
        _resultFlyAgainBtnText.font = font;
        _resultFlyAgainBtnText.fontSize = 21;
        _resultFlyAgainBtnText.fontStyle = FontStyle.Bold;
        _resultFlyAgainBtnText.alignment = TextAnchor.MiddleCenter;
        _resultFlyAgainBtnText.color = Color.white;
        _resultFlyAgainBtnText.raycastTarget = false;
        var flyBtnTextOutline = flyBtnTextGo.AddComponent<Outline>();
        flyBtnTextOutline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        flyBtnTextOutline.effectDistance = new Vector2(1.2f, -1.2f);

        // 右ボタン: 探索復帰「🌴 探索に戻る [Esc]」
        var resumeBtnGo = new GameObject("ResumeExplorationBtn");
        resumeBtnGo.transform.SetParent(panelGo.transform, false);
        _resultResumeBtnRect = resumeBtnGo.AddComponent<RectTransform>();
        _resultResumeBtnRect.anchorMin = _resultResumeBtnRect.anchorMax = new Vector2(0.5f, 0f);
        _resultResumeBtnRect.pivot = new Vector2(0.5f, 0f);
        _resultResumeBtnRect.anchoredPosition = new Vector2(175f, 26f);
        _resultResumeBtnRect.sizeDelta = new Vector2(320f, 58f);

        _resultResumeBtnImg = resumeBtnGo.AddComponent<Image>();
        _resultResumeBtnImg.color = ResumeBtnNormalColor;
        _resultResumeBtnImg.raycastTarget = true;

        _resultResumeBtn = resumeBtnGo.AddComponent<Button>();
        _resultResumeBtn.targetGraphic = _resultResumeBtnImg;
        _resultResumeBtn.transition = Selectable.Transition.ColorTint;
        var resumeColors = _resultResumeBtn.colors;
        resumeColors.normalColor = ResumeBtnNormalColor;
        resumeColors.highlightedColor = ResumeBtnHoverColor;
        resumeColors.pressedColor = new Color(0.08f, 0.45f, 0.28f, 1f);
        resumeColors.selectedColor = ResumeBtnNormalColor;
        _resultResumeBtn.colors = resumeColors;
        _resultResumeBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        _resultResumeBtn.onClick.AddListener(CloseResultAndResumeGame);

        var resumeBtnTextGo = new GameObject("Label");
        resumeBtnTextGo.transform.SetParent(resumeBtnGo.transform, false);
        var resumeBtnTextRt = resumeBtnTextGo.AddComponent<RectTransform>();
        resumeBtnTextRt.anchorMin = Vector2.zero;
        resumeBtnTextRt.anchorMax = Vector2.one;
        resumeBtnTextRt.offsetMin = Vector2.zero;
        resumeBtnTextRt.offsetMax = Vector2.zero;
        _resultResumeBtnText = resumeBtnTextGo.AddComponent<Text>();
        _resultResumeBtnText.font = font;
        _resultResumeBtnText.fontSize = 21;
        _resultResumeBtnText.fontStyle = FontStyle.Bold;
        _resultResumeBtnText.alignment = TextAnchor.MiddleCenter;
        _resultResumeBtnText.color = Color.white;
        _resultResumeBtnText.raycastTarget = false;
        var resumeBtnTextOutline = resumeBtnTextGo.AddComponent<Outline>();
        resumeBtnTextOutline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        resumeBtnTextOutline.effectDistance = new Vector2(1.2f, -1.2f);

        UpdateResultModalTexts();
    }

    /// <summary>
    /// リザルトモーダル表示中の直接入力検知（マウスクリック＆キーボード対応）
    /// [R] / [T] でリトライ、[Esc] / [Space] / [B] で通常探索へスムーズに復帰
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
        bool isFlyHovered = _resultFlyAgainBtnRect != null && PointerHitsButton(_resultFlyAgainBtnRect, mousePos);
        if (_resultFlyAgainBtnImg != null)
        {
            _resultFlyAgainBtnImg.color = isFlyHovered ? BtnHoverColor : BtnNormalColor;
        }

        bool isResumeHovered = _resultResumeBtnRect != null && PointerHitsButton(_resultResumeBtnRect, mousePos);
        if (_resultResumeBtnImg != null)
        {
            _resultResumeBtnImg.color = isResumeHovered ? ResumeBtnHoverColor : ResumeBtnNormalColor;
        }

        // 1. マウスクリック直接検知
        if (WasPointerPressedThisFrame(out Vector2 clickPos))
        {
            if (PointerHitsButton(_resultFlyAgainBtnRect, clickPos))
            {
                Debug.Log("[RustAndFloat] リザルト【もう一度飛ぶ】マウスクリック直接検知 → RetryAtStart");
                RetryAtStart();
                return;
            }
            if (PointerHitsButton(_resultResumeBtnRect, clickPos))
            {
                Debug.Log("[RustAndFloat] リザルト【探索に戻る】マウスクリック直接検知 → CloseResultAndResumeGame");
                CloseResultAndResumeGame();
                return;
            }
        }

        // 2. キーボード操作
        // [R] または [T]: リトライ
        if (AdventureInputReader.ResetDown || AdventureInputReader.TDown)
        {
            Debug.Log("[RustAndFloat] リザルト【もう一度飛ぶ】キーボード検知 → RetryAtStart");
            RetryAtStart();
            return;
        }

        // [Esc] または [Space] または [B]: 通常探索に戻る
        if (AdventureInputReader.EscapeDown || AdventureInputReader.SpaceOrJDown)
        {
            Debug.Log("[RustAndFloat] リザルト【探索に戻る】キーボード検知 → CloseResultAndResumeGame");
            CloseResultAndResumeGame();
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

        if (_resultResumeBtnText != null)
        {
            _resultResumeBtnText.text = AdventureLocalizationManager.Get("ta_btn_resume");
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
