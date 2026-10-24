using UnityEngine;
using System.Collections;

/// <summary>
/// 滑空中にくぐると加速ブーストがかかる「風のリング」
/// エメラルドグリーンに輝く巨大な気流トンネル（直径約7m）
/// </summary>
public class AdventureWindRing : MonoBehaviour
{
    public float boostMultiplier = 1.95f;
    public float boostDuration = 3.5f;
    public Color ringColor = new Color(0.15f, 1.0f, 0.70f); // 鮮烈なエメラルドシアン

    public static event System.Action<AdventureWindRing, AdventurePlayerController> OnRingPassed;

    Transform _ringVisual;
    Material _ringMat;
    bool _isCooldown = false;
    AudioSource _audio;
    static AudioClip _boostClip;

    /// <summary>地上・空中リング共通の通過SE（プロシージャル合成）</summary>
    public static AudioClip GetSharedPassBoostClip()
    {
        if (_boostClip == null)
            _boostClip = SynthesizeWindBoostClip();
        return _boostClip;
    }

    void Start()
    {
        CreateRingVisual();
        RemoveExistingBeaconPillar();
        SetupAudio();

        if (GetComponent<SphereCollider>() == null)
        {
            var col = gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 4.2f;
        }

        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    public void CreateRingVisual()
    {
        if (transform.Find("RingVisual") != null)
        {
            var oldVisual = transform.Find("RingVisual");
            if (Application.isPlaying)
                Destroy(oldVisual.gameObject);
            else
                DestroyImmediate(oldVisual.gameObject);
        }

        var visualGo = new GameObject("RingVisual");
        visualGo.transform.SetParent(transform, false);
        _ringVisual = visualGo.transform;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        _ringMat = new Material(shader);
        _ringMat.SetColor("_BaseColor", ringColor);
        _ringMat.EnableKeyword("_EMISSION");
        _ringMat.SetColor("_EmissionColor", ringColor * 3.2f); // 昼間でも美しく際立つ発光
        _ringMat.SetFloat("_Metallic", 0.5f);
        _ringMat.SetFloat("_Smoothness", 0.95f);

        // 継ぎ目やカクカクのない滑らかな真円トーラス（外径約3.8m、太さ0.24m）
        var mf = visualGo.AddComponent<MeshFilter>();
        var mr = visualGo.AddComponent<MeshRenderer>();
        mf.sharedMesh = CreateTorusMesh(3.8f, 0.24f, 48, 16);
        mr.sharedMaterial = _ringMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // 既存パーティクルがあれば再生成（繊細でシンプルな気流に調整）
        var existingPs = transform.Find("RingParticles");
        if (existingPs != null)
        {
            if (Application.isPlaying)
                Destroy(existingPs.gameObject);
            else
                DestroyImmediate(existingPs.gameObject);
        }

        var pGo = new GameObject("RingParticles");
        pGo.transform.SetParent(transform, false);
        var ps = pGo.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.startLifetime = 1.2f;
        main.startSpeed = 3.8f;
        main.startSize = 0.25f; // 主役のリングを邪魔しない繊細な光粒
        main.startColor = new Color(0.35f, 1.0f, 0.88f, 0.6f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 18f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 3.8f;

        var rendPs = pGo.GetComponent<ParticleSystemRenderer>();
        if (rendPs != null)
        {
            var pShader = Shader.Find("RustAndFloat/WhiteSmoke") ?? Shader.Find("Sprites/Default");
            var pMat = new Material(pShader);
            pMat.SetTexture("_BaseMap", AdventureRustDrone.GetSoftSmokeTexture());
            pMat.SetColor("_BaseColor", ringColor * 1.8f);
            rendPs.material = pMat;
            rendPs.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    /// <summary>滑らかで継ぎ目のない真円トーラス（ドーナツ型）メッシュを生成</summary>
    public static Mesh CreateTorusMesh(float rTorus, float rTube, int radialSegments, int tubeSegments)
    {
        Mesh mesh = new Mesh();
        mesh.name = "SmoothWindRingMesh";
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

    /// <summary>
    /// 光の輪（風のリング）上空のビーコン柱は他のランドマーク光柱との混同を防ぐため削除
    /// </summary>
    public void CreateBeaconPillar()
    {
        RemoveExistingBeaconPillar();
    }

    public void RemoveExistingBeaconPillar()
    {
        var existing = transform.Find("FlightBeacon");
        if (existing != null)
        {
            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }
    }

    void SetupAudio()
    {
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.spatialBlend = 0.0f; // 爽快なダイレクト2Dサウンド
        _audio.volume = 0.85f;
        _audio.playOnAwake = false;

        GetSharedPassBoostClip();
    }

    void Update()
    {
        if (_ringVisual != null)
        {
            // リングの自転とふんわり脈動
            _ringVisual.Rotate(Vector3.forward, 40f * Time.deltaTime, Space.Self);
            float pulse = 1.0f + Mathf.Sin(Time.time * 3.5f) * 0.06f;
            _ringVisual.localScale = Vector3.one * pulse;
        }

        // 確実な通過感知
        if (!_isCooldown)
        {
            var player = AdventurePlayerController.Resolve();
            if (player != null && IsPlayerInsideGate(player))
                TriggerBoost(player);
        }
    }

    void LateUpdate()
    {
        // プレイヤー移動の後に吸い込み（同一フレームでMoveが打ち消されず滑らかに吸い込まれる）
        TickRingMagnet();
    }

    /// <summary>
    /// リング接近時の気流ファネル吸引（滑空中のグライダーを中心へ引き寄せ、連続コンボを吸い込まれるように決める）
    /// </summary>
    void TickRingMagnet()
    {
        if (_isCooldown) return;

        var player = AdventurePlayerController.Resolve();
        // ★絶対防衛: 滑空していない地上歩行時や通常ジャンプ時は一切吸引しない！
        // プレイヤーが自ら翼を広げて滑空（IsGliding）している時のみ、気流アシストを発動する。
        if (player == null || player.IsGrounded || !player.IsGliding) return;

        Vector3 center = transform.position;
        Vector3 playerPos = player.transform.position + Vector3.up * 0.9f;
        Vector3 toPlayer = playerPos - center;

        // リングの法線軸（前後方向）
        Vector3 forward = transform.forward.normalized;
        float axialDist = Vector3.Dot(toPlayer, forward);

        // リングの軸から外れた半径方向の距離
        Vector3 onAxis = center + forward * axialDist;
        Vector3 radialOffset = playerPos - onAxis;
        float radialDist = radialOffset.magnitude;

        // 吸引ゾーン：リング手前・奥 18m、半径 10.5m の気流ファネル（連鎖しやすい余裕）
        const float maxAxial = 18.0f;
        const float maxRadial = 10.5f;

        if (Mathf.Abs(axialDist) > maxAxial || radialDist > maxRadial) return;

        bool inGate = IsPlayerInsideGate(player);

        // 中心へ向かう緊急度・引力カーブ
        float radialFactor = 1.0f - Mathf.Clamp01(radialDist / maxRadial);
        float axialFactor = 1.0f - Mathf.Clamp01(Mathf.Abs(axialDist) / maxAxial);
        float urgency = radialFactor * axialFactor;
        urgency = Mathf.Pow(urgency, 0.6f) + 0.35f;

        float dt = Time.deltaTime;
        Vector3 pullDelta = Vector3.zero;

        // 1. リングの中心線（軸）への滑らかな吸引
        if (radialDist > 0.15f)
        {
            Vector3 toAxis = -radialOffset.normalized;
            float pullSpeed = inGate ? 4.5f : 13.5f;
            pullDelta += toAxis * (pullSpeed * urgency * dt);
        }

        // 2. リング開口部をくぐり抜ける進行方向への気流ガイド
        Vector3 through = ResolveBoostDirection(player);
        float pushSpeed = inGate ? 3.5f : 9.0f;
        pullDelta += through * (pushSpeed * urgency * dt);

        if (pullDelta.sqrMagnitude > 0.000001f)
        {
            player.ApplyRingMagnetPull(pullDelta);
        }

        if (inGate)
        {
            TriggerBoost(player);
        }
    }

    bool IsPlayerInsideGate(AdventurePlayerController player)
    {
        // 進入角度や高速滑空時でも確実に感知できるよう判定を適正拡大（半径5.2m、厚さ2.5m）
        Vector3[] samplePoints =
        {
            player.transform.position + Vector3.up * 0.5f,
            player.transform.position + Vector3.up * 1.0f,
            player.transform.position + Vector3.up * 1.5f,
        };

        for (int i = 0; i < samplePoints.Length; i++)
        {
            Vector3 localPos = transform.InverseTransformPoint(samplePoints[i]);
            float radiusDist = Mathf.Sqrt(localPos.x * localPos.x + localPos.y * localPos.y);
            float zDist = Mathf.Abs(localPos.z);
            if (radiusDist <= 5.6f && zDist <= 2.85f)
                return true;
        }
        return false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_isCooldown) return;

        var player = other.GetComponent<AdventurePlayerController>()
            ?? other.GetComponentInParent<AdventurePlayerController>();
        // 球トリガーは広めになりやすいので、開口内にいるときだけ発火
        if (player != null && IsPlayerInsideGate(player))
            TriggerBoost(player);
    }

    void TriggerBoost(AdventurePlayerController player)
    {
        if (player == null) return;

        _isCooldown = true;

        // プレイヤーの向き／進入方向に合わせて押し出す（逆向きブーストで押し戻さない）
        Vector3 through = ResolveBoostDirection(player);
        float mult = player.IsGliding ? boostMultiplier : Mathf.Lerp(1.35f, boostMultiplier, 0.55f);
        float dur = player.IsGliding ? boostDuration : 2.2f;
        player.ApplyGlideBoost(mult, dur, through);

        // ゲームパッド触覚ハプティクス（気流突入のスイープパルス）
        AdventureInputHaptics.PlayWindStreamEntry();

        if (_audio != null)
            _audio.PlayOneShot(GetSharedPassBoostClip());

        int comboCount = AdventureWindRingCombo.RegisterPass();
        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
            drone.GrantWindRingOil(comboCount);

        try { OnRingPassed?.Invoke(this, player); } catch (System.Exception ex) { Debug.LogWarning($"[AdventureWindRing] OnRingPassed callback error: {ex.Message}"); }

        StartCoroutine(ShockwaveAndCooldown());
    }

    /// <summary>リング貫通方向を、プレイヤーの向きに合わせて前後どちらにするか決める</summary>
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
            // 向き不明ならリング中心からプレイヤーへ向かう逆＝くぐり抜け方向
            Vector3 fromRing = player.transform.position - transform.position;
            fromRing.y = 0f;
            if (fromRing.sqrMagnitude > 0.001f && Vector3.Dot(through, fromRing.normalized) < 0f)
                through = -through;
        }

        return through;
    }

    IEnumerator ShockwaveAndCooldown()
    {
        float elapsed = 0f;
        Vector3 startScale = _ringVisual.localScale;
        while (elapsed < 0.35f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.35f;
            _ringVisual.localScale = Vector3.Lerp(startScale, startScale * 1.8f, t);
            if (_ringMat != null)
                _ringMat.SetColor("_EmissionColor", ringColor * Mathf.Lerp(4.5f, 0.1f, t));
            yield return null;
        }

        _ringVisual.gameObject.SetActive(false);

        // クールダウン（3.0秒後に再点灯）
        yield return new WaitForSeconds(3.0f);

        _ringVisual.localScale = startScale;
        if (_ringMat != null)
            _ringMat.SetColor("_EmissionColor", ringColor * 2.8f);
        _ringVisual.gameObject.SetActive(true);
        _isCooldown = false;
    }

    /// <summary>「サァァー…ン」という柔らかく澄んだ自然の風のそよぎとチャイム音の合成</summary>
    static AudioClip SynthesizeWindBoostClip()
    {
        const int rate = 44100;
        float duration = 0.85f;
        int count = (int)(rate * duration);
        float[] data = new float[count];
        var rng = new System.Random(42);

        float phaseChime1 = 0f;
        float phaseChime2 = 0f;
        float prevNoise = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;

            // 柔らかな風のホワイトノイズ（ローパス気味）
            float rawNoise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float smoothNoise = Mathf.Lerp(prevNoise, rawNoise, 0.18f);
            prevNoise = smoothNoise;

            // 澄んだアコースティックな風鈴・チャイムの微かな倍音（F#とC#の調和）
            phaseChime1 += 2f * Mathf.PI * 740f / rate;
            phaseChime2 += 2f * Mathf.PI * 1110f / rate;
            float chime = (Mathf.Sin(phaseChime1) * 0.4f + Mathf.Sin(phaseChime2) * 0.25f) * Mathf.Exp(-t * 3.5f);

            float env = Mathf.Sin(t * Mathf.PI);
            env = Mathf.Pow(env, 0.65f);

            data[i] = (smoothNoise * 0.4f + chime * 0.35f) * env * 0.55f;
        }

        var clip = AudioClip.Create("OrganicWindFloat", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
