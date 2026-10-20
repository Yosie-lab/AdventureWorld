using UnityEngine;

/// <summary>
/// 探索HUDの「詳細ナビ行」（最寄り m・ボックス・塔）の表示窓。
/// Tab で固定表示、Q 案内で 10 秒間パルス。
/// </summary>
public static class AdventureExplorationHudDetail
{
    public const float GuidePulseSeconds = 10f;

    static bool _pinnedByTab;
    static float _visibleUntil;

    public static bool PinnedByTab => _pinnedByTab;

    public static bool IsDetailNavVisible()
    {
        if (AdventureStoryFlow.HidesExplorationHud)
            return false;
        if (Time.time < _visibleUntil)
            return true;
        return _pinnedByTab && AdventureExplorationHudSettings.ShowDetailNav;
    }

    public static void ToggleTabPin()
    {
        if (!AdventureExplorationHudSettings.ShowDetailNav)
            return;
        _pinnedByTab = !_pinnedByTab;
        AdventureExplorationHudSettings.ApplyAll();
        AdventureScrapHUD.Instance?.RefreshExplorationHudVisibility();
    }

    public static void PulseFromGuide(float seconds = GuidePulseSeconds)
    {
        _visibleUntil = Mathf.Max(_visibleUntil, Time.time + seconds);
        AdventureExplorationHudSettings.ApplyAll();
    }

    public static void ClearForNewGame()
    {
        _pinnedByTab = false;
        _visibleUntil = 0f;
    }
}
