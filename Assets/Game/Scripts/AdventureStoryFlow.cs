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

    public static event System.Action<Phase, Phase> OnPhaseChanged;

    static Phase _lastPhase = Phase.Explore;
    static int _lastPhaseFrame = -1;
    static Phase _cachedPhase = Phase.Explore;

    /// <summary>現在フェーズ。後段を優先（Clear &gt; Epilogue &gt; Climax &gt; Skybreak）。同フレーム内はキャッシュを返し負荷を抑制。</summary>
    public static Phase Current
    {
        get
        {
            if (_lastPhaseFrame == Time.frameCount)
                return _cachedPhase;

            Phase nextPhase = ResolveCurrentPhase();
            _lastPhaseFrame = Time.frameCount;
            _cachedPhase = nextPhase;

            if (_lastPhase != nextPhase)
            {
                Phase prev = _lastPhase;
                _lastPhase = nextPhase;
                try
                {
                    HandlePhaseExit(prev, nextPhase);
                    HandlePhaseEnter(nextPhase, prev);
                    OnPhaseChanged?.Invoke(prev, nextPhase);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[AdventureStoryFlow] OnPhaseChanged 例外: {ex.Message}");
                }
            }

            return nextPhase;
        }
    }

    /// <summary>キャッシュを即座に破棄し、次回の参照で強制再評価させる</summary>
    public static void InvalidateCache()
    {
        _lastPhaseFrame = -1;
    }

    /// <summary>ニューゲーム初期化時にストーリーフロー状態をリセット</summary>
    public static void ResetFlowToNewGame()
    {
        _lastPhase = Phase.Opening;
        _cachedPhase = Phase.Opening;
        _lastPhaseFrame = -1;
    }

    /// <summary>前フェーズ退出時の自動クリーンアップ</summary>
    static void HandlePhaseExit(Phase exiting, Phase entering)
    {
        switch (exiting)
        {
            case Phase.Opening:
                // オープニング終了時の入力ガード解消
                if (AdventureRustFloatOpening.IsInputGuarded)
                    AdventureRustFloatOpening.DismissInputGuard();
                break;
            case Phase.Epilogue:
                // エピローグ映画終了時の環境音・字幕クリーンアップ
                var tower = AdventureSanctuaryTowerManager.Instance;
                if (tower != null)
                {
                    tower.StopEpilogueSeagullAmbience();
                }
                break;
            case Phase.Clear:
                // クリアモーダル退出時の処理
                break;
        }
    }

    /// <summary>新フェーズ突入時の自動セットアップ</summary>
    static void HandlePhaseEnter(Phase entering, Phase previous)
    {
        switch (entering)
        {
            case Phase.Explore:
                // 自由探索復帰時に穏やかな探索テーマ曲を確実に復旧
                if (previous == Phase.Clear || previous == Phase.FreeFlight)
                {
                    AdventureMusicDirector.Ensure();
                    AdventureMusicDirector.Instance?.RestoreExplorationTheme();
                }
                break;
        }
    }

    static Phase ResolveCurrentPhase()
    {
        // 1. 時代背景オープニングボード表示中
        var opening = AdventureRustFloatOpening.Instance;
        if (opening != null && opening.IsModalBoardOpen())
            return Phase.Opening;

        // 2. 遭難目覚め・Rust手当（プロローグ）中
        if (AdventurePrologueDrama.Instance != null && AdventurePrologueDrama.Instance.IsPrologueActive)
            return Phase.Prologue;

        var tower = AdventureSanctuaryTowerManager.Instance;
        if (tower != null)
        {
            // 3. ゲームクリアリザルト画面
            if (tower.ShowGameClearModal)
                return Phase.Clear;

            // 4. エピローグ字幕映画再生中（※すでにクリア済みの場合やClearからの逆流は防護）
            if (tower.IsEpiloguePlaying && _lastPhase != Phase.Clear && !AdventureSanctuaryTowerManager.IsGameCleared)
                return Phase.Epilogue;

            // 5. クライマックス（限界高度・凍結危機・ラスト注油）
            if (tower.ClimaxCrisisStarted || tower.IsClimaxOilPromptActive)
                return Phase.Climax;

            // 6. 天蓋台本中、およびダイブ後の光の柱上昇中・クライマックス保留中も確実に Skybreak を維持
            if (tower.IsSkybreakModalActive || tower.IsPendingClimax || tower.EndingSequenceActive)
                return Phase.Skybreak;
        }

        var player = AdventurePlayerController.Instance;
        if (player != null && player.IsSkybreakPillarAscending)
            return Phase.Skybreak;

        // 7. クリア後の大空遊覧
        if (AdventureSanctuaryTowerManager.IsGameCleared)
            return Phase.FreeFlight;

        // 8. 通常探索
        return Phase.Explore;
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

    /// <summary>Rustの吹き出しを消す（オープニング・台本・エピローグ・クリア）。</summary>
    public static bool HidesRustSpeech
    {
        get
        {
            var p = Current;
            if (p == Phase.Opening || p == Phase.Skybreak || p == Phase.Epilogue || p == Phase.Clear)
                return true;
            var opening = AdventureRustFloatOpening.Instance;
            if (opening != null && opening.IsModalBoardOpen())
                return true;
            return false;
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

    /// <summary>【E】Inspect・会話案内・工房近接など近接プロンプト板を出さない（キー操作は有効）。</summary>
    public static bool HideWorldInteractPrompts => true;

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
                || tower.ShowGameClearModal))
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

    /// <summary>マウスを画面に出す。ポーズメニュー、着せ替え工房、オープニング、レバー、台本、注油、クリア、手記モーダル等。</summary>
    public static bool WantsFreeCursor
    {
        get
        {
            // ポーズメニュー
            if (AdventurePauseMenu.IsOpen)
                return true;

            // Rust着せ替え工房UI
            if (AdventureRustWorkshopUI.IsOpen)
                return true;

            // 漂流物木箱の手記モーダル
            if (AdventureBeachDriftBox.IsModalOpen)
                return true;

            // 海岸日誌・石碑ナラティブモーダル
            if (AdventureBeachNarrativeManager.Instance != null && AdventureBeachNarrativeManager.Instance.IsShowingModal)
                return true;

            // オープニングストーリーボード
            var opening = AdventureRustFloatOpening.Instance;
            if (opening != null && opening.IsModalBoardOpen())
                return true;

            // 聖域の塔／クリア画面／台本モーダル（※レバー接近中は視点をロックせずEキー操作を維持）
            var tower = AdventureSanctuaryTowerManager.Instance;
            if (tower != null)
            {
                if (tower.IsSkybreakModalActive || tower.ShowGameClearModal || tower.IsClimaxOilPromptActive)
                    return true;
            }

            // プロローグ：ダッシュ解禁キーストーンボード表示中のみカーソル解放
            if (AdventurePrologueDrama.Instance != null && AdventurePrologueDrama.Instance.IsShowingDashBoard)
                return true;

            return false;
        }
    }
}
