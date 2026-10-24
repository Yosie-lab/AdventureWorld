using UnityEngine;

/// <summary>
/// Rustから滴り落ちた、または島で見つかる黒光りする潤滑油（アイテム）
/// プレイヤーが拾うとオイル所持数が増え、Rustの手当て（きしみ解消）に使用できる
/// </summary>
public class AdventureRustOilDrop : MonoBehaviour
{
    public int amount = 3;
    Transform _visual;
    bool _isCollected = false;
    static AudioClip _pickupClip;
    AudioSource _audio;

    void Start()
    {
        CreateVisual();
        SetupAudio();

        var col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.6f;
    }

    void SetupAudio()
    {
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.spatialBlend = 0.8f;
        _audio.minDistance = 1.0f;
        _audio.maxDistance = 12f;
        _audio.playOnAwake = false;

        if (_pickupClip == null)
            _pickupClip = MakePickupSound();
    }

    void CreateVisual()
    {
        var go = new GameObject("Visual");
        go.transform.SetParent(transform, false);
        _visual = go.transform;

        // 油滴・オイル小瓶のプロシージャル3D形状
        var drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        drop.name = "DropMesh";
        drop.transform.SetParent(_visual, false);
        drop.transform.localScale = new Vector3(0.22f, 0.12f, 0.22f);
        Destroy(drop.GetComponent<Collider>());

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(0.04f, 0.02f, 0.01f, 0.95f)); // 黒光りするオイル
        mat.SetFloat("_Metallic", 0.95f);
        mat.SetFloat("_Smoothness", 0.98f);
        drop.GetComponent<Renderer>().material = mat;

        // 油滴の微かな金色の油膜ハイライト
        var highlight = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        highlight.name = "Highlight";
        highlight.transform.SetParent(drop.transform, false);
        highlight.transform.localPosition = new Vector3(0.2f, 0.4f, 0.1f);
        highlight.transform.localScale = Vector3.one * 0.35f;
        Destroy(highlight.GetComponent<Collider>());

        var hMat = new Material(shader);
        hMat.SetColor("_BaseColor", new Color(0.9f, 0.75f, 0.2f, 0.8f));
        hMat.EnableKeyword("_EMISSION");
        hMat.SetColor("_EmissionColor", new Color(0.6f, 0.5f, 0.1f) * 0.8f);
        highlight.GetComponent<Renderer>().material = hMat;
    }

    void Update()
    {
        if (_isCollected) return;

        // ゆっくりと息づくような油滴の表面張力アニメーション
        if (_visual != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.08f;
            _visual.localScale = new Vector3(pulse, 1f / pulse, pulse);
        }

        var player = AdventurePlayerController.Instance;
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist < 1.8f)
        {
            Collect(player);
        }
    }

    void Collect(AdventurePlayerController player)
    {
        _isCollected = true;
        if (_audio != null && _pickupClip != null)
            _audio.PlayOneShot(_pickupClip, 0.85f);

        var drone = AdventureRustDrone.Instance ?? Object.FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            drone.AddOil(amount);
        }

        Destroy(gameObject, 0.35f);
    }

    static AudioClip MakePickupSound()
    {
        const int rate = 44100;
        float duration = 0.28f;
        int count = (int)(rate * duration);
        float[] data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float env = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-t * 9f);
            float pop = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(440f, 880f, t) * t);
            data[i] = pop * env * 0.6f;
        }
        var clip = AudioClip.Create("OilPickup", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
