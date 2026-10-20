using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 『Rust & Float』専用の水平リボンコンパスHUD（DeltaAngle方式・完全シームレス・パーツマーカー連動）
/// プレイヤーの追従カメラと100%完全同期し、360度どこを向いても滑らかに方角・角度・パーツ位置を案内する
/// </summary>
[DefaultExecutionOrder(50)] // CameraFollow(0)の後に方位を読む
public class AdventureCompassHUD : MonoBehaviour
{
    static AdventureCompassHUD _instance;
    public static AdventureCompassHUD Instance => _instance;

    Camera _cam;
    Text _headingBadgeText;
    Text _scrapNavText;
    RectTransform _ribbonContainer;
    RectTransform _scrapMarkerRt;
    Text _scrapMarkerText;
    RectTransform _boxMarkerRt;
    Text _boxMarkerText;
    RectTransform _towerMarkerRt;
    Text _towerMarkerText;

    const float PixelsPerDegree = 2.05f; // 1度あたりのピクセル幅（表示視野角 約±88度）
    const float RibbonHalfWidth = 180f;
    const float YawSmoothTime = 0.045f;
    const float BearingSmoothTime = 0.05f;
    const float OnRibbonLimit = 78f;

    float _displayYaw;
    float _displayYawVel;
    float _scrapBearing;
    float _scrapBearingVel;
    float _boxBearing;
    float _boxBearingVel;
    float _towerBearing;
    float _towerBearingVel;
    bool _yawInitialized;
    bool _scrapBearingInit;
    bool _boxBearingInit;
    bool _towerBearingInit;
    int _scrapHint;
    int _boxHint;
    int _shownDeg = int.MinValue;
    float _targetSearchCooldown = 0f;
    enum TargetMode { None, Tower, Scrap, Crystal, Terrace }
    TargetMode _cachedTargetMode = TargetMode.None;
    Vector3 _cachedTargetPos;
    string _cachedTargetName = "";
    Color _cachedTargetColor = Color.white;
    bool _cachedHasTarget = false;
    bool _cachedIsGuidingToTower = false;
    bool _cachedIsGuidingToCrystal = false;
    bool _cachedIsGuidingToTerrace = false;
    AdventureScrapManager _cachedScrapMgr;
    AdventurePlayerController _cachedPlayer;

    // Float（相棒ドローン）の道案内Pingシステム
    Vector3 _guidePingPos;
    string _guidePingName = "";
    Color _guidePingColor = Color.yellow;
    float _guidePingTimer = 0f;
    bool _hasGuidePing = false;

    /// <summary>相棒FloatがQキーで案内した目標をコンパス上で一定時間パルス強調表示する</summary>
    public void TriggerGuidePing(Vector3 worldPos, string name, Color color, float duration = 8.5f)
    {
        _guidePingPos = worldPos;
        _guidePingName = name;
        _guidePingColor = color;
        _guidePingTimer = duration;
        _hasGuidePing = true;
    }

    [System.Serializable]
    public struct CompassElement
    {
        public float TargetAngle;
        public RectTransform Rt;
        public Text Txt;
    }

    [SerializeField] List<CompassElement> _elements = new List<CompassElement>();

    /// <summary>ドメインリロードや再コンパイル後でもコンパス目盛り要素を確実に復元・再バインドする</summary>
    void EnsureElementsReady()
    {
        if (_elements.Count > 0 && _elements[0].Rt != null) return;

        _elements.Clear();
        if (_ribbonContainer == null)
        {
            var rc = transform.Find("CompassRibbon");
            if (rc != null) _ribbonContainer = rc.GetComponent<RectTransform>();
        }

        if (_ribbonContainer == null) return;

        string[] cardinals = { "北", "北東", "東", "南東", "南", "南西", "西", "北西" };
        int[] angles = { 0, 45, 90, 135, 180, 225, 270, 315 };

        for (int i = 0; i < cardinals.Length; i++)
        {
            var t = _ribbonContainer.Find("Card_" + cardinals[i]);
            if (t != null)
            {
                var rt = t.GetComponent<RectTransform>();
                var txt = t.GetComponent<Text>();
                _elements.Add(new CompassElement { TargetAngle = angles[i], Rt = rt, Txt = txt });
            }
            for (int sub = 1; sub < 3; sub++)
            {
                float subAngle = (angles[i] + sub * 15f) % 360f;
                var dt = _ribbonContainer.Find("Tick_" + subAngle);
                if (dt != null)
                {
                    var dRt = dt.GetComponent<RectTransform>();
                    var dTxt = dt.GetComponent<Text>();
                    _elements.Add(new CompassElement { TargetAngle = subAngle, Rt = dRt, Txt = dTxt });
                }
            }
        }

        if (_elements.Count == 0)
        {
            BuildUI(ResolveSafeFont());
        }
    }



    void UpdateRibbonCardinals()
    {
        if (_elements == null || _elements.Count == 0) return;
        string[] currentCardinals = AdventureLocalizationManager.CurrentLanguage switch
        {
            AdventureLocalizationManager.GameLanguage.English => new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" },
            AdventureLocalizationManager.GameLanguage.SimplifiedChinese => new[] { "北", "东北", "东", "东南", "南", "西南", "西", "西北" },
            AdventureLocalizationManager.GameLanguage.Spanish => new[] { "N", "NE", "E", "SE", "S", "SO", "O", "NO" },
            _ => new[] { "北", "北東", "東", "南東", "南", "南西", "西", "北西" }
        };

        int cardIdx = 0;
        for (int i = 0; i < _elements.Count; i++)
        {
            var elem = _elements[i];
            if (elem.Rt != null && elem.Rt.name.StartsWith("Card_"))
            {
                if (cardIdx < currentCardinals.Length && elem.Txt != null)
                {
                    elem.Txt.text = currentCardinals[cardIdx];
                }
                cardIdx++;
            }
        }
    }

