using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 漂着パーツ収集カウンターおよびアップグレード通知HUD
/// 没入感を損なわないスマート・オートハイド設計（普段は完全非表示、取得時のみスッと数秒表示）
/// </summary>
public class AdventureScrapHUD : MonoBehaviour
{
    static AdventureScrapHUD _instance;
    public static AdventureScrapHUD Instance => _instance;

    int _count = 0;
    int _total = 12;

    Canvas _canvas;
    Text _labelTitleText;
    Text _countValueText;
    RectTransform _counterPanelRt;
    CanvasGroup _counterCg;
    float _counterTimer = 2.8f; // 開始時に2.8秒だけ存在を伝えてスッとフェードアウト

    // アップグレード大バナー
    GameObject _bannerGo;
    Text _bannerText;
    CanvasGroup _bannerCg;
    float _bannerTimer = 0f;

    public static void Ensure()
    {
        var existingList = FindObjectsByType<AdventureScrapHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ex in existingList)
        {
            if (ex != null && ex.gameObject != null)
                Destroy(ex.gameObject);
        }
        _instance = null;

        var go = new GameObject("AdventureScrapHUD");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AdventureScrapHUD>();
    }

    void Awake()
    {
        _instance = this;
        CreateCanvasUI();
    }

    void CreateCanvasUI()
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

        Font uiFont = ResolveFont();

        // 1. スリム・オートハイドカウンター（画面上部中央：普段は非表示、取得時のみスッと現れる）
        var counterPanel = new GameObject("CounterPanel");
        counterPanel.transform.SetParent(canvasGo.transform, false);
        _counterPanelRt = counterPanel.AddComponent<RectTransform>();
        _counterPanelRt.anchorMin = new Vector2(0.5f, 1f);
        _counterPanelRt.anchorMax = new Vector2(0.5f, 1f);
        _counterPanelRt.pivot = new Vector2(0.5f, 1f);
        _counterPanelRt.anchoredPosition = new Vector2(0f, -48f);
        _counterPanelRt.sizeDelta = new Vector2(230f, 34f);

        var panelBg = counterPanel.AddComponent<Image>();
        panelBg.color = new Color(0.06f, 0.10f, 0.16f, 0.88f);

        _counterCg = counterPanel.AddComponent<CanvasGroup>();
        _counterCg.alpha = 1.0f; // 開始直後は表示され、2.8秒後に自然に消える

        // 左端のシアンアクセントバー
        var barGo = new GameObject("AccentBar");
        barGo.transform.SetParent(counterPanel.transform, false);
        var barRt = barGo.AddComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0f, 0f);
        barRt.anchorMax = new Vector2(0f, 1f);
        barRt.pivot = new Vector2(0f, 0.5f);
        barRt.anchoredPosition = Vector2.zero;
        barRt.sizeDelta = new Vector2(4f, 0f);
        var barImg = barGo.AddComponent<Image>();
        barImg.color = new Color(0.2f, 0.88f, 1.0f, 1.0f);

        // 左側：タイトルテキスト「⚙ 漂着遺物」
        var titleGo = new GameObject("TitleText");
        titleGo.transform.SetParent(counterPanel.transform, false);
        var titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0f);
        titleRt.anchorMax = new Vector2(0.58f, 1f);
        titleRt.pivot = new Vector2(0f, 0.5f);
        titleRt.anchoredPosition = new Vector2(12f, 0f);
        titleRt.sizeDelta = new Vector2(-12f, 0f);

        _labelTitleText = titleGo.AddComponent<Text>();
        _labelTitleText.font = uiFont;
        _labelTitleText.fontSize = 15;
        _labelTitleText.fontStyle = FontStyle.Bold;
        _labelTitleText.alignment = TextAnchor.MiddleLeft;
        _labelTitleText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _labelTitleText.verticalOverflow = VerticalWrapMode.Overflow;
        _labelTitleText.color = new Color(0.92f, 0.96f, 1.0f);
        _labelTitleText.text = "⚙ 漂着遺物";

        // 右側：数値バッジ背景
        var badgeGo = new GameObject("CountBadge");
        badgeGo.transform.SetParent(counterPanel.transform, false);
        var badgeRt = badgeGo.AddComponent<RectTransform>();
        badgeRt.anchorMin = new Vector2(0.58f, 0.12f);
        badgeRt.anchorMax = new Vector2(0.96f, 0.88f);
        badgeRt.sizeDelta = Vector2.zero;
        badgeRt.anchoredPosition = Vector2.zero;

        var badgeBg = badgeGo.AddComponent<Image>();
        badgeBg.color = new Color(0.12f, 0.18f, 0.28f, 0.85f);

        // 右側：数値テキスト「0 / 12」
        var countGo = new GameObject("CountText");
        countGo.transform.SetParent(badgeGo.transform, false);
        var countRt = countGo.AddComponent<RectTransform>();
        countRt.anchorMin = Vector2.zero;
        countRt.anchorMax = Vector2.one;
        countRt.sizeDelta = Vector2.zero;
        countRt.anchoredPosition = Vector2.zero;

        _countValueText = countGo.AddComponent<Text>();
        _countValueText.font = uiFont;
        _countValueText.fontSize = 16;
        _countValueText.fontStyle = FontStyle.Bold;
        _countValueText.alignment = TextAnchor.MiddleCenter;
        _countValueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _countValueText.verticalOverflow = VerticalWrapMode.Overflow;
        _countValueText.color = new Color(1.0f, 0.88f, 0.38f);
        _countValueText.text = $"{_count} / {_total}";

        // 2. アップグレード達成通知バナー（画面中央上部：達成時のみ表示）
        _bannerGo = new GameObject("UpgradeBanner");
        _bannerGo.transform.SetParent(canvasGo.transform, false);
        var bannerRt = _bannerGo.AddComponent<RectTransform>();
        bannerRt.anchorMin = new Vector2(0.5f, 1.0f);
        bannerRt.anchorMax = new Vector2(0.5f, 1.0f);
        bannerRt.pivot = new Vector2(0.5f, 1.0f);
        bannerRt.anchoredPosition = new Vector2(0f, -92f);
        bannerRt.sizeDelta = new Vector2(560f, 85f);

        var bannerBg = _bannerGo.AddComponent<Image>();
        bannerBg.color = new Color(0.07f, 0.12f, 0.20f, 0.94f);
        _bannerCg = _bannerGo.AddComponent<CanvasGroup>();
        _bannerCg.alpha = 0f;

        var bTextGo = new GameObject("BannerText");
        bTextGo.transform.SetParent(_bannerGo.transform, false);
        var bTextRt = bTextGo.AddComponent<RectTransform>();
        bTextRt.anchorMin = Vector2.zero;
        bTextRt.anchorMax = Vector2.one;
        bTextRt.sizeDelta = new Vector2(-20f, -16f);

        _bannerText = bTextGo.AddComponent<Text>();
        _bannerText.font = uiFont;
        _bannerText.fontSize = 19;
        _bannerText.fontStyle = FontStyle.Bold;
        _bannerText.alignment = TextAnchor.MiddleCenter;
        _bannerText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _bannerText.verticalOverflow = VerticalWrapMode.Overflow;
        _bannerText.color = new Color(1.0f, 0.92f, 0.45f);
    }

    public void OnCollect(string itemName, int current, int total)
    {
        _count = current;
        _total = total;

        if (_countValueText != null)
        {
            _countValueText.text = $"{_count} / {_total}";
        }

        // 取得時にスッと表示し、3.5秒後に自動フェードアウト
        _counterTimer = 3.5f;

        // カウンターの強調パルスアニメーション
        if (_counterPanelRt != null)
        {
            _counterPanelRt.localScale = Vector3.one * 1.2f;
        }
    }

    public void ShowUpgradeBanner(string text)
    {
        if (_bannerText != null)
        {
            _bannerText.text = text;
            _bannerTimer = 5.0f;
        }
    }

    void Update()
    {
        // カウンターのスケール復帰
        if (_counterPanelRt != null && _counterPanelRt.localScale.x > 1.0f)
        {
            _counterPanelRt.localScale = Vector3.MoveTowards(_counterPanelRt.localScale, Vector3.one, Time.deltaTime * 1.5f);
        }

        // オートハイド（普段は非表示、必要な時だけスッと現れて自然に消える）
        if (_counterCg != null)
        {
            if (_counterTimer > 0f)
            {
                _counterTimer -= Time.deltaTime;
                _counterCg.alpha = Mathf.MoveTowards(_counterCg.alpha, 1.0f, Time.deltaTime * 4.0f);
            }
            else
            {
                _counterCg.alpha = Mathf.MoveTowards(_counterCg.alpha, 0.0f, Time.deltaTime * 2.0f);
            }
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

    static Font ResolveFont()
    {
        string[] fonts = {
            "Hiragino Sans",
            "Hiragino Kaku Gothic ProN",
            "Arial Unicode MS",
            "YuGothic",
            "Arial"
        };
        foreach (var name in fonts)
        {
            var f = Font.CreateDynamicFontFromOSFont(name, 18);
            if (f != null) return f;
        }
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
