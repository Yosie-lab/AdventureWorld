using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// シネマエピローグ＆クリア接続
/// </summary>
public partial class AdventureSanctuaryTowerManager
{
    #region 10. エピローグ演出 & オートグライド

    struct FilmLine
    {
        public string Text;
        public float Hold;
        public int Act;
        public Color Color;
        public FilmLine(string text, float hold, int act, Color color)
        {
            Text = text;
            Hold = hold;
            Act = act;
            Color = color;
        }
    }

    // 関門5：映画字幕は1行ずつ・3幕（格調高い映画テロップ）
    static readonly FilmLine[] EpilogueFilmLines =
    {
        new FilmLine("「わぁぁ……！見て、Niko！　世界はこんなに広かったんだ……！！」", 8.0f, 0,
            new Color(0.55f, 0.95f, 1f, 1f)),
        new FilmLine("空が割れた。", 3.4f, 1, new Color(1f, 0.96f, 0.82f, 1f)),
        new FilmLine("箱庭の外には、凍えるほどリアルで、優しい風が吹いていた。", 4.8f, 1, new Color(1f, 0.96f, 0.82f, 1f)),
        new FilmLine("人は、最適で最短な道を進むときじゃなく、", 4.4f, 2, new Color(1f, 0.98f, 0.88f, 1f)),
        new FilmLine("寄り道をして、躓きながらも出会えた感動に——", 4.6f, 2, new Color(1f, 0.98f, 0.88f, 1f)),
        new FilmLine("真の生きている証(あかし)を得るんだ。", 4.4f, 2, new Color(1f, 0.98f, 0.88f, 1f)),
        new FilmLine("傷つくかもしれない自由と、生きることの重みを取り戻した二人の旅が、", 4.8f, 3, new Color(1f, 0.94f, 0.70f, 1f)),
        new FilmLine("ここから、また始まる。—— 『Rust & Float』", 3.55f, 3, new Color(1f, 0.88f, 0.45f, 1f)),
    };

    Font ResolveEpilogueFont()
    {
        if (_epilogueFont != null)
            return _epilogueFont;
        _epilogueFont = CreateJapaneseFont(32);
        return _epilogueFont;
    }

    void BeginEpiloguePlayback()
    {
        if (_epilogueRoutine != null)
        {
            StopCoroutine(_epilogueRoutine);
            _epilogueRoutine = null;
        }

        var player = GetPlayer();
        if (player != null)
        {
            if (player.transform.position.y < 110f)
                player.ApplyLaunchUpdraft(18f, 16f);
            player.SetAutoGlideMode(true, 120f);
            player.ApplyGlideBoost(1.2f, 8f);
        }

        SetCinematicCamera(true);
        SetExplorationHudVisible(false);
        try
        {
            SpawnWildernessPanorama(coldCrisis: false);
            SoftenSkybreakColdAtmosphere();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[RustAndFloat] エピローグ背景演出: " + e.Message);
        }
        SuppressAllSpeechAndBanners();
        HideScriptBoardCompletely();
        TeardownScriptBoardUi();
        HideOilPromptUI();

        var flash = GameObject.Find("BreakthroughFlashCanvas");
        if (flash != null)
            Destroy(flash);

        RebuildCinematicLetterbox();
        if (_letterboxRoot != null)
        {
            var canvas = _letterboxRoot.GetComponent<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = 9500; // フラッシュより前面
            _letterboxRoot.SetActive(true);
        }
        if (_letterboxCg != null)
            _letterboxCg.alpha = 1f;
        _letterboxTargetAlpha = 1f;

        _epilogueStartedAt = Time.unscaledTime;
        _filmLastAct = -1;
        _filmIndex = 0;
        PrepareFilmLine(0, allowActGap: false);
        Debug.Log($"[RustAndFloat] シネマエピローグ開始（Update） lines={EpilogueFilmLines.Length} subtitle={(_filmSubtitleUi != null)}");
    }

