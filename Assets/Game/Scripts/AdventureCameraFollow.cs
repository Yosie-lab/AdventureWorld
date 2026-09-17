using UnityEngine;
using UnityEngine.InputSystem;

public class AdventureCameraFollow : MonoBehaviour
{
    public Transform target;
    public float height = 1.25f;
    public float distance = 6.2f;
    public float sensitivity = 0.16f; // マウスの快適で自然な感度
    public float pitchMin = -12f;
    public float pitchMax = 32f;

    [Header("位置スムージング（段差・揺れの吸収）")]
    public float positionSmoothTime = 0.04f;

    [Header("視点スムージング（マウス／キーの角加速度を吸収）")]
    public float lookSmoothTime = 0.055f;
    public float mouseDeltaSmooth = 18f;

    [Header("シネマティック用")]
    public float CurrentYaw { get; private set; }

    /// <summary>歩行：水平寄りだがNiko全身が画角に入る程度の俯角</summary>
    const float WalkPitch = 9f;
    /// <summary>歩行時の注視点（胸〜頭の間＝画角中央にNiko）</summary>
    const float WalkFocusHeight = 1.15f;
    const float WalkPivotHeight = 1.28f;

    float _yaw;
    float _pitch = WalkPitch;
    float _targetYaw;
    float _targetPitch = WalkPitch;
    float _yawVel;
    float _pitchVel;
    float _mouseSmoothX;
    float _mouseSmoothY;
    float _lastMouseInputTime;

    Vector3 _currentPivot;
    Vector3 _pivotVelocity;
    Vector3 _posVelocity;

    float _currentDistance;
    float _distVel;

    Terrain _land;
    Camera _cam;

    // クライマックス〜エピローグ用シネマティック追従
    bool _cinematic;
    float _cinematicBlend;
    const float CinematicDistance = 5.0f;
    const float CinematicHeight = 1.6f;
    const float CinematicFov = 70f;
    const float CinematicBlendSpeed = 1.35f;

    /// <summary>天蓋クライマックス〜エピローグ中は後方広め・広角の映画カメラへブレンド</summary>
    public void SetCinematicMode(bool enabled)
    {
        _cinematic = enabled;
        if (!enabled)
            _cinematicBlend = 0f;
    }

    public bool IsCinematic => _cinematic;

