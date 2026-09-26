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
    AudioSource _audioSource;
    static AudioClip _chimeClip;

    public bool IsCollected => _isCollected;

    void Awake()
    {
        _startPos = transform.position;
        CreateVisual();
        SetupAudio();
        CreateSparkleFx();

        // 採取判定コライダー
        var col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 2.4f;
    }

    void Start()
    {
        // セーブ済み判定
        if (!string.IsNullOrEmpty(itemId) && PlayerPrefs.GetInt("Seashell_Collected_" + itemId, 0) == 1)
        {
            _isCollected = true;
            gameObject.SetActive(false);
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

        switch (kind)
        {
            case ShellKind.Sakuragai:
                itemName = "桜色のサクラガイ";
                rustReaction = "わぁ、花びらみたいな貝殻だね！";
                themeColor = new Color(1f, 0.72f, 0.82f, 1f);
                mat.color = new Color(1f, 0.82f, 0.88f, 0.95f);
                mat.SetFloat("_Smoothness", 0.85f);
                CreateShellMesh(vGo.transform, mat, isSakura: true);
                break;

            case ShellKind.SeaGlassEmerald:
                itemName = "エメラルド・シーグラス";
                rustReaction = "波に磨かれて角がすべすべだ！宝石みたい…！";
                themeColor = new Color(0.25f, 0.95f, 0.65f, 1f);
                mat.color = new Color(0.35f, 0.88f, 0.65f, 0.92f);
                mat.SetFloat("_Smoothness", 0.92f);
                CreateGlassMesh(vGo.transform, mat);
                break;

            case ShellKind.SeaGlassSapphire:
                itemName = "サファイア・シーグラス";
                rustReaction = "深海みたいな綺麗な青色！空に透かすとキラキラするよ！";
                themeColor = new Color(0.35f, 0.78f, 1f, 1f);
                mat.color = new Color(0.25f, 0.68f, 0.95f, 0.92f);
                mat.SetFloat("_Smoothness", 0.95f);
                CreateGlassMesh(vGo.transform, mat);
                break;

            case ShellKind.AmberPebble:
                itemName = "太陽の小琥珀";
                rustReaction = "黄金色に光ってる…！昔の太陽の光を閉じ込めたみたい！";
                themeColor = new Color(1f, 0.82f, 0.25f, 1f);
                mat.color = new Color(1f, 0.78f, 0.20f, 0.95f);
                mat.SetFloat("_Smoothness", 0.90f);
                CreateAmberMesh(vGo.transform, mat);
                break;

            case ShellKind.SpiralShell:
                itemName = "純白の小巻貝";
                rustReaction = "耳を当ててみて、Niko！遠くの波の音が聞こえるよ！";
                themeColor = new Color(0.95f, 0.98f, 1f, 1f);
                mat.color = new Color(0.96f, 0.95f, 0.90f, 1f);
                mat.SetFloat("_Smoothness", 0.75f);
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
        sphere.transform.localScale = new Vector3(0.18f, 0.035f, 0.22f);
        sphere.GetComponent<Renderer>().material = mat;
        Destroy(sphere.GetComponent<Collider>());
    }

    void CreateGlassMesh(Transform parent, Material mat)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(parent, false);
        cube.transform.localScale = new Vector3(0.14f, 0.05f, 0.16f);
        cube.transform.localRotation = Quaternion.Euler(15f, 30f, 10f);
        cube.GetComponent<Renderer>().material = mat;
        Destroy(cube.GetComponent<Collider>());
    }

    void CreateAmberMesh(Transform parent, Material mat)
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.SetParent(parent, false);
        sphere.transform.localScale = new Vector3(0.13f, 0.09f, 0.15f);
        sphere.GetComponent<Renderer>().material = mat;
        Destroy(sphere.GetComponent<Collider>());
    }

    void CreateSpiralMesh(Transform parent, Material mat)
    {
        var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        cap.transform.SetParent(parent, false);
        cap.transform.localScale = new Vector3(0.08f, 0.16f, 0.08f);
        cap.transform.localRotation = Quaternion.Euler(0f, 0f, 65f);
        cap.GetComponent<Renderer>().material = mat;
        Destroy(cap.GetComponent<Collider>());
    }

    void CreateSparkleFx()
    {
        var psGo = new GameObject("Sparkles");
        psGo.transform.SetParent(transform, false);
        psGo.transform.localPosition = Vector3.up * 0.12f;

        _sparklePs = psGo.AddComponent<ParticleSystem>();
        var main = _sparklePs.main;
        main.startLifetime = 1.2f;
        main.startSpeed = 0.18f;
        main.startSize = 0.08f;
        main.startColor = themeColor;
        main.loop = true;

        var emission = _sparklePs.emission;
        emission.rateOverTime = 2.5f;

        var shape = _sparklePs.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.22f;

        var colOverLifetime = _sparklePs.colorOverLifetime;
        colOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(themeColor, 0.6f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.4f), new GradientAlphaKey(0f, 1f) }
        );
        colOverLifetime.color = grad;
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

        // プレイヤー接近判定
        var p = AdventurePlayerController.Instance;
        if (p != null)
        {
            float dist = Vector3.Distance(transform.position, p.transform.position);
            _isPlayerNear = (dist < 2.3f);

            if (_isPlayerNear)
            {
                // Eキー入力、またはクリックで採取！
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

        // トースト通知を表示
        if (AdventureBeachSeashellManager.Instance != null)
        {
            AdventureBeachSeashellManager.Instance.NotifyCollected(this);
        }

        // 相棒Rustのリアクション
        var drone = AdventureRustDrone.Instance;
        if (drone != null && Random.value < 0.75f)
        {
            drone.SpeakCustom(rustReaction, 2.8f);
        }

        gameObject.SetActive(false);
    }

    void OnGUI()
    {
        if (_isCollected || _isCollecting || !_isPlayerNear || AdventurePauseMenu.IsOpen) return;

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
