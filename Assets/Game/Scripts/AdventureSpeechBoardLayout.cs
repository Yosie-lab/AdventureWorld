using UnityEngine;

/// <summary>
/// セリフ／物語ボードのサイズと表示時間。エンディング（エピローグ・クリア）は大きめ・長め、それ以外はコンパクト。
/// </summary>
public static class AdventureSpeechBoardLayout
{
    public static bool IsCinematicEndingBoard(AdventureSanctuaryTowerManager tower)
    {
        if (tower == null) return false;
        return tower.EpilogueTriggered || tower.ShowGameClearModal;
    }

    static bool IsExploreSpeechContext()
    {
        if (AdventureStoryFlow.IsPerformance) return false;
        var tower = AdventureSanctuaryTowerManager.Instance;
        if (tower != null && IsCinematicEndingBoard(tower)) return false;
        return true;
    }

    /// <summary>探索中 Rust 吹き出しなど（エンディング台本以外）</summary>
    public static float ScaleExploreSpeechDuration(float seconds)
    {
        if (!IsExploreSpeechContext()) return seconds;
        return Mathf.Max(1.5f, seconds * ExploreSpeechDurationMul);
    }

    public static float ScaleScriptAutoAdvance(float seconds, AdventureSanctuaryTowerManager tower)
    {
        if (IsCinematicEndingBoard(tower)) return seconds;
        return Mathf.Max(ScriptAutoAdvanceMinSec, seconds * ScriptAutoAdvanceMul);
    }

    public readonly struct ScriptBoardMetrics
    {
        public readonly int TitleSize;
        public readonly int SpeakerSize;
        public readonly int BodySize;
        public readonly int HintSize;
        public readonly float BoardWidth;
        public readonly float BoardHeightMin;
        public readonly float BoardHeightMax;
        public readonly float DimAlpha;
        public readonly float PanelAlpha;

        public ScriptBoardMetrics(int title, int speaker, int body, int hint,
            float w, float hMin, float hMax, float dimA, float panelA)
        {
            TitleSize = title; SpeakerSize = speaker; BodySize = body; HintSize = hint;
            BoardWidth = w; BoardHeightMin = hMin; BoardHeightMax = hMax;
            DimAlpha = dimA; PanelAlpha = panelA;
        }
    }

    public static ScriptBoardMetrics GetScriptBoardMetrics(AdventureSanctuaryTowerManager tower)
    {
        if (IsCinematicEndingBoard(tower))
        {
            return new ScriptBoardMetrics(34, 23, 32, 18, 820f, 190f, 380f, 0.28f, 0.55f);
        }
        return new ScriptBoardMetrics(21, 16, 21, 14, 480f, 118f, 210f, 0.12f, 0.36f);
    }

    // --- 表示時間 ---
    public const float ExploreSpeechDurationMul = 0.62f;
    public const float RustSpeechDefaultDuration = 2.8f;
    public const float RustIdleChatDuration = 2.2f;
    public const float RustSpeechMinHoldSec = 0.65f;
    public const float RustSpeechAfterActionLinger = 1.2f;
    public const float ScriptAutoAdvanceMul = 0.52f;
    public const float ScriptAutoAdvanceMinSec = 1.8f;
    public const float ScriptManualMinShowSec = 0.22f;
    public const float PoeticBannerSeconds = 5.5f;
    public const float ScrapUpgradeBannerSeconds = 3.8f;
    public const float SaveBannerSeconds = 2.4f;

    // --- 探索中 Rust 吹き出し（uGUI） ---
    public const float RustSpeechPanelW = 480f;
    public const float RustSpeechPanelH = 84f;
    public const float RustSpeechBottomY = 22f;
    public const int RustSpeechSpeakerSize = 14;
    public const int RustSpeechBodySize = 18;
    public const float RustSpeechBgAlpha = 0.72f;
    public const float RustSpeechFadeOutSpeed = 7f;

