using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面下部のテキストのみ字幕（中央パネル・全画面暗幕なし）。漂着箱・砂浜読み物・ロア通知向け。
/// </summary>
public class AdventureSceneCaption : MonoBehaviour
{
    static AdventureSceneCaption _instance;
    public static bool IsVisible => _instance != null && _instance._visible;

    CanvasGroup _cg;
    Text _titleText;
    Text _metaText;
    Text _bodyText;
    bool _visible;
    float _timer = -1f;
    bool _waitDismiss;
    float _ignoreDismissUntil;
    Action _onDismiss;

    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = FindAnyObjectByType<AdventureSceneCaption>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }
        var go = new GameObject("AdventureSceneCaption");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AdventureSceneCaption>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        BuildUi();
    }

    void BuildUi()
    {
        var canvasGo = new GameObject("CaptionCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 340;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        var root = new GameObject("CaptionRoot");
        root.transform.SetParent(canvasGo.transform, false);
        var rt = root.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 96f);
        rt.sizeDelta = new Vector2(920f, 120f);

        _cg = root.AddComponent<CanvasGroup>();
        _cg.alpha = 0f;
        _cg.blocksRaycasts = false;
        _cg.interactable = false;

        Font font = ResolveFont();

        _titleText = MakeLine(root.transform, "Title", new Vector2(0f, 88f), 16, FontStyle.Bold, new Color(1f, 0.88f, 0.42f, 1f), font);
        _metaText = MakeLine(root.transform, "Meta", new Vector2(0f, 68f), 12, FontStyle.Italic, new Color(0.55f, 0.88f, 1f, 0.95f), font);
        _bodyText = MakeLine(root.transform, "Body", new Vector2(0f, 8f), 15, FontStyle.Normal, new Color(0.96f, 0.98f, 1f, 0.98f), font);
        _bodyText.alignment = TextAnchor.LowerCenter;
        var bodyRt = _bodyText.rectTransform;
        bodyRt.sizeDelta = new Vector2(900f, 72f);
    }

    static Text MakeLine(Transform parent, string name, Vector2 pos, int size, FontStyle style, Color color, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(900f, 22f);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.color = color;
        t.raycastTarget = false;
        var o = go.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.92f);
        o.effectDistance = new Vector2(1.2f, -1.2f);
        return t;
    }

    /// <param name="autoSeconds">0 以下で操作 dismiss のみ</param>
    public static void Show(string title, string body, string meta = null, float autoSeconds = 8f, bool waitDismiss = true, Action onDismiss = null)
    {
        Ensure();
        if (_instance == null) return;

        title = string.IsNullOrWhiteSpace(title) ? "" : title.Trim();
        body = string.IsNullOrWhiteSpace(body) ? "" : body.Trim();
        meta = string.IsNullOrWhiteSpace(meta) ? "" : meta.Trim();

        if (title.Length == 0 && body.Length == 0)
        {
            Dismiss();
            return;
        }

        Font f = ResolveFont();
        if (_instance._titleText != null)
        {
            _instance._titleText.font = f;
            _instance._titleText.text = title.Length > 0 ? title : "";
            _instance._titleText.gameObject.SetActive(title.Length > 0);
        }
        if (_instance._metaText != null)
        {
            _instance._metaText.font = f;
            _instance._metaText.text = meta;
            _instance._metaText.gameObject.SetActive(meta.Length > 0);
        }
        if (_instance._bodyText != null)
        {
            _instance._bodyText.font = f;
            _instance._bodyText.supportRichText = body.IndexOf('<') >= 0;
            _instance._bodyText.text = body;
            _instance._bodyText.gameObject.SetActive(body.Length > 0);
        }

        _instance._visible = true;
        _instance._waitDismiss = waitDismiss;
        _instance._timer = autoSeconds > 0f ? autoSeconds : -1f;
        _instance._ignoreDismissUntil = Time.unscaledTime + 0.35f;
        _instance._onDismiss = onDismiss;
        _instance._cg.alpha = 1f;
    }

    public static void ShowRich(string richText, float autoSeconds = 6f, Action onDismiss = null)
    {
        Ensure();
        Show("", richText, null, autoSeconds, waitDismiss: autoSeconds <= 0f, onDismiss);
    }

    public static void Dismiss(bool invokeCallback = true)
    {
        if (_instance == null) return;
        _instance._visible = false;
        _instance._timer = -1f;
        _instance._waitDismiss = false;
        if (_instance._cg != null) _instance._cg.alpha = 0f;
        var cb = invokeCallback ? _instance._onDismiss : null;
        _instance._onDismiss = null;
        cb?.Invoke();
    }

    void Update()
    {
        if (!_visible || _cg == null) return;

        if (_timer > 0f)
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer <= 0f)
            {
                Dismiss();
                return;
            }
        }

        if (_waitDismiss && Time.unscaledTime >= _ignoreDismissUntil && PollDismissInput())
            Dismiss();
    }

    static bool PollDismissInput()
    {
        if (AdventureInputReader.InteractDown) return true;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
            return true;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
        return false;
    }

    static Font _cachedFont;
    static Font ResolveFont()
    {
        if (_cachedFont != null) return _cachedFont;
        string[] fonts = {
            "PingFang SC", "Hiragino Sans GB", "Heiti SC", "Hiragino Sans",
            "Hiragino Kaku Gothic ProN", "Microsoft YaHei", "Arial Unicode MS", "Arial"
        };
        foreach (var name in fonts)
        {
            var f = Font.CreateDynamicFontFromOSFont(name, 15);
            if (f != null) { _cachedFont = f; return f; }
        }
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
               ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
