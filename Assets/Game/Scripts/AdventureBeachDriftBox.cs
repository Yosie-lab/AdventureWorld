using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 砂浜に漂着した情報ボックス（Drift Box）
/// プレイヤーが近づくと蓋が開き、島の情報・サバイバルメモ・次の目標を提示する
/// </summary>
public class AdventureBeachDriftBox : MonoBehaviour
{
    public int boxId = 1;
    public string boxTitle = "漂着防水ケース";
    public string BoxDisplayName => string.IsNullOrEmpty(boxTitle) ? "漂着防水ケース" : boxTitle;
    public string author = "記録メモ";
    [TextArea(3, 8)]
    public string message = "ここにメッセージが入ります。";
    public string rustDialogue = "何か入ってるよ！";
    public string nextObjective = "内陸のせせらぎ池を目指そう";

    public bool isOpened = false;

    // ── パラメータ定数 ──
    private static class VisualConfig
    {
        public static readonly Color UnopenedGold = new Color(1.0f, 0.80f, 0.28f);
        public static readonly Color ChampagneGlow = new Color(1.0f, 0.92f, 0.55f);
        public static readonly Color SoftAuraColor = new Color(1.0f * 2.5f, 0.82f * 2.5f, 0.30f * 2.5f, 0.55f);
        public static readonly Color SeamLightColor = new Color(1.0f * 2.2f, 0.95f * 2.2f, 0.60f * 2.2f, 0.90f);
        public static readonly Color OpenedEmerald = new Color(0.20f, 1.0f, 0.60f);
        public static readonly Color OpenedLightColor = new Color(0.25f, 1.0f, 0.65f);

        public const float TriggerDistance = 4.5f; // 4.5m接近で確実に反応
        public const float LightRangeUnopenedBase = 24.0f;
        public const float LightRangeUnopenedPulse = 4.0f;
        public const float LightIntensityUnopenedBase = 5.6f;
        public const float LightIntensityUnopenedPulse = 1.8f;

        public const float LightRangeOpened = 6.0f;
        public const float LightIntensityOpened = 1.6f;

        public const float BoxAuraBaseScale = 1.85f;
        public const float BoxAuraPulseScale = 0.25f;
    }

    private Transform _lid;
    private Renderer _lampRenderer;
    private ParticleSystem _particles;
    private AudioSource _audioSource;
    private AudioClip _openClip;
    private MaterialPropertyBlock _mpb;

    // ── 発光・漂着パーツ同等ビーコン＆幻想エフェクト ──
    private Light _pointLight;
    private Transform _boxBodyAura;
    private Transform _groundGlow;
    private Transform _lampGlow;
    private Transform _seamGlow;
    private ParticleSystem _magicalDust;
    private ParticleSystem _twinkleStars;
    private Transform _beaconPillar;
    private Transform _beaconOuterPillar;
    private ParticleSystem _verticalBeam;
    private Material _boxAuraMat;
    private Material _groundGlowMat;
    private Material _lampGlowMat;
    private Material _seamMat;
    private Material _particleMat;
    private Material _twinkleMat;
    private Material _beaconMat;
    private Material _beaconOuterMat;
    private Material _verticalBeamMat;
    private Coroutine _auraFadeCoroutine;

    // UI関連（シングルトン共有モーダル）
    private static Canvas _modalCanvas;
    private static GameObject _modalPanel;
    private static Text _modalTitleText;
    private static Text _modalBodyText;
    private static Text _modalAuthorText;
    private static Text _modalCloseHintText;
    private static bool _isModalOpen = false;
    private static float _modalOpenTimestamp = 0f;

    public static bool IsModalOpen => _isModalOpen;
    public static bool CanCloseModal =>
        _isModalOpen && Time.unscaledTime - _modalOpenTimestamp >= 0.28f;

    // ── アクティブインスタンス管理（コンパスHUD・Rustドローン連携用） ──
    private static readonly List<AdventureBeachDriftBox> _activeBoxes = new List<AdventureBeachDriftBox>();
    public static IReadOnlyList<AdventureBeachDriftBox> ActiveBoxes => _activeBoxes;

    /// <summary>指定地点から最も近い未開封の漂着ボックスを取得する</summary>
    public static AdventureBeachDriftBox GetNearestUnopenedBox(Vector3 playerPos, out float minDistance)
    {
        minDistance = float.MaxValue;
        AdventureBeachDriftBox nearest = null;
        for (int i = 0; i < _activeBoxes.Count; i++)
        {
            var box = _activeBoxes[i];
            if (box == null || box.isOpened) continue;
            float d = Vector3.Distance(playerPos, box.transform.position);
            if (d < minDistance)
            {
                minDistance = d;
                nearest = box;
            }
        }
        return nearest;
    }

    void OnEnable()
    {
        if (!_activeBoxes.Contains(this))
            _activeBoxes.Add(this);
    }

    void OnDisable()
    {
        _activeBoxes.Remove(this);
    }

    bool _setupComplete;

    void Awake()
    {
        _mpb = new MaterialPropertyBlock();

        // オーディオのみ Awake で用意（蓋・boxId は Spawn 後に Configure される）
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0.15f;
        _audioSource.playOnAwake = false;
        _audioSource.minDistance = 8f;
        _audioSource.maxDistance = 50f;
        _audioSource.rolloffMode = AudioRolloffMode.Linear;
        _audioSource.volume = 1.0f;
        _openClip = CreateChimeSound();

        EnsureModalUI();
    }

    /// <summary>
    /// 階層・boxId 確定後に呼ぶ。開封済みは蓋を開けたまま復元する。
    /// </summary>
    public void ConfigureAfterBuild()
    {
        CacheHierarchyRefs();
        isOpened = PlayerPrefs.GetInt("DriftBox_Opened_" + boxId, 0) == 1;
        if (!_setupComplete)
        {
            Transform lamp = transform.Find("SignalLamp");
            SetupGlowEffects(lamp);
            _setupComplete = true;
        }
        ApplyVisualState(isOpened, immediate: true);
    }

