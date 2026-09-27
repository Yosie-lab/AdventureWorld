using UnityEngine;

/// <summary>
/// niko専用のかわいいトコトコ足音コンポーネント。
/// 通常の陸地では愛らしい左右交互のトコトコ音、
/// 波打ち際や池の浅瀬（水辺）では「チャプッ、ピシャッ」と心地よい水しぶき足音＆水滴飛沫エフェクトへ自動切替。
/// </summary>
public class AdventureNikoFootsteps : MonoBehaviour
{
    CharacterController _cc;
    AdventurePlayerController _player;
    AudioSource _audioSource;

    // 陸地用トコトコ足音クリップ
    AudioClip _stepL;
    AudioClip _stepR;

    // 水辺用スプラッシュ足音クリップ
    AudioClip _waterStepL;
    AudioClip _waterStepR;
    AudioClip _waterLanding;

    // 白砂ビーチ用サクサク足音クリップ
    AudioClip _sandStepL;
    AudioClip _sandStepR;

    float _stepTimer = 0f;
    int _stepCount = 0;
    Vector3 _lastPos;
    bool _wasGrounded = true;

    [Header("Volume & Tuning")]
    [Range(0f, 1f)] public float volume = 0.22f;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _player = GetComponent<AdventurePlayerController>();

        // 足音専用の2D AudioSourceをセットアップ
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0.0f; // 2D音響（クリアに届く）
        _audioSource.volume = 1.0f;
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;

