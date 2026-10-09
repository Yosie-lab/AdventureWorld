using UnityEngine;
using System.Collections;

/// <summary>
/// 砂浜に佇むチルな焚き火スポット（Campfire Spot）。
/// 1. 流木と丸石で組まれた焚き火炉 ＆ 流木の丸太ベンチ（Driftwood Log Bench）
/// 2. 【Eキー】で点火 ➔ 薪がパチパチとはぜる心地よいASMR音と温かな揺らめく光
/// 3. 【Eキー】で丸太に腰掛ける ➔ Nikoが腰掛け、Rustが隣にちょこんと座って一緒に海と炎を眺める
/// 4. シネマティック・チル視点カメラ ＆ Rustの心温まるつぶやき
/// 5. 移動入力（WASD / Space）で自然に立ち上がり
/// </summary>
public class AdventureBeachCampfire : MonoBehaviour
{
    public static AdventureBeachCampfire Instance { get; private set; }

    [Header("配置座標（スタート地点の白砂ビーチ：渡し板から離れた安全な海辺）")]
    public Vector3 campfirePos = new Vector3(168.0f, 6.3f, 254.0f);

    private bool _isLit = false;
    public bool IsLit => _isLit;

    private bool _isSitting = false;
    public bool IsSitting => _isSitting;

    private Transform _firePit;
    private Transform _logBench;
    private GameObject _flameRoot;
    private Light _fireLight;
    private AudioSource _fireAudio;
    private ParticleSystem _sparksParticle;

    private Vector3 _benchSeatNiko;
    private Vector3 _benchSeatRust;
    private Vector3 _lookDirection;

    private float _chillTimer = 0f;
    private float _nextDialogueTime = 0f;
    private int _dialogueIndex = 0;

    // マテリアルキャッシュ
    private Material _woodMat;
    private Material _stoneMat;
    private Material _ashMat;
    private Material _flameMat;

