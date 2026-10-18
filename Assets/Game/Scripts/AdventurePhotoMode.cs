using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 案C：旅のシネマティック「フォトモード」（Photo Mode）。
/// 【Pキー】でいつでも起動。ゲーム内HUDを全非表示にし、スーパースロー（0.05x）で
/// NikoやFloat、大自然の風景を自由なアングル・画角（FOV）で撮影可能。
/// </summary>
[DefaultExecutionOrder(-150)]
public class AdventurePhotoMode : MonoBehaviour
{
    public static AdventurePhotoMode Instance { get; private set; }
    public static bool IsActive { get; private set; }

    [Header("Photo Camera Settings")]
    public float moveSpeed = 6.0f;
    public float rotateSensitivity = 0.24f;
    public float minFov = 20.0f;
    public float maxFov = 85.0f;

    // フォトモード用カメラオフセット＆アングル
    float _photoYaw;
    float _photoPitch = 12.0f;
    float _photoDistance = 5.5f;
    float _photoFov = 60.0f;
    Vector3 _photoPivotOffset = Vector3.zero;

    // 元の状態のバックアップ
    float _prevFov = 60.0f;
    float _prevTimeScale = 1.0f;
    Dictionary<Canvas, bool> _canvasVisibilityBackup = new Dictionary<Canvas, bool>();

    // UI・撮影演出
    bool _isCapturing = false;
    float _flashAlpha = 0f;
    float _toastTimer = 0f;
    string _toastMessage = "";
    float _lastInputTime = 0f;
    AudioSource _audioSource;
    AudioClip _shutterClip;

    Camera _cam;
    Vector2 _prevMousePos;
    bool _isDragging;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        if (Instance == null)
        {
            var go = new GameObject("AdventurePhotoMode");
            go.AddComponent<AdventurePhotoMode>();
            DontDestroyOnLoad(go);
        }
    }

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f; // 2D音響
        _audioSource.playOnAwake = false;
        _shutterClip = SynthesizeShutterSound();
    }

    void OnDestroy()
    {
        if (IsActive)
            ExitPhotoMode();
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        _cam = Camera.main;
        if (_cam != null)
            _prevFov = _cam.fieldOfView;
    }

    void Update()
    {
        // Pキーによるフォトモードのトグル
        bool pPressed = false;
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
            pPressed = true;
#if ENABLE_LEGACY_INPUT_MANAGER
        else if (Input.GetKeyDown(KeyCode.P))
            pPressed = true;
#endif

        if (pPressed)
        {
            // オープニングボードや会話中などの入力ガード中は起動不可
            var opening = AdventureRustFloatOpening.Instance;
            bool isGuarded = (opening != null && (opening.IsModalBoardOpen() || AdventureRustFloatOpening.IsInputGuarded));
            if (!isGuarded)
            {
                if (IsActive)
                    ExitPhotoMode();
                else
                    EnterPhotoMode();
            }
        }

        if (!IsActive) return;

        UpdatePhotoControls();
        UpdatePhotoAnimations();
    }

    /// <summary>フォトモード起動</summary>
    public void EnterPhotoMode()
    {
        if (IsActive) return;
        IsActive = true;
        _lastInputTime = Time.realtimeSinceStartup;

        if (_cam == null) _cam = Camera.main;
        if (_cam != null)
        {
            _prevFov = _cam.fieldOfView;
            _photoFov = Mathf.Clamp(_prevFov, minFov, maxFov);
        }

        var follow = AdventureCameraFollow.Instance;
        if (follow != null)
        {
            _photoYaw = follow.CurrentYaw;
            _photoDistance = Mathf.Clamp(follow.distance * 0.85f, 2.5f, 15f);
            if (follow.target != null)
                _photoPivotOffset = follow.target.position + Vector3.up * 1.35f;
            else
                _photoPivotOffset = follow.transform.position;
        }

        // 時間を0.05倍速のスーパースローに（浮遊感と光のチリが微かに煌めく映画的フリーズ）
        _prevTimeScale = Time.timeScale;
        Time.timeScale = 0.05f;

        // 全HUD Canvasを非表示化（キャッシュして安全に退避）
        _canvasVisibilityBackup.Clear();
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            if (c != null && c.gameObject.activeInHierarchy)
            {
                _canvasVisibilityBackup[c] = c.enabled;
                c.enabled = false;
            }
        }

        // マウスカーソル解放
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        _toastMessage = "📸 フォトモード起動 [Spaceで撮影]";
        _toastTimer = 2.0f;
    }

    /// <summary>フォトモード終了</summary>
    public void ExitPhotoMode()
    {
        if (!IsActive) return;
        IsActive = false;

        // 時間スケール復元
        Time.timeScale = (_prevTimeScale > 0.01f) ? _prevTimeScale : 1.0f;

        // Canvas復元
        foreach (var pair in _canvasVisibilityBackup)
        {
            if (pair.Key != null)
                pair.Key.enabled = pair.Value;
        }
        _canvasVisibilityBackup.Clear();

        // 画角復元
        if (_cam != null)
            _cam.fieldOfView = _prevFov;

        // カーソル再ロック
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>フォトモード中の自由カメラ移動・回転・ズーム操作</summary>
    void UpdatePhotoControls()
    {
        float dt = Time.unscaledDeltaTime; // timeScale=0.05の影響を受けないunscaledDeltaTimeを使用
        Vector2 mousePos = Vector2.zero;
        bool leftDown = false;
        bool rightDown = false;

        if (Mouse.current != null)
        {
            mousePos = Mouse.current.position.ReadValue();
            leftDown = Mouse.current.leftButton.isPressed;
            rightDown = Mouse.current.rightButton.isPressed;
        }
#if ENABLE_LEGACY_INPUT_MANAGER
        else
        {
            mousePos = Input.mousePosition;
            leftDown = Input.GetMouseButton(0);
            rightDown = Input.GetMouseButton(1);
        }
#endif

        // ドラッグによるカメラ角度（Yaw/Pitch）操作
        bool draggingNow = (leftDown || rightDown);
        if (draggingNow)
        {
            if (_isDragging)
            {
                Vector2 delta = mousePos - _prevMousePos;
                if (delta.sqrMagnitude > 0.01f)
                {
                    _photoYaw += delta.x * rotateSensitivity;
                    _photoPitch -= delta.y * rotateSensitivity;
                    _photoPitch = Mathf.Clamp(_photoPitch, -65f, 75f);
                    _lastInputTime = Time.realtimeSinceStartup;
                }
            }
            _prevMousePos = mousePos;
            _isDragging = true;
        }
        else
        {
            _isDragging = false;
        }

        // マウスホイールによるFOVズーム調整
        float scroll = 0f;
        if (Mouse.current != null)
            scroll = Mouse.current.scroll.ReadValue().y;
#if ENABLE_LEGACY_INPUT_MANAGER
        else
            scroll = Input.GetAxis("Mouse ScrollWheel");
#endif

        if (Mathf.Abs(scroll) > 0.01f)
        {
            _photoFov -= Mathf.Sign(scroll) * 4.0f;
            _photoFov = Mathf.Clamp(_photoFov, minFov, maxFov);
            _lastInputTime = Time.realtimeSinceStartup;
        }

        // キーボードによるPivot平行移動（WASD & QE）
        Vector3 moveInput = Vector3.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) moveInput += Vector3.forward;
            if (Keyboard.current.sKey.isPressed) moveInput += Vector3.back;
            if (Keyboard.current.aKey.isPressed) moveInput += Vector3.left;
            if (Keyboard.current.dKey.isPressed) moveInput += Vector3.right;
            if (Keyboard.current.eKey.isPressed) moveInput += Vector3.up;
            if (Keyboard.current.qKey.isPressed) moveInput += Vector3.down;
        }
