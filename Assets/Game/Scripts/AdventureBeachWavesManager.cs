using UnityEngine;

/// <summary>
/// 島の外周ビーチ・海岸線に近づいた時に、リアルな潮騒・寄せては返す波音を臨場感たっぷりに再生するマネージャー
/// シーン起動時に自動生成され、プレイヤーの海抜高度・海岸との距離に応じてスムーズにフェードイン/アウトする
/// </summary>
public class AdventureBeachWavesManager : MonoBehaviour
{
    static AdventureBeachWavesManager _instance;
    public static AdventureBeachWavesManager Instance => _instance;

    AudioClip _waveClip;
    AudioClip _seagullClip1;
    AudioClip _seagullClipMany;
    AudioSource _waveSourceA;
    AudioSource _waveSourceB;
    AudioSource _seagullSource;
    Transform _playerTransform;

    [Header("Wave & Seagull Settings")]
    [Range(0f, 1f)] public float masterVolume = 0.55f;
    [Range(0f, 1f)] public float seagullVolume = 0.70f;

    float _seagullTimer = 0.5f; // 開始0.5秒後に最初のウミネコを再生
    bool _firstCallPlayed = false;
    GameObject _seagullFlockRoot;

    const float IslandCenterX = 512f;
    const float IslandCenterZ = 512f;
    const float BeachRadiusMin = 345f; // 内陸からビーチに近づき始める半径
    const float BeachRadiusMax = 445f; // 白砂ビーチのピーク半径

    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = FindObjectOfType<AdventureBeachWavesManager>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("ParadiseBeachWavesManager");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AdventureBeachWavesManager>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        LoadAudioClips();
        SetupAudioSources();
        CreateSeagullFlockVisuals();
    }

    void LoadAudioClips()
    {
#if UNITY_EDITOR
        _waveClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RustAndFloat/Audio/Ambience/ocean_waves_grand.wav");
        if (_waveClip == null)
            _waveClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/AudioFiles/03_amb/watershore_amb.wav");

        _seagullClip1 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/AudioFiles/06_birds/seagulls_1.wav");
        _seagullClipMany = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/AudioFiles/06_birds/seagulls_many_2.wav");
#endif
    }

    void SetupAudioSources()
    {
        // 2系統の音源を時間差（16.5秒ずらし）でクロスブレンドし、寄せては引く波の立体感とうねりを再現
        _waveSourceA = CreateWaveSource("WaveSource_A");
        _waveSourceB = CreateWaveSource("WaveSource_B");

        if (_waveClip != null)
        {
            _waveSourceA.time = 0f;
            _waveSourceA.Play();

            _waveSourceB.time = Mathf.Repeat(16.5f, _waveClip.length);
            _waveSourceB.Play();
        }

        // ウミネコ専用の空間3Dオーディオソース
        var sgGo = new GameObject("SeagullAudioSource");
        sgGo.transform.SetParent(transform, false);
        _seagullSource = sgGo.AddComponent<AudioSource>();
        _seagullSource.spatialBlend = 0.55f; // 3Dの定位感を保ちつつ、どの向きでも心地よく広がるブレンド
        _seagullSource.volume = seagullVolume;
        _seagullSource.minDistance = 15f;
        _seagullSource.maxDistance = 180f;
        _seagullSource.playOnAwake = false;
        _seagullSource.rolloffMode = AudioRolloffMode.Logarithmic;
    }

    AudioSource CreateWaveSource(string name)
    {
        var src = gameObject.AddComponent<AudioSource>();
        src.clip = _waveClip;
        src.loop = true;
        src.spatialBlend = 0.0f; // 2Dステレオで耳全体を心地よく包み込む
        src.volume = 0f;
        src.playOnAwake = false;
        return src;
    }

    void Update()
    {
        if (_playerTransform == null)
        {
            var player = FindObjectOfType<AdventurePlayerController>();
            if (player != null)
                _playerTransform = player.transform;
            else
                return;
        }

        if (_waveClip == null)
        {
            LoadAudioClips();
            if (_waveClip != null)
            {
                if (_waveSourceA != null) { _waveSourceA.clip = _waveClip; _waveSourceA.Play(); }
                if (_waveSourceB != null) { _waveSourceB.clip = _waveClip; _waveSourceB.Play(); }
            }
            return;
        }

        Vector3 pos = _playerTransform.position;

        // 1. 半径ベースのビーチ接近度（島の中心 512, 512 からの水平距離）
        float dx = pos.x - IslandCenterX;
        float dz = pos.z - IslandCenterZ;
        float sqrDist = dx * dx + dz * dz;
        float radius = Mathf.Sqrt(sqrDist);
        float radiusFactor = Mathf.InverseLerp(BeachRadiusMin, BeachRadiusMax, radius);

        // 2. 高度ベースの海岸接近度（海抜 5.5m〜10m のビーチテラス）
        float y = pos.y;
        float heightFactor = 0f;
        if (y <= 7.5f)
        {
            heightFactor = 1.0f; // 水辺・波打ち際
        }
        else if (y <= 13.0f)
        {
            heightFactor = Mathf.InverseLerp(13.0f, 7.5f, y); // ビーチ斜面
        }

        // 半径または低高度のどちらかで海岸に近ければ波音が聞こえる
        float targetIntensity = Mathf.Max(radiusFactor, heightFactor);

        // 波の音量制御（2系統のクロスブレンドでうねりを生む）
        float targetVolA = targetIntensity * masterVolume;
        float targetVolB = targetIntensity * (masterVolume * 0.85f);

        // スムーズな音量フェードイン・フェードアウト
        float dt = Time.deltaTime;
        if (_waveSourceA != null)
            _waveSourceA.volume = Mathf.MoveTowards(_waveSourceA.volume, targetVolA, dt * 0.4f);
        if (_waveSourceB != null)
            _waveSourceB.volume = Mathf.MoveTowards(_waveSourceB.volume, targetVolB, dt * 0.4f);

        // 3. ウミネコ（カモメ）の鳴き声制御
        HandleSeagullAmbience(pos, targetIntensity, dt);
    }

    void HandleSeagullAmbience(Vector3 playerPos, float beachIntensity, float dt)
    {
        if (_seagullSource == null) return;
        _seagullTimer -= dt;

        if (_seagullTimer <= 0f)
        {
            if (!_firstCallPlayed)
            {
                // ゲーム開始直後：砂浜で目覚めた瞬間に青空から響く最初のファーストコール！
                PlaySeagullCall(true, playerPos);
                _firstCallPlayed = true;
                _seagullTimer = Random.Range(14f, 22f);
            }
            else if (beachIntensity > 0.15f)
            {
                // 海岸・ビーチにいる間：定期的に青空高くから鳴き交わす
                PlaySeagullCall(false, playerPos);
                _seagullTimer = Random.Range(15f, 28f);
            }
            else
            {
                // 内陸にいる時は少し待機
                _seagullTimer = 3.5f;
            }
        }
    }

    void PlaySeagullCall(bool isFirst, Vector3 playerPos)
    {
        AudioClip clip = _seagullClip1;
        if (!isFirst && _seagullClipMany != null && Random.value < 0.4f)
        {
            clip = _seagullClipMany;
        }

        if (clip == null) return;

        // プレイヤーの上空18m〜30mの海風が吹く空の位置に音源を配置
        Vector3 offset = isFirst 
            ? new Vector3(-8f, 22f, 12f) // 開始時は前方海側の上空
            : new Vector3(Random.Range(-30f, 30f), Random.Range(18f, 32f), Random.Range(-30f, 30f));

        _seagullSource.transform.position = playerPos + offset;
        _seagullSource.pitch = Random.Range(0.96f, 1.05f);
        float vol = seagullVolume * (isFirst ? 1.0f : Random.Range(0.75f, 1.0f));
        _seagullSource.PlayOneShot(clip, vol);
    }

    /// <summary>西側砂浜の上空を優雅に旋回するウミネコ（海鳥）のビジュアル群を生成</summary>
    void CreateSeagullFlockVisuals()
    {
        if (_seagullFlockRoot != null) return;

        _seagullFlockRoot = new GameObject("SeagullFlock_Sky");
        _seagullFlockRoot.transform.SetParent(transform, false);

        // 西側砂浜（160, 280）周辺の上空、および南西海岸の上空に数羽のカモメを配置
        Vector3[] flockCenters = {
            new Vector3(160f, 26f, 280f), // 西側ビーチ（スポーン地点真上）
            new Vector3(145f, 32f, 320f), // 北西寄り海面上空
            new Vector3(175f, 28f, 230f)  // 南西寄りビーチ上空
        };

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var birdMat = new Material(shader);
        birdMat.SetColor("_BaseColor", new Color(0.96f, 0.96f, 0.98f)); // 清潔な白い羽毛
        birdMat.SetFloat("_Smoothness", 0.4f);

        var beakMat = new Material(shader);
        beakMat.SetColor("_BaseColor", new Color(1.0f, 0.75f, 0.05f)); // 黄色いクチバシ

        var wingTipMat = new Material(shader);
        wingTipMat.SetColor("_BaseColor", new Color(0.2f, 0.2f, 0.22f)); // 初列風切羽の黒灰色のアクセント

        for (int i = 0; i < flockCenters.Length; i++)
        {
            int birdsInGroup = (i == 0) ? 3 : 2; // スポーン地点頭上には3羽
            for (int b = 0; b < birdsInGroup; b++)
            {
                var bird = new GameObject($"Seagull_{i}_{b}");
                bird.transform.SetParent(_seagullFlockRoot.transform, false);

                var flight = bird.AddComponent<AdventureSeagullFlightVisual>();
                flight.center = flockCenters[i];
                flight.radius = Random.Range(14f, 26f);
                flight.speed = Random.Range(12f, 18f);
                flight.altitude = flockCenters[i].y + Random.Range(-3f, 4f);
                flight.angleOffset = b * (360f / birdsInGroup) + Random.Range(-20f, 20f);
                flight.isClockwise = (i % 2 == 0);

                // カモメのプロシージャル3D形状
                BuildSeagullMesh(bird.transform, birdMat, beakMat, wingTipMat, flight);
            }
        }
    }

    void BuildSeagullMesh(Transform parent, Material bodyMat, Material beakMat, Material tipMat, AdventureSeagullFlightVisual flight)
    {
        // 胴体（流線型の白いボディ）
        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "Body";
        body.transform.SetParent(parent, false);
        body.transform.localScale = new Vector3(0.24f, 0.18f, 0.72f);
        Destroy(body.GetComponent<Collider>());
        body.GetComponent<Renderer>().material = bodyMat;

        // クチバシ（黄色）
        var beak = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beak.name = "Beak";
        beak.transform.SetParent(body.transform, false);
        beak.transform.localPosition = new Vector3(0f, 0.05f, 0.55f);
        beak.transform.localScale = new Vector3(0.25f, 0.25f, 0.45f);
        Destroy(beak.GetComponent<Collider>());
        beak.GetComponent<Renderer>().material = beakMat;

        // 左翼
        var leftWingPivot = new GameObject("LeftWingPivot");
        leftWingPivot.transform.SetParent(parent, false);
        leftWingPivot.transform.localPosition = new Vector3(-0.10f, 0.05f, 0.05f);

        var leftWing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftWing.name = "WingL";
        leftWing.transform.SetParent(leftWingPivot.transform, false);
        leftWing.transform.localPosition = new Vector3(-0.55f, 0f, 0f);
        leftWing.transform.localScale = new Vector3(1.10f, 0.035f, 0.24f);
        leftWing.transform.localRotation = Quaternion.Euler(0f, 8f, 0f);
        Destroy(leftWing.GetComponent<Collider>());
        leftWing.GetComponent<Renderer>().material = bodyMat;

        // 左翼先端の黒い羽
        var leftTip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftTip.name = "TipL";
        leftTip.transform.SetParent(leftWing.transform, false);
        leftTip.transform.localPosition = new Vector3(-0.45f, 0.002f, 0f);
        leftTip.transform.localScale = new Vector3(0.25f, 1.05f, 0.85f);
        Destroy(leftTip.GetComponent<Collider>());
        leftTip.GetComponent<Renderer>().material = tipMat;

        // 右翼
        var rightWingPivot = new GameObject("RightWingPivot");
        rightWingPivot.transform.SetParent(parent, false);
        rightWingPivot.transform.localPosition = new Vector3(0.10f, 0.05f, 0.05f);

        var rightWing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightWing.name = "WingR";
        rightWing.transform.SetParent(rightWingPivot.transform, false);
        rightWing.transform.localPosition = new Vector3(0.55f, 0f, 0f);
        rightWing.transform.localScale = new Vector3(1.10f, 0.035f, 0.24f);
        rightWing.transform.localRotation = Quaternion.Euler(0f, -8f, 0f);
        Destroy(rightWing.GetComponent<Collider>());
        rightWing.GetComponent<Renderer>().material = bodyMat;

        // 右翼先端の黒い羽
        var rightTip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightTip.name = "TipR";
        rightTip.transform.SetParent(rightWing.transform, false);
        rightTip.transform.localPosition = new Vector3(0.45f, 0.002f, 0f);
        rightTip.transform.localScale = new Vector3(0.25f, 1.05f, 0.85f);
        Destroy(rightTip.GetComponent<Collider>());
        rightTip.GetComponent<Renderer>().material = tipMat;

        flight.leftWingPivot = leftWingPivot.transform;
        flight.rightWingPivot = rightWingPivot.transform;
    }
}

