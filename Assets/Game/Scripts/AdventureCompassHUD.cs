using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 『Rust & Float』専用の水平リボンコンパスHUD（DeltaAngle方式・完全シームレス・パーツマーカー連動）
/// 360度の境界（北）でもワープせず滑らかに回転し、リボン上にパーツの方向アイコンもリアルタイム表示する
/// </summary>
public class AdventureCompassHUD : MonoBehaviour
{
    Camera _cam;
    Text _headingBadgeText;
    Text _scrapNavText;
    RectTransform _ribbonContainer;
    RectTransform _scrapMarkerRt;
    Text _scrapMarkerText;

    const float PixelsPerDegree = 2.4f; // 1度あたりのピクセル幅（表示視野角 約±68度）
    const float HalfWidth = 162f;       // コンパスの有効表示半幅 (324px / 2)

    struct CompassElement
    {
        public float TargetAngle;
        public RectTransform Rt;
        public Text Txt;
    }

    readonly List<CompassElement> _elements = new List<CompassElement>();

    public static AdventureCompassHUD Create(Transform parent, Font font)
    {
        var hudGo = new GameObject("CompassHUD");
        hudGo.transform.SetParent(parent, false);

        var hud = hudGo.AddComponent<AdventureCompassHUD>();
        hud.BuildUI(font);
        return hud;
    }

    void BuildUI(Font font)
    {
        // 1. コンパス外枠ルート（画面最上部中央）
        var rootRt = gameObject.AddComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 1f);
        rootRt.anchorMax = new Vector2(0.5f, 1f);
        rootRt.pivot = new Vector2(0.5f, 1f);
        rootRt.anchoredPosition = new Vector2(0f, -8f);
        rootRt.sizeDelta = new Vector2(340f, 26f);

