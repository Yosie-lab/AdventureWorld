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
    [Range(0f, 1f)] public float soundVolume = 0.28f;

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

    // スクラップ収集・アップグレード対話
    string _speechText = "";
    float _speechTimer = 0f;
    AudioClip _happyBeepClip;
    AudioClip _sonarBeepClip;
    float _sonarTimer = 0f;
    GUIStyle _speechStyle;
    Texture2D _speechBg;

    // オイルアイテムと手当てシステム
    public int oilCount = 2;
    public float wellOiledUntil = 0f;
    bool _isPlayerNear = false;
    float _lastInteractTime = 0f;

    public static AdventureRustDrone Instance { get; private set; }

    public static void Ensure()
    {
        if (Instance != null) return;
        var existing = FindAnyObjectByType<AdventureRustDrone>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var niko = GameObject.Find("Niko");
        Vector3 spawnPos = niko != null ? niko.transform.position + niko.transform.right * 1.5f + Vector3.up * 1.2f : new Vector3(266f, 49.5f, 331f);

        GameObject droneGo = null;
#if UNITY_EDITOR
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RustAndFloat/Prefabs/Rust.prefab");
        if (prefab != null)
        {
            droneGo = Object.Instantiate(prefab, spawnPos, Quaternion.identity);
            droneGo.name = "Rust";
        }
#endif
        if (droneGo == null)
        {
            droneGo = new GameObject("Rust");
            droneGo.transform.position = spawnPos;
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(droneGo.transform, false);
            body.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            var col = body.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        Instance = droneGo.GetComponent<AdventureRustDrone>() ?? droneGo.AddComponent<AdventureRustDrone>();
    }

    void Awake()
    {
        Instance = this;
    }

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
        oilCount = Mathf.Max(oilCount, 2); // ゲーム開始時に確実に2個以上油を所持

        // 起動時のあたたかい挨拶
        _speechText = "ピピッ…！起動したよ、Niko。一緒に行こう！";
        _speechTimer = 4.5f;
        _nextIdleTalk = Time.time + 20f;
    }

    float _nextIdleTalk;

    void Update()
    {
        if (_lookAt == null)
            return;

        Vector3 goal = FollowPoint();
        bool wellOiled = Time.time < wellOiledUntil;
        bool hitching = !wellOiled && Time.time < _hitchUntil;

        if (!wellOiled && !hitching && Time.time >= _nextHitch && FlatDistance(goal) > 2.4f)
        {
            _hitchUntil = Time.time + Random.Range(0.22f, 0.5f);
            _nextHitch = Time.time + Random.Range(3.5f, 6.5f);
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
        float smoothTime = wellOiled ? 0.38f : 0.52f; // 油を差してもらうと機敏に追従
        transform.position = Vector3.SmoothDamp(transform.position, _lagTarget, ref _velocity, smoothTime, 5.5f);

        Vector3 to = _lookAt.position + Vector3.up * 0.7f - transform.position;
        if (to.sqrMagnitude > 0.04f)
        {
            Quaternion look = Quaternion.LookRotation(to);
            if (hitching)
                look *= Quaternion.Euler(0f, Mathf.Sin(Time.time * 18f) * 8f, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, (wellOiled ? 3.5f : 2.4f) * Time.deltaTime);
        }

        if (!hitching && _velocity.sqrMagnitude > 6f && !wellOiled)
            PlayCreak(false);

        UpdateHeat(hitching);
        if (_wasHitching && !hitching)
            DripOil();
        _wasHitching = hitching;

        UpdatePlayerInteraction();
        UpdateSpeech();
        UpdateSonar();
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
        _audio.volume = soundVolume;
        _creaks = new[] { MakeCreak(11), MakeCreak(29), MakeCreak(47) };
        _happyBeepClip = MakeSynthBeep(880f, 1320f, 0.18f);
        _sonarBeepClip = MakeSynthBeep(1480f, 1100f, 0.22f);
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
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.32f, 0.65f);
        main.startColor = new Color(1f, 1f, 1f, 0.65f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.16f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 16;
        main.scalingMode = ParticleSystemScalingMode.Local;

        var emission = _heatFx.emission;
        emission.rateOverTime = 0f;

        var shape = _heatFx.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 24f;
        shape.radius = 0.12f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var color = _heatFx.colorOverLifetime;
        color.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.92f, 0.94f, 0.98f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),      // 発生時はスッとフェードイン
                new GradientAlphaKey(0.6f, 0.25f), // ピーク透明度
                new GradientAlphaKey(0f, 1f)       // 自然に消滅
            });
        color.color = grad;

        var size = _heatFx.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.8f));

        var rot = _heatFx.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-12f, 12f);

        var noise = _heatFx.noise;
        noise.enabled = true;
        noise.strength = 0.45f;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.15f;
        noise.damping = true;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.maxParticleSize = 1.5f;
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
        float groundY = SurfaceY(drop.transform.position);

        while (t < 2.4f && drop != null)
        {
            vel += Vector3.down * 9.8f * 0.55f * Time.deltaTime;
            drop.transform.position += vel * Time.deltaTime;
            if (drop.transform.position.y <= groundY + 0.08f)
            {
                // 地面に到達！
                drop.transform.position = new Vector3(drop.transform.position.x, groundY + 0.05f, drop.transform.position.z);
                break;
            }
            t += Time.deltaTime;
            yield return null;
        }

        if (drop != null)
        {
            // フィールド上のオイルが4個未満なら採取可能なアイテムとして残す
            int existingOils = Object.FindObjectsByType<AdventureRustOilDrop>(FindObjectsInactive.Exclude).Length;
            if (existingOils < 4)
            {
                var oilItem = new GameObject("RustOilDrop");
                oilItem.transform.position = drop.transform.position;
                oilItem.AddComponent<AdventureRustOilDrop>();
            }
            Destroy(drop);
        }
    }

    /// <summary>オイル採取時の通知</summary>
    public void AddOil(int amount)
    {
        oilCount += amount;
        SpeakCustom("✦ 潤滑油を採取した！（所持数: " + oilCount + "）", 3.2f);
        if (_happyBeepClip != null && _audio != null)
            _audio.PlayOneShot(_happyBeepClip, 0.45f);
    }

    void UpdatePlayerInteraction()
    {
        var player = AdventurePlayerController.Instance ?? FindAnyObjectByType<AdventurePlayerController>();
        if (player == null) return;

        // Rustの浮遊高さを考慮し、水平5.5m・高低差5.0mまで広角に接近検知
        Vector3 diff = transform.position - player.transform.position;
        float horizontalDist = new Vector2(diff.x, diff.z).magnitude;
        float verticalDist = Mathf.Abs(diff.y);
        _isPlayerNear = (horizontalDist < 5.5f && verticalDist < 5.0f);

        // キーボードEキー・パッド決定・PlayerController経由のいずれでも100%直接検知
        var kb = UnityEngine.InputSystem.Keyboard.current;
        var pad = UnityEngine.InputSystem.Gamepad.current;
        bool ePressed = (kb != null && (kb.eKey.wasPressedThisFrame || kb.eKey.wasReleasedThisFrame))
                     || (pad != null && pad.buttonWest.wasPressedThisFrame)
                     || (player != null && player.InteractPressed);

        if (_isPlayerNear && ePressed && Time.time - _lastInteractTime > 0.35f)
        {
            _lastInteractTime = Time.time;
            InteractWithNiko();
        }
    }

    /// <summary>Nikoとの直接対話または手当て</summary>
    void InteractWithNiko()
    {
        bool needsOil = (_heat > 0.15f || Time.time < _hitchUntil || Time.time > wellOiledUntil);

        // 1. 油を持っている場合は手当て・整備を確実に実行
        if (oilCount > 0)
        {
            oilCount--;
            wellOiledUntil = Mathf.Max(wellOiledUntil, Time.time) + 90f; // 90秒間快調
            _heatUntil = 0f;
            _heat = 0f;
            _velocity += Vector3.up * 3.5f;

            if (_happyBeepClip != null && _audio != null)
                _audio.PlayOneShot(_happyBeepClip, 0.85f);

            StartCoroutine(CheerSpinRoutine());
            return;
        }

        // 2. 油が切れている場合
        if (needsOil && oilCount == 0)
        {
            _velocity += Vector3.up * 1.2f;
            SpeakCustom("ピピッ…潤滑油が切れちゃった。僕が落とした黒いオイルのしずくを拾ってくれたら嬉しいな！", 4.5f);
            return;
        }

        // 3. 通常の対話
        _velocity += Vector3.up * 1.5f;
        if (_happyBeepClip != null && _audio != null)
            _audio.PlayOneShot(_happyBeepClip, 0.35f);

        var player = AdventurePlayerController.Instance;
        var scrapMgr = AdventureScrapManager.Instance;
        int scraps = scrapMgr != null ? scrapMgr.CollectedCount : 0;

        string reply;
        if (player != null && player.transform.position.y > 60f)
        {
            reply = "すごい見晴らしだね、Niko！ここから風に乗ったらどこまで飛べるかな？";
        }
        else if (player != null && player.transform.position.y < 9f)
        {
            reply = "波の音がするね…昔の世界から流れてきたものが砂に埋もれているみたい";
        }
        else if (scraps >= 6)
        {
            reply = "ギアの波長が合ってきたよ！島を一緒に巡れて嬉しいな、Niko";
        }
        else
        {
            string[] casualLines = {
                "この島、静かで風が温かいね…一緒にのんびり行こう、Niko",
                "ピピッ！何かな？ぼくはいつでもNikoの隣にいるよ",
                "焦らなくていいんだよ。寄り道しながら、空と海を眺めよう"
            };
            reply = casualLines[Random.Range(0, casualLines.Length)];
        }

        SpeakCustom(reply, 4.5f);
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

    static Texture2D _softSmokeTex;

    public static Texture2D GetSoftSmokeTexture()
    {
        if (_softSmokeTex != null)
            return _softSmokeTex;

        const int res = 64;
        _softSmokeTex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        _softSmokeTex.wrapMode = TextureWrapMode.Clamp;
        _softSmokeTex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(res * 0.5f, res * 0.5f);
        float radius = res * 0.48f;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float t = Mathf.Clamp01(dist / radius);
                // コサイン曲線による非常に滑らかな減衰（端は完全な透明、境界の四角感をゼロに）
                float alpha = t >= 1f ? 0f : (Mathf.Cos(t * Mathf.PI) * 0.5f + 0.5f);
                alpha = Mathf.Pow(alpha, 1.5f);
                _softSmokeTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        _softSmokeTex.Apply();
        return _softSmokeTex;
    }

    static Material LoadHeatMaterial()
    {
        var shader = Shader.Find("RustAndFloat/WhiteSmoke");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader);
        mat.SetTexture("_BaseMap", GetSoftSmokeTexture());
        mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.85f));
        mat.renderQueue = 3100;
        return mat;
    }

    void PlayCreak(bool force)
    {
        if (_audio == null || _creaks == null || _creaks.Length == 0)
            return;
        if (!force && Time.time < _nextCreak)
            return;
        var clip = _creaks[Random.Range(0, _creaks.Length)];
        if (clip == null)
            return;
        _audio.volume = soundVolume;
        _audio.pitch = Random.Range(0.86f, 1.08f);
        _audio.PlayOneShot(clip, Random.Range(0.22f, 0.38f));
        _nextCreak = Time.time + Random.Range(1.2f, 2.2f);
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

    public void OnNikoFoundScrap(int count)
    {
        // 嬉しそうにピョンと跳ねる
        _velocity += Vector3.up * 2.8f;
        if (_audio != null && _happyBeepClip != null)
            _audio.PlayOneShot(_happyBeepClip, 0.45f);

        switch (count)
        {
            case 1:
                _speechText = "ピピピッ！綺麗なギアを見つけたね、Niko！";
                break;
            case 2:
                _speechText = "古代のエネルギーが微かに残ってるよ…！";
                break;
            case 3:
                _speechText = "ピキーン！歯車が噛み合った！ダッシュが速くなったよ！";
                break;
            case 6:
                _speechText = "コア同期完了！二段ジャンプができるようになったよ！";
                break;
            case 9:
                _speechText = "探知レーダーが作動！近くの遺物を探知するよ！";
                break;
            case 12:
                _speechText = "全パーツ結合完了！大滑空ブーストが全開になったよ！！";
                break;
            default:
                string[] barks = {
                    "ピピッ！また見つけたね！",
                    "調子が出てきたよ、Niko！",
                    "島の遺物はあといくつかな？",
                    "すごい！ギアの波長が合ってきた！"
                };
                _speechText = barks[Random.Range(0, barks.Length)];
                break;
        }
        _speechTimer = 4.2f;
    }

    /// <summary>滑空を開始した瞬間のRustの穏やかなセリフ</summary>
    public void OnGlideStarted()
    {
        _velocity += Vector3.up * 1.5f;
        string[] glideStartLines = {
            "わぁ…！風が気持ちいいね、Niko",
            "ふわりと浮いたよ…！",
            "風を掴んだね…！すごいよ！"
        };
        _speechText = glideStartLines[Random.Range(0, glideStartLines.Length)];
        _speechTimer = 4.0f;
    }

    /// <summary>気流に乗った時のRustの穏やかなセリフ</summary>
    public void OnFloatWindCaught()
    {
        _velocity += Vector3.up * 1.8f;
        string[] windLines = {
            "わぁ…！風が気持ちいいね、Niko",
            "ふわりと浮いたよ…！",
            "風に乗って、どこまでも行けそう",
            "島を見下ろすと、すごく綺麗だね"
        };
        _speechText = windLines[Random.Range(0, windLines.Length)];
        _speechTimer = 4.0f;
    }

    /// <summary>指定したテキストを特大ダイアログで発話</summary>
    public void SpeakCustom(string text, float duration = 4.5f)
    {
        _velocity += Vector3.up * 0.8f;
        _speechText = text;
        _speechTimer = duration;
    }

    void UpdateSpeech()
    {
        if (_speechTimer > 0f)
            _speechTimer -= Time.deltaTime;
        else if (Time.time >= _nextIdleTalk)
        {
            _nextIdleTalk = Time.time + Random.Range(30f, 50f);
            var player = AdventurePlayerController.Instance;
            if (player != null && player.IsGliding)
            {
                string[] glideLines = {
                    "風に乗って、どこまでも行けそう",
                    "島を見下ろすと、すごく綺麗だね",
                    "わぁ…！風が気持ちいいね、Niko"
                };
                _speechText = glideLines[Random.Range(0, glideLines.Length)];
                _speechTimer = 4.0f;
            }
            else
            {
                string[] exploreLines = {
                    "風の音が心地いいね、Niko",
                    "どこへ行こうか？のんびり行こう",
                    "この島の空気、すこし温かいね",
                    "ピピッ…何か光るものがあるかな？"
                };
                _speechText = exploreLines[Random.Range(0, exploreLines.Length)];
                _speechTimer = 3.8f;
            }
        }
    }

    void UpdateSonar()
    {
        var mgr = AdventureScrapManager.Instance;
        if (mgr == null || !mgr.hasPetRadar)
            return;

        var nearest = mgr.GetNearestScrap(transform.position, out float dist);
        if (nearest == null || dist > 45f)
            return;

        _sonarTimer -= Time.deltaTime;
        if (_sonarTimer <= 0f)
        {
            float rate = Mathf.Lerp(1.0f, 2.8f, 1f - Mathf.Clamp01(dist / 45f));
            _sonarTimer = 3.2f / rate;

            if (_audio != null && _sonarBeepClip != null)
            {
                _audio.pitch = Mathf.Lerp(0.9f, 1.35f, 1f - Mathf.Clamp01(dist / 45f));
                _audio.PlayOneShot(_sonarBeepClip, 0.32f);
            }
        }
    }

    void OnGUI()
    {
        // 0. Eキー検知の確実なフォールバック（InputSystemのフレーム遅延やEventSystem遮断を完全救済）
        if (_isPlayerNear && Event.current != null && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.E && Time.time - _lastInteractTime > 0.35f)
        {
            _lastInteractTime = Time.time;
            InteractWithNiko();
        }

        // 1. Niko接近時の頭上インタラクションプロンプト（特大フォント・高コントラスト）
        if (_isPlayerNear && Camera.main != null)
        {
            Vector3 headPos = transform.position + Vector3.up * 0.85f;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(headPos);
            if (screenPos.z > 0.2f)
            {
                bool needsOil = (_heat > 0.15f || Time.time < _hitchUntil || Time.time > wellOiledUntil);
                string prompt;
                Color textColor;

                if (oilCount > 0)
                {
                    prompt = needsOil 
                        ? $"【E】油をさして手当てする（所持: {oilCount}）" 
                        : $"【E】油をさして整備（所持: {oilCount}）";
                    textColor = new Color(1.0f, 0.90f, 0.25f); // 鮮やかなゴールド
                }
                else
                {
                    prompt = needsOil 
                        ? "【⚠ Rustが不調…油切れ（油滴を拾おう）】" 
                        : "【E】話しかける（油切れ: 0）";
                    textColor = needsOil ? new Color(1.0f, 0.55f, 0.15f) : new Color(0.40f, 0.96f, 1.0f);
                }

                // セリフ本文と調和する上品で読みやすいフォント（20〜30pt）
                int promptFontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.026f, 20f, 30f));
                GUIStyle pStyle = new GUIStyle(GUI.skin.label);
                pStyle.fontSize = promptFontSize;
                pStyle.fontStyle = FontStyle.Bold;
                pStyle.alignment = TextAnchor.MiddleCenter;

                Vector2 pSize = pStyle.CalcSize(new GUIContent(prompt));
                float padX = 28f;
                float padY = 12f;
                float boxW = pSize.x + padX;
                float boxH = pSize.y + padY;
                float boxX = screenPos.x - boxW * 0.5f;
                float boxY = Screen.height - screenPos.y - boxH - 16f;
                Rect promptBoxRect = new Rect(boxX, boxY, boxW, boxH);

                // 半透明ダーク背景
                if (_speechBg == null)
                {
                    _speechBg = new Texture2D(1, 1);
                    _speechBg.SetPixel(0, 0, new Color(0.04f, 0.07f, 0.12f, 0.92f));
                    _speechBg.Apply();
                }
                GUI.DrawTexture(promptBoxRect, _speechBg);

                // 上部アクセントライン
                Rect lineRect = new Rect(boxX, boxY, boxW, 3f);
                GUI.DrawTexture(lineRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, textColor, 0, 0);

                // 黒アウトライン付き特大テキスト描画
                DrawOutlinedText(promptBoxRect, prompt, pStyle, textColor, new Color(0f, 0f, 0f, 0.95f));
            }
        }

        // 2. 画面右上のオイル所持数＆RustコンディションHUD（文字欠け防止＆余裕のセーフマージン）
        bool isOiled = Time.time < wellOiledUntil;
        bool isDistressed = (_heat > 0.15f || Time.time < _hitchUntil || !isOiled);
        if (oilCount > 0 || isOiled || isDistressed)
        {
            // 上品で視認性の高いHUDフォント（15〜22pt）
            int badgeSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.018f, 15f, 22f));
            GUIStyle badgeStyle = new GUIStyle(GUI.skin.label);
            badgeStyle.fontSize = badgeSize;
            badgeStyle.fontStyle = FontStyle.Bold;
            badgeStyle.alignment = TextAnchor.MiddleCenter;
            badgeStyle.clipping = TextClipping.Overflow; // 文字クリッピングを完全防止

            string status;
            Color bColor;
            if (isOiled)
            {
                status = "✦ Rust好調（整備済）";
                bColor = new Color(0.45f, 0.95f, 0.65f); // エメラルドグリーン
            }
            else if (oilCount > 0)
            {
                status = $"⚠ 要整備 (油: {oilCount})";
                bColor = new Color(1.0f, 0.88f, 0.35f); // イエロー
            }
            else
            {
                status = "⚠ Rust不調 (油が必要)";
                bColor = new Color(1.0f, 0.62f, 0.22f); // オレンジ
            }

            GUIContent bContent = new GUIContent(status);
            Vector2 bSize = badgeStyle.CalcSize(bContent);
            // 幅と高さを大幅に広げて余裕を確保（絶対に文字欠けしない）
            float bw = Mathf.Max(200f, bSize.x + 54f);
            float bh = Mathf.Max(36f, badgeSize + 18f);

            // 画面右端からしっかり離す（セーフエリアマージン 65〜110px）
            float rightMargin = Mathf.Clamp(Screen.width * 0.055f, 65f, 110f);
            float topMargin = Mathf.Clamp(Screen.height * 0.045f, 45f, 75f);
            Rect bRect = new Rect(Screen.width - bw - rightMargin, topMargin, bw, bh);

            if (_speechBg == null)
            {
                _speechBg = new Texture2D(1, 1);
                _speechBg.SetPixel(0, 0, new Color(0.04f, 0.07f, 0.12f, 0.92f));
                _speechBg.Apply();
            }
            GUI.DrawTexture(bRect, _speechBg);
            Rect bLine = new Rect(bRect.x, bRect.y, bw, 2.5f);
            GUI.DrawTexture(bLine, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, bColor, 0, 0);

            DrawOutlinedText(bRect, status, badgeStyle, bColor, new Color(0f, 0f, 0f, 0.95f));
        }

        // 3. セリフダイアログ表示（視認性抜群＆文字欠け防止設計）
        if (_speechTimer <= 0f || string.IsNullOrEmpty(_speechText))
            return;

        // しっかり大きく読みやすいシネマフォント設計（1080pで約30〜31pt）
        int bodyFontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.029f, 23f, 34f));
        int nameFontSize = Mathf.RoundToInt(bodyFontSize * 0.70f);

        // スタイル生成・キャッシュ
        if (_speechStyle == null)
        {
            _speechStyle = new GUIStyle();
            _speechBg = new Texture2D(1, 1);
            _speechBg.SetPixel(0, 0, new Color(0.04f, 0.07f, 0.12f, 0.90f));
            _speechBg.Apply();
        }

        // ウィンドウサイズの計算（拡大した文字が欠けずゆったり収まるサイズ）
        float boxWidth = Mathf.Clamp(Screen.width * 0.68f, 520f, 960f);
        float boxHeight = bodyFontSize * 2.8f + nameFontSize + 32f;
        float x = (Screen.width - boxWidth) * 0.5f;
        float y = Screen.height - boxHeight - Mathf.Clamp(Screen.height * 0.05f, 35f, 65f);

        float alpha = Mathf.Clamp01(_speechTimer);
        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);

        Rect boxRect = new Rect(x, y, boxWidth, boxHeight);

        // 1. 半透明ダーク背景（映画字幕風ウィンドウ）
        GUI.DrawTexture(boxRect, _speechBg);

        // 上部アクセントバー（エメラルドシアンの風の光彩ライン）
        Rect barRect = new Rect(x, y, boxWidth, 2.5f);
        GUI.DrawTexture(barRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.2f, 0.95f, 0.85f, 0.9f * alpha), 0, 0);

        // 2. ネームタグ [ 相棒 Rust ]
        GUIStyle nameStyle = new GUIStyle(GUI.skin.label);
        nameStyle.fontSize = nameFontSize;
        nameStyle.fontStyle = FontStyle.Bold;
        nameStyle.alignment = TextAnchor.MiddleLeft;
        nameStyle.clipping = TextClipping.Overflow;

        Rect nameRect = new Rect(x + 28f, y + 10f, boxWidth - 56f, nameFontSize + 4f);
        DrawOutlinedText(nameRect, "✦ 相棒 Rust", nameStyle, new Color(0.35f, 0.92f, 0.98f, alpha), new Color(0f, 0f, 0f, 0.9f * alpha));

        // 3. セリフ本文（大きくてはっきり読める・クリッピング防止）
        GUIStyle bodyStyle = new GUIStyle(GUI.skin.label);
        bodyStyle.fontSize = bodyFontSize;
        bodyStyle.fontStyle = FontStyle.Bold;
        bodyStyle.alignment = TextAnchor.UpperLeft;
        bodyStyle.wordWrap = true;
        bodyStyle.clipping = TextClipping.Overflow; // 上下左右の文字クリップを完全排除

        Rect bodyRect = new Rect(x + 28f, y + nameFontSize + 14f, boxWidth - 56f, bodyFontSize * 2.6f);
        DrawOutlinedText(bodyRect, "「" + _speechText + "」", bodyStyle, new Color(1.0f, 1.0f, 1.0f, alpha), new Color(0f, 0f, 0f, 0.95f * alpha));

        GUI.color = prevColor;
    }

    /// <summary>4方向の黒フチ取り（アウトライン）で背景色問わず100%くっきり描画</summary>
    static void DrawOutlinedText(Rect rect, string text, GUIStyle style, Color textColor, Color outlineColor)
    {
        int spread = Mathf.Max(1, style.fontSize / 16);
        Color origColor = style.normal.textColor;

        style.normal.textColor = outlineColor;
        GUI.Label(new Rect(rect.x - spread, rect.y, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x + spread, rect.y, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x, rect.y - spread, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x, rect.y + spread, rect.width, rect.height), text, style);

        style.normal.textColor = textColor;
        GUI.Label(rect, text, style);
        style.normal.textColor = origColor;
    }

    static AudioClip MakeSynthBeep(float startFreq, float endFreq, float duration)
    {
        const int hz = 44100;
        int samples = (int)(hz * duration);
        float[] data = new float[samples];
        float phase = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)samples;
            float freq = Mathf.Lerp(startFreq, endFreq, t);
            phase += 2f * Mathf.PI * freq / hz;
            float env = Mathf.Sin(t * Mathf.PI);
            data[i] = Mathf.Sin(phase) * env * 0.45f;
        }
        var clip = AudioClip.Create("SynthBeep", samples, 1, hz, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>油を差してもらって快調になったRustの祝祭・宙返り＆パーツレーダー演出</summary>
    IEnumerator CheerSpinRoutine()
    {
        // 1. 黄金の光粒子スパークルをバースト放出
        SpawnGoldSparkles(transform.position + Vector3.up * 0.4f, 26);

        string[] treatLines = {
            "わぁ…！ありがとうNiko、身体がすごく軽くなったよ…！",
            "油を差してくれてありがとう！ギアが滑らかに回ってるよ！",
            "ピピッ…！温かい手当てをありがとう。もうギシギシしないよ！"
        };
        SpeakCustom(treatLines[Random.Range(0, treatLines.Length)], 4.2f);

        // 2. 宙返りアニメーション（嬉しそうに一回転）
        float elapsed = 0f;
        float duration = 0.75f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float step = (Time.deltaTime / duration) * 360f;
            transform.Rotate(Vector3.right, step, Space.Self);
            transform.position += Vector3.up * (Mathf.Sin((elapsed / duration) * Mathf.PI) * 0.035f);
            yield return null;
        }

        // 3. 最寄りの未回収パーツ（スクラップ）を探知して方角を案内
        yield return new WaitForSeconds(0.4f);
        var allScraps = FindObjectsByType<AdventureScrapItem>(FindObjectsInactive.Exclude);
        AdventureScrapItem nearest = null;
        float minDist = float.MaxValue;
        foreach (var s in allScraps)
        {
            if (s == null || !s.gameObject.activeInHierarchy) continue;
            float d = Vector3.Distance(transform.position, s.transform.position);
            if (d < minDist)
            {
                minDist = d;
                nearest = s;
            }
        }

        if (nearest != null && minDist < 95f)
        {
            Vector3 diff = nearest.transform.position - transform.position;
            string dirName;
            if (Mathf.Abs(diff.x) > Mathf.Abs(diff.z))
                dirName = diff.x > 0 ? "東（右奥）" : "西（海側）";
            else
                dirName = diff.z > 0 ? "北（奥の高台）" : "南（浜辺側）";

            if (_audio != null && _sonarBeepClip != null)
                _audio.PlayOneShot(_sonarBeepClip, 0.7f);

            SpeakCustom($"ピピッ！{dirName}の方角から、古代パーツの共鳴を感じるよ！", 5.0f);
        }
    }

    void SpawnGoldSparkles(Vector3 pos, int count)
    {
        var go = new GameObject("Rust_GoldSparkles");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.8f;
        main.loop = false;
        main.startLifetime = 1.4f;
        main.startSpeed = 3.6f;
        main.startSize = 0.35f;
        main.startColor = new Color(1f, 0.90f, 0.35f, 0.95f); // 鮮やかなゴールド
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.45f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.92f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.6f, 0.1f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        var rend = go.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.SetTexture("_BaseMap", GetSoftSmokeTexture());
            rend.material = mat;
        }

        ps.Play();
        Destroy(go, 2.0f);
    }
}

