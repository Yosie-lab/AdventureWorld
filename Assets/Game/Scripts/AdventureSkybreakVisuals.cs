using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 天蓋突破の外の世界パノラマ・光芒・フラッシュ・極寒霧。
/// 進行は AdventureSanctuaryTowerManager、絵だけをここに分離する。
/// </summary>
public static class AdventureSkybreakVisuals
{
    static bool _coldAtmosphereActive;
    static bool _savedFogEnabled;
    static Color _savedFogColor;
    static float _savedFogDensity;
    static Color _savedAmbient;
    static Material _savedSkybox;
    static Material _runtimeSkyMat;
    static Light _cachedSun;

    public static void DestroyNamed(string objectName)
    {
        var all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null || t.gameObject == null) continue;
            if (t.name != objectName) continue;
            if (!t.gameObject.scene.IsValid()) continue;
            Object.DestroyImmediate(t.gameObject);
        }
    }

    public static void SpawnWildernessPanorama(bool coldCrisis)
    {
        DestroyNamed("WildernessPanorama");
        DestroyNamed("SkybreakColdMist");

        var panoramaGo = new GameObject("WildernessPanorama");
        panoramaGo.transform.position = new Vector3(512f, 90f, 512f);

        var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var unlit = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Sprites/Default")
                    ?? Shader.Find("Unlit/Color");

        if (coldCrisis)
        {
            // 限界中：以前の冷たい荒野パノラマ（スカイボックスは触らない）
            SpawnColdCrisisWilderness(panoramaGo.transform, lit, unlit);
            SpawnSkybreakColdMist(panoramaGo.transform);
        }
        else
        {
            // 突破後：豊かな緑の大地＋海＋澄んだ青空
            SpawnOuterSkyScene(panoramaGo.transform, coldCrisis: false);
            ApplySkyboxForSkybreak(coldCrisis: false);
            SpawnLushLiberationWorld(panoramaGo.transform, lit, unlit);
            SpawnLiberationDust(panoramaGo.transform);
        }
    }

    /// <summary>Rust限界中：凍える稜線・冷たい地平・雲海（以前の空の感じ）</summary>
    static void SpawnColdCrisisWilderness(Transform parent, Shader lit, Shader unlit)
    {
        var mountainMat = new Material(lit);
        mountainMat.color = new Color(0.12f, 0.16f, 0.26f);

        var iceMat = new Material(lit);
        iceMat.color = new Color(0.82f, 0.90f, 0.98f);

        var horizon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        horizon.name = "HorizonGlow";
        horizon.transform.SetParent(parent, false);
        horizon.transform.localPosition = new Vector3(0f, -8f, 0f);
        horizon.transform.localScale = new Vector3(1600f, 1.6f, 1600f);
        Object.Destroy(horizon.GetComponent<Collider>());
        var horizonMat = new Material(unlit);
        horizonMat.color = new Color(0.55f, 0.68f, 0.88f, 0.55f);
        var hr = horizon.GetComponent<Renderer>();
        if (hr != null) hr.material = horizonMat;

        for (int i = 0; i < 14; i++)
        {
            float ang = i * (360f / 14f) * Mathf.Deg2Rad;
            float dist = 620f + (i % 3) * 40f;
            float peakH = Random.Range(70f, 130f);
            Vector3 pos = new Vector3(Mathf.Cos(ang) * dist, Random.Range(-5f, 25f), Mathf.Sin(ang) * dist);

            var peak = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            peak.name = $"WildernessRidge_{i}";
            peak.transform.SetParent(parent, false);
            peak.transform.localPosition = pos;
            peak.transform.localScale = new Vector3(220f + (i % 4) * 30f, peakH, 180f + (i % 3) * 25f);
            Object.Destroy(peak.GetComponent<Collider>());
            var rend = peak.GetComponent<Renderer>();
            if (rend != null) rend.material = mountainMat;

            if ((i % 2) == 0)
            {
                var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cap.name = $"IceCrown_{i}";
                cap.transform.SetParent(peak.transform, false);
                cap.transform.localPosition = new Vector3(0f, 0.42f, 0f);
                cap.transform.localScale = new Vector3(0.45f, 0.18f, 0.45f);
                Object.Destroy(cap.GetComponent<Collider>());
                var capRend = cap.GetComponent<Renderer>();
                if (capRend != null) capRend.material = iceMat;
            }
        }

        var cloudMat = new Material(unlit);
        cloudMat.color = new Color(0.78f, 0.86f, 0.95f, 0.45f);
        for (int c = 0; c < 18; c++)
        {
            float ang = c * 20f * Mathf.Deg2Rad + 0.15f;
            float dist = 280f + (c % 5) * 55f;
            var cloud = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cloud.name = $"CloudSea_{c}";
            cloud.transform.SetParent(parent, false);
            cloud.transform.localPosition = new Vector3(
                Mathf.Cos(ang) * dist,
                Random.Range(25f, 55f),
                Mathf.Sin(ang) * dist);
            float s = Random.Range(48f, 95f);
            cloud.transform.localScale = new Vector3(s * 1.6f, s * 0.35f, s * 1.2f);
            Object.Destroy(cloud.GetComponent<Collider>());
            var cr = cloud.GetComponent<Renderer>();
            if (cr != null) cr.material = cloudMat;
        }

        var raysGo = new GameObject("SkybreakGodRays");
        raysGo.transform.SetParent(parent, false);
        raysGo.transform.localPosition = new Vector3(0f, 50f, 0f);
        var warmRayMat = new Material(unlit);
        warmRayMat.color = new Color(1.0f, 0.90f, 0.55f, 0.14f);
        var coldRayMat = new Material(unlit);
        coldRayMat.color = new Color(0.70f, 0.88f, 1.0f, 0.16f);
        for (int r = 0; r < 10; r++)
        {
            var ray = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ray.name = $"GodRay_{r}";
            ray.transform.SetParent(raysGo.transform, false);
            ray.transform.localScale = new Vector3(3.2f, 140f, 3.2f);
            ray.transform.localRotation = Quaternion.Euler(Random.Range(12f, 38f), r * 36f + 8f, 0f);
            Object.Destroy(ray.GetComponent<Collider>());
            var rend = ray.GetComponent<Renderer>();
            if (rend != null)
                rend.material = (r % 2 == 0) ? coldRayMat : warmRayMat;
        }
    }

    /// <summary>突破後：緑豊かな大地・森・海・澄んだ空</summary>
    static void SpawnLushLiberationWorld(Transform parent, Shader lit, Shader unlit)
    {
        // 海面（低空の広い水面）
        var seaMat = new Material(lit);
        seaMat.color = new Color(0.12f, 0.42f, 0.72f);
        if (seaMat.HasProperty("_Smoothness")) seaMat.SetFloat("_Smoothness", 0.85f);
        var sea = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        sea.name = "LiberationSea";
        sea.transform.SetParent(parent, false);
        sea.transform.localPosition = new Vector3(0f, -28f, 0f);
        sea.transform.localScale = new Vector3(2200f, 0.6f, 2200f);
        Object.Destroy(sea.GetComponent<Collider>());
        var seaR = sea.GetComponent<Renderer>();
        if (seaR != null) seaR.material = seaMat;

        // 浅い沿岸の帯（ターコイズ）
        var shoreMat = new Material(unlit);
        shoreMat.color = new Color(0.35f, 0.78f, 0.82f, 0.55f);
        var shore = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shore.name = "LiberationShoreGlow";
        shore.transform.SetParent(parent, false);
        shore.transform.localPosition = new Vector3(0f, -24f, 0f);
        shore.transform.localScale = new Vector3(980f, 0.9f, 980f);
        Object.Destroy(shore.GetComponent<Collider>());
        var shoreR = shore.GetComponent<Renderer>();
        if (shoreR != null) shoreR.material = shoreMat;

        // 地平の明るい草原の霞
        var horizonMat = new Material(unlit);
        horizonMat.color = new Color(0.78f, 0.92f, 0.55f, 0.48f);
        var horizon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        horizon.name = "HorizonGlow";
        horizon.transform.SetParent(parent, false);
        horizon.transform.localPosition = new Vector3(0f, -4f, 0f);
        horizon.transform.localScale = new Vector3(1600f, 2.8f, 1600f);
        Object.Destroy(horizon.GetComponent<Collider>());
        var hr = horizon.GetComponent<Renderer>();
        if (hr != null) hr.material = horizonMat;

        // 明るい草原パレット（黄緑〜若葉）
        var softHill = new Material(lit);
        softHill.color = new Color(0.52f, 0.72f, 0.34f);
        var sunMeadow = new Material(lit);
        sunMeadow.color = new Color(0.62f, 0.82f, 0.38f);
        var lightMeadow = new Material(lit);
        lightMeadow.color = new Color(0.72f, 0.88f, 0.48f);
        var paleGrass = new Material(lit);
        paleGrass.color = new Color(0.80f, 0.90f, 0.55f);

        // 遠景：なだらかな明るい丘（深い森の稜線は控えめ）
        for (int i = 0; i < 14; i++)
        {
            float ang = i * (360f / 14f) * Mathf.Deg2Rad;
            float dist = 560f + (i % 4) * 50f;
            float peakH = Random.Range(38f, 78f);
            Vector3 pos = new Vector3(Mathf.Cos(ang) * dist, Random.Range(-22f, -4f), Mathf.Sin(ang) * dist);

            var peak = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            peak.name = $"GreenRidge_{i}";
            peak.transform.SetParent(parent, false);
            peak.transform.localPosition = pos;
            peak.transform.localScale = new Vector3(210f + (i % 5) * 30f, peakH, 180f + (i % 3) * 28f);
            Object.Destroy(peak.GetComponent<Collider>());
            var rend = peak.GetComponent<Renderer>();
            if (rend != null)
                rend.material = (i % 3 == 0) ? softHill : ((i % 3 == 1) ? sunMeadow : lightMeadow);
        }

        // 中景〜近景：広がる明るい草原の丘（主役）
        for (int i = 0; i < 20; i++)
        {
            float ang = (i * 18f + 8f) * Mathf.Deg2Rad;
            float dist = 240f + (i % 6) * 42f;
            var hill = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hill.name = $"MeadowHill_{i}";
            hill.transform.SetParent(parent, false);
            hill.transform.localPosition = new Vector3(
                Mathf.Cos(ang) * dist, Random.Range(-24f, -10f), Mathf.Sin(ang) * dist);
            float s = Random.Range(100f, 175f);
            hill.transform.localScale = new Vector3(s * 1.55f, s * 0.28f, s * 1.2f);
            Object.Destroy(hill.GetComponent<Collider>());
            var rend = hill.GetComponent<Renderer>();
            if (rend != null)
                rend.material = (i % 3 == 0) ? paleGrass : ((i % 3 == 1) ? lightMeadow : sunMeadow);
        }

        // 点在する明るい木立（森ではなくまばらな木陰）
        var canopyMat = new Material(lit);
        canopyMat.color = new Color(0.42f, 0.68f, 0.32f);
        for (int i = 0; i < 10; i++)
        {
            float ang = i * (360f / 10f) * Mathf.Deg2Rad + 0.25f;
            float dist = 360f + (i % 4) * 55f;
            var tree = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tree.name = $"MeadowTree_{i}";
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = new Vector3(
                Mathf.Cos(ang) * dist, Random.Range(-14f, -2f), Mathf.Sin(ang) * dist);
            float s = Random.Range(18f, 32f);
            tree.transform.localScale = new Vector3(s, s * 1.05f, s);
            Object.Destroy(tree.GetComponent<Collider>());
            var rend = tree.GetComponent<Renderer>();
            if (rend != null) rend.material = canopyMat;
        }

        // 白いふわ雲
        var cloudMat = new Material(unlit);
        cloudMat.color = new Color(0.96f, 0.98f, 1f, 0.55f);
        for (int c = 0; c < 16; c++)
        {
            float ang = c * 22.5f * Mathf.Deg2Rad + 0.1f;
            float dist = 260f + (c % 5) * 60f;
            var cloud = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cloud.name = $"CloudSea_{c}";
            cloud.transform.SetParent(parent, false);
            cloud.transform.localPosition = new Vector3(
                Mathf.Cos(ang) * dist,
                Random.Range(40f, 85f),
                Mathf.Sin(ang) * dist);
            float s = Random.Range(50f, 100f);
            cloud.transform.localScale = new Vector3(s * 1.7f, s * 0.32f, s * 1.15f);
            Object.Destroy(cloud.GetComponent<Collider>());
            var cr = cloud.GetComponent<Renderer>();
            if (cr != null) cr.material = cloudMat;
        }

        // 高空の薄雲
        var cirrusMat = new Material(unlit);
        cirrusMat.color = new Color(0.92f, 0.97f, 1f, 0.28f);
        for (int c = 0; c < 8; c++)
        {
            float ang = c * 45f * Mathf.Deg2Rad + 0.35f;
            float dist = 440f + (c % 3) * 80f;
            var wisps = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            wisps.name = $"Cirrus_{c}";
            wisps.transform.SetParent(parent, false);
            wisps.transform.localPosition = new Vector3(
                Mathf.Cos(ang) * dist,
                Random.Range(120f, 180f),
                Mathf.Sin(ang) * dist);
            float s = Random.Range(100f, 170f);
            wisps.transform.localScale = new Vector3(s * 2.2f, s * 0.1f, s * 0.85f);
            Object.Destroy(wisps.GetComponent<Collider>());
            var wr = wisps.GetComponent<Renderer>();
            if (wr != null) wr.material = cirrusMat;
        }

        // 柔らかい緑がかった光芒（砂漠の金ではなく）
        var raysGo = new GameObject("SkybreakGodRays");
        raysGo.transform.SetParent(parent, false);
        raysGo.transform.localPosition = new Vector3(0f, 55f, 0f);
        var softRayMat = new Material(unlit);
        softRayMat.color = new Color(0.95f, 0.98f, 0.72f, 0.24f);
        var skyRayMat = new Material(unlit);
        skyRayMat.color = new Color(0.85f, 0.95f, 1.0f, 0.16f);
        for (int r = 0; r < 10; r++)
        {
            var ray = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ray.name = $"GodRay_{r}";
            ray.transform.SetParent(raysGo.transform, false);
            ray.transform.localScale = new Vector3(4.2f, 150f, 4.2f);
            ray.transform.localRotation = Quaternion.Euler(Random.Range(14f, 36f), r * 36f + 10f, 0f);
            Object.Destroy(ray.GetComponent<Collider>());
            var rend = ray.GetComponent<Renderer>();
            if (rend != null)
                rend.material = (r % 2 == 0) ? softRayMat : skyRayMat;
        }
    }

    /// <summary>割れ目の外側に見える本物の空（巨大ドーム）</summary>
    static void SpawnOuterSkyScene(Transform parent, bool coldCrisis)
    {
        var skyShader = Shader.Find("RustAndFloat/ClearBlueSky")
                        ?? Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Unlit/Color");

        var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dome.name = "OuterSkyDome";
        dome.transform.SetParent(parent, false);
        // 内側から見えるよう巨大化し、法線を内向きに反転
        dome.transform.localPosition = new Vector3(0f, 40f, 0f);
        dome.transform.localScale = new Vector3(-2400f, 2400f, 2400f);
        Object.Destroy(dome.GetComponent<Collider>());

        var rend = dome.GetComponent<Renderer>();
        if (rend == null) return;

        var mat = new Material(skyShader);
        ApplySkyMaterialColors(mat, coldCrisis);
        if (mat.HasProperty("_Cull"))
            mat.SetFloat("_Cull", 0f);
        mat.renderQueue = 1000; // Background — 山脈より奥に
        rend.material = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        // 太陽寄りに明るい半球ハイライト（ドーム補助）
        var sunGlow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sunGlow.name = "OuterSkySunGlow";
        sunGlow.transform.SetParent(parent, false);
        sunGlow.transform.localPosition = coldCrisis
            ? new Vector3(380f, 220f, -420f)
            : new Vector3(420f, 260f, -380f);
        sunGlow.transform.localScale = Vector3.one * (coldCrisis ? 120f : 160f);
        Object.Destroy(sunGlow.GetComponent<Collider>());
        var unlit = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Sprites/Default")
                    ?? Shader.Find("Unlit/Color");
        var glowMat = new Material(unlit);
        glowMat.color = coldCrisis
            ? new Color(0.85f, 0.92f, 1f, 0.35f)
            : new Color(0.95f, 0.98f, 0.85f, 0.40f);
        var gr = sunGlow.GetComponent<Renderer>();
        if (gr != null)
        {
            gr.material = glowMat;
            gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    static void ApplySkyMaterialColors(Material mat, bool coldCrisis)
    {
        if (mat == null) return;
        if (mat.HasProperty("_TopColor"))
        {
            if (coldCrisis)
            {
                mat.SetColor("_TopColor", new Color(0.04f, 0.14f, 0.42f, 1f));
                mat.SetColor("_MidColor", new Color(0.22f, 0.48f, 0.82f, 1f));
                mat.SetColor("_HorizonColor", new Color(0.62f, 0.78f, 0.95f, 1f));
                mat.SetColor("_GroundColor", new Color(0.35f, 0.48f, 0.72f, 1f));
                mat.SetColor("_SunColor", new Color(0.92f, 0.96f, 1f, 1f));
            }
            else
            {
                // 解放後：澄んだ青空＋海際の柔らかい光（砂漠の金は使わない）
                mat.SetColor("_TopColor", new Color(0.05f, 0.32f, 0.88f, 1f));
                mat.SetColor("_MidColor", new Color(0.22f, 0.62f, 0.98f, 1f));
                mat.SetColor("_HorizonColor", new Color(0.78f, 0.92f, 0.82f, 1f));
                mat.SetColor("_GroundColor", new Color(0.35f, 0.58f, 0.48f, 1f));
                mat.SetColor("_SunColor", new Color(1f, 0.98f, 0.92f, 1f));
            }
            if (mat.HasProperty("_SunSize")) mat.SetFloat("_SunSize", coldCrisis ? 0.028f : 0.038f);
            if (mat.HasProperty("_SunGlow")) mat.SetFloat("_SunGlow", coldCrisis ? 2.2f : 2.4f);
            if (mat.HasProperty("_Exponent")) mat.SetFloat("_Exponent", 0.62f);
            if (mat.HasProperty("_HorizonOffset")) mat.SetFloat("_HorizonOffset", coldCrisis ? 0.02f : 0.0f);
        }
        else
        {
            mat.color = coldCrisis
                ? new Color(0.35f, 0.55f, 0.85f, 1f)
                : new Color(0.45f, 0.72f, 1f, 1f);
        }
    }

    static void ApplySkyboxForSkybreak(bool coldCrisis)
    {
        if (!_coldAtmosphereActive && _savedSkybox == null && RenderSettings.skybox != _runtimeSkyMat)
            _savedSkybox = RenderSettings.skybox;

        var skyShader = Shader.Find("RustAndFloat/ClearBlueSky");
        if (skyShader == null) return;

        if (_runtimeSkyMat == null || _runtimeSkyMat.shader != skyShader)
            _runtimeSkyMat = new Material(skyShader);

        ApplySkyMaterialColors(_runtimeSkyMat, coldCrisis);
        RenderSettings.skybox = _runtimeSkyMat;
        DynamicGI.UpdateEnvironment();

        if (_cachedSun == null)
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    _cachedSun = lights[i];
                    break;
                }
            }
        }
        if (_cachedSun != null)
        {
            _cachedSun.transform.rotation = coldCrisis
                ? Quaternion.Euler(28f, 150f, 0f)
                : Quaternion.Euler(48f, 125f, 0f);
            _cachedSun.intensity = coldCrisis ? 1.35f : 1.85f;
            _cachedSun.color = coldCrisis
                ? new Color(0.85f, 0.92f, 1f)
                : new Color(1f, 0.98f, 0.92f);
        }
    }

    static void SpawnLiberationDust(Transform parent)
    {
        var dustGo = new GameObject("LiberationDust");
        dustGo.transform.SetParent(parent, false);
        dustGo.transform.localPosition = new Vector3(0f, 55f, 0f);

        var ps = dustGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 1.4f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.85f, 0.98f, 0.65f, 0.55f),
            new Color(0.95f, 1f, 0.88f, 0.18f));
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 2.2f);
        main.maxParticles = 160;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 22f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 28f;
        shape.radius = 8f;

        var colorOver = ps.colorOverLifetime;
        colorOver.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.9f, 1f, 0.75f), 0f),
                new GradientColorKey(new Color(0.7f, 0.92f, 0.55f), 1f)
            },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.65f, 0.2f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOver.color = grad;

        var rend = dustGo.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Sprites/Default");
            var mat = new Material(sh);
            mat.color = new Color(0.88f, 0.98f, 0.70f, 0.75f);
            rend.material = mat;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
        }
        ps.Play();
    }

    public static IEnumerator BreakthroughFlashRoutine()
    {
        var canvasGo = new GameObject("BreakthroughFlashCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);

        var imgGo = new GameObject("Flash");
        imgGo.transform.SetParent(canvasGo.transform, false);
        var rt = imgGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = imgGo.AddComponent<Image>();
        img.raycastTarget = false;
        img.color = new Color(1f, 0.96f, 0.82f, 0f);

        float t = 0f;
        while (t < 0.1f)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / 0.1f);
            img.color = new Color(1f, 0.95f, 0.78f, a);
            yield return null;
        }
        img.color = new Color(1f, 0.97f, 0.88f, 1f);
        yield return new WaitForSecondsRealtime(0.12f);

        t = 0f;
        while (t < 1.35f)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / 1.35f);
            a *= a;
            img.color = new Color(1f, 0.9f, 0.55f, a * 0.95f);
            yield return null;
        }

        if (canvasGo != null)
            Object.Destroy(canvasGo);
    }

    static void SpawnSkybreakColdMist(Transform parent)
    {
        var mistGo = new GameObject("SkybreakColdMist");
        mistGo.transform.SetParent(parent, false);
        mistGo.transform.localPosition = new Vector3(0f, 40f, 0f);

        var ps = mistGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 3.8f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.85f, 0.93f, 1f, 0.18f),
            new Color(0.70f, 0.85f, 1f, 0.08f));
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 8f);
        main.maxParticles = 120;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 18f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 90f;

        // velocityOverLifetime の軸モード不一致で毎フレームエラーになるため使わない
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 1.1f;
        noise.frequency = 0.22f;
        noise.scrollSpeed = 0.35f;
        noise.octaveCount = 2;

        var colorOver = ps.colorOverLifetime;
        colorOver.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.9f, 0.95f, 1f), 0f),
                new GradientColorKey(new Color(0.7f, 0.85f, 1f), 1f)
            },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.22f, 0.25f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOver.color = grad;

        var rend = mistGo.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Sprites/Default");
            var mat = new Material(sh);
            mat.color = new Color(0.85f, 0.92f, 1f, 0.35f);
            rend.material = mat;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
        }
        ps.Play();
    }

    public static void ApplyColdAtmosphere()
    {
        if (!_coldAtmosphereActive)
        {
            _savedFogEnabled = RenderSettings.fog;
            _savedFogColor = RenderSettings.fogColor;
            _savedFogDensity = RenderSettings.fogDensity;
            _savedAmbient = RenderSettings.ambientLight;
            _coldAtmosphereActive = true;
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.62f, 0.74f, 0.88f);
        RenderSettings.fogDensity = 0.0048f;
        RenderSettings.ambientLight = new Color(0.55f, 0.68f, 0.82f);
    }

    public static void SoftenColdAtmosphere()
    {
        if (!_coldAtmosphereActive)
            ApplyColdAtmosphere();

        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.82f, 0.92f, 0.78f);
        RenderSettings.fogDensity = 0.0018f;
        RenderSettings.ambientLight = new Color(0.82f, 0.90f, 0.72f);
        ApplySkyboxForSkybreak(coldCrisis: false);

        // ドーム色も解放後の空へ
        var dome = GameObject.Find("OuterSkyDome");
        if (dome != null)
        {
            var r = dome.GetComponent<Renderer>();
            if (r != null && r.material != null)
                ApplySkyMaterialColors(r.material, coldCrisis: false);
        }

        var mist = GameObject.Find("SkybreakColdMist");
        if (mist != null)
        {
            var ps = mist.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var emission = ps.emission;
                emission.rateOverTime = 6f;
            }
        }
    }

    public static void ClearColdAtmosphere()
    {
        if (!_coldAtmosphereActive && _savedSkybox == null) return;
        if (_coldAtmosphereActive)
        {
            RenderSettings.fog = _savedFogEnabled;
            RenderSettings.fogColor = _savedFogColor;
            RenderSettings.fogDensity = _savedFogDensity;
            RenderSettings.ambientLight = _savedAmbient;
            _coldAtmosphereActive = false;
        }
        if (_savedSkybox != null)
        {
            RenderSettings.skybox = _savedSkybox;
            _savedSkybox = null;
            DynamicGI.UpdateEnvironment();
        }
    }
}
