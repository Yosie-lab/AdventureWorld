using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 視点入力は Update（プレイヤーより先）、位置は LateUpdate。
/// 歩行の移動方向は TargetYaw（スムーズなし）を使い、入力遅延をなくす。
/// </summary>
[DefaultExecutionOrder(-200)]
public class AdventureCameraFollow : MonoBehaviour
{
    public Transform target;
    public float height = 1.25f;
    public float distance = 7.8f;
    public float sensitivity = 0.12f;
    public float pitchMin = -42f; // 足元・砂浜・貝殻を自然に見下ろせる
    public float pitchMax = 58f;  // ヤシの木・大空・ウミネコを気持ちよく見上げられる

    [Header("位置スムージング（段差・揺れの吸収）")]
    public float positionSmoothTime = 0.02f;

    [Header("視点スムージング")]
    public float lookSmoothTime = 0.012f;

    [Header("シネマティック用")]
    public float CurrentYaw { get; private set; }

    /// <summary>歩行・移動計算用：マウス意図ヨー（スムーズなし）</summary>
    public float TargetYaw => _targetYaw;

    /// <summary>歩行：完全水平</summary>
    const float WalkPitch = 0f;
    /// <summary>歩行時の注視点（腰〜胸）</summary>
    const float WalkFocusHeight = 1.05f;
    const float WalkPivotHeight = 1.35f;
    float WalkMinDistance => AdventureRustFloatFeel.IsActiveScene
        ? AdventureRustFloatFeel.WalkMinDistance
        : 5.2f;

    float _yaw;
    float _pitch = WalkPitch;
    float _targetYaw;
    float _targetPitch = WalkPitch;
    float _yawVel;
    float _pitchVel;
    float _lastMouseInputTime;

    Vector3 _currentPivot;
    Vector3 _pivotVelocity;

    float _currentDistance;
    float _distVel;

    float _framedPitch;
    float _framedPitchVel;
    float _camGroundY;
    float _camGroundYVel;

    Terrain _land;
    Camera _cam;

    bool _cinematic;
    float _cinematicBlend;
    const float CinematicDistance = 6.4f;
    const float CinematicHeight = 1.6f;
    const float CinematicFov = 74f;
    const float CinematicBlendSpeed = 1.35f;

    bool _lookUpdatedThisFrame;

    float _shakeIntensity;
    float _shakeUntil;
    float _shakeSeed;

    public void SetCinematicMode(bool enabled)
    {
        _cinematic = enabled;
        if (!enabled)
            _cinematicBlend = 0f;
    }

    /// <summary>地面・衝撃用のカメラ揺れ（天蓋裂開など）。intensity はワールドメートル相当。</summary>
    public void Shake(float intensity, float duration)
    {
        _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
        _shakeUntil = Mathf.Max(_shakeUntil, Time.unscaledTime + Mathf.Max(0.05f, duration));
        if (_shakeSeed < 0.01f)
            _shakeSeed = Random.Range(10f, 100f);
    }

    public bool IsCinematic => _cinematic;

    static AdventureCameraFollow _instance;
    public static AdventureCameraFollow Instance => _instance != null ? _instance : (_instance = Object.FindFirstObjectByType<AdventureCameraFollow>());

    public static AdventureCameraFollow InstanceOrFind() => Instance;

    public static float MasterSensitivity
    {
        get
        {
            float val = PlayerPrefs.GetFloat("Adventure_MouseSensitivity", 0.28f);
            // 過去の鈍い0.12fや異常値が保存されていたら軽快な0.28fへ自動更新
            if (val < 0.18f || val > 1.20f)
            {
                val = 0.28f;
                PlayerPrefs.SetFloat("Adventure_MouseSensitivity", 0.28f);
                PlayerPrefs.Save();
            }
            return val;
        }
        set
        {
            float clamped = Mathf.Clamp(value, 0.10f, 1.20f);
            PlayerPrefs.SetFloat("Adventure_MouseSensitivity", clamped);
            PlayerPrefs.Save();
            if (_instance != null) _instance.sensitivity = clamped;
        }
    }

    void Awake()
    {
        if (_instance == null)
            _instance = this;
        sensitivity = MasterSensitivity;
    }

