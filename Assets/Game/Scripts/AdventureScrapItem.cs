using UnityEngine;

/// <summary>
/// 島に散らばる漂着パーツ（古代ギア・エネルギーコア）
/// 発光しながら優雅に浮遊・回転し、プレイヤーが近づくと吸い寄せられて気持ちよく取得できる
/// </summary>
public class AdventureScrapItem : MonoBehaviour
{
    public int itemId;
    public string itemName = "古代のギア";
    public Color itemColor = new Color(1.0f, 0.78f, 0.28f); // 黄金に輝くギア

    Transform _model;
    Transform _beaconPillar;
    Vector3 _initialPos;
    float _hoverOffset;
    bool _isCollected = false;
    public bool IsCollected => _isCollected;
    AudioSource _audioSource;
    static AudioClip _collectClip;

    void Start()
    {
        _initialPos = transform.position;
        _hoverOffset = Random.Range(0f, Mathf.PI * 2f);

        CreateModel();
        CreateBeacon();
        CreateIdleSparkles();
        SetupAudio();
    }

    void CreateModel()
    {
        var modelGo = new GameObject("Visual");
        modelGo.transform.SetParent(transform, false);
        _model = modelGo.transform;

        // ギア・コアの3Dモデル（視認性向上のため1.5倍サイズ：ハブ 0.85m）
        var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hub.name = "GearHub";
        hub.transform.SetParent(_model, false);
        hub.transform.localScale = new Vector3(0.85f, 0.12f, 0.85f);
        Destroy(hub.GetComponent<Collider>());

        // 歯車用の突起（4つの突起、直径約1.15mの堂々たるシルエット）
        for (int i = 0; i < 4; i++)
        {
            var tooth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tooth.name = "Tooth_" + i;
            tooth.transform.SetParent(_model, false);
            tooth.transform.localRotation = Quaternion.Euler(0f, i * 45f, 0f);
            tooth.transform.localScale = new Vector3(1.15f, 0.10f, 0.26f);
            Destroy(tooth.GetComponent<Collider>());
            ApplyMaterial(tooth.GetComponent<Renderer>());
        }

        // 中心のエネルギーコア球体
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        core.transform.SetParent(_model, false);
        core.transform.localScale = Vector3.one * 0.44f;
        Destroy(core.GetComponent<Collider>());

        ApplyMaterial(hub.GetComponent<Renderer>());
        ApplyCoreMaterial(core.GetComponent<Renderer>());

        // コライダー（接近感知用トリガー：3.5m以内で吸い寄せ開始）
        var col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 3.5f;
    }

    void CreateBeacon()
    {
        // 遠くからでも山や木立の向こうから一目でわかる天空への光の柱（高さ35m）
        var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "BeaconPillar";
        beacon.transform.SetParent(transform, false);
        beacon.transform.localPosition = new Vector3(0f, 17.5f, 0f);
        beacon.transform.localScale = new Vector3(0.40f, 17.5f, 0.40f);
        Destroy(beacon.GetComponent<Collider>());

        var rend = beacon.GetComponent<Renderer>();
        if (rend != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("RustAndFloat/WhiteSmoke")
                ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.SetTexture("_BaseMap", AdventureRustDrone.GetSoftSmokeTexture());
            Color bCol = itemColor;
            bCol.a = 0.70f;
            mat.SetColor("_BaseColor", bCol);
            mat.renderQueue = 3150;
            rend.material = mat;
        }
        _beaconPillar = beacon.transform;

        // 周囲の地面や草木を照らし出す自発光ポイントライト
        var light = gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = itemColor;
        light.range = 16f;
        light.intensity = 2.8f;
    }

    void CreateIdleSparkles()
    {
        // 1. アイテム周囲の浮遊スパークル
        var pGo = new GameObject("IdleSparkles");
        pGo.transform.SetParent(transform, false);
        pGo.transform.localPosition = Vector3.zero;
        var ps = pGo.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.startLifetime = 1.8f;
        main.startSpeed = 0.25f;
        main.startSize = 0.22f;
        main.startColor = itemColor * 2.0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 12f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.85f;

        var rend = pGo.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("RustAndFloat/WhiteSmoke")
                ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.SetTexture("_BaseMap", AdventureRustDrone.GetSoftSmokeTexture());
            mat.SetColor("_BaseColor", itemColor * 2.5f);
            rend.material = mat;
        }

