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

    const float PixelsPerDegree = 2.4f; // 1度あたりのピクセル幅（表示視野角 約±68度）
    const float RibbonHalfWidth = 160f; // コンパス枠の表示半幅

    struct CompassElement
    {
        public float TargetAngle;
        public RectTransform Rt;
        public Text Txt;
    }

    readonly List<CompassElement> _elements = new List<CompassElement>();

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

        // コンパステープコンテナ（RectMask2Dを使わず、コード側の表示判定で安全にクリッピング）
        var ribbonGo = new GameObject("CompassRibbon", typeof(RectTransform));
        ribbonGo.transform.SetParent(transform, false);
        _ribbonContainer = ribbonGo.GetComponent<RectTransform>();
        _ribbonContainer.anchorMin = new Vector2(0.5f, 0.5f);
        _ribbonContainer.anchorMax = new Vector2(0.5f, 0.5f);
        _ribbonContainer.pivot = new Vector2(0.5f, 0.5f);
        _ribbonContainer.anchoredPosition = Vector2.zero;
        _ribbonContainer.sizeDelta = new Vector2(360f, 28f);

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

        // 5. 最寄り漂着パーツの方向・距離ナビゲーションバッジ（デジタルバッジの下に配置）
        var navGo = new GameObject("ScrapNavBadge", typeof(RectTransform));
        navGo.transform.SetParent(transform, false);
        var navRt = navGo.GetComponent<RectTransform>();
        navRt.anchorMin = new Vector2(0.5f, 0f);
        navRt.anchorMax = new Vector2(0.5f, 0f);
        navRt.pivot = new Vector2(0.5f, 1f);
        navRt.anchoredPosition = new Vector2(0f, -24f);
        navRt.sizeDelta = new Vector2(460f, 22f);

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
        var cam = GetActiveCamera();
        if (cam == null)
            return;

        // カメラ正面XZから方位を取る（CurrentYaw累積や1フレームズレに依存しない）
        float yaw = YawFromForward(cam.transform.forward);

        // 1. 各方角要素のシームレス配置（DeltaAngle方式：境界でのワープが物理的にゼロ）
        for (int i = 0; i < _elements.Count; i++)
        {
            var elem = _elements[i];
            float delta = Mathf.DeltaAngle(yaw, elem.TargetAngle);

            if (Mathf.Abs(delta) <= 68f)
            {
                elem.Rt.gameObject.SetActive(true);
                float x = delta * PixelsPerDegree;
                elem.Rt.anchoredPosition = new Vector2(x, 0f);
            }
            else
            {
                elem.Rt.gameObject.SetActive(false);
            }
        }

        // 2. デジタル方角表示の更新
        if (_headingBadgeText != null)
        {
            string cardinal = GetCardinal(yaw);
            _headingBadgeText.text = $"{cardinal}  {Mathf.RoundToInt(yaw)}°";
        }

        // 3. 最寄り漂着パーツへの方向と距離のナビゲーション
        var mgr = AdventureScrapManager.Instance ?? FindAnyObjectByType<AdventureScrapManager>();
        var player = AdventurePlayerController.Instance ?? FindAnyObjectByType<AdventurePlayerController>();

        if (mgr != null && player != null)
        {
            var nearest = mgr.GetNearestScrapItem(player.transform.position, out float dist);
            if (nearest != null)
            {
                Vector3 toScrap = nearest.transform.position - player.transform.position;
                toScrap.y = 0f;

                if (toScrap.sqrMagnitude > 0.04f)
                {
                    // パーツの絶対方位も同じYaw定義で計算（リボンと矢印を一致させる）
                    float scrapYaw = YawFromForward(toScrap);
                    float angle = Mathf.DeltaAngle(yaw, scrapYaw);
                    string scrapCardinal = GetCardinal(scrapYaw);

                    // コンパスリボン上に「✦」マーカーをダイレクト描画
                    if (_scrapMarkerRt != null)
                    {
                        _scrapMarkerRt.gameObject.SetActive(true);
                        if (Mathf.Abs(angle) <= 65f)
                        {
                            // 視野内：正確な方角位置にプロット
                            _scrapMarkerRt.anchoredPosition = new Vector2(angle * PixelsPerDegree, 0f);
                            if (_scrapMarkerText != null)
                            {
                                _scrapMarkerText.text = Mathf.Abs(angle) < 6f ? "★" : "✦";
                                _scrapMarkerText.color = nearest.itemColor;
                            }
                        }
                        else if (angle > 65f)
                        {
                            // 右側画面外：右端にクランプして「✦▶」表示
                            _scrapMarkerRt.anchoredPosition = new Vector2(RibbonHalfWidth - 10f, 0f);
                            if (_scrapMarkerText != null)
                            {
                                _scrapMarkerText.text = "✦▶";
                                Color c = nearest.itemColor;
                                c.a = 0.70f + 0.30f * Mathf.Sin(Time.time * 7f); // 脈動
                                _scrapMarkerText.color = c;
                            }
                        }
                        else
                        {
                            // 左側画面外：左端にクランプして「◀✦」表示
                            _scrapMarkerRt.anchoredPosition = new Vector2(-RibbonHalfWidth + 10f, 0f);
                            if (_scrapMarkerText != null)
                            {
                                _scrapMarkerText.text = "◀✦";
                                Color c = nearest.itemColor;
                                c.a = 0.70f + 0.30f * Mathf.Sin(Time.time * 7f); // 脈動
                                _scrapMarkerText.color = c;
                            }
                        }
                    }

                    // テキストによる誘導表示（方角・距離・相対方向を明確に伝達）
                    if (_scrapNavText != null)
                    {
                        string arrow;
                        if (Mathf.Abs(angle) < 18f) arrow = "▲ 正面";
                        else if (angle >= 18f && angle < 155f) arrow = "▶ 右方向";
                        else if (angle <= -18f && angle > -155f) arrow = "◀ 左方向";
                        else arrow = "▼ 背後";

                        _scrapNavText.text = $"✦ {nearest.itemName}  約{Mathf.RoundToInt(dist)}m（{scrapCardinal}方角） [{arrow}]";
                        _scrapNavText.color = nearest.itemColor;
                    }
                }
                else
                {
                    if (_scrapMarkerRt != null) _scrapMarkerRt.gameObject.SetActive(false);
                    if (_scrapNavText != null)
                    {
                        _scrapNavText.text = $"✦ {nearest.itemName}  [★ 足元]";
                        _scrapNavText.color = nearest.itemColor;
                    }
                }
            }
            else
            {
                if (_scrapMarkerRt != null) _scrapMarkerRt.gameObject.SetActive(false);
                if (_scrapNavText != null)
                {
                    if (mgr.CollectedCount >= AdventureScrapManager.TotalScrapCount)
                    {
                        _scrapNavText.text = "✦ 全ての漂着パーツ回収完了！";
                        _scrapNavText.color = new Color(0.35f, 1.0f, 0.85f, 0.95f);
                    }
                    else
                    {
                        _scrapNavText.text = "✦ 最寄りの漂着パーツを探知中…";
                        _scrapNavText.color = new Color(1.0f, 0.85f, 0.35f, 0.85f);
                    }
                }
            }
        }
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
        if (yaw >= 337.5f || yaw < 22.5f) return "北";
        if (yaw >= 22.5f && yaw < 67.5f) return "北東";
        if (yaw >= 67.5f && yaw < 112.5f) return "東";
        if (yaw >= 112.5f && yaw < 157.5f) return "南東";
        if (yaw >= 157.5f && yaw < 202.5f) return "南";
        if (yaw >= 202.5f && yaw < 247.5f) return "南西";
        if (yaw >= 247.5f && yaw < 292.5f) return "西";
        return "北西";
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
