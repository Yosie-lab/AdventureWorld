using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 『Rust & Float』オープニング画面のUI構築およびレイアウト定義（HUD、ストーリーカード、メニューカード、モーダルなど）。
/// </summary>
public partial class AdventureRustFloatOpening
{
    void BuildHud()
    {
        if (_canvasGo != null)
        {
            Destroy(_canvasGo);
            _canvasGo = null;
            _overlayGo = null;
            _modalBoard = null;
            _modalCg = null;
            _guideText = null;
            _storyHeaderText = null;
            _storyBodyText = null;
            _preStartImageRoot = null;
            _preStartImage = null;
            _preStartTexture = null;
        }

        // 孤児化した旧HUDがあれば掃除
        foreach (var orphanCanvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (orphanCanvas != null && orphanCanvas.gameObject.name == "RustFloatHUD")
                Destroy(orphanCanvas.gameObject);
        }

        AdventurePauseMenu.Ensure();
        AdventurePauseMenu.EnsureEventSystemForUi();

        _canvasGo = new GameObject("RustFloatHUD");
        var canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = TitleCanvasSortOrder;

        var scaler = _canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        _canvasGo.AddComponent<GraphicRaycaster>();

        Font font = ResolveFont();

        CreatePreStartBackground();

        // 1. 画面最上部中央の水平リボンコンパスHUD
        AdventureCompassHUD.Create(_canvasGo.transform, font);

        // 2. 上部中央の操作ガイド
        _guideText = MakeText(_canvasGo.transform, "Guide", Vector2.zero, new Vector2(0.5f, 1f), new Vector2(980f, 32f), 13, TextAnchor.UpperCenter, font);
        var guideRt = _guideText.rectTransform;
        guideRt.anchorMin = new Vector2(0.5f, 1f);
        guideRt.anchorMax = new Vector2(0.5f, 1f);
        guideRt.pivot = new Vector2(0.5f, 1f);
        guideRt.anchoredPosition = new Vector2(0f, AdventureExplorationHudSettings.GetGuideRowY(false));
        guideRt.sizeDelta = new Vector2(980f, AdventureExplorationHudSettings.GuideRowHeight);
        _guideText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _guideText.verticalOverflow = VerticalWrapMode.Overflow;
        _guideText.color = new Color(0.9f, 0.95f, 1f, 0.75f);
        var guideOutline = _guideText.gameObject.AddComponent<Outline>();
        guideOutline.effectColor = new Color(0f, 0f, 0f, 0.7f);
        guideOutline.effectDistance = new Vector2(1f, -1f);
        _guideText.gameObject.SetActive(false);

        // 3. 全画面のアンビエントオーバーレイ（暗転させず背景の自然光を活かす）
        _overlayGo = new GameObject("OpeningOverlay");
        _overlayGo.transform.SetParent(_canvasGo.transform, false);
        var overlayRt = _overlayGo.AddComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        var overlayImg = _overlayGo.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.05f); // 背景画像が明るくクリアに見える極薄アンビエント
        overlayImg.raycastTarget = false;

        // 4. メインタイトルボード（洗練されたグラスモーフィズムカード: 幅860, 高さ540）
        _modalBoard = new GameObject("StoryModalBoard");
        _modalBoard.transform.SetParent(_overlayGo.transform, false);
        _modalCg = _modalBoard.AddComponent<CanvasGroup>();
        _modalCg.interactable = true;
        _modalCg.blocksRaycasts = true;

        var boardRt = _modalBoard.AddComponent<RectTransform>();
        boardRt.anchorMin = new Vector2(0.5f, 0.5f);
        boardRt.anchorMax = new Vector2(0.5f, 0.5f);
        boardRt.pivot = new Vector2(0.5f, 0.5f);
        boardRt.anchoredPosition = new Vector2(0f, 0f);
        boardRt.sizeDelta = new Vector2(860f, 540f);

        // ボード背景（背景の美しい絶景が透けて見えるプレミアム半透明フロストガラス調）
        var boardImg = _modalBoard.AddComponent<Image>();
        boardImg.color = new Color(0.012f, 0.035f, 0.075f, 0.55f);
        boardImg.raycastTarget = false;

        // 外枠線アウトライン（サイバーエメラルド光彩）
        var boardOutline = _modalBoard.AddComponent<Outline>();
        boardOutline.effectColor = new Color(0.35f, 0.85f, 1.0f, 0.65f);
        boardOutline.effectDistance = new Vector2(2f, -2f);

