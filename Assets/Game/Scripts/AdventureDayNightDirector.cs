using UnityEngine;

/// <summary>
/// 探索の進行度（スクラップ回収数 0〜12個）に連動して、
/// 昼（青空）→ 黄金の夕暮れ（茜色のサンセット）→ 神秘的な夜空へとドラマチックに変化する時間推移ディレクター。
/// </summary>
public class AdventureDayNightDirector : MonoBehaviour
{
    public static AdventureDayNightDirector Instance { get; private set; }

    Light _mainSunLight;
    Quaternion _origSunRot;
    Color _origSunColor;
    float _origSunIntensity;

    Color _origAmbientColor;
    Color _origFogColor;

    public static void Ensure()
    {
        if (Instance != null) return;
        var existing = FindAnyObjectByType<AdventureDayNightDirector>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject("AdventureDayNightDirector");
        if (Application.isPlaying) DontDestroyOnLoad(go);
        Instance = go.AddComponent<AdventureDayNightDirector>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        FindMainSun();
        _origAmbientColor = RenderSettings.ambientLight;
        _origFogColor = RenderSettings.fogColor;
    }

    void FindMainSun()
    {
        var lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
        foreach (var l in lights)
        {
            if (l.type == LightType.Directional)
            {
                _mainSunLight = l;
                _origSunRot = l.transform.rotation;
                _origSunColor = l.color;
                _origSunIntensity = l.intensity;
                break;
            }
        }
    }

    public enum DayNightMode
    {
        ProgressBased, // スクラップ探索進捗連動
        AlwaysDay,     // 爽快な昼の青空
        AlwaysSunset,  // 黄金と茜色の夕暮れマジックアワー
        AlwaysNight,   // 満天の星空と夜光虫の幻想夜
        RealtimeCycle  // リアルタイム時間サイクル（約8分で1日）
    }

    public static DayNightMode CurrentMode { get; private set; } = DayNightMode.ProgressBased;

    /// <summary>夜の深さ（0.0=昼、1.0=完全な夜空）。星空や夜光虫、ランタンが連動。</summary>
    public static float NightFactor { get; private set; } = 0f;

    /// <summary>夕暮れマジックアワーの深さ（0.0=昼/夜、1.0=最も鮮烈な夕焼け）。</summary>
    public static float SunsetFactor { get; private set; } = 0f;

    private float _realtimeCyclePhase = 0.15f; // 0.0〜1.0 (0.15=朝昼, 0.55=夕暮れ, 0.85=深夜)

    void Start()
    {
        // 満天の星空ドーム＆夜光虫・ホタル生成の初期化
        AdventureStarrySky.Ensure();
        AdventureFireflies.Ensure();
        EnsureAtmosphericSunMotes();
    }

    void Update()
    {
        if (_mainSunLight == null)
        {
            FindMainSun();
            if (_mainSunLight == null) return;
        }

        // 【Tキー】で時間帯モードをトグル切り替え（情緒的な鑑賞用）
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.tKey.wasPressedThisFrame)
        {
            ToggleDayNightMode();
        }

        // 天蓋破壊後：危機中は凍える外気、注油後／エピローグは夜明けへ
        if (AdventureSanctuaryTowerManager.IsCanopyBroken)
        {
            var tower = AdventureSanctuaryTowerManager.Instance;
            bool freezing = tower != null
                            && tower.ClimaxCrisisStarted
                            && !tower.ClimaxOilInjected
                            && !tower.EpilogueTriggered;
            if (freezing)
                ApplyFreezingOuterSky();
            else
                ApplyDawnSky();
            return;
        }

        // 時間進行度（0.0 〜 1.0）の決定
        float progress = CalculateCurrentProgress();

        // NightFactor & SunsetFactor の計算
        if (progress < 0.35f)
        {
            NightFactor = 0f;
            SunsetFactor = 0f;
        }
        else if (progress < 0.70f)
        {
            float t = (progress - 0.35f) / 0.35f;
            SunsetFactor = Mathf.Sin(t * Mathf.PI); // 夕暮れ中央で1.0
            NightFactor = Mathf.Max(0f, (progress - 0.55f) / 0.15f * 0.3f);
        }
        else
        {
            SunsetFactor = Mathf.Max(0f, 1f - (progress - 0.70f) / 0.12f);
            NightFactor = Mathf.Clamp01((progress - 0.65f) / 0.25f);
        }