    void Start()
    {
        if (target != null)
        {
            _yaw = target.eulerAngles.y;
            _targetYaw = _yaw;
            _targetPitch = _pitch;
            CurrentYaw = _yaw;
            _currentPivot = target.position + Vector3.up * height;
        }
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

        // 実際にモーダル説明ボードが画面に表示されているかどうかの厳密な判定
        var opening = FindAnyObjectByType<AdventureRustFloatOpening>();
        bool isModalBoardOpen = opening != null && opening.IsModalBoardOpen();
        var towerMgr = AdventureSanctuaryTowerManager.Instance;
        bool isLeverNear = towerMgr != null && towerMgr.IsPlayerNearLever;
        bool isScriptBoard = towerMgr != null && towerMgr.IsSkybreakModalActive;

        // モーダル／台本ボード／レバー付近／エネルギー注入中はカーソル解放（クリック操作を優先）
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
            // エスケープや左Altキーでカーソル解放 ⇄ ロックを手動切り替え
            bool toggleCursor = (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.leftAltKey.wasPressedThisFrame));
            try { if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.Escape)) toggleCursor = true; } catch { }

            if (toggleCursor)
            {
                bool locked = Cursor.lockState != CursorLockMode.Locked;
                Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !locked;
            }
            // 画面クリックで即座にカーソルロック復帰（いつでも視点操作を再開）
            else if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
            {
                LockCursor();
            }
        }

        if (kb != null && kb.rKey.wasPressedThisFrame)
        {
            _pitch = WalkPitch;
            _targetPitch = WalkPitch;
            _yaw = target.eulerAngles.y;
            _targetYaw = _yaw;
            _yawVel = 0f;
            _pitchVel = 0f;
            _mouseSmoothX = 0f;
            _mouseSmoothY = 0f;
        }

        // 1. マウス入力の取得（Input System + レガシーInputの多重サポート）
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

        // 生デルタの微小ジッターを指数平滑で吸収（応答は保ったままカクつきだけ落とす）
        float mouseBlend = 1f - Mathf.Exp(-mouseDeltaSmooth * Time.unscaledDeltaTime);
        _mouseSmoothX = Mathf.Lerp(_mouseSmoothX, mouseX, mouseBlend);
        _mouseSmoothY = Mathf.Lerp(_mouseSmoothY, mouseY, mouseBlend);
        mouseX = _mouseSmoothX;
        mouseY = _mouseSmoothY;

        // モーダルボードが画面に出ていない時は、マウス移動だけで100%確実にカメラ旋回！
        // （右ドラッグでも、カーソルロック中でも、通常のマウス移動でも確実に視点が追従）
        bool isRightDragging = mouse != null && (mouse.rightButton.isPressed || mouse.middleButton.isPressed);
        try { if (Input.GetMouseButton(1) || Input.GetMouseButton(2)) isRightDragging = true; } catch { }

        bool isCursorLocked = Cursor.lockState == CursorLockMode.Locked;
        bool canRotateByMouse = !isModalBoardOpen || isRightDragging || isCursorLocked;

        if (canRotateByMouse && (Mathf.Abs(mouseX) > 0.001f || Mathf.Abs(mouseY) > 0.001f))
        {
            _targetYaw += mouseX * sensitivity;
            _targetPitch = Mathf.Clamp(_targetPitch - mouseY * sensitivity, pitchMin, pitchMax);
            _lastMouseInputTime = Time.time;
        }

        // 2. キーボードによるカメラアングル操作（矢印キー または I, J, K, L キー）
        // マウスの状態やフォーカスに関わらず、いつでも確実にカメラアングルを変更できる
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

        // 3. ゲームパッド右スティック対応
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

        bool isGliding = AdventurePlayerController.Instance != null && AdventurePlayerController.Instance.IsGliding;
        bool isAutoGlide = AdventurePlayerController.Instance != null && AdventurePlayerController.Instance.IsAutoGliding;

        float blendTarget = _cinematic ? 1f : 0f;
        _cinematicBlend = Mathf.MoveTowards(_cinematicBlend, blendTarget, CinematicBlendSpeed * Time.unscaledDeltaTime);
        float cine = _cinematicBlend;

        // シネマ／オートグライド中は背後フォローを強め、後頭部ドアップを避ける
        float followIdle = cine > 0.2f || isAutoGlide ? 0.35f : 1.2f;
        float followRate = cine > 0.2f || isAutoGlide ? 55f : 36f;
        bool walkingGround = !isGliding && !isAutoGlide && cine < 0.05f;
        if ((isGliding || isAutoGlide) && (Time.time - _lastMouseInputTime > followIdle))
        {
            float targetHeading = target.eulerAngles.y;
            _targetYaw = Mathf.MoveTowardsAngle(_targetYaw, targetHeading, followRate * Time.deltaTime);
            if (cine > 0.01f)
                _targetPitch = Mathf.MoveTowards(_targetPitch, Mathf.Lerp(WalkPitch, 6f, cine), 24f * Time.deltaTime);
        }
        // 歩行中：Nikoが画角に収まる俯角へ戻す（水平すぎると足元だけ／姿消え）
        else if (walkingGround)
        {
            float idle = Time.time - _lastMouseInputTime;
            float settle = idle > 0.35f ? 40f : 10f;
            _targetPitch = Mathf.MoveTowards(_targetPitch, WalkPitch, settle * Time.deltaTime);
        }

        // シネマ中はマウス旋回を弱めて映画構図を崩しにくくする
        if (cine > 0.4f && !isAutoGlide)
        {
            // 入力は上で加算済み。ピッチだけ映画用レンジに緩くクランプ
            _targetPitch = Mathf.Clamp(_targetPitch, -6f, 22f);
        }

        // 目標角へ短いスムージング（マウスの段差・高Hzジッターを消しつつ操作感は保つ）
        float lookSmooth = Mathf.Max(0.01f, lookSmoothTime);
        _yaw = Mathf.SmoothDampAngle(_yaw, _targetYaw, ref _yawVel, lookSmooth, Mathf.Infinity, Time.unscaledDeltaTime);
        _pitch = Mathf.SmoothDamp(_pitch, _targetPitch, ref _pitchVel, lookSmooth, Mathf.Infinity, Time.unscaledDeltaTime);
        _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);

        // ヨーを 0〜360 に正規化（累積巨大化でコンパス／回転がおかしくなるのを防ぐ）
        _yaw = Mathf.Repeat(_yaw, 360f);
        _targetYaw = Mathf.Repeat(_targetYaw, 360f);
        CurrentYaw = _yaw;
        Quaternion currentRot = Quaternion.Euler(_pitch, _yaw, 0f);

        // 2. ピボット：歩行時は胸〜肩高さ（頭だと水平時に姿が画角外へ落ちる）
        Vector3 targetPivot;
        if (walkingGround)
            targetPivot = target.position + Vector3.up * WalkPivotHeight;
        else
        {
            float useHeight = Mathf.Lerp(height, CinematicHeight, cine);
            targetPivot = target.position + Vector3.up * useHeight;
        }
        float pivotSmooth = isAutoGlide ? 0.08f : (isGliding ? 0.045f : positionSmoothTime);
        _currentPivot = Vector3.SmoothDamp(_currentPivot, targetPivot, ref _pivotVelocity, pivotSmooth);

        // 3. 画角（FOV）演出
        float targetFov = Mathf.Lerp(isGliding ? 64f : 60f, CinematicFov, cine);
        if (_cam != null)
        {
            _cam.fieldOfView = Mathf.MoveTowards(_cam.fieldOfView, targetFov, (cine > 0.01f ? 18f : 8f) * Time.deltaTime);
        }

        // 4. 障害物検知と距離のスムーズダンピング（壁際でのカメラのガクつき・急伸縮を防止）
        float desiredDist = Mathf.Lerp(isGliding ? (distance + 0.8f) : distance, CinematicDistance, cine);
        float safeTargetDist = CalculateSafeDistance(_currentPivot, currentRot, desiredDist);
        // シネマ中は後頭部に寄らないよう、障害物で詰めても最低3.2mを確保
        if (cine > 0.2f)
            safeTargetDist = Mathf.Max(safeTargetDist, Mathf.Lerp(0.9f, 3.2f, cine));
        // 歩行時も最低距離を確保してNikoがフレームアウトしないようにする
        if (walkingGround)
            safeTargetDist = Mathf.Max(safeTargetDist, 3.6f);
        _currentDistance = Mathf.SmoothDamp(_currentDistance, safeTargetDist, ref _distVel, cine > 0.2f ? 0.12f : 0.05f);

        // 5. 最終カメラ位置
        Vector3 targetPos = _currentPivot + currentRot * new Vector3(0f, 0f, -_currentDistance);
        float minCamY = Mathf.Lerp(1.05f, 1.35f, cine);
        targetPos.y = Mathf.Max(targetPos.y, target.position.y + minCamY);

        // 地面めり込み防止
        if (_land == null)
            _land = AdventureQuestLocations.FindLand();
        float raisedByGround = 0f;
        if (_land != null)
        {
            float groundY = AdventureQuestLocations.GroundY(_land, targetPos.x, targetPos.z) + 0.8f;
            if (targetPos.y < groundY)
            {
                raisedByGround = groundY - targetPos.y;
                targetPos.y = groundY;
            }
        }

        transform.position = targetPos;

        // 歩行時：Nikoの胸を画角中央付近に保つ（地面でカメラが持ち上がっても姿を見失わない）
        if (walkingGround)
        {
            Vector3 focus = target.position + Vector3.up * WalkFocusHeight;
            Vector3 toFocus = focus - targetPos;
            if (toFocus.sqrMagnitude > 0.01f)
            {
                float pitchToNiko = Quaternion.LookRotation(toFocus.normalized).eulerAngles.x;
                if (pitchToNiko > 180f) pitchToNiko -= 360f;
                // プレイヤー俯角と「Nikoを捉える俯角」の大きい方（＝より下を見る方）を採用
                float framed = Mathf.Max(_pitch, pitchToNiko);
                if (raisedByGround > 0.05f)
                    framed = Mathf.Max(framed, pitchToNiko);
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
        float castRadius = 0.30f;
        if (Physics.SphereCast(pivot, castRadius, backDir, out RaycastHit hit, maxDist, ~0, QueryTriggerInteraction.Ignore))
        {
            // プレイヤー自身やドローン、トリガーを誤検知して急激にズームイン・カクつくのを防止
            if (target != null && (hit.transform == target || hit.transform.IsChildOf(target)))
            {
                return maxDist;
            }
            return Mathf.Clamp(hit.distance - 0.12f, 0.9f, maxDist);
        }
        return maxDist;
    }
}
