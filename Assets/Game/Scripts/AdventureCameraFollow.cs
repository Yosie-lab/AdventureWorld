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

        // エスケープでカーソル解放、画面クリックで確実にロック復帰（スタート前はクリックによる強制ロックを停止）
        if (!isOpeningActive)
        {
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                bool locked = Cursor.lockState != CursorLockMode.Locked;
                Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !locked;
            }
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

        // マウス視点操作（ダイレクト即時反映：遅延ゼロで指先の動きにピタッと追従、スタート前は待機）
        bool isMouseActive = Cursor.lockState == CursorLockMode.Locked && !isOpeningActive;
        if (mouse != null && isMouseActive)
        {
            Vector2 delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude > 0.0001f)
            {
                _yaw += delta.x * sensitivity;
                _pitch = Mathf.Clamp(_pitch - delta.y * sensitivity, pitchMin, pitchMax);
                _lastMouseInputTime = Time.time;
            }
        }

        // ゲームパッド右スティック対応
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

        // 2. 位置のスムーズダンピング（キャラクターの小刻みな足踏みや段差ショックだけを滑らかに吸収）
        Vector3 targetPivot = target.position + Vector3.up * height;
        _currentPivot = Vector3.SmoothDamp(_currentPivot, targetPivot, ref _pivotVelocity, isGliding ? 0.07f : positionSmoothTime);

        // 3. 画角（FOV）演出
        float targetFov = isGliding ? 64f : 60f;
        if (_cam != null)
        {
            _cam.fieldOfView = Mathf.MoveTowards(_cam.fieldOfView, targetFov, 8f * Time.deltaTime);
        }

        // 4. 障害物検知と距離のスムーズダンピング（壁際でのカメラのガクつき・急伸縮を防止）
        float desiredDist = isGliding ? (distance + 0.8f) : distance;
        float safeTargetDist = CalculateSafeDistance(_currentPivot, currentRot, desiredDist);
        _currentDistance = Mathf.SmoothDamp(_currentDistance, safeTargetDist, ref _distVel, 0.06f);

        // 5. 最終カメラ位置の計算
        Vector3 targetPos = _currentPivot + currentRot * new Vector3(0f, 0f, -_currentDistance);
        targetPos.y = Mathf.Max(targetPos.y, target.position.y + 1.2f);

        // 地面めり込み防止
        if (_land == null)
            _land = AdventureQuestLocations.FindLand();
        if (_land != null)
        {
            float groundY = AdventureQuestLocations.GroundY(_land, targetPos.x, targetPos.z) + 0.85f;
            targetPos.y = Mathf.Max(targetPos.y, groundY);
        }

        // 位置の最終スムーズ追従（ブレを吸収しつつ遅れすぎない絶妙なバランス）
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _posVelocity, isGliding ? 0.04f : 0.018f);
        transform.rotation = currentRot;
    }

    float CalculateSafeDistance(Vector3 pivot, Quaternion rot, float maxDist)
    {
        Vector3 backDir = rot * Vector3.back;
        float castRadius = 0.32f;
        if (Physics.SphereCast(pivot, castRadius, backDir, out RaycastHit hit, maxDist, ~0, QueryTriggerInteraction.Ignore))
        {
            return Mathf.Clamp(hit.distance - 0.15f, 0.9f, maxDist);
        }
        return maxDist;
    }
}