        // 上部アクセントバー
        var accentGo = new GameObject("AccentLine");
        accentGo.transform.SetParent(_modalBoard.transform, false);
        var accentRt = accentGo.AddComponent<RectTransform>();
        accentRt.anchorMin = new Vector2(0f, 1f);
        accentRt.anchorMax = new Vector2(1f, 1f);
        accentRt.pivot = new Vector2(0.5f, 1f);
        accentRt.anchoredPosition = Vector2.zero;
        accentRt.sizeDelta = new Vector2(0f, 3.5f);
        var accentImg = accentGo.AddComponent<Image>();
        accentImg.color = new Color(0.35f, 0.95f, 0.85f, 1f);
        accentImg.raycastTarget = false;

        // タイトルヘッダー
        var titleText = MakeText(_modalBoard.transform, "Title", new Vector2(0f, -20f), new Vector2(0.5f, 1f), new Vector2(800f, 40f), 28, TextAnchor.MiddleCenter, font);
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = new Color(1.0f, 0.90f, 0.45f, 1f);
        var titleOutline = titleText.gameObject.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        titleOutline.effectDistance = new Vector2(1.5f, -1.5f);
        titleText.text = TitleText;

        // サブタイトル
        _subTitleText = MakeText(_modalBoard.transform, "SubTitle", new Vector2(0f, -58f), new Vector2(0.5f, 1f), new Vector2(800f, 22f), 14, TextAnchor.MiddleCenter, font);
        _subTitleText.color = new Color(0.50f, 0.90f, 1.0f, 0.95f);
        _subTitleText.fontStyle = FontStyle.Bold;
        _subTitleText.text = SubTitleText;

        // 2カラムレイアウト（左: 世界観カード, 右: メニューボタン）
        var contentGo = new GameObject("ContentColumns");
        contentGo.transform.SetParent(_modalBoard.transform, false);
        var contentRt = contentGo.AddComponent<RectTransform>();
        contentRt.anchorMin = Vector2.zero;
        contentRt.anchorMax = Vector2.one;
        contentRt.offsetMin = new Vector2(25f, 35f);
        contentRt.offsetMax = new Vector2(-25f, -88f);

        CreateLeftStoryCard(contentGo.transform, font);
        CreateRightMenuCard(contentGo.transform, font);

        // フッター情報（バージョン ＆ 操作ガイド）
        var footLeft = MakeText(_modalBoard.transform, "FootLeft", new Vector2(30f, 12f), new Vector2(0f, 0f), new Vector2(400f, 20f), 12, TextAnchor.LowerLeft, font);
        footLeft.color = new Color(0.65f, 0.78f, 0.90f, 0.75f);
        footLeft.text = "【マウス / 1〜4】選択　【ESC / 3】設定";

        var footRight = MakeText(_modalBoard.transform, "FootRight", new Vector2(-30f, 12f), new Vector2(1f, 0f), new Vector2(400f, 20f), 12, TextAnchor.LowerRight, font);
        footRight.color = new Color(0.50f, 0.75f, 0.85f, 0.75f);
        footRight.text = "✦ Rust & Float Project  v1.0.0";

