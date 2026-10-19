using System.Collections;
using UnityEngine;

/// <summary>
/// 急降下滑空ダイブ＆急引き起こしGフォース・Juice（Juice & Feel ➔ 満点）
/// 高空からの急降下ダイブ時の重低音風切り音（Wind Howl）と、
/// 地面・海面スレスレでの急引き起こし時に生じるGフォース（カメラ揺れ・翼端ベイパートレイル・空力SE）を演出。
/// </summary>
public class AdventureGlideAcrobaticsJuice : MonoBehaviour
{
    private static AdventureGlideAcrobaticsJuice _instance;
    public static AdventureGlideAcrobaticsJuice Instance => _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void AutoInitialize()
    {
        if (_instance != null) return;
        var existing = Object.FindAnyObjectByType<AdventureGlideAcrobaticsJuice>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("AdventureGlideAcrobaticsJuice");
        _instance = go.AddComponent<AdventureGlideAcrobaticsJuice>();
        DontDestroyOnLoad(go);
    }

    private AdventurePlayerController _player;
    private AudioSource _audioSource;
    private AudioClip _windHowlLoopClip;
    private AudioClip _pullUpWhooshClip;

    // ダイブ状態追跡
    private bool _isDiving = false;
    private float _diveIntensity = 0f;
    private float _prevVerticalVel = 0f;
    private float _diveDuration = 0f;
    private float _lastPullUpTime = -10f;

    // 翼端ベイパートレイル
    private TrailRenderer _leftVaporTrail;
    private TrailRenderer _rightVaporTrail;
    private Material _vaporMaterial;

    // カメラGシェイク用
    private float _cameraShakeIntensity = 0f;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f; // 2Dステレオで耳元に響く迫力