    // --- 砂浜読み物（IMGUI） ---
    public const float NarrativeBoardWidthFrac = 0.64f;
    public const float NarrativeBoardMaxW = 580f;
    public const float NarrativeBoardHeightFrac = 0.52f;
    public const float NarrativeBoardMaxH = 300f;
    public const int NarrativeTitleSize = 23;
    public const int NarrativeSubSize = 15;
    public const int NarrativeBodySize = 18;
    public const int NarrativeBtnSize = 16;

    // --- 漂着箱モーダル ---
    public static readonly Vector2 DriftBoxPanelSize = new Vector2(440f, 260f);
    public const int DriftBoxTitleSize = 21;
    public const int DriftBoxAuthorSize = 14;
    public const int DriftBoxBodySize = 17;

    // --- 詩的ロアバナー ---
    public const float PoeticBannerW = 520f;
    public const float PoeticBannerH = 72f;
    public const int PoeticBannerFontSize = 17;

    // --- プロローグ／ダッシュ祝福 ---
    public const float PrologueBoardMaxW = 580f;
    public const float PrologueBoardMaxH = 260f;
    public const int PrologueTitleSize = 25;
    public const int PrologueBodySize = 18;
    public const int PrologueHintSize = 16;

    // --- タイトルメニュー（左物語・右メニュー） ---
    public const int TitleMenuStoryHeaderSize = 16;
    public const int TitleMenuStoryHeaderMinSize = 10;
    public const int TitleMenuStoryBodySize = 16;
    /// <summary>日本語は Best Fit で過度に縮むため固定表示（本文 16px）</summary>
    public const int TitleMenuStoryBodySizeJapanese = 16;
    public const int TitleMenuStoryBodyMinSize = 10;
    public const float TitleMenuStoryLineSpacing = 1.22f;
    public const float TitleMenuStoryLineSpacingJapanese = 1.16f;
    public const float TitleMenuStoryHeaderBandHeight = 38f;
    public const float TitleMenuStoryPanelPadding = 8f;

    /// <summary>英語・スペイン語のみ枠内に Best Fit。日・中は固定サイズ</summary>
    public static bool TitleMenuStoryBodyUsesBestFit(AdventureLocalizationManager.GameLanguage lang) =>
        lang is AdventureLocalizationManager.GameLanguage.English
            or AdventureLocalizationManager.GameLanguage.Spanish;

    public static int GetTitleMenuStoryBodyFixedSize(AdventureLocalizationManager.GameLanguage lang) =>
        lang == AdventureLocalizationManager.GameLanguage.Japanese
            ? TitleMenuStoryBodySizeJapanese
            : TitleMenuStoryBodySize;

    public static float GetTitleMenuStoryLineSpacing(AdventureLocalizationManager.GameLanguage lang) =>
        lang == AdventureLocalizationManager.GameLanguage.Japanese
            ? TitleMenuStoryLineSpacingJapanese
            : TitleMenuStoryLineSpacing;

    /// <summary>英語・スペイン語は行長が長いため Best Fit の上限を少し下げる</summary>
    public static int GetTitleMenuStoryBodyMaxSize(AdventureLocalizationManager.GameLanguage lang) =>
        lang is AdventureLocalizationManager.GameLanguage.English
            or AdventureLocalizationManager.GameLanguage.Spanish
            ? 14
            : TitleMenuStoryBodySize;
    public const int TitleMenuSectionHeaderSize = 16;
    public const int TitleMenuButtonPrimarySize = 18;
    public const int TitleMenuButtonSecondarySize = 17;
    public const int TitleMenuButtonTertiarySize = 16;

    // --- 下部シーン字幕 ---
    public const int SceneCaptionTitleSize = 18;
    public const int SceneCaptionMetaSize = 14;
    public const int SceneCaptionBodySize = 18;

    // --- レターボックス映画字幕（クライマックス注油・エピローグ） ---
    public const int CinematicFilmSubtitleSize = 38;

    // --- Rust IMGUI プロンプト ---
    public const int RustCenterPromptFontSize = 16;
}
