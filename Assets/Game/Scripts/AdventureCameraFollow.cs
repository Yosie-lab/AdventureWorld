using UnityEngine;
using UnityEngine.InputSystem;

public class AdventureCameraFollow : MonoBehaviour
{
    public Transform target;
    public float height = 1.25f;
    public float distance = 6.2f;
    public float sensitivity = 0.16f;
    public float pitchMin = -12f;
    public float pitchMax = 32f;

    [Header("位置スムージング（段差・揺れの吸収）")]
    public float positionSmoothTime = 0.04f;

    [Header("視点スムージング")]
    public float lookSmoothTime = 0.03f;

    [Header("シネマティック用")]
    public float CurrentYaw { get; private set; }

    /// <summary>歩行：Niko全身が入る俯角</summary>
    const float WalkPitch = 10f;
    /// <summary>歩行時の注視点（腰〜胸）</summary>
    const float WalkFocusHeight = 1.05f;
    const float WalkPivotHeight = 1.35f;
    const float WalkMinDistance = 4.2f;

    float _yaw;
    float _pitch = WalkPitch;
    float _targetYaw;
    float _targetPitch = WalkPitch;
    float _yawVel;
    float _pitchVel;
    float _lastMouseInputTime;

    Vector3 _currentPivot;
    Vector3 _pivotVelocity;
    Vector3 _posVelocity;

    float _currentDistance;
    float _distVel;

    Terrain _land;
    Camera _cam;

    bool _cinematic;
    float _cinematicBlend;
    const float CinematicDistance = 5.0f;
    const float CinematicHeight = 1.6f;
    const float CinematicFov = 70f;
    const float CinematicBlendSpeed = 1.35f;

    public void SetCinematicMode(bool enabled)
    {
        _cinematic = enabled;
        if (!enabled)
            _cinematicBlend = 0f;
    }

