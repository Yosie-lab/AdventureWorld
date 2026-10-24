using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 相棒Rustの感情LEDエクスプレッション（フェイスアイコン発光プロジェクター）。
/// 前面アイレンズに、セリフや状況に応じた動的アイコン（^ ▽ ^ 笑顔、> < 歓喜、✦ ✦ 発見、? 好奇心、! 驚き、u u 居眠り）
/// をプロシージャル発光描画し、会話・アクションと完全同期する。
/// </summary>
public partial class AdventureRustDrone
{
    public enum FaceExpressionType
    {
        Normal,      // 穏やかな通常待機眼（●  ●）
        Smiling,     // 笑顔・満足・撫でられ（^ ▽ ^）
        Joyful,      // 歓喜・大興奮・ダイブ（> ▽ <）
        Discovery,   // 発見・ひらめき・パーツ（✦  ✦）
        Curious,     // 好奇心・首かしげ・観察（?  ?）
        Surprised,   // 驚き・パニック（!  !）
        Sleeping,    // 居眠り・チル・安らぎ（u  u）
        Blinking     // 瞬き（-  -）
    }

    private FaceExpressionType _currentExpression = FaceExpressionType.Normal;
    private float _expressionExpireTime = 0f;
    private float _nextBlinkTime = 3f;
    private bool _isBlinking = false;
    private float _blinkEndTime = 0f;

    private GameObject _faceRootGo;
    private Text _faceText;
    private Outline _faceOutline;
    private Image _faceBackdropGlow;
    private Light _faceGlowLight;
    private Vector3 _faceBaseScale = Vector3.one * 0.0024f;
    private float _popAnimTimer = 0f;

    /// <summary>現在の表情タイプ</summary>
    public FaceExpressionType CurrentExpression => _currentExpression;

    /// <summary>
    /// 表情を直接指定して変更（会話・アクション連動）
    /// </summary>
    public void SetExpression(FaceExpressionType type, float duration = 3.5f)
    {
        EnsureFaceDisplay();
        _currentExpression = type;
        _expressionExpireTime = Time.time + duration;
        _popAnimTimer = 0.20f; // ポップアニメーション開始
        UpdateFaceVisuals();
    }

    /// <summary>
    /// 感情LEDディスプレイの初期化・保証
    /// </summary>
    private void EnsureFaceDisplay()
    {
        if (_faceRootGo != null) return;

        // Rust直下に前面HUD Canvasを生成
        _faceRootGo = new GameObject("Rust_FaceDisplay");
        _faceRootGo.transform.SetParent(transform, false);
        // Eyeの位置: (0, 0.04, 0.23) ➔ わずかに前面: (0, 0.045, 0.268)
        _faceRootGo.transform.localPosition = new Vector3(0f, 0.045f, 0.268f);
        _faceRootGo.transform.localRotation = Quaternion.identity;
        _faceRootGo.transform.localScale = _faceBaseScale;

        var canvas = _faceRootGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;

        var rt = _faceRootGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160f, 80f);

        // 背景のホログラム光彩レンズサークル
        var glowGo = new GameObject("LensHoloGlow");
        glowGo.transform.SetParent(_faceRootGo.transform, false);
        var gRt = glowGo.AddComponent<RectTransform>();
        gRt.anchorMin = new Vector2(0.1f, 0.1f);
        gRt.anchorMax = new Vector2(0.9f, 0.9f);
        gRt.offsetMin = gRt.offsetMax = Vector2.zero;
        _faceBackdropGlow = glowGo.AddComponent<Image>();
        _faceBackdropGlow.color = new Color(0.12f, 0.55f, 0.85f, 0.18f); // 柔らかなシアン光彩

        // 表情テキスト（高精細発光LED）
        var textGo = new GameObject("FaceExpressionText");
        textGo.transform.SetParent(_faceRootGo.transform, false);
        var tRt = textGo.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = tRt.offsetMax = Vector2.zero;

        _faceText = textGo.AddComponent<Text>();
        Font font = ResolveFaceFont();
        _faceText.font = font;
        _faceText.fontSize = 38;
        _faceText.fontStyle = FontStyle.Bold;
        _faceText.alignment = TextAnchor.MiddleCenter;
        _faceText.color = new Color(0.35f, 1.0f, 0.88f, 0.95f); // ネオンシアン

        _faceOutline = textGo.AddComponent<Outline>();
        _faceOutline.effectColor = new Color(0.05f, 0.35f, 0.75f, 0.75f);
        _faceOutline.effectDistance = new Vector2(1.8f, -1.8f);

        // 表情連動の微小PointLight
        var lGo = new GameObject("FaceGlowLight");
        lGo.transform.SetParent(_faceRootGo.transform, false);
        lGo.transform.localPosition = new Vector3(0f, 0f, 0.05f);
        _faceGlowLight = lGo.AddComponent<Light>();
        _faceGlowLight.type = LightType.Point;
        _faceGlowLight.range = 2.4f;
        _faceGlowLight.intensity = 0.65f;
        _faceGlowLight.color = new Color(0.35f, 0.95f, 1.0f);

