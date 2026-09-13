using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 『Rust & Float』現在のクエスト目標（MISSION TRACKER）および遺物カウンターHUD
/// 画面左上に常に直感的で分かりやすいクエスト目標・進捗・最寄りパーツ方角を表示
/// 【Tab】キーで詳細表示 ⇄ ミニマル表示をスムーズに切り替え可能
/// </summary>
public class AdventureScrapHUD : MonoBehaviour
{
    static AdventureScrapHUD _instance;
    public static AdventureScrapHUD Instance => _instance;

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

    bool _isHidden = false;
    float _radarUpdateTimer = 0f;

    public static void Ensure()
    {
        var existingList = FindObjectsByType<AdventureScrapHUD>(FindObjectsInactive.Include);
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
        CreateUI();
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

        _font = ResolveFont();

        // ── 邪魔にならない極薄ミニマル1行クエストティッカー（画面上部コンパス直下・背景板なし） ──
        var panelGo = new GameObject("QuestTickerPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        _questPanelRt = panelGo.AddComponent<RectTransform>();
        _questPanelRt.anchorMin = new Vector2(0.5f, 1f);
        _questPanelRt.anchorMax = new Vector2(0.5f, 1f);
        _questPanelRt.pivot = new Vector2(0.5f, 1f);
        _questPanelRt.anchoredPosition = new Vector2(0f, -40f); // コンパスのすぐ下
        _questPanelRt.sizeDelta = new Vector2(620f, 26f);

        // 背景板（四角い枠）は全廃！景色を一切遮らない透明設計
        _questCg = panelGo.AddComponent<CanvasGroup>();
        _questCg.alpha = 0.85f;

        // 1行の統合クエストテキスト（フチ取り付きでどんな背景でも美しく可読）
        var textGo = new GameObject("TickerText");
        textGo.transform.SetParent(panelGo.transform, false);
        var tRt = textGo.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.sizeDelta = Vector2.zero;
        tRt.anchoredPosition = Vector2.zero;

        _tickerText = textGo.AddComponent<Text>();
        _tickerText.font = _font;
        _tickerText.fontSize = 13;
        _tickerText.fontStyle = FontStyle.Bold;
        _tickerText.alignment = TextAnchor.MiddleCenter;
        _tickerText.color = new Color(0.92f, 0.98f, 1.0f, 0.95f);
        _tickerText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _tickerText.verticalOverflow = VerticalWrapMode.Overflow;

        var outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.05f, 0.12f, 0.90f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);

        // ── 詩的ロア・アップグレードバナー（画面下部中央・映画のようなシネマティック表示） ──
        _bannerGo = new GameObject("PoeticLoreBanner");
        _bannerGo.transform.SetParent(canvasGo.transform, false);
        var bannerRt = _bannerGo.AddComponent<RectTransform>();
        bannerRt.anchorMin = new Vector2(0.5f, 0.0f);
        bannerRt.anchorMax = new Vector2(0.5f, 0.0f);
        bannerRt.pivot = new Vector2(0.5f, 0.0f);
        bannerRt.anchoredPosition = new Vector2(0f, 75f); // 画面下部、足元より少し上
        bannerRt.sizeDelta = new Vector2(720f, 105f);

        var bannerBg = _bannerGo.AddComponent<Image>();
        bannerBg.color = new Color(0.03f, 0.06f, 0.12f, 0.90f);
        var bOutline = _bannerGo.AddComponent<Outline>();
        bOutline.effectColor = new Color(0.35f, 0.85f, 0.95f, 0.6f);
        bOutline.effectDistance = new Vector2(1.2f, -1.2f);

        _bannerCg = _bannerGo.AddComponent<CanvasGroup>();
        _bannerCg.alpha = 0f;

        var bTextGo = new GameObject("BannerText");
        bTextGo.transform.SetParent(_bannerGo.transform, false);
        var bTextRt = bTextGo.AddComponent<RectTransform>();
        bTextRt.anchorMin = Vector2.zero;
        bTextRt.anchorMax = Vector2.one;
        bTextRt.sizeDelta = new Vector2(-28f, -14f);

        _bannerText = bTextGo.AddComponent<Text>();
        _bannerText.font = _font;
        _bannerText.fontSize = 15;
        _bannerText.lineSpacing = 1.25f;
        _bannerText.alignment = TextAnchor.MiddleCenter;
        _bannerText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _bannerText.verticalOverflow = VerticalWrapMode.Overflow;
        _bannerText.supportRichText = true;
        _bannerText.color = Color.white;

        RefreshQuestDisplay();
    }