    // カメラチル制御用
    private Vector3 _origCamOffset;
    private bool _hasOverriddenCamera = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void AutoEnsure()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName == "RustAndFlat" || sceneName == "RustAndFloat")
        {
            Ensure();
        }
    }

    public static void Ensure()
    {
        if (Instance != null && Instance.gameObject != null) return;
        var existing = Object.FindAnyObjectByType<AdventureBeachCampfire>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject("AdventureBeachCampfire");
        Instance = go.AddComponent<AdventureBeachCampfire>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        InitMaterials();
        AlignToTerrain();
        BuildCampfireStructure();
    }

    void InitMaterials()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        _woodMat = new Material(shader);
        _woodMat.SetColor("_BaseColor", new Color(0.38f, 0.28f, 0.18f)); // 乾いた流木色
        _woodMat.SetFloat("_Smoothness", 0.18f);

        _stoneMat = new Material(shader);
        _stoneMat.SetColor("_BaseColor", new Color(0.48f, 0.46f, 0.44f)); // 海岸の丸石
        _stoneMat.SetFloat("_Smoothness", 0.32f);

        _ashMat = new Material(shader);
        _ashMat.SetColor("_BaseColor", new Color(0.18f, 0.16f, 0.15f)); // 炭・灰
        _ashMat.SetFloat("_Smoothness", 0.10f);

        var unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        _flameMat = new Material(unlitShader);
        _flameMat.SetColor("_BaseColor", new Color(1.0f, 0.55f, 0.12f, 0.95f));
    }

    void AlignToTerrain()
    {
        var land = Terrain.activeTerrain ?? FindAnyObjectByType<Terrain>();
        if (land != null)
        {
            float groundY = land.SampleHeight(campfirePos) + land.transform.position.y;
            campfirePos.y = groundY;
        }
        transform.position = campfirePos;
    }

    void BuildCampfireStructure()
    {
        // 1. 焚き火炉（FirePit）
        var pitGo = new GameObject("FirePit");
        pitGo.transform.SetParent(transform, false);
        pitGo.transform.localPosition = Vector3.zero;
        _firePit = pitGo.transform;

        // 丸石サークル（15個の大きめの丸石が円形に並ぶ：直径約2.5m）
        int stoneCount = 15;
        float pitRadius = 1.25f;
        for (int i = 0; i < stoneCount; i++)
        {
            float rad = (i / (float)stoneCount) * Mathf.PI * 2f;
            Vector3 sPos = new Vector3(Mathf.Cos(rad) * pitRadius, 0.12f, Mathf.Sin(rad) * pitRadius);
            var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stone.name = $"PitStone_{i}";
            stone.transform.SetParent(_firePit, false);
            stone.transform.localPosition = sPos;
            stone.transform.localScale = new Vector3(0.48f, 0.34f, 0.48f);
            stone.transform.rotation = Random.rotation;

            var mr = stone.GetComponent<MeshRenderer>();
            if (mr != null) mr.material = _stoneMat;
            var col = stone.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        // 中央の炭・灰
        var ash = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ash.name = "CampfireAsh";
        ash.transform.SetParent(_firePit, false);
        ash.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        ash.transform.localScale = new Vector3(1.8f, 0.05f, 1.8f);
        var ashMr = ash.GetComponent<MeshRenderer>();
        if (ashMr != null) ashMr.material = _ashMat;
        var ashCol = ash.GetComponent<Collider>();
        if (ashCol != null) Destroy(ashCol);

        // 円錐状に組まれた薪（8本の立派な流木）
        int logCount = 8;
        for (int i = 0; i < logCount; i++)
        {
            float angle = i * (360f / logCount) + 12f;
            float rad = angle * Mathf.Deg2Rad;
            var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = $"FireLog_{i}";
            log.transform.SetParent(_firePit, false);
            log.transform.localPosition = new Vector3(Mathf.Cos(rad) * 0.46f, 0.30f, Mathf.Sin(rad) * 0.46f);
            log.transform.localScale = new Vector3(0.18f, 0.58f, 0.18f);
            log.transform.rotation = Quaternion.Euler(38f, -angle + 90f, 0f);

            var lmr = log.GetComponent<MeshRenderer>();
            if (lmr != null) lmr.material = _woodMat;
            var lcol = log.GetComponent<Collider>();
            if (lcol != null) Destroy(lcol);
        }

        // 2. 炎と演出エフェクト（初期は非アクティブ）
        _flameRoot = new GameObject("FlameVisuals");
        _flameRoot.transform.SetParent(_firePit, false);
        _flameRoot.transform.localPosition = new Vector3(0f, 0.20f, 0f);

        // 炎メッシュ（立体的に交差する炎のビルボード：大きめ）
        for (int f = 0; f < 3; f++)
        {
            var flamePlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            flamePlane.name = $"FlameMesh_{f}";
            flamePlane.transform.SetParent(_flameRoot.transform, false);
            flamePlane.transform.localPosition = new Vector3(0f, 0.50f, 0f);
            flamePlane.transform.localRotation = Quaternion.Euler(0f, f * 60f, 0f);
            flamePlane.transform.localScale = new Vector3(1.10f, 1.40f, 1f);

            var mr = flamePlane.GetComponent<MeshRenderer>();
            if (mr != null) mr.material = _flameMat;
            var col = flamePlane.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        // 火の粉パーティクル
        var psGo = new GameObject("CampfireSparks");
        psGo.transform.SetParent(_flameRoot.transform, false);
        psGo.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        _sparksParticle = psGo.AddComponent<ParticleSystem>();
        var main = _sparksParticle.main;
        main.startColor = new Color(1.0f, 0.72f, 0.25f, 0.9f);
        main.startSize = 0.11f;
        main.startLifetime = 2.4f;
        main.startSpeed = 1.1f;
        main.maxParticles = 60;
        var emission = _sparksParticle.emission;
        emission.rateOverTime = 22f;
        var shape = _sparksParticle.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.52f;

        // 焚き火ライト（より暖かく広範囲を照らす）
        var ltGo = new GameObject("CampfireLight");
        ltGo.transform.SetParent(_flameRoot.transform, false);
        ltGo.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        _fireLight = ltGo.AddComponent<Light>();
        _fireLight.type = LightType.Point;
        _fireLight.color = new Color(1.0f, 0.62f, 0.22f);
        _fireLight.intensity = 5.0f;
        _fireLight.range = 18.0f;

        // 焚き火ASMRパチパチ音
        _fireAudio = _flameRoot.AddComponent<AudioSource>();
        _fireAudio.spatialBlend = 1.0f;
        _fireAudio.minDistance = 2.5f;
        _fireAudio.maxDistance = 26f;
        _fireAudio.rolloffMode = AudioRolloffMode.Linear;
        _fireAudio.loop = true;
        _fireAudio.clip = CreateCracklingAudioClip();
        _fireAudio.volume = 0.68f;

        _flameRoot.SetActive(false);

        // 3. 流木丸太ベンチ（Driftwood Log Bench）
        // 焚き火の背後（南東）約2.1mに配置。座ると北西（海と波打ち際・夕日）を正面に見渡せる
        var benchGo = new GameObject("DriftwoodLogBench");
        benchGo.transform.SetParent(transform, false);
        Vector3 benchOffset = new Vector3(-0.55f, 0.28f, -2.15f); // 焚き火の背後
        benchGo.transform.localPosition = benchOffset;
        _logBench = benchGo.transform;

        // 丸太本体（長さ約3.5m、直径0.58mのどっしりとした流木ベンチ）
        var logMesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        logMesh.name = "BenchLogMesh";
        logMesh.transform.SetParent(_logBench, false);
        logMesh.transform.localPosition = Vector3.zero;
        logMesh.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // 横倒し
        logMesh.transform.localScale = new Vector3(0.58f, 1.75f, 0.58f);
        var bmr = logMesh.GetComponent<MeshRenderer>();
        if (bmr != null) bmr.material = _woodMat;

        // 座席位置の定義
        // 海と焚き火を見る向き（北西方向）
        _lookDirection = (transform.position - _logBench.position).normalized;
        _lookDirection.y = 0f;
        _lookDirection = Quaternion.Euler(0f, -15f, 0f) * _lookDirection; // 海の地平線へ少し角度を合わせる

        Vector3 benchWorld = _logBench.position;
        Vector3 right = Vector3.Cross(Vector3.up, _lookDirection).normalized;

        // Nikoの座席（丸太の中央やや左）
        _benchSeatNiko = benchWorld - right * 0.36f + Vector3.up * 0.46f;

        // Rustの座席（Nikoのすぐ右隣 0.72m）
        _benchSeatRust = benchWorld + right * 0.50f + Vector3.up * 0.48f;
    }

    /// <summary>パチパチとはぜる焚き火のプロシージャルASMRオーディオクリップ</summary>
    AudioClip CreateCracklingAudioClip()
    {
        int sampleRate = 22050;
        int lengthSamples = sampleRate * 3; // 3秒ループ
        float[] samples = new float[lengthSamples];

        System.Random rng = new System.Random(42);
        for (int i = 0; i < lengthSamples; i++)
        {
            // ベースの暖かな低周波ゴォー音（火の燃焼音）
            float roar = Mathf.Sin(i * 0.018f) * 0.08f + ((float)rng.NextDouble() * 2f - 1f) * 0.03f;

            // ランダムな薪のはぜる「パチッ！」「パチパチ」ポップスパイク
            float pop = 0f;
            if (rng.NextDouble() < 0.0035f) // 一定頻度ではぜる
            {
                pop = ((float)rng.NextDouble() * 2f - 1f) * 0.75f;
            }

            samples[i] = Mathf.Clamp(roar + pop, -1f, 1f);
        }

        var clip = AudioClip.Create("CampfireCracklingLoop", lengthSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    void Update()
    {
        var player = AdventurePlayerController.Instance;
        if (player == null) return;

        // 炎の揺らめきフリッカー演出＆メッシュ拡大縮小アニメーション
        if (_isLit)
        {
            if (_fireLight != null)
            {
                float noise = Mathf.PerlinNoise(Time.time * 7.5f, 0.0f);
                _fireLight.intensity = Mathf.Lerp(4.0f, 6.2f, noise);
            }

            if (_flameRoot != null)
            {
                float flameScaleY = 1.35f + Mathf.Sin(Time.time * 12f) * 0.18f + Mathf.PerlinNoise(Time.time * 5f, 1f) * 0.22f;
                float flameScaleXZ = 1.15f + Mathf.Cos(Time.time * 9f) * 0.12f;
                _flameRoot.transform.localScale = new Vector3(flameScaleXZ, flameScaleY, flameScaleXZ);
            }
        }

        Vector3 pPos = player.transform.position;
        float xzDistToFire = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(transform.position.x, transform.position.z));
        float yDistToFire = Mathf.Abs(pPos.y - transform.position.y);
        bool inFireZone = xzDistToFire < 4.8f && yDistToFire < 3.2f;

        float xzDistToBench = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(_benchSeatNiko.x, _benchSeatNiko.z));
        float yDistToBench = Mathf.Abs(pPos.y - _benchSeatNiko.y);
        bool inBenchZone = xzDistToBench < 3.8f && yDistToBench < 3.2f;

        bool interactPressed = CheckInteractInput();

        if (_isSitting)
        {
            UpdateSittingState(player);
        }
        else
        {
            // 未点火時：焚き火に近づいて【E】または左クリックで点火
            if (!_isLit && inFireZone)
            {
                if (interactPressed)
                {
                    IgniteCampfire();
                }
            }
            // 点火後（またはベンチ付近）：【E】または左クリックで腰掛ける
            else if (inBenchZone)
            {
                if (interactPressed)
                {
                    SitDown(player);
                }
            }
        }
    }

    /// <summary>Eキー、左クリック、ゲームパッドのアクションボタンを包括的に判定</summary>
    bool CheckInteractInput()
    {
        return AdventureInputReader.InteractDown || AdventureInputReader.MouseLeftDown;
    }

    public void IgniteCampfire()
    {
        if (_isLit) return;
        _isLit = true;

        if (_flameRoot != null)
        {
            _flameRoot.SetActive(true);
            if (_sparksParticle != null)
                _sparksParticle.Play();
        }

        if (_fireAudio != null)
            _fireAudio.Play();

        // 点火時のRustのリアクション
        var drone = AdventureRustDrone.Instance;
        if (drone != null)
        {
            drone.SpeakCustom("わぁ！火が点いた！すごくあったかいね……！", 3.8f);
        }
    }

    public void SitDown(AdventurePlayerController player)
    {
        if (_isSitting) return;
        _isSitting = true;
        _chillTimer = 0f;
        _nextDialogueTime = 5.0f; // 5秒後に最初のチル台詞
        _dialogueIndex = 0;

        // もし火が点いていなければ自動的に点火
        if (!_isLit)
        {
            IgniteCampfire();
        }

        // 1. Nikoを丸太の上に腰掛けさせる
        player.enabled = false;
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.transform.position = _benchSeatNiko;
        player.transform.rotation = Quaternion.LookRotation(_lookDirection, Vector3.up);

        // 2. Rustを隣の座席に呼んでちょこんと座らせる
        var drone = AdventureRustDrone.Instance;
        if (drone != null)
        {
            drone.SetCampfireChill(true, _benchSeatRust, _lookDirection);
        }

        // 3. カメラをシネマティック・チル視点へ
        var camFollow = Camera.main != null ? Camera.main.GetComponent<AdventureCameraFollow>() : null;
        if (camFollow != null)
        {
            camFollow.SetCinematicMode(true);
            _hasOverriddenCamera = true;
        }
    }

    public void StandUp(AdventurePlayerController player)
    {
        if (!_isSitting) return;
        _isSitting = false;

        // 1. Nikoの操作を復帰
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = true;
        player.enabled = true;

        // ベンチの前に少し進んだ位置に立たせる
        player.transform.position = _benchSeatNiko + _lookDirection * 0.85f;

        // 2. Rustを通常追従に復帰
        var drone = AdventureRustDrone.Instance;
        if (drone != null)
        {
            drone.SetCampfireChill(false);
            drone.SpeakCustom("よいしょ！さぁ、冒険を続けよう！", 3.0f);
        }

        // 3. カメラを通常モードへ復帰
        if (_hasOverriddenCamera)
        {
            var camFollow = Camera.main != null ? Camera.main.GetComponent<AdventureCameraFollow>() : null;
            if (camFollow != null)
            {
                camFollow.SetCinematicMode(false);
            }
            _hasOverriddenCamera = false;
        }
    }

    void UpdateSittingState(AdventurePlayerController player)
    {
        _chillTimer += Time.deltaTime;

        // Nikoの座り位置を固定（アニメーターのアイドル再生）
        player.transform.position = _benchSeatNiko;
        player.transform.rotation = Quaternion.LookRotation(_lookDirection, Vector3.up);

        // 立ち上がり判定（移動キー・ジャンプキー・Eキー・クリック）
        bool movePressed = AdventureInputReader.HasAnyMove || AdventureInputReader.SpaceOrJDown;

        // 座り始め0.5秒以降に立ち上がりを受け付ける
        if (_chillTimer > 0.5f && (movePressed || CheckInteractInput()))
        {
            StandUp(player);
            return;
        }

        // Rustの心温まるチルな呟き（定期的にぽつりぽつりと話す）
        if (_chillTimer >= _nextDialogueTime)
        {
            var drone = AdventureRustDrone.Instance;
            if (drone != null)
            {
                string[] dialogues = {
                    "……あったかいね、Niko。",
                    "パチパチって、なんだか落ち着く音だね……",
                    "波の音がきれいに聴こえるよ……",
                    "ずっとこうして、海を眺めていたいな……",
                    "ふぅ……少し休んでいこう……",
                    "Nikoと一緒にいると、心があったかくなるよ。"
                };

                string msg = dialogues[_dialogueIndex % dialogues.Length];
                drone.SpeakCustom(msg, 5.0f);
                _dialogueIndex++;
                _nextDialogueTime = _chillTimer + Random.Range(16f, 24f);
            }
        }
    }

    void OnGUI()
    {
        var player = AdventurePlayerController.Instance;
        if (player == null) return;

        Vector3 pPos = player.transform.position;
        float xzDistToFire = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(transform.position.x, transform.position.z));
        float yDistToFire = Mathf.Abs(pPos.y - transform.position.y);
        bool inFireZone = xzDistToFire < 4.2f && yDistToFire < 2.8f;

        float xzDistToBench = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(_benchSeatNiko.x, _benchSeatNiko.z));
        float yDistToBench = Mathf.Abs(pPos.y - _benchSeatNiko.y);
        bool inBenchZone = xzDistToBench < 3.2f && yDistToBench < 2.8f;

        var skin = GUI.skin;
        var style = new GUIStyle(skin.label);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 20;
        style.normal.textColor = Color.white;

        // ドロップシャドウ
        var shadow = new GUIStyle(style);
        shadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f);

        if (_isSitting)
        {
            // 座っている時の控えめなプロンプト
            string sitPrompt = "【 W / A / S / D 】または【 Space 】立ち上がる";
            float w = 500f;
            float h = 40f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - 90f;

            GUI.Label(new Rect(x + 1, y + 1, w, h), sitPrompt, shadow);
            GUI.Label(new Rect(x, y, w, h), sitPrompt, style);
        }
        else
        {
            if (!_isLit && inFireZone)
            {
                string prompt = "🔥 【E / クリック】焚き火に火を点ける";
                float w = 420f;
                float h = 40f;
                float x = (Screen.width - w) * 0.5f;
                float y = Screen.height * 0.65f;

                GUI.Label(new Rect(x + 1, y + 1, w, h), prompt, shadow);
                GUI.Label(new Rect(x, y, w, h), prompt, style);
            }
            else if (inBenchZone)
            {
                string prompt = "🪵 【E / クリック】丸太に腰掛ける（海と炎を眺める）";
                float w = 480f;
                float h = 40f;
                float x = (Screen.width - w) * 0.5f;
                float y = Screen.height * 0.65f;

                GUI.Label(new Rect(x + 1, y + 1, w, h), prompt, shadow);
                GUI.Label(new Rect(x, y, w, h), prompt, style);
            }
        }
    }

}
