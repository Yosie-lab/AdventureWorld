using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// 『Rust & Float』相棒Rustとの協力ギミック第1弾：
/// 【古代の蓄電クリスタルと上昇気流パズル（Ancient Power Crystal & Wind Vent）】
/// 1. 西側断崖の麓に眠る古代のクリスタルと閉じた風孔ファン
/// 2. Floatが近づいて【Fキー】を押すと、相棒Rustが飛行してエネルギービームを照射チャージ
/// 3. クリスタルが覚醒してファンが高速回転、垂直な巨大上昇気流（サーマル）が発生
/// 4. Floatが気流に乗って断崖の上（高台テラス）へ大跳躍
/// 5. 崖上の古代コンテナを開封して特別な探索ポイント(+2pt)と素材を獲得！
/// </summary>
public class AdventureRustCrystalPuzzle : MonoBehaviour
{
    public static AdventureRustCrystalPuzzle Instance { get; private set; }

    public const string PrefKeyCrystalActivated = "Adventure_AncientCrystal_Activated";
    public const string PrefKeyChestOpened = "Adventure_AncientCrystal_ChestOpened";

    [Header("配置座標（西側砂浜から内陸へ進んだ断崖の麓）")]
    public Vector3 crystalBasePos = new Vector3(205.0f, 13.0f, 292.0f);
    public Vector3 ventBasePos = new Vector3(208.5f, 12.8f, 293.0f);
    public Vector3 cliffTerracePos = new Vector3(208.0f, 32.5f, 310.0f);

    private bool _isActivated = false;
    public bool IsActivated => _isActivated;

    private bool _isCharging = false;
    private bool _isChestOpened = false;

    // クリスタル本体
    private Transform _crystalCore;
    private Material _crystalMat;
    private Light _crystalLight;

    // 送風ファン
    private Transform _fanBlades;
    private ParticleSystem _windPS;
    private AudioSource _windAudio;
    private SphereCollider _windTrigger;

    // 崖上の宝箱
    private GameObject _chestRoot;
    private Transform _chestLid;
    private Light _chestLight;

    // プロンプトUI
    private GameObject _promptCanvasGo;
    private CanvasGroup _promptCg;
    private Text _promptText;
    private float _rustGuideCooldown = 0f;

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
        var existing = Object.FindAnyObjectByType<AdventureRustCrystalPuzzle>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject("AdventureRustCrystalPuzzle");
        Instance = go.AddComponent<AdventureRustCrystalPuzzle>();
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
        AlignToTerrain();
        BuildStructures();
        CreatePromptUI();

        // 記録の復元
        _isActivated = PlayerPrefs.GetInt(PrefKeyCrystalActivated, 0) == 1;
        _isChestOpened = PlayerPrefs.GetInt(PrefKeyChestOpened, 0) == 1;

