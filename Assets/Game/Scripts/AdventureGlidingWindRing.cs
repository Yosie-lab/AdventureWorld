using UnityEngine;

/// <summary>
/// 『Rust & Float』滑空加速リング（Gliding Wind Ring）
/// 空中に浮かぶ古代の風のリング。滑空中にくぐり抜けると
/// 「シュオォォン！」と風切りブーストと上昇リフトが発生し、爽快に空中を疾走できる。
/// 連続でくぐることでコンボ音（ピッチ上昇）が鳴り、高台やパーツ地点へ到達できる。
/// </summary>
public class AdventureGlidingWindRing : MonoBehaviour
{
    public float ringRadius = 1.85f;
    public float tubeRadius = 0.12f;
    public float boostMultiplier = 1.45f;
    public float boostDuration = 3.2f;
    public Color normalColor = new Color(0.25f, 0.85f, 1.0f, 0.85f);
    public Color boostColor = new Color(1.0f, 0.95f, 0.40f, 1.0f);

    private static int _comboCount = 0;
    private static float _lastRingPassTime = -10f;

    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Material _ringMaterial;
    private AudioSource _audioSource;
    private ParticleSystem _sparklePs;
    private bool _isCoolingDown = false;
    private float _cooldownTimer = 0f;

    void Awake()
    {
        BuildRingMesh();
        SetupCollider();
        SetupAudio();
        SetupParticles();
    }

    void Update()
    {
        // ゆっくりと優雅に自転・呼吸
        float breathe = 1.0f + Mathf.Sin(Time.time * 2.5f) * 0.04f;
        transform.localScale = Vector3.one * breathe;

        if (_isCoolingDown)
        {
            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f)
            {
                _isCoolingDown = false;
                ResetVisual();
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_isCoolingDown) return;

        var player = other.GetComponent<AdventurePlayerController>()
                     ?? other.GetComponentInParent<AdventurePlayerController>();
        if (player == null) return;

        Vector3 forward = transform.forward;

        // 滑空状態でなくても、リングに触れれば自動で滑空ブースト射出！
        PassThroughRing(player, forward);
    }

    public void PassThroughRing(AdventurePlayerController player, Vector3 forwardDir)
    {
        _isCoolingDown = true;
        _cooldownTimer = 2.0f; // 2秒間クールダウン

        // コンボ判定（直前のリング通過から5秒以内ならコンボ加算）
        if (Time.time - _lastRingPassTime < 5.0f)
        {
            _comboCount = Mathf.Min(_comboCount + 1, 5);
        }
        else
        {
            _comboCount = 1;
        }
        _lastRingPassTime = Time.time;

        // プレイヤーに滑空加速を適用
        player.ApplyGlideBoost(boostMultiplier, boostDuration, forwardDir);

        // 音響再生（コンボに応じてピッチが上がる）
        if (_audioSource != null)
        {
            float pitch = 1.0f + (_comboCount - 1) * 0.15f;
            _audioSource.pitch = pitch;
            _audioSource.Play();
        }

        // パーティクル放出
        if (_sparklePs != null)
        {
            _sparklePs.Emit(24);
        }

        // ビジュアル発光フラッシュ
        if (_ringMaterial != null)
        {
            _ringMaterial.SetColor("_BaseColor", boostColor);
            _ringMaterial.SetColor("_EmissionColor", boostColor * 2.8f);
        }
    }

    void ResetVisual()
    {
        if (_ringMaterial != null)
        {
            _ringMaterial.SetColor("_BaseColor", normalColor);
            _ringMaterial.SetColor("_EmissionColor", normalColor * 1.5f);
        }
    }

