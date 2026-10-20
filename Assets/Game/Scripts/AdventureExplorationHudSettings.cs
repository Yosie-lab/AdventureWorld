using System;
using UnityEngine;

/// <summary>
/// 探索HUD（コンパス・パーツ回収・潤滑油）と操作ガイド行の縦レイアウト。
/// </summary>
public static class AdventureExplorationHudSettings
{
    public const string PrefShowCompass = "RustFloat_HudShowCompass";
    public const string PrefShowPartsBoard = "RustFloat_HudShowPartsBoard";
    public const string PrefShowOilBoard = "RustFloat_HudShowOilBoard";

    public const float TopInset = 8f;
    public const float RibbonHeight = 34f;
    public const float HeadingRowHeight = 20f;
    public const float NavRowHeight = 22f;
    public const float GuideRowHeight = 28f;
    public const float RowGap = 8f;
    public const float PartsBoardHeight = 58f;

    public static event Action OnChanged;

    public static bool ShowCompass
    {
        get => PlayerPrefs.GetInt(PrefShowCompass, 1) != 0;
        set => SetBool(PrefShowCompass, value);
    }

    public static bool ShowPartsBoard
    {
        get => PlayerPrefs.GetInt(PrefShowPartsBoard, 1) != 0;
        set => SetBool(PrefShowPartsBoard, value);
    }

    public static bool ShowOilBoard
    {
        get => PlayerPrefs.GetInt(PrefShowOilBoard, 1) != 0;
        set => SetBool(PrefShowOilBoard, value);
    }

    /// <summary>コンパス下の白文字ナビ行（パーツボード非表示時のみ）</summary>
    public static float GetCompassStackHeight(bool showCenterNavLine)
    {
        float h = TopInset + RibbonHeight + HeadingRowHeight;
        if (showCenterNavLine)
            h += RowGap * 0.5f + NavRowHeight;
        return h;
    }

    public static float GetGuideRowY(bool showCenterNavLine)
        => -(GetCompassStackHeight(showCenterNavLine) + RowGap);

    public static float GetPartsRowY(bool showCenterNavLine)
        => GetGuideRowY(showCenterNavLine) - GuideRowHeight - RowGap;

    public static float GetBannerRowY(bool showCenterNavLine)
        => GetPartsRowY(showCenterNavLine) - PartsBoardHeight - RowGap;

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
        bool showNav = AdventureScrapHUD.ComputeCenterNavVisible();
        ApplyLayout(showNav);
        AdventureScrapHUD.Instance?.RefreshExplorationHudVisibility();
        AdventureCompassHUD.Instance?.RefreshExplorationHudVisibility();
        AdventureRustFloatOpening.ApplyTopHudGuideLayout(showNav);
    }

    public static void ApplyLayout(bool showCenterNavLine)
    {
        var compass = AdventureCompassHUD.Instance
                      ?? UnityEngine.Object.FindAnyObjectByType<AdventureCompassHUD>(FindObjectsInactive.Include);
        if (compass != null)
            compass.ApplyLayoutPosition(showCenterNavLine);

        AdventureScrapHUD.Instance?.ApplyLayoutPositions(showCenterNavLine);
        AdventureRustFloatOpening.ApplyTopHudGuideLayout(showCenterNavLine);
    }
}