        if (_isActivated)
        {
            ApplyActivatedState(immediate: true);
        }
        if (_isChestOpened && _chestLid != null)
        {
            _chestLid.localRotation = Quaternion.Euler(-95f, 0f, 0f);
            if (_chestLight != null) _chestLight.enabled = false;
        }
    }

    void AlignToTerrain()
    {
        var land = Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();
        if (land != null)
        {
            float cy = land.SampleHeight(crystalBasePos) + land.transform.position.y;
            crystalBasePos.y = cy + 0.1f;

            float vy = land.SampleHeight(ventBasePos) + land.transform.position.y;
            ventBasePos.y = vy + 0.05f;

            float ty = land.SampleHeight(cliffTerracePos) + land.transform.position.y;
            cliffTerracePos.y = Mathf.Max(ty, 31.5f) + 0.1f;
        }
    }

    void BuildStructures()
    {
        var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // ═══════════════════════════════════════════════════════════════
        // 1. 古代の蓄電クリスタル塔（Ancient Power Crystal）
        // ═══════════════════════════════════════════════════════════════
        var crystalRoot = new GameObject("AncientCrystalPillar");
        crystalRoot.transform.SetParent(transform, false);
        crystalRoot.transform.position = crystalBasePos;

        // 石造の台座
        var basePillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        basePillar.name = "PillarBase";
        basePillar.transform.SetParent(crystalRoot.transform, false);
        basePillar.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        basePillar.transform.localScale = new Vector3(1.4f, 0.9f, 1.4f);
        var baseRend = basePillar.GetComponent<Renderer>();
        var stoneMat = new Material(litShader);
        stoneMat.SetColor("_BaseColor", new Color(0.42f, 0.44f, 0.46f));
        stoneMat.SetFloat("_Smoothness", 0.25f);
        if (baseRend != null) baseRend.material = stoneMat;

        // 浮遊する受光クリスタルコア（8角・六角錐プリミティブ）
        var crystalCoreGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crystalCoreGo.name = "CrystalCore";
        crystalCoreGo.transform.SetParent(crystalRoot.transform, false);
        crystalCoreGo.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        crystalCoreGo.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
        crystalCoreGo.transform.localScale = new Vector3(0.95f, 0.95f, 0.95f);
        _crystalCore = crystalCoreGo.transform;

        var coreCol = crystalCoreGo.GetComponent<Collider>();
        if (coreCol != null) Destroy(coreCol); // 当たり判定は台座側で持つ

        _crystalMat = new Material(litShader);
        _crystalMat.SetColor("_BaseColor", new Color(0.2f, 0.85f, 1.0f, 0.95f));
        _crystalMat.EnableKeyword("_EMISSION");
        _crystalMat.SetColor("_EmissionColor", new Color(0.1f, 0.45f, 0.75f) * 1.5f);
        _crystalMat.SetFloat("_Smoothness", 0.9f);
        var coreRend = crystalCoreGo.GetComponent<Renderer>();
        if (coreRend != null) coreRend.material = _crystalMat;

        // クリスタルライト
        var clGo = new GameObject("CrystalGlowLight");
        clGo.transform.SetParent(_crystalCore, false);
        _crystalLight = clGo.AddComponent<Light>();
        _crystalLight.type = LightType.Point;
        _crystalLight.color = new Color(0.3f, 0.9f, 1.0f);
        _crystalLight.range = 8.5f;
        _crystalLight.intensity = 2.2f;

        // 空高く立ち上る古代遺跡のシアン光柱（遠景ビーコン：遠くからでも場所がわかる）
        var beaconPillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beaconPillar.name = "CrystalBeaconPillar";
        beaconPillar.transform.SetParent(crystalRoot.transform, false);
        beaconPillar.transform.localPosition = new Vector3(0f, 22f, 0f);
        beaconPillar.transform.localScale = new Vector3(0.55f, 22f, 0.55f);
        var bpCol = beaconPillar.GetComponent<Collider>();
        if (bpCol != null) Destroy(bpCol);
        var bpRend = beaconPillar.GetComponent<Renderer>();
        var beaconMat = new Material(litShader);
        beaconMat.SetColor("_BaseColor", new Color(0.2f, 0.9f, 1f, 0.65f));
        beaconMat.EnableKeyword("_EMISSION");
        beaconMat.SetColor("_EmissionColor", new Color(0.3f, 1.2f, 2.2f) * 2.5f);
        if (bpRend != null) bpRend.material = beaconMat;

        // ═══════════════════════════════════════════════════════════════
        // 2. 上昇気流送風孔ファン（Wind Vent）
        // ═══════════════════════════════════════════════════════════════
        var ventRoot = new GameObject("AncientWindVent");
        ventRoot.transform.SetParent(transform, false);
        ventRoot.transform.position = ventBasePos;

        // 外枠リング
        var ventRim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ventRim.name = "VentRim";
        ventRim.transform.SetParent(ventRoot.transform, false);
        ventRim.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        ventRim.transform.localScale = new Vector3(3.4f, 0.15f, 3.4f);
        var rimRend = ventRim.GetComponent<Renderer>();
        var bronzeMat = new Material(litShader);
        bronzeMat.SetColor("_BaseColor", new Color(0.35f, 0.38f, 0.36f));
        bronzeMat.SetFloat("_Metallic", 0.65f);
        bronzeMat.SetFloat("_Smoothness", 0.45f);
        if (rimRend != null) rimRend.material = bronzeMat;

        // 回転するプロペラブレード
        var bladeRoot = new GameObject("FanBlades");
        bladeRoot.transform.SetParent(ventRoot.transform, false);
        bladeRoot.transform.localPosition = new Vector3(0f, 0.22f, 0f);
        _fanBlades = bladeRoot.transform;

        for (int i = 0; i < 4; i++)
        {
            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.transform.SetParent(_fanBlades, false);
            blade.transform.localRotation = Quaternion.Euler(0f, i * 45f, 15f);
            blade.transform.localScale = new Vector3(2.8f, 0.05f, 0.45f);
            var bCol = blade.GetComponent<Collider>();
            if (bCol != null) Destroy(bCol);
            var bRend = blade.GetComponent<Renderer>();
            if (bRend != null) bRend.material = bronzeMat;
        }

        // 上昇気流パーティクル
        var psGo = new GameObject("UpdraftWindParticles");
        psGo.transform.SetParent(ventRoot.transform, false);
        psGo.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        _windPS = psGo.AddComponent<ParticleSystem>();
        SetupWindParticles(_windPS);
        _windPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 気流の心地よいASMR風音
        _windAudio = ventRoot.AddComponent<AudioSource>();
        _windAudio.spatialBlend = 0.85f;
        _windAudio.minDistance = 4f;
        _windAudio.maxDistance = 35f;
        _windAudio.loop = true;
        _windAudio.volume = 0.75f;
        _windAudio.clip = CreateWindAudioClip();

        // ═══════════════════════════════════════════════════════════════
        // 3. 崖上の古代テラス＆宝箱（Cliff Terrace & Ancient Chest）
        // ═══════════════════════════════════════════════════════════════
        _chestRoot = new GameObject("AncientTerraceChest");
        _chestRoot.transform.SetParent(transform, false);
        _chestRoot.transform.position = cliffTerracePos;

        // テラス石床
        var terraceFloor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        terraceFloor.name = "TerracePlatform";
        terraceFloor.transform.SetParent(_chestRoot.transform, false);
        terraceFloor.transform.localPosition = new Vector3(0f, -0.2f, 0f);
        terraceFloor.transform.localScale = new Vector3(4.5f, 0.2f, 4.5f);
        var tfRend = terraceFloor.GetComponent<Renderer>();
        if (tfRend != null) tfRend.material = stoneMat;

        // 宝箱本体
        var chestBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chestBase.name = "ChestBase";
        chestBase.transform.SetParent(_chestRoot.transform, false);
        chestBase.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        chestBase.transform.localScale = new Vector3(1.1f, 0.7f, 0.8f);
        var goldMat = new Material(litShader);
        goldMat.SetColor("_BaseColor", new Color(0.85f, 0.72f, 0.35f));
        goldMat.SetFloat("_Metallic", 0.75f);
        goldMat.SetFloat("_Smoothness", 0.65f);
        var cbRend = chestBase.GetComponent<Renderer>();
        if (cbRend != null) cbRend.material = goldMat;

        // 宝箱の蓋
        var lidGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lidGo.name = "ChestLid";
        lidGo.transform.SetParent(_chestRoot.transform, false);
        lidGo.transform.localPosition = new Vector3(0f, 0.7f, -0.38f);
        lidGo.transform.localScale = new Vector3(1.15f, 0.25f, 0.85f);
        _chestLid = lidGo.transform;
        var lidRend = lidGo.GetComponent<Renderer>();
        if (lidRend != null) lidRend.material = goldMat;

        // 宝箱の黄金の輝き
        var chlGo = new GameObject("ChestGlowLight");
        chlGo.transform.SetParent(_chestRoot.transform, false);
        chlGo.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        _chestLight = chlGo.AddComponent<Light>();
        _chestLight.type = LightType.Point;
        _chestLight.color = new Color(1.0f, 0.85f, 0.4f);
        _chestLight.range = 6f;
        _chestLight.intensity = 2.8f;
    }

    void SetupWindParticles(ParticleSystem ps)
    {
        var main = ps.main;
        main.duration = 4f;
        main.loop = true;
        main.startLifetime = 1.6f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(12f, 16f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 65;

        var emission = ps.emission;
        emission.rateOverTime = 32f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1.4f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(0.4f, 0.9f, 1f), 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.2f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var curve = new AnimationCurve();
        curve.AddKey(0f, 0.4f);
        curve.AddKey(0.5f, 1.0f);
        curve.AddKey(1f, 0.2f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    AudioClip CreateWindAudioClip()
    {
        int rate = 44100;
        float duration = 2.0f;
        int numSamples = (int)(rate * duration);
        float[] samples = new float[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / rate;
            // 柔らかいピンクノイズ（風のゴオオオというASMR）
            float noise = (UnityEngine.Random.value - 0.5f) * 2f;
            float pulse = Mathf.Sin(t * 1.5f * Mathf.PI) * 0.15f + 0.85f;
            samples[i] = noise * pulse * 0.25f;
        }

        var clip = AudioClip.Create("UpdraftWindASMR", numSamples, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    void CreatePromptUI()
    {
        _promptCanvasGo = new GameObject("CrystalPuzzlePromptCanvas");
        _promptCanvasGo.transform.SetParent(transform, false);
        var canvas = _promptCanvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;
        _promptCanvasGo.AddComponent<CanvasScaler>();
        _promptCanvasGo.AddComponent<GraphicRaycaster>();

        _promptCg = _promptCanvasGo.AddComponent<CanvasGroup>();
        _promptCg.alpha = 0f;
        _promptCg.blocksRaycasts = false;

        var textGo = new GameObject("PromptText");
        textGo.transform.SetParent(_promptCanvasGo.transform, false);
        var rt = textGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.15f);
        rt.anchorMax = new Vector2(0.5f, 0.15f);
        rt.sizeDelta = new Vector2(700f, 60f);

        _promptText = textGo.AddComponent<Text>();
        _promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Font.CreateDynamicFontFromOSFont("Hiragino Sans", 20);
        _promptText.fontSize = 20;
        _promptText.alignment = TextAnchor.MiddleCenter;
        _promptText.color = Color.white;
        _promptText.raycastTarget = false;

        var shadow = textGo.AddComponent<Outline>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
    }

    void Update()
    {
        var player = AdventurePlayerController.Instance;
        if (player == null) return;

        Vector3 pPos = player.transform.position;

        // クリスタルの自転アニメーション
        if (_crystalCore != null)
        {
            float rotSpeed = _isActivated ? 85f : (_isCharging ? 180f : 25f);
            _crystalCore.Rotate(Vector3.up, rotSpeed * Time.deltaTime, Space.World);
            _crystalCore.Rotate(Vector3.forward, (rotSpeed * 0.45f) * Time.deltaTime, Space.Self);
        }

        // プロペラの高速回転
        if (_isActivated && _fanBlades != null)
        {
            _fanBlades.Rotate(Vector3.up, 650f * Time.deltaTime, Space.Self);
        }

        // ═══════════════════════════════════════════════════════════════
        // 上昇気流（サーマル）リフト判定
        // ═══════════════════════════════════════════════════════════════
        if (_isActivated)
        {
            float distToVent = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(ventBasePos.x, ventBasePos.z));
            float yDiff = pPos.y - ventBasePos.y;

            // ファン半径2.2m、高度+22m以内の空間にいる場合、強力な上昇気流を付与
            if (distToVent < 2.3f && yDiff >= -0.2f && yDiff < 22f)
            {
                player.ApplyUpdraft(16.5f);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // 崖上の古代宝箱インタラクション
        // ═══════════════════════════════════════════════════════════════
        float distToChest = Vector3.Distance(pPos, cliffTerracePos);
        if (!_isChestOpened && distToChest < 2.8f)
        {
            _promptCg.alpha = Mathf.MoveTowards(_promptCg.alpha, 1f, 5f * Time.deltaTime);
            _promptText.text = "<color=#FFE066><b>✨ 【Eキー】古代の浮遊遺物を開封する</b></color>";

            if (CheckInteractInput())
            {
                OpenChest();
            }
            return;
        }

        // ═══════════════════════════════════════════════════════════════
        // クリスタルへのFキー送電合図インタラクション
        // ═══════════════════════════════════════════════════════════════
        float distToCrystal = Vector3.Distance(pPos, crystalBasePos);
        if (!_isActivated && !_isCharging)
        {
            if (distToCrystal < 6.8f)
            {
                _promptCg.alpha = Mathf.MoveTowards(_promptCg.alpha, 1f, 5f * Time.deltaTime);
                _promptText.text = "<color=#80D8FF><b>✦ 【Fキー】相棒Rustにクリスタルへの送電を指示する ✦</b></color>";

                // 近づいた時にRustが一度だけアドバイス
                _rustGuideCooldown -= Time.deltaTime;
                if (_rustGuideCooldown <= 0f)
                {
                    _rustGuideCooldown = 40f;
                    var drone = AdventureRustDrone.Instance;
                    if (drone != null)
                    {
                        drone.SpeakCustom("ピピッ！あそこに古代の送電クリスタルがあるよ！\n【Fキー】を押して合図してくれたら、ボクのライトでチャージしてみるよ、Float！", 6.0f);
                    }
                }

                if (CheckFKeyInput())
                {
                    StartCrystalCharging();
                }
            }
            else
            {
                _promptCg.alpha = Mathf.MoveTowards(_promptCg.alpha, 0f, 5f * Time.deltaTime);
            }
        }
        else
        {
            if (distToChest >= 2.8f)
            {
                _promptCg.alpha = Mathf.MoveTowards(_promptCg.alpha, 0f, 5f * Time.deltaTime);
            }
        }
    }

    bool CheckFKeyInput()
    {
        var kb = Keyboard.current ?? AdventureInputReader.Keyboard;
        if (kb != null && kb.fKey.wasPressedThisFrame) return true;
        try { if (Input.GetKeyDown(KeyCode.F)) return true; } catch { }
        return false;
    }

    bool CheckInteractInput()
    {
        return AdventureInputReader.InteractDown || AdventureInputReader.MouseLeftDown;
    }

    void StartCrystalCharging()
    {
        var drone = AdventureRustDrone.Instance;
        if (drone == null || drone.IsPerformingGimmick) return;

        _isCharging = true;
        _promptCg.alpha = 0f;

        Vector3 targetPos = _crystalCore != null ? _crystalCore.position : crystalBasePos + Vector3.up * 2.5f;

        drone.CommandChargeCrystal(
            targetPos,
            onProgress: () =>
            {
                // チャージ中の明滅・発光強化
                if (_crystalMat != null)
                {
                    float pulse = Mathf.Sin(Time.time * 25f) * 0.5f + 0.5f;
                    Color glow = Color.Lerp(new Color(0.2f, 0.8f, 1f), new Color(1f, 0.95f, 0.4f), pulse) * 3.5f;
                    _crystalMat.SetColor("_EmissionColor", glow);
                }
                if (_crystalLight != null)
                {
                    _crystalLight.intensity = UnityEngine.Random.Range(3.5f, 5.5f);
                }
            },
            onComplete: () =>
            {
                _isCharging = false;
                _isActivated = true;
                PlayerPrefs.SetInt(PrefKeyCrystalActivated, 1);
                PlayerPrefs.Save();

                ApplyActivatedState(immediate: false);

                var hud = AdventureScrapHUD.Instance;
                if (hud != null)
                {
                    hud.ShowUpgradeBanner("✦ 上昇気流ファン起動！ ✦\n💡 気流に乗って断崖の上へ大跳躍しよう！");
                }
            }
        );
    }

    void ApplyActivatedState(bool immediate)
    {
        // クリスタルを完全覚醒カラーへ
        if (_crystalMat != null)
        {
            _crystalMat.SetColor("_EmissionColor", new Color(0.4f, 1.8f, 2.5f) * 2.8f);
        }
        if (_crystalLight != null)
        {
            _crystalLight.color = new Color(0.4f, 0.95f, 1.0f);
            _crystalLight.intensity = 4.2f;
        }

        // 上昇気流ファン・パーティクル・サウンドの開始
        if (_windPS != null && !_windPS.isPlaying)
        {
            _windPS.Play();
        }
        if (_windAudio != null && !_windAudio.isPlaying)
        {
            _windAudio.Play();
        }
    }

    void OpenChest()
    {
        _isChestOpened = true;
        PlayerPrefs.SetInt(PrefKeyChestOpened, 1);
        PlayerPrefs.Save();

        // 宝箱オープン演出
        StartCoroutine(AnimateChestOpen());

        // 探索ポイント+2pt加算
        var scrapMgr = AdventureScrapManager.Instance;
        if (scrapMgr != null)
        {
            scrapMgr.OnDriftBoxOpened(99, "古代の浮遊遺物");
        }

        var hud = AdventureScrapHUD.Instance;
        if (hud != null)
        {
            hud.ShowUpgradeBanner("✨ 古代の浮遊遺物を発見！ (+2 pt) ✨\n✦ 崖上の隠された宝箱を解放しました！");
        }

        var drone = AdventureRustDrone.Instance;
        if (drone != null)
        {
            drone.TriggerCelebration("わぁぁ！崖の上にこんなお宝があったんだね！ボクたちの力で登れたよ、Float！", 4.0f);
        }
    }

    IEnumerator AnimateChestOpen()
    {
        if (_chestLid != null)
        {
            Quaternion startRot = _chestLid.localRotation;
            Quaternion endRot = startRot * Quaternion.Euler(-95f, 0f, 0f);
            float elapsed = 0f;
            float dur = 0.5f;

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
                _chestLid.localRotation = Quaternion.Slerp(startRot, endRot, t);
                yield return null;
            }
            _chestLid.localRotation = endRot;
        }

        if (_chestLight != null)
        {
            float elapsed = 0f;
            while (elapsed < 0.6f)
            {
                elapsed += Time.deltaTime;
                _chestLight.intensity = Mathf.Lerp(2.8f, 0f, elapsed / 0.6f);
                yield return null;
            }
            _chestLight.enabled = false;
        }
    }

    /// <summary>ニューゲーム時の完全初期化リセット</summary>
    public void ResetForNewGame()
    {
        _isActivated = false;
        _isCharging = false;
        _isChestOpened = false;

        PlayerPrefs.DeleteKey(PrefKeyCrystalActivated);
        PlayerPrefs.DeleteKey(PrefKeyChestOpened);
        PlayerPrefs.Save();

        if (_crystalMat != null)
        {
            _crystalMat.SetColor("_EmissionColor", new Color(0.1f, 0.45f, 0.75f) * 1.5f);
        }
        if (_crystalLight != null)
        {
            _crystalLight.intensity = 2.2f;
        }

        if (_windPS != null)
        {
            _windPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        if (_windAudio != null)
        {
            _windAudio.Stop();
        }

        if (_chestLid != null)
        {
            _chestLid.localRotation = Quaternion.identity;
        }
        if (_chestLight != null)
        {
            _chestLight.enabled = true;
            _chestLight.intensity = 2.8f;
        }

        Debug.Log("[AdventureRustCrystalPuzzle] 🔄 古代クリスタルパズルを初期状態へリセットしました。");
    }
}
