using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// 『Rust & Float』洗練されたポーズ＆設定メニュー。
/// 【ESCキー】で開閉し、ゲームを一時停止。
/// BGM音量・SE音量・マウス視点感度のリアルタイム調整、
/// 操作ガイドチートシート確認、および最初からやり直す（リスタート）機能を提供する。
/// </summary>
public class AdventurePauseMenu : MonoBehaviour
{
    public static AdventurePauseMenu Instance { get; private set; }

    public static bool IsOpen { get; private set; } = false;

    Canvas _canvas;
    CanvasGroup _canvasGroup;
    GameObject _panelRoot;
    GameObject _confirmModalRoot;
    Font _font;
    AudioSource _audioSource;
    AudioClip _testBeepClip;

    // スライダー UI
    Slider _bgmSlider;
    Text _bgmValueText;
    Slider _seSlider;
    Text _seValueText;
    Slider _sensSlider;
    Text _sensValueText;

    float _prevTimeScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        Ensure();
    }

    public static void Ensure()
    {
        if (Instance != null) return;
        var existing = Object.FindFirstObjectByType<AdventurePauseMenu>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject("AdventurePauseMenu");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AdventurePauseMenu>();
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

        // 澄んだ高音（E6: 1318Hz -> B6: 1975Hz のアルペジオチャイム）
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
        var kb = Keyboard.current;
        bool escPressed = false;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            escPressed = true;
        try { if (Input.GetKeyDown(KeyCode.Escape)) escPressed = true; } catch { }

        if (escPressed)
        {
            // 確認モーダルが開いている場合は先に確認モーダルを閉じる
            if (_confirmModalRoot != null && _confirmModalRoot.activeSelf)
            {
                _confirmModalRoot.SetActive(false);
                return;
            }

            TogglePause();
        }
    }

    public void TogglePause()
    {
        SetMenuVisible(!IsOpen);
    }

    public void SetMenuVisible(bool visible, bool instant = false)
    {
        IsOpen = visible;

        if (visible)
        {
            _prevTimeScale = Time.timeScale > 0.001f ? Time.timeScale : 1f;
            Time.timeScale = 0f;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            LoadCurrentSettings();
            if (_confirmModalRoot != null) _confirmModalRoot.SetActive(false);

            if (_panelRoot != null) _panelRoot.SetActive(true);
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }
        }
        else
        {
            Time.timeScale = _prevTimeScale > 0.001f ? _prevTimeScale : 1f;

            // オープニングやダイアログが開いていない限りカーソルをロック
            var opening = AdventureRustFloatOpening.Instance;
            bool isOpening = opening != null && opening.IsModalBoardOpen();
            if (!isOpening && !AdventureStoryFlow.WantsFreeCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (_panelRoot != null) _panelRoot.SetActive(false);
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
        }
    }

    void LoadCurrentSettings()
    {
        float bgm = AdventureMusicDirector.MasterBgmVolumeScale;
        if (_bgmSlider != null) _bgmSlider.SetValueWithoutNotify(bgm);
        if (_bgmValueText != null) _bgmValueText.text = $"{Mathf.RoundToInt(bgm * 100f)}%";

        float se = PlayerPrefs.GetFloat("Adventure_SeVolume", 1.0f);
        if (_seSlider != null) _seSlider.SetValueWithoutNotify(se);
        if (_seValueText != null) _seValueText.text = $"{Mathf.RoundToInt(se * 100f)}%";

        float sens = AdventureCameraFollow.MasterSensitivity;
        if (_sensSlider != null) _sensSlider.SetValueWithoutNotify(sens);
        if (_sensValueText != null) _sensValueText.text = $"{sens:F2}";
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

        // スライダー操作時に音量確認用SEを再生
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
            AdventureSaveManager.Instance.ResetAllForNewGame();
        }
    }

    void OnConfirmRestartNo()
    {
        if (_confirmModalRoot != null)
            _confirmModalRoot.SetActive(false);
    }

    void CreateUI()
    {
        var canvasGo = new GameObject("PauseMenu_Canvas");
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 110; // ScrapHUD(95)より前面

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
        _canvasGroup = canvasGo.AddComponent<CanvasGroup>();

        _font = ResolveFont();

        // 1. 全画面ディム背景
        var dimGo = new GameObject("DimOverlay");
        dimGo.transform.SetParent(canvasGo.transform, false);
        var dimRt = dimGo.AddComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.sizeDelta = Vector2.zero;
        var dimImg = dimGo.AddComponent<Image>();
        dimImg.color = new Color(0.015f, 0.03f, 0.07f, 0.88f);

        // 2. メインダイアログパネル（中央配置・幅860, 高さ540）
        _panelRoot = new GameObject("MainDialogPanel");
        _panelRoot.transform.SetParent(canvasGo.transform, false);
        var pRt = _panelRoot.AddComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0.5f, 0.5f);
        pRt.anchorMax = new Vector2(0.5f, 0.5f);
        pRt.pivot = new Vector2(0.5f, 0.5f);
        pRt.sizeDelta = new Vector2(880f, 550f);
        pRt.anchoredPosition = Vector2.zero;

        var pBg = _panelRoot.AddComponent<Image>();
        pBg.color = new Color(0.035f, 0.065f, 0.12f, 0.98f);

        var pOutline = _panelRoot.AddComponent<Outline>();
        pOutline.effectColor = new Color(0.25f, 0.75f, 0.95f, 0.45f);
        pOutline.effectDistance = new Vector2(2f, -2f);

        // 上部サイバーグラデーションアクセントバー
        var barGo = new GameObject("TopAccentBar");
        barGo.transform.SetParent(_panelRoot.transform, false);
        var bRt = barGo.AddComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0f, 1f);
        bRt.anchorMax = new Vector2(1f, 1f);
        bRt.pivot = new Vector2(0.5f, 1f);
        bRt.sizeDelta = new Vector2(0f, 4f);
        bRt.anchoredPosition = Vector2.zero;
        var bImg = barGo.AddComponent<Image>();
        bImg.color = new Color(0.35f, 0.88f, 1f, 1f);

        // ヘッダータイトル
        var titleGo = new GameObject("TitleText");
        titleGo.transform.SetParent(_panelRoot.transform, false);
        var tRt = titleGo.AddComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -22f);
        tRt.sizeDelta = new Vector2(0f, 38f);
        var titleText = titleGo.AddComponent<Text>();
        titleText.font = _font;
        titleText.fontSize = 24;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(1f, 0.90f, 0.45f, 1f);
        titleText.text = "✦ PAUSE & SETTINGS ✦";

        var subTitleGo = new GameObject("SubTitleText");
        subTitleGo.transform.SetParent(_panelRoot.transform, false);
        var stRt = subTitleGo.AddComponent<RectTransform>();
        stRt.anchorMin = new Vector2(0f, 1f);
        stRt.anchorMax = new Vector2(1f, 1f);
        stRt.pivot = new Vector2(0.5f, 1f);
        stRt.anchoredPosition = new Vector2(0f, -54f);
        stRt.sizeDelta = new Vector2(0f, 24f);
        var subTitleText = subTitleGo.AddComponent<Text>();
        subTitleText.font = _font;
        subTitleText.fontSize = 13;
        subTitleText.alignment = TextAnchor.MiddleCenter;
        subTitleText.color = new Color(0.65f, 0.85f, 1f, 0.85f);
        subTitleText.text = "ゲーム設定・操作ガイド確認・進行リセット";

        // 3. 2カラム領域（左: 設定, 右: 操作ガイド）
        var contentGo = new GameObject("ContentArea");
        contentGo.transform.SetParent(_panelRoot.transform, false);
        var cRt = contentGo.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0f, 0f);
        cRt.anchorMax = new Vector2(1f, 1f);
        cRt.offsetMin = new Vector2(30f, 85f);
        cRt.offsetMax = new Vector2(-30f, -85f);

        CreateSettingsColumn(contentGo.transform);
        CreateGuideColumn(contentGo.transform);

        // 4. フッターボタンバー（「ゲームに戻る」「最初からやり直す」）
        CreateFooterButtons(_panelRoot.transform);

        // 5. リスタート確認モーダル
        CreateRestartConfirmModal(canvasGo.transform);
    }

    void CreateSettingsColumn(Transform parent)
    {
        var colGo = new GameObject("LeftSettingsColumn");
        colGo.transform.SetParent(parent, false);
        var colRt = colGo.AddComponent<RectTransform>();
        colRt.anchorMin = new Vector2(0f, 0f);
        colRt.anchorMax = new Vector2(0.48f, 1f);
        colRt.offsetMin = Vector2.zero;
        colRt.offsetMax = Vector2.zero;

        var headerGo = new GameObject("SettingsHeader");
        headerGo.transform.SetParent(colGo.transform, false);
        var hRt = headerGo.AddComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0f, 1f);
        hRt.anchorMax = new Vector2(1f, 1f);
        hRt.pivot = new Vector2(0f, 1f);
        hRt.anchoredPosition = new Vector2(10f, 0f);
        hRt.sizeDelta = new Vector2(0f, 26f);
        var hText = headerGo.AddComponent<Text>();
        hText.font = _font;
        hText.fontSize = 16;
        hText.fontStyle = FontStyle.Bold;
        hText.color = new Color(0.4f, 0.9f, 1f, 1f);
        hText.text = "⚙ 音量 ＆ 操作設定";

        // BGM スライダー
        CreateSettingSlider(colGo.transform, "BGM 音量", -45f, 0f, 1f, out _bgmSlider, out _bgmValueText, OnBgmSliderChanged);

        // SE スライダー
        CreateSettingSlider(colGo.transform, "SE 音量", -125f, 0f, 1f, out _seSlider, out _seValueText, OnSeSliderChanged);

        // マウス視点感度 スライダー
        CreateSettingSlider(colGo.transform, "マウス視点感度", -205f, 0.05f, 0.60f, out _sensSlider, out _sensValueText, OnSensitivitySliderChanged);
    }

    void CreateSettingSlider(Transform parent, string label, float topOffset, float minVal, float maxVal, out Slider slider, out Text valueText, UnityEngine.Events.UnityAction<float> onValueChanged)
    {
        var rowGo = new GameObject(label + "_Row");
        rowGo.transform.SetParent(parent, false);
        var rRt = rowGo.AddComponent<RectTransform>();
        rRt.anchorMin = new Vector2(0f, 1f);
        rRt.anchorMax = new Vector2(1f, 1f);
        rRt.pivot = new Vector2(0f, 1f);
        rRt.anchoredPosition = new Vector2(10f, topOffset);
        rRt.sizeDelta = new Vector2(-20f, 65f);

        // 背景プレート
        var rowBg = rowGo.AddComponent<Image>();
        rowBg.color = new Color(0.02f, 0.04f, 0.08f, 0.6f);

        // ラベル
        var lGo = new GameObject("Label");
        lGo.transform.SetParent(rowGo.transform, false);
        var lRt = lGo.AddComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0f, 1f);
        lRt.anchorMax = new Vector2(0.7f, 1f);
        lRt.pivot = new Vector2(0f, 1f);
        lRt.anchoredPosition = new Vector2(12f, -8f);
        lRt.sizeDelta = new Vector2(0f, 22f);
        var lText = lGo.AddComponent<Text>();
        lText.font = _font;
        lText.fontSize = 14;
        lText.color = new Color(0.92f, 0.95f, 1f, 0.95f);
        lText.text = label;

        // 数値表示
        var vGo = new GameObject("ValueText");
        vGo.transform.SetParent(rowGo.transform, false);
        var vRt = vGo.AddComponent<RectTransform>();
        vRt.anchorMin = new Vector2(0.7f, 1f);
        vRt.anchorMax = new Vector2(1f, 1f);
        vRt.pivot = new Vector2(1f, 1f);
        vRt.anchoredPosition = new Vector2(-12f, -8f);
        vRt.sizeDelta = new Vector2(0f, 22f);
        valueText = vGo.AddComponent<Text>();
        valueText.font = _font;
        valueText.fontSize = 14;
        valueText.fontStyle = FontStyle.Bold;
        valueText.alignment = TextAnchor.MiddleRight;
        valueText.color = new Color(1f, 0.85f, 0.35f, 1f);
        valueText.text = "100%";

        // スライダーバー本体
        var sGo = new GameObject("Slider");
        sGo.transform.SetParent(rowGo.transform, false);
        var sRt = sGo.AddComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 0f);
        sRt.anchorMax = new Vector2(1f, 0f);
        sRt.pivot = new Vector2(0.5f, 0f);
        sRt.anchoredPosition = new Vector2(0f, 12f);
        sRt.sizeDelta = new Vector2(-24f, 20f);

        slider = sGo.AddComponent<Slider>();
        slider.minValue = minVal;
        slider.maxValue = maxVal;
        slider.onValueChanged.AddListener(onValueChanged);

        // 背景レール
        var bgRailGo = new GameObject("Background");
        bgRailGo.transform.SetParent(sGo.transform, false);
        var brRt = bgRailGo.AddComponent<RectTransform>();
        brRt.anchorMin = new Vector2(0f, 0.3f);
        brRt.anchorMax = new Vector2(1f, 0.7f);
        brRt.offsetMin = Vector2.zero;
        brRt.offsetMax = Vector2.zero;
        var brImg = bgRailGo.AddComponent<Image>();
        brImg.color = new Color(0.12f, 0.18f, 0.28f, 0.9f);

        // 塗りつぶし領域
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

        // つまみハンドル
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
        hRtObj.sizeDelta = new Vector2(20f, 20f);
        var hImg = handleGo.AddComponent<Image>();
        hImg.color = new Color(1f, 0.92f, 0.5f, 1f);
        slider.handleRect = hRtObj;
        slider.targetGraphic = hImg;
    }

    void CreateGuideColumn(Transform parent)
    {
        var colGo = new GameObject("RightGuideColumn");
        colGo.transform.SetParent(parent, false);
        var colRt = colGo.AddComponent<RectTransform>();
        colRt.anchorMin = new Vector2(0.52f, 0f);
        colRt.anchorMax = new Vector2(1f, 1f);
        colRt.offsetMin = Vector2.zero;
        colRt.offsetMax = Vector2.zero;

        var headerGo = new GameObject("GuideHeader");
        headerGo.transform.SetParent(colGo.transform, false);
        var hRt = headerGo.AddComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0f, 1f);
        hRt.anchorMax = new Vector2(1f, 1f);
        hRt.pivot = new Vector2(0f, 1f);
        hRt.anchoredPosition = new Vector2(10f, 0f);
        hRt.sizeDelta = new Vector2(0f, 26f);
        var hText = headerGo.AddComponent<Text>();
        hText.font = _font;
        hText.fontSize = 16;
        hText.fontStyle = FontStyle.Bold;
        hText.color = new Color(0.4f, 0.9f, 1f, 1f);
        hText.text = "📖 操作ガイド一覧";

        var cardGo = new GameObject("GuideCard");
        cardGo.transform.SetParent(colGo.transform, false);
        var cardRt = cardGo.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0f, 0f);
        cardRt.anchorMax = new Vector2(1f, 1f);
        cardRt.offsetMin = new Vector2(5f, 5f);
        cardRt.offsetMax = new Vector2(-5f, -32f);

        var cardBg = cardGo.AddComponent<Image>();
        cardBg.color = new Color(0.02f, 0.04f, 0.08f, 0.6f);

        // ガイドテキスト
        var guideTextGo = new GameObject("GuideText");
        guideTextGo.transform.SetParent(cardGo.transform, false);
        var gtRt = guideTextGo.AddComponent<RectTransform>();
        gtRt.anchorMin = Vector2.zero;
        gtRt.anchorMax = Vector2.one;
        gtRt.offsetMin = new Vector2(14f, 10f);
        gtRt.offsetMax = new Vector2(-14f, -10f);

        var gText = guideTextGo.AddComponent<Text>();
        gText.font = _font;
        gText.fontSize = 13;
        gText.lineSpacing = 1.35f;
        gText.supportRichText = true;
        gText.color = new Color(0.92f, 0.95f, 1f, 0.95f);
        gText.alignment = TextAnchor.MiddleLeft;

        gText.text =
            "<b>[W][A][S][D]</b>  移動 / 歩行\n" +
            "<b>[マウス移動]</b>  視点回転（左右360°/上下）\n" +
            "<b>[J]</b>  小ジャンプ（軽快・段差や小岩乗り越え）\n" +
            "<b>[Space]</b>  通常ジャンプ / <b>長押しで滑空</b>\n" +
            "<b>[W] / [S]</b>  ダイブ急降下 / 滞空ブレーキ（滑空中）\n" +
            "<b>[A] / [D]</b>  機首旋回バンク（滑空中）\n" +
            "<b>[E]</b>  相棒Rustへ手当て(注油) / <b>長押しで撫でる</b>\n" +
            "<b>[F]</b>  相棒Rustへ遠隔回収・調査指示\n" +
            "<b>[Tab]</b>  クエスト目標 ⇄ ミニマルHUD切替\n" +
            "<b>[F5]</b>  クイックセーブ / <b>[ESC]</b>  ポーズ切替";
    }

    void CreateFooterButtons(Transform panel)
    {
        var footerGo = new GameObject("FooterBar");
        footerGo.transform.SetParent(panel, false);
        var fRt = footerGo.AddComponent<RectTransform>();
        fRt.anchorMin = new Vector2(0f, 0f);
        fRt.anchorMax = new Vector2(1f, 0f);
        fRt.pivot = new Vector2(0.5f, 0f);
        fRt.anchoredPosition = new Vector2(0f, 20f);
        fRt.sizeDelta = new Vector2(-60f, 50f);

        // 1. 「ゲームに戻る」（Resume）ボタン（右寄り）
        var resumeGo = new GameObject("ResumeButton");
        resumeGo.transform.SetParent(footerGo.transform, false);
        var rRt = resumeGo.AddComponent<RectTransform>();
        rRt.anchorMin = new Vector2(0.55f, 0f);
        rRt.anchorMax = new Vector2(0.95f, 1f);
        rRt.offsetMin = Vector2.zero;
        rRt.offsetMax = Vector2.zero;

        var rImg = resumeGo.AddComponent<Image>();
        rImg.color = new Color(0.12f, 0.55f, 0.85f, 1f);
        var rBtn = resumeGo.AddComponent<Button>();
        var rColors = rBtn.colors;
        rColors.highlightedColor = new Color(0.25f, 0.75f, 1f, 1f);
        rColors.pressedColor = new Color(0.08f, 0.45f, 0.75f, 1f);
        rBtn.colors = rColors;
        rBtn.onClick.AddListener(() => SetMenuVisible(false));

        var rTextGo = new GameObject("Text");
        rTextGo.transform.SetParent(resumeGo.transform, false);
        var rtRt = rTextGo.AddComponent<RectTransform>();
        rtRt.anchorMin = Vector2.zero;
        rtRt.anchorMax = Vector2.one;
        var rtText = rTextGo.AddComponent<Text>();
        rtText.font = _font;
        rtText.fontSize = 17;
        rtText.fontStyle = FontStyle.Bold;
        rtText.alignment = TextAnchor.MiddleCenter;
        rtText.color = Color.white;
        rtText.text = "▶ ゲームに戻る [ESC]";

        // 2. 「最初からやり直す」（Restart）ボタン（左寄り）
        var restartGo = new GameObject("RestartButton");
        restartGo.transform.SetParent(footerGo.transform, false);
        var restRt = restartGo.AddComponent<RectTransform>();
        restRt.anchorMin = new Vector2(0.05f, 0f);
        restRt.anchorMax = new Vector2(0.45f, 1f);
        restRt.offsetMin = Vector2.zero;
        restRt.offsetMax = Vector2.zero;

        var restImg = restartGo.AddComponent<Image>();
        restImg.color = new Color(0.48f, 0.15f, 0.18f, 0.95f);
        var restBtn = restartGo.AddComponent<Button>();
        var restColors = restBtn.colors;
        restColors.highlightedColor = new Color(0.68f, 0.22f, 0.25f, 1f);
        restColors.pressedColor = new Color(0.35f, 0.10f, 0.12f, 1f);
        restBtn.colors = restColors;
        restBtn.onClick.AddListener(OnRestartClicked);

        var restTextGo = new GameObject("Text");
        restTextGo.transform.SetParent(restartGo.transform, false);
        var resttRt = restTextGo.AddComponent<RectTransform>();
        resttRt.anchorMin = Vector2.zero;
        resttRt.anchorMax = Vector2.one;
        var resttText = restTextGo.AddComponent<Text>();
        resttText.font = _font;
        resttText.fontSize = 16;
        resttText.fontStyle = FontStyle.Bold;
        resttText.alignment = TextAnchor.MiddleCenter;
        resttText.color = new Color(1f, 0.88f, 0.88f, 1f);
        resttText.text = "🗑 最初からやり直す";
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

        // 中央確認カード
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

        // タイトル
        var tGo = new GameObject("Title");
        tGo.transform.SetParent(cardGo.transform, false);
        var tRt = tGo.AddComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -24f);
        tRt.sizeDelta = new Vector2(0f, 32f);
        var tText = tGo.AddComponent<Text>();
        tText.font = _font;
        tText.fontSize = 20;
        tText.fontStyle = FontStyle.Bold;
        tText.alignment = TextAnchor.MiddleCenter;
        tText.color = new Color(1f, 0.45f, 0.45f, 1f);
        tText.text = "⚠️ 冒険の初期化確認";

        // 説明文
        var descGo = new GameObject("Desc");
        descGo.transform.SetParent(cardGo.transform, false);
        var dRt = descGo.AddComponent<RectTransform>();
        dRt.anchorMin = new Vector2(0f, 1f);
        dRt.anchorMax = new Vector2(1f, 1f);
        dRt.pivot = new Vector2(0.5f, 1f);
        dRt.anchoredPosition = new Vector2(0f, -68f);
        dRt.sizeDelta = new Vector2(-40f, 75f);
        var dText = descGo.AddComponent<Text>();
        dText.font = _font;
        dText.fontSize = 14;
        dText.lineSpacing = 1.35f;
        dText.alignment = TextAnchor.MiddleCenter;
        dText.color = new Color(0.95f, 0.92f, 0.92f, 0.95f);
        dText.text = "セーブデータと全パーツ・ポイントを初期化し、\n西側白砂ビーチの座礁艇前から新規開始します。\nよろしいですか？";

        // ボタン領域
        // 1. 「はい、やり直す」
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
        var ytGo = new GameObject("Text");
        ytGo.transform.SetParent(yesGo.transform, false);
        var ytRt = ytGo.AddComponent<RectTransform>();
        ytRt.anchorMin = Vector2.zero;
        ytRt.anchorMax = Vector2.one;
        var ytText = ytGo.AddComponent<Text>();
        ytText.font = _font;
        ytText.fontSize = 15;
        ytText.fontStyle = FontStyle.Bold;
        ytText.alignment = TextAnchor.MiddleCenter;
        ytText.color = Color.white;
        ytText.text = "はい、初期化する";

        // 2. 「キャンセル」
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
        var ntGo = new GameObject("Text");
        ntGo.transform.SetParent(noGo.transform, false);
        var ntRt = ntGo.AddComponent<RectTransform>();
        ntRt.anchorMin = Vector2.zero;
        ntRt.anchorMax = Vector2.one;
        var ntText = ntGo.AddComponent<Text>();
        ntText.font = _font;
        ntText.fontSize = 15;
        ntText.fontStyle = FontStyle.Bold;
        ntText.alignment = TextAnchor.MiddleCenter;
        ntText.color = Color.white;
        ntText.text = "キャンセル";

        _confirmModalRoot.SetActive(false);
    }

    Font ResolveFont()
    {
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f != null) return f;
        f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (f != null) return f;
        return Font.CreateDynamicFontFromOSFont("Hiragino Sans", 14);
    }
}
