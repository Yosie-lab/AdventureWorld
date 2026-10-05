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

    // クラフトUI音響
    AudioSource _uiAudioSource;
    AudioClip _craftSuccessClip;
    AudioClip _equipClip;
    AudioClip _errorClip;

    static readonly Vector3 WorkbenchPosition = new Vector3(149.0f, 6.25f, 278.0f);

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
        var existing = Object.FindAnyObjectByType<AdventureRustWorkshopUI>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject("AdventureRustWorkshopUI");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AdventureRustWorkshopUI>();
    }

    public static void EnsureEventSystemForUi()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null)
        {
            var go = new GameObject("EventSystem");
            es = go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        }

        var legacy = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        if (legacy != null)
            Destroy(legacy);

        if (es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
            es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        EnsureEventSystemForUi();
        SetupAudio();
        CreateUI();
        SpawnWorkbenchSpot();
    }

    void SetupAudio()
    {
        _uiAudioSource = gameObject.AddComponent<AudioSource>();
        _uiAudioSource.spatialBlend = 0f;
        _uiAudioSource.playOnAwake = false;

        _craftSuccessClip = CreateCraftSuccessClip();
        _equipClip = CreateEquipClip();
        _errorClip = CreateErrorClip();
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
        bool prologueActive = AdventurePrologueDrama.Instance != null && AdventurePrologueDrama.Instance.IsPrologueActive;
        bool gameStarted = AdventureRustFloatOpening.IsGameStarted;

        // Bキーで工房トグル開閉（新旧InputSystem両対応、プロローグ完了後のみ有効）
        bool bPressed = false;
        try
        {
            if (AdventureInputReader.Keyboard?.bKey.wasPressedThisFrame == true) bPressed = true;
            if (Input.GetKeyDown(KeyCode.B)) bPressed = true;
        }
        catch { }

        if (bPressed && !AdventurePauseMenu.IsOpen && gameStarted && !prologueActive)
        {
            SetVisible(!isVisible);
        }

        // ESCキーで閉じる
        if (AdventureInputReader.EscapeDown && isVisible)
        {
            SetVisible(false);
        }

        // 工房が開いている時の数字キー【1】〜【5】ショートカット＆マウスクリック処理
        if (isVisible)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) TriggerCardByIndex(0);
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) TriggerCardByIndex(1);
                if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) TriggerCardByIndex(2);
                if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) TriggerCardByIndex(3);
                if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) TriggerCardByIndex(4);
            }
            try
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) TriggerCardByIndex(0);
                if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) TriggerCardByIndex(1);
                if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) TriggerCardByIndex(2);
                if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) TriggerCardByIndex(3);
                if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) TriggerCardByIndex(4);
            }
            catch { }

            UpdateDirectMouseClicks();
        }

        // 作業台への接近判定とEキー入力
        UpdateWorkbenchInteraction();
    }

    void TriggerCardByIndex(int index)
    {
        if (index >= 0 && index < _cardUIs.Count)
        {
            var card = _cardUIs[index];
            if (card != null)
            {
                OnCardButtonClicked(card);
            }
        }
    }

    void OnGUI()
    {
        bool prologueActive = AdventurePrologueDrama.Instance != null && AdventurePrologueDrama.Instance.IsPrologueActive;

        // 1. 工房が閉じている時：画面右上に常設の「👗 Rust工房 (B)」GUIボタン（プロローグ完了後のみ表示）
        if (!isVisible && !AdventurePauseMenu.IsOpen && AdventureRustFloatOpening.IsGameStarted && !prologueActive)
        {
            Rect btnRect = new Rect(Screen.width - 160, 56, 145, 34);
            GUI.color = new Color(0.2f, 0.85f, 1f, 0.95f);
            if (GUI.Button(btnRect, "👗 Rust工房 [B]"))
            {
                SetVisible(true);
            }
            GUI.color = Color.white;
            return;
        }

        // 2. 工房が開いている時のクリック安全網
        if (!isVisible) return;
        Event e = Event.current;
        if (e == null) return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Vector2 mouseScreen = new Vector2(e.mousePosition.x, Screen.height - e.mousePosition.y);
            for (int i = 0; i < _cardUIs.Count; i++)
            {
                var card = _cardUIs[i];
                if (card == null) continue;

                var rt = card.actionBtn != null ? card.actionBtn.GetComponent<RectTransform>() : null;
                if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, mouseScreen, null))
                {
                    OnCardButtonClicked(card);
                    e.Use();
                    return;
                }
            }
        }
    }

    void UpdateDirectMouseClicks()
    {
        var mouse = UnityEngine.InputSystem.Mouse.current;
        bool leftPressed = mouse != null && mouse.leftButton.wasPressedThisFrame;
        try { if (Input.GetMouseButtonDown(0)) leftPressed = true; } catch { }

        if (!leftPressed) return;

        Vector2 mousePos = mouse != null ? mouse.position.ReadValue() : (Vector2)Input.mousePosition;

        for (int i = 0; i < _cardUIs.Count; i++)
        {
            var card = _cardUIs[i];
            if (card == null || card.actionBtn == null) continue;

            var rt = card.actionBtn.GetComponent<RectTransform>();
            if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, mousePos, null))
            {
                OnCardButtonClicked(card);
                return;
            }
        }
    }

    void UpdateWorkbenchInteraction()
    {
        var player = AdventurePlayerController.Instance;
        if (player == null || _workbenchWorldObj == null) return;

        // 1. オープニング前・プロローグドラマ中（Rust遭難・注油・蘇生中）は作業台を完全休止
        if (!AdventureRustFloatOpening.IsGameStarted)
        {
            if (_promptCg != null) _promptCg.alpha = 0f;
            return;
        }

        if (AdventurePrologueDrama.Instance != null && AdventurePrologueDrama.Instance.IsPrologueActive)
        {
            if (_promptCg != null) _promptCg.alpha = 0f;
            return;
        }

        // 2. Rustにプレイヤーが接近している時はRustへの注油・手当て・会話を最優先（作業台インタラクトを遮断）
        var drone = AdventureRustDrone.Instance ?? Object.FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            float rustDist = Vector3.Distance(player.transform.position, drone.transform.position);
            if (drone.IsPlayerNear || rustDist < 4.0f)
            {
                if (_promptCg != null) _promptCg.alpha = 0f;
                return;
            }
        }

        // 3. 作業台との水平距離および向き判定（正面から机を見た時のみ有効）
        Vector3 toBench = WorkbenchPosition - player.transform.position;
        toBench.y = 0f;
        float dist = toBench.magnitude;
        bool isLookingAtBench = dist > 0.05f && Vector3.Dot(player.transform.forward, toBench.normalized) > 0.40f;
        bool isNear = dist <= 2.4f && isLookingAtBench && !isVisible && !AdventurePauseMenu.IsOpen;

        // プロンプト表示フェード
        if (_promptCg != null)
        {
            _promptCg.alpha = Mathf.MoveTowards(_promptCg.alpha, isNear ? 1.0f : 0.0f, Time.deltaTime * 6f);
        }

        if (isNear)
        {
            if (AdventureInputReader.InteractDown)
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
            EnsureEventSystemForUi();
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            RefreshUI();
        }
        else
        {
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

            int keyIndex = i + 1;
            if (isUnlocked)
            {
                if (isEquipped)
                {
                    card.statusText.text = "<color=#66FFAA><b>✦ そうび中</b></color>";
                    card.actionBtnText.text = $"[{keyIndex}] はずす";
                    card.actionBtnText.color = Color.white;
                    card.actionBtnImage.color = new Color(0.35f, 0.45f, 0.55f, 0.95f);
                    card.actionBtn.interactable = true;
                }
                else
                {
                    card.statusText.text = "<color=#BBBBCC>所持中</color>";
                    card.actionBtnText.text = $"[{keyIndex}] そうび";
                    card.actionBtnText.color = new Color(0.10f, 0.12f, 0.18f);
                    card.actionBtnImage.color = new Color(0.2f, 0.75f, 0.55f, 0.95f);
                    card.actionBtn.interactable = true;
                }
                card.costText.text = "<color=#AAAAAA>作成済み</color>";
            }
            else
            {
                string kindName = GetKindName(card.def.requiredKind);
                string hexCol = ColorUtility.ToHtmlStringRGB(card.def.themeColor);

                int curCount = shellMgr != null ? shellMgr.GetShellCount(card.def.requiredKind) : 0;
                string countColor = curCount >= card.def.requiredCount ? "#66FFAA" : "#FF7777";

                card.costText.text = $"必要: <color=#{hexCol}>{kindName}</color> <color={countColor}>{curCount}/{card.def.requiredCount}</color>";

                if (canCraft)
                {
                    card.statusText.text = "<color=#FFAA33><b>✦ 作成可能！</b></color>";
                    card.actionBtnText.text = $"[{keyIndex}] ✦ 作成する";
                    card.actionBtnText.color = new Color(0.10f, 0.12f, 0.18f);
                    card.actionBtnImage.color = new Color(1f, 0.78f, 0.22f, 1f);
                    card.actionBtn.interactable = true;
                }
                else
                {
                    card.statusText.text = "<color=#AA9988>素材を集めて作成</color>";
                    card.actionBtnText.text = $"[{keyIndex}] ✦ 作成する";
                    card.actionBtnText.color = new Color(0.96f, 0.96f, 0.96f);
                    card.actionBtnImage.color = new Color(0.50f, 0.36f, 0.22f, 1f);
                    card.actionBtn.interactable = true; // クリック可能！押したときに不足素材を案内！
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
            // クラフト実行
            if (cosmetics.CraftAndEquip(card.def.id))
            {
                PlayCraftSuccessSound();
                RefreshUI();
            }
            else
            {
                // 素材不足時のフィードバック音＆Rustセリフ案内
                var shellMgr = AdventureBeachSeashellManager.Instance;
                int count = shellMgr != null ? shellMgr.GetShellCount(card.def.requiredKind) : 0;
                string kindName = GetKindName(card.def.requiredKind);
                int needed = Mathf.Max(1, card.def.requiredCount - count);
                PlayErrorSound();
                var drone = AdventureRustDrone.Instance;
                drone?.SetSpeech($"ピピッ！「{card.def.displayName}」を作るには「{kindName}」があと{needed}個必要だよ！砂浜の波打ち際で拾おう！", 4.5f);
            }
        }
        else
        {
            // 装備切替
            bool isEquipped = cosmetics.IsEquipped(card.def.id);
            cosmetics.SetEquipped(card.def.id, !isEquipped);
            PlayEquipSound();
            RefreshUI();
        }
    }

    void PlayErrorSound()
    {
        if (_uiAudioSource != null && _errorClip != null)
        {
            _uiAudioSource.pitch = 0.95f;
            _uiAudioSource.PlayOneShot(_errorClip, 0.60f);
        }
    }



    void PlayCraftSuccessSound()
    {
        if (_uiAudioSource != null && _craftSuccessClip != null)
        {
            _uiAudioSource.pitch = 1.0f;
            _uiAudioSource.PlayOneShot(_craftSuccessClip, 0.75f);
        }
    }

    void PlayEquipSound()
    {
        if (_uiAudioSource != null && _equipClip != null)
        {
            _uiAudioSource.pitch = 1.05f;
            _uiAudioSource.PlayOneShot(_equipClip, 0.65f);
        }
    }

    #region UI Construction
    void CreateUI()
    {
        var canvasGo = new GameObject("RustWorkshop_Canvas");
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 600; // 最前面に表示して他のHUDにクリックを阻害されないようにする

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
        r.sizeDelta = new Vector2(0f, 86f);

        // 縦レイアウトで確実に高さを保証するLayoutElement
        var le = cardObj.AddComponent<LayoutElement>();
        le.minHeight = 86f;
        le.preferredHeight = 86f;
        le.flexibleHeight = 0f;

        var cardBg = cardObj.AddComponent<Image>();
        cardBg.color = new Color(0.11f, 0.16f, 0.25f, 0.96f);
        cardBg.raycastTarget = false;

        var cardOutline = cardObj.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.2f, 0.35f, 0.5f, 0.45f);
        cardOutline.effectDistance = new Vector2(1f, -1f);

        // ── 左部：情報エリア（左端から右側ボタンの手前まで広く確保） ──
        var infoGo = new GameObject("Info");
        infoGo.transform.SetParent(cardObj.transform, false);
        var infoRect = infoGo.AddComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0f, 0f);
        infoRect.anchorMax = new Vector2(1f, 1f);
        infoRect.offsetMin = new Vector2(16f, 6f);
        infoRect.offsetMax = new Vector2(-185f, -6f); // 右側の作成ボタン(幅165)と絶対に重ならない

        string slotTag = $"[{def.slot}]";
        string hexCol = ColorUtility.ToHtmlStringRGB(def.themeColor);

        // タイトル（Y=18）
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(infoGo.transform, false);
        var titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -2f);
        titleRt.sizeDelta = new Vector2(0f, 24f);
        var titleText = titleGo.AddComponent<Text>();
        titleText.font = font;
        titleText.fontSize = 15;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
        titleText.verticalOverflow = VerticalWrapMode.Overflow;
        titleText.text = $"<color=#{hexCol}><b>✦ {def.displayName}</b></color>  <size=12><color=#AABBCC>{slotTag}</color></size>";
        titleText.raycastTarget = false;

        // 説明文（Y=0）
        var descGo = new GameObject("Desc");
        descGo.transform.SetParent(infoGo.transform, false);
        var descRt = descGo.AddComponent<RectTransform>();
        descRt.anchorMin = new Vector2(0f, 0.5f);
        descRt.anchorMax = new Vector2(1f, 0.5f);
        descRt.pivot = new Vector2(0f, 0.5f);
        descRt.anchoredPosition = new Vector2(0f, 1f);
        descRt.sizeDelta = new Vector2(0f, 20f);
        var descText = descGo.AddComponent<Text>();
        descText.font = font;
        descText.fontSize = 11;
        descText.color = new Color(0.80f, 0.86f, 0.94f);
        descText.alignment = TextAnchor.MiddleLeft;
        descText.horizontalOverflow = HorizontalWrapMode.Wrap;
        descText.verticalOverflow = VerticalWrapMode.Overflow;
        descText.text = def.description;
        descText.raycastTarget = false;

        // 必要素材表示（Y=-20）
        var costGo = new GameObject("Cost");
        costGo.transform.SetParent(infoGo.transform, false);
        var costRt = costGo.AddComponent<RectTransform>();
        costRt.anchorMin = new Vector2(0f, 0f);
        costRt.anchorMax = new Vector2(1f, 0f);
        costRt.pivot = new Vector2(0f, 0f);
        costRt.anchoredPosition = new Vector2(0f, 3f);
        costRt.sizeDelta = new Vector2(0f, 20f);
        var costText = costGo.AddComponent<Text>();
        costText.font = font;
        costText.fontSize = 12;
        costText.color = new Color(1f, 0.85f, 0.5f);
        costText.alignment = TextAnchor.MiddleLeft;
        costText.horizontalOverflow = HorizontalWrapMode.Overflow;
        costText.verticalOverflow = VerticalWrapMode.Overflow;
        costText.text = "必要素材: ...";
        costText.raycastTarget = false;

        // ── 右部：カード右端に確実に固定される特大作成ボタン領域 ──
        var rightArea = new GameObject("RightArea");
        rightArea.transform.SetParent(cardObj.transform, false);
        var raRt = rightArea.AddComponent<RectTransform>();
        raRt.anchorMin = new Vector2(1f, 0.5f);
        raRt.anchorMax = new Vector2(1f, 0.5f);
        raRt.pivot = new Vector2(1f, 0.5f);
        raRt.anchoredPosition = new Vector2(-16f, 0f);
        raRt.sizeDelta = new Vector2(168f, 74f);

        // ステータステキスト（ボタンの上端に小さく中央揃え）
        var statusGo = new GameObject("Status");
        statusGo.transform.SetParent(rightArea.transform, false);
        var sRt = statusGo.AddComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 1f);
        sRt.anchorMax = new Vector2(1f, 1f);
        sRt.pivot = new Vector2(0.5f, 1f);
        sRt.anchoredPosition = new Vector2(0f, 0f);
        sRt.sizeDelta = new Vector2(168f, 18f);
        var statusText = statusGo.AddComponent<Text>();
        statusText.font = font;
        statusText.fontSize = 11;
        statusText.alignment = TextAnchor.MiddleCenter;
        statusText.horizontalOverflow = HorizontalWrapMode.Overflow;
        statusText.verticalOverflow = VerticalWrapMode.Overflow;
        statusText.color = Color.gray;
        statusText.text = "未作成";
        statusText.raycastTarget = false;

        // 特大作成ボタン（幅165px、高さ46px：誰が見てもひと目でわかる立体ゴールドボタン！）
        var btnGo = new GameObject("ActionBtn");
        btnGo.transform.SetParent(rightArea.transform, false);
        var bRect = btnGo.AddComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0.5f, 0f);
        bRect.anchorMax = new Vector2(0.5f, 0f);
        bRect.pivot = new Vector2(0.5f, 0f);
        bRect.anchoredPosition = new Vector2(0f, 3f);
        bRect.sizeDelta = new Vector2(165f, 46f);

        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(1f, 0.78f, 0.22f, 1f);
        btnImg.raycastTarget = true;

        var btnOutline = btnGo.AddComponent<Outline>();
        btnOutline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        btnOutline.effectDistance = new Vector2(1.5f, -1.5f);

        var btn = btnGo.AddComponent<Button>();

        var btnTextGo = new GameObject("BtnText");
        btnTextGo.transform.SetParent(btnGo.transform, false);
        var btRect = btnTextGo.AddComponent<RectTransform>();
        btRect.anchorMin = Vector2.zero;
        btRect.anchorMax = Vector2.one;
        btRect.sizeDelta = Vector2.zero;

        var btnText = btnTextGo.AddComponent<Text>();
        btnText.font = font;
        btnText.fontSize = 14;
        btnText.fontStyle = FontStyle.Bold;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.horizontalOverflow = HorizontalWrapMode.Overflow;
        btnText.verticalOverflow = VerticalWrapMode.Overflow;
        btnText.color = new Color(0.10f, 0.12f, 0.18f);
        btnText.text = "✦ 作成する";
        btnText.raycastTarget = false;

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
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false; // UIテキスト全般が背後や親ボタンのRaycastを邪魔しないように設定
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

    #region Audio Synthesis
    static AudioClip CreateCraftSuccessClip()
    {
        int rate = 44100;
        float duration = 0.45f;
        int count = Mathf.RoundToInt(rate * duration);
        float[] samples = new float[count];

        // 澄んだクラフト成功ファンファーレ（C6 -> E6 -> G6 -> C7）
        float[] notes = { 1046.50f, 1318.51f, 1567.98f, 2093.00f };
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float sum = 0f;
            for (int n = 0; n < notes.Length; n++)
            {
                float noteT = t - n * 0.055f;
                if (noteT >= 0f)
                {
                    float env = Mathf.Exp(-noteT * 12f);
                    sum += Mathf.Sin(2f * Mathf.PI * notes[n] * noteT) * env * 0.25f;
                }
            }
            samples[i] = Mathf.Clamp(sum, -1f, 1f);
        }

        var clip = AudioClip.Create("CraftSuccess", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    static AudioClip CreateEquipClip()
    {
        int rate = 44100;
        float duration = 0.20f;
        int count = Mathf.RoundToInt(rate * duration);
        float[] samples = new float[count];

        // 軽快な装着カチャッ音（短く小気味よいクリック）
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float env = Mathf.Exp(-t * 28f);
            float s = Mathf.Sin(2f * Mathf.PI * 1800f * t) * env * 0.35f
                    + Mathf.Sin(2f * Mathf.PI * 2800f * t) * env * 0.20f;
            samples[i] = Mathf.Clamp(s, -1f, 1f);
        }

        var clip = AudioClip.Create("EquipClick", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    static AudioClip CreateErrorClip()
    {
        int rate = 44100;
        float duration = 0.22f;
        int count = Mathf.RoundToInt(rate * duration);
        float[] samples = new float[count];

        // 優しい注意音（低めのポコッ音）
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float env = Mathf.Exp(-t * 22f);
            float s = Mathf.Sin(2f * Mathf.PI * 340f * t) * env * 0.35f;
            samples[i] = Mathf.Clamp(s, -1f, 1f);
        }

        var clip = AudioClip.Create("WorkshopError", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
    #endregion
}