    void PrepareFilmLine(int index, bool allowActGap)
    {
        if (index < 0 || index >= EpilogueFilmLines.Length)
        {
            FinishEpilogueFilm();
            return;
        }

        var line = EpilogueFilmLines[index];
        _filmIndex = index;
        bool isFinalLine = (index == EpilogueFilmLines.Length - 1);
        // Hold＝放置の総時間（フェードイン＋保持＋フェードアウト）
        _filmFadeInSec = 0.55f;
        _filmFadeOutSec = isFinalLine ? 0f : 0.50f;
        float total = Mathf.Max(1.4f, line.Hold);
        _filmHoldSec = Mathf.Max(0.6f, total - _filmFadeInSec - _filmFadeOutSec);
        _filmColor = line.Color;

        if (allowActGap && line.Act != _filmLastAct && line.Act >= 1 && _filmLastAct >= 0)
        {
            ClearFilmSubtitle();
            _epilogueAct = line.Act;
            _filmPhase = 4;
            _filmPhaseAt = Time.unscaledTime;
            return;
        }

        _filmLastAct = line.Act;
        _epilogueAct = Mathf.Max(1, line.Act);
        PresentFilmLineContent(line.Text, line.Color);
        _filmPhase = 1;
        _filmPhaseAt = Time.unscaledTime;
    }

    void PresentFilmLineContent(string text, Color color)
    {
        EnsureCinematicLetterbox();
        if (_filmSubtitleUi == null)
            RebuildCinematicLetterbox();
        if (_letterboxRoot != null && !_letterboxRoot.activeSelf)
            _letterboxRoot.SetActive(true);
        if (_letterboxCg != null)
            _letterboxCg.alpha = 1f;
        _letterboxTargetAlpha = 1f;

        if (_filmSubtitleUi == null)
        {
            Debug.LogError("[RustAndFloat] FilmSubtitle UI 生成失敗");
            return;
        }
        _filmSubtitleUi.text = text ?? "";
        _filmSubtitleUi.color = new Color(color.r, color.g, color.b, 0f);
        _filmSubtitleUi.transform.localScale = new Vector3(0.985f, 0.985f, 1f);
        _epilogueAlpha = 0f;
    }

    void TickEpilogueFilm()
    {
        if (!_epilogueTriggered || _showGameClearModal)
            return;
        if (_filmIndex < 0 || _filmPhase <= 0)
            return;

        var player = GetPlayer();
        KeepAutoGlide(player);
        SetExplorationHudVisible(false);
        SuppressAllSpeechAndBanners();
        _letterboxTargetAlpha = 1f;
        if (_letterboxCg != null)
            _letterboxCg.alpha = 1f;
        if (_letterboxRoot != null && !_letterboxRoot.activeSelf)
            _letterboxRoot.SetActive(true);

        float elapsed = Time.unscaledTime - _filmPhaseAt;

        // 幕あい（余韻）
        if (_filmPhase == 4)
        {
            if (elapsed >= 1.35f)
            {
                var line = EpilogueFilmLines[_filmIndex];
                _filmLastAct = line.Act;
                PresentFilmLineContent(line.Text, line.Color);
                _filmPhase = 1;
                _filmPhaseAt = Time.unscaledTime;
            }
            return;
        }

        if (_filmSubtitleUi == null)
        {
            // UI欠落時は時間だけ進めて次へ
            if (elapsed >= _filmHoldSec)
                AdvanceFilmLine();
            return;
        }

        Color c = _filmColor;
        if (_filmPhase == 1)
        {
            float t = Mathf.Clamp01(elapsed / _filmFadeInSec);
            float a = Mathf.SmoothStep(0f, 1f, t);
            _filmSubtitleUi.color = new Color(c.r, c.g, c.b, a);
            _epilogueAlpha = a;
            float scale = Mathf.Lerp(0.985f, 1.0f, t);
            _filmSubtitleUi.transform.localScale = new Vector3(scale, scale, 1f);
            if (elapsed >= _filmFadeInSec)
            {
                _filmSubtitleUi.color = c;
                _filmSubtitleUi.transform.localScale = Vector3.one;
                _filmPhase = 2;
                _filmPhaseAt = Time.unscaledTime;
            }
        }
        else if (_filmPhase == 2)
        {
            _filmSubtitleUi.color = c;
            _epilogueAlpha = 1f;
            float holdT = Mathf.Clamp01(elapsed / _filmHoldSec);
            float scale = Mathf.Lerp(1.0f, 1.025f, holdT);
            _filmSubtitleUi.transform.localScale = new Vector3(scale, scale, 1f);

            bool canSkip = elapsed >= 1.2f && (Time.unscaledTime - _epilogueStartedAt) >= 2.0f;
            if ((canSkip && WasFilmSkipPressed()) || elapsed >= _filmHoldSec)
            {
                if (_filmFadeOutSec <= 0.01f)
                {
                    AdvanceFilmLine();
                }
                else
                {
                    _filmPhase = 3;
                    _filmPhaseAt = Time.unscaledTime;
                }
            }
        }
        else if (_filmPhase == 3)
        {
            if (_filmFadeOutSec <= 0.01f)
            {
                AdvanceFilmLine();
                return;
            }
            float t = Mathf.Clamp01(elapsed / _filmFadeOutSec);
            float a = Mathf.SmoothStep(1f, 0f, t);
            _filmSubtitleUi.color = new Color(c.r, c.g, c.b, a);
            _epilogueAlpha = a;
            float scale = Mathf.Lerp(1.025f, 1.04f, t);
            _filmSubtitleUi.transform.localScale = new Vector3(scale, scale, 1f);
            if (elapsed >= _filmFadeOutSec)
                AdvanceFilmLine();
        }
    }