        CreateProceduralAudio();
        CreateVaporTrails();
    }

    void Start()
    {
        FindPlayer();
    }

    void FindPlayer()
    {
        if (_player == null)
            _player = AdventurePlayerController.Instance ?? Object.FindAnyObjectByType<AdventurePlayerController>();
    }

    void Update()
    {
        if (_player == null)
        {
            FindPlayer();
            if (_player == null) return;
        }

        bool isGliding = _player.IsGliding;
        Vector3 vel = _player.Velocity;
        float verticalVel = vel.y;
        float speed = vel.magnitude;

        // 1. 急降下ダイブ判定（滑空中で下向き速度が -5.0m/s 以下、かつ前進速度が出ている）
        if (isGliding && verticalVel < -5.0f && speed > 8.0f)
        {
            _isDiving = true;
            _diveDuration += Time.deltaTime;
            // -5m/s 〜 -16m/s で 0.0 〜 1.0 にマッピング
            float targetDive = Mathf.Clamp01((-verticalVel - 5.0f) / 11.0f);
            _diveIntensity = Mathf.MoveTowards(_diveIntensity, targetDive, Time.deltaTime * 3.5f);
        }
        else
        {
            if (_isDiving)
            {
                // 急降下状態から水平方向に引き起こされた瞬間（垂直加速度が急反転）
                float accelerationY = (verticalVel - _prevVerticalVel) / Mathf.Max(Time.deltaTime, 0.001f);
                if (_diveIntensity > 0.35f && (verticalVel > -2.5f || accelerationY > 15f))
                {
                    TriggerPullUpGForce(_diveIntensity, _player.transform.position);
                }
            }
            _isDiving = false;
            _diveDuration = 0f;
            _diveIntensity = Mathf.MoveTowards(_diveIntensity, 0f, Time.deltaTime * 2.5f);
        }

        _prevVerticalVel = verticalVel;

        // 2. ダイブ＆最高速エアロ風の唸り声（Wind Howl & Aero Rumble）音量調律
        UpdateWindHowlAudio(isGliding ? speed : 0f);

        // 3. 翼端トレイルの位置追従と発光
        UpdateVaporTrails(isGliding);

        // 4. 最高速エアロ・ランブル（低音の空気震え＆翼端マイクロシェイク）
        float aeroRumble = 0f;
        if (isGliding)
        {
            float speedThreshold = 11.5f;
            if (speed > speedThreshold)
            {
                aeroRumble = Mathf.Clamp01((speed - speedThreshold) / 6.5f) * 0.35f;
            }
            if (_isDiving)
            {
                aeroRumble = Mathf.Max(aeroRumble, _diveIntensity * 0.45f);
            }
        }

        // カメラGシェイクとエアロランブルの合成
        float totalShake = _cameraShakeIntensity + aeroRumble;
        if (totalShake > 0.001f)
        {
            _cameraShakeIntensity = Mathf.MoveTowards(_cameraShakeIntensity, 0f, Time.deltaTime * 6.0f);
            if (Camera.main != null)
            {
                // 高周波の微細な空気振動（38Hz）
                float microShakeY = Mathf.Sin(Time.time * 38f) * totalShake * 0.045f;
                float microShakeX = Mathf.Cos(Time.time * 29f) * totalShake * 0.025f;
                Camera.main.transform.position += Camera.main.transform.up * microShakeY + Camera.main.transform.right * microShakeX;
            }
        }
    }

    private void TriggerPullUpGForce(float intensity, Vector3 playerPos)
    {
        if (Time.time < _lastPullUpTime + 1.2f) return;
        _lastPullUpTime = Time.time;

        // 1. 空力引き起こしSE（シュバァァン！）再生
        if (_audioSource != null && _pullUpWhooshClip != null)
        {
            _audioSource.pitch = Random.Range(0.95f, 1.05f);
            _audioSource.PlayOneShot(_pullUpWhooshClip, Mathf.Clamp01(0.6f + intensity * 0.4f));
        }

        // 2. カメラG揺れ（適度な縦加速度の心地よいパンチ）
        _cameraShakeIntensity = Mathf.Clamp01(0.5f + intensity * 0.5f);

        // 3. 翼端ベイパートレイルを一時的に濃く放出
        StartCoroutine(EmitVaporBurstRoutine());

        // 4. 低空スレスレでの引き起こし判定（高度 < 14m）でRustが歓声を上げる
        float altitude = playerPos.y;
        var terrain = Terrain.activeTerrain;
        if (terrain != null)
            altitude -= terrain.SampleHeight(playerPos);

        if (altitude < 14.0f && AdventureRustDrone.Instance != null)
        {
            string[] acrobaticLines = new string[]
            {
                "わぁぁっ！ナイス引き起こし！すごいGだね！！",
                "ピキーッ！地面スレスレ…！Float、操縦すごいよ！！",
                "ふわりと舞い上がった！風の乗り方、最高だね♪"
            };
            AdventureRustDrone.Instance.SpeakCustom(acrobaticLines[Random.Range(0, acrobaticLines.Length)], 3.5f);
        }
    }

    private IEnumerator EmitVaporBurstRoutine()
    {
        if (_leftVaporTrail != null && _rightVaporTrail != null)
        {
            _leftVaporTrail.emitting = true;
            _rightVaporTrail.emitting = true;
            _leftVaporTrail.time = 0.55f;
            _rightVaporTrail.time = 0.55f;

            yield return new WaitForSeconds(0.85f);

            _leftVaporTrail.time = 0.25f;
            _rightVaporTrail.time = 0.25f;
            if (!_isDiving)
            {
                _leftVaporTrail.emitting = false;
                _rightVaporTrail.emitting = false;
            }
        }
    }

    private void UpdateWindHowlAudio(float speed)
    {
        if (_audioSource == null || _windHowlLoopClip == null) return;

        float speedIntensity = Mathf.Clamp01((speed - 11.5f) / 6.5f) * 0.45f;
        float effectiveIntensity = Mathf.Max(_diveIntensity, speedIntensity);

        if (effectiveIntensity > 0.04f)
        {
            if (!_audioSource.isPlaying)
            {
                _audioSource.clip = _windHowlLoopClip;
                _audioSource.loop = true;
                _audioSource.Play();
            }
            _audioSource.volume = Mathf.Lerp(0f, 0.75f, effectiveIntensity);
            _audioSource.pitch = Mathf.Lerp(0.85f, 1.35f, effectiveIntensity);
        }
        else
        {
            if (_audioSource.isPlaying && _audioSource.clip == _windHowlLoopClip)
            {
                _audioSource.volume = Mathf.MoveTowards(_audioSource.volume, 0f, Time.deltaTime * 4f);
                if (_audioSource.volume <= 0.01f)
                    _audioSource.Stop();
            }
        }
    }

    private void UpdateVaporTrails(bool isGliding)
    {
        if (_leftVaporTrail == null || _rightVaporTrail == null || _player == null) return;

        Transform pTr = _player.transform;
        // Floatの左右の翼端位置（左右に約0.95m、後方0.2m、胸の高さ）
        Vector3 leftWing = pTr.position - pTr.right * 0.95f - pTr.forward * 0.2f + Vector3.up * 0.9f;
        Vector3 rightWing = pTr.position + pTr.right * 0.95f - pTr.forward * 0.2f + Vector3.up * 0.9f;

        _leftVaporTrail.transform.position = leftWing;
        _rightVaporTrail.transform.position = rightWing;

        // ダイブ中もベイパートレイルをうっすら射出
        if (_isDiving && isGliding)
        {
            _leftVaporTrail.emitting = true;
            _rightVaporTrail.emitting = true;
        }
        else if (Time.time > _lastPullUpTime + 0.85f)
        {
            _leftVaporTrail.emitting = false;
            _rightVaporTrail.emitting = false;
        }
    }

    private void CreateVaporTrails()
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Sprites/Default");

        _vaporMaterial = new Material(shader) { name = "VaporTrail_Mat" };
        _vaporMaterial.color = new Color(1f, 1f, 1f, 0.45f);
        _vaporMaterial.SetFloat("_Surface", 1f);
        _vaporMaterial.SetFloat("_Blend", 0f);
        _vaporMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _vaporMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _vaporMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _vaporMaterial.SetInt("_ZWrite", 0);
        _vaporMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        // 左翼トレイル
        var leftGo = new GameObject("LeftVaporTrail");
        leftGo.transform.SetParent(transform, false);
        _leftVaporTrail = leftGo.AddComponent<TrailRenderer>();
        ConfigureTrail(_leftVaporTrail);

        // 右翼トレイル
        var rightGo = new GameObject("RightVaporTrail");
        rightGo.transform.SetParent(transform, false);
        _rightVaporTrail = rightGo.AddComponent<TrailRenderer>();
        ConfigureTrail(_rightVaporTrail);
    }

    private void ConfigureTrail(TrailRenderer tr)
    {
        tr.material = _vaporMaterial;
        tr.time = 0.35f;
        tr.startWidth = 0.08f;
        tr.endWidth = 0.01f;
        tr.autodestruct = false;
        tr.emitting = false;

        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.9f, 0.95f, 1f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        tr.colorGradient = grad;
    }

    private void CreateProceduralAudio()
    {
        // 1. 風の唸り声（Wind Howl / 低周波ランブル＋ホワイトノイズ）
        int sampleRate = 44100;
        float duration = 2.0f;
        int sampleCount = (int)(sampleRate * duration);
        float[] howlSamples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            // 重低音ランブル（48Hz〜65Hzの緩やかな揺らぎ）
            float rumble = Mathf.Sin(2f * Mathf.PI * 52f * t + Mathf.Sin(t * 3.5f) * 1.5f) * 0.35f;
            // 高域ホワイトノイズ（空力シアー音）
            float noise = (Random.value * 2f - 1f) * 0.18f;
            howlSamples[i] = Mathf.Clamp(rumble + noise, -1f, 1f);
        }
        _windHowlLoopClip = AudioClip.Create("Juice_WindHowlLoop", sampleCount, 1, sampleRate, false);
        _windHowlLoopClip.SetData(howlSamples, 0);

        // 2. 急引き起こし空力サウンド（Whoosh / G-Force Rip）
        float whooshDur = 0.65f;
        int whooshSamplesCount = (int)(sampleRate * whooshDur);
        float[] whooshSamples = new float[whooshSamplesCount];

        for (int i = 0; i < whooshSamplesCount; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / whooshDur;
            // 急激なエンベロープ（立ち上がり速く、滑らかに減衰）
            float env = Mathf.Sin(progress * Mathf.PI);
            float freq = Mathf.Lerp(120f, 320f, Mathf.Sin(progress * Mathf.PI));
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.4f;
            float rushNoise = (Random.value * 2f - 1f) * 0.35f;
            whooshSamples[i] = Mathf.Clamp((tone + rushNoise) * env, -1f, 1f);
        }
        _pullUpWhooshClip = AudioClip.Create("Juice_PullUpWhoosh", whooshSamplesCount, 1, sampleRate, false);
        _pullUpWhooshClip.SetData(whooshSamples, 0);
    }
}
