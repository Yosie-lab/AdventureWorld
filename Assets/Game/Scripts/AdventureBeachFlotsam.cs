using UnityEngine;

/// <summary>
/// 砂浜に漂着した文明のゴミ（2030〜2050年代の年代グラデーション）
/// 白砂に埋もれ、プレイヤーが近づいてEキーで調べるとRustがその遺物について穏やかに語りかける
/// </summary>
public class AdventureBeachFlotsam : MonoBehaviour
{
    public enum FlotsamEra
    {
        Era2050, // 波打ち際（最新のAI管理社会ゴミ：ドローン羽、生体リング、カプセル）
        Era2040, // 砂浜中央（高度管理社会ゴミ：健康バンド、規格化ボトル、ロボット部品）
        Era2030  // 砂浜奥・草むら境界（旧世代のアナログゴミ：スマホ、イヤホン、カセットテープ、時計）
    }

    [Header("漂着ゴミ設定")]
    public string itemId = "flotsam_01";
    public FlotsamEra era = FlotsamEra.Era2050;
    public string itemName = "旧世代のスマートフォン";
    public string eraLabel = "2030年代初頭の遺物";
    [TextArea(2, 4)]
    public string description = "画面にヒビが入った黒いガラス板。かつて人類が肌身離さず持っていた個人端末。";
    public string rustDialogue = "四角いガラスの板…昔の人は指で触って、遠くの誰かと繋がっていたんだね。";

    Transform _visual;
    bool _isPlayerNear = false;
    AudioSource _audio;
    static AudioClip _inspectClip;

    void Start()
    {
        CreateVisual();
        SetupAudio();

        // 当たり判定・インタラクション用コライダー
        var col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 2.4f;
    }

    void SetupAudio()
    {
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.spatialBlend = 0.85f;
        _audio.minDistance = 1.0f;
        _audio.maxDistance = 15f;
        _audio.playOnAwake = false;

        if (_inspectClip == null)
            _inspectClip = MakeInspectSound();
    }

    void CreateVisual()
    {
        var go = new GameObject("Visual");
        go.transform.SetParent(transform, false);
        _visual = go.transform;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        switch (era)
        {
            case FlotsamEra.Era2030:
                CreateVisual2030(go.transform, shader);
                break;
            case FlotsamEra.Era2040:
                CreateVisual2040(go.transform, shader);
                break;
            case FlotsamEra.Era2050:
                CreateVisual2050(go.transform, shader);
                break;
        }

        // 砂に少し埋もれた自然な傾き
        _visual.localRotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
    }

    void CreateVisual2030(Transform parent, Shader shader)
    {
        // 2030年代：アナログなスマートフォンやカセットテープ風の薄型直方体
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "PhoneBody";
        body.transform.SetParent(parent, false);
        body.transform.localScale = new Vector3(0.24f, 0.035f, 0.44f);
        Destroy(body.GetComponent<Collider>());

        var mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(0.18f, 0.18f, 0.20f)); // 黒ずんだマットブラック
        mat.SetFloat("_Metallic", 0.65f);
        mat.SetFloat("_Smoothness", 0.35f);
        body.GetComponent<Renderer>().material = mat;

        // ガラス画面
        var screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
        screen.name = "GlassScreen";
        screen.transform.SetParent(body.transform, false);
        screen.transform.localPosition = new Vector3(0f, 0.52f, 0f);
        screen.transform.localScale = new Vector3(0.92f, 0.1f, 0.92f);
        Destroy(screen.GetComponent<Collider>());

