using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 『Rust & Float』クライマックス：島中央の白亜タワー頂上
/// 「アナログ真鍮レバー」と天蓋破壊シークエンスを統括するマネージャー
/// </summary>
public class AdventureSanctuaryTowerManager : MonoBehaviour
{
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
    bool _coldAtmosphereActive;
    bool _savedFogEnabled;
    Color _savedFogColor;
    float _savedFogDensity;
    Color _savedAmbient;

    bool _leverPulled = false;
    bool _endingSequenceActive = false;
    bool _playerNearby = false;
    bool _epilogueTriggered = false;
    float _epilogueAlpha = 0f;
    int _epilogueAct = 0; // 0=帯のみ / 1〜3=字幕幕
    bool _showGameClearModal = false;
    Font _epilogueFont;

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
        new CanopyBeat("", "✦ Niko", "ありがとうRust…！あなたがいたからここまで来られたの。さあ行こう！", new Color(1f, 0.88f, 0.45f, 1f)),
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
    public bool IsEpiloguePlaying => _scriptBoardVisible || (_epilogueTriggered && !_showGameClearModal && (_epilogueAct > 0 || _epilogueAlpha > 0.01f));
    /// <summary>クライマックス注油待ち中</summary>
    public bool IsClimaxOilPromptActive =>
        _climaxOilWaiting && !_climaxOilInjected && !_suppressClimax;

