using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 『Rust & Float』上空を優雅に舞うウミネコ（カモメ）の群れと鳴き声マネージャー。
/// 【物語演出ルール】：
/// 「ピピッ！……ありがとう、Niko！これで僕たちの翼は折れることはないよ！　大空の向こうまで、全力で行こう！！」
/// のセリフから初めてウミネコが大空に出現して飛行＆鳴き声を開始。
/// それまでの通常探索時・天蓋崩壊・注油・「あ 温かい油が 心臓部に…」の間はウミネコ飛行は完全になし（非表示・非アクティブ）。
/// </summary>
public class AdventureSoaringSeagullsManager : MonoBehaviour
{
    public static AdventureSoaringSeagullsManager Instance { get; private set; }

    [Header("Flock Settings")]
    public int flockCount = 7;

    [Header("Audio")]
    public AudioClip seagullCrySolo;
    public AudioClip seagullCryFlock;

    GameObject _birdsContainer;
    readonly List<SoaringBird> _birds = new List<SoaringBird>();
    AudioSource _ambientFlockAudio;
    float _ambientFlockTimer = 6.0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        Ensure();
    }

    public static void Ensure()
    {
        if (Instance != null) return;
        var existing = Object.FindAnyObjectByType<AdventureSoaringSeagullsManager>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject("AdventureSoaringSeagullsManager");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AdventureSoaringSeagullsManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        LoadAudioAssets();
        SetupAmbientFlockAudio();
        SpawnSoaringFlock();

        // 初期状態：飛行許可が出るまでコンテナを非アクティブ（ウミネコ飛行なし）に設定
        if (_birdsContainer != null)
            _birdsContainer.SetActive(IsSeagullFlightAllowed());
    }

    void LoadAudioAssets()
    {
#if UNITY_EDITOR
        seagullCrySolo = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/AudioFiles/06_birds/seagulls_1.wav");
        seagullCryFlock = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/AudioFiles/06_birds/seagulls_many_2.wav");
#endif
        if (seagullCrySolo == null)
            seagullCrySolo = SynthesizeSeagullCry(isFlock: false);
        if (seagullCryFlock == null)
            seagullCryFlock = SynthesizeSeagullCry(isFlock: true);
    }

    void SetupAmbientFlockAudio()
    {
        _ambientFlockAudio = gameObject.AddComponent<AudioSource>();
        _ambientFlockAudio.spatialBlend = 0.25f; // 広域上空アンビエント
        _ambientFlockAudio.playOnAwake = false;
        _ambientFlockAudio.volume = 0.32f;
    }

    void SpawnSoaringFlock()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        var featherMat = new Material(shader);
        featherMat.SetColor("_BaseColor", new Color(0.98f, 0.99f, 1.0f));

        var wingTipMat = new Material(shader);
        wingTipMat.SetColor("_BaseColor", new Color(0.20f, 0.22f, 0.28f));

        var beakMat = new Material(shader) { name = "Seagull_Beak" };
        beakMat.SetColor("_BaseColor", new Color(0.98f, 0.72f, 0.12f));

        // 目・口・鼻用の高精細パーツマテリアル
        var eyeIrisMat = new Material(shader) { name = "Seagull_EyeIris" };
        eyeIrisMat.SetColor("_BaseColor", new Color(1.0f, 0.90f, 0.35f)); // ウミネコの黄色い虹彩

        var eyePupilMat = new Material(shader) { name = "Seagull_EyePupil" };
        eyePupilMat.SetColor("_BaseColor", new Color(0.04f, 0.04f, 0.06f)); // つぶらな黒い瞳孔

        var eyeHlMat = new Material(shader) { name = "Seagull_EyeHighlight" };
        eyeHlMat.SetColor("_BaseColor", Color.white); // 澄んだ光の反射

        var beakRedMat = new Material(shader) { name = "Seagull_BeakRed" };
        beakRedMat.SetColor("_BaseColor", new Color(0.92f, 0.15f, 0.10f)); // クチバシ先端の赤斑

        var beakBlackMat = new Material(shader) { name = "Seagull_BeakBlack" };
        beakBlackMat.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.15f)); // クチバシ先端の黒帯

        var mouthLineMat = new Material(shader) { name = "Seagull_MouthLine" };
        mouthLineMat.SetColor("_BaseColor", new Color(0.35f, 0.18f, 0.06f)); // 口の合わせ目ライン

        var nostrilMat = new Material(shader) { name = "Seagull_Nostril" };
        nostrilMat.SetColor("_BaseColor", new Color(0.15f, 0.12f, 0.08f)); // 鼻孔

        var birdMats = new BirdMaterials
        {
            featherMat = featherMat,
            wingTipMat = wingTipMat,
            beakMat = beakMat,
            eyeIrisMat = eyeIrisMat,
            eyePupilMat = eyePupilMat,
            eyeHlMat = eyeHlMat,
            beakRedMat = beakRedMat,
            beakBlackMat = beakBlackMat,
            mouthLineMat = mouthLineMat,
            nostrilMat = nostrilMat
        };

        // ウミネコ全羽をまとめるコンテナ（一括で表示/非表示を切り替え可能）
        _birdsContainer = new GameObject("SoaringSeagullsContainer");
        _birdsContainer.transform.SetParent(transform, false);

        Vector3 initPlayerPos = new Vector3(160f, 10f, 280f);
        var player = Object.FindAnyObjectByType<AdventurePlayerController>();
        if (player != null) initPlayerPos = player.transform.position;

        for (int i = 0; i < flockCount; i++)
        {
            float altOffset = 18f + (i * 3.5f);
            Vector2 offset = Random.insideUnitCircle * 22f;
            Vector3 center = initPlayerPos + new Vector3(offset.x, altOffset, offset.y);

            float radiusX = (i < 3) ? Random.Range(22f, 36f) : Random.Range(38f, 62f);
            float radiusZ = (i < 3) ? Random.Range(20f, 32f) : Random.Range(32f, 54f);
            float speed = Random.Range(0.28f, 0.42f) * (i % 2 == 0 ? 1f : -1f);
            float startAngle = (i / (float)flockCount) * Mathf.PI * 2f + Random.Range(0f, 0.8f);

            var birdGo = new GameObject($"SoaringSeagull_{i + 1}");
            birdGo.transform.SetParent(_birdsContainer.transform, false);

            var bird = birdGo.AddComponent<SoaringBird>();
            // i < 3 は「わああ 見て Niko」以降にNikoの側まで舞い降りる近接エスコート鳥
            bird.Initialize(i, center, altOffset, radiusX, radiusZ, speed, startAngle, birdMats, seagullCrySolo);
            _birds.Add(bird);
        }
    }

    /// <summary>
    /// ウミネコの飛行および鳴き声が解禁されているか？
    /// 【ルール】「ピピッ！……ありがとう、Niko！」以降から大空を飛び始め、
    /// それまでの間（ゲーム開始・通常探索・天蓋崩壊・注油中・「あ 温かい油が 心臓部に…」）はウミネコ飛行は完全になし。
    /// </summary>
    public static bool IsSeagullFlightAllowed()
    {
        var tower = AdventureSanctuaryTowerManager.Instance
                    ?? Object.FindAnyObjectByType<AdventureSanctuaryTowerManager>();
        if (tower != null)
        {
            if (tower.ShowGameClearModal)
                return false;

            // エピローグ中、またはクリア後は当然許可
            if (tower.EpilogueTriggered || AdventureSanctuaryTowerManager.IsGameCleared)
                return true;

            // クライマックス中：「ピピッ！……ありがとう、Niko！」（_climaxBeatIndex >= 4）に達したら解禁！
            if (tower.IsClimaxAfterThanksSpeech)
                return true;

            // それまでの間は一切飛行禁止（なし）
            return false;
        }

        // クリア済みセーブデータがある場合は自由探索として許可、未クリアなら箱庭のため禁止
        return AdventureSanctuaryTowerManager.IsGameCleared;
    }

    /// <summary>
    /// 「わぁぁ……！見て、Niko！世界はこんなにも広かったんだ……！！」以降のエピローグ中判定。
    /// このフェーズからウミネコたちがNikoのすぐ側（近く）まで舞い降りてきて、
    /// 画面の至近距離を一緒に優雅に並走・クロス飛行（エスコート）する。
    /// </summary>
    public static bool IsEpilogueActive()
    {
        var tower = AdventureSanctuaryTowerManager.Instance
                    ?? Object.FindAnyObjectByType<AdventureSanctuaryTowerManager>();
        if (tower != null)
        {
            return tower.EpilogueTriggered || AdventureSanctuaryTowerManager.IsGameCleared;
        }
        return AdventureSanctuaryTowerManager.IsGameCleared;
    }

    /// <summary>ウミネコ鳴き声のミュート判定（飛行禁止中は常に鳴き声も完全ミュート）</summary>
    public static bool ShouldMuteSeagullCries()
    {
        var tower = AdventureSanctuaryTowerManager.Instance
                    ?? Object.FindAnyObjectByType<AdventureSanctuaryTowerManager>();
        if (tower != null && tower.ShowGameClearModal)
            return true;
        return !IsSeagullFlightAllowed();
    }

    /// <summary>クリアボード表示時：群れアンビエンスと各鳥の鳴き声を即停止</summary>
    public static void StopAmbientAndBirdCriesImmediate()
    {
        var mgr = Instance ?? Object.FindAnyObjectByType<AdventureSoaringSeagullsManager>();
        if (mgr != null && mgr._ambientFlockAudio != null && mgr._ambientFlockAudio.isPlaying)
            mgr._ambientFlockAudio.Stop();

        var birds = Object.FindObjectsByType<AdventureSoaringSeagullsManager.SoaringBird>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < birds.Length; i++)
        {
            var bird = birds[i];
            if (bird == null) continue;
            bird.StopCryImmediate();
        }
    }

    static List<AudioSource> _cachedWorldSeagullSources;
    static float _lastWorldSeagullScanTime = -10f;
    static bool _lastSilencedState = false;
    static bool _silencedStateInitialized = false;

    /// <summary>
    /// シーン内のすべてのウミネコ・カモメ音源（SeagullSound_*、BeachSeagulls配下、BeachWavesManager等）を
    /// 完全にミュート・停止させる強制安全網。状態が変化した時のみ走査・切り替えを行う。
    /// </summary>
    public static void SilenceAllWorldSeagulls(bool silence, bool force = false)
    {
        if (!force && _silencedStateInitialized && _lastSilencedState == silence)
            return;

        _silencedStateInitialized = true;
        _lastSilencedState = silence;

        if (_cachedWorldSeagullSources == null || _cachedWorldSeagullSources.Count == 0 || (Time.unscaledTime - _lastWorldSeagullScanTime > 30f))
        {
            _lastWorldSeagullScanTime = Time.unscaledTime;
            if (_cachedWorldSeagullSources == null)
                _cachedWorldSeagullSources = new List<AudioSource>();
            else
                _cachedWorldSeagullSources.Clear();

            var allSources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include);
            for (int i = 0; i < allSources.Length; i++)
            {
                var src = allSources[i];
                if (src == null) continue;
                string goName = src.gameObject.name.ToLower();
                string clipName = src.clip != null ? src.clip.name.ToLower() : "";

                if (goName.Contains("seagull") || goName.Contains("umineko") ||
                    clipName.Contains("seagull") || clipName.Contains("umineko"))
                {
                    _cachedWorldSeagullSources.Add(src);
                }
            }
        }

        for (int i = 0; i < _cachedWorldSeagullSources.Count; i++)
        {
            var src = _cachedWorldSeagullSources[i];
            if (src == null) continue;

            if (silence)
            {
                if (src.isPlaying)
                {
                    if (src.loop) src.Pause();
                    else src.Stop();
                }
                src.mute = true;
                src.volume = 0f;
            }
            else
            {
                src.mute = false;
                if (!src.isPlaying && src.loop)
                {
                    src.UnPause();
                }
            }
        }
    }

    void Update()
    {
        bool allowed = IsSeagullFlightAllowed();

        // 1. ウミネコの飛行：許可されるまでコンテナを完全非アクティブ（飛行なし・非表示）！
        if (_birdsContainer != null)
        {
            if (_birdsContainer.activeSelf != allowed)
            {
                _birdsContainer.SetActive(allowed);
                if (allowed)
                {
                    Debug.Log("[RustAndFloat] ✦ 「ピピッ！ありがとう、Float！」大空への全開ダイブ開始：ウミネコたちの飛行解禁！");
                }
            }
        }

        // 2. 音声の沈黙安全網（状態変化時のみ実行）
        SilenceAllWorldSeagulls(!allowed);

        if (!allowed)
        {
            if (_ambientFlockAudio != null && _ambientFlockAudio.isPlaying)
                _ambientFlockAudio.Stop();
            return;
        }

        // 「ピピッ！ありがとうNiko」以降：定期的に上空から遠くのカモメの群れの鳴き声を再生
        _ambientFlockTimer -= Time.deltaTime;
        if (_ambientFlockTimer <= 0f)
        {
            _ambientFlockTimer = Random.Range(18f, 32f);
            if (_ambientFlockAudio != null && seagullCryFlock != null)
            {
                _ambientFlockAudio.pitch = Random.Range(0.92f, 1.08f);
                _ambientFlockAudio.PlayOneShot(seagullCryFlock, Random.Range(0.25f, 0.38f));
            }
        }
    }

    /// <summary>
    /// プレイヤーの上空を滑空・旋回する1羽のウミネココンポーネント（プレイヤー追従で常に頭上を舞う）
    /// </summary>
    public class SoaringBird : MonoBehaviour
    {
        int _roleIndex; // 0, 1, 2: Nikoのすぐ側を飛ぶ近接エスコート鳥 / 3+: 広域上空旋回鳥
        Vector3 _orbitCenter;
        Vector3 _centerOffset;
        float _altitudeOffset;
        float _radiusX;
        float _radiusZ;
        float _speed;
        float _angle;

        Transform _playerTarget;
        Transform _body;
        Transform _head;
        Transform _leftWing;
        Transform _rightWing;
        Transform _tail;

        AudioSource _cryAudio;
        AudioClip _cryClip;
        float _cryTimer;

        float _flightTimer;
        float _glideTimer;
        bool _isGliding = true;

        float _closeBlend = 0f; // 0=通常オービット, 1=「見て、Niko」至近距離エスコート飛行

        public void Initialize(int role, Vector3 center, float altOffset, float rx, float rz, float spd, float initAngle,
                               BirdMaterials mats, AudioClip cryClip)
        {
            _roleIndex = role;
            _orbitCenter = center;
            _altitudeOffset = altOffset;
            _centerOffset = new Vector3(Random.Range(-15f, 15f), 0f, Random.Range(-15f, 15f));
            _radiusX = rx;
            _radiusZ = rz;
            _speed = spd;
            _angle = initAngle;
            _cryClip = cryClip;
            _cryTimer = Random.Range(4.0f, 14.0f);
            _glideTimer = Random.Range(3.5f, 7.5f);

            // 近接鳥はカメラ・Nikoのすぐ側を飛ぶため、自然で見やすい適正スケール（1.35倍）、上空鳥は1.7倍
            float baseScale = (_roleIndex < 3) ? 1.35f : 1.7f;
            transform.localScale = Vector3.one * baseScale;

            BuildBirdVisuals(mats);

            _cryAudio = gameObject.AddComponent<AudioSource>();
            _cryAudio.spatialBlend = (_roleIndex < 3) ? 0.65f : 0.85f;
            _cryAudio.minDistance = (_roleIndex < 3) ? 5f : 15f;
            _cryAudio.maxDistance = 160f;
            _cryAudio.rolloffMode = AudioRolloffMode.Linear;
            _cryAudio.playOnAwake = false;

            UpdatePositionAndRotation(0f);
        }

        void BuildBirdVisuals(BirdMaterials mats)
        {
            // 1. 胴体（滑らかな有機的流線型・紡錘形プロシージャルメッシュ）
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            bodyGo.transform.localScale = Vector3.one;
            bodyGo.transform.localPosition = Vector3.zero;
            var bodyMf = bodyGo.AddComponent<MeshFilter>();
            bodyMf.sharedMesh = CreateBirdBodyMesh();
            var bodyMr = bodyGo.AddComponent<MeshRenderer>();
            bodyMr.sharedMaterial = mats.featherMat;
            _body = bodyGo.transform;

            // 2. 頭部（Head）
            var headGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            headGo.name = "Head";
            headGo.transform.SetParent(transform, false);
            headGo.transform.localScale = new Vector3(0.25f, 0.25f, 0.28f);
            headGo.transform.localPosition = new Vector3(0f, 0.16f, 0.38f);
            headGo.GetComponent<Renderer>().sharedMaterial = mats.featherMat;
            Object.Destroy(headGo.GetComponent<Collider>());
            _head = headGo.transform;

            // 3. クチバシ（黄色く尖った嘴）
            var beakGo = new GameObject("Beak");
            beakGo.transform.SetParent(_head, false);
            beakGo.transform.localPosition = new Vector3(0f, -0.02f, 0.18f);
            beakGo.transform.localScale = new Vector3(0.07f, 0.07f, 0.25f);
            var beakMf = beakGo.AddComponent<MeshFilter>();
            beakMf.sharedMesh = CreateConeMesh(10);
            var beakMr = beakGo.AddComponent<MeshRenderer>();
            beakMr.sharedMaterial = mats.beakMat;

            // 3-1. 目・口・鼻の精緻なフェイシャルパーツ構築
            BuildBirdFace(_head, beakGo.transform, mats);

            // 4. 左右の翼（カモメの雄大な翼幅：翼長1.45m）
            var rWingGo = new GameObject("RightWing");
            rWingGo.transform.SetParent(transform, false);
            rWingGo.transform.localPosition = new Vector3(0.14f, 0.09f, 0.06f);
            rWingGo.transform.localScale = new Vector3(1.45f, 1.0f, 1.15f);
            var rMf = rWingGo.AddComponent<MeshFilter>();
            rMf.sharedMesh = CreateBirdWingMesh(true);
            var rMr = rWingGo.AddComponent<MeshRenderer>();
            rMr.sharedMaterial = mats.wingTipMat;
            _rightWing = rWingGo.transform;

            var lWingGo = new GameObject("LeftWing");
            lWingGo.transform.SetParent(transform, false);
            lWingGo.transform.localPosition = new Vector3(-0.14f, 0.09f, 0.06f);
            lWingGo.transform.localScale = new Vector3(1.45f, 1.0f, 1.15f);
            var lMf = lWingGo.AddComponent<MeshFilter>();
            lMf.sharedMesh = CreateBirdWingMesh(false);
            var lMr = lWingGo.AddComponent<MeshRenderer>();
            lMr.sharedMaterial = mats.wingTipMat;
            _leftWing = lWingGo.transform;

            // 5. 尾羽（扇状ファン）
            var tailGo = new GameObject("Tail");
            tailGo.transform.SetParent(transform, false);
            tailGo.transform.localPosition = new Vector3(0f, 0.06f, -0.42f);
            tailGo.transform.localScale = new Vector3(0.32f, 0.035f, 0.40f);
            var tMf = tailGo.AddComponent<MeshFilter>();
            tMf.sharedMesh = CreateTailFanMesh();
            var tMr = tailGo.AddComponent<MeshRenderer>();
            tMr.sharedMaterial = mats.featherMat;
            _tail = tailGo.transform;
        }

        void Update()
        {
            _flightTimer += Time.deltaTime;
            _angle += _speed * Time.deltaTime;

            UpdatePositionAndRotation(Time.deltaTime);
            UpdateFlightAnimation(Time.deltaTime);
            UpdateCries(Time.deltaTime);
        }

        void UpdatePositionAndRotation(float dt)
        {
            // プレイヤー（またはメインカメラ）を探索して位置を自動追従！
            if (_playerTarget == null)
            {
                var pc = AdventurePlayerController.Instance;
                if (pc != null) _playerTarget = pc.transform;
                else if (Camera.main != null) _playerTarget = Camera.main.transform;
            }

            bool isEpilogue = IsEpilogueActive();
            bool isCloseBird = (_roleIndex < 3);

            // 近接ブレンド（「わぁぁ……！見て、Niko！」に入ると、Nikoのすぐ側へスムーズに舞い降りてくる）
            float targetCloseBlend = (isEpilogue && isCloseBird) ? 1f : 0f;
            _closeBlend = Mathf.MoveTowards(_closeBlend, targetCloseBlend, dt * 0.75f);

            // 1. 通常旋回（オービット）計算
            if (_playerTarget != null)
            {
                Vector3 pPos = _playerTarget.position;
                Vector3 desiredCenter = new Vector3(pPos.x + _centerOffset.x, pPos.y + _altitudeOffset, pPos.z + _centerOffset.z);
                // プレイヤーの移動に遅れずついていくためLerp係数を2.8fに設定
                _orbitCenter = Vector3.Lerp(_orbitCenter, desiredCenter, dt * 2.8f);
            }

            float orbX = _orbitCenter.x + Mathf.Cos(_angle) * _radiusX;
            float orbZ = _orbitCenter.z + Mathf.Sin(_angle) * _radiusZ;
            float orbY = _orbitCenter.y + Mathf.Sin(_angle * 2.2f + _speed) * 3.8f;
            Vector3 orbitPos = new Vector3(orbX, orbY, orbZ);

            float dx = -Mathf.Sin(_angle) * _radiusX * _speed;
            float dz = Mathf.Cos(_angle) * _radiusZ * _speed;
            float dy = Mathf.Cos(_angle * 2.2f + _speed) * 2.5f * _speed;
            Vector3 orbitForward = new Vector3(dx, dy, dz).normalized;

            Vector3 finalPos = orbitPos;
            Vector3 finalForward = orbitForward;
            float bankTarget = -Mathf.Clamp(_speed * 65f, -34f, 34f);

            // 2. 「わああ 見て Niko」以降の近接エスコート飛行
            // プレイヤー（Niko）の視界前方・斜め前・頭上近くを一緒に並走滑空し、ウミネコが間近に美しく見える！
            if (_closeBlend > 0.001f && _playerTarget != null)
            {
                Vector3 pPos = _playerTarget.position;
                Vector3 pFwd = _playerTarget.forward;
                pFwd.y = 0f;
                if (pFwd.sqrMagnitude < 0.01f) pFwd = Vector3.forward;
                pFwd.Normalize();
                Vector3 pRight = Vector3.Cross(Vector3.up, pFwd).normalized;

                Vector3 closePos = pPos;
                Vector3 closeFwd = pFwd;
                float closeBank = 0f;

                if (_roleIndex == 0)
                {
                    // 【鳥0: Nikoの右斜め前エスコート】
                    // 視界の右斜め前でふわりと波打ちながら併走滑空
                    float sway = Mathf.Sin(_flightTimer * 1.6f);
                    float fwdDist = 8.2f + Mathf.Sin(_flightTimer * 0.9f) * 1.8f;
                    float rightDist = 4.8f + sway * 1.4f;
                    float upDist = 2.6f + Mathf.Cos(_flightTimer * 1.3f) * 0.7f;

                    closePos = pPos + pFwd * fwdDist + pRight * rightDist + Vector3.up * upDist;
                    closeFwd = (pFwd + pRight * (sway * 0.25f) + Vector3.up * (Mathf.Cos(_flightTimer * 1.3f) * 0.12f)).normalized;
                    closeBank = -sway * 18f;
                }
                else if (_roleIndex == 1)
                {
                    // 【鳥1: Nikoの左斜め前エスコート】
                    // 視界の左斜め前で風に乗りながら優雅に併走滑空
                    float sway = Mathf.Cos(_flightTimer * 1.4f);
                    float fwdDist = 6.6f + Mathf.Cos(_flightTimer * 0.8f) * 1.6f;
                    float rightDist = -(4.6f + sway * 1.2f);
                    float upDist = 3.4f + Mathf.Sin(_flightTimer * 1.1f) * 0.8f;

                    closePos = pPos + pFwd * fwdDist + pRight * rightDist + Vector3.up * upDist;
                    closeFwd = (pFwd - pRight * (sway * 0.22f) + Vector3.up * (Mathf.Sin(_flightTimer * 1.1f) * 0.10f)).normalized;
                    closeBank = sway * 16f;
                }
                else if (_roleIndex == 2)
                {
                    // 【鳥2: Nikoの前方を左右に優雅に横切るクロス飛行】
                    float cross = Mathf.Sin(_flightTimer * 0.85f);
                    float crossSpeed = Mathf.Cos(_flightTimer * 0.85f);
                    float fwdDist = 9.2f + Mathf.Abs(cross) * 1.5f;
                    float rightDist = cross * 6.5f;
                    float upDist = 4.2f + Mathf.Sin(_flightTimer * 1.7f) * 0.9f;

                    closePos = pPos + pFwd * fwdDist + pRight * rightDist + Vector3.up * upDist;
                    closeFwd = (pFwd * 0.85f + pRight * (crossSpeed * 0.65f) + Vector3.up * (Mathf.Cos(_flightTimer * 1.7f) * 0.15f)).normalized;
                    closeBank = -crossSpeed * 24f;
                }

                finalPos = Vector3.Lerp(orbitPos, closePos, _closeBlend);
                finalForward = Vector3.Slerp(orbitForward, closeFwd, _closeBlend);
                bankTarget = Mathf.Lerp(bankTarget, closeBank, _closeBlend);
            }

            if (finalForward.sqrMagnitude > 0.001f)
            {
                // 旋回方向に応じた自然なバンク（Roll傾斜）
                Quaternion lookRot = Quaternion.LookRotation(finalForward, Vector3.up);
                Quaternion bankRot = Quaternion.Euler(0f, 0f, bankTarget);

                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot * bankRot, dt * 6.0f);
            }

            transform.position = finalPos;
        }

        void UpdateFlightAnimation(float dt)
        {
            _glideTimer -= dt;
            if (_glideTimer <= 0f)
            {
                _isGliding = !_isGliding;
                _glideTimer = _isGliding ? Random.Range(4.5f, 9.0f) : Random.Range(1.8f, 3.2f);
            }

            float flapRoll = 0f;
            float flapPitch = 0f;

            if (_isGliding)
            {
                // 優雅な滑空（Soaring）：風に乗りわずかに揺らめく
                flapRoll = Mathf.Sin(_flightTimer * 2.5f) * 3.5f;
                flapPitch = -2.5f;
            }
            else
            {
                // パタパタとリズムよく羽ばたく
                float flapPhase = _flightTimer * 12.0f;
                flapRoll = Mathf.Sin(flapPhase) * 26.0f;
                flapPitch = Mathf.Cos(flapPhase) * 7.0f;

                if (_body != null)
                {
                    _body.localPosition = new Vector3(0f, Mathf.Sin(flapPhase) * 0.018f, 0f);
                }
            }

            if (_rightWing != null)
                _rightWing.localRotation = Quaternion.Euler(flapPitch, 0f, -flapRoll);
            if (_leftWing != null)
                _leftWing.localRotation = Quaternion.Euler(flapPitch, 0f, flapRoll);

            // 首（Head）が飛行中に時折地上を見下ろす自然な首振り
            if (_head != null)
            {
                float lookPitch = Mathf.Sin(_flightTimer * 1.2f) * 8f - 6f;
                float lookYaw = Mathf.Sin(_flightTimer * 0.8f) * 12f;
                _head.localRotation = Quaternion.Euler(lookPitch, lookYaw, 0f);
            }
        }

        public void StopCryImmediate()
        {
            if (_cryAudio != null && _cryAudio.isPlaying)
                _cryAudio.Stop();
        }

        void UpdateCries(float dt)
        {
            if (ShouldMuteSeagullCries())
            {
                StopCryImmediate();
                return;
            }

            _cryTimer -= dt;
            if (_cryTimer <= 0f)
            {
                _cryTimer = Random.Range(9.0f, 22.0f);
                if (_cryAudio != null && _cryClip != null)
                {
                    _cryAudio.pitch = Random.Range(0.95f, 1.12f);
                    _cryAudio.PlayOneShot(_cryClip, Random.Range(0.48f, 0.65f));
                }
            }
        }
    }

    #region Procedural Bird Meshes
    static Mesh CreateBirdBodyMesh()
    {
        var mesh = new Mesh { name = "ProcBirdBodySmooth" };
        var verts = new List<Vector3>();
        var tris = new List<int>();

        // 7断面（前〜後ろ）のロフト形状で胸がふっくらと丸く、尾に向かって自然に細くなる滑らかな鳥体
        var sections = new (float z, float y, float rx, float ry)[]
        {
            ( 0.38f,  0.13f, 0.05f, 0.05f), // 首付け根先端
            ( 0.30f,  0.11f, 0.13f, 0.12f), // 首〜胸のふくらみ
            ( 0.16f,  0.05f, 0.18f, 0.16f), // 丸みのある胸部
            ( 0.00f,  0.00f, 0.19f, 0.15f), // 胴体中央（翼基部）
            (-0.16f, -0.02f, 0.15f, 0.12f), // 腹部
            (-0.30f,  0.01f, 0.09f, 0.07f), // 後部絞り込み
            (-0.44f,  0.03f, 0.04f, 0.03f)  // 尾羽付け根
        };

        const int radialSegments = 12;

        // 前端キャップ頂点
        verts.Add(new Vector3(0f, 0.14f, 0.40f));
        int frontTipIndex = 0;

        // 各段のリング頂点
        int ringStart = 1;
        for (int s = 0; s < sections.Length; s++)
        {
            var sec = sections[s];
            for (int r = 0; r < radialSegments; r++)
            {
                float ang = (r / (float)radialSegments) * Mathf.PI * 2f;
                float x = Mathf.Cos(ang) * sec.rx;
                float y = sec.y + Mathf.Sin(ang) * sec.ry;
                verts.Add(new Vector3(x, y, sec.z));
            }
        }

        // 後端キャップ頂点
        int backTipIndex = verts.Count;
        verts.Add(new Vector3(0f, 0.03f, -0.46f));

        // 前端ファン
        for (int r = 0; r < radialSegments; r++)
        {
            int next = (r + 1) % radialSegments;
            tris.Add(frontTipIndex);
            tris.Add(ringStart + next);
            tris.Add(ringStart + r);
        }

        // 胴体リング間のクアッド
        for (int s = 0; s < sections.Length - 1; s++)
        {
            int curRing = ringStart + s * radialSegments;
            int nextRing = curRing + radialSegments;
            for (int r = 0; r < radialSegments; r++)
            {
                int nextR = (r + 1) % radialSegments;
                int a = curRing + r;
                int b = curRing + nextR;
                int c = nextRing + nextR;
                int d = nextRing + r;

                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(a); tris.Add(c); tris.Add(d);
            }
        }

        // 後端ファン
        int lastRing = ringStart + (sections.Length - 1) * radialSegments;
        for (int r = 0; r < radialSegments; r++)
        {
            int next = (r + 1) % radialSegments;
            tris.Add(backTipIndex);
            tris.Add(lastRing + r);
            tris.Add(lastRing + next);
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh CreateConeMesh(int subdivisions = 6)
    {
        var mesh = new Mesh { name = "ProcBeakCone" };
        var verts = new List<Vector3>();
        var tris = new List<int>();

        Vector3 tip = new Vector3(0f, 0f, 1f);
        verts.Add(tip);

        for (int i = 0; i < subdivisions; i++)
        {
            float angle = (i / (float)subdivisions) * Mathf.PI * 2f;
            verts.Add(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
        }

        for (int i = 0; i < subdivisions; i++)
        {
            int next = (i + 1) % subdivisions;
            tris.Add(0);
            tris.Add(1 + i);
            tris.Add(1 + next);
        }
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh CreateBirdWingMesh(bool isRight)
    {
        var mesh = new Mesh { name = isRight ? "ProcBirdWingR" : "ProcBirdWingL" };
        float dir = isRight ? 1f : -1f;

        var verts = new Vector3[]
        {
            new Vector3(0f, 0.025f, 0.12f),
            new Vector3(0f, 0.010f, -0.15f),
            new Vector3(dir * 0.45f, 0.018f, 0.08f),
            new Vector3(dir * 0.48f, 0.007f, -0.11f),
            new Vector3(dir * 1.0f, 0.003f, -0.03f),

            new Vector3(0f, -0.018f, 0.12f),
            new Vector3(0f, -0.007f, -0.15f),
            new Vector3(dir * 0.45f, -0.010f, 0.08f),
            new Vector3(dir * 0.48f, -0.004f, -0.11f),
            new Vector3(dir * 1.0f, -0.002f, -0.03f)
        };

        var tris = new List<int>();

        void AddQuad(int a, int b, int c, int d, bool flip)
        {
            if (flip)
            {
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
            else
            {
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
        }

        void AddTri(int a, int b, int c, bool flip)
        {
            if (flip)
            {
                tris.Add(a); tris.Add(c); tris.Add(b);
            }
            else
            {
                tris.Add(a); tris.Add(b); tris.Add(c);
            }
        }

        bool flipTop = !isRight;
        AddQuad(0, 2, 1, 3, flipTop);
        AddTri(2, 4, 3, flipTop);

        bool flipBottom = isRight;
        AddQuad(5, 7, 6, 8, flipBottom);
        AddTri(7, 9, 8, flipBottom);

        AddQuad(0, 5, 2, 7, !isRight);
        AddQuad(2, 7, 4, 9, !isRight);
        AddQuad(1, 3, 6, 8, isRight);
        AddQuad(3, 4, 8, 9, isRight);

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh CreateTailFanMesh()
    {
        var mesh = new Mesh { name = "ProcBirdTail" };
        var verts = new Vector3[]
        {
            new Vector3(-0.06f, 0.01f, 0.05f),
            new Vector3(0.06f, 0.01f, 0.05f),
            new Vector3(-0.16f, 0.005f, -0.22f),
            new Vector3(0.16f, 0.005f, -0.22f),

            new Vector3(-0.06f, -0.01f, 0.05f),
            new Vector3(0.06f, -0.01f, 0.05f),
            new Vector3(-0.16f, -0.005f, -0.22f),
            new Vector3(0.16f, -0.005f, -0.22f)
        };
        var tris = new int[]
        {
            0, 1, 2,  1, 3, 2,
            4, 6, 5,  5, 6, 7
        };
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
    #endregion

    #region Audio Synthesis Fallback
    static AudioClip SynthesizeSeagullCry(bool isFlock)
    {
        int rate = 44100;
        float duration = isFlock ? 3.0f : 1.2f;
        int count = Mathf.RoundToInt(rate * duration);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float sum = 0f;

            int cryCount = isFlock ? 3 : 1;
            for (int c = 0; c < cryCount; c++)
            {
                float offset = c * 0.75f;
                float ct = t - offset;
                if (ct >= 0f && ct < 0.95f)
                {
                    float env = Mathf.Sin(ct / 0.95f * Mathf.PI);
                    env = Mathf.Pow(env, 1.4f);

                    float fBase = Mathf.Lerp(1200f, 2400f, Mathf.Sin(ct / 0.95f * Mathf.PI));
                    float s = Mathf.Sin(2f * Mathf.PI * fBase * ct) * 0.45f
                            + Mathf.Sin(4f * Mathf.PI * fBase * ct) * 0.30f
                            + Mathf.Sin(6f * Mathf.PI * fBase * ct) * 0.15f;

                    sum += s * env * (isFlock ? 0.28f : 0.45f);
                }
            }

            samples[i] = Mathf.Clamp(sum, -1f, 1f);
        }

        var clip = AudioClip.Create("ProcSeagullCry", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
    #endregion
    /// <summary>ウミネコ用レンダリングマテリアル一式</summary>
    public struct BirdMaterials
    {
        public Material featherMat;
        public Material wingTipMat;
        public Material beakMat;
        public Material eyeIrisMat;
        public Material eyePupilMat;
        public Material eyeHlMat;
        public Material beakRedMat;
        public Material beakBlackMat;
        public Material mouthLineMat;
        public Material nostrilMat;
    }

    static void BuildBirdFace(Transform head, Transform beak, BirdMaterials mats)
    {
        // 1. 左右の目（黄色い虹彩 + つぶらな黒瞳 + 澄んだハイライト）
        BuildEye(head, true, mats.eyeIrisMat, mats.eyePupilMat, mats.eyeHlMat);
        BuildEye(head, false, mats.eyeIrisMat, mats.eyePupilMat, mats.eyeHlMat);

        // 2. 口（クチバシの開口・合わせ目ライン）
        var mouthGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mouthGo.name = "MouthLine";
        mouthGo.transform.SetParent(beak, false);
        mouthGo.transform.localPosition = new Vector3(0f, 0f, 0.48f);
        mouthGo.transform.localScale = new Vector3(1.02f, 0.04f, 0.90f);
        mouthGo.transform.localRotation = Quaternion.identity;
        mouthGo.GetComponent<Renderer>().sharedMaterial = mats.mouthLineMat;
        Object.Destroy(mouthGo.GetComponent<Collider>());

        // 3. ウミネコ特有のクチバシ先端の赤黒斑紋（象徴的アイデンティティ）
        // 黒帯（先端手前の黒いバンド）
        var blackBandGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blackBandGo.name = "BeakBlackBand";
        blackBandGo.transform.SetParent(beak, false);
        blackBandGo.transform.localPosition = new Vector3(0f, 0f, 0.72f);
        blackBandGo.transform.localScale = new Vector3(0.36f, 0.36f, 0.14f);
        blackBandGo.transform.localRotation = Quaternion.identity;
        blackBandGo.GetComponent<Renderer>().sharedMaterial = mats.beakBlackMat;
        Object.Destroy(blackBandGo.GetComponent<Collider>());

        // 赤斑（下嘴先端の鮮やかな赤色スポット）
        var redSpotGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        redSpotGo.name = "BeakRedSpot";
        redSpotGo.transform.SetParent(beak, false);
        redSpotGo.transform.localPosition = new Vector3(0f, -0.06f, 0.84f);
        redSpotGo.transform.localScale = new Vector3(0.24f, 0.20f, 0.16f);
        redSpotGo.transform.localRotation = Quaternion.identity;
        redSpotGo.GetComponent<Renderer>().sharedMaterial = mats.beakRedMat;
        Object.Destroy(redSpotGo.GetComponent<Collider>());

        // 4. 左右の鼻（クチバシ根元上面の鼻孔スリット）
        BuildNostril(beak, true, mats.nostrilMat);
        BuildNostril(beak, false, mats.nostrilMat);
    }

    static void BuildEye(Transform head, bool isRight, Material irisMat, Material pupilMat, Material hlMat)
    {
        float side = isRight ? 1f : -1f;

        // 虹彩（黄色いアイリング）
        var irisGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        irisGo.name = isRight ? "RightEye_Iris" : "LeftEye_Iris";
        irisGo.transform.SetParent(head, false);
        irisGo.transform.localPosition = new Vector3(side * 0.46f, 0.12f, 0.26f);
        irisGo.transform.localScale = new Vector3(0.10f, 0.14f, 0.14f);
        irisGo.transform.localRotation = Quaternion.Euler(0f, side * 35f, 0f);
        irisGo.GetComponent<Renderer>().sharedMaterial = irisMat;
        Object.Destroy(irisGo.GetComponent<Collider>());

        // 瞳孔（黒目）
        var pupilGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pupilGo.name = isRight ? "RightEye_Pupil" : "LeftEye_Pupil";
        pupilGo.transform.SetParent(irisGo.transform, false);
        pupilGo.transform.localPosition = new Vector3(side * 0.22f, 0f, 0.15f);
        pupilGo.transform.localScale = new Vector3(0.85f, 0.75f, 0.75f);
        pupilGo.GetComponent<Renderer>().sharedMaterial = pupilMat;
        Object.Destroy(pupilGo.GetComponent<Collider>());

        // ハイライト（澄んだ光の反射）
        var hlGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hlGo.name = isRight ? "RightEye_Highlight" : "LeftEye_Highlight";
        hlGo.transform.SetParent(pupilGo.transform, false);
        hlGo.transform.localPosition = new Vector3(side * 0.20f, 0.32f, 0.35f);
        hlGo.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
        hlGo.GetComponent<Renderer>().sharedMaterial = hlMat;
        Object.Destroy(hlGo.GetComponent<Collider>());
    }

    static void BuildNostril(Transform beak, bool isRight, Material nostrilMat)
    {
        float side = isRight ? 1f : -1f;
        var nostrilGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nostrilGo.name = isRight ? "RightNostril" : "LeftNostril";
        nostrilGo.transform.SetParent(beak, false);
        nostrilGo.transform.localPosition = new Vector3(side * 0.18f, 0.22f, 0.20f);
        nostrilGo.transform.localScale = new Vector3(0.08f, 0.05f, 0.22f);
        nostrilGo.transform.localRotation = Quaternion.Euler(-6f, side * 10f, 0f);
        nostrilGo.GetComponent<Renderer>().sharedMaterial = nostrilMat;
        Object.Destroy(nostrilGo.GetComponent<Collider>());
    }

}
