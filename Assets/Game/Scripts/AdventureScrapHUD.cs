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

    // 所持潤滑油（常時表示）
    RectTransform _oilPanelRt;
    CanvasGroup _oilCg;
    Text _oilText;
    int _lastOilShown = int.MinValue;

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
        _questPanelRt.sizeDelta = new Vector2(460f, 58f); // 20ptポイント表示にも対応したゆったりサイズ

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

        CreateOilCounter(canvasGo.transform);

        RefreshQuestDisplay();
        RefreshOilDisplay(force: true);
    }

    void CreateOilCounter(Transform canvasRoot)
    {
        var panelGo = new GameObject("OilCounterPanel");
        panelGo.transform.SetParent(canvasRoot, false);
        _oilPanelRt = panelGo.AddComponent<RectTransform>();
        _oilPanelRt.anchorMin = new Vector2(1f, 1f);
        _oilPanelRt.anchorMax = new Vector2(1f, 1f);
        _oilPanelRt.pivot = new Vector2(1f, 1f);
        _oilPanelRt.anchoredPosition = new Vector2(-24f, -24f);
        _oilPanelRt.sizeDelta = new Vector2(210f, 48f);

        var panelBg = panelGo.AddComponent<Image>();
        panelBg.color = new Color(0.06f, 0.04f, 0.02f, 0.90f);
        panelBg.raycastTarget = false;

        var accentGo = new GameObject("OilAccent");
        accentGo.transform.SetParent(panelGo.transform, false);
        var accRt = accentGo.AddComponent<RectTransform>();
        accRt.anchorMin = new Vector2(0f, 0f);
        accRt.anchorMax = new Vector2(0f, 1f);
        accRt.pivot = new Vector2(0f, 0.5f);
        accRt.anchoredPosition = Vector2.zero;
        accRt.sizeDelta = new Vector2(3.5f, 0f);
        var accImg = accentGo.AddComponent<Image>();
        accImg.color = new Color(1f, 0.78f, 0.28f, 1f);
        accImg.raycastTarget = false;

        _oilCg = panelGo.AddComponent<CanvasGroup>();
        _oilCg.alpha = 1f;
        _oilCg.blocksRaycasts = false;
        _oilCg.interactable = false;

        var textGo = new GameObject("OilText");
        textGo.transform.SetParent(panelGo.transform, false);
        var tRt = textGo.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(14f, 4f);
        tRt.offsetMax = new Vector2(-10f, -4f);

        _oilText = textGo.AddComponent<Text>();
        _oilText.font = _font;
        _oilText.fontSize = 18;
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
        // 【Tab】キーでクエスト表示 ⇄ 非表示（油カウンターは常時表示のまま）
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.tabKey.wasPressedThisFrame)
        {
            _isHidden = !_isHidden;
            if (_questCg != null)
                _questCg.alpha = _isHidden ? 0f : 0.85f;
        }

        RefreshOilDisplay(force: false);

        if (_isHidden)
        {
            TickBannerOnly();
            return;
        }

        // 定期的にクエスト進捗と最寄りパーツレーダーを更新
        _radarUpdateTimer -= Time.deltaTime;
        if (_radarUpdateTimer <= 0f)
        {
            _radarUpdateTimer = 0.4f;
            RefreshQuestDisplay();
        }

        TickBannerOnly();
    }

    void TickBannerOnly()
    {
        // 天蓋破壊シネマティックストーリーボード表示中はバナーを即座に非表示
        if (AdventureSanctuaryTowerManager.Instance != null && AdventureSanctuaryTowerManager.Instance.IsSkybreakModalActive)
        {
            _bannerTimer = 0f;
            if (_bannerCg != null) _bannerCg.alpha = 0f;
            return;
        }

        // レバー操作中は下部バナーを出さない（最後のパーツ取得ロアがレバーボタンを塞ぐのを防ぐ）
        var towerNear = AdventureSanctuaryTowerManager.Instance;
        if (towerNear != null && towerNear.IsPlayerNearLever)
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

    void RefreshOilDisplay(bool force)
    {
        if (_oilText == null || _oilCg == null) return;

        var tower = AdventureSanctuaryTowerManager.Instance;
        bool cinematicHide = tower != null && (
            tower.IsSkybreakModalActive
            || tower.IsEpiloguePlaying
            || tower.ShowGameClearModal
            || tower.IsClimaxOilPromptActive);

        _oilCg.alpha = cinematicHide ? 0f : 1f;
        if (cinematicHide) return;

        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        int oil = drone != null ? Mathf.Max(0, drone.oilCount) : 0;
        if (!force && oil == _lastOilShown) return;
        _lastOilShown = oil;

        bool well = drone != null && Time.time < drone.wellOiledUntil;
        string state = well
            ? "<color=#A8FFB0>快調</color>"
            : "<color=#FFB070>手当て可</color>";
        _oilText.text = $"潤滑油  <color=#FFE066><b>{oil}</b></color>  {state}";
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
        // レバー前ではバナーを出さず、操作を塞がない
        var tower = AdventureSanctuaryTowerManager.Instance;
        if (tower != null && tower.IsPlayerNearLever)
            return;

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
    public void RefreshQuestDisplay()
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
        int totalPts = scrapMgr != null ? scrapMgr.TotalProgressPoints : 0;
        bool leverUnlocked = scrapMgr != null && scrapMgr.IsLeverUnlocked;

        // 1行目：現在のメイン目標
        string goalText;
        if (AdventureSanctuaryTowerManager.IsGameCleared)
            goalText = "<color=#FFE066><b>✦ GAME CLEAR！</b></color> <color=#FFFFFF>箱庭からの脱獄達成！</color>";
        else if (AdventureSanctuaryTowerManager.IsCanopyBroken)
            goalText = "<color=#FFD700><b>✦ 天蓋崩壊！</b></color> <color=#FFFFFF>光の柱から空の裂け目へダイブ！</color>";
        else if (leverUnlocked)
            goalText = $"<color=#00E5FF><b>✦ レバーロック解除！</b></color> <color=#FFFFFF>中央タワーの黄金レバーへ</color> <color=#FFE066><b>({totalPts}/20 pt)</b></color>";
        else if (count < 3)
            goalText = $"<color=#FFD54F><b>✦ 目標:</b></color> <color=#FFFFFF><b>パーツ回収</b></color> <color=#00E5FF><b>({count}/3)</b></color> <color=#FFE066><b>(計 {totalPts}/20 pt)</b></color>";
        else if (count < 6)
            goalText = $"<color=#FFD54F><b>✦ 目標:</b></color> <color=#FFFFFF><b>反重力コア</b></color> <color=#00E5FF><b>({count}/6)</b></color> <color=#FFD54F>▶ 2段ジャンプ</color> <color=#FFE066><b>({totalPts}/20 pt)</b></color>";
        else if (count < 9)
            goalText = $"<color=#FFD54F><b>✦ 目標:</b></color> <color=#FFFFFF><b>探知ソナー</b></color> <color=#00E5FF><b>({count}/9)</b></color> <color=#FFD54F>▶ レーダー</color> <color=#FFE066><b>({totalPts}/20 pt)</b></color>";
        else if (count < 12)
            goalText = $"<color=#FFD54F><b>✦ 目標:</b></color> <color=#FFFFFF><b>グライダー完成</b></color> <color=#00E5FF><b>({count}/12)</b></color> <color=#FFE066><b>({totalPts}/20 pt)</b></color>";
        else
            goalText = $"<color=#FFD54F><b>✦ 目標:</b></color> <color=#FFFFFF><b>ケースやピアノでポイント獲得</b></color> <color=#FFE066><b>({totalPts}/20 pt)</b></color>";

        // 2行目：最寄りパーツ探知（方角と距離）またはレバー誘導
        string subInfo = "";
        var player = AdventurePlayerController.Instance ?? FindAnyObjectByType<AdventurePlayerController>();
        if (player != null && !leverUnlocked && !AdventureSanctuaryTowerManager.IsCanopyBroken)
        {
            float dist = 0f;
            var nearest = scrapMgr != null ? scrapMgr.GetNearestScrapItem(player.transform.position, out dist) : null;
            if (nearest != null)
            {
                Vector3 diff = nearest.transform.position - player.transform.position;
                var (cardinal, hint) = GetDirectionParts(diff);
                subInfo = $"<color=#80D8FF><b>📍 最寄りパーツ:</b></color> <color=#FFEB3B><b>{cardinal}</b></color><color=#ECEFF1>（{hint}）</color> <color=#69F0AE><b>約{Mathf.RoundToInt(dist)}m</b></color>";
            }
            else
            {
                subInfo = "<color=#B0BEC5>📍 漂着パーツ(1pt)・ケース(2pt)・ピアノ(3pt)を集めよう！</color>";
            }
        }
        else if (leverUnlocked && !AdventureSanctuaryTowerManager.IsCanopyBroken)
        {
            var tower = AdventureSanctuaryTowerManager.Instance;
            if (tower != null && tower.IsPlayerNearLever)
            {
                subInfo = "<size=24><color=#FFE066><b>✨ 【Eキー】で巨大真鍮レバーを引く！</b></color></size> <color=#80D8FF>（天蓋開放開始）</color>";
            }
            else if (player != null)
            {
                Vector3 towerCenter = new Vector3(512f, 63.2f, 512f);
                Vector3 diff = towerCenter - player.transform.position;
                var (cardinal, hint) = GetDirectionParts(diff);
                float dist = Vector3.Distance(player.transform.position, towerCenter);
                subInfo = $"<color=#80D8FF><b>📍 レバー解除済:</b></color> <color=#FFEB3B><b>中央タワー白亜テラス（{cardinal}）</b></color> <color=#69F0AE><b>約{Mathf.RoundToInt(dist)}m</b></color> <color=#80D8FF>【レバーを引いて天蓋開放】</color>";
            }
            else
            {
                subInfo = "<color=#80D8FF><b>📍 目標地点:</b></color> <color=#FFEB3B><b>中央タワー白亜テラスの巨大真鍮レバー</b></color>";
            }
        }
        else if (count >= 12 && AdventureSanctuaryTowerManager.IsGameCleared)
        {
            Vector3 towerCenter = new Vector3(512f, 63.2f, 512f);
            if (player != null)
            {
                float dist = Vector3.Distance(player.transform.position, towerCenter);
                if (dist < 28f)
                {
                    subInfo = "<size=15><color=#FFE066><b>✨ 中央の光の柱へ！</b></color> <color=#80D8FF>（大空へ無限再跳躍できます）</color></size>";
                }
                else
                {
                    Vector3 diff = towerCenter - player.transform.position;
                    var (cardinal, hint) = GetDirectionParts(diff);
                    subInfo = $"<color=#80D8FF><b>📍 自由飛行探索中:</b></color> <color=#FFD700><b>タワー中心の光の柱（{cardinal} 約{Mathf.RoundToInt(dist)}m）で大空へ再ダイブ！</b></color>";
                }
            }
            else
            {
                subInfo = "<color=#80D8FF><b>📍 自由飛行探索中:</b></color> <color=#FFD700><b>タワー中心からいつでも大空へ再ダイブ！</b></color>";
            }
        }
        else if (count >= 12)
        {
            Vector3 towerCenter = new Vector3(512f, 63.2f, 512f);
            if (player != null)
            {
                float dist = Vector3.Distance(player.transform.position, towerCenter);
                if (dist < 28f)
                {
                    subInfo = "<size=15><color=#FFE066><b>✨ 中央の光の柱へ飛び込め！</b></color> <color=#80D8FF>（大空へ自動射出されます）</color></size>";
                }
                else
                {
                    Vector3 diff = towerCenter - player.transform.position;
                    var (cardinal, hint) = GetDirectionParts(diff);
                    subInfo = $"<color=#80D8FF><b>📍 目標:</b></color> <color=#FFD700><b>タワー中心の光の柱（{cardinal}）</b></color> <color=#69F0AE><b>約{Mathf.RoundToInt(dist)}m</b></color> <color=#80D8FF>【光の柱に入ると大空へ打ち上がります】</color>";
                }
            }
            else
            {
                subInfo = "<color=#80D8FF><b>📍 目標地点:</b></color> <color=#FFD700><b>タワー中心の光の柱</b></color>";
            }
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
