using UnityEngine;
using UnityEngine.InputSystem;

public class AdventureCameraFollow : MonoBehaviour
{
    public Transform target;
    public float height = 1.7f;
    public float distance = 6.5f;
    public float sensitivity = 0.16f; // マウスの快適で自然な感度
    public float pitchMin = -10f;
    public float pitchMax = 32f;

    [Header("位置スムージング（段差・揺れの吸収）")]
    public float positionSmoothTime = 0.04f;

    [Header("シネマティック用")]
    public float CurrentYaw { get; private set; }

    float _yaw;
    float _pitch = 12f;
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

        // モーダル／台本ボード／レバー付近ではカーソル解放（クリックで次へ進める）
        if (isModalBoardOpen || isLeverNear || isScriptBoard)
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
            _pitch = 12f;
            _yaw = target.eulerAngles.y;
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

        // モーダルボードが画面に出ていない時は、マウス移動だけで100%確実にカメラ旋回！
        // （右ドラッグでも、カーソルロック中でも、通常のマウス移動でも確実に視点が追従）
        bool isRightDragging = mouse != null && (mouse.rightButton.isPressed || mouse.middleButton.isPressed);
        try { if (Input.GetMouseButton(1) || Input.GetMouseButton(2)) isRightDragging = true; } catch { }

        bool isCursorLocked = Cursor.lockState == CursorLockMode.Locked;
        bool canRotateByMouse = !isModalBoardOpen || isRightDragging || isCursorLocked;

        if (canRotateByMouse && (Mathf.Abs(mouseX) > 0.001f || Mathf.Abs(mouseY) > 0.001f))
        {
            _yaw += mouseX * sensitivity;
            _pitch = Mathf.Clamp(_pitch - mouseY * sensitivity, pitchMin, pitchMax);
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
            _yaw += keyYaw * 95f * Time.deltaTime;
            _pitch = Mathf.Clamp(_pitch - keyPitch * 75f * Time.deltaTime, pitchMin, pitchMax);
            _lastMouseInputTime = Time.time;
        }

        // 3. ゲームパッド右スティック対応
        var pad = Gamepad.current;
        if (pad != null)
        {
            Vector2 rStick = pad.rightStick.ReadValue();
            if (rStick.sqrMagnitude > 0.04f)
            {
                _yaw += rStick.x * 130f * sensitivity * Time.deltaTime;
                _pitch = Mathf.Clamp(_pitch - rStick.y * 100f * sensitivity * Time.deltaTime, pitchMin, pitchMax);
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
        if ((isGliding || isAutoGlide) && (Time.time - _lastMouseInputTime > followIdle))
        {
            float targetHeading = target.eulerAngles.y;
            _yaw = Mathf.MoveTowardsAngle(_yaw, targetHeading, followRate * Time.deltaTime);
            if (cine > 0.01f)
                _pitch = Mathf.MoveTowards(_pitch, Mathf.Lerp(12f, 8f, cine), 24f * Time.deltaTime);
        }

        // シネマ中はマウス旋回を弱めて映画構図を崩しにくくする
        if (cine > 0.4f && !isAutoGlide)
        {
            // 入力は上で加算済み。ピッチだけ映画用レンジに緩くクランプ
            _pitch = Mathf.Clamp(_pitch, -6f, 22f);
        }

        // 1. 回転はプレイヤーの入力に即座に1対1で忠実追従（遅延・ラグを完全排除）
        CurrentYaw = _yaw;
        Quaternion currentRot = Quaternion.Euler(_pitch, _yaw, 0f);

        // 2. ピボット位置のスムーズダンピング（キャラクターの小刻みな段差ショックだけを滑らかに吸収）
        float useHeight = Mathf.Lerp(height, CinematicHeight, cine);
        Vector3 targetPivot = target.position + Vector3.up * useHeight;
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
        _currentDistance = Mathf.SmoothDamp(_currentDistance, safeTargetDist, ref _distVel, cine > 0.2f ? 0.12f : 0.05f);

        // 5. 最終カメラ位置の計算（二重ダンピングを廃止し、ピボット基準で直結配置することで位相差振動・カクつきを完全根絶）
        Vector3 targetPos = _currentPivot + currentRot * new Vector3(0f, 0f, -_currentDistance);
        float minCamY = Mathf.Lerp(1.1f, 1.35f, cine);
        targetPos.y = Mathf.Max(targetPos.y, target.position.y + minCamY);

        // 地面めり込み防止
        if (_land == null)
            _land = AdventureQuestLocations.FindLand();
        if (_land != null)
        {
            float groundY = AdventureQuestLocations.GroundY(_land, targetPos.x, targetPos.z) + 0.8f;
            if (targetPos.y < groundY)
            {
                targetPos.y = groundY;
            }
        }

        transform.position = targetPos;
        transform.rotation = currentRot;
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
