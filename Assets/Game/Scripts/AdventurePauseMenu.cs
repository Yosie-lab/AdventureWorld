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
    Text _resumeBtnText;

    // タブ UI
    int _currentTab = 0;
    GameObject _tabAudioPanel;
    GameObject _tabDisplayPanel;
    GameObject _tabGuidePanel;

    Button _tabAudioBtn;
    Button _tabDisplayBtn;
    Button _tabGuideBtn;
    Image _tabAudioBtnImg;
    Image _tabDisplayBtnImg;
    Image _tabGuideBtnImg;
    Text _tabAudioBtnLabel;
    Text _tabDisplayBtnLabel;
    Text _tabGuideBtnLabel;
    GameObject _tabBarGo;

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
        {
            var all = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
            if (all.Length > 0)
                es = all[0];
        }

        if (es == null)
        {
            var go = new GameObject("EventSystem");
            Object.DontDestroyOnLoad(go);
            es = go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        }

        if (!es.gameObject.activeInHierarchy)
            es.gameObject.SetActive(true);

        var legacy = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        var uiModule = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        // Input System のみ構成では Standalone はマウスを拾えない → 常に InputSystemUIInputModule
        if (legacy != null)
            legacy.enabled = false;
        if (uiModule == null)
            uiModule = es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        try { uiModule.AssignDefaultActions(); } catch { }
        uiModule.enabled = true;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;

        // 複数 EventSystem があると UI クリックが死ぬ
        var systems = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] != null && systems[i] != es)
                systems[i].gameObject.SetActive(false);
        }
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

    void Start()
    {
        _font = ResolveFont();
        SetupAudio();
        LoadCurrentSettings();
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
        bool escPressed = AdventureInputReader.EscapeDown;
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
    }

    void TrySettingsDirectMouseClicks()
    {
        if (!IsOpen || IsSettingsModalBlockingInput()) return;
        if (!AdventureInputReader.MouseLeftDown) return;

        var mouse = Mouse.current;
        Vector2 mousePos = mouse != null ? mouse.position.ReadValue() : (Vector2)Input.mousePosition;

        if (TryClickUiButton(_tabDisplayBtn, mousePos)) { SwitchTab(1); return; }
        if (TryClickUiButton(_tabGuideBtn, mousePos)) { SwitchTab(2); return; }
        if (TryClickUiButton(_tabAudioBtn, mousePos)) { SwitchTab(0); return; }

        if (_currentTab == 1)
            TryDisplayTabDirectMouseClicks(mousePos);
    }

    void TryDisplayTabDirectMouseClicks(Vector2 mousePos)
    {
        if (TryClickUiButton(_fsBtn, mousePos)) { SetFullscreen(true); return; }
        if (TryClickUiButton(_winBtn, mousePos)) { SetFullscreen(false); return; }
        if (TryClickUiButton(_qFastBtn, mousePos)) { SetQuality(0); return; }
        if (TryClickUiButton(_qHighBtn, mousePos)) { SetQuality(1); return; }
        if (TryClickUiButton(_qUltraBtn, mousePos)) { SetQuality(2); return; }
        if (TryClickUiButton(_vsyncOnBtn, mousePos)) { SetVSync(true); return; }
        if (TryClickUiButton(_vsyncOffBtn, mousePos)) { SetVSync(false); return; }
        if (TryClickUiButton(_res1080Btn, mousePos)) { SetResolution(1920, 1080); return; }
        if (TryClickUiButton(_res900Btn, mousePos)) { SetResolution(1600, 900); return; }
        if (TryClickUiButton(_res720Btn, mousePos)) { SetResolution(1280, 720); return; }
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

            LoadCurrentSettings();
            SwitchTab(_currentTab);

            if (_confirmModalRoot != null) _confirmModalRoot.SetActive(false);
            if (_returnTitleConfirmModal != null) _returnTitleConfirmModal.SetActive(false);

            if (_openedFromTitle)
            {
                if (_headerTitleText != null) _headerTitleText.text = "✦ SETTINGS ✦";
                if (_headerSubTitleText != null) _headerSubTitleText.text = "音量・操作感度・画面表示の設定";
                if (_restartBtnGo != null) _restartBtnGo.SetActive(false);
                if (_returnTitleBtnGo != null) _returnTitleBtnGo.SetActive(false);
                if (_resumeBtnText != null) _resumeBtnText.text = "閉じる [ESC]";
            }
            else
            {
                if (_headerTitleText != null) _headerTitleText.text = "✦ PAUSE & SETTINGS ✦";
                if (_headerSubTitleText != null) _headerSubTitleText.text = "ゲーム設定・画面表示・操作ガイド・タイトル移動";
                if (_restartBtnGo != null) _restartBtnGo.SetActive(true);
                if (_returnTitleBtnGo != null) _returnTitleBtnGo.SetActive(true);
                if (_resumeBtnText != null) _resumeBtnText.text = "▶ ゲームに戻る [ESC]";
            }

            if (_canvas != null)
            {
                _canvas.enabled = true;
                _canvas.sortingOrder = _openedFromTitle ? 960 : 110;
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

            if (!AdventureStoryFlow.WantsFreeCursor && !wasFromTitle && AdventureRustFloatOpening.IsGameStarted)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
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
        if (!IsOpen) return;

        Event e = Event.current;
        if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            SetMenuVisible(false);
            e.Use();
            return;
        }

        if (!_openedFromTitle && AdventureRustFloatOpening.IsGameStarted) return;

        var closeRect = new Rect(Screen.width * 0.5f - 110f, Screen.height - 58f, 220f, 42f);
        if (GUI.Button(closeRect, "閉じる [ESC]"))
            SetMenuVisible(false);
    }

    void SwitchTab(int tabIndex)
    {
        _currentTab = tabIndex;

        if (_tabAudioPanel != null) _tabAudioPanel.SetActive(tabIndex == 0);
        if (_tabDisplayPanel != null) _tabDisplayPanel.SetActive(tabIndex == 1);
        if (_tabGuidePanel != null) _tabGuidePanel.SetActive(tabIndex == 2);

        Color activeBg = new Color(0.12f, 0.55f, 0.75f, 0.95f);
        Color inactiveBg = new Color(0.04f, 0.08f, 0.14f, 0.70f);

        if (_tabAudioBtnImg != null) _tabAudioBtnImg.color = (tabIndex == 0) ? activeBg : inactiveBg;
        if (_tabDisplayBtnImg != null) _tabDisplayBtnImg.color = (tabIndex == 1) ? activeBg : inactiveBg;
        if (_tabGuideBtnImg != null) _tabGuideBtnImg.color = (tabIndex == 2) ? activeBg : inactiveBg;

        if (_tabAudioBtnLabel != null) _tabAudioBtnLabel.color = (tabIndex == 0) ? Color.white : new Color(0.7f, 0.85f, 0.95f, 0.75f);
        if (_tabDisplayBtnLabel != null) _tabDisplayBtnLabel.color = (tabIndex == 1) ? Color.white : new Color(0.7f, 0.85f, 0.95f, 0.75f);
        if (_tabGuideBtnLabel != null) _tabGuideBtnLabel.color = (tabIndex == 2) ? Color.white : new Color(0.7f, 0.85f, 0.95f, 0.75f);

        if (tabIndex == 1)
            UpdateDisplayHighlights();
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

        if (_displayStatusText != null)
            _displayStatusText.text = "現在: " + AdventureDisplaySettings.DescribeCurrent();
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
        _canvas.sortingOrder = 110;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

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
        bdImg.raycastTarget = true;
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

        // 2. タブバー（3タブ）
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

        var tabBarCanvas = barGo.AddComponent<Canvas>();
        tabBarCanvas.overrideSorting = true;
        tabBarCanvas.sortingOrder = 8;
        barGo.AddComponent<GraphicRaycaster>();

        float tabW = 270f;
        _tabAudioBtn = CreateTabButton(barGo.transform, "TabAudio", new Vector2(-280f, 0f), new Vector2(tabW, 36f), "⚙  オーディオ・操作", () => SwitchTab(0), out _tabAudioBtnImg, out _tabAudioBtnLabel);
        _tabDisplayBtn = CreateTabButton(barGo.transform, "TabDisplay", new Vector2(0f, 0f), new Vector2(tabW, 36f), "🖥  画面・グラフィック", () => SwitchTab(1), out _tabDisplayBtnImg, out _tabDisplayBtnLabel);
        _tabGuideBtn = CreateTabButton(barGo.transform, "TabGuide", new Vector2(280f, 0f), new Vector2(tabW, 36f), "📖  操作ガイド一覧", () => SwitchTab(2), out _tabGuideBtnImg, out _tabGuideBtnLabel);
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

        // BGM スライダー
        CreateSettingSlider(_tabAudioPanel.transform, "BGM 音量", -15f, 0f, 1f, out _bgmSlider, out _bgmValueText, OnBgmSliderChanged);

        // SE スライダー
        CreateSettingSlider(_tabAudioPanel.transform, "SE 音量", -95f, 0f, 1f, out _seSlider, out _seValueText, OnSeSliderChanged);

        // マウス視点感度 スライダー
        CreateSettingSlider(_tabAudioPanel.transform, "マウス視点感度", -175f, 0.05f, 0.60f, out _sensSlider, out _sensValueText, OnSensitivitySliderChanged);

        // ガイドヒント
        var hint = MakeLabel(_tabAudioPanel.transform, "AudioHint", new Vector2(12f, -265f), new Vector2(800f, 30f),
            "✦ スライダーを動かすとリアルタイムに音量と感度が反映されます。SE音量はテストチャイムで確認できます。", 12, new Color(0.6f, 0.8f, 0.95f, 0.8f));
        hint.alignment = TextAnchor.MiddleLeft;
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
        CreateDisplayRow(_tabDisplayPanel.transform, "画面モード", -10f, rowH, out var row1);
        _fsBtn = CreateOptionButton(row1, "FsBtn", new Vector2(240f, 0f), new Vector2(220f, 38f), "⛶  フルスクリーン", () => SetFullscreen(true), out _fsBtnImg);
        _winBtn = CreateOptionButton(row1, "WinBtn", new Vector2(480f, 0f), new Vector2(220f, 38f), "🗖  ウィンドウ", () => SetFullscreen(false), out _winBtnImg);

        // 2. グラフィックス品質
        CreateDisplayRow(_tabDisplayPanel.transform, "グラフィック品質", -10f - rowH - 12f, rowH, out var row2);
        _qUltraBtn = CreateOptionButton(row2, "QUltra", new Vector2(240f, 0f), new Vector2(160f, 38f), "✦ 最高 (Ultra)", () => SetQuality(2), out _qUltraBtnImg);
        _qHighBtn = CreateOptionButton(row2, "QHigh", new Vector2(420f, 0f), new Vector2(160f, 38f), "✦ 標準 (High)", () => SetQuality(1), out _qHighBtnImg);
        _qFastBtn = CreateOptionButton(row2, "QFast", new Vector2(600f, 0f), new Vector2(160f, 38f), "✦ 軽量 (Fast)", () => SetQuality(0), out _qFastBtnImg);

        // 3. 垂直同期 (VSync)
        CreateDisplayRow(_tabDisplayPanel.transform, "垂直同期 (VSync)", -10f - (rowH + 12f) * 2, rowH, out var row3);
        _vsyncOnBtn = CreateOptionButton(row3, "VSyncOn", new Vector2(240f, 0f), new Vector2(220f, 38f), "✓ 垂直同期 ON (安定)", () => SetVSync(true), out _vsyncOnBtnImg);
        _vsyncOffBtn = CreateOptionButton(row3, "VSyncOff", new Vector2(480f, 0f), new Vector2(220f, 38f), "✕ 垂直同期 OFF (低遅延)", () => SetVSync(false), out _vsyncOffBtnImg);

        // 4. 画面解像度
        CreateDisplayRow(_tabDisplayPanel.transform, "画面解像度", -10f - (rowH + 12f) * 3, rowH, out var row4);
        _res1080Btn = CreateOptionButton(row4, "Res1080", new Vector2(240f, 0f), new Vector2(160f, 38f), "1920 × 1080", () => SetResolution(1920, 1080), out _res1080BtnImg);
        _res900Btn = CreateOptionButton(row4, "Res900", new Vector2(420f, 0f), new Vector2(160f, 38f), "1600 × 900", () => SetResolution(1600, 900), out _res900BtnImg);
        _res720Btn = CreateOptionButton(row4, "Res720", new Vector2(600f, 0f), new Vector2(160f, 38f), "1280 × 720", () => SetResolution(1280, 720), out _res720BtnImg);

        _displayStatusText = MakeLabel(_tabDisplayPanel.transform, "DisplayStatus", new Vector2(12f, 8f),
            new Vector2(820f, 22f), "", 12, new Color(0.55f, 0.82f, 0.95f, 0.9f));
        _displayStatusText.alignment = TextAnchor.LowerLeft;
    }

    void CreateDisplayRow(Transform parent, string title, float topY, float height, out Transform rowTransform)
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

        var l = MakeLabel(rowGo.transform, "Label", new Vector2(16f, 0f), new Vector2(200f, height), title, 14, new Color(0.92f, 0.96f, 1f, 1f));
        l.fontStyle = FontStyle.Bold;
        l.alignment = TextAnchor.MiddleLeft;

        rowTransform = rowGo.transform;
    }

    Button CreateOptionButton(Transform parent, string name, Vector2 pos, Vector2 size, string label, UnityEngine.Events.UnityAction onClick, out Image outImg)
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

        var text = MakeLabel(btnGo.transform, "Text", Vector2.zero, size, label, 13, Color.white);
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;

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

        // 2カラムで操作ガイドを分かりやすく表示
        var leftCol = MakeLabel(_tabGuidePanel.transform, "LeftCol", new Vector2(25f, -15f), new Vector2(380f, 320f), "", 13, new Color(0.95f, 0.97f, 1f, 0.95f));
        leftCol.lineSpacing = 1.35f;
        leftCol.alignment = TextAnchor.UpperLeft;
        leftCol.supportRichText = true;
        leftCol.text =
            "<b>[ 基本操作 ＆ アクション ]</b>\n\n" +
            "<b>[W][A][S][D]</b>  移動 / 歩行\n" +
            "<b>[Shift]</b>  ダッシュ走行（速度アップ）\n" +
            "<b>[マウス移動]</b>  視点回転（左右360°/上下）\n" +
            "<b>[Space]</b>  ジャンプ / <b>長押しで滑空展開</b>\n" +
            "<b>[J]</b>  小ジャンプ（軽快・段差乗り越え）\n" +
            "<b>[W] / [S]</b>  ダイブ急降下 / 滞空フレア（滑空中）\n" +
            "<b>[A] / [D]</b>  機首バンク旋回（滑空中）";

        var rightCol = MakeLabel(_tabGuidePanel.transform, "RightCol", new Vector2(430f, -15f), new Vector2(380f, 320f), "", 13, new Color(0.95f, 0.97f, 1f, 0.95f));
        rightCol.lineSpacing = 1.35f;
        rightCol.alignment = TextAnchor.UpperLeft;
        rightCol.supportRichText = true;
        rightCol.text =
            "<b>[ インタラクト ＆ システム ]</b>\n\n" +
            "<b>[E] / [左クリック]</b>  調べる / 注油 / 撫でる\n" +
            "<b>[Q] / [中クリック]</b>  <b>相棒Floatに道案内を頼む（最寄り目標へ先行フライト）</b>\n" +
            "<b>[F]</b>  遠隔パーツ回収 / 視線偵察指示\n" +
            "<b>[B]</b>  Rustの着せ替え工房を開く\n" +
            "<b>[Tab]</b>  クエスト目標 ⇄ ミニマルHUD切替\n" +
            "<b>[F5] / [Cmd+S]</b>  クイックセーブ\n" +
            "<b>[ESC]</b>  ポーズ ＆ 設定メニュー開閉\n\n" +
            "✦ コンパスHUDと相棒Floatが最寄りの風脈・パーツ・タワーを常時案内します。";
    }

    void CreateSettingSlider(Transform parent, string label, float topOffset, float minVal, float maxVal, out Slider slider, out Text valueText, UnityEngine.Events.UnityAction<float> onValueChanged)
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

        var lText = MakeLabel(rowGo.transform, "Label", new Vector2(16f, -10f), new Vector2(300f, 22f), label, 14, new Color(0.92f, 0.96f, 1f, 1f));
        lText.fontStyle = FontStyle.Bold;

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
        var restBtn = _restartBtnGo.AddComponent<Button>();
        restBtn.onClick.AddListener(OnRestartClicked);

        var resttText = MakeLabel(_restartBtnGo.transform, "Text", Vector2.zero, Vector2.zero, "🗑 最初からやり直す", 15, new Color(1f, 0.88f, 0.88f, 1f));
        resttText.fontStyle = FontStyle.Bold;
        resttText.alignment = TextAnchor.MiddleCenter;

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
        var titleBtn = _returnTitleBtnGo.AddComponent<Button>();
        titleBtn.onClick.AddListener(OnReturnToTitleClicked);

        var titleTextComp = MakeLabel(_returnTitleBtnGo.transform, "Text", Vector2.zero, Vector2.zero, "✦ タイトルへ戻る", 15, new Color(0.90f, 0.96f, 1f, 1f));
        titleTextComp.fontStyle = FontStyle.Bold;
        titleTextComp.alignment = TextAnchor.MiddleCenter;

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
        var rBtn = resumeGo.AddComponent<Button>();
        rBtn.onClick.AddListener(() => SetMenuVisible(false));

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

        var tText = MakeLabel(cardGo.transform, "Title", new Vector2(0f, -24f), new Vector2(480f, 32f), "⚠️ 冒険の初期化確認", 20, new Color(1f, 0.45f, 0.45f, 1f));
        tText.fontStyle = FontStyle.Bold;
        tText.alignment = TextAnchor.MiddleCenter;

        var dText = MakeLabel(cardGo.transform, "Desc", new Vector2(0f, -68f), new Vector2(440f, 75f),
            "セーブデータと全パーツ・ポイントを初期化し、\n西側白砂ビーチの座礁艇前から新規開始します。\nよろしいですか？", 14, new Color(0.95f, 0.92f, 0.92f, 0.95f));
        dText.lineSpacing = 1.35f;
        dText.alignment = TextAnchor.MiddleCenter;

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
        var ytText = MakeLabel(yesGo.transform, "Text", Vector2.zero, Vector2.zero, "はい、初期化する", 15, Color.white);
        ytText.fontStyle = FontStyle.Bold;
        ytText.alignment = TextAnchor.MiddleCenter;

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
        var ntText = MakeLabel(noGo.transform, "Text", Vector2.zero, Vector2.zero, "キャンセル", 15, Color.white);
        ntText.fontStyle = FontStyle.Bold;
        ntText.alignment = TextAnchor.MiddleCenter;

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

        var tText = MakeLabel(cardGo.transform, "Title", new Vector2(0f, -24f), new Vector2(480f, 32f), "✦ タイトル画面へ戻る", 20, new Color(0.45f, 0.90f, 1f, 1f));
        tText.fontStyle = FontStyle.Bold;
        tText.alignment = TextAnchor.MiddleCenter;

        var dText = MakeLabel(cardGo.transform, "Desc", new Vector2(0f, -68f), new Vector2(440f, 75f),
            "現在の冒険の進行状況をオートセーブして\nタイトル画面へ戻ります。\nよろしいですか？", 14, new Color(0.92f, 0.95f, 1f, 0.95f));
        dText.lineSpacing = 1.35f;
        dText.alignment = TextAnchor.MiddleCenter;

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
        var ytText = MakeLabel(yesGo.transform, "Text", Vector2.zero, Vector2.zero, "はい、保存して戻る", 15, Color.white);
        ytText.fontStyle = FontStyle.Bold;
        ytText.alignment = TextAnchor.MiddleCenter;

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
        var ntText = MakeLabel(noGo.transform, "Text", Vector2.zero, Vector2.zero, "キャンセル", 15, Color.white);
        ntText.fontStyle = FontStyle.Bold;
        ntText.alignment = TextAnchor.MiddleCenter;

        _returnTitleConfirmModal.SetActive(false);
    }

    Text MakeLabel(Transform parent, string name, Vector2 pos, Vector2 size, string textStr, int fontSize, Color col)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var t = go.AddComponent<Text>();
        t.font = _font;
        t.fontSize = fontSize;
        t.color = col;
        t.text = textStr;
        t.raycastTarget = false;
        return t;
    }

    Font ResolveFont()
    {
        string[] candidates =
        {
            "Hiragino Sans",
            "Hiragino Kaku Gothic ProN",
            "Hiragino Sans GB",
            "YuGothic",
            "Apple SD Gothic Neo",
            "Noto Sans CJK JP",
            "Arial Unicode MS"
        };
        foreach (string name in candidates)
        {
            try
            {
                Font os = Font.CreateDynamicFontFromOSFont(name, 20);
                if (os != null)
                    return os;
            }
            catch { }
        }

        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f != null) return f;
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
