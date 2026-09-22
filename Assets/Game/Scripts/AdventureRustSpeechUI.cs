using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Rust／Nikoのセリフを画面下部に確実表示する専用HUD。
/// IMGUI停止後も見えるよう、ScrapHUDと独立して DontDestroyOnLoad で常駐する。
/// </summary>
public class AdventureRustSpeechUI : MonoBehaviour
{
    static AdventureRustSpeechUI _instance;
    public static AdventureRustSpeechUI Instance => _instance;

    Canvas _canvas;
    CanvasGroup _cg;
    Image _bg;
    Image _accent;
    Text _speaker;
    Text _body;
    Font _font;

    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = Object.FindFirstObjectByType<AdventureRustSpeechUI>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }
        var go = new GameObject("AdventureRustSpeechUI");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AdventureRustSpeechUI>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        Build();
    }

    void Build()
    {
        _font = ResolveFont();

        var canvasGo = new GameObject("RustSpeech_Canvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 320; // クエスト／油／コンパスより前面
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("SpeechPanel");
        panel.transform.SetParent(canvasGo.transform, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 36f);
        rt.sizeDelta = new Vector2(820f, 128f);

        _bg = panel.AddComponent<Image>();
        _bg.color = new Color(0.02f, 0.05f, 0.10f, 0.94f);
        _bg.raycastTarget = false;

        var accentGo = new GameObject("Accent");
        accentGo.transform.SetParent(panel.transform, false);
        var aRt = accentGo.AddComponent<RectTransform>();
        aRt.anchorMin = new Vector2(0f, 0f);
        aRt.anchorMax = new Vector2(0f, 1f);
        aRt.pivot = new Vector2(0f, 0.5f);
        aRt.anchoredPosition = Vector2.zero;
        aRt.sizeDelta = new Vector2(5f, 0f);
        _accent = accentGo.AddComponent<Image>();
        _accent.color = new Color(0.35f, 0.92f, 0.98f, 1f);
        _accent.raycastTarget = false;

        _cg = panel.AddComponent<CanvasGroup>();
        _cg.alpha = 0f;
        _cg.blocksRaycasts = false;
        _cg.interactable = false;

        var nameGo = new GameObject("Speaker");
        nameGo.transform.SetParent(panel.transform, false);
        var nRt = nameGo.AddComponent<RectTransform>();
        nRt.anchorMin = new Vector2(0f, 1f);
        nRt.anchorMax = new Vector2(1f, 1f);
        nRt.pivot = new Vector2(0.5f, 1f);
        nRt.anchoredPosition = new Vector2(10f, -10f);
        nRt.sizeDelta = new Vector2(-30f, 30f);
        _speaker = nameGo.AddComponent<Text>();
        _speaker.font = _font;
        _speaker.fontSize = 18;
        _speaker.fontStyle = FontStyle.Bold;
        _speaker.alignment = TextAnchor.MiddleLeft;
        _speaker.color = new Color(0.4f, 0.95f, 1f, 1f);
        _speaker.raycastTarget = false;
        var nOut = nameGo.AddComponent<Outline>();
        nOut.effectColor = new Color(0f, 0f, 0f, 0.95f);
        nOut.effectDistance = new Vector2(1.4f, -1.4f);

        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(panel.transform, false);
        var bRt = bodyGo.AddComponent<RectTransform>();
        bRt.anchorMin = Vector2.zero;
        bRt.anchorMax = Vector2.one;
        bRt.offsetMin = new Vector2(20f, 12f);
        bRt.offsetMax = new Vector2(-16f, -40f);
        _body = bodyGo.AddComponent<Text>();
        _body.font = _font;
        _body.fontSize = 22;
        _body.alignment = TextAnchor.UpperLeft;
        _body.horizontalOverflow = HorizontalWrapMode.Wrap;
        _body.verticalOverflow = VerticalWrapMode.Overflow;
        _body.color = Color.white;
        _body.raycastTarget = false;
        var bOut = bodyGo.AddComponent<Outline>();
        bOut.effectColor = new Color(0f, 0f, 0f, 0.95f);
        bOut.effectDistance = new Vector2(1.5f, -1.5f);
    }

    void LateUpdate()
    {
        if (_cg == null || _body == null) return;

        bool hide = AdventureStoryFlow.HidesRustSpeech;

        var drone = AdventureRustDrone.Instance ?? Object.FindFirstObjectByType<AdventureRustDrone>();
        bool show = !hide && drone != null && drone.HasActiveSpeech;

        if (show)
        {
            _speaker.text = drone.ActiveSpeechSpeaker ?? "✦ 相棒 Rust";
            _speaker.color = drone.ActiveSpeechSpeakerColor;
            if (_accent != null) _accent.color = drone.ActiveSpeechSpeakerColor;
            _body.text = "「" + (drone.ActiveSpeechText ?? "") + "」";
            _cg.alpha = 1f; // 即座に表示（フェード待ちで見えない事故を防ぐ）
        }
        else
        {
            _cg.alpha = Mathf.MoveTowards(_cg.alpha, 0f, Time.unscaledDeltaTime * 4f);
        }
    }

    static Font ResolveFont()
    {
        string[] fonts = {
            "Hiragino Sans",
            "Hiragino Kaku Gothic ProN",
            "Arial Unicode MS",
            "YuGothic",
            "Apple SD Gothic Neo",
            "Arial"
        };
        foreach (var name in fonts)
        {
            var f = Font.CreateDynamicFontFromOSFont(name, 22);
            if (f != null) return f;
        }
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
               ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
