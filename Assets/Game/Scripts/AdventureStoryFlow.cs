using UnityEngine;

/// <summary>
/// ゲームスタートからエンドまでの現在地。
/// オープニング・プロローグ・探索・天蓋・クライマックス・エピローグ・クリアは
/// 既存の各フラグから導出する。進行の本体は各システムが持ち、ここは判定の窓口。
/// </summary>
public static class AdventureStoryFlow
{
    public enum Phase
    {
        Opening,
        Prologue,
        Explore,
        Skybreak,
        Climax,
        Epilogue,
        Clear,
        FreeFlight
    }

    public static Phase Current
    {
        get
        {
            var opening = AdventureRustFloatOpening.Instance;
            if (opening != null && opening.IsModalBoardOpen())
                return Phase.Opening;

            var tower = AdventureSanctuaryTowerManager.Instance;
            if (tower != null)
            {
                if (tower.ShowGameClearModal)
                    return Phase.Clear;
                if (tower.ClimaxCrisisStarted || tower.IsClimaxOilPromptActive)
                    return Phase.Climax;
                if (tower.EpilogueTriggered)
                    return Phase.Epilogue;
                if (tower.IsSkybreakModalActive)
                    return Phase.Skybreak;
            }

            if (AdventurePrologueDrama.Instance != null && AdventurePrologueDrama.Instance.IsPrologueActive)
                return Phase.Prologue;
            if (AdventureSanctuaryTowerManager.IsGameCleared)
                return Phase.FreeFlight;
            return Phase.Explore;
        }
    }

    /// <summary>台本・注油・クライマックス・エピローグ・クリア。探索の雑談を止める。</summary>
    public static bool IsPerformance
    {
        get
        {
            var tower = AdventureSanctuaryTowerManager.Instance;
            if (tower == null) return false;
            return tower.IsSkybreakModalActive
                || tower.IsClimaxOilPromptActive
                || tower.IsEpiloguePlaying
                || tower.EpilogueTriggered
                || tower.ShowGameClearModal
                || tower.ClimaxCrisisStarted;
        }
    }

    /// <summary>Rustの吹き出しを消す（台本・エピローグ・クリア）。</summary>
    public static bool HidesRustSpeech
    {
        get
        {
            var tower = AdventureSanctuaryTowerManager.Instance;
            return tower != null && (
                tower.IsSkybreakModalActive
                || tower.IsEpiloguePlaying
                || tower.ShowGameClearModal);
        }
    }

    /// <summary>油HUDを消す。</summary>
    public static bool HidesOilHud
    {
        get
        {
            var tower = AdventureSanctuaryTowerManager.Instance;
            return tower != null && (
                tower.IsSkybreakModalActive
                || tower.IsEpiloguePlaying
                || tower.ShowGameClearModal
                || tower.IsClimaxOilPromptActive
                || tower.ClimaxCrisisStarted);
        }
    }

    /// <summary>下部バナー（台本・クライマックス・エピローグ・レバー前）。</summary>
    public static bool HidesBottomBanner
    {
        get
        {
            var tower = AdventureSanctuaryTowerManager.Instance;
            if (tower == null) return false;
            return tower.IsSkybreakModalActive
                || tower.IsPlayerNearLever
                || tower.ClimaxCrisisStarted
                || tower.IsEpiloguePlaying
                || tower.ShowGameClearModal;
        }
    }

    /// <summary>コンパス・クエストHUD・操作ガイドを消す（映画モード）。</summary>
    public static bool HidesExplorationHud => IsPerformance;

    /// <summary>Rustへの話しかけと、アイドル雑談。</summary>
    public static bool HidesRustInteraction
    {
        get
        {
            var tower = AdventureSanctuaryTowerManager.Instance;
            if (tower == null) return false;
            return IsPerformance || tower.EpilogueTriggered;
        }
    }

    /// <summary>カピタの贈り物プロンプト。</summary>
    public static bool HidesCapytaPrompt
    {
        get
        {
            var tower = AdventureSanctuaryTowerManager.Instance;
            return tower != null && (
                tower.IsSkybreakModalActive
                || tower.IsClimaxOilPromptActive
                || (tower.IsPlayerNearLever && tower.IsLeverReadyToOpen));
        }
    }

    /// <summary>光柱上昇の再ロックを拒否する（クライマックス以降）。</summary>
    public static bool BlocksPillarRelock
    {
        get
        {
            var tower = AdventureSanctuaryTowerManager.Instance;
            return tower != null && (
                tower.ClimaxCrisisStarted
                || tower.ShowGameClearModal
                || tower.EpilogueTriggered);
        }
    }

    /// <summary>オープニングボードを出さない。天蓋後・クリア後・演出中・滑空中。</summary>
    public static bool ShouldSkipOpening
    {
        get
        {
            if (AdventureSanctuaryTowerManager.IsCanopyBroken || AdventureSanctuaryTowerManager.IsGameCleared)
                return true;
            var tower = AdventureSanctuaryTowerManager.Instance;
            if (tower != null && (tower.IsEpiloguePlaying || tower.ClimaxCrisisStarted || tower.ShowGameClearModal))
                return true;
            var player = AdventurePlayerController.Instance;
            return player != null && (player.IsSkybreakPillarAscending || player.IsAutoGliding);
        }
    }

    /// <summary>クリア後の自由探索で、天空BGMではなく探索曲に戻す。</summary>
    public static bool ShouldUseExplorationTheme
    {
        get
        {
            if (!AdventureSanctuaryTowerManager.IsGameCleared)
                return false;
            var tower = AdventureSanctuaryTowerManager.Instance;
            if (tower == null) return true;
            return !tower.ShowGameClearModal
                && !tower.IsEpiloguePlaying
                && !tower.IsSkybreakModalActive
                && !tower.ClimaxCrisisStarted;
        }
    }

    /// <summary>テレポート時に地上へ吸着しない。光柱・滑空・クライマックス・エピローグ。</summary>
    public static bool KeepsAirborne(AdventurePlayerController player)
    {
        if (player != null && (player.IsSkybreakPillarAscending || player.IsAutoGliding))
            return true;
        var tower = AdventureSanctuaryTowerManager.Instance;
        return tower != null && (
            tower.ClimaxCrisisStarted
            || tower.EpilogueTriggered
            || tower.IsEpiloguePlaying);
    }

    /// <summary>地上に戻ってもシネマカメラを維持する。</summary>
    public static bool HoldCinematicCamera
    {
        get
        {
            var tower = AdventureSanctuaryTowerManager.Instance;
            if (tower == null) return true;
            return tower.IsEpiloguePlaying || tower.ClimaxCrisisStarted || tower.ShowGameClearModal;
        }
    }

    /// <summary>マウスを画面に出す。オープニング、レバー、台本、注油、クリア、プロローグ注油。</summary>
    public static bool WantsFreeCursor
    {
        get
        {
            var opening = AdventureRustFloatOpening.Instance;
            if (opening != null && opening.IsModalBoardOpen())
                return true;
            var tower = AdventureSanctuaryTowerManager.Instance;
            if (tower != null && (
                tower.IsPlayerNearLever
                || tower.IsSkybreakModalActive
                || tower.IsClimaxOilPromptActive
                || tower.ShowGameClearModal))
                return true;
            return AdventurePrologueDrama.Instance != null && AdventurePrologueDrama.Instance.IsWaitingForOil;
        }
    }
}
