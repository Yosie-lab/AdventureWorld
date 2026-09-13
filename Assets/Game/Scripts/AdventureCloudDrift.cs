using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 風に乗って雄大に漂い、呼吸するように形を変えるリアルで気持ち良い雲システム。
/// 滑空中のプレイヤーが雲を突っ切ると、水蒸気ミストが包み込み、最高の飛行快感をもたらす。
/// </summary>
public class AdventureCloudDrift : MonoBehaviour
{
    [Header("Drift Settings")]
    public float driftSpeed = 1.4f;
    public Vector3 driftDirection = new Vector3(-0.95f, 0f, 0.31f);
    public float bobSpeed = 0.35f;
    public float bobHeight = 2.2f;

    [Header("Bounds")]
    public float boundMinX = -300f;
    public float boundMaxX = 1300f;
    public float boundMinZ = -300f;
    public float boundMaxZ = 1300f;

    float _baseHeight;
    float _seed;
    List<Transform> _puffs = new List<Transform>();
    List<Vector3> _origPuffScales = new List<Vector3>();
    List<float> _puffOffsets = new List<float>();

    // プレイヤー雲突入インタラクション
    static ParticleSystem _mistParticleInstance;
    static AudioSource _mistAudioInstance;
    static AudioClip _mistPassClip;
    float _lastMistTime = 0f;

