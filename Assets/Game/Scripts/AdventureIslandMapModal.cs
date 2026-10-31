using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// 『Rust & Float』羊皮紙調の手描き島嶼探検マップ（冒険日誌）。
/// 【Mキー】またはポーズメニューから開閉し、
/// 島のランドマーク、地形、漂着物、そしてFloatの現在地と方角をスケッチ風に俯瞰できる。
/// </summary>
public class AdventureIslandMapModal : MonoBehaviour
{
    private static AdventureIslandMapModal _instance;
    public static AdventureIslandMapModal Instance => _instance;

    public static bool IsOpen => _instance != null && _instance._isOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void AutoInitialize()
    {
        if (_instance != null) return;
        var existing = Object.FindAnyObjectByType<AdventureIslandMapModal>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("AdventureIslandMapModal");
        _instance = go.AddComponent<AdventureIslandMapModal>();
        DontDestroyOnLoad(go);
    }

    private bool _isOpen = false;
    private GameObject _uiRoot;
    private RectTransform _mapContentRt;
    private RectTransform _playerPinRt;
    private Text _playerPinLabel;
    private Text _mapHeaderTitle;
    private Text _mapFooterText;
    private Text _questSummaryText;

    private float _lastToggleTime = -10f;
    private Font _cachedFont;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    void Update()
    {
        // 【Mキー】でマップ開閉
        bool mPressed = false;
        var kb = Keyboard.current;
        if (kb != null && kb.mKey.wasPressedThisFrame) mPressed = true;
        try { if (Input.GetKeyDown(KeyCode.M)) mPressed = true; } catch { }

        // ゲームパッドのSelect/Back/Viewボタンでも開閉可能
        var pad = Gamepad.current;
        if (pad != null && pad.selectButton.wasPressedThisFrame) mPressed = true;

        if (mPressed && Time.unscaledTime > _lastToggleTime + 0.25f)
        {
            _lastToggleTime = Time.unscaledTime;
            ToggleMap();
        }

        if (_isOpen)
        {
            // EscキーまたはBボタンで閉じる
            bool escPressed = false;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) escPressed = true;
            try { if (Input.GetKeyDown(KeyCode.Escape)) escPressed = true; } catch { }
            if (pad != null && pad.buttonEast.wasPressedThisFrame) escPressed = true;

            if (escPressed && Time.unscaledTime > _lastToggleTime + 0.25f)
            {
                _lastToggleTime = Time.unscaledTime;
                CloseMap();
                return;
            }

            UpdatePlayerMarker();
        }
    }

    public void ToggleMap()
    {
        if (_isOpen) CloseMap();
        else OpenMap();
    }

    public void OpenMap()
    {
        if (AdventurePauseMenu.IsOpen)
            AdventurePauseMenu.Instance?.SetMenuVisible(false);

        _isOpen = true;
        EnsureUI();
        _uiRoot.SetActive(true);
        RefreshLocalization();
        UpdatePlayerMarker();
    }

    public void CloseMap()
    {
        _isOpen = false;
        if (_uiRoot != null)
            _uiRoot.SetActive(false);
    }

    void EnsureUI()
    {
        if (_uiRoot != null) return;

        Font font = ResolveFont();
        var canvasGo = new GameObject("IslandMapCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9980; // ポーズメニューと同等層
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        canvasGo.AddComponent<GraphicRaycaster>();

        // 背景ディム
        var dimGo = new GameObject("Dim");
        dimGo.transform.SetParent(canvasGo.transform, false);
        var dimRt = dimGo.AddComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = dimRt.offsetMax = Vector2.zero;
        var dimImg = dimGo.AddComponent<Image>();
        dimImg.color = new Color(0.02f, 0.04f, 0.06f, 0.75f);
        var dimBtn = dimGo.AddComponent<Button>();
        dimBtn.onClick.AddListener(CloseMap);

        // 羊皮紙調メインパネル
        var panelGo = new GameObject("ParchmentPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelGo.AddComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(1040f, 620f);
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0.12f, 0.09f, 0.06f, 0.98f); // セピア古文書調

        // 装飾インナーフレーム
        var innerGo = new GameObject("InnerFrame");
        innerGo.transform.SetParent(panelGo.transform, false);
        var innerRt = innerGo.AddComponent<RectTransform>();
        innerRt.anchorMin = Vector2.zero;
        innerRt.anchorMax = Vector2.one;
        innerRt.offsetMin = new Vector2(12f, 12f);
        innerRt.offsetMax = new Vector2(-12f, -12f);
        var innerImg = innerGo.AddComponent<Image>();
        innerImg.color = new Color(0.20f, 0.16f, 0.11f, 0.92f);

        // ヘッダータイトル
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(panelGo.transform, false);
        var titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1.0f);
        titleRt.pivot = new Vector2(0.5f, 1.0f);
        titleRt.anchoredPosition = new Vector2(0f, -18f);
        titleRt.sizeDelta = new Vector2(900f, 44f);
        _mapHeaderTitle = titleGo.AddComponent<Text>();
        _mapHeaderTitle.font = font;
        _mapHeaderTitle.fontSize = 26;
        _mapHeaderTitle.fontStyle = FontStyle.Bold;
        _mapHeaderTitle.alignment = TextAnchor.MiddleCenter;
        _mapHeaderTitle.color = new Color(1.0f, 0.88f, 0.52f, 1.0f);

        // マップ描画キャンバス領域 (560x520)
        var mapAreaGo = new GameObject("MapContent");
        mapAreaGo.transform.SetParent(panelGo.transform, false);
        _mapContentRt = mapAreaGo.AddComponent<RectTransform>();
        _mapContentRt.anchorMin = _mapContentRt.anchorMax = new Vector2(0.5f, 0.5f);
        _mapContentRt.sizeDelta = new Vector2(560f, 500f);
        var mapAreaImg = mapAreaGo.AddComponent<Image>();
        mapAreaImg.color = new Color(0.16f, 0.22f, 0.20f, 0.85f); // 海・淡い青緑の古地図

        // 島の大まかな海岸線スケッチ（手描き風テクスチャプロット）
        CreateIslandLandmarks(mapAreaGo.transform, font);

        // プレイヤーのピン（📍 現在地・進行方向矢印）
        var pinGo = new GameObject("PlayerPin");
        pinGo.transform.SetParent(mapAreaGo.transform, false);
        _playerPinRt = pinGo.AddComponent<RectTransform>();
        _playerPinRt.sizeDelta = new Vector2(36f, 36f);
        _playerPinLabel = pinGo.AddComponent<Text>();
        _playerPinLabel.font = font;
        _playerPinLabel.fontSize = 22;
        _playerPinLabel.fontStyle = FontStyle.Bold;
        _playerPinLabel.alignment = TextAnchor.MiddleCenter;
        _playerPinLabel.color = new Color(1.0f, 0.25f, 0.25f, 1.0f);
        _playerPinLabel.text = "▲\nFloat";

        // サイドパネル（探索進行状況・収集情報）
        var infoGo = new GameObject("SideInfo");
        infoGo.transform.SetParent(panelGo.transform, false);
        var infoRt = infoGo.AddComponent<RectTransform>();
        infoRt.anchorMin = infoRt.anchorMax = new Vector2(1.0f, 0.5f);
        infoRt.pivot = new Vector2(1.0f, 0.5f);
        infoRt.anchoredPosition = new Vector2(-35f, 0f);
        infoRt.sizeDelta = new Vector2(240f, 480f);
        _questSummaryText = infoGo.AddComponent<Text>();
        _questSummaryText.font = font;
        _questSummaryText.fontSize = 17;
        _questSummaryText.alignment = TextAnchor.UpperLeft;
        _questSummaryText.lineSpacing = 1.35f;
        _questSummaryText.color = new Color(0.92f, 0.94f, 0.96f, 1.0f);

        // フッター閉じる案内
        var footerGo = new GameObject("Footer");
        footerGo.transform.SetParent(panelGo.transform, false);
        var footerRt = footerGo.AddComponent<RectTransform>();
        footerRt.anchorMin = footerRt.anchorMax = new Vector2(0.5f, 0.0f);
        footerRt.pivot = new Vector2(0.5f, 0.0f);
        footerRt.anchoredPosition = new Vector2(0f, 16f);
        footerRt.sizeDelta = new Vector2(900f, 32f);
        _mapFooterText = footerGo.AddComponent<Text>();
        _mapFooterText.font = font;
        _mapFooterText.fontSize = 16;
        _mapFooterText.alignment = TextAnchor.MiddleCenter;
        _mapFooterText.color = new Color(0.75f, 0.80f, 0.85f, 0.95f);

        _uiRoot = canvasGo;
        _uiRoot.SetActive(false);
    }

    void CreateIslandLandmarks(Transform parent, Font font)
    {
        // 島の中心(512,512) ➔ マップ中心(0,0)
        // ワールドサイズ 約 1024x1024 ➔ マップサイズ 520x480
        AddLandmark(parent, "白亜のタワー", "🏛️", new Vector3(512f, 0f, 512f), font, new Color(1f, 0.92f, 0.45f));
        AddLandmark(parent, "目覚めの白砂ビーチ", "🏖️", new Vector3(220f, 0f, 190f), font, new Color(0.5f, 1f, 0.8f));
        AddLandmark(parent, "せせらぎ池", "💧", new Vector3(360f, 0f, 480f), font, new Color(0.4f, 0.85f, 1f));
        AddLandmark(parent, "カルデラ湖", "🌊", new Vector3(310f, 0f, 740f), font, new Color(0.45f, 0.75f, 1f));
        AddLandmark(parent, "大樹海・原生林", "🌲", new Vector3(720f, 0f, 520f), font, new Color(0.6f, 0.95f, 0.5f));
        AddLandmark(parent, "北の大滑空崖", "🦅", new Vector3(512f, 0f, 860f), font, new Color(1f, 0.8f, 0.6f));
        AddLandmark(parent, "砂浜の焚き火", "🔥", new Vector3(180f, 0f, 340f), font, new Color(1f, 0.65f, 0.35f));
    }

    void AddLandmark(Transform parent, string rawName, string icon, Vector3 worldPos, Font font, Color color)
    {
        var go = new GameObject("Landmark_" + rawName);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160f, 45f);

        Vector2 mapPos = WorldToMapPos(worldPos);
        rt.anchoredPosition = mapPos;

        var txt = go.AddComponent<Text>();
        txt.font = font;
        txt.fontSize = 13;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = color;
        string locName = AdventureLocalizationManager.Get(rawName);
        txt.text = $"{icon}\n{locName}";
    }

    Vector2 WorldToMapPos(Vector3 worldPos)
    {
        // ワールド(X: 0〜1024, Z: 0〜1024) ➔ マップ(X: -240〜240, Y: -220〜220)
        float nx = (worldPos.x - 512f) / 512f;
        float nz = (worldPos.z - 512f) / 512f;
        return new Vector2(nx * 230f, nz * 215f);
    }

    void UpdatePlayerMarker()
    {
        if (_playerPinRt == null) return;
        var player = AdventurePlayerController.Instance;
        if (player == null) return;

        Vector3 pPos = player.transform.position;
        _playerPinRt.anchoredPosition = WorldToMapPos(pPos);

        // プレイヤーの向き（回転）
        float yaw = player.transform.eulerAngles.y;
        _playerPinRt.localRotation = Quaternion.Euler(0f, 0f, -yaw);

        // サイドパネルの収集情報リアルタイム更新
        var scrap = AdventureScrapManager.Instance;
        int collected = scrap != null ? scrap.CollectedCount : 0;
        int pts = scrap != null ? scrap.TotalProgressPoints : 0;
        bool hasSuperJump = player.hasCapytaSuperJump;
        bool canDash = collected >= 3;
        bool canDouble = player.canDoubleJump;
        bool hasSonar = player.hasPetRadar;

        string langDash = canDash ? "✔ 解放済み" : "✖ 未解放 (3個)";
        string langDouble = canDouble ? "✔ 解放済み" : "✖ 未解放 (6個)";
        string langSonar = hasSonar ? "✔ 解放済み" : "✖ 未解放 (9個)";
        string langCapyta = hasSuperJump ? "✔ 祝福済み" : "未遭遇";

        if (AdventureLocalizationManager.CurrentLanguage == AdventureLocalizationManager.GameLanguage.English)
        {
            langDash = canDash ? "✔ Unlocked" : "✖ Locked (3 scrap)";
            langDouble = canDouble ? "✔ Unlocked" : "✖ Locked (6 scrap)";
            langSonar = hasSonar ? "✔ Unlocked" : "✖ Locked (9 scrap)";
            langCapyta = hasSuperJump ? "✔ Blessed" : "Undiscovered";
        }

        string qTitle = AdventureLocalizationManager.Get("map_quest_title");
        string scrapLabel = AdventureLocalizationManager.Get("map_scrap_status");
        string capytaLabel = AdventureLocalizationManager.Get("map_capyta_status");
        string dashLabel = AdventureLocalizationManager.Get("map_dash_status");
        string jumpLabel = AdventureLocalizationManager.Get("map_jump_status");
        string sonarLabel = AdventureLocalizationManager.Get("map_sonar_status");

        _questSummaryText.text =
            $"<b><color=#FFE066>✦ {qTitle} ✦</color></b>\n\n" +
            $"<b>{scrapLabel}:</b> {collected} / 12 ({pts}pt)\n\n" +
            $"<b>{capytaLabel}:</b>\n  {langCapyta}\n\n" +
            $"<b>{dashLabel}:</b>\n  {langDash}\n\n" +
            $"<b>{jumpLabel}:</b>\n  {langDouble}\n\n" +
            $"<b>{sonarLabel}:</b>\n  {langSonar}";
    }

    void RefreshLocalization()
    {
        if (_mapHeaderTitle != null)
            _mapHeaderTitle.text = AdventureLocalizationManager.Get("map_modal_header");

        if (_mapFooterText != null)
            _mapFooterText.text = AdventureLocalizationManager.Get("map_modal_footer");
    }

    Font ResolveFont()
    {
        if (_cachedFont != null) return _cachedFont;
        string[] fonts = {
            "Hiragino Sans",
            "Hiragino Kaku Gothic ProN",
            "PingFang SC",
            "Microsoft YaHei",
            "YuGothic",
            "Arial"
        };
        foreach (var name in fonts)
        {
            var f = Font.CreateDynamicFontFromOSFont(name, 18);
            if (f != null) { _cachedFont = f; return f; }
        }
        _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return _cachedFont;
    }
}
