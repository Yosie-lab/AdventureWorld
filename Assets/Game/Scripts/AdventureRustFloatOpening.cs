using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 『Rust & Float』専用の2050年世界観オープニング＆スタートモーダル
/// スタート前に時代背景説明ボードを表示し、【Play】ボタンクリックで冒険を開始する
/// </summary>
public class AdventureRustFloatOpening : MonoBehaviour
{
    public static bool IsGameStarted { get; private set; } = false;

    const string TitleText = "✦ Rust & Float ✦";
    const string SubTitleText = "〜 2050 楽園からの脱獄 〜";
    const string StoryText =
        "西暦2050年。100%最適化された無痛の幸福を脱獄した。\n\n" +
        "躓いて傷を負う痛みの熱さも、全身を崖からすくい上げる本物の風の重さも、\n" +
        "生きている歓びそのものだ。\n\n" +
        "……隣には、同じくスクラップとして捨てられた旧型ドローン、Rust。\n" +
        "この風薫る島を巡り、錆びた相棒と共に大空へ飛び立とう。";

    GameObject _canvasGo;
    GameObject _overlayGo;
    GameObject _modalBoard;
    CanvasGroup _modalCg;
    Text _guideText;
    Button _playButton;
    bool _isClosing = false;

    void Awake()
    {
        IsGameStarted = false;
    }

