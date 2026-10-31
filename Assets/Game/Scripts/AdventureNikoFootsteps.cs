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

    // 着地インパクト音クリップ（案A：陸地＆砂浜のソフトクッション着地）
    AudioClip _landImpact;
    AudioClip _sandLanding;

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

        // 着地インパクト音（案A：柔らかな土煙クッション音＆白砂ザフッ沈み込み音）
        _landImpact = SynthesizeLandImpact("LandImpact");
        _sandLanding = SynthesizeSandLanding("SandLanding");
    }

    void Update()
    {
        if (_audioSource == null) return;

        // 接地＆滑空判定（空中や滑空時は自然に消音）
        bool isGrounded = (_player != null) ? _player.IsGrounded : (_cc != null && _cc.isGrounded);
        bool isGliding = (_player != null && _player.IsGliding);

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

    /// <summary>Nikoが波打ち際や池・湖の水中に足を踏み入れているかを正確に判定</summary>
    bool CheckIsInWater(Vector3 pos)
    {
        // 1. 海面・波打ち際（正規海面水位 5.50m、波打ち際 5.62m 以下）
        // 標高が 5.62m 以下であり、かつ海岸線外周エリア（西側白砂ビーチ浅瀬など）にある場合のみ水音
        if (pos.y <= 5.62f)
        {
            var land = Terrain.activeTerrain;
            float landH = land != null ? (land.SampleHeight(pos) + land.transform.position.y) : pos.y;
            // 地形標高が海面（5.50m）付近またはそれ以下、かつ西海岸または外周海域
            if (landH <= 5.62f && (pos.x <= 165f || pos.x >= 850f || pos.z <= 160f || pos.z >= 850f))
                return true;
        }

        // 2. 内陸オアシス池・段々池の浅瀬（標高47.8m〜48.7m付近）
        Vector3 oasisPos = new Vector3(480f, 48.0f, 455f);
        if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(oasisPos.x, oasisPos.z)) < 18f)
        {
            if (pos.y < 48.75f) return true;
        }

        // 3. カルデラ湖（標高25.5m付近）
        Vector3 calderaPos = new Vector3(420f, 25.5f, 440f);
        if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(calderaPos.x, calderaPos.z)) < 40f)
        {
            if (pos.y <= 25.8f) return true;
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
            // 水辺・波打ち際：ピチャッ、チャプッという涼やかな水しぶき足音
            clip = isLeft ? _waterStepL : _waterStepR;
            SpawnFootstepSplashFx(footPos, isLeft, bigSplash: false);
        }
        else if (onSand)
        {
            // 白砂ビーチ：サクッ、サクッという細粒砂の心地よい砂踏み足音
            clip = isLeft ? _sandStepL : _sandStepR;
        }
        else
        {
            // 通常陸地：愛らしいトコトコ音
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

    static Material _dustMat;
    static Material GetDustMaterial()
    {
        if (_dustMat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                         ?? Shader.Find("Universal Render Pipeline/Unlit") 
                         ?? Shader.Find("Mobile/Particles/Alpha Blended")
                         ?? Shader.Find("Particles/Standard Unlit");
            if (shader == null) return null;
            _dustMat = new Material(shader) { name = "FootstepDust_Mat" };
            var tex = AdventureRustDrone.GetSoftSmokeTexture();
            _dustMat.SetTexture("_BaseMap", tex);
            _dustMat.mainTexture = tex;
            _dustMat.SetFloat("_Surface", 1f); // Transparent
            _dustMat.SetFloat("_Blend", 0f); // Alpha blend
            _dustMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _dustMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _dustMat.SetInt("_ZWrite", 0);
            _dustMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _dustMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            _dustMat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.7f));
            _dustMat.color = new Color(1f, 1f, 1f, 0.7f);
        }
        return _dustMat;
    }

    /// <summary>
    /// 案A：70m大滑空や急降下着地時の「ランディング・インパクトJUICE」。
    /// 落下速度と滞空時間に応じた着地SE、足元ダスト煙リング、心地よいカメラバンプを一括演出。
    /// </summary>
    public void PlayLandImpact(float airborneDuration, float fallSpeed, bool wasGliding)
    {
        if (_audioSource == null) return;
        Vector3 footPos = transform.position;

        // 水面浅瀬着地
        if (CheckIsInWater(footPos))
        {
            PlayWaterLanding();
            return;
        }

        // わずかな段差歩行（滞空0.16秒未満かつ低速）は無視して足音に任せる
        if (airborneDuration < 0.16f && fallSpeed < 3.0f) return;

        bool onSand = AdventureBeachVisualEnhancer.CheckIsOnSandBeach(footPos);
        float seVol = PlayerPrefs.GetFloat("Adventure_SeVolume", 1.0f);

        // インパクト強度（0.0〜1.0）
        float impactStrength = Mathf.Clamp01(Mathf.Max(
            Mathf.InverseLerp(0.20f, 1.4f, airborneDuration),
            Mathf.InverseLerp(3.5f, 16.0f, fallSpeed)
        ));

        // 着地SE再生
        AudioClip clip = onSand ? _sandLanding : _landImpact;
        if (clip != null)
        {
            float vol = Mathf.Lerp(0.28f, 0.72f, impactStrength) * volume * 2.2f * seVol;
            _audioSource.pitch = Random.Range(0.95f, 1.05f) - (impactStrength * 0.06f);
            _audioSource.PlayOneShot(clip, Mathf.Clamp01(vol));
        }

        // 足元ダスト煙リング生成
        SpawnLandingDustFx(footPos, impactStrength, onSand);

        // 高所落下・急降下着地・大滑空着地時のカメラバンプ（JUICE演出）
        if (impactStrength > 0.32f || fallSpeed > 6.5f || wasGliding)
        {
            float shakeIntensity = Mathf.Lerp(0.04f, 0.18f, impactStrength);
            AdventureCameraFollow.Instance?.Shake(shakeIntensity, 0.12f);
        }
    }

    /// <summary>着地時に足元から外周へドーナツ状に広がるダスト煙リングエフェクト</summary>
    void SpawnLandingDustFx(Vector3 pos, float impactStrength, bool onSand)
    {
        var fxGo = new GameObject("LandingDustFx");
        fxGo.transform.position = pos + Vector3.up * 0.05f;

        var ps = fxGo.AddComponent<ParticleSystem>();
        var psr = fxGo.GetComponent<ParticleSystemRenderer>();
        var dustMat = GetDustMaterial();
        if (psr != null && dustMat != null)
        {
            psr.sharedMaterial = dustMat;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;
        }

        var main = ps.main;
        main.startLifetime = Mathf.Lerp(0.32f, 0.48f, impactStrength);
        main.startSpeed = Mathf.Lerp(1.2f, 3.2f, impactStrength);
        main.startSize = Mathf.Lerp(0.28f, 0.68f, impactStrength);
        // 砂浜は温かみのある白砂クリーム、陸地は柔らかなオフホワイト
        Color dustColor = onSand ? new Color(0.96f, 0.93f, 0.85f, 0.65f) : new Color(0.92f, 0.92f, 0.90f, 0.60f);
        main.startColor = dustColor;
        main.gravityModifier = -0.04f;
        main.loop = false;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        int particleCount = (int)Mathf.Lerp(8, 22, impactStrength);
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, particleCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.28f;
        shape.rotation = new Vector3(90f, 0f, 0f); // 水平リング状に全方向へ拡散

        var colOverLifetime = ps.colorOverLifetime;
        colOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(dustColor, 1f) },
            new[] { new GradientAlphaKey(dustColor.a, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colOverLifetime.color = grad;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var curve = new AnimationCurve();
        curve.AddKey(0f, 0.5f);
        curve.AddKey(0.4f, 1.0f);
        curve.AddKey(1f, 1.35f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        Destroy(fxGo, 0.8f);
    }

    static Material _splashMat;
    static Material GetSplashMaterial()
    {
        if (_splashMat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                         ?? Shader.Find("Universal Render Pipeline/Unlit") 
                         ?? Shader.Find("Mobile/Particles/Additive")
                         ?? Shader.Find("Particles/Standard Unlit");
            if (shader == null) return null;
            _splashMat = new Material(shader) { name = "FootstepSplash_Mat" };
            var tex = AdventureRustDrone.GetSoftSmokeTexture();
            _splashMat.SetTexture("_BaseMap", tex);
            _splashMat.mainTexture = tex;
            _splashMat.SetFloat("_Surface", 1f); // Transparent
            _splashMat.SetFloat("_Blend", 1f); // Additive
            _splashMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _splashMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            _splashMat.SetInt("_ZWrite", 0);
            _splashMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _splashMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            _splashMat.SetColor("_BaseColor", new Color(0.9f, 0.96f, 1f, 0.8f));
            _splashMat.color = new Color(0.9f, 0.96f, 1f, 0.8f);
        }
        return _splashMat;
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
        var psr = fxGo.GetComponent<ParticleSystemRenderer>();
        var splashMat = GetSplashMaterial();
        if (psr != null && splashMat != null)
        {
            psr.sharedMaterial = splashMat;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;
        }

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

    /// <summary>陸地着地用の柔らかくコシのあるドフッという衝撃クッション音波形合成</summary>
    static AudioClip SynthesizeLandImpact(string name)
    {
        const int rate = 44100;
        float duration = 0.22f;
        int count = (int)(rate * duration);
        float[] data = new float[count];

        float phase = 0f;
        float lastFilter = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float p = t / duration;

            // 115Hzから48Hzへ落ちる低周波ボディインパクト
            float freq = Mathf.Lerp(115f, 48f, Mathf.Sqrt(p));
            phase += 2f * Mathf.PI * freq / rate;
            float lowThump = Mathf.Sin(phase);

            // 着地時の布擦れ・衣類クッション微細ノイズ
            float rawNoise = (Random.value * 2f - 1f);
            float filteredNoise = lastFilter * 0.65f + rawNoise * 0.35f;
            lastFilter = filteredNoise;

            // エンベロープ
            float env = (t < 0.005f) ? (t / 0.005f) : Mathf.Exp(-(t - 0.005f) * 28f) * (1f - p);

            float total = (lowThump * 0.65f + filteredNoise * 0.35f) * env;
            data[i] = Mathf.Clamp(total * 0.88f, -1f, 1f);
        }

        var ac = AudioClip.Create(name, count, 1, rate, false);
        ac.SetData(data, 0);
        return ac;
    }

    /// <summary>白砂ビーチ着地用のザフッと沈み込む細粒砂クッション音波形合成</summary>
    static AudioClip SynthesizeSandLanding(string name)
    {
        const int rate = 44100;
        float duration = 0.25f;
        int count = (int)(rate * duration);
        float[] data = new float[count];

        float phase = 0f;
        float lastFilter = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float p = t / duration;

            // 135Hzから52Hzの柔らかな砂めり込み低音
            float freq = Mathf.Lerp(135f, 52f, Mathf.Pow(p, 0.7f));
            phase += 2f * Mathf.PI * freq / rate;
            float lowThump = Mathf.Sin(phase);

            // 砂粒が広がる心地よいザフッというホワイトノイズ
            float rawNoise = (Random.value * 2f - 1f);
            float filteredNoise = lastFilter * 0.50f + rawNoise * 0.50f;
            lastFilter = filteredNoise;

            // エンベロープ
            float env = (t < 0.006f) ? (t / 0.006f) : Mathf.Exp(-(t - 0.006f) * 22f) * (1f - p);

            float total = (lowThump * 0.48f + filteredNoise * 0.52f) * env;
            data[i] = Mathf.Clamp(total * 0.85f, -1f, 1f);
        }

        var ac = AudioClip.Create(name, count, 1, rate, false);
        ac.SetData(data, 0);
        return ac;
    }
}
