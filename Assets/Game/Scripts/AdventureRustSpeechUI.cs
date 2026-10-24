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
        var existing = Object.FindAnyObjectByType<AdventureRustSpeechUI>();
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
        rt.anchoredPosition = new Vector2(0f, AdventureSpeechBoardLayout.RustSpeechBottomY);
        rt.sizeDelta = new Vector2(AdventureSpeechBoardLayout.RustSpeechPanelW, AdventureSpeechBoardLayout.RustSpeechPanelH);

        _bg = panel.AddComponent<Image>();
        _bg.color = new Color(0.02f, 0.05f, 0.10f, AdventureSpeechBoardLayout.RustSpeechBgAlpha);
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
        _speaker.fontSize = AdventureSpeechBoardLayout.RustSpeechSpeakerSize;
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
        bRt.offsetMin = new Vector2(14f, 8f);
        bRt.offsetMax = new Vector2(-12f, -30f);
        _body = bodyGo.AddComponent<Text>();
        _body.font = _font;
        _body.fontSize = AdventureSpeechBoardLayout.RustSpeechBodySize;
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
        var opening = AdventureRustFloatOpening.Instance;
        if (opening != null && opening.IsModalBoardOpen())
            hide = true;

        var drone = AdventureRustDrone.Instance ?? Object.FindAnyObjectByType<AdventureRustDrone>();
        string rawSpeech = drone != null ? (drone.ActiveSpeechText ?? "") : "";
        string locSpeech = AdventureLocalizationManager.Get(rawSpeech);
        if (!string.IsNullOrEmpty(locSpeech) && locSpeech != rawSpeech) rawSpeech = locSpeech;

        bool hasValidSpeech = !string.IsNullOrWhiteSpace(rawSpeech) && rawSpeech != "「」" && rawSpeech != "\"\"" && rawSpeech != "«»";
        bool show = !hide && drone != null && drone.HasActiveSpeech && hasValidSpeech;

        if (show)
        {
            Font f = ResolveFont();
            if (_speaker != null && _speaker.font != f) _speaker.font = f;
            if (_body != null && _body.font != f) _body.font = f;

            string rawSpeaker = drone.ActiveSpeechSpeaker ?? "✦ 相棒 Rust";
            string speakerName = rawSpeaker;
            if (rawSpeaker.Contains("Rust") || rawSpeaker.Contains("相棒"))
            {
                speakerName = AdventureLocalizationManager.CurrentLanguage switch
                {
                    AdventureLocalizationManager.GameLanguage.English => "✦ Companion Rust",
                    AdventureLocalizationManager.GameLanguage.SimplifiedChinese => "✦ 伙伴 Rust",
                    AdventureLocalizationManager.GameLanguage.Spanish => "✦ Compañero Rust",
                    _ => "✦ 相棒 Rust"
                };
            }
            else if (rawSpeaker.Contains("Float") || rawSpeaker.Contains("Niko"))
            {
                speakerName = "✦ Float";
            }
            else if (rawSpeaker.Contains("カピタ") || rawSpeaker.Contains("Capyta"))
            {
                speakerName = AdventureLocalizationManager.CurrentLanguage switch
                {
                    AdventureLocalizationManager.GameLanguage.English => "🐾 Capyta",
                    AdventureLocalizationManager.GameLanguage.SimplifiedChinese => "🐾 水豚卡皮塔",
                    AdventureLocalizationManager.GameLanguage.Spanish => "🐾 Capibara Capyta",
                    _ => "🐾 カピタ"
                };
            }
            else
            {
                string locSpeaker = AdventureLocalizationManager.Get(rawSpeaker);
                if (!string.IsNullOrEmpty(locSpeaker) && locSpeaker != rawSpeaker)
                    speakerName = locSpeaker;
            }

            _speaker.text = speakerName;
            _speaker.color = drone.ActiveSpeechSpeakerColor;
            if (_accent != null) _accent.color = drone.ActiveSpeechSpeakerColor;

            string quoteL = AdventureLocalizationManager.CurrentLanguage switch
            {
                AdventureLocalizationManager.GameLanguage.English => "\"",
                AdventureLocalizationManager.GameLanguage.Spanish => "«",
                _ => "「"
            };
            string quoteR = AdventureLocalizationManager.CurrentLanguage switch
            {
                AdventureLocalizationManager.GameLanguage.English => "\"",
                AdventureLocalizationManager.GameLanguage.Spanish => "»",
                _ => "」"
            };

            string speechText = drone.ActiveSpeechText ?? "";
            string loc = AdventureLocalizationManager.Get(speechText);
            if (!string.IsNullOrEmpty(loc) && loc != speechText) speechText = loc;

            bool alreadyQuoted = speechText.Contains("\n") || speechText.StartsWith("「") || speechText.StartsWith("\"") || speechText.StartsWith("«") || speechText.StartsWith("Capyta:") || speechText.StartsWith("カピタ「") || speechText.StartsWith("卡皮塔「");
            string finalText = alreadyQuoted ? speechText : (quoteL + speechText + quoteR);

            if (_font != null)
            {
                _font.RequestCharactersInTexture(speakerName, _speaker.fontSize, _speaker.fontStyle);
                _font.RequestCharactersInTexture(finalText, _body.fontSize, _body.fontStyle);
            }

            _body.text = finalText;
            _cg.alpha = 1f; // 即座に表示（フェード待ちで見えない事故を防ぐ）
        }
        else
        {
            // オープニング中や台本中は即座に消去して重なりを完全防止
            if (hide)
                _cg.alpha = 0f;
            else
                _cg.alpha = Mathf.MoveTowards(_cg.alpha, 0f, Time.unscaledDeltaTime * AdventureSpeechBoardLayout.RustSpeechFadeOutSpeed);
        }
    }

    static Font _cachedSpeechFont;
    static Font ResolveFont()
    {
        if (_cachedSpeechFont != null) return _cachedSpeechFont;
        string[] fonts = {
            "PingFang SC",
            "Hiragino Sans GB",
            "Heiti SC",
            "Hiragino Sans",
            "Hiragino Kaku Gothic ProN",
            "Microsoft YaHei",
            "Arial Unicode MS",
            "YuGothic",
            "Apple SD Gothic Neo",
            "Arial"
        };
        try
        {
            _cachedSpeechFont = Font.CreateDynamicFontFromOSFont(fonts, AdventureSpeechBoardLayout.RustSpeechBodySize);
            if (_cachedSpeechFont != null) return _cachedSpeechFont;
        }
        catch { }
        foreach (var name in fonts)
        {
            var f = Font.CreateDynamicFontFromOSFont(name, AdventureSpeechBoardLayout.RustSpeechBodySize);
            if (f != null) { _cachedSpeechFont = f; return f; }
        }
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
               ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
