using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 急斜面高速スライディング＆サーフ（Slope Surfing）拡張。
/// 急勾配の草地や白砂ビーチを駆け下りる際、または【Cキー / ゲームパッドBボタン】で足を滑らせて
/// 高速滑走・スライドジャンプを展開する。（※パーティクル不使用・視界完全クリーン保証）
/// </summary>
public partial class AdventurePlayerController
{
    public bool IsSliding => _isSliding;

    private bool _isSliding = false;
    private float _slideSpeed = 0f;
    private Vector3 _slideDir = Vector3.forward;
    private float _slideDuration = 0f;
    private float _slideInputBufferTimer = 0f;

    private AudioSource _slideAudioSource;
    private AudioClip _slideLoopClip;

    private const float MinSlopeAngleForAutoSlide = 16f;
    private const float MaxSlideSpeed = 22.0f;
    private const float SlideAccel = 24f;
    private const float SlideFriction = 5.5f;

    /// <summary>
    /// 滑空から着地した瞬間の「着地サーフィン（シームレス・スライド移行）」判定。
    /// 接地直前（0.35秒以内）にCキー/Bボタンを押していた場合、滑空速度をそのまま保持して超高速スライディングへ移行！
    /// </summary>
    public bool TryTriggerGlideToSlideSurfing(float prevGlideSpeed, Vector3 glideDir)
    {
        EnsureSlideResources();

        bool hasInput = CheckSlideInputHeld() || _slideInputBufferTimer > 0f;
        if (!hasInput) return false;

        _slideInputBufferTimer = 0f;

        Vector3 groundNormal = GetGroundNormal();
        Vector3 slopeDown = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;

        _isSliding = true;
        _slideDuration = 0f;

        Vector3 dir = glideDir.sqrMagnitude > 0.01f ? glideDir.normalized : transform.forward;
        if (slopeDown.sqrMagnitude > 0.01f)
            dir = Vector3.Slerp(dir, slopeDown, 0.35f).normalized;
        _slideDir = dir;
        transform.rotation = Quaternion.LookRotation(_slideDir, Vector3.up);

        // 滑空の勢いを100%保持し、最低14.0m/s、最大22.0m/sの圧倒的初速で着地サーフィン
        _slideSpeed = Mathf.Clamp(Mathf.Max(prevGlideSpeed * 1.15f, HorizontalSpeed), 14.0f, MaxSlideSpeed);

        PlaySlideAudio(0.52f, 1.25f);

        // 強烈な着地サーフィン衝撃＋摩擦ハプティクス
        AdventureInputHaptics.PlayPulse(0.55f, 0.70f, 0.22f);

        Debug.Log($"[RustAndFloat] 着地サーフィン発動！速度={_slideSpeed:F1}m/s");
        return true;
    }

    private void PlaySlideAudio(float volume, float pitch)
    {
        if (_slideAudioSource != null && _slideLoopClip != null)
        {
            _slideAudioSource.clip = _slideLoopClip;
            _slideAudioSource.volume = volume;
            _slideAudioSource.pitch = pitch;
            if (!_slideAudioSource.isPlaying)
                _slideAudioSource.Play();
        }
    }

    /// <summary>
    /// スライディング状態の更新（Updateから毎フレーム呼び出し）
    /// </summary>
    private void UpdateSlopeSliding()
    {
        EnsureSlideResources();

        if (CheckSlideInputHeld())
            _slideInputBufferTimer = 0.35f;
        else if (_slideInputBufferTimer > 0f)
            _slideInputBufferTimer -= Time.deltaTime;

        if (!_grounded || _gliding)
        {
            if (_isSliding) StopSliding();
            return;
        }

        // 北のスカイダイブスタート台（フライトデッキ）上にいる場合は自動スライディングを防止
        if (AdventureGlidingTimeAttackManager.GetFlightDeckSurfaceY(transform.position) > float.NegativeInfinity)
        {
            if (_isSliding) StopSliding();
            return;
        }

        Vector3 groundNormal = GetGroundNormal();
        float slopeAngle = Vector3.Angle(Vector3.up, groundNormal);

        bool inputSlide = CheckSlideInputHeld();
        bool isSteepDownhill = slopeAngle >= MinSlopeAngleForAutoSlide;

        // 進行方向と斜面の下降方向の内積
        Vector3 slopeDown = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
        float moveDownDot = Vector3.Dot(transform.forward, slopeDown);

        bool shouldSlide = inputSlide || (isSteepDownhill && moveDownDot > 0.45f && HorizontalSpeed > 5.5f);

        if (shouldSlide)
        {
            if (!_isSliding)
            {
                StartSliding(slopeDown);
            }
            TickSliding(slopeDown, slopeAngle);
        }
        else
        {
            if (_isSliding)
            {
                StopSliding();
            }
        }
    }

    private void StartSliding(Vector3 slopeDown)
    {
        _isSliding = true;
        _slideDuration = 0f;
        _slideDir = transform.forward;
        if (slopeDown.sqrMagnitude > 0.01f)
            _slideDir = Vector3.Slerp(_slideDir, slopeDown, 0.45f).normalized;

        _slideSpeed = Mathf.Max(HorizontalSpeed * 1.15f, 9.5f);

        PlaySlideAudio(0.35f, 1.1f);

        AdventureInputHaptics.PlayPulse(0.35f, 0.50f, 0.12f);
    }