    // 注油前台本(0-2) → 注油 → 注油後台本(3-4)
    const int ClimaxOilSlot = 3; // next==3 のとき注油フェーズへ入る
    static readonly CanopyBeat[] ClimaxBeats =
    {
        new CanopyBeat("警告", "", "Rustが極寒で機能停止寸前", new Color(1f, 0.55f, 0.45f, 1f)),
        new CanopyBeat("", "✦ 相棒 Rust", "キキキッ……！ Niko……外の気流が冷たすぎる……僕の古いギアが……凍りついて……", new Color(0.35f, 0.92f, 0.98f, 1f)),
        new CanopyBeat("", "✦ Niko", "Rust…待ってて！　今、油を目一杯さすからね！", new Color(1f, 0.88f, 0.45f, 1f)),
        // ← ここで注油フェーズ
        new CanopyBeat("", "✦ 相棒 Rust", "……あ……温かい油が……心臓に……！", new Color(0.35f, 0.92f, 0.98f, 1f)),
        new CanopyBeat("", "✦ 相棒 Rust", "ピピッ！……ありがとうNiko！僕たちの翼はこれで絶対に折れない！全出力で行くよ！！", new Color(0.35f, 0.92f, 0.98f, 1f)),
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

    /// <summary>クリア後／F8：天蓋・クリア・クライマックス進行をニューゲーム用に完全リセット</summary>
    public void ResetProgressForNewGame()
    {
        ClearEndingRuntimeState(ignoreSavedCanopy: false);
        RestoreSkybreakAscentBlockers();
        RestoreExplorationPresentation(resetMusicToAmbient: false);
        AdventureMusicDirector.Ensure();
        AdventureMusicDirector.Instance?.ResetSkybreakMusicState();
        Debug.Log("[RustAndFloat] ニューゲーム用に天蓋／クリア進行をリセットしました");
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
        _scriptBoardVisible = false;
        _scriptBoardAdvance = false;
        _scriptBoardTitle = "";
        _scriptBoardSpeaker = "";
        _scriptBoardBody = "";
        _canopyBeatIndex = -1;
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

        // 天蓋破壊済みなら、ハイパー上昇気流光柱を即座に再配置
        if (IsCanopyBroken)
        {
            BuildSkybreakHyperUpdraft(new Vector3(512f, 62f, 512f));
        }
    }

    void FixPodiumColliders()
    {
        var tower = GameObject.Find("SanctuaryZero_Tower");
        if (tower == null) return;

        // 薄い上面コライダーは CharacterController.skinWidth より薄く貫通→地形に落下→台座に埋まる。
        // 上面が歩行面の「厚い固体スラブ」に差し替え、XZ内では絶対に床下へ潜れないようにする。
        MakeSolidTerraceCollider(tower.transform.Find("WhiteMarblePodium")?.gameObject, thickness: 5f);
        MakeSolidTerraceCollider(tower.transform.Find("SanctuaryGridFloor")?.gameObject, thickness: 2.5f);
    }

    /// <summary>タワー白亜テラス上なら歩行面Y、それ以外は負の無限大</summary>
    public static float GetTerraceSurfaceY(Vector3 worldPos)
    {
        float dx = worldPos.x - 512f;
        float dz = worldPos.z - 512f;
        // 直径70mの基壇より少し内側
        if (dx * dx + dz * dz > 34f * 34f)
            return float.NegativeInfinity;
        return TerraceTopY;
    }

    public const float TerraceTopY = 63.05f;

    static void MakeSolidTerraceCollider(GameObject floor, float thickness)
    {
        if (floor == null) return;

        foreach (var c in floor.GetComponents<Collider>())
            Destroy(c);

        Transform parent = floor.transform.parent != null ? floor.transform.parent : floor.transform;
        string solidName = floor.name + "_SolidWalk";
        var existing = parent.Find(solidName);
        GameObject solid = existing != null ? existing.gameObject : new GameObject(solidName);
        if (existing == null)
            solid.transform.SetParent(parent, false);

        // 単位円柱: 半径0.5 → lossyScale.x が直径、上面 = pos.y + lossyScale.y
        float topY = floor.transform.position.y + floor.transform.lossyScale.y;
        float diameter = Mathf.Max(floor.transform.lossyScale.x, floor.transform.lossyScale.z);
        thickness = Mathf.Max(thickness, 1.5f);

        solid.transform.position = new Vector3(floor.transform.position.x, topY - thickness * 0.5f, floor.transform.position.z);
        solid.transform.rotation = Quaternion.identity;
        solid.transform.localScale = Vector3.one;

        var box = solid.GetComponent<BoxCollider>();
        if (box == null) box = solid.AddComponent<BoxCollider>();
        box.center = Vector3.zero;
        box.size = new Vector3(diameter, thickness, diameter);
    }

    /// <summary>台座に埋まった／潜ったNikoをテラス上面へ引き上げる</summary>
    public void RescuePlayerIfBuriedInTerrace(AdventurePlayerController player)
    {
        if (player == null) return;
        // スカイブレイク上昇／オートグライド中は絶対に地上へ引きずり下ろさない
        if (player.IsSkybreakPillarAscending || player.IsAutoGliding) return;
        if (IsCanopyBroken && player.transform.position.y > 80f) return;

        Vector3 pos = player.transform.position;
        float terraceY = GetTerraceSurfaceY(pos);
        if (terraceY <= float.NegativeInfinity) return;

        // テラス上面より下、または薄いめり込み帯にいる
        if (pos.y >= terraceY - 0.02f && pos.y <= terraceY + 0.35f)
            return; // 正常に乗っている
        if (pos.y > terraceY + 0.35f)
            return; // ジャンプ／滑空中

        if (pos.y < terraceY - 0.02f)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = new Vector3(pos.x, terraceY + 0.05f, pos.z);
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

    void BuildTowerLever()
    {
        string[] oldNames = {
            "SanctuaryLeverStructure", "SanctuaryWestLeverStructure",
            "SanctuaryNorthLeverStructure", "SanctuaryEastLeverStructure", "SanctuaryTopLeverStructure"
        };
        foreach (var n in oldNames)
        {
            var old = GameObject.Find(n);
            if (old != null) Destroy(old);
        }
        _allLeverHandles.Clear();
        _allLeverLights.Clear();

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // 大理石台座マテリアル
        var pedMat = new Material(shader);
        pedMat.SetColor("_BaseColor", new Color(0.94f, 0.96f, 0.98f));
        pedMat.SetFloat("_Smoothness", 0.92f);

        // 黄金真鍮ハウジングマテリアル
        var hMat = new Material(shader);
        hMat.SetColor("_BaseColor", new Color(0.82f, 0.62f, 0.24f));
        hMat.SetFloat("_Metallic", 0.95f);
        hMat.SetFloat("_Smoothness", 0.78f);

        // シャフトマテリアル
        var sMat = new Material(shader);
        sMat.SetColor("_BaseColor", new Color(0.88f, 0.72f, 0.30f));
        sMat.SetFloat("_Metallic", 0.92f);

        // グリップ球マテリアル（真紅）
        var gMat = new Material(shader);
        gMat.SetColor("_BaseColor", new Color(0.80f, 0.18f, 0.15f));
        gMat.SetFloat("_Smoothness", 0.65f);

        // 天を衝く光の柱マテリアル（シアン発光）
        var bShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        var bMat = new Material(bShader);
        bMat.SetColor("_BaseColor", new Color(0.35f, 0.92f, 1.0f, 0.75f));

        // ── 四方＋頂上にレバーを配備（どの方向から来ても絶対に目の前に見つかる！） ──
        // 1. 南側正面レバー (512, 63.2, 501.5)
        CreateLeverStation("SanctuaryLeverStructure", _mainLeverPos, Quaternion.identity, pedMat, hMat, sMat, gMat, bMat, true);

        // 2. 西側レバー (501.5, 63.2, 512)
        CreateLeverStation("SanctuaryWestLeverStructure", new Vector3(501.5f, 63.2f, 512f), Quaternion.Euler(0f, 90f, 0f), pedMat, hMat, sMat, gMat, bMat, false);

        // 3. 北側レバー (512, 63.2, 522.5) — 今まさにプレイヤーがいる北東側からも最短距離！
        CreateLeverStation("SanctuaryNorthLeverStructure", new Vector3(512f, 63.2f, 522.5f), Quaternion.Euler(0f, 180f, 0f), pedMat, hMat, sMat, gMat, bMat, false);

        // 4. 東側レバー (522.5, 63.2, 512)
        CreateLeverStation("SanctuaryEastLeverStructure", new Vector3(522.5f, 63.2f, 512f), Quaternion.Euler(0f, 270f, 0f), pedMat, hMat, sMat, gMat, bMat, false);

        // 5. 頂上レバー (512, 137.2, 512)
        CreateLeverStation("SanctuaryTopLeverStructure", _topLeverPos, Quaternion.identity, pedMat, hMat, sMat, gMat, bMat, false);
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
        grip.transform.localScale = Vector3.one * 0.85f;
        Destroy(grip.GetComponent<Collider>());
        var gMr = grip.GetComponent<MeshRenderer>();
        if (gMr != null) gMr.material = gMat;

        // 天を衝く巨大光柱ビーコン
        var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "LeverSkyBeacon";
        beacon.transform.SetParent(root.transform, false);
        beacon.transform.localPosition = new Vector3(0f, 45f, 0f);
        beacon.transform.localScale = new Vector3(1.6f, 45f, 1.6f);
        Destroy(beacon.GetComponent<Collider>());
        var bRend = beacon.GetComponent<Renderer>();
        if (bRend != null) bRend.material = bMat;

        // 発光インジケーターライト
        var lightGo = new GameObject("LeverIndicatorLight");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 3.6f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.35f, 0.95f, 1.0f);
        light.intensity = 7.5f;
        light.range = 42f;

        if (isMain) _leverLight = light;
        _allLeverLights.Add(light);

        // 接近判定トリガー（大型レバーに合わせて広め）
        var col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 12.0f;
    }

    public bool IsPlayerNearLever =>
        _playerNearby
        && !_scriptBoardVisible
        && !_climaxCrisisStarted
        && !_epilogueTriggered;

    /// <summary>全パーツ回収済みでレバー操作可能な状態（再演含む）</summary>
    public bool IsLeverReadyToOpen
    {
        get
        {
            var scrapMgr = AdventureScrapManager.Instance ?? Object.FindFirstObjectByType<AdventureScrapManager>();
            return scrapMgr != null && scrapMgr.CollectedCount >= AdventureScrapManager.TotalScrapCount;
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
        // F9/F10: オープニング中でも最優先でレバー検証（二重起動防止のためここ一本化）
        var debugKb = UnityEngine.InputSystem.Keyboard.current;
        if (debugKb != null && (debugKb.f9Key.wasPressedThisFrame || debugKb.f10Key.wasPressedThisFrame))
        {
            DebugJumpToCanopyOpening();
            return;
        }
        try
        {
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
        TickGameClearModal();

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

        var player = AdventurePlayerController.Instance
                     ?? Object.FindFirstObjectByType<AdventurePlayerController>();

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

        // 各レバー本体の近くだけ反応（遠距離UI誤認を防ぐ）
        bool nearAnyLever = false;
        Vector3[] leverSpots =
        {
            _mainLeverPos, _topLeverPos,
            new Vector3(501.5f, 63.2f, 512f),
            new Vector3(512f, 63.2f, 522.5f),
            new Vector3(522.5f, 63.2f, 512f)
        };
        for (int i = 0; i < leverSpots.Length; i++)
        {
            if (Vector3.Distance(p, leverSpots[i]) < 28f)
            {
                nearAnyLever = true;
                break;
            }
        }

        _playerNearby = nearAnyLever && !_showSkybreakModal;
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
            _leverUiLabel.text = ready
                ? "【ここを押す / E / Space】巨大真鍮レバーを引く"
                : "レバーはロック中（遺物12個が必要）";
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

    /// <summary>パーツ12個達成時：巨大レバーを再生成し、操作UIを確実に出す</summary>
    public void OnAllScrapsCollectedForLever()
    {
        // 誤って残った危機／台本フラグをクリア（レバー操作を塞がない）
        if (!_endingSequenceActive)
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

        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
            drone.SpeakCustom("翼が完成したよ！巨大な真鍮レバーを引いて、天蓋を開こう、Niko！！", 6.0f);

        Debug.Log("[RustAndFloat] パーツ12/12：巨大レバー操作可能");
    }

    bool WasInteractEdge()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame) return true;
        try { if (Input.GetKeyDown(KeyCode.E)) return true; } catch { }
        var pad = UnityEngine.InputSystem.Gamepad.current;
        return pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame);
    }

    bool IsLeverHoldInput()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && (kb.eKey.isPressed || kb.spaceKey.isPressed || kb.enterKey.isPressed))
            return true;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed) return true;
        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (pad != null && (pad.buttonSouth.isPressed || pad.buttonWest.isPressed)) return true;
        try
        {
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return)) return true;
            if (Input.GetMouseButton(0)) return true;
        }
        catch { }
        return false;
    }

    /// <summary>レバー前に残る詩的バナー／砂浜読み物／オープニングを消して操作を塞がない</summary>
    void ClearBoardsBlockingLever()
    {
        if (AdventureScrapHUD.Instance != null)
            AdventureScrapHUD.Instance.HideBannerImmediately();

        var beach = AdventureBeachNarrativeManager.Instance
                    ?? Object.FindFirstObjectByType<AdventureBeachNarrativeManager>();
        if (beach != null && beach.IsShowingModal)
            beach.CloseModal();

        var opening = Object.FindFirstObjectByType<AdventureRustFloatOpening>();
        if (opening != null && opening.IsModalBoardOpen())
            opening.ForceDismissForGameplay();
    }

    bool CheckLeverInputTriggered()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            if (kb.eKey.wasPressedThisFrame) return true;
            if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                return true;
        }

        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
            return true;

        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame)) return true;

        try
        {
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
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
        AdventureScrapManager.Instance?.ResetToCount(AdventureScrapManager.TotalScrapCount);
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
        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();

        if (!allCollected)
        {
            var scrapMgr = AdventureScrapManager.Instance;
            int count = scrapMgr != null ? scrapMgr.CollectedCount : 0;
            int remaining = 12 - count;
            if (drone != null)
            {
                drone.SpeakCustom($"まだレバーがロックされてるみたい…あと{remaining}個の遺物を集めて、僕たちの翼を完全に直そう！", 4.5f);
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

    IEnumerator AnimateLeverPullRoutine()
    {
        float elapsed = 0f;
        float duration = 0.65f;
        Quaternion startRot = _leverHandle != null ? _leverHandle.localRotation : Quaternion.identity;
        Quaternion endRot = Quaternion.Euler(38f, 0f, 0f);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            foreach (var h in _allLeverHandles)
            {
                if (h != null) h.localRotation = Quaternion.Slerp(startRot, endRot, t * t);
            }
            yield return null;
        }
    }

    void BeginCanopyScriptBeats()
    {
        _suppressClimax = true;
        _climaxCrisisStarted = false;
        _endingSequenceActive = true;
        _leverPulled = true;
        _scriptHoldTimer = 0f;
        ClearPendingClimax();

        var player = AdventurePlayerController.Instance
                     ?? Object.FindFirstObjectByType<AdventurePlayerController>();
        if (player != null && player.transform.position.y < 90f)
            player.ForceGroundReset();

        AdventureMusicDirector.Ensure();
        AdventureMusicDirector.Instance?.PlaySkybreakTheme(force: true);
        StartSkybreakWindAmbience();

        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            drone.ClearSpeech();
            drone.StartSkybreakNestle();
        }

        _canopyBeatIndex = 0;
        PresentCanopyBeat(0);
        Debug.Log("[RustAndFloat] 天蓋台本を Update 駆動で開始（全" + CanopyBeats.Length + "枚）");
    }

    void PresentCanopyBeat(int index)
    {
        if (index < 0 || index >= CanopyBeats.Length) return;
        var beat = CanopyBeats[index];

        SuppressAllSpeechAndBanners();
        _scriptBoardTitle = beat.Title ?? "";
        _scriptBoardSpeaker = beat.Speaker ?? "";
        _scriptBoardBody = beat.Body ?? "";
        _scriptBoardAccent = beat.Accent;
        _scriptBoardIsDive = beat.IsDive;
        _scriptBoardAdvance = false;
        _scriptBoardVisible = true;
        _scriptBoardOpenedAt = Time.unscaledTime;
        _scriptHoldTimer = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        EnsureEventSystemForUi();
        EnsureScriptBoardUI();
        ApplyScriptBoardUI();

        if (_scriptHintUi != null)
        {
            _scriptHintUi.text = beat.IsDive
                ? "【Space長押し / 下のボタン】ダイブ！"
                : "【Space長押し / 下のボタン】つづき";
        }

        Debug.Log($"[RustAndFloat] 台本 {index + 1}/{CanopyBeats.Length}: {beat.Title} {beat.Speaker}");
    }

    void TickCanopyScriptBeats()
    {
        // 孤児救済：シークエンス中にボードだけ残ってインデックスが死んでいる場合
        if (_canopyBeatIndex < 0)
        {
            if (_endingSequenceActive && _scriptBoardVisible && !_climaxCrisisStarted && !_epilogueTriggered)
            {
                Debug.LogWarning("[RustAndFloat] 台本インデックス喪失を検出 → Update駆動で再開");
                _canopyBeatIndex = 0;
                PresentCanopyBeat(0);
            }
            return;
        }

        float openFor = Time.unscaledTime - _scriptBoardOpenedAt;

        // 入力
        if (openFor >= 0.35f)
        {
            PollScriptBoardAdvance();

            if (IsDiveConfirmHeld())
            {
                _scriptHoldTimer += Time.unscaledDeltaTime;
                if (_scriptHoldTimer >= 0.08f)
                    _scriptBoardAdvance = true;
            }
            else
            {
                _scriptHoldTimer = 0f;
            }

            // 入力が一切取れなくても必ず進む
            // 1枚目7秒／2〜6枚目3.5秒／ダイブ5秒
            float autoSec = _scriptBoardIsDive ? 5f : 3.0f;
            if (_canopyBeatIndex == 0)
                autoSec = 7.0f;
            else if (!_scriptBoardIsDive && _canopyBeatIndex >= 1 && _canopyBeatIndex <= 5)
                autoSec = 3.5f;
            if (openFor >= autoSec)
                _scriptBoardAdvance = true;
        }

        if (!_scriptBoardAdvance) return;

        int next = _canopyBeatIndex + 1;
        if (next >= CanopyBeats.Length)
        {
            FinishCanopyScriptBeats();
            return;
        }

        _canopyBeatIndex = next;
        PresentCanopyBeat(next);
    }

    void FinishCanopyScriptBeats()
    {
        Debug.Log("[RustAndFloat] 天蓋台本完了 → 光の柱上昇 → クライマックスへ");
        _canopyBeatIndex = -1;
        _scriptBoardAdvance = false;
        _scriptBoardVisible = false;
        _scriptBoardIsDive = false;
        _scriptBoardTitle = "";
        _scriptBoardSpeaker = "";
        _scriptBoardBody = "";
        if (_scriptUiRoot != null)
            _scriptUiRoot.SetActive(false);

        _endingSequenceActive = false;
        // レバー再入禁止を維持（false にすると Space 長押しで台本が最初へ巻き戻る）
        _leverPulled = true;
        _leverPullLockUntil = Time.unscaledTime + 3600f;
        _suppressClimax = false;
        _climaxOilInjected = false;
        // クライマックスは柱上昇完了後に開始（ここでは立てない）
        _climaxCrisisStarted = false;
        _ignoreSavedCanopyState = false;
        IsCanopyBroken = true;

        // 上昇演出を優先。最大4.5秒でクライマックスへ必ず接続（後半途切れ防止）
        ArmPendingClimax(failsafeSeconds: 4.5f);

        AdventureSaveManager.Instance?.SaveGame("天蓋開放・到達記録を保存しました");

        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
            drone.StartSkybreakNestle();

        BuildSkybreakHyperUpdraft(new Vector3(512f, 62f, 512f));

        var player = AdventurePlayerController.Instance
                     ?? Object.FindFirstObjectByType<AdventurePlayerController>();
        if (player != null)
        {
            player.PrepareSkybreakPillarAscend();
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            float startY = Mathf.Max(player.transform.position.y, TerraceTopY + 1f);
            player.transform.position = new Vector3(512f, startY + 2.5f, 512f);
            if (cc != null) cc.enabled = true;
            player.BeginSkybreakPillarAscend(new Vector3(512f, 0f, 512f), 36f, 150f);
        }
    }

    /// <summary>光の柱到達：上昇完了からクライマックスへ</summary>
    public void NotifyPillarAscendComplete()
    {
        if (_climaxCrisisStarted || _epilogueTriggered) return;
        // 高度到達待ち。0秒＝無期限タイムアウトではなく「高さ条件のみ」
        ArmPendingClimax(failsafeSeconds: 8f);
        _suppressClimax = false;
        _ignoreSavedCanopyState = false;
        if (!_isCanopyBroken)
            IsCanopyBroken = true;

        var player = AdventurePlayerController.Instance
                     ?? Object.FindFirstObjectByType<AdventurePlayerController>();
        TryStartPendingClimax(player);
    }

    void TryStartPendingClimax(AdventurePlayerController player)
    {
        if (!_pendingClimaxAfterCanopy) return;
        if (_climaxCrisisStarted || _epilogueTriggered) return;
        if (_canopyBeatIndex >= 0 || _endingSequenceActive) return;

        bool highEnough = player != null
            && (player.transform.position.y >= 140f || player.IsAutoGliding);
        // deadline<=0 は「高さ待ちのみ」。0を即タイムアウト扱いすると地上リセット後にエンディングが再点火する
        bool timedOut = _pendingClimaxDeadline > 0f
                        && Time.unscaledTime >= _pendingClimaxDeadline;
        if (!highEnough && !timedOut) return;

        ClearPendingClimax();
        _suppressClimax = false;
        _ignoreSavedCanopyState = false;
        if (!_isCanopyBroken)
            IsCanopyBroken = true;

        // タイムアウト時のみ高度を強制確保（通常は上昇演出の到達を活かす）
        if (player != null && player.transform.position.y < 140f)
            player.ForceSkybreakArrival(new Vector3(512f, 150f, 512f), 150f);

        Debug.Log($"[RustAndFloat] クライマックス開始 high={highEnough} timeout={timedOut}");
        BeginClimaxSequence();
    }

    void ClearPendingClimax()
    {
        _pendingClimaxAfterCanopy = false;
        _pendingClimaxDeadline = 0f;
    }

    void ArmPendingClimax(float failsafeSeconds)
    {
        _pendingClimaxAfterCanopy = true;
        _pendingClimaxDeadline = failsafeSeconds <= 0f
            ? 0f // 0 = タイムアウトなし（高さ条件のみ）
            : Time.unscaledTime + failsafeSeconds;
    }

    /// <summary>天蓋破壊ボード（旧コルーチン版は未使用・互換のため残置）</summary>
    IEnumerator SkybreakFromTitleBoardRoutine()
    {
        BeginCanopyScriptBeats();
        while (_canopyBeatIndex >= 0)
            yield return null;
    }

    void SuppressAllSpeechAndBanners()
    {
        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
            drone.ClearSpeech();
        if (AdventureScrapHUD.Instance != null)
            AdventureScrapHUD.Instance.HideBannerImmediately();
    }

    /// <summary>台本を1枚のボードで表示し、進む入力まで待つ（吹き出しと重ねない）</summary>
    /// <param name="autoAdvanceOverride">0より大きいとき、通常の自動送り秒数の代わりに使う</param>
    IEnumerator ShowScriptBeat(string title, string speaker, string body, Color accent, bool isDive = false, float autoAdvanceOverride = -1f)
    {
        SuppressAllSpeechAndBanners();

        var player = AdventurePlayerController.Instance;
        if (player != null
            && player.transform.position.y < 90f
            && !player.IsSkybreakPillarAscending
            && !player.IsAutoGliding)
            player.ForceGroundReset();

        _scriptBoardTitle = title ?? "";
        _scriptBoardSpeaker = speaker ?? "";
        _scriptBoardBody = body ?? "";
        _scriptBoardAccent = accent;
        _scriptBoardIsDive = isDive;
        _scriptBoardAdvance = false;
        _scriptBoardVisible = true;
        _scriptBoardOpenedAt = Time.unscaledTime;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        EnsureEventSystemForUi();
        EnsureScriptBoardUI();
        ApplyScriptBoardUI();

        if (_scriptBtn != null)
        {
            var btnRt = _scriptBtn.GetComponent<RectTransform>();
            if (btnRt != null)
            {
                btnRt.sizeDelta = isDive ? new Vector2(560f, 72f) : new Vector2(520f, 56f);
                btnRt.anchoredPosition = new Vector2(0f, 16f);
            }
        }
        if (_scriptHintUi != null)
        {
            _scriptHintUi.fontSize = isDive ? 22 : 18;
            _scriptHintUi.text = isDive
                ? "【Space長押し / クリック】ダイブ！"
                : "【Space長押し / クリック】つづき";
        }

        // 最低表示
        const float minShow = 0.5f;
        while (Time.unscaledTime - _scriptBoardOpenedAt < minShow)
            yield return null;

        float holdTimer = 0f;
        float autoAfter = isDive ? 8f : 3.5f;
        if (autoAdvanceOverride > 0f)
            autoAfter = autoAdvanceOverride;
        while (!_scriptBoardAdvance)
        {
            PollScriptBoardAdvance();

            // 全台本：Space/クリック押しっぱなしで進む
            if (IsDiveConfirmHeld())
            {
                holdTimer += Time.unscaledDeltaTime;
                if (holdTimer >= 0.12f)
                    _scriptBoardAdvance = true;
            }
            else
            {
                holdTimer = 0f;
            }

            if (Time.unscaledTime - _scriptBoardOpenedAt > autoAfter)
                _scriptBoardAdvance = true;

            yield return null;
        }

        _scriptBoardAdvance = false;
        _scriptBoardVisible = false;
        _scriptBoardIsDive = false;
        _scriptBoardTitle = "";
        _scriptBoardSpeaker = "";
        _scriptBoardBody = "";
        if (_scriptUiRoot != null)
            _scriptUiRoot.SetActive(false);
        yield return null;
    }

    /// <summary>プレイヤー／UIから台本送りを直接要求</summary>
    public void NotifyScriptBoardAdvance()
    {
        if (!_scriptBoardVisible) return;
        if (_scriptRequireInputRelease) return;
        float minShow = _climaxCrisisStarted && _climaxBeatIndex >= ClimaxOilSlot ? 2.2f : 0.45f;
        if (Time.unscaledTime - _scriptBoardOpenedAt < minShow) return;
        _scriptBoardAdvance = true;
        Debug.Log("[RustAndFloat] 台本送り入力を受け付けました");
    }

    static bool IsDiveConfirmHeld()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && (kb.spaceKey.isPressed || kb.enterKey.isPressed || kb.eKey.isPressed))
            return true;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed))
            return true;
        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (pad != null && (pad.buttonSouth.isPressed || pad.buttonWest.isPressed))
            return true;
        try
        {
            if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.E))
                return true;
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
                return true;
        }
        catch { }
        return false;
    }

    /// <summary>クリック／Space で台本を進める（Input System の wasPressed を直接見る）</summary>
    void PollScriptBoardAdvance()
    {
        if (!_scriptBoardVisible || _scriptBoardAdvance) return;
        if (_scriptRequireInputRelease) return;

        // 注油後セリフは短すぎるスキップを防ぐ
        float minShow = _climaxCrisisStarted && _climaxBeatIndex >= ClimaxOilSlot ? 2.2f : 0.35f;
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

    static GUIStyle MakeCleanLabelStyle(Font font, int fontSize, FontStyle fontStyle, TextAnchor align, bool wordWrap)
    {
        // GUI.skin.label をコピーするとEditorのGizmosフォントが混入する
        var style = new GUIStyle
        {
            font = font,
            fontSize = fontSize,
            fontStyle = fontStyle,
            alignment = align,
            wordWrap = wordWrap,
            richText = false,
            clipping = TextClipping.Overflow
        };
        style.normal.textColor = Color.white;
        style.hover.textColor = Color.white;
        style.active.textColor = Color.white;
        return style;
    }

    bool CheckScriptBoardAdvanceInput()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame ||
                           kb.numpadEnterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
            return true;

        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
            return true;

        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame))
            return true;

        try
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.E))
                return true;
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
                return true;
        }
        catch { }
        return false;
    }

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
                // 見た目は残し、物理だけ完全削除（enabled=false だと再有効化で戻る）
                var cols = mono.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i] != null)
                        Object.Destroy(cols[i]);
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

    void OnGUI()
    {
        // クリア／台本／注油／クライマックス中は IMGUI 禁止
        // （GUI.Button＋壊れた日本語フォントが空ボード＋「Gizmos」3ボタンになる）
        if (_showGameClearModal || _scriptBoardVisible || _climaxCrisisStarted || _epilogueTriggered
            || _endingSequenceActive || _climaxOilWaiting || _climaxPostOilPhase > 0)
            return;

        bool allCollected = IsLeverReadyToOpen;
        bool showLeverUi = IsPlayerNearLever && !IsCanopyBroken && !_pendingClimaxAfterCanopy;
        if (!showLeverUi)
            return;

        bool canReplay = allCollected && (_leverPulled || IsCanopyBroken);
        float w = Mathf.Min(1100f, Screen.width * 0.94f);
        float h = 120f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height - 168f;
        var boxRect = new Rect(x, y, w, h);

        GUI.color = new Color(0.02f, 0.06f, 0.12f, 0.82f);
        GUI.DrawTexture(boxRect, Texture2D.whiteTexture);
        if (allCollected && _leverHoldTimer > 0f)
        {
            float fill = Mathf.Clamp01(_leverHoldTimer / LeverHoldSeconds);
            GUI.color = new Color(0.25f, 0.95f, 0.85f, 0.9f);
            GUI.DrawTexture(new Rect(x, y + h - 10f, w * fill, 10f), Texture2D.whiteTexture);
        }
        Color accentCol = allCollected ? new Color(0.35f, 0.95f, 1.0f, 0.95f) : new Color(1.0f, 0.85f, 0.40f, 0.9f);
        GUI.color = accentCol;
        GUI.DrawTexture(new Rect(x, y, w, 4f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x, y + h - 4f, w, 4f), Texture2D.whiteTexture);

        GUI.color = new Color(1f, 1f, 1f, 0.01f);
        if (allCollected && GUI.Button(boxRect, GUIContent.none))
            BeginCanopyOpeningFromLever(true);
        GUI.color = Color.white;
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

    void BeginClimaxSequence()
    {
        if (_climaxCrisisStarted) return;
        ClearPendingClimax();
        _climaxCrisisStarted = true;
        _climaxOilInjected = false;
        _climaxOilWaiting = false;
        _oilHoldTimer = 0f;
        _climaxBeatIndex = 0;
        _scriptHoldTimer = 0f;
        _suppressClimax = false;
        _ignoreSavedCanopyState = false;

        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        var player = AdventurePlayerController.Instance
                     ?? Object.FindFirstObjectByType<AdventurePlayerController>();

        SetCinematicCamera(true);
        SetExplorationHudVisible(false);
        SpawnWildernessPanorama(coldCrisis: true);
        ApplySkybreakColdAtmosphere();

        if (player != null)
        {
            // 警告台本時点で必ず空中にいる
            if (player.transform.position.y < 140f)
                player.ForceSkybreakArrival(new Vector3(512f, 150f, 512f), 150f);
            else
            {
                player.EndSkybreakPillarAscend();
                player.SetAutoGlideMode(true, Mathf.Clamp(player.transform.position.y, 140f, 160f));
            }
        }

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SuppressAllSpeechAndBanners();

        if (drone != null)
        {
            drone.StartClimaxCrisis();
            drone.ClearSpeech();
        }

        PresentClimaxBeat(0);
        Debug.Log("[RustAndFloat] クライマックスを Update 駆動で開始");
    }

    void PresentClimaxBeat(int index)
    {
        if (index < 0 || index >= ClimaxBeats.Length) return;
        var beat = ClimaxBeats[index];
        SuppressAllSpeechAndBanners();
        _scriptBoardTitle = beat.Title ?? "";
        _scriptBoardSpeaker = beat.Speaker ?? "";
        _scriptBoardBody = beat.Body ?? "";
        _scriptBoardAccent = beat.Accent;
        _scriptBoardIsDive = false;
        _scriptBoardAdvance = false;
        _scriptBoardVisible = true;
        _scriptBoardOpenedAt = Time.unscaledTime;
        _scriptHoldTimer = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;
        EnsureEventSystemForUi();
        EnsureScriptBoardUI();
        ApplyScriptBoardUI();
        if (_scriptHintUi != null)
            _scriptHintUi.text = "【Space長押し / 下のボタン】つづき";

        // 台本1（警告）：そばで震え始める
        // 台本2（気流が冷たい）：力なく落ちていく
        if (index == 1)
        {
            var droneFall = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
            if (droneFall != null)
                droneFall.BeginClimaxColdFallAway();
        }

        // 台本12：全出力セリフと同時にオーバードライブ演出
        if (index == ClimaxOilSlot + 1)
        {
            var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
            if (drone != null)
                drone.TriggerClimaxOverdrive();
        }
    }

    void BeginClimaxOilWait()
    {
        _scriptBoardVisible = false;
        _scriptBoardAdvance = false;
        _scriptBoardTitle = "";
        _scriptBoardSpeaker = "";
        _scriptBoardBody = "";
        if (_scriptUiRoot != null)
            _scriptUiRoot.SetActive(false);

        _climaxOilWaiting = true;
        _climaxOilInjected = false;
        _oilHoldTimer = 0f;
        _oilWaitOpenedAt = Time.unscaledTime;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SuppressAllSpeechAndBanners();
        EnsureOilPromptUI();
        Debug.Log("[RustAndFloat] 注油フェーズ開始（E/Space/クリック長押し）");
    }

    void TickClimaxSequence()
    {
        if (!_climaxCrisisStarted || _epilogueTriggered || _suppressClimax)
            return;

        if (_climaxOilWaiting)
        {
            TickClimaxOilHold();
            return;
        }

        if (_climaxBeatIndex < 0 || !_scriptBoardVisible)
            return;

        bool postOilBeat = _climaxBeatIndex >= ClimaxOilSlot;
        float openFor = Time.unscaledTime - _scriptBoardOpenedAt;

        // 注油完了直後：Space/E が押されたままだと「温かい油」が即スキップされる
        if (_scriptRequireInputRelease)
        {
            if (IsDiveConfirmHeld())
            {
                _scriptHoldTimer = 0f;
                _scriptBoardAdvance = false;
                return;
            }
            _scriptRequireInputRelease = false;
            _scriptHoldTimer = 0f;
        }

        // 注油後の最初のセリフは最低2.2秒見せる
        float minHoldOpen = postOilBeat && _climaxBeatIndex == ClimaxOilSlot ? 2.2f : 0.35f;
        if (openFor >= minHoldOpen)
        {
            PollScriptBoardAdvance();
            if (IsDiveConfirmHeld())
            {
                _scriptHoldTimer += Time.unscaledDeltaTime;
                if (_scriptHoldTimer >= 0.18f)
                    _scriptBoardAdvance = true;
            }
            else _scriptHoldTimer = 0f;

            float autoSec = GetScriptBeatAutoAdvanceSeconds(_scriptBoardBody, postOilBeat);
            if (openFor >= autoSec)
                _scriptBoardAdvance = true;
        }

        if (!_scriptBoardAdvance) return;

        int next = _climaxBeatIndex + 1;

        // index 0,1,2 のあと（next==3）で注油へ
        if (next == ClimaxOilSlot && !_climaxOilInjected)
        {
            BeginClimaxOilWait();
            return;
        }

        if (next >= ClimaxBeats.Length)
        {
            FinishClimaxSequence();
            return;
        }

        _climaxBeatIndex = next;
        PresentClimaxBeat(next);
    }

    static float GetScriptBeatAutoAdvanceSeconds(string body, bool postOil)
    {
        int len = string.IsNullOrEmpty(body) ? 0 : body.Length;
        if (postOil)
            return Mathf.Clamp(8f + len * 0.14f, 9f, 20f);
        return Mathf.Clamp(3.4f + len * 0.07f, 3f, 12f);
    }

    void HideScriptBoardCompletely()
    {
        _scriptBoardVisible = false;
        _scriptBoardAdvance = false;
        _scriptBoardIsDive = false;
        _scriptBoardTitle = "";
        _scriptBoardSpeaker = "";
        _scriptBoardBody = "";
        if (_scriptUiRoot != null)
            _scriptUiRoot.SetActive(false);

        var orphanBoard = GameObject.Find("EndingScriptBoardCanvas");
        if (orphanBoard != null)
            orphanBoard.SetActive(false);

        if (!IsClimaxOilPromptActive)
        {
            var oilCanvas = GameObject.Find("ClimaxOilPromptCanvas");
            if (oilCanvas != null)
                oilCanvas.SetActive(false);
        }
    }

    void TickClimaxOilHold()
    {
        if (!_climaxOilWaiting || _climaxOilInjected) return;

        bool holding = IsDiveConfirmHeld();
        if (holding)
            _oilHoldTimer += Time.unscaledDeltaTime;
        else
            _oilHoldTimer = Mathf.Max(0f, _oilHoldTimer - Time.unscaledDeltaTime * 1.1f);

        if (Time.unscaledTime - _oilWaitOpenedAt >= 8f)
            _oilHoldTimer = OilHoldRequired;

        RefreshOilPromptUI();

        if (_oilHoldTimer >= OilHoldRequired)
            CompleteClimaxOil();
    }

    /// <summary>外部／プレイヤーから注油ホールドを加算</summary>
    public void NotifyOilHold(float dt)
    {
        if (!IsClimaxOilPromptActive) return;
        _oilHoldTimer += Mathf.Max(0f, dt);
        if (_oilHoldTimer >= OilHoldRequired)
            CompleteClimaxOil();
    }

    void CompleteClimaxOil()
    {
        if (_climaxOilInjected) return;
        _climaxOilInjected = true;
        _climaxOilWaiting = false;
        _oilHoldTimer = OilHoldRequired;
        HideOilPromptUI();

        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
            drone.StartClimaxPetAndOil();

        // 注油後：極寒の気配を少し緩め、解放の金色へ寄せる
        SoftenSkybreakColdAtmosphere();

        _climaxBeatIndex = ClimaxOilSlot;
        _climaxPostOilPhase = 0;
        _climaxPostOilUntil = 0f;
        _scriptBoardAdvance = false;
        _scriptHoldTimer = 0f;
        // 注油ゲージを満たした押しっぱなしが、そのまま台本送りにならないようにする
        _scriptRequireInputRelease = true;
        PresentClimaxBeat(ClimaxOilSlot);
        Debug.Log("[RustAndFloat] 注油完了 → 台本11（蘇生セリフ）");
    }

    void EnsureOilPromptUI()
    {
        EnsureEventSystemForUi();
        if (_oilUiRoot != null)
        {
            _oilUiRoot.SetActive(true);
            RefreshOilPromptUI();
            return;
        }

        Font font = ResolveUiFont();
        var canvasGo = new GameObject("ClimaxOilPromptCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5200;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        canvasGo.AddComponent<GraphicRaycaster>();

        // 上下レターボックス
        CreateOilBar(canvasGo.transform, true);
        CreateOilBar(canvasGo.transform, false);

        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelGo.AddComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(860f, 320f);
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0.02f, 0.05f, 0.10f, 0.96f);
        panelImg.raycastTarget = true;
        var panelBtn = panelGo.AddComponent<Button>();
        panelBtn.targetGraphic = panelImg;
        panelBtn.transition = Selectable.Transition.None;
        // 押し続け判定は Update 側。ここでは見た目用

        _oilTitleUi = MakeScriptText(panelGo.transform, "OilTitle", new Vector2(0f, -24f), new Vector2(0.5f, 1f), new Vector2(800f, 40f), 30, TextAnchor.MiddleCenter, font);
        _oilTitleUi.color = new Color(1f, 0.55f, 0.45f, 1f);
        _oilTitleUi.text = "警告　Rustが極寒で機能停止寸前";
        PrepareFontForText(font, _oilTitleUi.text, 30, FontStyle.Bold);

        _oilPromptUi = MakeScriptText(panelGo.transform, "OilPrompt", new Vector2(0f, 10f), new Vector2(0.5f, 0.5f), new Vector2(780f, 140f), 26, TextAnchor.MiddleCenter, font);
        _oilPromptUi.color = new Color(1f, 0.92f, 0.4f, 1f);
        _oilPromptUi.text = "✦ エネルギー注入 ✦\n【E / Space / クリック長押し】\nゲージを満タンにして油をさす";
        PrepareFontForText(font, _oilPromptUi.text, 26);

        var gaugeBgGo = new GameObject("GaugeBg");
        gaugeBgGo.transform.SetParent(panelGo.transform, false);
        var gaugeBgRt = gaugeBgGo.AddComponent<RectTransform>();
        gaugeBgRt.anchorMin = gaugeBgRt.anchorMax = new Vector2(0.5f, 0f);
        gaugeBgRt.anchoredPosition = new Vector2(0f, 36f);
        gaugeBgRt.sizeDelta = new Vector2(640f, 28f);
        var gaugeBgImg = gaugeBgGo.AddComponent<Image>();
        gaugeBgImg.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);

        var gaugeFillGo = new GameObject("GaugeFill");
        gaugeFillGo.transform.SetParent(gaugeBgGo.transform, false);
        var gaugeFillRt = gaugeFillGo.AddComponent<RectTransform>();
        gaugeFillRt.anchorMin = new Vector2(0f, 0f);
        gaugeFillRt.anchorMax = new Vector2(0f, 1f);
        gaugeFillRt.pivot = new Vector2(0f, 0.5f);
        gaugeFillRt.anchoredPosition = Vector2.zero;
        gaugeFillRt.sizeDelta = new Vector2(0f, 0f);
        _oilGaugeFill = gaugeFillGo.AddComponent<Image>();
        _oilGaugeFill.color = new Color(1f, 0.82f, 0.22f, 1f);

        var holdGo = new GameObject("HoldBtn");
        holdGo.transform.SetParent(canvasGo.transform, false);
        var holdRt = holdGo.AddComponent<RectTransform>();
        holdRt.anchorMin = holdRt.anchorMax = new Vector2(0.5f, 0f);
        holdRt.anchoredPosition = new Vector2(0f, 88f);
        holdRt.sizeDelta = new Vector2(640f, 64f);
        var holdImg = holdGo.AddComponent<Image>();
        holdImg.color = new Color(0.95f, 0.75f, 0.2f, 0.95f);
        _oilHoldBtn = holdGo.AddComponent<Button>();
        _oilHoldBtn.targetGraphic = holdImg;
        _oilHoldBtn.transition = Selectable.Transition.None;
        _oilHoldLabelUi = MakeScriptText(holdGo.transform, "HoldLabel", Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(600f, 56f), 26, TextAnchor.MiddleCenter, font);
        _oilHoldLabelUi.color = new Color(0.12f, 0.08f, 0.02f, 1f);
        _oilHoldLabelUi.text = "【押し続け】Rustに油をさす";
        PrepareFontForText(font, _oilHoldLabelUi.text, 26, FontStyle.Bold);

        _oilUiRoot = canvasGo;
        RefreshOilPromptUI();
    }

    static void CreateOilBar(Transform parent, bool top)
    {
        var go = new GameObject(top ? "LetterTop" : "LetterBottom");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        if (top)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
        }
        else
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
        }
        rt.sizeDelta = new Vector2(0f, 90f);
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.02f, 0.04f, 0.08f, 0.92f);
        img.raycastTarget = false;
    }

    void RefreshOilPromptUI()
    {
        if (_oilUiRoot == null) return;
        _oilUiRoot.SetActive(IsClimaxOilPromptActive);
        if (_oilGaugeFill != null)
        {
            float ratio = Mathf.Clamp01(_oilHoldTimer / OilHoldRequired);
            var parent = _oilGaugeFill.transform.parent as RectTransform;
            float w = parent != null ? parent.sizeDelta.x : 640f;
            _oilGaugeFill.rectTransform.sizeDelta = new Vector2(w * ratio, 0f);
        }
    }

    void HideOilPromptUI()
    {
        if (_oilUiRoot != null)
            _oilUiRoot.SetActive(false);
    }

    void FinishClimaxSequence()
    {
        _climaxBeatIndex = -1;
        _climaxOverdriveCinematicUntil = 0f;
        _climaxPostOilPhase = 0;
        _climaxPostOilUntil = 0f;
        HideScriptBoardCompletely();
        TeardownScriptBoardUi();
        HideOilPromptUI();

        Time.timeScale = 1f;
        var player = AdventurePlayerController.Instance;
        if (player != null)
        {
            player.SetAutoGlideMode(false);
            player.ApplyGlideBoost(3.2f, 75f);
            player.ApplyUpdraft(28f);
        }

        _epilogueTriggered = true;
        AdventureMusicDirector.Ensure();
        AdventureMusicDirector.Instance?.KeepEndingThemeActive();
        StartCoroutine(EpilogueSequenceRoutine());
    }

    /// <summary>旧コルーチン版（互換・未使用）</summary>
    IEnumerator ClimaxCrisisSequenceRoutine()
    {
        BeginClimaxSequence();
        while (_climaxCrisisStarted && !_epilogueTriggered)
            yield return null;
    }

    void DrawScriptBoardGUI()
    {
        // IMGUI日本語描画は停止（Gizmos文字化け防止）。入力は DrawScriptBoardInputFallback。
    }

    static void DrawShadowedText(Rect rect, string text, GUIStyle style, Color textColor, Color shadowColor, float offset = 1.5f)
    {
        // IMGUI日本語は使わない（呼び出されても何もしない）
    }

    void DrawEpilogueGUI()
    {
        // エピローグ字幕も台本ボード(uGUI)へ統一済みのため IMGUI は描画しない
    }

    static readonly string[] EpilogueActs =
    {
        "空が割れた。\n100%最適化された箱庭の外には、\n凍えるほどリアルな風が吹いていた。",
        "人は最適で最短距離を進んでる時じゃなく、\n寄り道をしては、躓きながらも、\n突然出会えた感動に、生きてる証(あかし)を得るんだ。",
        "傷つくかもしれない自由と、命の重みを取り戻した\n二人の旅が、また始まる。\n―― 『Rust & Float』"
    };

    Font ResolveEpilogueFont()
    {
        if (_epilogueFont != null)
            return _epilogueFont;
        _epilogueFont = CreateJapaneseFont(32);
        return _epilogueFont;
    }

    void DrawGameClearModalGUI()
    {
        // IMGUI クリア画面は廃止（空ボード＋Gizmos化の原因）。uGUI の TickGameClearModal を使う。
    }

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

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            if (kb.spaceKey.wasPressedThisFrame)
                RelaunchIntoSky();
            else if (kb.nKey.wasPressedThisFrame)
                StartNewGameFromClearModal();
            else if (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
                CloseGameClearModalForFreeExplore();
        }
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

        MakeClearModalButton(panelGo.transform, "DiveBtn", new Vector2(-300f, 36f), new Color(0.20f, 0.75f, 0.95f, 0.95f),
            "【Space】大空へダイブ", font, RelaunchIntoSky);
        MakeClearModalButton(panelGo.transform, "NewBtn", new Vector2(0f, 36f), new Color(0.35f, 0.82f, 0.55f, 0.95f),
            "【N】はじめから", font, StartNewGameFromClearModal);
        MakeClearModalButton(panelGo.transform, "CloseBtn", new Vector2(300f, 36f), new Color(0.25f, 0.35f, 0.45f, 0.95f),
            "【E】閉じる", font, CloseGameClearModalForFreeExplore);

        _clearUiRoot = canvasGo;
    }

    void MakeClearModalButton(Transform parent, string name, Vector2 anchoredPos, Color color, string label, Font font, UnityEngine.Events.UnityAction onClick)
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
    }

    void HideGameClearModalUI()
    {
        if (_clearUiRoot != null)
            _clearUiRoot.SetActive(false);
        var orphan = GameObject.Find("GameClearModalCanvas");
        if (orphan != null && orphan != _clearUiRoot)
            orphan.SetActive(false);
    }

    void StartNewGameFromClearModal()
    {
        _showGameClearModal = false;
        HideGameClearModalUI();
        AdventureSaveManager.Ensure();
        AdventureSaveManager.Instance?.ResetToNewGame();
    }

    void CloseGameClearModalForFreeExplore()
    {
        _showGameClearModal = false;
        HideGameClearModalUI();
        AdventureMusicDirector.Ensure();
        AdventureMusicDirector.Instance?.RestoreExplorationTheme();
        var player = AdventurePlayerController.Instance;
        if (player != null)
            player.SetAutoGlideMode(false);
        SetCinematicCamera(false);
        SetExplorationHudVisible(true);
        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
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
        _showGameClearModal = false;
        HideGameClearModalUI();
        AdventureMusicDirector.Ensure();
        AdventureMusicDirector.Instance?.RestoreExplorationTheme();
        var player = AdventurePlayerController.Instance;
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
        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            drone.SpeakCustom("いっくよー！大空へダイブ！！", 5.0f);
        }
    }

    IEnumerator EpilogueSequenceRoutine()
    {
        var player = AdventurePlayerController.Instance;
        if (player != null)
        {
            if (player.transform.position.y < 110f)
                player.ApplyLaunchUpdraft(18f, 16f);
            player.SetAutoGlideMode(true, 120f);
            player.ApplyGlideBoost(1.2f, 8f);
        }

        SetCinematicCamera(true);
        SetExplorationHudVisible(false);
        SpawnWildernessPanorama(coldCrisis: false);
        SoftenSkybreakColdAtmosphere();
        SuppressAllSpeechAndBanners();

        // 旧テロップ帯は使わず、台本ボードで1枚ずつ
        _epilogueAct = 0;
        _epilogueAlpha = 0f;

        yield return StartCoroutine(ShowScriptBeat(
            "",
            "✦ 相棒 Rust",
            "わぁぁ……！見て、Niko！世界はこんなに広かったんだ……！！",
            new Color(0.35f, 0.92f, 0.98f, 1f),
            false,
            5.0f));

        for (int i = 0; i < EpilogueActs.Length; i++)
        {
            KeepAutoGlide(player);
            // 幕ごとの自動送り（標準3.5秒からの延長）
            float autoSec = i switch
            {
                0 => 7.0f, // エピローグ導入
                1 => 10.0f, // 証の幕
                2 => 6.5f, // 結び
                _ => -1f
            };
            yield return StartCoroutine(ShowScriptBeat(
                i == 0 ? "エピローグ" : "",
                "",
                EpilogueActs[i],
                new Color(1f, 0.92f, 0.55f, 1f),
                false,
                autoSec));
        }

        KeepAutoGlide(player);
        IsGameCleared = true;
        _showGameClearModal = true;
        SuppressAllSpeechAndBanners();
        AdventureMusicDirector.Ensure();
        AdventureMusicDirector.Instance?.KeepEndingThemeActive();
    }

    static void KeepAutoGlide(AdventurePlayerController player)
    {
        if (player != null && !player.IsAutoGliding)
            player.SetAutoGlideMode(true, 120f);
    }

    static void SetExplorationHudVisible(bool visible)
    {
        if (visible)
            AdventureCompassHUD.Ensure();

        var compass = AdventureCompassHUD.Instance
                      ?? Object.FindFirstObjectByType<AdventureCompassHUD>(FindObjectsInactive.Include);
        if (compass != null)
            compass.gameObject.SetActive(visible);

        var scrap = AdventureScrapHUD.Instance
                    ?? Object.FindFirstObjectByType<AdventureScrapHUD>(FindObjectsInactive.Include);
        if (scrap != null)
            scrap.gameObject.SetActive(visible);

        foreach (var t in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include))
        {
            if (t != null && t.name == "Guide")
                t.gameObject.SetActive(visible);
        }
    }

    static void SetCinematicCamera(bool enabled)
    {
        var cam = Camera.main;
        if (cam == null)
            cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null)
            return;
        var follow = cam.GetComponent<AdventureCameraFollow>();
        if (follow != null)
            follow.SetCinematicMode(enabled);
    }

    /// <summary>天蓋の割れ目の外側：未知の荒野。危機時は凍える稜線と氷霞、突破後は朝焼けの金へ</summary>
    void SpawnWildernessPanorama(bool coldCrisis = true)
    {
        DestroyAllByName("WildernessPanorama");
        DestroyAllByName("SkybreakColdMist");

        var panoramaGo = new GameObject("WildernessPanorama");
        panoramaGo.transform.position = new Vector3(512f, 90f, 512f);

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mountainMat = new Material(shader);
        // 危機時は冷たい藍灰色、突破後はやや暖かい藍色
        mountainMat.color = coldCrisis
            ? new Color(0.14f, 0.18f, 0.28f)
            : new Color(0.18f, 0.22f, 0.35f);

        var iceMat = new Material(shader);
        iceMat.color = new Color(0.82f, 0.90f, 0.98f); // 氷雪の冠

        // 全周12方向に連なる巨大な未知の山脈・稜線
        for (int i = 0; i < 12; i++)
        {
            float ang = i * 30f * Mathf.Deg2Rad;
            float dist = 680f;
            float peakH = Random.Range(85f, 150f);
            Vector3 pos = new Vector3(Mathf.Cos(ang) * dist, Random.Range(10f, 40f), Mathf.Sin(ang) * dist);

            var peak = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            peak.name = $"WildernessRidge_{i}";
            peak.transform.SetParent(panoramaGo.transform, false);
            peak.transform.localPosition = pos;
            peak.transform.localScale = new Vector3(260f, peakH, 260f);
            peak.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), i * 30f, Random.Range(-8f, 8f));

            var col = peak.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = peak.GetComponent<Renderer>();
            if (rend != null) rend.material = mountainMat;

            // 凍える冠雪（少しだけ：半分の稜線に氷冠）
            if (coldCrisis && (i % 2 == 0))
            {
                var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cap.name = $"IceCrown_{i}";
                cap.transform.SetParent(peak.transform, false);
                cap.transform.localPosition = new Vector3(0f, 0.85f, 0f);
                cap.transform.localScale = new Vector3(0.55f, 0.22f, 0.55f);
                var capCol = cap.GetComponent<Collider>();
                if (capCol != null) Destroy(capCol);
                var capRend = cap.GetComponent<Renderer>();
                if (capRend != null) capRend.material = iceMat;
            }
        }

        // 光芒：危機時は冷たい蒼白＋薄い金、突破後は金色中心
        var raysGo = new GameObject("SkybreakGodRays");
        raysGo.transform.SetParent(panoramaGo.transform, false);
        raysGo.transform.localPosition = new Vector3(0f, 60f, 0f);

        var rayShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        var warmRayMat = new Material(rayShader);
        warmRayMat.color = new Color(1.0f, 0.92f, 0.65f, coldCrisis ? 0.22f : 0.35f);
        var coldRayMat = new Material(rayShader);
        coldRayMat.color = new Color(0.72f, 0.88f, 1.0f, 0.28f);

        for (int r = 0; r < 8; r++)
        {
            var ray = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ray.name = $"GodRay_{r}";
            ray.transform.SetParent(raysGo.transform, false);
            ray.transform.localScale = new Vector3(coldCrisis && r % 2 == 0 ? 6f : 8f, 120f, coldCrisis && r % 2 == 0 ? 6f : 8f);
            ray.transform.localRotation = Quaternion.Euler(Random.Range(15f, 35f), r * 45f + 15f, 0f);

            var col = ray.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = ray.GetComponent<Renderer>();
            if (rend != null)
                rend.material = (coldCrisis && r % 2 == 0) ? coldRayMat : warmRayMat;
        }

        if (coldCrisis)
            SpawnSkybreakColdMist(panoramaGo.transform);
    }

    /// <summary>極寒の氷霞・粉雪が頬をかすめる遠景パーティクル</summary>
    void SpawnSkybreakColdMist(Transform parent)
    {
        var mistGo = new GameObject("SkybreakColdMist");
        mistGo.transform.SetParent(parent, false);
        mistGo.transform.localPosition = new Vector3(0f, 40f, 0f);

        var ps = mistGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 3.8f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.85f, 0.93f, 1f, 0.18f),
            new Color(0.70f, 0.85f, 1f, 0.08f));
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 8f);
        main.maxParticles = 120;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 18f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 90f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-6f, 6f);
        vel.y = new ParticleSystem.MinMaxCurve(-1.5f, 0.5f);
        vel.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

        var colorOver = ps.colorOverLifetime;
        colorOver.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.9f, 0.95f, 1f), 0f),
                new GradientColorKey(new Color(0.7f, 0.85f, 1f), 1f)
            },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.22f, 0.25f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOver.color = grad;

        var rend = mistGo.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Sprites/Default");
            var mat = new Material(sh);
            mat.color = new Color(0.85f, 0.92f, 1f, 0.35f);
            rend.material = mat;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
        }

        ps.Play();
    }

    /// <summary>外気の凍える霧・寒色アンビエント（危機シークエンス用）</summary>
    void ApplySkybreakColdAtmosphere()
    {
        if (!_coldAtmosphereActive)
        {
            _savedFogEnabled = RenderSettings.fog;
            _savedFogColor = RenderSettings.fogColor;
            _savedFogDensity = RenderSettings.fogDensity;
            _savedAmbient = RenderSettings.ambientLight;
            _coldAtmosphereActive = true;
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.62f, 0.74f, 0.88f);
        RenderSettings.fogDensity = 0.0048f;
        RenderSettings.ambientLight = new Color(0.55f, 0.68f, 0.82f);
    }

    /// <summary>注油／突破後：寒さを残しつつ金色の解放感へ寄せる</summary>
    void SoftenSkybreakColdAtmosphere()
    {
        if (!_coldAtmosphereActive)
            ApplySkybreakColdAtmosphere();

        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.78f, 0.82f, 0.88f);
        RenderSettings.fogDensity = 0.0028f;
        RenderSettings.ambientLight = new Color(0.78f, 0.74f, 0.68f);

        // 氷霞を弱める
        var mist = GameObject.Find("SkybreakColdMist");
        if (mist != null)
        {
            var ps = mist.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var emission = ps.emission;
                emission.rateOverTime = 6f;
            }
        }
    }

    void ClearSkybreakColdAtmosphere()
    {
        if (!_coldAtmosphereActive) return;
        RenderSettings.fog = _savedFogEnabled;
        RenderSettings.fogColor = _savedFogColor;
        RenderSettings.fogDensity = _savedFogDensity;
        RenderSettings.ambientLight = _savedAmbient;
        _coldAtmosphereActive = false;
    }

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
}