    void BuildRingMesh()
    {
        _meshFilter = gameObject.AddComponent<MeshFilter>();
        _meshRenderer = gameObject.AddComponent<MeshRenderer>();

        // トーラス（ドーナツ型円環）メッシュをプロシージャル生成
        Mesh mesh = CreateTorusMesh(ringRadius, tubeRadius, 32, 12);
        mesh.name = "GlidingWindRingMesh";
        _meshFilter.sharedMesh = mesh;

        // 加算発光シェーダー
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Unlit/Color");
        _ringMaterial = new Material(shader);
        _ringMaterial.SetColor("_BaseColor", normalColor);
        _ringMaterial.SetColor("_EmissionColor", normalColor * 1.5f);
        _ringMaterial.EnableKeyword("_EMISSION");
        _meshRenderer.sharedMaterial = _ringMaterial;
        _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void SetupCollider()
    {
        // 中央通過用の薄型ボックス（またはカプセル）トリガー
        var box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector2(ringRadius * 2.2f, ringRadius * 2.2f);
        box.size = new Vector3(ringRadius * 2.2f, ringRadius * 2.2f, 1.2f);
    }

    void SetupAudio()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0.5f;
        _audioSource.minDistance = 5f;
        _audioSource.maxDistance = 45f;
        _audioSource.volume = 0.85f;

        _audioSource.clip = CreateWindChimeClip();
    }

    void SetupParticles()
    {
        var psGo = new GameObject("Sparkles");
        psGo.transform.SetParent(transform, false);
        psGo.transform.localPosition = Vector3.zero;

        _sparklePs = psGo.AddComponent<ParticleSystem>();
        var main = _sparklePs.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 1.0f;
        main.startSpeed = 8.0f;
        main.startSize = 0.28f;
        main.startColor = new Color(0.4f, 0.95f, 1f, 0.9f);

        var shape = _sparklePs.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = ringRadius;

        var vel = _sparklePs.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.z = new ParticleSystem.MinMaxCurve(6f, 12f); // 前方に吹き出す

        var renderer = psGo.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = _ringMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    static Mesh CreateTorusMesh(float rTorus, float rTube, int radialSegments, int tubeSegments)
    {
        Mesh mesh = new Mesh();
        int numVertices = (radialSegments + 1) * (tubeSegments + 1);
        Vector3[] vertices = new Vector3[numVertices];
        Vector3[] normals = new Vector3[numVertices];
        Vector2[] uvs = new Vector2[numVertices];
        int[] triangles = new int[radialSegments * tubeSegments * 6];

        for (int i = 0; i <= radialSegments; i++)
        {
            float u = (float)i / radialSegments * Mathf.PI * 2f;
            Vector3 center = new Vector3(Mathf.Cos(u) * rTorus, Mathf.Sin(u) * rTorus, 0f);

            for (int j = 0; j <= tubeSegments; j++)
            {
                float v = (float)j / tubeSegments * Mathf.PI * 2f;
                Vector3 normal = new Vector3(Mathf.Cos(u) * Mathf.Cos(v), Mathf.Sin(u) * Mathf.Cos(v), Mathf.Sin(v));
                Vector3 vertex = center + normal * rTube;

                int index = i * (tubeSegments + 1) + j;
                vertices[index] = vertex;
                normals[index] = normal;
                uvs[index] = new Vector2((float)i / radialSegments, (float)j / tubeSegments);
            }
        }

        int triIndex = 0;
        for (int i = 0; i < radialSegments; i++)
        {
            for (int j = 0; j < tubeSegments; j++)
            {
                int current = i * (tubeSegments + 1) + j;
                int next = (i + 1) * (tubeSegments + 1) + j;

                triangles[triIndex++] = current;
                triangles[triIndex++] = next;
                triangles[triIndex++] = next + 1;

                triangles[triIndex++] = current;
                triangles[triIndex++] = next + 1;
                triangles[triIndex++] = current + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    static AudioClip CreateWindChimeClip()
    {
        const int rate = 44100;
        const float duration = 0.75f;
        int count = (int)(rate * duration);
        float[] samples = new float[count];

        // 爽快な風切りホワイトノイズ + クリスタル和音（E5, G#5, B5, E6）
        float[] chord = { 659.25f, 830.61f, 987.77f, 1318.51f };

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float env = Mathf.Exp(-t * 5.5f);

            // 風の突風スウィッシュ成分
            float whoosh = (UnityEngine.Random.value * 2f - 1f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.35f)) * 0.35f;

            // 澄んだチャイム音
            float tone = 0f;
            for (int c = 0; c < chord.Length; c++)
            {
                float f = chord[c];
                tone += Mathf.Sin(2f * Mathf.PI * f * t) * (0.15f / (c + 1));
            }

            samples[i] = (whoosh + tone) * env * 0.85f;
        }

        var clip = AudioClip.Create("GlidingRingBoostSfx", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
