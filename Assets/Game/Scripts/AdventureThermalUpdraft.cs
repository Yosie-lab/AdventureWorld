using UnityEngine;

/// <summary>
/// 滑空中のプレイヤーをパラグライダーのように上空へ持ち上げる「上昇気流（サーマル）」
/// 谷間や滝壺、海岸線から立ち上る優しい上昇風の柱
/// </summary>
public class AdventureThermalUpdraft : MonoBehaviour
{
    public float radius = 7.0f;
    public float height = 45.0f;
    public float liftSpeed = 5.2f; // 毎秒+5.2mで上空へ浮遊上昇
    public bool autoLaunch = false; // true＝光の柱専用（ForceSkybreakPillarAscend）。通常サーマルは false
    public bool isMegaVent = false; // true＝70m級メガ・スチームウィンド
    public bool pullToCenter = false; // 水平に中心へ吸い寄せる
    /// <summary>滑空フラグが立つ前でも、気流柱内で空中にいれば上昇（風脈クリスタル用）</summary>
    public bool liftWhenAirborneInColumn = false;
    /// <summary>地上から気流柱に入った瞬間に自動で垂直射出＆滑空展開を行うか</summary>
    public bool enableGroundLaunch = true;
    public float groundLaunchForce = 18.5f;

    float _lastGroundLaunchTime = -5f;
    ParticleSystem _windPs;
    AudioSource _audio;
    AudioSource _seAudio;
    static AudioClip _shubonClip;

    void Start()
    {
        CreateWindFx();
        SetupTrigger();
        SetupAudio();
    }

    void SetupTrigger()
    {
        var col = gameObject.AddComponent<CapsuleCollider>();
        col.isTrigger = true;
        col.radius = radius;
        // Unity Capsule は height >= 2*radius 必須。巨大柱でも確実に届く高さへ
        col.height = Mathf.Max(height, radius * 2f + 1f);
        col.center = new Vector3(0f, col.height * 0.5f, 0f);
        var rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void Update()
    {
        var player = AdventurePlayerController.Instance;
        if (player == null) return;

        Vector3 pos = transform.position;
        Vector3 pPos = player.transform.position;

        // XZ平面の距離と高さ範囲を判定（上限は視覚高さより少し余裕）
        float distXZ = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(pPos.x, pPos.z));
        float effectiveRadius = autoLaunch ? radius * 1.75f : radius;
        bool inY = pPos.y >= pos.y - 4.0f && pPos.y <= pos.y + height + 80f;

        if (distXZ < effectiveRadius && inY)
            ApplyLiftToPlayer(player, distXZ);
        else if (_audio != null && _audio.volume > 0f)
            _audio.volume = Mathf.MoveTowards(_audio.volume, 0f, Time.deltaTime * 2.0f);
    }