    void CacheHierarchyRefs()
    {
        _lid = transform.Find("BoxLid");
        Transform lamp = transform.Find("SignalLamp");
        if (lamp != null) _lampRenderer = lamp.GetComponent<Renderer>();
        _particles = GetComponentInChildren<ParticleSystem>();
    }

    /// <summary>セーブ復元後など、PlayerPrefs の開封状態を見た目へ再同期</summary>
    public static void SyncAllOpenedVisualsFromPrefs()
    {
        var boxes = Object.FindObjectsByType<AdventureBeachDriftBox>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < boxes.Length; i++)
        {
            var b = boxes[i];
            if (b == null) continue;
            if (b.boxId <= 0) continue;
            bool opened = PlayerPrefs.GetInt("DriftBox_Opened_" + b.boxId, 0) == 1;
            b.isOpened = opened;
            if (b._lid == null) b.CacheHierarchyRefs();
            b.ApplyVisualState(opened, immediate: true);
        }
        AdventureScrapManager.Instance?.InvalidateDriftBoxCache();
    }

    void OnDestroy()
    {
        // 動的生成マテリアルの安全なメモリ解放
        if (_boxAuraMat != null) Destroy(_boxAuraMat);
        if (_groundGlowMat != null) Destroy(_groundGlowMat);
        if (_lampGlowMat != null) Destroy(_lampGlowMat);
        if (_seamMat != null) Destroy(_seamMat);
        if (_particleMat != null) Destroy(_particleMat);
        if (_twinkleMat != null) Destroy(_twinkleMat);
        if (_beaconMat != null) Destroy(_beaconMat);
        if (_beaconOuterMat != null) Destroy(_beaconOuterMat);
        if (_verticalBeamMat != null) Destroy(_verticalBeamMat);
    }

    void Start()
    {
        // シーン配置／Configure 漏れ時のフォールバック
        if (!_setupComplete)
            ConfigureAfterBuild();
        else
            ApplyVisualState(isOpened, immediate: true);
    }

    void Update()
    {
        // カメラ向きオーラグローの姿勢追従
        if (Camera.main != null)
        {
            var camRot = Camera.main.transform.rotation;
            if (_boxBodyAura != null) _boxBodyAura.rotation = camRot;
            if (_lampGlow != null) _lampGlow.rotation = camRot;
        }

        // 実行時デバッグ：Shift + B で全ドリフトボックスを即座に未開封リセット
        CheckDebugResetKey();

        if (!isOpened)
        {
            UpdateUnopenedPulses();
            CheckPlayerProximity();
        }
        else
        {
            // 開封済みでも近くでEキーを押せばいつでもメモを再読可能
            CheckReopenProximity();
        }
    }

    private void CheckDebugResetKey()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var kb = Keyboard.current;
        if (kb != null && kb.bKey.wasPressedThisFrame && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed))
        {
            ResetAllBoxesStatic();
        }