    /// <summary>コンパスHUDがシーン内に確実に存在することを保証する</summary>
    public static AdventureCompassHUD Ensure(Transform parent = null, Font font = null)
    {
        // 二重生成を掃除して1つだけ残す
        var all = Object.FindObjectsByType<AdventureCompassHUD>(FindObjectsInactive.Include);
        AdventureCompassHUD keep = _instance;
        if (keep == null || keep.gameObject == null)
            keep = all.Length > 0 ? all[0] : null;

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || all[i] == keep)
                continue;
            Object.Destroy(all[i].gameObject);
        }

        if (keep != null && keep.gameObject != null)
        {
            _instance = keep;
            if (!keep.gameObject.activeSelf)
                keep.gameObject.SetActive(true);
            return _instance;
        }

        if (parent == null)
        {
            var scrapHud = AdventureScrapHUD.Instance ?? FindAnyObjectByType<AdventureScrapHUD>();
            if (scrapHud != null)
            {
                var canvas = scrapHud.GetComponentInChildren<Canvas>();
                if (canvas != null) parent = canvas.transform;
            }
        }

        if (parent == null)
        {
            var canvasGo = GameObject.Find("RustFloatHUD") ?? GameObject.Find("ScrapHUD_Canvas");
            if (canvasGo != null) parent = canvasGo.transform;
        }

        return Create(parent, font);
    }

    public static AdventureCompassHUD Create(Transform parent, Font font = null)
    {
        // 既存があれば新規を作らず再利用
        var existing = Object.FindObjectsByType<AdventureCompassHUD>(FindObjectsInactive.Include);
        if (existing.Length > 0)
            return Ensure(parent, font);

        if (font == null) font = ResolveSafeFont();

        var hudGo = new GameObject("CompassHUD", typeof(RectTransform));
        if (parent != null)
            hudGo.transform.SetParent(parent, false);

        var hud = hudGo.AddComponent<AdventureCompassHUD>();
        hud.BuildUI(font);
        _instance = hud;
        return hud;
    }

    void Awake()
    {
        _instance = this;
    }

    void OnEnable()
    {
        AdventureStoryFlow.OnPhaseChanged += HandleStoryPhaseChanged;
        AdventureLocalizationManager.OnLanguageChanged += UpdateRibbonCardinals;
        UpdateRibbonCardinals();
    }

    void OnDisable()
    {
        AdventureStoryFlow.OnPhaseChanged -= HandleStoryPhaseChanged;
        AdventureLocalizationManager.OnLanguageChanged -= UpdateRibbonCardinals;
    }

    void HandleStoryPhaseChanged(AdventureStoryFlow.Phase prev, AdventureStoryFlow.Phase next)
    {
        bool hide = AdventureStoryFlow.HidesExplorationHud;
        if (_headingBadgeText != null) _headingBadgeText.enabled = !hide;
        if (_scrapNavText != null) _scrapNavText.enabled = !hide;
        if (_ribbonContainer != null) _ribbonContainer.gameObject.SetActive(!hide);
        if (_scrapMarkerRt != null) _scrapMarkerRt.gameObject.SetActive(!hide);
        if (_boxMarkerRt != null) _boxMarkerRt.gameObject.SetActive(!hide);
    }

    void BuildUI(Font font)
    {
        // 1. コンパス外枠ルート（画面最上部中央）
        var rootRt = GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 1f);
        rootRt.anchorMax = new Vector2(0.5f, 1f);
        rootRt.pivot = new Vector2(0.5f, 1f);
        rootRt.anchoredPosition = new Vector2(0f, -10f);
        rootRt.sizeDelta = new Vector2(420f, 34f);

        // 背景プレート（半透明の深藍ダークグラデーション）
        var bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.06f, 0.11f, 0.88f);
        bg.raycastTarget = false;

        // 上部ライン（サイバーグロー）
        var borderTop = new GameObject("BorderTop", typeof(RectTransform));
        borderTop.transform.SetParent(transform, false);
        var topRt = borderTop.GetComponent<RectTransform>();
        topRt.anchorMin = new Vector2(0f, 1f);
        topRt.anchorMax = new Vector2(1f, 1f);
        topRt.pivot = new Vector2(0.5f, 1f);
        topRt.anchoredPosition = Vector2.zero;
        topRt.sizeDelta = new Vector2(0f, 1.2f);
        var topImg = borderTop.AddComponent<Image>();
        topImg.color = new Color(0.35f, 0.85f, 1.0f, 0.45f);

        // 下部ライン（爽やかなシアンの光彩）
        var borderBottom = new GameObject("BorderBottom", typeof(RectTransform));
        borderBottom.transform.SetParent(transform, false);
        var bRt = borderBottom.GetComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0f, 0f);
        bRt.anchorMax = new Vector2(1f, 0f);
        bRt.pivot = new Vector2(0.5f, 0f);
        bRt.anchoredPosition = Vector2.zero;
        bRt.sizeDelta = new Vector2(0f, 1.8f);
        var bImg = borderBottom.AddComponent<Image>();
        bImg.color = new Color(0.35f, 0.90f, 1.0f, 0.75f);

        // コンパステープ。端はマスクで切り、目盛りはポップせず流れる
        var ribbonGo = new GameObject("CompassRibbon", typeof(RectTransform));
        ribbonGo.transform.SetParent(transform, false);
        _ribbonContainer = ribbonGo.GetComponent<RectTransform>();
        _ribbonContainer.anchorMin = new Vector2(0.5f, 0.5f);
        _ribbonContainer.anchorMax = new Vector2(0.5f, 0.5f);
        _ribbonContainer.pivot = new Vector2(0.5f, 0.5f);
        _ribbonContainer.anchoredPosition = Vector2.zero;
        _ribbonContainer.sizeDelta = new Vector2(360f, 28f);
        ribbonGo.AddComponent<RectMask2D>();

        // 方角マーカー（8方位：0°, 45°, 90°, 135°, 180°, 225°, 270°, 315°）
        string[] cardinals = { "北", "北東", "東", "南東", "南", "南西", "西", "北西" };
        int[] angles = { 0, 45, 90, 135, 180, 225, 270, 315 };

        for (int i = 0; i < cardinals.Length; i++)
        {
            var elemGo = new GameObject("Card_" + cardinals[i], typeof(RectTransform));
            elemGo.transform.SetParent(_ribbonContainer, false);
            var eRt = elemGo.GetComponent<RectTransform>();
            eRt.sizeDelta = new Vector2(44f, 26f);

            var txt = elemGo.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = cardinals[i].Length == 1 ? 14 : 11;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = cardinals[i];
            txt.raycastTarget = false;

            if (cardinals[i] == "北")
                txt.color = new Color(0.20f, 0.95f, 1.0f, 1.0f); // 北は鮮やかなシアン発光
            else if (cardinals[i].Length == 1)
                txt.color = new Color(0.95f, 0.98f, 1.0f, 0.92f); // 東、南、西
            else
                txt.color = new Color(0.72f, 0.84f, 0.94f, 0.70f); // 北東、南東、南西、北西

            var outline = elemGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0.02f, 0.06f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);

            _elements.Add(new CompassElement { TargetAngle = angles[i], Rt = eRt, Txt = txt });

            // 15度ごとの目盛りドット
            for (int sub = 1; sub < 3; sub++)
            {
                float subAngle = (angles[i] + sub * 15f) % 360f;
                var dotGo = new GameObject("Tick_" + subAngle, typeof(RectTransform));
                dotGo.transform.SetParent(_ribbonContainer, false);
                var dRt = dotGo.GetComponent<RectTransform>();
                dRt.sizeDelta = new Vector2(16f, 16f);

                var dTxt = dotGo.AddComponent<Text>();
                dTxt.font = font;
                dTxt.fontSize = 10;
                dTxt.alignment = TextAnchor.MiddleCenter;
                dTxt.text = "·";
                dTxt.color = new Color(0.60f, 0.78f, 0.90f, 0.55f);
                dTxt.raycastTarget = false;

                _elements.Add(new CompassElement { TargetAngle = subAngle, Rt = dRt, Txt = dTxt });
            }
        }

        // 2. コンパスリボン上に表示されるリアルタイム「パーツ探知マーカー（✦）」
        var smGo = new GameObject("CompassScrapMarker", typeof(RectTransform));
        smGo.transform.SetParent(_ribbonContainer, false);
        _scrapMarkerRt = smGo.GetComponent<RectTransform>();
        _scrapMarkerRt.sizeDelta = new Vector2(36f, 26f);
        _scrapMarkerText = smGo.AddComponent<Text>();
        _scrapMarkerText.font = font;
        _scrapMarkerText.fontSize = 15;
        _scrapMarkerText.fontStyle = FontStyle.Bold;
        _scrapMarkerText.alignment = TextAnchor.MiddleCenter;
        _scrapMarkerText.text = "✦";
        _scrapMarkerText.color = new Color(1.0f, 0.88f, 0.25f, 1f);
        _scrapMarkerText.raycastTarget = false;
        var smOutline = smGo.AddComponent<Outline>();
        smOutline.effectColor = new Color(0f, 0f, 0f, 0.98f);
        smOutline.effectDistance = new Vector2(1.2f, -1.2f);
        smGo.SetActive(false);

        // 2-B. コンパスリボン上に表示されるリアルタイム「漂着ボックス探知マーカー（📦）」
        var bmGo = new GameObject("CompassDriftBoxMarker", typeof(RectTransform));
        bmGo.transform.SetParent(_ribbonContainer, false);
        _boxMarkerRt = bmGo.GetComponent<RectTransform>();
        _boxMarkerRt.sizeDelta = new Vector2(36f, 26f);
        _boxMarkerText = bmGo.AddComponent<Text>();
        _boxMarkerText.font = font;
        _boxMarkerText.fontSize = 14;
        _boxMarkerText.fontStyle = FontStyle.Bold;
        _boxMarkerText.alignment = TextAnchor.MiddleCenter;
        _boxMarkerText.text = "📦";
        _boxMarkerText.color = new Color(0.35f, 1.0f, 0.65f, 1f); // 鮮やかなエメラルドグリーン
        _boxMarkerText.raycastTarget = false;
        var bmOutline = bmGo.AddComponent<Outline>();
        bmOutline.effectColor = new Color(0f, 0f, 0f, 0.98f);
        bmOutline.effectDistance = new Vector2(1.2f, -1.2f);
        // 2-C. コンパスリボン上に常時表示される「中央・白亜聖域タワーマーカー（🏛️）」
        var tmGo = new GameObject("CompassSanctuaryTowerMarker", typeof(RectTransform));
        tmGo.transform.SetParent(_ribbonContainer, false);
        _towerMarkerRt = tmGo.GetComponent<RectTransform>();
        _towerMarkerRt.sizeDelta = new Vector2(36f, 26f);
        _towerMarkerText = tmGo.AddComponent<Text>();
        _towerMarkerText.font = font;
        _towerMarkerText.fontSize = 14;
        _towerMarkerText.fontStyle = FontStyle.Bold;
        _towerMarkerText.alignment = TextAnchor.MiddleCenter;
        _towerMarkerText.text = "🏛️";
        _towerMarkerText.color = new Color(0.40f, 0.95f, 1.0f, 0.95f); // 聖なるシアン発光
        _towerMarkerText.raycastTarget = false;
        var tmOutline = tmGo.AddComponent<Outline>();
        tmOutline.effectColor = new Color(0f, 0f, 0f, 0.98f);
        tmOutline.effectDistance = new Vector2(1.2f, -1.2f);
        tmGo.SetActive(false);

        // 3. 中央インジケーター（▼ マーカー）
        var needleGo = new GameObject("CompassNeedle", typeof(RectTransform));
        needleGo.transform.SetParent(transform, false);
        var nRt = needleGo.GetComponent<RectTransform>();
        nRt.anchorMin = new Vector2(0.5f, 1f);
        nRt.anchorMax = new Vector2(0.5f, 1f);
        nRt.pivot = new Vector2(0.5f, 1f);
        nRt.anchoredPosition = new Vector2(0f, 1f);
        nRt.sizeDelta = new Vector2(24f, 16f);
        var nTxt = needleGo.AddComponent<Text>();
        nTxt.font = font;
        nTxt.fontSize = 12;
        nTxt.fontStyle = FontStyle.Bold;
        nTxt.alignment = TextAnchor.UpperCenter;
        nTxt.text = "▼";
        nTxt.color = new Color(0.20f, 0.95f, 1.0f, 0.98f);
        nTxt.raycastTarget = false;
        var nOutline = needleGo.AddComponent<Outline>();
        nOutline.effectColor = new Color(0f, 0f, 0f, 0.90f);
        nOutline.effectDistance = new Vector2(1f, -1f);

        // 4. 中央下部の方角・角度デジタルバッジ（例: "北  15°"）
        var badgeGo = new GameObject("HeadingBadge", typeof(RectTransform));
        badgeGo.transform.SetParent(transform, false);
        var bBadgeRt = badgeGo.GetComponent<RectTransform>();
        bBadgeRt.anchorMin = new Vector2(0.5f, 0f);
        bBadgeRt.anchorMax = new Vector2(0.5f, 0f);
        bBadgeRt.pivot = new Vector2(0.5f, 1f);
        bBadgeRt.anchoredPosition = new Vector2(0f, -4f);
        bBadgeRt.sizeDelta = new Vector2(160f, 18f);

        _headingBadgeText = badgeGo.AddComponent<Text>();
        _headingBadgeText.font = font;
        _headingBadgeText.fontSize = 12;
        _headingBadgeText.fontStyle = FontStyle.Bold;
        _headingBadgeText.alignment = TextAnchor.MiddleCenter;
        _headingBadgeText.color = new Color(0.88f, 0.96f, 1.0f, 0.92f);
        _headingBadgeText.text = "北  0°";
        _headingBadgeText.raycastTarget = false;
        var badgeOutline = badgeGo.AddComponent<Outline>();
        badgeOutline.effectColor = new Color(0f, 0f, 0f, 0.90f);
        badgeOutline.effectDistance = new Vector2(1f, -1f);

        // 5. 最寄り漂着パーツ・ボックスの方向・距離ナビゲーションバッジ（デジタルバッジの下に配置）
        var navGo = new GameObject("ScrapNavBadge", typeof(RectTransform));
        navGo.transform.SetParent(transform, false);
        var navRt = navGo.GetComponent<RectTransform>();
        navRt.anchorMin = new Vector2(0.5f, 0f);
        navRt.anchorMax = new Vector2(0.5f, 0f);
        navRt.pivot = new Vector2(0.5f, 1f);
        navRt.anchoredPosition = new Vector2(0f, -24f);
        navRt.sizeDelta = new Vector2(600f, 22f);

        _scrapNavText = navGo.AddComponent<Text>();
        _scrapNavText.font = font;
        _scrapNavText.fontSize = 12;
        _scrapNavText.fontStyle = FontStyle.Bold;
        _scrapNavText.alignment = TextAnchor.MiddleCenter;
        _scrapNavText.color = new Color(1.0f, 0.88f, 0.28f, 0.98f);
        _scrapNavText.text = "✦ 最寄りの漂着パーツを探知中…";
        _scrapNavText.raycastTarget = false;
        var navOutline = navGo.AddComponent<Outline>();
        navOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        navOutline.effectDistance = new Vector2(1f, -1f);
    }

    Camera GetActiveCamera()
    {
        // 1. プレイヤー追従カメラを最優先（プレイヤーの視点と完全同期）
        var follow = FindAnyObjectByType<AdventureCameraFollow>();
        if (follow != null)
        {
            var c = follow.GetComponent<Camera>() ?? follow.GetComponentInChildren<Camera>();
            if (c != null && c.isActiveAndEnabled)
            {
                _cam = c;
                return _cam;
            }
        }

        // 2. Camera.main
        if (Camera.main != null && Camera.main.isActiveAndEnabled)
        {
            _cam = Camera.main;
            return _cam;
        }

        // 3. 既存の有効カメラ
        if (_cam != null && _cam.isActiveAndEnabled)
            return _cam;

        // 4. シーン内のアクティブカメラ探索
        var cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
        foreach (var c in cams)
        {
            if (c.isActiveAndEnabled)
            {
                _cam = c;
                return _cam;
            }
        }

        return null;
    }

    void LateUpdate()
    {
        if (AdventureStoryFlow.HidesExplorationHud)
        {
            if (_headingBadgeText != null) _headingBadgeText.enabled = false;
            if (_scrapNavText != null) _scrapNavText.enabled = false;
            if (_ribbonContainer != null) _ribbonContainer.gameObject.SetActive(false);
            if (_scrapMarkerRt != null) _scrapMarkerRt.gameObject.SetActive(false);
            if (_boxMarkerRt != null) _boxMarkerRt.gameObject.SetActive(false);
            return;
        }
        if (_headingBadgeText != null) _headingBadgeText.enabled = true;
        if (_scrapNavText != null) _scrapNavText.enabled = true;
        if (_ribbonContainer != null && !_ribbonContainer.gameObject.activeSelf)
            _ribbonContainer.gameObject.SetActive(true);

        var cam = GetActiveCamera();
        float rawYaw = 0f;
        if (cam != null && cam.isActiveAndEnabled)
        {
            rawYaw = cam.transform.eulerAngles.y;
        }
        else
        {
            var follow = AdventureCameraFollow.Instance ?? FindAnyObjectByType<AdventureCameraFollow>();
            if (follow != null)
                rawYaw = follow.transform.eulerAngles.y;
            else if (AdventurePlayerController.Instance != null)
                rawYaw = AdventurePlayerController.Instance.transform.eulerAngles.y;
            else
                return;
        }

        // ヨー角を即座かつ滑らかに追従（SmoothDampAngleの0/360度境界スタックを完全根絶）
        float dt = Time.unscaledDeltaTime;
        if (!_yawInitialized || dt <= 0f)
        {
            _displayYaw = rawYaw;
            _yawInitialized = true;
        }
        else
        {
            _displayYaw = Mathf.MoveTowardsAngle(_displayYaw, rawYaw, 720f * dt);
        }
        float yaw = _displayYaw;

        // ドメインリロード等で要素リストが消去されていた場合の自動復元
        EnsureElementsReady();

        // 1. 各方角は常に流し、枠外はマスクで切る
        for (int i = 0; i < _elements.Count; i++)
        {
            var elem = _elements[i];
            float delta = Mathf.DeltaAngle(yaw, elem.TargetAngle);
            if (!elem.Rt.gameObject.activeSelf)
                elem.Rt.gameObject.SetActive(true);
            elem.Rt.anchoredPosition = new Vector2(delta * PixelsPerDegree, 0f);
        }

        // 2. デジタル方角は1度単位でのみ書き換える
        if (_headingBadgeText != null)
        {
            int deg = Mathf.RoundToInt(yaw) % 360;
            if (deg < 0) deg += 360;
            if (deg != _shownDeg)
            {
                _shownDeg = deg;
                _headingBadgeText.text = $"{GetCardinal(yaw)}  {deg}°";
            }
        }

        if (_cachedScrapMgr == null) _cachedScrapMgr = AdventureScrapManager.Instance ?? FindAnyObjectByType<AdventureScrapManager>();
        if (_cachedPlayer == null) _cachedPlayer = AdventurePlayerController.Instance ?? FindAnyObjectByType<AdventurePlayerController>();

        var mgr = _cachedScrapMgr;
        var player = _cachedPlayer;

        if (mgr != null && player != null)
        {
            Vector3 playerPos = player.transform.position;
            _targetSearchCooldown -= dt;

            if (_targetSearchCooldown <= 0f)
            {
                _targetSearchCooldown = 0.15f; // 1秒間に約6回更新でCPU負荷を大幅削減
                _cachedIsGuidingToTower = mgr.IsLeverUnlocked && !AdventureSanctuaryTowerManager.IsCanopyBroken;

                var puzzle = AdventureRustCrystalPuzzle.Instance ?? FindAnyObjectByType<AdventureRustCrystalPuzzle>();
                _cachedIsGuidingToCrystal = puzzle != null && !puzzle.IsActivated;
                _cachedIsGuidingToTerrace = puzzle != null && puzzle.IsActivated && !puzzle.IsChestOpened;

                var nearest = mgr.GetNearestScrapItem(playerPos, out float _);

                if (_cachedIsGuidingToTower)
                {
                    _cachedTargetMode = TargetMode.Tower;
                    _cachedTargetPos = new Vector3(512f, 63.2f, 501.5f); // 南正面メイン巨大レバー
                    _cachedTargetName = "白亜タワー（巨大レバー）";
                    _cachedTargetColor = new Color(0.25f, 0.95f, 1.0f, 1.0f);
                    _cachedHasTarget = true;
                }
                else if (nearest != null)
                {
                    // 未回収パーツを最優先でコンパスHUDに表示
                    _cachedTargetMode = TargetMode.Scrap;
                    _cachedTargetPos = nearest.transform.position;
                    _cachedTargetName = AdventureLocalizationManager.Get(nearest.itemName);
                    _cachedTargetColor = nearest.itemColor;
                    _cachedHasTarget = true;
                }
                else if (_cachedIsGuidingToCrystal)
                {
                    _cachedTargetMode = TargetMode.Crystal;
                    _cachedTargetPos = puzzle.crystalBasePos;
                    _cachedTargetName = "古代の送電クリスタル";
                    _cachedTargetColor = new Color(0.25f, 0.95f, 1.0f, 1.0f);
                    _cachedHasTarget = true;
                }
                else if (_cachedIsGuidingToTerrace)
                {
                    _cachedTargetMode = TargetMode.Terrace;
                    _cachedTargetPos = puzzle.cliffTerracePos;
                    _cachedTargetName = "崖上の隠しテラス（古代宝箱）";
                    _cachedTargetColor = new Color(1.0f, 0.85f, 0.35f, 1.0f);
                    _cachedHasTarget = true;
                }
                else
                {
                    _cachedTargetMode = TargetMode.None;
                    _cachedTargetPos = Vector3.zero;
                    _cachedTargetName = "";
                    _cachedTargetColor = Color.white;
                    _cachedHasTarget = false;
                }
            }

            Vector3 targetPos = _cachedTargetPos;
            string targetName = _cachedTargetName;
            Color targetColor = _cachedTargetColor;
            bool hasTarget = _cachedHasTarget;
            float dist = hasTarget ? Vector3.Distance(playerPos, targetPos) : 0f;

            // ── 漂着パーツ / 古代クリスタル / タワーレバーのマーカー更新 ──
            if (hasTarget)
            {
                Vector3 toTarget = targetPos - playerPos;
                toTarget.y = 0f;

                if (toTarget.sqrMagnitude > 4.0f)
                {
                    float targetYaw = YawFromForward(toTarget);
                    float angle = Mathf.DeltaAngle(yaw, targetYaw);
                    if (!_scrapBearingInit || dt <= 0f)
                    {
                        _scrapBearing = angle;
                        _scrapBearingInit = true;
                    }
                    else
                    {
                        _scrapBearing = Mathf.MoveTowardsAngle(_scrapBearing, angle, 540f * dt);
                    }

                    if (_scrapMarkerRt != null)
                    {
                        _scrapMarkerRt.gameObject.SetActive(true);
                        string iconCenter = _cachedTargetMode == TargetMode.Tower ? "🏛️" : (_cachedTargetMode == TargetMode.Crystal ? "⚡" : (_cachedTargetMode == TargetMode.Terrace ? "🎁" : "★"));
                        string iconSub = _cachedTargetMode == TargetMode.Tower ? "🏛️" : (_cachedTargetMode == TargetMode.Crystal ? "⚡" : (_cachedTargetMode == TargetMode.Terrace ? "🎁" : "✦"));
                        string iconRight = _cachedTargetMode == TargetMode.Tower ? "🏛️▶" : (_cachedTargetMode == TargetMode.Crystal ? "⚡▶" : (_cachedTargetMode == TargetMode.Terrace ? "🎁▶" : "✦▶"));
                        string iconLeft = _cachedTargetMode == TargetMode.Tower ? "◀🏛️" : (_cachedTargetMode == TargetMode.Crystal ? "◀⚡" : (_cachedTargetMode == TargetMode.Terrace ? "◀🎁" : "◀✦"));

                        PlaceEdgeMarker(
                            _scrapMarkerRt, _scrapMarkerText, _scrapBearing, targetColor,
                            iconCenter, iconSub, iconRight, iconLeft);
                    }
                }
                else
                {
                    if (_scrapMarkerRt != null) _scrapMarkerRt.gameObject.SetActive(false);
                    _scrapBearingInit = false;
                }
            }
            else
            {
                if (_scrapMarkerRt != null) _scrapMarkerRt.gameObject.SetActive(false);
                _scrapBearingInit = false;
            }

            // ── 漂着ボックス（Drift Box）の探知＆マーカー（📦）更新 ──
            var nearestBox = AdventureBeachDriftBox.GetNearestUnopenedBox(playerPos, out float boxDist);
            bool hasBoxTarget = nearestBox != null;
            float boxAngle = 0f;
            string boxCardinal = "";
            string boxArrow = "";

            if (hasBoxTarget)
            {
                Vector3 toBox = nearestBox.transform.position - playerPos;
                toBox.y = 0f;
                if (toBox.sqrMagnitude > 4.0f)
                {
                    float boxTargetYaw = YawFromForward(toBox);
                    boxAngle = Mathf.DeltaAngle(yaw, boxTargetYaw);
                    boxCardinal = GetCardinal(boxTargetYaw);

                    boxArrow = BearingHint(boxAngle, ref _boxHint);

                    if (!_boxBearingInit || dt <= 0f)
                    {
                        _boxBearing = boxAngle;
                        _boxBearingInit = true;
                    }
                    else
                    {
                        _boxBearing = Mathf.MoveTowardsAngle(_boxBearing, boxAngle, 540f * dt);
                    }

                    if (_boxMarkerRt != null)
                    {
                        _boxMarkerRt.gameObject.SetActive(true);
                        PlaceEdgeMarker(
                            _boxMarkerRt, _boxMarkerText, _boxBearing,
                            new Color(0.35f, 1.0f, 0.65f, 1f),
                            "🎁", "📦", "📦▶", "◀📦");
                    }
                }
                else
                {
                    if (_boxMarkerRt != null) _boxMarkerRt.gameObject.SetActive(false);
                    _boxBearingInit = false;
                }
            }
            else
            {
                if (_boxMarkerRt != null) _boxMarkerRt.gameObject.SetActive(false);
                _boxBearingInit = false;
            }

            // ── 白亜聖域タワー（島中央 512, 512）の常時マーカー更新 ──
            if (!_cachedIsGuidingToTower) // タワーがメインターゲットの時はメインマーカーに任せる
            {
                Vector3 towerCenter = new Vector3(512f, 63.2f, 512f);
                Vector3 toTowerCenter = towerCenter - playerPos;
                toTowerCenter.y = 0f;
                if (toTowerCenter.sqrMagnitude > 25f)
                {
                    float towerYaw = YawFromForward(toTowerCenter);
                    float tAngle = Mathf.DeltaAngle(yaw, towerYaw);
                    if (!_towerBearingInit || dt <= 0f)
                    {
                        _towerBearing = tAngle;
                        _towerBearingInit = true;
                    }
                    else
                    {
                        _towerBearing = Mathf.MoveTowardsAngle(_towerBearing, tAngle, 540f * dt);
                    }

                    if (_towerMarkerRt != null)
                    {
                        _towerMarkerRt.gameObject.SetActive(true);
                        PlaceEdgeMarker(
                            _towerMarkerRt, _towerMarkerText, _towerBearing,
                            new Color(0.40f, 0.95f, 1.0f, 0.90f),
                            "🏛️", "🏛️", "🏛️▶", "◀🏛️");
                    }
                }
                else
                {
                    if (_towerMarkerRt != null) _towerMarkerRt.gameObject.SetActive(false);
                    _towerBearingInit = false;
                }
            }
            else
            {
                if (_towerMarkerRt != null) _towerMarkerRt.gameObject.SetActive(false);
            }

            // ── Float（相棒ドローン）の道案内Pingタイマー更新 ──
            if (_hasGuidePing)
            {
                _guidePingTimer -= dt;
                if (_guidePingTimer <= 0f)
                {
                    _hasGuidePing = false;
                }
            }

            // ── ナビゲーションテキストの総合案内 ──
            if (_scrapNavText != null)
            {
                if (_hasGuidePing)
                {
                    // FloatがQキーで案内中の目標を最優先表示
                    float pingDist = Vector3.Distance(playerPos, _guidePingPos);
                    Vector3 toPing = _guidePingPos - playerPos; toPing.y = 0f;
                    string pCard = GetCardinal(YawFromForward(toPing));
                    _scrapNavText.text = FormatNavPing(_guidePingName, Mathf.RoundToInt(pingDist), pCard);
                    _scrapNavText.color = _guidePingColor;
                }
                else if (_cachedTargetMode == TargetMode.Tower)
                {
                    Vector3 toTower = targetPos - playerPos;
                    toTower.y = 0f;
                    float towerDist = toTower.magnitude;
                    if (towerDist <= 3.5f)
                    {
                        _scrapNavText.text = FormatNavTowerNear();
                        _scrapNavText.color = new Color(1.0f, 0.90f, 0.35f, 1.0f);
                    }
                    else
                    {
                        float towerYaw = YawFromForward(toTower);
                        string towerCard = GetCardinal(towerYaw);
                        _scrapNavText.text = FormatNavTowerFar(Mathf.RoundToInt(towerDist), towerCard);
                        _scrapNavText.color = new Color(0.25f, 0.95f, 1.0f, 1.0f);
                    }
                }
                else if (_cachedTargetMode == TargetMode.Scrap && hasTarget)
                {
                    Vector3 toTarget = targetPos - playerPos;
                    toTarget.y = 0f;
                    float targetYaw = YawFromForward(toTarget);
                    float angle = Mathf.DeltaAngle(yaw, targetYaw);
                    string targetCardinal = GetCardinal(targetYaw);
                    string arrow = BearingHint(angle, ref _scrapHint);

                    if (dist <= 3.5f)
                    {
                        _scrapNavText.text = FormatNavNear(targetName, "E");
                    }
                    else if (hasBoxTarget && boxDist > 3.5f)
                    {
                        string boxShortName = nearestBox.BoxDisplayName.Replace("漂着", "");
                        string distLabel = AdventureLocalizationManager.CurrentLanguage == AdventureLocalizationManager.GameLanguage.English ? "~" : "約";
                        _scrapNavText.text = $"✦ {targetName} {distLabel}{Mathf.RoundToInt(dist)}m({targetCardinal}) [{arrow}]   |   📦 {boxShortName} {distLabel}{Mathf.RoundToInt(boxDist)}m({boxCardinal}) [{boxArrow}]";
                    }
                    else
                    {
                        _scrapNavText.text = FormatNavFar(targetName, Mathf.RoundToInt(dist), targetCardinal, arrow, "Q");
                    }
                    _scrapNavText.color = targetColor;
                }
                else if (_cachedTargetMode == TargetMode.Crystal)
                {
                    Vector3 toCrystal = targetPos - playerPos;
                    toCrystal.y = 0f;
                    float cryDist = toCrystal.magnitude;
                    float cryYaw = YawFromForward(toCrystal);
                    float cryAngle = Mathf.DeltaAngle(yaw, cryYaw);
                    string cryCard = GetCardinal(cryYaw);
                    string cryArrow = BearingHint(cryAngle, ref _scrapHint);

                    if (cryDist <= 4.5f)
                    {
                        _scrapNavText.text = FormatNavCrystalNear();
                        _scrapNavText.color = new Color(1.0f, 0.90f, 0.35f, 1.0f);
                    }
                    else
                    {
                        _scrapNavText.text = FormatNavCrystalFar(Mathf.RoundToInt(cryDist), cryCard, cryArrow);
                        _scrapNavText.color = new Color(0.25f, 0.95f, 1.0f, 1.0f);
                    }
                }
                else if (_cachedTargetMode == TargetMode.Terrace)
                {
                    Vector3 toTerrace = targetPos - playerPos;
                    toTerrace.y = 0f;
                    float terDist = toTerrace.magnitude;
                    float terYaw = YawFromForward(toTerrace);
                    float terAngle = Mathf.DeltaAngle(yaw, terYaw);
                    string terCard = GetCardinal(terYaw);
                    string terArrow = BearingHint(terAngle, ref _scrapHint);

                    if (terDist <= 3.5f)
                    {
                        _scrapNavText.text = FormatNavTerraceNear();
                        _scrapNavText.color = new Color(1.0f, 0.90f, 0.35f, 1.0f);
                    }
                    else
                    {
                        _scrapNavText.text = FormatNavTerraceFar(Mathf.RoundToInt(terDist), terCard, terArrow);
                        _scrapNavText.color = new Color(1.0f, 0.85f, 0.35f, 1.0f);
                    }
                }
                else if (hasBoxTarget && boxDist > 3.5f)
                {
                    // パーツ全回収済み、またはボックス優先表示
                    _scrapNavText.text = FormatNavFar(nearestBox.BoxDisplayName, Mathf.RoundToInt(boxDist), boxCardinal, boxArrow, "Q");
                    _scrapNavText.color = new Color(0.35f, 1.0f, 0.65f, 1.0f);
                }
                else if (hasBoxTarget && boxDist <= 3.5f)
                {
                    _scrapNavText.text = FormatNavNear(nearestBox.BoxDisplayName, "E");
                    _scrapNavText.color = new Color(0.35f, 1.0f, 0.65f, 1.0f);
                }
                else
                {
                    _scrapNavText.text = AdventureLocalizationManager.CurrentLanguage switch
                    {
                        AdventureLocalizationManager.GameLanguage.English => "✦ All Scrap Parts & Boxes Collected! [Q Scenic Tour]",
                        AdventureLocalizationManager.GameLanguage.SimplifiedChinese => "✦ 已集齐所有零件与漂着宝箱！ [按Q键风景游览]",
                        AdventureLocalizationManager.GameLanguage.Spanish => "✦ ¡Todas las piezas y cajas recogidas! [Q Tour Panorámico]",
                        _ => "✦ 全ての漂着パーツ＆ボックス回収完了！  [Qキー 絶景案内]"
                    };
                    _scrapNavText.color = new Color(0.35f, 1.0f, 0.85f, 0.95f);
                }
            }
        }
    }

    void PlaceEdgeMarker(RectTransform rt, Text txt, float bearing, Color color, string ahead, string onRibbon, string rightLabel, string leftLabel)
    {
        float x;
        if (Mathf.Abs(bearing) <= OnRibbonLimit)
        {
            x = bearing * PixelsPerDegree;
            if (txt != null)
            {
                txt.text = Mathf.Abs(bearing) < 8f ? ahead : onRibbon;
                txt.color = color;
            }
        }
        else if (bearing > 0f)
        {
            x = RibbonHalfWidth - 16f;
            if (txt != null)
            {
                txt.text = rightLabel;
                Color c = color;
                c.a = 0.82f;
                txt.color = c;
            }
        }
        else
        {
            x = -RibbonHalfWidth + 16f;
            if (txt != null)
            {
                txt.text = leftLabel;
                Color c = color;
                c.a = 0.82f;
                txt.color = c;
            }
        }
        rt.anchoredPosition = new Vector2(Mathf.Clamp(x, -RibbonHalfWidth + 16f, RibbonHalfWidth - 16f), 0f);
    }

    static string FormatNavNear(string name, string actionKey)
    {
        string locName = AdventureLocalizationManager.Get(name);
        if (!string.IsNullOrEmpty(locName)) name = locName;

        return AdventureLocalizationManager.CurrentLanguage switch
        {
            AdventureLocalizationManager.GameLanguage.English => $"✦ {name} [Press {actionKey} to Collect!]",
            AdventureLocalizationManager.GameLanguage.SimplifiedChinese => $"✦ {name} 【按{actionKey}键回收！】",
            AdventureLocalizationManager.GameLanguage.Spanish => $"✦ {name} [¡Pulsa {actionKey} para recoger!]",
            _ => $"✦ {name} 【{actionKey}キーで回収！】"
        };
    }

    static string FormatNavFar(string name, int dist, string cardinal, string arrow, string extraKey = "Q")
    {
        string locName = AdventureLocalizationManager.Get(name);
        if (!string.IsNullOrEmpty(locName)) name = locName;

        return AdventureLocalizationManager.CurrentLanguage switch
        {
            AdventureLocalizationManager.GameLanguage.English => $"✦ {name} ~{dist}m ({cardinal}) [{arrow}]  [{extraKey} Guide]",
            AdventureLocalizationManager.GameLanguage.SimplifiedChinese => $"✦ {name} 约{dist}米（{cardinal}方向） [{arrow}]  [按{extraKey}引路]",
            AdventureLocalizationManager.GameLanguage.Spanish => $"✦ {name} ~{dist}m ({cardinal}) [{arrow}]  [{extraKey} Guiar]",
            _ => $"✦ {name} 約{dist}m（{cardinal}方角） [{arrow}]  [{extraKey}キー 道案内]"
        };
    }

    static string FormatNavPing(string name, int dist, string cardinal)
    {
        string locName = AdventureLocalizationManager.Get(name);
        if (!string.IsNullOrEmpty(locName)) name = locName;

        return AdventureLocalizationManager.CurrentLanguage switch
        {
            AdventureLocalizationManager.GameLanguage.English => $"✦ Float's Guide ➔ {name} ~{dist}m ({cardinal}) [Q to Guide]",
            AdventureLocalizationManager.GameLanguage.SimplifiedChinese => $"✦ Float的引路 ➔ {name} 约{dist}米（{cardinal}方向） [按Q键重新引路]",
            AdventureLocalizationManager.GameLanguage.Spanish => $"✦ Guía de Float ➔ {name} ~{dist}m ({cardinal}) [Q para guiar]",
            _ => $"✦ Floatの道案内 ➔ {name} 約{dist}m（{cardinal}方角） [Qキーで再案内]"
        };
    }

    static string FormatNavTowerNear()
    {
        return AdventureLocalizationManager.CurrentLanguage switch
        {
            AdventureLocalizationManager.GameLanguage.English => "✦ Central Tower Terrace [Press E to Pull Lever!]",
            AdventureLocalizationManager.GameLanguage.SimplifiedChinese => "✦ 中央高塔白垩露台 【按E键扳动机关手柄！】",
            AdventureLocalizationManager.GameLanguage.Spanish => "✦ Terraza de la torre central [¡Pulsa E para tirar de la palanca!]",
            _ => "✦ 中央タワー白亜テラス 【Eキーで巨大レバーを引く！】"
        };
    }

    static string FormatNavTowerFar(int dist, string cardinal)
    {
        return AdventureLocalizationManager.CurrentLanguage switch
        {
            AdventureLocalizationManager.GameLanguage.English => $"✦ White Tower (Giant Lever) ~{dist}m ({cardinal}) [Open Canopy]",
            AdventureLocalizationManager.GameLanguage.SimplifiedChinese => $"✦ 白垩高塔（巨型机关） 约{dist}米（{cardinal}方向） 【开启天盖】",
            AdventureLocalizationManager.GameLanguage.Spanish => $"✦ Torre Blanca (Palanca gigante) ~{dist}m ({cardinal}) [Abrir cúpula]",
            _ => $"✦ 白亜タワー（巨大レバー） 約{dist}m（{cardinal}方角） 【天蓋開放へ】"
        };
    }

    static string FormatNavCrystalNear()
    {
        return AdventureLocalizationManager.CurrentLanguage switch
        {
            AdventureLocalizationManager.GameLanguage.English => "⚡ Ancient Crystal [Press F to Instruct Rust!]",
            AdventureLocalizationManager.GameLanguage.SimplifiedChinese => "⚡ 古代风脉水晶 【按F键指引Rust充能传导！】",
            AdventureLocalizationManager.GameLanguage.Spanish => "⚡ Cristal antiguo [¡Pulsa F para ordenar a Rust!]",
            _ => "⚡ 古代クリスタル 【FキーでRustに送電を指示！】"
        };
    }

    static string FormatNavCrystalFar(int dist, string cardinal, string arrow)
    {
        return AdventureLocalizationManager.CurrentLanguage switch
        {
            AdventureLocalizationManager.GameLanguage.English => $"⚡ Ancient Crystal ~{dist}m ({cardinal}) [{arrow}] [F Power Gimmick]",
            AdventureLocalizationManager.GameLanguage.SimplifiedChinese => $"⚡ 古代风脉水晶 约{dist}米（{cardinal}方向） [{arrow}] 【F键充能传导】",
            AdventureLocalizationManager.GameLanguage.Spanish => $"⚡ Cristal antiguo ~{dist}m ({cardinal}) [{arrow}] [Mecanismo de F]",
            _ => $"⚡ 古代クリスタル 約{dist}m（{cardinal}方角） [{arrow}] 【Fキー送電ギミック】"
        };
    }

    static string FormatNavTerraceNear()
    {
        return AdventureLocalizationManager.CurrentLanguage switch
        {
            AdventureLocalizationManager.GameLanguage.English => "🎁 Ancient Cliff Terrace [Press E to Open Relic!]",
            AdventureLocalizationManager.GameLanguage.SimplifiedChinese => "🎁 悬崖上的古代露台 【按E键开启浮游遗物！】",
            AdventureLocalizationManager.GameLanguage.Spanish => "🎁 Terraza del acantilado [¡Pulsa E para abrir reliquia!]",
            _ => "🎁 崖上の古代テラス 【Eキーで古代の浮遊遺物を開封！】"
        };
    }

    static string FormatNavTerraceFar(int dist, string cardinal, string arrow)
    {
        return AdventureLocalizationManager.CurrentLanguage switch
        {
            AdventureLocalizationManager.GameLanguage.English => $"🎁 Hidden Cliff Terrace (Golden Beam) ~{dist}m ({cardinal}) [{arrow}] [Ride Updraft Fan!]",
            AdventureLocalizationManager.GameLanguage.SimplifiedChinese => $"🎁 悬崖隐藏露台（黄金光柱） 约{dist}米（{cardinal}方向） [{arrow}] 【乘风扇升空！】",
            AdventureLocalizationManager.GameLanguage.Spanish => $"🎁 Terraza secreta (Haz dorado) ~{dist}m ({cardinal}) [{arrow}] [¡Sube en ventilador!]",
            _ => $"🎁 崖上の隠しテラス（黄金光柱） 約{dist}m（{cardinal}方角） [{arrow}] 【送風ファンで上昇！】"
        };
    }

    /// <summary>正面・左右・背後。境界付近では直前の表示を維持してちらつかない。</summary>
    static string BearingHint(float angle, ref int held)
    {
        float abs = Mathf.Abs(angle);
        if (held == 2)
        {
            if (abs < 138f) held = angle >= 0f ? 1 : -1;
        }
        else if (abs > 150f)
            held = 2;
        else if (held == 0)
        {
            if (abs > 22f) held = angle >= 0f ? 1 : -1;
        }
        else if (abs < 12f)
            held = 0;
        else
            held = angle >= 0f ? 1 : -1;

        return AdventureLocalizationManager.CurrentLanguage switch
        {
            AdventureLocalizationManager.GameLanguage.English => held == 0 ? "▲ Ahead" : held == 1 ? "▶ Right" : held == -1 ? "◀ Left" : "▼ Behind",
            AdventureLocalizationManager.GameLanguage.SimplifiedChinese => held == 0 ? "▲ 正面" : held == 1 ? "▶ 右侧" : held == -1 ? "◀ 左侧" : "▼ 背后",
            AdventureLocalizationManager.GameLanguage.Spanish => held == 0 ? "▲ Frente" : held == 1 ? "▶ Derecha" : held == -1 ? "◀ Izquierda" : "▼ Detrás",
            _ => held == 0 ? "▲正面" : held == 1 ? "▶右" : held == -1 ? "◀左" : "▼背後"
        };
    }

    /// <summary>ワールドXZ前方ベクトル → コンパス方位角（北=0 / 東=90 / 南=180 / 西=270）</summary>
    static float YawFromForward(Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            return 0f;
        forward.Normalize();
        return Mathf.Repeat(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, 360f);
    }

    public static string GetCardinal(float yaw)
    {
        int index = 0;
        if (yaw >= 337.5f || yaw < 22.5f) index = 0;
        else if (yaw >= 22.5f && yaw < 67.5f) index = 1;
        else if (yaw >= 67.5f && yaw < 112.5f) index = 2;
        else if (yaw >= 112.5f && yaw < 157.5f) index = 3;
        else if (yaw >= 157.5f && yaw < 202.5f) index = 4;
        else if (yaw >= 202.5f && yaw < 247.5f) index = 5;
        else if (yaw >= 247.5f && yaw < 292.5f) index = 6;
        else index = 7;

        return AdventureLocalizationManager.CurrentLanguage switch
        {
            AdventureLocalizationManager.GameLanguage.English => (new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" })[index],
            AdventureLocalizationManager.GameLanguage.SimplifiedChinese => (new[] { "北", "东北", "东", "东南", "南", "西南", "西", "西北" })[index],
            AdventureLocalizationManager.GameLanguage.Spanish => (new[] { "N", "NE", "E", "SE", "S", "SO", "O", "NO" })[index],
            _ => (new[] { "北", "北東", "東", "南東", "南", "南西", "西", "北西" })[index]
        };
    }

    public static Font ResolveSafeFont()
    {
        var anyText = FindAnyObjectByType<Text>();
        if (anyText != null && anyText.font != null)
            return anyText.font;

        string[] fonts = {
            "Hiragino Sans",
            "Hiragino Kaku Gothic ProN",
            "Yu Gothic UI",
            "YuGothic",
            "Meiryo",
            "Noto Sans CJK JP",
            "Arial Unicode MS",
            "Arial"
        };
        foreach (var name in fonts)
        {
            try
            {
                var f = Font.CreateDynamicFontFromOSFont(name, 14);
                if (f != null) return f;
            }
            catch { }
        }

        try
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f != null) return f;
        }
        catch { }

        try
        {
            var f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f != null) return f;
        }
        catch { }

        return Font.CreateDynamicFontFromOSFont("Arial", 14);
    }
}
