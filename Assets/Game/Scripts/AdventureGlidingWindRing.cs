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
    [Tooltip("この距離以内でリング中心へ吸引（半径の倍数）")]
    public float magnetRadiusMultiplier = 5.0f;
    public float magnetAxialHalfLength = 16f;
    public float magnetPullSpeed = 28f;
    public float magnetForwardPush = 18f;
    [Tooltip("リング開口の通過判定厚み（local Z）")]
    public float gateHalfDepth = 0.95f;

    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Material _ringMaterial;
    private AudioSource _audioSource;
    private ParticleSystem _sparklePs;
    private bool _isCoolingDown = false;
    private float _cooldownTimer = 0f;
    private bool _visualBuilt = false;

    void Awake()
    {
        // 未起動時に SetActive(false) されると Start が走らずリングが透明のままになるため Awake で構築
        EnsureVisualBuilt();
    }

    void Start()
    {
        EnsureVisualBuilt();
    }

    public void EnsureVisualBuilt()
    {
        if (_visualBuilt) return;
        _visualBuilt = true;
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

    void LateUpdate()
    {
        // プレイヤー移動の後に吸引（同一フレームで Move が打ち消されないように）
        TickRingMagnet();
    }

    void TickRingMagnet()
    {
        if (_isCoolingDown) return;

        var player = AdventurePlayerController.Instance
                     ?? Object.FindAnyObjectByType<AdventurePlayerController>();
        if (player == null || player.IsGrounded) return;

        bool inGate = IsPlayerInsideGate(player);

        float magnetRadial = ringRadius * magnetRadiusMultiplier;
        Vector3 center = transform.position;
        Vector3 forward = transform.forward.normalized;
        Vector3 playerPos = player.transform.position;

        Vector3 toPlayer = playerPos - center;
        float alongForward = Vector3.Dot(toPlayer, forward);
        Vector3 axisPoint = center + forward * alongForward;
        Vector3 toAxis = axisPoint - playerPos;
        float radialDist = toAxis.magnitude;
        float dist3D = toPlayer.magnitude;

        // 旧: dist3D のみ → 進行方向から近づくと圏外になる。円柱＋球で判定
        bool inCylinder = radialDist <= magnetRadial && Mathf.Abs(alongForward) <= magnetAxialHalfLength;
        bool inSphere = dist3D <= magnetRadial;
        if (!inCylinder && !inSphere) return;

        Vector3 toHole = center - playerPos;
        float holeDist = toHole.magnitude;
        float urgency = 1f - Mathf.Clamp01(holeDist / Mathf.Max(magnetRadial, 0.01f));
        urgency = Mathf.Pow(urgency, 0.55f) + 0.42f;

        float dt = Time.deltaTime;
        Vector3 pull = Vector3.zero;
        if (holeDist > 0.12f)
        {
            float centerPull = inGate ? 0.22f : 1f;
            pull += toHole.normalized * (magnetPullSpeed * urgency * centerPull * dt);
        }

        // 開口付近は中心へ寄せつつ、進行方向へ流す（逆向き押し戻しを避ける）
        if (radialDist <= ringRadius * 2.2f)
        {
            Vector3 through = ResolveBoostDirection(player);
            pull += through * (magnetForwardPush * urgency * dt);
            if (inGate)
                pull *= 0.35f;
        }

        if (pull.sqrMagnitude > 0.000001f)
            player.ApplyRingMagnetPull(pull);

        if (inGate)
            PassThroughRing(player);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_isCoolingDown) return;

        var player = other.GetComponent<AdventurePlayerController>()
                     ?? other.GetComponentInParent<AdventurePlayerController>();
        if (player == null || player.IsGrounded) return;
        if (!IsPlayerInsideGate(player)) return;

        PassThroughRing(player);
    }

    bool IsPlayerInsideGate(AdventurePlayerController player)
    {
        float gateRadius = ringRadius * 1.02f;
        Vector3 p = player.transform.position;
        Vector3[] samples =
        {
            p + Vector3.up * 0.45f,
            p + Vector3.up * 1.05f,
            p + Vector3.up * 1.55f,
        };

        for (int i = 0; i < samples.Length; i++)
        {
            Vector3 local = transform.InverseTransformPoint(samples[i]);
            float radial = Mathf.Sqrt(local.x * local.x + local.y * local.y);
            if (radial <= gateRadius && Mathf.Abs(local.z) <= gateHalfDepth)
                return true;
        }

        return false;
    }

    Vector3 ResolveBoostDirection(AdventurePlayerController player)
    {
        Vector3 through = transform.forward;
        through.y = 0f;
        if (through.sqrMagnitude < 0.001f)
            through = player.transform.forward;
        through.Normalize();

        Vector3 face = player.transform.forward;
        face.y = 0f;
        if (face.sqrMagnitude > 0.001f)
        {
            face.Normalize();
            if (Vector3.Dot(through, face) < 0f)
                through = -through;
        }
        else
        {
            Vector3 fromRing = player.transform.position - transform.position;
            fromRing.y = 0f;
            if (fromRing.sqrMagnitude > 0.001f && Vector3.Dot(through, fromRing.normalized) < 0f)
                through = -through;
        }

        return through;
    }

    public void PassThroughRing(AdventurePlayerController player)
    {
        if (_isCoolingDown || player == null) return;

        _isCoolingDown = true;
        _cooldownTimer = 1.6f;

        int comboCount = AdventureWindRingCombo.RegisterPass();

        Vector3 through = ResolveBoostDirection(player);
        float liftHop = 1.15f + (comboCount - 1) * 0.12f;
        player.ApplyWindRingPassBoost(boostMultiplier, boostDuration, through, liftHop);
        player.NudgeThroughRingGate(through * 2.1f);

        PlayPassBoostSound(comboCount);

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

        var drone = AdventureRustDrone.Instance ?? Object.FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
            drone.GrantWindRingOil(comboCount);
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
        // 球だと手前で誤爆しやすい。開口に合わせた薄いボックスのみ
        var gate = gameObject.AddComponent<BoxCollider>();
        gate.isTrigger = true;
        float d = ringRadius * 2.05f;
        gate.size = new Vector3(d, d, gateHalfDepth * 2f);
        gate.center = Vector3.zero;
    }

    void SetupAudio()
    {
        if (_audioSource != null) return;

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;
        _audioSource.volume = 1f;
        _audioSource.dopplerLevel = 0f;
    }

    void PlayPassBoostSound(int comboCount)
    {
        EnsureVisualBuilt();
        if (_audioSource == null)
            SetupAudio();
        if (_audioSource == null) return;

        var clip = AdventureWindRing.GetSharedPassBoostClip();
        if (clip == null) return;

        float pitch = 1.0f + (comboCount - 1) * 0.12f;
        float seVol = PlayerPrefs.GetFloat("Adventure_SeVolume", 1f);
        _audioSource.spatialBlend = 0f;
        _audioSource.pitch = pitch;
        _audioSource.PlayOneShot(clip, 0.92f * seVol);
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

}