    /// <summary>移動用の水平カメラ基底（意図ヨー即時）</summary>
    public void GetPlanarMoveBasis(out Vector3 forward, out Vector3 right)
    {
        // 同フレームでまだ Update 前なら、ここで視点入力を拾う
        if (!_lookUpdatedThisFrame)
            UpdateLookInputOnly();

        Quaternion flat = Quaternion.Euler(0f, _targetYaw, 0f);
        forward = flat * Vector3.forward;
        right = flat * Vector3.right;
    }

    /// <summary>Rキー／リセット時：標準後方カメラへ即復帰</summary>
    public void SnapBehindTarget()
    {
        if (target == null) return;
        _yaw = target.eulerAngles.y;
        _targetYaw = _yaw;
        _pitch = WalkPitch;
        _targetPitch = WalkPitch;
        _yawVel = 0f;
        _pitchVel = 0f;
        _currentPivot = target.position + Vector3.up * WalkPivotHeight;
        _pivotVelocity = Vector3.zero;
        _currentDistance = distance;
        _distVel = 0f;
        _framedPitch = WalkPitch;
        _framedPitchVel = 0f;
        CurrentYaw = _yaw;
    }

    void Start()
    {
        sensitivity = MasterSensitivity;
        positionSmoothTime = 0.01f;
        lookSmoothTime = 0.002f;
        _cam = GetComponent<Camera>();
        if (AdventureRustFloatFeel.IsActiveScene)
            AdventureRustFloatFeel.ApplyCameraDefaults(this, _cam);
        else if (distance < 7.5f)
            distance = 7.8f;

        if (target != null)
            SnapBehindTarget();
        else
            _currentDistance = distance;

        _land = AdventureQuestLocations.FindLand();
        if (_cam != null && !AdventureRustFloatFeel.IsActiveScene)
        {
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 1200f;
            _cam.useOcclusionCulling = false;
            if (_cam.fieldOfView < 60f || _cam.fieldOfView > 68f)
                _cam.fieldOfView = 64f;
        }
        if (!AdventureStoryFlow.WantsFreeCursor)
        {
            LockCursor();
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) return;

        if (AdventureStoryFlow.WantsFreeCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void LockCursor()
    {
        if (AdventureStoryFlow.WantsFreeCursor) return;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void EnsureTarget()
    {
        if (target != null) return;
        var player = AdventurePlayerController.InstanceOrFind();
        if (player != null)
        {
            target = player.transform;
            SnapBehindTarget();
        }
    }

    void Update()
    {
        _lookUpdatedThisFrame = false;
        EnsureTarget();
        if (target == null) return;
        UpdateLookInputOnly();
        ApplyLookSmoothingForMove();
    }

    void LateUpdate()
    {
        EnsureTarget();
        if (target == null) return;

        // Update を飛ばした場合の保険
        if (!_lookUpdatedThisFrame)
        {
            UpdateLookInputOnly();
            ApplyLookSmoothingForMove();
        }

        ApplyCameraRig();
        _lookUpdatedThisFrame = false;
    }

    Vector2 _prevMouseScreenPos;
    bool _hasPrevMousePos;

    void UpdateLookInputOnly()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        // ポーズ中のみカメラ回転を停止
        if (AdventurePauseMenu.IsOpen) return;

        // 画面クリックで即座にカーソルをロックしてゲームに復帰
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
        {
            bool isPointerOverUi = UnityEngine.EventSystems.EventSystem.current != null &&
                                   UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            if (!isPointerOverUi && !AdventureStoryFlow.WantsFreeCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        // Altキーでカーソルロックのトグル
        if (kb != null && kb.leftAltKey.wasPressedThisFrame)
        {
            bool locked = Cursor.lockState != CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        if (kb != null && kb.rKey.wasPressedThisFrame)
            SnapBehindTarget();

        // ── マウス移動量の高信頼取得 ──
        float mouseX = 0f;
        float mouseY = 0f;

        if (mouse != null)
        {
            // 系統1: delta による取得
            Vector2 delta = mouse.delta.ReadValue();
            mouseX = delta.x;
            mouseY = delta.y;

            // 系統2: delta が 0 の場合、スクリーン座標の差分から直接算出（エディタ・OS非ロック時対応）
            Vector2 curScreenPos = mouse.position.ReadValue();
            if (_hasPrevMousePos && Mathf.Abs(mouseX) < 0.001f && Mathf.Abs(mouseY) < 0.001f)
            {
                Vector2 diff = curScreenPos - _prevMouseScreenPos;
                if (diff.sqrMagnitude < 250000f) // 画面端ワープ防止
                {
                    mouseX = diff.x;
                    mouseY = diff.y;
                }
            }
            _prevMouseScreenPos = curScreenPos;
            _hasPrevMousePos = true;
        }

        // カメラ回転へ即時反映
        if (Mathf.Abs(mouseX) > 0.001f || Mathf.Abs(mouseY) > 0.001f)
        {
            _targetYaw += mouseX * sensitivity;
            _targetPitch = Mathf.Clamp(_targetPitch - mouseY * sensitivity, pitchMin, pitchMax);
            _lastMouseInputTime = Time.time;
        }

        // ゲームパッド対応
        var pad = Gamepad.current;
        if (pad != null)
        {
            Vector2 rStick = pad.rightStick.ReadValue();
            if (rStick.sqrMagnitude > 0.04f)
            {
                float padMul = AdventureRustFloatFeel.IsActiveScene ? AdventureRustFloatFeel.PadLookMul : 160f;
                _targetYaw += rStick.x * padMul * Mathf.Max(0.35f, sensitivity) * Time.deltaTime;
                _targetPitch = Mathf.Clamp(_targetPitch - rStick.y * (padMul * 0.75f) * Mathf.Max(0.35f, sensitivity) * Time.deltaTime, pitchMin, pitchMax);
                _lastMouseInputTime = Time.time;
            }
        }

        // 感度微調整ホットキー（[ / ] キー または テンキー - / +）
        if (kb != null)
        {
            if (kb.leftBracketKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame)
            {
                MasterSensitivity = Mathf.Max(0.10f, MasterSensitivity - 0.05f);
                AdventureNotificationToast.Show($"カメラ感度: {MasterSensitivity:F2}", 1.5f);
            }
            else if (kb.rightBracketKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame)
            {
                MasterSensitivity = Mathf.Min(0.90f, MasterSensitivity + 0.05f);
                AdventureNotificationToast.Show($"カメラ感度: {MasterSensitivity:F2}", 1.5f);
            }
        }

        _targetYaw = Mathf.Repeat(_targetYaw, 360f);
        _lookUpdatedThisFrame = true;
    }

    void ApplyLookSmoothingForMove()
    {
        var player = AdventurePlayerController.Instance;
        bool isGliding = player != null && player.IsGliding;
        bool isAutoGlide = player != null && player.IsAutoGliding;
        bool playerGrounded = player != null && player.IsGrounded;
        bool playerMoving = player != null && player.HasMoveInput;

        if (playerGrounded && !isAutoGlide && _cinematic && !AdventureStoryFlow.HoldCinematicCamera)
            SetCinematicMode(false);

        float blendTarget = _cinematic ? 1f : 0f;
        _cinematicBlend = Mathf.MoveTowards(_cinematicBlend, blendTarget, CinematicBlendSpeed * Time.unscaledDeltaTime);
        float cine = _cinematicBlend;

        float followIdle = cine > 0.2f || isAutoGlide ? 0.35f : 1.2f;
        float followRate = cine > 0.2f || isAutoGlide ? 55f : 36f;
        bool walkingGround = !isGliding && !isAutoGlide && cine < 0.05f && (playerGrounded || playerMoving);

        if ((isGliding || isAutoGlide) && (Time.time - _lastMouseInputTime > followIdle))
        {
            float targetHeading = target.eulerAngles.y;
            // エピローグのシネマ中：映画的な斜めアングル＋微細ドリフト（ドローンショット）
            if (isAutoGlide && cine > 0.3f)
            {
                float driftYaw = 14f + Mathf.Sin(Time.unscaledTime * 0.16f) * 8f;
                float driftPitch = WalkPitch + 3.4f + Mathf.Sin(Time.unscaledTime * 0.11f) * 1.6f;
                _targetYaw = Mathf.MoveTowardsAngle(_targetYaw, targetHeading + driftYaw, followRate * 0.85f * Time.deltaTime);
                _targetPitch = Mathf.MoveTowards(_targetPitch, driftPitch, 14f * Time.deltaTime);
            }
            else
            {
                _targetYaw = Mathf.MoveTowardsAngle(_targetYaw, targetHeading, followRate * Time.deltaTime);
                if (cine > 0.01f)
                    _targetPitch = Mathf.MoveTowards(_targetPitch, WalkPitch, 24f * Time.deltaTime);
            }
        }
        else if (walkingGround)
        {
            // プレイヤーが向けた上下アングル（ピッチ）を100%維持（勝手な引き戻しは一切行わない）
        }

        if (cine > 0.4f && !isAutoGlide)
            _targetPitch = Mathf.Clamp(_targetPitch, -6f, 22f);

        // 手動操作時（マウス操作直後や歩行中）は SmoothDamp による遅延を完全排除し、ダイレクト即時反映
        bool isManualAiming = (Time.time - _lastMouseInputTime < 0.25f);
        if (walkingGround || isManualAiming || cine < 0.05f)
        {
            _yaw = _targetYaw;
            _pitch = _targetPitch;
            _yawVel = 0f;
            _pitchVel = 0f;
        }
        else
        {
            float lookSmooth = Mathf.Max(0.003f, lookSmoothTime);
            _yaw = Mathf.SmoothDampAngle(_yaw, _targetYaw, ref _yawVel, lookSmooth, Mathf.Infinity, Time.unscaledDeltaTime);
            _pitch = Mathf.SmoothDamp(_pitch, _targetPitch, ref _pitchVel, lookSmooth, Mathf.Infinity, Time.unscaledDeltaTime);
        }

        _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);
        _yaw = Mathf.Repeat(_yaw, 360f);
        _targetYaw = Mathf.Repeat(_targetYaw, 360f);
        CurrentYaw = _yaw;
    }

    void ApplyCameraRig()
    {
        var player = AdventurePlayerController.InstanceOrFind();
        bool isGliding = player != null && player.IsGliding;
        bool isAutoGlide = player != null && player.IsAutoGliding;
        bool playerGrounded = player != null && player.IsGrounded;
        bool playerMoving = player != null && player.HasMoveInput;
        float cine = _cinematicBlend;
        bool inAir = !playerGrounded && !isGliding && !isAutoGlide;
        // 接地フラグが1F遅れても、移動入力中は歩行扱い（ただし空中ジャンプ中は除く）
        bool walkingGround = !isGliding && !isAutoGlide && !inAir && cine < 0.05f && (playerGrounded || playerMoving);

        Quaternion currentRot = Quaternion.Euler(_pitch, _yaw, 0f);

        Vector3 targetPivot;
        if (walkingGround)
            targetPivot = target.position + Vector3.up * WalkPivotHeight;
        else
        {
            float targetHeight = (isAutoGlide && cine > 0.3f) ? 1.95f : CinematicHeight;
            float useHeight = Mathf.Lerp(height, targetHeight, cine);
            targetPivot = target.position + Vector3.up * useHeight;
        }

        if (Vector3.Distance(_currentPivot, targetPivot) > 12f)
        {
            _currentPivot = targetPivot;
            _pivotVelocity = Vector3.zero;
            _currentDistance = Mathf.Max(distance, WalkMinDistance);
            _distVel = 0f;
        }

        // 歩行：ピボットもほぼスナップ（カメラ遅れ＝操作の重さ）
        if (walkingGround)
        {
            _currentPivot = targetPivot;
            _pivotVelocity = Vector3.zero;
        }
        else if (inAir)
        {
            // ジャンプ中：水平XZはプレイヤーの移動に即応させ、垂直Yは緩やかにスムージング（0.38秒）
            // これにより Niko が画面内でしっかり地面から跳び上がり、地面が下に揺れるのを防ぐ
            float targetX = targetPivot.x;
            float targetZ = targetPivot.z;
            float smoothY = Mathf.SmoothDamp(_currentPivot.y, targetPivot.y, ref _pivotVelocity.y, 0.38f);
            _currentPivot = new Vector3(targetX, smoothY, targetZ);
            _pivotVelocity.x = 0f;
            _pivotVelocity.z = 0f;
        }
        else
        {
            float pivotSmooth = isAutoGlide ? 0.06f : (isGliding ? 0.035f : positionSmoothTime);
            _currentPivot = Vector3.SmoothDamp(_currentPivot, targetPivot, ref _pivotVelocity, pivotSmooth);
        }

        float cineFov = (isAutoGlide && cine > 0.3f) ? 76f : CinematicFov;
        float targetFov = Mathf.Lerp(isGliding ? 68f : 64f, cineFov, cine);
        if (_cam != null)
            _cam.fieldOfView = Mathf.MoveTowards(_cam.fieldOfView, targetFov, (cine > 0.01f ? 18f : 8f) * Time.deltaTime);

        float cineDist = (isAutoGlide && cine > 0.3f) ? 7.8f : CinematicDistance;
        float desiredDist = Mathf.Lerp(isGliding ? (distance + 1.0f) : distance, cineDist, cine);
        float safeTargetDist = CalculateSafeDistance(_currentPivot, currentRot, desiredDist);
        if (cine > 0.2f)
            safeTargetDist = Mathf.Max(safeTargetDist, Mathf.Lerp(0.9f, 3.2f, cine));
        if (walkingGround)
            safeTargetDist = Mathf.Max(safeTargetDist, WalkMinDistance);

        if (walkingGround)
        {
            _currentDistance = safeTargetDist;
            _distVel = 0f;
        }
        else
        {
            _currentDistance = Mathf.SmoothDamp(_currentDistance, safeTargetDist, ref _distVel, cine > 0.2f ? 0.12f : 0.08f);
        }

        Vector3 targetPos = _currentPivot + currentRot * new Vector3(0f, 0f, -_currentDistance);
        if (!inAir)
        {
            float minCamY = Mathf.Lerp(1.15f, 1.35f, cine);
            targetPos.y = Mathf.Max(targetPos.y, target.position.y + minCamY);
        }

        if (_land == null)
            _land = AdventureQuestLocations.FindLand();
        if (_land != null)
        {
            float groundY = AdventureQuestLocations.GroundY(_land, targetPos.x, targetPos.z) + 0.85f;
            if (targetPos.y < groundY)
            {
                if (_camGroundY < 0.1f) _camGroundY = groundY;
                _camGroundY = Mathf.SmoothDamp(_camGroundY, groundY, ref _camGroundYVel, 0.06f);
                targetPos.y = Mathf.Max(targetPos.y, _camGroundY);
            }
            else
            {
                _camGroundY = targetPos.y;
            }
        }

        transform.position = targetPos;

        _framedPitch = _pitch;
        transform.rotation = currentRot;

        if (Time.unscaledTime < _shakeUntil && _shakeIntensity > 0.001f)
        {
            float remain = _shakeUntil - Time.unscaledTime;
            float falloff = Mathf.Clamp01(remain / 0.55f);
            float amp = _shakeIntensity * falloff;
            float t = Time.unscaledTime * 55f + _shakeSeed;
            Vector3 offset = new Vector3(
                Mathf.Sin(t * 1.7f) * amp,
                Mathf.Sin(t * 2.3f) * amp * 0.75f,
                Mathf.Cos(t * 1.9f) * amp * 0.55f);
            transform.position += offset;
            transform.rotation = Quaternion.Euler(
                transform.eulerAngles.x + Mathf.Sin(t * 2.1f) * amp * 4.5f,
                transform.eulerAngles.y + Mathf.Cos(t * 1.6f) * amp * 3.2f,
                Mathf.Sin(t * 2.8f) * amp * 6f);
        }
        else if (_shakeIntensity > 0f)
        {
            _shakeIntensity = 0f;
            _shakeSeed = 0f;
        }
    }

    float CalculateSafeDistance(Vector3 pivot, Quaternion rot, float maxDist)
    {
        Vector3 backDir = rot * Vector3.back;
        float castRadius = 0.28f;
        if (Physics.SphereCast(pivot, castRadius, backDir, out RaycastHit hit, maxDist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (target != null && (hit.transform == target || hit.transform.IsChildOf(target)))
                return maxDist;

            string n = hit.collider != null ? hit.collider.name : "";
            if (n.IndexOf("Rust", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Grass", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Flower", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Bush", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Rock", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Stone", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Pond", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("River", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Plant", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Reed", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Podium", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Floor", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Terrace", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Pedestal", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Step", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Stair", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return maxDist;

            return Mathf.Clamp(hit.distance - 0.15f, WalkMinDistance * 0.85f, maxDist);
        }
        return maxDist;
    }
}
