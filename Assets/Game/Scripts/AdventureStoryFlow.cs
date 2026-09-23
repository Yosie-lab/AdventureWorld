using UnityEngine;

/// <summary>
/// RustAndFloat 進行の窓口（スタート→エンド）。
/// <para>
/// フェーズ順:
/// Opening → Prologue → Explore → Skybreak → Climax → Epilogue → Clear → FreeFlight
/// </para>
/// 進行の本体は各システム（Opening / Prologue / Tower partials）が持ち、
/// HUD・入力・BGM・カメラはここ経由で判定する。
/// </summary>
public static class AdventureStoryFlow
{
    public enum Phase
    {
        Opening = 0,
        Prologue = 1,
        Explore = 2,
        Skybreak = 3,
        Climax = 4,
        Epilogue = 5,
        Clear = 6,
        FreeFlight = 7
    }

    /// <summary>現在フェーズ。後段を優先（Clear &gt; Epilogue &gt; Climax &gt; Skybreak）。</summary>
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
                if (tower.IsEpiloguePlaying)
                    return Phase.Epilogue;
                if (tower.ClimaxCrisisStarted || tower.IsClimaxOilPromptActive)
                    return Phase.Climax;
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

    public static bool Is(Phase phase) => Current == phase;

    public static bool IsAtLeast(Phase phase) => (int)Current >= (int)phase;

    /// <summary>天蓋台本〜クリアまでのシネマ帯。</summary>
    public static bool IsEndingArc => IsAtLeast(Phase.Skybreak) && Current != Phase.FreeFlight;

    /// <summary>台本・注油・クライマックス・エピローグ・クリア。探索の雑談を止める。</summary>
    public static bool IsPerformance
    {
        get
        {
            var p = Current;
            return p == Phase.Skybreak
                || p == Phase.Climax
                || p == Phase.Epilogue
                || p == Phase.Clear;
        }
    }

    /// <summary>Rustの吹き出しを消す（台本・エピローグ・クリア）。</summary>
    public static bool HidesRustSpeech
    {
        get
        {
            var p = Current;
            return p == Phase.Skybreak || p == Phase.Epilogue || p == Phase.Clear;
        }
    }

    /// <summary>油HUDを消す。</summary>
    public static bool HidesOilHud => IsPerformance;

    /// <summary>下部バナー（台本・クライマックス・エピローグ・レバー前）。</summary>
    public static bool HidesBottomBanner
    {
        get
        {
            if (IsPerformance) return true;
            var tower = AdventureSanctuaryTowerManager.Instance;
            return tower != null && tower.IsPlayerNearLever;
        }
    }

    /// <summary>コンパス・クエストHUD・操作ガイドを消す（映画モード）。</summary>
    public static bool HidesExplorationHud => IsPerformance;

    /// <summary>Rustへの話しかけと、アイドル雑談。</summary>
    public static bool HidesRustInteraction
    {
        get
        {
            if (IsPerformance) return true;
            var tower = AdventureSanctuaryTowerManager.Instance;
            return tower != null && tower.EpilogueTriggered;
        }
    }

    /// <summary>カピタの贈り物プロンプト。</summary>
    public static bool HidesCapytaPrompt
    {
        get
        {
            var tower = AdventureSanctuaryTowerManager.Instance;
            if (tower == null) return false;
            if (tower.IsSkybreakModalActive || tower.IsClimaxOilPromptActive)
                return true;
            return tower.IsPlayerNearLever && tower.IsLeverReadyToOpen;
        }
    }

    /// <summary>光柱上昇の再ロックを拒否する（クライマックス以降）。</summary>
    public static bool BlocksPillarRelock => IsAtLeast(Phase.Climax);

    /// <summary>オープニングボードを出さない。天蓋後・クリア後・演出中・滑空中。</summary>
    public static bool ShouldSkipOpening
    {
        get
        {
            // Current 経由は IsModalBoardOpen → ここ の循環になるためフラグ直読み
            if (AdventureSanctuaryTowerManager.IsCanopyBroken || AdventureSanctuaryTowerManager.IsGameCleared)
                return true;
            var tower = AdventureSanctuaryTowerManager.Instance;
            if (tower != null && (
                tower.IsSkybreakModalActive
                || tower.IsEpiloguePlaying
                || tower.ClimaxCrisisStarted
                || tower.ShowGameClearModal
                || tower.EpilogueTriggered))
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
            return Current == Phase.FreeFlight || Current == Phase.Explore;
        }
    }

    /// <summary>テレポート時に地上へ吸着しない。光柱・滑空・クライマックス・エピローグ。</summary>
    public static bool KeepsAirborne(AdventurePlayerController player)
    {
        if (player != null && (player.IsSkybreakPillarAscending || player.IsAutoGliding))
            return true;
        var p = Current;
        return p == Phase.Climax || p == Phase.Epilogue;
    }

    /// <summary>地上に戻ってもシネマカメラを維持する。</summary>
    public static bool HoldCinematicCamera
    {
        get
        {
            var p = Current;
            return p == Phase.Climax || p == Phase.Epilogue || p == Phase.Clear;
        }
    }

    /// <summary>マウスを画面に出す。オープニング、レバー、台本、注油、クリア、プロローグ注油。</summary>
    public static bool WantsFreeCursor
    {
        get
        {
            var p = Current;
            if (p == Phase.Opening || p == Phase.Skybreak || p == Phase.Clear)
                return true;
            if (p == Phase.Climax)
            {
                var tower = AdventureSanctuaryTowerManager.Instance;
                if (tower != null && tower.IsClimaxOilPromptActive)
                    return true;
            }
            var towerNear = AdventureSanctuaryTowerManager.Instance;
            if (towerNear != null && towerNear.IsPlayerNearLever)
                return true;
            return AdventurePrologueDrama.Instance != null && AdventurePrologueDrama.Instance.IsWaitingForOil;
        }
    }
}