#if ENABLE_LEGACY_INPUT_MANAGER
        else
        {
            if (Input.GetKey(KeyCode.W)) moveInput += Vector3.forward;
            if (Input.GetKey(KeyCode.S)) moveInput += Vector3.back;
            if (Input.GetKey(KeyCode.A)) moveInput += Vector3.left;
            if (Input.GetKey(KeyCode.D)) moveInput += Vector3.right;
            if (Input.GetKey(KeyCode.E)) moveInput += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) moveInput += Vector3.down;
        }
#endif

        if (moveInput.sqrMagnitude > 0.01f)
        {
            Quaternion camRot = Quaternion.Euler(0f, _photoYaw, 0f);
            Vector3 worldMove = camRot * new Vector3(moveInput.x, 0f, moveInput.z) + Vector3.up * moveInput.y;
            _photoPivotOffset += worldMove.normalized * (moveSpeed * dt);
            _lastInputTime = Time.realtimeSinceStartup;
        }

        // カメラ位置・回転・FOVの適用
        if (_cam != null)
        {
            Quaternion rot = Quaternion.Euler(_photoPitch, _photoYaw, 0f);
            Vector3 camPos = _photoPivotOffset - (rot * Vector3.forward * _photoDistance);
            _cam.transform.position = camPos;
            _cam.transform.rotation = rot;
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, _photoFov, dt * 12f);
        }

        // SpaceキーまたはEnterキーでシャッター撮影
        bool snapPressed = false;
        if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
            snapPressed = true;
#if ENABLE_LEGACY_INPUT_MANAGER
        else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            snapPressed = true;
