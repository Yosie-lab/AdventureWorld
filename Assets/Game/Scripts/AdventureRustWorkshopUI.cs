using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// 『Rust & Float』相棒Rustの着せ替え＆アクセサリークラフト工房UI。
/// 【Bキー】または西側砂浜の座礁艇作業台で【Eキー】を押すと開き、
/// 集めた貝殻・シーグラスを消費してRustのアクセサリー（花冠、発光アンテナ、小翼トレイル等）をクラフト・着せ替えできる。
/// </summary>
public class AdventureRustWorkshopUI : MonoBehaviour
{
    public static AdventureRustWorkshopUI Instance { get; private set; }

    public static bool IsOpen => Instance != null && Instance.isVisible;

    [Header("UI State")]
    public bool isVisible = false;

    // UIコンポーネント
    Canvas _canvas;
    CanvasGroup _canvasGroup;
    GameObject _panelRoot;

    // 左カラム：素材ポーチ
    Text _textSakuragai;
    Text _textEmerald;
    Text _textSapphire;
    Text _textAmber;
    Text _textConch;

    // 右カラム：アクセサリーカード
    readonly List<CosmeticCardUI> _cardUIs = new List<CosmeticCardUI>();

    // 座礁艇の作業台スポット
    GameObject _workbenchWorldObj;
    Text _promptText;
    CanvasGroup _promptCg;

    static readonly Vector3 WorkbenchPosition = new Vector3(158.5f, 6.25f, 278.5f);

    class CosmeticCardUI
    {
        public AdventureRustCosmetics.CosmeticDef def;
        public GameObject rootGo;
        public Text titleText;
        public Text descText;
        public Text costText;
        public Text statusText;
        public Button actionBtn;
        public Text actionBtnText;
        public Image actionBtnImage;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        Ensure();
    }

    public static void Ensure()
    {
        if (Instance != null) return;
        var existing = Object.FindFirstObjectByType<AdventureRustWorkshopUI>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject("AdventureRustWorkshopUI");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AdventureRustWorkshopUI>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        CreateUI();
        SpawnWorkbenchSpot();
    }

    void OnEnable()
    {
        AdventureBeachSeashellManager.OnInventoryChanged += RefreshUI;
    }

    void OnDisable()
    {
        AdventureBeachSeashellManager.OnInventoryChanged -= RefreshUI;
    }

    void Update()
    {
        var kb = Keyboard.current;

        // Bキーで工房トグル開閉
        bool bPressed = (kb != null && kb.bKey.wasPressedThisFrame);
        try { if (Input.GetKeyDown(KeyCode.B)) bPressed = true; } catch { }

        if (bPressed)
        {
            // ポーズ中などでなければトグル
            if (!AdventurePauseMenu.IsOpen)
            {
                SetVisible(!isVisible);
            }
        }

        // ESCキーで閉じる
        bool escPressed = (kb != null && kb.escapeKey.wasPressedThisFrame);
        try { if (Input.GetKeyDown(KeyCode.Escape)) escPressed = true; } catch { }

        if (escPressed && isVisible)
        {
            SetVisible(false);
        }

        // 作業台への接近判定とEキー入力
        UpdateWorkbenchInteraction();
    }

    void UpdateWorkbenchInteraction()
    {
        var player = AdventurePlayerController.Instance;
        if (player == null || _workbenchWorldObj == null) return;

        float dist = Vector3.Distance(player.transform.position, WorkbenchPosition);
        bool isNear = dist <= 3.8f && !isVisible && !AdventurePauseMenu.IsOpen;

        // プロンプト表示フェード
        if (_promptCg != null)
        {
            _promptCg.alpha = Mathf.MoveTowards(_promptCg.alpha, isNear ? 1.0f : 0.0f, Time.deltaTime * 6f);
        }

        if (isNear)
        {
            var kb = Keyboard.current;
            bool ePressed = (kb != null && kb.eKey.wasPressedThisFrame);
            try { if (Input.GetKeyDown(KeyCode.E)) ePressed = true; } catch { }

            if (ePressed)
            {
                SetVisible(true);
            }
        }
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        if (_panelRoot != null)
            _panelRoot.SetActive(visible);

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = visible ? 1.0f : 0.0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        if (visible)
        {
            Time.timeScale = 0.0f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            RefreshUI();
        }
        else
        {
            Time.timeScale = 1.0f;
            if (!AdventureStoryFlow.WantsFreeCursor)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
        }
    }

