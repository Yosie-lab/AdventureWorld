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
        // 北・東・西・南（階段入口手前まで）にそれぞれ滑らかな斜面Boxコライダーを配置
        BuildPodiumEntryRamps(tower.transform);
    }

    /// <summary>テラス（63m）と周囲の台地（62m）をつなぐ進入スロープコライダーを4方向に生成</summary>
    void BuildPodiumEntryRamps(Transform towerRoot)
    {
        // 既存スロープを削除して多重生成を防止
        var oldRamps = towerRoot.Find("PodiumEntryRamps");
        if (oldRamps != null) Destroy(oldRamps.gameObject);

        var rampsRoot = new GameObject("PodiumEntryRamps");
        rampsRoot.transform.SetParent(towerRoot, false);
        rampsRoot.transform.position = Vector3.zero;

        // テラス中心: (512, 63.0, 512)、半径34.5m
        // スロープ仕様: 幅 8m、地面側始点 Y=62.0m（テラス外縁+2m手前）、テラス側終点 Y=63.0m
        const float terraceTopY  = 63.0f;   // テラス上面
        const float groundY      = 62.0f;   // 周囲台地面
        const float rampLength   = 5.0f;    // スロープの斜面長（XZ投影）
        const float rampWidth    = 8.0f;    // スロープ幅
        const float rampThickness = 0.8f;   // コライダーの物理厚み（段差スタック防止）

        // (方位角, 注記) — 南側は古代階段が通るので除外
        float[] rampAngles = { 0f, 90f, 180f }; // 北(0°=z+)、東(90°=x+)、西(180°=x-)
        // Note: 角度は「テラス中心から外側に向かう」方向(deg)。
        // z+ = 北(0°), x+ = 東(90°), x- = 西(270°)
        float[] outDirs = { 0f, 90f, 270f };

        Vector3 terraceCenter = new Vector3(512f, terraceTopY, 512f);
        float terraceRadius   = 34.5f;

        foreach (float outDeg in outDirs)
        {
            float rad = outDeg * Mathf.Deg2Rad;
            // 外向き単位ベクトル
            Vector3 outDir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

            // スロープの中心位置：テラス縁 + rampLength/2 分だけ外側
            Vector3 slopeMid = new Vector3(512f, 0f, 512f)
                               + outDir * (terraceRadius + rampLength * 0.5f);
            // Y: 地面〜テラスの中間点
            slopeMid.y = (groundY + terraceTopY) * 0.5f;

            // 回転：外向き方向（Z+ 前）を法線方向へ
            Quaternion rot = Quaternion.LookRotation(outDir, Vector3.up);

            // 傾き角度 θ = atan((63-62) / rampLength)
            float slopeAngle = Mathf.Atan2(terraceTopY - groundY, rampLength) * Mathf.Rad2Deg;

            var rampGo = new GameObject($"PodiumRamp_{outDeg:F0}deg");
            rampGo.transform.SetParent(rampsRoot.transform, false);
            rampGo.transform.position = slopeMid;
            // OutDir 方向へ pitch（X軸回転）で傾ける
            rampGo.transform.rotation = rot * Quaternion.Euler(-slopeAngle, 0f, 0f);

            var box = rampGo.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size   = new Vector3(rampWidth, rampThickness, rampLength);
        }
    }

    /// <summary>タワー白亜テラス上（またはレバー基壇上、スロープ上）なら歩行面Y、それ以外は負の無限大</summary>
    public static float GetTerraceSurfaceY(Vector3 worldPos)
    {
        Vector2 posXZ = new Vector2(worldPos.x, worldPos.z);

        // 1. 南正面メインレバー台座の範囲（上面 64.02m、半径3.2m）
        Vector2 mainLeverCenter = new Vector2(512f, 501.5f);
        if (Vector2.SqrMagnitude(posXZ - mainLeverCenter) <= 3.2f * 3.2f)
        {
            return 64.02f; // レバー台座の上面
        }

        // 1b. レバー台座アプローチスロープ（台座南側、テラス上面63m → 台座上64.02m）
        // XZ: x∈[508,516], z∈[476,501.5], 線形補間でY算出
        if (posXZ.x >= 508f && posXZ.x <= 516f && posXZ.y >= 476f && posXZ.y <= 501.5f)
        {
            float t = Mathf.InverseLerp(501.5f, 476f, posXZ.y); // 台座に近い=0、遠い=1
            // 台座前（z=490付近）まではテラスと同高度、それより台座に向かってステップ
            // ステップ範囲 z=490〜501.5 のみ高さ変化
            if (posXZ.y >= 490f)
            {
                float stepT = Mathf.InverseLerp(490f, 501.5f, posXZ.y);
                return Mathf.Lerp(63.00f, 64.02f, stepT);
            }
            return 63.00f;
        }

        // 2. 白大理石円盤テラスの範囲（中心 512, 512、半径 34.5m、上面 63.00m）
        Vector2 towerCenter = new Vector2(512f, 512f);
        if (Vector2.SqrMagnitude(posXZ - towerCenter) <= 34.5f * 34.5f)
        {
            return 63.00f; // テラス上面
        }

        // 3. テラス外周スロープ（台地62m → テラス63m）の補間歩行面
        // 北(z+)・東(x+)・西(x-) の3方向スロープ、各幅8m、奥行5m
        const float terraceTopY = 63.0f;
        const float groundTopY  = 62.0f;
        const float rampLen     = 5.0f;
        const float rampHalf    = 4.0f; // 幅/2
        const float terraceR    = 34.5f;
        // 北スロープ: z∈[512+34.5, 512+34.5+5], x∈[512-4, 512+4]
        // 東スロープ: x∈[512+34.5, 512+34.5+5], z∈[512-4, 512+4]
        // 西スロープ: x∈[512-34.5-5, 512-34.5], z∈[512-4, 512+4]
        float cx = 512f, cz = 512f;
        // 北スロープ
        float northInner = cz + terraceR;
        float northOuter = northInner + rampLen;
        if (posXZ.x >= cx - rampHalf && posXZ.x <= cx + rampHalf
            && posXZ.y >= northInner   && posXZ.y <= northOuter)
        {
            float t = Mathf.InverseLerp(northInner, northOuter, posXZ.y);
            return Mathf.Lerp(terraceTopY, groundTopY, t);
        }
        // 東スロープ
        float eastInner = cx + terraceR;
        float eastOuter = eastInner + rampLen;
        if (posXZ.x >= eastInner   && posXZ.x <= eastOuter
            && posXZ.y >= cz - rampHalf && posXZ.y <= cz + rampHalf)
        {
            float t = Mathf.InverseLerp(eastInner, eastOuter, posXZ.x);
            return Mathf.Lerp(terraceTopY, groundTopY, t);
        }
        // 西スロープ
        float westInner = cx - terraceR;
        float westOuter = westInner - rampLen;
        if (posXZ.x >= westOuter   && posXZ.x <= westInner
            && posXZ.y >= cz - rampHalf && posXZ.y <= cz + rampHalf)
        {
            float t = Mathf.InverseLerp(westInner, westOuter, posXZ.x);
            return Mathf.Lerp(terraceTopY, groundTopY, t);
        }

        // それ以外の台地全域（芝生・木立・森林）はTerrain（自然な地面・標高62.0m）を歩行
        return float.NegativeInfinity;
    }

    public const float TerraceTopY = 64.02f;

    /// <summary>台座に完全に潜って落下したNikoをテラス上面へ安全救出する（通常歩行中は一切介入しない）</summary>
    public void RescuePlayerIfBuriedInTerrace(AdventurePlayerController player)
    {
        if (player == null) return;
        if (player.IsSkybreakPillarAscending || player.IsAutoGliding) return;
        if (IsCanopyBroken && player.transform.position.y > 80f) return;

        Vector3 pos = player.transform.position;
        float terraceY = GetTerraceSurfaceY(pos);
        if (terraceY <= float.NegativeInfinity) return;

        // 通常の歩行やジャンプ中は物理コライダーに任せ、テレポートを行わない
        // 床下深く（0.6m以上下）に完全に潜り込んでしまった時のみ緊急救出
        if (pos.y < terraceY - 0.60f)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = new Vector3(pos.x, terraceY + 0.02f, pos.z);
            if (cc != null) cc.enabled = true;
            player.ForceGroundReset();
        }
    }

    void SetupAudio()
    {
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.spatialBlend = 0.5f;
        _audio.minDistance = 6f;
        _audio.maxDistance = 50f;
    }

    readonly Vector3 _mainLeverPos = new Vector3(512f, 63.2f, 501.5f); // オベリスク南側正面・白亜テラスの特等席
    readonly Vector3 _topLeverPos = new Vector3(512f, 137.2f, 512f);    // オベリスク天面頂上
    Transform _topLeverHandle;

    public Vector3 MainLeverPosition => _mainLeverPos;

    #endregion

    #region 3. 台座レバーギミック & プロンプトUI

    void BuildTowerLever()
    {
        // 既に正常なメインレバーが存在している場合でも、見た目強化のため再生成する
        // （古いしょぼいマテリアル／簡素ジオメトリを残さない）

        // 1. 自階層配下にある古いレバーオブジェクトを即座に完全一掃
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child != null && (child.name.Contains("LeverStructure") || child.name.Contains("SanctuaryLever")))
            {
                DestroyImmediate(child.gameObject);
            }
        }

        // 2. シーン内にあるすべての古いレバーオブジェクトを根こそぎ即時完全消去
        var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var go in allObjects)
        {
            if (go == null) continue;
            if (go.name == "SanctuaryLeverStructure" || go.name == "SanctuaryWestLeverStructure" ||
                go.name == "SanctuaryNorthLeverStructure" || go.name == "SanctuaryEastLeverStructure" ||
                go.name == "SanctuaryTopLeverStructure")
            {
                DestroyImmediate(go);
            }
        }

        _allLeverHandles.Clear();
        _allLeverLights.Clear();
        _leverHandle = null;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // 大理石台座マテリアル
        var pedMat = new Material(shader);
        pedMat.SetColor("_BaseColor", new Color(0.94f, 0.96f, 0.98f));
        pedMat.SetFloat("_Smoothness", 0.92f);
        if (pedMat.HasProperty("_Metallic")) pedMat.SetFloat("_Metallic", 0.08f);

        // 黄金真鍮ハウジングマテリアル
        var hMat = new Material(shader);
        hMat.SetColor("_BaseColor", new Color(0.92f, 0.72f, 0.28f));
        hMat.SetFloat("_Metallic", 0.98f);
        hMat.SetFloat("_Smoothness", 0.88f);
        if (hMat.HasProperty("_EmissionColor"))
        {
            hMat.EnableKeyword("_EMISSION");
            hMat.SetColor("_EmissionColor", new Color(0.55f, 0.35f, 0.08f) * 1.4f);
        }

        // シャフトマテリアル
        var sMat = new Material(shader);
        sMat.SetColor("_BaseColor", new Color(0.95f, 0.78f, 0.32f));
        sMat.SetFloat("_Metallic", 0.96f);
        sMat.SetFloat("_Smoothness", 0.86f);

        // グリップ球マテリアル（真紅＋発光）
        var gMat = new Material(shader);
        gMat.SetColor("_BaseColor", new Color(0.88f, 0.12f, 0.10f));
        gMat.SetFloat("_Smoothness", 0.78f);
        if (gMat.HasProperty("_EmissionColor"))
        {
            gMat.EnableKeyword("_EMISSION");
            gMat.SetColor("_EmissionColor", new Color(0.85f, 0.08f, 0.05f) * 2.2f);
        }

        // 天を衝く光の柱マテリアル（シアン発光、半透明加算ブレンドで遠景から美しく輝く）
        var bShader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("RustAndFloat/WhiteSmoke")
            ?? Shader.Find("Sprites/Default");
        var bMat = new Material(bShader);
        bMat.SetTexture("_BaseMap", AdventureRustDrone.GetSoftSmokeTexture());
        bMat.SetColor("_BaseColor", new Color(0.45f, 0.98f, 1.0f, 0.82f));
        if (bMat.HasProperty("_Surface")) bMat.SetFloat("_Surface", 1f);
        if (bMat.HasProperty("_Blend")) bMat.SetFloat("_Blend", 1f);
        if (bMat.HasProperty("_Cull")) bMat.SetFloat("_Cull", 0f);
        if (bMat.HasProperty("_ZWrite")) bMat.SetFloat("_ZWrite", 0f);
        bMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        bMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        bMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        bMat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        bMat.renderQueue = 3150;

        // ── 南側正面メインレバー1基のみを堂々と配備（白亜テラスの特等席） ──
        CreateLeverStation("SanctuaryLeverStructure", _mainLeverPos, Quaternion.identity, pedMat, hMat, sMat, gMat, bMat, true);
    }

    void CreateLeverStation(string name, Vector3 worldPos, Quaternion rotation,
                            Material pedMat, Material hMat, Material sMat, Material gMat, Material bMat,
                            bool isMain)
    {
        var root = new GameObject(name);
        root.transform.SetParent(transform, false);
        root.transform.position = worldPos;
        root.transform.rotation = rotation;

        // 白亜大理石の巨大円形台座（遠くから一目で分かるサイズ）
        var ped = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ped.name = "LeverPedestal";
        ped.transform.SetParent(root.transform, false);
        ped.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        ped.transform.localScale = new Vector3(6.2f, 0.55f, 6.2f);
        // CapsuleCollider＋非均一スケールは約6m球になりNikoを埋める → スケール非依存の固体箱
        foreach (var c in ped.GetComponents<Collider>())
            Destroy(c);
        var pedSolid = new GameObject("LeverPedestal_SolidWalk");
        pedSolid.transform.SetParent(root.transform, false);
        pedSolid.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        pedSolid.transform.localRotation = Quaternion.identity;
        pedSolid.transform.localScale = Vector3.one;
        var pedBox = pedSolid.AddComponent<BoxCollider>();
        pedBox.center = Vector3.zero;
        pedBox.size = new Vector3(6.2f, 1.1f, 6.2f);
        var pedMr = ped.GetComponent<MeshRenderer>();
        if (pedMr != null) pedMr.material = pedMat;

        // 真鍮ギアハウジング（大型）
        var housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        housing.name = "BrassGearHousing";
        housing.transform.SetParent(root.transform, false);
        housing.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        housing.transform.localScale = new Vector3(2.4f, 1.05f, 1.9f);
        var hMr = housing.GetComponent<MeshRenderer>();
        if (hMr != null) hMr.material = hMat;

        // レバーピボット
        var pivot = new GameObject("LeverPivot");
        pivot.transform.SetParent(root.transform, false);
        pivot.transform.localPosition = new Vector3(0f, 2.05f, 0f);
        pivot.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);

        if (isMain) _leverHandle = pivot.transform;
        _allLeverHandles.Add(pivot.transform);

        // シャフト（太い真鍮棒）
        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Shaft";
        shaft.transform.SetParent(pivot.transform, false);
        shaft.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        shaft.transform.localScale = new Vector3(0.32f, 0.95f, 0.32f);
        Destroy(shaft.GetComponent<Collider>());
        var sMr = shaft.GetComponent<MeshRenderer>();
        if (sMr != null) sMr.material = sMat;

        // 深紅グリップ球（大きく掴みやすい）
        var grip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        grip.name = "GripBall";
        grip.transform.SetParent(pivot.transform, false);
        grip.transform.localPosition = new Vector3(0f, 1.95f, 0f);
        grip.transform.localScale = Vector3.one * 0.95f;
        Destroy(grip.GetComponent<Collider>());
        var gMr = grip.GetComponent<MeshRenderer>();
        if (gMr != null) gMr.material = gMat;

        // 真鍮フランジ／ギア装飾（貧相な棒＋球だけに見せない）
        var flange = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        flange.name = "BrassFlange";
        flange.transform.SetParent(root.transform, false);
        flange.transform.localPosition = new Vector3(0f, 1.12f, 0f);
        flange.transform.localScale = new Vector3(3.4f, 0.12f, 3.4f);
        Destroy(flange.GetComponent<Collider>());
        var fMr = flange.GetComponent<MeshRenderer>();
        if (fMr != null) fMr.material = hMat;

        var gear = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        gear.name = "BrassGearDisc";
        gear.transform.SetParent(root.transform, false);
        gear.transform.localPosition = new Vector3(0f, 1.72f, -0.55f);
        gear.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        gear.transform.localScale = new Vector3(1.6f, 0.18f, 1.6f);
        Destroy(gear.GetComponent<Collider>());
        var gearMr = gear.GetComponent<MeshRenderer>();
        if (gearMr != null) gearMr.material = sMat;

        var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.name = "MarbleRim";
        rim.transform.SetParent(root.transform, false);
        rim.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        rim.transform.localScale = new Vector3(7.0f, 0.12f, 7.0f);
        Destroy(rim.GetComponent<Collider>());
        var rimMr = rim.GetComponent<MeshRenderer>();
        if (rimMr != null) rimMr.material = pedMat;

        // 天を衝く巨大光柱ビーコン＋外周オーラ
        var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "LeverSkyBeacon";
        beacon.transform.SetParent(root.transform, false);
        beacon.transform.localPosition = new Vector3(0f, 48f, 0f);
        beacon.transform.localScale = new Vector3(1.85f, 48f, 1.85f);
        Destroy(beacon.GetComponent<Collider>());
        var bRend = beacon.GetComponent<Renderer>();
        if (bRend != null) bRend.material = bMat;

        var aura = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        aura.name = "LeverSkyBeaconAura";
        aura.transform.SetParent(root.transform, false);
        aura.transform.localPosition = new Vector3(0f, 42f, 0f);
        aura.transform.localScale = new Vector3(4.2f, 42f, 4.2f);
        Destroy(aura.GetComponent<Collider>());
        var auraMat = new Material(bMat);
        auraMat.SetColor("_BaseColor", new Color(0.55f, 0.92f, 1f, 0.28f));
        var aRend = aura.GetComponent<Renderer>();
        if (aRend != null) aRend.material = auraMat;

        // 発光インジケーターライト（コア＋広いリム）
        var lightGo = new GameObject("LeverIndicatorLight");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 3.8f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.45f, 0.98f, 1.0f);
        light.intensity = 11.5f;
        light.range = 55f;

        var rimLightGo = new GameObject("LeverRimLight");
        rimLightGo.transform.SetParent(root.transform, false);
        rimLightGo.transform.localPosition = new Vector3(0f, 2.2f, 1.2f);
        var rimLight = rimLightGo.AddComponent<Light>();
        rimLight.type = LightType.Point;
        rimLight.color = new Color(1f, 0.78f, 0.35f);
        rimLight.intensity = 4.5f;
        rimLight.range = 18f;

        if (isMain) _leverLight = light;
        _allLeverLights.Add(light);
        _allLeverLights.Add(rimLight);

        // 接近判定トリガー（大型レバーに合わせて広め）
        var col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 12.0f;

        // ── レバー台座アプローチステップ（南側 3段）──
        // テラス上面 63.0m → 台座上面 64.02m を3段（各約0.34m）で登れるよう
        // 大理石ステップビジュアル＋コライダーを台座南側に設置
        if (isMain)
            BuildLeverApproachSteps(root.transform, worldPos);
    }

    /// <summary>レバー台座（LeverPedestal）南側に Niko が歩いて登れる3段ステップを生成</summary>
    void BuildLeverApproachSteps(Transform leverRoot, Vector3 leverWorldPos)
    {
        // 既存ステップを削除して多重生成を防止
        var old = leverRoot.Find("LeverApproachSteps");
        if (old != null) Destroy(old.gameObject);

        var stepsRoot = new GameObject("LeverApproachSteps");
        stepsRoot.transform.SetParent(leverRoot, false);
        stepsRoot.transform.position = Vector3.zero;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var marbleMat = new Material(shader);
        marbleMat.SetColor("_BaseColor", new Color(0.92f, 0.94f, 0.96f)); // 純白大理石
        marbleMat.SetFloat("_Smoothness", 0.85f);

        // ステップ仕様:
        //   テラス上面 Y = 63.00m、台座上面 Y = 64.02m（差 1.02m）
        //   3段 → 1段あたり 0.34m
        //   台座半径 3.1m（XZ）、ステップは z- 方向（南側 = テラス外縁方向）から台座へ
        //   各ステップ: 幅 4.2m、奥行 1.2m、高さ（厚み）0.8m
        const float terraceY   = 63.00f;
        const float pedestalY  = 64.02f; // 台座上面
        int   numSteps   = 3;
        float stepHeight = (pedestalY - terraceY) / numSteps; // ≈ 0.34m
        float stepDepth  = 1.2f;   // 奥行き（z 方向）
        float stepWidth  = 4.2f;   // 幅
        float stepThick  = 0.8f;   // コライダー物理厚み

        // 台座南縁 Z = leverWorldPos.z - 3.1f あたりから始める
        // レバー位置: (512, 63.2, 501.5)  台座南縁 z ≈ 501.5 - 3.1 = 498.4
        float pedestalSouthEdgeZ = leverWorldPos.z - 3.1f;

        for (int i = 0; i < numSteps; i++)
        {
            // 南から台座へ向かって配置（i=0 が最も南＝テラス側）
            float stepTopY  = terraceY + stepHeight * (i + 1); // この段の上面 Y
            float stepMidZ  = pedestalSouthEdgeZ - stepDepth * (numSteps - 1 - i) - stepDepth * 0.5f;

            var stepGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stepGo.name = $"LeverStep_{i}";
            stepGo.transform.SetParent(stepsRoot.transform, false);
            stepGo.transform.position = new Vector3(leverWorldPos.x, stepTopY - stepThick * 0.5f, stepMidZ);
            stepGo.transform.rotation = Quaternion.identity;
            stepGo.transform.localScale = new Vector3(stepWidth, stepThick, stepDepth);

            var mr = stepGo.GetComponent<MeshRenderer>();
            if (mr != null) mr.material = marbleMat;
        }

        // スロープコライダー（ビジュアル不要・コライダーのみ）
        // 階段全体を覆う傾斜板: ステップスタック完全防止（stepOffset超え対策）
        float totalRampLen  = stepDepth * numSteps;
        float rampStartZ    = pedestalSouthEdgeZ - totalRampLen;
        float rampEndZ      = pedestalSouthEdgeZ;
        float rampMidZ      = (rampStartZ + rampEndZ) * 0.5f;
        float rampMidY      = (terraceY + pedestalY) * 0.5f;

        var rampGo = new GameObject("LeverStepRampCollider");
        rampGo.transform.SetParent(stepsRoot.transform, false);
        rampGo.transform.position = new Vector3(leverWorldPos.x, rampMidY, rampMidZ);

        float slopeAngle = Mathf.Atan2(pedestalY - terraceY, totalRampLen) * Mathf.Rad2Deg;
        // Z- 方向（南→台座 = z+ 方向）へ傾けるため pitch = -slopeAngle
        rampGo.transform.rotation = Quaternion.Euler(-slopeAngle, 0f, 0f);

        var rampBox = rampGo.AddComponent<BoxCollider>();
        rampBox.center = Vector3.zero;
        rampBox.size   = new Vector3(stepWidth, 0.8f, totalRampLen);
    }

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
        StartCoroutine(FadeAudioSource(_skybreakWindSource, 0.22f, 1.4f));
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

    IEnumerator FadeAudioSource(AudioSource src, float targetVol, float duration)
    {
        if (src == null) yield break;
        float start = src.volume;
        float t = 0f;
        while (t < duration && src != null)
        {
            t += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(start, targetVol, t / duration);
            yield return null;
        }
        if (src != null) src.volume = targetVol;
    }

    static AudioClip LoadOrSynthesizeSkybreakWindClip()
    {
        // Resources にあれば実音源を優先
        var fromRes = Resources.Load<AudioClip>("skywind_1");
        if (fromRes != null) return fromRes;

#if UNITY_EDITOR
        var fromEditor = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Audio/AudioFiles/03_amb/skywind_1.wav");
        if (fromEditor != null) return fromEditor;
#endif

        return SynthesizeColdWindClip();
    }

    static AudioClip SynthesizeColdWindClip()
    {
        const int rate = 22050;
        int count = rate * 4;
        float[] data = new float[count];
        float lp = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            float noise = (UnityEngine.Random.value * 2f - 1f);
            lp = Mathf.Lerp(lp, noise, 0.08f);
            float gust = 0.55f + 0.45f * Mathf.Sin(t * 0.7f) * Mathf.Sin(t * 1.3f + 0.4f);
            float low = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.12f;
            float env = 1f;
            if (i < rate / 5) env = i / (rate / 5f);
            else if (i > count - rate / 5) env = (count - i) / (rate / 5f);
            data[i] = (lp * 0.72f + low) * gust * env * 0.35f;
        }
        var clip = AudioClip.Create("SkybreakColdWind", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

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
             kb.numpadEnterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
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

    void TickGameClearModal()
    {
        if (!_showGameClearModal)
        {
            HideGameClearModalUI();
            return;
        }

        HideScriptBoardCompletely();
        TeardownScriptBoardUi();
        HideOilPromptUI();
        EnsureGameClearModalUI();
        if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        var kb = UnityEngine.InputSystem.Keyboard.current;
        bool spacePressed = kb != null && kb.spaceKey.wasPressedThisFrame;
        bool nPressed = kb != null && kb.nKey.wasPressedThisFrame;
        bool closePressed = kb != null && (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame);
        try
        {
            if (Input.GetKeyDown(KeyCode.Space)) spacePressed = true;
            if (Input.GetKeyDown(KeyCode.N)) nPressed = true;
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape)) closePressed = true;
        }
        catch { }

        if (spacePressed)
            RelaunchIntoSky();
        else if (nPressed)
            StartNewGameFromClearModal();
        else if (closePressed)
            CloseGameClearModalForFreeExplore();

        if (WasPointerPressedThisFrame(out Vector2 pointer))
        {
            if (PointerHits(_clearDiveBtn, pointer))
                RelaunchIntoSky();
            else if (PointerHits(_clearNewBtn, pointer))
                StartNewGameFromClearModal();
            else if (PointerHits(_clearCloseBtn, pointer))
                CloseGameClearModalForFreeExplore();
        }
    }

    static bool WasPointerPressedThisFrame(out Vector2 pointer)
    {
        pointer = Vector2.zero;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
            pointer = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
                return true;
        }
        try
        {
            if (Input.GetMouseButtonDown(0))
            {
                pointer = Input.mousePosition;
                return true;
            }
        }
        catch { }
        return false;
    }

    static bool PointerHits(RectTransform rt, Vector2 screenPos)
    {
        if (rt == null || !rt.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null);
    }

    void SetGameClearModalVisible(bool visible)
    {
        _showGameClearModal = visible;
        if (visible)
            EnsureGameClearModalUI();
        else
            HideGameClearModalUI();
    }

    void EnsureGameClearModalUI()
    {
        EnsureEventSystemForUi();
        if (_clearUiRoot != null)
        {
            _clearUiRoot.SetActive(true);
            if (_clearDiveBtn == null)
                _clearDiveBtn = _clearUiRoot.transform.Find("Panel/DiveBtn") as RectTransform;
            if (_clearNewBtn == null)
                _clearNewBtn = _clearUiRoot.transform.Find("Panel/NewBtn") as RectTransform;
            if (_clearCloseBtn == null)
                _clearCloseBtn = _clearUiRoot.transform.Find("Panel/CloseBtn") as RectTransform;
            return;
        }

        Font font = ResolveUiFont();
        var canvasGo = new GameObject("GameClearModalCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6000;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        canvasGo.AddComponent<GraphicRaycaster>();

        var dimGo = new GameObject("Dim");
        dimGo.transform.SetParent(canvasGo.transform, false);
        var dimRt = dimGo.AddComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        var dimImg = dimGo.AddComponent<Image>();
        dimImg.color = new Color(0.01f, 0.02f, 0.05f, 0.55f);
        dimImg.raycastTarget = true;

        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelGo.AddComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(980f, 560f);
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0.02f, 0.05f, 0.10f, 0.96f);

        var title = MakeScriptText(panelGo.transform, "Title", new Vector2(0f, -28f), new Vector2(0.5f, 1f), new Vector2(900f, 48f), 34, TextAnchor.MiddleCenter, font);
        title.color = new Color(1f, 0.88f, 0.4f, 1f);
        title.fontStyle = FontStyle.Bold;
        title.text = "✦ 『Rust & Float』 GAME CLEAR ✦";
        PrepareFontForText(font, title.text, 34, FontStyle.Bold);

        var body = MakeScriptText(panelGo.transform, "Body", new Vector2(0f, 20f), new Vector2(0.5f, 0.5f), new Vector2(880f, 280f), 24, TextAnchor.UpperCenter, font);
        body.color = new Color(0.9f, 0.95f, 1f, 1f);
        body.text =
            "天蓋の檻を打ち破り、二人は蒼い風が吹く空へ羽ばたいた。\n\n" +
            "✦ 漂着古代パーツ回収： 12 / 12\n" +
            "✦ 相棒Rust： 二段ジャンプ・超滑空・探知ソナー\n\n" +
            "「ありがとう、Niko。僕たちの翼で、どこまでも行こう……！」\n\n" +
            "【Space】大空へ　【N】はじめから　【E / Esc】閉じる";
        PrepareFontForText(font, body.text, 24);

        _clearDiveBtn = MakeClearModalButton(panelGo.transform, "DiveBtn", new Vector2(-300f, 36f), new Color(0.20f, 0.75f, 0.95f, 0.95f),
            "【Space】大空へダイブ", font, RelaunchIntoSky);
        _clearNewBtn = MakeClearModalButton(panelGo.transform, "NewBtn", new Vector2(0f, 36f), new Color(0.35f, 0.82f, 0.55f, 0.95f),
            "【N】はじめから", font, StartNewGameFromClearModal);
        _clearCloseBtn = MakeClearModalButton(panelGo.transform, "CloseBtn", new Vector2(300f, 36f), new Color(0.25f, 0.35f, 0.45f, 0.95f),
            "【E】閉じる", font, CloseGameClearModalForFreeExplore);

        _clearUiRoot = canvasGo;
    }

    RectTransform MakeClearModalButton(Transform parent, string name, Vector2 anchoredPos, Color color, string label, Font font, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(280f, 56f);
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(onClick);
        var text = MakeScriptText(go.transform, "Label", Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(260f, 48f), 20, TextAnchor.MiddleCenter, font);
        text.color = Color.white;
        text.fontStyle = FontStyle.Bold;
        text.text = label;
        text.raycastTarget = false;
        PrepareFontForText(font, label, 20, FontStyle.Bold);
        return rt;
    }

    void HideGameClearModalUI()
    {
        if (_clearUiRoot != null)
            _clearUiRoot.SetActive(false);
        var orphan = GameObject.Find("GameClearModalCanvas");
        if (orphan != null && orphan != _clearUiRoot)
            orphan.SetActive(false);
    }

    bool ConsumeClearModalAction()
    {
        if (_clearModalActionFrame == Time.frameCount) return false;
        _clearModalActionFrame = Time.frameCount;
        return true;
    }

    void StartNewGameFromClearModal()
    {
        if (!ConsumeClearModalAction()) return;
        _showGameClearModal = false;
        HideGameClearModalUI();
        var player = GetPlayer();
        if (player != null)
        {
            player.SetAutoGlideMode(false);
            player.ForceGroundReset();
        }
        SetCinematicCamera(false);
        var drone = GetDrone();
        if (drone != null)
        {
            drone.StopSkybreakNestle();
            drone.ResetClimaxState();
        }
        AdventureSaveManager.Ensure();
        AdventureSaveManager.Instance?.ResetToNewGame();
    }

    void CloseGameClearModalForFreeExplore()
    {
        if (!ConsumeClearModalAction()) return;
        _showGameClearModal = false;
        HideGameClearModalUI();
        AdventureMusicDirector.Ensure();
        AdventureMusicDirector.Instance?.RestoreExplorationTheme();
        var player = GetPlayer();
        if (player != null)
            player.SetAutoGlideMode(false);
        SetCinematicCamera(false);
        SetExplorationHudVisible(true);
        var drone = GetDrone();
        if (drone != null)
        {
            drone.StopSkybreakNestle();
            drone.ResetClimaxState();
        }
    }

    /// <summary>ゲームクリアリザルト画面を直接開く</summary>
    public void OpenGameClearModal()
    {
        SetGameClearModalVisible(true);
    }

    /// <summary>クリア後に何度でも大空へ飛び立てるリダイブ処理</summary>
    public void RelaunchIntoSky()
    {
        if (!ConsumeClearModalAction()) return;
        _showGameClearModal = false;
        HideGameClearModalUI();
        AdventureMusicDirector.Ensure();
        AdventureMusicDirector.Instance?.RestoreExplorationTheme();
        var player = GetPlayer();
        if (player != null)
        {
            // タワー上空の光柱へワープし、大空へ打ち上げ＆スーパー滑空
            player.SetAutoGlideMode(false);
            player.transform.position = new Vector3(512f, 110f, 512f);
            player.ApplyLaunchUpdraft(25f, 25f);
            player.ApplyGlideBoost(3.0f, 65f);
            player.SetAutoGlideMode(true, 120f);
        }
        SetCinematicCamera(true);
        var drone = GetDrone();
        if (drone != null)
        {
            drone.SpeakCustom("いっくよー！大空へダイブ！！", 5.0f);
        }
    }

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

    /// <summary>オアシス湧水池（480, 455）からタワー台地（512, 512）へ登る白亜の古代神殿アプローチ階段道を生成</summary>
    void BuildTowerStairs(Transform parent, Terrain land)
    {
        var old = parent.Find("SanctuaryApproachStairs");
        if (old != null)
            Destroy(old.gameObject);

        var stairsRoot = new GameObject("SanctuaryApproachStairs");
        stairsRoot.transform.SetParent(parent, false);

        Vector3 startP = new Vector3(472f, 48.5f, 455f); // オアシス池のほとり
        // 基壇南縁（中心512へ突っ込むと最後の段が固体台座に食い込む）
        Vector3 endP = new Vector3(512f, TerraceTopY, 478f);
        if (land != null)
        {
            startP.y = land.SampleHeight(startP) + land.transform.position.y + 0.2f;
            endP.y = Mathf.Max(TerraceTopY, land.SampleHeight(endP) + land.transform.position.y + 0.2f);
        }

        int steps = 22;
        float width = 4.8f;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var marbleMat = new Material(shader);
        marbleMat.SetColor("_BaseColor", new Color(0.92f, 0.94f, 0.96f)); // 純白大理石
        marbleMat.SetFloat("_Smoothness", 0.85f);

        var pillarMat = new Material(shader);
        pillarMat.SetColor("_BaseColor", new Color(0.82f, 0.85f, 0.88f));

        for (int i = 0; i < steps; i++)
        {
            float t0 = (float)i / steps;
            float t1 = (float)(i + 1) / steps;
            Vector3 p0 = Vector3.Lerp(startP, endP, t0);
            Vector3 p1 = Vector3.Lerp(startP, endP, t1);

            if (land != null)
            {
                p0.y = Mathf.Max(p0.y, land.SampleHeight(p0) + land.transform.position.y + 0.15f);
                p1.y = Mathf.Max(p1.y, land.SampleHeight(p1) + land.transform.position.y + 0.15f);
            }

            Vector3 center = (p0 + p1) * 0.5f;
            Vector3 forward = (p1 - p0);
            float len = forward.magnitude;
            if (len < 0.01f) continue;

            Vector3 fwdNorm = forward.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwdNorm).normalized;

            var stepObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stepObj.name = $"MarbleStep_{i}";
            stepObj.transform.SetParent(stairsRoot.transform, false);
            stepObj.transform.position = center;
            stepObj.transform.rotation = Quaternion.LookRotation(fwdNorm, Vector3.up);
            stepObj.transform.localScale = new Vector3(width, 0.32f, len * 1.05f);

            var mr = stepObj.GetComponent<MeshRenderer>();
            if (mr != null) mr.material = marbleMat;

            // ステップ自体の微小段差スタックを排除するため、デフォルトCubeコライダーは削除
            // （連続した傾斜スロープコライダー StairRamp が滑らかな登攀を担当する）
            var stepCol = stepObj.GetComponent<Collider>();
            if (stepCol != null) Object.Destroy(stepCol);

            // 4段ごとに両脇に白亜の装飾オベリスク支柱を配置
            if (i % 4 == 0)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    post.name = $"Pillar_{i}";
                    post.transform.SetParent(stairsRoot.transform, false);
                    post.transform.position = center + right * (side * (width * 0.5f + 0.35f)) + Vector3.up * 0.7f;
                    post.transform.localScale = new Vector3(0.24f, 0.7f, 0.24f);

                    var pmr = post.GetComponent<MeshRenderer>();
                    if (pmr != null) pmr.material = pillarMat;
                    var pcol = post.GetComponent<Collider>();
                    if (pcol != null) pcol.isTrigger = true;
                }
            }
        }

        // ── 木道スロープ式・連続傾斜コライダー（段差スタック完全解消） ──
        // 大理石ステップの美しいビジュアルはそのままに、段差ゼロで地面からテラスまで
        // 一気に駆け上がれる連続傾斜コライダーと進入ウェッジを配置する。
        AddStairsRampColliders(stairsRoot.transform, startP, endP, width);
        ClearTowerStreamWalkBlockers(startP, endP);
    }

    /// <summary>
    /// タワー手前のオアシス湧水池〜アプローチ階段まわりで、渓流岩場の歩行阻害を減らす。
    /// 見た目は100%残し、進路・階段周囲の岩コライダーを完全にトリガー化してスタックを根絶する。
    /// </summary>
    static void ClearTowerStreamWalkBlockers(Vector3 startP, Vector3 endP)
    {
        Vector3 oasis = new Vector3(480f, 48.2f, 455f);
        SoftenOrThinRocksAlongPath(startP, endP, corridorHalfWidth: 8.0f, thinHalfWidth: 4.0f, alongTMax: 0.85f);
        SoftenRockRootNearPoint("SanctuarySpringPond_Rocks", oasis, softRadius: 36f, thinRadius: 18f);
        SoftenRockRootNearPoint("MountainGorgeProps", oasis, softRadius: 44f, thinRadius: 20f);
        SoftenRockRootNearPoint("UpperParadiseStream", oasis, softRadius: 48f, thinRadius: 0f);
        SoftenRockRootNearPoint("BeachStepPonds_Rocks", oasis, softRadius: 36f, thinRadius: 0f);

        // 階段足元・池〜階段アプローチ全域の岩を強制トリガー化
        SoftenRocksNearStairFoot(startP);

        // 名称に Rock/Stone を含むオブジェクトも、階段〜池の回廊だけ追加で緩和
        SoftenLooseRocksNearCorridor(startP, endP, oasis);
    }

    static void SoftenOrThinRocksAlongPath(Vector3 startP, Vector3 endP, float corridorHalfWidth, float thinHalfWidth, float alongTMax)
    {
        var rocks = GameObject.Find("SanctuarySpringPond_Rocks");
        if (rocks == null) return;
        SoftenCollidersOnRoot(rocks.transform, startP, endP, corridorHalfWidth, thinHalfWidth, alongTMax, forceAllInRadius: false, center: default, softRadius: 0f, thinRadius: 0f);
    }

    static void SoftenRockRootNearPoint(string rootName, Vector3 center, float softRadius, float thinRadius)
    {
        var root = GameObject.Find(rootName);
        if (root == null) return;
        SoftenCollidersOnRoot(root.transform, default, default, 0f, 0f, 0f, forceAllInRadius: true, center: center, softRadius: softRadius, thinRadius: thinRadius);
    }

    /// <summary>
    /// アプローチ階段最下部（StairRamp_0〜1）および池〜階段進入路付近の岩コライダーを歩行阻害しないよう強制緩和。
    /// </summary>
    static void SoftenRocksNearStairFoot(Vector3 stairStart)
    {
        var all = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var col = all[i];
            if (col == null || col.isTrigger) continue;
            string n = col.gameObject.name;
            if (n.IndexOf("Rock", System.StringComparison.OrdinalIgnoreCase) < 0
                && n.IndexOf("Stone", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            Vector3 c = col.bounds.center;
            float dx = c.x - stairStart.x;
            float dz = c.z - stairStart.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            if (dist > 24f) continue;

            // 見た目は残し、足元の物理だけ外す
            col.isTrigger = true;
        }
    }

    static void SoftenLooseRocksNearCorridor(Vector3 startP, Vector3 endP, Vector3 oasis)
    {
        var all = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var col = all[i];
            if (col == null || col.isTrigger) continue;
            string n = col.gameObject.name;
            if (n.IndexOf("Rock", System.StringComparison.OrdinalIgnoreCase) < 0
                && n.IndexOf("Stone", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            Vector3 c = col.bounds.center;
            float distOasis = Vector2.Distance(new Vector2(c.x, c.z), new Vector2(oasis.x, oasis.z));
            float distPath = DistancePointToSegment(c, startP, endP);
            if (distOasis > 36f && distPath > 9f) continue;

            // 進路真上は間引き、近傍はコライダー無効
            if (distPath < 3.6f && distOasis < 28f)
            {
                col.gameObject.SetActive(false);
                continue;
            }
            if (distPath < 8.5f || distOasis < 24f)
                col.isTrigger = true;
        }
    }

    static void SoftenCollidersOnRoot(
        Transform root,
        Vector3 startP, Vector3 endP,
        float corridorHalfWidth, float thinHalfWidth, float alongTMax,
        bool forceAllInRadius, Vector3 center, float softRadius, float thinRadius)
    {
        var cols = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            var col = cols[i];
            if (col == null) continue;

            Vector3 c = col.bounds.center;
            bool soft = false;
            bool thin = false;

            if (forceAllInRadius)
            {
                float d = Vector2.Distance(new Vector2(c.x, c.z), new Vector2(center.x, center.z));
                if (thinRadius > 0.1f && d <= thinRadius)
                {
                    // 池の南側〜階段側（歩行ルート）の岩だけ間引き。北側の景色用は残す
                    if (c.z <= center.z + 2.5f || c.x <= center.x + 4f)
                        thin = true;
                    else
                        soft = true;
                }
                else if (d <= softRadius)
                {
                    soft = true;
                }
            }
            else
            {
                Vector3 ab = endP - startP;
                float denom = ab.sqrMagnitude;
                float t = denom < 0.01f ? 0f : Mathf.Clamp01(Vector3.Dot(c - startP, ab) / denom);
                if (t > alongTMax) continue;
                float dist = DistancePointToSegment(c, startP, endP);
                if (dist <= thinHalfWidth) thin = true;
                else if (dist <= corridorHalfWidth) soft = true;
            }

            if (thin)
            {
                // 見た目ごと減らす（進路を塞ぐ密集岩）
                if (col.transform != root)
                    col.gameObject.SetActive(false);
                else
                    col.isTrigger = true;
            }
            else if (soft)
            {
                col.isTrigger = true;
            }
        }
    }

    static float DistancePointToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float denom = ab.sqrMagnitude;
        if (denom < 0.01f) return Vector3.Distance(p, a);
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / denom);
        return Vector3.Distance(p, a + ab * t);
    }

    /// <summary>
    /// 古代階段全体をカバーする「滑らかな連続傾斜スロープコライダー」を配置する。
    /// 各段Cubeの物理コライダーを排除した上で、階段全域＋手前エントリー（導入部）＋奥テラス接続部を
    /// 完全にフラットな傾斜板で覆い、歩行・ダッシュの引っ掛かりをゼロにする。
    /// </summary>
    void AddStairsRampColliders(Transform parent, Vector3 startP, Vector3 endP, float width)
    {
        const int   segments       = 5;    // 階段 22 段を 5 区間に分割
        const float thickness      = 0.35f; // 物理厚み
        const float stepHalfHeight = 0.16f; // ステップ厚み 0.32m の上面オフセット

        var terrain = Terrain.activeTerrain ?? FindAnyObjectByType<Terrain>();

        // 1. 最下部手前からの進入用「エントリースロープ（進入ウェッジ）」
        // 地面からステップ最下段の上面へ滑らかに段差ゼロで誘導する
        Vector3 totalForward = (endP - startP).normalized;
        Vector3 entryStart = startP - totalForward * 2.8f;
        if (terrain != null)
        {
            entryStart.y = terrain.SampleHeight(entryStart) + terrain.transform.position.y - 0.15f;
        }
        else
        {
            entryStart.y = startP.y - 0.5f;
        }

        Vector3 entryEnd = startP;
        if (terrain != null)
        {
            entryEnd.y = Mathf.Max(startP.y, terrain.SampleHeight(entryEnd) + terrain.transform.position.y + 0.16f);
        }

        Vector3 entryCenter = (entryStart + entryEnd) * 0.5f;
        Vector3 entryFwd = entryEnd - entryStart;
        float entryLen = entryFwd.magnitude;
        if (entryLen > 0.05f)
        {
            var entryGo = new GameObject("StairRamp_Entry");
            entryGo.transform.SetParent(parent, false);
            entryGo.transform.position = entryCenter;
            entryGo.transform.rotation = Quaternion.LookRotation(entryFwd.normalized, Vector3.up);

            var entryBox = entryGo.AddComponent<BoxCollider>();
            entryBox.center = new Vector3(0f, stepHalfHeight - thickness * 0.5f, 0f);
            entryBox.size   = new Vector3(width * 1.15f, thickness, entryLen * 1.1f);
        }

        // 2. 階段本体のスロープセグメント
        for (int s = 0; s < segments; s++)
        {
            float t0 = (float)s       / segments;
            float t1 = (float)(s + 1) / segments;

            Vector3 p0 = Vector3.Lerp(startP, endP, t0);
            Vector3 p1 = Vector3.Lerp(startP, endP, t1);

            // 実際の地形高度に合わせて Y を補正
            if (terrain != null)
            {
                p0.y = Mathf.Max(p0.y, terrain.SampleHeight(p0) + terrain.transform.position.y + 0.16f);
                p1.y = Mathf.Max(p1.y, terrain.SampleHeight(p1) + terrain.transform.position.y + 0.16f);
            }

            Vector3 segCenter  = (p0 + p1) * 0.5f;
            Vector3 segForward = p1 - p0;
            float   segLen     = segForward.magnitude;
            if (segLen < 0.01f) continue;

            var rampGo = new GameObject($"StairRamp_{s}");
            rampGo.transform.SetParent(parent, false);
            rampGo.transform.position = segCenter;
            // LookRotationだけで上り傾斜方向へピッタリ揃う
            rampGo.transform.rotation = Quaternion.LookRotation(segForward.normalized, Vector3.up);
            rampGo.transform.localScale = Vector3.one;

            var box = rampGo.AddComponent<BoxCollider>();
            // 上面をステップ上面（+0.16f）に正確に一致させる
            box.center = new Vector3(0f, stepHalfHeight - thickness * 0.5f, 0f);
            // 隙間や段差が出ないよう、前後にわずかにラップ（1.06倍、終端はテラスに食い込ませる）
            box.size   = new Vector3(width, thickness, segLen * (s == segments - 1 ? 1.15f : 1.06f));
        }
    }

    static AudioClip MakeClankSound()
    {
        int rate = 22050;
        int count = rate / 4;
        float[] d = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            d[i] = Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t * 18f);
        }
        var clip = AudioClip.Create("Clank", count, 1, rate, false);
        clip.SetData(d, 0);
        return clip;
    }

    static AudioClip MakeHeavyLeverSound()
    {
        int rate = 22050;
        int count = (int)(rate * 0.65f);
        float[] d = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float snap = Mathf.Sin(2f * Mathf.PI * 90f * t) * Mathf.Exp(-t * 6f);
            float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 14f) * 0.4f;
            d[i] = snap + noise;
        }
        var clip = AudioClip.Create("HeavyLever", count, 1, rate, false);
        clip.SetData(d, 0);
        return clip;
    }

    #endregion
}