#endif

        if (snapPressed && !_isCapturing)
        {
            StartCoroutine(CapturePhotoRoutine());
        }
    }

    void UpdatePhotoAnimations()
    {
        float dt = Time.unscaledDeltaTime;
        if (_flashAlpha > 0f)
            _flashAlpha = Mathf.Max(0f, _flashAlpha - dt * 3.5f);

        if (_toastTimer > 0f)
            _toastTimer = Mathf.Max(0f, _toastTimer - dt);
    }

    /// <summary>シャッターを切ってスクリーンショットを保存するコルーチン</summary>
    IEnumerator CapturePhotoRoutine()
    {
        _isCapturing = true;

        // シャッター音再生
        if (_audioSource != null && _shutterClip != null)
            _audioSource.PlayOneShot(_shutterClip, 0.95f);

        // 白フラッシュ開始
        _flashAlpha = 0.92f;

        // ガイドUIを完全に非表示にして1フレーム待機
        yield return new WaitForEndOfFrame();

        // スクリーンショット保存パス構築
        string dir = Path.Combine(Application.persistentDataPath, "Screenshots");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string fileName = $"RustAndFloat_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        string filePath = Path.Combine(dir, fileName);

        ScreenCapture.CaptureScreenshot(filePath);

        _toastMessage = $"✨ 撮影しました: {fileName}";
        _toastTimer = 2.8f;
        _isCapturing = false;
    }

    void OnGUI()
    {
        if (!IsActive) return;

        // シャッターフラッシュ演出（白い幕がスッと消える）
        if (_flashAlpha > 0.01f)
        {
            var oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _flashAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        // 撮影フレーム中はガイドUIを描画しない（写真にUIを残さない）
        if (_isCapturing) return;

        // 操作ガイド（入力から時間が経つと半透明にフェード）
        float idleTime = Time.realtimeSinceStartup - _lastInputTime;
        float guideAlpha = Mathf.Clamp(1.0f - (idleTime - 2.5f) * 0.45f, 0.22f, 0.95f);

        DrawGuideBar(guideAlpha);

        // トースト通知（撮影完了メッセージなど）
        if (_toastTimer > 0.01f)
        {
            DrawToast();
        }
    }

    void DrawGuideBar(float alpha)
    {
        var oldColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);

        float barWidth = Mathf.Min(680f, Screen.width - 40f);
        float barHeight = 42f;
        Rect barRect = new Rect((Screen.width - barWidth) * 0.5f, Screen.height - barHeight - 20f, barWidth, barHeight);

        // 黒ガラス背景
        GUI.color = new Color(0.08f, 0.10f, 0.14f, 0.75f * alpha);
        GUI.DrawTexture(barRect, Texture2D.whiteTexture);

        // ガイドテキスト
        GUI.color = new Color(0.95f, 0.97f, 1.0f, alpha);
        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13,
            fontStyle = FontStyle.Normal
        };

        string guideText = "📸 [P] 終了  |  [ドラッグ] 視点回転  |  [WASD/QE] カメラ移動  |  [ホイール] 画角(FOV)  |  [Space] 撮影";
        GUI.Label(barRect, guideText, style);

        // 画面上部に控えめな現在のFOV表示
        Rect fovRect = new Rect(Screen.width - 130f, 20f, 110f, 28f);
        GUI.color = new Color(0.08f, 0.10f, 0.14f, 0.65f * alpha);
        GUI.DrawTexture(fovRect, Texture2D.whiteTexture);
        GUI.color = new Color(0.85f, 0.95f, 1.0f, alpha);
        var fovStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12
        };
        GUI.Label(fovRect, $"FOV: {_photoFov:F0}°", fovStyle);

        GUI.color = oldColor;
    }

    void DrawToast()
    {
        float alpha = Mathf.Clamp01(_toastTimer * 2.0f);
        var oldColor = GUI.color;

        float toastWidth = 420f;
        float toastHeight = 36f;
        Rect toastRect = new Rect((Screen.width - toastWidth) * 0.5f, 32f, toastWidth, toastHeight);

        GUI.color = new Color(0.12f, 0.18f, 0.24f, 0.88f * alpha);
        GUI.DrawTexture(toastRect, Texture2D.whiteTexture);

        GUI.color = new Color(1f, 1f, 1f, alpha);
        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13,
            fontStyle = FontStyle.Bold
        };
        GUI.Label(toastRect, _toastMessage, style);

        GUI.color = oldColor;
    }

    /// <summary>心地よいプロシージャル・メカニカルシャッターSE合成</summary>
    static AudioClip SynthesizeShutterSound()
    {
        const int rate = 44100;
        float duration = 0.22f;
        int count = (int)(rate * duration);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float val = 0f;

            // 前半の「カチッ」（ミラーアップ音：0.00〜0.06s）
            if (t < 0.06f)
            {
                float env = Mathf.Exp(-t * 90f);
                float click = Mathf.Sin(2f * Mathf.PI * 1850f * t) * 0.6f;
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.4f;
                val += (click + noise) * env;
            }

            // 後半の「シャッ」（スリット幕・メカ音：0.07〜0.20s）
            if (t >= 0.07f)
            {
                float t2 = t - 0.07f;
                float env2 = Mathf.Exp(-t2 * 32f);
                float motor = Mathf.Sin(2f * Mathf.PI * 820f * t2) * 0.45f;
                float noise2 = (UnityEngine.Random.value * 2f - 1f) * 0.55f;
                val += (motor + noise2) * env2 * 0.85f;
            }

            samples[i] = Mathf.Clamp(val * 0.75f, -1f, 1f);
        }

        var clip = AudioClip.Create("ShutterSound", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
