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
    private Text _resultFlyAgainBtnText;

    private void EnsureResultModalUI()
    {
        AdventurePauseMenu.EnsureEventSystemForUi();

        if (_resultCanvasGo != null)
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
        var btnRt = btnGo.AddComponent<RectTransform>();
        btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 0f);
        btnRt.pivot = new Vector2(0.5f, 0f);
        btnRt.anchoredPosition = new Vector2(0f, 28f);
        btnRt.sizeDelta = new Vector2(360f, 62f);

        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0.12f, 0.52f, 0.92f, 0.96f);

        _resultFlyAgainBtn = btnGo.AddComponent<Button>();
        _resultFlyAgainBtn.targetGraphic = btnImg;
        _resultFlyAgainBtn.transition = Selectable.Transition.ColorTint;
        var colors = _resultFlyAgainBtn.colors;
        colors.normalColor = new Color(0.12f, 0.52f, 0.92f, 0.96f);
        colors.highlightedColor = new Color(0.25f, 0.70f, 1.0f, 1f);
        colors.pressedColor = new Color(0.08f, 0.35f, 0.65f, 1f);
        colors.selectedColor = colors.normalColor;
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
        var btnTextOutline = btnTextGo.AddComponent<Outline>();
        btnTextOutline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        btnTextOutline.effectDistance = new Vector2(1.2f, -1.2f);

        UpdateResultModalTexts();
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
        if (_resultCanvasGo != null)
        {
            _resultCanvasGo.SetActive(false);
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