    public static void EnsureCloudSystem()
    {
        var existing = GameObject.Find("RustFloat_Clouds");
        if (existing != null && existing.transform.childCount >= 10)
            return;

        var root = existing ?? new GameObject("RustFloat_Clouds");

        // 雲マテリアル取得
        var cloudShader = Shader.Find("RustAndFloat/FluffyCloud") ?? Shader.Find("Universal Render Pipeline/Lit");
        var cloudMat = Resources.Load<Material>("FluffyCloud");
#if UNITY_EDITOR
        if (cloudMat == null)
            cloudMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/RustAndFloat/Materials/FluffyCloud.mat");
#endif
        if (cloudMat == null)
        {
            cloudMat = new Material(cloudShader);
            cloudMat.color = new Color(0.98f, 0.99f, 1.0f, 0.93f);
        }

        // 島の広がり（1000m四方）の上空に22個の重厚でぽっかりとしたモコモコ雲クラスターを配置
        var rng = new System.Random(42);
        const int cloudCount = 22;

        for (int i = 0; i < cloudCount; i++)
        {
            var clusterGo = new GameObject("FluffyCloudCluster_" + i);
            clusterGo.transform.SetParent(root.transform, false);

            float cx = (float)(rng.NextDouble() * 1200.0 - 100.0);
            float cz = (float)(rng.NextDouble() * 1200.0 - 100.0);
            float cy = 110f + (float)(rng.NextDouble() * 115.0); // 高度110m〜225m
            clusterGo.transform.position = new Vector3(cx, cy, cz);

            // 各雲クラスターは7〜11個の重なり合う球体で綿菓子のような立体積雲を形成
            int puffCount = 7 + rng.Next(5);
            float clusterScale = 28f + (float)rng.NextDouble() * 32f; // 直径28m〜60mの壮大な雲

            for (int p = 0; p < puffCount; p++)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puff.name = "Puff_" + p;
                puff.transform.SetParent(clusterGo.transform, false);

                // 中心に厚みを持たせ、外周へふんわり広がる有機的配置
                float rx = (float)(rng.NextDouble() * clusterScale * 1.5 - clusterScale * 0.75);
                float rz = (float)(rng.NextDouble() * clusterScale * 1.1 - clusterScale * 0.55);
                float ry = (float)(rng.NextDouble() * clusterScale * 0.45 - clusterScale * 0.15);
                puff.transform.localPosition = new Vector3(rx, ry, rz);

                // 扁平・丸みを帯びた自然な雲のフォルム
                float sx = clusterScale * (0.65f + (float)rng.NextDouble() * 0.55f);
                float sy = clusterScale * (0.40f + (float)rng.NextDouble() * 0.45f);
                float sz = clusterScale * (0.65f + (float)rng.NextDouble() * 0.55f);
                puff.transform.localScale = new Vector3(sx, sy, sz);

                var rend = puff.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = cloudMat;

                var col = puff.GetComponent<Collider>();
                if (col != null)
                {
#if UNITY_EDITOR
                    DestroyImmediate(col);
#else
                    Destroy(col);
#endif
                }
            }

            clusterGo.AddComponent<AdventureCloudDrift>();
        }
    }

    void Start()
    {
        _baseHeight = transform.position.y;
        _seed = Random.Range(0f, 100f);
        driftDirection.Normalize();

        // 子要素のパフ球体をキャッシュ
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child.name.StartsWith("Puff_"))
            {
                _puffs.Add(child);
                _origPuffScales.Add(child.localScale);
                _puffOffsets.Add(Random.Range(0f, Mathf.PI * 2f));
            }
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // 1. 高度に応じた風速補正（高空ほど少し速く流れる）
        float altFactor = Mathf.Lerp(0.85f, 1.35f, Mathf.Clamp01((_baseHeight - 100f) / 130f));
        Vector3 pos = transform.position;
        pos += driftDirection * (driftSpeed * altFactor * dt);

        // 2. 緩やかな上下の浮遊ゆらぎ
        float bob = Mathf.Sin(Time.time * bobSpeed + _seed) * bobHeight;
        pos.y = _baseHeight + bob;

        // 3. 境界外に出たら反対側からループ
        if (pos.x < boundMinX) pos.x = boundMaxX;
        if (pos.x > boundMaxX) pos.x = boundMinX;
        if (pos.z < boundMinZ) pos.z = boundMaxZ;
        if (pos.z > boundMaxZ) pos.z = boundMinZ;

        transform.position = pos;

        // 4. 雲の呼吸・有機的な変形（各パフが微かに伸縮して生きた雲になる）
        float t = Time.time * 0.6f;
        for (int i = 0; i < _puffs.Count; i++)
        {
            if (_puffs[i] == null) continue;
            float breath = 1f + Mathf.Sin(t + _puffOffsets[i]) * 0.065f;
            _puffs[i].localScale = _origPuffScales[i] * breath;
        }

        // 5. プレイヤー（滑空中のNiko）が雲を突っ切る快感演出
        CheckPlayerCloudDive();
    }

    void CheckPlayerCloudDive()
    {
        var player = AdventurePlayerController.Instance;
        if (player == null || !player.IsGliding) return;

        Vector3 pPos = player.transform.position;
        float dist = Vector3.Distance(pPos, transform.position);

        // 雲クラスターの内部（半径35m以内）を滑空通過中
        if (dist < 35f && Time.time - _lastMistTime > 2.0f)
        {
            _lastMistTime = Time.time;
            TriggerCloudPassMist(player.transform.position);
        }
    }

    void TriggerCloudPassMist(Vector3 playerPos)
    {
        // 雲通過時の白い水蒸気ミストをプレイヤーの周りに優しく噴出
        if (_mistParticleInstance == null)
        {
            var mistGo = new GameObject("CloudDiveMistFX");
            _mistParticleInstance = mistGo.AddComponent<ParticleSystem>();
            var main = _mistParticleInstance.main;
            main.startSpeed = 2f;
            main.startLifetime = 1.2f;
            main.startSize = 4.5f;
            main.startColor = new Color(1f, 1f, 1f, 0.45f);
            main.loop = false;
            main.maxParticles = 60;

            var shape = _mistParticleInstance.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 3.5f;

            var rend = mistGo.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            rend.material = new Material(shader);
            rend.material.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.45f));

            _mistAudioInstance = mistGo.AddComponent<AudioSource>();
            _mistAudioInstance.spatialBlend = 0f;
            _mistAudioInstance.playOnAwake = false;

            _mistPassClip = MakeSoftWhooshClip();
        }

        _mistParticleInstance.transform.position = playerPos;
        _mistParticleInstance.Play();

        if (_mistAudioInstance != null && _mistPassClip != null)
        {
            _mistAudioInstance.pitch = Random.Range(0.95f, 1.15f);
            _mistAudioInstance.PlayOneShot(_mistPassClip, 0.45f);
        }
    }

    static AudioClip MakeSoftWhooshClip()
    {
        int rate = 44100;
        float dur = 1.0f;
        int samples = (int)(rate * dur);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            // ベル型エンベロープでフワッと抜けるホワイトノイズ（風切り水蒸気音）
            float env = Mathf.Sin(t * Mathf.PI);
            float noise = (Random.value * 2f - 1f) * 0.35f;
            float lowSine = Mathf.Sin(2f * Mathf.PI * 180f * (float)i / rate) * 0.2f;
            data[i] = (noise + lowSine) * env * 0.25f;
        }

        var clip = AudioClip.Create("CloudPassWhoosh", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