        // 2. 天に向かって垂直に昇る光の粒子ビーム（遠景からもハッキリ視認可能）
        var beamGo = new GameObject("VerticalBeamSparkles");
        beamGo.transform.SetParent(transform, false);
        beamGo.transform.localPosition = Vector3.zero;
        var psBeam = beamGo.AddComponent<ParticleSystem>();

        var mainBeam = psBeam.main;
        mainBeam.loop = true;
        mainBeam.startLifetime = 2.5f;
        mainBeam.startSpeed = 12.0f; // 上空へぐんぐん昇る
        mainBeam.startSize = 0.35f;
        mainBeam.startColor = itemColor * 2.2f;
        mainBeam.simulationSpace = ParticleSystemSimulationSpace.World;

        var emissionBeam = psBeam.emission;
        emissionBeam.rateOverTime = 16f;

        var shapeBeam = psBeam.shape;
        shapeBeam.shapeType = ParticleSystemShapeType.Cone;
        shapeBeam.angle = 1.5f;
        shapeBeam.radius = 0.3f;
        shapeBeam.rotation = new Vector3(-90f, 0f, 0f); // 真上に向ける

        var rendBeam = beamGo.GetComponent<ParticleSystemRenderer>();
        if (rendBeam != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("RustAndFloat/WhiteSmoke")
                ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.SetTexture("_BaseMap", AdventureRustDrone.GetSoftSmokeTexture());
            mat.SetColor("_BaseColor", itemColor * 2.8f);
            rendBeam.material = mat;
        }
    }

    void ApplyMaterial(Renderer rend)
    {
        if (rend == null) return;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.SetColor("_BaseColor", itemColor);
        mat.SetFloat("_Metallic", 0.9f);
        mat.SetFloat("_Smoothness", 0.85f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", itemColor * 1.6f); // 強烈な黄金エミッション
        rend.material = mat;
    }

    void ApplyCoreMaterial(Renderer rend)
    {
        if (rend == null) return;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        Color cyanCore = new Color(0.2f, 0.95f, 1.0f);
        mat.SetColor("_BaseColor", cyanCore);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", cyanCore * 2.8f); // 鮮やかなコア発光
        rend.material = mat;
    }

    void SetupAudio()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0.0f; // 2D音響で耳元に気持ちよく響く
        _audioSource.volume = 0.75f;
        _audioSource.playOnAwake = false;

        if (_collectClip == null)
            _collectClip = SynthesizeCollectChime();
    }

    Transform _carriedByDrone;

    public void AttachToDrone(Transform drone)
    {
        _carriedByDrone = drone;
        if (_beaconPillar != null)
            _beaconPillar.gameObject.SetActive(false); // 運搬中は柱を消してスマートに
    }

    void Update()
    {
        if (_isCollected) return;

        // ドローン運搬中の追従
        if (_carriedByDrone != null)
        {
            transform.position = _carriedByDrone.position - Vector3.up * 0.42f;
            if (_model != null)
                _model.localRotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
            return;
        }

        // 浮遊アニメーション（上下ホバー ＆ 優雅な回転）
        float t = Time.time + _hoverOffset;
        if (_model != null)
        {
            _model.localPosition = new Vector3(0f, Mathf.Sin(t * 2.2f) * 0.18f, 0f);
            _model.localRotation = Quaternion.Euler(22f, t * 65f, Mathf.Sin(t * 1.5f) * 12f);
        }

        // 光の柱（ビーコン）の神秘的な脈動
        if (_beaconPillar != null)
        {
            float pulse = 1f + Mathf.Sin(t * 2.8f) * 0.18f;
            _beaconPillar.localScale = new Vector3(0.22f * pulse, 5.0f, 0.22f * pulse);
        }

        // プレイヤーへの吸い寄せチェック（3.8m以内）
        var player = AdventurePlayerController.Instance ?? FindAnyObjectByType<AdventurePlayerController>();
        if (player != null)
        {
            Vector3 playerPos = player.transform.position + Vector3.up * 0.85f;
            float dist = Vector3.Distance(transform.position, playerPos);

            if (dist < 3.8f)
            {
                // スムーズに吸い寄せられるマグネット効果
                transform.position = Vector3.MoveTowards(transform.position, playerPos, Time.deltaTime * 8.5f);

                // 接触したら取得完了
                if (dist < 0.75f)
                {
                    Collect();
                }
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_isCollected) return;
        if (other.GetComponent<AdventurePlayerController>() != null || other.CompareTag("Player"))
        {
            Collect();
        }
    }

    public void Collect()
    {
        if (_isCollected) return;
        _isCollected = true;

        // 爽快な取得音の再生
        if (_audioSource != null && _collectClip != null)
        {
            _audioSource.pitch = Random.Range(0.98f, 1.05f);
            _audioSource.PlayOneShot(_collectClip, 0.85f);
        }

        // 取得エフェクト（弾ける光のスパーク）
        SpawnCollectParticles();

        // ビジュアルモデルの非表示
        if (_model != null)
            _model.gameObject.SetActive(false);

        // マネージャーへ通知
        if (AdventureScrapManager.Instance != null)
        {
            AdventureScrapManager.Instance.OnScrapCollected(this);
        }

        // 音の再生完了後に自身を破棄
        Destroy(gameObject, 0.8f);
    }

    void SpawnCollectParticles()
    {
        var pGo = new GameObject("CollectSpark");
        pGo.transform.position = transform.position;
        var ps = pGo.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.startLifetime = 0.5f;
        main.startSpeed = 3.5f;
        main.startSize = 0.12f;
        main.startColor = itemColor;
        main.loop = false;

        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 25) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        var rend = pGo.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            var shader = Shader.Find("RustAndFloat/WhiteSmoke") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.SetTexture("_BaseMap", AdventureRustDrone.GetSoftSmokeTexture());
            mat.SetColor("_BaseColor", itemColor * 1.4f);
            rend.material = mat;
        }

        ps.Play();
        Destroy(pGo, 1.0f);
    }

    /// <summary>「ティロリン♪」という爽快な和音チャイム（G5 - C6 - E6 - G6）の合成</summary>
    static AudioClip SynthesizeCollectChime()
    {
        const int rate = 44100;
        float duration = 0.45f;
        int count = (int)(rate * duration);
        float[] data = new float[count];

        // G5 (784Hz), C6 (1046Hz), E6 (1318Hz), G6 (1568Hz) のアルペジオ
        float[] notes = { 784f, 1046f, 1318f, 1568f };
        float noteOffset = 0.045f; // 各音が少しずつ遅れて鳴る気持ちいいアルペジオ

        for (int n = 0; n < notes.Length; n++)
        {
            float f = notes[n];
            float startT = n * noteOffset;

            for (int i = (int)(startT * rate); i < count; i++)
            {
                float t = (float)i / rate - startT;
                float env = Mathf.Exp(-t * 8.5f) * Mathf.Sin(Mathf.Clamp01(t / 0.005f) * Mathf.PI * 0.5f);
                float wave = Mathf.Sin(2f * Mathf.PI * f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * (f * 2f) * t);
                data[i] += wave * env * 0.28f;
            }
        }

        // ノーマライズ
        float max = 0f;
        for (int i = 0; i < count; i++)
            if (Mathf.Abs(data[i]) > max) max = Mathf.Abs(data[i]);
        if (max > 0.001f)
        {
            float scale = 0.88f / max;
            for (int i = 0; i < count; i++) data[i] *= scale;
        }

        var ac = AudioClip.Create("ScrapCollectChime", count, 1, rate, false);
        ac.SetData(data, 0);
        return ac;
    }
}
