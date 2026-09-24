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
    public float sensitivity = 0.26f;
    public float pitchMin = -12f;
    public float pitchMax = 32f;

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

    public static AdventureCameraFollow InstanceOrFind()
    {
        return Object.FindFirstObjectByType<AdventureCameraFollow>();
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
        sensitivity = 0.26f;
        positionSmoothTime = 0.02f;
        lookSmoothTime = 0.012f;
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
        LockCursor();
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        _lookUpdatedThisFrame = false;
        if (target == null) return;
        UpdateLookInputOnly();
        ApplyLookSmoothingForMove();
    }

    void LateUpdate()
    {
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

    void UpdateLookInputOnly()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        var opening = FindAnyObjectByType<AdventureRustFloatOpening>();
        bool isModalBoardOpen = opening != null && opening.IsModalBoardOpen();
        if (AdventureStoryFlow.WantsFreeCursor)
        {
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
        else
        {
            bool toggleCursor = (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.leftAltKey.wasPressedThisFrame));
            try { if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.Escape)) toggleCursor = true; } catch { }

            if (toggleCursor)
            {
                bool locked = Cursor.lockState != CursorLockMode.Locked;
                Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !locked;
            }
            else if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
            {
                LockCursor();
            }
        }

        if (kb != null && kb.rKey.wasPressedThisFrame)
            SnapBehindTarget();

        float mouseX = 0f;
        float mouseY = 0f;
        if (mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            mouseX = delta.x;
            mouseY = delta.y;
        }
        if (Mathf.Abs(mouseX) < 0.001f && Mathf.Abs(mouseY) < 0.001f)
        {
            try
            {
                mouseX = Input.GetAxis("Mouse X") * 10f;
                mouseY = Input.GetAxis("Mouse Y") * 10f;
            }
            catch { }
        }

        bool isRightDragging = mouse != null && (mouse.rightButton.isPressed || mouse.middleButton.isPressed);
        try { if (Input.GetMouseButton(1) || Input.GetMouseButton(2)) isRightDragging = true; } catch { }

        bool isCursorLocked = Cursor.lockState == CursorLockMode.Locked;
        bool canRotateByMouse = !isModalBoardOpen || isRightDragging || isCursorLocked;

        if (canRotateByMouse && (Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f))
        {
            _targetYaw += mouseX * sensitivity;
            _targetPitch = Mathf.Clamp(_targetPitch - mouseY * sensitivity, pitchMin, pitchMax);
            _lastMouseInputTime = Time.time;
        }

        float keyYaw = 0f;
        float keyPitch = 0f;
        if (kb != null)
        {
            if (kb.leftArrowKey.isPressed || kb.jKey.isPressed) keyYaw -= 1f;
            if (kb.rightArrowKey.isPressed || kb.lKey.isPressed) keyYaw += 1f;
            if (kb.upArrowKey.isPressed || kb.iKey.isPressed) keyPitch -= 1f;
            if (kb.downArrowKey.isPressed || kb.kKey.isPressed) keyPitch += 1f;
        }
        try
        {
            if (Input.GetKey(KeyCode.LeftArrow)) keyYaw -= 1f;
            if (Input.GetKey(KeyCode.RightArrow)) keyYaw += 1f;
            if (Input.GetKey(KeyCode.UpArrow)) keyPitch -= 1f;
            if (Input.GetKey(KeyCode.DownArrow)) keyPitch += 1f;
        }
        catch { }

        if (Mathf.Abs(keyYaw) > 0.01f || Mathf.Abs(keyPitch) > 0.01f)
        {
            float keyRate = AdventureRustFloatFeel.IsActiveScene ? AdventureRustFloatFeel.KeyYawRate : 110f;
            float pitchRate = AdventureRustFloatFeel.IsActiveScene ? AdventureRustFloatFeel.KeyPitchRate : 90f;
            _targetYaw += keyYaw * keyRate * Time.deltaTime;
            _targetPitch = Mathf.Clamp(_targetPitch - keyPitch * pitchRate * Time.deltaTime, pitchMin, pitchMax);
            _lastMouseInputTime = Time.time;
        }

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
            _targetYaw = Mathf.MoveTowardsAngle(_targetYaw, targetHeading, followRate * Time.deltaTime);
            if (cine > 0.01f)
                _targetPitch = Mathf.MoveTowards(_targetPitch, WalkPitch, 24f * Time.deltaTime);
        }
        else if (walkingGround)
        {
            float idle = Time.time - _lastMouseInputTime;
            float settle = idle > 0.55f ? 22f : 0f; // 操作中はピッチを引き戻さない（重い感の原因）
            if (settle > 0f)
                _targetPitch = Mathf.MoveTowards(_targetPitch, WalkPitch, settle * Time.deltaTime);
        }

        if (cine > 0.4f && !isAutoGlide)
            _targetPitch = Mathf.Clamp(_targetPitch, -6f, 22f);

        // 歩行中は視点ヨーを即時反映（SmoothDampしない）
        if (walkingGround)
        {
            _yaw = _targetYaw;
            _pitch = _targetPitch;
            _yawVel = 0f;
            _pitchVel = 0f;
        }
        else
        {
            float lookSmooth = Mathf.Max(0.01f, lookSmoothTime);
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
        var player = AdventurePlayerController.Instance;
        bool isGliding = player != null && player.IsGliding;
        bool isAutoGlide = player != null && player.IsAutoGliding;
        bool playerGrounded = player != null && player.IsGrounded;
        bool playerMoving = player != null && player.HasMoveInput;
        float cine = _cinematicBlend;
        // 接地フラグが1F遅れても、移動入力中は歩行扱い（出だしのカメラ遅れ＝反応の悪さ）
        bool walkingGround = !isGliding && !isAutoGlide && cine < 0.05f && (playerGrounded || playerMoving);

        Quaternion currentRot = Quaternion.Euler(_pitch, _yaw, 0f);

        Vector3 targetPivot;
        if (walkingGround)
            targetPivot = target.position + Vector3.up * WalkPivotHeight;
        else
        {
            float useHeight = Mathf.Lerp(height, CinematicHeight, cine);
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
        else
        {
            float pivotSmooth = isAutoGlide ? 0.06f : (isGliding ? 0.035f : positionSmoothTime);
            _currentPivot = Vector3.SmoothDamp(_currentPivot, targetPivot, ref _pivotVelocity, pivotSmooth);
        }

        float targetFov = Mathf.Lerp(isGliding ? 68f : 64f, CinematicFov, cine);
        if (_cam != null)
            _cam.fieldOfView = Mathf.MoveTowards(_cam.fieldOfView, targetFov, (cine > 0.01f ? 18f : 8f) * Time.deltaTime);

        float desiredDist = Mathf.Lerp(isGliding ? (distance + 1.0f) : distance, CinematicDistance, cine);
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
        float minCamY = Mathf.Lerp(1.15f, 1.35f, cine);
        targetPos.y = Mathf.Max(targetPos.y, target.position.y + minCamY);

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

        if (walkingGround)
        {
            Vector3 focus = target.position + Vector3.up * WalkFocusHeight;
            Vector3 toFocus = focus - targetPos;
            if (toFocus.sqrMagnitude > 0.01f)
            {
                Quaternion look = Quaternion.LookRotation(toFocus.normalized);
                float lookPitch = look.eulerAngles.x;
                if (lookPitch > 180f) lookPitch -= 360f;
                lookPitch = Mathf.Clamp(lookPitch, pitchMin, pitchMax);
                // 歩行中はフレーミングも即時（SmoothDampなし）
                _framedPitch = Mathf.Lerp(_pitch, lookPitch, 0.55f);
                transform.rotation = Quaternion.Euler(_framedPitch, _yaw, 0f);
            }
            else
            {
                _framedPitch = _pitch;
                transform.rotation = currentRot;
            }
        }
        else
        {
            _framedPitch = _pitch;
            transform.rotation = currentRot;
        }

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