/// <summary>
/// カモメ（ウミネコ）の上空旋回＆翼フラッピングアニメーション
/// </summary>
public class AdventureSeagullFlightVisual : MonoBehaviour
{
    public Vector3 center;
    public float radius = 20f;
    public float speed = 15f;
    public float altitude = 28f;
    public float angleOffset = 0f;
    public bool isClockwise = true;

    public Transform leftWingPivot;
    public Transform rightWingPivot;

    float _currentAngle;
    float _flapSpeed = 4.2f;

    void Start()
    {
        _currentAngle = angleOffset;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float dir = isClockwise ? 1f : -1f;
        _currentAngle += (speed / radius) * Mathf.Rad2Deg * dir * dt;

        float rad = _currentAngle * Mathf.Deg2Rad;
        float x = center.x + Mathf.Cos(rad) * radius;
        float z = center.z + Mathf.Sin(rad) * radius;
        float y = altitude + Mathf.Sin(Time.time * 0.8f + angleOffset) * 1.5f;

        Vector3 nextPos = new Vector3(x, y, z);
        Vector3 forwardDir = (nextPos - transform.position).normalized;
        if (forwardDir.sqrMagnitude > 0.001f)
        {
            // 旋回バンク（傾き）を加えた飛行姿勢
            float bankAngle = dir * 18f;
            Quaternion targetRot = Quaternion.LookRotation(forwardDir, Vector3.up) * Quaternion.Euler(0f, 0f, -bankAngle);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * 8f);
        }
        transform.position = nextPos;

        // 優雅な翼のフラッピング（羽ばたきとグライドのブレンド）
        if (leftWingPivot != null && rightWingPivot != null)
        {
            float flapCycle = Mathf.Sin(Time.time * _flapSpeed + angleOffset);
            // 滑空（グライド）時は羽ばたきを抑え、上昇時に大きく羽ばたく
            float wingAngle = flapCycle * 22f;
            leftWingPivot.localRotation = Quaternion.Euler(0f, 0f, -wingAngle);
            rightWingPivot.localRotation = Quaternion.Euler(0f, 0f, wingAngle);
        }
    }
}