    public void RefreshUI()
    {
        var shellMgr = AdventureBeachSeashellManager.Instance;
        var cosmetics = AdventureRustCosmetics.Instance;

        // 1. 左カラム：素材ポーチの個数更新
        if (shellMgr != null)
        {
            if (_textSakuragai != null) _textSakuragai.text = $"{shellMgr.GetShellCount(AdventureBeachSeashellItem.ShellKind.Sakuragai)} 個";
            if (_textEmerald != null) _textEmerald.text = $"{shellMgr.GetShellCount(AdventureBeachSeashellItem.ShellKind.SeaGlassEmerald)} 個";
            if (_textSapphire != null) _textSapphire.text = $"{shellMgr.GetShellCount(AdventureBeachSeashellItem.ShellKind.SeaGlassSapphire)} 個";
            if (_textAmber != null) _textAmber.text = $"{shellMgr.GetShellCount(AdventureBeachSeashellItem.ShellKind.AmberPebble)} 個";
            if (_textConch != null) _textConch.text = $"{shellMgr.GetShellCount(AdventureBeachSeashellItem.ShellKind.SpiralShell)} 個";
        }

        // 2. 右カラム：各カードのステータスとボタン更新
        for (int i = 0; i < _cardUIs.Count; i++)
        {
            var card = _cardUIs[i];
            if (card == null || card.def == null || cosmetics == null) continue;

            bool isUnlocked = cosmetics.IsUnlocked(card.def.id);
            bool isEquipped = cosmetics.IsEquipped(card.def.id);
            bool canCraft = cosmetics.CanCraft(card.def.id);

            if (isUnlocked)
            {
                if (isEquipped)
                {
                    card.statusText.text = "<color=#66FFAA><b>✦ そうび中</b></color>";
                    card.actionBtnText.text = "はずす";
                    card.actionBtnImage.color = new Color(0.35f, 0.45f, 0.55f, 0.95f);
                    card.actionBtn.interactable = true;
                }
                else
                {
                    card.statusText.text = "<color=#BBBBCC>所持中</color>";
                    card.actionBtnText.text = "そうびする";
                    card.actionBtnImage.color = new Color(0.2f, 0.75f, 0.55f, 0.95f);
                    card.actionBtn.interactable = true;
                }
                card.costText.text = "<color=#AAAAAA>作成済み</color>";
            }
            else
            {
                card.statusText.text = "<color=#888899>未作成</color>";
                string kindName = GetKindName(card.def.requiredKind);
                string hexCol = ColorUtility.ToHtmlStringRGB(card.def.themeColor);

                int curCount = shellMgr != null ? shellMgr.GetShellCount(card.def.requiredKind) : 0;
                string countColor = curCount >= card.def.requiredCount ? "#66FFAA" : "#FF7777";

                card.costText.text = $"必要: <color=#{hexCol}>{kindName}</color> <color={countColor}>{curCount}/{card.def.requiredCount}</color>";

                if (canCraft)
                {
                    card.actionBtnText.text = "✦ つくる";
                    card.actionBtnImage.color = new Color(1f, 0.72f, 0.25f, 1f);
                    card.actionBtn.interactable = true;
                }
                else
                {
                    card.actionBtnText.text = "素材不足";
                    card.actionBtnImage.color = new Color(0.25f, 0.25f, 0.3f, 0.6f);
                    card.actionBtn.interactable = false;
                }
            }
        }
    }

    static string GetKindName(AdventureBeachSeashellItem.ShellKind kind)
    {
        switch (kind)
        {
            case AdventureBeachSeashellItem.ShellKind.Sakuragai: return "サクラガイ";
            case AdventureBeachSeashellItem.ShellKind.SeaGlassEmerald: return "エメラルド硝子";
            case AdventureBeachSeashellItem.ShellKind.SeaGlassSapphire: return "サファイア硝子";
            case AdventureBeachSeashellItem.ShellKind.AmberPebble: return "太陽の小琥珀";
            case AdventureBeachSeashellItem.ShellKind.SpiralShell: return "純白の巻貝";
            default: return "貝殻";
        }
    }

