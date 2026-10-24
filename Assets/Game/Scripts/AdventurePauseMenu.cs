using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// 『Rust & Float』洗練された統合ポーズ＆環境設定メニュー。
/// 【ESCキー】で開閉し、ゲームを一時停止。
/// 【オーディオ・操作】【ディスプレイ・画質】【操作ガイド】のタブ形式で
/// BGM/SE音量、マウス感度、フルスクリーン切替、画質プリセット、VSync、解像度、
/// そしてタイトルへ戻る／最初からやり直す機能を提供する。
/// </summary>
public class AdventurePauseMenu : MonoBehaviour
{
    public static AdventurePauseMenu Instance { get; private set; }

    public static bool IsOpen { get; private set; } = false;
    public bool IsPaused => IsOpen;

    Canvas _canvas;
    CanvasGroup _canvasGroup;
    GameObject _panelRoot;
    GameObject _confirmModalRoot;
    GameObject _returnTitleConfirmModal;
    GameObject _titleSettingsBackdrop;
    Font _font;
    AudioSource _audioSource;
    AudioClip _testBeepClip;

    // ヘッダー・フッター UI
    Text _headerTitleText;
    Text _headerSubTitleText;
    GameObject _restartBtnGo;
    GameObject _returnTitleBtnGo;
    Button _restartBtn;
    Button _returnTitleBtn;
    Button _resumeBtn;
    Text _restartBtnText;
    Text _returnTitleBtnText;
    Text _resumeBtnText;
    Text _langValueText;
    Button _prevLangBtn;
    Button _nextLangBtn;
    Button _langValueBtn;
    GraphicRaycaster _graphicRaycaster;
    Text _creditsLeftText;
    Text _creditsRightText;

    // タブ UI
    int _currentTab = 0;
    GameObject _tabAudioPanel;
    GameObject _tabDisplayPanel;
    GameObject _tabGuidePanel;
    GameObject _tabCreditsPanel;

    Button _tabAudioBtn;
    Button _tabDisplayBtn;
    Button _tabGuideBtn;
    Button _tabCreditsBtn;
    Image _tabAudioBtnImg;
    Image _tabDisplayBtnImg;
    Image _tabGuideBtnImg;
    Image _tabCreditsBtnImg;
    Text _tabAudioBtnLabel;
    Text _tabDisplayBtnLabel;
    Text _tabGuideBtnLabel;
    Text _tabCreditsBtnLabel;
    GameObject _tabBarGo;

    // ガイド＆モーダル ローカライズ参照
    Text _guideLeftText;
    Text _guideRightText;
    Text _restartModalTitleText;
    Text _restartModalDescText;
    Text _restartModalYesBtnText;
    Text _restartModalNoBtnText;
    Text _returnTitleModalTitleText;
    Text _returnTitleModalDescText;
    Text _returnTitleModalYesBtnText;
    Text _returnTitleModalNoBtnText;

    // オーディオ＆操作 スライダー
    Slider _bgmSlider;
    Text _bgmValueText;
    Slider _seSlider;
    Text _seValueText;
    Slider _sensSlider;
    Text _sensValueText;

    // ディスプレイ＆画質 ボタン
    Button _fsBtn;
    Image _fsBtnImg;
    Button _winBtn;
    Image _winBtnImg;

    Button _qUltraBtn;
    Image _qUltraBtnImg;
    Button _qHighBtn;
    Image _qHighBtnImg;
    Button _qFastBtn;
    Image _qFastBtnImg;

    Button _vsyncOnBtn;
    Image _vsyncOnBtnImg;
    Button _vsyncOffBtn;
    Image _vsyncOffBtnImg;

    Button _res1080Btn;
    Image _res1080BtnImg;
    Button _res900Btn;
    Image _res900BtnImg;
    Button _res720Btn;
    Image _res720BtnImg;
    Text _displayStatusText;

    // オーディオ＆操作 設定ラベル
    Text _langLabelText;
    Text _audioHintText;
    Text _bgmLabelText;
    Text _seLabelText;
    Text _sensLabelText;

    // ディスプレイ 行ラベル＆ボタンラベル
    Text _screenModeRowLabel;
    Text _qualityRowLabel;
    Text _vsyncRowLabel;
    Text _resolutionRowLabel;
    Text _hudExplorationRowLabel;
    Button _hudCompassBtn;
    Image _hudCompassBtnImg;
    Text _hudCompassBtnText;
    Button _hudPartsBtn;
    Image _hudPartsBtnImg;
    Text _hudPartsBtnText;
    Button _hudOilBtn;
    Image _hudOilBtnImg;
    Text _hudOilBtnText;
    Text _hudGuideHintText;
    Text _fullscreenBtnText;
    Text _windowedBtnText;
    Text _qUltraBtnText;
    Text _qHighBtnText;
    Text _qFastBtnText;
    Text _vsyncOnBtnText;
    Text _vsyncOffBtnText;

    float _prevTimeScale = 1f;
    float _lastToggleTime = -1f;
    bool _openedFromTitle = false;
    static float _suppressTitleSettingsOpenUntil = -1f;

