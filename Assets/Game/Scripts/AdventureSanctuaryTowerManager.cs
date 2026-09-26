using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 『Rust & Float』中央タワー統括。
/// 進行パイプライン（判定窓口は <see cref="AdventureStoryFlow"/>）:
/// Opening → Prologue → Explore → レバー →
/// Canopy.cs（天蓋台本）→ Climax.cs（注油危機）→ Epilogue.cs（映画〜クリア）→ FreeFlight
/// </summary>
public partial class AdventureSanctuaryTowerManager : MonoBehaviour
{
    #region 1. プロパティ・定数・状態変数

    static AdventureSanctuaryTowerManager _instance;
    public static AdventureSanctuaryTowerManager Instance => _instance;

    static bool _isCanopyBroken = false;
    const string PrefKeyCanopyBroken = "RustAndFloat_CanopyBroken";
    /// <summary>F9確認中はセーブ済み天蓋開放を無視（警告クライマックスに飛ばない）</summary>
    static bool _ignoreSavedCanopyState = false;

    public static bool IsCanopyBroken
    {
        get
        {
            if (_ignoreSavedCanopyState)
                return false;
            return _isCanopyBroken || PlayerPrefs.GetInt(PrefKeyCanopyBroken, 0) == 1;
        }
        set
        {
            _isCanopyBroken = value;
            PlayerPrefs.SetInt(PrefKeyCanopyBroken, value ? 1 : 0);
            PlayerPrefs.Save();
            if (value)
                _ignoreSavedCanopyState = false;
        }
    }