        // 進行度に応じたライティング補間
        Color targetSunColor;
        Color targetAmbient;
        Color targetFog;
        float targetIntensity;
        Vector3 targetSunAngles;

        if (progress < 0.35f)
        {
            // 【昼】爽やかな青空と澄んだピュアな太陽光
            float t = progress / 0.35f;
            targetSunColor = Color.Lerp(new Color(1f, 1f, 0.98f), new Color(1f, 0.98f, 0.94f), t);
            targetAmbient = Color.Lerp(new Color(0.58f, 0.70f, 0.85f), new Color(0.62f, 0.68f, 0.78f), t);
            targetFog = Color.Lerp(new Color(0.70f, 0.88f, 1.0f), new Color(0.75f, 0.85f, 0.98f), t);
            targetIntensity = Mathf.Lerp(1.35f, 1.25f, t);
            targetSunAngles = Vector3.Lerp(new Vector3(50f, -30f, 0f), new Vector3(32f, -15f, 0f), t);
        }
        else if (progress < 0.75f)
        {
            // 【夕暮れ】息をのむ赤寄りの鮮烈なオレンジ・茜色のマジックアワー（少し暗めで情緒的な深み）
            float t = (progress - 0.35f) / 0.40f;
            targetSunColor = Color.Lerp(new Color(1.0f, 0.58f, 0.16f), new Color(1.0f, 0.24f, 0.02f), t); // 鮮烈な赤寄りのオレンジ（茜色・深紅のサンセット夕陽）
            targetAmbient = Color.Lerp(new Color(0.58f, 0.35f, 0.24f), new Color(0.48f, 0.18f, 0.09f), t); // 眩しさを抑えた落ち着いた茜色の環境反射
            targetFog = Color.Lerp(new Color(0.84f, 0.40f, 0.18f), new Color(0.76f, 0.20f, 0.06f), t); // 落ち着いた深い赤橙色の夕焼けフォグ
            targetIntensity = Mathf.Lerp(1.12f, 0.95f, t); // 少し暗めで夕陽と光芒が最も美しく映える光量
            targetSunAngles = Vector3.Lerp(new Vector3(42f, 225f, 0f), new Vector3(35.2f, 237.5f, 0f), t); // スカイボックスの太陽と一致
        }
        else
        {
            // 【黄昏〜満天の星空】深い藍色・星明かりと夜光虫
            float t = (progress - 0.75f) / 0.25f;
            targetSunColor = Color.Lerp(new Color(1.0f, 0.24f, 0.02f), new Color(0.32f, 0.50f, 0.85f), t); // 赤橙の残光から月光へ
            targetAmbient = Color.Lerp(new Color(0.48f, 0.18f, 0.09f), new Color(0.12f, 0.16f, 0.30f), t);
            targetFog = Color.Lerp(new Color(0.76f, 0.20f, 0.06f), new Color(0.08f, 0.12f, 0.24f), t);
            targetIntensity = Mathf.Lerp(0.95f, 0.40f, t);
            targetSunAngles = Vector3.Lerp(new Vector3(35.2f, 237.5f, 0f), new Vector3(-25f, 45f, 0f), t); // 太陽は沈み、反対側から月光
        }

        // スムーズな徐変（フレームごとの穏やかなブレンド）
        float blendSpeed = (CurrentMode == DayNightMode.RealtimeCycle) ? 0.8f : 2.5f;
        float dt = Time.deltaTime * blendSpeed;
        _mainSunLight.color = Color.Lerp(_mainSunLight.color, targetSunColor, dt);
        _mainSunLight.intensity = Mathf.Lerp(_mainSunLight.intensity, targetIntensity, dt);
        _mainSunLight.transform.rotation = Quaternion.Slerp(_mainSunLight.transform.rotation, Quaternion.Euler(targetSunAngles), dt);

        RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, targetAmbient, dt);
        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFog, dt);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, Mathf.Lerp(0.0016f, 0.0028f, SunsetFactor), dt);

        // スカイボックスの夕暮れ赤橙ブレンド（茶褐色の地面反射・大気厚みを鮮明化）
        if (RenderSettings.skybox != null)
        {
            if (RenderSettings.skybox.HasProperty("_GroundColor"))
            {
                Color targetSkyGround = Color.Lerp(new Color(0.37f, 0.35f, 0.34f), new Color(0.55f, 0.24f, 0.14f), SunsetFactor);
                RenderSettings.skybox.SetColor("_GroundColor", Color.Lerp(RenderSettings.skybox.GetColor("_GroundColor"), targetSkyGround, dt));
            }
            if (RenderSettings.skybox.HasProperty("_AtmosphereThickness"))
            {
                float targetAtmosphere = Mathf.Lerp(1.0f, 1.28f, SunsetFactor);
                RenderSettings.skybox.SetFloat("_AtmosphereThickness", Mathf.Lerp(RenderSettings.skybox.GetFloat("_AtmosphereThickness"), targetAtmosphere, dt));
            }
            if (RenderSettings.skybox.HasProperty("_SunColor"))
            {
                RenderSettings.skybox.SetColor("_SunColor", Color.Lerp(RenderSettings.skybox.GetColor("_SunColor"), targetSunColor, dt));
            }
        }

        // 夕暮れの太陽キラキラ度・Bloom・海面反射の連動
        UpdateBloomGlitter(SunsetFactor, dt);
        UpdateOceanSunGlitter(SunsetFactor, dt);
        UpdateSunsetSunSparkleVisual(targetSunColor, SunsetFactor);
    }

    /// <summary>エディタ非再生時でも夕暮れキラキラ太陽とライティングを即座に反映するプレビューヘルパー</summary>
    public void ForceUpdateSunsetPreview()
    {
        FindMainSun();
        if (_mainSunLight == null) return;

        Color sunCol = new Color(1.0f, 0.24f, 0.02f);
        _mainSunLight.color = sunCol;
        _mainSunLight.intensity = 1.05f;
        _mainSunLight.transform.rotation = Quaternion.LookRotation(-SkyboxSunDir, Vector3.up);

        RenderSettings.ambientLight = new Color(0.52f, 0.24f, 0.14f);
        RenderSettings.fogColor = new Color(0.80f, 0.26f, 0.10f);
        RenderSettings.fogDensity = 0.0022f;

        SunsetFactor = 1.0f;
        UpdateBloomGlitter(1.0f, 1.0f);
        UpdateOceanSunGlitter(1.0f, 1.0f);
        UpdateSunsetSunSparkleVisual(sunCol, 1.0f);
    }

    float CalculateCurrentProgress()
    {
        switch (CurrentMode)
        {
            case DayNightMode.AlwaysDay:
                return 0.10f; // 爽快な昼
            case DayNightMode.AlwaysSunset:
                return 0.58f; // 黄金と茜色の夕暮れマジックアワー
            case DayNightMode.AlwaysNight:
                return 0.95f; // 満天の星空と夜光虫
            case DayNightMode.RealtimeCycle:
                // 約8分（480秒）で1周期
                _realtimeCyclePhase = Mathf.Repeat(_realtimeCyclePhase + (Time.deltaTime / 480f), 1f);
                return _realtimeCyclePhase;
            case DayNightMode.ProgressBased:
            default:
                var mgr = AdventureScrapManager.Instance;
                int count = mgr != null ? mgr.CollectedCount : 0;
                return Mathf.Clamp01((float)count / 12f);
        }
    }

    void ToggleDayNightMode()
    {
        switch (CurrentMode)
        {
            case DayNightMode.ProgressBased:
                CurrentMode = DayNightMode.AlwaysDay;
                AdventureNotificationToast.Show("時間帯: 【昼】青空と輝く海 ☀️", 2.2f);
                break;
            case DayNightMode.AlwaysDay:
                CurrentMode = DayNightMode.AlwaysSunset;
                AdventureNotificationToast.Show("時間帯: 【夕暮れ】茜色のマジックアワー 🌅", 2.2f);
                break;
            case DayNightMode.AlwaysSunset:
                CurrentMode = DayNightMode.AlwaysNight;
                AdventureNotificationToast.Show("時間帯: 【夜】満天の星空と波打ち際の夜光虫 🌌", 2.2f);
                break;
            case DayNightMode.AlwaysNight:
                CurrentMode = DayNightMode.RealtimeCycle;
                AdventureNotificationToast.Show("時間帯: 【移ろい】自然な時間サイクル（約8分/日） ⏳", 2.2f);
                break;
            case DayNightMode.RealtimeCycle:
                CurrentMode = DayNightMode.ProgressBased;
                AdventureNotificationToast.Show("時間帯: 【探索連動】スクラップ探索進行に同期 🧭", 2.2f);
                break;
        }
    }

    void ApplyFreezingOuterSky()
    {
        // 箱庭の外：凍える本物の風と蒼白い光
        float dt = Time.deltaTime * 1.4f;
        Color coldSun = new Color(0.78f, 0.88f, 1f);
        Color coldAmbient = new Color(0.50f, 0.62f, 0.78f);
        Color coldFog = new Color(0.60f, 0.72f, 0.86f);

        _mainSunLight.color = Color.Lerp(_mainSunLight.color, coldSun, dt);
        _mainSunLight.intensity = Mathf.Lerp(_mainSunLight.intensity, 1.15f, dt);
        _mainSunLight.transform.rotation = Quaternion.Slerp(
            _mainSunLight.transform.rotation, Quaternion.Euler(28f, -55f, 0f), dt);

        RenderSettings.fog = true;
        RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, coldAmbient, dt);
        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, coldFog, dt);
        RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, 0.0045f, dt);
    }

    void ApplyDawnSky()
    {
        // 天蓋崩壊時：未知の世界の神々しい夜明け・澄み渡る朝陽
        float dt = Time.deltaTime * 1.2f;
        Color dawnSun = new Color(1f, 0.99f, 0.95f);
        Color dawnAmbient = new Color(0.68f, 0.78f, 0.92f);
        Color dawnFog = new Color(0.72f, 0.88f, 0.98f);

        _mainSunLight.color = Color.Lerp(_mainSunLight.color, dawnSun, dt);
        _mainSunLight.intensity = Mathf.Lerp(_mainSunLight.intensity, 1.45f, dt);
        _mainSunLight.transform.rotation = Quaternion.Slerp(_mainSunLight.transform.rotation, Quaternion.Euler(42f, -40f, 0f), dt);

        RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, dawnAmbient, dt);
        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, dawnFog, dt);
        RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, 0.0012f, dt);
    }

    // 映画的大気光粒子（Atmospheric Sun Motes）：カメラ前の茶色い不定形モヤ・破片に見えるため完全無効化
    ParticleSystem _sunMotesPs;

    void EnsureAtmosphericSunMotes()
    {
        var existing = GameObject.Find("AtmosphericSunMotes");
        if (existing != null)
        {
            Destroy(existing);
        }
        _sunMotesPs = null;
    }

    // ── 夕暮れの太陽キラキラ度・Bloom・海面反射の演出 ──
    UnityEngine.Rendering.Universal.Bloom _bloomComp;
    bool _searchedBloom;

    void UpdateBloomGlitter(float sunset, float dt)
    {
        if (!_searchedBloom || _bloomComp == null)
        {
            _searchedBloom = true;
            var vol = GameObject.Find("PostProcessing")?.GetComponent<UnityEngine.Rendering.Volume>();
            if (vol != null && vol.profile != null)
            {
                vol.profile.TryGet(out _bloomComp);
            }
        }
        if (_bloomComp != null)
        {
            float targetIntensity = Mathf.Lerp(0.55f, 1.45f, sunset);
            float targetThreshold = Mathf.Lerp(0.88f, 0.78f, sunset);
            Color targetTint = Color.Lerp(Color.white, new Color(1.0f, 0.68f, 0.40f), sunset);

            _bloomComp.intensity.Override(Mathf.Lerp(_bloomComp.intensity.value, targetIntensity, dt * 2.0f));
            _bloomComp.threshold.Override(Mathf.Lerp(_bloomComp.threshold.value, targetThreshold, dt * 2.0f));
            _bloomComp.tint.Override(Color.Lerp(_bloomComp.tint.value, targetTint, dt * 2.0f));
        }
    }

    Material _oceanMat;
    bool _searchedOcean;

    void UpdateOceanSunGlitter(float sunset, float dt)
    {
        if (!_searchedOcean || _oceanMat == null)
        {
            _searchedOcean = true;
            var oceanPlane = GameObject.Find("OceanPlane");
            if (oceanPlane != null)
            {
                var mr = oceanPlane.GetComponent<MeshRenderer>();
                if (mr != null) _oceanMat = mr.sharedMaterial;
            }
        }
        if (_oceanMat != null)
        {
            Color targetGlitterCol = Color.Lerp(new Color(1.0f, 0.98f, 0.88f), new Color(1.0f, 0.46f, 0.12f), sunset);
            float targetGlitterInt = Mathf.Lerp(5.2f, 11.0f, sunset);

            if (_oceanMat.HasProperty("_SunGlitterColor"))
            {
                Color curCol = _oceanMat.GetColor("_SunGlitterColor");
                _oceanMat.SetColor("_SunGlitterColor", Color.Lerp(curCol, targetGlitterCol, dt * 2.0f));
            }
            if (_oceanMat.HasProperty("_SunGlitterIntensity"))
            {
                float curInt = _oceanMat.GetFloat("_SunGlitterIntensity");
                _oceanMat.SetFloat("_SunGlitterIntensity", Mathf.Lerp(curInt, targetGlitterInt, dt * 2.0f));
            }
        }
    }

    GameObject _sunsetSunRoot;
    Transform _sunCoreT;
    Transform _sunCoronaT;
    Material _sunCoreMat;
    Material _sunCoronaMat;
    Texture2D _sunCoreTex;
    Texture2D _sunCoronaTex;

    static readonly Vector3 SkyboxSunDir = new Vector3(0.690f, 0.575f, 0.440f).normalized;

    void EnsureSunsetSunVisual()
    {
        if (_sunsetSunRoot != null) return;
        _sunsetSunRoot = new GameObject("SunsetSparkleSun_Visual");
        _sunsetSunRoot.transform.SetParent(transform, false);

        var shader = Shader.Find("Mobile/Particles/Additive")
                     ?? Shader.Find("Legacy Shaders/Particles/Additive")
                     ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");

        // 1. 円形コアテクスチャ生成（黒フェード加算）
        if (_sunCoreTex == null)
        {
            const int res = 128;
            _sunCoreTex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            _sunCoreTex.wrapMode = TextureWrapMode.Clamp;
            Vector2 center = new Vector2((res - 1) * 0.5f, (res - 1) * 0.5f);
            float maxR = (res - 1) * 0.5f;
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxR;
                    float a = Mathf.Clamp01(1f - d);
                    a = Mathf.SmoothStep(0f, 1f, a);
                    a = Mathf.Pow(a, 1.6f);
                    _sunCoreTex.SetPixel(x, y, new Color(a, a, a, a));
                }
            }
            _sunCoreTex.Apply();
        }

        // 2. スターバースト放射状コロナテクスチャ生成（12条のシャープな光芒・黒フェード加算）
        if (_sunCoronaTex == null)
        {
            const int res = 256;
            _sunCoronaTex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            _sunCoronaTex.wrapMode = TextureWrapMode.Clamp;
            Vector2 center = new Vector2((res - 1) * 0.5f, (res - 1) * 0.5f);
            float maxR = (res - 1) * 0.5f;
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    Vector2 diff = new Vector2(x, y) - center;
                    float d = diff.magnitude / maxR;
                    float angle = Mathf.Atan2(diff.y, diff.x);
                    
                    float cross = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 2f)), 32f) * 2.5f;
                    float subCross = Mathf.Pow(Mathf.Abs(Mathf.Sin(angle * 2f)), 24f) * 1.5f;
                    float fineRays = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 6f)), 10f) * 0.8f;
                    float halo = Mathf.Clamp01(1f - d);
                    halo = Mathf.Pow(halo, 1.4f);
                    
                    float a = (cross + subCross + fineRays + halo * 0.6f) * Mathf.Clamp01(1f - d * d);
                    a = Mathf.Clamp01(a);
                    _sunCoronaTex.SetPixel(x, y, new Color(a, a, a, a));
                }
            }
            _sunCoronaTex.Apply();
        }

        // 太陽コア Quad
        var coreGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        coreGo.name = "SunCore";
        coreGo.transform.SetParent(_sunsetSunRoot.transform, false);
        coreGo.transform.localScale = Vector3.one * 36f;
        Destroy(coreGo.GetComponent<Collider>());
        _sunCoreT = coreGo.transform;

        _sunCoreMat = new Material(shader);
        _sunCoreMat.name = "SunCore_Mat";
        _sunCoreMat.mainTexture = _sunCoreTex;
        coreGo.GetComponent<MeshRenderer>().material = _sunCoreMat;

        // 太陽キラキラ放射状コロナ Quad
        var coronaGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        coronaGo.name = "SunCorona";
        coronaGo.transform.SetParent(_sunsetSunRoot.transform, false);
        coronaGo.transform.localScale = Vector3.one * 125f;
        Destroy(coronaGo.GetComponent<Collider>());
        _sunCoronaT = coronaGo.transform;

        _sunCoronaMat = new Material(shader);
        _sunCoronaMat.name = "SunCorona_Mat";
        _sunCoronaMat.mainTexture = _sunCoronaTex;
        coronaGo.GetComponent<MeshRenderer>().material = _sunCoronaMat;
    }

    void UpdateSunsetSunSparkleVisual(Color sunColor, float sunset)
    {
        if (sunset < 0.02f)
        {
            if (_sunsetSunRoot != null && _sunsetSunRoot.activeSelf)
                _sunsetSunRoot.SetActive(false);
            return;
        }

        EnsureSunsetSunVisual();
        if (_sunsetSunRoot == null) return;
        if (!_sunsetSunRoot.activeSelf) _sunsetSunRoot.SetActive(true);

        var cam = Camera.main;
        if (cam == null) cam = FindAnyObjectByType<Camera>();
        if (cam == null) return;

        // 太陽の位置：スカイボックスの太陽方位へ遠方（380m）にカメラ追従で配置
        Vector3 sunWorldPos = cam.transform.position + SkyboxSunDir * 380f;
        _sunsetSunRoot.transform.position = sunWorldPos;
        _sunsetSunRoot.transform.rotation = Quaternion.LookRotation(-SkyboxSunDir, Vector3.up);

        // キラキラ明滅シマーリングパルス（2.8Hzの繊細なきらめき）
        float shimmer = 0.86f + 0.14f * Mathf.Sin(Time.time * 2.8f) + 0.06f * Mathf.Cos(Time.time * 5.6f);
        float coronaRot = Time.time * 1.8f;

        // コア：鮮烈な赤寄りオレンジ（茜色）
        Color coreCol = new Color(1.0f, 0.24f, 0.02f, 1.0f) * (sunset * shimmer);
        if (_sunCoreMat.HasProperty("_TintColor")) _sunCoreMat.SetColor("_TintColor", coreCol);
        else _sunCoreMat.color = coreCol;
        _sunCoreT.localScale = Vector3.one * (36f * (0.96f + 0.06f * Mathf.Sin(Time.time * 2.1f)));

        // コロナ：外側に広がる茜色・黄金のキラキラ光輪
        Color coronaCol = new Color(1.0f, 0.42f, 0.08f, 0.95f) * (sunset * shimmer);
        if (_sunCoronaMat.HasProperty("_TintColor")) _sunCoronaMat.SetColor("_TintColor", coronaCol);
        else _sunCoronaMat.color = coronaCol;
        _sunCoronaT.localScale = Vector3.one * (125f * shimmer);
        _sunCoronaT.localRotation = Quaternion.Euler(0f, 0f, coronaRot);
    }
}
