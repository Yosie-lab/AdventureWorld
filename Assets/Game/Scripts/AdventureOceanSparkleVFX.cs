using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// 海の波に太陽光が反射してキラキラと輝く「スカッとする海のサン・グリッター（波のきらめき）」VFXマネージャー。
/// ・プレイヤーやカメラの視界に入る海面全体に、ダイヤモンドダストのような眩しい水面のきらめきを動的展開。
/// ・白砂ビーチから見渡す海、高空から見下ろす滑空視点でも、海面全体が太陽に照らされて美しく煌めく。
/// </summary>
[ExecuteAlways]
public class AdventureOceanSparkleVFX : MonoBehaviour
{
    private static AdventureOceanSparkleVFX _instance;
    public static AdventureOceanSparkleVFX Instance => _instance;

    public const string SparkleGameObjectName = "Ocean_Sun_Sparkles_Root";
    private const string MaterialPath = "Assets/RustAndFloat/Materials/OceanSunGlitter.mat";

    private ParticleSystem _particleSystem;
    private ParticleSystemRenderer _particleRenderer;
    private Transform _targetTransform;
    private bool _liberationBoost;

    const float BaseEmissionRate = 220f;
    const float BaseShapeX = 220f;
    const int BaseMaxParticles = 600;
    const float LiberationEmissionRate = 520f;
    const float LiberationShapeX = 480f;
    const int LiberationMaxParticles = 1400;

    public const float OceanSurfaceY = 5.54f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        Ensure();
    }

    public static AdventureOceanSparkleVFX Ensure()
    {
        if (_instance != null)
        {
            _instance.SetupSparkleSystemIfNeeded();
            return _instance;
        }

        var existing = Object.FindAnyObjectByType<AdventureOceanSparkleVFX>();
        if (existing != null)
        {
            _instance = existing;
            _instance.SetupSparkleSystemIfNeeded();
            return _instance;
        }

        var sceneRoot = GameObject.Find(SparkleGameObjectName);
        GameObject go;
        if (sceneRoot != null)
        {
            go = sceneRoot;
            _instance = go.GetComponent<AdventureOceanSparkleVFX>() ?? go.AddComponent<AdventureOceanSparkleVFX>();
        }
        else
        {
            go = new GameObject("AdventureOceanSparkleVFX");
            _instance = go.AddComponent<AdventureOceanSparkleVFX>();
        }

        if (Application.isPlaying)
            Object.DontDestroyOnLoad(go);

        _instance.SetupSparkleSystemIfNeeded();
        return _instance;
    }

    public void SetLiberationBoost(bool enabled)
    {
        _liberationBoost = enabled;
        if (_particleSystem == null)
            SetupSparkleSystemIfNeeded();
        ApplyEmissionProfile();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
            return;
        }
        _instance = this;
    }

    private void OnEnable()
    {
        SetupSparkleSystemIfNeeded();
    }

    private void Update()
    {
        UpdatePositionAndIntensity();
    }

    private void SetupSparkleSystemIfNeeded()
    {
        if (_particleSystem != null) return;

        var existingRoot = transform.Find("OceanSparkleParticles");
        GameObject pGo;
        if (existingRoot != null)
        {
            pGo = existingRoot.gameObject;
        }
        else
        {
            pGo = new GameObject("OceanSparkleParticles");
            pGo.transform.SetParent(transform, false);
        }

        _particleSystem = pGo.GetComponent<ParticleSystem>();
        if (_particleSystem == null) _particleSystem = pGo.AddComponent<ParticleSystem>();
        _particleRenderer = pGo.GetComponent<ParticleSystemRenderer>();

        EnsureGlitterMaterial();

        var main = _particleSystem.main;
        main.loop = true;
        main.startLifetime = 1.4f;
        main.startSpeed = 0.05f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
        main.startColor = new Color(1f, 1f, 0.98f, 0.95f);
        main.maxParticles = BaseMaxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;

        var col = _particleSystem.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.95f, 0.98f, 1.0f), 0.5f),
                new GradientColorKey(new Color(1f, 0.95f, 0.85f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1.0f, 0.4f),
                new GradientAlphaKey(0.85f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = grad;

        var sz = _particleSystem.sizeOverLifetime;
        sz.enabled = true;
        var curve = new AnimationCurve();
        curve.AddKey(0f, 0.15f);
        curve.AddKey(0.35f, 1.0f);
        curve.AddKey(0.70f, 0.85f);
        curve.AddKey(1f, 0.05f);
        sz.size = new ParticleSystem.MinMaxCurve(1f, curve);

        // キラキラ点滅（Noise / 回転）
        var rot = _particleSystem.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-45f, 45f);

        ApplyEmissionProfile();

        if (!_particleSystem.isPlaying)
            _particleSystem.Play();
    }

    void EnsureGlitterMaterial()
    {
        if (_particleRenderer == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            var glitterMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (glitterMat != null)
                _particleRenderer.sharedMaterial = glitterMat;
            return;
        }
#endif
        if (_particleRenderer.sharedMaterial != null) return;

        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                 ?? Shader.Find("Universal Render Pipeline/Particles/Additive")
                 ?? Shader.Find("Sprites/Default");
        var mat = new Material(sh);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3100;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", Color.white);
        else
            mat.color = Color.white;
        _particleRenderer.material = mat;
    }

    void ApplyEmissionProfile()
    {
        if (_particleSystem == null) return;

        float rate = _liberationBoost ? LiberationEmissionRate : BaseEmissionRate;
        float span = _liberationBoost ? LiberationShapeX : BaseShapeX;
        int maxP = _liberationBoost ? LiberationMaxParticles : BaseMaxParticles;

        var main = _particleSystem.main;
        main.maxParticles = maxP;

        var emission = _particleSystem.emission;
        emission.rateOverTime = rate;

        var shape = _particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(span, 0.25f, span);
    }

    private void UpdatePositionAndIntensity()
    {
        if (_targetTransform == null)
        {
            var cam = Camera.main;
            if (cam != null) _targetTransform = cam.transform;
            else
            {
                var player = GameObject.Find("Niko");
                if (player != null) _targetTransform = player.transform;
            }
        }

        Vector3 targetPos = _targetTransform != null ? _targetTransform.position : new Vector3(200f, 10f, 280f);
        // 海面高さ (Y=5.54m) に追従（水平面XZのみプレイヤー・カメラに追従）
        transform.position = new Vector3(targetPos.x, OceanSurfaceY, targetPos.z);
    }