    /// <summary>タイトルで設定を閉じた直後、同じ ESC で開き直すのを防ぐ</summary>
    public static bool ShouldSuppressTitleSettingsOpen =>
        Time.unscaledTime < _suppressTitleSettingsOpenUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        Ensure();
    }

    public static void Ensure()
    {
        if (Instance == null)
            Instance = Object.FindAnyObjectByType<AdventurePauseMenu>();

        if (Instance == null)
        {
            var go = new GameObject("AdventurePauseMenu");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<AdventurePauseMenu>();
        }

        Instance.EnsureUiBuilt();
    }

    void EnsureUiBuilt()
    {
        if (_canvas == null)
            CreateUI();
    }

    public static void EnsureEventSystemForUi()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null)
            es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();

        if (es == null)
        {
            var go = new GameObject("EventSystem");
            Object.DontDestroyOnLoad(go);
            es = go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        }

        if (!es.gameObject.activeInHierarchy)
            es.gameObject.SetActive(true);
        es.enabled = true;

        var uiModule = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        if (uiModule == null)
            uiModule = es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        var legacy = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        if (legacy != null)
            Object.Destroy(legacy);

        try
        {
            if (uiModule.actionsAsset == null)
                uiModule.AssignDefaultActions();
            if (uiModule.actionsAsset != null)
                uiModule.actionsAsset.Enable();
        }
        catch { }

        uiModule.enabled = true;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
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
        CreateUI();
    }

    void OnEnable()
    {
        AdventureLocalizationManager.OnLanguageChanged += ApplyLocalization;
    }

    void OnDisable()
    {
        AdventureLocalizationManager.OnLanguageChanged -= ApplyLocalization;
    }

    void Start()
    {
        _font = ResolveFont();
        SetupAudio();
        LoadCurrentSettings();
        ApplyLocalization();
        SetMenuVisible(false, instant: true);
    }

    void SetupAudio()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;
        _audioSource.ignoreListenerPause = true;

        // 心地よいテスト用クリスタルチャイム音
        _testBeepClip = CreateTestBeepClip();
    }

    AudioClip CreateTestBeepClip()
    {
        int sampleRate = 44100;
        float duration = 0.22f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 14f);
            float f = (t < 0.08f) ? 1318.5f : 1975.5f;
            float s = Mathf.Sin(2f * Mathf.PI * f * t) * 0.45f
                    + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.15f;
            samples[i] = s * env;
        }

        var clip = AudioClip.Create("PauseTestBeep", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    void Update()
    {
        var gp = Gamepad.current;
        bool padBack = gp != null && gp.buttonEast.wasPressedThisFrame;
        bool escPressed = AdventureInputReader.EscapeDown || padBack;
        bool onTitle = !AdventureRustFloatOpening.IsGameStarted;

        // タイトル中: ESC で「開く」のは Opening 側。ここでは設定表示中の「閉じる」だけ担当（同一フレーム開閉競合を防ぐ）
        if (onTitle)
        {
            if (IsOpen)
            {
                TrySettingsDirectMouseClicks();
                TrySettingsTabKeyboardShortcuts();
            }

            if (escPressed && IsOpen)
            {
                if (_confirmModalRoot != null && _confirmModalRoot.activeSelf)
                {
                    _confirmModalRoot.SetActive(false);
                    _lastToggleTime = Time.unscaledTime;
                    return;
                }

                if (_returnTitleConfirmModal != null && _returnTitleConfirmModal.activeSelf)
                {
                    _returnTitleConfirmModal.SetActive(false);
                    _lastToggleTime = Time.unscaledTime;
                    return;
                }

                TogglePause();
            }

            return;
        }

        // 設定／ポーズを閉じる ESC はデバウンスしない（開いた直後に閉じられない不具合対策）
        if (escPressed && IsOpen)
        {
            if (_confirmModalRoot != null && _confirmModalRoot.activeSelf)
            {
                _confirmModalRoot.SetActive(false);
                _lastToggleTime = Time.unscaledTime;
                return;
            }

            if (_returnTitleConfirmModal != null && _returnTitleConfirmModal.activeSelf)
            {
                _returnTitleConfirmModal.SetActive(false);
                _lastToggleTime = Time.unscaledTime;
                return;
            }

            TogglePause();
            return;
        }

        if (Time.unscaledTime - _lastToggleTime < 0.2f) return;

        if (escPressed)
        {
            if (AdventureRustWorkshopUI.IsOpen) return;
            if (AdventureBeachDriftBox.IsModalOpen) return;
            if (AdventureBeachNarrativeManager.Instance != null && AdventureBeachNarrativeManager.Instance.IsShowingModal) return;
            if (AdventureRustFloatOpening.Instance != null && AdventureRustFloatOpening.Instance.IsModalBoardOpen()) return;
            if (AdventurePrologueDrama.Instance != null && AdventurePrologueDrama.Instance.IsAwakening) return;

            TogglePause();
        }

        if (IsOpen)
        {
            TrySettingsDirectMouseClicks();
            TrySettingsTabKeyboardShortcuts();
        }
    }

    bool IsSettingsModalBlockingInput()
    {
        if (_confirmModalRoot != null && _confirmModalRoot.activeSelf) return true;
        if (_returnTitleConfirmModal != null && _returnTitleConfirmModal.activeSelf) return true;
        return false;
    }

    void TrySettingsTabKeyboardShortcuts()
    {
        if (!IsOpen || IsSettingsModalBlockingInput()) return;

        if (AdventureInputReader.TitleMenuDigitDown(1)) SwitchTab(0);
        else if (AdventureInputReader.TitleMenuDigitDown(2)) SwitchTab(1);
        else if (AdventureInputReader.TitleMenuDigitDown(3)) SwitchTab(2);
        else if (AdventureInputReader.TitleMenuDigitDown(4)) SwitchTab(3);

        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.leftShoulder.wasPressedThisFrame)
            {
                int nextTab = (_currentTab + 3) % 4;
                SwitchTab(nextTab);
            }
            else if (gp.rightShoulder.wasPressedThisFrame)
            {
                int nextTab = (_currentTab + 1) % 4;
                SwitchTab(nextTab);
            }
        }

        // オーディオ＆言語タブ（タブ0）表示中の左右矢印・A/Dキー・ゲームパッド十字キーでの言語切り替え
        if (_currentTab == 0)
        {
            bool prevLang = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
            bool nextLang = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
                    prevLang = true;
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
                    nextLang = true;
            }

            if (gp != null)
            {
                if (gp.dpad.left.wasPressedThisFrame)
                    prevLang = true;
                if (gp.dpad.right.wasPressedThisFrame)
                    nextLang = true;
            }

            if (prevLang && !nextLang)
                OnPrevLanguageClicked();
            else if (nextLang && !prevLang)
                OnNextLanguageClicked();
        }
    }

    void TrySettingsDirectMouseClicks()
    {
        if (!IsOpen || IsSettingsModalBlockingInput()) return;
        if (!AdventureInputReader.MouseLeftDown) return;

        var mouse = Mouse.current;
        Vector2 mousePos = mouse != null ? mouse.position.ReadValue() : (Vector2)Input.mousePosition;

        // 1. タブ切り替え
        if (IsHit(_tabDisplayBtn, mousePos)) { SwitchTab(1); return; }
        if (IsHit(_tabGuideBtn, mousePos)) { SwitchTab(2); return; }
        if (IsHit(_tabCreditsBtn, mousePos)) { SwitchTab(3); return; }
        if (IsHit(_tabAudioBtn, mousePos)) { SwitchTab(0); return; }

        // 2. フッターボタン
        if (IsHit(_resumeBtn, mousePos)) { SetMenuVisible(false); return; }
        if (!_openedFromTitle)
        {
            if (IsHit(_restartBtn, mousePos)) { OnRestartClicked(); return; }
            if (IsHit(_returnTitleBtn, mousePos)) { OnReturnToTitleClicked(); return; }
        }

        // 3. 各タブ内容
        if (_currentTab == 1)
        {
            if (IsHit(_fsBtn, mousePos)) { SetFullscreen(true); return; }
            if (IsHit(_winBtn, mousePos)) { SetFullscreen(false); return; }
            if (IsHit(_qFastBtn, mousePos)) { SetQuality(0); return; }
            if (IsHit(_qHighBtn, mousePos)) { SetQuality(1); return; }
            if (IsHit(_qUltraBtn, mousePos)) { SetQuality(2); return; }
            if (IsHit(_vsyncOnBtn, mousePos)) { SetVSync(true); return; }
            if (IsHit(_vsyncOffBtn, mousePos)) { SetVSync(false); return; }
            if (IsHit(_res1080Btn, mousePos)) { SetResolution(1920, 1080); return; }
            if (IsHit(_res900Btn, mousePos)) { SetResolution(1600, 900); return; }
            if (IsHit(_res720Btn, mousePos)) { SetResolution(1280, 720); return; }
        }
        else if (_currentTab == 0)
        {
            if (IsHit(_prevLangBtn, mousePos)) { OnPrevLanguageClicked(); return; }
            if (IsHit(_nextLangBtn, mousePos)) { OnNextLanguageClicked(); return; }
            if (IsHit(_langValueBtn, mousePos)) { OnNextLanguageClicked(); return; }
        }
        else if (_currentTab == 2)
        {
            if (IsHit(_hudCompassBtn, mousePos)) { ToggleHudCompass(); return; }
            if (IsHit(_hudPartsBtn, mousePos)) { ToggleHudPartsBoard(); return; }
            if (IsHit(_hudOilBtn, mousePos)) { ToggleHudOilBoard(); return; }
        }
    }

    bool IsHit(Button btn, Vector2 mousePos)
    {
        if (btn == null || !btn.gameObject.activeInHierarchy) return false;

        // A. GraphicRaycaster による高精度レイキャスト（CanvasScaler や Retina 倍率を Unity が自動補正）
        if (_graphicRaycaster != null)
        {
            var ped = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {
                position = mousePos
            };
            var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            _graphicRaycaster.Raycast(ped, results);
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].gameObject == btn.gameObject || results[i].gameObject.transform.IsChildOf(btn.transform))
                    return true;
            }
        }

        // B. RectTransformUtility（フォールバック ＆ Retina 倍率自動補正）
        var rt = btn.GetComponent<RectTransform>();
        if (rt != null)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(rt, mousePos, null))
                return true;
            if (RectTransformUtility.RectangleContainsScreenPoint(rt, mousePos * 2f, null))
                return true;
            if (RectTransformUtility.RectangleContainsScreenPoint(rt, mousePos * 0.5f, null))
                return true;
        }

        return false;
    }

    float _lastLangClickTime = -1f;
    int _lastLangClickFrame = -1;

    void EnsureLanguageButtonListeners()
    {
        if (_prevLangBtn != null)
        {
            _prevLangBtn.onClick.RemoveListener(OnPrevLanguageClicked);
            _prevLangBtn.onClick.AddListener(OnPrevLanguageClicked);
        }
        if (_nextLangBtn != null)
        {
            _nextLangBtn.onClick.RemoveListener(OnNextLanguageClicked);
            _nextLangBtn.onClick.AddListener(OnNextLanguageClicked);
        }
        if (_langValueBtn != null)
        {
            _langValueBtn.onClick.RemoveListener(OnNextLanguageClicked);
            _langValueBtn.onClick.AddListener(OnNextLanguageClicked);
        }
    }

    void OnPrevLanguageClicked()
    {
        if (Time.frameCount == _lastLangClickFrame) return;
        if (Time.unscaledTime - _lastLangClickTime < 0.20f) return;
        _lastLangClickFrame = Time.frameCount;
        _lastLangClickTime = Time.unscaledTime;

        PlayClickSound();
        AdventureLocalizationManager.CycleLanguage(-1);
        ApplyLocalization();
    }

    void OnNextLanguageClicked()
    {
        if (Time.frameCount == _lastLangClickFrame) return;
        if (Time.unscaledTime - _lastLangClickTime < 0.20f) return;
        _lastLangClickFrame = Time.frameCount;
        _lastLangClickTime = Time.unscaledTime;

        PlayClickSound();
        AdventureLocalizationManager.CycleLanguage(1);
        ApplyLocalization();
    }

    void PlayClickSound()
    {
        if (_audioSource != null && _testBeepClip != null)
            _audioSource.PlayOneShot(_testBeepClip, 0.45f);
    }

    static bool TryClickUiButton(Button btn, Vector2 screenPos)
    {
        if (btn == null || !btn.isActiveAndEnabled) return false;
        var rt = btn.GetComponent<RectTransform>();
        return rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null);
    }

    public static void CloseIfOpen()
    {
        if (Instance != null && IsOpen)
            Instance.SetMenuVisible(false, instant: true);
    }

    /// <summary>タイトル／設定 UI が入力不能になったときの救済</summary>
    public static void ForceCloseTitleSettings()
    {
        if (Instance == null || !IsOpen) return;
        Instance.SetMenuVisible(false, instant: true);
        Time.timeScale = 1f;
        AdventureRustFloatOpening.Instance?.RestoreTitleUiInput();
    }

    /// <summary>IsOpen=false なのに Canvas だけ残ってタイトルをブロックする状態を解消</summary>
    public static void EnsureClosedIfNotOpen()
    {
        if (Instance == null || IsOpen) return;
        if (Instance._canvas != null && Instance._canvas.enabled)
            Instance.SetMenuVisible(false, instant: true);
        if (Instance._canvasGroup != null && Instance._canvasGroup.blocksRaycasts)
        {
            Instance._canvasGroup.blocksRaycasts = false;
            Instance._canvasGroup.interactable = false;
            Instance._canvasGroup.alpha = 0f;
        }
    }

    public void OpenSettingsFromTitle()
    {
        ForceOpenTitleSettings();
    }

    /// <summary>タイトルから環境設定を確実に前面表示</summary>
    public void ForceOpenTitleSettings()
    {
        EnsureUiBuilt();
        EnsureEventSystemForUi();
        _openedFromTitle = true;
        SetMenuVisible(true);
    }

    public void TogglePause()
    {
        SetMenuVisible(!IsOpen);
    }

    public void SetMenuVisible(bool visible, bool instant = false)
    {
        IsOpen = visible;
        _lastToggleTime = Time.unscaledTime;

        if (visible)
        {
            EnsureEventSystemForUi();

            _prevTimeScale = Time.timeScale > 0.05f ? Time.timeScale : 1f;
            bool titleSettings = _openedFromTitle || !AdventureRustFloatOpening.IsGameStarted;
            if (titleSettings)
            {
                Time.timeScale = 1f;
                AdventureRustFloatOpening.Instance?.SetTitleOverlayForSettings(true);
            }
            else
                Time.timeScale = 0f;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            EnsureLanguageButtonListeners();
            if (_prevLangBtn != null) _prevLangBtn.interactable = true;
            if (_nextLangBtn != null) _nextLangBtn.interactable = true;
            if (_langValueBtn != null) _langValueBtn.interactable = true;
            LoadCurrentSettings();
            SwitchTab(_currentTab);
            if (_langValueText != null)
                _langValueText.text = AdventureLocalizationManager.LanguageNames[(int)AdventureLocalizationManager.CurrentLanguage];

            if (_confirmModalRoot != null) _confirmModalRoot.SetActive(false);
            if (_returnTitleConfirmModal != null) _returnTitleConfirmModal.SetActive(false);

            if (_openedFromTitle)
            {
                if (_restartBtnGo != null) _restartBtnGo.SetActive(false);
                if (_returnTitleBtnGo != null) _returnTitleBtnGo.SetActive(false);
            }
            else
            {
                if (_restartBtnGo != null) _restartBtnGo.SetActive(true);
                if (_returnTitleBtnGo != null) _returnTitleBtnGo.SetActive(true);
            }

            ApplyLocalization();

            if (_canvas != null)
            {
                _canvas.enabled = true;
                _canvas.sortingOrder = 5500; // いかなるHUDや暗幕よりも最前面に配置してクリック判定を完全独占
            }

            if (_titleSettingsBackdrop != null)
                _titleSettingsBackdrop.SetActive(_openedFromTitle);

            if (_panelRoot != null)
            {
                _panelRoot.SetActive(true);
                _panelRoot.transform.SetAsLastSibling();
            }

            if (_tabBarGo != null)
                _tabBarGo.transform.SetAsLastSibling();
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }

            if (_canvas != null)
                _canvas.transform.SetAsLastSibling();
            transform.SetAsLastSibling();
        }
        else
        {
            bool wasFromTitle = _openedFromTitle;
            _openedFromTitle = false;

            if (wasFromTitle)
            {
                _suppressTitleSettingsOpenUntil = Time.unscaledTime + 0.35f;
                AdventureRustFloatOpening.Instance?.RestoreTitleUiInput();
            }

            Time.timeScale = wasFromTitle ? 1f : (_prevTimeScale > 0.05f ? _prevTimeScale : 1f);

            if (!wasFromTitle && AdventureRustFloatOpening.IsGameStarted)
                AdventureGameplayInputGate.TryRestoreExplorationLookIfReady();
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (_panelRoot != null) _panelRoot.SetActive(false);
            if (_confirmModalRoot != null) _confirmModalRoot.SetActive(false);
            if (_returnTitleConfirmModal != null) _returnTitleConfirmModal.SetActive(false);
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
            if (_titleSettingsBackdrop != null)
                _titleSettingsBackdrop.SetActive(false);

            if (_canvas != null) _canvas.enabled = false;
        }
    }

    void OnGUI()
    {
        // uGUI（EventSystem）に入力を任せる。IMGUI ボタンはタイトル設定で言語◀▶のクリックを奪っていた。
    }

    void SwitchTab(int tabIndex)
    {
        _currentTab = tabIndex;

        if (_tabAudioPanel != null) _tabAudioPanel.SetActive(tabIndex == 0);
        if (_tabDisplayPanel != null) _tabDisplayPanel.SetActive(tabIndex == 1);
        if (_tabGuidePanel != null) _tabGuidePanel.SetActive(tabIndex == 2);
        if (_tabCreditsPanel != null) _tabCreditsPanel.SetActive(tabIndex == 3);

        Color activeBg = new Color(0.12f, 0.55f, 0.75f, 0.95f);
        Color inactiveBg = new Color(0.04f, 0.08f, 0.14f, 0.70f);

        if (_tabAudioBtnImg != null) _tabAudioBtnImg.color = (tabIndex == 0) ? activeBg : inactiveBg;
        if (_tabDisplayBtnImg != null) _tabDisplayBtnImg.color = (tabIndex == 1) ? activeBg : inactiveBg;
        if (_tabGuideBtnImg != null) _tabGuideBtnImg.color = (tabIndex == 2) ? activeBg : inactiveBg;
        if (_tabCreditsBtnImg != null) _tabCreditsBtnImg.color = (tabIndex == 3) ? activeBg : inactiveBg;

        if (_tabAudioBtnLabel != null) _tabAudioBtnLabel.color = (tabIndex == 0) ? Color.white : new Color(0.7f, 0.85f, 0.95f, 0.75f);
        if (_tabDisplayBtnLabel != null) _tabDisplayBtnLabel.color = (tabIndex == 1) ? Color.white : new Color(0.7f, 0.85f, 0.95f, 0.75f);
        if (_tabGuideBtnLabel != null) _tabGuideBtnLabel.color = (tabIndex == 2) ? Color.white : new Color(0.7f, 0.85f, 0.95f, 0.75f);
        if (_tabCreditsBtnLabel != null) _tabCreditsBtnLabel.color = (tabIndex == 3) ? Color.white : new Color(0.7f, 0.85f, 0.95f, 0.75f);

        if (tabIndex == 1)
            UpdateDisplayHighlights();
        if (tabIndex == 2)
            UpdateHudToggleHighlights();
    }

    void LoadCurrentSettings()
    {
        // 1. 音量・感度
        float bgm = AdventureMusicDirector.MasterBgmVolumeScale;
        if (_bgmSlider != null) _bgmSlider.SetValueWithoutNotify(bgm);
        if (_bgmValueText != null) _bgmValueText.text = $"{Mathf.RoundToInt(bgm * 100f)}%";

        float se = PlayerPrefs.GetFloat("Adventure_SeVolume", 1.0f);
        if (_seSlider != null) _seSlider.SetValueWithoutNotify(se);
        if (_seValueText != null) _seValueText.text = $"{Mathf.RoundToInt(se * 100f)}%";

        float sens = AdventureCameraFollow.MasterSensitivity;
        if (_sensSlider != null) _sensSlider.SetValueWithoutNotify(sens);
        if (_sensValueText != null) _sensValueText.text = $"{sens:F2}";

        // 2. ディスプレイ・画質
        UpdateDisplayHighlights();
    }

    void UpdateDisplayHighlights()
    {
        Color activeCol = new Color(0.15f, 0.65f, 0.80f, 1f);
        Color inactiveCol = new Color(0.05f, 0.10f, 0.18f, 0.80f);

        bool isFs = AdventureDisplaySettings.IsFullscreen;
        if (_fsBtnImg != null) _fsBtnImg.color = isFs ? activeCol : inactiveCol;
        if (_winBtnImg != null) _winBtnImg.color = !isFs ? activeCol : inactiveCol;

        // 画質クオリティ
        int q = QualitySettings.GetQualityLevel();
        if (_qFastBtnImg != null) _qFastBtnImg.color = (q == 0) ? activeCol : inactiveCol;
        if (_qHighBtnImg != null) _qHighBtnImg.color = (q == 1) ? activeCol : inactiveCol;
        if (_qUltraBtnImg != null) _qUltraBtnImg.color = (q >= 2) ? activeCol : inactiveCol;

        // VSync
        bool vsync = QualitySettings.vSyncCount > 0;
        if (_vsyncOnBtnImg != null) _vsyncOnBtnImg.color = vsync ? activeCol : inactiveCol;
        if (_vsyncOffBtnImg != null) _vsyncOffBtnImg.color = !vsync ? activeCol : inactiveCol;

        // 解像度
        int w = Screen.width;
        int h = Screen.height;
        if (_res1080BtnImg != null) _res1080BtnImg.color = (w >= 1900) ? activeCol : inactiveCol;
        if (_res900BtnImg != null) _res900BtnImg.color = (w >= 1500 && w < 1900) ? activeCol : inactiveCol;
        if (_res720BtnImg != null) _res720BtnImg.color = (w < 1500) ? activeCol : inactiveCol;

        UpdateHudToggleHighlights();

        if (_displayStatusText != null)
        {
            string prefix = AdventureLocalizationManager.CurrentLanguage switch
            {
                AdventureLocalizationManager.GameLanguage.English => "Current: ",
                AdventureLocalizationManager.GameLanguage.SimplifiedChinese => "当前: ",
                AdventureLocalizationManager.GameLanguage.Spanish => "Actual: ",
                _ => "現在: "
            };
            _displayStatusText.text = prefix + AdventureDisplaySettings.DescribeCurrent();
        }
    }

    void OnBgmSliderChanged(float val)
    {
        AdventureMusicDirector.MasterBgmVolumeScale = val;
        if (_bgmValueText != null) _bgmValueText.text = $"{Mathf.RoundToInt(val * 100f)}%";
    }

    void OnSeSliderChanged(float val)
    {
        PlayerPrefs.SetFloat("Adventure_SeVolume", val);
        PlayerPrefs.Save();
        if (_seValueText != null) _seValueText.text = $"{Mathf.RoundToInt(val * 100f)}%";

        if (_audioSource != null && _testBeepClip != null)
        {
            _audioSource.volume = Mathf.Clamp01(val);
            _audioSource.PlayOneShot(_testBeepClip);
        }
    }

    void OnSensitivitySliderChanged(float val)
    {
        AdventureCameraFollow.MasterSensitivity = val;
        if (_sensValueText != null) _sensValueText.text = $"{val:F2}";
    }

    IEnumerator RefreshDisplayHighlightsAfterDisplayChange()
    {
        yield return new WaitForSecondsRealtime(0.06f);
        UpdateDisplayHighlights();
        yield return new WaitForSecondsRealtime(0.12f);
        UpdateDisplayHighlights();
    }

    void SetFullscreen(bool fs)
    {
        AdventureDisplaySettings.GetSavedResolution(out int w, out int h);
        if (!fs && w >= Screen.currentResolution.width - 80)
        {
            w = 1280;
            h = 720;
        }

        if (fs)
            AdventureDisplaySettings.ApplyFullscreen();
        else
            AdventureDisplaySettings.ApplyWindowed(w, h);

        AdventureDisplaySettings.SavePreferences(fs, w, h);
        UpdateDisplayHighlights();
        StartCoroutine(RefreshDisplayHighlightsAfterDisplayChange());
    }

    void SetQuality(int level)
    {
        QualitySettings.SetQualityLevel(level, true);
        PlayerPrefs.SetInt("RustFloat_Quality", level);
        PlayerPrefs.Save();
        UpdateDisplayHighlights();
    }

    void SetVSync(bool on)
    {
        QualitySettings.vSyncCount = on ? 1 : 0;
        PlayerPrefs.SetInt("RustFloat_VSync", on ? 1 : 0);
        PlayerPrefs.Save();
        UpdateDisplayHighlights();
    }

    void SetResolution(int w, int h)
    {
        AdventureDisplaySettings.ApplyResolution(w, h);
        AdventureDisplaySettings.SavePreferences(AdventureDisplaySettings.IsFullscreen, w, h);
        UpdateDisplayHighlights();
        StartCoroutine(RefreshDisplayHighlightsAfterDisplayChange());
    }

    void OnRestartClicked()
    {
        if (_confirmModalRoot != null)
            _confirmModalRoot.SetActive(true);
    }

    void OnConfirmRestartYes()
    {
        if (_confirmModalRoot != null)
            _confirmModalRoot.SetActive(false);

        SetMenuVisible(false);

        if (AdventureSaveManager.Instance != null)
        {
            AdventureSaveManager.Instance.ResetToNewGame();
        }
    }

    void OnConfirmRestartNo()
    {
        if (_confirmModalRoot != null)
            _confirmModalRoot.SetActive(false);
    }

    void OnReturnToTitleClicked()
    {
        if (_returnTitleConfirmModal != null)
            _returnTitleConfirmModal.SetActive(true);
    }

    void OnConfirmReturnTitleYes()
    {
        if (_returnTitleConfirmModal != null)
            _returnTitleConfirmModal.SetActive(false);

        if (AdventureSaveManager.Instance != null)
        {
            AdventureSaveManager.Instance.SaveGame("タイトル移動セーブ完了");
        }

        SetMenuVisible(false);

        var opening = AdventureRustFloatOpening.Instance;
        if (opening != null)
        {
            opening.ShowTitleScreen();
        }
    }

    void OnConfirmReturnTitleNo()
    {
        if (_returnTitleConfirmModal != null)
            _returnTitleConfirmModal.SetActive(false);
    }

    void CreateUI()
    {
        if (_canvas != null) return;

        var canvasGo = new GameObject("PauseMenu_Canvas");
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5500;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        _graphicRaycaster = canvasGo.AddComponent<GraphicRaycaster>();

        _font = ResolveFont();

        _titleSettingsBackdrop = new GameObject("TitleSettingsBackdrop");
        _titleSettingsBackdrop.transform.SetParent(canvasGo.transform, false);
        var bdRt = _titleSettingsBackdrop.AddComponent<RectTransform>();
        bdRt.anchorMin = Vector2.zero;
        bdRt.anchorMax = Vector2.one;
        bdRt.offsetMin = Vector2.zero;
        bdRt.offsetMax = Vector2.zero;
        var bdImg = _titleSettingsBackdrop.AddComponent<Image>();
        bdImg.color = new Color(0.01f, 0.03f, 0.06f, 0.72f);
        bdImg.raycastTarget = false; // 暗幕がクリック判定を吸い取らないよう透過
        _titleSettingsBackdrop.SetActive(false);

        // 1. パネル背景
        _panelRoot = new GameObject("PauseMenuPanel");
        _panelRoot.transform.SetParent(canvasGo.transform, false);
        _canvasGroup = _panelRoot.AddComponent<CanvasGroup>();

        var pRt = _panelRoot.AddComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0.5f, 0.5f);
        pRt.anchorMax = new Vector2(0.5f, 0.5f);
        pRt.pivot = new Vector2(0.5f, 0.5f);
        pRt.sizeDelta = new Vector2(900f, 560f);
        pRt.anchoredPosition = Vector2.zero;

        var pBg = _panelRoot.AddComponent<Image>();
        pBg.color = new Color(0.025f, 0.055f, 0.10f, 0.98f);

        var pOutline = _panelRoot.AddComponent<Outline>();
        pOutline.effectColor = new Color(0.25f, 0.75f, 0.95f, 0.55f);
        pOutline.effectDistance = new Vector2(2f, -2f);

        // 上部アクセントバー
        var barGo = new GameObject("TopAccentBar");
        barGo.transform.SetParent(_panelRoot.transform, false);
        var bRt = barGo.AddComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0f, 1f);
        bRt.anchorMax = new Vector2(1f, 1f);
        bRt.pivot = new Vector2(0.5f, 1f);
        bRt.sizeDelta = new Vector2(0f, 4f);
        bRt.anchoredPosition = Vector2.zero;
        var bImg = barGo.AddComponent<Image>();
        bImg.color = new Color(0.35f, 0.92f, 0.85f, 1f);

        // ヘッダータイトル
        var titleGo = new GameObject("TitleText");
        titleGo.transform.SetParent(_panelRoot.transform, false);
        var tRt = titleGo.AddComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -18f);
        tRt.sizeDelta = new Vector2(0f, 32f);
        var titleText = titleGo.AddComponent<Text>();
        titleText.font = _font;
        titleText.fontSize = 22;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(1f, 0.90f, 0.45f, 1f);
        titleText.text = "✦ PAUSE & SETTINGS ✦";
        titleText.raycastTarget = false;
        _headerTitleText = titleText;

        var subTitleGo = new GameObject("SubTitleText");
        subTitleGo.transform.SetParent(_panelRoot.transform, false);
        var stRt = subTitleGo.AddComponent<RectTransform>();
        stRt.anchorMin = new Vector2(0f, 1f);
        stRt.anchorMax = new Vector2(1f, 1f);
        stRt.pivot = new Vector2(0.5f, 1f);
        stRt.anchoredPosition = new Vector2(0f, -46f);
        stRt.sizeDelta = new Vector2(0f, 20f);
        var subTitleText = subTitleGo.AddComponent<Text>();
        subTitleText.font = _font;
        subTitleText.fontSize = 12;
        subTitleText.alignment = TextAnchor.MiddleCenter;
        subTitleText.color = new Color(0.65f, 0.85f, 1f, 0.85f);
        subTitleText.text = "ゲーム設定・画面表示・操作ガイド・進行管理";
        subTitleText.raycastTarget = false;
        _headerSubTitleText = subTitleText;

        // 探検マップクイックボタン
        var mapBtnGo = new GameObject("QuickMapButton");
        mapBtnGo.transform.SetParent(_panelRoot.transform, false);
        var mRt = mapBtnGo.AddComponent<RectTransform>();
        mRt.anchorMin = new Vector2(1.0f, 1.0f);
        mRt.anchorMax = new Vector2(1.0f, 1.0f);
        mRt.pivot = new Vector2(1.0f, 1.0f);
        mRt.anchoredPosition = new Vector2(-30f, -22f);
        mRt.sizeDelta = new Vector2(165f, 36f);
        var mImg = mapBtnGo.AddComponent<Image>();
        mImg.color = new Color(0.18f, 0.48f, 0.38f, 0.95f);
        var mBtn = mapBtnGo.AddComponent<Button>();
        mBtn.onClick.AddListener(() => AdventureIslandMapModal.Instance?.ToggleMap());
        var mTxt = MakeLabel(mapBtnGo.transform, "Text", Vector2.zero, Vector2.zero, "🗺 探検マップ [M]", 14, Color.white);
        mTxt.fontStyle = FontStyle.Bold;
        mTxt.alignment = TextAnchor.MiddleCenter;

        // 2. タブバー（4タブ）
        CreateTabBar(_panelRoot.transform);

        // 3. コンテンツ領域（タブごとのパネル）
        var contentGo = new GameObject("TabContentArea");
        contentGo.transform.SetParent(_panelRoot.transform, false);
        var cRt = contentGo.AddComponent<RectTransform>();
        cRt.anchorMin = Vector2.zero;
        cRt.anchorMax = Vector2.one;
        cRt.offsetMin = new Vector2(30f, 80f);
        cRt.offsetMax = new Vector2(-30f, -122f);

        CreateAudioTab(contentGo.transform);
        CreateDisplayTab(contentGo.transform);
        CreateGuideTab(contentGo.transform);
        CreateCreditsTab(contentGo.transform);

        // 4. フッターボタンバー
        CreateFooterButtons(_panelRoot.transform);

        // 5. 確認モーダル群
        CreateRestartConfirmModal(canvasGo.transform);
        CreateReturnTitleConfirmModal(canvasGo.transform);
    }

    void CreateTabBar(Transform parent)
    {
        var barGo = new GameObject("TabBar");
        barGo.transform.SetParent(parent, false);
        _tabBarGo = barGo;
        var bRt = barGo.AddComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.5f, 1f);
        bRt.anchorMax = new Vector2(0.5f, 1f);
        bRt.pivot = new Vector2(0.5f, 1f);
        bRt.anchoredPosition = new Vector2(0f, -72f);
        bRt.sizeDelta = new Vector2(840f, 36f);

        float tabW = 200f;
        _tabAudioBtn = CreateTabButton(barGo.transform, "TabAudio", new Vector2(-315f, 0f), new Vector2(tabW, 36f), "⚙  オーディオ・操作", () => SwitchTab(0), out _tabAudioBtnImg, out _tabAudioBtnLabel);
        _tabDisplayBtn = CreateTabButton(barGo.transform, "TabDisplay", new Vector2(-105f, 0f), new Vector2(tabW, 36f), "🖥  画面・画質", () => SwitchTab(1), out _tabDisplayBtnImg, out _tabDisplayBtnLabel);
        _tabGuideBtn = CreateTabButton(barGo.transform, "TabGuide", new Vector2(105f, 0f), new Vector2(tabW, 36f), "📖  操作ガイド", () => SwitchTab(2), out _tabGuideBtnImg, out _tabGuideBtnLabel);
        _tabCreditsBtn = CreateTabButton(barGo.transform, "TabCredits", new Vector2(315f, 0f), new Vector2(tabW, 36f), "📜  クレジット・表記", () => SwitchTab(3), out _tabCreditsBtnImg, out _tabCreditsBtnLabel);
    }

    Button CreateTabButton(Transform parent, string name, Vector2 pos, Vector2 size, string label, UnityEngine.Events.UnityAction onClick, out Image outImg, out Text outLabel)
    {
        var btnGo = new GameObject(name);
        btnGo.transform.SetParent(parent, false);
        var rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        outImg = btnGo.AddComponent<Image>();
        outImg.color = new Color(0.04f, 0.08f, 0.14f, 0.70f);
        outImg.raycastTarget = true;

        var outline = btnGo.AddComponent<Outline>();
        outline.effectColor = new Color(0.25f, 0.65f, 0.85f, 0.45f);
        outline.effectDistance = new Vector2(1f, -1f);

        var btn = btnGo.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(btnGo.transform, false);
        var tRt = textGo.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        outLabel = textGo.AddComponent<Text>();
        outLabel.font = _font;
        outLabel.fontSize = 14;
        outLabel.fontStyle = FontStyle.Bold;
        outLabel.alignment = TextAnchor.MiddleCenter;
        outLabel.color = Color.white;
        outLabel.raycastTarget = false;
        outLabel.text = label;

        return btn;
    }

    void CreateAudioTab(Transform parent)
    {
        _tabAudioPanel = new GameObject("TabAudioPanel");
        _tabAudioPanel.transform.SetParent(parent, false);
        var rt = _tabAudioPanel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // 言語セレクター
        CreateLanguageSelectorRow(_tabAudioPanel.transform, -10f);

        // BGM スライダー
        CreateSettingSlider(_tabAudioPanel.transform, AdventureLocalizationManager.Get("opt_bgm"), -78f, 0f, 1f, out _bgmSlider, out _bgmValueText, out _bgmLabelText, OnBgmSliderChanged);

        // SE スライダー
        CreateSettingSlider(_tabAudioPanel.transform, AdventureLocalizationManager.Get("opt_se"), -146f, 0f, 1f, out _seSlider, out _seValueText, out _seLabelText, OnSeSliderChanged);

        // マウス視点感度 スライダー
        CreateSettingSlider(_tabAudioPanel.transform, AdventureLocalizationManager.Get("opt_sensitivity"), -214f, 0.05f, 0.60f, out _sensSlider, out _sensValueText, out _sensLabelText, OnSensitivitySliderChanged);

        // ガイドヒント
        _audioHintText = MakeLabel(_tabAudioPanel.transform, "AudioHint", new Vector2(12f, -284f), new Vector2(800f, 30f),
            AdventureLocalizationManager.Get("opt_audio_hint"), 12, new Color(0.6f, 0.8f, 0.95f, 0.8f));
        _audioHintText.alignment = TextAnchor.MiddleLeft;
    }

    void CreateLanguageSelectorRow(Transform parent, float topOffset)
    {
        var rowGo = new GameObject("Language_Row");
        rowGo.transform.SetParent(parent, false);
        var rRt = rowGo.AddComponent<RectTransform>();
        rRt.anchorMin = new Vector2(0f, 1f);
        rRt.anchorMax = new Vector2(1f, 1f);
        rRt.pivot = new Vector2(0f, 1f);
        rRt.anchoredPosition = new Vector2(0f, topOffset);
        rRt.sizeDelta = new Vector2(0f, 58f);

        var rowBg = rowGo.AddComponent<Image>();
        rowBg.color = new Color(0.015f, 0.04f, 0.08f, 0.65f);
        rowBg.raycastTarget = false;

        var outline = rowGo.AddComponent<Outline>();
        outline.effectColor = new Color(0.15f, 0.35f, 0.55f, 0.35f);
        outline.effectDistance = new Vector2(1f, -1f);

        _langLabelText = MakeLabel(rowGo.transform, "Label", new Vector2(16f, -18f), new Vector2(300f, 22f), AdventureLocalizationManager.Get("opt_language"), 14, new Color(0.92f, 0.96f, 1f, 1f));
        _langLabelText.fontStyle = FontStyle.Bold;

        // セレクター [ ◀ ] [ 言語名 ] [ ▶ ]
        var selGo = new GameObject("SelectorContainer");
        selGo.transform.SetParent(rowGo.transform, false);
        var sRt = selGo.AddComponent<RectTransform>();
        sRt.anchorMin = new Vector2(1f, 0.5f);
        sRt.anchorMax = new Vector2(1f, 0.5f);
        sRt.pivot = new Vector2(1f, 0.5f);
        sRt.anchoredPosition = new Vector2(-16f, 0f);
        sRt.sizeDelta = new Vector2(320f, 44f);

        // 左ボタン [ ◀ ]
        var prevBtnGo = new GameObject("PrevBtn");
        prevBtnGo.transform.SetParent(selGo.transform, false);
        var pbRt = prevBtnGo.AddComponent<RectTransform>();
        pbRt.anchorMin = new Vector2(0f, 0.5f);
        pbRt.anchorMax = new Vector2(0f, 0.5f);
        pbRt.pivot = new Vector2(0f, 0.5f);
        pbRt.anchoredPosition = new Vector2(0f, 0f);
        pbRt.sizeDelta = new Vector2(48f, 40f);
        var pbImg = prevBtnGo.AddComponent<Image>();
        pbImg.color = new Color(0.15f, 0.35f, 0.55f, 0.95f);
        pbImg.raycastTarget = true;
        _prevLangBtn = prevBtnGo.AddComponent<Button>();
        _prevLangBtn.onClick.AddListener(OnPrevLanguageClicked);
        var pbColors = _prevLangBtn.colors;
        pbColors.highlightedColor = new Color(0.25f, 0.55f, 0.85f, 1f);
        pbColors.pressedColor = new Color(0.10f, 0.20f, 0.35f, 1f);
        _prevLangBtn.colors = pbColors;
        var pbOutline = prevBtnGo.AddComponent<Outline>();
        pbOutline.effectColor = new Color(0.35f, 0.75f, 1f, 0.6f);
        pbOutline.effectDistance = new Vector2(1f, -1f);
        var pbText = MakeLabel(prevBtnGo.transform, "Text", Vector2.zero, Vector2.zero, "◀", 16, Color.white);
        pbText.alignment = TextAnchor.MiddleCenter;

        // 言語名表示ラベル（クリックで次言語へ進むボタンとしても機能）
        var langTextGo = new GameObject("LangText");
        langTextGo.transform.SetParent(selGo.transform, false);
        var ltRt = langTextGo.AddComponent<RectTransform>();
        ltRt.anchorMin = new Vector2(0.5f, 0.5f);
        ltRt.anchorMax = new Vector2(0.5f, 0.5f);
        ltRt.pivot = new Vector2(0.5f, 0.5f);
        ltRt.anchoredPosition = Vector2.zero;
        ltRt.sizeDelta = new Vector2(212f, 40f);
        var ltBg = langTextGo.AddComponent<Image>();
        ltBg.color = new Color(0.04f, 0.09f, 0.16f, 0.85f);
        ltBg.raycastTarget = true;
        _langValueBtn = langTextGo.AddComponent<Button>();
        _langValueBtn.onClick.AddListener(OnNextLanguageClicked);
        var ltColors = _langValueBtn.colors;
        ltColors.highlightedColor = new Color(0.08f, 0.16f, 0.28f, 0.95f);
        ltColors.pressedColor = new Color(0.02f, 0.05f, 0.10f, 1f);
        _langValueBtn.colors = ltColors;
        var ltOutline = langTextGo.AddComponent<Outline>();
        ltOutline.effectColor = new Color(0.20f, 0.45f, 0.70f, 0.4f);
        ltOutline.effectDistance = new Vector2(1f, -1f);

        _langValueText = MakeLabel(langTextGo.transform, "LabelText", Vector2.zero, Vector2.zero,
            AdventureLocalizationManager.LanguageNames[(int)AdventureLocalizationManager.CurrentLanguage], 15, new Color(1f, 0.88f, 0.40f, 1f));
        _langValueText.fontStyle = FontStyle.Bold;
        _langValueText.alignment = TextAnchor.MiddleCenter;
        _langValueText.raycastTarget = false;

        // 右ボタン [ ▶ ]
        var nextBtnGo = new GameObject("NextBtn");
        nextBtnGo.transform.SetParent(selGo.transform, false);
        var nbRt = nextBtnGo.AddComponent<RectTransform>();
        nbRt.anchorMin = new Vector2(1f, 0.5f);
        nbRt.anchorMax = new Vector2(1f, 0.5f);
        nbRt.pivot = new Vector2(1f, 0.5f);
        nbRt.anchoredPosition = new Vector2(0f, 0f);
        nbRt.sizeDelta = new Vector2(48f, 40f);
        var nbImg = nextBtnGo.AddComponent<Image>();
        nbImg.color = new Color(0.15f, 0.35f, 0.55f, 0.95f);
        nbImg.raycastTarget = true;
        _nextLangBtn = nextBtnGo.AddComponent<Button>();
        _nextLangBtn.onClick.AddListener(OnNextLanguageClicked);
        var nbColors = _nextLangBtn.colors;
        nbColors.highlightedColor = new Color(0.25f, 0.55f, 0.85f, 1f);
        nbColors.pressedColor = new Color(0.10f, 0.20f, 0.35f, 1f);
        _nextLangBtn.colors = nbColors;
        var nbOutline = nextBtnGo.AddComponent<Outline>();
        nbOutline.effectColor = new Color(0.35f, 0.75f, 1f, 0.6f);
        nbOutline.effectDistance = new Vector2(1f, -1f);
        var nbText = MakeLabel(nextBtnGo.transform, "Text", Vector2.zero, Vector2.zero, "▶", 16, Color.white);
        nbText.alignment = TextAnchor.MiddleCenter;
    }

    void CreateDisplayTab(Transform parent)
    {
        _tabDisplayPanel = new GameObject("TabDisplayPanel");
        _tabDisplayPanel.transform.SetParent(parent, false);
        var rt = _tabDisplayPanel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        float rowH = 62f;

        // 1. 画面モード
        CreateDisplayRow(_tabDisplayPanel.transform, AdventureLocalizationManager.Get("opt_display_mode"), -10f, rowH, out var row1, out _screenModeRowLabel);
        _fsBtn = CreateOptionButton(row1, "FsBtn", new Vector2(240f, 0f), new Vector2(220f, 38f), AdventureLocalizationManager.Get("opt_fullscreen"), () => SetFullscreen(true), out _fsBtnImg, out _fullscreenBtnText);
        _winBtn = CreateOptionButton(row1, "WinBtn", new Vector2(480f, 0f), new Vector2(220f, 38f), AdventureLocalizationManager.Get("opt_windowed"), () => SetFullscreen(false), out _winBtnImg, out _windowedBtnText);

        // 2. グラフィックス品質
        CreateDisplayRow(_tabDisplayPanel.transform, AdventureLocalizationManager.Get("opt_quality"), -10f - rowH - 12f, rowH, out var row2, out _qualityRowLabel);
        _qUltraBtn = CreateOptionButton(row2, "QUltra", new Vector2(240f, 0f), new Vector2(160f, 38f), AdventureLocalizationManager.Get("opt_quality_ultra"), () => SetQuality(2), out _qUltraBtnImg, out _qUltraBtnText);
        _qHighBtn = CreateOptionButton(row2, "QHigh", new Vector2(420f, 0f), new Vector2(160f, 38f), AdventureLocalizationManager.Get("opt_quality_high"), () => SetQuality(1), out _qHighBtnImg, out _qHighBtnText);
        _qFastBtn = CreateOptionButton(row2, "QFast", new Vector2(600f, 0f), new Vector2(160f, 38f), AdventureLocalizationManager.Get("opt_quality_fast"), () => SetQuality(0), out _qFastBtnImg, out _qFastBtnText);

        // 3. 垂直同期 (VSync)
        CreateDisplayRow(_tabDisplayPanel.transform, AdventureLocalizationManager.Get("opt_vsync"), -10f - (rowH + 12f) * 2, rowH, out var row3, out _vsyncRowLabel);
        _vsyncOnBtn = CreateOptionButton(row3, "VSyncOn", new Vector2(240f, 0f), new Vector2(220f, 38f), AdventureLocalizationManager.Get("opt_vsync_on"), () => SetVSync(true), out _vsyncOnBtnImg, out _vsyncOnBtnText);
        _vsyncOffBtn = CreateOptionButton(row3, "VSyncOff", new Vector2(480f, 0f), new Vector2(220f, 38f), AdventureLocalizationManager.Get("opt_vsync_off"), () => SetVSync(false), out _vsyncOffBtnImg, out _vsyncOffBtnText);

        // 4. 画面解像度
        CreateDisplayRow(_tabDisplayPanel.transform, AdventureLocalizationManager.Get("opt_resolution"), -10f - (rowH + 12f) * 3, rowH, out var row4, out _resolutionRowLabel);
        _res1080Btn = CreateOptionButton(row4, "Res1080", new Vector2(240f, 0f), new Vector2(160f, 38f), "1920 × 1080", () => SetResolution(1920, 1080), out _res1080BtnImg, out _);
        _res900Btn = CreateOptionButton(row4, "Res900", new Vector2(420f, 0f), new Vector2(160f, 38f), "1600 × 900", () => SetResolution(1600, 900), out _res900BtnImg, out _);
        _res720Btn = CreateOptionButton(row4, "Res720", new Vector2(600f, 0f), new Vector2(160f, 38f), "1280 × 720", () => SetResolution(1280, 720), out _res720BtnImg, out _);

        _displayStatusText = MakeLabel(_tabDisplayPanel.transform, "DisplayStatus", new Vector2(12f, 8f),
            new Vector2(820f, 22f), "", 12, new Color(0.55f, 0.82f, 0.95f, 0.9f));
        _displayStatusText.alignment = TextAnchor.LowerLeft;
    }

    void ToggleHudCompass()
    {
        AdventureExplorationHudSettings.ShowCompass = !AdventureExplorationHudSettings.ShowCompass;
        UpdateHudToggleHighlights();
    }

    void ToggleHudPartsBoard()
    {
        AdventureExplorationHudSettings.ShowPartsBoard = !AdventureExplorationHudSettings.ShowPartsBoard;
        UpdateHudToggleHighlights();
    }

    void ToggleHudOilBoard()
    {
        AdventureExplorationHudSettings.ShowOilBoard = !AdventureExplorationHudSettings.ShowOilBoard;
        UpdateHudToggleHighlights();
    }

    void UpdateHudToggleHighlights()
    {
        Color hudOn = new Color(0.12f, 0.55f, 0.42f, 1f);
        Color hudOff = new Color(0.22f, 0.12f, 0.12f, 0.85f);
        if (_hudCompassBtnImg != null)
            _hudCompassBtnImg.color = AdventureExplorationHudSettings.ShowCompass ? hudOn : hudOff;
        if (_hudPartsBtnImg != null)
            _hudPartsBtnImg.color = AdventureExplorationHudSettings.ShowPartsBoard ? hudOn : hudOff;
        if (_hudOilBtnImg != null)
            _hudOilBtnImg.color = AdventureExplorationHudSettings.ShowOilBoard ? hudOn : hudOff;
        RefreshHudToggleLabels();
    }

    void RefreshHudToggleLabels()
    {
        string on = AdventureLocalizationManager.Get("opt_hud_show");
        string off = AdventureLocalizationManager.Get("opt_hud_hide");
        if (_hudCompassBtnText != null)
            _hudCompassBtnText.text = $"{AdventureLocalizationManager.Get("opt_hud_compass")}: {(AdventureExplorationHudSettings.ShowCompass ? on : off)}";
        if (_hudPartsBtnText != null)
            _hudPartsBtnText.text = $"{AdventureLocalizationManager.Get("opt_hud_parts")}: {(AdventureExplorationHudSettings.ShowPartsBoard ? on : off)}";
        if (_hudOilBtnText != null)
            _hudOilBtnText.text = $"{AdventureLocalizationManager.Get("opt_hud_oil")}: {(AdventureExplorationHudSettings.ShowOilBoard ? on : off)}";
    }

    void CreateDisplayRow(Transform parent, string title, float topY, float height, out Transform rowTransform, out Text titleText)
    {
        var rowGo = new GameObject(title + "_Row");
        rowGo.transform.SetParent(parent, false);
        var rRt = rowGo.AddComponent<RectTransform>();
        rRt.anchorMin = new Vector2(0f, 1f);
        rRt.anchorMax = new Vector2(1f, 1f);
        rRt.pivot = new Vector2(0f, 1f);
        rRt.anchoredPosition = new Vector2(0f, topY);
        rRt.sizeDelta = new Vector2(0f, height);

        var bg = rowGo.AddComponent<Image>();
        bg.color = new Color(0.015f, 0.04f, 0.08f, 0.65f);
        bg.raycastTarget = false;

        var outline = rowGo.AddComponent<Outline>();
        outline.effectColor = new Color(0.15f, 0.35f, 0.55f, 0.35f);
        outline.effectDistance = new Vector2(1f, -1f);

        titleText = MakeLabel(rowGo.transform, "Label", new Vector2(16f, 0f), new Vector2(200f, height), title, 14, new Color(0.92f, 0.96f, 1f, 1f));
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleLeft;

        rowTransform = rowGo.transform;
    }

    Button CreateOptionButton(Transform parent, string name, Vector2 pos, Vector2 size, string label, UnityEngine.Events.UnityAction onClick, out Image outImg, out Text outText)
    {
        var btnGo = new GameObject(name);
        btnGo.transform.SetParent(parent, false);
        var rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        outImg = btnGo.AddComponent<Image>();
        outImg.color = new Color(0.05f, 0.10f, 0.18f, 0.80f);

        var outline = btnGo.AddComponent<Outline>();
        outline.effectColor = new Color(0.25f, 0.65f, 0.85f, 0.45f);
        outline.effectDistance = new Vector2(1f, -1f);

        var btn = btnGo.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        outText = MakeLabel(btnGo.transform, "Text", Vector2.zero, size, label, 13, Color.white);
        outText.fontStyle = FontStyle.Bold;
        outText.alignment = TextAnchor.MiddleCenter;

        return btn;
    }

    void CreateGuideTab(Transform parent)
    {
        _tabGuidePanel = new GameObject("TabGuidePanel");
        _tabGuidePanel.transform.SetParent(parent, false);
        var rt = _tabGuidePanel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bg = _tabGuidePanel.AddComponent<Image>();
        bg.color = new Color(0.015f, 0.04f, 0.08f, 0.65f);

        float hudRowH = 62f;
        CreateDisplayRow(_tabGuidePanel.transform, AdventureLocalizationManager.Get("opt_hud_exploration"), -8f, hudRowH, out var hudRow, out _hudExplorationRowLabel);
        _hudCompassBtn = CreateOptionButton(hudRow, "HudCompass", new Vector2(240f, 0f), new Vector2(200f, 38f), "", () => { }, out _hudCompassBtnImg, out _hudCompassBtnText);
        _hudPartsBtn = CreateOptionButton(hudRow, "HudParts", new Vector2(460f, 0f), new Vector2(200f, 38f), "", () => { }, out _hudPartsBtnImg, out _hudPartsBtnText);
        _hudOilBtn = CreateOptionButton(hudRow, "HudOil", new Vector2(680f, 0f), new Vector2(160f, 38f), "", () => { }, out _hudOilBtnImg, out _hudOilBtnText);
        _hudGuideHintText = MakeLabel(_tabGuidePanel.transform, "HudGuideHint", new Vector2(20f, -78f), new Vector2(820f, 36f),
            AdventureLocalizationManager.Get("opt_hud_guide_hint"), 12, new Color(1f, 0.88f, 0.45f, 0.95f));
        _hudGuideHintText.alignment = TextAnchor.UpperLeft;
        UpdateHudToggleHighlights();

        // 2カラムで操作ガイドを分かりやすく表示（多言語対応）
        _guideLeftText = MakeLabel(_tabGuidePanel.transform, "LeftCol", new Vector2(25f, -118f), new Vector2(380f, 250f), "", 13, new Color(0.95f, 0.97f, 1f, 0.95f));
        _guideLeftText.lineSpacing = 1.35f;
        _guideLeftText.alignment = TextAnchor.UpperLeft;
        _guideLeftText.supportRichText = true;

        _guideRightText = MakeLabel(_tabGuidePanel.transform, "RightCol", new Vector2(430f, -118f), new Vector2(380f, 250f), "", 13, new Color(0.95f, 0.97f, 1f, 0.95f));
        _guideRightText.lineSpacing = 1.35f;
        _guideRightText.alignment = TextAnchor.UpperLeft;
        _guideRightText.supportRichText = true;
    }

    void CreateCreditsTab(Transform parent)
    {
        _tabCreditsPanel = new GameObject("TabCreditsPanel");
        _tabCreditsPanel.transform.SetParent(parent, false);
        var rt = _tabCreditsPanel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bg = _tabCreditsPanel.AddComponent<Image>();
        bg.color = new Color(0.015f, 0.04f, 0.08f, 0.65f);

        _creditsLeftText = MakeLabel(_tabCreditsPanel.transform, "CreditsLeft", new Vector2(25f, -15f), new Vector2(380f, 320f), "", 12, new Color(0.95f, 0.97f, 1f, 0.95f));
        _creditsLeftText.lineSpacing = 1.30f;
        _creditsLeftText.alignment = TextAnchor.UpperLeft;
        _creditsLeftText.supportRichText = true;

        _creditsRightText = MakeLabel(_tabCreditsPanel.transform, "CreditsRight", new Vector2(430f, -15f), new Vector2(380f, 320f), "", 12, new Color(0.95f, 0.97f, 1f, 0.95f));
        _creditsRightText.lineSpacing = 1.30f;
        _creditsRightText.alignment = TextAnchor.UpperLeft;
        _creditsRightText.supportRichText = true;

        ApplyLocalization();
    }

    void ApplyLocalization()
    {
        if (_headerTitleText != null)
            _headerTitleText.text = _openedFromTitle ? AdventureLocalizationManager.Get("settings_header") : AdventureLocalizationManager.Get("pause_header");
        if (_headerSubTitleText != null)
            _headerSubTitleText.text = _openedFromTitle ? AdventureLocalizationManager.Get("settings_sub") : AdventureLocalizationManager.Get("pause_sub");

        if (_tabAudioBtnLabel != null) _tabAudioBtnLabel.text = AdventureLocalizationManager.Get("tab_audio");
        if (_tabDisplayBtnLabel != null) _tabDisplayBtnLabel.text = AdventureLocalizationManager.Get("tab_display");
        if (_tabGuideBtnLabel != null) _tabGuideBtnLabel.text = AdventureLocalizationManager.Get("tab_guide");
        if (_tabCreditsBtnLabel != null) _tabCreditsBtnLabel.text = AdventureLocalizationManager.Get("tab_credits");

        if (_resumeBtnText != null)
            _resumeBtnText.text = _openedFromTitle ? AdventureLocalizationManager.Get("pause_close") : AdventureLocalizationManager.Get("pause_resume");
        if (_restartBtnText != null)
            _restartBtnText.text = AdventureLocalizationManager.Get("pause_restart");
        if (_returnTitleBtnText != null)
            _returnTitleBtnText.text = AdventureLocalizationManager.Get("pause_title_return");

        // オーディオ＆操作
        Font menuFont = ResolveFont();
        if (_langLabelText != null)
        {
            if (menuFont != null) _langLabelText.font = menuFont;
            _langLabelText.text = AdventureLocalizationManager.Get("opt_language");
        }
        if (_langValueText != null)
        {
            if (menuFont != null) _langValueText.font = menuFont;
            _langValueText.text = AdventureLocalizationManager.LanguageNames[(int)AdventureLocalizationManager.CurrentLanguage];
        }
        if (_bgmLabelText != null) _bgmLabelText.text = AdventureLocalizationManager.Get("opt_bgm");
        if (_seLabelText != null) _seLabelText.text = AdventureLocalizationManager.Get("opt_se");
        if (_sensLabelText != null) _sensLabelText.text = AdventureLocalizationManager.Get("opt_sensitivity");
        if (_audioHintText != null) _audioHintText.text = AdventureLocalizationManager.Get("opt_audio_hint");

        // ディスプレイ＆画質
        if (_screenModeRowLabel != null) _screenModeRowLabel.text = AdventureLocalizationManager.Get("opt_display_mode");
        if (_qualityRowLabel != null) _qualityRowLabel.text = AdventureLocalizationManager.Get("opt_quality");
        if (_vsyncRowLabel != null) _vsyncRowLabel.text = AdventureLocalizationManager.Get("opt_vsync");
        if (_resolutionRowLabel != null) _resolutionRowLabel.text = AdventureLocalizationManager.Get("opt_resolution");
        if (_hudExplorationRowLabel != null) _hudExplorationRowLabel.text = AdventureLocalizationManager.Get("opt_hud_exploration");
        if (_hudGuideHintText != null) _hudGuideHintText.text = AdventureLocalizationManager.Get("opt_hud_guide_hint");
        UpdateHudToggleHighlights();
        if (_fullscreenBtnText != null) _fullscreenBtnText.text = AdventureLocalizationManager.Get("opt_fullscreen");
        if (_windowedBtnText != null) _windowedBtnText.text = AdventureLocalizationManager.Get("opt_windowed");
        if (_qUltraBtnText != null) _qUltraBtnText.text = AdventureLocalizationManager.Get("opt_quality_ultra");
        if (_qHighBtnText != null) _qHighBtnText.text = AdventureLocalizationManager.Get("opt_quality_high");
        if (_qFastBtnText != null) _qFastBtnText.text = AdventureLocalizationManager.Get("opt_quality_fast");
        if (_vsyncOnBtnText != null) _vsyncOnBtnText.text = AdventureLocalizationManager.Get("opt_vsync_on");
        if (_vsyncOffBtnText != null) _vsyncOffBtnText.text = AdventureLocalizationManager.Get("opt_vsync_off");

        if (_creditsLeftText != null)
            _creditsLeftText.text = $"<b>{AdventureLocalizationManager.Get("credits_project_title")}</b>\n{AdventureLocalizationManager.Get("credits_project_body")}\n\n<b>{AdventureLocalizationManager.Get("credits_license_title")}</b>\n{AdventureLocalizationManager.Get("credits_license_body")}";

        if (_creditsRightText != null)
            _creditsRightText.text = $"<b>{AdventureLocalizationManager.Get("credits_thanks_header")}</b>\n{AdventureLocalizationManager.Get("credits_thanks_body")}\n\n<b>{AdventureLocalizationManager.Get("credits_platform_title")}</b>\n{AdventureLocalizationManager.Get("credits_platform_body")}\n\n<i>Special Thanks to all testers and explorers soaring in the sky.</i>";

        if (_guideLeftText != null)
            _guideLeftText.text = AdventureLocalizationManager.Get("guide_basic_body");
        if (_guideRightText != null)
            _guideRightText.text = AdventureLocalizationManager.Get("guide_interact_body");

        if (_restartModalTitleText != null) _restartModalTitleText.text = AdventureLocalizationManager.Get("confirm_restart_title");
        if (_restartModalDescText != null) _restartModalDescText.text = AdventureLocalizationManager.Get("confirm_restart_desc");
        if (_restartModalYesBtnText != null) _restartModalYesBtnText.text = AdventureLocalizationManager.Get("confirm_restart_yes");
        if (_restartModalNoBtnText != null) _restartModalNoBtnText.text = AdventureLocalizationManager.Get("confirm_restart_no");

        if (_returnTitleModalTitleText != null) _returnTitleModalTitleText.text = AdventureLocalizationManager.Get("confirm_title_title");
        if (_returnTitleModalDescText != null) _returnTitleModalDescText.text = AdventureLocalizationManager.Get("confirm_title_desc");
        if (_returnTitleModalYesBtnText != null) _returnTitleModalYesBtnText.text = AdventureLocalizationManager.Get("confirm_title_yes");
        if (_returnTitleModalNoBtnText != null) _returnTitleModalNoBtnText.text = AdventureLocalizationManager.Get("confirm_title_no");

        UpdateDisplayHighlights();
    }

    void CreateSettingSlider(Transform parent, string label, float topOffset, float minVal, float maxVal, out Slider slider, out Text valueText, out Text labelText, UnityEngine.Events.UnityAction<float> onValueChanged)
    {
        var rowGo = new GameObject(label + "_Row");
        rowGo.transform.SetParent(parent, false);
        var rRt = rowGo.AddComponent<RectTransform>();
        rRt.anchorMin = new Vector2(0f, 1f);
        rRt.anchorMax = new Vector2(1f, 1f);
        rRt.pivot = new Vector2(0f, 1f);
        rRt.anchoredPosition = new Vector2(0f, topOffset);
        rRt.sizeDelta = new Vector2(0f, 68f);

        var rowBg = rowGo.AddComponent<Image>();
        rowBg.color = new Color(0.015f, 0.04f, 0.08f, 0.65f);
        rowBg.raycastTarget = false;

        var outline = rowGo.AddComponent<Outline>();
        outline.effectColor = new Color(0.15f, 0.35f, 0.55f, 0.35f);
        outline.effectDistance = new Vector2(1f, -1f);

        labelText = MakeLabel(rowGo.transform, "Label", new Vector2(16f, -10f), new Vector2(300f, 22f), label, 14, new Color(0.92f, 0.96f, 1f, 1f));
        labelText.fontStyle = FontStyle.Bold;

        valueText = MakeLabel(rowGo.transform, "ValueText", new Vector2(-20f, -10f), new Vector2(120f, 22f), "100%", 14, new Color(1f, 0.85f, 0.35f, 1f));
        valueText.fontStyle = FontStyle.Bold;
        valueText.alignment = TextAnchor.MiddleRight;
        var vRt = valueText.rectTransform;
        vRt.anchorMin = new Vector2(1f, 1f);
        vRt.anchorMax = new Vector2(1f, 1f);
        vRt.pivot = new Vector2(1f, 1f);

        var sGo = new GameObject("Slider");
        sGo.transform.SetParent(rowGo.transform, false);
        var sRt = sGo.AddComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 0f);
        sRt.anchorMax = new Vector2(1f, 0f);
        sRt.pivot = new Vector2(0.5f, 0f);
        sRt.anchoredPosition = new Vector2(0f, 14f);
        sRt.sizeDelta = new Vector2(-32f, 20f);

        slider = sGo.AddComponent<Slider>();
        slider.minValue = minVal;
        slider.maxValue = maxVal;
        slider.onValueChanged.AddListener(onValueChanged);

        var bgRailGo = new GameObject("Background");
        bgRailGo.transform.SetParent(sGo.transform, false);
        var brRt = bgRailGo.AddComponent<RectTransform>();
        brRt.anchorMin = new Vector2(0f, 0.3f);
        brRt.anchorMax = new Vector2(1f, 0.7f);
        brRt.offsetMin = Vector2.zero;
        brRt.offsetMax = Vector2.zero;
        var brImg = bgRailGo.AddComponent<Image>();
        brImg.color = new Color(0.12f, 0.18f, 0.28f, 0.9f);

        var fillAreaGo = new GameObject("Fill Area");
        fillAreaGo.transform.SetParent(sGo.transform, false);
        var faRt = fillAreaGo.AddComponent<RectTransform>();
        faRt.anchorMin = new Vector2(0f, 0.3f);
        faRt.anchorMax = new Vector2(1f, 0.7f);
        faRt.offsetMin = Vector2.zero;
        faRt.offsetMax = Vector2.zero;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(fillAreaGo.transform, false);
        var fRt = fillGo.AddComponent<RectTransform>();
        fRt.sizeDelta = Vector2.zero;
        var fImg = fillGo.AddComponent<Image>();
        fImg.color = new Color(0.28f, 0.78f, 1f, 1f);
        slider.fillRect = fRt;

        var handleAreaGo = new GameObject("Handle Slide Area");
        handleAreaGo.transform.SetParent(sGo.transform, false);
        var haRt = handleAreaGo.AddComponent<RectTransform>();
        haRt.anchorMin = Vector2.zero;
        haRt.anchorMax = Vector2.one;
        haRt.offsetMin = Vector2.zero;
        haRt.offsetMax = Vector2.zero;

        var handleGo = new GameObject("Handle");
        handleGo.transform.SetParent(handleAreaGo.transform, false);
        var hRtObj = handleGo.AddComponent<RectTransform>();
        hRtObj.sizeDelta = new Vector2(22f, 22f);
        var hImg = handleGo.AddComponent<Image>();
        hImg.color = new Color(1f, 0.92f, 0.5f, 1f);
        slider.handleRect = hRtObj;
        slider.targetGraphic = hImg;
    }

    void CreateFooterButtons(Transform panel)
    {
        var footerGo = new GameObject("FooterBar");
        footerGo.transform.SetParent(panel, false);
        var fRt = footerGo.AddComponent<RectTransform>();
        fRt.anchorMin = new Vector2(0f, 0f);
        fRt.anchorMax = new Vector2(1f, 0f);
        fRt.pivot = new Vector2(0.5f, 0f);
        fRt.anchoredPosition = new Vector2(0f, 18f);
        fRt.sizeDelta = new Vector2(-60f, 48f);

        // 1. 「最初からやり直す」（左: 0.02〜0.31）
        _restartBtnGo = new GameObject("RestartButton");
        _restartBtnGo.transform.SetParent(footerGo.transform, false);
        var restRt = _restartBtnGo.AddComponent<RectTransform>();
        restRt.anchorMin = new Vector2(0.02f, 0f);
        restRt.anchorMax = new Vector2(0.31f, 1f);
        restRt.offsetMin = Vector2.zero;
        restRt.offsetMax = Vector2.zero;

        var restImg = _restartBtnGo.AddComponent<Image>();
        restImg.color = new Color(0.48f, 0.15f, 0.18f, 0.95f);
        _restartBtn = _restartBtnGo.AddComponent<Button>();
        _restartBtn.onClick.AddListener(OnRestartClicked);

        _restartBtnText = MakeLabel(_restartBtnGo.transform, "Text", Vector2.zero, Vector2.zero, "🗑 最初からやり直す", 15, new Color(1f, 0.88f, 0.88f, 1f));
        _restartBtnText.fontStyle = FontStyle.Bold;
        _restartBtnText.alignment = TextAnchor.MiddleCenter;

        // 2. 「タイトルへ戻る」（中央: 0.35〜0.64）
        _returnTitleBtnGo = new GameObject("ReturnTitleButton");
        _returnTitleBtnGo.transform.SetParent(footerGo.transform, false);
        var titleRt = _returnTitleBtnGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.35f, 0f);
        titleRt.anchorMax = new Vector2(0.64f, 1f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;

        var titleImg = _returnTitleBtnGo.AddComponent<Image>();
        titleImg.color = new Color(0.20f, 0.35f, 0.55f, 0.95f);
        _returnTitleBtn = _returnTitleBtnGo.AddComponent<Button>();
        _returnTitleBtn.onClick.AddListener(OnReturnToTitleClicked);

        _returnTitleBtnText = MakeLabel(_returnTitleBtnGo.transform, "Text", Vector2.zero, Vector2.zero, "✦ タイトルへ戻る", 15, new Color(0.90f, 0.96f, 1f, 1f));
        _returnTitleBtnText.fontStyle = FontStyle.Bold;
        _returnTitleBtnText.alignment = TextAnchor.MiddleCenter;

        // 3. 「ゲームに戻る」（右: 0.68〜0.98）
        var resumeGo = new GameObject("ResumeButton");
        resumeGo.transform.SetParent(footerGo.transform, false);
        var rRt = resumeGo.AddComponent<RectTransform>();
        rRt.anchorMin = new Vector2(0.68f, 0f);
        rRt.anchorMax = new Vector2(0.98f, 1f);
        rRt.offsetMin = Vector2.zero;
        rRt.offsetMax = Vector2.zero;

        var rImg = resumeGo.AddComponent<Image>();
        rImg.color = new Color(0.12f, 0.55f, 0.85f, 1f);
        _resumeBtn = resumeGo.AddComponent<Button>();
        _resumeBtn.onClick.AddListener(() => SetMenuVisible(false));

        _resumeBtnText = MakeLabel(resumeGo.transform, "Text", Vector2.zero, Vector2.zero, "▶ ゲームに戻る [ESC]", 15, Color.white);
        _resumeBtnText.fontStyle = FontStyle.Bold;
        _resumeBtnText.alignment = TextAnchor.MiddleCenter;
    }

    void CreateRestartConfirmModal(Transform canvasRoot)
    {
        _confirmModalRoot = new GameObject("RestartConfirmModal");
        _confirmModalRoot.transform.SetParent(canvasRoot, false);
        var mRt = _confirmModalRoot.AddComponent<RectTransform>();
        mRt.anchorMin = Vector2.zero;
        mRt.anchorMax = Vector2.one;
        mRt.offsetMin = Vector2.zero;
        mRt.offsetMax = Vector2.zero;

        var mBg = _confirmModalRoot.AddComponent<Image>();
        mBg.color = new Color(0f, 0f, 0f, 0.85f);

        var cardGo = new GameObject("ConfirmCard");
        cardGo.transform.SetParent(_confirmModalRoot.transform, false);
        var cRt = cardGo.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.5f, 0.5f);
        cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.sizeDelta = new Vector2(520f, 260f);
        cRt.anchoredPosition = Vector2.zero;

        var cBg = cardGo.AddComponent<Image>();
        cBg.color = new Color(0.08f, 0.04f, 0.05f, 0.98f);
        var cOutline = cardGo.AddComponent<Outline>();
        cOutline.effectColor = new Color(0.85f, 0.35f, 0.35f, 0.8f);
        cOutline.effectDistance = new Vector2(2f, -2f);

        _restartModalTitleText = MakeLabel(cardGo.transform, "Title", new Vector2(0f, -24f), new Vector2(480f, 32f), AdventureLocalizationManager.Get("confirm_restart_title"), 20, new Color(1f, 0.45f, 0.45f, 1f));
        _restartModalTitleText.fontStyle = FontStyle.Bold;
        _restartModalTitleText.alignment = TextAnchor.MiddleCenter;

        _restartModalDescText = MakeLabel(cardGo.transform, "Desc", new Vector2(0f, -68f), new Vector2(440f, 75f),
            AdventureLocalizationManager.Get("confirm_restart_desc"), 14, new Color(0.95f, 0.92f, 0.92f, 0.95f));
        _restartModalDescText.lineSpacing = 1.35f;
        _restartModalDescText.alignment = TextAnchor.MiddleCenter;

        // はい
        var yesGo = new GameObject("YesButton");
        yesGo.transform.SetParent(cardGo.transform, false);
        var yRt = yesGo.AddComponent<RectTransform>();
        yRt.anchorMin = new Vector2(0.1f, 0f);
        yRt.anchorMax = new Vector2(0.48f, 0f);
        yRt.pivot = new Vector2(0.5f, 0f);
        yRt.anchoredPosition = new Vector2(0f, 22f);
        yRt.sizeDelta = new Vector2(0f, 44f);
        var yImg = yesGo.AddComponent<Image>();
        yImg.color = new Color(0.75f, 0.18f, 0.22f, 1f);
        var yBtn = yesGo.AddComponent<Button>();
        yBtn.onClick.AddListener(OnConfirmRestartYes);
        _restartModalYesBtnText = MakeLabel(yesGo.transform, "Text", Vector2.zero, Vector2.zero, AdventureLocalizationManager.Get("confirm_restart_yes"), 15, Color.white);
        _restartModalYesBtnText.fontStyle = FontStyle.Bold;
        _restartModalYesBtnText.alignment = TextAnchor.MiddleCenter;

        // キャンセル
        var noGo = new GameObject("NoButton");
        noGo.transform.SetParent(cardGo.transform, false);
        var nRt = noGo.AddComponent<RectTransform>();
        nRt.anchorMin = new Vector2(0.52f, 0f);
        nRt.anchorMax = new Vector2(0.9f, 0f);
        nRt.pivot = new Vector2(0.5f, 0f);
        nRt.anchoredPosition = new Vector2(0f, 22f);
        nRt.sizeDelta = new Vector2(0f, 44f);
        var nImg = noGo.AddComponent<Image>();
        nImg.color = new Color(0.2f, 0.3f, 0.45f, 1f);
        var nBtn = noGo.AddComponent<Button>();
        nBtn.onClick.AddListener(OnConfirmRestartNo);
        _restartModalNoBtnText = MakeLabel(noGo.transform, "Text", Vector2.zero, Vector2.zero, AdventureLocalizationManager.Get("confirm_restart_no"), 15, Color.white);
        _restartModalNoBtnText.fontStyle = FontStyle.Bold;
        _restartModalNoBtnText.alignment = TextAnchor.MiddleCenter;

        _confirmModalRoot.SetActive(false);
    }

    void CreateReturnTitleConfirmModal(Transform canvasRoot)
    {
        _returnTitleConfirmModal = new GameObject("ReturnTitleConfirmModal");
        _returnTitleConfirmModal.transform.SetParent(canvasRoot, false);
        var mRt = _returnTitleConfirmModal.AddComponent<RectTransform>();
        mRt.anchorMin = Vector2.zero;
        mRt.anchorMax = Vector2.one;
        mRt.offsetMin = Vector2.zero;
        mRt.offsetMax = Vector2.zero;

        var mBg = _returnTitleConfirmModal.AddComponent<Image>();
        mBg.color = new Color(0f, 0f, 0f, 0.85f);

        var cardGo = new GameObject("ConfirmCard");
        cardGo.transform.SetParent(_returnTitleConfirmModal.transform, false);
        var cRt = cardGo.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.5f, 0.5f);
        cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.sizeDelta = new Vector2(520f, 260f);
        cRt.anchoredPosition = Vector2.zero;

        var cBg = cardGo.AddComponent<Image>();
        cBg.color = new Color(0.04f, 0.07f, 0.12f, 0.98f);
        var cOutline = cardGo.AddComponent<Outline>();
        cOutline.effectColor = new Color(0.35f, 0.75f, 1f, 0.8f);
        cOutline.effectDistance = new Vector2(2f, -2f);

        _returnTitleModalTitleText = MakeLabel(cardGo.transform, "Title", new Vector2(0f, -24f), new Vector2(480f, 32f), AdventureLocalizationManager.Get("confirm_title_title"), 20, new Color(0.45f, 0.90f, 1f, 1f));
        _returnTitleModalTitleText.fontStyle = FontStyle.Bold;
        _returnTitleModalTitleText.alignment = TextAnchor.MiddleCenter;

        _returnTitleModalDescText = MakeLabel(cardGo.transform, "Desc", new Vector2(0f, -68f), new Vector2(440f, 75f),
            AdventureLocalizationManager.Get("confirm_title_desc"), 14, new Color(0.92f, 0.95f, 1f, 0.95f));
        _returnTitleModalDescText.lineSpacing = 1.35f;
        _returnTitleModalDescText.alignment = TextAnchor.MiddleCenter;

        // はい
        var yesGo = new GameObject("YesButton");
        yesGo.transform.SetParent(cardGo.transform, false);
        var yRt = yesGo.AddComponent<RectTransform>();
        yRt.anchorMin = new Vector2(0.1f, 0f);
        yRt.anchorMax = new Vector2(0.48f, 0f);
        yRt.pivot = new Vector2(0.5f, 0f);
        yRt.anchoredPosition = new Vector2(0f, 22f);
        yRt.sizeDelta = new Vector2(0f, 44f);
        var yImg = yesGo.AddComponent<Image>();
        yImg.color = new Color(0.15f, 0.55f, 0.75f, 1f);
        var yBtn = yesGo.AddComponent<Button>();
        yBtn.onClick.AddListener(OnConfirmReturnTitleYes);
        _returnTitleModalYesBtnText = MakeLabel(yesGo.transform, "Text", Vector2.zero, Vector2.zero, AdventureLocalizationManager.Get("confirm_title_yes"), 15, Color.white);
        _returnTitleModalYesBtnText.fontStyle = FontStyle.Bold;
        _returnTitleModalYesBtnText.alignment = TextAnchor.MiddleCenter;

        // キャンセル
        var noGo = new GameObject("NoButton");
        noGo.transform.SetParent(cardGo.transform, false);
        var nRt = noGo.AddComponent<RectTransform>();
        nRt.anchorMin = new Vector2(0.52f, 0f);
        nRt.anchorMax = new Vector2(0.9f, 0f);
        nRt.pivot = new Vector2(0.5f, 0f);
        nRt.anchoredPosition = new Vector2(0f, 22f);
        nRt.sizeDelta = new Vector2(0f, 44f);
        var nImg = noGo.AddComponent<Image>();
        nImg.color = new Color(0.2f, 0.3f, 0.45f, 1f);
        var nBtn = noGo.AddComponent<Button>();
        nBtn.onClick.AddListener(OnConfirmReturnTitleNo);
        _returnTitleModalNoBtnText = MakeLabel(noGo.transform, "Text", Vector2.zero, Vector2.zero, AdventureLocalizationManager.Get("confirm_title_no"), 15, Color.white);
        _returnTitleModalNoBtnText.fontStyle = FontStyle.Bold;
        _returnTitleModalNoBtnText.alignment = TextAnchor.MiddleCenter;

        _returnTitleConfirmModal.SetActive(false);
    }

    Text MakeLabel(Transform parent, string name, Vector2 pos, Vector2 size, string textStr, int fontSize, Color col)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        if (size == Vector2.zero)
        {
            // 親全体にピッタリ配置（ボタン内テキスト用）
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        else
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        var t = go.AddComponent<Text>();
        t.font = _font;
        t.fontSize = fontSize;
        t.color = col;
        t.text = textStr;
        t.raycastTarget = false;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        return t;
    }

    Font ResolveFont()
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
