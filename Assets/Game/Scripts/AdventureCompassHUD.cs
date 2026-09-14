using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 『Rust & Float』専用の邪魔にならないスタイリッシュな水平リボンコンパスHUD
/// 画面最上部中央に配置され、カメラの向きに合わせて方角（N, NE, E, SE, S, SW, W, NW）を滑らかに表示する
/// </summary>
public class AdventureCompassHUD : MonoBehaviour
{
    RectTransform _tapeRt;
    Text _headingBadgeText;
    Camera _cam;

    const float PixelsPerDegree = 2.4f; // 1度あたりのピクセル幅（360度 = 864px）
    const float TotalTapeWidth = 360f * PixelsPerDegree;

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
        var rootRt = gameObject.AddComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 1f);
        rootRt.anchorMax = new Vector2(0.5f, 1f);
        rootRt.pivot = new Vector2(0.5f, 1f);
        rootRt.anchoredPosition = new Vector2(0f, -8f);
        rootRt.sizeDelta = new Vector2(340f, 26f);

        // 背景（半透明のダークバー）
        var bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.07f, 0.10f, 0.42f);

        // 下部の細い境界ライン
        var borderBottom = new GameObject("BorderBottom");
        borderBottom.transform.SetParent(transform, false);
        var bRt = borderBottom.AddComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0f, 0f);
        bRt.anchorMax = new Vector2(1f, 0f);
        bRt.pivot = new Vector2(0.5f, 0f);
        bRt.anchoredPosition = Vector2.zero;
        bRt.sizeDelta = new Vector2(0f, 1.2f);
        var bImg = borderBottom.AddComponent<Image>();
        bImg.color = new Color(0.3f, 0.6f, 0.8f, 0.35f);

        // マスク領域（左右のフェード・クリップ用）
        var maskGo = new GameObject("CompassMask");
        maskGo.transform.SetParent(transform, false);
        var maskRt = maskGo.AddComponent<RectTransform>();
        maskRt.anchorMin = Vector2.zero;
        maskRt.anchorMax = Vector2.one;
        maskRt.sizeDelta = Vector2.zero;
        maskGo.AddComponent<RectMask2D>();

        // コンパステープ（左右にスクロールするコンテナ）
        var tapeGo = new GameObject("CompassTape");
        tapeGo.transform.SetParent(maskGo.transform, false);
        _tapeRt = tapeGo.AddComponent<RectTransform>();
        _tapeRt.anchorMin = new Vector2(0.5f, 0.5f);
        _tapeRt.anchorMax = new Vector2(0.5f, 0.5f);
        _tapeRt.pivot = new Vector2(0.5f, 0.5f);
        _tapeRt.sizeDelta = new Vector2(TotalTapeWidth * 4f, 26f);

        // 方角マーカーをテープ内に生成 (-360° 〜 720°)
        string[] cardinals = { "北", "北東", "東", "南東", "南", "南西", "西", "北西" };
        int[] angles = { 0, 45, 90, 135, 180, 225, 270, 315 };

        for (int cycle = -1; cycle <= 2; cycle++)
        {
            for (int i = 0; i < cardinals.Length; i++)
            {
                float deg = cycle * 360f + angles[i];
                float x = deg * PixelsPerDegree;

                // 方角テキスト
                var textGo = new GameObject("Dir_" + cardinals[i] + "_" + cycle);
                textGo.transform.SetParent(_tapeRt, false);
                var trt = textGo.AddComponent<RectTransform>();
                trt.anchoredPosition = new Vector2(x, -1f);
                trt.sizeDelta = new Vector2(40f, 24f);

                var txt = textGo.AddComponent<Text>();
                txt.font = font;
                txt.fontSize = cardinals[i].Length == 1 ? 13 : 11;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.text = cardinals[i];

                if (cardinals[i] == "北")
                    txt.color = new Color(0.25f, 0.95f, 1f, 0.95f); // 北は鮮やかなシアン
                else if (cardinals[i].Length == 1)
                    txt.color = new Color(0.92f, 0.95f, 1f, 0.85f); // 東、南、西
                else
                    txt.color = new Color(0.70f, 0.78f, 0.85f, 0.60f); // 北東、南東、南西、北西

                var outline = textGo.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
                outline.effectDistance = new Vector2(1f, -1f);

                // 間の目盛りドット（15°ごと）
                for (int sub = 1; sub < 3; sub++)
                {
                    float subDeg = deg + sub * 15f;
                    if (subDeg >= (cycle + 1) * 360f && i == cardinals.Length - 1) continue;

                    var dotGo = new GameObject("Tick_" + subDeg);
                    dotGo.transform.SetParent(_tapeRt, false);
                    var drt = dotGo.AddComponent<RectTransform>();
                    drt.anchoredPosition = new Vector2(subDeg * PixelsPerDegree, -1f);
                    drt.sizeDelta = new Vector2(20f, 20f);
                    var dotTxt = dotGo.AddComponent<Text>();
                    dotTxt.font = font;
                    dotTxt.fontSize = 9;
                    dotTxt.alignment = TextAnchor.MiddleCenter;
                    dotTxt.text = "·";
                    dotTxt.color = new Color(0.6f, 0.7f, 0.8f, 0.45f);
                }
            }
        }

        // 中央インジケーター（▼ マーカー）
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
        nTxt.color = new Color(0.25f, 0.95f, 1f, 0.95f);
        var nOutline = needleGo.AddComponent<Outline>();
        nOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        nOutline.effectDistance = new Vector2(1f, -1f);

        // 中央下部の方角・角度デジタルバッジ（例: "N  15°"）
        var badgeGo = new GameObject("HeadingBadge");
        badgeGo.transform.SetParent(transform, false);
        var bBadgeRt = badgeGo.AddComponent<RectTransform>();
        bBadgeRt.anchorMin = new Vector2(0.5f, 0f);
        bBadgeRt.anchorMax = new Vector2(0.5f, 0f);
        bBadgeRt.pivot = new Vector2(0.5f, 1f);
        bBadgeRt.anchoredPosition = new Vector2(0f, -2f);
        bBadgeRt.sizeDelta = new Vector2(100f, 16f);

        _headingBadgeText = badgeGo.AddComponent<Text>();
        _headingBadgeText.font = font;
        _headingBadgeText.fontSize = 10;
        _headingBadgeText.fontStyle = FontStyle.Bold;
        _headingBadgeText.alignment = TextAnchor.MiddleCenter;
        _headingBadgeText.color = new Color(0.85f, 0.95f, 1f, 0.85f);
        _headingBadgeText.text = "北  0°";
        var badgeOutline = badgeGo.AddComponent<Outline>();
        badgeOutline.effectColor = new Color(0f, 0f, 0f, 0.7f);
        badgeOutline.effectDistance = new Vector2(1f, -1f);

        // 最寄り漂着パーツの方向・距離ナビゲーションバッジ（例: "✦ 古代の黄金ギア  11m  [▲ 正面]"）
        var navGo = new GameObject("ScrapNavBadge");
        navGo.transform.SetParent(transform, false);
        var navRt = navGo.AddComponent<RectTransform>();
        navRt.anchorMin = new Vector2(0.5f, 0f);
        navRt.anchorMax = new Vector2(0.5f, 0f);
        navRt.pivot = new Vector2(0.5f, 1f);
        navRt.anchoredPosition = new Vector2(0f, -19f);
        navRt.sizeDelta = new Vector2(340f, 18f);

        _scrapNavText = navGo.AddComponent<Text>();
        _scrapNavText.font = font;
        _scrapNavText.fontSize = 11;
        _scrapNavText.fontStyle = FontStyle.Bold;
        _scrapNavText.alignment = TextAnchor.MiddleCenter;
        _scrapNavText.color = new Color(1.0f, 0.82f, 0.25f, 0.95f);
        _scrapNavText.text = "✦ 最寄りの漂着パーツを探知中…";
        var navOutline = navGo.AddComponent<Outline>();
        navOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        navOutline.effectDistance = new Vector2(1f, -1f);
    }

    Text _scrapNavText;

    void Update()
    {
        if (_cam == null)
            _cam = Camera.main ?? FindAnyObjectByType<Camera>();
        if (_cam == null || _tapeRt == null)
            return;

        // カメラの向いている水平角度 (0°〜360°)
        float yaw = _cam.transform.eulerAngles.y;
        yaw = (yaw % 360f + 360f) % 360f;

        // テープ位置を更新（0°〜360°に滑らかにオフセット）
        float targetX = -yaw * PixelsPerDegree;
        _tapeRt.anchoredPosition = new Vector2(targetX, 0f);

        // デジタル表示の更新
        if (_headingBadgeText != null)
        {
            string cardinal = GetCardinal(yaw);
            _headingBadgeText.text = $"{cardinal}  {(int)yaw}°";
        }

        // 最寄り漂着パーツへの方向と距離のリアルタイムナビゲーション
        if (_scrapNavText != null)
        {
            var mgr = AdventureScrapManager.Instance ?? FindAnyObjectByType<AdventureScrapManager>();
            var player = AdventurePlayerController.Instance ?? FindAnyObjectByType<AdventurePlayerController>();
            if (mgr != null && player != null)
            {
                var nearest = mgr.GetNearestScrapItem(player.transform.position, out float dist);
                if (nearest != null)
                {
                    Vector3 camFwd = _cam.transform.forward;
                    camFwd.y = 0f;
                    camFwd.Normalize();

                    Vector3 toScrap = (nearest.transform.position - player.transform.position);
                    toScrap.y = 0f;

                    if (toScrap.sqrMagnitude > 0.04f)
                    {
                        toScrap.Normalize();
                        float angle = Vector3.SignedAngle(camFwd, toScrap, Vector3.up);

                        string arrow;
                        if (Mathf.Abs(angle) < 25f) arrow = "▲ 正面";
                        else if (angle >= 25f && angle < 155f) arrow = "▶ 右";
                        else if (angle <= -25f && angle > -155f) arrow = "◀ 左";
                        else arrow = "▼ 後方";

                        _scrapNavText.text = $"✦ {nearest.itemName}  {(int)dist}m  [{arrow}]";
                        _scrapNavText.color = nearest.itemColor;
                    }
                    else
                    {
                        _scrapNavText.text = $"✦ {nearest.itemName}  [★ 足元]";
                        _scrapNavText.color = nearest.itemColor;
                    }
                }
                else if (mgr.CollectedCount >= mgr.TotalScrapCount)
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