    private void TickSliding(Vector3 slopeDown, float slopeAngle)
    {
        _slideDuration += Time.deltaTime;

        // 1. 斜面勾配に応じた加速と減速
        float gravityAccel = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * 28f;
        float dot = Vector3.Dot(_slideDir, slopeDown);

        if (dot > 0.15f)
        {
            // 下り坂：一気に爽快加速
            _slideSpeed = Mathf.MoveTowards(_slideSpeed, MaxSlideSpeed, (gravityAccel * dot) * Time.deltaTime);
        }
        else
        {
            // 平地または上り：摩擦減速
            _slideSpeed = Mathf.MoveTowards(_slideSpeed, 0f, SlideFriction * Time.deltaTime);
            if (_slideSpeed < 2.5f)
            {
                StopSliding();
                return;
            }
        }

        // 2. プレイヤーの入力によるドリフト旋回
        Vector2 moveInput = AdventureInputReader.MoveAxis;
        if (Mathf.Abs(moveInput.x) > 0.05f)
        {
            float steer = moveInput.x * 140f * Time.deltaTime;
            _slideDir = Quaternion.Euler(0f, steer, 0f) * _slideDir;
            transform.rotation = Quaternion.LookRotation(_slideDir, Vector3.up);
        }
        else if (slopeDown.sqrMagnitude > 0.01f && dot > 0f)
        {
            // 斜面の自然な重力誘導
            _slideDir = Vector3.Slerp(_slideDir, slopeDown, Time.deltaTime * 3.5f).normalized;
            transform.rotation = Quaternion.LookRotation(_slideDir, Vector3.up);
        }

        // 3. 移動ベクトルの適用
        Vector3 slideMotion = _slideDir * _slideSpeed;
        if (_cc != null && _cc.enabled)
        {
            _cc.Move((slideMotion + Vector3.down * 4.5f) * Time.deltaTime);
        }

        // 4. ハプティクス＆サウンド更新
        AdventureInputHaptics.UpdateSlopeSlideRumble(_slideSpeed, true);

        if (_slideAudioSource != null && _slideAudioSource.isPlaying)
        {
            float speedRatio = Mathf.Clamp01(_slideSpeed / MaxSlideSpeed);
            _slideAudioSource.volume = Mathf.Lerp(0.18f, 0.48f, speedRatio);
            _slideAudioSource.pitch = Mathf.Lerp(0.85f, 1.35f, speedRatio);
        }
    }

    private void StopSliding()
    {
        _isSliding = false;
        AdventureInputHaptics.UpdateSlopeSlideRumble(0f, false);

        if (_slideAudioSource != null && _slideAudioSource.isPlaying)
            _slideAudioSource.Stop();
    }

    /// <summary>スライディング入力の検知</summary>
    private bool CheckSlideInputHeld()
    {
        try
        {
            if (Input.GetKey(KeyCode.C)) return true;
        }
        catch { }

        var kb = AdventureInputReader.Keyboard;
        if (kb != null && kb.cKey.isPressed) return true;

        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.buttonEast.isPressed || gp.leftStickButton.isPressed)
                return true;
        }

        return false;
    }

    /// <summary>足元の地形法線取得</summary>
    private Vector3 GetGroundNormal()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1.4f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.normal;
        }
        return Vector3.up;
    }

    /// <summary>スライディング中のジャンプ（加速を保持したままの大ジャンプ）</summary>
    public bool TrySlideJump(float jumpPower)
    {
        if (!_isSliding || _slideSpeed < 6.0f) return false;

        _hop = Mathf.Sqrt(jumpPower * 1.55f * -2f * gravity);
        _grounded = false;
        _airborneTime = 0f;
        _airMomentum = _slideDir * (_slideSpeed * 1.25f);

        StopSliding();
        AdventureInputHaptics.PlayPulse(0.45f, 0.70f, 0.18f);
        return true;
    }

    /// <summary>滑走サウンドリソースの初期化（※パーティクル完全不使用）</summary>
    private void EnsureSlideResources()
    {
        // 既存の不要なパーティクルオブジェクトが万一残っていれば完全消去
        var oldDust = transform.Find("Player_SlideDustFx");
        if (oldDust != null)
        {
            Destroy(oldDust.gameObject);
        }

        if (_slideAudioSource == null)
        {
            var audioGo = new GameObject("Player_SlideAudio");
            audioGo.transform.SetParent(transform, false);

            _slideAudioSource = audioGo.AddComponent<AudioSource>();
            _slideAudioSource.spatialBlend = 0.2f;
            _slideAudioSource.loop = true;
            _slideAudioSource.playOnAwake = false;
            _slideLoopClip = GenerateProceduralSlideSound();
        }
    }

    private AudioClip GenerateProceduralSlideSound()
    {
        int sampleRate = 22050;
        int lengthSamples = sampleRate; // 1秒ループ
        float[] samples = new float[lengthSamples];
        for (int i = 0; i < lengthSamples; i++)
        {
            // ホワイトノイズにローパスと周期摩擦を乗せる
            float noise = Random.Range(-1f, 1f);
            float surfCycle = Mathf.Sin(i * 0.05f) * 0.25f;
            samples[i] = noise * (0.6f + surfCycle);
        }

        var clip = AudioClip.Create("Procedural_SlopeSlide", lengthSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
