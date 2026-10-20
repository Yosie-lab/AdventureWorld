using System;
using UnityEngine;

/// <summary>
/// 探索HUD（コンパス・パーツ回収・潤滑油）と操作ガイド行の縦レイアウト。
/// </summary>
public static class AdventureExplorationHudSettings
{
    public const string PrefShowCompass = "RustFloat_HudShowCompass";
    public const string PrefShowPartsBoard = "RustFloat_HudShowPartsBoard"; // 詳細ナビ（Tab）の許可
    public const string PrefShowOilBoard = "RustFloat_HudShowOilBoard";

    public const float TopInset = 8f;
    public const float RibbonHeight = 34f;
    public const float HeadingRowHeight = 20f;
    public const float GoalRowHeight = 20f;
    public const float NavRowHeight = 22f;
    public const float GuideRowHeight = 28f;
    public const float RowGap = 8f;
    public const float PartsBoardHeight = 58f;

    /// <summary>上部コンパス＋目標行の背景プレート不透明度（低いほど透ける）</summary>
    public const float CompassPlateAlpha = 0.54f;
    public const float CompassBorderTopAlpha = 0.28f;
    public const float CompassBorderBottomAlpha = 0.42f;
    public const float CompassGoalLineTextAlpha = 0.78f;
    public const float CompassHeadingTextAlpha = 0.76f;
    public const float CompassDetailNavTextAlpha = 0.80f;

    public static event Action OnChanged;

    public static bool ShowCompass
    {
        get => PlayerPrefs.GetInt(PrefShowCompass, 1) != 0;
        set => SetBool(PrefShowCompass, value);
    }

    /// <summary>Tab で詳細ナビ行を固定表示できるか（Q の 10 秒表示は別途常に有効）</summary>
    public static bool ShowDetailNav
    {
        get => PlayerPrefs.GetInt(PrefShowPartsBoard, 1) != 0;
        set => SetBool(PrefShowPartsBoard, value);
    }

    [System.Obsolete("Use ShowDetailNav")]
    public static bool ShowPartsBoard
    {
        get => ShowDetailNav;
        set => ShowDetailNav = value;
    }

    public static bool ShowOilBoard
    {
        get => PlayerPrefs.GetInt(PrefShowOilBoard, 1) != 0;
        set => SetBool(PrefShowOilBoard, value);
    }

    /// <summary>画面上部スタック（常時: コンパス＋方角＋目標1行／任意: 詳細ナビ行）</summary>
    public static float GetCompassStackHeight(bool showDetailNavLine)
    {
        float h = TopInset + RibbonHeight + HeadingRowHeight + RowGap * 0.35f + GoalRowHeight;
        if (showDetailNavLine)
            h += RowGap * 0.5f + NavRowHeight;
        return h;
    }

    /// <summary>詳細行を含めない高さ（油カウンター等の固定配置用）</summary>
    public static float GetCompassStackHeightStable()
        => GetCompassStackHeight(showDetailNavLine: false);

    public static float GetGuideRowY(bool showDetailNavLine)
        => -(GetCompassStackHeight(showDetailNavLine) + RowGap);

    public static float GetSideHudRowY()
        => -(GetCompassStackHeightStable() + RowGap);

    public static float GetPartsRowY(bool showDetailNavLine)
        => GetSideHudRowY();

    public static float GetBannerRowY(bool showDetailNavLine)
        => GetSideHudRowY() - PartsBoardHeight - RowGap;

    static void SetBool(string key, bool value)
    {
        int v = value ? 1 : 0;
        if (PlayerPrefs.GetInt(key, 1) == v)
            return;
        PlayerPrefs.SetInt(key, v);
        PlayerPrefs.Save();
        ApplyAll();
        OnChanged?.Invoke();
    }

    public static void ApplyAll()
    {
        bool showDetail = AdventureScrapHUD.ComputeDetailNavVisible();
        ApplyLayout(showDetail);
        AdventureScrapHUD.Instance?.RefreshExplorationHudVisibility();
        AdventureCompassHUD.Instance?.RefreshExplorationHudVisibility();
        AdventureRustFloatOpening.ApplyTopHudGuideLayout(showDetail);
    }

    public static void ApplyLayout(bool showDetailNavLine)
    {
        var compass = AdventureCompassHUD.Instance
                      ?? UnityEngine.Object.FindAnyObjectByType<AdventureCompassHUD>(FindObjectsInactive.Include);
        if (compass != null)
            compass.ApplyLayoutPosition(showDetailNavLine);

        AdventureScrapHUD.Instance?.ApplyLayoutPositions(showDetailNavLine);
        AdventureRustFloatOpening.ApplyTopHudGuideLayout(showDetailNavLine);
    }
}
