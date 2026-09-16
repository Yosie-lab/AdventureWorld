using UnityEngine;
using System.Collections;

/// <summary>
/// 滑空中にくぐると加速ブーストがかかる「風のリング」
/// エメラルドグリーンに輝く巨大な気流トンネル（直径約7m）
/// </summary>
public class AdventureWindRing : MonoBehaviour
{
    public float boostMultiplier = 1.95f;
    public float boostDuration = 3.5f;
    public Color ringColor = new Color(0.15f, 1.0f, 0.70f); // 鮮烈なエメラルドシアン

    Transform _ringVisual;
    Transform _beaconPillar;
    Material _ringMat;
    bool _isCooldown = false;
    AudioSource _audio;
    static AudioClip _boostClip;

    void Start()
    {
        CreateRingVisual();
        CreateBeaconPillar();
        SetupAudio();

        // 物理トリガー＋Rigidbody（CharacterControllerとの確実な接触用）
        var col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 4.5f;

        var rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void CreateRingVisual()
    {
        var visualGo = new GameObject("RingVisual");
        visualGo.transform.SetParent(transform, false);
        _ringVisual = visualGo.transform;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        _ringMat = new Material(shader);
        _ringMat.SetColor("_BaseColor", ringColor);
        _ringMat.EnableKeyword("_EMISSION");
        _ringMat.SetColor("_EmissionColor", ringColor * 2.8f); // 昼間でも圧倒的に目立つ強烈な発光
        _ringMat.SetFloat("_Metallic", 0.85f);
        _ringMat.SetFloat("_Smoothness", 0.95f);

        // 直径約7.0m、太さ0.5mの迫力ある16セグメント円環
        int segments = 20;
        float radius = 3.5f;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * (Mathf.PI * 2f / segments);
            Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            var seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            seg.name = "Seg_" + i;
            seg.transform.SetParent(_ringVisual, false);
            seg.transform.localPosition = pos;
            seg.transform.localScale = new Vector3(0.55f, 0.65f, 0.55f);

            float deg = angle * Mathf.Rad2Deg;
            seg.transform.localRotation = Quaternion.Euler(0f, 0f, deg + 90f);
            Destroy(seg.GetComponent<Collider>());

            var rend = seg.GetComponent<Renderer>();
            if (rend != null)
                rend.material = _ringMat;
        }

        // リング周囲を回転する風の気流パーティクル
        var pGo = new GameObject("RingParticles");
        pGo.transform.SetParent(transform, false);
        var ps = pGo.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.startLifetime = 1.4f;
        main.startSpeed = 4.5f;
        main.startSize = 0.45f;
        main.startColor = new Color(0.3f, 1.0f, 0.85f, 0.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 24f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;

        var rendPs = pGo.GetComponent<ParticleSystemRenderer>();
        if (rendPs != null)
        {
            var pShader = Shader.Find("RustAndFloat/WhiteSmoke") ?? Shader.Find("Sprites/Default");
            var pMat = new Material(pShader);
            pMat.SetTexture("_BaseMap", AdventureRustDrone.GetSoftSmokeTexture());
            pMat.SetColor("_BaseColor", ringColor * 2.0f);
            rendPs.material = pMat;
        }
    }

    void CreateBeaconPillar()
    {
        // 遠くからでも一目で場所がわかる天空への光の柱（高さ15m）
        var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "FlightBeacon";
        beacon.transform.SetParent(transform, false);
        beacon.transform.localPosition = new Vector3(0f, 7.5f, 0f);
        beacon.transform.localScale = new Vector3(0.25f, 7.5f, 0.25f);
        Destroy(beacon.GetComponent<Collider>());

        var rend = beacon.GetComponent<Renderer>();
        if (rend != null)
        {
            var pShader = Shader.Find("RustAndFloat/WhiteSmoke") ?? Shader.Find("Sprites/Default");
            var pMat = new Material(pShader);
            pMat.SetTexture("_BaseMap", AdventureRustDrone.GetSoftSmokeTexture());
            Color bCol = ringColor;
            bCol.a = 0.55f;
            pMat.SetColor("_BaseColor", bCol);
            pMat.renderQueue = 3150;
            rend.material = pMat;
        }
        _beaconPillar = beacon.transform;
    }

    void SetupAudio()
    {
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.spatialBlend = 0.0f; // 爽快なダイレクト2Dサウンド
        _audio.volume = 0.85f;
        _audio.playOnAwake = false;

        if (_boostClip == null)
            _boostClip = SynthesizeWindBoostClip();
    }

    void Update()
    {
        if (_ringVisual != null)
        {
            // リングの自転とふんわり脈動
            _ringVisual.Rotate(Vector3.forward, 40f * Time.deltaTime, Space.Self);
            float pulse = 1.0f + Mathf.Sin(Time.time * 3.5f) * 0.06f;
            _ringVisual.localScale = Vector3.one * pulse;
        }

        if (_beaconPillar != null)
        {
            float bPulse = 1.0f + Mathf.Sin(Time.time * 2.8f) * 0.15f;
            _beaconPillar.localScale = new Vector3(0.25f * bPulse, 7.5f, 0.25f * bPulse);
        }

        // 確実な通過感知（巨大な円筒ゲート判定：半径5.2m、前後厚み3.2m）
        if (!_isCooldown)
        {
            var player = AdventurePlayerController.Instance;
            if (player != null)
            {
                Vector3 playerCenter = player.transform.position + Vector3.up * 0.9f;
                // リングのローカル空間に変換
                Vector3 localPos = transform.InverseTransformPoint(playerCenter);
                float radiusDist = Mathf.Sqrt(localPos.x * localPos.x + localPos.y * localPos.y);
                float zDist = Mathf.Abs(localPos.z);

                // 半径5.2m以内（リングの内外）かつ厚み前後3.2m以内を通過
                if (radiusDist <= 5.2f && zDist <= 3.2f)
                {
                    TriggerBoost(player);
                }
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_isCooldown) return;

        var player = other.GetComponent<AdventurePlayerController>()
            ?? other.GetComponentInParent<AdventurePlayerController>();
        if (player != null)
        {
            TriggerBoost(player);
        }
    }

    void TriggerBoost(AdventurePlayerController player)
    {
        // 滑空中の時のみ風のリングの空中加速ブーストを発動（地上歩行中の意図しない浮遊を防止）
        if (player == null || !player.IsGliding)
            return;

        _isCooldown = true;

        // プレイヤーに前進ロケット加速ブーストを付与（リングの貫通方向へ猛烈に射出！）
        player.ApplyGlideBoost(boostMultiplier, boostDuration, transform.forward);

        // 爽快な風切りブースト効果音
        if (_audio != null && _boostClip != null)
            _audio.PlayOneShot(_boostClip);

        // 相棒Rustのリアクション＋光るリング通過ボーナス油（量は都度ランダム）
        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
            drone.OnFloatWindCaught(); // 内部で油量をランダム決定

        // リングショックウェーブ演出
        StartCoroutine(ShockwaveAndCooldown());
    }

    IEnumerator ShockwaveAndCooldown()
    {
        float elapsed = 0f;
        Vector3 startScale = _ringVisual.localScale;
        while (elapsed < 0.35f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.35f;
            _ringVisual.localScale = Vector3.Lerp(startScale, startScale * 1.8f, t);
            if (_ringMat != null)
                _ringMat.SetColor("_EmissionColor", ringColor * Mathf.Lerp(4.5f, 0.1f, t));
            yield return null;
        }

        _ringVisual.gameObject.SetActive(false);

        // クールダウン（3.0秒後に再点灯）
        yield return new WaitForSeconds(3.0f);

        _ringVisual.localScale = startScale;
        if (_ringMat != null)
            _ringMat.SetColor("_EmissionColor", ringColor * 2.8f);
        _ringVisual.gameObject.SetActive(true);
        _isCooldown = false;
    }

    /// <summary>「サァァー…ン」という柔らかく澄んだ自然の風のそよぎとチャイム音の合成</summary>
    static AudioClip SynthesizeWindBoostClip()
    {
        const int rate = 44100;
        float duration = 0.85f;
        int count = (int)(rate * duration);
        float[] data = new float[count];
        var rng = new System.Random(42);

        float phaseChime1 = 0f;
        float phaseChime2 = 0f;
        float prevNoise = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;

            // 柔らかな風のホワイトノイズ（ローパス気味）
            float rawNoise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float smoothNoise = Mathf.Lerp(prevNoise, rawNoise, 0.18f);
            prevNoise = smoothNoise;

            // 澄んだアコースティックな風鈴・チャイムの微かな倍音（F#とC#の調和）
            phaseChime1 += 2f * Mathf.PI * 740f / rate;
            phaseChime2 += 2f * Mathf.PI * 1110f / rate;
            float chime = (Mathf.Sin(phaseChime1) * 0.4f + Mathf.Sin(phaseChime2) * 0.25f) * Mathf.Exp(-t * 3.5f);

            float env = Mathf.Sin(t * Mathf.PI);
            env = Mathf.Pow(env, 0.65f);

            data[i] = (smoothNoise * 0.4f + chime * 0.35f) * env * 0.55f;
        }

        var clip = AudioClip.Create("OrganicWindFloat", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
