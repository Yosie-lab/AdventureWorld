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
        DontDestroyOnLoad(go);
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
            // 【昼】爽やかな青空と眩しい太陽光
            float t = progress / 0.35f;
            targetSunColor = Color.Lerp(new Color(1f, 0.98f, 0.92f), new Color(1f, 0.92f, 0.78f), t);
            targetAmbient = Color.Lerp(new Color(0.55f, 0.65f, 0.78f), new Color(0.62f, 0.60f, 0.70f), t);
            targetFog = Color.Lerp(new Color(0.68f, 0.82f, 0.95f), new Color(0.75f, 0.74f, 0.85f), t);
            targetIntensity = Mathf.Lerp(1.25f, 1.15f, t);
            targetSunAngles = Vector3.Lerp(new Vector3(50f, -30f, 0f), new Vector3(32f, -15f, 0f), t);
        }
        else if (progress < 0.75f)
        {
            // 【夕暮れ】息をのむ茜色・黄金のマジックアワー
            float t = (progress - 0.35f) / 0.40f;
            targetSunColor = Color.Lerp(new Color(1f, 0.85f, 0.55f), new Color(1f, 0.45f, 0.20f), t); // 鮮やかな茜色・夕陽
            targetAmbient = Color.Lerp(new Color(0.65f, 0.55f, 0.65f), new Color(0.48f, 0.32f, 0.50f), t);
            targetFog = Color.Lerp(new Color(0.88f, 0.62f, 0.50f), new Color(0.78f, 0.38f, 0.42f), t);
            targetIntensity = Mathf.Lerp(1.15f, 0.85f, t);
            targetSunAngles = Vector3.Lerp(new Vector3(32f, -15f, 0f), new Vector3(8f, 15f, 0f), t); // 太陽が地平線近くへ沈む
        }
        else
        {
            // 【黄昏〜満天の星空】深い藍色・星明かりと夜光虫
            float t = (progress - 0.75f) / 0.25f;
            targetSunColor = Color.Lerp(new Color(1f, 0.42f, 0.25f), new Color(0.32f, 0.50f, 0.85f), t); // 月光・星明かりの青白い光
            targetAmbient = Color.Lerp(new Color(0.40f, 0.30f, 0.48f), new Color(0.12f, 0.16f, 0.30f), t);
            targetFog = Color.Lerp(new Color(0.58f, 0.32f, 0.45f), new Color(0.08f, 0.12f, 0.24f), t);
            targetIntensity = Mathf.Lerp(0.85f, 0.45f, t);
            targetSunAngles = Vector3.Lerp(new Vector3(8f, 15f, 0f), new Vector3(-25f, 45f, 0f), t); // 太陽は沈み、反対側から月光
        }

        // スムーズな徐変（フレームごとの穏やかなブレンド）
        float blendSpeed = (CurrentMode == DayNightMode.RealtimeCycle) ? 0.8f : 2.5f;
        float dt = Time.deltaTime * blendSpeed;
        _mainSunLight.color = Color.Lerp(_mainSunLight.color, targetSunColor, dt);
        _mainSunLight.intensity = Mathf.Lerp(_mainSunLight.intensity, targetIntensity, dt);
        _mainSunLight.transform.rotation = Quaternion.Slerp(_mainSunLight.transform.rotation, Quaternion.Euler(targetSunAngles), dt);

        RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, targetAmbient, dt);
        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFog, dt);
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
        // 天蓋崩壊時：未知の世界の神々しい夜明け・黄金の朝陽
        float dt = Time.deltaTime * 1.2f;
        Color dawnSun = new Color(1f, 0.95f, 0.82f);
        Color dawnAmbient = new Color(0.62f, 0.68f, 0.82f);
        Color dawnFog = new Color(0.88f, 0.82f, 0.72f);

        _mainSunLight.color = Color.Lerp(_mainSunLight.color, dawnSun, dt);
        _mainSunLight.intensity = Mathf.Lerp(_mainSunLight.intensity, 1.45f, dt);
        _mainSunLight.transform.rotation = Quaternion.Slerp(_mainSunLight.transform.rotation, Quaternion.Euler(42f, -40f, 0f), dt);

        RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, dawnAmbient, dt);
        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, dawnFog, dt);
        RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, 0.0022f, dt);
    }
}
