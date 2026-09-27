using UnityEngine;

/// <summary>
/// 『Rust & Float』島に散らばる漂着パーツ（古代ギア・エネルギーコア）
/// 発光しながら優雅に浮遊・回転し、プレイヤーが近づくと吸い寄せられて気持ちよく取得できる
/// </summary>
public class AdventureScrapItem : MonoBehaviour
{
    // ── 基本設定 ──
    public int itemId;
    public string itemName = "古代のギア";
    public Color itemColor = new Color(1.0f, 0.78f, 0.28f); // 黄金に輝くギア

    // ── パラメータ定数 ──
    const float SpawnGuardDuration = 0.6f;     // スポーン直後の誤取得ガード時間（秒）
    const float CollectDirectDist = 0.95f;     // 即時取得判定距離（3D球状）
    const float CollectHorizontalDist = 0.85f; // 即時取得の水平距離
    const float CollectVerticalDiff = 1.3f;    // 即時取得の許容高低差
    const float MagnetStartDist = 2.8f;        // マグネット吸い寄せ開始距離
    const float MagnetSpeed = 6.5f;            // 吸い寄せ移動速度
    const float InteractCollectDist = 2.5f;    // Eキー（インタラクト）取得許容距離
    const float DestroyDelayAfterCollect = 0.8f; // 回収演出後のオブジェクト破棄遅延

    // ── 内部状態 ──
    Transform _model;
    Transform _beaconPillar;
    Transform _carriedByDrone;
    AdventurePlayerController _cachedPlayer;

    float _hoverOffset;
    float _spawnTime;
    bool _isCollected = false;
    public bool IsCollected => _isCollected;

    // 動的生成マテリアルのキャッシュ（破棄時のメモリリーク防止用）
    Material _gearMat;
    Material _coreMat;
    Material _beaconMat;

    void Start()
    {
        _hoverOffset = Random.Range(0f, Mathf.PI * 2f);
        _spawnTime = Time.time;

        CreateModel();
        CreateBeacon();
        CreateIdleSparkles();
    }

    void OnDestroy()
    {
        // 生成したマテリアルの安全なメモリ解放
        if (_gearMat != null) Destroy(_gearMat);
        if (_coreMat != null) Destroy(_coreMat);
        if (_beaconMat != null) Destroy(_beaconMat);
    }

    #region ビジュアル生成

    void CreateModel()
    {
        var modelGo = new GameObject("Visual");
        modelGo.transform.SetParent(transform, false);
        _model = modelGo.transform;

        // ギアのハブ（視認性の高いサイズ：直径0.85m）
        var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hub.name = "GearHub";
        hub.transform.SetParent(_model, false);
        hub.transform.localScale = new Vector3(0.85f, 0.12f, 0.85f);
        Destroy(hub.GetComponent<Collider>());

        // 歯車用の突起（4つの突起・直径約1.15mのシルエット）
        _gearMat = CreateGearMaterial();
        for (int i = 0; i < 4; i++)
        {
            var tooth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tooth.name = "Tooth_" + i;
            tooth.transform.SetParent(_model, false);
            tooth.transform.localRotation = Quaternion.Euler(0f, i * 45f, 0f);
            tooth.transform.localScale = new Vector3(1.15f, 0.10f, 0.26f);
            Destroy(tooth.GetComponent<Collider>());

            var rend = tooth.GetComponent<Renderer>();
            if (rend != null) rend.material = _gearMat;
        }

        var hubRend = hub.GetComponent<Renderer>();
        if (hubRend != null) hubRend.material = _gearMat;

        // 中心のエネルギーコア球体
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        core.transform.SetParent(_model, false);
        core.transform.localScale = Vector3.one * 0.44f;
        Destroy(core.GetComponent<Collider>());

        _coreMat = CreateCoreMaterial();
        var coreRend = core.GetComponent<Renderer>();
        if (coreRend != null) coreRend.material = _coreMat;

        // 接触取得用トリガーコライダー
        var col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.85f;
    }

    void CreateBeacon()
    {
        // 遠景からでも一目で位置がわかる天空への光の柱（高さ60m）
        var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "BeaconPillar";
        beacon.transform.SetParent(transform, false);
        beacon.transform.localPosition = new Vector3(0f, 30f, 0f);
        beacon.transform.localScale = new Vector3(0.60f, 30f, 0.60f);
        Destroy(beacon.GetComponent<Collider>());

        var rend = beacon.GetComponent<Renderer>();
        if (rend != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("RustAndFloat/WhiteSmoke")
                ?? Shader.Find("Sprites/Default");

            _beaconMat = new Material(shader);
            _beaconMat.SetTexture("_BaseMap", AdventureRustDrone.GetSoftSmokeTexture());
            Color bCol = itemColor;
            bCol.a = 0.70f;
            _beaconMat.SetColor("_BaseColor", bCol);
            _beaconMat.renderQueue = 3150;
            rend.material = _beaconMat;
        }
        _beaconPillar = beacon.transform;

        // 周囲を温かく照らす自発光ポイントライト
        var light = gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = itemColor;
        light.range = 16f;
        light.intensity = 2.8f;
    }

    void CreateIdleSparkles()
    {
        var particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("RustAndFloat/WhiteSmoke")
            ?? Shader.Find("Sprites/Default");
        var smokeTex = AdventureRustDrone.GetSoftSmokeTexture();

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
            var mat = new Material(particleShader);
            mat.SetTexture("_BaseMap", smokeTex);
            mat.SetColor("_BaseColor", itemColor * 2.5f);
            rend.material = mat;
        }

