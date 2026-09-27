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
        panelImg.sprite = CreateRoundedRectSprite(24, 6);
        panelImg.type = Image.Type.Sliced;
        panelImg.color = new Color(0.06f, 0.10f, 0.16f, 0.88f); // 落ち着いた深いブルーグレー半透明

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
        textRect.offsetMin = new Vector2(24f, 6f);
        textRect.offsetMax = new Vector2(-24f, -6f);

        _messageText = textGo.AddComponent<Text>();
        _messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                         ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        _messageText.fontSize = 19;
        _messageText.alignment = TextAnchor.MiddleCenter;
        _messageText.supportRichText = true;
        _messageText.color = new Color(1f, 1f, 1f, 0.95f);
    }

    static Sprite CreateRoundedRectSprite(int size, int radius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color fill = Color.white;
        Color clear = new Color(1f, 1f, 1f, 0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int dx = Mathf.Min(x, size - 1 - x);
                int dy = Mathf.Min(y, size - 1 - y);

                if (dx < radius && dy < radius)
                {
                    float dist = Vector2.Distance(new Vector2(dx, dy), new Vector2(radius, radius));
                    if (dist > radius)
                        tex.SetPixel(x, y, clear);
                    else
                    {
                        float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                        tex.SetPixel(x, y, new Color(fill.r, fill.g, fill.b, alpha));
                    }
                }
                else
                {
                    tex.SetPixel(x, y, fill);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    void ShowInternal(string message, float duration)
    {
        if (_messageText != null)
        {
            _messageText.text = message;
            // メッセージ長さに応じてパネル幅を程よくフィット（最小420px、最大680px）
            int charCount = message.Length;
            float targetWidth = Mathf.Clamp(charCount * 22f + 80f, 420f, 680f);
            var rt = _messageText.transform.parent as RectTransform;
            if (rt != null)
                rt.sizeDelta = new Vector2(targetWidth, 52f);
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
