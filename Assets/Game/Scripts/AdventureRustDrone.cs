using UnityEngine;
using System.Collections;
using System.Linq;

public class AdventureRustDrone : MonoBehaviour
{
    public float hoverHeight = 1.15f;
    public float bobAmount = 0.1f;
    public float bobSpeed = 1.35f;
    public float followDistance = 2.1f;
    public float stopDistance = 1.25f;

    Transform _lookAt;
    Transform _body;
    Terrain _land;
    Vector3 _velocity;
    Vector3 _lagTarget;
    AudioSource _audio;
    AudioClip[] _creaks;
    ParticleSystem _heatFx;
    Material _bodyMat;
    Material _oilMat;
    float _hitchUntil;
    float _heatUntil;
    float _nextHitch;
    float _nextCreak;
    float _heat;
    bool _wasHitching;

    void Start()
    {
        var niko = GameObject.Find("Niko");
        if (niko != null)
            _lookAt = niko.transform;

        _land = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude)
            .FirstOrDefault(t => t.name == "LandTerrain" || t.name == "IslandTerrain");
        _lagTarget = transform.position;
        _nextHitch = Time.time + 2.2f;
        SetupAudio();
        SetupHeat();
        SetupOil();
    }

    void Update()
    {
        if (_lookAt == null)
            return;

        Vector3 goal = FollowPoint();
        bool hitching = Time.time < _hitchUntil;
        if (!hitching && Time.time >= _nextHitch && FlatDistance(goal) > 2.4f)
        {
            _hitchUntil = Time.time + Random.Range(0.22f, 0.5f);
            _nextHitch = Time.time + Random.Range(2.6f, 5.4f);
            hitching = true;
            PlayCreak(true);
            BeginHeatBurst();
        }

        if (hitching)
        {
            goal.x = transform.position.x;
            goal.z = transform.position.z;
        }

        _lagTarget = Vector3.Lerp(_lagTarget, goal, 1f - Mathf.Exp(-1.7f * Time.deltaTime));
        transform.position = Vector3.SmoothDamp(transform.position, _lagTarget, ref _velocity, 0.52f, 5.1f);

        Vector3 to = _lookAt.position + Vector3.up * 0.7f - transform.position;
        if (to.sqrMagnitude > 0.04f)
        {
            Quaternion look = Quaternion.LookRotation(to);
            if (hitching)
                look *= Quaternion.Euler(0f, Mathf.Sin(Time.time * 18f) * 8f, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 2.4f * Time.deltaTime);
        }

        if (!hitching && _velocity.sqrMagnitude > 6f)
            PlayCreak(false);

        UpdateHeat(hitching);
        if (_wasHitching && !hitching)
            DripOil();
        _wasHitching = hitching;
    }

    Vector3 FollowPoint()
    {
        Vector3 niko = _lookAt.position;
        Vector3 back = Vector3.ProjectOnPlane(-_lookAt.forward, Vector3.up);
        if (back.sqrMagnitude < 0.01f)
            back = Vector3.back;
        back.Normalize();

        Vector3 flat = niko + back * followDistance;
        if (FlatDistance(niko) < stopDistance)
            flat = new Vector3(transform.position.x, 0f, transform.position.z);

        float surface = SurfaceY(flat) + hoverHeight;
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        float y = surface + bob;
        if (niko.y > surface + 1.2f)
            y = niko.y + 0.35f + bob * 0.5f;

        return new Vector3(flat.x, y, flat.z);
    }

    float SurfaceY(Vector3 pos)
    {
        float landY = pos.y;
        if (_land != null)
            landY = _land.SampleHeight(pos) + _land.transform.position.y;

        var bounds = AdventureIslandBoundary.Instance;
        float water = bounds != null ? bounds.waterLevel : float.NegativeInfinity;
        return landY < water ? water : landY;
    }

    float FlatDistance(Vector3 target)
    {
        Vector3 a = transform.position;
        a.y = 0f;
        target.y = 0f;
        return Vector3.Distance(a, target);
    }

    void SetupAudio()
    {
        _audio = GetComponent<AudioSource>();
        if (_audio == null)
            _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 1f;
        _audio.minDistance = 1.5f;
        _audio.maxDistance = 22f;
        _audio.volume = 0.42f;
        _creaks = new[] { MakeCreak(11), MakeCreak(29), MakeCreak(47) };
    }

    void SetupHeat()
    {
        _body = transform.Find("Body");
        if (_body != null)
        {
            var rend = _body.GetComponent<Renderer>();
            if (rend != null)
            {
                _bodyMat = rend.material;
                _bodyMat.EnableKeyword("_EMISSION");
            }
        }

        var go = new GameObject("Heat");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.38f, 0f);
        _heatFx = go.AddComponent<ParticleSystem>();

        var main = _heatFx.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.7f);
        main.startColor = new Color(1f, 1f, 1f, 0.78f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.22f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 24;
        main.scalingMode = ParticleSystemScalingMode.Local;

        var emission = _heatFx.emission;
        emission.rateOverTime = 0f;

        var shape = _heatFx.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 38f;
        shape.radius = 0.2f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var color = _heatFx.colorOverLifetime;
        color.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.8f, 0f),
                new GradientAlphaKey(0.45f, 0.4f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = grad;

        var size = _heatFx.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 2.2f));

        var rot = _heatFx.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-18f, 18f);

        var noise = _heatFx.noise;
        noise.enabled = true;
        noise.strength = 0.7f;
        noise.frequency = 0.22f;
        noise.scrollSpeed = 0.18f;
        noise.damping = true;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.maxParticleSize = 3f;
        renderer.material = LoadHeatMaterial();
        _heatFx.Play();
    }

    void SetupOil()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard");
        _oilMat = new Material(shader);
        _oilMat.SetColor("_BaseColor", new Color(0.04f, 0.02f, 0.01f, 1f));
        _oilMat.SetColor("_Color", new Color(0.04f, 0.02f, 0.01f, 1f));
        _oilMat.SetFloat("_Metallic", 0.9f);
        _oilMat.SetFloat("_Smoothness", 0.92f);
        _oilMat.SetFloat("_Glossiness", 0.92f);
    }

    void DripOil()
    {
        int n = Random.Range(1, 3);
        for (int i = 0; i < n; i++)
            StartCoroutine(FallingOilDrop(i * 0.16f));
    }

    IEnumerator FallingOilDrop(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        var drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        drop.name = "OilDrop";
        var col = drop.GetComponent<Collider>();
        if (col != null)
            Destroy(col);
        drop.transform.position = transform.position + Vector3.down * 0.7f
            + new Vector3(Random.Range(-0.12f, 0.12f), 0f, Random.Range(-0.12f, 0.12f));
        drop.transform.localScale = Vector3.one * Random.Range(0.07f, 0.1f);
        var rend = drop.GetComponent<Renderer>();
        if (rend != null && _oilMat != null)
            rend.sharedMaterial = _oilMat;

        Vector3 vel = Vector3.down * 0.35f;
        float t = 0f;
        while (t < 2.4f && drop != null)
        {
            vel += Vector3.down * 9.8f * 0.55f * Time.deltaTime;
            drop.transform.position += vel * Time.deltaTime;
            t += Time.deltaTime;
            yield return null;
        }

        if (drop != null)
            Destroy(drop);
    }

    void BeginHeatBurst()
    {
        _heatUntil = Time.time + 4f;
        if (_heatFx != null)
            _heatFx.Emit(2);
    }

    void UpdateHeat(bool hitching)
    {
        if (hitching || _velocity.sqrMagnitude > 3f)
            _heatUntil = Mathf.Max(_heatUntil, Time.time + 1.4f);

        float want = Time.time < _heatUntil ? 1f : 0f;
        _heat = Mathf.MoveTowards(_heat, want, Time.deltaTime * (want > _heat ? 4f : 0.32f));

        if (_heatFx != null)
        {
            var emission = _heatFx.emission;
            emission.rateOverTime = _heat * 6f;
        }

        if (_bodyMat != null)
            _bodyMat.SetColor("_EmissionColor", new Color(1.6f, 0.35f, 0.05f) * (_heat * 2.1f));

        if (_body != null)
        {
            float h = _heat * _heat;
            _body.localPosition = new Vector3(
                Mathf.Sin(Time.time * 19f) * 0.01f * h,
                Mathf.Sin(Time.time * 27f) * 0.014f * h,
                Mathf.Cos(Time.time * 15f) * 0.008f * h);
        }
    }

    static Material LoadHeatMaterial()
    {
        var shader = Shader.Find("RustAndFlat/WhiteSmoke");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader);
        var src = Resources.Load<Material>("RustHeat");
        if (src != null)
        {
            var tex = src.GetTexture("_BaseMap");
            if (tex != null)
                mat.SetTexture("_BaseMap", tex);
        }

        mat.SetColor("_BaseColor", Color.white);
        mat.renderQueue = 3100;
        return mat;
    }

    void PlayCreak(bool force)
    {
        if (_audio == null || _creaks == null)
            return;
        if (!force && Time.time < _nextCreak)
            return;
        _audio.pitch = Random.Range(0.86f, 1.08f);
        _audio.PlayOneShot(_creaks[Random.Range(0, _creaks.Length)], Random.Range(0.28f, 0.5f));
        _nextCreak = Time.time + Random.Range(0.9f, 1.8f);
    }

    static AudioClip MakeCreak(int seed)
    {
        const int hz = 22050;
        int n = (int)(hz * 0.24f);
        float[] data = new float[n];
        var rng = new System.Random(seed);
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float env = Mathf.Exp(-t * 7.5f) * (1f - t);
            phase += (320f + (float)rng.NextDouble() * 280f) * (2f * Mathf.PI / hz);
            float grit = (float)rng.NextDouble() * 2f - 1f;
            data[i] = (Mathf.Sin(phase) * 0.32f + grit * 0.62f) * env * 0.5f;
        }

        var clip = AudioClip.Create("RustCreak", n, 1, hz, false);
        clip.SetData(data, 0);
        return clip;
    }
}