    void OnCardButtonClicked(CosmeticCardUI card)
    {
        var cosmetics = AdventureRustCosmetics.Instance;
        if (cosmetics == null || card == null || card.def == null) return;

        bool isUnlocked = cosmetics.IsUnlocked(card.def.id);
        if (!isUnlocked)
        {
            // クラフト
            if (cosmetics.CraftAndEquip(card.def.id))
            {
                RefreshUI();
            }
        }
        else
        {
            // 装備切替
            bool isEquipped = cosmetics.IsEquipped(card.def.id);
            cosmetics.SetEquipped(card.def.id, !isEquipped);
            RefreshUI();
        }
    }

    #region UI Construction
    void CreateUI()
    {
        var canvasGo = new GameObject("RustWorkshop_Canvas");
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 98; // ポーズメニュー(99)の直下

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
        _canvasGroup = canvasGo.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ── 半透明ダークバックドロップ ──
        var backdrop = new GameObject("Backdrop");
        backdrop.transform.SetParent(canvasGo.transform, false);
        var bdRect = backdrop.AddComponent<RectTransform>();
        bdRect.anchorMin = Vector2.zero;
        bdRect.anchorMax = Vector2.one;
        bdRect.sizeDelta = Vector2.zero;
        var bdImg = backdrop.AddComponent<Image>();
        bdImg.color = new Color(0.04f, 0.06f, 0.10f, 0.88f);

        // ── メインパネル（幅 960 × 高 580） ──
        _panelRoot = new GameObject("WorkshopPanel");
        _panelRoot.transform.SetParent(canvasGo.transform, false);
        var pRect = _panelRoot.AddComponent<RectTransform>();
        pRect.anchorMin = new Vector2(0.5f, 0.5f);
        pRect.anchorMax = new Vector2(0.5f, 0.5f);
        pRect.sizeDelta = new Vector2(980f, 600f);

        var pImg = _panelRoot.AddComponent<Image>();
        pImg.color = new Color(0.10f, 0.14f, 0.22f, 0.95f);

        // ── ヘッダー（タイトル＆閉じるボタン） ──
        var header = new GameObject("Header");
        header.transform.SetParent(_panelRoot.transform, false);
        var hRect = header.AddComponent<RectTransform>();
        hRect.anchorMin = new Vector2(0f, 1f);
        hRect.anchorMax = new Vector2(1f, 1f);
        hRect.pivot = new Vector2(0.5f, 1f);
        hRect.sizeDelta = new Vector2(0f, 65f);
        var hImg = header.AddComponent<Image>();
        hImg.color = new Color(0.14f, 0.20f, 0.32f, 0.95f);

        var titleGo = new GameObject("TitleText");
        titleGo.transform.SetParent(header.transform, false);
        var tRect = titleGo.AddComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0f, 0f);
        tRect.anchorMax = new Vector2(0.85f, 1f);
        tRect.offsetMin = new Vector2(25f, 0f);
        var titleText = titleGo.AddComponent<Text>();
        titleText.font = font;
        titleText.fontSize = 22;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.color = new Color(1f, 0.92f, 0.55f);
        titleText.text = "✦ RUST CRAFT WORKSHOP ✦ 〜 相棒のドレスアップ工房 〜";

        // 閉じる×ボタン
        var closeGo = new GameObject("CloseButton");
        closeGo.transform.SetParent(header.transform, false);
        var cRect = closeGo.AddComponent<RectTransform>();
        cRect.anchorMin = new Vector2(1f, 0.5f);
        cRect.anchorMax = new Vector2(1f, 0.5f);
        cRect.sizeDelta = new Vector2(40f, 40f);
        cRect.anchoredPosition = new Vector2(-25f, 0f);
        var cBtn = closeGo.AddComponent<Button>();
        var cImg = closeGo.AddComponent<Image>();
        cImg.color = new Color(0.35f, 0.4f, 0.5f, 0.8f);

