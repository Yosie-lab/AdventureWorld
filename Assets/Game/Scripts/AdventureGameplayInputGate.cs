using UnityEngine;

/// <summary>
/// プレイヤー操作が止まる条件を一元化し、字幕のみの場面では歩行を維持する。
/// 残留フラグ・timeScale・入力ガードのウォッチドッグもここで処理する。
/// </summary>
public static class AdventureGameplayInputGate
{
    public enum LocomotionBlock
    {
        None = 0,
        TitleBoard,
        InputGuard,
        PrologueAwakening,
        PauseMenu,
        SkybreakScriptHold,
    }

    /// <summary>毎フレーム先頭で呼ぶ。古いブロック状態を自動復旧する。</summary>
    public static void TickRecovery()
    {
        if (AdventureRustFloatOpening.IsGameStarted && AdventureRustFloatOpening.IsInputGuarded)
            AdventureRustFloatOpening.DismissInputGuard();

        if (AdventureRustDrone.ScrapSlowMoDepth > 0
            && (AdventureInputReader.HasAnyMove || AdventureInputReader.HasMouseLookIntent))
            AdventureRustDrone.ForceEndScrapSlowMo();

        AdventureRustDrone.RecoverScrapSlowMoIfStale();

        if (!AdventurePauseMenu.IsOpen && AdventureRustFloatOpening.IsGameStarted)
        {
            if (Time.timeScale <= 0.05f)
                Time.timeScale = 1f;
            else if (AdventureRustDrone.ScrapSlowMoDepth == 0
                     && Time.timeScale > 0.05f && Time.timeScale < 0.5f)
                Time.timeScale = 1f;
        }

        if (AdventureScrapManager.IsWithinPickupInputGrace
            && !AdventureStoryFlow.WantsFreeCursor
            && !AdventurePauseMenu.IsOpen)
            TryLockCursorForGameplay();

        if (AdventureRustFloatOpening.IsGameStarted
            && !AdventureStoryFlow.WantsFreeCursor
            && !AdventurePauseMenu.IsOpen
            && (AdventureInputReader.HasAnyMove || AdventureInputReader.HasMouseLookIntent)
            && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        var narrative = AdventureBeachNarrativeManager.Instance;
        if (narrative != null)
            narrative.SyncModalFlagWithCaption();

        AdventureBeachDriftBox.SyncModalFlagWithCaption();

        var campfire = AdventureBeachCampfire.Instance;
        var player = AdventurePlayerController.Instance;
        if (campfire != null && player != null && !campfire.IsSitting && !player.enabled)
            player.enabled = true;
    }

    /// <summary>オープニング／プロローグの入力ガードを処理。true ならこのフレーム以降の Update を打ち切る。</summary>
    public static bool TryEarlyExitForTitleOrPrologue()
    {
        var opening = AdventureRustFloatOpening.Instance;
        if (opening != null && opening.IsModalBoardOpen())
            return true;

        if (opening != null && AdventureRustFloatOpening.IsInputGuarded)
        {
            if (AdventureStoryFlow.ShouldSkipOpening)
                opening.ForceDismissForGameplay();
            else if (AdventureInputReader.HasAnyMove)
                AdventureRustFloatOpening.DismissInputGuard();
            else
                return true;
        }

        var prologue = AdventurePrologueDrama.Instance;
        if (prologue != null && prologue.IsAwakening)
        {
            if (AdventureInputReader.HasAnyMove || AdventureInputReader.DialogAdvanceDown)
                prologue.SkipAwakening();
            else
                return true;
        }

        return false;
    }

    public static LocomotionBlock GetLocomotionBlock(out bool skybreakJumpOnly)
    {
        skybreakJumpOnly = false;

        if (AdventurePauseMenu.IsOpen)
            return LocomotionBlock.PauseMenu;

        // 天蓋台本中も歩行・空中操縦・滑空は維持（Space/クリックは HandleJump 側で台本送りへ回す）
        return LocomotionBlock.None;
    }

    /// <summary>Space/J を字幕の「閉じる」に使う場面（ジャンプに回さない）</summary>
    public static bool ShouldRouteJumpToCaptionDismiss =>
        AdventureSceneCaption.IsWaitingDismiss && AdventureSceneCaption.IsVisible;

    /// <summary>クリック取得後など、探索中はカーソルをロックして WASD を即効かせる</summary>
    public static void TryLockCursorForGameplay()
    {
        if (!AdventureRustFloatOpening.IsGameStarted) return;
        if (AdventureStoryFlow.WantsFreeCursor || AdventurePauseMenu.IsOpen) return;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