#if UNITY_EDITOR
    [MenuItem("Adventure/✨ Ensure Ocean Sun Sparkles (海の波のキラキラ煌めきを一括配置)", false, 17)]
    public static void EditorSetupOceanSparkles()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("停止してください", "■で再生を止めてから実行してください。", "OK");
            return;
        }

        var existing = GameObject.Find(SparkleGameObjectName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        var root = new GameObject(SparkleGameObjectName);
        root.AddComponent<AdventureOceanSparkleVFX>();

        // さらに、外周の主要ビーチ（西海岸、南海岸、東海岸、北海岸）の4大エリアに常設の広域グリッターエミッターを設置
        SetupPermanentBeachGlitters(root.transform);

        Undo.RegisterCreatedObjectUndo(root, "Ensure Ocean Sun Sparkles");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log("✨ 【Ocean Sun Sparkles】海に波がキラキラして輝くスカッとするビジュアルの配置＆シーン保存が完了しました！");
    }

    private static void SetupPermanentBeachGlitters(Transform parent)
    {
        var glitterMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        // 4大エリアの海面アンカー（中心, 幅X, 幅Z）
        (Vector3 pos, Vector3 size)[] sectors = new[]
        {
            // 西の白砂ビーチ沖合（座礁艇〜大草原前）
            (new Vector3(135f, OceanSurfaceY, 280f), new Vector3(70f, 0.2f, 180f)),
            // 南のビーチ・河口沖合
            (new Vector3(320f, OceanSurfaceY, 135f), new Vector3(180f, 0.2f, 70f)),
            // 東の森林ビーチ沖合
            (new Vector3(720f, OceanSurfaceY, 360f), new Vector3(80f, 0.2f, 180f)),
            // 北の滑空崖下・大海洋
            (new Vector3(500f, OceanSurfaceY, 780f), new Vector3(260f, 0.2f, 90f))
        };

        for (int i = 0; i < sectors.Length; i++)
        {
            var (pos, size) = sectors[i];
            var sectorGo = new GameObject($"PermanentGlitter_Sector_{i + 1}");
            sectorGo.transform.SetParent(parent, false);
            sectorGo.transform.position = pos;

            var ps = sectorGo.AddComponent<ParticleSystem>();
            var psr = sectorGo.GetComponent<ParticleSystemRenderer>();
            if (glitterMat != null) psr.sharedMaterial = glitterMat;

            var main = ps.main;
            main.loop = true;
            main.startLifetime = 1.6f;
            main.startSpeed = 0.06f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.40f, 0.95f);
            main.startColor = new Color(1f, 1f, 0.98f, 0.95f);
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 120f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.98f, 0.88f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1.0f, 0.45f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = grad;

            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            var curve = new AnimationCurve();
            curve.AddKey(0f, 0.2f);
            curve.AddKey(0.5f, 1.0f);
            curve.AddKey(1f, 0.1f);
            sz.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }
    }
#endif
}
