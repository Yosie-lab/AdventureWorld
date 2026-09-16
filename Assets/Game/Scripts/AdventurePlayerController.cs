using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class AdventurePlayerController : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────
    [Header("Walk / Run")]
    public float walkSpeed         = 4.2f;
    public float runSpeed          = 7.8f;
    public float turnSpeed         = 14f;

    [Header("Jump")]
    public float jumpHeight        = 2.2f;
    public float gravity           = -24f;
    public bool  canDoubleJump     = false;
    public float jumpMultiplier    = 1.0f;
    /// <summary>カピタ祝福によるスーパージャンプ解禁済み</summary>
    public bool  hasCapytaSuperJump = false;

    [Header("Glide")]
    public bool  canGlide          = true;
    public float glideEnterDelay   = 0.18f;
    /// <summary>滑空中の前進速度（最大）。パーツ12個収集でアップグレードされる。</summary>
    public float glideForwardSpeed = 7.2f;
    /// <summary>滑空中の自然落下速度。パーツ12個収集でアップグレードされる。</summary>
    public float glideFallSpeed    = -2.4f;

    [Header("Misc")]
    public Transform cameraPivot;
    public Vector3   spawnPosition;
    public float     moveSpeedMultiplier = 1.0f;
    public bool      hasPetRadar        = false;

    // ─── 内部状態 ──────────────────────────────────────────────────────
    bool    _doubleJumpUsed;
    bool    _gliding;
    float   _airborneTime;
    Vector3 _airMomentum;
    float   _glideBoostTimer;
    float   _glideBoostMultiplier = 1.0f;
    float   _updraftLift;
    float   _updraftTimer;
    /// <summary>光の柱上昇ロック中（範囲外・Space離しでも落下しない）</summary>
    bool    _skybreakPillarLock;
    bool    _skybreakPillarDone; // 一度解放したら再ロックしない（柱ゾーン滞在での永久ループ防止）
    float   _skybreakPillarLift = 36f;
    float   _skybreakPillarTargetY = 152f;
    Vector3 _skybreakPillarCenter = new Vector3(512f, 0f, 512f);
    float   _skybreakPillarEndAt;
    float   _skybreakLastY;
    float   _skybreakStuckTimer;

    CharacterController _cc;
    Animator            _anim;
    Terrain             _land;
    float               _hop;
    bool                _grounded = true;
    string              _clip;

    // ─── 定数 ─────────────────────────────────────────────────────────
    const float Skin                 = 0.1f;
    const float StepOffsetGround     = 1.35f;
    const float SteepNormalThreshold = 0.20f;

    // 湖脱出ジャンプ定数
    const float LakeHop          = 19.5f;
    const float LakeBoostDur     = 3.2f;
    static readonly Vector3 LakeCenter = new Vector3(135f, 18.15f, 166f);

    // 砂浜サーマルジャンプ定数
    const float BeachHop         = 22.0f;
    const float BeachBoostDur    = 4.2f;

    // 崖カタパルト定数
    const float CliffBoostDur    = 3.8f;
    const float CliffFwdSpeed    = 10.5f;

    // 滑空フライト定数
    const float GlideYawRate     = 145f;
    const float GlideYawRateBoosted = 110f;
    const float GlideBankAngle   = 22f;
    const float GlidePitchRateDive = 7.5f;

    // ─── 公開プロパティ ────────────────────────────────────────────────
    public static AdventurePlayerController Instance { get; private set; }
    public bool InteractPressed { get; private set; }
    public bool IsGliding    => _gliding;
    public bool IsGrounded   => _grounded;
    public bool IsInAir      => !_grounded;
    public bool IsBoostActive => _glideBoostTimer > 0f;
    public bool IsAutoGliding => _autoGlide;

    bool  _autoGlide;
    float _autoGlideAltitude = 120f;
    const float AutoGlideYawRate = 16f;
    const float AutoGlideCruiseSpeed = 7.2f;

    // ═══════════════════════════════════════════════════════════════════
    // Unity ライフサイクル
    // ═══════════════════════════════════════════════════════════════════

    void Awake()
    {
        Instance = this;
        InitInputSystem();
        InitCharacterController();
        InitAnimator();
        CacheTerrains();
    }

    void Start()
    {
        EnsureAllManagers();
        InitSpawnPosition();
    }

    void Update()
    {
        var kb = GetKeyboard();
        // F9/F10はオープニング中でも最優先（レバー検証用）
        if (kb != null && (kb.f9Key.wasPressedThisFrame || kb.f10Key.wasPressedThisFrame))
        {
            AdventureSanctuaryTowerManager.Ensure();
            AdventureSanctuaryTowerManager.Instance?.DebugJumpToCanopyOpening();
            return;
        }
        try
        {
            if (Input.GetKeyDown(KeyCode.F9) || Input.GetKeyDown(KeyCode.F10))
            {
                AdventureSanctuaryTowerManager.Ensure();
                AdventureSanctuaryTowerManager.Instance?.DebugJumpToCanopyOpening();
                return;
            }
        }
        catch { }

        // オープニングボード表示中は操作不可（進行中に誤表示された場合は強制閉じ）
        var opening = FindAnyObjectByType<AdventureRustFloatOpening>();
        if (opening != null && opening.IsModalBoardOpen())
        {
            var tower = AdventureSanctuaryTowerManager.Instance;
            bool inEnding = AdventureSanctuaryTowerManager.IsCanopyBroken
                            || AdventureSanctuaryTowerManager.IsGameCleared
                            || _skybreakPillarLock
                            || _autoGlide
                            || (tower != null && (tower.IsEpiloguePlaying || tower.ClimaxCrisisStarted || tower.ShowGameClearModal));
            if (inEnding)
                opening.ForceDismissForGameplay();
            else
                return;
        }

        ReadInputFlags(kb);

        if (TryHandleResetKey(kb)) return;
        TryHandleSaveKey(kb);

        // 天蓋台本表示中は位置を固定（テラスから落下して物語が途切れるのを防ぐ）
        var towerHold = AdventureSanctuaryTowerManager.Instance;
        if (towerHold != null
            && towerHold.IsSkybreakModalActive
            && !towerHold.ClimaxCrisisStarted
            && !_skybreakPillarLock
            && !_autoGlide)
        {
            HandleJump(kb); // Space送りのみ
            return;
        }

        Vector2 input   = ReadMove(kb);
        bool    running = IsRunning(kb);
        float   speed   = (running ? runSpeed : walkSpeed) * moveSpeedMultiplier;
        bool    spaceHeld = kb != null && kb.spaceKey.isPressed;
        try { if (Input.GetKey(KeyCode.Space)) spaceHeld = true; } catch { }
        bool    holdGlide = canGlide && spaceHeld;

        // 光の柱：落下防止を入力処理より先に毎フレーム確定
        TickSkybreakPillarLock();

        UpdateGroundedState();
        _cc.stepOffset = _grounded ? StepOffsetGround : 0f;

        HandleJump(kb);
        UpdateGlidingState(holdGlide || _skybreakPillarLock || _autoGlide);
        NotifyGlideStart();

        Vector3 horizontal = ComputeHorizontal(input, speed, running);

        ApplyMotion(horizontal);
        // 上昇ロック中は ApplyMotion 後にもう一度高度を保証（衝突で押し戻されても落ちない）
        if (_skybreakPillarLock)
            EnforceSkybreakPillarHeight();

        FloatOnWater();
        KeepWalkable();
        PlayLocomotion(_grounded ? horizontal.magnitude : 0f, running && _grounded);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 初期化ヘルパー
    // ═══════════════════════════════════════════════════════════════════

    void InitInputSystem()
    {
#if UNITY_EDITOR
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#endif
        if (Keyboard.current == null)
        {
            try { InputSystem.AddDevice<Keyboard>(); }
            catch (System.Exception) { }
        }
    }

    void InitCharacterController()
    {
        _cc = GetComponent<CharacterController>();
        _cc.slopeLimit     = 78f;           // 78度の急斜面もスムーズに駆け上がれる
        _cc.stepOffset     = StepOffsetGround;
        _cc.minMoveDistance = 0f;
    }

    void InitAnimator()
    {
        _anim = GetComponentInChildren<Animator>();
        if (_anim != null)
        {
            _anim.applyRootMotion = false;
            _anim.cullingMode     = AnimatorCullingMode.AlwaysAnimate;
        }
        // SkinnedMeshRenderer のバウンズが極小の場合を補正
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.quality             = SkinQuality.Bone4;
            smr.updateWhenOffscreen = true;
            var b = smr.localBounds;
            if (b.extents.x < 0.04f || b.extents.y < 0.04f || b.extents.z < 0.04f)
                smr.localBounds = new Bounds(Vector3.zero, Vector3.one * 0.12f);
        }
    }

    void EnsureAllManagers()
    {
        AdventureIslandBoundary.Ensure();
        AdventureBeachWavesManager.Ensure();
        AdventureCicadaAmbienceManager.Ensure();
        AdventureScrapManager.Ensure();
        AdventureScrapHUD.Ensure();
        AdventureFlightManager.Ensure();
        AdventureRustDrone.Ensure();
        AdventureBeachFlotsamManager.Ensure();
        AdventureLakeVisualEnhancer.Ensure();
        AdventureBeachEscapeManager.Ensure();
        AdventureBeachNarrativeManager.Ensure();
        AdventureSanctuaryTowerManager.Ensure();
        AdventureMusicDirector.Ensure();
        AdventureCloudDrift.EnsureCloudSystem();
        AdventureDayNightDirector.Ensure();
        AdventurePettingAction.Ensure(gameObject);
        AdventureSaveManager.Ensure();
        AdventurePrologueDrama.Ensure();
        AdventureCapytaBlessing.Ensure();

        if (GetComponent<AdventureNikoFootsteps>() == null)
            gameObject.AddComponent<AdventureNikoFootsteps>();

        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (scene != "RustAndFlat" && scene != "RustAndFloat")
            AdventureMarkerCleanup.RemoveFloatingWaterSurfaces();

        CacheTerrains();
    }

    void InitSpawnPosition()
    {
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        bool   isRustFloat = (scene == "RustAndFlat" || scene == "RustAndFloat");

        if (isRustFloat)
        {
            // 西側白砂ビーチ、座礁艇（152, 6.2, 275）のすぐ東側
            spawnPosition = new Vector3(158f, 6.5f, 275f);
        }
        else if (spawnPosition == Vector3.zero)
        {
            spawnPosition = transform.position;
        }

        spawnPosition = Stick(spawnPosition);
        Teleport(spawnPosition);

        // 初期向き：内陸（東・大草原・中央タワー方向）を向く
        if (isRustFloat)
            transform.rotation = Quaternion.Euler(0f, 75f, 0f);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Update 分割メソッド
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>インタラクトボタンの押下フラグを更新する</summary>
    void ReadInputFlags(Keyboard kb)
    {
        InteractPressed = kb != null && kb.eKey.wasPressedThisFrame;
        try { if (Input.GetKeyDown(KeyCode.E)) InteractPressed = true; } catch { }
        var pad = Gamepad.current;
        if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame))
            InteractPressed = true;
    }

    /// <summary>Rキー押下でスポーン地点へリセット。trueを返したらUpdateを早期リターン。</summary>
    bool TryHandleResetKey(Keyboard kb)
    {
        bool resetPressed = kb != null && kb.rKey.wasPressedThisFrame;
        try { if (Input.GetKeyDown(KeyCode.R)) resetPressed = true; } catch { }
        if (!resetPressed) return false;
        ForceGroundReset();
        Teleport(spawnPosition);
        return true;
    }

    /// <summary>K/F5キーで手動クイックセーブ</summary>
    void TryHandleSaveKey(Keyboard kb)
    {
        bool savePressed = kb != null && (kb.kKey.wasPressedThisFrame || kb.f5Key.wasPressedThisFrame);
        try { if (Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.F5)) savePressed = true; } catch { }
        if (savePressed)
            AdventureSaveManager.Instance?.SaveGame("SAVEしました");
    }

    static bool IsRunning(Keyboard kb)
    {
        if (kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)) return true;
        try { if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return true; } catch { }
        return false;
    }

    /// <summary>接地状態を更新する（ブーストタイマー考慮）</summary>
    void UpdateGroundedState()
    {
        if (_skybreakPillarLock)
        {
            _grounded = false;
            _gliding = true;
            _airborneTime = Mathf.Max(_airborneTime, 1f);
            return;
        }

        if (_autoGlide)
        {
            _grounded = false;
            _gliding = true;
            _airborneTime = Mathf.Max(_airborneTime, 1f);
            return;
        }

        if (_glideBoostTimer > 0f)
        {
            // ブースト中でも着地していればタイマーを即キャンセル（宙で止まるバグを防止）
            if (_cc.isGrounded && _hop <= 0.05f && !TooSteep() && !StandingOnSeafloor())
            {
                _glideBoostTimer  = 0f;
                _grounded         = true;
                _gliding          = false;
                _doubleJumpUsed   = false;
                _airborneTime     = 0f;
                if (_hop < 0f) _hop = -2f;
            }
            else
            {
                _grounded    = false;
                _gliding     = true;
                _airborneTime = Mathf.Max(_airborneTime, 1.0f);
            }
        }
        else if (Floating() || (_cc.isGrounded && _hop <= 0.05f && !TooSteep() && !StandingOnSeafloor()))
        {
            if (_hop < 0f) _hop = -2f;
            _grounded       = true;
            _doubleJumpUsed = false;
            _gliding        = false;
            _airborneTime   = 0f;
        }
        else
        {
            _grounded = false;
            _airborneTime += Time.deltaTime;
        }
    }

    /// <summary>ジャンプ処理（湖脱出・砂浜サーマル・崖カタパルト・通常・二段ジャンプ）</summary>
    void HandleJump(Keyboard kb)
    {
        // 天蓋レバー操作中／台本ボード表示中は Space をジャンプに使わず、台本送りへ回す
        var tower = AdventureSanctuaryTowerManager.Instance;
        if (tower != null)
        {
            if (tower.IsClimaxOilPromptActive)
            {
                bool held = kb != null && (kb.spaceKey.isPressed || kb.eKey.isPressed || kb.enterKey.isPressed);
                try
                {
                    if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Return))
                        held = true;
                }
                catch { }
                if (held)
                    tower.NotifyOilHold(Time.unscaledDeltaTime);
                return;
            }
            if (tower.IsSkybreakModalActive)
            {
                bool spaceDown = kb != null && kb.spaceKey.wasPressedThisFrame;
                bool spaceHeld = kb != null && kb.spaceKey.isPressed;
                try
                {
                    if (Input.GetKeyDown(KeyCode.Space)) spaceDown = true;
                    if (Input.GetKey(KeyCode.Space)) spaceHeld = true;
                }
                catch { }
                // 全台本：タップ or 押しっぱなしで送り
                if (spaceDown || spaceHeld)
                    tower.NotifyScriptBoardAdvance();
                return;
            }
            if (tower.IsPlayerNearLever && tower.IsLeverReadyToOpen) return;
        }

        bool jumpPressed = kb != null && kb.spaceKey.wasPressedThisFrame;
        try { if (Input.GetKeyDown(KeyCode.Space)) jumpPressed = true; } catch { }
        if (!jumpPressed) return;

        float effectiveJumpHeight = jumpHeight * jumpMultiplier;

        if (IsInLakeOrStreamBasin(transform.position))
        {
            LaunchBoostJump(
                hop:       LakeHop,
                boostDur:  LakeBoostDur,
                escapeDir: (transform.position - LakeCenter).SetY(0f),
                escapeSpd: 7.5f, fwdSpd: 4.5f, totalSpd: 9.5f,
                voice:     "ナイスジャンプ！風に乗って岸へ戻ろう、Niko！", voiceDur: 4.0f);
        }
        else if (IsInBeachOrCoastZone(transform.position))
        {
            Vector3 inwardDir = GetIslandCenterXZ() - transform.position.SetY(0f);
            LaunchBoostJump(
                hop:       BeachHop,
                boostDur:  BeachBoostDur,
                escapeDir: inwardDir,
                escapeSpd: 8.5f, fwdSpd: 3.5f, totalSpd: 11.5f,
                voice:     "海風の上昇気流をつかまえたよ！島の内陸へ飛んで帰ろう、Niko！", voiceDur: 4.5f);
        }
        else if (CheckCliffSurround(out float cliffHop, out Vector3 cliffDir))
        {
            LaunchBoostJump(
                hop:       cliffHop,
                boostDur:  CliffBoostDur,
                escapeDir: cliffDir,
                escapeSpd: 8.5f, fwdSpd: 4.5f, totalSpd: CliffFwdSpeed,
                voice:     "崖の上昇気流をつかまえたよ！一気に上へ登ろう、Niko！", voiceDur: 4.2f);
        }
        else if (_grounded)
        {
            _hop          = Mathf.Sqrt(effectiveJumpHeight * -2f * gravity);
            _grounded     = false;
            _airborneTime = 0f;
        }
        else if (canDoubleJump && !_doubleJumpUsed && !_gliding)
        {
            if (CheckCliffSurround(out float airCliffHop, out Vector3 airCliffDir))
            {
                _hop              = airCliffHop * 0.9f;
                _gliding          = true;
                _glideBoostTimer  = 3.2f;
                _airMomentum      = (airCliffDir * 8.0f + transform.forward * 4.0f).normalized * 9.5f;
            }
            else
            {
                _hop = Mathf.Sqrt(effectiveJumpHeight * -1.8f * gravity);
            }
            _doubleJumpUsed = true;
        }
    }

    /// <summary>ブーストジャンプの共通処理（湖・砂浜・崖で共有）</summary>
    void LaunchBoostJump(float hop, float boostDur,
                         Vector3 escapeDir, float escapeSpd, float fwdSpd, float totalSpd,
                         string voice, float voiceDur)
    {
        _hop              = hop;
        _grounded         = false;
        _gliding          = true;
        _airborneTime     = 1.0f;
        _glideBoostTimer  = boostDur;

        escapeDir.y = 0f;
        if (escapeDir.sqrMagnitude < 0.1f) escapeDir = transform.forward;
        escapeDir.Normalize();

        _airMomentum      = (escapeDir * escapeSpd + transform.forward * fwdSpd).normalized * totalSpd;
        transform.rotation = Quaternion.LookRotation(_airMomentum);

        GetDrone()?.SpeakCustom(voice, voiceDur);
    }

    /// <summary>滑空状態フラグを更新する</summary>
    void UpdateGlidingState(bool holdGlide)
    {
        if (_skybreakPillarLock || _autoGlide)
        {
            _gliding = true;
            _grounded = false;
            return;
        }

        if (_glideBoostTimer > 0f)
        {
            _gliding  = true;
            _grounded = false;
        }
        else
        {
            _gliding = !_grounded && holdGlide && _airborneTime >= glideEnterDelay;
        }
    }

    bool _wasGliding;
    /// <summary>滑空開始瞬間にRustへ通知する</summary>
    void NotifyGlideStart()
    {
        if (!_wasGliding && _gliding)
            GetDrone()?.OnGlideStarted();
        _wasGliding = _gliding;
    }

    /// <summary>入力ベクトルからフレームの水平移動量を算出する</summary>
    Vector3 ComputeHorizontal(Vector2 input, float speed, bool running)
    {
        // カメラ相対の入力方向を算出
        Vector3 camForward = transform.forward;
        Vector3 camRight   = transform.right;
        if (cameraPivot != null)
        {
            camForward = Vector3.ProjectOnPlane(cameraPivot.forward, Vector3.up);
            camRight   = Vector3.ProjectOnPlane(cameraPivot.right,   Vector3.up);
            if (camForward.sqrMagnitude < 0.001f) camForward = transform.forward;
            else camForward.Normalize();
            camRight.Normalize();
        }

        // ペット中は移動禁止
        if (AdventurePettingAction.Instance != null && AdventurePettingAction.Instance.IsPetting)
            input = Vector2.zero;

        Vector3 wishWalk = input.sqrMagnitude > 0.0001f
            ? Vector3.ClampMagnitude(camRight * input.x + camForward * input.y, 1f) * speed
            : Vector3.zero;

        if (_grounded)     return ComputeGroundHorizontal(wishWalk, running);
        if (_gliding)      return ComputeGlideHorizontal(input);
        return ComputeAirHorizontal();
    }

    /// <summary>地上移動量の計算（砂浜登坂アシスト・回転含む）</summary>
    Vector3 ComputeGroundHorizontal(Vector3 wishWalk, bool running)
    {
        Vector3 horizontal = wishWalk;
        _airMomentum = wishWalk;

        if (wishWalk.sqrMagnitude > 0.0001f)
        {
            // 砂浜から内陸方向への登坂アシスト
            if (IsInBeachOrCoastZone(transform.position))
            {
                Vector3 inward = GetIslandCenterXZ() - transform.position.SetY(0f);
                if (inward.sqrMagnitude > 0.1f)
                {
                    inward.Normalize();
                    if (Vector3.Dot(wishWalk.normalized, inward) > 0.1f)
                        horizontal += inward * (running ? 3.5f : 2.0f);
                }
            }
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(wishWalk),
                turnSpeed * Time.deltaTime);
        }
        else
        {
            // 静止時はピッチ・ロールを水平にリセット
            Vector3 euler = transform.eulerAngles;
            if (Mathf.Abs(Mathf.DeltaAngle(euler.x, 0f)) > 0.1f ||
                Mathf.Abs(Mathf.DeltaAngle(euler.z, 0f)) > 0.1f)
                transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
        }
        return horizontal;
    }

    /// <summary>滑空中の移動量計算（ヨー・バンク・ピッチ・サーマル）</summary>
    Vector3 ComputeGlideHorizontal(Vector2 input)
    {
        if (_autoGlide)
            return ComputeAutoGlideHorizontal();

        // ボーストタイマーを進める
        if (_glideBoostTimer > 0f)
            _glideBoostTimer -= Time.deltaTime;
        bool isBoosted = _glideBoostTimer > 0f;

        // ヨー（左右旋回）
        float yawRate    = isBoosted ? GlideYawRateBoosted : GlideYawRate;
        float currentYaw = transform.eulerAngles.y + input.x * yawRate * Time.deltaTime;

        // バンク（機体傾き）
        float targetRoll  = -input.x * GlideBankAngle;
        float currentRoll = Mathf.MoveTowardsAngle(transform.eulerAngles.z, targetRoll, 90f * Time.deltaTime);

        // ピッチ設定（W=ダイブ加速 / S=フレア滞空 / なし=巡航）
        float targetSpeed, targetFall, pitchAngle;
        if (input.y > 0.15f)        // W：ダイブ加速
        {
            targetSpeed = (isBoosted ? 15.0f : 11.5f) * moveSpeedMultiplier;
            targetFall  = isBoosted ? -0.8f : -2.8f;
            pitchAngle  = 8f;
        }
        else if (input.y < -0.15f) // S：フレア滞空
        {
            targetSpeed = (isBoosted ? 8.5f : 4.6f) * moveSpeedMultiplier;
            targetFall  = isBoosted ? 0.3f : -0.28f;
            pitchAngle  = -6f;
        }
        else                        // 通常巡航
        {
            targetSpeed = (isBoosted ? 11.0f : 7.4f) * moveSpeedMultiplier;
            targetFall  = isBoosted ? 0.2f : -1.15f;
            pitchAngle  = 0f;
        }

        // 機体姿勢を反映（ピッチ・ヨー・ロール）
        transform.rotation = Quaternion.Euler(pitchAngle, currentYaw, currentRoll);

        // 前進方向ベクトル（XZ 平面に投影）
        Vector3 forwardFlat = transform.forward.SetY(0f);
        if (forwardFlat.sqrMagnitude < 0.001f) forwardFlat = Vector3.forward;
        forwardFlat.Normalize();

        _airMomentum = Vector3.Lerp(_airMomentum, forwardFlat * targetSpeed, 6.5f * Time.deltaTime);

        // 上昇気流（サーマル）適用
        if (_skybreakPillarLock)
        {
            _hop = _skybreakPillarLift;
            return _airMomentum;
        }
        if (_updraftTimer > 0f)
        {
            _updraftTimer -= Time.deltaTime;
            targetFall = _updraftLift;
            // 強い上昇気流はゆっくり加速せず、即座に上昇速度へ合わせる（光の柱用）
            if (_updraftLift >= 12f)
            {
                _hop = Mathf.Max(_hop, _updraftLift);
                return _airMomentum;
            }
        }

        _hop = Mathf.MoveTowards(_hop, targetFall, GlidePitchRateDive * Time.deltaTime);
        return _airMomentum;
    }

    /// <summary>空中（非滑空）の移動量計算（慣性減衰＋重力）</summary>
    Vector3 ComputeAirHorizontal()
    {
        _airMomentum = Vector3.MoveTowards(_airMomentum, Vector3.zero, 2.2f * Time.deltaTime);
        _hop += gravity * Time.deltaTime;
        return _airMomentum;
    }

    void ApplyMotion(Vector3 horizontal)
    {
        Vector3 motion = ClipMotion(horizontal * Time.deltaTime);
        motion.y = _hop * Time.deltaTime;
        _cc.Move(motion);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 境界・水面制御
    // ═══════════════════════════════════════════════════════════════════

    Vector3 ClipMotion(Vector3 motion)
    {
        // 滑空中は島境界の見えない壁を完全バイパス
        if (_gliding || _glideBoostTimer > 0f) return motion;

        var bounds = AdventureIslandBoundary.Instance;
        return bounds != null ? bounds.ClipMotion(transform.position, motion) : motion;
    }

    void KeepWalkable()
    {
        // 滑空中は歩行エリア制限をバイパス
        if (_gliding || _glideBoostTimer > 0f) return;

        var bounds = AdventureIslandBoundary.Instance;
        if (bounds == null) return;

        Vector3 pos = transform.position;
        if (bounds.IsWalkable(pos)) return;

        Vector3 clamped = bounds.ClampWalkable(pos);
        if (!_grounded && _hop > 0f)
        {
            _cc.enabled = false;
            transform.position = new Vector3(clamped.x, pos.y, clamped.z);
            _cc.enabled = true;
        }
        else
        {
            clamped.y = SurfaceY(clamped) + Skin;
            Teleport(clamped);
        }
    }

    float WaterY()
    {
        var bounds = AdventureIslandBoundary.Instance;
        return bounds != null ? bounds.waterLevel : float.NegativeInfinity;
    }

    bool OverWater(Vector3 pos) => GroundY(pos) < WaterY() - 0.2f;
    bool Floating()             => OverWater(transform.position) && transform.position.y <= WaterY() + 0.45f;
    bool StandingOnSeafloor()   => OverWater(transform.position) && transform.position.y < WaterY() - 0.05f;

    bool TooSteep()
    {
        if (_land == null || _land.terrainData == null) return false;
        Vector3 origin = _land.transform.position;
        Vector3 size   = _land.terrainData.size;
        float   nx     = Mathf.Clamp01((transform.position.x - origin.x) / size.x);
        float   nz     = Mathf.Clamp01((transform.position.z - origin.z) / size.z);
        return _land.terrainData.GetInterpolatedNormal(nx, nz).y < SteepNormalThreshold;
    }

    void FloatOnWater()
    {
        Vector3 pos = transform.position;
        if (!OverWater(pos)) return;
        float surface = WaterY() + Skin;
        if (pos.y > surface) return;

        _cc.enabled = false;
        transform.position = new Vector3(pos.x, surface, pos.z);
        _cc.enabled = true;
        _hop          = Mathf.Max(_hop, 0f);
        _grounded     = true;
        _gliding      = false;
        _airborneTime = 0f;
    }

    float GroundY(Vector3 pos)
        => _land != null ? _land.SampleHeight(pos) + _land.transform.position.y : pos.y;

    float SurfaceY(Vector3 pos)
    {
        float landY = GroundY(pos);
        float water = WaterY();
        float baseY = landY < water ? water : landY;

        // 中央タワー白亜テラスは地形より高い固体床。地形Yへスナップすると台座に埋まる
        float terrace = AdventureSanctuaryTowerManager.GetTerraceSurfaceY(pos);
        if (terrace > float.NegativeInfinity)
            baseY = Mathf.Max(baseY, terrace);

        return baseY;
    }

    Vector3 Stick(Vector3 pos)
    {
        var bounds = AdventureIslandBoundary.Instance;
        if (bounds != null) pos = bounds.ClampWalkable(pos);
        pos.y = SurfaceY(pos) + Skin;
        return pos;
    }

    // ═══════════════════════════════════════════════════════════════════
    // テレポート / 状態リセット
    // ═══════════════════════════════════════════════════════════════════

    public void Teleport(Vector3 pos)
    {
        // エンディング中は地上Stickで高度を潰さない
        var tower = AdventureSanctuaryTowerManager.Instance;
        bool ending = _skybreakPillarLock || _autoGlide
                      || (tower != null && (tower.ClimaxCrisisStarted || tower.EpilogueTriggered
                                           || AdventureSanctuaryTowerManager.IsCanopyBroken));
        if (!ending)
            pos = Stick(pos);
        if (_cc != null) _cc.enabled = false;
        transform.position = pos;
        if (_cc != null) _cc.enabled = true;
        if (!ending)
            ForceGroundReset();
    }

    /// <summary>ブースト・滑空・ホップ状態を強制リセットして地上に着地させる（緊急回復）</summary>
    public void ForceGroundReset()
    {
        _skybreakPillarLock  = false;
        _skybreakPillarDone  = false;
        _skybreakStuckTimer  = 0f;
        _skybreakPillarEndAt = 0f;
        _hop                 = 0f;
        _grounded            = true;
        _gliding             = false;
        _autoGlide           = false;
        _glideBoostTimer     = 0f;
        _glideBoostMultiplier = 1.0f;
        _airborneTime        = 0f;
        _airMomentum         = Vector3.zero;
        _doubleJumpUsed      = false;
        _updraftTimer        = 0f;
        _updraftLift         = 0f;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 外部から呼ばれる効果付与
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>気流リングや風のレーンに乗った時の浮揚・推進</summary>
    public void ApplyGlideBoost(float boostMultiplier, float duration, Vector3 boostDirection = default)
    {
        _glideBoostMultiplier = Mathf.Max(_glideBoostMultiplier, boostMultiplier);
        _glideBoostTimer      = Mathf.Max(_glideBoostTimer, duration);
        _grounded             = false;
        _gliding              = true;
        _airborneTime         = 1.0f;
        _hop                  = 3.6f;

        Vector3 dir = boostDirection.sqrMagnitude > 0.01f ? boostDirection : transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
        dir.Normalize();

        _airMomentum       = dir * (10.5f * moveSpeedMultiplier);
        transform.rotation = Quaternion.LookRotation(dir);
    }

    /// <summary>上昇気流（サーマル）突入時の浮遊・上昇力付与</summary>
    public void ApplyUpdraft(float liftForce)
    {
        _updraftLift  = liftForce;
        _updraftTimer = 0.25f;
        if (!_grounded) _gliding = true;
    }

    /// <summary>エピローグ用：高度を保ちながら雄大に水平旋回するオートグライド</summary>
    Vector3 ComputeAutoGlideHorizontal()
    {
        float currentYaw = transform.eulerAngles.y + AutoGlideYawRate * Time.deltaTime;
        transform.rotation = Quaternion.Euler(2f, currentYaw, -8f);

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        forward.Normalize();

        float cruise = AutoGlideCruiseSpeed * moveSpeedMultiplier;
        _airMomentum = Vector3.MoveTowards(_airMomentum, forward * cruise, 6f * Time.deltaTime);

        float dy = _autoGlideAltitude - transform.position.y;
        float targetFall = Mathf.Clamp(dy * 2.8f, -1.2f, 3.8f);
        _hop = Mathf.MoveTowards(_hop, targetFall, 10f * Time.deltaTime);
        return _airMomentum;
    }

    /// <summary>エピローグ中のオートグライド（指定高度付近で水平旋回）。手を離しても墜落しない</summary>
    public void SetAutoGlideMode(bool enabled, float altitude = 120f)
    {
        _autoGlide = enabled;
        _autoGlideAltitude = Mathf.Clamp(altitude, 115f, 165f);
        if (!enabled)
            return;

        _skybreakPillarLock = false;
        _skybreakPillarDone = true;
        _grounded = false;
        _gliding = true;
        _airborneTime = 1f;
        _glideBoostTimer = Mathf.Max(_glideBoostTimer, 2f);
        if (transform.position.y < _autoGlideAltitude - 8f)
            _hop = Mathf.Max(_hop, 10f);
        Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.01f)
            fwd = Vector3.forward;
        _airMomentum = fwd.normalized * AutoGlideCruiseSpeed;
    }

    /// <summary>天蓋破壊後のハイパーサーマル等で、地上歩行からでも自動的に大空へ射出・滑空開始させる強力な打ち上げ</summary>
    public void ApplyLaunchUpdraft(float liftSpeed, float initialHop = 22f)
    {
        // 光の柱級の上昇中は高度ロック・オートグライドを切る（途中で止まる主因）
        if (liftSpeed >= 18f)
            _autoGlide = false;

        _grounded        = false;
        _gliding         = true;
        _airborneTime    = 1.0f;
        _glideBoostTimer = Mathf.Max(_glideBoostTimer, 4.5f);
        float hopTarget = Mathf.Max(initialHop, liftSpeed);
        if (_hop < hopTarget) _hop = hopTarget;
        _updraftLift     = liftSpeed;
        _updraftTimer    = 0.85f;
    }

    /// <summary>ダイブ完了時：物理上昇を待たず空中高度へ固定しオートグライド開始</summary>
    public void ForceSkybreakArrival(Vector3 worldPos, float glideAltitude)
    {
        _skybreakPillarLock = false;
        _skybreakPillarDone = true;
        _skybreakStuckTimer = 0f;
        _skybreakPillarEndAt = 0f;

        if (_cc == null) _cc = GetComponent<CharacterController>();
        if (_cc != null) _cc.enabled = false;
        transform.position = worldPos;
        if (_cc != null) _cc.enabled = true;

        _grounded = false;
        _gliding = true;
        _hop = 2f;
        _airborneTime = 1f;
        _glideBoostTimer = Mathf.Max(_glideBoostTimer, 4f);
        SetAutoGlideMode(true, glideAltitude);
    }

    /// <summary>天蓋台本完了後の柱上昇を確実に開始できるよう、前回の完了フラグをクリア</summary>
    public void PrepareSkybreakPillarAscend()
    {
        _skybreakPillarDone = false;
        _skybreakPillarLock = false;
        _skybreakPillarEndAt = 0f;
        _skybreakStuckTimer = 0f;
        _autoGlide = false;
    }

    /// <summary>光の柱上昇を開始（クライマックス開始まで維持。解放時は高度確保＋物語継続）</summary>
    public void BeginSkybreakPillarAscend(Vector3 pillarCenter, float liftSpeed, float releaseY = 150f)
    {
        // クライマックス開始後のみ再ロックしない（台本ボード表示中の IsEpiloguePlaying では弾かない）
        if (_autoGlide) return;
        var tower = AdventureSanctuaryTowerManager.Instance;
        if (tower != null && (tower.ClimaxCrisisStarted || tower.ShowGameClearModal || tower.EpilogueTriggered))
            return;

        _skybreakPillarDone = false;
        _skybreakPillarLock = true;
        _skybreakPillarLift = Mathf.Clamp(liftSpeed, 24f, 42f);
        _skybreakPillarTargetY = Mathf.Clamp(releaseY, 148f, 155f);
        _skybreakPillarCenter = new Vector3(pillarCenter.x, 0f, pillarCenter.z);
        _skybreakPillarEndAt = Time.unscaledTime + 6.5f;
        _skybreakStuckTimer = 0f;
        _skybreakLastY = transform.position.y;
        _autoGlide = false;
        ApplyLaunchUpdraft(_skybreakPillarLift, _skybreakPillarLift);

        if (_cc == null) _cc = GetComponent<CharacterController>();
        if (_cc != null) _cc.enabled = false;
        Vector3 p = transform.position;
        transform.position = new Vector3(pillarCenter.x, Mathf.Max(p.y, 64f), pillarCenter.z);
        if (_cc != null) _cc.enabled = true;
    }

    public void EndSkybreakPillarAscend()
    {
        _skybreakPillarLock = false;
        _skybreakPillarDone = true;
        _skybreakStuckTimer = 0f;
    }

    public bool IsSkybreakPillarAscending => _skybreakPillarLock;

    void ReleaseSkybreakPillarLock(bool snapToTarget)
    {
        // 必ず目標高度へ載せ、自由落下させない
        float y = Mathf.Max(transform.position.y, _skybreakPillarTargetY);
        if (snapToTarget || transform.position.y < _skybreakPillarTargetY - 0.25f)
            y = _skybreakPillarTargetY;

        if (_cc == null) _cc = GetComponent<CharacterController>();
        if (_cc != null) _cc.enabled = false;
        transform.position = new Vector3(_skybreakPillarCenter.x, y, _skybreakPillarCenter.z);
        if (_cc != null) _cc.enabled = true;

        _skybreakPillarLock = false;
        _gliding = true;
        _grounded = false;
        _hop = 4f;
        _updraftTimer = 1f;
        _updraftLift = 2f;

        SetAutoGlideMode(true, Mathf.Clamp(y, 148f, 160f));
        AdventureSanctuaryTowerManager.Instance?.NotifyPillarAscendComplete();
    }

    void TickSkybreakPillarLock()
    {
        if (!_skybreakPillarLock) return;

        bool timedOut = Time.unscaledTime >= _skybreakPillarEndAt;
        bool highEnough = transform.position.y >= _skybreakPillarTargetY;

        if (timedOut || highEnough)
        {
            ReleaseSkybreakPillarLock(snapToTarget: true);
            return;
        }

        _autoGlide = false;
        _grounded = false;
        _gliding = true;
        _airborneTime = 1f;
        _glideBoostTimer = Mathf.Max(_glideBoostTimer, 2f);
        _updraftLift = _skybreakPillarLift;
        _updraftTimer = 1f;
        _hop = _skybreakPillarLift;
    }

    void EnforceSkybreakPillarHeight()
    {
        if (!_skybreakPillarLock) return;
        if (_cc == null) _cc = GetComponent<CharacterController>();

        Vector3 pos = transform.position;
        float nextY = pos.y + _skybreakPillarLift * Time.deltaTime;
        nextY = Mathf.Min(nextY, _skybreakPillarTargetY + 0.25f);

        if (_cc != null) _cc.enabled = false;
        transform.position = new Vector3(_skybreakPillarCenter.x, nextY, _skybreakPillarCenter.z);
        if (_cc != null) _cc.enabled = true;

        _hop = _skybreakPillarLift;
        _grounded = false;
        _gliding = true;
        _skybreakLastY = nextY;
    }

    /// <summary>光の柱ゾーン内にいる間の補助。クライマックス後は再ロックしない。</summary>
    public void ForceSkybreakPillarAscend(Vector3 pillarBase, float liftSpeed, bool pullToCenter)
    {
        if (_autoGlide) return;
        var tower = AdventureSanctuaryTowerManager.Instance;
        if (tower != null && (tower.ClimaxCrisisStarted || tower.ShowGameClearModal || tower.EpilogueTriggered))
            return;

        if (!_skybreakPillarLock)
        {
            BeginSkybreakPillarAscend(pillarBase, liftSpeed, 150f);
            return;
        }

        _skybreakPillarLift = Mathf.Max(_skybreakPillarLift, Mathf.Min(liftSpeed, 42f));
        _skybreakPillarCenter = new Vector3(pillarBase.x, 0f, pillarBase.z);
    }


    // ═══════════════════════════════════════════════════════════════════
    // ゾーン判定ヘルパー
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>オアシス池・渓流のすり鉢窪地にいるか判定</summary>
    public bool IsInLakeOrStreamBasin(Vector3 pos)
    {
        float dist2Lake = new Vector2(pos.x - 135f, pos.z - 166f).magnitude;
        bool  nearLake  = dist2Lake < 32f && pos.y <= 21.5f;
        bool  inStream  = pos.x >= 143f && pos.x <= 163f && pos.z >= 156f && pos.z <= 172f && pos.y <= 26.0f;
        return nearLake || inStream;
    }

    /// <summary>外周砂浜・海岸線にいるか判定</summary>
    public bool IsInBeachOrCoastZone(Vector3 pos)
    {
        float waterY = WaterY();
        if (float.IsNegativeInfinity(waterY)) waterY = 5.5f;

        Vector3 center       = GetIslandCenterXZ();
        float   distToCenter = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(center.x, center.z));
        float   islandRadius = _land != null && _land.terrainData != null ? _land.terrainData.size.x * 0.5f : 512f;

        // 島外周エリア（半径62%以遠）かつ低地
        if (distToCenter >= islandRadius * 0.62f && pos.y <= waterY + 9.5f) return true;
        // 海水・波打ち際付近
        if (pos.y <= waterY + 3.0f) return true;
        return false;
    }

    /// <summary>島の中央座標（XZ 平面）を取得</summary>
    public Vector3 GetIslandCenterXZ()
    {
        if (_land != null && _land.terrainData != null)
        {
            Vector3 origin = _land.transform.position;
            Vector3 size   = _land.terrainData.size;
            return new Vector3(origin.x + size.x * 0.5f, 0f, origin.z + size.z * 0.5f);
        }
        return new Vector3(512f, 0f, 512f);
    }

    /// <summary>
    /// 周囲の崖を動的に判定し、崖の上へ飛び乗るための上昇力と推進方向を算出する
    /// </summary>
    public bool CheckCliffSurround(out float requiredHop, out Vector3 escapeDirection)
    {
        requiredHop    = 0f;
        escapeDirection = transform.forward;

        if (_land == null) return false;

        Vector3 myPos  = transform.position;
        float   curY   = myPos.y;
        float   maxH   = curY;
        Vector3 highDir = transform.forward;
        int     higherCount = 0;

        // 8方向サンプル
        Vector3[] dirs =
        {
            transform.forward,
            (transform.forward + transform.right).normalized,
            transform.right,
            (-transform.forward + transform.right).normalized,
            -transform.forward,
            (-transform.forward - transform.right).normalized,
            -transform.right,
            (transform.forward - transform.right).normalized,
        };
        float[] dists = { 3.5f, 7.5f, 14.0f };

        for (int d = 0; d < dirs.Length; d++)
        {
            bool dirIsHigher = false;
            for (int s = 0; s < dists.Length; s++)
            {
                float   dist     = dists[s];
                Vector3 checkPos = myPos + dirs[d] * dist;
                float   h        = _land.SampleHeight(checkPos) + _land.transform.position.y;
                if (h > curY + 2.4f)
                {
                    dirIsHigher = true;
                    if (h > maxH) { maxH = h; highDir = dirs[d]; }
                }
            }
            if (dirIsHigher) higherCount++;
        }

        // 正面の急斜面判定（Raycast）
        bool facingCliff = false;
        if (Physics.Raycast(myPos + Vector3.up * 0.8f, transform.forward, out RaycastHit hit, 5.0f))
        {
            if (!hit.collider.isTrigger && Vector3.Dot(hit.normal, Vector3.up) < 0.65f)
                facingCliff = true;
        }

        float heightDiff = maxH - curY;
        if (higherCount < 3 && !facingCliff && heightDiff < 2.8f) return false;

        // 崖の頂上を余裕で飛び越える跳躍初速を物理計算
        float targetClear = Mathf.Clamp(Mathf.Max(heightDiff, 5.5f) + 5.5f, 10.0f, 32.0f);
        requiredHop = Mathf.Sqrt(targetClear * -2f * gravity);

        if (facingCliff || Vector3.Dot(transform.forward, highDir) > 0.1f)
            escapeDirection = transform.forward;
        else if (higherCount >= 6)
            escapeDirection = transform.forward;
        else
            escapeDirection = (transform.forward * 0.7f + highDir * 0.3f).normalized;

        return true;
    }

    // ═══════════════════════════════════════════════════════════════════
    // アニメーション
    // ═══════════════════════════════════════════════════════════════════

    void PlayLocomotion(float speed, bool running)
    {
        if (_anim == null) return;
        string next = speed < 0.2f ? "NikoIdle" : (running ? "NikoRuns" : "NikoWalks");
        if (next != _clip)
        {
            _clip = next;
            _anim.CrossFadeInFixedTime(next, 0.15f);
        }
        _anim.speed = next == "NikoIdle"
            ? 1f
            : Mathf.Clamp(speed / (running ? 5.4f : 2.4f), 0.9f, 1.7f);
    }

    // ═══════════════════════════════════════════════════════════════════
    // キャッシュ / ユーティリティ
    // ═══════════════════════════════════════════════════════════════════

    void CacheTerrains()
    {
        foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
        {
            if (terrain.name == "LandTerrain" || terrain.name == "IslandTerrain")
                _land = terrain;
            else if (terrain.name.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var col = terrain.GetComponent<TerrainCollider>();
                if (col != null) col.enabled = false;
            }
        }
    }

    static Keyboard GetKeyboard()
    {
        var kb = Keyboard.current;
        if (kb != null) return kb;
        foreach (var device in InputSystem.devices)
            if (device is Keyboard found) return found;
        return null;
    }

    static Vector2 ReadMove(Keyboard kb)
    {
        Vector2 input = Vector2.zero;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    input.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  input.y -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  input.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
        }
        // レガシーInputのフォールバック（エディタフォーカス外れ等のフェイルセーフ）
        try
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(h) > 0.1f) input.x += h;
            if (Mathf.Abs(v) > 0.1f) input.y += v;
        }
        catch { }

        if (input.sqrMagnitude < 0.001f)
        {
            var gp = Gamepad.current;
            if (gp != null)
            {
                Vector2 stick = gp.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.04f) input = stick;
                if (gp.dpad.up.isPressed)    input.y += 1f;
                if (gp.dpad.down.isPressed)  input.y -= 1f;
                if (gp.dpad.left.isPressed)  input.x -= 1f;
                if (gp.dpad.right.isPressed) input.x += 1f;
            }
        }
        return Vector2.ClampMagnitude(input, 1f);
    }

    /// <summary>AdventureRustDrone のシングルトンを取得（キャッシュなし）</summary>
    static AdventureRustDrone GetDrone()
        => AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
}

// ─── Vector3 拡張：Y 成分セット（コードを簡潔にするためファイル末尾に定義）──
internal static class Vector3Ext
{
    /// <summary>Y成分を置き換えた新しいVector3を返す</summary>
    public static Vector3 SetY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
}
