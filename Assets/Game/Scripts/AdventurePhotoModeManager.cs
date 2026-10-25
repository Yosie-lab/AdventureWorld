using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ワンキーUI非表示・フォトモード統括マネージャー。
/// 【P】キーまたはゲームパッド【十字キー上】で全ゲームプレイHUDを完全非表示にし、
/// 絶景の大空滑空やRustとのツーショット写真を美しくスクリーンショット撮影できるようにする。
/// フォトモード中は【T】キーで時間停止（ポーズ）も可能。
/// </summary>
public class AdventurePhotoModeManager : MonoBehaviour
{
    private static AdventurePhotoModeManager _instance;
    public static AdventurePhotoModeManager Instance => _instance;

    private bool _isPhotoModeActive = false;
    public bool IsPhotoModeActive => _isPhotoModeActive;

    private bool _isTimeFrozen = false;
    public bool IsTimeFrozen => _isTimeFrozen;

    // 非表示前のCanvas有効状態を記録
    private readonly Dictionary<Canvas, bool> _savedCanvasStates = new Dictionary<Canvas, bool>();

    // ピル通知用UI
    private Canvas _toastCanvas;
    private CanvasGroup _toastCg;
    private Text _toastText;
    private float _toastTimer = 0f;
    private const float ToastDuration = 2.2f;

    // プロシージャルシャッター音
    private AudioSource _audioSource;
    private AudioClip _shutterClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInit()
    {
        Ensure();
    }

    public static AdventurePhotoModeManager Ensure()
    {
        if (_instance != null) return _instance;
        _instance = FindAnyObjectByType<AdventurePhotoModeManager>();
        if (_instance != null) return _instance;

        var go = new GameObject("AdventurePhotoModeManager");
        _instance = go.AddComponent<AdventurePhotoModeManager>();
        DontDestroyOnLoad(go);
        return _instance;
    }

    private void Awake()
    {
        if (_instance == null) _instance = this;
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        CreateToastUI();
        CreateProceduralShutterAudio();
    }

    private void Update()
    {
        // ポーズメニュー中、またはタイトル画面中はフォトモード入力を抑制
        if (AdventurePauseMenu.IsOpen) return;
        if (!AdventureRustFloatOpening.IsGameStarted) return;
        var opening = AdventureRustFloatOpening.Instance;
        if (opening != null && opening.IsModalBoardOpen()) return;

        // 1. フォトモード トグル入力（Pキー / 十字キー上）
        if (AdventureInputReader.PhotoModeDown)
        {
            TogglePhotoMode();
        }

        // 2. フォトモード中の時間停止トグル（Tキー / L3押し込み）
        if (_isPhotoModeActive && AdventureInputReader.PhotoModeFreezeDown)
        {
            ToggleTimeFreeze();
        }

        // 3. トースト通知の自動フェードアウト
        if (_toastTimer > 0f)
        {
            _toastTimer -= Time.unscaledDeltaTime;
            if (_toastCg != null)
            {
                if (_toastTimer < 0.6f)
                {
                    _toastCg.alpha = Mathf.Clamp01(_toastTimer / 0.6f);
                }
                else
                {
                    _toastCg.alpha = 1f;
                }
            }
        }
    }

    /// <summary>フォトモードの切り替え</summary>
    public void TogglePhotoMode()
    {
        _isPhotoModeActive = !_isPhotoModeActive;

        if (_isPhotoModeActive)
        {
            EnterPhotoMode();
        }
        else
        {
            ExitPhotoMode();
        }

        PlayShutterJuice();
    }

    private void EnterPhotoMode()
    {
        _savedCanvasStates.Clear();

        // 画面上の全Canvasを探索
        var allCanvases = Resources.FindObjectsOfTypeAll<Canvas>();
        foreach (var c in allCanvases)
        {
            if (c == null || !c.gameObject.scene.isLoaded) continue;

            string name = c.gameObject.name;

            // 除外リスト: Rustの顔LED、フォトモード通知、ポーズメニュー自身
            if (name == "RustFaceCanvas" || name == "PhotoModeNotificationCanvas" || name == "PauseMenu_Canvas")
                continue;

            // 既に非アクティブなオブジェクトは除外
            if (!c.gameObject.activeInHierarchy) continue;

            _savedCanvasStates[c] = c.enabled;
            c.enabled = false;
        }

        ShowToast(AdventureLocalizationManager.Get("photomode_toast_on"));
    }