    public bool IsCinematic => _cinematic;

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
        CurrentYaw = _yaw;
    }

    void Start()
    {
        if (target != null)
            SnapBehindTarget();
        else
            _currentDistance = distance;

        _land = AdventureQuestLocations.FindLand();
        _cam = GetComponent<Camera>();
        if (_cam != null)
        {
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 1200f;
            _cam.useOcclusionCulling = false;
        }
        LockCursor();
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        var kb = Keyboard.current;
        var mouse = Mouse.current;

        var opening = FindAnyObjectByType<AdventureRustFloatOpening>();
        bool isModalBoardOpen = opening != null && opening.IsModalBoardOpen();
        var towerMgr = AdventureSanctuaryTowerManager.Instance;
        bool isLeverNear = towerMgr != null && towerMgr.IsPlayerNearLever;
        bool isScriptBoard = towerMgr != null && towerMgr.IsSkybreakModalActive;
        bool isOilPrompt = towerMgr != null && towerMgr.IsClimaxOilPromptActive;
        bool isPrologueOil = AdventurePrologueDrama.Instance != null && AdventurePrologueDrama.Instance.IsWaitingForOil;
        if (isModalBoardOpen || isLeverNear || isScriptBoard || isOilPrompt || isPrologueOil)
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

        // 生マウス入力のみ使用（平滑の残留で勝手に回り続けるのを防ぐ）
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
            _targetYaw += keyYaw * 95f * Time.deltaTime;
            _targetPitch = Mathf.Clamp(_targetPitch - keyPitch * 75f * Time.deltaTime, pitchMin, pitchMax);
            _lastMouseInputTime = Time.time;
        }

        var pad = Gamepad.current;
        if (pad != null)
        {
            Vector2 rStick = pad.rightStick.ReadValue();
            if (rStick.sqrMagnitude > 0.04f)
            {
                _targetYaw += rStick.x * 130f * sensitivity * Time.deltaTime;
                _targetPitch = Mathf.Clamp(_targetPitch - rStick.y * 100f * sensitivity * Time.deltaTime, pitchMin, pitchMax);
                _lastMouseInputTime = Time.time;
            }
        }

        var player = AdventurePlayerController.Instance;
        bool isGliding = player != null && player.IsGliding;
        bool isAutoGlide = player != null && player.IsAutoGliding;
        bool playerGrounded = player != null && player.IsGrounded;

        // 地上に戻ったらシネマ残りを切る（エンディング後にNikoが見えない主因）
        if (playerGrounded && !isAutoGlide && _cinematic && towerMgr != null
            && !towerMgr.IsEpiloguePlaying && !towerMgr.ClimaxCrisisStarted && !towerMgr.ShowGameClearModal)
        {
            SetCinematicMode(false);
        }

        float blendTarget = _cinematic ? 1f : 0f;
        _cinematicBlend = Mathf.MoveTowards(_cinematicBlend, blendTarget, CinematicBlendSpeed * Time.unscaledDeltaTime);
        float cine = _cinematicBlend;

        float followIdle = cine > 0.2f || isAutoGlide ? 0.35f : 1.2f;
        float followRate = cine > 0.2f || isAutoGlide ? 55f : 36f;
        bool walkingGround = !isGliding && !isAutoGlide && cine < 0.05f && playerGrounded;
        if ((isGliding || isAutoGlide) && (Time.time - _lastMouseInputTime > followIdle))
        {
            float targetHeading = target.eulerAngles.y;
            _targetYaw = Mathf.MoveTowardsAngle(_targetYaw, targetHeading, followRate * Time.deltaTime);
            if (cine > 0.01f)
                _targetPitch = Mathf.MoveTowards(_targetPitch, Mathf.Lerp(WalkPitch, 6f, cine), 24f * Time.deltaTime);
        }
        else if (walkingGround)
        {
            float idle = Time.time - _lastMouseInputTime;
            float settle = idle > 0.45f ? 28f : 8f;
            _targetPitch = Mathf.MoveTowards(_targetPitch, WalkPitch, settle * Time.deltaTime);
        }

        if (cine > 0.4f && !isAutoGlide)
            _targetPitch = Mathf.Clamp(_targetPitch, -6f, 22f);

        // 歩行はほぼ即応、滑空／シネマだけ少し滑らか
        float lookSmooth = walkingGround ? 0.018f : Mathf.Max(0.01f, lookSmoothTime);
        _yaw = Mathf.SmoothDampAngle(_yaw, _targetYaw, ref _yawVel, lookSmooth, Mathf.Infinity, Time.unscaledDeltaTime);
        _pitch = Mathf.SmoothDamp(_pitch, _targetPitch, ref _pitchVel, lookSmooth, Mathf.Infinity, Time.unscaledDeltaTime);
        _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);

        _yaw = Mathf.Repeat(_yaw, 360f);
        _targetYaw = Mathf.Repeat(_targetYaw, 360f);
        CurrentYaw = _yaw;
        Quaternion currentRot = Quaternion.Euler(_pitch, _yaw, 0f);

        Vector3 targetPivot;
        if (walkingGround)
            targetPivot = target.position + Vector3.up * WalkPivotHeight;
        else
        {
            float useHeight = Mathf.Lerp(height, CinematicHeight, cine);
            targetPivot = target.position + Vector3.up * useHeight;
        }

        // テレポート／柱上昇後にピボットが取り残されないようスナップ
        if (Vector3.Distance(_currentPivot, targetPivot) > 12f)
        {
            _currentPivot = targetPivot;
            _pivotVelocity = Vector3.zero;
            _currentDistance = Mathf.Max(distance, WalkMinDistance);
            _distVel = 0f;
        }

        float pivotSmooth = isAutoGlide ? 0.08f : (isGliding ? 0.045f : positionSmoothTime);
        _currentPivot = Vector3.SmoothDamp(_currentPivot, targetPivot, ref _pivotVelocity, pivotSmooth);

        float targetFov = Mathf.Lerp(isGliding ? 64f : 58f, CinematicFov, cine);
        if (_cam != null)
            _cam.fieldOfView = Mathf.MoveTowards(_cam.fieldOfView, targetFov, (cine > 0.01f ? 18f : 8f) * Time.deltaTime);

        float desiredDist = Mathf.Lerp(isGliding ? (distance + 0.8f) : distance, CinematicDistance, cine);
        float safeTargetDist = CalculateSafeDistance(_currentPivot, currentRot, desiredDist);
        if (cine > 0.2f)
            safeTargetDist = Mathf.Max(safeTargetDist, Mathf.Lerp(0.9f, 3.2f, cine));
        if (walkingGround)
            safeTargetDist = Mathf.Max(safeTargetDist, WalkMinDistance);
        _currentDistance = Mathf.SmoothDamp(_currentDistance, safeTargetDist, ref _distVel, cine > 0.2f ? 0.12f : 0.05f);

        Vector3 targetPos = _currentPivot + currentRot * new Vector3(0f, 0f, -_currentDistance);
        float minCamY = Mathf.Lerp(1.15f, 1.35f, cine);
        targetPos.y = Mathf.Max(targetPos.y, target.position.y + minCamY);

        if (_land == null)
            _land = AdventureQuestLocations.FindLand();
        if (_land != null)
        {
            float groundY = AdventureQuestLocations.GroundY(_land, targetPos.x, targetPos.z) + 0.85f;
            if (targetPos.y < groundY)
                targetPos.y = groundY;
        }

        transform.position = targetPos;

        // 歩行：常にNikoの上半身を画角中央へ（俯角の取り合いで空だけ／足元だけになるのを防ぐ）
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
                // プレイヤー操作ピッチと注視点の中間〜注視点寄り
                float framed = Mathf.Lerp(_pitch, lookPitch, 0.72f);
                framed = Mathf.Clamp(framed, pitchMin, pitchMax);
                transform.rotation = Quaternion.Euler(framed, _yaw, 0f);
            }
            else
            {
                transform.rotation = currentRot;
            }
        }
        else
        {
            transform.rotation = currentRot;
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

            // Rust／小さな草木で寄られすぎない
            string n = hit.collider != null ? hit.collider.name : "";
            if (n.IndexOf("Rust", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Grass", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Flower", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Bush", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return maxDist;

            return Mathf.Clamp(hit.distance - 0.15f, WalkMinDistance * 0.85f, maxDist);
        }
        return maxDist;
    }
}
