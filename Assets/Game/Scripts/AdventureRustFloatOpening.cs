using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;
using System.IO;

/// <summary>
/// 『Rust & Float』本格タイトル画面（Title Menu）＆ プロローグシステム。
/// 【つづきから】【はじめから】【設定】【終了】を備えた製品クオリティのメインメニューを提供し、
/// セーブデータの状況に応じてスムーズにゲームプレイへ突入する。
/// </summary>
public class AdventureRustFloatOpening : MonoBehaviour
{
    private static AdventureRustFloatOpening _instance;
    public static AdventureRustFloatOpening Instance => _instance != null ? _instance : (_instance = FindAnyObjectByType<AdventureRustFloatOpening>());

    public static bool IsGameStarted
    {
        get => _isGameStarted;
        set => _isGameStarted = value;
    }
    static bool _isGameStarted = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _isGameStarted = false;
        _inputGuardUntil = 0f;
        _instance = null;
    }

    const int TitleCanvasSortOrder = 860;

    const string TitleText = "✦ Rust & Float ✦";
    const string SubTitleText = "〜 2050 静かなる脱出 〜";
    const string StoryText =
        "西暦2050年。100%最適化された無痛の幸福を脱獄した。\n\n" +
        "荒波を越え、気がつくとこの波静かな白砂のビーチに打ち上げられていた。\n" +
        "頬を打つ冷たい潮風も、駆け抜けた足の痛みさえ、\n" +
        "ここではすべてが生きている証そのものだ。\n\n" +
        "……隣には、スクラップ寸前で連れ出した相棒ドローン、Rust。\n" +
        "島を巡って翼を直し、あの自由な蒼穹（そら）へ飛び立とう。";

    GameObject _canvasGo;
    GameObject _overlayGo;
    GameObject _modalBoard;
    CanvasGroup _modalCg;
    Text _guideText;

    // メインメニューボタン群
    Button _continueBtn;
    Text _continueBtnLabel;
    Text _continueSubText;
    Image _continueBtnImg;

    Button _newGameBtn;
    Button _settingsBtn;
    Button _quitBtn;

    Text _subTitleText;
    Text _storyHeaderText;
    Text _storyBodyText;
    Text _menuHeaderText;
    Text _newGameBtnLabel;
    Text _settingsBtnLabel;
    Text _quitBtnLabel;
    Text _footLeftText;

    // 新規開始時の上書き確認モーダル
    GameObject _newGameConfirmModal;
    Button _confirmNewGameYesBtn;
    Button _confirmNewGameNoBtn;
    Text _confirmModalTitle;
    Text _confirmModalDesc;
    Text _confirmModalYesBtnLabel;
    Text _confirmModalNoBtnLabel;

    bool _isClosing = false;
    float _openTime = 0f;
    static float _inputGuardUntil = 0f;

    public Text GuideText => _guideText;

    /// <summary>オープニング決定後、入力の誤爆（ジャンプや滑空）を防止する入力ガード中か判定</summary>
    public static bool IsInputGuarded => Time.unscaledTime < _inputGuardUntil;

    /// <summary>移動入力があった場合、入力ガードを即座に破棄して歩行を開始</summary>
    public static void DismissInputGuard()
    {
        _inputGuardUntil = 0f;
    }

    public bool IsModalBoardOpen()
    {
        return !IsGameStarted && _modalBoard != null && _modalBoard.activeSelf;
    }

    /// <summary>レバー操作などゲームプレイ優先時にオープニングボードを強制閉じ</summary>
    public void ForceDismissForGameplay()
    {
        StopAllCoroutines();
        _isClosing = false;
        IsGameStarted = true;
        if (_overlayGo != null) _overlayGo.SetActive(false);
        if (_modalBoard != null) _modalBoard.SetActive(false);
        if (_modalCg != null) _modalCg.alpha = 0f;
        if (_guideText != null) _guideText.gameObject.SetActive(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>進行中セーブのロード後など、ボードを出さずゲーム継続</summary>
    public void DismissBecauseContinuingSave()
    {
        if (_canvasGo == null)
            BuildHud();
        ForceDismissForGameplay();
        if (_guideText != null)
        {
            _guideText.text = AdventureLocalizationManager.Get("guide_hud_opening");
            _guideText.gameObject.SetActive(true);
        }
    }

    /// <summary>タイトル画面を再表示（ポーズメニューやF8キーから呼ばれる）</summary>
    public void ShowTitleScreen()
    {
        StopAllCoroutines();
        _isClosing = false;
        _openTime = Time.realtimeSinceStartup;

        if (_canvasGo == null)
            BuildHud();

        if (_overlayGo != null) _overlayGo.SetActive(true);
        EnsureTitleStoryTextVisible();
        if (_modalBoard != null)
        {
            _modalBoard.SetActive(true);
            if (_modalCg != null) _modalCg.alpha = 1f;
        }
        if (_newGameConfirmModal != null) _newGameConfirmModal.SetActive(false);
        if (_guideText != null) _guideText.gameObject.SetActive(false);

        // 前面へ
        var canvas = _canvasGo != null ? _canvasGo.GetComponent<Canvas>() : null;
        if (canvas != null) canvas.sortingOrder = TitleCanvasSortOrder;

        IsGameStarted = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        RefreshSaveDataStatus();
        AdventurePauseMenu.ForceCloseTitleSettings();
        Time.timeScale = 1f;
        RestoreTitleUiInput();
        ApplyLocalization();
        Debug.Log("[RustAndFloat] タイトル画面を表示しました");
    }

    void OnEnable()
    {
        AdventureLocalizationManager.OnLanguageChanged += ApplyLocalization;
        AdventureExplorationHudSettings.OnChanged += SyncTopHudLayout;
        SyncTopHudLayout();
    }

    void OnDisable()
    {
        AdventureLocalizationManager.OnLanguageChanged -= ApplyLocalization;
        AdventureExplorationHudSettings.OnChanged -= SyncTopHudLayout;
    }

    void SyncTopHudLayout()
    {
        ApplyTopHudGuideLayout(AdventureScrapHUD.ComputeCenterNavVisible());
    }

    public static void ApplyTopHudGuideLayout(bool showCenterNavLine)
    {
        var opening = Instance;
        if (opening == null || opening._guideText == null) return;
        var guideRt = opening._guideText.rectTransform;
        guideRt.anchoredPosition = new Vector2(0f, AdventureExplorationHudSettings.GetGuideRowY(showCenterNavLine));
        guideRt.sizeDelta = new Vector2(980f, AdventureExplorationHudSettings.GuideRowHeight);
    }

    void ApplyLocalization()
    {
        if (_subTitleText != null) _subTitleText.text = AdventureLocalizationManager.Get("title_subtitle");
        if (_storyHeaderText != null) _storyHeaderText.text = "📜 " + AdventureLocalizationManager.Get("title_story_head");
        if (_storyBodyText != null) _storyBodyText.text = AdventureLocalizationManager.Get("title_story_body");
        EnsureTitleStoryTextVisible();
        if (_menuHeaderText != null)
        {
            _menuHeaderText.text = AdventureLocalizationManager.CurrentLanguage switch
            {
                AdventureLocalizationManager.GameLanguage.English => "✦ Main Menu",
                AdventureLocalizationManager.GameLanguage.SimplifiedChinese => "✦ 主菜单",
                AdventureLocalizationManager.GameLanguage.Spanish => "✦ Menú Principal",
                _ => "✦ メインメニュー"
            };
        }

        if (_newGameBtnLabel != null) _newGameBtnLabel.text = "✦  " + AdventureLocalizationManager.Get("title_newgame");
        if (_settingsBtnLabel != null) _settingsBtnLabel.text = "⚙  " + AdventureLocalizationManager.Get("title_settings");
        if (_quitBtnLabel != null) _quitBtnLabel.text = "✖  " + AdventureLocalizationManager.Get("title_quit");

        if (_confirmModalTitle != null) _confirmModalTitle.text = AdventureLocalizationManager.Get("confirm_overwrite_title");
        if (_confirmModalDesc != null) _confirmModalDesc.text = AdventureLocalizationManager.Get("confirm_overwrite_desc");
        if (_confirmModalYesBtnLabel != null) _confirmModalYesBtnLabel.text = AdventureLocalizationManager.Get("confirm_overwrite_yes");
        if (_confirmModalNoBtnLabel != null) _confirmModalNoBtnLabel.text = AdventureLocalizationManager.Get("confirm_cancel");

        RefreshSaveDataStatus();
        RefreshGuideText();
    }

    /// <summary>タイトル上の設定を開いている間、メニュー Canvas の入力・描画順を下げる</summary>
    public void SetTitleOverlayForSettings(bool settingsCoveringTitle)
    {
        if (_modalCg != null)
        {
            _modalCg.interactable = !settingsCoveringTitle;
            _modalCg.blocksRaycasts = !settingsCoveringTitle;
        }

        // 描画順は下げず Raycast だけ止める（sort を下げると設定パネルより前に残る環境がある）
        var canvas = _canvasGo != null ? _canvasGo.GetComponent<Canvas>() : null;
        if (canvas != null && !settingsCoveringTitle)
            canvas.sortingOrder = TitleCanvasSortOrder;
    }

    /// <summary>設定閉鎖後など、タイトルボードのクリック・Raycast を確実に復帰させる</summary>
    public void RestoreTitleUiInput()
    {
        if (IsGameStarted || _isClosing) return;

        SetTitleOverlayForSettings(false);

        if (_modalCg != null)
        {
            _modalCg.interactable = true;
            _modalCg.blocksRaycasts = true;
            if (_modalBoard != null && _modalBoard.activeSelf)
                _modalCg.alpha = 1f;
        }

        if (_overlayGo != null && _modalBoard != null && _modalBoard.activeSelf)
            _overlayGo.SetActive(true);

        var canvas = _canvasGo != null ? _canvasGo.GetComponent<Canvas>() : null;
        if (canvas != null)
        {
            canvas.enabled = true;
            canvas.sortingOrder = TitleCanvasSortOrder;
        }
    }

    /// <summary>F8／はじめから等の旧互換</summary>
    public void ShowForNewGame()
    {
        ShowTitleScreen();
    }

    static bool ShouldSkipOpeningBoard() => AdventureStoryFlow.ShouldSkipOpening;

    void Awake()
    {
        _instance = this;
        // スクリプト再コンパイル等で Awake が再走っても、進行中なら初期画面に戻さない
        if (ShouldSkipOpeningBoard())
        {
            _isGameStarted = true;
            return;
        }
        // 通常の新規起動のみ初期化
        if (!_isGameStarted)
            _isGameStarted = false;
    }

    void Start()
    {
        _openTime = Time.realtimeSinceStartup;
        BuildHud();

        // エンディング進行中や既にゲーム開始済みの場合はスキップ
        bool skip = ShouldSkipOpeningBoard() || IsGameStarted;
        if (skip)
        {
            if (_overlayGo != null) _overlayGo.SetActive(false);
            if (_modalBoard != null) _modalBoard.SetActive(false);
            IsGameStarted = true;
            if (_guideText != null) _guideText.gameObject.SetActive(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return;
        }

        // タイトル画面のセーブ状況を更新して表示
        RefreshSaveDataStatus();
        AdventurePauseMenu.CloseIfOpen();
        AdventurePauseMenu.EnsureClosedIfNotOpen();
        if (AdventureRustWorkshopUI.Instance != null)
            AdventureRustWorkshopUI.Instance.SetVisible(false);
        Time.timeScale = 1f;
        RestoreTitleUiInput();

        EnsureTitleStoryTextVisible();

        // スタート前はマウスカーソルを表示・アンロック
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void RefreshSaveDataStatus()
    {
        bool hasSave = AdventureSaveManager.HasSaveData;
        var summary = hasSave ? AdventureSaveManager.GetSaveDataSummary() : null;

        if (_continueBtn != null)
        {
            _continueBtn.interactable = hasSave;
        }

        if (_continueBtnImg != null)
        {
            _continueBtnImg.color = hasSave
                ? new Color(0.12f, 0.58f, 0.72f, 0.96f)
                : new Color(0.10f, 0.16f, 0.22f, 0.60f);
        }

        if (_continueBtnLabel != null)
        {
            _continueBtnLabel.color = hasSave ? Color.white : new Color(0.6f, 0.7f, 0.8f, 0.5f);
            string contText = AdventureLocalizationManager.Get("title_continue");
            string noSaveText = AdventureLocalizationManager.CurrentLanguage switch
            {
                AdventureLocalizationManager.GameLanguage.English => "Continue (No Save)",
                AdventureLocalizationManager.GameLanguage.SimplifiedChinese => "继续游戏 (无存档)",
                AdventureLocalizationManager.GameLanguage.Spanish => "Continuar (Sin partida)",
                _ => "つづきから（記録なし）"
            };
            _continueBtnLabel.text = hasSave ? $"▶  {contText}" : noSaveText;
        }

        if (_continueSubText != null)
        {
            if (hasSave && summary != null)
            {
                _continueSubText.text = AdventureLocalizationManager.GetFormat("title_save_summary", summary.collectedCount, summary.oilCount, summary.saveTime);
                _continueSubText.color = new Color(0.45f, 0.95f, 0.85f, 0.95f);
            }
            else
            {
                _continueSubText.text = AdventureLocalizationManager.Get("title_save_none");
                _continueSubText.color = new Color(0.6f, 0.7f, 0.8f, 0.5f);
            }
        }
    }

    void Update()
    {
        // タイトルモーダルが非表示なのに未開始状態の場合（ドメインリロード時など）、自己修復してゲーム開始状態へ復帰
        if (!IsGameStarted)
        {
            bool titleActive = (_modalBoard != null && _modalBoard.activeInHierarchy) || (_overlayGo != null && _overlayGo.activeInHierarchy);
            if (!titleActive && _canvasGo != null)
            {
                IsGameStarted = true;
                return;
            }
        }

        // エンディング中にタイトルが勝手に復活したときだけ即消す（ShowTitleScreen 意図表示は除外）
        if (!IsGameStarted && ShouldSkipOpeningBoard())
        {
            bool titleMenuVisible = _modalBoard != null && _modalBoard.activeSelf;
            if (!titleMenuVisible)
            {
                ForceDismissForGameplay();
                return;
            }
        }

        bool titleMenuUp = IsModalBoardOpen()
            || (_newGameConfirmModal != null && _newGameConfirmModal.activeSelf);
        if (!titleMenuUp && (IsGameStarted || _isClosing))
            return;

        // 起動直後（0.3秒間）は誤爆防止
        if (!titleMenuUp && Time.realtimeSinceStartup - _openTime < 0.3f)
            return;

        // スタート前は常にカーソルを表示
        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        AdventurePauseMenu.EnsureEventSystemForUi();
        AdventurePauseMenu.EnsureClosedIfNotOpen();

        if (!AdventurePauseMenu.IsOpen)
        {
            RestoreTitleUiInput();
        }
        else
        {
            SetTitleOverlayForSettings(true);
        }

        if (!IsGameStarted && Time.timeScale < 0.05f && !AdventurePauseMenu.IsOpen)
            Time.timeScale = 1f;

        TryTitleKeyboardShortcuts();
        TryTitleNewGameConfirmKeyboard();

        if (TitleEscapePressed())
        {
            if (_newGameConfirmModal != null && _newGameConfirmModal.activeSelf)
            {
                _newGameConfirmModal.SetActive(false);
            }
            else if (AdventurePauseMenu.IsOpen)
            {
                AdventurePauseMenu.ForceCloseTitleSettings();
            }
            else if (!AdventurePauseMenu.ShouldSuppressTitleSettingsOpen)
            {
                OnSettingsClicked();
            }
        }
    }

    void LateUpdate()
    {
        if (IsGameStarted && !IsModalBoardOpen()
            && (_newGameConfirmModal == null || !_newGameConfirmModal.activeSelf))
            return;

        AdventurePauseMenu.EnsureEventSystemForUi();
        TryTitleNewGameConfirmDirectMouseClicks();
        TryTitleDirectMouseClicks();
    }

    static bool TitleEscapePressed()
    {
        if (AdventureInputReader.EscapeDown) return true;
        try { return Input.GetKeyDown(KeyCode.Escape); } catch { return false; }
    }

    static bool TitleDigitPressed(int digit) => AdventureInputReader.TitleMenuDigitDown(digit);

    void TryTitleNewGameConfirmDirectMouseClicks()
    {
        if (AdventurePauseMenu.IsOpen) return;
        if (_newGameConfirmModal == null || !_newGameConfirmModal.activeSelf) return;
        if (!AdventureInputReader.MouseLeftDown) return;

        Vector2 mousePos = ReadTitleMouseScreenPos();

        if (TryClickButton(_confirmNewGameYesBtn, mousePos)) { OnConfirmNewGameYes(); return; }
        if (TryClickButton(_confirmNewGameNoBtn, mousePos)) OnConfirmNewGameNo();
    }

    void TryTitleNewGameConfirmKeyboard()
    {
        if (_newGameConfirmModal == null || !_newGameConfirmModal.activeSelf) return;
        if (AdventurePauseMenu.IsOpen) return;

        if (AdventureInputReader.SpaceOrJDown || AdventureInputReader.EnterDown)
            OnConfirmNewGameYes();
    }

    void TryTitleDirectMouseClicks()
    {
        if (AdventurePauseMenu.IsOpen) return;
        if (_newGameConfirmModal != null && _newGameConfirmModal.activeSelf) return;
        if (!IsModalBoardOpen()) return;
        if (!AdventureInputReader.MouseLeftDown) return;

        Vector2 mousePos = ReadTitleMouseScreenPos();

        if (TryClickButton(_continueBtn, mousePos)) { OnContinueClicked(); return; }
        if (TryClickButton(_newGameBtn, mousePos)) { OnNewGameClicked(); return; }
        if (TryClickButton(_settingsBtn, mousePos)) { OnSettingsClicked(); return; }
        if (TryClickButton(_quitBtn, mousePos)) { OnQuitClicked(); }
    }

    static bool TryClickButton(Button btn, Vector2 screenPos)
    {
        if (btn == null || !btn.gameObject.activeInHierarchy) return false;
        var rt = btn.GetComponent<RectTransform>();
        return rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null);
    }

    static Vector2 ReadTitleMouseScreenPos()
    {
        var mouse = Mouse.current;
        return mouse != null ? mouse.position.ReadValue() : (Vector2)Input.mousePosition;
    }

    void TryTitleKeyboardShortcuts()
    {
        if (AdventurePauseMenu.IsOpen) return;
        if (_newGameConfirmModal != null && _newGameConfirmModal.activeSelf) return;

        if (TitleDigitPressed(1)) { OnContinueClicked(); return; }
        if (TitleDigitPressed(2)) { OnNewGameClicked(); return; }
        if (TitleDigitPressed(3)) { OnSettingsClicked(); return; }
        if (TitleDigitPressed(4)) OnQuitClicked();
    }

    public void OnContinueClicked()
    {
        if (IsGameStarted || _isClosing) return;
        if (_continueBtn != null && !_continueBtn.interactable) return;

        // セーブデータをロードしてゲーム開始
        if (AdventureSaveManager.Instance != null)
        {
            AdventureSaveManager.Instance.TryLoadGame();
        }

        StartGameTransition(isNewGame: false);
    }

    public void OnNewGameClicked()
    {
        if (IsGameStarted || _isClosing) return;

        // セーブデータが既に存在する場合は確認モーダルを挟む
        if (AdventureSaveManager.HasSaveData)
        {
            if (_newGameConfirmModal != null)
            {
                _newGameConfirmModal.SetActive(true);
            }
            return;
        }

        // セーブデータがない場合は直接新規開始
        StartNewGameDirectly();
    }

    void OnConfirmNewGameYes()
    {
        if (IsGameStarted || _isClosing) return;
        if (_newGameConfirmModal != null)
            _newGameConfirmModal.SetActive(false);

        StartNewGameDirectly();
    }

    void OnConfirmNewGameNo()
    {
        if (_newGameConfirmModal != null)
            _newGameConfirmModal.SetActive(false);
    }

    void StartNewGameDirectly()
    {
        if (IsGameStarted || _isClosing) return;
        AdventureSaveManager.Ensure();
        AdventureSaveManager.Instance?.ResetToNewGame(returnToOpeningTitle: false);
        StartGameTransition(isNewGame: true);
    }

    public void OnSettingsClicked()
    {
        if (_isClosing) return;
        if (!IsModalBoardOpen()) return;
        if (_newGameConfirmModal != null && _newGameConfirmModal.activeSelf) return;

        AdventurePauseMenu.Ensure();
        AdventurePauseMenu.EnsureEventSystemForUi();
        SetTitleOverlayForSettings(true);
        AdventurePauseMenu.Instance?.ForceOpenTitleSettings();
    }

    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void StartGameTransition(bool isNewGame)
    {
        _isClosing = true;
        _inputGuardUntil = Time.unscaledTime + 0.85f;
        StartCoroutine(StartGameRoutine(isNewGame));
    }

    IEnumerator StartGameRoutine(bool isNewGame)
    {
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.unscaledDeltaTime;
            if (_modalCg != null)
                _modalCg.alpha = Mathf.Lerp(1f, 0f, t / 0.35f);
            yield return null;
        }

        if (_overlayGo != null)
            _overlayGo.SetActive(false);
        if (_modalBoard != null)
            _modalBoard.SetActive(false);
        if (_modalCg != null)
            _modalCg.alpha = 0f;
        IsGameStarted = true;
        _isClosing = false;
        _inputGuardUntil = Mathf.Max(_inputGuardUntil, Time.unscaledTime + 0.40f);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // カメラ視線を自然な標準見下ろしへリセット
        var camFollow = AdventureCameraFollow.InstanceOrFind();
        if (camFollow != null)
        {
            camFollow.SnapBehindTarget();
        }

        RefreshGuideText();
        if (_guideText != null)
        {
            _guideText.gameObject.SetActive(true);
        }

        if (isNewGame)
        {
            AdventurePrologueDrama.Ensure();
            var prologue = AdventurePrologueDrama.Instance;
            if (prologue != null)
            {
                prologue.BeginAfterOpening();
                AdventureStoryFlow.InvalidateCache();
                if (!prologue.IsPrologueActive)
                {
                    var drone = AdventureRustDrone.Instance ?? Object.FindAnyObjectByType<AdventureRustDrone>();
                    drone?.PlayStartupGreeting();
                }
            }
        }
        else
        {
            var drone = AdventureRustDrone.Instance ?? Object.FindAnyObjectByType<AdventureRustDrone>();
            drone?.PlayStartupGreeting();
        }
    }

    private bool _isCurrentlyGlidingGuide = false;

    /// <summary>滑空状態に応じて操作ガイドの文言を動的に切り替える</summary>
    public void SetGlideGuideActive(bool isGliding)
    {
        _isCurrentlyGlidingGuide = isGliding;
        RefreshGuideText();
    }

    private void RefreshGuideText()
    {
        if (_guideText == null) return;
        _guideText.text = _isCurrentlyGlidingGuide
            ? AdventureLocalizationManager.Get("guide_hud_gliding")
            : AdventureLocalizationManager.Get("guide_hud_gameplay");
    }

    void BuildHud()
    {
        if (_canvasGo != null)
        {
            Destroy(_canvasGo);
            _canvasGo = null;
            _overlayGo = null;
            _modalBoard = null;
            _modalCg = null;
            _guideText = null;
            _storyHeaderText = null;
            _storyBodyText = null;
        }

        // 孤児化した旧HUDがあれば掃除
        foreach (var orphanCanvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (orphanCanvas != null && orphanCanvas.gameObject.name == "RustFloatHUD")
                Destroy(orphanCanvas.gameObject);
        }

        AdventurePauseMenu.Ensure();
        AdventurePauseMenu.EnsureEventSystemForUi();

        _canvasGo = new GameObject("RustFloatHUD");
        var canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = TitleCanvasSortOrder;

        var scaler = _canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        _canvasGo.AddComponent<GraphicRaycaster>();

        Font font = ResolveFont();

        // 1. 画面最上部中央の水平リボンコンパスHUD
        AdventureCompassHUD.Create(_canvasGo.transform, font);

        // 2. 上部中央の操作ガイド
        _guideText = MakeText(_canvasGo.transform, "Guide", Vector2.zero, new Vector2(0.5f, 1f), new Vector2(980f, 32f), 13, TextAnchor.UpperCenter, font);
        var guideRt = _guideText.rectTransform;
        guideRt.anchorMin = new Vector2(0.5f, 1f);
        guideRt.anchorMax = new Vector2(0.5f, 1f);
        guideRt.pivot = new Vector2(0.5f, 1f);
        guideRt.anchoredPosition = new Vector2(0f, AdventureExplorationHudSettings.GetGuideRowY(false));
        guideRt.sizeDelta = new Vector2(980f, AdventureExplorationHudSettings.GuideRowHeight);
        _guideText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _guideText.verticalOverflow = VerticalWrapMode.Overflow;
        _guideText.color = new Color(0.9f, 0.95f, 1f, 0.75f);
        var guideOutline = _guideText.gameObject.AddComponent<Outline>();
        guideOutline.effectColor = new Color(0f, 0f, 0f, 0.7f);
        guideOutline.effectDistance = new Vector2(1f, -1f);
        _guideText.gameObject.SetActive(false);

        // 3. 全画面のアンビエントオーバーレイ（暗転させず背景の自然光を活かす）
        _overlayGo = new GameObject("OpeningOverlay");
        _overlayGo.transform.SetParent(_canvasGo.transform, false);
        var overlayRt = _overlayGo.AddComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.sizeDelta = Vector2.zero;
        var overlayImg = _overlayGo.AddComponent<Image>();
        overlayImg.color = new Color(0.02f, 0.05f, 0.09f, 0.38f);
        overlayImg.raycastTarget = false;

        // 4. メインタイトルボード（洗練されたグラスモーフィズムカード: 幅860, 高さ540）
        _modalBoard = new GameObject("StoryModalBoard");
        _modalBoard.transform.SetParent(_overlayGo.transform, false);
        _modalCg = _modalBoard.AddComponent<CanvasGroup>();
        _modalCg.interactable = true;
        _modalCg.blocksRaycasts = true;

        var boardRt = _modalBoard.AddComponent<RectTransform>();
        boardRt.anchorMin = new Vector2(0.5f, 0.5f);
        boardRt.anchorMax = new Vector2(0.5f, 0.5f);
        boardRt.pivot = new Vector2(0.5f, 0.5f);
        boardRt.anchoredPosition = new Vector2(0f, 0f);
        boardRt.sizeDelta = new Vector2(860f, 540f);

        // ボード背景
        var boardImg = _modalBoard.AddComponent<Image>();
        boardImg.color = new Color(0.025f, 0.065f, 0.12f, 0.88f);
        boardImg.raycastTarget = false;

        // 外枠線アウトライン（サイバーエメラルド光彩）
        var boardOutline = _modalBoard.AddComponent<Outline>();
        boardOutline.effectColor = new Color(0.35f, 0.85f, 1.0f, 0.65f);
        boardOutline.effectDistance = new Vector2(2f, -2f);

        // 上部アクセントバー
        var accentGo = new GameObject("AccentLine");
        accentGo.transform.SetParent(_modalBoard.transform, false);
        var accentRt = accentGo.AddComponent<RectTransform>();
        accentRt.anchorMin = new Vector2(0f, 1f);
        accentRt.anchorMax = new Vector2(1f, 1f);
        accentRt.pivot = new Vector2(0.5f, 1f);
        accentRt.anchoredPosition = Vector2.zero;
        accentRt.sizeDelta = new Vector2(0f, 3.5f);
        var accentImg = accentGo.AddComponent<Image>();
        accentImg.color = new Color(0.35f, 0.95f, 0.85f, 1f);
        accentImg.raycastTarget = false;

        // タイトルヘッダー
        var titleText = MakeText(_modalBoard.transform, "Title", new Vector2(0f, -20f), new Vector2(0.5f, 1f), new Vector2(800f, 40f), 28, TextAnchor.MiddleCenter, font);
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = new Color(1.0f, 0.90f, 0.45f, 1f);
        var titleOutline = titleText.gameObject.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        titleOutline.effectDistance = new Vector2(1.5f, -1.5f);
        titleText.text = TitleText;

        // サブタイトル
        _subTitleText = MakeText(_modalBoard.transform, "SubTitle", new Vector2(0f, -58f), new Vector2(0.5f, 1f), new Vector2(800f, 22f), 14, TextAnchor.MiddleCenter, font);
        _subTitleText.color = new Color(0.50f, 0.90f, 1.0f, 0.95f);
        _subTitleText.fontStyle = FontStyle.Bold;
        _subTitleText.text = SubTitleText;

        // 2カラムレイアウト（左: 世界観カード, 右: メニューボタン）
        var contentGo = new GameObject("ContentColumns");
        contentGo.transform.SetParent(_modalBoard.transform, false);
        var contentRt = contentGo.AddComponent<RectTransform>();
        contentRt.anchorMin = Vector2.zero;
        contentRt.anchorMax = Vector2.one;
        contentRt.offsetMin = new Vector2(25f, 35f);
        contentRt.offsetMax = new Vector2(-25f, -88f);

        CreateLeftStoryCard(contentGo.transform, font);
        CreateRightMenuCard(contentGo.transform, font);

        // フッター情報（バージョン ＆ 操作ガイド）
        var footLeft = MakeText(_modalBoard.transform, "FootLeft", new Vector2(30f, 12f), new Vector2(0f, 0f), new Vector2(400f, 20f), 12, TextAnchor.LowerLeft, font);
        footLeft.color = new Color(0.65f, 0.78f, 0.90f, 0.75f);
        footLeft.text = "【マウス / 1〜4】選択　【ESC / 3】設定";

        var footRight = MakeText(_modalBoard.transform, "FootRight", new Vector2(-30f, 12f), new Vector2(1f, 0f), new Vector2(400f, 20f), 12, TextAnchor.LowerRight, font);
        footRight.color = new Color(0.50f, 0.75f, 0.85f, 0.75f);
        footRight.text = "✦ Rust & Float Project  v1.0.0";

        // New Game 確認モーダル
        CreateNewGameConfirmModal(_canvasGo.transform, font);
    }

    void CreateLeftStoryCard(Transform parent, Font font)
    {
        var cardGo = new GameObject("LeftStoryCard");
        cardGo.transform.SetParent(parent, false);
        var cRt = cardGo.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0f, 0f);
        cRt.anchorMax = new Vector2(0.48f, 1f);
        cRt.offsetMin = Vector2.zero;
        cRt.offsetMax = Vector2.zero;

        var cBg = cardGo.AddComponent<Image>();
        cBg.color = new Color(0.015f, 0.04f, 0.08f, 0.92f);
        cBg.raycastTarget = false;

        var cOutline = cardGo.AddComponent<Outline>();
        cOutline.effectColor = new Color(0.20f, 0.50f, 0.75f, 0.35f);
        cOutline.effectDistance = new Vector2(1f, -1f);

        var textPanelGo = new GameObject("StoryTextPanel");
        textPanelGo.transform.SetParent(cardGo.transform, false);
        var textPanelRt = textPanelGo.AddComponent<RectTransform>();
        textPanelRt.anchorMin = Vector2.zero;
        textPanelRt.anchorMax = Vector2.one;
        textPanelRt.offsetMin = new Vector2(8f, 8f);
        textPanelRt.offsetMax = new Vector2(-8f, -8f);
        var textPanelBg = textPanelGo.AddComponent<Image>();
        textPanelBg.color = new Color(0.01f, 0.03f, 0.06f, 0.97f);
        textPanelBg.raycastTarget = false;

        _storyHeaderText = MakeText(textPanelGo.transform, "StoryHeader", new Vector2(10f, -8f), new Vector2(0f, 1f), new Vector2(360f, 24f), 14, TextAnchor.UpperLeft, font);
        _storyHeaderText.fontStyle = FontStyle.Bold;
        _storyHeaderText.color = new Color(0.40f, 0.88f, 1.0f, 1f);
        _storyHeaderText.text = "📜 時代背景と物語";

        _storyBodyText = MakeText(textPanelGo.transform, "StoryBody", new Vector2(10f, -40f), new Vector2(0f, 1f), new Vector2(350f, 320f), 13, TextAnchor.UpperLeft, font);
        _storyBodyText.fontStyle = FontStyle.Bold;
        _storyBodyText.lineSpacing = 1.35f;
        _storyBodyText.color = new Color(0.95f, 0.97f, 1.0f, 0.95f);
        var bodyOutline = _storyBodyText.gameObject.AddComponent<Outline>();
        bodyOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        bodyOutline.effectDistance = new Vector2(1f, -1f);
        _storyBodyText.text = StoryText;
    }

    /// <summary>左カードは常に物語テキスト。旧 PreStartPreview / 全画面キャプチャを除去。</summary>
    void EnsureTitleStoryTextVisible()
    {
        if (_storyHeaderText != null)
            _storyHeaderText.gameObject.SetActive(true);
        if (_storyBodyText != null)
            _storyBodyText.gameObject.SetActive(true);

        if (_canvasGo == null)
            return;

        var legacyBg = _canvasGo.transform.Find("PreStartCaptureBackground");
        if (legacyBg != null)
            Destroy(legacyBg.gameObject);

        if (_modalBoard == null)
            return;

        var left = _modalBoard.transform.Find("ContentColumns/LeftStoryCard");
        if (left == null)
            return;

        var legacyPreview = left.Find("PreStartPreview");
        if (legacyPreview != null)
            Destroy(legacyPreview.gameObject);
    }

    void CreateRightMenuCard(Transform parent, Font font)
    {
        var cardGo = new GameObject("RightMenuCard");
        cardGo.transform.SetParent(parent, false);
        var cRt = cardGo.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.52f, 0f);
        cRt.anchorMax = new Vector2(1f, 1f);
        cRt.offsetMin = Vector2.zero;
        cRt.offsetMax = Vector2.zero;

        var cBg = cardGo.AddComponent<Image>();
        cBg.color = new Color(0.015f, 0.04f, 0.08f, 0.70f);
        cBg.raycastTarget = false;

        var cOutline = cardGo.AddComponent<Outline>();
        cOutline.effectColor = new Color(0.20f, 0.50f, 0.75f, 0.35f);
        cOutline.effectDistance = new Vector2(1f, -1f);

        _menuHeaderText = MakeText(cardGo.transform, "MenuHeader", new Vector2(18f, -16f), new Vector2(0f, 1f), new Vector2(360f, 24f), 14, TextAnchor.UpperLeft, font);
        _menuHeaderText.fontStyle = FontStyle.Bold;
        _menuHeaderText.color = new Color(0.40f, 0.88f, 1.0f, 1f);
        _menuHeaderText.text = "✦ メインメニュー";

        float btnW = 340f;
        float startY = -52f;

        // 1. 【つづきから（Continue）】
        _continueBtn = CreateMenuButton(cardGo.transform, "ContinueBtn", new Vector2(0f, startY), new Vector2(btnW, 56f),
            new Color(0.12f, 0.58f, 0.72f, 0.96f), new Color(0.25f, 0.85f, 0.98f, 1f),
            "▶  つづきから", 16, OnContinueClicked, font, out _continueBtnImg, out _continueBtnLabel);

        // つづきから サブ情報テキスト
        _continueSubText = MakeText(_continueBtn.transform, "ContinueSub", new Vector2(0f, 8f), new Vector2(0.5f, 0f), new Vector2(btnW - 20f, 18f), 11, TextAnchor.MiddleCenter, font);
        _continueSubText.color = new Color(0.45f, 0.95f, 0.85f, 0.95f);

        // ラベル位置を少し上へ
        if (_continueBtnLabel != null)
        {
            var lRt = _continueBtnLabel.rectTransform;
            lRt.anchoredPosition = new Vector2(0f, 8f);
        }

        // 2. 【はじめから（New Game）】
        _newGameBtn = CreateMenuButton(cardGo.transform, "NewGameBtn", new Vector2(0f, startY - 72f), new Vector2(btnW, 48f),
            new Color(0.15f, 0.60f, 0.52f, 0.96f), new Color(0.25f, 0.85f, 0.75f, 1f),
            "✦  はじめから", 16, OnNewGameClicked, font, out _, out _newGameBtnLabel);

        // 3. 【設定（Settings）】
        _settingsBtn = CreateMenuButton(cardGo.transform, "SettingsBtn", new Vector2(0f, startY - 138f), new Vector2(btnW, 46f),
            new Color(0.18f, 0.32f, 0.50f, 0.96f), new Color(0.30f, 0.52f, 0.75f, 1f),
            "⚙  環境設定", 15, OnSettingsClicked, font, out _, out _settingsBtnLabel);

        // 4. 【ゲーム終了（Quit）】
        _quitBtn = CreateMenuButton(cardGo.transform, "QuitBtn", new Vector2(0f, startY - 200f), new Vector2(btnW, 44f),
            new Color(0.38f, 0.16f, 0.18f, 0.95f), new Color(0.60f, 0.22f, 0.25f, 1f),
            "✖  ゲーム終了", 14, OnQuitClicked, font, out _, out _quitBtnLabel);
    }

    Button CreateMenuButton(Transform parent, string name, Vector2 pos, Vector2 size, Color normalCol, Color highlightCol,
        string label, int fontSize, UnityEngine.Events.UnityAction onClick, Font font, out Image outImg, out Text outLabel)
    {
        var btnGo = new GameObject(name);
        btnGo.transform.SetParent(parent, false);
        var bRt = btnGo.AddComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.5f, 1f);
        bRt.anchorMax = new Vector2(0.5f, 1f);
        bRt.pivot = new Vector2(0.5f, 1f);
        bRt.anchoredPosition = pos;
        bRt.sizeDelta = size;

        outImg = btnGo.AddComponent<Image>();
        outImg.color = normalCol;
        outImg.raycastTarget = true;

        var outline = btnGo.AddComponent<Outline>();
        outline.effectColor = new Color(highlightCol.r, highlightCol.g, highlightCol.b, 0.65f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var btn = btnGo.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = normalCol;
        colors.highlightedColor = highlightCol;
        colors.pressedColor = normalCol * 0.8f;
        colors.disabledColor = new Color(0.1f, 0.15f, 0.2f, 0.5f);
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        outLabel = MakeText(btnGo.transform, "Label", Vector2.zero, new Vector2(0.5f, 0.5f), size, fontSize, TextAnchor.MiddleCenter, font);
        outLabel.fontStyle = FontStyle.Bold;
        outLabel.color = Color.white;
        var lOutline = outLabel.gameObject.AddComponent<Outline>();
        lOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        lOutline.effectDistance = new Vector2(1f, -1f);
        outLabel.text = label;

        return btn;
    }

    void CreateNewGameConfirmModal(Transform parent, Font font)
    {
        _newGameConfirmModal = new GameObject("NewGameConfirmModal");
        _newGameConfirmModal.transform.SetParent(parent, false);
        var mRt = _newGameConfirmModal.AddComponent<RectTransform>();
        mRt.anchorMin = Vector2.zero;
        mRt.anchorMax = Vector2.one;
        mRt.offsetMin = Vector2.zero;
        mRt.offsetMax = Vector2.zero;

        var mBg = _newGameConfirmModal.AddComponent<Image>();
        mBg.color = new Color(0f, 0f, 0f, 0.85f);

        // 中央カード
        var cardGo = new GameObject("Card");
        cardGo.transform.SetParent(_newGameConfirmModal.transform, false);
        var cRt = cardGo.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.5f, 0.5f);
        cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.sizeDelta = new Vector2(520f, 260f);
        cRt.anchoredPosition = Vector2.zero;

        var cBg = cardGo.AddComponent<Image>();
        cBg.color = new Color(0.04f, 0.08f, 0.14f, 0.98f);
        var cOutline = cardGo.AddComponent<Outline>();
        cOutline.effectColor = new Color(0.95f, 0.65f, 0.25f, 0.85f);
        cOutline.effectDistance = new Vector2(2f, -2f);

        // タイトル
        _confirmModalTitle = MakeText(cardGo.transform, "Title", new Vector2(0f, -24f), new Vector2(0.5f, 1f), new Vector2(480f, 32f), 20, TextAnchor.MiddleCenter, font);
        _confirmModalTitle.fontStyle = FontStyle.Bold;
        _confirmModalTitle.color = new Color(1f, 0.75f, 0.35f, 1f);
        _confirmModalTitle.text = AdventureLocalizationManager.Get("confirm_overwrite_title");

        // 説明文
        _confirmModalDesc = MakeText(cardGo.transform, "Desc", new Vector2(0f, -68f), new Vector2(0.5f, 1f), new Vector2(440f, 80f), 14, TextAnchor.MiddleCenter, font);
        _confirmModalDesc.lineSpacing = 1.35f;
        _confirmModalDesc.color = new Color(0.95f, 0.95f, 1.0f, 0.95f);
        _confirmModalDesc.text = AdventureLocalizationManager.Get("confirm_overwrite_desc");

        // ボタン領域
        // 1. 「はい、新しくはじめる」
        _confirmNewGameYesBtn = CreateMenuButton(cardGo.transform, "YesBtn", new Vector2(-120f, 22f), new Vector2(210f, 44f),
            new Color(0.75f, 0.25f, 0.20f, 1f), new Color(0.95f, 0.35f, 0.30f, 1f),
            AdventureLocalizationManager.Get("confirm_overwrite_yes"), 15, OnConfirmNewGameYes, font, out _, out _confirmModalYesBtnLabel);
        var yesBtn = _confirmNewGameYesBtn;
        var yRt = yesBtn.GetComponent<RectTransform>();
        yRt.anchorMin = new Vector2(0.5f, 0f);
        yRt.anchorMax = new Vector2(0.5f, 0f);
        yRt.pivot = new Vector2(0.5f, 0f);
        yRt.anchoredPosition = new Vector2(-115f, 22f);

        // 2. 「キャンセル」
        _confirmNewGameNoBtn = CreateMenuButton(cardGo.transform, "CancelBtn", new Vector2(120f, 22f), new Vector2(180f, 44f),
            new Color(0.20f, 0.35f, 0.50f, 1f), new Color(0.30f, 0.50f, 0.70f, 1f),
            AdventureLocalizationManager.Get("confirm_cancel"), 15, OnConfirmNewGameNo, font, out _, out _confirmModalNoBtnLabel);
        var cancelBtn = _confirmNewGameNoBtn;
        var cBtnRt = cancelBtn.GetComponent<RectTransform>();
        cBtnRt.anchorMin = new Vector2(0.5f, 0f);
        cBtnRt.anchorMax = new Vector2(0.5f, 0f);
        cBtnRt.pivot = new Vector2(0.5f, 0f);
        cBtnRt.anchoredPosition = new Vector2(115f, 22f);

        _newGameConfirmModal.SetActive(false);
    }

    static Text MakeText(Transform parent, string name, Vector2 pos, Vector2 anchor, Vector2 size, int fontSize, TextAnchor align, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = align;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    static Font ResolveFont()
    {
        string[] candidates =
        {
            "Hiragino Sans",
            "PingFang SC",
            "Hiragino Sans GB",
            "Heiti SC",
            "Hiragino Kaku Gothic ProN",
            "Apple SD Gothic Neo",
            "YuGothic",
            "Noto Sans CJK JP",
            "Microsoft YaHei",
            "Arial Unicode MS"
        };
        try
        {
            Font multi = Font.CreateDynamicFontFromOSFont(candidates, 20);
            if (multi != null) return multi;
        }
        catch { }

        foreach (string name in candidates)
        {
            try
            {
                Font os = Font.CreateDynamicFontFromOSFont(name, 20);
                if (os != null) return os;
            }
            catch { }
        }

        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f != null) return f;
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