        // 2. 天に向かって垂直に昇る光の粒子ビーム
        var beamGo = new GameObject("VerticalBeamSparkles");
        beamGo.transform.SetParent(transform, false);
        beamGo.transform.localPosition = Vector3.zero;
        var psBeam = beamGo.AddComponent<ParticleSystem>();

        var mainBeam = psBeam.main;
        mainBeam.loop = true;
        mainBeam.startLifetime = 2.5f;
        mainBeam.startSpeed = 12.0f;
        mainBeam.startSize = 0.35f;
        mainBeam.startColor = itemColor * 2.2f;
        mainBeam.simulationSpace = ParticleSystemSimulationSpace.World;

        var emissionBeam = psBeam.emission;
        emissionBeam.rateOverTime = 16f;

        var shapeBeam = psBeam.shape;
        shapeBeam.shapeType = ParticleSystemShapeType.Cone;
        shapeBeam.angle = 1.5f;
        shapeBeam.radius = 0.3f;
        shapeBeam.rotation = new Vector3(-90f, 0f, 0f);

        var rendBeam = beamGo.GetComponent<ParticleSystemRenderer>();
        if (rendBeam != null)
        {
            var mat = new Material(particleShader);
            mat.SetTexture("_BaseMap", smokeTex);
            mat.SetColor("_BaseColor", itemColor * 2.8f);
            rendBeam.material = mat;
        }
    }

    Material CreateGearMaterial()
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.SetColor("_BaseColor", itemColor);
        mat.SetFloat("_Metallic", 0.9f);
        mat.SetFloat("_Smoothness", 0.85f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", itemColor * 1.6f);
        return mat;
    }

    Material CreateCoreMaterial()
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        Color cyanCore = new Color(0.2f, 0.95f, 1.0f);
        mat.SetColor("_BaseColor", cyanCore);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", cyanCore * 2.8f);
        return mat;
    }

    #endregion

    #region 更新と回収ロジック

    public void AttachToDrone(Transform drone)
    {
        _carriedByDrone = drone;
        if (_beaconPillar != null)
            _beaconPillar.gameObject.SetActive(false);
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

        // スポーン直後の誤取得防止ガード
        if (Time.time < _spawnTime + SpawnGuardDuration) return;

        // プレイヤー参照のキャッシュと接近判定
        if (_cachedPlayer == null)
            _cachedPlayer = AdventurePlayerController.Instance ?? FindAnyObjectByType<AdventurePlayerController>();

        if (_cachedPlayer != null)
        {
            Vector3 playerPos = _cachedPlayer.transform.position + Vector3.up * 0.95f;
            float dist = Vector3.Distance(transform.position, playerPos);
            float horizontalDist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z), 
                new Vector2(_cachedPlayer.transform.position.x, _cachedPlayer.transform.position.z)
            );
            float verticalDiff = Mathf.Abs(transform.position.y - playerPos.y);

            // 1. 即時取得判定（接触距離以内）
            if (dist < CollectDirectDist || (horizontalDist < CollectHorizontalDist && verticalDiff < CollectVerticalDiff))
            {
                Collect();
                return;
            }

            // 2. ふわっと近づくマグネット吸い寄せ
            if (dist < MagnetStartDist || (horizontalDist < 2.4f && verticalDiff < 2.0f))
            {
                transform.position = Vector3.MoveTowards(transform.position, playerPos, Time.deltaTime * MagnetSpeed);
            }

            // 3. インタラクトキーによる取得
            if (_cachedPlayer.InteractPressed && dist < InteractCollectDist)
            {
                Collect();
                return;
            }
        }
    }

    void OnTriggerEnter(Collider other) => CheckColliderCollect(other);
    void OnTriggerStay(Collider other) => CheckColliderCollect(other);

    void CheckColliderCollect(Collider other)
    {
        if (_isCollected || other == null) return;
        if (Time.time < _spawnTime + SpawnGuardDuration) return;

        if (other.GetComponentInParent<AdventurePlayerController>() != null 
            || other.CompareTag("Player") 
            || other.name.ToLower().Contains("niko"))
        {
            Collect();
        }
    }

    public void Collect()
    {
        if (_isCollected) return;
        _isCollected = true;

        // 快感チャイム音の再生（ScrapManagerに一本化）
        if (AdventureScrapManager.Instance != null)
        {
            AdventureScrapManager.Instance.PlayScrapCollectFanfare();
        }

        // 弾けるスパークル演出
        SpawnCollectParticles();

        // ビジュアルの非表示
        if (_model != null)
            _model.gameObject.SetActive(false);

        // 管理マネージャーへ回収通知
        if (AdventureScrapManager.Instance != null)
        {
            AdventureScrapManager.Instance.OnScrapCollected(this);
        }

        // NikoとRustのスキンシップアクション＆Rustの歓喜宙返り
        if (AdventureRustDrone.Instance != null)
        {
            AdventureRustDrone.Instance.TriggerCelebration();
        }
        else if (AdventurePettingAction.Instance != null && !AdventurePettingAction.Instance.IsPetting)
        {
            AdventurePettingAction.Instance.PetRust("やったねRust！パーツを見つけたよ！", 1.8f);
        }

        // オブジェクト破棄
        Destroy(gameObject, DestroyDelayAfterCollect);
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

    #endregion
}
