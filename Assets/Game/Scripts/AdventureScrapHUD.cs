using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 『Rust & Float』現在のクエスト目標（MISSION TRACKER）および遺物カウンターHUD
/// 常時: コンパス上の目標1行＋右上潤滑油。詳細ナビ（m・箱・塔）はコンパス1系統のみ（Tab 固定 / Q 後10秒）。
/// </summary>
public class AdventureScrapHUD : MonoBehaviour
{
    static AdventureScrapHUD _instance;
    public static AdventureScrapHUD Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = FindAnyObjectByType<AdventureScrapHUD>();
            return _instance;
        }
    }

    Canvas _canvas;
    Font _font;

    // ミニマル1行クエストティッカー（画面上部中央・コンパス直下・背景枠なし）
    RectTransform _questPanelRt;
    CanvasGroup _questCg;
    Text _tickerText;

    // アップグレード大バナー
    GameObject _bannerGo;
    Text _bannerText;
    CanvasGroup _bannerCg;
    float _bannerTimer = 0f;

    // 所持潤滑油（常時表示）
    RectTransform _oilPanelRt;
    CanvasGroup _oilCg;
    Text _oilText;

    float _radarUpdateTimer = 0f;
    int _lastKnownCount = 0;
    bool _lastDetailNavVisible;

    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = Object.FindAnyObjectByType<AdventureScrapHUD>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("AdventureScrapHUD");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AdventureScrapHUD>();
    }

    /// <summary>ニューゲーム用：内部カウントを0に巻き戻し、HUD表示を即時更新</summary>
    public void ResetForNewGame()
    {
        _lastKnownCount = 0;
        AdventureExplorationHudDetail.ClearForNewGame();
        OnCollect("", 0, AdventureScrapManager.TotalScrapCount);
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        CreateUI();
    }

    void OnEnable()
    {
        AdventureStoryFlow.OnPhaseChanged += OnStoryPhaseChanged;
        AdventureScrapManager.OnProgressChanged += OnScrapProgressUpdated;
        AdventureLocalizationManager.OnLanguageChanged += OnLanguageChanged;
        AdventureExplorationHudSettings.OnChanged += RefreshExplorationHudVisibility;
        RefreshExplorationHudVisibility();
        _lastDetailNavVisible = ComputeDetailNavVisible();
    }

    void OnDisable()
    {
        AdventureStoryFlow.OnPhaseChanged -= OnStoryPhaseChanged;
        AdventureScrapManager.OnProgressChanged -= OnScrapProgressUpdated;
        AdventureLocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        AdventureExplorationHudSettings.OnChanged -= RefreshExplorationHudVisibility;
    }

    void OnLanguageChanged()
    {
        RefreshQuestDisplay();
        RefreshOilDisplay(force: true);
    }

    void OnStoryPhaseChanged(AdventureStoryFlow.Phase prev, AdventureStoryFlow.Phase next)
    {
        RefreshQuestDisplay();
        RefreshOilDisplay(force: true);
    }

    void OnScrapProgressUpdated()
    {
        RefreshQuestDisplay();
    }

    void CreateUI()
    {
        var canvasGo = new GameObject("ScrapHUD_Canvas");
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 95;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        _font = ResolveFont();

        // ── 画面最上部中央の水平リボンコンパスHUD（常時確実な起動を保証） ──
        AdventureCompassHUD.Ensure(canvasGo.transform, _font);

        // ── 画面左上の独立したクエスト＆最寄りパーツHUDカード（他UIと絶対に重ならない特等席） ──
        var panelGo = new GameObject("QuestTickerPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        _questPanelRt = panelGo.AddComponent<RectTransform>();
        _questPanelRt.anchorMin = new Vector2(0f, 1f);
        _questPanelRt.anchorMax = new Vector2(0f, 1f);
        _questPanelRt.pivot = new Vector2(0f, 1f);
        _questPanelRt.anchoredPosition = new Vector2(24f, AdventureExplorationHudSettings.GetPartsRowY(false));
        _questPanelRt.sizeDelta = new Vector2(420f, 58f);

        // どんな背景でもクッキリ読める半透明ダーク背景プレート
        var panelBg = panelGo.AddComponent<Image>();
        panelBg.color = new Color(0.02f, 0.05f, 0.10f, 0.92f);

        // 左端のアクセントライン（サイバーシアン光彩）
        var accentGo = new GameObject("LeftAccent");
        accentGo.transform.SetParent(panelGo.transform, false);
        var accRt = accentGo.AddComponent<RectTransform>();
        accRt.anchorMin = new Vector2(0f, 0f);
        accRt.anchorMax = new Vector2(0f, 1f);
        accRt.pivot = new Vector2(0f, 0.5f);
        accRt.anchoredPosition = Vector2.zero;
        accRt.sizeDelta = new Vector2(3.5f, 0f);
        var accImg = accentGo.AddComponent<Image>();
        accImg.color = new Color(0.35f, 0.85f, 1.0f, 1.0f);

        _questCg = panelGo.AddComponent<CanvasGroup>();
        _questCg.alpha = 0.98f;

        // 2行構成のクッキリしたクエストテキスト
        var textGo = new GameObject("TickerText");
        textGo.transform.SetParent(panelGo.transform, false);
        var tRt = textGo.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.sizeDelta = new Vector2(-24f, -8f);
        tRt.anchoredPosition = new Vector2(10f, 0f);

        _tickerText = textGo.AddComponent<Text>();
        _tickerText.font = _font;
        _tickerText.fontSize = 14;
        _tickerText.lineSpacing = 1.18f;
        _tickerText.alignment = TextAnchor.MiddleLeft;
        _tickerText.color = new Color(0.95f, 0.98f, 1.0f, 0.98f);
        _tickerText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _tickerText.verticalOverflow = VerticalWrapMode.Overflow;

        var outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.02f, 0.06f, 0.98f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // ── 詩的ロア・アップグレードバナー（画面中央上部・下部のキャラクターセリフと被らない独立領域） ──
        _bannerGo = new GameObject("PoeticLoreBanner");
        _bannerGo.transform.SetParent(canvasGo.transform, false);
        var bannerRt = _bannerGo.AddComponent<RectTransform>();
        bannerRt.anchorMin = new Vector2(0.5f, 1.0f);
        bannerRt.anchorMax = new Vector2(0.5f, 1.0f);
        bannerRt.pivot = new Vector2(0.5f, 1.0f);
        bannerRt.anchoredPosition = new Vector2(0f, AdventureExplorationHudSettings.GetBannerRowY(false));
        bannerRt.sizeDelta = new Vector2(AdventureSpeechBoardLayout.PoeticBannerW, AdventureSpeechBoardLayout.PoeticBannerH);

        var bannerBg = _bannerGo.AddComponent<Image>();
        bannerBg.color = new Color(0.02f, 0.05f, 0.10f, 0.62f);
        bannerBg.raycastTarget = false;
        var bOutline = _bannerGo.AddComponent<Outline>();
        bOutline.effectColor = new Color(0.35f, 0.85f, 0.95f, 0.55f);
        bOutline.effectDistance = new Vector2(1.8f, -1.8f);

        _bannerCg = _bannerGo.AddComponent<CanvasGroup>();
        _bannerCg.alpha = 0f;
        _bannerCg.blocksRaycasts = false;
        _bannerCg.interactable = false;

        var bTextGo = new GameObject("BannerText");
        bTextGo.transform.SetParent(_bannerGo.transform, false);
        var bTextRt = bTextGo.AddComponent<RectTransform>();
        bTextRt.anchorMin = Vector2.zero;
        bTextRt.anchorMax = Vector2.one;
        bTextRt.sizeDelta = new Vector2(-36f, -18f);

        _bannerText = bTextGo.AddComponent<Text>();
        _bannerText.font = _font;
        _bannerText.fontSize = AdventureSpeechBoardLayout.PoeticBannerFontSize;
        _bannerText.lineSpacing = 1.30f;
        _bannerText.alignment = TextAnchor.MiddleCenter;
        _bannerText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _bannerText.verticalOverflow = VerticalWrapMode.Overflow;
        _bannerText.supportRichText = true;
        _bannerText.color = Color.white;

        var textOutline = bTextGo.AddComponent<Outline>();
        textOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        textOutline.effectDistance = new Vector2(1.5f, -1.5f);

        CreateOilCounter(canvasGo.transform);

        ApplyLayoutPositions(ComputeDetailNavVisible());
        RefreshQuestDisplay();
        RefreshOilDisplay(force: true);
        RefreshExplorationHudVisibility();
    }

    public static bool ComputeDetailNavVisible()
    {
        if (!AdventureExplorationHudSettings.ShowCompass)
            return false;
        return AdventureExplorationHudDetail.IsDetailNavVisible();
    }

    public static bool ComputeCenterNavVisible() => ComputeDetailNavVisible();

    public void ApplyLayoutPositions(bool showDetailNavLine)
    {
        float rowY = AdventureExplorationHudSettings.GetSideHudRowY();
        if (_questPanelRt != null)
            _questPanelRt.gameObject.SetActive(false);
        if (_oilPanelRt != null)
            _oilPanelRt.anchoredPosition = new Vector2(-24f, rowY);
        if (_bannerGo != null)
        {
            var bannerRt = _bannerGo.GetComponent<RectTransform>();
            if (bannerRt != null)
                bannerRt.anchoredPosition = new Vector2(0f, AdventureExplorationHudSettings.GetBannerRowY(showDetailNavLine));
        }
    }

    /// <summary>設定・Tab・Q・シネマを反映（左上パーツ板は廃止、詳細はコンパス下1系統）</summary>
    public void RefreshExplorationHudVisibility()
    {
        bool cinematic = AdventureStoryFlow.HidesExplorationHud;
        bool showOil = !cinematic && AdventureExplorationHudSettings.ShowOilBoard;
        bool showCompass = !cinematic && AdventureExplorationHudSettings.ShowCompass;
        bool showDetail = showCompass && ComputeDetailNavVisible();

        if (_questPanelRt != null)
            _questPanelRt.gameObject.SetActive(false);
        if (_oilPanelRt != null)
            _oilPanelRt.gameObject.SetActive(showOil);

        var compass = AdventureCompassHUD.Instance
                      ?? Object.FindAnyObjectByType<AdventureCompassHUD>(FindObjectsInactive.Include);
        if (compass != null)
        {
            if (compass.gameObject.activeSelf != showCompass)
                compass.gameObject.SetActive(showCompass);
            compass.SetDetailNavVisible(showDetail);
        }

        ApplyLayoutPositions(showDetail);
        AdventureRustFloatOpening.ApplyTopHudGuideLayout(showDetail);
    }

    void CreateOilCounter(Transform canvasRoot)
    {
        var panelGo = new GameObject("OilCounterPanel");
        panelGo.transform.SetParent(canvasRoot, false);
        _oilPanelRt = panelGo.AddComponent<RectTransform>();
        _oilPanelRt.anchorMin = new Vector2(1f, 1f);
        _oilPanelRt.anchorMax = new Vector2(1f, 1f);
        _oilPanelRt.pivot = new Vector2(1f, 1f);
        _oilPanelRt.anchoredPosition = new Vector2(-24f, AdventureExplorationHudSettings.GetPartsRowY(false));
        _oilPanelRt.sizeDelta = new Vector2(245f, 48f);

        var panelBg = panelGo.AddComponent<Image>();
        panelBg.color = new Color(0.06f, 0.04f, 0.02f, 0.90f);
        panelBg.raycastTarget = true;

        var btn = panelGo.AddComponent<Button>();
        btn.targetGraphic = panelBg;
        var colors = btn.colors;
        colors.normalColor = new Color(0.06f, 0.04f, 0.02f, 0.90f);
        colors.highlightedColor = new Color(0.18f, 0.12f, 0.05f, 0.98f);
        colors.pressedColor = new Color(0.28f, 0.18f, 0.08f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(OnOilPanelClicked);

        var accentGo = new GameObject("OilAccent");
        accentGo.transform.SetParent(panelGo.transform, false);
        var accRt = accentGo.AddComponent<RectTransform>();
        accRt.anchorMin = new Vector2(0f, 0f);
        accRt.anchorMax = new Vector2(0f, 1f);
        accRt.pivot = new Vector2(0f, 0.5f);
        accRt.anchoredPosition = Vector2.zero;
        accRt.sizeDelta = new Vector2(4.0f, 0f);
        var accImg = accentGo.AddComponent<Image>();
        accImg.color = new Color(1f, 0.78f, 0.28f, 1f);
        accImg.raycastTarget = false;

        _oilCg = panelGo.AddComponent<CanvasGroup>();
        _oilCg.alpha = 1f;
        _oilCg.blocksRaycasts = true;
        _oilCg.interactable = true;

        var textGo = new GameObject("OilText");
        textGo.transform.SetParent(panelGo.transform, false);
        var tRt = textGo.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(14f, 2f);
        tRt.offsetMax = new Vector2(-6f, -2f);

        _oilText = textGo.AddComponent<Text>();
        _oilText.font = _font;
        _oilText.fontSize = 17;
        _oilText.fontStyle = FontStyle.Bold;
        _oilText.alignment = TextAnchor.MiddleLeft;
        _oilText.supportRichText = true;
        _oilText.color = new Color(1f, 0.92f, 0.55f, 1f);
        _oilText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _oilText.verticalOverflow = VerticalWrapMode.Overflow;
        _oilText.raycastTarget = false;

        var outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0.05f, 0.02f, 0f, 0.95f);
        outline.effectDistance = new Vector2(1.4f, -1.4f);
    }

    void Update()
    {
        if (AdventureStoryFlow.HidesExplorationHud)
        {
            if (_questCg != null) _questCg.alpha = 0f;
            if (_oilCg != null) _oilCg.alpha = 0f;
            if (_bannerCg != null) _bannerCg.alpha = 0f;
            RefreshExplorationHudVisibility();
            return;
        }

        // 【Tab】詳細ナビ行（最寄り m 等）の固定表示 ON/OFF
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.tabKey.wasPressedThisFrame)
            AdventureExplorationHudDetail.ToggleTabPin();

        // 右上潤滑油パネルの直接クリック検知（カーソル表示時／uGUI補完）
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && _oilPanelRt != null && _oilCg != null && _oilCg.alpha > 0.5f)
        {
            Vector2 mPos = mouse.position.ReadValue();
            if (RectTransformUtility.RectangleContainsScreenPoint(_oilPanelRt, mPos, null))
            {
                OnOilPanelClicked();
            }
        }

        RefreshOilDisplay(force: false);

        bool detailNow = ComputeDetailNavVisible();
        if (detailNow != _lastDetailNavVisible)
        {
            _lastDetailNavVisible = detailNow;
            RefreshExplorationHudVisibility();
        }

        _radarUpdateTimer -= Time.deltaTime;
        if (_radarUpdateTimer <= 0f)
        {
            _radarUpdateTimer = 0.5f;
            RefreshQuestDisplay();
        }

        TickBannerOnly();
    }

    void TickBannerOnly()
    {
        // 天蓋破壊シネマティックストーリーボード表示中はバナーを即座に非表示
        if (AdventureStoryFlow.HidesBottomBanner)
        {
            _bannerTimer = 0f;
            if (_bannerCg != null) _bannerCg.alpha = 0f;
            return;
        }

        // アップグレードバナーのフェード制御
        if (_bannerTimer > 0f)
        {
            _bannerTimer -= Time.deltaTime;
            if (_bannerCg != null)
                _bannerCg.alpha = Mathf.MoveTowards(_bannerCg.alpha, 1.0f, Time.deltaTime * 3.5f);
        }
        else
        {
            if (_bannerCg != null)
                _bannerCg.alpha = Mathf.MoveTowards(_bannerCg.alpha, 0.0f, Time.deltaTime * 2.0f);
        }
    }

    string _lastFormattedOilText = "";

    void RefreshOilDisplay(bool force)
    {
        if (_oilText == null || _oilCg == null) return;

        bool cinematicHide = AdventureStoryFlow.HidesOilHud;
        bool userHide = !AdventureExplorationHudSettings.ShowOilBoard;

        _oilCg.alpha = (cinematicHide || userHide) ? 0f : 1f;
        if (cinematicHide || userHide) return;

        var drone = AdventureRustDrone.Instance ?? Object.FindAnyObjectByType<AdventureRustDrone>();
        int oil = drone != null ? Mathf.Max(0, drone.oilCount) : 0;

        bool well = drone != null && Time.time < drone.wellOiledUntil;
        string state;
        if (well)
        {
            int remSec = Mathf.CeilToInt(drone.wellOiledUntil - Time.time);
            state = $"<color=#A8FFB0>{string.Format(AdventureLocalizationManager.Get("hud_oil_well"), remSec)}</color>";
        }
        else
        {
            state = $"<color=#FFB070>{AdventureLocalizationManager.Get("hud_oil_cure_hint")}</color>";
        }

        string formatted = $"{AdventureLocalizationManager.Get("hud_oil_label")}  <color=#FFE066><b>{oil}</b></color>  {state}";
        if (!force && formatted == _lastFormattedOilText) return;
        _lastFormattedOilText = formatted;
        _oilText.text = formatted;
    }

    /// <summary>右上潤滑油パネルクリック時：ダイレクトにRustへ手当て＆全快調化</summary>
    public void OnOilPanelClicked()
    {
        var drone = AdventureRustDrone.Instance ?? Object.FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            drone.InteractWithNiko();
            RefreshOilDisplay(force: true);
            AdventureNotificationToast.Show(AdventureLocalizationManager.Get("hud_oil_cured_toast"), 2.2f);
        }
    }

    /// <summary>シネマティックストーリーボード表示時などにHUDバナーを即座に消去</summary>
    public void HideBannerImmediately()
    {
        _bannerTimer = 0f;
        if (_bannerCg != null)
        {
            _bannerCg.alpha = 0f;
        }
    }

    public void OnCollect(string itemName, int current, int total)
    {
        RefreshQuestDisplay();
    }

    public void ShowUpgradeBanner(string text)
    {
        _bannerTimer = 0f;
        if (_bannerCg != null) _bannerCg.alpha = 0f;

        if (string.IsNullOrWhiteSpace(text))
            return;

        string loc = AdventureLocalizationManager.NormalizeLineBreaks(AdventureLocalizationManager.Get(text));
        if (string.IsNullOrEmpty(loc)) loc = text;
        AdventureSceneCaption.Show("", loc, null,
            AdventureSpeechBoardLayout.ScrapUpgradeBannerSeconds, waitDismiss: false);
        RefreshQuestDisplay();
    }

    /// <summary>世界観仕様書に基づく詩的ロアモーダル（タイトル・詩的ナレーション・機能アンロック）を表示</summary>
    public void ShowPoeticLore(string title, string loreQuote, string unlockEffect)
    {
        // レバー前ではバナーを出さず、操作を塞がない
        var tower = AdventureSanctuaryTowerManager.Instance;
        if (tower != null && tower.IsPlayerNearLever)
            return;

        string locTitle = AdventureLocalizationManager.NormalizeLineBreaks(AdventureLocalizationManager.Get(title));
        if (string.IsNullOrEmpty(locTitle)) locTitle = title;
        string locQuote = AdventureLocalizationManager.NormalizeLineBreaks(AdventureLocalizationManager.Get(loreQuote));
        if (string.IsNullOrEmpty(locQuote)) locQuote = loreQuote;
        string locEffect = AdventureLocalizationManager.NormalizeLineBreaks(AdventureLocalizationManager.Get(unlockEffect));
        if (string.IsNullOrEmpty(locEffect)) locEffect = unlockEffect;

        if (string.IsNullOrWhiteSpace(locTitle)
            && string.IsNullOrWhiteSpace(locQuote)
            && string.IsNullOrWhiteSpace(locEffect))
        {
            _bannerTimer = 0f;
            if (_bannerCg != null) _bannerCg.alpha = 0f;
            return;
        }

        string quoteL = AdventureLocalizationManager.CurrentLanguage switch
        {
            AdventureLocalizationManager.GameLanguage.English => "\"",
            AdventureLocalizationManager.GameLanguage.Spanish => "«",
            _ => "「"
        };
        string quoteR = AdventureLocalizationManager.CurrentLanguage switch
        {
            AdventureLocalizationManager.GameLanguage.English => "\"",
            AdventureLocalizationManager.GameLanguage.Spanish => "»",
            _ => "」"
        };

        _bannerTimer = 0f;
        if (_bannerCg != null) _bannerCg.alpha = 0f;

        string body = $"{quoteL}{locQuote}{quoteR}\n▶ {locEffect}";
        AdventureSceneCaption.Show(locTitle, body, null,
            AdventureSpeechBoardLayout.PoeticBannerSeconds, waitDismiss: false);
        RefreshQuestDisplay();
    }

    /// <summary>常時: コンパス直下の目標1行のみ更新（詳細 m 等はコンパス詳細行）</summary>
    public void RefreshQuestDisplay()
    {
        var scrapMgr = AdventureScrapManager.Instance;
        int count = scrapMgr != null ? scrapMgr.CollectedCount : _lastKnownCount;
        if (scrapMgr != null && scrapMgr.CollectedCount == 0)
        {
            _lastKnownCount = 0;
            count = 0;
        }
        else if (count > _lastKnownCount)
        {
            _lastKnownCount = count;
        }
        else if (count == 0 && _lastKnownCount > 0)
        {
            // ドメインリロード等で一時的にscrapMgrがnullまたは未初期化の場合の防壁
            count = _lastKnownCount;
        }

        string goalText = BuildMissionGoalLine(scrapMgr, count);
        Font questFont = ResolveFont();
        PrepareHudText(questFont, goalText, 11);

        var compass = AdventureCompassHUD.Instance ?? Object.FindAnyObjectByType<AdventureCompassHUD>(FindObjectsInactive.Include);
        compass?.SetMissionGoalLine(goalText);
    }

    public static string BuildMissionGoalLine(AdventureScrapManager scrapMgr, int count)
    {
        int totalPts = scrapMgr != null ? scrapMgr.TotalProgressPoints : 0;
        bool leverUnlocked = scrapMgr != null && scrapMgr.IsLeverUnlocked;

        if (AdventureSanctuaryTowerManager.IsGameCleared)
            return AdventureLocalizationManager.Get("hud_goal_cleared");
        if (AdventureSanctuaryTowerManager.IsCanopyBroken)
            return AdventureLocalizationManager.Get("hud_goal_canopy");
        if (leverUnlocked)
            return string.Format(AdventureLocalizationManager.Get("hud_goal_lever"), totalPts);
        if (count < 3)
            return string.Format(AdventureLocalizationManager.Get("hud_goal_scrap_3"), count, totalPts);
        if (count < 6)
            return string.Format(AdventureLocalizationManager.Get("hud_goal_scrap_6"), count, totalPts);
        if (count < 9)
            return string.Format(AdventureLocalizationManager.Get("hud_goal_scrap_9"), count, totalPts);
        if (count < 12)
            return string.Format(AdventureLocalizationManager.Get("hud_goal_scrap_12"), count, totalPts);
        return string.Format(AdventureLocalizationManager.Get("hud_goal_scrap_more"), totalPts);
    }

    static void PrepareHudText(Font font, string text, int fontSize)
    {
        if (font == null || string.IsNullOrEmpty(text)) return;
        font.RequestCharactersInTexture(text, fontSize, FontStyle.Normal);
        font.RequestCharactersInTexture(text, fontSize, FontStyle.Bold);
    }

    static Font _cachedHudFont;

    static Font ResolveFont()
    {
        if (_cachedHudFont != null) return _cachedHudFont;
        string[] fonts = {
            "PingFang SC",
            "Hiragino Sans GB",
            "Heiti SC",
            "Hiragino Sans",
            "Hiragino Kaku Gothic ProN",
            "Microsoft YaHei",
            "Arial Unicode MS",
            "YuGothic",
            "Apple SD Gothic Neo",
            "Arial"
        };
        try
        {
            _cachedHudFont = Font.CreateDynamicFontFromOSFont(fonts, 18);
            if (_cachedHudFont != null) return _cachedHudFont;
        }
        catch { }
        foreach (var name in fonts)
        {
            var f = Font.CreateDynamicFontFromOSFont(name, 18);
            if (f != null)
            {
                _cachedHudFont = f;
                return f;
            }
        }
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
               ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
