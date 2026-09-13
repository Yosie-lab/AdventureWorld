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
        _cc.slopeLimit = 50f;
        _cc.stepOffset = 0.45f;
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
        var kb = GetKeyboard();
        InteractPressed = kb != null && kb.eKey.wasPressedThisFrame;
        if (kb != null && kb.rKey.wasPressedThisFrame)
        {
            Teleport(spawnPosition);
            return;
        }

        Vector2 input = ReadMove(kb);
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

        float effectiveJumpHeight = jumpHeight * jumpMultiplier;
        if (kb != null && kb.spaceKey.wasPressedThisFrame)
        {
            if (_grounded)
            {
                _hop = Mathf.Sqrt(effectiveJumpHeight * -2f * gravity);
                _grounded = false;
                _airborneTime = 0f;
            }
            else if (canDoubleJump && !_doubleJumpUsed && !_gliding)
            {
                _hop = Mathf.Sqrt(effectiveJumpHeight * -1.8f * gravity);
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
        return _land.terrainData.GetInterpolatedNormal(nx, nz).y < 0.68f;
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

    void Teleport(Vector3 pos)
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
}
