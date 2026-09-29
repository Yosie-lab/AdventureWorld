using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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

    // ── リアル極寒ポストプロセス＆凍結ビネット ──
    static GameObject _coldVolumeGo;
    static Volume _coldVolume;
    static GameObject _frostVignetteCanvas;
    static Image _frostVignetteImage;
    static Coroutine _warmthFadeCoroutine;

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

        var unlit = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Sprites/Default")
                    ?? Shader.Find("Unlit/Color");

        if (coldCrisis)
        {
            // 限界中：オープニングの高品質映画的空を基調に、天蓋が割れて冷涼な光芒と冷気ミストが天空から降り注ぐ
            ApplySkyboxForSkybreak(coldCrisis: true);
            SpawnSkybreakGodRays(panoramaGo.transform, unlit, coldCrisis: true);
            SpawnSkybreakColdMist(panoramaGo.transform);
        }
        else
        {
            // 突破後：オープニングの空が全開に広がり、黄金の祝福光芒と光粒子が空を舞う
            ApplySkyboxForSkybreak(coldCrisis: false);
            SpawnSkybreakGodRays(panoramaGo.transform, unlit, coldCrisis: false);
            SpawnLiberationDust(panoramaGo.transform, golden: true);
            AdventureCloudDrift.EnsureCloudSystem();
        }
    }

    /// <summary>天蓋の裂け目から島全体へ降り注ぐ映画的シネマティック光芒（God Rays）</summary>
    static void SpawnSkybreakGodRays(Transform parent, Shader unlit, bool coldCrisis)
    {
        // プレイヤーやRustの視界を塞ぐ近距離シリンダーは配置せず、遠景（半径350m以上）にのみ配置
        var raysGo = new GameObject("SkybreakGodRays");
        raysGo.transform.SetParent(parent, false);
        raysGo.transform.localPosition = new Vector3(0f, 60f, 0f);

        var rayMat = new Material(Shader.Find("Sprites/Default") ?? unlit);
        Color rayColor = coldCrisis
            ? new Color(0.70f, 0.90f, 1.0f, 0.08f)  // 寒冷限界：澄み渡る蒼白の淡い光芒
            : new Color(1.0f, 0.94f, 0.72f, 0.12f); // 解放後：黄金に輝く祝福の淡い光芒
        rayMat.color = rayColor;

        for (int r = 0; r < 8; r++)
        {
            var ray = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ray.name = $"GodRay_{r}";
            ray.transform.SetParent(raysGo.transform, false);
            float angle = r * (360f / 8f) + Random.Range(-10f, 10f);
            float tilt = Random.Range(15f, 30f);
            // プレイヤー（半径0〜50m）から遥かに離れた外周（280m〜380m）に配置して絶対に遮蔽しない
            float dist = Random.Range(280f, 380f);
            ray.transform.localPosition = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * dist, 40f, Mathf.Sin(angle * Mathf.Deg2Rad) * dist);
            ray.transform.localRotation = Quaternion.Euler(tilt, angle, 0f);
            ray.transform.localScale = new Vector3(12f, 220f, 12f);
            Object.Destroy(ray.GetComponent<Collider>());
            var rend = ray.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material = rayMat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }
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
        horizon.transform.localScale = new Vector3(1600f, 1.2f, 1600f);
        Object.Destroy(horizon.GetComponent<Collider>());
        var horizonMat = new Material(unlit);
        // 地平は薄い青白のみ（オレンジ／汚れたシアンで空を染めない）
        horizonMat.color = new Color(0.70f, 0.85f, 0.98f, 0.22f);
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

        // 地平の明るい草原の霞（空の青を汚さない薄い帯）
        var horizonMat = new Material(unlit);
        horizonMat.color = new Color(0.85f, 0.94f, 0.78f, 0.28f);
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

    /// <summary>
    /// 半透明の細い光柱（未使用フォールバック。冷危機／解放は上記のクラシック光芒を使う）。
    /// </summary>
    static void SpawnSoftGodRays(Transform parent, Shader unlit, bool coldCrisis)
    {
        var raysGo = new GameObject("SkybreakGodRays");
        raysGo.transform.SetParent(parent, false);
        raysGo.transform.localPosition = new Vector3(0f, 58f, 0f);

        var coreMat = new Material(unlit);
        coreMat.color = coldCrisis
            ? new Color(1f, 0.92f, 0.62f, 0.08f)
            : new Color(1f, 0.96f, 0.78f, 0.09f);
        var auraMat = new Material(unlit);
        auraMat.color = coldCrisis
            ? new Color(1f, 0.78f, 0.40f, 0.04f)
            : new Color(0.95f, 0.98f, 0.70f, 0.05f);

        int count = coldCrisis ? 10 : 9;
        for (int r = 0; r < count; r++)
        {
            float yaw = r * (360f / count) + Random.Range(-8f, 8f);
            float pitch = Random.Range(12f, 28f);
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);

            // 極細コア（太さは視界で線に近い）
            var core = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            core.name = $"GodRayCore_{r}";
            core.transform.SetParent(raysGo.transform, false);
            core.transform.localRotation = rot;
            core.transform.localPosition = rot * Vector3.up * 75f;
            float thin = Random.Range(0.55f, 1.1f);
            core.transform.localScale = new Vector3(thin, Random.Range(100f, 150f), thin);
            Object.Destroy(core.GetComponent<Collider>());
            var cr = core.GetComponent<Renderer>();
            if (cr != null)
            {
                cr.material = coreMat;
                cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cr.receiveShadows = false;
            }

            var aura = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            aura.name = $"GodRayAura_{r}";
            aura.transform.SetParent(raysGo.transform, false);
            aura.transform.localRotation = rot;
            aura.transform.localPosition = rot * Vector3.up * 70f;
            float wide = thin * Random.Range(3.5f, 5.5f);
            aura.transform.localScale = new Vector3(wide, Random.Range(90f, 135f), wide);
            Object.Destroy(aura.GetComponent<Collider>());
            var ar = aura.GetComponent<Renderer>();
            if (ar != null)
            {
                ar.material = auraMat;
                ar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ar.receiveShadows = false;
            }
        }

        // 光の粉ストリーク（円柱感をぼかす）
        var streakGo = new GameObject("GodRayStreaks");
        streakGo.transform.SetParent(raysGo.transform, false);
        var ps = streakGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 22f);
        main.startColor = coldCrisis
            ? new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.9f, 0.5f, 0.45f),
                new Color(0.75f, 0.9f, 1f, 0.15f))
            : new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.96f, 0.8f, 0.4f),
                new Color(0.9f, 0.98f, 0.75f, 0.12f));
        main.maxParticles = 180;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = ps.emission;
        emission.rateOverTime = 24f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 22f;
        shape.radius = 6f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var rend = streakGo.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Sprites/Default");
            var mat = new Material(sh);
            mat.color = coldCrisis
                ? new Color(1f, 0.88f, 0.5f, 0.55f)
                : new Color(1f, 0.95f, 0.8f, 0.5f);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", mat.color);
            rend.material = mat;
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.lengthScale = 3.5f;
            rend.velocityScale = 0.12f;
        }
        ps.Play();
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
        glowMat.color = new Color(0.95f, 0.98f, 1f, 0.32f);
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
            // BrightenScene / ClearBlueSky.mat と同じ「以前の澄んだ青空」
            // coldCrisis でも空の色は変えず、稜線・霧だけで危機感を出す
            mat.SetColor("_TopColor", new Color(0.01f, 0.24f, 0.85f, 1f));
            mat.SetColor("_MidColor", new Color(0.05f, 0.48f, 0.98f, 1f));
            mat.SetColor("_HorizonColor", new Color(0.40f, 0.75f, 0.98f, 1f));
            mat.SetColor("_GroundColor", new Color(0.22f, 0.58f, 0.90f, 1f));
            mat.SetColor("_SunColor", new Color(1.0f, 0.98f, 0.90f, 1f));
            if (mat.HasProperty("_SunSize")) mat.SetFloat("_SunSize", coldCrisis ? 0.032f : 0.035f);
            if (mat.HasProperty("_SunGlow")) mat.SetFloat("_SunGlow", coldCrisis ? 2.4f : 2.6f);
            if (mat.HasProperty("_Exponent")) mat.SetFloat("_Exponent", 0.65f);
            if (mat.HasProperty("_HorizonOffset")) mat.SetFloat("_HorizonOffset", 0.01f);
        }
        else
        {
            mat.color = new Color(0.45f, 0.72f, 1f, 1f);
        }
    }

    static void ApplySkyboxForSkybreak(bool coldCrisis)
    {
        // オープニングの映画的ファンタジースカイボックスを確実に適用
        Material skyMat = null;
#if UNITY_EDITOR
        skyMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(AdventureClassicSkyRuntime.SkyboxPath);
#endif
        if (skyMat != null)
        {
            RenderSettings.skybox = skyMat;
        }

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
                ? Quaternion.Euler(42f, 145f, 0f)
                : Quaternion.Euler(50f, 140f, 0f);
            _cachedSun.intensity = coldCrisis ? 1.55f : 1.70f;
            _cachedSun.color = coldCrisis
                ? new Color(0.96f, 0.98f, 1.0f)   // 寒冷限界：白銀の凛とした日光
                : new Color(1.0f, 0.96f, 0.82f);  // 解放後：黄金に輝く希望の日光
        }
    }

    static void SpawnLiberationDust(Transform parent, bool golden = false)
    {
        var dustGo = new GameObject(golden ? "LiberationDustGold" : "LiberationDust");
        dustGo.transform.SetParent(parent, false);
        dustGo.transform.localPosition = new Vector3(0f, 55f, 0f);

        var ps = dustGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(golden ? 0.25f : 0.35f, golden ? 1.1f : 1.4f);
        main.startColor = golden
            ? new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.92f, 0.45f, 0.65f),
                new Color(1f, 0.98f, 0.82f, 0.2f))
            : new ParticleSystem.MinMaxGradient(
                new Color(0.85f, 0.98f, 0.65f, 0.55f),
                new Color(0.95f, 1f, 0.88f, 0.18f));
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 2.2f);
        main.maxParticles = golden ? 200 : 160;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = golden ? -0.02f : 0f;

        var emission = ps.emission;
        emission.rateOverTime = golden ? 28f : 22f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = golden ? 34f : 28f;
        shape.radius = golden ? 12f : 8f;

        var colorOver = ps.colorOverLifetime;
        colorOver.enabled = true;
        var grad = new Gradient();
        if (golden)
        {
            grad.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f),
                    new GradientColorKey(new Color(1f, 0.78f, 0.35f), 1f)
                },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.75f, 0.18f),
                    new GradientAlphaKey(0f, 1f)
                });
        }
        else
        {
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
        }
        colorOver.color = grad;

        var rend = dustGo.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Sprites/Default");
            var mat = new Material(sh);
            mat.color = golden
                ? new Color(1f, 0.9f, 0.5f, 0.8f)
                : new Color(0.88f, 0.98f, 0.70f, 0.75f);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", mat.color);
            rend.material = mat;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
        }
        ps.Play();
    }

    public static IEnumerator BreakthroughFlashRoutine()
    {
        DestroyNamed("BreakthroughFlashCanvas");

        var canvasGo = new GameObject("BreakthroughFlashCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);

        // 白コア → 黄金グローの二層で「暗い箱庭から外の光へ」
        var whiteImg = CreateFullScreenImage(canvasGo.transform, "FlashWhite", new Color(1f, 1f, 1f, 0f));
        var goldImg = CreateFullScreenImage(canvasGo.transform, "FlashGold", new Color(1f, 0.88f, 0.45f, 0f));

        // 急激なホワイトアウト
        float t = 0f;
        while (t < 0.08f)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / 0.08f);
            whiteImg.color = new Color(1f, 0.99f, 0.96f, a);
            goldImg.color = new Color(1f, 0.9f, 0.5f, a * 0.55f);
            yield return null;
        }
        whiteImg.color = new Color(1f, 0.99f, 0.97f, 1f);
        goldImg.color = new Color(1f, 0.92f, 0.55f, 0.7f);
        yield return new WaitForSecondsRealtime(0.18f);

        // 黄金に溶けながら外の世界が見える
        t = 0f;
        const float fade = 1.65f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / fade);
            float soft = 1f - u;
            soft *= soft;
            whiteImg.color = new Color(1f, 0.98f, 0.92f, soft * 0.85f);
            goldImg.color = new Color(1f, 0.86f, 0.42f, soft * 0.55f);
            yield return null;
        }

        if (canvasGo != null)
            Object.Destroy(canvasGo);
    }

    static void SpawnSkybreakColdMist(Transform parent)
    {
        // 1. 漂う高高度の透明冷気ミスト
        var mistGo = new GameObject("SkybreakColdMist");
        mistGo.transform.SetParent(parent, false);
        mistGo.transform.localPosition = new Vector3(0f, 40f, 0f);

        var ps = mistGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.0f, 2.5f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.85f, 0.94f, 1f, 0.12f),
            new Color(0.70f, 0.85f, 1f, 0.05f));
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5f);
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 12f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 120f;

        var colorOver = ps.colorOverLifetime;
        colorOver.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.92f, 0.96f, 1f), 0f),
                new GradientColorKey(new Color(0.70f, 0.85f, 1f), 1f)
            },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.15f, 0.3f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOver.color = grad;

        var rend = mistGo.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Mobile/Particles/Additive");
            var mat = new Material(sh);
            mat.color = new Color(0.85f, 0.92f, 1f, 0.25f);
            rend.material = mat;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
        }
        ps.Play();

        // 2. 冷たく強力に光り輝くキラキラ粒子のダイヤモンドダスト（空間全体を覆う加算発光）
        var dustGo = new GameObject("SkybreakDiamondDust");
        dustGo.transform.SetParent(parent, false);
        dustGo.transform.localPosition = new Vector3(0f, 35f, 0f);

        var dustPs = dustGo.AddComponent<ParticleSystem>();
        var dMain = dustPs.main;
        dMain.loop = true;
        dMain.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.0f);
        dMain.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.38f); // 煌めく星屑・氷晶サイズ
        // HDR発光カラー（URP Bloomに直接乗り、まばゆくキラキラと発光）
        dMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(2.4f, 3.0f, 4.2f, 1.0f),  // 透徹なアイスブルー閃光
            new Color(3.8f, 3.8f, 4.2f, 1.0f)); // 白銀に輝くダイヤモンド光
        dMain.startSpeed = new ParticleSystem.MinMaxCurve(16f, 30f);
        dMain.maxParticles = 850;
        dMain.simulationSpace = ParticleSystemSimulationSpace.World;

        var dEmission = dustPs.emission;
        dEmission.rateOverTime = 320f; // 画面全体を覆う高密度のキラキラ粒子

        var dShape = dustPs.shape;
        dShape.shapeType = ParticleSystemShapeType.Box;
        dShape.scale = new Vector3(75f, 35f, 75f); // プレイヤーと視界全体を包み込む広がり

        // 強風の方向（斜め下方に激しく吹き抜ける）
        dustGo.transform.localRotation = Quaternion.Euler(22f, -55f, 0f);

        // キラキラ瞬くアニメーション（点滅・明滅）
        var dColorOver = dustPs.colorOverLifetime;
        dColorOver.enabled = true;
        var dGrad = new Gradient();
        dGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                new GradientColorKey(new Color(0.85f, 0.95f, 1f), 1f)
            },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1.0f, 0.15f),
                new GradientAlphaKey(0.85f, 0.80f),
                new GradientAlphaKey(0f, 1f)
            });
        dColorOver.color = dGrad;

        // 結晶の回転によるキラメキ効果
        var rot = dustPs.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-180f * Mathf.Deg2Rad, 180f * Mathf.Deg2Rad);

        var dRend = dustGo.GetComponent<ParticleSystemRenderer>();
        if (dRend != null)
        {
            // 加算合成（Additive）でNiko/Rustを一切隠さず、強力に発光
            var sh = Shader.Find("Universal Render Pipeline/Particles/Additive")
                     ?? Shader.Find("Mobile/Particles/Additive")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Sprites/Default");
            var mat = new Material(sh);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3200;
            mat.color = Color.white;
            dRend.material = mat;
            dRend.renderMode = ParticleSystemRenderMode.Billboard;
        }
        dustPs.Play();

        // 冷気ドライバーにダイヤモンドダストを登録（プレイヤー追従および雪解け消滅）
        if (AdventureColdAtmosphereDriver.Instance != null)
        {
            AdventureColdAtmosphereDriver.Instance.RegisterDiamondDust(dustGo.transform, dustPs, dRend != null ? dRend.material : null);
        }
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

        // 1. 環境フォグ＆アンビエント（手前のNiko/Rustを決して隠さず、遠景のみ冷たく澄み渡るアイスブルー）
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.70f, 0.84f, 0.98f);
        RenderSettings.fogDensity = 0.0018f;
        RenderSettings.ambientLight = new Color(0.68f, 0.80f, 0.96f);

        // 2. 太陽光の特定と極寒化（青白く冴えた光）
        if (_cachedSun == null)
            _cachedSun = RenderSettings.sun;
        if (_cachedSun == null)
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional && lights[i].isActiveAndEnabled)
                {
                    _cachedSun = lights[i];
                    break;
                }
            }
        }

        Color originalSunColor = _cachedSun != null ? _cachedSun.color : new Color(1.0f, 0.95f, 0.84f);
        float originalSunIntensity = _cachedSun != null ? _cachedSun.intensity : 1.45f;

        Color coldSunColor = new Color(0.90f, 0.95f, 1.0f);
        float coldSunIntensity = 1.75f;

        if (_cachedSun != null)
        {
            _cachedSun.color = coldSunColor;
            _cachedSun.intensity = coldSunIntensity;
        }

        // 3. URP ポストプロセス Volume（極寒の青白さ＋ダイヤモンドダストの強力な発光Bloom）
        DestroyNamed("SkybreakColdVolume");
        _coldVolumeGo = new GameObject("SkybreakColdVolume");
        _coldVolume = _coldVolumeGo.AddComponent<Volume>();
        _coldVolume.isGlobal = true;
        _coldVolume.priority = 35f;
        _coldVolume.weight = 1f;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _coldVolume.profile = profile;

        if (!profile.TryGet<WhiteBalance>(out var wb))
            wb = profile.Add<WhiteBalance>(true);
        wb.temperature.overrideState = true;
        wb.temperature.value = -35f; // 自然で澄んだ極寒色温度
        wb.tint.overrideState = true;
        wb.tint.value = -5f;

        if (!profile.TryGet<ColorAdjustments>(out var ca))
            ca = profile.Add<ColorAdjustments>(true);
        ca.saturation.overrideState = true;
        ca.saturation.value = -18f; // 凍える空気の冷涼感
        ca.contrast.overrideState = true;
        ca.contrast.value = 12f; // NikoとRustがくっきりと浮き立つコントラスト
        ca.colorFilter.overrideState = true;
        ca.colorFilter.value = new Color(0.90f, 0.95f, 1.0f);

        // ダイヤモンドダストの強力なキラキラ発光を引き出すブルーム
        if (!profile.TryGet<Bloom>(out var bloom))
            bloom = profile.Add<Bloom>(true);
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.85f;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 1.02f; // 通常オブジェクトは光らず、HDRダイヤモンドダストのみ強烈に発光
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.70f;

        if (!profile.TryGet<Vignette>(out var vig))
            vig = profile.Add<Vignette>(true);
        vig.color.overrideState = true;
        vig.color.value = new Color(0.35f, 0.58f, 0.88f); // 画面端の青白い冷気
        vig.intensity.overrideState = true;
        vig.intensity.value = 0.30f;
        vig.smoothness.overrideState = true;
        vig.smoothness.value = 0.60f;

        DestroyNamed("SkybreakFrostVignetteCanvas");

        // 4. 冷気ドライバーのアタッチ（プレイヤー追従および注油時の雪解けLerp）
        var driver = _coldVolumeGo.AddComponent<AdventureColdAtmosphereDriver>();
        driver.ColdVolume = _coldVolume;
        driver.SunLight = _cachedSun;
        driver.ColdSunColor = coldSunColor;
        driver.WarmSunColor = originalSunColor;
        driver.ColdSunIntensity = coldSunIntensity;
        driver.WarmSunIntensity = originalSunIntensity;
        driver.ColdFogColor = new Color(0.70f, 0.84f, 0.98f);
        driver.WarmFogColor = AdventureClassicSkyRuntime.FogColor;

        // すでに生成されているダイヤモンドダストがあれば登録
        var existingDust = GameObject.Find("SkybreakDiamondDust");
        if (existingDust != null)
        {
            var dps = existingDust.GetComponent<ParticleSystem>();
            var drend = existingDust.GetComponent<ParticleSystemRenderer>();
            driver.RegisterDiamondDust(existingDust.transform, dps, drend != null ? drend.material : null);
        }
    }

    public static void SoftenColdAtmosphere()
    {
        if (!_coldAtmosphereActive)
            ApplyColdAtmosphere();

        // 1. 冷気ドライバーによる滑らかな雪解け移行（2.5秒フェード）
        if (AdventureColdAtmosphereDriver.Instance != null)
        {
            AdventureColdAtmosphereDriver.Instance.StartThaw(2.5f);
        }
        else
        {
            ClearColdAtmosphere();
        }

        // 2. スカイボックスをオープニングの暖色・快晴仕様に復帰
        ApplySkyboxForSkybreak(coldCrisis: false);

        // 3. 冷気ミストの段階的停止
        var mist = GameObject.Find("SkybreakColdMist");
        if (mist != null)
        {
            var ps = mist.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var emission = ps.emission;
                emission.rateOverTime = 2f;
            }
        }
    }

    public static void ClearColdAtmosphere()
    {
        if (!_coldAtmosphereActive && _savedSkybox == null) return;

        if (AdventureColdAtmosphereDriver.Instance != null)
        {
            Object.Destroy(AdventureColdAtmosphereDriver.Instance.gameObject);
        }

        DestroyNamed("SkybreakColdVolume");
        DestroyNamed("SkybreakFrostVignetteCanvas");

        if (_coldAtmosphereActive)
        {
            RenderSettings.fog = _savedFogEnabled;
            RenderSettings.fogColor = _savedFogColor;
            RenderSettings.fogDensity = _savedFogDensity;
            RenderSettings.ambientLight = _savedAmbient;
            if (_cachedSun != null)
            {
                _cachedSun.color = new Color(1.0f, 0.95f, 0.84f);
                _cachedSun.intensity = 1.45f;
            }
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
    /// 冷気のリアルタイムプレイヤー追従および注油時の劇的な雪解けフェード制御
    /// </summary>
    private sealed class AdventureColdAtmosphereDriver : MonoBehaviour
    {
        public static AdventureColdAtmosphereDriver Instance { get; private set; }

        public Volume ColdVolume;
        public Light SunLight;
        public Color ColdSunColor;
        public Color WarmSunColor;
        public float ColdSunIntensity;
        public float WarmSunIntensity;
        public Color ColdFogColor;
        public Color WarmFogColor;

        private Transform _dustTransform;
        private ParticleSystem _dustPs;
        private Material _dustMat;

        private bool _isThawing;
        private float _thawProgress;
        private float _thawDuration = 2.5f;

        void Awake()
        {
            Instance = this;
        }

        public void RegisterDiamondDust(Transform t, ParticleSystem ps, Material mat)
        {
            _dustTransform = t;
            _dustPs = ps;
            _dustMat = mat;
        }

        void Update()
        {
            // プレイヤーまたはカメラの位置に追従し、常に視界全体をダイヤモンドダストで覆う
            if (_dustTransform != null)
            {
                var player = AdventurePlayerController.InstanceOrFind();
                Vector3 centerPos = player != null ? player.transform.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
                if (centerPos.sqrMagnitude > 1f)
                {
                    _dustTransform.position = centerPos;
                }
            }

            if (_isThawing)
            {
                // 注油回復時：ダイヤモンドダストがスッと溶けて消え、温かいオープニングの空へと回帰
                _thawProgress += Time.deltaTime / _thawDuration;
                float t = Mathf.Clamp01(_thawProgress);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                if (ColdVolume != null)
                {
                    ColdVolume.weight = 1f - smoothT;
                }

                if (_dustMat != null)
                {
                    // 粒子マテリアルの輝度を滑らかにフェードアウト
                    Color c = Color.Lerp(Color.white, Color.clear, smoothT);
                    _dustMat.color = c;
                }

                if (SunLight != null)
                {
                    SunLight.color = Color.Lerp(ColdSunColor, WarmSunColor, smoothT);
                    SunLight.intensity = Mathf.Lerp(ColdSunIntensity, WarmSunIntensity, smoothT);
                }

                RenderSettings.fogColor = Color.Lerp(ColdFogColor, WarmFogColor, smoothT);

                if (t >= 1f)
                {
                    if (_dustTransform != null)
                        Destroy(_dustTransform.gameObject);
                    Destroy(gameObject);
                }
            }
        }

        public void StartThaw(float duration = 2.5f)
        {
            _isThawing = true;
            _thawProgress = 0f;
            _thawDuration = Mathf.Max(0.1f, duration);

            if (_dustPs != null)
            {
                var em = _dustPs.emission;
                em.rateOverTime = 0f;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
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

        // ── シネマティック天蓋崩壊：幾何学ガラスクラック網 ──
        var lattice = CreateSkyCanopyLattice(root.transform, lineSh);

        // ── 天蓋衝撃波リング（水平に急拡大する光輪） ──
        var shockwave = CreateShockwaveRing(root.transform, lineSh);
        Material shockwaveMat = shockwave != null ? shockwave.GetComponent<Renderer>()?.material : null;

        // ── 割れ目から地上へ差し込む天空光芒シャフト ──
        SpawnSkybreakApertureBeams(root.transform, lineSh);

        SpawnSkyTearBurst(root.transform, particleSh, "SkyTearShards",
            new Color(0.65f, 0.95f, 1f, 0.95f), new Color(1f, 0.98f, 0.85f, 0.90f),
            speedMin: 22f, speedMax: 65f, sizeMin: 0.6f, sizeMax: 2.8f,
            gravity: 0.45f, radius: 15f,
            bursts: new[] { 140, 110, 80, 50 }, burstTimes: new[] { 0.02f, 0.28f, 0.7f, 1.35f });

        SpawnSkyTearBurst(root.transform, particleSh, "SkyTearDebris",
            new Color(0.45f, 0.88f, 1f, 0.85f), new Color(1f, 0.94f, 0.75f, 0.85f),
            speedMin: 8f, speedMax: 28f, sizeMin: 1.2f, sizeMax: 4.5f,
            gravity: 1.2f, radius: 24f,
            bursts: new[] { 45, 35 }, burstTimes: new[] { 0.15f, 0.9f });

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

        // 画面フラッシュ
        var flashGo = new GameObject("SkyTearFlashCanvas");
        var canvas = flashGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 8850;
        var scaler = flashGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        var flashImg = CreateFullScreenImage(flashGo.transform, "Flash", new Color(0.85f, 0.95f, 1f, 0f));
        var flashWhite = CreateFullScreenImage(flashGo.transform, "FlashWhite", new Color(1f, 1f, 1f, 0f));

        // 各稲妻の撃つタイミング（7秒間のリアルな連続落雷）
        float[] strikeAt = { 0.08f, 0.32f, 0.55f, 0.95f, 1.45f, 2.15f, 3.2f, 4.35f, 5.4f, 6.35f };
        float[] strikeDur = { 0.12f, 0.09f, 0.18f, 0.11f, 0.14f, 0.1f, 0.13f, 0.11f, 0.12f, 0.10f };
        var boltsExtra = new LightningBolt[4];
        for (int i = 0; i < boltsExtra.Length; i++)
        {
            float yaw = -40f + i * 28f + Random.Range(-8f, 8f);
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
        var allBolts = new LightningBolt[bolts.Length + boltsExtra.Length];
        for (int i = 0; i < bolts.Length; i++) allBolts[i] = bolts[i];
        for (int i = 0; i < boltsExtra.Length; i++) allBolts[bolts.Length + i] = boltsExtra[i];
        bolts = allBolts;
        bool[] struck = new bool[bolts.Length];
        bool[] reshaped = new bool[bolts.Length];

        float duration = 7.0f;
        float t = 0f;
        bool shookHard = false;
        bool shookMid = false;
        bool shookLate1 = false;
        bool shookLate2 = false;
        bool shookLate3 = false;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            // ── 天蓋ショックウェーブの急拡大アニメーション ──
            if (shockwave != null && shockwaveMat != null)
            {
                if (t >= 0.22f && t <= 2.2f)
                {
                    float swT = (t - 0.22f) / 1.98f;
                    float swScale = Mathf.Lerp(12f, 420f, Mathf.Sqrt(swT));
                    shockwave.localScale = new Vector3(swScale, 0.5f, swScale);
                    float swAlpha = (1f - swT) * 0.85f;
                    shockwaveMat.color = new Color(0.65f, 0.95f, 1.0f, swAlpha);
                }
                else if (t > 2.2f)
                {
                    shockwave.gameObject.SetActive(false);
                }
            }

            // ── 天空ガラス亀裂（Lattice）の鼓動発光 ──
            if (lattice.root != null && lattice.mat != null)
            {
                float latticeAlpha = Mathf.Clamp01(1f - (t / 6.5f));
                // 稲妻放電に合わせて亀裂がバチバチと明滅
                float flicker = 0.75f + Mathf.Sin(t * 32f) * 0.25f;
                lattice.mat.color = new Color(0.6f, 0.92f, 1.0f, latticeAlpha * flicker);
                if (lattice.centerGlow != null)
                    lattice.centerGlow.intensity = latticeAlpha * flicker * 12f;
            }

            for (int i = 0; i < bolts.Length; i++)
            {
                float start = strikeAt[i];
                float end = start + strikeDur[i];
                if (!reshaped[i] && t >= start - 0.02f)
                {
                    reshaped[i] = true;
                    ReshapeLightningBolt(bolts[i]);
                }

                bool on = t >= start && t <= end;
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
            flashA += SkyTearFlashEnvelope(t, 2.6f, 1.2f, 0.14f);
            flashA += SkyTearFlashEnvelope(t, 4.0f, 1.4f, 0.18f);
            flashA += SkyTearFlashEnvelope(t, 5.2f, 0.9f, 0.12f);
            flashA += SkyTearFlashEnvelope(t, 6.3f, 0.8f, 0.10f);
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
            if (!shookLate3 && t >= 5.5f)
            {
                shookLate3 = true;
                if (cam != null) cam.Shake(0.4f, 1.5f);
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

    struct SkyCanopyLattice
    {
        public GameObject root;
        public LineRenderer[] lines;
        public Material mat;
        public Light centerGlow;
    }

    /// <summary>天空全体に網の目のように広がる巨大幾何学ガラス亀裂ネットワーク</summary>
    static SkyCanopyLattice CreateSkyCanopyLattice(Transform parent, Shader lineShader)
    {
        var latticeGo = new GameObject("SkyCanopyLattice");
        latticeGo.transform.SetParent(parent, false);
        latticeGo.transform.localPosition = new Vector3(0f, 35f, 0f);

        var mat = new Material(lineShader);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        mat.renderQueue = 3200;
        mat.color = new Color(0.65f, 0.95f, 1.0f, 0.95f);

        var lineList = new System.Collections.Generic.List<LineRenderer>();
        Vector3 center = Vector3.zero;

        // 1. 放射状の主亀裂（10本）
        int mainBranches = 10;
        var branchEnds = new Vector3[mainBranches];
        for (int b = 0; b < mainBranches; b++)
        {
            float ang = b * (360f / mainBranches) + Random.Range(-8f, 8f);
            float rad = Random.Range(140f, 210f);
            Vector3 target = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad) * rad, Random.Range(8f, 28f), Mathf.Sin(ang * Mathf.Deg2Rad) * rad);
            branchEnds[b] = target;

            var go = new GameObject($"LatticeMain_{b}");
            go.transform.SetParent(latticeGo.transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = mat;
            lr.startWidth = 2.8f;
            lr.endWidth = 0.5f;
            lr.useWorldSpace = false;
            lr.positionCount = 8;

            for (int p = 0; p < 8; p++)
            {
                float frac = (float)p / 7f;
                Vector3 pt = Vector3.Lerp(center, target, frac);
                if (p > 0 && p < 7)
                {
                    pt += new Vector3(Random.Range(-6f, 6f), Random.Range(-3f, 3f), Random.Range(-6f, 6f));
                }
                lr.SetPosition(p, pt);
            }
            lineList.Add(lr);
        }

        // 2. 環状・同心円の連結亀裂（クモの巣状・幾何学破砕グリッド）
        for (int ring = 1; ring <= 3; ring++)
        {
            float ringFrac = (float)ring / 3.2f;
            for (int b = 0; b < mainBranches; b++)
            {
                int nextB = (b + 1) % mainBranches;
                Vector3 p1 = Vector3.Lerp(center, branchEnds[b], ringFrac);
                Vector3 p2 = Vector3.Lerp(center, branchEnds[nextB], ringFrac);

                var go = new GameObject($"LatticeRing_{ring}_{b}");
                go.transform.SetParent(latticeGo.transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.material = mat;
                lr.startWidth = 1.4f * (1.1f - ringFrac * 0.5f);
                lr.endWidth = 1.1f * (1.1f - ringFrac * 0.5f);
                lr.useWorldSpace = false;
                lr.positionCount = 4;
                lr.SetPosition(0, p1);
                lr.SetPosition(1, Vector3.Lerp(p1, p2, 0.33f) + Random.insideUnitSphere * 3.5f);
                lr.SetPosition(2, Vector3.Lerp(p1, p2, 0.66f) + Random.insideUnitSphere * 3.5f);
                lr.SetPosition(3, p2);
                lineList.Add(lr);
            }
        }

        var lightGo = new GameObject("LatticeGlow");
        lightGo.transform.SetParent(latticeGo.transform, false);
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.6f, 0.92f, 1f);
        l.range = 240f;
        l.intensity = 0f;

        return new SkyCanopyLattice
        {
            root = latticeGo,
            lines = lineList.ToArray(),
            mat = mat,
            centerGlow = l
        };
    }

    /// <summary>天蓋が割れた瞬間に水平に急拡大する光の衝撃波リング</summary>
    static Transform CreateShockwaveRing(Transform parent, Shader unlit)
    {
        var ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ringGo.name = "SkyTearShockwave";
        ringGo.transform.SetParent(parent, false);
        ringGo.transform.localPosition = new Vector3(0f, 25f, 0f);
        ringGo.transform.localScale = new Vector3(12f, 0.4f, 12f);
        Object.Destroy(ringGo.GetComponent<Collider>());

        var mat = new Material(unlit);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        mat.renderQueue = 3150;
        mat.color = new Color(0.70f, 0.95f, 1.0f, 0.85f);

        var r = ringGo.GetComponent<Renderer>();
        if (r != null)
        {
            r.material = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        return ringGo.transform;
    }

    /// <summary>割れ目の中心から地上（タワー）へ真っ直ぐ降り注ぐ天空光芒シャフト</summary>
    static void SpawnSkybreakApertureBeams(Transform parent, Shader unlit)
    {
        // プレイヤーNiko・Rustを内部に包み込んで遮蔽する巨大シリンダーは生成しない
    }

    /// <summary>
    /// 後続の台本表示中（ありがとうRust〜光の柱へ）に空の裂け目から走るミニ裂開パルス演出。
    /// pulses > 1 で複数回の持続パルス（波状落雷・明滅）を発生させる。
    /// </summary>
    public static IEnumerator PlaySkyTearMiniPulseRoutine(float shakeIntensity = 0.25f, int pulses = 1, float totalDuration = 0.4f)
    {
        var cam = AdventureCameraFollow.InstanceOrFind();

        var flashGo = new GameObject("SkyTearMiniFlashCanvas");
        var canvas = flashGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 8850;
        var scaler = flashGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        var flashImg = CreateFullScreenImage(flashGo.transform, "MiniFlash", new Color(0.85f, 0.95f, 1f, 0f));

        if (pulses <= 1)
        {
            if (cam != null) cam.Shake(shakeIntensity, 0.45f);
            float t = 0f;
            const float peak = 0.05f;
            while (t < peak)
            {
                t += Time.unscaledDeltaTime;
                flashImg.color = new Color(0.85f, 0.95f, 1f, Mathf.Clamp01(t / peak) * 0.25f);
                yield return null;
            }
            t = 0f;
            const float fade = 0.35f;
            while (t < fade)
            {
                t += Time.unscaledDeltaTime;
                float a = 1f - Mathf.Clamp01(t / fade);
                flashImg.color = new Color(0.85f, 0.95f, 1f, a * 0.25f);
                yield return null;
            }
        }
        else
        {
            // 持続マルチパルス（指定時間内に複数回の閃光・揺れが波状に持続）
            float interval = totalDuration / pulses;
            for (int p = 0; p < pulses; p++)
            {
                if (cam != null) cam.Shake(shakeIntensity * (p == 0 ? 1f : 0.85f), interval * 0.7f);
                float pt = 0f;
                float pulsePeak = Mathf.Min(0.06f, interval * 0.22f);
                while (pt < pulsePeak)
                {
                    pt += Time.unscaledDeltaTime;
                    flashImg.color = new Color(0.85f, 0.95f, 1f, Mathf.Clamp01(pt / pulsePeak) * 0.27f);
                    yield return null;
                }
                pt = 0f;
                float pulseFade = interval - pulsePeak;
                while (pt < pulseFade)
                {
                    pt += Time.unscaledDeltaTime;
                    float a = 1f - Mathf.Clamp01(pt / pulseFade);
                    flashImg.color = new Color(0.85f, 0.95f, 1f, a * 0.27f);
                    yield return null;
                }
            }
        }

        if (flashGo != null)
            Object.Destroy(flashGo);
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