        UpdateFaceVisuals();
    }

    /// <summary>
    /// 毎フレームの表情アニメーション（呼吸パルス、瞬き、ポップイン）
    /// </summary>
    private void UpdateFaceExpressionAnimation()
    {
        EnsureFaceDisplay();
        if (_faceRootGo == null || _faceText == null) return;

        float now = Time.time;

        // 1. 表情の有効期限判定（期限切れで通常または居眠りへ戻る）
        if (_currentExpression != FaceExpressionType.Normal && _currentExpression != FaceExpressionType.Sleeping)
        {
            if (now > _expressionExpireTime)
            {
                // 居眠り状態ならSleeping、それ以外ならNormal
                bool isPerching = CurrentState == RustState.Perching;
                _currentExpression = isPerching ? FaceExpressionType.Sleeping : FaceExpressionType.Normal;
                UpdateFaceVisuals();
            }
        }
        else if (CurrentState == RustState.Perching && _currentExpression != FaceExpressionType.Sleeping)
        {
            _currentExpression = FaceExpressionType.Sleeping;
            UpdateFaceVisuals();
        }

        // 2. 通常待機時の自然な瞬き（Blink）
        if (_currentExpression == FaceExpressionType.Normal)
        {
            if (!_isBlinking && now > _nextBlinkTime)
            {
                _isBlinking = true;
                _blinkEndTime = now + 0.14f;
                _nextBlinkTime = now + Random.Range(3.5f, 6.5f);
                _faceText.text = "-   -";
            }
            else if (_isBlinking && now > _blinkEndTime)
            {
                _isBlinking = false;
                _faceText.text = "●   ●";
            }
        }

        // 3. 呼吸パルス（微細な明滅）
        float pulse = Mathf.Sin(now * 3.2f) * 0.08f;
        Color baseCol = GetExpressionBaseColor(_currentExpression);
        _faceText.color = new Color(baseCol.r, baseCol.g, baseCol.b, Mathf.Clamp01(baseCol.a + pulse));

        // 4. ポップイン・スケール変化
        if (_popAnimTimer > 0f)
        {
            _popAnimTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(_popAnimTimer / 0.20f);
            float scaleMultiplier = 1f + Mathf.Sin(t * Mathf.PI) * 0.25f;
            _faceRootGo.transform.localScale = _faceBaseScale * scaleMultiplier;
        }
        else
        {
            _faceRootGo.transform.localScale = _faceBaseScale;
        }
    }

    /// <summary>
    /// 表情タイプに応じた文字列、カラー、光彩の更新
    /// </summary>
    private void UpdateFaceVisuals()
    {
        if (_faceText == null) return;

        string glyph = "●   ●";
        Color col = new Color(0.35f, 1.0f, 0.88f, 0.95f);      // ネオンシアン（通常）
        Color outlineCol = new Color(0.05f, 0.35f, 0.75f, 0.75f);
        Color lightCol = new Color(0.35f, 0.95f, 1.0f);

        switch (_currentExpression)
        {
            case FaceExpressionType.Normal:
                glyph = _isBlinking ? "-   -" : "●   ●";
                col = new Color(0.35f, 1.0f, 0.88f, 0.95f);
                outlineCol = new Color(0.05f, 0.35f, 0.75f, 0.75f);
                lightCol = new Color(0.35f, 0.95f, 1.0f);
                break;

            case FaceExpressionType.Smiling:
                glyph = "^ ▽ ^";
                col = new Color(1.0f, 0.88f, 0.40f, 0.98f);     // 暖かな黄金色
                outlineCol = new Color(0.75f, 0.45f, 0.05f, 0.8f);
                lightCol = new Color(1.0f, 0.85f, 0.45f);
                break;

            case FaceExpressionType.Joyful:
                glyph = "> ▽ <";
                col = new Color(1.0f, 0.50f, 0.85f, 0.98f);     // 祝祭ピンク・マゼンタ
                outlineCol = new Color(0.65f, 0.10f, 0.55f, 0.8f);
                lightCol = new Color(1.0f, 0.55f, 0.85f);
                break;

            case FaceExpressionType.Discovery:
                glyph = "✦   ✦";
                col = new Color(1.0f, 0.95f, 0.30f, 1.0f);      // 輝く星のイエローゴールド
                outlineCol = new Color(0.85f, 0.60f, 0.05f, 0.85f);
                lightCol = new Color(1.0f, 0.92f, 0.35f);
                break;

            case FaceExpressionType.Curious:
                glyph = "?   ?";
                col = new Color(0.40f, 0.85f, 1.0f, 0.95f);     // 探求スカイブルー
                outlineCol = new Color(0.10f, 0.40f, 0.80f, 0.8f);
                lightCol = new Color(0.45f, 0.85f, 1.0f);
                break;

            case FaceExpressionType.Surprised:
                glyph = "!   !";
                col = new Color(1.0f, 0.35f, 0.35f, 0.98f);     // 驚き・警告オレンジレッド
                outlineCol = new Color(0.75f, 0.15f, 0.15f, 0.85f);
                lightCol = new Color(1.0f, 0.40f, 0.35f);
                break;

            case FaceExpressionType.Sleeping:
                glyph = "u   u";
                col = new Color(0.45f, 0.75f, 0.95f, 0.75f);    // 穏やかな眠りシアンブルー
                outlineCol = new Color(0.15f, 0.35f, 0.65f, 0.6f);
                lightCol = new Color(0.35f, 0.65f, 0.95f);
                break;

            case FaceExpressionType.Blinking:
                glyph = "-   -";
                col = new Color(0.35f, 1.0f, 0.88f, 0.95f);
                break;
        }

        _faceText.text = glyph;
        _faceText.color = col;
        if (_faceOutline != null) _faceOutline.effectColor = outlineCol;
        if (_faceBackdropGlow != null) _faceBackdropGlow.color = new Color(col.r, col.g, col.b, 0.18f);
        if (_faceGlowLight != null) _faceGlowLight.color = lightCol;
    }

    private Color GetExpressionBaseColor(FaceExpressionType type)
    {
        return type switch
        {
            FaceExpressionType.Smiling => new Color(1.0f, 0.88f, 0.40f, 0.98f),
            FaceExpressionType.Joyful => new Color(1.0f, 0.50f, 0.85f, 0.98f),
            FaceExpressionType.Discovery => new Color(1.0f, 0.95f, 0.30f, 1.0f),
            FaceExpressionType.Curious => new Color(0.40f, 0.85f, 1.0f, 0.95f),
            FaceExpressionType.Surprised => new Color(1.0f, 0.35f, 0.35f, 0.98f),
            FaceExpressionType.Sleeping => new Color(0.45f, 0.75f, 0.95f, 0.75f),
            _ => new Color(0.35f, 1.0f, 0.88f, 0.95f)
        };
    }

    /// <summary>
    /// セリフテキストから自動的に感情エクスプレッションを判別して適用
    /// </summary>
    public void AutoTriggerExpressionFromSpeech(string text, float duration = 4.0f)
    {
        if (string.IsNullOrEmpty(text)) return;

        // 1. 発見・パーツ・共鳴
        if (text.Contains("パーツ") || text.Contains("発見") || text.Contains("光る") || text.Contains("共鳴") || text.Contains("宝") || text.Contains("拾った") || text.Contains("見つけた") || text.Contains("貝殻") || text.Contains("スクラップ") || text.Contains("part") || text.Contains("relic") || text.Contains("find") || text.Contains("found"))
        {
            SetExpression(FaceExpressionType.Discovery, duration);
            return;
        }

        // 2. 歓喜・大興奮・ダイブ・ターン
        if (text.Contains("大空へダイブ") || text.Contains("ターン") || text.Contains("最高") || text.Contains("すごい") || text.Contains("わああ") || text.Contains("ピキーッ") || text.Contains("風に乗") || text.Contains("Dive") || text.Contains("Turn") || text.Contains("Awesome") || text.Contains("Great"))
        {
            SetExpression(FaceExpressionType.Joyful, duration);
            return;
        }

        // 3. 驚き・パニック・警戒
        if (text.Contains("冷たい") || text.Contains("パニック") || text.Contains("溺れ") || text.Contains("危な") || text.Contains("わっ！") || text.Contains("びっくり") || text.Contains("水だ") || text.Contains("Panic") || text.Contains("Danger") || text.Contains("Cold"))
        {
            SetExpression(FaceExpressionType.Surprised, duration);
            return;
        }

        // 4. 好奇心・観察・首かしげ
        if (text.Contains("チョウチョ") || text.Contains("草花") || text.Contains("なんだろう") || text.Contains("見て") || text.Contains("？」") || text.Contains("?") || text.Contains("Curious") || text.Contains("Butterfly"))
        {
            SetExpression(FaceExpressionType.Curious, duration);
            return;
        }

        // 5. 居眠り・チル
        if (text.Contains("すやすや") || text.Contains("チル") || text.Contains("ふぁ…") || text.Contains("眠") || text.Contains("Sleep") || text.Contains("Chill"))
        {
            SetExpression(FaceExpressionType.Sleeping, duration);
            return;
        }

        // 6. 笑顔・親愛・安心
        SetExpression(FaceExpressionType.Smiling, duration);
    }

    private Font ResolveFaceFont()
    {
        string[] fontNames = {
            "Hiragino Sans",
            "Hiragino Kaku Gothic ProN",
            "PingFang SC",
            "Microsoft YaHei",
            "YuGothic",
            "Arial"
        };
        foreach (var name in fontNames)
        {
            var f = Font.CreateDynamicFontFromOSFont(name, 36);
            if (f != null) return f;
        }
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
