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
public partial class AdventureRustFloatOpening : MonoBehaviour
{
    private static AdventureRustFloatOpening _instance;
    public static AdventureRustFloatOpening Instance => _instance != null ? _instance : (_instance = FindAnyObjectByType<AdventureRustFloatOpening>());

    public static bool IsGameStarted
    {
        get => _isGameStarted;
        set
        {
            if (_isGameStarted != value)
            {
                _isGameStarted = value;
                Debug.Log($"<color=#ff00ff><b>[IsGameStarted]</b> Changed to {value}</color>\n{System.Environment.StackTrace}");
            }
        }
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
        "2050年。AIで100%最適化された「完璧な社会」を脱出した。\n\n" +
        "荒波を越え、気がつくとこの波静かな白砂のビーチに打ち上げられていた。\n" +
        "頬を打つ冷たい潮風も、駆け抜けた足の痛みさえ、\n" +
        "ここではすべてが生きている証そのものだ。\n\n" +
        "……隣には、スクラップ寸前で連れ出した相棒ドローン、Rust。\n" +
        "島を巡って翼を直し、あの自由な蒼穹（そら）へ飛び立とう。";

    const string PreStartCaptureAssetPath = "Assets/RustAndFloat/Textures/CurrentSceneCapture.png";
    const string PreStartCaptureResourcesPath = "CurrentSceneCapture";

    // 画面遷移・誤爆防止パラメータ
    const float TransitionFadeDuration = 0.35f;
    const float TransitionInputGuardDuration = 0.85f;
    const float TransitionInputGuardPadding = 0.40f;
    const float OpenGuardMinDuration = 0.30f;

    GameObject _canvasGo;
    GameObject _overlayGo;
    GameObject _preStartImageRoot;
    RawImage _preStartImage;
    Texture2D _preStartTexture;
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

    void ApplyTitleLiveBackdrop(bool enabled)
    {
        if (enabled)
        {
            AdventureTitleSplashScene.SetActive(true);
            SetPreStartImageVisible(false);
        }
        else
        {
            AdventureTitleSplashScene.SetActive(false);
        }
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
        ApplyTitleLiveBackdrop(false);
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
        _inputGuardUntil = 0f;

        var tower = AdventureSanctuaryTowerManager.Instance;
        if (tower != null)
        {
            tower.AbortEndingForEmergencyReset();
        }

        if (_canvasGo == null)
            BuildHud();

        if (_overlayGo != null) _overlayGo.SetActive(true);
        ApplyTitleLiveBackdrop(true);
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
        ApplyTitleStoryTextFit();
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
            SetPreStartImageVisible(false);
            IsGameStarted = true;
            if (_guideText != null) _guideText.gameObject.SetActive(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return;
        }

        ApplyTitleLiveBackdrop(true);

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
        // エンディング中にタイトルが勝手に復活したときだけ即消す（ShowTitleScreen 意図表示は除外）

        if (!IsGameStarted && ShouldSkipOpeningBoard())
        {
            bool titleMenuVisible = _modalBoard != null && _modalBoard.activeSelf;
            if (!titleMenuVisible)
            {
                return;
            }
        }

        bool titleMenuUp = IsModalBoardOpen()
            || (_newGameConfirmModal != null && _newGameConfirmModal.activeSelf);
        if (!titleMenuUp && (IsGameStarted || _isClosing))
            return;

        // 起動直後（0.3秒間）は誤爆防止
        if (!titleMenuUp && Time.realtimeSinceStartup - _openTime < OpenGuardMinDuration)
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
        _inputGuardUntil = Time.unscaledTime + TransitionInputGuardDuration;
        StartCoroutine(StartGameRoutine(isNewGame));
    }

    IEnumerator StartGameRoutine(bool isNewGame)
    {
        float t = 0f;
        while (t < TransitionFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(1f, 0f, t / TransitionFadeDuration);
            if (_modalCg != null)
                _modalCg.alpha = alpha;
            if (_preStartImage != null)
                _preStartImage.color = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }

        if (_overlayGo != null)
            _overlayGo.SetActive(false);
        if (_modalBoard != null)
            _modalBoard.SetActive(false);
        if (_modalCg != null)
            _modalCg.alpha = 0f;
        ApplyTitleLiveBackdrop(false);
        if (_preStartImage != null)
            _preStartImage.color = Color.white;
        IsGameStarted = true;
        _isClosing = false;
        _inputGuardUntil = Mathf.Max(_inputGuardUntil, Time.unscaledTime + TransitionInputGuardPadding);
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

}
