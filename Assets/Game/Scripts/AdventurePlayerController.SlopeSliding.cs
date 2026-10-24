using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 急斜面高速スライディング＆砂煙サーフ（Slope Surfing）拡張。
/// 急勾配の草地や白砂ビーチを駆け下りる際、または【Cキー / ゲームパッドBボタン】で足を滑らせて
/// 高速滑走・砂煙ドリフト・スライドジャンプを展開する。
/// </summary>
public partial class AdventurePlayerController
{
    public bool IsSliding => _isSliding;

    private bool _isSliding = false;
    private float _slideSpeed = 0f;
    private Vector3 _slideDir = Vector3.forward;
    private float _slideDuration = 0f;

    private GameObject _slideDustGo;
    private ParticleSystem _slideDustPs;
    private AudioSource _slideAudioSource;
    private AudioClip _slideLoopClip;

    private const float MinSlopeAngleForAutoSlide = 18f;
    private const float MaxSlideSpeed = 16.5f;
    private const float SlideAccel = 18f;
    private const float SlideFriction = 6.5f;

    /// <summary>
    /// スライディング状態の更新（Updateから毎フレーム呼び出し）
    /// </summary>
    private void UpdateSlopeSliding()
    {
        EnsureSlideResources();

        if (!_grounded || _gliding)
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

        if (_slideDustPs != null && !_slideDustPs.isPlaying)
            _slideDustPs.Play();

        if (_slideAudioSource != null && _slideLoopClip != null)
        {
            _slideAudioSource.clip = _slideLoopClip;
            _slideAudioSource.volume = 0.35f;
            _slideAudioSource.pitch = 1.1f;
            _slideAudioSource.Play();
        }

        AdventureInputHaptics.PlayPulse(0.35f, 0.50f, 0.12f);
    }

    private void TickSliding(Vector3 slopeDown, float slopeAngle)
    {
        _slideDuration += Time.deltaTime;

        // 1. 斜面勾配に応じた加速と減速
        float gravityAccel = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * 22f;
        float dot = Vector3.Dot(_slideDir, slopeDown);

        if (dot > 0.2f)
        {
            // 下り坂：一気に加速
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

        // 5. パーティクル位置追従
        if (_slideDustGo != null)
        {
            _slideDustGo.transform.position = transform.position + Vector3.up * 0.12f - transform.forward * 0.25f;
        }
    }

    private void StopSliding()
    {
        _isSliding = false;
        AdventureInputHaptics.UpdateSlopeSlideRumble(0f, false);

        if (_slideDustPs != null && _slideDustPs.isPlaying)
            _slideDustPs.Stop();

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

        _hop = Mathf.Sqrt(jumpPower * 1.35f * -2f * gravity);
        _grounded = false;
        _airborneTime = 0f;
        _airMomentum = _slideDir * (_slideSpeed * 1.15f);

        StopSliding();
        AdventureInputHaptics.PlayPulse(0.40f, 0.65f, 0.16f);
        return true;
    }

    /// <summary>砂煙パーティクル＆プロシージャル滑走音の初期化</summary>
    private void EnsureSlideResources()
    {
        if (_slideDustGo == null)
        {
            _slideDustGo = new GameObject("Player_SlideDustFx");
            _slideDustGo.transform.SetParent(transform, false);
            _slideDustGo.transform.localPosition = new Vector3(0f, 0.1f, -0.3f);

            _slideDustPs = _slideDustGo.AddComponent<ParticleSystem>();
            var main = _slideDustPs.main;
            main.loop = true;
            main.startLifetime = 0.45f;
            main.startSpeed = 3.2f;
            main.startSize = 0.55f;
            main.startColor = new Color(0.92f, 0.88f, 0.76f, 0.45f); // 白砂・砂埃色
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = _slideDustPs.emission;
            emission.rateOverTime = 32f;

            var shape = _slideDustPs.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.25f;
            shape.rotation = new Vector3(0f, 180f, 0f);

            var psr = _slideDustGo.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
            if (shader != null) psr.material = new Material(shader);

            // プロシージャル滑走サウンド
            _slideAudioSource = _slideDustGo.AddComponent<AudioSource>();
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
