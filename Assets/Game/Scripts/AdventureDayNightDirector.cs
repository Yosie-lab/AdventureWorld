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

    void Update()
    {
        if (_mainSunLight == null)
        {
            FindMainSun();
            if (_mainSunLight == null) return;
        }

        // 進行度（0.0 〜 1.0）
        var mgr = AdventureScrapManager.Instance;
        int count = mgr != null ? mgr.CollectedCount : 0;
        float progress = Mathf.Clamp01((float)count / 12f);

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

        // 進行度に応じたライティング補間
        // 0.0〜0.35: 昼 (Day)
        // 0.35〜0.75: 夕暮れ (Sunset / Golden Hour)
        // 0.75〜1.0: 神秘の薄明・トワイライト星空 (Twilight / Night)

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
            targetSunColor = Color.Lerp(new Color(1f, 0.85f, 0.55f), new Color(1f, 0.48f, 0.22f), t); // 鮮やかな茜色・夕陽
            targetAmbient = Color.Lerp(new Color(0.65f, 0.55f, 0.65f), new Color(0.48f, 0.35f, 0.55f), t);
            targetFog = Color.Lerp(new Color(0.85f, 0.65f, 0.55f), new Color(0.75f, 0.42f, 0.45f), t);
            targetIntensity = Mathf.Lerp(1.15f, 0.92f, t);
            targetSunAngles = Vector3.Lerp(new Vector3(32f, -15f, 0f), new Vector3(12f, 10f, 0f), t); // 太陽が地平線近くへ沈む
        }
        else
        {
            // 【黄昏〜夜空】深い藍色・星空と神殿の古代光
            float t = (progress - 0.75f) / 0.25f;
            targetSunColor = Color.Lerp(new Color(1f, 0.42f, 0.25f), new Color(0.38f, 0.55f, 0.85f), t); // 月光・星明かりの青白い光
            targetAmbient = Color.Lerp(new Color(0.42f, 0.32f, 0.52f), new Color(0.18f, 0.22f, 0.38f), t);
            targetFog = Color.Lerp(new Color(0.62f, 0.35f, 0.48f), new Color(0.12f, 0.16f, 0.28f), t);
            targetIntensity = Mathf.Lerp(0.88f, 0.65f, t);
            targetSunAngles = Vector3.Lerp(new Vector3(12f, 10f, 0f), new Vector3(-8f, 35f, 0f), t);
        }

        // スムーズな徐変（フレームごとの穏やかなブレンド）
        float dt = Time.deltaTime * 1.5f;
        _mainSunLight.color = Color.Lerp(_mainSunLight.color, targetSunColor, dt);
        _mainSunLight.intensity = Mathf.Lerp(_mainSunLight.intensity, targetIntensity, dt);
        _mainSunLight.transform.rotation = Quaternion.Slerp(_mainSunLight.transform.rotation, Quaternion.Euler(targetSunAngles), dt);

        RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, targetAmbient, dt);
        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFog, dt);
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
