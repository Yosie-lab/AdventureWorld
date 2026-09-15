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

    GameObject _scriptUiRoot;
    Image _scriptDimImg;
    Image _scriptPanelImg;
    Image _scriptAccentImg;
    Text _scriptTitleUi;
    Text _scriptSpeakerUi;
    Text _scriptBodyUi;
    Text _scriptHintUi;
    Button _scriptBtn;

    public bool IsSkybreakModalActive => _scriptBoardVisible;
    bool _showSkybreakModal
    {
        get => _scriptBoardVisible;
        set => _scriptBoardVisible = value;
    }
    bool _skybreakModalClosed;

    // ── 【案1】クライマックス演出制御 ──
    bool _climaxCrisisStarted = false;
    bool _climaxOilInjected = false;
    float _oilHoldTimer = 0f;
    const float OilHoldRequired = 1.2f;
    /// <summary>F9／天蓋台本中はクライマックス（警告・注油）を絶対に開始・表示しない</summary>
    bool _suppressClimax = false;

    public bool ClimaxCrisisStarted => _climaxCrisisStarted;
    public bool ClimaxOilInjected => _climaxOilInjected;
    public bool EpilogueTriggered => _epilogueTriggered;
    public float EpilogueAlpha => _epilogueAlpha;
    public bool ShowGameClearModal => _showGameClearModal;
    /// <summary>エピローグ字幕／台本ボード表示中（他HUD・セリフを抑止するため）</summary>
    public bool IsEpiloguePlaying => _scriptBoardVisible || (_epilogueTriggered && !_showGameClearModal && (_epilogueAct > 0 || _epilogueAlpha > 0.01f));
    /// <summary>クライマックス注油待ち中</summary>
    public bool IsClimaxOilPromptActive =>
        !_suppressClimax
        && !_endingSequenceActive
        && _climaxCrisisStarted
        && !_climaxOilInjected
        && !_scriptBoardVisible;



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
        // 開放済みでもレバー再演できるように、ここでは _leverPulled を立てない
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

        var podium = tower.transform.Find("WhiteMarblePodium")?.gameObject;
        if (podium != null)
        {
            var cap = podium.GetComponent<CapsuleCollider>();
            if (cap != null) Destroy(cap);
            if (podium.GetComponent<MeshCollider>() == null)
                podium.AddComponent<MeshCollider>();
        }

        var gridFloor = tower.transform.Find("SanctuaryGridFloor")?.gameObject;
        if (gridFloor != null)
        {
            var cap = gridFloor.GetComponent<CapsuleCollider>();
            if (cap != null) Destroy(cap);
            if (gridFloor.GetComponent<MeshCollider>() == null)
                gridFloor.AddComponent<MeshCollider>();
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

    void Update()
    {
        // F9: パーツ11個から開始（演出確認用）
        var debugKb = UnityEngine.InputSystem.Keyboard.current;
        if (debugKb != null && debugKb.f9Key.wasPressedThisFrame)
        {
            DebugJumpToCanopyOpening();
            return;
        }
        try
        {
            if (Input.GetKeyDown(KeyCode.F9))
            {
                DebugJumpToCanopyOpening();
                return;
            }
        }
        catch { }

        // 台本ボード：Updateでも進む入力を拾う（コルーチン／uGUI漏れ対策）
        if (_scriptBoardVisible && !_scriptBoardAdvance)
            PollScriptBoardAdvance();

        var player = AdventurePlayerController.Instance;
        if (player == null) return;

        // 天蓋破壊後、高度105m付近でRust危機イベント（【案1】クライマックス）を開始
        // タイトル台本〜ダイブ中／F9再生中は絶対に開始しない
        if (!_ignoreSavedCanopyState
            && IsCanopyBroken
            && !_climaxCrisisStarted
            && !_suppressClimax
            && !_endingSequenceActive
            && !_scriptBoardVisible
            && !_epilogueTriggered
            && player.transform.position.y >= 105f)
        {
            _climaxCrisisStarted = true;
            StartCoroutine(ClimaxCrisisSequenceRoutine());
        }

        if (IsGameCleared)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.tabKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
            {
                _showGameClearModal = !_showGameClearModal;
            }
        }

        // 全パーツ回収済み・天蓋未開放ならレバー操作を最優先（誤った危機フラグを解除）
        if (IsLeverReadyToOpen && !IsCanopyBroken && !_scriptBoardVisible && !_endingSequenceActive)
        {
            if (_climaxCrisisStarted && !_climaxOilInjected)
                _climaxCrisisStarted = false;
            if (_epilogueTriggered)
                _epilogueTriggered = false;
        }

        // 台本／危機／エピローグ／シークエンス実行中はレバー入力を止める
        if (_scriptBoardVisible || _climaxCrisisStarted || _epilogueTriggered || _endingSequenceActive)
            return;

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
            return;
        }

        bool tapped = CheckLeverInputTriggered() || (player.InteractPressed && WasInteractEdge());
        bool holding = IsLeverHoldInput();

        if (holding && allCollected)
            _leverHoldTimer += Time.unscaledDeltaTime;
        else if (!holding)
            _leverHoldTimer = 0f;

        if (tapped || _leverHoldTimer >= LeverHoldSeconds)
        {
            _leverHoldTimer = 0f;
            BeginCanopyOpeningFromLever(allCollected);
        }
    }

    void UpdateLeverProximity(AdventurePlayerController player)
    {
        // クリア後の自由探索ではレバー再演プロンプトを出さない（再演は F9）
        // ※F9確認中（_ignoreSavedCanopyState）はクリア済みでもレバー操作を許可
        if (IsGameCleared && !_ignoreSavedCanopyState)
        {
            _playerNearby = false;
            return;
        }

        Vector3 p = player.transform.position;
        Vector2 pXZ = new Vector2(p.x, p.z);
        float distFromCenter = Vector2.Distance(pXZ, new Vector2(512f, 512f));

        // テラス〜頂上を広くカバー（階段途中・端でも反応）
        bool inPlaza = distFromCenter < 70f && p.y >= 40f && p.y <= 170f;

        // 各レバー本体の近くでも確実に反応（大型レバーに合わせて半径拡大）
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
            if (Vector3.Distance(p, leverSpots[i]) < 22f)
            {
                nearAnyLever = true;
                break;
            }
        }

        _playerNearby = (inPlaza || nearAnyLever) && !_showSkybreakModal;
    }

    /// <summary>パーツ12個達成時：巨大レバーを再生成し、操作UIを確実に出す</summary>
    public void OnAllScrapsCollectedForLever()
    {
        // 誤って残った危機／台本フラグをクリア（レバー操作を塞がない）
        if (!_endingSequenceActive)
        {
            _climaxCrisisStarted = false;
            _climaxOilInjected = false;
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

    /// <summary>レバーから天蓋開放へ。既に開放済み・途中停止でも再演できる</summary>
    void BeginCanopyOpeningFromLever(bool allCollected)
    {
        // ボード／クライマックス／エピローグ中は触らない
        if (_scriptBoardVisible || _climaxCrisisStarted || _epilogueTriggered)
            return;
        // 実行中（レバーアニメ〜台本）は再入禁止。開放済みの再演は下のリセットへ
        if (_endingSequenceActive && !IsCanopyBroken)
            return;

        if (!allCollected)
        {
            TryPullLever(false);
            return;
        }

        // 途中停止・開放済み・実行フラグ残りをクリアして開始
        if (_leverPulled || IsCanopyBroken || IsGameCleared || _endingSequenceActive)
        {
            StopAllCoroutines();
            Time.timeScale = 1f;
            _endingSequenceActive = false;
            _climaxCrisisStarted = false;
            _climaxOilInjected = false;
            _oilHoldTimer = 0f;
            _epilogueTriggered = false;
            _epilogueAlpha = 0f;
            _epilogueAct = 0;
            _scriptBoardVisible = false;
            _scriptBoardAdvance = false;
            _scriptBoardTitle = "";
            _scriptBoardSpeaker = "";
            _scriptBoardBody = "";
            _showGameClearModal = false;
            IsGameCleared = false;
            IsCanopyBroken = false;
            _leverPulled = false;
            if (_scriptUiRoot != null)
                _scriptUiRoot.SetActive(false);
            SetExplorationHudVisible(true);
            SetCinematicCamera(false);
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

    /// <summary>クリア後／F8：天蓋・クリア・クライマックス進行をニューゲーム用に完全リセット</summary>
    public void ResetProgressForNewGame()
    {
        ClearEndingRuntimeState(ignoreSavedCanopy: false);
        DestroySkybreakWorldFx();
        SetExplorationHudVisible(true);
        SetCinematicCamera(false);
        AdventureMusicDirector.Ensure();
        AdventureMusicDirector.Instance?.ResetSkybreakMusicState();
        Debug.Log("[RustAndFloat] ニューゲーム用に天蓋／クリア進行をリセットしました");
    }

    /// <summary>
    /// 演出確認用（F9）：パーツ11個取得済み・天蓋未開放から開始。
    /// ※12個目を取ってからレバー。警告クライマックスには飛ばない。
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
        BuildTowerLever();
        DestroySkybreakWorldFx();
        SetExplorationHudVisible(true);
        SetCinematicCamera(false);

        AdventureScrapManager.Ensure();
        AdventureScrapManager.Instance?.ResetToCount(11);
        AdventureMusicDirector.Ensure();
        AdventureMusicDirector.Instance?.ResetSkybreakMusicState();

        foreach (var h in _allLeverHandles)
        {
            if (h != null)
                h.localRotation = Quaternion.identity;
        }

        FindAnyObjectByType<AdventureRustFloatOpening>()?.ForceDismissForGameplay();

        var player = AdventurePlayerController.Instance
                     ?? Object.FindFirstObjectByType<AdventurePlayerController>();
        if (player != null)
        {
            player.SetAutoGlideMode(false);
            player.ForceGroundReset();
            Vector3 pos = new Vector3(512f, 63.5f, 504f);
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
            drone.SpeakCustom("あと1個で翼が完成する！最後のパーツを見つけてから、真鍮レバーを引こう、Niko！", 6.0f);
        }

        ClearBoardsBlockingLever();
        _leverHoldTimer = 0f;
        if (player != null)
            UpdateLeverProximity(player);

        Debug.Log("[RustAndFloat] F9: パーツ11/12・天蓋未開放から開始（12個目取得→巨大レバー→タイトルボード）");
    }

    void ClearEndingRuntimeState(bool ignoreSavedCanopy)
    {
        StopAllCoroutines();
        Time.timeScale = 1f;

        _ignoreSavedCanopyState = ignoreSavedCanopy;
        IsCanopyBroken = false;
        IsGameCleared = false;

        _leverPulled = false;
        _endingSequenceActive = false;
        _suppressClimax = false;
        _climaxCrisisStarted = false;
        _climaxOilInjected = false;
        _oilHoldTimer = 0f;
        _epilogueTriggered = false;
        _epilogueAlpha = 0f;
        _epilogueAct = 0;
        _scriptBoardVisible = false;
        _scriptBoardAdvance = false;
        _scriptBoardTitle = "";
        _scriptBoardSpeaker = "";
        _scriptBoardBody = "";
        _showGameClearModal = false;
        _playerNearby = false;
        _leverHoldTimer = 0f;

        TeardownScriptBoardUi();
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
        if (_hyperUpdraftGo != null)
        {
            Destroy(_hyperUpdraftGo);
            _hyperUpdraftGo = null;
        }
        DestroyAllByName("SkybreakHyperUpdraft");
        DestroyAllByName("WildernessPanorama");
        DestroyAllByName("SkybreakEffect");
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
        // IsCanopyBroken / 上昇気流 はダイブ確定後。BGMはナレーションから
        StartCoroutine(SkybreakSequenceRoutine());
    }

    IEnumerator SkybreakSequenceRoutine()
    {
        // クライマックス中は蝉時雨を完全カット（BGM・セリフを優先）
        AdventureCicadaAmbienceManager.Ensure();
        AdventureCicadaAmbienceManager.Instance?.MuteForEndingSequence();

        var player = AdventurePlayerController.Instance
                     ?? Object.FindFirstObjectByType<AdventurePlayerController>();
        // 台本中に空中で浮いたままにならないよう接地へ戻す
        if (player != null)
            player.ForceGroundReset();

        // 1. レバーをガチャンと手前へ引き倒す
        if (_audio != null)
            _audio.PlayOneShot(MakeHeavyLeverSound(), 0.9f);

        float elapsed = 0f;
        float duration = 0.65f;
        Quaternion startRot = _leverHandle != null ? _leverHandle.localRotation : Quaternion.identity;
        Quaternion endRot = Quaternion.Euler(38f, 0f, 0f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            foreach (var h in _allLeverHandles)
            {
                if (h != null) h.localRotation = Quaternion.Slerp(startRot, endRot, t * t);
            }
            yield return null;
        }

        SpawnLeverSparks(new Vector3(512f, 63.5f, 512f));
        SuppressAllSpeechAndBanners();
        SpawnSkybreakCracks(new Vector3(512f, 150f, 512f));

        if (_hyperUpdraftGo != null)
            _hyperUpdraftGo.SetActive(false);

        yield return StartCoroutine(SkybreakFromTitleBoardRoutine());
    }

    /// <summary>天蓋破壊ボード（タイトル＋ナレ）から台本〜ダイブ。RustはNikoに寄り添う</summary>
    IEnumerator SkybreakFromTitleBoardRoutine()
    {
        _suppressClimax = true;
        _climaxCrisisStarted = false;
        _endingSequenceActive = true;

        var player = AdventurePlayerController.Instance
                     ?? Object.FindFirstObjectByType<AdventurePlayerController>();
        if (player != null && player.transform.position.y < 90f)
            player.ForceGroundReset();

        // ボード表示と同時に天空突破BGM（D→F#m→Em→Gm）を開始
        AdventureMusicDirector.Ensure();
        AdventureMusicDirector.Instance?.PlaySkybreakTheme(force: true);

        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            drone.ClearSpeech();
            // 天蓋破壊ボード表示と同時に、Nikoの頭の辺りへ寄り添う
            drone.StartSkybreakNestle();
        }

        // ① 天蓋破壊ボード（タイトル＋ナレーション）
        yield return StartCoroutine(ShowScriptBeat(
            "天蓋崩壊　未知の荒野への跳躍",
            "",
            "空が割れた。\n冷たい本物の風が頬を打つ。",
            new Color(1f, 0.9f, 0.45f, 1f)));

        // 台本：Rust → Niko → …
        yield return StartCoroutine(ShowScriptBeat(
            "",
            "✦ 相棒 Rust",
            "この楽園もAIに最適化された虚構の島だったんだ!!",
            new Color(0.35f, 0.92f, 0.98f, 1f)));

        yield return StartCoroutine(ShowScriptBeat(
            "",
            "✦ 相棒 Rust",
            "空が……割れるよ、Niko！　つかまって！！",
            new Color(0.35f, 0.92f, 0.98f, 1f)));

        yield return StartCoroutine(ShowScriptBeat(
            "",
            "✦ Niko",
            "ありがとうRust…！君がいたからここまで来られた。行こう！",
            new Color(1f, 0.88f, 0.45f, 1f)));

        yield return StartCoroutine(ShowScriptBeat(
            "",
            "✦ 相棒 Rust",
            "あれが本物の空だ……！風に乗って、あの裂け目へ飛び込もう、Niko！！",
            new Color(0.35f, 0.92f, 0.98f, 1f)));

        yield return StartCoroutine(ShowScriptBeat(
            "",
            "",
            "タワー中央の光の柱へ飛び込み、\n空の裂け目を突き抜けよう。",
            new Color(0.85f, 0.95f, 1f, 1f)));

        yield return StartCoroutine(ShowScriptBeat(
            "空の裂け目へ",
            "",
            "【Space / クリック】でダイブする",
            new Color(1f, 0.88f, 0.4f, 1f),
            isDive: true));

        // ダイブ確定後に天蓋フラグ・セーブ・上昇気流
        IsCanopyBroken = true;
        AdventureSaveManager.Instance?.SaveGame("天蓋開放・到達記録を保存しました");

        BuildSkybreakHyperUpdraft(new Vector3(512f, 62f, 512f));
        _endingSequenceActive = false;
        _suppressClimax = false; // 高度到達後のクライマックスは許可
        _climaxOilInjected = false;
        _climaxCrisisStarted = false;
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
    IEnumerator ShowScriptBeat(string title, string speaker, string body, Color accent, bool isDive = false)
    {
        SuppressAllSpeechAndBanners();

        var player = AdventurePlayerController.Instance;
        if (player != null && player.transform.position.y < 90f)
            player.ForceGroundReset();

        _scriptBoardTitle = title ?? "";
        _scriptBoardSpeaker = speaker ?? "";
        _scriptBoardBody = body ?? "";
        _scriptBoardAccent = accent;
        _scriptBoardIsDive = isDive;
        _scriptBoardAdvance = false;
        _scriptBoardVisible = true;
        _scriptBoardOpenedAt = Time.unscaledTime;
        _scriptAdvanceArmed = false;
        _scriptPrevPointerDown = true; // 表示直後の押しっぱなしは無視
        _scriptPrevKeyDown = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureScriptBoardUI();
        ApplyScriptBoardUI();

        // 最低表示（短め）＋入力アーム待ち
        const float minShow = 0.25f;
        while (Time.unscaledTime - _scriptBoardOpenedAt < minShow)
            yield return null;

        while (!_scriptBoardAdvance)
        {
            PollScriptBoardAdvance();
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

    /// <summary>クリック／Space の立ち上がりで確実に進む（UI・Input Systemの漏れ対策）</summary>
    void PollScriptBoardAdvance()
    {
        if (!_scriptBoardVisible || _scriptBoardAdvance) return;
        if (Time.unscaledTime - _scriptBoardOpenedAt < 0.25f) return;

        bool pointerDown = IsScriptPointerDown();
        bool keyDown = IsScriptKeyDown();

        // 一度離してから次の押下だけを受け付ける
        if (!pointerDown && !keyDown)
            _scriptAdvanceArmed = true;

        if (_scriptAdvanceArmed)
        {
            bool pointerPressed = pointerDown && !_scriptPrevPointerDown;
            bool keyPressed = keyDown && !_scriptPrevKeyDown;
            if (pointerPressed || keyPressed)
                _scriptBoardAdvance = true;
        }

        _scriptPrevPointerDown = pointerDown;
        _scriptPrevKeyDown = keyDown;
    }

    static bool IsScriptPointerDown()
    {
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed))
            return true;
        var pen = UnityEngine.InputSystem.Pen.current;
        if (pen != null && pen.tip.isPressed)
            return true;
        var touch = UnityEngine.InputSystem.Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.isPressed)
            return true;
        try
        {
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
                return true;
        }
        catch { }
        return false;
    }

    static bool IsScriptKeyDown()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && (kb.spaceKey.isPressed || kb.enterKey.isPressed ||
                           kb.numpadEnterKey.isPressed || kb.eKey.isPressed))
            return true;
        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (pad != null && (pad.buttonSouth.isPressed || pad.buttonWest.isPressed))
            return true;
        try
        {
            if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.E))
                return true;
        }
        catch { }
        return false;
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
            _scriptUiRoot.SetActive(true);
            return;
        }

        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
        else
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>() == null
                && es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
            {
                es.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
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
        if (!_scriptBoardVisible) return;
        if (Time.unscaledTime - _scriptBoardOpenedAt < 0.25f) return;
        _scriptAdvanceArmed = true;
        _scriptBoardAdvance = true;
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

        // セリフ＝大きく／説明＝やや控えめ、見た目で差をつける
        int titleSize = isNarration ? 30 : 26;
        int speakerSize = 22;
        int bodySize = isDialogue ? 34 : 28;
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
            _scriptHintUi.text = _scriptBoardIsDive
                ? "【Space / クリック】空の裂け目へダイブ"
                : "【Space / クリック】つづき";
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

    static Font ResolveUiFont()
    {
        string[] candidates =
        {
            "Hiragino Sans",
            "Hiragino Kaku Gothic ProN",
            "HiraginoSans-W3",
            "YuGothic",
            "Yu Gothic",
            "Arial Unicode MS"
        };
        foreach (string name in candidates)
        {
            try
            {
                Font os = Font.CreateDynamicFontFromOSFont(name, 28);
                if (os != null) return os;
            }
            catch { }
        }
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
               ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
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

        _hyperUpdraftGo = new GameObject("SkybreakHyperUpdraft");
        _hyperUpdraftGo.transform.position = basePos;

        var updraft = _hyperUpdraftGo.AddComponent<AdventureThermalUpdraft>();
        updraft.autoLaunch = true; // 歩いて触れるだけでも自動で大空へダイブ・射出！
        updraft.radius = 32.0f; // タワー中央テラス全域を覆う巨大な上昇気流
        updraft.height = 180.0f; // 高度240m以上の空の裂け目まで突き抜ける
        updraft.liftSpeed = 22.0f; // 超高速で大空へ射出！

        // 天を衝く超巨大な天空光柱（シアン＆黄金に輝く半透明シリンダー）
        var pillarGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillarGo.name = "SkybreakHyperBeam";
        pillarGo.transform.SetParent(_hyperUpdraftGo.transform, false);
        pillarGo.transform.localPosition = new Vector3(0f, 90f, 0f);
        pillarGo.transform.localScale = new Vector3(20f, 90f, 20f);

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

    void OnGUI()
    {
        // 台本ボードを最優先（他UIより先に描画）
        DrawScriptBoardGUI();
        DrawClimaxCrisisGUI();
        DrawGameClearModalGUI();

        // シークエンス表示・実行中だけレバー再演ボタンを隠す
        if (_scriptBoardVisible || _climaxCrisisStarted || _epilogueTriggered || _endingSequenceActive)
        {
            DrawEpilogueGUI();
            return;
        }

        if (!IsPlayerNearLever)
        {
            DrawEpilogueGUI();
            return;
        }

        bool allCollected = IsLeverReadyToOpen;
        bool canReplay = allCollected && (_leverPulled || IsCanopyBroken);

        // 押しやすい超大型ボタン（画面下部中央）
        float w = Mathf.Min(1100f, Screen.width * 0.94f);
        float h = 120f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height - 168f;
        var boxRect = new Rect(x, y, w, h);

        GUI.color = new Color(0.02f, 0.06f, 0.12f, 0.82f);
        GUI.DrawTexture(boxRect, Texture2D.whiteTexture);

        // 長押しゲージ
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

        var btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        btnStyle.normal.background = Texture2D.whiteTexture;

        if (allCollected)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.01f);
            if (GUI.Button(boxRect, GUIContent.none, btnStyle))
                BeginCanopyOpeningFromLever(true);

            string btnText = canReplay
                ? "【E / Space 長押し or クリック】天蓋開放を再演する"
                : "【E / Space 長押し or クリック】巨大真鍮レバーを引く";

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            labelStyle.normal.textColor = new Color(0f, 0f, 0f, 0.9f);
            GUI.Label(new Rect(x, y + 2f, w, h), btnText, labelStyle);
            labelStyle.normal.textColor = new Color(0.35f, 0.98f, 0.88f, 1f);
            GUI.Label(boxRect, btnText, labelStyle);
        }
        else
        {
            var scrapMgr = AdventureScrapManager.Instance ?? Object.FindFirstObjectByType<AdventureScrapManager>();
            int count = scrapMgr != null ? scrapMgr.CollectedCount : 0;
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            labelStyle.normal.textColor = new Color(1.0f, 0.85f, 0.45f);
            GUI.Label(boxRect, $"【E】巨大真鍮レバーを調べる（要：遺物 12個 / 現在 {count}個）", labelStyle);
        }

        GUI.color = Color.white;
        DrawEpilogueGUI();
    }

    void DrawClimaxCrisisGUI()
    {
        if (_ignoreSavedCanopyState) return;
        // 台本／F9再生中は注油UIを絶対に出さない
        if (_suppressClimax || _endingSequenceActive || _scriptBoardVisible) return;
        if (!_climaxCrisisStarted || _climaxOilInjected) return;

        // 映画のような上下黒帯
        Color barCol = new Color(0.02f, 0.04f, 0.08f, 0.92f);
        float barH = Mathf.Clamp(Screen.height * 0.11f, 70f, 120f);
        GUI.color = barCol;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, barH), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, Screen.height - barH, Screen.width, barH), Texture2D.whiteTexture);

        Font font = ResolveEpilogueFont();
        int titleSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.032f), 26, 36);
        int promptSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.036f), 28, 40);

        // 画面中央の大パネル（下部帯や他セリフと重ねない）
        float panelW = Mathf.Min(980f, Screen.width * 0.9f);
        float panelH = Mathf.Clamp(Screen.height * 0.28f, 200f, 280f);
        float px = (Screen.width - panelW) * 0.5f;
        float py = (Screen.height - panelH) * 0.5f;

        GUI.color = new Color(0.02f, 0.05f, 0.10f, 0.96f);
        GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.45f, 0.35f, 0.95f);
        GUI.DrawTexture(new Rect(px, py, panelW, 3f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(px, py + panelH - 3f, panelW, 3f), Texture2D.whiteTexture);

        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            font = font,
            fontSize = titleSize,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            clipping = TextClipping.Overflow
        };
        DrawShadowedText(
            new Rect(px + 24f, py + 18f, panelW - 48f, titleSize * 1.4f),
            "警告　Rustが極寒で機能停止寸前",
            titleStyle,
            new Color(1f, 0.55f, 0.45f, 1f),
            new Color(0f, 0f, 0f, 0.9f),
            1.2f);

        var promptStyle = new GUIStyle(GUI.skin.label)
        {
            font = font,
            fontSize = promptSize,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            clipping = TextClipping.Overflow
        };
        DrawShadowedText(
            new Rect(px + 24f, py + 18f + titleSize * 1.5f, panelW - 48f, promptSize * 2.2f),
            "【E または クリック長押し】\n最後の油を注ぐ",
            promptStyle,
            new Color(1f, 0.92f, 0.4f, 1f),
            new Color(0f, 0f, 0f, 0.9f),
            1.2f);

        float barW = Mathf.Min(720f, panelW - 80f);
        float barH2 = 28f;
        float bx = px + (panelW - barW) * 0.5f;
        float by = py + panelH - 52f;

        GUI.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);
        GUI.DrawTexture(new Rect(bx, by, barW, barH2), Texture2D.whiteTexture);
        float fillRatio = Mathf.Clamp01(_oilHoldTimer / OilHoldRequired);
        GUI.color = new Color(1.0f, 0.82f, 0.22f, 1.0f);
        GUI.DrawTexture(new Rect(bx, by, barW * fillRatio, barH2), Texture2D.whiteTexture);

        GUI.color = Color.white;
    }

    IEnumerator ClimaxCrisisSequenceRoutine()
    {
        // タイトル台本再生中／F9再生中は危機シークエンスを開始しない
        if (_suppressClimax || _endingSequenceActive || _scriptBoardVisible)
        {
            _climaxCrisisStarted = false;
            yield break;
        }

        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        var player = AdventurePlayerController.Instance;

        SetCinematicCamera(true);
        SetExplorationHudVisible(false);

        if (player != null)
            player.SetAutoGlideMode(true, Mathf.Clamp(player.transform.position.y, 115f, 125f));

        Time.timeScale = 0.35f;
        SuppressAllSpeechAndBanners();

        // 台本：警告 → Rust危機セリフ（吹き出しは出さない）
        yield return StartCoroutine(ShowScriptBeat(
            "警告",
            "",
            "Rustが極寒で機能停止寸前",
            new Color(1f, 0.55f, 0.45f, 1f)));

        if (drone != null)
        {
            drone.StartClimaxCrisis();
            drone.ClearSpeech(); // 吹き出し抑止・ボードで読む
        }

        yield return StartCoroutine(ShowScriptBeat(
            "",
            "✦ 相棒 Rust",
            "キキキッ……！ Niko……外の気流が冷たすぎる……僕の古いギアが……凍りついて……",
            new Color(0.35f, 0.92f, 0.98f, 1f)));

        yield return StartCoroutine(ShowScriptBeat(
            "",
            "✦ Niko",
            "Rust…待ってて！　今、油を目一杯さすからね！",
            new Color(1f, 0.88f, 0.45f, 1f)));

        // 注油UIのみ表示（他ボード・セリフなし）
        float elapsed = 0f;
        while (!_climaxOilInjected && elapsed < 12.0f)
        {
            elapsed += Time.unscaledDeltaTime;

            bool eHolding = false;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (kb != null && (kb.eKey.isPressed || kb.spaceKey.isPressed)) eHolding = true;
            if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed)) eHolding = true;
            try { if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0) || Input.GetMouseButton(1)) eHolding = true; } catch { }

            if (eHolding)
            {
                _oilHoldTimer += Time.unscaledDeltaTime;
                if (_oilHoldTimer >= OilHoldRequired)
                    _climaxOilInjected = true;
            }
            else
            {
                _oilHoldTimer = Mathf.Max(0f, _oilHoldTimer - Time.unscaledDeltaTime * 1.5f);
            }

            yield return null;
        }

        _climaxOilInjected = true;
        if (drone != null)
        {
            drone.StartClimaxPetAndOil();
            drone.ClearSpeech();
        }

        yield return StartCoroutine(ShowScriptBeat(
            "",
            "✦ 相棒 Rust",
            "……あ……温かい油が……心臓に……！",
            new Color(0.35f, 0.92f, 0.98f, 1f)));

        yield return new WaitForSecondsRealtime(0.35f);

        if (drone != null)
        {
            drone.TriggerClimaxOverdrive();
            drone.ClearSpeech();
        }

        yield return StartCoroutine(ShowScriptBeat(
            "",
            "✦ 相棒 Rust",
            "ピピッ！……ありがとうNiko！僕たちの翼は絶対に折れない！全出力で行くよ！！",
            new Color(0.35f, 0.92f, 0.98f, 1f)));

        float blend = 0f;
        while (blend < 1f)
        {
            blend += Time.unscaledDeltaTime * 1.6f;
            Time.timeScale = Mathf.Lerp(0.35f, 1.0f, blend);
            yield return null;
        }
        Time.timeScale = 1.0f;

        if (player != null)
        {
            player.SetAutoGlideMode(false);
            player.ApplyGlideBoost(3.2f, 75f);
            player.ApplyUpdraft(28f);
        }

        yield return new WaitForSeconds(1.8f);
        _epilogueTriggered = true;
        StartCoroutine(EpilogueSequenceRoutine());
    }

    void DrawScriptBoardGUI()
    {
        // 台本ボード本体は uGUI。ここでは全画面クリックを最優先で拾う
        if (!_scriptBoardVisible) return;
        if (Time.unscaledTime - _scriptBoardOpenedAt < 0.25f) return;

        // Gameビュー全面の透明ボタン（Input System漏れ時の最終手段）
        Color prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.002f);
        if (GUI.Button(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none, GUIStyle.none))
        {
            _scriptAdvanceArmed = true;
            _scriptBoardAdvance = true;
        }
        GUI.color = prev;

        Event e = Event.current;
        if (e == null) return;

        if (e.type == EventType.KeyDown &&
            (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.E))
        {
            _scriptAdvanceArmed = true;
            _scriptBoardAdvance = true;
            e.Use();
        }
    }

    /// <summary>文字を太らせず、映画字幕のようにシャープで読みやすい上品なドロップシャドウ描画</summary>
    static void DrawShadowedText(Rect rect, string text, GUIStyle style, Color textColor, Color shadowColor, float offset = 1.5f)
    {
        if (string.IsNullOrEmpty(text)) return;

        // 動的OSフォントは字形リクエストしないと日本語が空になる
        if (style.font != null)
        {
            style.font.RequestCharactersInTexture(text, style.fontSize, style.fontStyle);
            style.font.RequestCharactersInTexture(text, style.fontSize, FontStyle.Normal);
        }

        Color prevGui = GUI.color;
        Color prevContent = GUI.contentColor;
        GUI.color = Color.white;
        GUI.contentColor = Color.white;

        Color origColor = style.normal.textColor;

        style.normal.textColor = shadowColor;
        GUI.Label(new Rect(rect.x, rect.y + offset, rect.width, rect.height), text, style);

        style.normal.textColor = textColor;
        GUI.Label(rect, text, style);

        style.normal.textColor = origColor;
        GUI.color = prevGui;
        GUI.contentColor = prevContent;
    }

    static readonly string[] EpilogueActs =
    {
        "空が割れた。\n100%最適化された箱庭の外には、\n凍えるほどリアルな風が吹いていた。",
        "人は最短距離を走っている時ではなく、\n寄り道をして、躓き、\n息をのんだ瞬間に、生きてる実感を得るんだ。",
        "傷つく自由と、風の重さを取り戻した\n二人の旅が、いま始まる。\n―― 『Rust & Float』"
    };

    Font ResolveEpilogueFont()
    {
        if (_epilogueFont != null)
            return _epilogueFont;

        // 配列指定のほうが Mac で日本語グリフを取りやすい
        try
        {
            _epilogueFont = Font.CreateDynamicFontFromOSFont(
                new[]
                {
                    "Hiragino Sans",
                    "HiraginoSans-W3",
                    "Hiragino Kaku Gothic ProN",
                    "YuGothic",
                    "Yu Gothic",
                    "Arial Unicode MS"
                },
                32);
        }
        catch { }

        if (_epilogueFont == null)
        {
            _epilogueFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                            ?? Resources.GetBuiltinResource<Font>("Arial.ttf")
                            ?? GUI.skin.font;
        }
        return _epilogueFont;
    }

    void DrawEpilogueGUI()
    {
        // テロップは台本ボード（ShowScriptBeat）に統一。旧帯テロップは出さない
        if (_scriptBoardVisible) return;
        if (_epilogueAlpha <= 0.01f && _epilogueAct <= 0) return;

        float alpha = Mathf.Clamp01(_epilogueAlpha);

        // 上下黒帯（字幕は帯の外＝画面中央に置くので、帯は装飾のみ）
        Color barCol = new Color(0.01f, 0.02f, 0.05f, alpha * 0.94f);
        float barH = Mathf.Clamp(Screen.height * 0.10f, 64f, 110f);
        GUI.color = barCol;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, barH), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, Screen.height - barH, Screen.width, barH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (_epilogueAct < 1 || _epilogueAct > EpilogueActs.Length)
            return;

        string line = EpilogueActs[_epilogueAct - 1];
        Font font = ResolveEpilogueFont();

        // 中央パネルに大テロップ（下部のRustセリフ帯と絶対に重ねない）
        int fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.038f), 28, 44);
        var style = new GUIStyle(GUI.skin.label)
        {
            font = font,
            fontSize = fontSize,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            richText = false,
            clipping = TextClipping.Overflow
        };

        float panelW = Mathf.Min(1100f, Screen.width * 0.9f);
        float panelH = Mathf.Clamp(fontSize * 5.2f, 160f, Screen.height * 0.42f);
        float padX = (Screen.width - panelW) * 0.5f;
        float panelY = (Screen.height - panelH) * 0.5f;

        // 半透明ダーク板で背景と分離
        GUI.color = new Color(0.02f, 0.04f, 0.08f, 0.82f * alpha);
        GUI.DrawTexture(new Rect(padX - 12f, panelY - 10f, panelW + 24f, panelH + 20f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        Rect bodyRect = new Rect(padX, panelY, panelW, panelH);
        DrawShadowedText(
            bodyRect,
            line,
            style,
            new Color(0.98f, 0.99f, 1.0f, alpha),
            new Color(0f, 0f, 0f, alpha * 0.75f),
            1.2f);
    }

    void DrawGameClearModalGUI()
    {
        if (!_showGameClearModal || _scriptBoardVisible) return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Font font = ResolveEpilogueFont();

        // 全画面の半透明オーバーレイ
        GUI.color = new Color(0.01f, 0.02f, 0.05f, 0.55f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        // 大きめのシネマティック・リザルトカード（画面の大部分）
        float bw = Mathf.Min(1280f, Screen.width * 0.96f);
        float bh = Mathf.Min(760f, Screen.height * 0.94f);
        float bx = (Screen.width - bw) * 0.5f;
        float by = (Screen.height - bh) * 0.5f;

        GUI.color = new Color(0.02f, 0.05f, 0.10f, 0.94f);
        GUI.DrawTexture(new Rect(bx, by, bw, bh), Texture2D.whiteTexture);

        GUI.color = new Color(0.35f, 0.92f, 1.0f, 0.95f);
        GUI.DrawTexture(new Rect(bx, by, bw, 4f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(bx, by + bh - 4f, bw, 4f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(bx, by, 4f, bh), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(bx + bw - 4f, by, 4f, bh), Texture2D.whiteTexture);

        GUI.color = new Color(1.0f, 0.85f, 0.40f, 0.85f);
        GUI.DrawTexture(new Rect(bx + 14f, by + 12f, bw - 28f, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(bx + 14f, by + bh - 14f, bw - 28f, 2f), Texture2D.whiteTexture);

        int titleSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.048f), 40, 56);
        int subSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.028f), 26, 34);
        int statSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.030f), 28, 36);
        int quoteSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.032f), 30, 38);
        int noteSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.024f), 22, 28);
        int btnSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.030f), 26, 34);

        float pad = 36f;
        float y = by + 28f;

        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            font = font,
            fontSize = titleSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            clipping = TextClipping.Overflow
        };
        float titleH = titleSize + 24f;
        DrawShadowedText(new Rect(bx + pad, y, bw - pad * 2f, titleH), "✦ 『Rust & Float』 GAME CLEAR ✦", titleStyle, new Color(1.0f, 0.88f, 0.40f, 1f), Color.black, 2.0f);
        y += titleH + 8f;

        var subStyle = new GUIStyle(GUI.skin.label)
        {
            font = font,
            fontSize = subSize,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            clipping = TextClipping.Overflow
        };
        string subText = "天蓋の檻を打ち破り、二人は未知なる本物の風の待つ空へ羽ばたいた。";
        float subH = Mathf.Max(subSize * 2.4f, 64f);
        DrawShadowedText(new Rect(bx + pad, y, bw - pad * 2f, subH), subText, subStyle, new Color(0.85f, 0.95f, 1.0f, 0.95f), Color.black, 1.5f);
        y += subH + 16f;

        float btnArea = 110f;
        float rx = bx + 40f;
        float rw = bw - 80f;
        float rh = Mathf.Max(300f, (by + bh - btnArea) - y - 12f);
        float ry = y;
        GUI.color = new Color(0.04f, 0.08f, 0.15f, 0.85f);
        GUI.DrawTexture(new Rect(rx, ry, rw, rh), Texture2D.whiteTexture);

        var statStyle = new GUIStyle(GUI.skin.label)
        {
            font = font,
            fontSize = statSize,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true,
            clipping = TextClipping.Overflow
        };
        var quoteStyle = new GUIStyle(GUI.skin.label)
        {
            font = font,
            fontSize = quoteSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true,
            clipping = TextClipping.Overflow
        };
        var noteStyle = new GUIStyle(GUI.skin.label)
        {
            font = font,
            fontSize = noteSize,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true,
            clipping = TextClipping.Overflow
        };

        float innerX = rx + 28f;
        float innerW = rw - 56f;
        float rowY = ry + 22f;
        float rowGap = 12f;
        float statRowH = statSize * 2.3f;
        float quoteRowH = quoteSize * 2.5f;
        float noteRowH = noteSize * 2.5f;

        DrawShadowedText(new Rect(innerX, rowY, innerW, statRowH), "✦ 漂着古代パーツ回収： 12 / 12  【完全修復 COMPLETE】", statStyle, Color.white, Color.black, 1.5f);
        rowY += statRowH + rowGap;
        DrawShadowedText(new Rect(innerX, rowY, innerW, statRowH), "✦ 相棒Rustの機能： 二段ジャンプ・超滑空・探知ソナー・魂の点火", statStyle, new Color(0.9f, 0.95f, 1f), Color.black, 1.5f);
        rowY += statRowH + rowGap;
        DrawShadowedText(new Rect(innerX, rowY, innerW, statRowH), "✦ 解放された世界： 未知の地球・連なる山脈パノラマ・無限天空", statStyle, new Color(0.9f, 0.95f, 1f), Color.black, 1.5f);
        rowY += statRowH + rowGap + 6f;
        DrawShadowedText(new Rect(innerX, rowY, innerW, quoteRowH), "「ありがとう、Niko。僕たちの翼で、どこまでも行こう……！」", quoteStyle, new Color(1.0f, 0.90f, 0.45f), Color.black, 1.5f);
        rowY += quoteRowH + rowGap;
        DrawShadowedText(new Rect(innerX, rowY, innerW, noteRowH), "※クリア後も自由探索できます。【N】／「はじめから」でパーツ配置を変えて再冒険！", noteStyle, new Color(0.65f, 0.85f, 0.95f, 0.85f), Color.black, 1.2f);

        float btnW = (bw - 120f) / 3f;
        float btnH = Mathf.Max(74f, btnSize + 40f);
        float btnY = by + bh - btnH - 28f;
        float gap = 20f;

        var btnStyle1 = new GUIStyle(GUI.skin.button)
        {
            font = font,
            fontSize = Mathf.Max(20, btnSize - 4),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            clipping = TextClipping.Overflow
        };

        GUI.color = new Color(0.20f, 0.75f, 0.95f, 0.95f);
        if (GUI.Button(new Rect(bx + 40f, btnY, btnW, btnH), "✨ 大空へダイブ\n【Space】", btnStyle1))
            RelaunchIntoSky();

        GUI.color = new Color(0.35f, 0.82f, 0.55f, 0.95f);
        if (GUI.Button(new Rect(bx + 40f + btnW + gap, btnY, btnW, btnH), "🌅 はじめから\n【N】", btnStyle1))
            StartNewGameFromClearModal();

        GUI.color = new Color(0.25f, 0.35f, 0.45f, 0.95f);
        if (GUI.Button(new Rect(bx + 40f + (btnW + gap) * 2f, btnY, btnW, btnH), "閉じる\n【E / Esc】", btnStyle1))
            CloseGameClearModalForFreeExplore();

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

        GUI.color = Color.white;
    }

    void StartNewGameFromClearModal()
    {
        _showGameClearModal = false;
        AdventureSaveManager.Ensure();
        AdventureSaveManager.Instance?.ResetToNewGame();
    }

    void CloseGameClearModalForFreeExplore()
    {
        _showGameClearModal = false;
        var player = AdventurePlayerController.Instance;
        if (player != null)
            player.SetAutoGlideMode(false);
        SetCinematicCamera(false);
        SetExplorationHudVisible(true);
    }

    /// <summary>ゲームクリアリザルト画面を直接開く</summary>
    public void OpenGameClearModal()
    {
        _showGameClearModal = true;
    }

    /// <summary>クリア後に何度でも大空へ飛び立てるリダイブ処理</summary>
    public void RelaunchIntoSky()
    {
        _showGameClearModal = false;
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
        SpawnWildernessPanorama();
        SuppressAllSpeechAndBanners();

        // 旧テロップ帯は使わず、台本ボードで1枚ずつ
        _epilogueAct = 0;
        _epilogueAlpha = 0f;

        yield return StartCoroutine(ShowScriptBeat(
            "",
            "✦ 相棒 Rust",
            "わぁぁ……！見て、Niko！世界はこんなに広かったんだ……！！",
            new Color(0.35f, 0.92f, 0.98f, 1f)));

        for (int i = 0; i < EpilogueActs.Length; i++)
        {
            KeepAutoGlide(player);
            yield return StartCoroutine(ShowScriptBeat(
                i == 0 ? "エピローグ" : "",
                "",
                EpilogueActs[i],
                new Color(1f, 0.92f, 0.55f, 1f)));
        }

        KeepAutoGlide(player);
        IsGameCleared = true;
        _showGameClearModal = true;
        SuppressAllSpeechAndBanners();
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

    /// <summary>天蓋の割れ目の外側に広がる「未知の地球・荒野の山脈シルエット」と光芒を生成</summary>
    void SpawnWildernessPanorama()
    {
        var panoramaGo = new GameObject("WildernessPanorama");
        panoramaGo.transform.position = new Vector3(512f, 90f, 512f);

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mountainMat = new Material(shader);
        mountainMat.color = new Color(0.18f, 0.22f, 0.35f); // 雄大な遠景の藍色シルエット

        // 全周12方向に連なる巨大な未知の山脈・稜線を配置
        for (int i = 0; i < 12; i++)
        {
            float ang = i * 30f * Mathf.Deg2Rad;
            float dist = 680f;
            Vector3 pos = new Vector3(Mathf.Cos(ang) * dist, Random.Range(10f, 40f), Mathf.Sin(ang) * dist);

            var peak = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            peak.name = $"WildernessRidge_{i}";
            peak.transform.SetParent(panoramaGo.transform, false);
            peak.transform.localPosition = pos;
            peak.transform.localScale = new Vector3(260f, Random.Range(85f, 150f), 260f);
            peak.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), i * 30f, Random.Range(-8f, 8f));

            var col = peak.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = peak.GetComponent<Renderer>();
            if (rend != null) rend.material = mountainMat;
        }

        // 天蓋の裂け目から差し込む金色の光芒（God Rays）
        var raysGo = new GameObject("SkybreakGodRays");
        raysGo.transform.SetParent(panoramaGo.transform, false);
        raysGo.transform.localPosition = new Vector3(0f, 60f, 0f);

        var rayShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        var rayMat = new Material(rayShader);
        rayMat.color = new Color(1.0f, 0.92f, 0.65f, 0.35f);

        for (int r = 0; r < 8; r++)
        {
            var ray = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ray.name = $"GodRay_{r}";
            ray.transform.SetParent(raysGo.transform, false);
            ray.transform.localScale = new Vector3(8f, 120f, 8f);
            ray.transform.localRotation = Quaternion.Euler(Random.Range(15f, 35f), r * 45f + 15f, 0f);

            var col = ray.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = ray.GetComponent<Renderer>();
            if (rend != null) rend.material = rayMat;
        }
    }

    /// <summary>オアシス湧水池（480, 455）からタワー台地（512, 512）へ登る白亜の古代神殿アプローチ階段道を生成</summary>
    void BuildTowerStairs(Transform parent, Terrain land)
    {
        var stairsRoot = new GameObject("SanctuaryApproachStairs");
        stairsRoot.transform.SetParent(parent, false);

        Vector3 startP = new Vector3(472f, 48.5f, 455f); // オアシス池のほとり
        Vector3 endP = new Vector3(512f, 62.5f, 512f);   // タワー基壇の入口
        if (land != null)
        {
            startP.y = land.SampleHeight(startP) + land.transform.position.y + 0.2f;
            endP.y = land.SampleHeight(endP) + land.transform.position.y + 0.2f;
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
