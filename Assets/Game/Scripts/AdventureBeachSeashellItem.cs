using UnityEngine;

/// <summary>
/// 白砂ビーチの波打ち際に打ち上げられた貝殻・シーグラス・琥珀の採取アイテム。
/// 太陽光を反射してキラキラ輝き、近づいてEキーで拾うと
/// クリスタルチャイム音とともに手元へフワリと吸い込まれ、
/// 相棒Rustが嬉しそうに語りかける小気味よい収集ループを提供する。
/// </summary>
public class AdventureBeachSeashellItem : MonoBehaviour
{
    public enum ShellKind
    {
        Sakuragai,          // 桜色のサクラガイ（薄紅色の二枚貝）
        SeaGlassEmerald,    // エメラルド・シーグラス（波に磨かれた緑ガラス）
        SeaGlassSapphire,   // サファイア・シーグラス（深海の蒼いガラス片）
        AmberPebble,        // 太陽の小琥珀（黄金色の樹脂化石）
        SpiralShell         // 純白の小巻貝（耳に当てると波の音）
    }

    [Header("Item Properties")]
    public string itemId;
    public ShellKind kind = ShellKind.Sakuragai;
    public string itemName = "桜色のサクラガイ";
    public string rustReaction = "わぁ、花びらみたいな貝殻だね！";
    public Color themeColor = new Color(1f, 0.75f, 0.85f, 1f);

    bool _isCollected = false;
    bool _isPlayerNear = false;
    bool _isCollecting = false;
    float _collectAnimTimer = 0f;
    Vector3 _startPos;
    Transform _visualRoot;
    ParticleSystem _sparklePs;
    ParticleSystem _twinklePs;
    Light _pointLight;
    AudioSource _audioSource;
    static AudioClip _chimeClip;
    bool _initialized = false;

    public bool IsCollected => _isCollected;
    public Color itemColor => themeColor;

    void Awake()
    {
        _startPos = transform.position;

        // 採取判定コライダー（至近距離での自動接触採取）
        var col = GetComponent<SphereCollider>();
        if (col == null)
            col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 2.0f;
    }

    /// <summary>Managerから指定ID・種類を受け取って固有の見た目・効果を構築</summary>
    public void Initialize(string id, ShellKind shellKind)
    {
        itemId = id;
        kind = shellKind;
        _startPos = transform.position;

        CreateVisual();
        SetupAudio();
        CreateSparkleFx();
        _initialized = true;

        // セーブ済み判定
        if (!string.IsNullOrEmpty(itemId) && PlayerPrefs.GetInt("Seashell_Collected_" + itemId, 0) == 1)
        {
            _isCollected = true;
            gameObject.SetActive(false);
        }
    }

    void Start()
    {
        if (!_initialized)
        {
            Initialize(itemId, kind);
        }
    }

    void SetupAudio()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0.5f;
        _audioSource.minDistance = 1.5f;
        _audioSource.maxDistance = 15f;
        _audioSource.playOnAwake = false;