        var glassMat = new Material(shader);
        glassMat.SetColor("_BaseColor", new Color(0.08f, 0.12f, 0.15f));
        glassMat.SetFloat("_Metallic", 0.85f);
        glassMat.SetFloat("_Smoothness", 0.85f);
        screen.GetComponent<Renderer>().material = glassMat;
    }

    void CreateVisual2040(Transform parent, Shader shader)
    {
        // 2040年代：AI管理社会の合成樹脂ボトル・健康バンド（円筒・リング）
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "BandBody";
        ring.transform.SetParent(parent, false);
        ring.transform.localScale = new Vector3(0.38f, 0.08f, 0.38f);
        Destroy(ring.GetComponent<Collider>());

        var mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(0.85f, 0.88f, 0.90f)); // 規格化された白合成樹脂
        mat.SetFloat("_Metallic", 0.2f);
        mat.SetFloat("_Smoothness", 0.6f);
        ring.GetComponent<Renderer>().material = mat;

        // 小型センサー発光部
        var sensor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sensor.name = "Sensor";
        sensor.transform.SetParent(ring.transform, false);
        sensor.transform.localPosition = new Vector3(0.42f, 0.35f, 0f);
        sensor.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
        Destroy(sensor.GetComponent<Collider>());

        var sensorMat = new Material(shader);
        sensorMat.SetColor("_BaseColor", new Color(0.1f, 0.7f, 0.9f));
        sensorMat.EnableKeyword("_EMISSION");
        sensorMat.SetColor("_EmissionColor", new Color(0.1f, 0.6f, 0.8f) * 0.8f);
        sensor.GetComponent<Renderer>().material = sensorMat;
    }

    void CreateVisual2050(Transform parent, Shader shader)
    {
        // 2050年代：ドローンの折れたカーボンブレード（細長い翼状）
        var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade.name = "DroneBlade";
        blade.transform.SetParent(parent, false);
        blade.transform.localScale = new Vector3(0.16f, 0.025f, 0.75f);
        Destroy(blade.GetComponent<Collider>());

        var mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(0.22f, 0.25f, 0.28f)); // 高級カーボン
        mat.SetFloat("_Metallic", 0.85f);
        mat.SetFloat("_Smoothness", 0.9f);
        blade.GetComponent<Renderer>().material = mat;

        // 先端の生体・追跡チップ
        var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tip.name = "TipSensor";
        tip.transform.SetParent(blade.transform, false);
        tip.transform.localPosition = new Vector3(0f, 0.35f, 0.45f);
        tip.transform.localScale = new Vector3(0.85f, 0.65f, 0.15f);
        Destroy(tip.GetComponent<Collider>());

        var tipMat = new Material(shader);
        tipMat.SetColor("_BaseColor", new Color(0.2f, 0.95f, 0.85f));
        tipMat.EnableKeyword("_EMISSION");
        tipMat.SetColor("_EmissionColor", new Color(0.2f, 0.95f, 0.85f) * 1.2f);
        tip.GetComponent<Renderer>().material = tipMat;
    }

    void Update()
    {
        var player = AdventurePlayerController.Instance;
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.transform.position);
        _isPlayerNear = dist < 2.5f;

        if (_isPlayerNear && player.InteractPressed)
        {
            Inspect();
        }
    }

    public void Inspect()
    {
        if (_audio != null && _inspectClip != null)
            _audio.PlayOneShot(_inspectClip, 0.75f);

        // Rustにこの漂着ゴミについて穏やかに語らせる
        var drone = AdventureRustDrone.Instance ?? Object.FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            drone.SpeakCustom(rustDialogue, 5.0f);
        }

        // 通知・案内表示
        AdventureBeachFlotsamManager.Instance?.ShowInspectHUD(itemName, eraLabel, description);
    }

    void OnGUI()
    {
        if (!_isPlayerNear || Camera.main == null)
            return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 0.45f);
        if (screenPos.z <= 0.2f) return;

        float x = screenPos.x - 70f;
        float y = Screen.height - screenPos.y - 25f;

        GUIStyle promptStyle = new GUIStyle(GUI.skin.box);
        promptStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.020f, 16f, 24f));
        promptStyle.fontStyle = FontStyle.Bold;
        promptStyle.normal.textColor = new Color(1.0f, 0.95f, 0.8f);
        promptStyle.alignment = TextAnchor.MiddleCenter;

        GUI.Box(new Rect(x, y, 140f, promptStyle.fontSize + 12f), "【E】調べる", promptStyle);
    }

    static AudioClip MakeInspectSound()
    {
        const int rate = 44100;
        float duration = 0.35f;
        int count = (int)(rate * duration);
        float[] data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float env = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-t * 8f);
            // 砂を払うような優しいアコースティックなカサッという音
            float noise = (Mathf.Sin(i * 0.15f) + Mathf.Sin(i * 0.33f)) * 0.3f;
            float chime = Mathf.Sin(2f * Mathf.PI * 920f * t) * 0.35f;
            data[i] = (noise + chime) * env;
        }
        var clip = AudioClip.Create("InspectFlotsam", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
