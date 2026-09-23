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

    /// <summary>
    /// 「空が……割れるよ」：リアル寄りの稲妻＋フラッシュ＋破片＋地面揺れ。
    /// （中心交差の放射帯は使わない）
    /// </summary>
    public static IEnumerator PlaySkyTearOpenRoutine()
    {
        DestroyNamed("SkyTearOpening");
        DestroyNamed("SkyTearFlashCanvas");

        var cam = AdventureCameraFollow.InstanceOrFind();
        if (cam != null)
            cam.Shake(0.55f, 5.8f);

        var root = new GameObject("SkyTearOpening");
        root.transform.position = new Vector3(512f, 145f, 512f);

        var particleSh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Sprites/Default");
        var lineSh = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Sprites/Default")
                     ?? Shader.Find("Unlit/Color");

        // 稲妻（ジグザグ本幹＋分岐）。複数本を時間差で撃つ
        var bolts = new LightningBolt[6];
        for (int i = 0; i < bolts.Length; i++)
        {
            float yaw = -55f + i * 22f + Random.Range(-8f, 8f);
            Vector3 origin = root.transform.position
                            + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 55f + Random.Range(-10f, 20f), 35f + i * 8f);
            Vector3 tip = root.transform.position
                          + Quaternion.Euler(0f, yaw + Random.Range(-12f, 12f), 0f)
                          * new Vector3(Random.Range(-18f, 18f), -70f - Random.Range(0f, 40f), Random.Range(-10f, 30f));
            bolts[i] = CreateLightningBolt(root.transform, lineSh, "Bolt_" + i, origin, tip,
                segments: 18 + Random.Range(0, 8),
                jag: 4.5f + Random.Range(0f, 3.5f),
                coreWidth: 0.85f + Random.Range(0f, 0.6f),
                glowWidth: 3.2f + Random.Range(0f, 2f),
                branchChance: 0.45f);
            SetLightningVisible(bolts[i], false);
        }

        // 外光（控えめなコアライトのみ。巨大球体は出さない）
        var lightGo = new GameObject("TearSkyLight");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 20f, 0f);
        var coreLight = lightGo.AddComponent<Light>();
        coreLight.type = LightType.Point;
        coreLight.color = new Color(0.78f, 0.92f, 1f);
        coreLight.intensity = 0f;
        coreLight.range = 160f;

        SpawnSkyTearBurst(root.transform, particleSh, "SkyTearShards",
            new Color(0.75f, 0.95f, 1f, 0.9f), new Color(1f, 0.95f, 0.85f, 0.75f),
            speedMin: 18f, speedMax: 55f, sizeMin: 0.4f, sizeMax: 2.0f,
            gravity: 0.35f, radius: 10f,
            bursts: new[] { 90, 70, 50, 35 }, burstTimes: new[] { 0.02f, 0.28f, 0.7f, 1.35f });

        SpawnSkyTearBurst(root.transform, particleSh, "SkyTearDebris",
            new Color(0.55f, 0.85f, 1f, 0.8f), new Color(0.9f, 0.9f, 0.85f, 0.7f),
            speedMin: 6f, speedMax: 22f, sizeMin: 1.0f, sizeMax: 3.8f,
            gravity: 1.1f, radius: 18f,
            bursts: new[] { 28, 22 }, burstTimes: new[] { 0.15f, 0.9f });

        var dustGo = new GameObject("SkyTearGroundDust");
        dustGo.transform.SetParent(root.transform, false);
        dustGo.transform.localPosition = new Vector3(0f, -80f, 0f);
        var dustPs = dustGo.AddComponent<ParticleSystem>();
        {
            var main = dustPs.main;
            main.loop = false;
            main.duration = 3.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 16f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 5f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.55f, 0.48f, 0.35f, 0.45f),
                new Color(0.7f, 0.65f, 0.5f, 0.2f));
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 180;
            var emission = dustPs.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0.08f, 50),
                new ParticleSystem.Burst(0.45f, 40),
                new ParticleSystem.Burst(1.1f, 30)
            });
            var shape = dustPs.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 55f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var rend = dustGo.GetComponent<ParticleSystemRenderer>();
            if (rend != null && particleSh != null)
            {
                var mat = new Material(particleSh);
                mat.SetColor("_BaseColor", new Color(0.6f, 0.55f, 0.4f, 0.4f));
                rend.material = mat;
            }
            dustPs.Play();
        }

        // 画面フラッシュのみ（放射割れラインなし）
        var flashGo = new GameObject("SkyTearFlashCanvas");
        var canvas = flashGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 8850;
        var scaler = flashGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        var flashImg = CreateFullScreenImage(flashGo.transform, "Flash", new Color(0.88f, 0.95f, 1f, 0f));
        var flashWhite = CreateFullScreenImage(flashGo.transform, "FlashWhite", new Color(1f, 1f, 1f, 0f));

        // 各稲妻の撃つタイミング（リアルな連続落雷）＋後半の余韻2秒
        float[] strikeAt = { 0.08f, 0.32f, 0.55f, 0.95f, 1.45f, 2.15f, 3.2f, 4.35f };
        float[] strikeDur = { 0.12f, 0.09f, 0.18f, 0.11f, 0.14f, 0.1f, 0.13f, 0.11f };
        var boltsExtra = new LightningBolt[2];
        for (int i = 0; i < boltsExtra.Length; i++)
        {
            float yaw = -30f + i * 50f + Random.Range(-10f, 10f);
            Vector3 origin = root.transform.position
                            + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 50f + Random.Range(-8f, 16f), 40f);
            Vector3 tip = root.transform.position
                          + Quaternion.Euler(0f, yaw + Random.Range(-14f, 14f), 0f)
                          * new Vector3(Random.Range(-20f, 20f), -75f - Random.Range(0f, 35f), Random.Range(-12f, 28f));
            boltsExtra[i] = CreateLightningBolt(root.transform, lineSh, "BoltExtra_" + i, origin, tip,
                segments: 16 + Random.Range(0, 6),
                jag: 4.2f + Random.Range(0f, 3f),
                coreWidth: 0.7f + Random.Range(0f, 0.5f),
                glowWidth: 2.8f + Random.Range(0f, 1.8f),
                branchChance: 0.4f);
            SetLightningVisible(boltsExtra[i], false);
        }
        // 後半2本を配列に結合
        var allBolts = new LightningBolt[bolts.Length + boltsExtra.Length];
        for (int i = 0; i < bolts.Length; i++) allBolts[i] = bolts[i];
        for (int i = 0; i < boltsExtra.Length; i++) allBolts[bolts.Length + i] = boltsExtra[i];
        bolts = allBolts;
        bool[] struck = new bool[bolts.Length];
        bool[] reshaped = new bool[bolts.Length];

        float duration = 6.0f;
        float t = 0f;
        bool shookHard = false;
        bool shookMid = false;
        bool shookLate1 = false;
        bool shookLate2 = false;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            for (int i = 0; i < bolts.Length; i++)
            {
                float start = strikeAt[i];
                float end = start + strikeDur[i];
                // 落雷直前に経路を再生成（毎回違うジグザグ）
                if (!reshaped[i] && t >= start - 0.02f)
                {
                    reshaped[i] = true;
                    ReshapeLightningBolt(bolts[i]);
                }

                bool on = t >= start && t <= end;
                // リアル稲妻：一瞬消え・再点灯（ステイマー）
                if (on)
                {
                    float local = t - start;
                    bool flickerOff = (local > 0.04f && local < 0.055f)
                                      || (local > 0.08f && local < 0.09f && strikeDur[i] > 0.12f);
                    on = !flickerOff;
                }
                SetLightningVisible(bolts[i], on);

                if (!struck[i] && t >= start)
                {
                    struck[i] = true;
                    if (cam != null)
                        cam.Shake(i == 0 || i == 2 ? 0.7f : 0.35f, 0.55f);
                }
            }

            // 稲妻に連動した空の閃光
            float flashA = 0f;
            float whiteA = 0f;
            for (int i = 0; i < strikeAt.Length; i++)
            {
                flashA += SkyTearFlashEnvelope(t, strikeAt[i], strikeDur[i] * 1.8f, i % 2 == 0 ? 0.72f : 0.45f);
                whiteA += SkyTearFlashEnvelope(t, strikeAt[i], Mathf.Min(0.08f, strikeDur[i]), 0.55f);
            }
            // 余韻の薄明かり（＋2秒）
            flashA += SkyTearFlashEnvelope(t, 2.6f, 1.2f, 0.14f);
            flashA += SkyTearFlashEnvelope(t, 4.0f, 1.4f, 0.18f);
            flashA += SkyTearFlashEnvelope(t, 5.2f, 0.9f, 0.1f);
            flashImg.color = new Color(0.78f, 0.92f, 1f, Mathf.Clamp01(flashA));
            flashWhite.color = new Color(1f, 1f, 1f, Mathf.Clamp01(whiteA));

            if (coreLight != null)
            {
                float lightPulse = Mathf.Clamp01(flashA) * 18f + Mathf.Clamp01(whiteA) * 8f;
                coreLight.intensity = lightPulse;
                coreLight.color = Color.Lerp(
                    new Color(0.7f, 0.88f, 1f),
                    new Color(0.95f, 0.97f, 1f),
                    Mathf.Clamp01(whiteA * 2f));
            }

            if (!shookHard && t >= 0.3f)
            {
                shookHard = true;
                if (cam != null) cam.Shake(0.85f, 2.8f);
            }
            if (!shookMid && t >= 1.0f)
            {
                shookMid = true;
                if (cam != null) cam.Shake(0.4f, 2.4f);
            }
            if (!shookLate1 && t >= 3.2f)
            {
                shookLate1 = true;
                if (cam != null) cam.Shake(0.5f, 1.8f);
            }
            if (!shookLate2 && t >= 4.35f)
            {
                shookLate2 = true;
                if (cam != null) cam.Shake(0.35f, 1.4f);
            }

            yield return null;
        }

        for (int i = 0; i < bolts.Length; i++)
            SetLightningVisible(bolts[i], false);

        float fadeT = 0f;
        while (fadeT < 1.2f)
        {
            fadeT += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(fadeT / 1.2f);
            flashImg.color = new Color(0.78f, 0.92f, 1f, a * 0.06f);
            flashWhite.color = new Color(1f, 1f, 1f, 0f);
            if (coreLight != null)
                coreLight.intensity = a * 4f;
            yield return null;
        }

        if (flashGo != null)
            Object.Destroy(flashGo);
        if (coreLight != null)
            coreLight.intensity = 2.5f;
    }

    struct LightningBolt
    {
        public LineRenderer Core;
        public LineRenderer Glow;
        public LineRenderer[] Branches;
        public Vector3 Origin;
        public Vector3 Tip;
        public int Segments;
        public float Jag;
    }

    static LightningBolt CreateLightningBolt(
        Transform parent, Shader lineSh, string name,
        Vector3 origin, Vector3 tip,
        int segments, float jag, float coreWidth, float glowWidth, float branchChance)
    {
        var holder = new GameObject(name);
        holder.transform.SetParent(parent, false);

        var bolt = new LightningBolt
        {
            Origin = origin,
            Tip = tip,
            Segments = segments,
            Jag = jag,
            Core = MakeLine(holder.transform, "Core", lineSh, new Color(0.92f, 0.97f, 1f, 1f), coreWidth),
            Glow = MakeLine(holder.transform, "Glow", lineSh, new Color(0.45f, 0.75f, 1f, 0.28f), glowWidth),
            Branches = new LineRenderer[4]
        };

        for (int b = 0; b < bolt.Branches.Length; b++)
        {
            bolt.Branches[b] = MakeLine(holder.transform, "Branch_" + b, lineSh,
                new Color(0.85f, 0.93f, 1f, 0.85f), coreWidth * 0.45f);
            bolt.Branches[b].enabled = false;
        }

        BuildLightningPath(bolt, branchChance);
        return bolt;
    }

    static LineRenderer MakeLine(Transform parent, string name, Shader sh, Color color, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.widthMultiplier = 1f;
        lr.startWidth = width;
        lr.endWidth = width * 0.35f;
        lr.positionCount = 2;
        var mat = new Material(sh);
        mat.SetColor("_BaseColor", color);
        mat.color = color;
        lr.material = mat;
        lr.startColor = color;
        lr.endColor = new Color(color.r, color.g, color.b, color.a * 0.55f);
        return lr;
    }

    static void BuildLightningPath(LightningBolt bolt, float branchChance)
    {
        int n = Mathf.Max(6, bolt.Segments);
        var pts = new Vector3[n];
        Vector3 dir = bolt.Tip - bolt.Origin;
        Vector3 right = Vector3.Cross(dir.normalized, Vector3.up);
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.Cross(dir.normalized, Vector3.right);
        right.Normalize();
        Vector3 up = Vector3.Cross(right, dir.normalized).normalized;

        pts[0] = bolt.Origin;
        pts[n - 1] = bolt.Tip;
        for (int i = 1; i < n - 1; i++)
        {
            float u = i / (float)(n - 1);
            // 中間ほど強くジグザグ（落雷らしい不規則さ）
            float amp = bolt.Jag * Mathf.Sin(u * Mathf.PI);
            Vector3 baseP = Vector3.Lerp(bolt.Origin, bolt.Tip, u);
            pts[i] = baseP
                     + right * Random.Range(-amp, amp)
                     + up * Random.Range(-amp * 0.55f, amp * 0.55f);
            // 急な折れ
            if (Random.value < 0.35f)
                pts[i] += right * Random.Range(-amp * 1.4f, amp * 1.4f);
        }

        bolt.Core.positionCount = n;
        bolt.Glow.positionCount = n;
        bolt.Core.SetPositions(pts);
        bolt.Glow.SetPositions(pts);

        for (int b = 0; b < bolt.Branches.Length; b++)
        {
            bool spawn = Random.value < branchChance;
            bolt.Branches[b].enabled = spawn;
            if (!spawn) continue;

            int forkAt = Random.Range(n / 4, (n * 3) / 4);
            int branchSegs = Random.Range(5, 10);
            var bPts = new Vector3[branchSegs];
            bPts[0] = pts[forkAt];
            Vector3 bDir = (right * Random.Range(-1f, 1f) + up * Random.Range(-0.4f, 0.4f)
                            + dir.normalized * Random.Range(0.2f, 0.7f)).normalized;
            float bLen = dir.magnitude * Random.Range(0.18f, 0.4f);
            for (int i = 1; i < branchSegs; i++)
            {
                float u = i / (float)(branchSegs - 1);
                Vector3 p = bPts[0] + bDir * (bLen * u);
                float amp = bolt.Jag * 0.45f * (1f - u);
                p += right * Random.Range(-amp, amp) + up * Random.Range(-amp, amp);
                bPts[i] = p;
            }
            bolt.Branches[b].positionCount = branchSegs;
            bolt.Branches[b].SetPositions(bPts);
            bolt.Branches[b].startWidth = bolt.Core.startWidth * 0.4f;
            bolt.Branches[b].endWidth = bolt.Core.startWidth * 0.08f;
        }
    }

    static void ReshapeLightningBolt(LightningBolt bolt)
    {
        // 着地点を少しずらして再生成
        Vector3 tipJitter = new Vector3(Random.Range(-12f, 12f), Random.Range(-8f, 8f), Random.Range(-12f, 12f));
        bolt.Tip += tipJitter;
        BuildLightningPath(bolt, 0.5f);
    }

    static void SetLightningVisible(LightningBolt bolt, bool visible)
    {
        if (bolt.Core != null) bolt.Core.enabled = visible;
        if (bolt.Glow != null) bolt.Glow.enabled = visible;
        if (bolt.Branches == null) return;
        for (int i = 0; i < bolt.Branches.Length; i++)
        {
            if (bolt.Branches[i] == null) continue;
            // 分岐は生成時に enabled が立っているものだけ点灯
            if (visible)
            {
                // positionCount>2 なら分岐として有効
                if (bolt.Branches[i].positionCount > 2)
                    bolt.Branches[i].enabled = true;
            }
            else
            {
                bolt.Branches[i].enabled = false;
            }
        }
    }

    static float SkyTearFlashEnvelope(float t, float start, float width, float peak)
    {
        if (t < start || t > start + width) return 0f;
        float u = (t - start) / width;
        return Mathf.Sin(u * Mathf.PI) * peak;
    }

    static Image CreateFullScreenImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        img.color = color;
        return img;
    }

    static void SpawnSkyTearBurst(
        Transform parent, Shader particleSh, string name,
        Color c0, Color c1,
        float speedMin, float speedMax, float sizeMin, float sizeMax,
        float gravity, float radius,
        int[] bursts, float[] burstTimes)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.duration = 3.8f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 4.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = new ParticleSystem.MinMaxGradient(c0, c1);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 500;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        int n = Mathf.Min(bursts.Length, burstTimes.Length);
        var burstArr = new ParticleSystem.Burst[n];
        for (int i = 0; i < n; i++)
            burstArr[i] = new ParticleSystem.Burst(burstTimes[i], bursts[i]);
        emission.SetBursts(burstArr);
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        if (rend != null && particleSh != null)
        {
            var mat = new Material(particleSh);
            mat.SetColor("_BaseColor", c0);
            rend.material = mat;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
        }
        ps.Play();
    }
}