    static bool _isGameCleared = false;
    const string PrefKeyGameCleared = "RustAndFloat_GameCleared";
    public static bool IsGameCleared
    {
        get
        {
            if (_ignoreSavedCanopyState)
                return false;
            return _isGameCleared || PlayerPrefs.GetInt(PrefKeyGameCleared, 0) == 1;
        }
        set
        {
            _isGameCleared = value;
            PlayerPrefs.SetInt(PrefKeyGameCleared, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public const float TerraceTopY = 64.02f;

    Transform _leverHandle;
    Light _leverLight;
    readonly System.Collections.Generic.List<Transform> _allLeverHandles = new System.Collections.Generic.List<Transform>();
    readonly System.Collections.Generic.List<Light> _allLeverLights = new System.Collections.Generic.List<Light>();
    ParticleSystem _crackPs;
    GameObject _hyperUpdraftGo;
    AudioSource _audio;
    AudioSource _skybreakWindSource;
    AudioClip _skybreakWindClip;

    bool _leverPulled = false;
    bool _endingSequenceActive = false;
    bool _playerNearby = false;
    bool _epilogueTriggered = false;
    float _epilogueAlpha = 0f;
    int _epilogueAct = 0; // 0=帯のみ / 1〜3=字幕幕
    bool _showGameClearModal = false;
    Coroutine _epilogueRoutine;
    Font _epilogueFont;

    // Update駆動のシネマ字幕（コルーチン停止に依存しない）
    int _filmIndex = -1;
    int _filmPhase; // 0=なし / 1=fadeIn / 2=hold / 3=fadeOut / 4=幕あい
    float _filmPhaseAt;
    float _filmHoldSec;
    float _filmFadeInSec = 0.35f;
    float _filmFadeOutSec = 0.4f;
    Color _filmColor;
    int _filmLastAct = -1;
    float _epilogueStartedAt;

    // ── キャッシュ参照（毎フレームFind廃止） ──
    AdventurePlayerController _cachedPlayer;
    AdventureRustDrone _cachedDrone;
    UnityEngine.UI.Text _cachedGuideText; // SetExplorationHudVisible 用
    GameObject _letterboxRoot;
    Image _letterboxTop;
    Image _letterboxBottom;
    CanvasGroup _letterboxCg;
    Text _filmSubtitleUi;
    float _letterboxTargetAlpha;

    AdventurePlayerController GetPlayer()
    {
        if (_cachedPlayer == null)
            _cachedPlayer = AdventurePlayerController.Instance
                            ?? Object.FindFirstObjectByType<AdventurePlayerController>();
        return _cachedPlayer;
    }

    AdventureRustDrone GetDrone()
    {
        if (_cachedDrone == null)
            _cachedDrone = AdventureRustDrone.Instance
                           ?? Object.FindFirstObjectByType<AdventureRustDrone>();
        return _cachedDrone;
    }

    // ── 台本ボード（1枚ずつ確実に表示）※uGUI：OnGUIだと日本語が空になるため ──
    bool _scriptBoardVisible = false;
    bool _scriptBoardAdvance = false;
    bool _scriptAdvanceArmed = false; // キー／ボタンを一度離してから次入力を受け付ける
    bool _scriptPrevPointerDown = false;
    bool _scriptPrevKeyDown = false;
    string _scriptBoardTitle = "";
    string _scriptBoardSpeaker = "";
    string _scriptBoardBody = "";
    Color _scriptBoardAccent = new Color(0.45f, 0.92f, 1f, 1f);
    bool _scriptBoardIsDive = false;
    float _scriptBoardOpenedAt = 0f;
    float _scriptHoldTimer = 0f;

    // Update駆動の天蓋台本（コルーチンが死んでも進む）
    int _canopyBeatIndex = -1;
    bool _skyTearPlayed;
    struct CanopyBeat
    {
        public string Title;
        public string Speaker;
        public string Body;
        public Color Accent;
        public bool IsDive;
        public CanopyBeat(string title, string speaker, string body, Color accent, bool isDive = false)
        {
            Title = title; Speaker = speaker; Body = body; Accent = accent; IsDive = isDive;
        }
    }
    static readonly CanopyBeat[] CanopyBeats =
    {
        new CanopyBeat("天蓋崩壊　未知の荒野への跳躍", "", "空が割れた。\n冷たいリアルな風が頬を打つ。", new Color(1f, 0.9f, 0.45f, 1f)),
        new CanopyBeat("", "✦ 相棒 Rust", "この楽園もAIに最適化された虚構の島だったんだ!!", new Color(0.35f, 0.92f, 0.98f, 1f)),
        new CanopyBeat("", "✦ 相棒 Rust", "空が……割れるよ、Niko！　つかまって！！", new Color(0.35f, 0.92f, 0.98f, 1f)),
        new CanopyBeat("", "✦ Niko", "ありがとうRust…！あなたのおかげでここまでたどり着くことができた。", new Color(1f, 0.88f, 0.45f, 1f)),
        new CanopyBeat("", "✦ 相棒 Rust", "あれが本物の空だ……！風に乗って、あの裂け目へ飛び込もう、Niko！！", new Color(0.35f, 0.92f, 0.98f, 1f)),
        new CanopyBeat("", "", "タワー中央の光の柱へ飛び込み、\n空の裂け目へ突き抜ける。", new Color(0.85f, 0.95f, 1f, 1f)),
        new CanopyBeat("空の裂け目へ", "", "【Space長押し / クリック】でダイブする", new Color(1f, 0.88f, 0.4f, 1f), true),
    };

    GameObject _scriptUiRoot;
    Image _scriptDimImg;
    Image _scriptPanelImg;
    Image _scriptAccentImg;
    Text _scriptTitleUi;
    Text _scriptSpeakerUi;
    Text _scriptBodyUi;
    Text _scriptHintUi;
    Button _scriptBtn;

    // 注油プロンプト（uGUI専用・IMGUI禁止＝Gizmos文字化け防止）
    GameObject _oilUiRoot;
    Image _oilGaugeFill;
    Text _oilTitleUi;
    Text _oilPromptUi;
    Text _oilHoldLabelUi;
    Button _oilHoldBtn;

    // クリアモーダルも uGUI 専用（IMGUIだと空ボード＋「Gizmos」ボタンになる）
    GameObject _clearUiRoot;
    RectTransform _clearDiveBtn;
    RectTransform _clearNewBtn;
    RectTransform _clearCloseBtn;
    int _clearModalActionFrame = -1;

    public bool IsSkybreakModalActive => _scriptBoardVisible;
    public bool IsDiveBoardActive => _scriptBoardVisible && _scriptBoardIsDive;
    bool _showSkybreakModal
    {
        get => _scriptBoardVisible;
        set => _scriptBoardVisible = value;
    }
    bool _skybreakModalClosed;

    // ── 【案1】クライマックス演出制御 ──
    bool _climaxCrisisStarted = false;
    bool _climaxOilInjected = false;
    bool _climaxOilWaiting = false;
    float _oilHoldTimer = 0f;
    float _oilWaitOpenedAt = 0f;
    const float OilHoldRequired = 0.7f;
    /// <summary>注油長押しの持ち越しで次台本を即スキップしないよう、一度離すまで送り不可</summary>
    bool _scriptRequireInputRelease = false;
    int _climaxBeatIndex = -1; // -1=非アクティブ / 0..=台本 / OilPhaseIndex=注油待ち後の再開用
    float _climaxOverdriveCinematicUntil = 0f;
    int _climaxPostOilPhase = 0; // 0=なし / 1=蘇生セリフ待ち / 2=全出力セリフ待ち
    float _climaxPostOilUntil = 0f;
    /// <summary>F9／天蓋台本中はクライマックス（警告・注油）を絶対に開始・表示しない</summary>
    bool _suppressClimax = false;
    /// <summary>天蓋台本完了後、柱上昇を待ってクライマックスへ必ず接続する</summary>
    bool _pendingClimaxAfterCanopy = false;
    float _pendingClimaxDeadline = 0f;

    public bool ClimaxCrisisStarted => _climaxCrisisStarted;
    public bool ClimaxOilInjected => _climaxOilInjected;
    public bool EpilogueTriggered => _epilogueTriggered;
    public float EpilogueAlpha => _epilogueAlpha;
    public bool ShowGameClearModal => _showGameClearModal;
    /// <summary>エピローグ字幕／台本ボード表示中（他HUD・セリフを抑止するため）</summary>
    public bool IsEpiloguePlaying =>
        _epilogueTriggered && !_showGameClearModal;
    /// <summary>クライマックス注油待ち中</summary>
    public bool IsClimaxOilPromptActive =>
        _climaxOilWaiting && !_climaxOilInjected && !_suppressClimax;

    // 注油前台本(0-2) → 注油 → 注油後台本(3-4)
    const string SkyLimitWarning = "✦ 限界高度：凍結危機 ✦";
    const int ClimaxOilSlot = 3; // next==3 のとき注油フェーズへ入る
    static readonly CanopyBeat[] ClimaxBeats =
    {
        new CanopyBeat("限界高度", "", "天蓋の裂け目から、凍てつく突風が吹き荒れる——\nRustのギアが、悲鳴のようなきしみ音を上げていた。", new Color(0.85f, 0.92f, 1f, 1f)),
        new CanopyBeat("", "✦ 相棒 Rust", "ギギッ……！ Niko……身体が……冷え切って動かないよ……！\nギアが……凍りついちゃう……！", new Color(0.45f, 0.92f, 1f, 1f)),
        new CanopyBeat("", "✦ Niko", "Rust、待ってて！　今、集めた油を全部注ぐから……！", new Color(1f, 0.92f, 0.55f, 1f)),
        // ← ここで注油フェーズ
        new CanopyBeat("", "✦ 相棒 Rust", "……あ……温かい油が……心臓部に……じわっと染み込んでいく……！", new Color(0.45f, 0.92f, 1f, 1f)),
        new CanopyBeat("", "✦ 相棒 Rust", "ピピッ！……ありがとう、Niko！これで僕たちの翼は折れることはないよ！　大空の向こうまで、全力で行こう！！", new Color(0.45f, 0.95f, 1f, 1f)),
    };

    public static void Ensure()
    {
        if (_instance != null && _instance.gameObject != null)
            return;

        var existing = Object.FindObjectsByType<AdventureSanctuaryTowerManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (_instance == null || _instance.gameObject == null)
        {
            _instance = null;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null)
                {
                    _instance = existing[i];
                    break;
                }
            }
        }

        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] == null || existing[i] == _instance)
                continue;
            Object.Destroy(existing[i].gameObject);
        }

        if (_instance != null)
            return;

        var go = new GameObject("AdventureSanctuaryTowerManager");
        _instance = go.AddComponent<AdventureSanctuaryTowerManager>();
    }

    #endregion

    #region 2. ライフサイクル & シーケンスリセット

    void Awake()
    {
        _instance = this;
        _isCanopyBroken = PlayerPrefs.GetInt(PrefKeyCanopyBroken, 0) == 1;
        // Domain/Scene Reload 無効時に前プレイの台本・保留が残ると再生直後にエンディングへ突入する
        ResetEndingSequenceFlags(clearWorldProgress: false, teardownUiFully: false);
        // 開放済みでもレバー再演できるように、ここでは _leverPulled を立てない
    }

    /// <summary>
    /// Rキー等の緊急リセット：エンディングを止め探索へ戻す（天蓋／クリア進行もクリア）。
    /// </summary>
    public void AbortEndingForEmergencyReset()
    {
        ClearEndingRuntimeState(ignoreSavedCanopy: false);
        RestoreExplorationPresentation(resetMusicToAmbient: true);
        EnsureLandTerrainColliderEnabled();
    }

    /// <summary>クリア後／ニューゲーム：天蓋・クリア・クライマックス・レバー進行を完全リセット</summary>
    public void ResetProgressForNewGame()
    {
        ClearEndingRuntimeState(ignoreSavedCanopy: true);
        ResetEndingSequenceFlags(clearWorldProgress: true, teardownUiFully: true);
        RestoreSkybreakAscentBlockers();
        RestoreExplorationPresentation(resetMusicToAmbient: true);
        AdventureMusicDirector.Ensure();
        AdventureMusicDirector.Instance?.ResetSkybreakMusicState();

        // レバーのロック状態を再構築
        BuildTowerLever();
        Debug.Log("[RustAndFloat] ニューゲーム用に天蓋／クリア／レバー進行を完全リセットしました");
    }

    /// <summary>探索HUD・カメラ・光柱FX・（必要なら）BGM／Rust状態を探索向けに戻す</summary>
    void RestoreExplorationPresentation(bool resetMusicToAmbient)
    {
        DestroySkybreakWorldFx();
        SetExplorationHudVisible(true);
        SetCinematicCamera(false);
        SetLeverPromptUI(false, false);
        if (resetMusicToAmbient)
        {
            AdventureMusicDirector.Ensure();
            AdventureMusicDirector.Instance?.RestoreExplorationTheme();
            var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
            if (drone != null)
            {
                drone.StopSkybreakNestle();
                drone.ResetClimaxState();
                drone.ClearSpeech();
            }
        }
    }

    /// <summary>
    /// エンディング台本／クライマックス／保留のランタイム状態を落とす共通処理。
    /// clearWorldProgress=true のとき天蓋・クリア・レバー進行もクリアする。
    /// </summary>
    void ResetEndingSequenceFlags(bool clearWorldProgress, bool teardownUiFully)
    {
        StopAllCoroutines();
        _epilogueRoutine = null;
        Time.timeScale = 1f;

        _endingSequenceActive = false;
        _suppressClimax = false;
        _climaxCrisisStarted = false;
        _climaxOilInjected = false;
        _climaxOilWaiting = false;
        _oilHoldTimer = 0f;
        _scriptRequireInputRelease = false;
        _climaxBeatIndex = -1;
        _climaxOverdriveCinematicUntil = 0f;
        _climaxPostOilPhase = 0;
        _climaxPostOilUntil = 0f;
        _epilogueTriggered = false;
        _epilogueAlpha = 0f;
        _epilogueAct = 0;
        _filmIndex = -1;
        _filmPhase = 0;
        _filmLastAct = -1;
        _letterboxTargetAlpha = 0f;
        ClearFilmSubtitle();
        if (_letterboxCg != null)
            _letterboxCg.alpha = 0f;
        if (_letterboxRoot != null)
            _letterboxRoot.SetActive(false);
        _scriptBoardVisible = false;
        _scriptBoardAdvance = false;
        _scriptBoardTitle = "";
        _scriptBoardSpeaker = "";
        _scriptBoardBody = "";
        _canopyBeatIndex = -1;
        _skyTearPlayed = false;
        _scriptHoldTimer = 0f;
        ClearPendingClimax();
        _showGameClearModal = false;
        _leverHoldTimer = 0f;
        _leverPullLockUntil = 0f;

        if (clearWorldProgress)
        {
            IsCanopyBroken = false;
            IsGameCleared = false;
            _leverPulled = false;
            _playerNearby = false;
        }

        if (teardownUiFully)
        {
            TeardownScriptBoardUi();
            HideOilPromptUI();
            HideGameClearModalUI();
            if (_clearUiRoot != null)
            {
                Destroy(_clearUiRoot);
                _clearUiRoot = null;
                _clearDiveBtn = null;
                _clearNewBtn = null;
                _clearCloseBtn = null;
            }
            if (_oilUiRoot != null)
            {
                Destroy(_oilUiRoot);
                _oilUiRoot = null;
                _oilGaugeFill = null;
                _oilTitleUi = null;
                _oilPromptUi = null;
                _oilHoldLabelUi = null;
                _oilHoldBtn = null;
            }
        }
        else
        {
            if (_scriptUiRoot != null)
                _scriptUiRoot.SetActive(false);
            HideOilPromptUI();
            HideGameClearModalUI();
        }
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    void Start()
    {
        FixPodiumColliders();
        SetupAudio();
        BuildTowerLever();
        var land = Terrain.activeTerrain ?? FindAnyObjectByType<Terrain>();
        BuildTowerStairs(transform, land);
        // シーン岩の遅延生成にも対応してもう一度歩行阻害を緩和
        Invoke(nameof(ReapplyTowerStreamWalkClear), 0.4f);

        // 天蓋破壊済みなら、ハイパー上昇気流光柱を即座に再配置
        if (IsCanopyBroken)
        {
            BuildSkybreakHyperUpdraft(new Vector3(512f, 62f, 512f));
        }
    }

    void ReapplyTowerStreamWalkClear()
    {
        Vector3 startP = new Vector3(472f, 48.5f, 455f);
        Vector3 endP = new Vector3(512f, TerraceTopY, 478f);
        var land = Terrain.activeTerrain ?? FindAnyObjectByType<Terrain>();
        if (land != null)
        {
            startP.y = land.SampleHeight(startP) + land.transform.position.y + 0.2f;
            endP.y = Mathf.Max(TerraceTopY, land.SampleHeight(endP) + land.transform.position.y + 0.2f);
        }
        ClearTowerStreamWalkBlockers(startP, endP);
    }

    void FixPodiumColliders()
    {
        var tower = GameObject.Find("SanctuaryZero_Tower");
        if (tower == null) return;

        // 過去の余計な直方体BoxCollider（四隅がはみ出して地面から浮く原因）を削除
        var oldPodiumSolid = tower.transform.Find("WhiteMarblePodium/WhiteMarblePodium_SolidWalk")
            ?? tower.transform.Find("WhiteMarblePodium_SolidWalk");
        if (oldPodiumSolid != null) Destroy(oldPodiumSolid.gameObject);

        var oldGridSolid = tower.transform.Find("SanctuaryGridFloor/SanctuaryGridFloor_SolidWalk")
            ?? tower.transform.Find("SanctuaryGridFloor_SolidWalk");
        if (oldGridSolid != null) Destroy(oldGridSolid.gameObject);

        // WhiteMarblePodium（白大理石円盤テラス）本体に正確なMeshColliderを付与
        var podium = tower.transform.Find("WhiteMarblePodium")?.gameObject;
        if (podium != null)
        {
            foreach (var c in podium.GetComponents<Collider>())
                Destroy(c);
            var mc = podium.AddComponent<MeshCollider>();
            var mf = podium.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) mc.sharedMesh = mf.sharedMesh;
        }

        // SanctuaryGridFloor（金属格子床）はコライダーを削除（テラス面と18cm重複してジッターするのを防止）
        var grid = tower.transform.Find("SanctuaryGridFloor")?.gameObject;
        if (grid != null)
        {
            foreach (var c in grid.GetComponents<Collider>())
                Destroy(c);
        }

        // CentralMonolith（中央オベリスク・タワー本体）のコライダー保証（すり抜け・埋まり防止）
        // ※天蓋開放前（IsCanopyBroken == false）はNikoやRustがタワー内部に潜り込まないよう頑丈なコライダーを配置
        var monolith = tower.transform.Find("CentralMonolith")?.gameObject;
        if (monolith != null)
        {
            var col = monolith.GetComponent<Collider>();
            if (col == null && !IsCanopyBroken)
            {
                var capsule = monolith.AddComponent<CapsuleCollider>();
                capsule.radius = 0.5f;
                capsule.height = 2.0f;
                capsule.direction = 1; // Y軸
            }
            else if (col != null)
            {
                col.enabled = !IsCanopyBroken;
            }
        }

        // ── テラス外周 4方向スロープ（地面62m → テラス63m）──
        // 台地の平坦な芝生面（標高62m）からテラス（標高63m）へ歩いて乗れるよう
        // テラス進入スロープ（AdventureSanctuaryTowerManager.Structure.cs に実装）
        BuildPodiumEntryRamps(tower.transform);
    }

    readonly Vector3 _mainLeverPos = new Vector3(512f, 63.2f, 501.5f); // オベリスク南側正面・白亜テラスの特等席
    readonly Vector3 _topLeverPos = new Vector3(512f, 137.2f, 512f);    // オベリスク天面頂上
    Transform _topLeverHandle;

    public Vector3 MainLeverPosition => _mainLeverPos;

    #endregion

    #region 3. 台座レバーギミック & プロンプトUI

    // (レバー物理構造・台座・アプローチステップは AdventureSanctuaryTowerManager.Structure.cs へ分離)

    public bool IsPlayerNearLever =>
        _playerNearby
        && !_scriptBoardVisible
        && !_climaxCrisisStarted
        && !_epilogueTriggered;

    /// <summary>総合20ポイント以上達成でレバーロック解除状態（再演含む）</summary>
    public bool IsLeverReadyToOpen
    {
        get
        {
            var scrapMgr = AdventureScrapManager.Instance ?? Object.FindFirstObjectByType<AdventureScrapManager>();
            return scrapMgr != null && scrapMgr.IsLeverUnlocked;
        }
    }

    float _leverHoldTimer;
    const float LeverHoldSeconds = 0.28f;
    float _endingStuckTimer;
    float _leverPullLockUntil;
    GameObject _leverUiRoot;
    UnityEngine.UI.Text _leverUiLabel;

    void Update()
    {
        // デバッグショートカット（オープニング中でも最優先で検証可能）
        var debugKb = UnityEngine.InputSystem.Keyboard.current;
        if (debugKb != null)
        {
            // 数字8: 20pt達成・現在地からタワー誘導（F8はセーブ初期化＝ニューゲーム専用）
            if (debugKb.digit8Key.wasPressedThisFrame || debugKb.numpad8Key.wasPressedThisFrame)
            {
                DebugSetup20PointsState(warpToLever: false);
                return;
            }
            // F7 / 数字7: 20pt達成・タワーテラス（レバー前）へワープ
            if (debugKb.f7Key.wasPressedThisFrame || debugKb.digit7Key.wasPressedThisFrame || debugKb.numpad7Key.wasPressedThisFrame)
            {
                DebugSetup20PointsState(warpToLever: true);
                return;
            }
            // F9 / F10: レバー前へワープして即時天蓋開放シーケンス開始
            if (debugKb.f9Key.wasPressedThisFrame || debugKb.f10Key.wasPressedThisFrame)
            {
                DebugJumpToCanopyOpening();
                return;
            }
        }
        try
        {
            if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8))
            {
                DebugSetup20PointsState(warpToLever: false);
                return;
            }
            if (Input.GetKeyDown(KeyCode.F7) || Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7))
            {
                DebugSetup20PointsState(warpToLever: true);
                return;
            }
            if (Input.GetKeyDown(KeyCode.F9) || Input.GetKeyDown(KeyCode.F10))
            {
                DebugJumpToCanopyOpening();
                return;
            }
        }
        catch { }

        // 天蓋台本は Update で必ず進める（コルーチン停止に依存しない）
        TickCanopyScriptBeats();
        TickClimaxSequence();
        TickEpilogueFilm();
        TickGameClearModal();
        TickCinematicLetterbox();

        // 台本ボード：Updateでも進む入力を拾う（注油直後の押しっぱなしは除外）
        if (_scriptBoardVisible && !_scriptBoardAdvance && !_climaxOilWaiting)
        {
            if (_scriptRequireInputRelease)
            {
                if (!IsDiveConfirmHeld())
                    _scriptRequireInputRelease = false;
            }
            else
            {
                PollScriptBoardAdvance();
            }
        }

        // キャッシュ参照を使用（毎フレームFind廃止）
        var player = GetPlayer();

        // プレイヤー未検出でも保留クライマックスは進める
        TryStartPendingClimax(player);
        if (player == null) return;

        RescuePlayerIfBuriedInTerrace(player);

        if (IsGameCleared)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.tabKey.wasPressedThisFrame)
                SetGameClearModalVisible(!_showGameClearModal);
        }

        // 全パーツ回収済み・天蓋未開放ならレバー操作を最優先（誤った危機フラグを解除）
        if (IsLeverReadyToOpen && !IsCanopyBroken && !_scriptBoardVisible)
        {
            // ※実行中コルーチンは絶対に Kill しない（以前ここで StopAllCoroutines して台本が消えていた）
            if (_climaxCrisisStarted && !_climaxOilInjected)
                _climaxCrisisStarted = false;
            if (_epilogueTriggered)
                _epilogueTriggered = false;
            if (_endingSequenceActive && !_leverPulled)
            {
                _endingSequenceActive = false;
                _suppressClimax = false;
            }
        }
        _endingStuckTimer = 0f;

        // 台本／危機／エピローグ／柱上昇待ち／天蓋開放後はレバー入力を止める
        // （ダイブ後の Space 長押しがレバー再作動→台本最初へ巻き戻るのを防ぐ）
        if (_scriptBoardVisible || _climaxCrisisStarted || _epilogueTriggered || _endingSequenceActive
            || _pendingClimaxAfterCanopy || IsCanopyBroken)
        {
            SetLeverPromptUI(false, false);
            return;
        }

        UpdateLeverProximity(player);

        if (_playerNearby)
            ClearBoardsBlockingLever();

        bool allCollected = IsLeverReadyToOpen;

        if (_allLeverLights.Count > 0)
        {
            float pulse = allCollected ? (2.8f + Mathf.Sin(Time.time * 4.5f) * 1.2f) : 1.2f;
            Color lightCol = allCollected ? new Color(0.35f, 0.95f, 1.0f) : new Color(1.0f, 0.75f, 0.25f);
            foreach (var l in _allLeverLights)
            {
                if (l != null)
                {
                    l.intensity = pulse;
                    l.color = lightCol;
                }
            }
        }

        if (!_playerNearby)
        {
            _leverHoldTimer = 0f;
            SetLeverPromptUI(false, false);
            return;
        }

        SetLeverPromptUI(true, allCollected);

        bool tapped = CheckLeverInputTriggered() || (player.InteractPressed && WasInteractEdge());
        bool holding = IsLeverHoldInput();

        if (holding && allCollected)
            _leverHoldTimer += Time.unscaledDeltaTime;
        else if (!holding)
            _leverHoldTimer = 0f;

        // パーツ齐全時はタップ1回で即開放（長押しは保険）
        if (allCollected && (tapped || _leverHoldTimer >= LeverHoldSeconds))
        {
            _leverHoldTimer = 0f;
            BeginCanopyOpeningFromLever(true);
        }
        else if (!allCollected && tapped)
        {
            _leverHoldTimer = 0f;
            BeginCanopyOpeningFromLever(false);
        }
    }

    void UpdateLeverProximity(AdventurePlayerController player)
    {
        // クリア後／天蓋開放後の自由探索ではレバー再演プロンプトを出さない（再演は F9）
        // ※F9確認中（_ignoreSavedCanopyState）はクリア済みでもレバー操作を許可
        if ((IsGameCleared || IsCanopyBroken || _pendingClimaxAfterCanopy) && !_ignoreSavedCanopyState)
        {
            _playerNearby = false;
            return;
        }

        Vector3 p = player.transform.position;

        // 南正面メインレバー台座の周辺（14m以内）にいる時だけ操作UIを表示
        bool nearMainLever = Vector3.Distance(p, _mainLeverPos) < 14f;

        _playerNearby = nearMainLever && !_showSkybreakModal;
    }

    static void EnsureEventSystemForUi()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null)
        {
            var go = new GameObject("EventSystem");
            es = go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        }

        // プロジェクトは Input System のみ（activeInputHandler=1）。旧Standaloneはクリック不能。
        var legacy = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        if (legacy != null)
            Object.Destroy(legacy);

        if (es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
            es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    void SetLeverPromptUI(bool show, bool ready)
    {
        if (!show)
        {
            if (_leverUiRoot != null)
                _leverUiRoot.SetActive(false);
            return;
        }

        EnsureLeverPromptUI();
        if (_leverUiRoot == null) return;
        _leverUiRoot.SetActive(true);
        if (_leverUiLabel != null)
        {
            int pts = AdventureScrapManager.Instance != null ? AdventureScrapManager.Instance.TotalProgressPoints : 0;
            _leverUiLabel.text = ready
                ? "【ここを押す / E】巨大真鍮レバーを引く"
                : $"レバーはロック中（20ポイントが必要 / 現在: {pts} pt）";
            _leverUiLabel.color = ready
                ? new Color(0.35f, 0.98f, 0.88f, 1f)
                : new Color(1f, 0.85f, 0.4f, 1f);
        }
    }

    void EnsureLeverPromptUI()
    {
        if (_leverUiRoot != null) return;

        EnsureEventSystemForUi();

        var canvasGo = new GameObject("LeverPromptCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4000;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        canvasGo.AddComponent<GraphicRaycaster>();

        var btnGo = new GameObject("LeverPullButton");
        btnGo.transform.SetParent(canvasGo.transform, false);
        var rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(920f, 110f);
        rt.anchoredPosition = new Vector2(0f, 36f);

        var img = btnGo.AddComponent<Image>();
        img.color = new Color(0.03f, 0.08f, 0.14f, 0.88f);
        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => BeginCanopyOpeningFromLever(IsLeverReadyToOpen));

        var textGo = new GameObject("Label");
        textGo.transform.SetParent(btnGo.transform, false);
        var trt = textGo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(16f, 8f);
        trt.offsetMax = new Vector2(-16f, -8f);
        _leverUiLabel = textGo.AddComponent<Text>();
        _leverUiLabel.font = ResolveUiFont();
        _leverUiLabel.fontSize = 32;
        _leverUiLabel.alignment = TextAnchor.MiddleCenter;
        _leverUiLabel.color = new Color(0.35f, 0.98f, 0.88f, 1f);
        _leverUiLabel.raycastTarget = false;

        _leverUiRoot = canvasGo;
        _leverUiRoot.SetActive(false);
    }

    /// <summary>総合20ポイント達成時：巨大レバーのロックを解除（※実際にタワーで引くまで天蓋開放は発動しない）</summary>
    public void OnLeverUnlockedByPoints()
    {
        // 誤って残った危機／台本フラグをクリア（レバー操作を塞がない）
        if (!_endingSequenceActive && !IsCanopyBroken)
        {
            _climaxCrisisStarted = false;
            _climaxOilInjected = false;
            _climaxOilWaiting = false;
            _climaxBeatIndex = -1;
            _climaxOverdriveCinematicUntil = 0f;
            _climaxPostOilPhase = 0;
            _climaxPostOilUntil = 0f;
            _scriptBoardVisible = false;
            _epilogueTriggered = false;
        }

        BuildTowerLever();
        ClearBoardsBlockingLever();

        var player = AdventurePlayerController.Instance
                     ?? Object.FindFirstObjectByType<AdventurePlayerController>();
        if (player != null)
            UpdateLeverProximity(player);

        Debug.Log("[RustAndFloat] 総合20ポイント達成：巨大レバーロック解除（プレイヤーがタワーで引くまで待機）");
    }

    /// <summary>パーツ12個達成時：巨大レバーを再生成し、操作UIを確実に出す</summary>
    public void OnAllScrapsCollectedForLever()
    {
        OnLeverUnlockedByPoints();
    }

    bool WasInteractEdge()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame) return true;
        try { if (Input.GetKeyDown(KeyCode.E)) return true; } catch { }
        var pad = UnityEngine.InputSystem.Gamepad.current;
        return pad != null && pad.buttonWest.wasPressedThisFrame;
    }

    bool IsLeverHoldInput()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && (kb.eKey.isPressed || kb.enterKey.isPressed))
            return true;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed) return true;
        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (pad != null && pad.buttonWest.isPressed) return true;
        try
        {
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Return)) return true;
            if (Input.GetMouseButton(0)) return true;
        }
        catch { }
        return false;
    }

    #endregion

    #region 4. 天蓋開放トリガー & 音響・環境制御

    /// <summary>レバー前に残る詩的バナー／砂浜読み物／オープニングを消して操作を塞がない</summary>
    void ClearBoardsBlockingLever()
    {
        if (AdventureScrapHUD.Instance != null)
            AdventureScrapHUD.Instance.HideBannerImmediately();

        var beach = AdventureBeachNarrativeManager.Instance;
        if (beach != null && beach.IsShowingModal)
            beach.CloseModal();

        // AdventureRustFloatOpening はシーン開始後は変わらないのでキャッシュで十分
        var opening = AdventureRustFloatOpening.Instance
                      ?? Object.FindFirstObjectByType<AdventureRustFloatOpening>();
        if (opening != null && opening.IsModalBoardOpen())
            opening.ForceDismissForGameplay();
    }

    bool CheckLeverInputTriggered()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            if (kb.eKey.wasPressedThisFrame) return true;
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                return true;
        }

        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
            return true;

        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (pad != null && pad.buttonWest.wasPressedThisFrame) return true;

        try
        {
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return))
                return true;
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) return true;
        }
        catch { }
        return false;
    }

    /// <summary>レバーから天蓋開放へ。forceRestart=true のときだけ再演（F9）。通常操作中の再入は台本を最初に戻さない。</summary>
    void BeginCanopyOpeningFromLever(bool allCollected, bool forceRestart = false)
    {
        // 台本／シークエンス／柱上昇／クライマックス進行中は通常操作で絶対に再スタートしない
        if (!forceRestart)
        {
            if (_scriptBoardVisible || _endingSequenceActive || _leverPulled || _epilogueTriggered || _canopyBeatIndex >= 0)
                return;
            if (_climaxCrisisStarted || _pendingClimaxAfterCanopy || IsCanopyBroken || IsGameCleared)
                return;

            var ascending = AdventurePlayerController.Instance
                            ?? Object.FindFirstObjectByType<AdventurePlayerController>();
            if (ascending != null && (ascending.IsSkybreakPillarAscending || ascending.IsAutoGliding))
                return;
        }

        if (!allCollected)
        {
            TryPullLever(false);
            return;
        }

        // F9再演のみ全リセット。開放済みフラグだけでは絶対に巻き戻さない
        if (forceRestart)
        {
            ClearEndingRuntimeState(ignoreSavedCanopy: true);
            RestoreExplorationPresentation(resetMusicToAmbient: false);
            var p = AdventurePlayerController.Instance;
            if (p != null)
            {
                p.SetAutoGlideMode(false);
                if (p.transform.position.y < 90f)
                    p.ForceGroundReset();
            }
            foreach (var h in _allLeverHandles)
            {
                if (h != null) h.localRotation = Quaternion.identity;
            }
            if (_hyperUpdraftGo != null)
                _hyperUpdraftGo.SetActive(false);
        }

        TryPullLever(true);
    }

    [ContextMenu("Debug: 20pt達成・タワー誘導開始（現在地から）")]
    public void DebugSetup20PointsCurrentPos() => DebugSetup20PointsState(warpToLever: false);

    [ContextMenu("Debug: 20pt達成・タワーレバー前へワープ")]
    public void DebugSetup20PointsWarpToLever() => DebugSetup20PointsState(warpToLever: true);

    [ContextMenu("Debug: レバー即時開放シーケンス開始（F9）")]
    public void DebugContextMenuJumpToCanopyOpening() => DebugJumpToCanopyOpening();

    /// <summary>
    /// デバッグ検証用（数字8: その場から誘導、F7/7: レバー前ワープ。F8はニューゲーム専用）：
    /// 総合20ポイント達成・レバーロック解除状態に即時セットアップし、その後の展開を自由に試せる。
    /// </summary>
    public void DebugSetup20PointsState(bool warpToLever)
    {
        var others = Object.FindObjectsByType<AdventureSanctuaryTowerManager>(FindObjectsSortMode.None);
        for (int i = 0; i < others.Length; i++)
        {
            if (others[i] != null && others[i] != this)
                Destroy(others[i].gameObject);
        }
        _instance = this;

        // 過去のエンディング・天蓋崩壊フラグをクリア（未開放状態から開始）
        ClearEndingRuntimeState(ignoreSavedCanopy: true);
        RestoreExplorationPresentation(resetMusicToAmbient: true);
        EnsureEventSystemForUi();
        FixPodiumColliders();
        ClearBoardsBlockingLever();

        // スクラップマネージャーの進行度を合計21pt（20pt以上）にセットアップ
        AdventureScrapManager.Ensure();
        var sm = AdventureScrapManager.Instance;
        if (sm != null)
        {
            sm.ResetToCount(12); // パーツ12個 (12pt)
            sm.IsPianoRelicCollected = true; // ピアノ遺物 (3pt)
            PlayerPrefs.SetInt("DriftBox_Opened_1", 1); // ドリフトボックス3箱 (6pt)
            PlayerPrefs.SetInt("DriftBox_Opened_2", 1);
            PlayerPrefs.SetInt("DriftBox_Opened_3", 1);
            PlayerPrefs.SetInt("DriftBox_Opened_4", 0);
            PlayerPrefs.SetInt("DriftBox_Opened_5", 0);
            PlayerPrefs.DeleteKey("Adventure_LeverUnlockedNotified"); // 通知フラグをクリアしてチャイム＆誘導を確実に発火
            PlayerPrefs.Save();
            AdventureBeachDriftBox.SyncAllOpenedVisualsFromPrefs();
            sm.InvalidateDriftBoxCache();
        }

        AdventureMusicDirector.Ensure();
        AdventureMusicDirector.Instance?.ResetSkybreakMusicState();

        // 白亜テラスの巨大真鍮レバー群を構築
        BuildTowerLever();

        // プレイヤーの移動
        var player = AdventurePlayerController.Instance
                     ?? Object.FindFirstObjectByType<AdventurePlayerController>();
        if (player != null)
        {
            player.SetAutoGlideMode(false);
            player.ForceGroundReset();
            if (warpToLever)
            {
                // 南側正面レバーの眼前（手前約3.2m）へワープ
                Vector3 pos = new Vector3(_mainLeverPos.x, TerraceTopY + 0.12f, _mainLeverPos.z - 3.2f);
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 0f, 0f));
                if (cc != null) cc.enabled = true;
            }
        }

        // 相棒ドローンRustの状態初期化（クライマックス注油用オイル確保）
        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            drone.oilCount = Mathf.Max(drone.oilCount, 3);
            drone.ResetClimaxState();
            drone.ClearSpeech();
        }

        FindAnyObjectByType<AdventureRustFloatOpening>()?.ForceDismissForGameplay();

        // 20pt達成通知・祝福チャイム・Rust先導・コンパスHUD誘導を同時発火！
        if (sm != null)
        {
            sm.CheckPointsAndNotifyLeverUnlock();
        }
        else
        {
            OnLeverUnlockedByPoints();
        }

        if (player != null)
            UpdateLeverProximity(player);

        string modeStr = warpToLever ? "【タワー白亜テラス（レバー前）へワープ】" : "【現在位置からタワー誘導開始】";
        Debug.Log($"[RustAndFloat] 20ポイント達成デバッグセットアップ完了: {modeStr}");

        var hud = AdventureScrapHUD.Instance ?? FindAnyObjectByType<AdventureScrapHUD>();
        if (hud != null)
        {
            hud.RefreshQuestDisplay();
            string bannerMsg = warpToLever
                ? "✦ 20ポイント達成！タワー白亜テラス（レバー前）へワープ ✦\n💡 【Eキー】で巨大レバーを引いて天蓋を開放しよう！"
                : "✦ 20ポイント達成！中央タワーのレバーロック解除！ ✦\n💡 相棒Rustとコンパスに従って中央タワーへ向かおう！";
            hud.ShowUpgradeBanner(bannerMsg);
        }
    }

    /// <summary>
    /// 演出確認用（F9）：パーツ12個・南側レバー前へ移動し、少し待って自動でレバー開放。
    /// </summary>
    public void DebugJumpToCanopyOpening()
    {
        var others = Object.FindObjectsByType<AdventureSanctuaryTowerManager>(FindObjectsSortMode.None);
        for (int i = 0; i < others.Length; i++)
        {
            if (others[i] != null && others[i] != this)
                Destroy(others[i].gameObject);
        }
        _instance = this;

        ClearEndingRuntimeState(ignoreSavedCanopy: true);
        FixPodiumColliders();
        BuildTowerLever();
        RestoreExplorationPresentation(resetMusicToAmbient: false);
        EnsureEventSystemForUi();

        AdventureScrapManager.Ensure();
        var sm = AdventureScrapManager.Instance;
        if (sm != null)
        {
            sm.ResetToCount(AdventureScrapManager.TotalScrapCount);
            sm.IsPianoRelicCollected = true;
            for (int i = 1; i <= 5; i++)
                PlayerPrefs.SetInt("DriftBox_Opened_" + i, 1);
            PlayerPrefs.Save();
        }
        AdventureMusicDirector.Ensure();
        AdventureMusicDirector.Instance?.ResetSkybreakMusicState();

        foreach (var h in _allLeverHandles)
        {
            if (h != null)
                h.localRotation = Quaternion.identity;
        }

        FindAnyObjectByType<AdventureRustFloatOpening>()?.ForceDismissForGameplay();
        ClearBoardsBlockingLever();

        var player = AdventurePlayerController.Instance
                     ?? Object.FindFirstObjectByType<AdventurePlayerController>();
        if (player != null)
        {
            player.SetAutoGlideMode(false);
            player.ForceGroundReset();
            Vector3 pos = new Vector3(_mainLeverPos.x, TerraceTopY + 0.12f, _mainLeverPos.z - 3.2f);
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 0f, 0f));
            if (cc != null) cc.enabled = true;
        }
        else
        {
            Debug.LogWarning("[RustAndFloat] DebugJump: AdventurePlayerController が見つかりません");
        }

        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            drone.oilCount = Mathf.Max(drone.oilCount, 2);
            drone.ResetClimaxState();
            drone.ClearSpeech();
        }

        _leverHoldTimer = 0f;
        OnAllScrapsCollectedForLever();
        if (player != null)
            UpdateLeverProximity(player);

        // 遅延なしで即開放（F9再演のみ forceRestart）
        BeginCanopyOpeningFromLever(true, forceRestart: true);

        Debug.Log("[RustAndFloat] F9: レバー前へ移動＋天蓋開放を即開始");
    }

    void ClearEndingRuntimeState(bool ignoreSavedCanopy)
    {
        _ignoreSavedCanopyState = ignoreSavedCanopy;
        ResetEndingSequenceFlags(clearWorldProgress: true, teardownUiFully: true);
    }

    void TeardownScriptBoardUi()
    {
        if (_scriptUiRoot != null)
        {
            _scriptUiRoot.SetActive(false);
            Destroy(_scriptUiRoot);
            _scriptUiRoot = null;
            _scriptDimImg = null;
            _scriptPanelImg = null;
            _scriptAccentImg = null;
            _scriptTitleUi = null;
            _scriptSpeakerUi = null;
            _scriptBodyUi = null;
            _scriptHintUi = null;
            _scriptBtn = null;
        }
        var orphanBoard = GameObject.Find("EndingScriptBoardCanvas");
        if (orphanBoard != null) Destroy(orphanBoard);
    }

    void DestroySkybreakWorldFx()
    {
        StopSkybreakWindAmbience();
        ClearSkybreakColdAtmosphere();
        if (_hyperUpdraftGo != null)
        {
            Destroy(_hyperUpdraftGo);
            _hyperUpdraftGo = null;
        }
        DestroyAllByName("SkybreakHyperUpdraft");
        DestroyAllByName("WildernessPanorama");
        DestroyAllByName("SkybreakEffect");
        DestroyAllByName("SkybreakColdMist");
        DestroyAllByName("SkyTearOpening");
        DestroyAllByName("SkyTearFlashCanvas");
    }

    /// <summary>天蓋破壊ボード表示と同時に、外気の冷たい風音をフェードイン</summary>
    void StartSkybreakWindAmbience()
    {
        if (_skybreakWindClip == null)
            _skybreakWindClip = LoadOrSynthesizeSkybreakWindClip();

        if (_skybreakWindSource == null)
        {
            var go = new GameObject("SkybreakWindAmbience");
            go.transform.SetParent(transform, false);
            _skybreakWindSource = go.AddComponent<AudioSource>();
            _skybreakWindSource.spatialBlend = 0f;
            _skybreakWindSource.loop = true;
            _skybreakWindSource.playOnAwake = false;
        }

        _skybreakWindSource.clip = _skybreakWindClip;
        _skybreakWindSource.volume = 0f;
        if (!_skybreakWindSource.isPlaying)
            _skybreakWindSource.Play();
        StartCoroutine(FadeAudioSource(_skybreakWindSource, 0.45f, 1.4f));
    }

    void StopSkybreakWindAmbience()
    {
        if (_skybreakWindSource == null) return;
        if (_skybreakWindSource.isPlaying)
            StartCoroutine(FadeOutAndStopWind());
        else
            _skybreakWindSource.volume = 0f;
    }

    IEnumerator FadeOutAndStopWind()
    {
        var src = _skybreakWindSource;
        if (src == null) yield break;
        float start = src.volume;
        float t = 0f;
        while (t < 0.8f && src != null)
        {
            t += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(start, 0f, t / 0.8f);
            yield return null;
        }
        if (src != null)
        {
            src.Stop();
            src.volume = 0f;
        }
    }
    // (天蓋風音フェード・合成は AdventureSanctuaryTowerManager.Audio.cs へ分離)

    static void DestroyAllByName(string objectName)
    {
        var all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null || t.gameObject == null) continue;
            if (t.name != objectName) continue;
            if (!t.gameObject.scene.IsValid()) continue;
            DestroyImmediate(t.gameObject);
        }
    }

    void TryPullLever(bool allCollected)
    {
        var drone = GetDrone();

        if (!allCollected)
        {
            var scrapMgr = AdventureScrapManager.Instance;
            int pts = scrapMgr != null ? scrapMgr.TotalProgressPoints : 0;
            int remaining = Mathf.Max(0, AdventureScrapManager.RequiredPointsForCanopy - pts);
            if (drone != null)
            {
                drone.SpeakCustom($"まだレバーがロックされてるみたい…あと{remaining}ポイント集めよう！（現在: {pts}/20 pt）\nパーツ(1pt)、ドリフトボックス(2pt)、カピタのピアノ(3pt)を探してみて！", 5.5f);
            }
            if (_audio != null)
                _audio.PlayOneShot(MakeClankSound(), 0.6f);
            return;
        }

        // 天蓋破壊シークエンス開始！
        _leverPulled = true;
        _endingSequenceActive = true;
        _suppressClimax = true;
        _climaxCrisisStarted = false;
        ClearPendingClimax();
        // シークエンス終了まで再入禁止（短いロックだと台本中に最初へ巻き戻る）
        _leverPullLockUntil = Time.unscaledTime + 3600f;
        SetLeverPromptUI(false, false);
        Debug.Log("[RustAndFloat] レバー作動 → 天蓋開放シークエンス開始");
        StartCoroutine(SkybreakSequenceRoutine());
    }

    IEnumerator SkybreakSequenceRoutine()
    {
        AdventureCicadaAmbienceManager.Ensure();
        AdventureCicadaAmbienceManager.Instance?.MuteForEndingSequence();
        AdventureTreeFrogAmbience.Ensure();
        AdventureTreeFrogAmbience.Instance?.MuteForEndingSequence();

        var player = AdventurePlayerController.Instance
                     ?? Object.FindFirstObjectByType<AdventurePlayerController>();
        if (player != null)
            player.ForceGroundReset();

        if (_audio != null)
            _audio.PlayOneShot(MakeHeavyLeverSound(), 0.9f);
        StartCoroutine(AnimateLeverPullRoutine());

        SpawnLeverSparks(new Vector3(512f, 63.5f, 512f));
        SuppressAllSpeechAndBanners();
        SpawnSkybreakCracks(new Vector3(512f, 150f, 512f));

        if (_hyperUpdraftGo != null)
            _hyperUpdraftGo.SetActive(false);

        // 台本は Update 駆動（このコルーチンが止まっても進む）
        BeginCanopyScriptBeats();
        yield break;
    }

    System.Collections.IEnumerator AnimateLeverPullRoutine()
    {
        // レバー作動時、南正面のメインレバー構造以外に重複している古いレバーがあれば完全一掃
        var allStructures = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        GameObject activeMainRoot = (_leverHandle != null && _leverHandle.parent != null) ? _leverHandle.parent.gameObject : null;
        foreach (var go in allStructures)
        {
            if (go == null) continue;
            if (go.name == "SanctuaryLeverStructure" && go != activeMainRoot)
            {
                DestroyImmediate(go);
            }
        }

        // ピボット（LeverPivot）が未登録ならメインレバーから再取得
        if (_allLeverHandles.Count == 0 && _leverHandle != null)
        {
            _allLeverHandles.Add(_leverHandle);
        }

        float elapsed = 0f;
        float duration = 0.85f;
        Quaternion startRot = _leverHandle != null ? _leverHandle.localRotation : Quaternion.identity;
        Quaternion endRot = Quaternion.Euler(52f, 0f, 0f); // 深く倒して「引いた」感をはっきり

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // 序盤ゆっくり→終盤で重く倒れる
            float ease = t * t * (3f - 2f * t);
            if (_leverHandle != null)
                _leverHandle.localRotation = Quaternion.Slerp(startRot, endRot, ease);
            foreach (var h in _allLeverHandles)
            {
                if (h != null) h.localRotation = Quaternion.Slerp(startRot, endRot, ease);
            }
            if (_leverLight != null)
                _leverLight.intensity = Mathf.Lerp(11.5f, 18f, ease);
            yield return null;
        }
    }

    #endregion


    #region 6. 台本ボードUI (uGUI) & 入力ハンドリング

    /// <summary>クリック／Space で台本を進める（Input System の wasPressed を直接見る）</summary>
    void PollScriptBoardAdvance()
    {
        if (!_scriptBoardVisible || _scriptBoardAdvance) return;
        if (_scriptRequireInputRelease) return;

        // 注油直後の最初のセリフだけ長めに守る
        float minShow = 0.35f;
        if (_climaxCrisisStarted && _climaxBeatIndex == ClimaxOilSlot)
            minShow = 2.2f;
        else if (_climaxCrisisStarted && _climaxBeatIndex > ClimaxOilSlot)
            minShow = 0.85f;
        if (Time.unscaledTime - _scriptBoardOpenedAt < minShow) return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null &&
            (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame ||
             kb.numpadEnterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame ||
             kb.jKey.wasPressedThisFrame))
        {
            _scriptBoardAdvance = true;
            return;
        }

        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null &&
            (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
        {
            _scriptBoardAdvance = true;
            return;
        }

        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (pad != null &&
            (pad.buttonSouth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame))
        {
            _scriptBoardAdvance = true;
            return;
        }

        try
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.E) ||
                Input.GetKeyDown(KeyCode.J) ||
                Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                _scriptBoardAdvance = true;
            }
        }
        catch { }
    }

    void EnsureScriptBoardUI()
    {
        // 古いキャンバスがクリックを食うのを防ぐ（即破棄）
        if (_scriptUiRoot == null)
        {
            var orphans = Resources.FindObjectsOfTypeAll<Canvas>();
            for (int i = 0; i < orphans.Length; i++)
            {
                var c = orphans[i];
                if (c == null || c.gameObject == null) continue;
                if (c.gameObject.name != "EndingScriptBoardCanvas") continue;
                if (!Application.isPlaying) continue;
                DestroyImmediate(c.gameObject);
            }
        }
        else if (_scriptDimImg == null || _scriptPanelImg == null)
        {
            DestroyImmediate(_scriptUiRoot);
            _scriptUiRoot = null;
            _scriptDimImg = null;
            _scriptPanelImg = null;
            _scriptBtn = null;
        }

        if (_scriptUiRoot != null)
        {
            _scriptUiRoot.SetActive(_scriptBoardVisible);
            return;
        }

        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            EnsureEventSystemForUi();
        }
        else
        {
            EnsureEventSystemForUi();
        }

        Font font = ResolveUiFont();

        var canvasGo = new GameObject("EndingScriptBoardCanvas");
        // シーン内に置き、DontDestroyOnLoadで孤児化しない
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000; // 他HUDより前面
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // 暗幕（薄め・全面クリック可）
        var dimGo = new GameObject("Dim");
        dimGo.transform.SetParent(canvasGo.transform, false);
        var dimRt = dimGo.AddComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        _scriptDimImg = dimGo.AddComponent<Image>();
        _scriptDimImg.color = new Color(0.01f, 0.02f, 0.05f, 0.28f);
        _scriptDimImg.raycastTarget = true;
        var dimBtn = dimGo.AddComponent<Button>();
        dimBtn.targetGraphic = _scriptDimImg;
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(RequestScriptBoardAdvance);

        // ボード本体（半透明）
        var boardGo = new GameObject("Board");
        boardGo.transform.SetParent(canvasGo.transform, false);
        var boardRt = boardGo.AddComponent<RectTransform>();
        boardRt.anchorMin = new Vector2(0.5f, 0.5f);
        boardRt.anchorMax = new Vector2(0.5f, 0.5f);
        boardRt.pivot = new Vector2(0.5f, 0.5f);
        boardRt.sizeDelta = new Vector2(860f, 420f);
        _scriptPanelImg = boardGo.AddComponent<Image>();
        _scriptPanelImg.color = new Color(0.03f, 0.06f, 0.11f, 0.55f);
        _scriptPanelImg.raycastTarget = true;
        var boardBtn = boardGo.AddComponent<Button>();
        boardBtn.targetGraphic = _scriptPanelImg;
        boardBtn.transition = Selectable.Transition.None;
        boardBtn.onClick.AddListener(RequestScriptBoardAdvance);

        var accentGo = new GameObject("Accent");
        accentGo.transform.SetParent(boardGo.transform, false);
        var accRt = accentGo.AddComponent<RectTransform>();
        accRt.anchorMin = new Vector2(0f, 0f);
        accRt.anchorMax = new Vector2(0f, 1f);
        accRt.pivot = new Vector2(0f, 0.5f);
        accRt.sizeDelta = new Vector2(5f, 0f);
        accRt.anchoredPosition = Vector2.zero;
        _scriptAccentImg = accentGo.AddComponent<Image>();
        _scriptAccentImg.color = _scriptBoardAccent;
        _scriptAccentImg.raycastTarget = false;

        _scriptTitleUi = MakeScriptText(boardGo.transform, "Title", new Vector2(0f, -22f), new Vector2(0.5f, 1f), new Vector2(800f, 40f), 28, TextAnchor.MiddleCenter, font);
        _scriptSpeakerUi = MakeScriptText(boardGo.transform, "Speaker", new Vector2(0f, -68f), new Vector2(0.5f, 1f), new Vector2(800f, 32f), 22, TextAnchor.MiddleLeft, font);
        _scriptBodyUi = MakeScriptText(boardGo.transform, "Body", new Vector2(0f, 10f), new Vector2(0.5f, 0.5f), new Vector2(780f, 220f), 26, TextAnchor.MiddleCenter, font);
        _scriptBodyUi.horizontalOverflow = HorizontalWrapMode.Wrap;
        _scriptBodyUi.verticalOverflow = VerticalWrapMode.Overflow;
        if (_scriptBodyUi.font == null)
            _scriptBodyUi.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var btnGo = new GameObject("ContinueBtn");
        btnGo.transform.SetParent(boardGo.transform, false);
        var btnRt = btnGo.AddComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0f);
        btnRt.anchorMax = new Vector2(0.5f, 0f);
        btnRt.pivot = new Vector2(0.5f, 0f);
        btnRt.anchoredPosition = new Vector2(0f, 22f);
        btnRt.sizeDelta = new Vector2(520f, 52f);
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0.12f, 0.42f, 0.72f, 0.80f);
        _scriptBtn = btnGo.AddComponent<Button>();
        _scriptBtn.targetGraphic = btnImg;
        _scriptBtn.onClick.AddListener(RequestScriptBoardAdvance);

        _scriptHintUi = MakeScriptText(btnGo.transform, "Hint", Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(500f, 48f), 18, TextAnchor.MiddleCenter, font);
        _scriptHintUi.raycastTarget = false;

        _scriptUiRoot = canvasGo;
        _scriptUiRoot.SetActive(true);
    }

    void RequestScriptBoardAdvance()
    {
        NotifyScriptBoardAdvance();
    }

    void ApplyScriptBoardUI()
    {
        if (_scriptUiRoot == null) return;
        _scriptUiRoot.SetActive(true);

        if (_scriptDimImg != null)
            _scriptDimImg.color = new Color(0.01f, 0.02f, 0.05f, 0.28f);
        if (_scriptPanelImg != null)
            _scriptPanelImg.color = new Color(0.03f, 0.06f, 0.11f, 0.55f);

        bool hasTitle = !string.IsNullOrEmpty(_scriptBoardTitle);
        bool hasSpeaker = !string.IsNullOrEmpty(_scriptBoardSpeaker);
        bool isDialogue = hasSpeaker; // Niko / Rust セリフ
        bool isNarration = !isDialogue; // タイトル説明・ナレ・操作説明

        // セリフ／説明とも読みやすい中サイズ（旧セリフ34は大きすぎた）
        int titleSize = isNarration ? 30 : 26;
        int speakerSize = 20;
        int bodySize = 28;
        Color bodyColor = isDialogue
            ? new Color(1f, 1f, 1f, 1f)
            : new Color(0.88f, 0.94f, 1f, 0.98f);

        int lineCount = 1;
        if (!string.IsNullOrEmpty(_scriptBoardBody))
        {
            lineCount = 1;
            for (int i = 0; i < _scriptBoardBody.Length; i++)
                if (_scriptBoardBody[i] == '\n') lineCount++;
            // 長い1行は折り返し想定
            if (lineCount == 1 && _scriptBoardBody.Length > 28)
                lineCount = 2;
            if (_scriptBoardBody.Length > 48)
                lineCount = Mathf.Max(lineCount, 3);
        }

        float padTop = 16f;
        float padBottom = 14f;
        float titleBlock = hasTitle ? titleSize + 10f : 0f;
        float speakerBlock = hasSpeaker ? speakerSize + 8f : 0f;
        float bodyBlock = Mathf.Max(bodySize * 1.35f * lineCount, bodySize + 8f) + 6f;
        float btnBlock = 46f;
        float boardH = padTop + titleBlock + speakerBlock + bodyBlock + btnBlock + padBottom;
        boardH = Mathf.Clamp(boardH, 170f, 340f);
        float boardW = isDialogue ? 780f : 760f;

        if (_scriptPanelImg != null)
            _scriptPanelImg.rectTransform.sizeDelta = new Vector2(boardW, boardH);

        float y = -padTop;

        if (_scriptTitleUi != null)
        {
            _scriptTitleUi.gameObject.SetActive(hasTitle);
            if (hasTitle)
            {
                Font f = ResolveUiFont();
                _scriptTitleUi.font = f;
                PrepareFontForText(f, _scriptBoardTitle, titleSize, FontStyle.Bold);
                _scriptTitleUi.text = _scriptBoardTitle;
                _scriptTitleUi.color = _scriptBoardAccent;
                _scriptTitleUi.fontSize = titleSize;
                _scriptTitleUi.fontStyle = FontStyle.Bold;
                var trt = _scriptTitleUi.rectTransform;
                trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 1f);
                trt.sizeDelta = new Vector2(boardW - 48f, titleSize + 8f);
                trt.anchoredPosition = new Vector2(0f, y);
                y -= titleBlock;
            }
        }

        if (_scriptSpeakerUi != null)
        {
            _scriptSpeakerUi.gameObject.SetActive(hasSpeaker);
            if (hasSpeaker)
            {
                Font f = ResolveUiFont();
                _scriptSpeakerUi.font = f;
                PrepareFontForText(f, _scriptBoardSpeaker, speakerSize, FontStyle.Bold);
                _scriptSpeakerUi.text = _scriptBoardSpeaker;
                _scriptSpeakerUi.color = _scriptBoardAccent;
                _scriptSpeakerUi.fontSize = speakerSize;
                _scriptSpeakerUi.fontStyle = FontStyle.Bold;
                var srt = _scriptSpeakerUi.rectTransform;
                srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 1f);
                srt.sizeDelta = new Vector2(boardW - 56f, speakerSize + 6f);
                srt.anchoredPosition = new Vector2(0f, y);
                y -= speakerBlock;
            }
        }

        if (_scriptBodyUi != null)
        {
            Font f = ResolveUiFont();
            _scriptBodyUi.font = f;
            PrepareFontForText(f, _scriptBoardBody, bodySize, isDialogue ? FontStyle.Bold : FontStyle.Normal);
            _scriptBodyUi.text = _scriptBoardBody;
            _scriptBodyUi.color = bodyColor;
            _scriptBodyUi.fontSize = bodySize;
            _scriptBodyUi.fontStyle = isDialogue ? FontStyle.Bold : FontStyle.Normal;
            _scriptBodyUi.alignment = TextAnchor.UpperCenter;
            var brt = _scriptBodyUi.rectTransform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 1f);
            brt.sizeDelta = new Vector2(boardW - 56f, bodyBlock);
            brt.anchoredPosition = new Vector2(0f, y);
        }

        if (_scriptAccentImg != null)
            _scriptAccentImg.color = _scriptBoardAccent;

        if (_scriptBtn != null)
        {
            var btnRt = _scriptBtn.GetComponent<RectTransform>();
            if (btnRt != null)
            {
                btnRt.sizeDelta = new Vector2(Mathf.Min(460f, boardW - 80f), 40f);
                btnRt.anchoredPosition = new Vector2(0f, 12f);
            }
            var img = _scriptBtn.targetGraphic as Image;
            if (img != null)
                img.color = _scriptBoardIsDive
                    ? new Color(0.12f, 0.48f, 0.82f, 0.85f)
                    : new Color(0.10f, 0.32f, 0.48f, 0.80f);
        }

        if (_scriptHintUi != null)
        {
            string hint = _scriptBoardIsDive
                ? "【Space / クリック】空の裂け目へダイブ"
                : "【Space / クリック】つづき";
            Font f = ResolveUiFont();
            _scriptHintUi.font = f;
            PrepareFontForText(f, hint, 16, FontStyle.Bold);
            _scriptHintUi.text = hint;
            _scriptHintUi.fontSize = 16;
            _scriptHintUi.color = new Color(1f, 0.96f, 0.88f, 1f);
        }
    }

    static Text MakeScriptText(Transform parent, string name, Vector2 pos, Vector2 anchor, Vector2 size, int fontSize, TextAnchor align, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        return text;
    }

    static Font _cachedUiFont;

    static Font ResolveUiFont()
    {
        // 壊れた OS フォント（Hiragino Sans）を掴んだまま残さない
        if (_cachedUiFont != null)
        {
            try
            {
                CharacterInfo info;
                _cachedUiFont.RequestCharactersInTexture("あ", 28, FontStyle.Normal);
                if (!_cachedUiFont.GetCharacterInfo('あ', out info, 28, FontStyle.Normal))
                    _cachedUiFont = null;
            }
            catch { _cachedUiFont = null; }
        }
        if (_cachedUiFont != null) return _cachedUiFont;
        _cachedUiFont = CreateJapaneseFont(28);
        return _cachedUiFont;
    }

    /// <summary>
    /// EditorのGUI.skin.fontは使わない（Play中に「Gizmos」等のEditor文字が化けるため）。
    /// Unity 6 では "Hiragino Sans" が face 読み込みに失敗するため除外する。
    /// </summary>
    static Font CreateJapaneseFont(int size)
    {
        string[] candidates =
        {
            "HiraginoSans-W3",
            "HiraginoSans-W6",
            "Hiragino Kaku Gothic ProN",
            "Hiragino Kaku Gothic ProN W3",
            "YuGothic",
            "Yu Gothic",
            "YuGothic-Medium",
            "Apple SD Gothic Neo",
            "Arial Unicode MS",
            "Helvetica Neue"
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            try
            {
                Font font = Font.CreateDynamicFontFromOSFont(candidates[i], size);
                if (font == null) continue;
                font.RequestCharactersInTexture("あA", size, FontStyle.Normal);
                CharacterInfo info;
                if (font.GetCharacterInfo('あ', out info, size, FontStyle.Normal) ||
                    font.GetCharacterInfo('A', out info, size, FontStyle.Normal))
                    return font;
            }
            catch { }
        }

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
               ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    static void PrepareFontForText(Font font, string text, int fontSize, FontStyle style = FontStyle.Normal)
    {
        if (font == null || string.IsNullOrEmpty(text)) return;
        font.RequestCharactersInTexture(text, fontSize, style);
        font.RequestCharactersInTexture(text, fontSize, FontStyle.Normal);
        font.RequestCharactersInTexture(text, fontSize, FontStyle.Bold);
    }

    // PollScriptBoardAdvance と同一内容のため CheckScriptBoardAdvanceInput は削除
    // （PollScriptBoardAdvance を直接使用する）

    #endregion

    #region 7. 天蓋破壊VFX & 上昇気流光柱

    void SpawnLeverSparks(Vector3 pos)
    {
        var pGo = new GameObject("LeverSparkBurst");
        pGo.transform.position = pos;
        var ps = pGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startSpeed = 7f;
        main.startLifetime = 0.6f;
        main.startSize = 0.18f;
        main.startColor = new Color(1.0f, 0.85f, 0.35f);
        main.loop = false;
        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 35) });
        ps.Play();
        Destroy(pGo, 1.5f);
    }

    void SpawnSkybreakCracks(Vector3 skyCenter)
    {
        var crackGo = new GameObject("SkybreakEffect");
        crackGo.transform.position = skyCenter;
        var ps = crackGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startSpeed = 22f;
        main.startLifetime = 4.5f;
        main.startSize = 1.8f;
        main.startColor = new Color(0.35f, 0.95f, 1.0f, 0.85f); // シアンと黄金のガラス片破片
        main.loop = true;
        main.maxParticles = 180;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 45f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        var rend = crackGo.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0.45f, 0.95f, 1.0f, 0.85f));
            rend.material = mat;
        }

        ps.Play();
    }

    void BuildSkybreakHyperUpdraft(Vector3 basePos)
    {
        if (_hyperUpdraftGo != null)
        {
            Destroy(_hyperUpdraftGo);
        }

        // 中央オベリスク／頂上レバーが光の柱を塞いで上昇不能になるのを防ぐ
        ClearSkybreakAscentBlockers();

        _hyperUpdraftGo = new GameObject("SkybreakHyperUpdraft");
        _hyperUpdraftGo.transform.position = basePos;

        var updraft = _hyperUpdraftGo.AddComponent<AdventureThermalUpdraft>();
        updraft.autoLaunch = true; // 歩いて触れるだけでも自動で大空へダイブ・射出！
        updraft.radius = 48.0f; // 離れても上昇が切れないよう広め
        updraft.height = 320.0f;
        updraft.liftSpeed = 36.0f;
        updraft.pullToCenter = true;

        // 天を衝く超巨大な天空光柱（シアン＆黄金に輝く半透明シリンダー）
        var pillarGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillarGo.name = "SkybreakHyperBeam";
        pillarGo.transform.SetParent(_hyperUpdraftGo.transform, false);
        pillarGo.transform.localPosition = new Vector3(0f, 120f, 0f);
        pillarGo.transform.localScale = new Vector3(22f, 120f, 22f);

        // コライダーは不要（UpdraftのTriggerのみ使用）
        var pCol = pillarGo.GetComponent<Collider>();
        if (pCol != null) Destroy(pCol);

        var pRend = pillarGo.GetComponent<Renderer>();
        if (pRend != null)
        {
            var pShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var pMat = new Material(pShader);
            pMat.SetColor("_BaseColor", new Color(0.40f, 0.95f, 1.0f, 0.35f));
            pRend.material = pMat;
        }

        // 光柱の中心コアライト
        var lightGo = new GameObject("SkybreakBeamLight");
        lightGo.transform.SetParent(_hyperUpdraftGo.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 15f, 0f);
        var bLight = lightGo.AddComponent<Light>();
        bLight.type = LightType.Point;
        bLight.range = 65f;
        bLight.intensity = 6.5f;
        bLight.color = new Color(0.4f, 0.95f, 1.0f);
    }

    /// <summary>光の柱上昇を塞ぐコライダーを徹底除去（中央オベリスク／頂上レバー／柱内障害）</summary>
    static void ClearSkybreakAscentBlockers()
    {
        DisableCollidersOn("SanctuaryTopLeverStructure");
        // 四方レバーのビーコン等は柱外だが、中央付近の固体も念のため
        var top = GameObject.Find("SanctuaryTopLeverStructure");
        if (top != null)
            top.SetActive(false);

        var tower = GameObject.Find("SanctuaryZero_Tower");
        if (tower != null)
        {
            var mono = tower.transform.Find("CentralMonolith");
            if (mono != null)
            {
                var cols = mono.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i] != null)
                        cols[i].enabled = false;
                }
            }

            // タワー配下で柱シャフト内（中央付近・高所）の固体コライダーを無効化
            var all = tower.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var col = all[i];
                if (col == null || col.isTrigger) continue;
                string n = col.gameObject.name;
                if (n.IndexOf("SolidWalk", System.StringComparison.Ordinal) >= 0) continue;
                if (n.IndexOf("Podium", System.StringComparison.Ordinal) >= 0 && col.bounds.center.y < 70f) continue;
                if (n.IndexOf("GridFloor", System.StringComparison.Ordinal) >= 0 && col.bounds.center.y < 70f) continue;

                Vector3 c = col.bounds.center;
                float dx = c.x - 512f;
                float dz = c.z - 512f;
                if (dx * dx + dz * dz < 16f * 16f && c.y > 68f)
                    col.enabled = false;
            }
        }

        // ワールド全体：光の柱カプセル内の固体を無効化
        // ※ TerrainCollider は島全体を覆う巨大コライダーなので絶対に触らない
        //   （無効化すると接地判定が消え、空中で止まったように見える）
        var hits = Physics.OverlapCapsule(
            new Vector3(512f, 68f, 512f),
            new Vector3(512f, 260f, 512f),
            12f,
            ~0,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h == null) continue;
            if (h is TerrainCollider) continue;
            if (h.GetComponentInParent<Terrain>() != null) continue;
            if (h.GetComponentInParent<AdventurePlayerController>() != null) continue;
            if (h.GetComponentInParent<AdventureThermalUpdraft>() != null) continue;
            if (h.GetComponentInParent<AdventureRustDrone>() != null) continue;
            // テラス床は残す
            if (h.bounds.max.y < 68f) continue;
            h.enabled = false;
        }

        EnsureLandTerrainColliderEnabled();
    }

    /// <summary>島の歩行面コライダーが誤って落ちていたら復帰させる</summary>
    static void EnsureLandTerrainColliderEnabled()
    {
        foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
        {
            if (terrain == null) continue;
            string n = terrain.name;
            if (n != "LandTerrain" && n != "IslandTerrain") continue;
            var col = terrain.GetComponent<TerrainCollider>();
            if (col != null && !col.enabled)
                col.enabled = true;
        }
    }

    static void DisableCollidersOn(string objectName)
    {
        var go = GameObject.Find(objectName);
        if (go == null) return;
        var cols = go.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
                cols[i].enabled = false;
        }
    }

    /// <summary>ニューゲーム用：上昇経路のコライダーを戻す</summary>
    static void RestoreSkybreakAscentBlockers()
    {
        var tower = GameObject.Find("SanctuaryZero_Tower");
        var mono = tower != null ? tower.transform.Find("CentralMonolith") : null;
        if (mono != null)
        {
            var cols = mono.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null)
                    cols[i].enabled = true;
            }
        }
        // 頂上レバーは BuildTowerLever で作り直される
    }

    #endregion

    #region 8. OnGUI & 入力フォールバック

    void OnGUI()
    {
        // レバー表示・台本・注油・クリアUIはすべて高精細uGUI（LeverPromptCanvas等）で一元管理。
        // 旧IMGUIの重複描画による半透明の空ボード（テキストなしの邪魔な枠）を完全排除。
    }

    /// <summary>台本：透明クリックのみ（文言はuGUI）</summary>
    void DrawScriptBoardInputFallback()
    {
        if (!_scriptBoardVisible) return;
        float w = Mathf.Min(640f, Screen.width * 0.85f);
        float h = 64f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height - 96f;
        GUI.color = new Color(1f, 1f, 1f, 0.01f);
        if (GUI.Button(new Rect(x, y, w, h), GUIContent.none))
        {
            if (Time.unscaledTime - _scriptBoardOpenedAt >= 0.45f)
                _scriptBoardAdvance = true;
        }
        GUI.color = Color.white;

        Event e = Event.current;
        if (e == null) return;
        if (Time.unscaledTime - _scriptBoardOpenedAt < 0.45f) return;
        if (e.type == EventType.KeyDown &&
            (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.E))
        {
            _scriptBoardAdvance = true;
            e.Use();
        }
        if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
        {
            _scriptBoardAdvance = true;
            e.Use();
        }
    }

    /// <summary>注油：透明長押しヒットのみ（文言・ゲージはuGUI）</summary>
    void DrawClimaxOilInputFallback()
    {
        if (!IsClimaxOilPromptActive) return;
        float panelW = Mathf.Min(980f, Screen.width * 0.9f);
        float panelH = Mathf.Clamp(Screen.height * 0.36f, 260f, 360f);
        float px = (Screen.width - panelW) * 0.5f;
        float py = (Screen.height - panelH) * 0.5f;
        GUI.color = new Color(1f, 1f, 1f, 0.01f);
        if (GUI.RepeatButton(new Rect(px, py, panelW, panelH), GUIContent.none))
            _oilHoldTimer += Time.unscaledDeltaTime;
        float btnW = Mathf.Min(720f, Screen.width * 0.85f);
        float btnH = 72f;
        float barH = Mathf.Clamp(Screen.height * 0.11f, 70f, 120f);
        if (GUI.RepeatButton(new Rect((Screen.width - btnW) * 0.5f, Screen.height - barH - 12f - btnH, btnW, btnH), GUIContent.none))
            _oilHoldTimer += Time.unscaledDeltaTime;
        GUI.color = Color.white;
    }

    #endregion



    #region 11. ゲームクリアモーダルUI
    // (ゲームクリアモーダルUI・入力・リダイブ処理は AdventureSanctuaryTowerManager.ClearUI.cs へ分離)
    #endregion

    #region 12. 外の世界パノラマ & 極寒環境霧

    void SpawnWildernessPanorama(bool coldCrisis = true)
        => AdventureSkybreakVisuals.SpawnWildernessPanorama(coldCrisis);

    void ApplySkybreakColdAtmosphere()
        => AdventureSkybreakVisuals.ApplyColdAtmosphere();

    void SoftenSkybreakColdAtmosphere()
        => AdventureSkybreakVisuals.SoftenColdAtmosphere();

    void ClearSkybreakColdAtmosphere()
        => AdventureSkybreakVisuals.ClearColdAtmosphere();

    #endregion

    #region 13. 古代アプローチ階段 & 効果音合成
    // (古代アプローチ階段・岩場緩和は Structure.cs、効果音・祝祭チャイム合成は Audio.cs へ分離)
    #endregion
}
