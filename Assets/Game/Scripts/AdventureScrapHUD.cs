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

    bool _isHidden = false;
    float _radarUpdateTimer = 0f;
    int _lastKnownCount = 0;

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

        // ── 画面最上部中央の水平リボンコンパスHUD（常時確実な起動を保証） ──
        AdventureCompassHUD.Ensure(canvasGo.transform, _font);

        // ── 画面左上の独立したクエスト＆最寄りパーツHUDカード（他UIと絶対に重ならない特等席） ──
        var panelGo = new GameObject("QuestTickerPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        _questPanelRt = panelGo.AddComponent<RectTransform>();
        _questPanelRt.anchorMin = new Vector2(0f, 1f);
        _questPanelRt.anchorMax = new Vector2(0f, 1f);
        _questPanelRt.pivot = new Vector2(0f, 1f);
        _questPanelRt.anchoredPosition = new Vector2(24f, -24f); // 画面左上にゆったり配置
        _questPanelRt.sizeDelta = new Vector2(390f, 56f);

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

        // ── 詩的ロア・アップグレードバナー（画面下部中央・映画のようなシネマティック表示） ──
        _bannerGo = new GameObject("PoeticLoreBanner");
        _bannerGo.transform.SetParent(canvasGo.transform, false);
        var bannerRt = _bannerGo.AddComponent<RectTransform>();
        bannerRt.anchorMin = new Vector2(0.5f, 0.0f);
        bannerRt.anchorMax = new Vector2(0.5f, 0.0f);
        bannerRt.pivot = new Vector2(0.5f, 0.0f);
        bannerRt.anchoredPosition = new Vector2(0f, 85f); // 画面下部、足元より少し上
        bannerRt.sizeDelta = new Vector2(1000f, 160f); // 1000pxのワイドな映画字幕ウィンドウ

        var bannerBg = _bannerGo.AddComponent<Image>();
        bannerBg.color = new Color(0.02f, 0.05f, 0.10f, 0.94f);
        var bOutline = _bannerGo.AddComponent<Outline>();
        bOutline.effectColor = new Color(0.35f, 0.85f, 0.95f, 0.8f);
        bOutline.effectDistance = new Vector2(1.8f, -1.8f);

        _bannerCg = _bannerGo.AddComponent<CanvasGroup>();
        _bannerCg.alpha = 0f;

        var bTextGo = new GameObject("BannerText");
        bTextGo.transform.SetParent(_bannerGo.transform, false);
        var bTextRt = bTextGo.AddComponent<RectTransform>();
        bTextRt.anchorMin = Vector2.zero;
        bTextRt.anchorMax = Vector2.one;
        bTextRt.sizeDelta = new Vector2(-36f, -18f);

        _bannerText = bTextGo.AddComponent<Text>();
        _bannerText.font = _font;
        _bannerText.fontSize = 22; // 15ptから22ptへ大幅拡大！
        _bannerText.lineSpacing = 1.30f;
        _bannerText.alignment = TextAnchor.MiddleCenter;
        _bannerText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _bannerText.verticalOverflow = VerticalWrapMode.Overflow;
        _bannerText.supportRichText = true;
        _bannerText.color = Color.white;

        var textOutline = bTextGo.AddComponent<Outline>();
        textOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        textOutline.effectDistance = new Vector2(1.5f, -1.5f);

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

        // 天蓋破壊シネマティックストーリーボード表示中はバナーを即座に非表示
        if (AdventureSanctuaryTowerManager.Instance != null && AdventureSanctuaryTowerManager.Instance.IsSkybreakModalActive)
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
            _bannerText.text = $"<size=26><color=#FFE066><b>✦ {title} ✦</b></color></size>\n" +
                               $"<size=20><color=#F0F4F8>「{loreQuote}」</color></size>\n" +
                               $"<size=22><color=#5CE1E6><b>▶ {unlockEffect}</b></color></size>";
            _bannerTimer = 11.0f; // 映画のようにじっくり味わえる11秒間表示
        }
        RefreshQuestDisplay();
    }

    /// <summary>画面左上の独立カードに収まる、美しく整理された2行クエスト表示</summary>
    void RefreshQuestDisplay()
    {
        if (_tickerText == null) return;

        var scrapMgr = AdventureScrapManager.Instance ?? FindAnyObjectByType<AdventureScrapManager>();
        int count = scrapMgr != null ? scrapMgr.CollectedCount : _lastKnownCount;
        if (count > _lastKnownCount)
        {
            _lastKnownCount = count;
        }
        else if (count == 0 && _lastKnownCount > 0)
        {
            // ドメインリロード等で一時的に0が返った場合の防壁
            count = _lastKnownCount;
        }

        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();

        // 1行目：現在のメイン目標
        string goalText;
        if (count < 3)
            goalText = $"<color=#FFD54F><b>✦ 目標:</b></color> <color=#FFFFFF><b>漂着パーツ回収</b></color> <color=#00E5FF><b>({count}/3)</b></color>";
        else if (count < 6)
            goalText = $"<color=#FFD54F><b>✦ 目標:</b></color> <color=#FFFFFF><b>反重力コア回収</b></color> <color=#00E5FF><b>({count}/6)</b></color> <color=#FFD54F>▶ 二段ジャンプ解放</color>";
        else if (count < 9)
            goalText = $"<color=#FFD54F><b>✦ 目標:</b></color> <color=#FFFFFF><b>探知ソナー修復</b></color> <color=#00E5FF><b>({count}/9)</b></color> <color=#FFD54F>▶ レーダー解放</color>";
        else if (count < 12)
            goalText = $"<color=#FFD54F><b>✦ 目標:</b></color> <color=#FFFFFF><b>スーパーグライダー完成</b></color> <color=#00E5FF><b>({count}/12)</b></color>";
        else if (!AdventureSanctuaryTowerManager.IsCanopyBroken)
            goalText = "<color=#00E5FF><b>✦ 全パーツ回収完了！</b></color> <color=#FFFFFF>中央タワーの黄金レバーへ</color>";
        else
            goalText = "<color=#FFD700><b>✦ 天蓋崩壊！</b></color> <color=#FFFFFF>光の柱から空の裂け目へダイブ！</color>";

        // 2行目：最寄りパーツ探知（方角と距離）
        string subInfo = "";
        var player = AdventurePlayerController.Instance ?? FindAnyObjectByType<AdventurePlayerController>();
        if (player != null && scrapMgr != null && count < 12)
        {
            var nearest = scrapMgr.GetNearestScrapItem(player.transform.position, out float dist);
            if (nearest != null)
            {
                Vector3 diff = nearest.transform.position - player.transform.position;
                var (cardinal, hint) = GetDirectionParts(diff);
                subInfo = $"<color=#80D8FF><b>📍 最寄り:</b></color> <color=#FFEB3B><b>{cardinal}</b></color><color=#ECEFF1>（{hint}）</color> <color=#69F0AE><b>約{Mathf.RoundToInt(dist)}m</b></color>";
            }
            else
            {
                subInfo = "<color=#B0BEC5>📍 最寄りのパーツを探知中…</color>";
            }
        }
        else if (count >= 12 && !AdventureSanctuaryTowerManager.IsCanopyBroken)
        {
            Vector3 leverPos = new Vector3(512f, 63.2f, 501.5f);
            if (player != null)
            {
                Vector3 diff = leverPos - player.transform.position;
                var (cardinal, hint) = GetDirectionParts(diff);
                float dist = Vector3.Distance(player.transform.position, leverPos);
                subInfo = $"<color=#80D8FF><b>📍 目標:</b></color> <color=#FFEB3B><b>中央タワー正面レバー（{cardinal}）</b></color> <color=#69F0AE><b>約{Mathf.RoundToInt(dist)}m</b></color>";
            }
            else
            {
                subInfo = "<color=#80D8FF><b>📍 目標地点:</b></color> <color=#FFEB3B><b>中央タワー正面広場の黄金レバー</b></color>";
            }
        }
        else if (count >= 12)
        {
            subInfo = "<color=#80D8FF><b>📍 目標地点:</b></color> <color=#FFD700><b>タワー中心の光の柱</b></color>";
        }

        _tickerText.text = $"{goalText}\n{subInfo}";
    }

    static (string cardinal, string hint) GetDirectionParts(Vector3 diff)
    {
        float angle = Mathf.Atan2(diff.x, diff.z) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        if (angle >= 337.5f || angle < 22.5f) return ("北", "奥の高台");
        if (angle >= 22.5f && angle < 67.5f) return ("北東", "丘陵地帯");
        if (angle >= 67.5f && angle < 112.5f) return ("東", "右奥の林");
        if (angle >= 112.5f && angle < 157.5f) return ("南東", "崖側");
        if (angle >= 157.5f && angle < 202.5f) return ("南", "手前の浜辺");
        if (angle >= 202.5f && angle < 247.5f) return ("南西", "浅瀬");
        if (angle >= 247.5f && angle < 292.5f) return ("西", "海・オアシス");
        return ("北西", "断崖");
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