    void AdvanceFilmLine()
    {
        int next = _filmIndex + 1;
        if (next >= EpilogueFilmLines.Length)
        {
            FinishEpilogueFilm();
            return;
        }
        PrepareFilmLine(next, allowActGap: true);
    }

    void FinishEpilogueFilm()
    {
        ClearFilmSubtitle();
        _filmIndex = -1;
        _filmPhase = 0;
        KeepAutoGlide(GetPlayer());
        IsGameCleared = true;
        _showGameClearModal = true;
        _epilogueAlpha = 0f;
        _letterboxTargetAlpha = 0f;
        SuppressAllSpeechAndBanners();
        AdventureMusicDirector.Ensure();
        AdventureMusicDirector.Instance?.KeepEndingThemeActive();
        Debug.Log("[RustAndFloat] シネマエピローグ完了 → クリアモーダル");
    }

    IEnumerator EpilogueSequenceRoutine()
    {
        // 互換用：Update駆動へ委譲
        BeginEpiloguePlayback();
        yield break;
    }

    IEnumerator ShowFilmSubtitleRoutine(string line, float holdSec, Color color)
    {
        yield break;
    }

    void ClearFilmSubtitle()
    {
        if (_filmSubtitleUi != null)
        {
            _filmSubtitleUi.text = "";
            var c = _filmSubtitleUi.color;
            c.a = 0f;
            _filmSubtitleUi.color = c;
        }
        _epilogueAlpha = 0f;
    }