    void Update()
    {
        // 【Tab】キーで表示 ⇄ 完全非表示を切り替え
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.tabKey.wasPressedThisFrame)
        {
            _isHidden = !_isHidden;
            if (_questCg != null)
                _questCg.alpha = _isHidden ? 0f : 0.85f;
        }

        if (_isHidden) return;

        // 定期的にクエスト進捗と最寄りパーツレーダーを更新
        _radarUpdateTimer -= Time.deltaTime;
        if (_radarUpdateTimer <= 0f)
        {
            _radarUpdateTimer = 0.4f;
            RefreshQuestDisplay();
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

    public void OnCollect(string itemName, int current, int total)
    {
        RefreshQuestDisplay();
    }

    public void ShowUpgradeBanner(string text)
    {
        if (_bannerText != null)
        {
            _bannerText.text = text;
            _bannerTimer = 6.0f;
        }
        RefreshQuestDisplay();
    }

    /// <summary>世界観仕様書に基づく詩的ロアモーダル（タイトル・詩的ナレーション・機能アンロック）を表示</summary>
    public void ShowPoeticLore(string title, string loreQuote, string unlockEffect)
    {
        if (_bannerText != null)
        {
            _bannerText.text = $"<color=#FFE066><b>✦ {title} ✦</b></color>\n" +
                               $"<color=#EAEFF5>「{loreQuote}」</color>\n" +
                               $"<color=#5CE1E6><b>▶ {unlockEffect}</b></color>";
            _bannerTimer = 8.0f; // じっくり味わえる8秒間表示
        }
        RefreshQuestDisplay();
    }

    /// <summary>画面上部コンパス直下に溶け込む、極薄1行のクエストティッカー</summary>
    void RefreshQuestDisplay()
    {
        if (_tickerText == null) return;

        var scrapMgr = AdventureScrapManager.Instance;
        int count = scrapMgr != null ? scrapMgr.CollectedCount : 0;
        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();

        // 最寄りパーツの方角
        string radarInfo = "";
        var player = AdventurePlayerController.Instance;
        if (player != null && scrapMgr != null && count < 12)
        {
            var nearest = scrapMgr.GetNearestScrapItem(player.transform.position, out float dist);
            if (nearest != null)
            {
                Vector3 diff = nearest.transform.position - player.transform.position;
                string dir = GetDirectionString(diff);
                radarInfo = $"　|　📍 最寄り: {dir} 約{Mathf.RoundToInt(dist)}m";
            }
        }

        if (count < 3)
        {
            string rustStatus = (drone != null && Time.time < drone.wellOiledUntil) ? "☑ Rust快調" : "【E】Rustに油をさす";
            _tickerText.text = $"✦ 目標: 漂着パーツ回収 ({count}/3)　〔{rustStatus}〕{radarInfo}";
        }
        else if (count < 6)
        {
            _tickerText.text = $"✦ 目標: 反重力コア回収 ({count}/6) ▶ 二段ジャンプ解放{radarInfo}";
        }
        else if (count < 9)
        {
            _tickerText.text = $"✦ 目標: 探知ソナー修復 ({count}/9) ▶ レーダー解放{radarInfo}";
        }
        else if (count < 12)
        {
            _tickerText.text = $"✦ 目標: スーパーグライダー完成 ({count}/12){radarInfo}";
        }
        else if (!AdventureSanctuaryTowerManager.IsCanopyBroken)
        {
            _tickerText.text = "✦ 全パーツ回収完了！島中央タワー頂上の【真鍮レバー】を引け！";
        }
        else
        {
            _tickerText.text = "✦ 天蓋崩壊！空の裂け目へ光のウインドピラーから大滑空ダイブせよ！";
        }
    }

    static string GetDirectionString(Vector3 diff)
    {
        float angle = Mathf.Atan2(diff.x, diff.z) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        if (angle >= 337.5f || angle < 22.5f) return "北（奥の高台）";
        if (angle >= 22.5f && angle < 67.5f) return "北東（丘陵地帯）";
        if (angle >= 67.5f && angle < 112.5f) return "東（右奥の林）";
        if (angle >= 112.5f && angle < 157.5f) return "南東（崖側）";
        if (angle >= 157.5f && angle < 202.5f) return "南（手前の浜辺）";
        if (angle >= 202.5f && angle < 247.5f) return "南西（浅瀬）";
        if (angle >= 247.5f && angle < 292.5f) return "西（海・オアシス）";
        return "北西（断崖）";
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
