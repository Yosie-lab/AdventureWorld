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

    void Start()
    {
        if (target != null)
        {
            _yaw = target.eulerAngles.y;
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

        bool isOpeningActive = !AdventureRustFloatOpening.IsGameStarted && FindAnyObjectByType<AdventureRustFloatOpening>() != null;
        var towerMgr = AdventureSanctuaryTowerManager.Instance;
        bool isLeverNear = towerMgr != null && towerMgr.IsPlayerNearLever;

        // レバーの近く、またはオープニング中はカーソルを常時自動解放・可視化（クリック操作を即座に可能に）
        if (isOpeningActive || isLeverNear)
        {
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
        else
        {
            // エスケープや左Altキー、または旧Inputでカーソル解放 ⇄ ロックを快適にトグル
            bool toggleCursor = (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.leftAltKey.wasPressedThisFrame));
            try { if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.Escape)) toggleCursor = true; } catch { }

            if (toggleCursor)
            {
                bool locked = Cursor.lockState != CursorLockMode.Locked;
                Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !locked;
            }
            // 画面クリックでロック復帰（レバー付近やUI表示中は絶対に強制ロックしない）
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
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

        // カメラ旋回が有効かどうかの判定：
        // ・カーソルロック中（通常プレイ中）：マウスを動かすだけで旋回
        // ・カーソル解放中（説明ボード中、またはAltでカーソル表示中）：マウス右ボタンドラッグ、または中ボタンドラッグで自由に旋回
        // ・さらに、ゲーム開始後は左クリックでもカーソルロックに復帰して旋回
        bool isRightDragging = mouse != null && (mouse.rightButton.isPressed || mouse.middleButton.isPressed);
        try { if (Input.GetMouseButton(1) || Input.GetMouseButton(2)) isRightDragging = true; } catch { }

        bool isCursorLocked = Cursor.lockState == CursorLockMode.Locked;
        bool canRotateByMouse = isCursorLocked || isRightDragging;

        // ゲーム開始後、画面を左クリックした場合はカーソルロックを復帰
        if (!isOpeningActive && mouse != null && mouse.leftButton.wasPressedThisFrame && !isCursorLocked)
        {
            LockCursor();
            isCursorLocked = true;
            canRotateByMouse = true;
        }

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

        // 滑空中の背後オートフォロー（マウス操作から1.2秒以上経過している場合のみ優美にアシスト）
        if (isGliding && (Time.time - _lastMouseInputTime > 1.2f))
        {
            float targetHeading = target.eulerAngles.y;
            _yaw = Mathf.MoveTowardsAngle(_yaw, targetHeading, 36f * Time.deltaTime);
        }

        // 1. 回転はプレイヤーの入力に即座に1対1で忠実追従（遅延・ラグを完全排除）
        Quaternion currentRot = Quaternion.Euler(_pitch, _yaw, 0f);

        // 2. ピボット位置のスムーズダンピング（キャラクターの小刻みな段差ショックだけを滑らかに吸収）
        Vector3 targetPivot = target.position + Vector3.up * height;
        _currentPivot = Vector3.SmoothDamp(_currentPivot, targetPivot, ref _pivotVelocity, isGliding ? 0.045f : positionSmoothTime);

        // 3. 画角（FOV）演出
        float targetFov = isGliding ? 64f : 60f;
        if (_cam != null)
        {
            _cam.fieldOfView = Mathf.MoveTowards(_cam.fieldOfView, targetFov, 8f * Time.deltaTime);
        }

        // 4. 障害物検知と距離のスムーズダンピング（壁際でのカメラのガクつき・急伸縮を防止）
        float desiredDist = isGliding ? (distance + 0.8f) : distance;
        float safeTargetDist = CalculateSafeDistance(_currentPivot, currentRot, desiredDist);
        _currentDistance = Mathf.SmoothDamp(_currentDistance, safeTargetDist, ref _distVel, 0.05f);

        // 5. 最終カメラ位置の計算（二重ダンピングを廃止し、ピボット基準で直結配置することで位相差振動・カクつきを完全根絶）
        Vector3 targetPos = _currentPivot + currentRot * new Vector3(0f, 0f, -_currentDistance);
        targetPos.y = Mathf.Max(targetPos.y, target.position.y + 1.1f);

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