#endif
    }

    private void UpdateUnopenedPulses()
    {
        float t = Time.time;

        // ランプの呼吸点滅（温かみのあるシャンパンゴールド・高輝度）
        float pulse = 1.0f + Mathf.Sin(t * 3.0f) * 0.45f;
        SetLampColor(VisualConfig.UnopenedGold, pulse * 4.5f);

        // ポイントライトによる砂浜の柔らかな照光
        if (_pointLight != null)
        {
            _pointLight.intensity = VisualConfig.LightIntensityUnopenedBase + Mathf.Sin(t * 3.0f) * VisualConfig.LightIntensityUnopenedPulse;
            _pointLight.range = VisualConfig.LightRangeUnopenedBase + Mathf.Sin(t * 3.0f) * VisualConfig.LightRangeUnopenedPulse;
        }

        // ボックスを包む柔らかな光のオーラの呼吸パルス
        if (_boxBodyAura != null)
        {
            float aScale = VisualConfig.BoxAuraBaseScale + Mathf.Sin(t * 2.5f) * VisualConfig.BoxAuraPulseScale;
            _boxBodyAura.localScale = Vector3.one * aScale;
        }

        // 足元グラウンドグローの脈動
        if (_groundGlow != null)
        {
            _groundGlow.rotation = Quaternion.Euler(90f, 0f, 0f);
            float gPulse = 1.0f + Mathf.Sin(t * 2.5f) * 0.12f;
            _groundGlow.localScale = Vector3.one * (4.0f * gPulse);
        }

        // ランプグローの微細な瞬き
        if (_lampGlow != null)
        {
            _lampGlow.localScale = Vector3.one * (0.75f + Mathf.Sin(t * 4.0f) * 0.12f);
        }

        // 蓋の隙間から漏れる光のゆらめき
        if (_seamGlow != null)
        {
            float seamAlpha = 0.70f + Mathf.Sin(t * 3.5f) * 0.25f;
            if (_seamMat != null)
            {
                Color sc = VisualConfig.SeamLightColor;
                sc.a = seamAlpha;
                _seamMat.SetColor("_BaseColor", sc);
            }
        }

        // 漂着パーツ同等の光の柱（ビーコン）の神秘的な脈動（高さ約110m・二重多層光柱で遠景視認性を劇的向上）
        if (_beaconPillar != null)
        {
            _beaconPillar.rotation = Quaternion.identity;
            float bPulse = 1.0f + Mathf.Sin(t * 2.8f) * 0.15f;
            _beaconPillar.localScale = new Vector3(0.65f * bPulse, 55f, 0.65f * bPulse);
        }

        if (_beaconOuterPillar != null)
        {
            _beaconOuterPillar.rotation = Quaternion.identity;
            float oPulse = 1.0f + Mathf.Cos(t * 2.0f) * 0.12f;
            _beaconOuterPillar.localScale = new Vector3(2.2f * oPulse, 55f, 2.2f * oPulse);
        }

        if (_verticalBeam != null)
        {
            _verticalBeam.transform.rotation = Quaternion.identity;
        }
    }

    private void CheckPlayerProximity()
    {
        // ナラティブモーダル（二人の漂着艇等）やプロローグ目覚め・キーストーン演出中は自動開封しない
        if (AdventureBeachNarrativeManager.Instance != null && AdventureBeachNarrativeManager.Instance.IsShowingModal)
            return;
        if (AdventurePrologueDrama.Instance != null && (AdventurePrologueDrama.Instance.IsAwakening || AdventurePrologueDrama.Instance.IsShowingDashBoard))
            return;

        var player = AdventurePlayerController.Instance;
        if (player != null)
        {
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist < VisualConfig.TriggerDistance)
            {
                // 4.5m接近で自動開封、またはEキーでも即時開封
                OpenBox();
            }
        }
    }

    private void CheckReopenProximity()
    {
        if (_isModalOpen) return;
        if (AdventureBeachNarrativeManager.Instance != null && AdventureBeachNarrativeManager.Instance.IsShowingModal)
            return;
        if (AdventurePrologueDrama.Instance != null && (AdventurePrologueDrama.Instance.IsAwakening || AdventurePrologueDrama.Instance.IsShowingDashBoard))
            return;

        var player = AdventurePlayerController.Instance;
        if (player != null)
        {
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist < VisualConfig.TriggerDistance)
            {
                bool ePressed = false;
                var kb = Keyboard.current;
                if (kb != null && kb.eKey.wasPressedThisFrame) ePressed = true;
                try
                {
                    if (Input.GetKeyDown(KeyCode.E)) ePressed = true;
                }
                catch { }

                if (ePressed)
                {
                    ShowModal(boxTitle, author, message);
                }
            }
        }
    }

    #region 発光・視認性エフェクト構築

    private static Shader GetSafeUnlitShader()
    {
        return Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("RustAndFloat/WhiteSmoke")
            ?? Shader.Find("Sprites/Default");
    }

    private static Material CreateTransparentAdditiveMaterial(Shader shader, Texture2D tex, Color color, int renderQueue = 3100)
    {
        var mat = new Material(shader);
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", color);

        // URP 半透明・加算ブレンド・両面描画設定
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1f); // Additive
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // Double-sided (Cull Off)
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);

        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = renderQueue;
        return mat;
    }

    /// <summary>星のようにキラッと瞬くプリズムテクスチャの生成</summary>
    private static Texture2D CreateStarTwinkleTexture()
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - center) / center;
                float dy = Mathf.Abs(y - center) / center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                // 十字の光の筋（水平・垂直）＋中心コア
                float crossH = Mathf.Clamp01(1f - dy * 4.5f) * Mathf.Clamp01(1f - dx);
                float crossV = Mathf.Clamp01(1f - dx * 4.5f) * Mathf.Clamp01(1f - dy);
                float core = Mathf.Clamp01(1f - d * 2.2f);

                float alpha = Mathf.Clamp01(crossH * 0.70f + crossV * 0.70f + core);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex;
    }

    private void SetupGlowEffects(Transform lamp)
    {
        Vector3 lampLocalPos = lamp != null ? lamp.localPosition : new Vector3(0.38f, 0.94f, 0.22f);
        var smokeTex = AdventureRustDrone.GetSoftSmokeTexture();
        var unlitShader = GetSafeUnlitShader();

        // 1. 周囲の砂浜を温かく照らす間接光ポイントライト
        var lightGo = new GameObject("DriftBoxPointLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.38f, 0f);
        _pointLight = lightGo.AddComponent<Light>();
        _pointLight.type = LightType.Point;
        _pointLight.range = VisualConfig.LightRangeUnopenedBase;
        _pointLight.intensity = VisualConfig.LightIntensityUnopenedBase;
        _pointLight.color = VisualConfig.UnopenedGold;
        _pointLight.shadows = LightShadows.None;

        // 2. ボックス全体を包み込む柔らかな光のオーラ（Soft Box Aura）
        var auraQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        auraQuad.name = "BoxBodyAura";
        auraQuad.transform.SetParent(transform, false);
        auraQuad.transform.localPosition = new Vector3(0f, 0.28f, 0f);
        auraQuad.transform.localScale = Vector3.one * VisualConfig.BoxAuraBaseScale;
        Destroy(auraQuad.GetComponent<Collider>());

        var auraRend = auraQuad.GetComponent<Renderer>();
        if (auraRend != null)
        {
            _boxAuraMat = CreateTransparentAdditiveMaterial(unlitShader, smokeTex, VisualConfig.SoftAuraColor, 3120);
            auraRend.material = _boxAuraMat;
        }
        _boxBodyAura = auraQuad.transform;

        // 2-B. 足元の砂浜を黄金に染めるグラウンドライトディスク
        var groundQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        groundQuad.name = "GroundGlowDisc";
        groundQuad.transform.SetParent(transform, false);
        groundQuad.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        groundQuad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        groundQuad.transform.localScale = Vector3.one * 4.0f;
        Destroy(groundQuad.GetComponent<Collider>());

        var groundRend = groundQuad.GetComponent<Renderer>();
        if (groundRend != null)
        {
            _groundGlowMat = CreateTransparentAdditiveMaterial(unlitShader, smokeTex, new Color(1.0f * 2.4f, 0.85f * 2.4f, 0.32f * 2.4f, 0.65f), 3125);
            groundRend.material = _groundGlowMat;
        }
        _groundGlow = groundQuad.transform;

        // 3. 蓋の隙間から漏れ出す神秘的な光（Seam Glow）
        var seamQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        seamQuad.name = "SeamGlow";
        seamQuad.transform.SetParent(transform, false);
        seamQuad.transform.localPosition = new Vector3(0f, 0.23f, 0.32f);
        seamQuad.transform.localScale = new Vector3(0.92f, 0.12f, 1f);
        Destroy(seamQuad.GetComponent<Collider>());

        var seamRend = seamQuad.GetComponent<Renderer>();
        if (seamRend != null)
        {
            _seamMat = CreateTransparentAdditiveMaterial(unlitShader, smokeTex, VisualConfig.SeamLightColor, 3130);
            seamRend.material = _seamMat;
        }
        _seamGlow = seamQuad.transform;

        // 4. アンテナランプのソフトグロー（内側高輝度コア）
        var lampQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        lampQuad.name = "LampGlow";
        lampQuad.transform.SetParent(transform, false);
        lampQuad.transform.localPosition = lampLocalPos;
        lampQuad.transform.localScale = Vector3.one * 0.75f;
        Destroy(lampQuad.GetComponent<Collider>());

        var lampRend = lampQuad.GetComponent<Renderer>();
        if (lampRend != null)
        {
            _lampGlowMat = CreateTransparentAdditiveMaterial(unlitShader, smokeTex, new Color(1.0f * 3.5f, 0.92f * 3.5f, 0.50f * 3.5f, 0.95f), 3140);
            lampRend.material = _lampGlowMat;
        }
        _lampGlow = lampQuad.transform;

        // 5. ボックスの周りを優雅に漂う星屑・光の蛍（Magical Dust Particles）
        _particleMat = CreateTransparentAdditiveMaterial(unlitShader, smokeTex, new Color(1.0f * 2.2f, 0.88f * 2.2f, 0.40f * 2.2f, 0.90f), 3150);

        var dustGo = new GameObject("MagicalDustParticles");
        dustGo.transform.SetParent(transform, false);
        dustGo.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        _magicalDust = dustGo.AddComponent<ParticleSystem>();

        var mainDust = _magicalDust.main;
        mainDust.loop = true;
        mainDust.startLifetime = 2.8f;
        mainDust.startSpeed = 0.32f;
        mainDust.startSize = 0.22f;
        mainDust.startColor = new Color(1.0f * 2.5f, 0.90f * 2.5f, 0.50f * 2.5f, 0.90f);
        mainDust.simulationSpace = ParticleSystemSimulationSpace.World;

        var emissionDust = _magicalDust.emission;
        emissionDust.rateOverTime = 18f;

        var shapeDust = _magicalDust.shape;
        shapeDust.shapeType = ParticleSystemShapeType.Box;
        shapeDust.scale = new Vector3(1.1f, 0.5f, 0.8f);

        var velDust = _magicalDust.velocityOverLifetime;
        velDust.enabled = true;
        velDust.y = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);

        var rendDust = dustGo.GetComponent<ParticleSystemRenderer>();
        if (rendDust != null) rendDust.material = _particleMat;

        // 6. ボックスの上で時折キラリと瞬くダイヤモンドスター（Twinkle Stars）
        var starTex = CreateStarTwinkleTexture();
        _twinkleMat = CreateTransparentAdditiveMaterial(unlitShader, starTex, new Color(1.0f * 3.0f, 0.96f * 3.0f, 0.70f * 3.0f, 0.95f), 3160);

        var starGo = new GameObject("TwinkleStars");
        starGo.transform.SetParent(transform, false);
        starGo.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        _twinkleStars = starGo.AddComponent<ParticleSystem>();

        var mainStar = _twinkleStars.main;
        mainStar.loop = true;
        mainStar.startLifetime = 1.4f;
        mainStar.startSpeed = 0.08f;
        mainStar.startSize = 0.65f;
        mainStar.startColor = new Color(1.0f * 3.0f, 0.98f * 3.0f, 0.75f * 3.0f, 0.95f);
        mainStar.simulationSpace = ParticleSystemSimulationSpace.World;

        var emissionStar = _twinkleStars.emission;
        emissionStar.rateOverTime = 4.5f; // 1秒に4〜5回キラッと瞬く

        var shapeStar = _twinkleStars.shape;
        shapeStar.shapeType = ParticleSystemShapeType.Sphere;
        shapeStar.radius = 0.75f;

        var rendStar = starGo.GetComponent<ParticleSystemRenderer>();
        if (rendStar != null) rendStar.material = _twinkleMat;

        // 7. 遠景ビーコン（二重多層光柱: 高さ約110m、天空へ届く高輝度光柱）
        // 7-A. 高輝度中心コア光柱（超高輝度ホワイトゴールド光芒）
        var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "BeaconPillarCore";
        beacon.transform.SetParent(transform, false);
        beacon.transform.localPosition = new Vector3(0f, 55f, 0f);
        beacon.transform.localScale = new Vector3(0.65f, 55f, 0.65f);
        Destroy(beacon.GetComponent<Collider>());

        var beaconRend = beacon.GetComponent<Renderer>();
        if (beaconRend != null)
        {
            _beaconMat = CreateTransparentAdditiveMaterial(unlitShader, smokeTex, new Color(1.0f * 4.2f, 0.94f * 4.2f, 0.65f * 4.2f, 0.95f), 3146);
            beaconRend.material = _beaconMat;
        }
        _beaconPillar = beacon.transform;

        // 7-B. 外周オーラ光芒柱（遠景・上空からの視認性を劇的に向上させる広域光柱）
        var beaconOuter = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beaconOuter.name = "BeaconPillarCorona";
        beaconOuter.transform.SetParent(transform, false);
        beaconOuter.transform.localPosition = new Vector3(0f, 55f, 0f);
        beaconOuter.transform.localScale = new Vector3(2.2f, 55f, 2.2f);
        Destroy(beaconOuter.GetComponent<Collider>());

        var beaconOuterRend = beaconOuter.GetComponent<Renderer>();
        if (beaconOuterRend != null)
        {
            _beaconOuterMat = CreateTransparentAdditiveMaterial(unlitShader, smokeTex, new Color(1.0f * 2.6f, 0.82f * 2.6f, 0.28f * 2.6f, 0.50f), 3144);
            beaconOuterRend.material = _beaconOuterMat;
        }
        _beaconOuterPillar = beaconOuter.transform;

        // 8. 天に向かって垂直に昇る光の粒子ビーム（高輝度・高速上昇・全高約110m到達）
        var beamGo = new GameObject("VerticalBeamSparkles");
        beamGo.transform.SetParent(transform, false);
        beamGo.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        _verticalBeam = beamGo.AddComponent<ParticleSystem>();

        var mainBeam = _verticalBeam.main;
        mainBeam.loop = true;
        mainBeam.startLifetime = 4.2f;
        mainBeam.startSpeed = 26.0f;
        mainBeam.startSize = 0.75f;
        mainBeam.startColor = new Color(1.0f * 3.6f, 0.90f * 3.6f, 0.45f * 3.6f, 1.0f);
        mainBeam.simulationSpace = ParticleSystemSimulationSpace.World;

        var emissionBeam = _verticalBeam.emission;
        emissionBeam.rateOverTime = 30f;

        var shapeBeam = _verticalBeam.shape;
        shapeBeam.shapeType = ParticleSystemShapeType.Cone;
        shapeBeam.angle = 0.8f;
        shapeBeam.radius = 0.50f;
        shapeBeam.rotation = new Vector3(-90f, 0f, 0f);

        var rendBeam = beamGo.GetComponent<ParticleSystemRenderer>();
        if (rendBeam != null)
        {
            _verticalBeamMat = CreateTransparentAdditiveMaterial(unlitShader, smokeTex, new Color(1.0f * 3.0f, 0.88f * 3.0f, 0.40f * 3.0f, 1.0f), 3155);
            rendBeam.material = _verticalBeamMat;
        }
    }

    #endregion

    /// <summary>
    /// 開封・未開封のビジュアル状態を一元適用
    /// </summary>
    public void ApplyVisualState(bool opened, bool immediate)
    {
        if (opened)
        {
            if (_lid != null)
            {
                if (immediate) _lid.localRotation = Quaternion.Euler(-95f, 0f, 0f);
            }

            SetLampColor(VisualConfig.OpenedEmerald, immediate ? 1.2f : 2.0f);

            if (_pointLight != null)
            {
                _pointLight.color = VisualConfig.OpenedLightColor;
                _pointLight.intensity = VisualConfig.LightIntensityOpened;
                _pointLight.range = VisualConfig.LightRangeOpened;
            }

            if (_lampGlowMat != null)
            {
                _lampGlowMat.SetColor("_BaseColor", new Color(0.25f, 1.0f, 0.65f, immediate ? 0.40f : 0.60f));
            }

            // オーラや星屑のフェードアウト／停止
            if (_magicalDust != null)
            {
                if (immediate) _magicalDust.gameObject.SetActive(false);
                else _magicalDust.Stop();
            }

            if (_twinkleStars != null)
            {
                if (immediate) _twinkleStars.gameObject.SetActive(false);
                else _twinkleStars.Stop();
            }

            if (_seamGlow != null)
            {
                _seamGlow.gameObject.SetActive(false);
            }

            if (_beaconPillar != null)
            {
                if (immediate) _beaconPillar.gameObject.SetActive(false);
            }

            if (_beaconOuterPillar != null)
            {
                if (immediate) _beaconOuterPillar.gameObject.SetActive(false);
            }

            if (_groundGlow != null)
            {
                if (immediate) _groundGlow.gameObject.SetActive(false);
            }

            if (_verticalBeam != null)
            {
                if (immediate) _verticalBeam.gameObject.SetActive(false);
                else _verticalBeam.Stop();
            }

            if (immediate)
            {
                if (_boxBodyAura != null) _boxBodyAura.gameObject.SetActive(false);
            }
            else
            {
                if (_auraFadeCoroutine != null) StopCoroutine(_auraFadeCoroutine);
                _auraFadeCoroutine = StartCoroutine(FadeOutAura());
            }
        }
        else
        {
            if (_auraFadeCoroutine != null)
            {
                StopCoroutine(_auraFadeCoroutine);
                _auraFadeCoroutine = null;
            }

            if (_lid != null)
            {
                _lid.localRotation = Quaternion.identity;
            }

            SetLampColor(VisualConfig.UnopenedGold, 3.5f);

            if (_pointLight != null)
            {
                _pointLight.color = VisualConfig.UnopenedGold;
                _pointLight.intensity = VisualConfig.LightIntensityUnopenedBase;
                _pointLight.range = VisualConfig.LightRangeUnopenedBase;
            }

            if (_boxAuraMat != null) _boxAuraMat.SetColor("_BaseColor", VisualConfig.SoftAuraColor);
            if (_lampGlowMat != null) _lampGlowMat.SetColor("_BaseColor", new Color(1.0f * 3.5f, 0.92f * 3.5f, 0.50f * 3.5f, 0.95f));
            if (_seamMat != null) _seamMat.SetColor("_BaseColor", VisualConfig.SeamLightColor);

            if (_boxBodyAura != null)
            {
                _boxBodyAura.gameObject.SetActive(true);
                _boxBodyAura.localScale = Vector3.one * VisualConfig.BoxAuraBaseScale;
            }
            if (_groundGlow != null)
            {
                _groundGlow.gameObject.SetActive(true);
                _groundGlow.localScale = Vector3.one * 4.0f;
            }
            if (_seamGlow != null)
            {
                _seamGlow.gameObject.SetActive(true);
            }
            if (_lampGlow != null)
            {
                _lampGlow.gameObject.SetActive(true);
            }

            if (_beaconPillar != null)
            {
                _beaconPillar.gameObject.SetActive(true);
                _beaconPillar.localScale = new Vector3(0.65f, 55f, 0.65f);
            }

            if (_beaconOuterPillar != null)
            {
                _beaconOuterPillar.gameObject.SetActive(true);
                _beaconOuterPillar.localScale = new Vector3(2.2f, 55f, 2.2f);
            }

            if (_verticalBeam != null)
            {
                _verticalBeam.gameObject.SetActive(true);
                if (!_verticalBeam.isPlaying) _verticalBeam.Play();
            }

            if (_magicalDust != null)
            {
                _magicalDust.gameObject.SetActive(true);
                if (!_magicalDust.isPlaying) _magicalDust.Play();
            }

            if (_twinkleStars != null)
            {
                _twinkleStars.gameObject.SetActive(true);
                if (!_twinkleStars.isPlaying) _twinkleStars.Play();
            }
        }
    }

    [ContextMenu("Reset Box to Unopened")]
    public void ResetBoxToUnopened()
    {
        PlayerPrefs.DeleteKey("DriftBox_Opened_" + boxId);
        PlayerPrefs.Save();
        isOpened = false;
        ApplyVisualState(false, immediate: true);
        Debug.Log($"[DriftBox] ボックス #{boxId} ({boxTitle}) を未開封状態にリセットしました。");
        var scrapMgr = AdventureScrapManager.Instance;
        if (scrapMgr != null) scrapMgr.CheckPointsAndNotifyLeverUnlock();
    }

    [ContextMenu("Force Open Box")]
    public void ForceOpenBox()
    {
        OpenBox();
    }

    /// <summary>
    /// 全ドリフトボックス（#1〜#5）を未開封状態へ完全リセットし、ポイントとHUDを再同期
    /// </summary>
    public static void ResetAllBoxesStatic(bool showBanner = true)
    {
        for (int i = 1; i <= 5; i++)
        {
            PlayerPrefs.DeleteKey("DriftBox_Opened_" + i);
        }
        PlayerPrefs.Save();

        var boxes = Object.FindObjectsByType<AdventureBeachDriftBox>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in boxes)
        {
            if (b != null)
            {
                b.isOpened = false;
                b.ApplyVisualState(false, immediate: true);
            }
        }

        var scrapMgr = AdventureScrapManager.Instance;
        if (scrapMgr != null)
        {
            scrapMgr.CheckPointsAndNotifyLeverUnlock();
        }

        if (showBanner)
        {
            var hud = AdventureScrapHUD.Instance ?? Object.FindFirstObjectByType<AdventureScrapHUD>();
            if (hud != null)
            {
                int pts = scrapMgr != null ? scrapMgr.TotalProgressPoints : 0;
                hud.ShowUpgradeBanner($"📦 全ドリフトボックスを未開封にリセット！\n✦ 現在の探索ポイント: {pts} / {AdventureScrapManager.RequiredPointsForCanopy} pt");
            }
        }

        Debug.Log("📦 【DriftBox】全5個のドリフトボックスを未開封状態にリセットしました！");
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Adventure/📦 全ドリフトボックスを未開封にリセット (Reset All Drift Boxes)")]
    public static void EditorResetAllBoxes()
    {
        ResetAllBoxesStatic();
    }