        // New Game 確認モーダル
        CreateNewGameConfirmModal(_canvasGo.transform, font);
    }

    void CreateLeftStoryCard(Transform parent, Font font)
    {
        var cardGo = new GameObject("LeftStoryCard");
        cardGo.transform.SetParent(parent, false);
        var cRt = cardGo.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0f, 0f);
        cRt.anchorMax = new Vector2(0.48f, 1f);
        cRt.offsetMin = Vector2.zero;
        cRt.offsetMax = Vector2.zero;

        var cBg = cardGo.AddComponent<Image>();
        cBg.color = new Color(0.015f, 0.04f, 0.08f, 0.92f);
        cBg.raycastTarget = false;

        var cOutline = cardGo.AddComponent<Outline>();
        cOutline.effectColor = new Color(0.20f, 0.50f, 0.75f, 0.35f);
        cOutline.effectDistance = new Vector2(1f, -1f);

        var textPanelGo = new GameObject("StoryTextPanel");
        textPanelGo.transform.SetParent(cardGo.transform, false);
        var textPanelRt = textPanelGo.AddComponent<RectTransform>();
        textPanelRt.anchorMin = Vector2.zero;
        textPanelRt.anchorMax = Vector2.one;
        textPanelRt.offsetMin = new Vector2(8f, 8f);
        textPanelRt.offsetMax = new Vector2(-8f, -8f);
        var textPanelBg = textPanelGo.AddComponent<Image>();
        textPanelBg.color = new Color(0.01f, 0.03f, 0.06f, 0.97f);
        textPanelBg.raycastTarget = false;
        textPanelGo.AddComponent<RectMask2D>();

        _storyHeaderText = MakeText(textPanelGo.transform, "StoryHeader", Vector2.zero, new Vector2(0f, 1f), new Vector2(100f, 24f), AdventureSpeechBoardLayout.TitleMenuStoryHeaderSize, TextAnchor.UpperLeft, font);
        _storyHeaderText.fontStyle = FontStyle.Bold;
        _storyHeaderText.color = new Color(0.40f, 0.88f, 1.0f, 1f);
        _storyHeaderText.text = "📜 時代背景と物語";

        _storyBodyText = MakeText(textPanelGo.transform, "StoryBody", Vector2.zero, new Vector2(0f, 1f), new Vector2(100f, 100f), AdventureSpeechBoardLayout.TitleMenuStoryBodySize, TextAnchor.UpperLeft, font);
        _storyBodyText.fontStyle = FontStyle.Bold;
        _storyBodyText.color = new Color(0.95f, 0.97f, 1.0f, 0.95f);
        var bodyOutline = _storyBodyText.gameObject.AddComponent<Outline>();
        bodyOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        bodyOutline.effectDistance = new Vector2(1f, -1f);
        _storyBodyText.text = StoryText;

        EnsureTitleStoryPanelLayout(textPanelRt);
    }

    static void ConfigureTitleStoryHeader(Text text)
    {
        if (text == null) return;
        var lang = AdventureLocalizationManager.CurrentLanguage;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        if (lang == AdventureLocalizationManager.GameLanguage.Japanese)
        {
            text.resizeTextForBestFit = false;
            text.fontSize = AdventureSpeechBoardLayout.TitleMenuStoryHeaderSize;
            return;
        }

        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = AdventureSpeechBoardLayout.TitleMenuStoryHeaderMinSize;
        text.resizeTextMaxSize = AdventureSpeechBoardLayout.TitleMenuStoryHeaderSize;
    }

    static void ConfigureTitleStoryBody(Text text)
    {
        if (text == null) return;
        var lang = AdventureLocalizationManager.CurrentLanguage;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.lineSpacing = AdventureSpeechBoardLayout.GetTitleMenuStoryLineSpacing(lang);

        if (AdventureSpeechBoardLayout.TitleMenuStoryBodyUsesBestFit(lang))
        {
            int max = AdventureSpeechBoardLayout.GetTitleMenuStoryBodyMaxSize(lang);
            text.resizeTextForBestFit = true;
            text.fontSize = max;
            text.resizeTextMinSize = AdventureSpeechBoardLayout.TitleMenuStoryBodyMinSize;
            text.resizeTextMaxSize = max;
            return;
        }

        text.resizeTextForBestFit = false;
        text.fontSize = AdventureSpeechBoardLayout.GetTitleMenuStoryBodyFixedSize(lang);
    }

    static void LayoutTitleStoryPanel(RectTransform panel, Text header, Text body)
    {
        if (panel == null || header == null || body == null) return;

        float pad = AdventureSpeechBoardLayout.TitleMenuStoryPanelPadding;
        float headerBand = AdventureSpeechBoardLayout.TitleMenuStoryHeaderBandHeight;

        var headerRt = header.rectTransform;
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = Vector2.zero;
        headerRt.offsetMin = new Vector2(pad, -headerBand);
        headerRt.offsetMax = new Vector2(-pad, -pad * 0.5f);

        var bodyRt = body.rectTransform;
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.pivot = new Vector2(0.5f, 1f);
        bodyRt.anchoredPosition = Vector2.zero;
        bodyRt.offsetMin = new Vector2(pad, pad);
        bodyRt.offsetMax = new Vector2(-pad, -(headerBand + pad * 0.5f));
    }

    void EnsureTitleStoryPanelLayout(RectTransform textPanelRt)
    {
        if (_storyHeaderText == null || _storyBodyText == null) return;

        if (textPanelRt != null && textPanelRt.GetComponent<RectMask2D>() == null)
            textPanelRt.gameObject.AddComponent<RectMask2D>();

        ConfigureTitleStoryHeader(_storyHeaderText);
        ConfigureTitleStoryBody(_storyBodyText);
        LayoutTitleStoryPanel(textPanelRt, _storyHeaderText, _storyBodyText);
    }

    void ApplyTitleStoryTextFit()
    {
        if (_storyBodyText == null) return;

        var panel = _storyBodyText.transform.parent as RectTransform;
        EnsureTitleStoryPanelLayout(panel);
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>左カードは物語テキスト。背景は CurrentSceneCapture の全画面表示。</summary>
    void EnsureTitleStoryTextVisible()
    {
        if (_storyHeaderText != null)
            _storyHeaderText.gameObject.SetActive(true);
        if (_storyBodyText != null)
            _storyBodyText.gameObject.SetActive(true);

        if (_modalBoard == null)
            return;

        var left = _modalBoard.transform.Find("ContentColumns/LeftStoryCard");
        if (left == null)
            return;

        var legacyPreview = left.Find("PreStartPreview");
        if (legacyPreview != null)
            Destroy(legacyPreview.gameObject);

        var storyPanel = left.Find("StoryTextPanel") as RectTransform;
        if (storyPanel != null)
            EnsureTitleStoryPanelLayout(storyPanel);
    }

    void CreatePreStartBackground()
    {
        _preStartTexture = LoadPreStartCaptureTexture();
        if (_preStartTexture == null)
            return;

        _preStartImageRoot = new GameObject("PreStartCaptureBackground");
        _preStartImageRoot.transform.SetParent(_canvasGo.transform, false);
        _preStartImageRoot.transform.SetAsFirstSibling();

        var rt = _preStartImageRoot.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _preStartImage = _preStartImageRoot.AddComponent<RawImage>();
        _preStartImage.texture = _preStartTexture;
        _preStartImage.raycastTarget = false;
        _preStartImage.color = Color.white;

        var fitter = _preStartImageRoot.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = (float)_preStartTexture.width / Mathf.Max(1, _preStartTexture.height);
    }

    void SetPreStartImageVisible(bool visible)
    {
        if (_preStartImageRoot != null)
            _preStartImageRoot.SetActive(visible);
    }

    static Texture2D LoadPreStartCaptureTexture()
    {
        string diskPath = System.IO.Path.GetFullPath(PreStartCaptureAssetPath);
        if (System.IO.File.Exists(diskPath))
        {
            byte[] bytes = System.IO.File.ReadAllBytes(diskPath);
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (tex.LoadImage(bytes))
            {
                tex.name = "CurrentSceneCapture_Disk";
                return tex;
            }
        }
#if UNITY_EDITOR
        var editorTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(PreStartCaptureAssetPath);
        if (editorTex != null)
            return editorTex;
#endif
        return Resources.Load<Texture2D>(PreStartCaptureResourcesPath);
    }

    void CreateRightMenuCard(Transform parent, Font font)
    {
        var cardGo = new GameObject("RightMenuCard");
        cardGo.transform.SetParent(parent, false);
        var cRt = cardGo.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.52f, 0f);
        cRt.anchorMax = new Vector2(1f, 1f);
        cRt.offsetMin = Vector2.zero;
        cRt.offsetMax = Vector2.zero;

        var cBg = cardGo.AddComponent<Image>();
        cBg.color = new Color(0.015f, 0.04f, 0.08f, 0.92f);
        cBg.raycastTarget = false;

        var cOutline = cardGo.AddComponent<Outline>();
        cOutline.effectColor = new Color(0.20f, 0.50f, 0.75f, 0.35f);
        cOutline.effectDistance = new Vector2(1f, -1f);

        _menuHeaderText = MakeText(cardGo.transform, "MenuHeader", new Vector2(0f, -12f), new Vector2(0.5f, 1f), new Vector2(320f, 26f), 14, TextAnchor.MiddleCenter, font);
        _menuHeaderText.fontStyle = FontStyle.Bold;
        _menuHeaderText.color = new Color(0.40f, 0.88f, 1.0f, 1f);
        _menuHeaderText.text = "✦ メインメニュー";

        float btnW = 340f;
        float startY = -48f;

        // 1. 【つづきから（Continue）】
        _continueBtn = CreateMenuButton(cardGo.transform, "ContinueBtn", new Vector2(0f, startY), new Vector2(btnW, 58f),
            new Color(0.12f, 0.58f, 0.72f, 0.96f), new Color(0.25f, 0.78f, 0.95f, 1f),
            "▶  つづきから", AdventureSpeechBoardLayout.TitleMenuButtonPrimarySize, OnContinueClicked, font, out _continueBtnImg, out _continueBtnLabel);

        _continueSubText = MakeText(_continueBtn.transform, "SubInfo", new Vector2(0f, -34f), new Vector2(0.5f, 1f), new Vector2(btnW - 20f, 18f), 11, TextAnchor.MiddleCenter, font);
        _continueSubText.color = new Color(0.45f, 0.95f, 0.85f, 0.95f);

        // 2. 【はじめから（New Game）】
        _newGameBtn = CreateMenuButton(cardGo.transform, "NewGameBtn", new Vector2(0f, startY - 72f), new Vector2(btnW, 52f),
            new Color(0.15f, 0.45f, 0.32f, 0.96f), new Color(0.25f, 0.70f, 0.48f, 1f),
            "✦  はじめから", AdventureSpeechBoardLayout.TitleMenuButtonPrimarySize, OnNewGameClicked, font, out _, out _newGameBtnLabel);

        // 3. 【設定（Settings）】
        _settingsBtn = CreateMenuButton(cardGo.transform, "SettingsBtn", new Vector2(0f, startY - 138f), new Vector2(btnW, 46f),
            new Color(0.18f, 0.32f, 0.50f, 0.96f), new Color(0.30f, 0.52f, 0.75f, 1f),
            "⚙  環境設定", AdventureSpeechBoardLayout.TitleMenuButtonSecondarySize, OnSettingsClicked, font, out _, out _settingsBtnLabel);

        // 4. 【ゲーム終了（Quit）】
        _quitBtn = CreateMenuButton(cardGo.transform, "QuitBtn", new Vector2(0f, startY - 200f), new Vector2(btnW, 44f),
            new Color(0.38f, 0.16f, 0.18f, 0.95f), new Color(0.60f, 0.22f, 0.25f, 1f),
            "✖  ゲーム終了", AdventureSpeechBoardLayout.TitleMenuButtonTertiarySize, OnQuitClicked, font, out _, out _quitBtnLabel);
    }

    Button CreateMenuButton(Transform parent, string name, Vector2 pos, Vector2 size, Color normalCol, Color highlightCol,
        string label, int fontSize, UnityEngine.Events.UnityAction onClick, Font font, out Image outImg, out Text outLabel)
    {
        var btnGo = new GameObject(name);
        btnGo.transform.SetParent(parent, false);
        var bRt = btnGo.AddComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.5f, 1f);
        bRt.anchorMax = new Vector2(0.5f, 1f);
        bRt.pivot = new Vector2(0.5f, 1f);
        bRt.anchoredPosition = pos;
        bRt.sizeDelta = size;

        outImg = btnGo.AddComponent<Image>();
        outImg.color = normalCol;
        outImg.raycastTarget = true;

        var outline = btnGo.AddComponent<Outline>();
        outline.effectColor = new Color(highlightCol.r, highlightCol.g, highlightCol.b, 0.65f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var btn = btnGo.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = normalCol;
        colors.highlightedColor = highlightCol;
        colors.pressedColor = normalCol * 0.8f;
        colors.disabledColor = new Color(0.1f, 0.15f, 0.2f, 0.5f);
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        outLabel = MakeText(btnGo.transform, "Label", Vector2.zero, new Vector2(0.5f, 0.5f), size, fontSize, TextAnchor.MiddleCenter, font);
        outLabel.fontStyle = FontStyle.Bold;
        outLabel.color = Color.white;
        var lOutline = outLabel.gameObject.AddComponent<Outline>();
        lOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        lOutline.effectDistance = new Vector2(1f, -1f);
        outLabel.text = label;

        return btn;
    }

    void CreateNewGameConfirmModal(Transform parent, Font font)
    {
        _newGameConfirmModal = new GameObject("NewGameConfirmModal");
        _newGameConfirmModal.transform.SetParent(parent, false);
        var mRt = _newGameConfirmModal.AddComponent<RectTransform>();
        mRt.anchorMin = Vector2.zero;
        mRt.anchorMax = Vector2.one;
        mRt.offsetMin = Vector2.zero;
        mRt.offsetMax = Vector2.zero;

        var mBg = _newGameConfirmModal.AddComponent<Image>();
        mBg.color = new Color(0f, 0f, 0f, 0.85f);

        // 中央カード
        var cardGo = new GameObject("Card");
        cardGo.transform.SetParent(_newGameConfirmModal.transform, false);
        var cRt = cardGo.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.5f, 0.5f);
        cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.sizeDelta = new Vector2(520f, 260f);
        cRt.anchoredPosition = Vector2.zero;

        var cBg = cardGo.AddComponent<Image>();
        cBg.color = new Color(0.04f, 0.08f, 0.14f, 0.98f);
        var cOutline = cardGo.AddComponent<Outline>();
        cOutline.effectColor = new Color(0.95f, 0.65f, 0.25f, 0.85f);
        cOutline.effectDistance = new Vector2(2f, -2f);

        // タイトル
        _confirmModalTitle = MakeText(cardGo.transform, "Title", new Vector2(0f, -24f), new Vector2(0.5f, 1f), new Vector2(480f, 32f), 20, TextAnchor.MiddleCenter, font);
        _confirmModalTitle.fontStyle = FontStyle.Bold;
        _confirmModalTitle.color = new Color(1f, 0.75f, 0.35f, 1f);
        _confirmModalTitle.text = AdventureLocalizationManager.Get("confirm_overwrite_title");

        // 説明文
        _confirmModalDesc = MakeText(cardGo.transform, "Desc", new Vector2(0f, -68f), new Vector2(0.5f, 1f), new Vector2(440f, 80f), 14, TextAnchor.MiddleCenter, font);
        _confirmModalDesc.lineSpacing = 1.35f;
        _confirmModalDesc.color = new Color(0.95f, 0.95f, 1.0f, 0.95f);
        _confirmModalDesc.text = AdventureLocalizationManager.Get("confirm_overwrite_desc");

        // ボタン領域
        // 1. 「はい、新しくはじめる」
        _confirmNewGameYesBtn = CreateMenuButton(cardGo.transform, "YesBtn", new Vector2(-120f, 22f), new Vector2(210f, 44f),
            new Color(0.75f, 0.25f, 0.20f, 1f), new Color(0.95f, 0.35f, 0.30f, 1f),
            AdventureLocalizationManager.Get("confirm_overwrite_yes"), 15, OnConfirmNewGameYes, font, out _, out _confirmModalYesBtnLabel);
        var yesBtn = _confirmNewGameYesBtn;
        var yRt = yesBtn.GetComponent<RectTransform>();
        yRt.anchorMin = new Vector2(0.5f, 0f);
        yRt.anchorMax = new Vector2(0.5f, 0f);
        yRt.pivot = new Vector2(0.5f, 0f);
        yRt.anchoredPosition = new Vector2(-115f, 22f);

        // 2. 「キャンセル」
        _confirmNewGameNoBtn = CreateMenuButton(cardGo.transform, "CancelBtn", new Vector2(120f, 22f), new Vector2(180f, 44f),
            new Color(0.20f, 0.35f, 0.50f, 1f), new Color(0.30f, 0.50f, 0.70f, 1f),
            AdventureLocalizationManager.Get("confirm_cancel"), 15, OnConfirmNewGameNo, font, out _, out _confirmModalNoBtnLabel);
        var cancelBtn = _confirmNewGameNoBtn;
        var cBtnRt = cancelBtn.GetComponent<RectTransform>();
        cBtnRt.anchorMin = new Vector2(0.5f, 0f);
        cBtnRt.anchorMax = new Vector2(0.5f, 0f);
        cBtnRt.pivot = new Vector2(0.5f, 0f);
        cBtnRt.anchoredPosition = new Vector2(115f, 22f);

        _newGameConfirmModal.SetActive(false);
    }

    static Text MakeText(Transform parent, string name, Vector2 pos, Vector2 anchor, Vector2 size, int fontSize, TextAnchor align, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = align;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    static Font ResolveFont()
    {
        string[] candidates =
        {
            "Hiragino Sans",
            "PingFang SC",
            "Hiragino Sans GB",
            "Heiti SC",
            "Hiragino Kaku Gothic ProN",
            "Apple SD Gothic Neo",
            "YuGothic",
            "Noto Sans CJK JP",
            "Microsoft YaHei",
            "Arial Unicode MS"
        };
        try
        {
            Font multi = Font.CreateDynamicFontFromOSFont(candidates, 20);
            if (multi != null) return multi;
        }
        catch { }

        foreach (string name in candidates)
        {
            try
            {
                Font os = Font.CreateDynamicFontFromOSFont(name, 20);
                if (os != null) return os;
            }
            catch { }
        }

        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f != null) return f;
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