        var cTextGo = new GameObject("X");
        cTextGo.transform.SetParent(closeGo.transform, false);
        var ctRect = cTextGo.AddComponent<RectTransform>();
        ctRect.anchorMin = Vector2.zero;
        ctRect.anchorMax = Vector2.one;
        ctRect.sizeDelta = Vector2.zero;
        var ctText = cTextGo.AddComponent<Text>();
        ctText.font = font;
        ctText.fontSize = 20;
        ctText.alignment = TextAnchor.MiddleCenter;
        ctText.color = Color.white;
        ctText.text = "✕";
        cBtn.onClick.AddListener(() => SetVisible(false));

        // ── 2カラムコンテナ ──
        var bodyContainer = new GameObject("BodyContainer");
        bodyContainer.transform.SetParent(_panelRoot.transform, false);
        var bcRect = bodyContainer.AddComponent<RectTransform>();
        bcRect.anchorMin = Vector2.zero;
        bcRect.anchorMax = Vector2.one;
        bcRect.offsetMin = new Vector2(20f, 20f);
        bcRect.offsetMax = new Vector2(-20f, -75f);

        // ── 左カラム：素材ポーチ (幅 260) ──
        BuildPouchColumn(bodyContainer.transform, font);

        // ── 右カラム：アクセサリーカタログ (幅 660) ──
        BuildCatalogColumn(bodyContainer.transform, font);