        // 背景（半透明のダークグラデーションバー）
        var bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.07f, 0.12f, 0.55f);

        // 下部の細い境界ライン（爽やかなシアンの光彩）
        var borderBottom = new GameObject("BorderBottom");
        borderBottom.transform.SetParent(transform, false);
        var bRt = borderBottom.AddComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0f, 0f);
        bRt.anchorMax = new Vector2(1f, 0f);
        bRt.pivot = new Vector2(0.5f, 0f);
        bRt.anchoredPosition = Vector2.zero;
        bRt.sizeDelta = new Vector2(0f, 1.5f);
        var bImg = borderBottom.AddComponent<Image>();
        bImg.color = new Color(0.35f, 0.85f, 0.98f, 0.50f);

        // マスク領域（左右のクリップ用）
        var maskGo = new GameObject("CompassMask");
        maskGo.transform.SetParent(transform, false);
        var maskRt = maskGo.AddComponent<RectTransform>();
        maskRt.anchorMin = Vector2.zero;
        maskRt.anchorMax = Vector2.one;
        maskRt.sizeDelta = Vector2.zero;
        maskGo.AddComponent<RectMask2D>();

        // コンパステープコンテナ
        var ribbonGo = new GameObject("CompassRibbon");
        ribbonGo.transform.SetParent(maskGo.transform, false);
        _ribbonContainer = ribbonGo.AddComponent<RectTransform>();
        _ribbonContainer.anchorMin = new Vector2(0.5f, 0.5f);
        _ribbonContainer.anchorMax = new Vector2(0.5f, 0.5f);
        _ribbonContainer.pivot = new Vector2(0.5f, 0.5f);
        _ribbonContainer.anchoredPosition = Vector2.zero;
        _ribbonContainer.sizeDelta = new Vector2(340f, 26f);

        // 方角マーカー（8方位：0°, 45°, 90°, 135°, 180°, 225°, 270°, 315°）
        string[] cardinals = { "北", "北東", "東", "南東", "南", "南西", "西", "北西" };
        int[] angles = { 0, 45, 90, 135, 180, 225, 270, 315 };

        for (int i = 0; i < cardinals.Length; i++)
        {
            var elemGo = new GameObject("Card_" + cardinals[i]);
            elemGo.transform.SetParent(_ribbonContainer, false);
            var eRt = elemGo.AddComponent<RectTransform>();
            eRt.sizeDelta = new Vector2(44f, 24f);

            var txt = elemGo.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = cardinals[i].Length == 1 ? 13 : 11;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = cardinals[i];
            txt.raycastTarget = false;

            if (cardinals[i] == "北")
                txt.color = new Color(0.25f, 0.98f, 1.0f, 0.98f); // 北は鮮やかな発光シアン
            else if (cardinals[i].Length == 1)
                txt.color = new Color(0.92f, 0.96f, 1.0f, 0.90f); // 東、南、西
            else
                txt.color = new Color(0.72f, 0.82f, 0.90f, 0.65f); // 北東、南東、南西、北西

            var outline = elemGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);

            _elements.Add(new CompassElement { TargetAngle = angles[i], Rt = eRt, Txt = txt });

            // 15度ごとの目盛りドット（8方位の間に2つずつ配置）
            for (int sub = 1; sub < 3; sub++)
            {
                float subAngle = (angles[i] + sub * 15f) % 360f;
                var dotGo = new GameObject("Tick_" + subAngle);
                dotGo.transform.SetParent(_ribbonContainer, false);
                var dRt = dotGo.AddComponent<RectTransform>();
                dRt.sizeDelta = new Vector2(16f, 16f);

                var dTxt = dotGo.AddComponent<Text>();
                dTxt.font = font;
                dTxt.fontSize = 9;
                dTxt.alignment = TextAnchor.MiddleCenter;
                dTxt.text = "·";
                dTxt.color = new Color(0.65f, 0.80f, 0.92f, 0.50f);
                dTxt.raycastTarget = false;

                _elements.Add(new CompassElement { TargetAngle = subAngle, Rt = dRt, Txt = dTxt });
            }
        }

        // 2. コンパスリボン上に表示されるリアルタイム「パーツ探知マーカー（✦）」
        var smGo = new GameObject("CompassScrapMarker");
        smGo.transform.SetParent(_ribbonContainer, false);
        _scrapMarkerRt = smGo.AddComponent<RectTransform>();
        _scrapMarkerRt.sizeDelta = new Vector2(28f, 22f);
        _scrapMarkerText = smGo.AddComponent<Text>();
        _scrapMarkerText.font = font;
        _scrapMarkerText.fontSize = 13;
        _scrapMarkerText.fontStyle = FontStyle.Bold;
        _scrapMarkerText.alignment = TextAnchor.MiddleCenter;
        _scrapMarkerText.text = "✦";
        _scrapMarkerText.color = new Color(1.0f, 0.88f, 0.25f, 1f);
        _scrapMarkerText.raycastTarget = false;
        var smOutline = smGo.AddComponent<Outline>();
        smOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        smOutline.effectDistance = new Vector2(1f, -1f);
        smGo.SetActive(false);

        // 3. 中央インジケーター（▼ マーカー）
        var needleGo = new GameObject("CompassNeedle");
        needleGo.transform.SetParent(transform, false);
        var nRt = needleGo.AddComponent<RectTransform>();
        nRt.anchorMin = new Vector2(0.5f, 1f);
        nRt.anchorMax = new Vector2(0.5f, 1f);
        nRt.pivot = new Vector2(0.5f, 1f);
        nRt.anchoredPosition = new Vector2(0f, 1f);
        nRt.sizeDelta = new Vector2(24f, 14f);
        var nTxt = needleGo.AddComponent<Text>();
        nTxt.font = font;
        nTxt.fontSize = 11;
        nTxt.fontStyle = FontStyle.Bold;
        nTxt.alignment = TextAnchor.UpperCenter;
        nTxt.text = "▼";
        nTxt.color = new Color(0.25f, 0.98f, 1.0f, 0.95f);
        nTxt.raycastTarget = false;
        var nOutline = needleGo.AddComponent<Outline>();
        nOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        nOutline.effectDistance = new Vector2(1f, -1f);

        // 4. 中央下部の方角・角度デジタルバッジ（例: "北  15°"）
        var badgeGo = new GameObject("HeadingBadge");
        badgeGo.transform.SetParent(transform, false);
        var bBadgeRt = badgeGo.AddComponent<RectTransform>();
        bBadgeRt.anchorMin = new Vector2(0.5f, 0f);
        bBadgeRt.anchorMax = new Vector2(0.5f, 0f);
        bBadgeRt.pivot = new Vector2(0.5f, 1f);
        bBadgeRt.anchoredPosition = new Vector2(0f, -4f);
        bBadgeRt.sizeDelta = new Vector2(120f, 16f);

        _headingBadgeText = badgeGo.AddComponent<Text>();
        _headingBadgeText.font = font;
        _headingBadgeText.fontSize = 11;
        _headingBadgeText.fontStyle = FontStyle.Bold;
        _headingBadgeText.alignment = TextAnchor.MiddleCenter;
        _headingBadgeText.color = new Color(0.88f, 0.96f, 1.0f, 0.90f);
        _headingBadgeText.text = "北  0°";
        _headingBadgeText.raycastTarget = false;
        var badgeOutline = badgeGo.AddComponent<Outline>();
        badgeOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        badgeOutline.effectDistance = new Vector2(1f, -1f);

        // 5. 最寄り漂着パーツの方向・距離ナビゲーションバッジ（デジタルバッジの下に綺麗に配置）
        var navGo = new GameObject("ScrapNavBadge");
        navGo.transform.SetParent(transform, false);
        var navRt = navGo.AddComponent<RectTransform>();
        navRt.anchorMin = new Vector2(0.5f, 0f);
        navRt.anchorMax = new Vector2(0.5f, 0f);
        navRt.pivot = new Vector2(0.5f, 1f);
        navRt.anchoredPosition = new Vector2(0f, -22f); // バッジと重ならない安全クリアランス
        navRt.sizeDelta = new Vector2(380f, 20f);

        _scrapNavText = navGo.AddComponent<Text>();
        _scrapNavText.font = font;
        _scrapNavText.fontSize = 11;
        _scrapNavText.fontStyle = FontStyle.Bold;
        _scrapNavText.alignment = TextAnchor.MiddleCenter;
        _scrapNavText.color = new Color(1.0f, 0.88f, 0.28f, 0.95f);
        _scrapNavText.text = "✦ 最寄りの漂着パーツを探知中…";
        _scrapNavText.raycastTarget = false;
        var navOutline = navGo.AddComponent<Outline>();
        navOutline.effectColor = new Color(0f, 0f, 0f, 0.90f);
        navOutline.effectDistance = new Vector2(1f, -1f);
    }

    Camera GetActiveCamera()
    {
        if (_cam != null && _cam.isActiveAndEnabled)
            return _cam;

        if (Camera.main != null && Camera.main.isActiveAndEnabled)
        {
            _cam = Camera.main;
            return _cam;
        }

        var follow = FindAnyObjectByType<AdventureCameraFollow>();
        if (follow != null)
        {
            var cam = follow.GetComponent<Camera>();
            if (cam != null && cam.isActiveAndEnabled)
            {
                _cam = cam;
                return _cam;
            }
        }

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

        // カメラの水平方位角 (0°〜360°)
        float yaw = cam.transform.eulerAngles.y;
        yaw = (yaw % 360f + 360f) % 360f;

        // 1. 各方角要素のシームレス配置（DeltaAngle方式：境界でのワープが物理的にゼロ）
        for (int i = 0; i < _elements.Count; i++)
        {
            var elem = _elements[i];
            float delta = Mathf.DeltaAngle(yaw, elem.TargetAngle);

            if (Mathf.Abs(delta) <= 70f)
            {
                elem.Rt.gameObject.SetActive(true);
                float x = delta * PixelsPerDegree;
                elem.Rt.anchoredPosition = new Vector2(x, -1f);
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

                Vector3 camFwd = cam.transform.forward;
                camFwd.y = 0f;

                if (toScrap.sqrMagnitude > 0.04f && camFwd.sqrMagnitude > 0.01f)
                {
                    toScrap.Normalize();
                    camFwd.Normalize();

                    // カメラ正面から見たパーツへの符号付き角度（-180° 〜 +180°）
                    float angle = Vector3.SignedAngle(camFwd, toScrap, Vector3.up);

                    // コンパスリボン上に「✦」マーカーをダイレクト描画！
                    if (_scrapMarkerRt != null)
                    {
                        if (Mathf.Abs(angle) <= 65f)
                        {
                            _scrapMarkerRt.gameObject.SetActive(true);
                            _scrapMarkerRt.anchoredPosition = new Vector2(angle * PixelsPerDegree, -1f);
                            if (_scrapMarkerText != null)
                                _scrapMarkerText.color = nearest.itemColor;
                        }
                        else
                        {
                            _scrapMarkerRt.gameObject.SetActive(false);
                        }
                    }

                    // テキストによる誘導表示
                    if (_scrapNavText != null)
                    {
                        string arrow;
                        if (Mathf.Abs(angle) < 18f) arrow = "▲ 正面";
                        else if (angle >= 18f && angle < 155f) arrow = "▶ 右";
                        else if (angle <= -18f && angle > -155f) arrow = "◀ 左";
                        else arrow = "▼ 後方";

                        _scrapNavText.text = $"✦ {nearest.itemName}  {Mathf.RoundToInt(dist)}m  [{arrow}]";
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

    static string GetCardinal(float yaw)
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
}