    private void ExitPhotoMode()
    {
        // 時間停止していた場合は解除
        if (_isTimeFrozen)
        {
            _isTimeFrozen = false;
            Time.timeScale = 1f;
        }

        // Canvasの有効状態を復元
        foreach (var kvp in _savedCanvasStates)
        {
            if (kvp.Key != null)
            {
                kvp.Key.enabled = kvp.Value;
            }
        }
        _savedCanvasStates.Clear();

        ShowToast(AdventureLocalizationManager.Get("photomode_toast_off"));
    }

    /// <summary>フォトモード中の時間停止（スロー/ポーズ）切り替え</summary>
    public void ToggleTimeFreeze()
    {
        _isTimeFrozen = !_isTimeFrozen;
        Time.timeScale = _isTimeFrozen ? 0f : 1f;

        string msg = _isTimeFrozen 
            ? AdventureLocalizationManager.Get("photomode_toast_freeze") 
            : AdventureLocalizationManager.Get("photomode_toast_resume");

        ShowToast(msg);
        AdventureInputHaptics.PlayPulse(0.2f, 0.3f, 0.05f);
    }

    private void ShowToast(string message)
    {
        if (_toastText != null)
        {
            _toastText.text = message;
        }
        if (_toastCg != null)
        {
            _toastCg.alpha = 1f;
        }
        _toastTimer = ToastDuration;
    }

    private void PlayShutterJuice()
    {
        if (_audioSource != null && _shutterClip != null)
        {
            _audioSource.pitch = Random.Range(0.96f, 1.04f);
            _audioSource.PlayOneShot(_shutterClip, 0.45f);
        }

        AdventureInputHaptics.PlayPulse(0.35f, 0.45f, 0.08f);
    }

    private void CreateToastUI()
    {
        var canvasGo = new GameObject("PhotoModeNotificationCanvas");
        canvasGo.transform.SetParent(transform, false);

        _toastCanvas = canvasGo.AddComponent<Canvas>();
        _toastCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _toastCanvas.sortingOrder = 9999; // 最前面

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var panelGo = new GameObject("ToastPill");
        panelGo.transform.SetParent(canvasGo.transform, false);

        var rt = panelGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -36f);
        rt.sizeDelta = new Vector2(580f, 48f);

        var bg = panelGo.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.12f, 0.16f, 0.82f);

        _toastCg = panelGo.AddComponent<CanvasGroup>();
        _toastCg.alpha = 0f;
        _toastCg.blocksRaycasts = false;
        _toastCg.interactable = false;

        var textGo = new GameObject("ToastText");
        textGo.transform.SetParent(panelGo.transform, false);

        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        _toastText = textGo.AddComponent<Text>();
        _toastText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.FindObjectsOfTypeAll<Font>()[0];
        _toastText.fontSize = 20;
        _toastText.alignment = TextAnchor.MiddleCenter;
        _toastText.color = new Color(0.92f, 0.97f, 1.0f, 0.95f);
        _toastText.text = "";
    }

    private void CreateProceduralShutterAudio()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;

        // 心地よいプロシージャルシャッター音（カシャッ！）
        int sampleRate = 44100;
        float duration = 0.16f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            // 2回の精密なクリック音（ミラーアップ＋シャッター幕走行）
            float click1 = Mathf.Sin(2f * Mathf.PI * 1800f * t) * Mathf.Exp(-t * 85f);
            float click2 = t > 0.055f ? Mathf.Sin(2f * Mathf.PI * 1350f * (t - 0.055f)) * Mathf.Exp(-(t - 0.055f) * 75f) * 0.9f : 0f;
            float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 60f) * 0.25f;

            samples[i] = Mathf.Clamp(click1 + click2 + noise, -1f, 1f);
        }

        _shutterClip = AudioClip.Create("Camera_Shutter", sampleCount, 1, sampleRate, false);
        _shutterClip.SetData(samples, 0);
    }

    private void OnDestroy()
    {
        if (_isTimeFrozen)
        {
            Time.timeScale = 1f;
        }
    }
}
