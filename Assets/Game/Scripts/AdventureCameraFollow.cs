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
    public float height = 1.65f;
    public float distance = 8.5f;
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

    /// <summary>歩行：昔の小さな島と同じ心地よい見下ろし俯瞰</summary>
    const float WalkPitch = 11.0f;
    /// <summary>歩行時の注視点（頭上〜全身が見晴らせる高さ）</summary>
    const float WalkFocusHeight = 1.25f;
    const float WalkPivotHeight = 1.65f;
    float WalkMinDistance => AdventureRustFloatFeel.IsActiveScene
        ? AdventureRustFloatFeel.WalkMinDistance
        : 6.8f;

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
    float _camGroundY;
    float _camGroundYVel;

    Terrain _land;
    Camera _cam;

    bool _cinematic;
    float _cinematicBlend;
    float _cinematicProgress;
    bool _islandOverview;
    float _islandOverviewBlend;
    float _overviewProgress;
    bool _canopyLookUp;
    float _canopyLookUpBlend;
    float _canopyLookUpProgress;
    /// <summary>クライマックス演出カメラのシーケンスフェーズ</summary>
    public enum ClimaxCameraPhase
    {
        None = 0,
        Chase = 1,       // 1: 柱抜け追走
        Approach = 2,    // 2: 接近
        OilFront = 3,    // 3: 正面注油
        SkyWide = 4      // 4: 大空の引き
    }

    private readonly struct ClimaxShotSetting
    {
        public readonly float yawOffset;
        public readonly float pitch;
        public readonly float distance;
        public readonly float fieldOfView;
        public readonly float sideOffset;

        public ClimaxShotSetting(float yawOffset, float pitch, float distance, float fieldOfView, float sideOffset)
        {
            this.yawOffset = yawOffset;
            this.pitch = pitch;
            this.distance = distance;
            this.fieldOfView = fieldOfView;
            this.sideOffset = sideOffset;
        }
    }

    float _skyWideSideBias;
    ClimaxCameraPhase _climaxCamPhase = ClimaxCameraPhase.None;
    float _climaxYawOff;
    float _climaxYawOffVel;
    float _climaxPitch;
    float _climaxPitchVel;
    float _climaxDist;
    float _climaxDistVel;
    float _climaxFov;
    float _climaxFovVel;
    bool _climaxCamSeeded;
    bool _titleClimaxSplash;
    Transform _savedFollowTarget;
    const float CinematicDistance = 7.4f;
    const float CinematicHeight = 1.8f;
    const float CinematicFov = 78f;
    const float CinematicBlendSpeed = 1.35f;

    /// <summary>3次ベジェ・完全ゼロショック（初速0・終速0・加速度連続）イージング関数</summary>
    public static float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }

    bool _lookUpdatedThisFrame;

    /// <summary>同一フレーム内で ApplyLookSmoothingForMove を二重実行しない</summary>
    bool _lookSmoothedThisFrame;

    float _shakeIntensity;
    float _shakeUntil;
    float _shakeSeed;

    /// <summary>接地フラグの瞬間切替で歩行↔空中カメラが揺れないようブレンド</summary>
    float _walkGroundBlend = 1f;
    float _walkGroundProgress = 1f;
    const float WalkPivotVerticalSmooth = 0.095f;
    const float WalkDistanceSmooth = 0.075f;

    public void SetCinematicMode(bool enabled, bool instant = false)
    {
        _cinematic = enabled;
        if (instant)
        {
            _cinematicProgress = enabled ? 1f : 0f;
            _cinematicBlend = _cinematicProgress;
        }
    }

    /// <summary>「全力で行こう!!」以降：Float・Rust・ウミネコと島・海が同時に入る広い大空ショット</summary>
    public void SetIslandOverviewMode(bool enabled, bool instant = false)
    {
        _islandOverview = enabled;
        if (enabled && Mathf.Abs(_skyWideSideBias) < 0.01f)
            _skyWideSideBias = Random.value > 0.5f ? 8f : -8f;
        if (instant)
        {
            _overviewProgress = enabled ? 1f : 0f;
            _islandOverviewBlend = _overviewProgress;
        }
    }

    /// <summary>「あれが本物の空だ……！」地上から上空の裂け目をダイナミックに見上げる仰角シネマティックカメラ</summary>
    public void SetCanopyLookUpMode(bool enabled, bool instant = false)
    {
        _canopyLookUp = enabled;
        if (instant)
        {
            _canopyLookUpProgress = enabled ? 1f : 0f;
            _canopyLookUpBlend = _canopyLookUpProgress;
        }
    }

    /// <summary>光の柱ダイブ開始時：FloatとRustが堂々と上昇するシネマティックカメラへ即座にアライメント</summary>
    public void SnapPillarAscentCam()
    {
        EnsureTarget();
        if (target == null) return;

        // 見上げモードを直ちに終了
        _canopyLookUp = false;
        _canopyLookUpProgress = 0f;
        _canopyLookUpBlend = 0f;

        // Floatの胸・頭部（高さ1.2m）を基準にカメラピボットを初期化
        _currentPivot = target.position + Vector3.up * 1.2f;

        float yaw = target.eulerAngles.y + 24f;
        _yaw = yaw;
        _targetYaw = yaw;
        _pitch = 18f;
        _targetPitch = 18f;

        const float dist = 6.2f;
        _currentDistance = dist;

        Quaternion rot = Quaternion.Euler(18f, yaw, 0f);
        transform.position = _currentPivot + rot * new Vector3(0.35f, 0.05f, -dist);
        transform.rotation = rot;
        if (_cam != null)
            _cam.fieldOfView = 68f;
    }

    /// <summary>柱抜け後の連続ショット。1追走 2接近 3正面注油 4大空。0で解除</summary>
    public void SetClimaxCinematicPhase(int phase)
    {
        SetClimaxCinematicPhase((ClimaxCameraPhase)phase);
    }

    public void SetClimaxCinematicPhase(ClimaxCameraPhase phase)
    {
        if (phase > ClimaxCameraPhase.None && _climaxCamPhase <= ClimaxCameraPhase.None)
            _climaxCamSeeded = false;
        _climaxCamPhase = phase;
        if (phase > ClimaxCameraPhase.None)
            SetIslandOverviewMode(false);
    }

    private ClimaxShotSetting GetClimaxShotSetting(ClimaxCameraPhase phase)
    {
        switch (phase)
        {
            case ClimaxCameraPhase.Approach:
                return new ClimaxShotSetting(86f, 9f, 9.6f, 72f, 0.45f);
            case ClimaxCameraPhase.OilFront:
                return new ClimaxShotSetting(180f, 7.5f, 9.2f, 70f, 0.2f);
            case ClimaxCameraPhase.SkyWide:
                if (Mathf.Abs(_skyWideSideBias) < 0.01f)
                    _skyWideSideBias = 14f;
                return new ClimaxShotSetting(180f + _skyWideSideBias, 8.5f, 15.2f, 76f, 0f);
            case ClimaxCameraPhase.Chase:
            default:
                return new ClimaxShotSetting(32f, 10f, 8.4f, 68f, 0.55f);
        }
    }

    /// <summary>タイトル待機：クライマックス広角と同じ構図でライブ背景</summary>
    public void EnableTitleClimaxSplash(bool enabled, Transform splashPivot)
    {
        _titleClimaxSplash = enabled;
        if (enabled && splashPivot != null)
        {
            EnsureTarget();
            if (_savedFollowTarget == null)
                _savedFollowTarget = target;
            target = splashPivot;

            _skyWideSideBias = 24f;
            SetCinematicMode(true, instant: true);
            SetIslandOverviewMode(true, instant: true);

            float pivotYaw = splashPivot.eulerAngles.y;
            float camYaw = pivotYaw + 180f + _skyWideSideBias;
            _pitch = 10f;
            _targetPitch = 10f;
            _yaw = camYaw;
            _targetYaw = camYaw;
            _currentPivot = splashPivot.position + Vector3.up * 4.4f;
            _currentDistance = 17.5f;
            if (_cam != null)
            {
                _cam.fieldOfView = 88f;
                _cam.farClipPlane = Mathf.Max(_cam.farClipPlane, 5000f);
            }
        }
        else
        {
            // 残留ブレンドを即時ゼロ化（カメラ無効中は減衰せず、再有効化後に回転暴走するため）
            SetIslandOverviewMode(false, instant: true);
            SetCinematicMode(false, instant: true);
            _skyWideSideBias = 0f;
            if (_savedFollowTarget != null)
                target = _savedFollowTarget;
            _savedFollowTarget = null;
        }
    }

    public bool IsTitleClimaxSplash => _titleClimaxSplash;

    /// <summary>地面・衝撃用のカメラ揺れ（天蓋裂開など）。intensity はワールドメートル相当。</summary>
    public void Shake(float intensity, float duration)
    {
        _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
        _shakeUntil = Mathf.Max(_shakeUntil, Time.unscaledTime + Mathf.Max(0.05f, duration));
        if (_shakeSeed < 0.01f)
            _shakeSeed = Random.Range(10f, 100f);
    }

    public bool IsCinematic => _cinematic;
    public bool IsIslandOverview => _islandOverview;

    static AdventureCameraFollow _instance;
    public static AdventureCameraFollow Instance => _instance != null ? _instance : (_instance = Object.FindAnyObjectByType<AdventureCameraFollow>());

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

    /// <summary>Rキー／リセット時／ダイブ発進時：標準後方カメラへ即復帰</summary>
    public void SnapBehindTarget()
    {
        SnapBehindTarget(WalkPitch);
    }

    /// <summary>指定ピッチ（見下ろし角度）で後方カメラへ即スナップ</summary>
    public void SnapBehindTarget(float targetPitchAngle)
    {
        if (target == null) return;
        SetIslandOverviewMode(false, instant: true);
        SetCinematicMode(false, instant: true);
        _skyWideSideBias = 0f;
        _yaw = target.eulerAngles.y;
        _targetYaw = _yaw;
        _pitch = targetPitchAngle;
        _targetPitch = targetPitchAngle;
        _yawVel = 0f;
        _pitchVel = 0f;
        _lastMouseInputTime = Time.time;
        _currentPivot = target.position + Vector3.up * WalkPivotHeight;
        _pivotVelocity = Vector3.zero;
        _currentDistance = distance;
        _distVel = 0f;
        _walkGroundBlend = 1f;
        _framedPitch = targetPitchAngle;
        CurrentYaw = _yaw;
    }

    void OnEnable()
    {
        AdventureStoryFlow.OnPhaseChanged += HandleStoryPhaseChanged;
    }

    void OnDisable()
    {
        AdventureStoryFlow.OnPhaseChanged -= HandleStoryPhaseChanged;
    }

    void HandleStoryPhaseChanged(AdventureStoryFlow.Phase prev, AdventureStoryFlow.Phase next)
    {
        if (next == AdventureStoryFlow.Phase.Climax || next == AdventureStoryFlow.Phase.Epilogue || next == AdventureStoryFlow.Phase.Clear)
        {
            SetCinematicMode(true);
        }
        else if (prev == AdventureStoryFlow.Phase.Clear && next == AdventureStoryFlow.Phase.FreeFlight)
        {
            SetCinematicMode(false);
        }

        if (AdventureStoryFlow.WantsFreeCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _hasPrevMousePos = false;
        }
        else
        {
            LockCursor();
        }
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

        if (target != null && AdventureRustFloatOpening.IsGameStarted)
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
        else
        {
            LockCursor();
        }
    }

    void LockCursor()
    {
        if (AdventureStoryFlow.WantsFreeCursor) return;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>パーツ取得・UI復帰直後など、探索視点操作を即座に有効化する</summary>
    public void RestoreGameplayLookLock()
    {
        if (AdventureStoryFlow.WantsFreeCursor || AdventurePauseMenu.IsOpen) return;
        if (!AdventureRustFloatOpening.IsGameStarted) return;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _hasPrevMousePos = false;
        _lastMouseInputTime = Time.time;
    }

    void EnsureTarget()
    {
        if (target != null) return;
        var player = AdventurePlayerController.InstanceOrFind();
        if (player != null)
        {
            target = player.transform;
            if (AdventureRustFloatOpening.IsGameStarted)
                SnapBehindTarget();
        }
    }

    static bool IsTitleModalBlocking()
    {
        var opening = AdventureRustFloatOpening.Instance;
        if (opening != null && opening.IsModalBoardOpen())
            return true;
        return false;
    }

    void Update()
    {
        _lookUpdatedThisFrame = false;
        _lookSmoothedThisFrame = false;
        if (IsTitleModalBlocking()) return;
        EnsureTarget();
        if (target == null) return;
        AdventureGameplayInputGate.TryRestoreExplorationLookIfReady();
        UpdateLookInputOnly();
        ApplyLookSmoothingForMove();
    }

    void LateUpdate()
    {
        if (IsTitleModalBlocking() && !_titleClimaxSplash) return;
        EnsureTarget();
        if (target == null) return;

        if (AdventureScrapManager.IsWithinPickupInputGrace)
            AdventureGameplayInputGate.TryRestoreExplorationLookIfReady();

        // Update を飛ばしたフレームだけ入力＋ヨー/Pitch を再計算（SmoothDamp の二重適用はしない）
        if (!_lookUpdatedThisFrame)
            UpdateLookInputOnly();
        if (!_lookSmoothedThisFrame)
            ApplyLookSmoothingForMove();

        ApplyCameraRig();
        _lookUpdatedThisFrame = false;
        _lookSmoothedThisFrame = false;
    }

    Vector2 _prevMouseScreenPos;
    bool _hasPrevMousePos;
    CursorLockMode _prevLockMode = CursorLockMode.None;

    void UpdateLookInputOnly()
    {
        var kb = AdventureInputReader.Keyboard;
        var mouse = Mouse.current;

        // ポーズ中はカメラ回転入力を受け付けない
        if (AdventurePauseMenu.IsOpen)
        {
            _hasPrevMousePos = false;
            return;
        }

        if (AdventureScrapManager.IsWithinPickupInputGrace
            && AdventureRustFloatOpening.IsGameStarted
            && !AdventureStoryFlow.WantsFreeCursor)
            LockCursor();

        bool isPointerOverUi = mouse != null
            && UnityEngine.EventSystems.EventSystem.current != null
            && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        bool canExploreLook = AdventureRustFloatOpening.IsGameStarted && !AdventureStoryFlow.WantsFreeCursor && !IsTitleModalBlocking();

        // 探索中：クリック／マウス移動でカーソルをロック（解除残留で視点が効かない問題を防ぐ）
        if (mouse != null && canExploreLook && Cursor.lockState != CursorLockMode.Locked)
        {
            Vector2 preDelta = mouse.delta.ReadValue();
            bool pointerDown = mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame;
            bool moved = preDelta.sqrMagnitude > 0.001f;
            if (!isPointerOverUi && (pointerDown || moved))
            {
                LockCursor();
                _hasPrevMousePos = false;
                _lastMouseInputTime = Time.time;
            }
        }

        // UIモーダル操作中（カーソルが解放されている状態）はカメラ回転入力を抑制
        if (AdventureStoryFlow.WantsFreeCursor && Cursor.lockState != CursorLockMode.Locked)
        {
            _hasPrevMousePos = false;
            return;
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

        // カーソルロック状態が変化した瞬間は、画面中央への強制移動による座標ジャンプを防ぐため履歴を破棄
        if (Cursor.lockState != _prevLockMode)
        {
            _prevLockMode = Cursor.lockState;
            _hasPrevMousePos = false;
            // ロック解除直後だけ1フレーム飛ばす（中央ジャンプ防止）。ロック時はそのまま視点入力を受ける
            if (Cursor.lockState != CursorLockMode.Locked)
                return;
        }

        // ── マウス移動量の高信頼取得 ──
        float mouseX = 0f;
        float mouseY = 0f;

        if (mouse != null)
        {
            // 系統1: delta による取得（最も安全かつ標準的）
            Vector2 delta = mouse.delta.ReadValue();
            mouseX = delta.x;
            mouseY = delta.y;

            // 系統2: カーソル解放中（非ロック時）のみスクリーン座標差分を補助利用
            // ※ロック中はOSがカーソルを画面中央へ引き戻すため、差分を見ると巨大な跳ね上がりが発生する
            // 探索中は取得クリック直後など delta が 0 のままになることがあるため、差分を優先
            if (Cursor.lockState != CursorLockMode.Locked && !AdventureStoryFlow.WantsFreeCursor)
            {
                Vector2 curScreenPos = mouse.position.ReadValue();
                if (_hasPrevMousePos)
                {
                    Vector2 diff = curScreenPos - _prevMouseScreenPos;
                    if (diff.sqrMagnitude > 0.0001f && diff.sqrMagnitude < 25000f
                        && Mathf.Abs(mouseX) < 0.15f && Mathf.Abs(mouseY) < 0.15f)
                    {
                        mouseX = diff.x;
                        mouseY = diff.y;
                    }
                }
                _prevMouseScreenPos = curScreenPos;
                _hasPrevMousePos = true;
            }
            else if (Cursor.lockState == CursorLockMode.Locked)
            {
                _hasPrevMousePos = false;
            }
        }

        // 系統3: レガシー Input フォールバック（適正スケール）
        bool pickupGrace = AdventureScrapManager.IsWithinPickupInputGrace;
        if (pickupGrace || (Mathf.Abs(mouseX) < 0.001f && Mathf.Abs(mouseY) < 0.001f))
        {
            try
            {
                float lx = Input.GetAxisRaw("Mouse X");
                float ly = Input.GetAxisRaw("Mouse Y");
                if (Mathf.Abs(lx) > 0.001f || Mathf.Abs(ly) > 0.001f)
                {
                    mouseX = lx * 2.0f;
                    mouseY = ly * 2.0f;
                }
            }
            catch { }
        }

        // 1フレームあたりのマウス入力変化量を安全に制限（急激な跳ね上がり・天井張り付きを物理的に防止）
        float maxStep = 18f;
        float deltaYaw = Mathf.Clamp(mouseX * sensitivity, -maxStep, maxStep);
        float deltaPitch = Mathf.Clamp(mouseY * sensitivity, -maxStep, maxStep);

        // カメラ回転へ即時反映
        if (Mathf.Abs(deltaYaw) > 0.0001f || Mathf.Abs(deltaPitch) > 0.0001f)
        {
            _targetYaw += deltaYaw;
            _targetPitch = Mathf.Clamp(_targetPitch - deltaPitch, pitchMin, pitchMax);
            _lastMouseInputTime = Time.time;
        }

        // ゲームパッド対応
        var pad = Gamepad.current;
        if (pad != null)
        {
            Vector2 rStick = pad.rightStick.ReadValue();
            if (rStick.sqrMagnitude > 0.04f)
            {
                float padMul = AdventureSceneContext.IsRustFloat ? AdventureRustFloatFeel.PadLookMul : 160f;
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

    bool IsCursorLookActive()
    {
        // 探索中はカーソルロックが一瞬外れても即応視点（取得クリック直後の「動かない」防止）
        return !AdventureStoryFlow.WantsFreeCursor;
    }

    void UpdateWalkGroundBlend(bool playerGrounded)
    {
        if (playerGrounded)
            _walkGroundProgress = Mathf.MoveTowards(_walkGroundProgress, 1f, Time.deltaTime / 0.09f);
        else
            _walkGroundProgress = Mathf.MoveTowards(_walkGroundProgress, 0f, Time.deltaTime / 0.12f);

        _walkGroundBlend = Mathf.SmoothStep(0f, 1f, _walkGroundProgress);
    }

    /// <summary>歩行カメラ判定（ApplyCameraRig と ApplyLookSmoothing で同一基準）</summary>
    static bool QueryWalkGroundCamera(bool isGliding, bool isAutoGlide, float cine, bool playerMoving, float walkGroundBlend, out bool inAir)
    {
        inAir = !isGliding && !isAutoGlide && walkGroundBlend < 0.32f;
        return !isGliding && !isAutoGlide && cine < 0.05f
               && walkGroundBlend > (playerMoving ? 0.38f : 0.52f);
    }

    /// <summary>プレイヤー入力中は常に即時。スムーズはエピローグ自動滑空のドローンショットのみ。</summary>
    bool ShouldUseInstantLook(bool isAutoGlide, float cine, bool walkingGround, bool cursorLook)
    {
        if (_islandOverview || _islandOverviewBlend > 0.01f) return false; // エピローグ概要遷移時は常に極上のスムーズ補間
        if (_canopyLookUp || _canopyLookUpBlend > 0.01f) return false;     // 天蓋見上げシネマティック遷移時もスムーズ補間
        if (walkingGround || cine < 0.02f) return true;
        if (cursorLook)
        {
            bool epilogueDrone = isAutoGlide && cine > 0.25f;
            if (!epilogueDrone) return true;
            if (Time.time - _lastMouseInputTime < 0.45f) return true;
            return false;
        }

        return Time.time - _lastMouseInputTime < 0.35f;
    }

    void ApplyLookSmoothingForMove()
    {
        if (_lookSmoothedThisFrame) return;
        _lookSmoothedThisFrame = true;

        var player = AdventurePlayerController.Instance;
        bool isGliding = player != null && player.IsGliding;
        bool isAutoGlide = player != null && player.IsAutoGliding;
        bool playerGrounded = player != null && player.IsGrounded;
        bool playerMoving = player != null && player.HasMoveInput;
        bool cursorLook = IsCursorLookActive();

        if (playerGrounded && !isAutoGlide && _cinematic && !AdventureStoryFlow.HoldCinematicCamera)
            SetCinematicMode(false);

        float blendTarget = _cinematic ? 1f : 0f;
        _cinematicProgress = Mathf.MoveTowards(_cinematicProgress, blendTarget, CinematicBlendSpeed * Time.unscaledDeltaTime);
        _cinematicBlend = EaseInOutCubic(_cinematicProgress);
        float cine = _cinematicBlend;

        UpdateWalkGroundBlend(playerGrounded);
        bool walkingGround = QueryWalkGroundCamera(isGliding, isAutoGlide, cine, playerMoving, _walkGroundBlend, out _);

        float followIdle = cine > 0.2f || isAutoGlide ? 0.35f : 1.2f;
        float followRate = cine > 0.2f || isAutoGlide ? 55f : 36f;

        bool hasGlideSteer = isGliding && Mathf.Abs(AdventureInputReader.MoveAxis.x) > 0.05f;
        float deltaToHeading = target != null ? Mathf.Abs(Mathf.DeltaAngle(_targetYaw, target.eulerAngles.y)) : 0f;
        bool recentLookInput = Time.time - _lastMouseInputTime < followIdle + 0.35f;
        bool shouldAutoFollow = !recentLookInput && (hasGlideSteer || deltaToHeading > 15f);
        if ((isGliding || isAutoGlide) && shouldAutoFollow)
        {
            float targetHeading = target.eulerAngles.y;
            float dt = Time.unscaledDeltaTime;
            float currentFollowRate = hasGlideSteer ? 85f : followRate;
            if (isAutoGlide && cine > 0.3f)
            {
                float driftYaw = 14f + Mathf.Sin(Time.unscaledTime * 0.16f) * 8f;
                float driftPitch = WalkPitch + 3.4f + Mathf.Sin(Time.unscaledTime * 0.11f) * 1.6f;
                _targetYaw = Mathf.MoveTowardsAngle(_targetYaw, targetHeading + driftYaw, currentFollowRate * 0.85f * dt);
                _targetPitch = Mathf.MoveTowards(_targetPitch, driftPitch, 14f * dt);
            }
            else
            {
                _targetYaw = Mathf.MoveTowardsAngle(_targetYaw, targetHeading, currentFollowRate * dt);
                if (cine > 0.01f)
                    _targetPitch = Mathf.MoveTowards(_targetPitch, WalkPitch, 24f * dt);
            }
        }

        if (_islandOverviewBlend > 0.02f && _climaxCamPhase <= ClimaxCameraPhase.None && target != null)
        {
            float playerYaw = target.eulerAngles.y;
            float sway = Mathf.Sin(Time.unscaledTime * 0.12f) * 2.5f;
            // 正面から二人を捉える映画的フロントアングル（水平目線ピッチ4.5度）
            _targetYaw = playerYaw + 180f + _skyWideSideBias + sway;
            _targetPitch = Mathf.Lerp(_targetPitch, 4.5f, _islandOverviewBlend);
        }
        else if (_canopyLookUpBlend > 0.02f && _climaxCamPhase <= ClimaxCameraPhase.None && target != null)
        {
            float playerYaw = target.eulerAngles.y;
            _targetYaw = playerYaw;
            _targetPitch = Mathf.Lerp(_targetPitch, -35f, _canopyLookUpBlend);
        }
        else if (cine > 0.4f && !isAutoGlide && !cursorLook)
            _targetPitch = Mathf.Clamp(_targetPitch, -6f, 22f);

        if (ShouldUseInstantLook(isAutoGlide, cine, walkingGround, cursorLook))
        {
            _yaw = _targetYaw;
            _pitch = _targetPitch;
            _yawVel = 0f;
            _pitchVel = 0f;
        }
        else
        {
            float lookSmooth = Mathf.Max(0.04f, lookSmoothTime * 8f);
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
        float overviewTarget = _islandOverview ? 1f : 0f;
        _overviewProgress = Mathf.MoveTowards(
            _overviewProgress, overviewTarget, Time.unscaledDeltaTime * 0.48f);
        _islandOverviewBlend = EaseInOutCubic(_overviewProgress);

        float canopyTarget = _canopyLookUp ? 1f : 0f;
        _canopyLookUpProgress = Mathf.MoveTowards(
            _canopyLookUpProgress, canopyTarget, Time.unscaledDeltaTime * 0.95f);
        _canopyLookUpBlend = EaseInOutCubic(_canopyLookUpProgress);

        var player = AdventurePlayerController.InstanceOrFind();
        bool isGliding = player != null && player.IsGliding;
        bool isAutoGlide = player != null && player.IsAutoGliding;
        bool playerMoving = player != null && player.HasMoveInput;
        float cine = _cinematicBlend;

        bool walkingGround = QueryWalkGroundCamera(isGliding, isAutoGlide, cine, playerMoving, _walkGroundBlend, out bool inAir);

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

        if (_climaxCamPhase <= ClimaxCameraPhase.None && Vector3.Distance(_currentPivot, targetPivot) > 12f)
        {
            _currentPivot = targetPivot;
            _pivotVelocity = Vector3.zero;
            _currentDistance = Mathf.Max(distance, WalkMinDistance);
            _distVel = 0f;
        }

        // 歩行：XZは即応、Yだけ斜面の微細な上下を吸収
        if (walkingGround)
        {
            float smoothY = Mathf.SmoothDamp(
                _currentPivot.y, targetPivot.y, ref _pivotVelocity.y,
                WalkPivotVerticalSmooth, Mathf.Infinity, Time.deltaTime);
            _currentPivot = new Vector3(targetPivot.x, smoothY, targetPivot.z);
            _pivotVelocity.x = 0f;
            _pivotVelocity.z = 0f;
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

        bool isOilPhase = false;
        var towerMgr = AdventureSanctuaryTowerManager.Instance;
        if (towerMgr != null && towerMgr.NeedsClimaxSkyAltitude)
        {
            var drone = AdventureRustDrone.Instance;
            if (drone == null || !drone.IsClimaxOverdrive)
                isOilPhase = true;
        }

        float walkBaseFov = AdventureRustFloatFeel.IsActiveScene ? AdventureRustFloatFeel.WalkFov : 64f;
        float glideBaseFov = walkBaseFov + 4f;
        float cineFov = (isAutoGlide && cine > 0.3f) ? 74f : CinematicFov;
        if (_islandOverviewBlend > 0.01f)
        {
            float targetFovVal = isOilPhase ? 60f : 71f;
            cineFov = Mathf.Lerp(cineFov, targetFovVal, _islandOverviewBlend);
        }
        float targetFov = Mathf.Lerp(isGliding ? glideBaseFov : walkBaseFov, cineFov, cine);

        // 70mメガジャンプ・急上昇・高速滑空ブースト・急斜面スライディングによるダイナミックFOV（最大82°までワイド化）
        if (player != null && (player.SpeedIntensity > 0.05f || player.IsSliding))
        {
            float slideBoost = player.IsSliding ? Mathf.Lerp(2.0f, 6.5f, player.HorizontalSpeed / 18.0f) : 0f;
            float speedBoostFov = player.SpeedIntensity * (player.IsUpdraftAscending ? 15.0f : 10.0f) + slideBoost;
            targetFov = Mathf.Min(82f, targetFov + speedBoostFov);
        }

        if (_cam != null && _climaxCamPhase <= ClimaxCameraPhase.None)
        {
            float fovSpeed = (cine > 0.01f || (player != null && player.IsUpdraftAscending)) ? 26f : 10f;
            _cam.fieldOfView = Mathf.MoveTowards(_cam.fieldOfView, targetFov, fovSpeed * Time.deltaTime);
        }

        float cineDist = (isAutoGlide && cine > 0.3f) ? 8.5f : CinematicDistance;
        if (_islandOverviewBlend > 0.01f)
        {
            float targetDistVal = isOilPhase ? 4.5f : 16.5f;
            cineDist = Mathf.Lerp(cineDist, targetDistVal, _islandOverviewBlend);
        }
        float speedExtraDist = (player != null && player.IsUpdraftAscending) ? (player.SpeedIntensity * 1.6f) : 0f;
        float desiredDist = Mathf.Lerp(isGliding ? (distance + 1.2f) : distance, cineDist, cine) + speedExtraDist;
        
        // 上空（高度80m以上またはオートグライド中）は障害物がないため、天蓋破片等の誤遮蔽SphereCastをバイパスして安定追従
        float safeTargetDist;
        if (isAutoGlide || (target != null && target.position.y > 80f))
        {
            safeTargetDist = desiredDist;
        }
        else
        {
            safeTargetDist = CalculateSafeDistance(_currentPivot, currentRot, desiredDist);
            if (cine > 0.2f)
                safeTargetDist = Mathf.Max(safeTargetDist, Mathf.Lerp(0.9f, 3.2f, cine));
            if (walkingGround)
                safeTargetDist = Mathf.Max(safeTargetDist, WalkMinDistance);
        }

        if (walkingGround)
        {
            _currentDistance = Mathf.SmoothDamp(
                _currentDistance, safeTargetDist, ref _distVel,
                WalkDistanceSmooth, Mathf.Infinity, Time.deltaTime);
        }
        else
        {
            float distSmooth = cine > 0.2f ? 0.12f : 0.08f;
            _currentDistance = Mathf.SmoothDamp(_currentDistance, safeTargetDist, ref _distVel, distSmooth, Mathf.Infinity, Time.unscaledDeltaTime);
        }

        Vector3 targetPos = _currentPivot + currentRot * new Vector3(0f, 0f, -_currentDistance);
        bool pillarFeetCam = _climaxCamPhase <= ClimaxCameraPhase.None && player != null && player.IsSkybreakPillarAscending;
        if (pillarFeetCam && target != null)
        {
            float dt = Time.unscaledDeltaTime;
            // FloatとRustの二人を美しく捉える斜め後方〜サイドのアングル
            float yaw = target.eulerAngles.y + 24f + Mathf.Sin(Time.unscaledTime * 0.35f) * 2f;
            currentRot = Quaternion.Euler(18f, yaw, 0f);
            // ピボットを足元(0.85m)から胸・頭部(1.20m)へ引き上げ、FloatとRust（頭部右斜め後ろ〜横で羽ばたく相棒）を黄金比でフレームに収める
            Vector3 pivot = target.position + Vector3.up * 1.20f;
            _currentPivot = Vector3.Lerp(_currentPivot, pivot, 1f - Mathf.Exp(-9f * dt));
            const float dist = 6.2f;
            targetPos = _currentPivot + currentRot * new Vector3(0.35f, 0.05f, -dist);
            _currentDistance = dist;
            if (_cam != null)
                _cam.fieldOfView = Mathf.MoveTowards(_cam.fieldOfView, 68f, 22f * dt);
        }
        else if (_climaxCamPhase > ClimaxCameraPhase.None && target != null)
        {
            ApplyClimaxCamera(ref currentRot, ref targetPos, Time.unscaledDeltaTime);
        }
        else if (_islandOverviewBlend > 0.01f && target != null)
        {
            float b = _islandOverviewBlend;
            float smoothB = EaseInOutCubic(b);

            // 目標ピボット：Floatの胸元（高さ1.8m）
            Vector3 pivot = target.position + Vector3.up * 1.80f;
            // ブレンド完了時は100%完全にピボット追従（遅れゼロで前進速度を完全同期）
            _currentPivot = Vector3.Lerp(_currentPivot, pivot, Mathf.Lerp(0.35f, 1.0f, smoothB));

            float playerYaw = target.eulerAngles.y;
            float sway = Mathf.Sin(Time.unscaledTime * 0.12f) * 2.5f;
            float camYaw = playerYaw + 180f + _skyWideSideBias + sway;
            // 正面からまっすぐ二人を捉える映画的フロントアングル（ピッチ 4.5度）
            float camPitch = Mathf.Lerp(6f, 4.5f, smoothB);
            float dist = Mathf.Lerp(10f, 13.5f, smoothB);

            // Slerpで滑らかに回転補間
            currentRot = Quaternion.Slerp(currentRot, Quaternion.Euler(camPitch, camYaw, 0f), smoothB);

            // 【完全同期】位置計算にもSlerp後のcurrentRotを適用し、位置と回転の脱臼・ねじれ・ガクつきを根絶！
            targetPos = _currentPivot + currentRot * new Vector3(0f, 0.15f * smoothB, -dist);
            _currentDistance = dist;
        }
        else if (_canopyLookUpBlend > 0.01f && target != null)
        {
            float b = _canopyLookUpBlend;
            float smoothB = EaseInOutCubic(b);

            // プレイヤー（Float & Niko）の背後・高さ2.2mから前上方の大空・光の柱を見上げるアングル
            Vector3 playerPos = target.position;
            Vector3 forwardFlat = target.forward;
            forwardFlat.y = 0f;
            if (forwardFlat.sqrMagnitude < 0.01f) forwardFlat = Vector3.forward;
            forwardFlat.Normalize();

            // プレイヤーの後方7.0m、高さ2.4mにカメラを安全に配置（床埋没を完全に防止）
            Vector3 safeCamPos = playerPos - forwardFlat * 7.0f + Vector3.up * 2.4f;
            float minSafeY = Mathf.Max(playerPos.y + 1.2f, 65.5f);
            if (safeCamPos.y < minSafeY) safeCamPos.y = minSafeY;

            // 上空の割れ目・光の柱中心（高度140m〜150m）を堂々と見上げる回転
            Vector3 skyLookTarget = new Vector3(512f, 140f, 512f);
            Quaternion skyRot = Quaternion.LookRotation(skyLookTarget - safeCamPos, Vector3.up);

            _currentPivot = Vector3.Lerp(_currentPivot, playerPos + Vector3.up * 1.5f, 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime));
            currentRot = Quaternion.Slerp(currentRot, skyRot, smoothB);
            targetPos = Vector3.Lerp(targetPos, safeCamPos, smoothB);
            _currentDistance = 7.0f;
            if (_cam != null)
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, 80f, smoothB);
        }

        if (!inAir && _islandOverviewBlend < 0.25f && _canopyLookUpBlend < 0.25f)
        {
            float minCamY = Mathf.Lerp(1.15f, 1.35f, cine);
            targetPos.y = Mathf.Max(targetPos.y, target.position.y + minCamY);
        }

        if (_land == null)
            _land = AdventureQuestLocations.FindLand();
        if (_land != null && _islandOverviewBlend < 0.35f && _canopyLookUpBlend < 0.35f)
        {
            float groundY = AdventureQuestLocations.GroundY(_land, targetPos.x, targetPos.z) + 0.85f;
            float groundSmooth = walkingGround ? 0.14f : 0.06f;
            if (targetPos.y < groundY - 0.015f)
            {
                if (_camGroundY < 0.1f) _camGroundY = groundY;
                _camGroundY = Mathf.SmoothDamp(_camGroundY, groundY, ref _camGroundYVel, groundSmooth);
                targetPos.y = Mathf.Max(targetPos.y, _camGroundY);
            }
            else
            {
                _camGroundY = Mathf.SmoothDamp(_camGroundY, targetPos.y, ref _camGroundYVel, groundSmooth);
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

    void ApplyClimaxCamera(ref Quaternion currentRot, ref Vector3 targetPos, float dt)
    {
        var setting = GetClimaxShotSetting(_climaxCamPhase);

        if (!_climaxCamSeeded)
        {
            _climaxYawOff = setting.yawOffset;
            _climaxPitch = _pitch;
            _climaxDist = _currentDistance;
            _climaxFov = _cam != null ? _cam.fieldOfView : setting.fieldOfView;
            _climaxYawOffVel = _climaxPitchVel = _climaxDistVel = _climaxFovVel = 0f;
            _climaxCamSeeded = true;
        }

        _climaxYawOff = Mathf.SmoothDampAngle(_climaxYawOff, setting.yawOffset, ref _climaxYawOffVel, 1.15f, Mathf.Infinity, dt);
        _climaxPitch = Mathf.SmoothDamp(_climaxPitch, setting.pitch, ref _climaxPitchVel, 0.9f, Mathf.Infinity, dt);
        _climaxDist = Mathf.SmoothDamp(_climaxDist, setting.distance, ref _climaxDistVel, 1.05f, Mathf.Infinity, dt);
        _climaxFov = Mathf.SmoothDamp(_climaxFov, setting.fieldOfView, ref _climaxFovVel, 0.85f, Mathf.Infinity, dt);
        if (_cam != null)
            _cam.fieldOfView = _climaxFov;

        float sway = Mathf.Sin(Time.unscaledTime * 0.18f) * 1.4f;
        float camYaw = target.eulerAngles.y + _climaxYawOff + sway;
        currentRot = Quaternion.Euler(_climaxPitch, camYaw, 0f);
        Vector3 pivot = target.position + Vector3.up * 1.65f;
        _currentPivot = Vector3.Lerp(_currentPivot, pivot, 1f - Mathf.Exp(-2.2f * dt));
        targetPos = _currentPivot + currentRot * new Vector3(setting.sideOffset, 0.12f, -_climaxDist);
        _currentDistance = _climaxDist;
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