        if (_chimeClip == null)
            _chimeClip = CreateCollectChimeClip();
    }

    static AudioClip CreateCollectChimeClip()
    {
        int rate = 44100;
        float duration = 0.38f;
        int count = Mathf.RoundToInt(rate * duration);
        float[] samples = new float[count];

        // 澄んだクリスタルチャイム（F#6: 1480Hz -> A#6: 1865Hz -> C#7: 2217Hz の三和音アルペジオ）
        float[] notes = { 1479.98f, 1864.66f, 2217.46f };
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float sum = 0f;
            for (int n = 0; n < notes.Length; n++)
            {
                float noteStart = n * 0.045f;
                if (t >= noteStart)
                {
                    float noteT = t - noteStart;
                    float env = Mathf.Exp(-noteT * 12f);
                    float s = Mathf.Sin(2f * Mathf.PI * notes[n] * noteT) * 0.32f
                            + Mathf.Sin(2f * Mathf.PI * notes[n] * 2f * noteT) * 0.10f;
                    sum += s * env;
                }
            }
            samples[i] = Mathf.Clamp(sum, -1f, 1f);
        }

        var clip = AudioClip.Create("SeashellChime", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    void CreateVisual()
    {
        var vGo = new GameObject("Visual");
        vGo.transform.SetParent(transform, false);
        _visualRoot = vGo.transform;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader);
        mat.EnableKeyword("_EMISSION");

        switch (kind)
        {
            case ShellKind.Sakuragai:
                itemName = "桜色のサクラガイ";
                rustReaction = "わぁ、花びらみたいな貝殻だね！";
                themeColor = new Color(1f, 0.72f, 0.85f, 1f);
                mat.color = new Color(1f, 0.78f, 0.88f, 0.95f);
                mat.SetColor("_EmissionColor", new Color(1f, 0.65f, 0.80f) * 1.5f);
                mat.SetFloat("_Smoothness", 0.85f);
                CreateShellMesh(vGo.transform, mat, isSakura: true);
                break;

            case ShellKind.SeaGlassEmerald:
                itemName = "エメラルド・シーグラス";
                rustReaction = "波に磨かれて角がすべすべだ！宝石みたい…！";
                themeColor = new Color(0.20f, 0.98f, 0.65f, 1f);
                mat.color = new Color(0.25f, 0.92f, 0.65f, 0.92f);
                mat.SetColor("_EmissionColor", new Color(0.15f, 0.95f, 0.55f) * 1.6f);
                mat.SetFloat("_Smoothness", 0.90f);
                CreateGlassMesh(vGo.transform, mat);
                break;

            case ShellKind.SeaGlassSapphire:
                itemName = "サファイア・シーグラス";
                rustReaction = "深海みたいな綺麗な青色！空に透かすとキラキラするよ！";
                themeColor = new Color(0.30f, 0.80f, 1f, 1f);
                mat.color = new Color(0.20f, 0.72f, 0.98f, 0.92f);
                mat.SetColor("_EmissionColor", new Color(0.20f, 0.75f, 1f) * 1.6f);
                mat.SetFloat("_Smoothness", 0.90f);
                CreateGlassMesh(vGo.transform, mat);
                break;

            case ShellKind.AmberPebble:
                itemName = "太陽の小琥珀";
                rustReaction = "黄金色に光ってる…！昔の太陽の光を閉じ込めたみたい！";
                themeColor = new Color(1f, 0.85f, 0.25f, 1f);
                mat.color = new Color(1f, 0.80f, 0.18f, 0.95f);
                mat.SetColor("_EmissionColor", new Color(1f, 0.78f, 0.20f) * 1.5f);
                mat.SetFloat("_Smoothness", 0.85f);
                CreateAmberMesh(vGo.transform, mat);
                break;

            case ShellKind.SpiralShell:
                itemName = "純白の小巻貝";
                rustReaction = "耳を当ててみて、Niko！遠くの波の音が聞こえるよ！";
                themeColor = new Color(0.95f, 0.98f, 1f, 1f);
                mat.color = new Color(0.98f, 0.97f, 0.92f, 1f);
                mat.SetColor("_EmissionColor", new Color(0.90f, 0.95f, 1f) * 1.3f);
                mat.SetFloat("_Smoothness", 0.80f);
                CreateSpiralMesh(vGo.transform, mat);
                break;
        }

        // 砂浜に少し埋もれた自然な傾き
        _visualRoot.localRotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-12f, 12f));
    }

    void CreateShellMesh(Transform parent, Material mat, bool isSakura)
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.SetParent(parent, false);
        sphere.transform.localScale = new Vector3(0.26f, 0.055f, 0.32f); // 砂に埋もれずコロンと目立つ
        sphere.GetComponent<Renderer>().material = mat;
        Destroy(sphere.GetComponent<Collider>());
    }

    void CreateGlassMesh(Transform parent, Material mat)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(parent, false);
        cube.transform.localScale = new Vector3(0.22f, 0.08f, 0.24f); // 宝石のように立体的に光る
        cube.transform.localRotation = Quaternion.Euler(15f, 30f, 10f);
        cube.GetComponent<Renderer>().material = mat;
        Destroy(cube.GetComponent<Collider>());
    }

    void CreateAmberMesh(Transform parent, Material mat)
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.SetParent(parent, false);
        sphere.transform.localScale = new Vector3(0.20f, 0.14f, 0.22f);
        sphere.GetComponent<Renderer>().material = mat;
        Destroy(sphere.GetComponent<Collider>());
    }

    void CreateSpiralMesh(Transform parent, Material mat)
    {
        var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        cap.transform.SetParent(parent, false);
        cap.transform.localScale = new Vector3(0.13f, 0.26f, 0.13f);
        cap.transform.localRotation = Quaternion.Euler(0f, 0f, 65f);
        cap.GetComponent<Renderer>().material = mat;
        Destroy(cap.GetComponent<Collider>());
    }

    static Shader GetSafeUnlitShader()
    {
        return Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Mobile/Particles/Additive")
            ?? Shader.Find("Unlit/Transparent");
    }

    static Material CreateSafeGlowMaterial(Texture2D tex, Color color, string name)
    {
        var shader = GetSafeUnlitShader();
        var mat = new Material(shader) { name = name };
        mat.mainTexture = tex;
        mat.color = color;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1f); // Additive
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // Double-sided
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);

        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3100;
        return mat;
    }

    void CreateSparkleFx()
    {
        var smokeTex = AdventureRustDrone.GetSoftSmokeTexture();

        // 1. 周囲の砂浜を優しく照らすポイントライト
        var lightGo = new GameObject("ItemPointLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        _pointLight = lightGo.AddComponent<Light>();
        _pointLight.type = LightType.Point;
        _pointLight.range = 3.5f;
        _pointLight.intensity = 1.8f;
        _pointLight.color = themeColor;
        _pointLight.shadows = LightShadows.None;

        // 2. 上空にフワリと立ち上る光の蛍粒子（Rising Dust）
        var dustGo = new GameObject("RisingDust");
        dustGo.transform.SetParent(transform, false);
        dustGo.transform.localPosition = Vector3.up * 0.12f;

        _sparklePs = dustGo.AddComponent<ParticleSystem>();
        var mainDust = _sparklePs.main;
        mainDust.loop = true;
        mainDust.startLifetime = 1.8f;
        mainDust.startSpeed = 0.32f;
        mainDust.startSize = 0.22f;
        mainDust.startColor = themeColor;

        var emissionDust = _sparklePs.emission;
        emissionDust.rateOverTime = 4.5f;

        var rendDust = dustGo.GetComponent<ParticleSystemRenderer>();
        if (rendDust != null)
        {
            rendDust.sharedMaterial = CreateSafeGlowMaterial(smokeTex, themeColor, "SeashellDust_Mat");
            rendDust.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rendDust.receiveShadows = false;
        }
    }

    void Update()
    {
        if (_isCollected) return;

        // 採取アニメーション（Nikoの手元へフワリと吸い込まれる）
        if (_isCollecting)
        {
            _collectAnimTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_collectAnimTimer / 0.32f);

            var player = AdventurePlayerController.Instance;
            Vector3 targetHandPos = player != null ? player.transform.position + Vector3.up * 1.1f : transform.position;

            // 弧を描くイージング補間
            float arc = Mathf.Sin(t * Mathf.PI) * 0.45f;
            transform.position = Vector3.Lerp(_startPos, targetHandPos, Mathf.SmoothStep(0f, 1f, t)) + Vector3.up * arc;
            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.15f, t);

            if (t >= 1f)
            {
                FinishCollection();
            }
            return;
        }

        // ライトの優しいパルス
        float pulseTime = (Time.time * 3.2f) + (itemId != null ? itemId.GetHashCode() % 10 : 0);
        float pulse = Mathf.Sin(pulseTime);

        if (_pointLight != null)
        {
            _pointLight.intensity = 1.8f + pulse * 0.45f;
            _pointLight.range = 3.5f + pulse * 0.6f;
        }

        // プレイヤー接近判定
        var p = AdventurePlayerController.Instance;
        if (p != null)
        {
            // オープニングボード表示中やプロローグ目覚め中は採取・プロンプトを完全停止
            if (ShouldSuppressInteraction())
            {
                _isPlayerNear = false;
                return;
            }

            float dist = Vector3.Distance(transform.position, p.transform.position);

            // すぐ近く（2.0m以内）まで行くと自動で吸い込み採取！
            const float autoPickupDist = 2.0f;
            const float promptDist = 2.8f;

            if (dist < autoPickupDist)
            {
                _isPlayerNear = false;
                StartCollecting();
                return;
            }

            // 少し離れた距離（2.0m〜2.8m）ではEキー/クリックでの手動採取も受付
            _isPlayerNear = (dist < promptDist);

            if (_isPlayerNear)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                var mouse = UnityEngine.InputSystem.Mouse.current;
                bool pressed = (kb != null && kb.eKey.wasPressedThisFrame)
                            || (mouse != null && mouse.leftButton.wasPressedThisFrame);
                try { if (Input.GetKeyDown(KeyCode.E)) pressed = true; } catch { }

                if (pressed && !AdventurePauseMenu.IsOpen)
                {
                    StartCollecting();
                }
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // 高速ダッシュやジャンプ等で接触した場合も確実に自動採取
        if (_isCollecting || _isCollected || ShouldSuppressInteraction()) return;
        if (other.GetComponentInParent<AdventurePlayerController>() != null)
        {
            StartCollecting();
        }
    }

    static bool ShouldSuppressInteraction()
    {
        if (!AdventureRustFloatOpening.IsGameStarted) return true;
        if (AdventureRustFloatOpening.Instance != null && AdventureRustFloatOpening.Instance.IsModalBoardOpen()) return true;
        if (AdventurePrologueDrama.Instance != null && AdventurePrologueDrama.Instance.IsAwakening) return true;
        if (AdventurePauseMenu.IsOpen) return true;
        return false;
    }

    void StartCollecting()
    {
        if (_isCollecting || _isCollected) return;
        _isCollecting = true;
        _collectAnimTimer = 0f;
        _startPos = transform.position;

        // 澄んだクリスタル採取音
        float seVol = PlayerPrefs.GetFloat("Adventure_SeVolume", 1.0f);
        if (_audioSource != null && _chimeClip != null)
        {
            _audioSource.pitch = Random.Range(0.98f, 1.05f);
            _audioSource.PlayOneShot(_chimeClip, 0.75f * seVol);
        }

        // スパークルバースト
        if (_sparklePs != null)
        {
            _sparklePs.Emit(14);
        }

        // ライトを消灯
        if (_pointLight != null) _pointLight.enabled = false;
    }

    void FinishCollection()
    {
        _isCollected = true;
        _isCollecting = false;

        // 保存
        if (!string.IsNullOrEmpty(itemId))
        {
            PlayerPrefs.SetInt("Seashell_Collected_" + itemId, 1);
            PlayerPrefs.Save();
        }

        // トースト通知＆音響・Rustリアクション発火
        if (AdventureBeachSeashellManager.Instance != null)
        {
            AdventureBeachSeashellManager.Instance.NotifyCollected(this);
        }

        gameObject.SetActive(false);
    }

    void OnGUI()
    {
        if (_isCollected || _isCollecting || !_isPlayerNear || ShouldSuppressInteraction()) return;

        // 採取プロンプトHUD（画面内スクリーン座標に小さく「【E】拾う」）
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(transform.position + Vector3.up * 0.22f);
        if (screenPos.z < 0.5f) return;

        float x = screenPos.x;
        float y = Screen.height - screenPos.y;

        var style = new GUIStyle(GUI.skin.box);
        style.fontSize = 13;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = new Color(1f, 0.95f, 0.6f);
        style.alignment = TextAnchor.MiddleCenter;

        string prompt = $"【E】{itemName}を拾う";
        Vector2 size = style.CalcSize(new GUIContent(prompt)) + new Vector2(16f, 6f);
        GUI.Box(new Rect(x - size.x * 0.5f, y - size.y * 0.5f, size.x, size.y), prompt, style);
    }
}