        _lastPos = transform.position;
        LoadClips();
    }

    void Start()
    {
        if (_stepL == null || _stepR == null)
            LoadClips();
    }

    void LoadClips()
    {
#if UNITY_EDITOR
        _stepL = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RustAndFloat/Audio/Footsteps/niko_step_L.wav");
        _stepR = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RustAndFloat/Audio/Footsteps/niko_step_R.wav");
#endif

        if (_stepL == null) _stepL = SynthesizeCuteStep("NikoStep_L", 520f, 360f);
        if (_stepR == null) _stepR = SynthesizeCuteStep("NikoStep_R", 640f, 440f);

        // 澄んだ水しぶき足音（ピチャッ、チャプッ）と水面着地音（バシャァン）
        _waterStepL = SynthesizeWaterSplash("WaterStep_L", 1150f, 380f, 0.11f);
        _waterStepR = SynthesizeWaterSplash("WaterStep_R", 1380f, 440f, 0.11f);
        _waterLanding = SynthesizeWaterSplash("WaterLanding", 850f, 240f, 0.24f);

        // 白砂ビーチのサクサク砂踏み足音（乾いた細粒砂の擦過音＋低音レゾナンス）
        _sandStepL = SynthesizeSandStep("SandStep_L", isLeft: true);
        _sandStepR = SynthesizeSandStep("SandStep_R", isLeft: false);
    }

    void Update()
    {
        if (_audioSource == null) return;

        // 接地＆滑空判定（空中や滑空時は自然に消音）
        bool isGrounded = (_player != null) ? _player.IsGrounded : (_cc != null && _cc.isGrounded);
        bool isGliding = (_player != null && _player.IsGliding);

        // 着地した瞬間の水しぶき音判定
        if (!_wasGrounded && isGrounded && !isGliding)
        {
            if (CheckIsInWater(transform.position))
            {
                PlayWaterLanding();
            }
        }
        _wasGrounded = isGrounded;

        if (!isGrounded || isGliding)
        {
            _stepTimer = 0.15f;
            _lastPos = transform.position;
            return;
        }

        // 実移動速度の計算（水平方向のみ）
        Vector3 curPos = transform.position;
        Vector3 diff = curPos - _lastPos;
        diff.y = 0f;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float speed = diff.magnitude / dt;
        _lastPos = curPos;

        // 移動中判定（歩行: 1.5〜4.2m/s、走行: 7.8m/s、微小な動きでも反応するよう閾値0.25f）
        if (speed > 0.25f)
        {
            float stepInterval = Mathf.Lerp(0.30f, 0.20f, Mathf.InverseLerp(1.5f, 7.5f, speed));
            _stepTimer += Time.deltaTime;

            if (_stepTimer >= stepInterval)
            {
                _stepTimer = 0f;
                PlayStep(curPos, speed);
            }
        }
        else
        {
            _stepTimer = 0.22f;
        }
    }

    /// <summary>Nikoが波打ち際や池の水中に足を踏み入れているかを判定</summary>
    bool CheckIsInWater(Vector3 pos)
    {
        // 1. 海面・波打ち際（標高5.80m〜6.25m）
        if (pos.y < 6.25f)
            return true;

        // 2. 内陸オアシス池・段々池の浅瀬（標高47.8m〜48.7m付近）
        Vector3 oasisPos = new Vector3(480f, 48.0f, 455f);
        if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(oasisPos.x, oasisPos.z)) < 18f)
        {
            if (pos.y < 48.75f) return true;
        }

        return false;
    }

    void PlayStep(Vector3 footPos, float speed)
    {
        _stepCount++;
        bool isLeft = (_stepCount % 2 == 0);
        bool inWater = CheckIsInWater(footPos);
        bool onSand = !inWater && AdventureBeachVisualEnhancer.CheckIsOnSandBeach(footPos);

        AudioClip clip;
        if (inWater)
        {
            // 水辺：ピチャッ、チャプッという涼やかな水しぶき足音
            clip = isLeft ? _waterStepL : _waterStepR;
            SpawnFootstepSplashFx(footPos, isLeft, bigSplash: false);
        }
        else
        {
            // 陸地：かわいいトコトコ音
            clip = isLeft ? _stepL : _stepR;
        }

        if (clip != null)
        {
            float speedFactor = Mathf.Lerp(0.75f, 1.0f, Mathf.InverseLerp(1.5f, 7.5f, speed));
            float seVol = PlayerPrefs.GetFloat("Adventure_SeVolume", 1.0f);
            float vol = speedFactor * volume * (inWater ? 1.25f : 1.0f) * seVol;

            _audioSource.pitch = Random.Range(0.96f, 1.04f);
            _audioSource.PlayOneShot(clip, vol);

            // 砂浜の場合、心地よいサクサク砂踏み音をブレンド再生
            if (onSand)
            {
                AudioClip sandClip = isLeft ? _sandStepL : _sandStepR;
                if (sandClip != null)
                {
                    _audioSource.PlayOneShot(sandClip, vol * 0.95f);
                }
            }
        }
    }

    /// <summary>水面着地時の水しぶき音＆エフェクト</summary>
    void PlayWaterLanding()
    {
        float seVol = PlayerPrefs.GetFloat("Adventure_SeVolume", 1.0f);
        if (_waterLanding != null)
        {
            _audioSource.pitch = Random.Range(0.95f, 1.05f);
            _audioSource.PlayOneShot(_waterLanding, volume * 1.55f * seVol);
        }
        SpawnFootstepSplashFx(transform.position, isLeft: true, bigSplash: true);
    }

    /// <summary>小ジャンプ・踏み切り時の軽快なポップホップ音</summary>
    public void PlayJumpSound()
    {
        if (_audioSource == null) return;
        AudioClip clip = _stepR ?? _stepL;
        if (clip != null)
        {
            float seVol = PlayerPrefs.GetFloat("Adventure_SeVolume", 1.0f);
            _audioSource.pitch = 1.35f;
            _audioSource.PlayOneShot(clip, volume * 1.35f * seVol);
        }
    }

    /// <summary>足元の水しぶき・波紋パーティクルエフェクト</summary>
    void SpawnFootstepSplashFx(Vector3 pos, bool isLeft, bool bigSplash)
    {
        var fxGo = new GameObject("WaterSplashFx");
        Vector3 footOffset = (isLeft ? -transform.right : transform.right) * 0.18f;
        Vector3 spawnPos = pos + footOffset;
        spawnPos.y = Mathf.Min(pos.y, 5.92f); // 水面高さ付近
        fxGo.transform.position = spawnPos;

        var ps = fxGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = bigSplash ? 0.45f : 0.28f;
        main.startSpeed = bigSplash ? 3.2f : 1.6f;
        main.startSize = bigSplash ? 0.22f : 0.12f;
        main.startColor = new Color(0.88f, 0.96f, 1.0f, 0.85f); // 澄んだ透明感のある白水滴
        main.gravityModifier = 1.8f; // 重力でパッと跳ね上がって落ちる
        main.loop = false;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, bigSplash ? 22 : 9) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.12f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // 上方向へ跳ね上げ

        var colOverLifetime = ps.colorOverLifetime;
        colOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.7f, 0.9f, 1f), 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colOverLifetime.color = grad;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var curve = new AnimationCurve();
        curve.AddKey(0f, 1f);
        curve.AddKey(1f, 0.2f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        Destroy(fxGo, 0.8f);
    }

    /// <summary>フォールバック用の愛らしいポップトコトコ音波形合成</summary>
    static AudioClip SynthesizeCuteStep(string name, float startFreq, float endFreq)
    {
        const int rate = 44100;
        float duration = 0.12f;
        int count = (int)(rate * duration);
        float[] data = new float[count];
        float phase = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float p = t / duration;
            float freq = Mathf.Lerp(startFreq, endFreq, Mathf.Sqrt(p));
            phase += 2f * Mathf.PI * freq / rate;

            float env = (t < 0.004f) ? (t / 0.004f) : Mathf.Exp(-(t - 0.004f) * 45f) * (1f - p);
            float wave = Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 2f);
            data[i] = Mathf.Clamp(wave * env * 0.9f, -1f, 1f);
        }

        var ac = AudioClip.Create(name, count, 1, rate, false);
        ac.SetData(data, 0);
        return ac;
    }

    /// <summary>澄んだ水しぶき足音・水飛沫波形合成（チャプッ、ピシャッ）</summary>
    static AudioClip SynthesizeWaterSplash(string name, float startFreq, float endFreq, float duration)
    {
        const int rate = 44100;
        int count = (int)(rate * duration);
        float[] data = new float[count];
        float phase = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float p = t / duration;

            // 水の気泡周波数スイープ
            float freq = Mathf.Lerp(startFreq, endFreq, Mathf.Pow(p, 0.7f));
            phase += 2f * Mathf.PI * freq / rate;

            // 高周波水滴バーストノイズ（アタック0.015秒）
            float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 85f);

            // 液体ポップサイン波
            float liquidPop = Mathf.Sin(phase) * Mathf.Exp(-t * 28f);

            // 複合波形
            float env = (t < 0.003f) ? (t / 0.003f) : Mathf.Exp(-(t - 0.003f) * 32f);
            float total = (liquidPop * 0.65f + noise * 0.35f) * env;

            data[i] = Mathf.Clamp(total * 0.92f, -1f, 1f);
        }

        var ac = AudioClip.Create(name, count, 1, rate, false);
        ac.SetData(data, 0);
        return ac;
    }

    /// <summary>乾いた細粒白砂を踏みしめたときの心地よいサクサク砂音（擦過ホワイトノイズ＋低音クッション）</summary>
    static AudioClip SynthesizeSandStep(string name, bool isLeft)
    {
        const int rate = 44100;
        float duration = 0.14f;
        int count = (int)(rate * duration);
        float[] data = new float[count];

        // 左右でわずかにピッチ・音色差をつけて自然な歩行リズム感を演出
        float baseFreq = isLeft ? 180f : 210f;
        Random.InitState(isLeft ? 101 : 202);

        float lastFilter = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float p = t / duration;

            // 乾いた砂粒が擦れ合う微細な粒状ノイズ（バンドパス風の滑らかさ）
            float rawNoise = (Random.value * 2f - 1f);
            float filteredNoise = lastFilter * 0.45f + rawNoise * 0.55f;
            lastFilter = filteredNoise;

            // 砂の踏み込みによる柔らかな低域レゾナンス（足の重みが砂に沈む）
            float lowThump = Mathf.Sin(2f * Mathf.PI * baseFreq * t * (1f - p * 0.4f)) * Mathf.Exp(-t * 36f);

            // 急峻な立ち上がりと自然な減衰エンベロープ
            float env = (t < 0.008f) ? (t / 0.008f) : Mathf.Exp(-(t - 0.008f) * 26f) * (1f - p);

            float total = (filteredNoise * 0.68f + lowThump * 0.32f) * env;
            data[i] = Mathf.Clamp(total * 0.75f, -1f, 1f);
        }

        var ac = AudioClip.Create(name, count, 1, rate, false);
        ac.SetData(data, 0);
        return ac;
    }
}