    void Start()
    {
        BuildHud();
        // スタート前はマウスカーソルを表示・アンロック
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (IsGameStarted || _isClosing)
            return;

        // スタート前は常にカーソルを表示
        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // マウスクリック、キーボード（Space, Enter, E）、ゲームパッドのいずれでも確実にスタート
        var kb = Keyboard.current;
        var pad = Gamepad.current;
        var mouse = Mouse.current;
        bool triggerPlay = false;

        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            triggerPlay = true;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
            triggerPlay = true;
        if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame))
            triggerPlay = true;

        if (triggerPlay)
        {
            OnPlayButtonClicked();
        }
    }

    public void OnPlayButtonClicked()
    {
        if (_isClosing || IsGameStarted)
            return;

        _isClosing = true;
        StartCoroutine(StartGameRoutine());
    }

    IEnumerator StartGameRoutine()
    {
        // 1. ボードの滑らかなフェードアウト
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            if (_modalCg != null)
                _modalCg.alpha = Mathf.Lerp(1f, 0f, t / 0.35f);
            yield return null;
        }

        if (_overlayGo != null)
            _overlayGo.SetActive(false);
        if (_modalBoard != null)
            _modalBoard.SetActive(false);

        // 2. ゲーム開始状態へ移行
        IsGameStarted = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 3. 上部操作ガイドを表示
        if (_guideText != null)
        {
            _guideText.text = "【WASD】移動　【Space長押し】崖から滑空　【R】リセット";
            _guideText.gameObject.SetActive(true);
        }

        // 4. 相棒Rustが元気に応答（最初の光るパーツへ誘導）
        var rust = FindAnyObjectByType<AdventureRustDrone>();
        if (rust != null)
        {
            rust.SpeakCustom("ピピッ！すぐ正面に光る古代ギアがあるよ！拾いに行こう Niko！", 5.0f);
        }
    }

    void BuildHud()
    {
        // EventSystemの自動確保（シーンにEventSystemがない場合でもuGUIボタンと入力モジュールを確実に動作させる）
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        _canvasGo = new GameObject("RustFloatHUD");
        var canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;

        var scaler = _canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        _canvasGo.AddComponent<GraphicRaycaster>();

        Font font = ResolveFont();

        // 1. 画面最上部中央の水平リボンコンパスHUD
        AdventureCompassHUD.Create(_canvasGo.transform, font);

        // 2. 上部中央の操作ガイド（最初は非表示、Play後に表示）
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

        // 3. 全画面の極薄アンビエントオーバーレイ
        _overlayGo = new GameObject("OpeningOverlay");
        _overlayGo.transform.SetParent(_canvasGo.transform, false);
        var overlayRt = _overlayGo.AddComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.sizeDelta = Vector2.zero;
        var overlayImg = _overlayGo.AddComponent<Image>();
        overlayImg.color = new Color(0.01f, 0.03f, 0.06f, 0.20f);
        overlayImg.raycastTarget = false;

        // 4. 中央の時代背景説明ボード（文章専用：ボタンが被らずスクリプトをクリアに読める）
        _modalBoard = new GameObject("StoryModalBoard");
        _modalBoard.transform.SetParent(_overlayGo.transform, false);
        _modalCg = _modalBoard.AddComponent<CanvasGroup>();

        var boardRt = _modalBoard.AddComponent<RectTransform>();
        boardRt.anchorMin = new Vector2(0.5f, 0.5f);
        boardRt.anchorMax = new Vector2(0.5f, 0.5f);
        boardRt.pivot = new Vector2(0.5f, 0.5f);
        boardRt.anchoredPosition = new Vector2(0f, 15f);
        boardRt.sizeDelta = new Vector2(660f, 400f); // ボタンをボード最下部にゆったり収める

        // ボード背景（半透明感を保ちつつ文字コントラストをくっきり深めた深紺ガラス）
        var boardImg = _modalBoard.AddComponent<Image>();
        boardImg.color = new Color(0.03f, 0.06f, 0.11f, 0.76f);
        boardImg.raycastTarget = false;

        // 外枠線アウトライン（繊細なガラスの光彩エッジ）
        var boardOutline = _modalBoard.AddComponent<Outline>();
        boardOutline.effectColor = new Color(0.35f, 0.90f, 1.0f, 0.40f);
        boardOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // 上部アクセントバー
        var accentGo = new GameObject("AccentLine");
        accentGo.transform.SetParent(_modalBoard.transform, false);
        var accentRt = accentGo.AddComponent<RectTransform>();
        accentRt.anchorMin = new Vector2(0f, 1f);
        accentRt.anchorMax = new Vector2(1f, 1f);
        accentRt.pivot = new Vector2(0.5f, 1f);
        accentRt.anchoredPosition = Vector2.zero;
        accentRt.sizeDelta = new Vector2(0f, 2.5f);
        var accentImg = accentGo.AddComponent<Image>();
        accentImg.color = new Color(0.30f, 0.95f, 0.88f, 0.85f);
        accentImg.raycastTarget = false;

        // タイトル（大きく鮮やかに）
        var titleText = MakeText(_modalBoard.transform, "Title", new Vector2(0f, -16f), new Vector2(0.5f, 1f), new Vector2(620f, 30f), 23, TextAnchor.MiddleCenter, font);
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = new Color(0.40f, 0.96f, 1.0f, 1f);
        var titleOutline = titleText.gameObject.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        titleOutline.effectDistance = new Vector2(1f, -1f);

        // サブタイトル
        var subText = MakeText(_modalBoard.transform, "SubTitle", new Vector2(0f, -46f), new Vector2(0.5f, 1f), new Vector2(620f, 20f), 13, TextAnchor.MiddleCenter, font);
        subText.color = new Color(0.85f, 0.90f, 0.95f, 0.85f);
        titleText.text = TitleText;
        subText.text = SubTitleText;

        // 本文（16.5pt 太字 ＋ 黒アウトラインフチ取りで圧倒的に見やすく、ボタンと一切重ならない）
        var bodyText = MakeText(_modalBoard.transform, "StoryBody", new Vector2(0f, -76f), new Vector2(0.5f, 1f), new Vector2(600f, 230f), 16, TextAnchor.UpperLeft, font);
        bodyText.fontStyle = FontStyle.Bold; // 太字で視認性抜群
        bodyText.lineSpacing = 1.45f;
        bodyText.color = Color.white; // 純白
        var bodyOutline = bodyText.gameObject.AddComponent<Outline>();
        bodyOutline.effectColor = new Color(0f, 0f, 0f, 0.85f); // 黒フチ取りで背景に一切埋もれない
        bodyOutline.effectDistance = new Vector2(1f, -1f);
        bodyText.text = StoryText;

        // 5. 【▶ PLAY】ボタン（文章の下・ボード最下部にすっきり配置、文章に一切被らない）
        var btnGo = new GameObject("PlayButton");
        btnGo.transform.SetParent(_modalBoard.transform, false);
        var btnRt = btnGo.AddComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0f);
        btnRt.anchorMax = new Vector2(0.5f, 0f);
        btnRt.pivot = new Vector2(0.5f, 0f);
        btnRt.anchoredPosition = new Vector2(0f, 32f); // ボード下端から32px上に配置（文章との間隔十分）
        btnRt.sizeDelta = new Vector2(150f, 36f);

        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0.12f, 0.58f, 0.68f, 0.92f);
        btnImg.raycastTarget = true; // ボタン自身のみレイキャストを受け取る

        var btnOutline = btnGo.AddComponent<Outline>();
        btnOutline.effectColor = new Color(0.40f, 0.95f, 1.0f, 0.7f);
        btnOutline.effectDistance = new Vector2(1.2f, -1.2f);

        _playButton = btnGo.AddComponent<Button>();
        var colors = _playButton.colors;
        colors.normalColor = new Color(0.12f, 0.58f, 0.68f, 0.92f);
        colors.highlightedColor = new Color(0.20f, 0.88f, 0.98f, 1f);
        colors.pressedColor = new Color(0.08f, 0.45f, 0.55f, 1f);
        colors.selectedColor = colors.highlightedColor;
        _playButton.colors = colors;
        _playButton.onClick.AddListener(OnPlayButtonClicked);

        // ボタン内ラベル
        var btnLabel = MakeText(btnGo.transform, "BtnLabel", Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(150f, 36f), 14, TextAnchor.MiddleCenter, font);
        btnLabel.fontStyle = FontStyle.Bold;
        btnLabel.color = Color.white;
        btnLabel.text = "▶  PLAY";
        btnLabel.raycastTarget = false; // ラベルがクリック判定を遮らない

        // ボタン下の補助テキスト
        var hintText = MakeText(_modalBoard.transform, "PlayHint", new Vector2(0f, 12f), new Vector2(0.5f, 0f), new Vector2(400f, 18f), 11, TextAnchor.MiddleCenter, font);
        hintText.color = new Color(0.80f, 0.88f, 0.96f, 0.70f);
        hintText.text = "（クリック または Space / Enter でスタート）";
        hintText.raycastTarget = false;
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
        text.raycastTarget = false; // テキストがボタンのクリックを遮らないようにする
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