#endif

    public void OpenBox()
    {
        if (isOpened) return;
        isOpened = true;
        PlayerPrefs.SetInt("DriftBox_Opened_" + boxId, 1);
        PlayerPrefs.Save();

        // 蓋参照が未キャッシュなら拾い直す（開きっぱなしを保証）
        if (_lid == null) CacheHierarchyRefs();

        // 開封アニメーション開始
        StartCoroutine(AnimateOpen());

        // 効果音再生（澄んだクリスタルチャイム）
        if (_audioSource != null && _openClip != null)
        {
            _audioSource.pitch = 1.0f + Random.Range(-0.02f, 0.02f);
            _audioSource.PlayOneShot(_openClip, 0.95f);
        }

        // 総合探索ポイント（+2 pt）加算とレバーロック解除チェック
        var scrapMgr = AdventureScrapManager.Instance;
        if (scrapMgr != null)
        {
            scrapMgr.OnDriftBoxOpened(boxId, boxTitle);
        }

        // 相棒Rustのセリフと歓喜の宙返り＆星スパークル
        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            drone.TriggerCelebration(rustDialogue, 2.2f);
        }

        // クエストティッカーの更新（+2 pt 獲得と現在ポイント）
        var hud = AdventureScrapHUD.Instance ?? FindAnyObjectByType<AdventureScrapHUD>();
        if (hud != null)
        {
            int pts = scrapMgr != null ? scrapMgr.TotalProgressPoints : 0;
            string objText = !string.IsNullOrEmpty(nextObjective) ? $"\n💡 目標: {nextObjective}" : "";
            hud.ShowUpgradeBanner($"📦 【{boxTitle}】を開封！ (+2 pt)\n✦ 探索ポイント: {pts} / {AdventureScrapManager.RequiredPointsForCanopy} pt{objText}");
        }

        // 情報モーダルUIの表示
        ShowModal(boxTitle, author, message);
    }

    private IEnumerator AnimateOpen()
    {
        // ビジュアルを開封状態へ移行（演出付き）
        ApplyVisualState(opened: true, immediate: false);

        // 開封祝祭パーティクル演出
        if (_particles != null)
        {
            _particles.Play();
        }

        // 蓋がパカッと後方へ95度開く
        if (_lid != null)
        {
            Quaternion startRot = _lid.localRotation;
            Quaternion endRot = startRot * Quaternion.Euler(-95f, 0f, 0f);
            float elapsed = 0f;
            float duration = 0.45f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // バウンスを伴うオープンカーブ
                float curve = 1f + Mathf.Sin((t - 1f) * Mathf.PI * 0.5f) * Mathf.Exp(-t * 3f) * 0.2f;
                _lid.localRotation = Quaternion.Slerp(startRot, endRot, t * curve);
                yield return null;
            }
            _lid.localRotation = endRot;
        }
    }

    private IEnumerator FadeOutAura()
    {
        Vector3 auraStart = _boxBodyAura != null ? _boxBodyAura.localScale : Vector3.zero;
        Vector3 groundStart = _groundGlow != null ? _groundGlow.localScale : Vector3.zero;
        Vector3 pillarStart = _beaconPillar != null ? _beaconPillar.localScale : Vector3.zero;
        Vector3 outerStart = _beaconOuterPillar != null ? _beaconOuterPillar.localScale : Vector3.zero;

        float elapsed = 0f;
        float dur = 0.50f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            if (_boxBodyAura != null) _boxBodyAura.localScale = Vector3.Lerp(auraStart, Vector3.zero, t);
            if (_groundGlow != null) _groundGlow.localScale = Vector3.Lerp(groundStart, Vector3.zero, t);
            if (_beaconPillar != null) _beaconPillar.localScale = Vector3.Lerp(pillarStart, new Vector3(0f, pillarStart.y, 0f), t);
            if (_beaconOuterPillar != null) _beaconOuterPillar.localScale = Vector3.Lerp(outerStart, new Vector3(0f, outerStart.y, 0f), t);
            yield return null;
        }

        if (_boxBodyAura != null) _boxBodyAura.gameObject.SetActive(false);
        if (_groundGlow != null) _groundGlow.gameObject.SetActive(false);
        if (_beaconPillar != null) _beaconPillar.gameObject.SetActive(false);
        if (_beaconOuterPillar != null) _beaconOuterPillar.gameObject.SetActive(false);
    }

    private void SetLampColor(Color c, float intensity)
    {
        if (_lampRenderer == null) return;
        if (_mpb == null)
            _mpb = new MaterialPropertyBlock();
        _lampRenderer.GetPropertyBlock(_mpb);
        _mpb.SetColor("_BaseColor", c);
        _mpb.SetColor("_EmissionColor", c * intensity);
        _lampRenderer.SetPropertyBlock(_mpb);
    }

    // ═══════════════════════════════════════════════════════════════════
    // モーダルウィンドウUIシステム
    // ═══════════════════════════════════════════════════════════════════

    private static void EnsureModalUI()
    {
        if (_modalCanvas != null && _modalPanel != null) return;

        // 孤児化した古いCanvasがあれば掃除
        var oldCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in oldCanvases)
        {
            if (c != null && c.name == "DriftBoxModalCanvas")
            {
                if (Application.isPlaying) Object.Destroy(c.gameObject);
                else Object.DestroyImmediate(c.gameObject);
            }
        }
        _modalCanvas = null;
        _modalPanel = null;

        var canvasGo = new GameObject("DriftBoxModalCanvas");
        DontDestroyOnLoad(canvasGo);
        _modalCanvas = canvasGo.AddComponent<Canvas>();
        _modalCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        _modalCanvas.worldCamera = Camera.main;
        _modalCanvas.planeDistance = 1.0f;
        _modalCanvas.sortingOrder = 200; // 最前面表示

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.AddComponent<DriftBoxModalInputHandler>();

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 背景暗転パネル（画面のどこをクリックしても閉じられる）
        var overlay = new GameObject("Overlay");
        overlay.transform.SetParent(canvasGo.transform, false);
        var overlayImg = overlay.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.50f);
        var overlayRt = overlay.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.sizeDelta = Vector2.zero;
        var overlayBtn = overlay.AddComponent<Button>();
        overlayBtn.transition = Selectable.Transition.None;
        overlayBtn.onClick.AddListener(CloseModal);

        // メインパネル
        _modalPanel = new GameObject("ModalPanel");
        _modalPanel.transform.SetParent(overlay.transform, false);
        var panelImg = _modalPanel.AddComponent<Image>();
        panelImg.color = new Color(0.12f, 0.15f, 0.20f, 0.96f); // 深いネイビーグレー
        var panelRt = _modalPanel.GetComponent<RectTransform>();
        panelRt.sizeDelta = new Vector2(680, 420);
        panelRt.anchoredPosition = Vector2.zero;

        // パネルの外枠（金色の飾り枠）
        AddOutline(_modalPanel, new Color(0.85f, 0.72f, 0.40f, 0.85f), new Vector2(2, -2));

        // アイコン＆タイトル
        _modalTitleText = CreateTextElement(_modalPanel.transform, "TitleText", defaultFont, 26,
            new Color(1.0f, 0.88f, 0.45f), FontStyle.Bold, TextAnchor.MiddleCenter,
            new Vector2(0, 160), new Vector2(600, 50));

        // 差出人／記録者
        _modalAuthorText = CreateTextElement(_modalPanel.transform, "AuthorText", defaultFont, 17,
            new Color(0.65f, 0.75f, 0.85f), FontStyle.Normal, TextAnchor.MiddleCenter,
            new Vector2(0, 120), new Vector2(600, 30));

        // 本文
        _modalBodyText = CreateTextElement(_modalPanel.transform, "BodyText", defaultFont, 20,
            new Color(0.95f, 0.96f, 0.98f), FontStyle.Normal, TextAnchor.UpperLeft,
            new Vector2(0, -10), new Vector2(580, 200), lineSpacing: 1.35f);

        // 閉じるヒントボタン
        CreateCloseButton(_modalPanel.transform, defaultFont);

        overlay.SetActive(false);
    }

    #region UIヘルパーメソッド

    private static Text CreateTextElement(Transform parent, string name, Font font, int fontSize,
        Color color, FontStyle fontStyle, TextAnchor alignment, Vector2 pos, Vector2 size, float lineSpacing = 1.0f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<Text>();
        txt.font = font;
        txt.fontSize = fontSize;
        txt.fontStyle = fontStyle;
        txt.color = color;
        txt.alignment = alignment;
        txt.lineSpacing = lineSpacing;

        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return txt;
    }

    private static void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        var outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
    }

    private static void CreateCloseButton(Transform parent, Font font)
    {
        var closeGo = new GameObject("CloseHintButton");
        closeGo.transform.SetParent(parent, false);
        var closeImg = closeGo.AddComponent<Image>();
        closeImg.color = new Color(0.18f, 0.24f, 0.32f, 0.85f);
        var closeBtn = closeGo.AddComponent<Button>();
        closeBtn.onClick.AddListener(CloseModal);
        var closeColors = closeBtn.colors;
        closeColors.highlightedColor = new Color(0.28f, 0.38f, 0.50f, 1f);
        closeColors.pressedColor = new Color(0.10f, 0.15f, 0.22f, 1f);
        closeBtn.colors = closeColors;

        AddOutline(closeGo, new Color(0.45f, 0.85f, 1.0f, 0.6f), new Vector2(1.5f, -1.5f));

        var closeText = CreateTextElement(closeGo.transform, "Text", font, 17,
            new Color(0.65f, 0.92f, 1.0f), FontStyle.Bold, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.zero);
        closeText.text = "【 Space / Enter / クリックで閉じる 】";
        var closeTextRt = closeText.GetComponent<RectTransform>();
        closeTextRt.anchorMin = Vector2.zero;
        closeTextRt.anchorMax = Vector2.one;
        closeTextRt.sizeDelta = Vector2.zero;

        var closeRt = closeGo.GetComponent<RectTransform>();
        closeRt.anchoredPosition = new Vector2(0, -165);
        closeRt.sizeDelta = new Vector2(440, 42);
    }

    #endregion

    public static void ShowModal(string title, string author, string body)
    {
        // ナラティブモーダルやキーストーンボード（手動の自由）が表示中の場合は重複表示を防止
        if (AdventureBeachNarrativeManager.Instance != null && AdventureBeachNarrativeManager.Instance.IsShowingModal)
            return;
        if (AdventurePrologueDrama.Instance != null && AdventurePrologueDrama.Instance.IsShowingDashBoard)
            return;

        EnsureModalUI();
        if (_modalCanvas == null || _modalPanel == null) return;
        if (_modalCanvas.worldCamera == null || !_modalCanvas.worldCamera.isActiveAndEnabled)
        {
            _modalCanvas.worldCamera = Camera.main;
        }

        _modalTitleText.text = "📦 " + title;
        _modalAuthorText.text = "── " + author + " ──";
        _modalBodyText.text = body;

        _modalPanel.transform.parent.gameObject.SetActive(true);
        _isModalOpen = true;
        _modalOpenTimestamp = Time.unscaledTime;

        // マウスカーソルを解放してクリックできるようにする
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void CloseModal()
    {
        if (!CanCloseModal && _isModalOpen) return;

        if (_modalCanvas != null && _modalPanel != null)
        {
            _modalPanel.transform.parent.gameObject.SetActive(false);
        }
        _isModalOpen = false;

        // ゲームプレイ用にマウスカーソルを再度ロック
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 心地よい開錠サウンドのプロシージャル合成（澄んだステレオクリスタルアルペジオ）
    // ═══════════════════════════════════════════════════════════════════
    private static AudioClip CreateChimeSound()
    {
        int rate = 44100;
        float duration = 2.4f;
        int samplesPerChannel = (int)(rate * duration);
        int totalSamples = samplesPerChannel * 2; // 2チャンネル（ステレオ）
        float[] interleavedData = new float[totalSamples];

        // 澄んだ高音クリスタルベル（C6, E6, G6, B6, E7）が煌びやかに立ち上がるアルペジオ
        float[] freqs = { 1046.50f, 1318.51f, 1567.98f, 1975.53f, 2637.02f };
        float[] starts = { 0.00f, 0.045f, 0.095f, 0.150f, 0.210f };
        // ステレオパンニング（左から右へと煌めきが駆け抜ける）
        float[] pans = { -0.35f, -0.15f, 0.05f, 0.25f, 0.45f };

        for (int note = 0; note < freqs.Length; note++)
        {
            float f0 = freqs[note];
            float startSec = starts[note];
            int startSample = (int)(startSec * rate);
            float pan = pans[note];
            float gainL = Mathf.Cos((pan + 1f) * 0.25f * Mathf.PI);
            float gainR = Mathf.Sin((pan + 1f) * 0.25f * Mathf.PI);

            for (int i = 0; i < samplesPerChannel - startSample; i++)
            {
                int sampleIdx = startSample + i;
                if (sampleIdx >= samplesPerChannel) break;

                float t = (float)i / rate;
                // クリスタルベルエンベロープ（3msソフトアタック、自然な指数減衰）
                float attack = (t < 0.003f) ? (t / 0.003f) : 1.0f;
                float decay = Mathf.Exp(-t * 3.2f);
                float env = attack * decay;

                // 基本波 + オクターブ倍音 + 金属的倍音（クリスタルの響き）
                float s1 = Mathf.Sin(2f * Mathf.PI * f0 * t);
                float s2 = Mathf.Sin(2f * Mathf.PI * (f0 * 2.002f) * t) * 0.35f;
                float s3 = Mathf.Sin(2f * Mathf.PI * (f0 * 3.010f) * t) * 0.12f;
                float sBell = Mathf.Sin(2f * Mathf.PI * (f0 * 2.76f) * t) * 0.08f * Mathf.Exp(-t * 6.0f);

                float val = (s1 + s2 + s3 + sBell) * env * 0.28f;

                interleavedData[sampleIdx * 2] += val * gainL;
                interleavedData[sampleIdx * 2 + 1] += val * gainR;
            }
        }

        var clip = AudioClip.Create("DriftBoxChime", samplesPerChannel, 2, rate, false);
        clip.SetData(interleavedData, 0);
        return clip;
    }
}

/// <summary>
/// 漂着ボックスモーダルの入力ハンドラー
/// 新旧Input Systemの両方で、キー入力やクリックによるモーダル閉じを100%確実に処理する
/// </summary>
public class DriftBoxModalInputHandler : MonoBehaviour
{
    void Update()
    {
        if (!AdventureBeachDriftBox.CanCloseModal) return;

        bool closeTriggered = false;

        // 1. 新Input System
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.spaceKey.wasPressedThisFrame ||
                kb.enterKey.wasPressedThisFrame ||
                kb.numpadEnterKey.wasPressedThisFrame ||
                kb.escapeKey.wasPressedThisFrame ||
                kb.eKey.wasPressedThisFrame)
            {
                closeTriggered = true;
            }
        }

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            closeTriggered = true;
        }

        // 2. 旧Input System（フォールバック）
        try
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E) ||
                Input.GetMouseButtonDown(0))
            {
                closeTriggered = true;
            }
        }
        catch { }

        if (closeTriggered)
        {
            AdventureBeachDriftBox.CloseModal();
        }
    }
}
