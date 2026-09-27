using UnityEngine;

/// <summary>
/// サンクチュアリ・ゼロの白亜の床が、プレイヤー接近時に
/// ワイヤーフレームの青いグリッドとして発光するギミック。
/// SanctuaryGridFloor にアタッチする。
/// </summary>
[DefaultExecutionOrder(100)]
public class AdventureSanctuaryGridGlow : MonoBehaviour
{
    [Header("トリガー設定")]
    [Tooltip("プレイヤーがこの距離以内に入ると発光開始")]
    public float triggerRadius = 40f;

    [Tooltip("グリッドが完全に展開する半径（床の半径）")]
    public float maxRevealRadius = 20f;

    [Header("演出タイミング")]
    [Tooltip("発光開始前のフリッカー時間（秒）")]
    public float flickerDuration = 0.6f;

    [Tooltip("グロー強度のフェードイン時間（秒）")]
    public float glowFadeInTime = 1.8f;

    [Tooltip("リビール（放射展開）の時間（秒）")]
    public float revealDuration = 2.5f;

    [Tooltip("消灯フェードアウト時間（秒）")]
    public float fadeOutTime = 1.2f;

    [Header("参照（自動検出）")]
    public Transform playerTransform;

    // ── 内部 ──
    Renderer _renderer;
    MaterialPropertyBlock _mpb;

    // シェーダーIDキャッシュ
    static readonly int ID_GlowIntensity = Shader.PropertyToID("_GlowIntensity");
    static readonly int ID_RevealRadius  = Shader.PropertyToID("_RevealRadius");
    static readonly int ID_CenterWorld   = Shader.PropertyToID("_CenterWorld");

    enum State { Idle, Flickering, FadingIn, Active, FadingOut }
    State _state = State.Idle;
    float _stateTimer;
    float _currentGlow;
    float _currentReveal;

    void Start()
    {
        _renderer = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();

        // プレイヤー自動検出
        if (playerTransform == null)
        {
            var pc = Object.FindFirstObjectByType<AdventurePlayerController>();
            if (pc != null) playerTransform = pc.transform;
        }

        // 初期状態：消灯
        ApplyMaterial(0f, 0f);
    }

    void Update()
    {
        if (playerTransform == null || _renderer == null) return;

        // XZ平面での距離計算
        Vector2 playerXZ = new Vector2(playerTransform.position.x, playerTransform.position.z);
        Vector2 selfXZ   = new Vector2(transform.position.x, transform.position.z);
        float dist = Vector2.Distance(playerXZ, selfXZ);

        bool inside = dist < triggerRadius;

        // ── 状態遷移 ──
        if (inside && _state == State.Idle)
        {
            TransitionTo(State.Flickering);
        }
        else if (!inside && (_state == State.Active || _state == State.FadingIn))
        {
            TransitionTo(State.FadingOut);
        }

        _stateTimer += Time.deltaTime;

        // ── 状態ごとの処理 ──
        switch (_state)
        {
            case State.Flickering:
                HandleFlicker();
                break;

            case State.FadingIn:
                HandleFadeIn();
                break;

            case State.Active:
                _currentGlow = 1f;
                _currentReveal = maxRevealRadius;
                break;

            case State.FadingOut:
                HandleFadeOut();
                break;

            case State.Idle:
                _currentGlow = 0f;
                _currentReveal = 0f;
                break;
        }

        ApplyMaterial(_currentGlow, _currentReveal);
    }

    /// <summary>フリッカー演出（電源が入る瞬間のチラつき）</summary>
    void HandleFlicker()
    {
        float t = _stateTimer / flickerDuration;

        if (t >= 1f)
        {
            TransitionTo(State.FadingIn);
            return;
        }

        // PerlinNoise で不規則なチラつき
        float noise = Mathf.PerlinNoise(_stateTimer * 35f, 7.77f);
        // 後半ほど点灯率が高くなる
        float threshold = Mathf.Lerp(0.65f, 0.15f, t);
        _currentGlow = noise > threshold ? Mathf.Lerp(0.3f, 0.8f, t) : 0.05f;
        _currentReveal = maxRevealRadius * 0.3f; // フリッカー中はわずかにリビール
    }

    /// <summary>グロー＋リビールのフェードイン</summary>
    void HandleFadeIn()
    {
        float tGlow   = Mathf.Clamp01(_stateTimer / glowFadeInTime);
        float tReveal = Mathf.Clamp01(_stateTimer / revealDuration);

        // イーズアウト曲線
        _currentGlow   = 1f - (1f - tGlow) * (1f - tGlow);
        _currentReveal = Mathf.Lerp(maxRevealRadius * 0.3f, maxRevealRadius, tReveal * tReveal * (3f - 2f * tReveal));

        if (tGlow >= 1f && tReveal >= 1f)
        {
            _currentGlow = 1f;
            _currentReveal = maxRevealRadius;
            TransitionTo(State.Active);
        }
    }

    /// <summary>消灯フェードアウト</summary>
    void HandleFadeOut()
    {
        float t = Mathf.Clamp01(_stateTimer / fadeOutTime);
        // イーズイン（最後に急速に消える）
        float ease = t * t;
        _currentGlow   = Mathf.Lerp(1f, 0f, ease);
        _currentReveal = Mathf.Lerp(maxRevealRadius, 0f, ease * 0.6f);

        if (t >= 1f)
        {
            _currentGlow = 0f;
            _currentReveal = 0f;
            TransitionTo(State.Idle);
        }
    }

    void TransitionTo(State newState)
    {
        _state = newState;
        _stateTimer = 0f;
    }

    /// <summary>MaterialPropertyBlock でシェーダーパラメータ更新</summary>
    void ApplyMaterial(float glow, float reveal)
    {
        if (_renderer == null) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(ID_GlowIntensity, glow);
        _mpb.SetFloat(ID_RevealRadius, reveal);
        _mpb.SetVector(ID_CenterWorld, new Vector4(
            transform.position.x, transform.position.y, transform.position.z, 0f));
        _renderer.SetPropertyBlock(_mpb);
    }
}
