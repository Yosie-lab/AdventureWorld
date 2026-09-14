using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class AdventurePlayerController : MonoBehaviour
{
    public float walkSpeed = 4.2f;
    public float runSpeed = 7.8f;
    public float jumpHeight = 2.2f;
    public float gravity = -24f;
    public float turnSpeed = 14f;
    public Transform cameraPivot;
    public Vector3 spawnPosition;

    public bool canDoubleJump = false;
    public float moveSpeedMultiplier = 1.0f;
    public float jumpMultiplier = 1.0f;
    public bool hasPetRadar = false;

    [Header("Glide")]
    public bool canGlide = true;
    public float glideFallSpeed = -2.4f;
    public float glideForwardSpeed = 7.2f;
    public float glideTurnSpeed = 5.5f;
    public float glideEnterDelay = 0.18f;

    bool _doubleJumpUsed = false;
    bool _gliding;
    float _airborneTime;
    Vector3 _airMomentum;
    float _glideBoostTimer;
    float _glideBoostMultiplier = 1.0f;
    float _updraftLift;
    float _updraftTimer;

    const float Skin = 0.1f;

    CharacterController _cc;
    Animator _anim;
    Terrain _land;
    float _hop;
    bool _grounded = true;
    string _clip;

    public static AdventurePlayerController Instance { get; private set; }
    public bool InteractPressed { get; private set; }
    public bool IsGliding => _gliding;
    public bool IsGrounded => _grounded;
    public bool IsBoostActive => _glideBoostTimer > 0f;

    void Awake()
    {
        Instance = this;
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

        _cc = GetComponent<CharacterController>();
        _cc.slopeLimit = 78f; // 78度の急斜面もスムーズに駆け上がれる
        _cc.stepOffset = 1.35f; // 1.35mの岩段差や砂浜のへり・段差もスムーズに乗り越えられる
        _cc.minMoveDistance = 0f;
        _anim = GetComponentInChildren<Animator>();
        if (_anim != null)
        {
            _anim.applyRootMotion = false;
            _anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.quality = SkinQuality.Bone4;
            smr.updateWhenOffscreen = true;
            var b = smr.localBounds;
            if (b.extents.x < 0.04f || b.extents.y < 0.04f || b.extents.z < 0.04f)
                smr.localBounds = new Bounds(Vector3.zero, Vector3.one * 0.12f);
        }
        CacheTerrains();
    }

    void Start()
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
        AdventureSanctuaryTowerManager.Ensure();
        AdventureMusicDirector.Ensure();
        AdventureCloudDrift.EnsureCloudSystem();
        AdventureDayNightDirector.Ensure();
        AdventurePettingAction.Ensure(gameObject);
        AdventureSaveManager.Ensure();
        if (GetComponent<AdventureNikoFootsteps>() == null)
            gameObject.AddComponent<AdventureNikoFootsteps>();
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (scene != "RustAndFlat" && scene != "RustAndFloat")
            AdventureMarkerCleanup.RemoveFloatingWaterSurfaces();
        CacheTerrains();
        if (spawnPosition == Vector3.zero)
            spawnPosition = transform.position;
        spawnPosition = Stick(spawnPosition);
        Teleport(spawnPosition);
    }

    void Update()
    {
        // スタート前（時代背景説明ボード表示中）は待機
        if (!AdventureRustFloatOpening.IsGameStarted && FindAnyObjectByType<AdventureRustFloatOpening>() != null)
            return;

        var kb = GetKeyboard();
        InteractPressed = kb != null && kb.eKey.wasPressedThisFrame;
        if (kb != null && kb.rKey.wasPressedThisFrame)
        {
            Teleport(spawnPosition);
            return;
        }

        // 【K】キーまたは【F5】キーで手動クイックセーブ
        bool savePressed = (kb != null && (kb.kKey.wasPressedThisFrame || kb.f5Key.wasPressedThisFrame));
        try { if (Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.F5)) savePressed = true; } catch { }
        if (savePressed)
        {
            AdventureSaveManager.Instance?.SaveGame("SAVEしました");
        }

        Vector2 input = ReadMove(kb);
        if (AdventurePettingAction.Instance != null && AdventurePettingAction.Instance.IsPetting)
        {
            input = Vector2.zero;
        }
        bool running = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
        float speed = (running ? runSpeed : walkSpeed) * moveSpeedMultiplier;
        bool holdGlide = canGlide && kb != null && kb.spaceKey.isPressed;

        // ブースト中は無条件で大空へ飛び上がり、接地判定や滑空ディレイを完全バイパス
        if (_glideBoostTimer > 0f)
        {
            _grounded = false;
            _gliding = true;
            _airborneTime = Mathf.Max(_airborneTime, 1.0f);
        }
        else if (Floating() || (_cc.isGrounded && _hop <= 0.05f && !TooSteep() && !StandingOnSeafloor()))
        {
            if (_hop < 0f)
                _hop = -2f;
            _grounded = true;
            _doubleJumpUsed = false;
            _gliding = false;
            _airborneTime = 0f;
        }
        else
        {
            _grounded = false;
            _airborneTime += Time.deltaTime;
        }

        // 空中浮遊時や滑空中に岩や急斜面をかすめた際の段差乗り上げ誤爆（垂直スナップ・ガクつき）を完全防止
        if (_cc != null)
        {
            _cc.stepOffset = _grounded ? 1.35f : 0f;
        }

        float effectiveJumpHeight = jumpHeight * jumpMultiplier;
        if (kb != null && kb.spaceKey.wasPressedThisFrame)
        {
            // 池・渓流のすり鉢窪地（水底・水面）からの超強力な脱出ローンチジャンプ！
            if (IsInLakeOrStreamBasin(transform.position))
            {
                _hop = 19.5f; // 重力-24fに対して高低差8m（岸の上）を軽々と超える大跳躍！
                _grounded = false;
                _gliding = true;
                _airborneTime = 1.0f;
                _glideBoostTimer = 3.2f; // 滑空ブーストで前進力も付与

                // 池の中心から外側（岸の方向）へ脱出推進
                Vector3 escapeDir = transform.position - new Vector3(135f, 18.15f, 166f);
                escapeDir.y = 0f;
                if (escapeDir.sqrMagnitude < 0.1f) escapeDir = transform.forward;
                escapeDir.Normalize();

                _airMomentum = (escapeDir * 7.5f + transform.forward * 4.5f).normalized * 9.5f;
                transform.rotation = Quaternion.LookRotation(_airMomentum);

                // 相棒Rustの歓喜ボイス
                var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
                if (drone != null)
                {
                    drone.SpeakCustom("ナイスジャンプ！風に乗って岸へ戻ろう、Niko！", 4.0f);
                }
            }
            // 外周砂浜・海岸からの超強力な「海風サーマル・ウインドジャンプ」！
            else if (IsInBeachOrCoastZone(transform.position))
            {
                _hop = 22.0f; // 海抜5.5mから一気に20m〜25mの島の上空へ舞い上がる大跳躍！
                _grounded = false;
                _gliding = true;
                _airborneTime = 1.0f;
                _glideBoostTimer = 4.2f; // 4秒間の滑空ブーストで島の内陸へロングクルーズ

                // 砂浜から島の内陸中心へ向かう推進ベクトル
                Vector3 center = GetIslandCenterXZ();
                Vector3 inwardDir = center - new Vector3(transform.position.x, 0f, transform.position.z);
                inwardDir.y = 0f;
                if (inwardDir.sqrMagnitude < 0.1f) inwardDir = transform.forward;
                inwardDir.Normalize();

                _airMomentum = (inwardDir * 8.5f + transform.forward * 3.5f).normalized * 11.5f;
                transform.rotation = Quaternion.LookRotation(_airMomentum);

                // 相棒Rustの誘導ボイス
                var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
                if (drone != null)
                {
                    drone.SpeakCustom("海風の上昇気流をつかまえたよ！島の内陸へ飛んで帰ろう、Niko！", 4.5f);
                }
            }
            // 崖に囲まれた場所・すり鉢窪地・切り立った崖下からの「クリフ・カタパルト大跳躍」！
            else if (CheckCliffSurround(out float cliffHop, out Vector3 cliffEscapeDir))
            {
                _hop = cliffHop;
                _grounded = false;
                _gliding = true;
                _airborneTime = 1.0f;
                _glideBoostTimer = 3.8f; // 崖の上へ飛び乗るための前進ブースト

                // 崖の上・前進方向への推進ベクトル
                float fwdSpeed = 10.5f;
                _airMomentum = (cliffEscapeDir * 8.5f + transform.forward * 4.5f).normalized * fwdSpeed;
                transform.rotation = Quaternion.LookRotation(_airMomentum);

                // 相棒Rustの誘導ボイス
                var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
                if (drone != null)
                {
                    drone.SpeakCustom("崖の上昇気流をつかまえたよ！一気に上へ登ろう、Niko！", 4.2f);
                }
            }
            else if (_grounded)
            {
                _hop = Mathf.Sqrt(effectiveJumpHeight * -2f * gravity);
                _grounded = false;
                _airborneTime = 0f;
            }
            else if (canDoubleJump && !_doubleJumpUsed && !_gliding)
            {
                if (CheckCliffSurround(out float airCliffHop, out Vector3 airCliffEscapeDir))
                {
                    _hop = airCliffHop * 0.9f;
                    _gliding = true;
                    _glideBoostTimer = 3.2f;
                    _airMomentum = (airCliffEscapeDir * 8.0f + transform.forward * 4.0f).normalized * 9.5f;
                }
                else
                {
                    _hop = Mathf.Sqrt(effectiveJumpHeight * -1.8f * gravity);
                }
                _doubleJumpUsed = true;
            }
        }

        bool wasGliding = _gliding;
        if (_glideBoostTimer > 0f)
        {
            _gliding = true;
            _grounded = false;
        }
        else
        {
            _gliding = !_grounded && holdGlide && _airborneTime >= glideEnterDelay;
        }

        // 滑空に入った瞬間に相棒Rustが穏やかに語りかける
        if (!wasGliding && _gliding)
        {
            var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
            if (drone != null)
            {
                drone.OnGlideStarted();
            }
        }

        Vector3 camForward = transform.forward;
        Vector3 camRight = transform.right;
        if (cameraPivot != null)
        {
            camForward = Vector3.ProjectOnPlane(cameraPivot.forward, Vector3.up);
            camRight = Vector3.ProjectOnPlane(cameraPivot.right, Vector3.up);
            if (camForward.sqrMagnitude < 0.001f)
                camForward = transform.forward;
            else
                camForward.Normalize();
            camRight.Normalize();
        }

        Vector3 wishWalk = Vector3.zero;
        if (input.sqrMagnitude > 0.0001f)
            wishWalk = Vector3.ClampMagnitude(camRight * input.x + camForward * input.y, 1f) * speed;

        Vector3 horizontal;
        if (_grounded)
        {
            horizontal = wishWalk;
            _airMomentum = wishWalk;
            if (wishWalk.sqrMagnitude > 0.0001f)
            {
                // 砂浜や低地から内陸へ向かって歩いている時、斜面をスルスル登れる強力な登坂アシスト
                if (IsInBeachOrCoastZone(transform.position))
                {
                    Vector3 center = GetIslandCenterXZ();
                    Vector3 inward = center - new Vector3(transform.position.x, 0f, transform.position.z);
                    inward.y = 0f;
                    if (inward.sqrMagnitude > 0.1f)
                    {
                        inward.Normalize();
                        if (Vector3.Dot(wishWalk.normalized, inward) > 0.1f)
                        {
                            horizontal += inward * (running ? 3.5f : 2.0f); // 内陸への登坂を力強くサポート
                        }
                    }
                }

                Quaternion targetRot = Quaternion.LookRotation(wishWalk);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }
            else
            {
                // 地上静止時はピッチ・ロールを水平にリセット
                Vector3 euler = transform.eulerAngles;
                if (Mathf.Abs(Mathf.DeltaAngle(euler.x, 0f)) > 0.1f || Mathf.Abs(Mathf.DeltaAngle(euler.z, 0f)) > 0.1f)
                {
                    transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
                }
            }
        }
        else if (_gliding)
        {
            // === 『A Short Hike』スタイルの心地よい滑空操作 ===
            if (_glideBoostTimer > 0f)
                _glideBoostTimer -= Time.deltaTime;

            bool isBoosted = _glideBoostTimer > 0f;

            // 1. A/Dキーによるダイレクトな機首旋回（大空を自由に飛び回る！）
            float turnRate = isBoosted ? 110f : 145f; // 毎秒145度でクイック旋回
            float currentYaw = transform.eulerAngles.y;
            currentYaw += input.x * turnRate * Time.deltaTime;

            // 左右旋回時の機体バンク（傾き）表現（-22°〜+22°）
            float targetRoll = -input.x * 22f;
            float currentRoll = Mathf.MoveTowardsAngle(transform.eulerAngles.z, targetRoll, 90f * Time.deltaTime);

            // 2. W/Sキーによるピッチ（ダイブ加速／滞空ブレーキ）制御
            // Wキー: 急降下ダイブ（高速加速＆鋭い降下）
            // Sキー: フレア滞空（ブレーキ＆ふわりと浮き上がり長時間滑空）
            float targetSpeed;
            float targetFall;
            float pitchAngle;

            if (input.y > 0.15f) // Wキー：ダイブ加速！
            {
                targetSpeed = (isBoosted ? 15.0f : 11.5f) * moveSpeedMultiplier;
                targetFall = isBoosted ? -0.8f : -2.8f;
                pitchAngle = 8f; // 機首下げ
            }
            else if (input.y < -0.15f) // Sキー：滞空フレア！
            {
                targetSpeed = (isBoosted ? 8.5f : 4.6f) * moveSpeedMultiplier;
                targetFall = isBoosted ? 0.3f : -0.28f; // ふわりとほとんど落ちない
                pitchAngle = -6f; // 機首上げ
            }
            else // 通常巡航：心地よい浮遊速度
            {
                targetSpeed = (isBoosted ? 11.0f : 7.4f) * moveSpeedMultiplier;
                targetFall = isBoosted ? 0.2f : -1.15f;
                pitchAngle = 0f;
            }

            // 機体の姿勢を反映（ピッチ・ヨー・ロール）
            transform.rotation = Quaternion.Euler(pitchAngle, currentYaw, currentRoll);

            // 3. 機首方向へ前進！
            Vector3 forwardFlat = transform.forward;
            forwardFlat.y = 0f;
            if (forwardFlat.sqrMagnitude < 0.001f) forwardFlat = Vector3.forward;
            forwardFlat.Normalize();

            Vector3 wishGlide = forwardFlat * targetSpeed;
            _airMomentum = Vector3.Lerp(_airMomentum, wishGlide, 6.5f * Time.deltaTime);

            // 上昇気流（サーマル）
            if (_updraftTimer > 0f)
            {
                _updraftTimer -= Time.deltaTime;
                targetFall = _updraftLift;
            }

            _hop = Mathf.MoveTowards(_hop, targetFall, 7.5f * Time.deltaTime);
            horizontal = _airMomentum;
        }
        else
        {
            _airMomentum = Vector3.MoveTowards(_airMomentum, Vector3.zero, 2.2f * Time.deltaTime);
            horizontal = _airMomentum;
            _hop += gravity * Time.deltaTime;
        }

        Vector3 motion = horizontal * Time.deltaTime;
        motion = ClipMotion(motion);
        motion.y = _hop * Time.deltaTime;
        _cc.Move(motion);
        FloatOnWater();
        KeepWalkable();
        PlayLocomotion(_grounded ? horizontal.magnitude : 0f, running && _grounded);
    }

    Vector3 ClipMotion(Vector3 motion)
    {
        if (_gliding || _glideBoostTimer > 0f)
            return motion; // 大空滑空飛行中は見えない壁判定を完全バイパス！

        var bounds = AdventureIslandBoundary.Instance;
        if (bounds != null)
            return bounds.ClipMotion(transform.position, motion);
        return motion;
    }

    void KeepWalkable()
    {
        if (_gliding || _glideBoostTimer > 0f)
            return; // 滑空飛行中は地上の歩行エリア制限を完全バイパス！

        var bounds = AdventureIslandBoundary.Instance;
        if (bounds == null)
            return;

        Vector3 pos = transform.position;
        if (bounds.IsWalkable(pos))
            return;

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

    bool OverWater(Vector3 pos)
    {
        return GroundY(pos) < WaterY() - 0.2f;
    }

    bool Floating()
    {
        Vector3 pos = transform.position;
        return OverWater(pos) && pos.y <= WaterY() + 0.45f;
    }

    bool StandingOnSeafloor()
    {
        Vector3 pos = transform.position;
        return OverWater(pos) && pos.y < WaterY() - 0.05f;
    }

    bool TooSteep()
    {
        if (_land == null || _land.terrainData == null)
            return false;
        Vector3 origin = _land.transform.position;
        Vector3 size = _land.terrainData.size;
        float nx = Mathf.Clamp01((transform.position.x - origin.x) / size.x);
        float nz = Mathf.Clamp01((transform.position.z - origin.z) / size.z);
        return _land.terrainData.GetInterpolatedNormal(nx, nz).y < 0.20f; // 78度以上の極端な絶壁以外は足が滑らず自力登坂可能に
    }

    void FloatOnWater()
    {
        Vector3 pos = transform.position;
        if (!OverWater(pos))
            return;
        float surface = WaterY() + Skin;
        if (pos.y > surface)
            return;

        _cc.enabled = false;
        transform.position = new Vector3(pos.x, surface, pos.z);
        _cc.enabled = true;
        _hop = Mathf.Max(_hop, 0f);
        _grounded = true;
        _gliding = false;
        _airborneTime = 0f;
    }

    float GroundY(Vector3 pos)
    {
        if (_land == null)
            return pos.y;
        return _land.SampleHeight(pos) + _land.transform.position.y;
    }

    float SurfaceY(Vector3 pos)
    {
        float landY = GroundY(pos);
        float water = WaterY();
        return landY < water ? water : landY;
    }

    Vector3 Stick(Vector3 pos)
    {
        var bounds = AdventureIslandBoundary.Instance;
        if (bounds != null)
            pos = bounds.ClampWalkable(pos);
        pos.y = SurfaceY(pos) + Skin;
        return pos;
    }

    public void Teleport(Vector3 pos)
    {
        pos = Stick(pos);
        _cc.enabled = false;
        transform.position = pos;
        _cc.enabled = true;
        _hop = 0f;
        _grounded = true;
        _gliding = false;
        _airborneTime = 0f;
        _airMomentum = Vector3.zero;
    }

    void CacheTerrains()
    {
        foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
        {
            if (terrain.name == "LandTerrain" || terrain.name == "IslandTerrain")
                _land = terrain;
            else if (terrain.name.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var col = terrain.GetComponent<TerrainCollider>();
                if (col != null)
                    col.enabled = false;
            }
        }
    }

    static Keyboard GetKeyboard()
    {
        var kb = Keyboard.current;
        if (kb != null) return kb;
        foreach (var device in InputSystem.devices)
        {
            if (device is Keyboard found)
                return found;
        }
        return null;
    }

    static Vector2 ReadMove(Keyboard kb)
    {
        Vector2 input = Vector2.zero;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
        }

        // 複数キーボードやゲームパッドのフォールバック
        if (input.sqrMagnitude < 0.001f)
        {
            var gp = Gamepad.current;
            if (gp != null)
            {
                Vector2 stick = gp.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.04f) input = stick;
                if (gp.dpad.up.isPressed) input.y += 1f;
                if (gp.dpad.down.isPressed) input.y -= 1f;
                if (gp.dpad.left.isPressed) input.x -= 1f;
                if (gp.dpad.right.isPressed) input.x += 1f;
            }
        }
        return Vector2.ClampMagnitude(input, 1f);
    }

    void PlayLocomotion(float speed, bool running)
    {
        if (_anim == null)
            return;
        string next = speed < 0.2f ? "NikoIdle" : (running ? "NikoRuns" : "NikoWalks");
        if (next != _clip)
        {
            _clip = next;
            _anim.CrossFadeInFixedTime(next, 0.15f);
        }
        if (next == "NikoIdle")
            _anim.speed = 1f;
        else
        {
            float reference = running ? 5.4f : 2.4f;
            _anim.speed = Mathf.Clamp(speed / reference, 0.9f, 1.7f);
        }
    }

    /// <summary>気流（ウインドレーン）や風のリングに乗った時の心地よい浮揚・推進</summary>
    public void ApplyGlideBoost(float boostMultiplier, float duration, Vector3 boostDirection = default)
    {
        _glideBoostMultiplier = Mathf.Max(_glideBoostMultiplier, boostMultiplier);
        _glideBoostTimer = Mathf.Max(_glideBoostTimer, duration);
        _grounded = false;
        _gliding = true;
        _airborneTime = 1.0f;
        _hop = 3.6f; // 気流を孕んでフワリと優雅に持ち上がる！

        Vector3 forwardDir = boostDirection.sqrMagnitude > 0.01f ? boostDirection : transform.forward;
        forwardDir.y = 0f;
        if (forwardDir.sqrMagnitude < 0.001f) forwardDir = transform.forward;
        forwardDir.Normalize();

        float cruiseSpeed = 10.5f * moveSpeedMultiplier; // 心地よい追い風クルージング速度
        _airMomentum = forwardDir * cruiseSpeed;
        transform.rotation = Quaternion.LookRotation(forwardDir);
    }

    /// <summary>上昇気流（サーマル）突入時の浮遊・上昇力付与</summary>
    public void ApplyUpdraft(float liftForce)
    {
        _updraftLift = liftForce;
        _updraftTimer = 0.25f;
        if (!_grounded)
        {
            _gliding = true;
        }
    }

    /// <summary>オアシス池（標高18.15m）および渓流のすり鉢窪地にいるかの判定</summary>
    public bool IsInLakeOrStreamBasin(Vector3 pos)
    {
        float distToLake = new Vector2(pos.x - 135f, pos.z - 166f).magnitude;
        bool nearLake = distToLake < 32f && pos.y <= 21.5f;
        bool inStream = (pos.x >= 143f && pos.x <= 163f && pos.z >= 156f && pos.z <= 172f && pos.y <= 26.0f);
        return nearLake || inStream;
    }

    /// <summary>島の中央座標（XZ平面）を取得</summary>
    public Vector3 GetIslandCenterXZ()
    {
        if (_land != null && _land.terrainData != null)
        {
            Vector3 origin = _land.transform.position;
            Vector3 size = _land.terrainData.size;
            return new Vector3(origin.x + size.x * 0.5f, 0f, origin.z + size.z * 0.5f);
        }
        return new Vector3(512f, 0f, 512f);
    }

    /// <summary>外周砂浜（白砂ビーチ・海岸線・低地）にいるかの判定</summary>
    public bool IsInBeachOrCoastZone(Vector3 pos)
    {
        float waterY = WaterY();
        if (float.IsNegativeInfinity(waterY)) waterY = 5.5f;

        Vector3 center = GetIslandCenterXZ();
        float distToCenter = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(center.x, center.z));
        float islandRadius = (_land != null && _land.terrainData != null) ? _land.terrainData.size.x * 0.5f : 512f;

        // 1. 島外周エリア（半径の約62%以遠、Grand Islandなら半径320m〜510m）かつ低地（海抜 <= waterY + 9.5f）
        if (distToCenter >= islandRadius * 0.62f && pos.y <= waterY + 9.5f)
            return true;

        // 2. 海水・波打ち際付近（海抜 <= waterY + 3.0f）
        if (pos.y <= waterY + 3.0f)
            return true;

        return false;
    }

    /// <summary>
    /// 周囲の崖（すり鉢窪地・切り立った崖下・谷底）を動的に判定し、崖の上へ一気に登るための上昇力と推進方向を算出
    /// </summary>
    public bool CheckCliffSurround(out float requiredHop, out Vector3 escapeDirection)
    {
        requiredHop = 0f;
        escapeDirection = transform.forward;

        if (_land == null) return false;

        Vector3 myPos = transform.position;
        float curY = myPos.y;

        float maxSurroundHeight = curY;
        Vector3 highestPointDir = transform.forward;
        int higherDirectionsCount = 0;

        // 8方向（前方、斜め前、左右、後方）
        Vector3[] dirs = new Vector3[]
        {
            transform.forward,
            (transform.forward + transform.right).normalized,
            transform.right,
            (-transform.forward + transform.right).normalized,
            -transform.forward,
            (-transform.forward - transform.right).normalized,
            -transform.right,
            (transform.forward - transform.right).normalized
        };

        // 近距離（3.5m）、中距離（7.5m）、遠距離（14.0m）を多重サンプリング
        float[] sampleDistances = new float[] { 3.5f, 7.5f, 14.0f };

        for (int d = 0; d < dirs.Length; d++)
        {
            Vector3 dir = dirs[d];
            bool dirIsHigher = false;

            for (int s = 0; s < sampleDistances.Length; s++)
            {
                float dist = sampleDistances[s];
                Vector3 checkPos = myPos + dir * dist;
                float h = _land.SampleHeight(checkPos) + _land.transform.position.y;

                if (h > curY + 2.4f) // 足元より2.4m以上高い段差・崖
                {
                    dirIsHigher = true;
                    if (h > maxSurroundHeight)
                    {
                        maxSurroundHeight = h;
                        highestPointDir = dir;
                    }
                }
            }

            if (dirIsHigher)
                higherDirectionsCount++;
        }

        // 正面の物理壁/急斜面Raycastチェック（目の前に切り立った岩壁・急峻な崖があるか）
        bool facingSteepCliff = false;
        RaycastHit wallHit;
        if (Physics.Raycast(myPos + Vector3.up * 0.8f, transform.forward, out wallHit, 5.0f))
        {
            if (!wallHit.collider.isTrigger && Vector3.Dot(wallHit.normal, Vector3.up) < 0.65f)
            {
                facingSteepCliff = true; // 傾斜角50度以上の急峻な壁
            }
        }

        float heightDiff = maxSurroundHeight - curY;

        // 判定条件：
        // 1. 周囲の3方向以上が崖に囲まれている（すり鉢・窪地・峡谷）
        // 2. 正面が急な崖で、上に高低差がある（目の前の崖登り）
        // 3. 周囲のいずれかの崖の高さが足元より2.8m以上高い
        if (higherDirectionsCount >= 3 || facingSteepCliff || heightDiff >= 2.8f)
        {
            // 崖のてっぺんを余裕で飛び越える跳躍初速を物理計算！
            // targetClearHeight = 崖の高さ + 5.5m（登りきって着地するための余裕マージン）
            // v = sqrt(2 * |g| * targetClearHeight)
            float targetClearHeight = Mathf.Clamp(Mathf.Max(heightDiff, 5.5f) + 5.5f, 10.0f, 32.0f);
            requiredHop = Mathf.Sqrt(targetClearHeight * -2f * gravity);

            // 推進方向：
            // 正面に崖がある場合はその崖の上（正面）へ跳び乗る
            // すり鉢状の場合は前進または崖の低い方向/高い方向へスムーズに脱出
            if (facingSteepCliff || Vector3.Dot(transform.forward, highestPointDir) > 0.1f)
            {
                escapeDirection = transform.forward;
            }
            else if (higherDirectionsCount >= 6)
            {
                // 四方八方が完全に塞がれている場合は前方へ打ち上げ
                escapeDirection = transform.forward;
            }
            else
            {
                escapeDirection = (transform.forward * 0.7f + highestPointDir * 0.3f).normalized;
            }
            return true;
        }

        return false;
    }
}
