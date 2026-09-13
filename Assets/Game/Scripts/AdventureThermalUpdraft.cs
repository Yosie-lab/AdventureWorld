using UnityEngine;

/// <summary>
/// 滑空中のプレイヤーをパラグライダーのように上空へ持ち上げる「上昇気流（サーマル）」
/// 谷間や滝壺、海岸線から立ち上る優しい上昇風の柱
/// </summary>
public class AdventureThermalUpdraft : MonoBehaviour
{
    public float radius = 7.0f;
    public float height = 45.0f;
    public float liftSpeed = 5.2f; // 毎秒+5.2mで上空へ浮遊上昇

    ParticleSystem _windPs;
    AudioSource _audio;

    void Start()
    {
        CreateWindFx();
        SetupTrigger();
        SetupAudio();
    }

    void SetupTrigger()
    {
        var col = gameObject.AddComponent<CapsuleCollider>();
        col.isTrigger = true;
        col.radius = radius;
        col.height = height;
        col.center = new Vector3(0f, height * 0.5f, 0f);
        var rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void Update()
    {
        var player = AdventurePlayerController.Instance;
        if (player == null) return;

        Vector3 pos = transform.position;
        Vector3 pPos = player.transform.position;

        // XZ平面の距離と高さ範囲を判定
        float distXZ = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(pPos.x, pPos.z));
        bool inY = pPos.y >= pos.y - 1.0f && pPos.y <= pos.y + height;

        if (distXZ < radius && inY)
        {
            if (player.IsGliding)
            {
                player.ApplyUpdraft(liftSpeed);
            }
            else
            {
                // 地上から歩いて気流に入った時、自動でフワリと上空へテイクオフ！
                player.ApplyGlideBoost(1.25f, 3.8f, transform.forward);
                player.ApplyUpdraft(liftSpeed * 1.3f);
            }

            if (_audio != null)
                _audio.volume = Mathf.MoveTowards(_audio.volume, 0.45f, Time.deltaTime * 3.5f);
        }
        else
        {
            if (_audio != null && _audio.volume > 0f)
                _audio.volume = Mathf.MoveTowards(_audio.volume, 0f, Time.deltaTime * 2.0f);
        }
    }

    void OnTriggerStay(Collider other)
    {
        var player = other.GetComponent<AdventurePlayerController>()
            ?? other.GetComponentInParent<AdventurePlayerController>();
        if (player != null)
        {
            if (player.IsGliding)
                player.ApplyUpdraft(liftSpeed);
            else
            {
                player.ApplyGlideBoost(1.25f, 3.8f, transform.forward);
                player.ApplyUpdraft(liftSpeed * 1.3f);
            }

            // 音量のフェードイン
            if (_audio != null)
                _audio.volume = Mathf.MoveTowards(_audio.volume, 0.45f, Time.deltaTime * 3.5f);
        }
    }

    void CreateWindFx()
    {
        var pGo = new GameObject("ThermalParticles");
        pGo.transform.SetParent(transform, false);
        pGo.transform.localPosition = Vector3.zero;

        _windPs = pGo.AddComponent<ParticleSystem>();
        var main = _windPs.main;
        main.loop = true;
        main.startLifetime = 3.2f;
        main.startSpeed = 12.0f; // 上昇風
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startColor = new Color(0.35f, 1.0f, 0.85f, 0.45f);
        main.gravityModifier = -0.15f; // さらに上向き加速
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = _windPs.emission;
        emission.rateOverTime = 28f;

        var shape = _windPs.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * 0.85f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // 上向き

        var colorOverLife = _windPs.colorOverLifetime;
        colorOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(0.4f, 1.0f, 0.9f), 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.45f, 0.2f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLife.color = grad;

        // らせん回転ノイズ
        var noise = _windPs.noise;
        noise.enabled = true;
        noise.strength = 0.55f;
        noise.frequency = 0.18f;
        noise.scrollSpeed = 0.25f;

        var rend = pGo.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            var pShader = Shader.Find("RustAndFloat/WhiteSmoke") ?? Shader.Find("Sprites/Default");
            var pMat = new Material(pShader);
            pMat.SetTexture("_BaseMap", AdventureRustDrone.GetSoftSmokeTexture());
            pMat.SetColor("_BaseColor", new Color(0.3f, 1.0f, 0.85f, 0.6f));
            rend.material = pMat;
        }
    }

    void SetupAudio()
    {
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.spatialBlend = 1.0f;
        _audio.minDistance = 3f;
        _audio.maxDistance = radius * 2.5f;
        _audio.loop = true;
        _audio.volume = 0f;
        _audio.clip = MakeThermalWindClip();
        _audio.Play();
    }

    void OnTriggerExit(Collider other)
    {
        var player = other.GetComponent<AdventurePlayerController>()
            ?? other.GetComponentInParent<AdventurePlayerController>();
        if (player != null && _audio != null)
        {
            _audio.volume = 0f;
        }
    }

    static AudioClip MakeThermalWindClip()
    {
        const int rate = 22050;
        int count = (int)(rate * 1.5f);
        float[] data = new float[count];
        var rng = new System.Random(1337);

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float sine = Mathf.Sin(t * Mathf.PI * 2f * 65f); // 温かい低周波
            data[i] = (noise * 0.4f + sine * 0.6f) * 0.22f;
        }

        var clip = AudioClip.Create("ThermalWindLoop", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
