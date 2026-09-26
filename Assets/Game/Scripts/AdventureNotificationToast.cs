using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面上部に短時間メッセージ（時間帯変更、カメラ感度微調整など）を
/// 美しく表示してフェードアウトする軽量トーストUIマネージャー。
/// </summary>
public class AdventureNotificationToast : MonoBehaviour
{
    private static AdventureNotificationToast _instance;
    public static AdventureNotificationToast Instance => _instance;

    private CanvasGroup _canvasGroup;
    private Text _messageText;
    private float _displayTimer = 0f;
    private float _fadeDuration = 0.35f;

    public static void Show(string message, float duration = 2.2f)
    {
        Ensure();
        if (_instance != null)
        {
            _instance.ShowInternal(message, duration);
        }
    }

    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = Object.FindFirstObjectByType<AdventureNotificationToast>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("AdventureNotificationToast");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AdventureNotificationToast>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        BuildToastUI();
    }

    void BuildToastUI()
    {
        var canvasGo = new GameObject("ToastCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // 最前面表示

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // パネル背景（上部中央に配置）
        var panelGo = new GameObject("ToastPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);

        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -80f); // 画面上部から80px下
        panelRect.sizeDelta = new Vector2(560f, 52f);

        var panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.12f, 0.18f, 0.88f); // シックなダークブルー半透明

        _canvasGroup = panelGo.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        // メッセージテキスト
        var textGo = new GameObject("ToastText");
        textGo.transform.SetParent(panelGo.transform, false);

        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.offsetMin = new Vector2(16f, 4f);
        textRect.offsetMax = new Vector2(-16f, -4f);

        _messageText = textGo.AddComponent<Text>();
        _messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                         ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        _messageText.fontSize = 20;
        _messageText.alignment = TextAnchor.MiddleCenter;
        _messageText.supportRichText = true;
        _messageText.color = new Color(1f, 1f, 1f, 0.95f);
    }

    void ShowInternal(string message, float duration)
    {
        if (_messageText != null)
        {
            _messageText.text = message;
        }
        _displayTimer = duration;
    }

    void Update()
    {
        if (_canvasGroup == null) return;

        if (_displayTimer > 0f)
        {
            _displayTimer -= Time.unscaledDeltaTime;
            // 素早くフェードイン
            _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 1.0f, Time.unscaledDeltaTime / 0.15f);
        }
        else if (_canvasGroup.alpha > 0f)
        {
            // ゆっくりフェードアウト
            _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 0.0f, Time.unscaledDeltaTime / _fadeDuration);
        }
    }
}