    static bool WasFilmSkipPressed()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            return true;
        try
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                return true;
        }
        catch { }
        return false;
    }

    static void KeepAutoGlide(AdventurePlayerController player)
    {
        if (player != null && !player.IsAutoGliding)
            player.SetAutoGlideMode(true, 120f);
    }

    void SetExplorationHudVisible(bool visible)
    {
        if (visible)
            AdventureCompassHUD.Ensure();

        var compass = AdventureCompassHUD.Instance
                      ?? Object.FindFirstObjectByType<AdventureCompassHUD>(FindObjectsInactive.Include);
        if (compass != null)
            compass.gameObject.SetActive(visible);

        var scrap = AdventureScrapHUD.Instance
                    ?? Object.FindFirstObjectByType<AdventureScrapHUD>(FindObjectsInactive.Include);
        if (scrap != null)
            scrap.gameObject.SetActive(visible);

        if (_cachedGuideText == null)
        {
            foreach (var t in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include))
            {
                if (t != null && t.name == "Guide")
                {
                    _cachedGuideText = t;
                    break;
                }
            }
        }
        if (_cachedGuideText != null)
            _cachedGuideText.gameObject.SetActive(visible);
    }

    void TickCinematicLetterbox()
    {
        bool filmPlaying = _epilogueTriggered && !_showGameClearModal && _filmIndex >= 0 && _filmPhase > 0;
        bool want = filmPlaying || AdventureStoryFlow.IsPerformance;
        _letterboxTargetAlpha = want ? 1f : 0f;
        if (filmPlaying)
            _letterboxTargetAlpha = 1f;

        if (want)
        {
            SetExplorationHudVisible(false);
            EnsureCinematicLetterbox();
            if (_letterboxRoot != null && !_letterboxRoot.activeSelf)
                _letterboxRoot.SetActive(true);
            if (filmPlaying && _letterboxCg != null)
                _letterboxCg.alpha = 1f;
        }

        if (_letterboxCg != null)
        {
            float a = filmPlaying
                ? 1f
                : Mathf.MoveTowards(_letterboxCg.alpha, _letterboxTargetAlpha, Time.unscaledDeltaTime * 2.4f);
            _letterboxCg.alpha = a;
            if (_letterboxRoot != null)
            {
                bool show = a > 0.02f || _letterboxTargetAlpha > 0.02f || filmPlaying;
                if (_letterboxRoot.activeSelf != show)
                    _letterboxRoot.SetActive(show);
            }
        }
        else if (!want && _letterboxRoot != null && _letterboxRoot.activeSelf)
        {
            _letterboxRoot.SetActive(false);
        }
    }

    void RebuildCinematicLetterbox()
    {
        if (_letterboxRoot != null)
        {
            Destroy(_letterboxRoot);
            _letterboxRoot = null;
        }
        _letterboxCg = null;
        _letterboxTop = null;
        _letterboxBottom = null;
        _filmSubtitleUi = null;
        EnsureCinematicLetterbox();
    }

    void EnsureCinematicLetterbox()
    {
        if (_letterboxRoot != null)
        {
            if (_filmSubtitleUi == null)
                BuildFilmSubtitleOnLetterbox();
            return;
        }
        EnsureEventSystemForUi();

        var canvasGo = new GameObject("CinematicLetterboxCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9500;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        _letterboxCg = canvasGo.AddComponent<CanvasGroup>();
        _letterboxCg.alpha = 0f;
        _letterboxCg.blocksRaycasts = false;
        _letterboxCg.interactable = false;

        _letterboxTop = MakeLetterbar(canvasGo.transform, "Top", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 64f);
        _letterboxBottom = MakeLetterbar(canvasGo.transform, "Bottom", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), 118f);
        _letterboxRoot = canvasGo;
        BuildFilmSubtitleOnLetterbox();
    }

    void BuildFilmSubtitleOnLetterbox()
    {
        if (_letterboxRoot == null || _filmSubtitleUi != null) return;
        Font font = ResolveEpilogueFont();
        var go = new GameObject("FilmSubtitle");
        go.transform.SetParent(_letterboxRoot.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.08f, 0f);
        rt.anchorMax = new Vector2(0.92f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 38f);
        rt.sizeDelta = new Vector2(0f, 84f);
        _filmSubtitleUi = go.AddComponent<Text>();
        _filmSubtitleUi.font = font;
        _filmSubtitleUi.fontSize = 35;
        _filmSubtitleUi.lineSpacing = 1.18f;
        _filmSubtitleUi.alignment = TextAnchor.MiddleCenter;
        _filmSubtitleUi.color = new Color(1f, 0.94f, 0.78f, 0f);
        _filmSubtitleUi.horizontalOverflow = HorizontalWrapMode.Wrap;
        _filmSubtitleUi.verticalOverflow = VerticalWrapMode.Overflow;
        _filmSubtitleUi.raycastTarget = false;
        if (_filmSubtitleUi.font == null)
            _filmSubtitleUi.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.01f, 0.02f, 0.04f, 0.88f);
        shadow.effectDistance = new Vector2(1.5f, -2.0f);
    }

    static Image MakeLetterbar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, anchorMin.y);
        rt.anchorMax = new Vector2(1f, anchorMax.y);
        rt.pivot = new Vector2(0.5f, anchorMin.y);
        rt.sizeDelta = new Vector2(0f, height);
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.94f);
        img.raycastTarget = false;
        return img;
    }

    static void SetCinematicCamera(bool enabled)
    {
        var cam = Camera.main;
        if (cam == null)
            cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null)
            return;
        var follow = cam.GetComponent<AdventureCameraFollow>();
        if (follow != null)
            follow.SetCinematicMode(enabled);
    }

    #endregion
}
