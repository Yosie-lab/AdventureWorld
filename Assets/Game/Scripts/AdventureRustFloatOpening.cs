using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 『Rust & Float』専用の2050年世界観オープニングダイアログ
/// AdventureWorldのシステムを汚さず、RustAndFloatシーンでのみ自律動作する
/// </summary>
public class AdventureRustFloatOpening : MonoBehaviour
{
    const string OpeningText =
        "西暦2050年。100%最適化された無痛の幸福を脱獄した。\n" +
        "躓いて傷を負う痛みの熱さも、全身を崖からすくい上げる本物の風の重さも、生きている歓びそのものだ。\n" +
        "……隣には、同じくスクラップとして捨てられた旧型ドローン、Rust。\n\n" +
        "【スペース】または【E】でつづける";

    GameObject _canvasGo;
    GameObject _dialoguePanel;
    CanvasGroup _panelCg;
    Text _bodyText;
    Text _guideText;
    bool _isClosed = false;

    void Start()
    {
        BuildHud();
    }

    void Update()
    {
        if (_isClosed)
            return;

        var kb = Keyboard.current;
        var pad = Gamepad.current;

        bool closePressed = false;
        if (kb != null)
        {
            if (kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
                closePressed = true;
        }
        if (pad != null)
        {
            if (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame)
                closePressed = true;
        }

        if (closePressed)
        {
            _isClosed = true;
            StartCoroutine(CloseDialogueRoutine());
        }
    }

    IEnumerator CloseDialogueRoutine()
    {
        // 滑らかにフェードアウト
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            if (_panelCg != null)
                _panelCg.alpha = Mathf.Lerp(1f, 0f, t / 0.35f);
            yield return null;
        }

        if (_dialoguePanel != null)
            _dialoguePanel.SetActive(false);

        // 閉じた後は控えめに操作ガイドを表示
        if (_guideText != null)
        {
            _guideText.text = "【WASD】移動　【Space長押し】崖から滑空　【R】リセット";
            _guideText.gameObject.SetActive(true);
        }
    }

    void BuildHud()
    {
        _canvasGo = new GameObject("RustFloatHUD");
        var canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;

        var scaler = _canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        _canvasGo.AddComponent<GraphicRaycaster>();

        Font font = ResolveFont();

        // 1. 画面最上部中央の水平リボンコンパスHUD（邪魔にならないミニマルデザイン）
        AdventureCompassHUD.Create(_canvasGo.transform, font);

        // 2. 上部中央の操作ガイド（最初は非表示、ダイアログ終了後に表示）。
        // コンパスの下（y = -44f）に控えめに配置
        _guideText = MakeText(_canvasGo.transform, "Guide", Vector2.zero, new Vector2(0.5f, 1f), new Vector2(980f, 32f), 15, TextAnchor.UpperCenter, font);
        var guideRt = _guideText.rectTransform;
        guideRt.anchorMin = new Vector2(0.5f, 1f);
        guideRt.anchorMax = new Vector2(0.5f, 1f);
        guideRt.pivot = new Vector2(0.5f, 1f);
        guideRt.anchoredPosition = new Vector2(0f, -44f);
        guideRt.sizeDelta = new Vector2(980f, 32f);
        _guideText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _guideText.verticalOverflow = VerticalWrapMode.Overflow;
        _guideText.color = new Color(0.9f, 0.95f, 1f, 0.75f);
        var guideOutline = _guideText.gameObject.AddComponent<Outline>();
        guideOutline.effectColor = new Color(0f, 0f, 0f, 0.7f);
        guideOutline.effectDistance = new Vector2(1f, -1f);
        _guideText.gameObject.SetActive(false);

        // 2. セリフ用パネル
        _dialoguePanel = new GameObject("OpeningDialoguePanel");
        _dialoguePanel.transform.SetParent(_canvasGo.transform, false);
        _panelCg = _dialoguePanel.AddComponent<CanvasGroup>();

        var panelRt = _dialoguePanel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0f);
        panelRt.anchorMax = new Vector2(0.5f, 0f);
        panelRt.pivot = new Vector2(0.5f, 0f);
        panelRt.anchoredPosition = new Vector2(0f, 36f);
        panelRt.sizeDelta = new Vector2(1040, 160);

        var bg = _dialoguePanel.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.08f, 0.11f, 0.92f);

        // 3. セリフ本文
        _bodyText = MakeText(_dialoguePanel.transform, "OpeningBody", Vector2.zero, Vector2.zero, Vector2.zero, 18, TextAnchor.MiddleCenter, font);
        var bodyRt = _bodyText.rectTransform;
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(32, 18);
        bodyRt.offsetMax = new Vector2(-32, -18);
        _bodyText.lineSpacing = 1.35f;
        _bodyText.color = new Color(0.96f, 0.97f, 1f, 1f);
        _bodyText.text = OpeningText;
    }

    static Text MakeText(Transform parent, string name, Vector2 pos, Vector2 anchor, Vector2 size, int fontSize, TextAnchor align, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = align;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    static Font ResolveFont()
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

        Font builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (builtin != null)
            return builtin;
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