    void OnTriggerStay(Collider other)
    {
        var player = other.GetComponent<AdventurePlayerController>()
            ?? other.GetComponentInParent<AdventurePlayerController>();
        if (player == null) return;

        Vector3 pos = transform.position;
        Vector3 pPos = player.transform.position;
        float distXZ = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(pPos.x, pPos.z));
        ApplyLiftToPlayer(player, distXZ);
    }

    void ApplyLiftToPlayer(AdventurePlayerController player, float distXZ)
    {
        if (autoLaunch)
        {
            // 光の柱：物理に頼らず中心＋上昇を強制（途中引っかかり対策）
            player.ForceSkybreakPillarAscend(transform.position, liftSpeed, pullToCenter);
            if (_audio != null)
                _audio.volume = Mathf.MoveTowards(_audio.volume, 0.45f, Time.deltaTime * 3.5f);
            return;
        }

        // 接地中は自由に歩けるようにし、空中でも地上射出直後は脱出・前進を妨げないよう引き戻さない
        if (pullToCenter && distXZ > 0.35f && !player.IsGrounded && Time.time > _lastGroundLaunchTime + 1.5f)
        {
            Vector3 p = player.transform.position;
            Vector3 center = new Vector3(transform.position.x, p.y, transform.position.z);
            Vector3 pull = (center - p);
            pull.y = 0f;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null && cc.enabled)
                cc.Move(pull.normalized * Mathf.Min(distXZ, 6f * Time.deltaTime));
        }

        bool canLift = player.IsGliding
                       || (liftWhenAirborneInColumn && !player.IsGrounded);
        if (canLift)
        {
            player.ApplyUpdraft(liftSpeed);
            if (_audio != null)
                _audio.volume = Mathf.MoveTowards(_audio.volume, isMegaVent ? 0.65f : 0.45f, Time.deltaTime * 3.5f);
        }
        else if (enableGroundLaunch && player.IsGrounded && distXZ < radius * 0.75f && Time.time > _lastGroundLaunchTime + 1.2f)
        {
            // 地上からスチーム・上昇気流に入った瞬間に大ジャンプ射出＆滑空自動展開
            _lastGroundLaunchTime = Time.time;
            player.LaunchFromSteamVent(groundLaunchForce);

            // 「シュボーン！！」という大迫力のジェット気流音を再生
            PlayLaunchSound();
        }
    }

    void PlayLaunchSound()
    {
        if (_seAudio == null)
        {
            _seAudio = gameObject.AddComponent<AudioSource>();
            _seAudio.spatialBlend = 0.1f; // 爽快なダイレクト2D寄りサウンド
            _seAudio.volume = 1.0f;
            _seAudio.playOnAwake = false;
        }

        if (_shubonClip == null)
            _shubonClip = SynthesizeShubonLaunchClip();

        float seVol = PlayerPrefs.GetFloat("Adventure_SeVolume", 1f);
        _seAudio.pitch = isMegaVent ? 0.92f : 1.15f; // メガは低く重厚、通常は軽快
        _seAudio.PlayOneShot(_shubonClip, (isMegaVent ? 1.0f : 0.75f) * seVol);
    }

    void CreateWindFx()
    {
        var pGo = new GameObject("ThermalParticles");
        pGo.transform.SetParent(transform, false);
        pGo.transform.localPosition = Vector3.zero;

        _windPs = pGo.AddComponent<ParticleSystem>();
        var main = _windPs.main;
        main.loop = true;
        main.startLifetime = isMegaVent ? 3.6f : 2.8f;
        main.startSpeed = isMegaVent ? 26.0f : 13.5f; // メガは上空70m以上まで一気に立ち昇る
        main.startSize = isMegaVent ? new ParticleSystem.MinMaxCurve(0.35f, 0.95f) : new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
        main.startColor = isMegaVent ? new Color(0.25f, 1.0f, 0.85f, 0.35f) : new Color(0.35f, 1.0f, 0.88f, 0.22f);
        main.gravityModifier = isMegaVent ? -0.28f : -0.15f; // 上向き急加速
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = _windPs.emission;
        emission.rateOverTime = isMegaVent ? 42f : 22f;

        var shape = _windPs.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * (isMegaVent ? 0.95f : 0.90f);
        shape.radiusThickness = 0.35f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // 上向き

        var colorOverLife = _windPs.colorOverLifetime;
        colorOverLife.enabled = true;
        var grad = new Gradient();
        Color topColor = isMegaVent ? new Color(0.15f, 0.95f, 1.0f) : new Color(0.4f, 1.0f, 0.9f);
        grad.SetKeys(
            new[] { new GradientColorKey(topColor, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(isMegaVent ? 0.38f : 0.25f, 0.15f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLife.color = grad;

        // らせん回転ノイズ
        var noise = _windPs.noise;
        noise.enabled = true;
        noise.strength = isMegaVent ? 0.65f : 0.45f;
        noise.frequency = 0.16f;
        noise.scrollSpeed = isMegaVent ? 0.40f : 0.25f;

        var rend = pGo.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            var pShader = Shader.Find("RustAndFloat/WhiteSmoke") ?? Shader.Find("Sprites/Default");
            var pMat = new Material(pShader);
            pMat.SetTexture("_BaseMap", AdventureRustDrone.GetSoftSmokeTexture());
            pMat.SetColor("_BaseColor", isMegaVent ? new Color(0.2f, 1.0f, 0.9f, 0.45f) : new Color(0.3f, 1.0f, 0.88f, 0.35f));
            rend.material = pMat;
        }

        // メガベントなら、上空へと吹き抜ける螺旋の光の粒子（スパークル）を追加
        if (isMegaVent)
        {
            var sparkleGo = new GameObject("MegaSparkles");
            sparkleGo.transform.SetParent(transform, false);
            sparkleGo.transform.localPosition = Vector3.zero;

            var sps = sparkleGo.AddComponent<ParticleSystem>();
            var sMain = sps.main;
            sMain.loop = true;
            sMain.startLifetime = 3.2f;
            sMain.startSpeed = 30.0f;
            sMain.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
            sMain.startColor = new Color(0.3f, 1.0f, 0.95f, 0.9f);
            sMain.simulationSpace = ParticleSystemSimulationSpace.World;

            var sEmission = sps.emission;
            sEmission.rateOverTime = 25f;

            var sShape = sps.shape;
            sShape.shapeType = ParticleSystemShapeType.Circle;
            sShape.radius = radius * 0.75f;
            sShape.rotation = new Vector3(-90f, 0f, 0f);

            var sRend = sparkleGo.GetComponent<ParticleSystemRenderer>();
            if (sRend != null && rend != null)
                sRend.material = rend.material;
        }
    }

    void SetupAudio()
    {
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.spatialBlend = 1.0f;
        _audio.minDistance = 3f;
        _audio.maxDistance = radius * 2.5f;
        _audio.loop = true;
        _audio.volume = 0f;
        _audio.clip = MakeThermalWindClip();
        _audio.Play();
    }

    void OnTriggerExit(Collider other)
    {
        var player = other.GetComponent<AdventurePlayerController>()
            ?? other.GetComponentInParent<AdventurePlayerController>();
        if (player != null && _audio != null)
        {
            _audio.volume = 0f;
        }
    }

    static AudioClip MakeThermalWindClip()
    {
        const int rate = 22050;
        int count = (int)(rate * 1.5f);
        float[] data = new float[count];
        var rng = new System.Random(1337);

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float sine = Mathf.Sin(t * Mathf.PI * 2f * 65f); // 温かい低周波
            data[i] = (noise * 0.4f + sine * 0.6f) * 0.22f;
        }

        var clip = AudioClip.Create("ThermalWindLoop", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>「シュボーン！！」という爽快・大迫力のスチームジェット噴射・上昇気流SE合成</summary>
    static AudioClip SynthesizeShubonLaunchClip()
    {
        const int rate = 44100;
        float duration = 1.15f;
        int count = (int)(rate * duration);
        float[] data = new float[count];
        var rng = new System.Random(777);

        float prevNoise = 0f;
        float phaseSub = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;

            // 1. 低音インパクト（発射瞬間の「ドムッ」という重圧）
            phaseSub += 2f * Mathf.PI * Mathf.Lerp(85f, 45f, t) / rate;
            float subImpact = Mathf.Sin(phaseSub) * Mathf.Exp(-t * 8f) * 0.75f;

            // 2. 「シュウゥゥーン！」という高圧スチーム噴射ノイズ（レゾナンススイープ）
            float rawNoise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float filterAlpha = Mathf.Lerp(0.08f, 0.35f, Mathf.Sin(t * Mathf.PI * 0.75f));
            float smoothNoise = Mathf.Lerp(prevNoise, rawNoise, filterAlpha);
            prevNoise = smoothNoise;

            // 音量エンベロープ（一気に立ち上がり、爽快に抜けていく）
            float env = 0f;
            if (t < 0.08f) env = t / 0.08f;
            else env = Mathf.Exp(-(t - 0.08f) * 3.2f);

            // チャイムのような澄んだ高周波倍音
            float chime = Mathf.Sin(t * Mathf.PI * 2f * 1480f) * Mathf.Exp(-t * 4.5f) * 0.25f;

            data[i] = Mathf.Clamp((subImpact * 0.65f + smoothNoise * 0.75f + chime) * env * 0.85f, -1f, 1f);
        }

        var clip = AudioClip.Create("SteamShubonLaunch", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
