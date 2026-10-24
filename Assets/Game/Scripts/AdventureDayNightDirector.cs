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
        AlwaysDawn,    // 清冽な夜明け・朝焼けのブルー＆ゴールドアワー
        AlwaysDay,     // 爽快な昼の青空と南国の陽光
        AlwaysSunset,  // 息をのむ茜色とロイヤルパープルの夕暮れマジックアワー
        AlwaysTwilight,// 宵闇の黄昏・街灯が灯る薄暮
        AlwaysNight,   // 満天の星空と蒼白い月光の幻想夜
        RealtimeCycle  // リアルタイム時間サイクル（約8分で24時間循環）
    }

    public static DayNightMode CurrentMode { get; private set; } = DayNightMode.ProgressBased;

    /// <summary>F9 はパーツ満了で夜に飛ぶ。崩壊シーケンスの開始だけ昼に戻す。</summary>
    public static void PinDaytimeForSequenceStart()
    {
        CurrentMode = DayNightMode.AlwaysDay;
        NightFactor = 0f;
        SunsetFactor = 0f;
        DawnFactor = 0f;
        var director = FindAnyObjectByType<AdventureDayNightDirector>();
        if (director != null)
            director.ForceUpdateDayPreview();
    }

    /// <summary>夜の深さ（0.0=昼、1.0=完全な夜空）。星空や夜光虫、ランタンが連動。</summary>
    public static float NightFactor { get; private set; } = 0f;

    /// <summary>夕暮れマジックアワーの深さ（0.0=昼/夜、1.0=最も鮮烈な夕焼け）。</summary>
    public static float SunsetFactor { get; private set; } = 0f;

    /// <summary>夜明け・朝の深さ（0.0=昼/夜、1.0=最も澄んだ朝焼け）。</summary>
    public static float DawnFactor { get; private set; } = 0f;

    private float _realtimeCyclePhase = 0.25f; // 0.0〜1.0 (0.08=朝焼け, 0.30=昼, 0.58=夕暮れ, 0.78=黄昏, 0.90=月夜)

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

        // ── 各時間帯因子の計算（シームレスな山型ブレンド） ──
        // 1. 夕暮れ因子 (0.42 〜 0.72 で山型、0.58で最大1.0)
        if (progress >= 0.42f && progress <= 0.72f)
        {
            float st = (progress - 0.42f) / 0.30f;
            SunsetFactor = Mathf.Sin(st * Mathf.PI);
        }
        else
        {
            SunsetFactor = 0f;
        }

        // 2. 夜因子 (0.74 〜 1.00、および 0.00 〜 0.06 で最大1.0)
        if (progress >= 0.74f)
        {
            float nt = Mathf.Clamp01((progress - 0.74f) / 0.12f);
            NightFactor = Mathf.SmoothStep(0f, 1f, nt);
        }
        else if (progress <= 0.06f)
        {
            float nt = Mathf.Clamp01(1f - (progress / 0.06f));
            NightFactor = Mathf.SmoothStep(0f, 1f, nt);
        }
        else
        {
            NightFactor = 0f;
        }

        // 3. 夜明け因子 (0.02 〜 0.18 で山型、0.09で最大1.0)
        if (progress >= 0.02f && progress <= 0.18f)
        {
            float dawnT = (progress - 0.02f) / 0.16f;
            DawnFactor = Mathf.Sin(dawnT * Mathf.PI);
        }
        else
        {
            DawnFactor = 0f;
        }

        // ── 進行度に応じたシネマティック24時間ライティング補間 ──
        Color targetSunColor;
        Color targetAmbient;
        Color targetFog;
        float targetIntensity;
        Vector3 targetSunAngles;
        float targetFogDensity;

        if (progress < 0.08f)
        {
            // 【未明〜ブルーアワー】(0.00 〜 0.08) 静寂の蒼白い月夜から地平線が淡く紫に染まる
            float t = Mathf.SmoothStep(0f, 1f, progress / 0.08f);
            targetSunColor = Color.Lerp(new Color(0.42f, 0.60f, 0.95f), new Color(0.92f, 0.58f, 0.48f), t);
            targetAmbient = Color.Lerp(new Color(0.14f, 0.18f, 0.32f), new Color(0.38f, 0.32f, 0.45f), t);
            targetFog = Color.Lerp(new Color(0.08f, 0.12f, 0.24f), new Color(0.55f, 0.38f, 0.52f), t);
            targetIntensity = Mathf.Lerp(0.42f, 0.78f, t);
            targetSunAngles = Vector3.Lerp(new Vector3(-25f, 45f, 0f), new Vector3(8f, 75f, 0f), t);
            targetFogDensity = Mathf.Lerp(0.0016f, 0.0018f, t);
        }
        else if (progress < 0.18f)
        {
            // 【朝焼け〜清冽な朝陽】(0.08 〜 0.18) 東の海から朝陽が昇り、爽やかな朝露と光が島を満たす
            float t = Mathf.SmoothStep(0f, 1f, (progress - 0.08f) / 0.10f);
            targetSunColor = Color.Lerp(new Color(0.92f, 0.58f, 0.48f), new Color(1.0f, 0.94f, 0.86f), t);
            targetAmbient = Color.Lerp(new Color(0.38f, 0.32f, 0.45f), new Color(0.65f, 0.72f, 0.82f), t);
            targetFog = Color.Lerp(new Color(0.55f, 0.38f, 0.52f), new Color(0.72f, 0.86f, 1.0f), t);
            targetIntensity = Mathf.Lerp(0.78f, 1.30f, t);
            targetSunAngles = Vector3.Lerp(new Vector3(8f, 75f, 0f), new Vector3(32f, 110f, 0f), t);
            targetFogDensity = Mathf.Lerp(0.0018f, 0.0015f, t);
        }
        else if (progress < 0.45f)
        {
            // 【昼・南国の陽光】(0.18 〜 0.45) 澄み渡る深い青空、白い雲、輝くエメラルドの海
            float t = Mathf.SmoothStep(0f, 1f, (progress - 0.18f) / 0.27f);
            targetSunColor = Color.Lerp(new Color(1.0f, 0.94f, 0.86f), new Color(1.0f, 0.99f, 0.96f), t);
            targetAmbient = Color.Lerp(new Color(0.65f, 0.72f, 0.82f), new Color(0.72f, 0.78f, 0.86f), t);
            targetFog = Color.Lerp(new Color(0.72f, 0.86f, 1.0f), new Color(0.75f, 0.88f, 1.0f), t);
            targetIntensity = Mathf.Lerp(1.30f, 1.35f, t);
            targetSunAngles = Vector3.Lerp(new Vector3(32f, 110f, 0f), new Vector3(45f, 195f, 0f), t);
            targetFogDensity = 0.0014f;
        }
        else if (progress < 0.65f)
        {
            // 【夕暮れ・息をのむ茜色マジックアワー】(0.45 〜 0.65) オレンジ色を適度に削った上品で澄んだ夕暮れ
            float t = Mathf.SmoothStep(0f, 1f, (progress - 0.45f) / 0.20f);
            targetSunColor = Color.Lerp(new Color(1.0f, 0.92f, 0.84f), new Color(0.95f, 0.68f, 0.52f), t); // オレンジの過剰な赤みを削り上品なピーチアンバーへ
            targetAmbient = Color.Lerp(new Color(0.68f, 0.68f, 0.74f), new Color(0.50f, 0.48f, 0.56f), t); // 影や地面のオレンジかぶりを完全にカットし涼やかなトーンへ
            targetFog = Color.Lerp(new Color(0.75f, 0.72f, 0.78f), new Color(0.74f, 0.56f, 0.52f), t); // 水平線・遠景フォグの強いオレンジをソフトに抑制
            targetIntensity = Mathf.Lerp(1.30f, 1.15f, t); // 直射日光の強さを適度に和らげ地面の赤飛びを抑える
            targetSunAngles = Vector3.Lerp(new Vector3(45f, 195f, 0f), new Vector3(35.2f, 237.5f, 0f), t); // スカイボックス太陽と一致
            targetFogDensity = Mathf.Lerp(0.0014f, 0.0018f, t);
        }
        else if (progress < 0.78f)
        {
            // 【黄昏・薄暮の静寂】(0.65 〜 0.78) 西の海に夕陽が沈み、地平線の残光と天頂の深い藍色
            float t = Mathf.SmoothStep(0f, 1f, (progress - 0.65f) / 0.13f);
            targetSunColor = Color.Lerp(new Color(0.95f, 0.68f, 0.52f), new Color(0.65f, 0.32f, 0.36f), t);
            targetAmbient = Color.Lerp(new Color(0.50f, 0.48f, 0.56f), new Color(0.28f, 0.22f, 0.38f), t);
            targetFog = Color.Lerp(new Color(0.74f, 0.56f, 0.52f), new Color(0.38f, 0.20f, 0.32f), t);
            targetIntensity = Mathf.Lerp(1.15f, 0.65f, t);
            targetSunAngles = Vector3.Lerp(new Vector3(35.2f, 237.5f, 0f), new Vector3(8f, 255f, 0f), t);
            targetFogDensity = Mathf.Lerp(0.0019f, 0.0022f, t);
        }
        else
        {
            // 【満天の星空・蒼白い月夜】(0.78 〜 1.00) 太陽は沈み、反対側からクールで神秘的な月光が海を照らす
            float t = Mathf.SmoothStep(0f, 1f, (progress - 0.78f) / 0.22f);
            targetSunColor = Color.Lerp(new Color(0.65f, 0.25f, 0.22f), new Color(0.42f, 0.60f, 0.95f), t);
            targetAmbient = Color.Lerp(new Color(0.28f, 0.22f, 0.38f), new Color(0.14f, 0.18f, 0.32f), t);
            targetFog = Color.Lerp(new Color(0.38f, 0.20f, 0.32f), new Color(0.08f, 0.12f, 0.24f), t);
            targetIntensity = Mathf.Lerp(0.65f, 0.42f, t);
            targetSunAngles = Vector3.Lerp(new Vector3(8f, 255f, 0f), new Vector3(-25f, 45f, 0f), t);
            targetFogDensity = Mathf.Lerp(0.0022f, 0.0016f, t);
        }

        // スムーズな徐変（フレームごとの穏やかなブレンド）
        float blendSpeed = (CurrentMode == DayNightMode.RealtimeCycle) ? 0.8f : 2.5f;
        float dt = Time.deltaTime * blendSpeed;
        _mainSunLight.color = Color.Lerp(_mainSunLight.color, targetSunColor, dt);
        _mainSunLight.intensity = Mathf.Lerp(_mainSunLight.intensity, targetIntensity, dt);
        _mainSunLight.transform.rotation = Quaternion.Slerp(_mainSunLight.transform.rotation, Quaternion.Euler(targetSunAngles), dt);

        RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, targetAmbient, dt);
        RenderSettings.ambientSkyColor = RenderSettings.ambientLight;
        RenderSettings.ambientEquatorColor = RenderSettings.ambientLight;
        RenderSettings.ambientGroundColor = RenderSettings.ambientLight;

        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFog, dt);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, targetFogDensity, dt);

        // スカイボックスの夕暮れブレンド（大気厚みを濁らせず澄んだ発色を維持）
        if (RenderSettings.skybox != null)
        {
            if (RenderSettings.skybox.HasProperty("_GroundColor"))
            {
                Color origGround = new Color(0.369f, 0.349f, 0.341f);
                RenderSettings.skybox.SetColor("_GroundColor", origGround);
            }
            if (RenderSettings.skybox.HasProperty("_AtmosphereThickness"))
            {
                RenderSettings.skybox.SetFloat("_AtmosphereThickness", 1.0f);
            }
            if (RenderSettings.skybox.HasProperty("_Tint"))
            {
                Color origTint = new Color(0.60377f, 0.60377f, 0.60377f, 0.447f);
                RenderSettings.skybox.SetColor("_Tint", origTint);
            }
            if (RenderSettings.skybox.HasProperty("_Exposure"))
            {
                float targetExposure = Mathf.Lerp(1.0f, 0.45f, NightFactor);
                RenderSettings.skybox.SetFloat("_Exposure", Mathf.Lerp(RenderSettings.skybox.GetFloat("_Exposure"), targetExposure, dt));
            }
            if (RenderSettings.skybox.HasProperty("_SunColor"))
            {
                Color sunVisCol = new Color(1.0f, 0.46f, 0.20f);
                RenderSettings.skybox.SetColor("_SunColor", Color.Lerp(RenderSettings.skybox.GetColor("_SunColor"), sunVisCol, dt));
            }
        }

        // 夕暮れ・昼・夜に応じた太陽キラキラ度・Bloom・海面反射の連動
        UpdateBloomGlitter(SunsetFactor, NightFactor, dt);
        UpdateOceanSunGlitter(SunsetFactor, NightFactor, dt);
        UpdateSunsetSunSparkleVisual(new Color(1.0f, 0.46f, 0.20f), SunsetFactor);
    }

    /// <summary>指定した時間帯のシネマティックライティングを即座に反映するプレビューヘルパー</summary>
    public void ForceUpdateProgressPreview(float progress)
    {
        FindMainSun();
        if (_mainSunLight == null) return;

        // 夕暮れ・夜因子の計算
        float sunset = 0f;
        if (progress >= 0.42f && progress <= 0.72f)
        {
            float st = (progress - 0.42f) / 0.30f;
            sunset = Mathf.Sin(st * Mathf.PI);
        }
        SunsetFactor = sunset;

        float night = 0f;
        if (progress >= 0.74f)
        {
            night = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.74f) / 0.12f));
        }
        else if (progress <= 0.06f)
        {
            night = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - (progress / 0.06f)));
        }
        NightFactor = night;

        Color sunCol;
        Color amb;
        Color fog;
        float intensity;
        Vector3 angles;
        float fogDensity;

        if (progress < 0.08f)
        {
            float t = Mathf.SmoothStep(0f, 1f, progress / 0.08f);
            sunCol = Color.Lerp(new Color(0.42f, 0.60f, 0.95f), new Color(0.92f, 0.58f, 0.48f), t);
            amb = Color.Lerp(new Color(0.14f, 0.18f, 0.32f), new Color(0.38f, 0.32f, 0.45f), t);
            fog = Color.Lerp(new Color(0.08f, 0.12f, 0.24f), new Color(0.55f, 0.38f, 0.52f), t);
            intensity = Mathf.Lerp(0.42f, 0.78f, t);
            angles = Vector3.Lerp(new Vector3(-25f, 45f, 0f), new Vector3(8f, 75f, 0f), t);
            fogDensity = Mathf.Lerp(0.0016f, 0.0018f, t);
        }
        else if (progress < 0.18f)
        {
            float t = Mathf.SmoothStep(0f, 1f, (progress - 0.08f) / 0.10f);
            sunCol = Color.Lerp(new Color(0.92f, 0.58f, 0.48f), new Color(1.0f, 0.94f, 0.86f), t);
            amb = Color.Lerp(new Color(0.38f, 0.32f, 0.45f), new Color(0.65f, 0.72f, 0.82f), t);
            fog = Color.Lerp(new Color(0.55f, 0.38f, 0.52f), new Color(0.72f, 0.86f, 1.0f), t);
            intensity = Mathf.Lerp(0.78f, 1.30f, t);
            angles = Vector3.Lerp(new Vector3(8f, 75f, 0f), new Vector3(32f, 110f, 0f), t);
            fogDensity = Mathf.Lerp(0.0018f, 0.0015f, t);
        }
        else if (progress < 0.45f)
        {
            float t = Mathf.SmoothStep(0f, 1f, (progress - 0.18f) / 0.27f);
            sunCol = Color.Lerp(new Color(1.0f, 0.94f, 0.86f), new Color(1.0f, 0.99f, 0.96f), t);
            amb = Color.Lerp(new Color(0.65f, 0.72f, 0.82f), new Color(0.72f, 0.78f, 0.86f), t);
            fog = Color.Lerp(new Color(0.72f, 0.86f, 1.0f), new Color(0.75f, 0.88f, 1.0f), t);
            intensity = Mathf.Lerp(1.30f, 1.35f, t);
            angles = Vector3.Lerp(new Vector3(32f, 110f, 0f), new Vector3(45f, 195f, 0f), t);
            fogDensity = 0.0014f;
        }
        else if (progress < 0.65f)
        {
            float t = Mathf.SmoothStep(0f, 1f, (progress - 0.45f) / 0.20f);
            sunCol = Color.Lerp(new Color(1.0f, 0.92f, 0.84f), new Color(0.95f, 0.68f, 0.52f), t);
            amb = Color.Lerp(new Color(0.68f, 0.68f, 0.74f), new Color(0.50f, 0.48f, 0.56f), t);
            fog = Color.Lerp(new Color(0.75f, 0.72f, 0.78f), new Color(0.74f, 0.56f, 0.52f), t);
            intensity = Mathf.Lerp(1.30f, 1.15f, t);
            angles = Vector3.Lerp(new Vector3(45f, 195f, 0f), new Vector3(35.2f, 237.5f, 0f), t);
            fogDensity = Mathf.Lerp(0.0014f, 0.0018f, t);
        }
        else if (progress < 0.78f)
        {
            float t = Mathf.SmoothStep(0f, 1f, (progress - 0.65f) / 0.13f);
            sunCol = Color.Lerp(new Color(0.95f, 0.68f, 0.52f), new Color(0.65f, 0.32f, 0.36f), t);
            amb = Color.Lerp(new Color(0.50f, 0.48f, 0.56f), new Color(0.28f, 0.22f, 0.38f), t);
            fog = Color.Lerp(new Color(0.74f, 0.56f, 0.52f), new Color(0.38f, 0.20f, 0.32f), t);
            intensity = Mathf.Lerp(1.15f, 0.65f, t);
            angles = Vector3.Lerp(new Vector3(35.2f, 237.5f, 0f), new Vector3(8f, 255f, 0f), t);
            fogDensity = Mathf.Lerp(0.0019f, 0.0022f, t);
        }
        else
        {
            float t = Mathf.SmoothStep(0f, 1f, (progress - 0.78f) / 0.22f);
            sunCol = Color.Lerp(new Color(0.65f, 0.25f, 0.22f), new Color(0.42f, 0.60f, 0.95f), t);
            amb = Color.Lerp(new Color(0.28f, 0.22f, 0.38f), new Color(0.14f, 0.18f, 0.32f), t);
            fog = Color.Lerp(new Color(0.38f, 0.20f, 0.32f), new Color(0.08f, 0.12f, 0.24f), t);
            intensity = Mathf.Lerp(0.65f, 0.42f, t);
            angles = Vector3.Lerp(new Vector3(8f, 255f, 0f), new Vector3(-25f, 45f, 0f), t);
            fogDensity = Mathf.Lerp(0.0022f, 0.0016f, t);
        }

        _mainSunLight.color = sunCol;
        _mainSunLight.intensity = intensity;
        _mainSunLight.transform.rotation = Quaternion.Euler(angles);

        RenderSettings.ambientLight = amb;
        RenderSettings.ambientSkyColor = amb;
        RenderSettings.ambientEquatorColor = amb;
        RenderSettings.ambientGroundColor = amb;

        RenderSettings.fogColor = fog;
        RenderSettings.fogDensity = fogDensity;

        if (RenderSettings.skybox != null)
        {
            if (RenderSettings.skybox.HasProperty("_GroundColor"))
                RenderSettings.skybox.SetColor("_GroundColor", new Color(0.369f, 0.349f, 0.341f));
            if (RenderSettings.skybox.HasProperty("_AtmosphereThickness"))
                RenderSettings.skybox.SetFloat("_AtmosphereThickness", 1.0f);
            if (RenderSettings.skybox.HasProperty("_Tint"))
                RenderSettings.skybox.SetColor("_Tint", new Color(0.60377f, 0.60377f, 0.60377f, 0.447f));
            if (RenderSettings.skybox.HasProperty("_Exposure"))
                RenderSettings.skybox.SetFloat("_Exposure", Mathf.Lerp(1.0f, 0.45f, night));
        }

        UpdateBloomGlitter(sunset, night, 1.0f);
        UpdateOceanSunGlitter(sunset, night, 1.0f);
        UpdateSunsetSunSparkleVisual(new Color(1.0f, 0.46f, 0.20f), sunset);
    }

    /// <summary>エディタ非再生時でも夕暮れキラキラ太陽とライティングを即座に反映するプレビューヘルパー</summary>
    public void ForceUpdateSunsetPreview()
    {
        ForceUpdateProgressPreview(0.58f);
    }

    public void ForceUpdateDawnPreview()
    {
        ForceUpdateProgressPreview(0.10f);
    }

    public void ForceUpdateDayPreview()
    {
        ForceUpdateProgressPreview(0.30f);
    }

    public void ForceUpdateTwilightPreview()
    {
        ForceUpdateProgressPreview(0.72f);
    }

    public void ForceUpdateNightPreview()
    {
        ForceUpdateProgressPreview(0.90f);
    }

    float CalculateCurrentProgress()
    {
        switch (CurrentMode)
        {
            case DayNightMode.AlwaysDawn:
                return 0.10f; // 清冽な朝焼け・夜明け
            case DayNightMode.AlwaysDay:
                return 0.30f; // 爽快な昼の南国陽光
            case DayNightMode.AlwaysSunset:
                return 0.58f; // 息をのむ茜色とロイヤルパープルの夕暮れ
            case DayNightMode.AlwaysTwilight:
                return 0.72f; // 宵闇の黄昏・薄暮
            case DayNightMode.AlwaysNight:
                return 0.90f; // 満天の星空と蒼白い月光
            case DayNightMode.RealtimeCycle:
                // 約8分（480秒）で1日（24時間）がドラマチックに移り変わる
                _realtimeCyclePhase = Mathf.Repeat(_realtimeCyclePhase + (Time.deltaTime / 480f), 1f);
                return _realtimeCyclePhase;
            case DayNightMode.ProgressBased:
            default:
                var mgr = AdventureScrapManager.Instance;
                int count = mgr != null ? mgr.CollectedCount : 0;
                // スクラップ 0〜4: 昼(0.30) ➔ 5〜8: 夕暮れ(0.58) ➔ 9〜11: 黄昏(0.74) ➔ 12: 月夜(0.90)
                if (count <= 4)
                    return Mathf.Lerp(0.20f, 0.38f, count / 4f);
                else if (count <= 8)
                    return Mathf.Lerp(0.45f, 0.65f, (count - 4) / 4f);
                else
                    return Mathf.Lerp(0.68f, 0.90f, (count - 8) / 4f);
        }
    }

    void ToggleDayNightMode()
    {
        switch (CurrentMode)
        {
            case DayNightMode.ProgressBased:
                CurrentMode = DayNightMode.AlwaysDawn;
                AdventureNotificationToast.Show("時間帯: 【夜明け】清冽な朝露と朝焼け 🌅", 2.2f);
                break;
            case DayNightMode.AlwaysDawn:
                CurrentMode = DayNightMode.AlwaysDay;
                AdventureNotificationToast.Show("時間帯: 【昼】青空と輝くエメラルドの海 ☀️", 2.2f);
                break;
            case DayNightMode.AlwaysDay:
                CurrentMode = DayNightMode.AlwaysSunset;
                AdventureNotificationToast.Show("時間帯: 【夕暮れ】茜色のシネマティックサンセット 🌇", 2.2f);
                break;
            case DayNightMode.AlwaysSunset:
                CurrentMode = DayNightMode.AlwaysTwilight;
                AdventureNotificationToast.Show("時間帯: 【黄昏】夕暮れ残光と街灯が灯る薄暮 🌆", 2.2f);
                break;
            case DayNightMode.AlwaysTwilight:
                CurrentMode = DayNightMode.AlwaysNight;
                AdventureNotificationToast.Show("時間帯: 【夜】満天の星空と波打ち際の夜光虫 🌌", 2.2f);
                break;
            case DayNightMode.AlwaysNight:
                CurrentMode = DayNightMode.RealtimeCycle;
                AdventureNotificationToast.Show("時間帯: 【移ろい】自然な24時間サイクル（約8分/日） ⏳", 2.2f);
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

    void UpdateBloomGlitter(float sunset, float night, float dt)
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
            // 昼=0.55f, 夕暮れ=1.45f, 夜=0.38f
            float baseIntensity = Mathf.Lerp(0.55f, 0.38f, night);
            float targetIntensity = Mathf.Lerp(baseIntensity, 1.45f, sunset);

            float baseThreshold = Mathf.Lerp(0.88f, 0.92f, night);
            float targetThreshold = Mathf.Lerp(baseThreshold, 0.78f, sunset);

            Color baseTint = Color.Lerp(Color.white, new Color(0.75f, 0.85f, 1.0f), night);
            Color targetTint = Color.Lerp(baseTint, new Color(1.0f, 0.78f, 0.58f), sunset);

            _bloomComp.intensity.Override(Mathf.Lerp(_bloomComp.intensity.value, targetIntensity, dt * 2.0f));
            _bloomComp.threshold.Override(Mathf.Lerp(_bloomComp.threshold.value, targetThreshold, dt * 2.0f));
            _bloomComp.tint.Override(Color.Lerp(_bloomComp.tint.value, targetTint, dt * 2.0f));
        }
    }

    Material _oceanMat;
    bool _searchedOcean;

    void UpdateOceanSunGlitter(float sunset, float night, float dt)
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
            // 昼=エメラルド白, 夕暮れ=琥珀ゴールド, 夜=月光シルバー
            Color dayCol = new Color(1.0f, 0.98f, 0.88f);
            Color nightCol = new Color(0.45f, 0.65f, 1.0f);
            Color baseCol = Color.Lerp(dayCol, nightCol, night);
            Color targetGlitterCol = Color.Lerp(baseCol, new Color(1.0f, 0.62f, 0.30f), sunset);

            float baseInt = Mathf.Lerp(5.2f, 3.2f, night);
            float targetGlitterInt = Mathf.Lerp(baseInt, 11.0f, sunset);
            if (AdventureCurvedHorizonOcean.LiberationSparkleBoostActive)
                targetGlitterInt *= 1.85f;

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

        // 2. ソフト円形ハローテクスチャ生成（放射光条・トゲを削った自然で柔らかな円形ソフトグロー）
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
                    float halo = Mathf.Clamp01(1f - d);
                    halo = Mathf.SmoothStep(0f, 1f, halo);
                    float a = Mathf.Pow(halo, 1.8f);
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

        // 太陽ソフトハロー Quad（放射状光条をなくした丸い光輪）
        var coronaGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        coronaGo.name = "SunCorona";
        coronaGo.transform.SetParent(_sunsetSunRoot.transform, false);
        coronaGo.transform.localScale = Vector3.one * 72f;
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

        // キラキラ明滅シマーリングパルス（穏やかなきらめき）
        float shimmer = 0.90f + 0.10f * Mathf.Sin(Time.time * 2.4f) + 0.04f * Mathf.Cos(Time.time * 4.8f);

        // コア：上品な茜アンバー
        Color coreCol = new Color(1.0f, 0.42f, 0.18f, 1.0f) * (sunset * shimmer);
        if (_sunCoreMat.HasProperty("_TintColor")) _sunCoreMat.SetColor("_TintColor", coreCol);
        else _sunCoreMat.color = coreCol;
        _sunCoreT.localScale = Vector3.one * (36f * (0.97f + 0.04f * Mathf.Sin(Time.time * 2.1f)));

        // ハロー：周囲を柔らかく包む丸い光輪（オレンジ赤みを抑えた温かい琥珀色）
        Color coronaCol = new Color(1.0f, 0.54f, 0.24f, 0.85f) * (sunset * shimmer);
        if (_sunCoronaMat.HasProperty("_TintColor")) _sunCoronaMat.SetColor("_TintColor", coronaCol);
        else _sunCoronaMat.color = coronaCol;
        _sunCoronaT.localScale = Vector3.one * (72f * (0.97f + 0.03f * Mathf.Sin(Time.time * 2.8f)));
        _sunCoronaT.localRotation = Quaternion.identity;
    }
}