        _panelRoot.SetActive(false);
    }

    void BuildPouchColumn(Transform parent, Font font)
    {
        var pouchGo = new GameObject("PouchColumn");
        pouchGo.transform.SetParent(parent, false);
        var r = pouchGo.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(0.28f, 1f);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;

        var bg = pouchGo.AddComponent<Image>();
        bg.color = new Color(0.07f, 0.10f, 0.16f, 0.95f);

        var vlg = pouchGo.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 16, 16);
        vlg.spacing = 10;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // 見出し
        CreateTextItem(pouchGo.transform, "✦ 素材ポーチ", 18, new Color(0.95f, 0.85f, 0.45f), font, TextAnchor.MiddleLeft);
        CreateTextItem(pouchGo.transform, "浜辺の波打ち際で拾った宝物", 12, new Color(0.7f, 0.75f, 0.85f), font, TextAnchor.MiddleLeft);

        // アイテムリスト
        _textSakuragai = CreatePouchRow(pouchGo.transform, "🌸 桜色のサクラガイ", font);
        _textEmerald = CreatePouchRow(pouchGo.transform, "🟢 エメラルド硝子", font);
        _textSapphire = CreatePouchRow(pouchGo.transform, "🔷 サファイア硝子", font);
        _textAmber = CreatePouchRow(pouchGo.transform, "☀️ 太陽の小琥珀", font);
        _textConch = CreatePouchRow(pouchGo.transform, "🐚 純白の小巻貝", font);

        // 下部ガイド
        var guide = CreateTextItem(pouchGo.transform, "\n【ヒント】\n砂浜のキラキラ光る場所へ行き、[Eキー] で拾えます。\n全24個の貝殻が波打ち際に漂着しています。", 12, new Color(0.65f, 0.75f, 0.85f), font, TextAnchor.MiddleLeft);
    }

    Text CreatePouchRow(Transform parent, string title, Font font)
    {
        var row = new GameObject("PouchRow");
        row.transform.SetParent(parent, false);
        var r = row.AddComponent<RectTransform>();
        r.sizeDelta = new Vector2(0f, 32f);

        var hl = row.AddComponent<HorizontalLayoutGroup>();
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = true;

        var nameText = CreateTextItem(row.transform, title, 13, Color.white, font, TextAnchor.MiddleLeft);
        var nRect = nameText.GetComponent<RectTransform>();
        nRect.sizeDelta = new Vector2(150f, 30f);

        var valText = CreateTextItem(row.transform, "0 個", 13, new Color(1f, 0.9f, 0.4f), font, TextAnchor.MiddleRight);
        var vRect = valText.GetComponent<RectTransform>();
        vRect.sizeDelta = new Vector2(60f, 30f);

        return valText;
    }

    void BuildCatalogColumn(Transform parent, Font font)
    {
        var catGo = new GameObject("CatalogColumn");
        catGo.transform.SetParent(parent, false);
        var r = catGo.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0.30f, 0f);
        r.anchorMax = new Vector2(1f, 1f);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;

        var bg = catGo.AddComponent<Image>();
        bg.color = new Color(0.07f, 0.10f, 0.16f, 0.95f);

        var vlg = catGo.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 16, 16);
        vlg.spacing = 10;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // 全5アクセサリーカードの構築
        for (int i = 0; i < AdventureRustCosmetics.AllDefs.Length; i++)
        {
            var def = AdventureRustCosmetics.AllDefs[i];
            var card = BuildCosmeticCard(catGo.transform, def, font);
            _cardUIs.Add(card);
        }
    }

    CosmeticCardUI BuildCosmeticCard(Transform parent, AdventureRustCosmetics.CosmeticDef def, Font font)
    {
        var cardObj = new GameObject("Card_" + def.id);
        cardObj.transform.SetParent(parent, false);
        var r = cardObj.AddComponent<RectTransform>();
        r.sizeDelta = new Vector2(0f, 78f);

        var cardBg = cardObj.AddComponent<Image>();
        cardBg.color = new Color(0.12f, 0.17f, 0.26f, 0.95f);

        var hl = cardObj.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(14, 14, 8, 8);
        hl.spacing = 12;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = true;

        // 左部情報（名前・スロット・説明文・コスト）
        var infoGo = new GameObject("Info");
        infoGo.transform.SetParent(cardObj.transform, false);
        var infoRect = infoGo.AddComponent<RectTransform>();
        infoRect.sizeDelta = new Vector2(460f, 62f);

        var vlg = infoGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 3;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        string slotTag = $"[{def.slot}]";
        string hexCol = ColorUtility.ToHtmlStringRGB(def.themeColor);
        var titleText = CreateTextItem(infoGo.transform, $"<color=#{hexCol}><b>✦ {def.displayName}</b></color>  <size=12><color=#AABBCC>{slotTag}</color></size>", 15, Color.white, font, TextAnchor.MiddleLeft);
        var descText = CreateTextItem(infoGo.transform, def.description, 11, new Color(0.8f, 0.85f, 0.92f), font, TextAnchor.MiddleLeft);
        var costText = CreateTextItem(infoGo.transform, "必要: ...", 11, new Color(1f, 0.85f, 0.5f), font, TextAnchor.MiddleLeft);

        // 右部（ステータス＆アクションボタン）
        var actionGo = new GameObject("Action");
        actionGo.transform.SetParent(cardObj.transform, false);
        var actRect = actionGo.AddComponent<RectTransform>();
        actRect.sizeDelta = new Vector2(130f, 62f);

        var actVlg = actionGo.AddComponent<VerticalLayoutGroup>();
        actVlg.spacing = 4;
        actVlg.childForceExpandWidth = true;
        actVlg.childForceExpandHeight = false;

        var statusText = CreateTextItem(actionGo.transform, "未作成", 11, Color.gray, font, TextAnchor.MiddleCenter);

        var btnGo = new GameObject("ActionBtn");
        btnGo.transform.SetParent(actionGo.transform, false);
        var bRect = btnGo.AddComponent<RectTransform>();
        bRect.sizeDelta = new Vector2(120f, 32f);

        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(1f, 0.72f, 0.25f, 1f);

        var btn = btnGo.AddComponent<Button>();

        var btnTextGo = new GameObject("BtnText");
        btnTextGo.transform.SetParent(btnGo.transform, false);
        var btRect = btnTextGo.AddComponent<RectTransform>();
        btRect.anchorMin = Vector2.zero;
        btRect.anchorMax = Vector2.one;
        btRect.sizeDelta = Vector2.zero;

        var btnText = btnTextGo.AddComponent<Text>();
        btnText.font = font;
        btnText.fontSize = 13;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.color = new Color(0.08f, 0.1f, 0.15f);
        btnText.text = "✦ つくる";

        var cardUI = new CosmeticCardUI
        {
            def = def,
            rootGo = cardObj,
            titleText = titleText,
            descText = descText,
            costText = costText,
            statusText = statusText,
            actionBtn = btn,
            actionBtnText = btnText,
            actionBtnImage = btnImg
        };

        btn.onClick.AddListener(() => OnCardButtonClicked(cardUI));
        return cardUI;
    }

    Text CreateTextItem(Transform parent, string content, int size, Color col, Font font, TextAnchor align)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = col;
        t.alignment = align;
        t.text = content;
        return t;
    }
    #endregion

    #region Workbench World Spot
    void SpawnWorkbenchSpot()
    {
        var land = Terrain.activeTerrain;
        Vector3 pos = WorkbenchPosition;
        if (land != null)
        {
            pos.y = land.SampleHeight(pos) + land.transform.position.y;
        }

        _workbenchWorldObj = new GameObject("RustCraft_WorkbenchSpot");
        _workbenchWorldObj.transform.position = pos;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // 作業台テーブル（木製作業机）
        var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
        table.name = "TableBoard";
        table.transform.SetParent(_workbenchWorldObj.transform, false);
        table.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        table.transform.localScale = new Vector3(1.8f, 0.12f, 1.0f);
        var woodMat = new Material(shader);
        woodMat.SetColor("_BaseColor", new Color(0.42f, 0.28f, 0.18f));
        table.GetComponent<Renderer>().sharedMaterial = woodMat;

        // 工具箱・パーツ箱
        var toolBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        toolBox.name = "Toolbox";
        toolBox.transform.SetParent(_workbenchWorldObj.transform, false);
        toolBox.transform.localPosition = new Vector3(-0.55f, 0.80f, 0.15f);
        toolBox.transform.localScale = new Vector3(0.45f, 0.25f, 0.35f);
        var boxMat = new Material(shader);
        boxMat.SetColor("_BaseColor", new Color(0.65f, 0.25f, 0.22f));
        toolBox.GetComponent<Renderer>().sharedMaterial = boxMat;

        // 温かいランプ
        var lamp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lamp.name = "Lamp";
        lamp.transform.SetParent(_workbenchWorldObj.transform, false);
        lamp.transform.localPosition = new Vector3(0.60f, 0.85f, -0.2f);
        lamp.transform.localScale = new Vector3(0.18f, 0.25f, 0.18f);
        var lampMat = new Material(shader);
        lampMat.SetColor("_BaseColor", new Color(1f, 0.85f, 0.5f));
        lampMat.EnableKeyword("_EMISSION");
        lampMat.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.4f) * 1.5f);
        lamp.GetComponent<Renderer>().sharedMaterial = lampMat;

        var light = lamp.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.82f, 0.55f);
        light.range = 4.5f;
        light.intensity = 1.4f;

        // 3Dインタラクションプロンプト Canvas
        var promptCanvasGo = new GameObject("WorkbenchPrompt_Canvas");
        promptCanvasGo.transform.SetParent(_workbenchWorldObj.transform, false);
        promptCanvasGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);

        var canvas = promptCanvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var cRect = promptCanvasGo.GetComponent<RectTransform>();
        cRect.sizeDelta = new Vector2(280f, 60f);
        cRect.localScale = Vector3.one * 0.012f;

        _promptCg = promptCanvasGo.AddComponent<CanvasGroup>();
        _promptCg.alpha = 0f;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var textGo = new GameObject("PromptText");
        textGo.transform.SetParent(promptCanvasGo.transform, false);
        var tRect = textGo.AddComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.sizeDelta = Vector2.zero;

        _promptText = textGo.AddComponent<Text>();
        _promptText.font = font;
        _promptText.fontSize = 22;
        _promptText.alignment = TextAnchor.MiddleCenter;
        _promptText.color = new Color(1f, 0.95f, 0.65f);
        _promptText.text = "<b>[E] Rustの着せ替え工房</b>\n<size=16><color=#DDEEFF>または [B]キーでどこでも開く</color></size>";

        // ビルボード（常にカメラの方を向く）
        promptCanvasGo.AddComponent<BillboardLookAtCamera>();
    }

    class BillboardLookAtCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                transform.rotation = cam.transform.rotation;
            }
        }
    }
    #endregion
}
