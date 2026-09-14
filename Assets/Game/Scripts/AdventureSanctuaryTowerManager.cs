using UnityEngine;
using System.Collections;

/// <summary>
/// 『Rust & Float』クライマックス：島中央の白亜タワー頂上
/// 「アナログ真鍮レバー」と天蓋破壊シークエンスを統括するマネージャー
/// </summary>
public class AdventureSanctuaryTowerManager : MonoBehaviour
{
    static AdventureSanctuaryTowerManager _instance;
    public static AdventureSanctuaryTowerManager Instance => _instance;

    public static bool IsCanopyBroken { get; private set; } = false;

    Transform _leverHandle;
    Light _leverLight;
    ParticleSystem _crackPs;
    GameObject _hyperUpdraftGo;
    AudioSource _audio;

    bool _leverPulled = false;
    bool _playerNearby = false;
    bool _epilogueTriggered = false;
    float _epilogueAlpha = 0f;

    // ── 【案1】クライマックス演出制御 ──
    bool _climaxCrisisStarted = false;
    bool _climaxOilInjected = false;
    float _oilHoldTimer = 0f;
    const float OilHoldRequired = 1.2f;

    public static void Ensure()
    {
        var existing = Object.FindObjectsByType<AdventureSanctuaryTowerManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ex in existing)
        {
            if (ex != null && ex.gameObject != null)
                Destroy(ex.gameObject);
        }
        _instance = null;

        var go = new GameObject("AdventureSanctuaryTowerManager");
        _instance = go.AddComponent<AdventureSanctuaryTowerManager>();
    }

    void Awake()
    {
        _instance = this;
        IsCanopyBroken = false;
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    void Start()
    {
        SetupAudio();
        BuildTowerLever();
        var land = Terrain.activeTerrain ?? FindAnyObjectByType<Terrain>();
        BuildTowerStairs(transform, land);
    }

    void SetupAudio()
    {
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.spatialBlend = 0.5f;
        _audio.minDistance = 6f;
        _audio.maxDistance = 50f;
    }

    readonly Vector3 _mainLeverPos = new Vector3(512f, 63.2f, 501.5f); // オベリスク南側正面・白亜テラスの特等席
    readonly Vector3 _topLeverPos = new Vector3(512f, 137.2f, 512f);    // オベリスク天面頂上
    Transform _topLeverHandle;

    public Vector3 MainLeverPosition => _mainLeverPos;

    void BuildTowerLever()
    {
        // 既存の古いオブジェクトがあれば破棄して再構築
        var old = GameObject.Find("SanctuaryLeverStructure");
        if (old != null) Destroy(old);
        var oldTop = GameObject.Find("SanctuaryTopLeverStructure");
        if (oldTop != null) Destroy(oldTop);

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // ── 1. メインレバー（中央オベリスク南側正面・白亜広場） ──
        var root = new GameObject("SanctuaryLeverStructure");
        root.transform.SetParent(transform, false);
        root.transform.position = _mainLeverPos;

        // 白亜とチタンの円形台座（直径3.6m、高さ0.8m）
        var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pedestal.name = "LeverPedestal";
        pedestal.transform.SetParent(root.transform, false);
        pedestal.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        pedestal.transform.localScale = new Vector3(3.6f, 0.4f, 3.6f);
        var pedMr = pedestal.GetComponent<MeshRenderer>();
        if (pedMr != null)
        {
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0.94f, 0.96f, 0.98f)); // 純白大理石
            mat.SetFloat("_Smoothness", 0.92f);
            pedMr.material = mat;
        }

        // 真鍮の手動ギアハウジング
        var housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        housing.name = "BrassGearHousing";
        housing.transform.SetParent(root.transform, false);
        housing.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        housing.transform.localScale = new Vector3(1.2f, 0.55f, 0.95f);
        var hMr = housing.GetComponent<MeshRenderer>();
        if (hMr != null)
        {
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0.82f, 0.62f, 0.24f)); // 黄金真鍮
            mat.SetFloat("_Metallic", 0.95f);
            mat.SetFloat("_Smoothness", 0.78f);
            hMr.material = mat;
        }

        // アナログ真鍮レバー（手前へ倒せるハンドル）
        var leverPivot = new GameObject("LeverPivot");
        leverPivot.transform.SetParent(root.transform, false);
        leverPivot.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        leverPivot.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
        _leverHandle = leverPivot.transform;

        // シャフト
        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Shaft";
        shaft.transform.SetParent(_leverHandle, false);
        shaft.transform.localPosition = new Vector3(0f, 0.48f, 0f);
        shaft.transform.localScale = new Vector3(0.14f, 0.48f, 0.14f);
        var sMr = shaft.GetComponent<MeshRenderer>();
        if (sMr != null)
        {
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0.88f, 0.72f, 0.30f));
            mat.SetFloat("_Metallic", 0.92f);
            sMr.material = mat;
        }
        Destroy(shaft.GetComponent<Collider>());

        // 深紅の木製グリップ球
        var grip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        grip.name = "GripBall";
        grip.transform.SetParent(_leverHandle, false);
        grip.transform.localPosition = new Vector3(0f, 0.98f, 0f);
        grip.transform.localScale = Vector3.one * 0.36f;
        var gMr = grip.GetComponent<MeshRenderer>();
        if (gMr != null)
        {
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0.80f, 0.18f, 0.15f));
            mat.SetFloat("_Smoothness", 0.65f);
            gMr.material = mat;
        }
        Destroy(grip.GetComponent<Collider>());

        // 天空へ向かってそびえる光の柱ビーコン（遠くや空からでも一目でわかる！）
        var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "LeverSkyBeacon";
        beacon.transform.SetParent(root.transform, false);
        beacon.transform.localPosition = new Vector3(0f, 35f, 0f);
        beacon.transform.localScale = new Vector3(0.7f, 35f, 0.7f);
        Destroy(beacon.GetComponent<Collider>());
        var bRend = beacon.GetComponent<Renderer>();
        if (bRend != null)
        {
            var bShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var bMat = new Material(bShader);
            bMat.SetColor("_BaseColor", new Color(0.35f, 0.92f, 1.0f, 0.65f));
            bRend.material = bMat;
        }

        // 発光インジケーターライト
        var lightGo = new GameObject("LeverIndicatorLight");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        _leverLight = lightGo.AddComponent<Light>();
        _leverLight.type = LightType.Point;
        _leverLight.color = new Color(0.35f, 0.95f, 1.0f);
        _leverLight.intensity = 3.5f;
        _leverLight.range = 22f;

        // 接近判定コライダー
        var col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        // ── 2. 西側レバー（中央オベリスク西側・カルデラ湖側広場・プレイヤー正面） ──
        var westRoot = new GameObject("SanctuaryWestLeverStructure");
        westRoot.transform.SetParent(transform, false);
        westRoot.transform.position = new Vector3(501.5f, 63.2f, 512f);

        var westPed = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        westPed.name = "WestPedestal";
        westPed.transform.SetParent(westRoot.transform, false);
        westPed.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        westPed.transform.localScale = new Vector3(3.6f, 0.4f, 3.6f);
        var wPedMr = westPed.GetComponent<MeshRenderer>();
        if (wPedMr != null) wPedMr.material = pedMr.material;

        var westHousing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        westHousing.name = "WestBrassGearHousing";
        westHousing.transform.SetParent(westRoot.transform, false);
        westHousing.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        westHousing.transform.localScale = new Vector3(0.95f, 0.55f, 1.2f);
        var whMr = westHousing.GetComponent<MeshRenderer>();
        if (whMr != null) whMr.material = hMr.material;

        var westPivot = new GameObject("WestLeverPivot");
        westPivot.transform.SetParent(westRoot.transform, false);
        westPivot.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        westPivot.transform.localRotation = Quaternion.Euler(0f, 0f, -25f);
        _topLeverHandle = westPivot.transform;

        var westShaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        westShaft.name = "WestShaft";
        westShaft.transform.SetParent(westPivot.transform, false);
        westShaft.transform.localPosition = new Vector3(0f, 0.48f, 0f);
        westShaft.transform.localScale = new Vector3(0.14f, 0.48f, 0.14f);
        Destroy(westShaft.GetComponent<Collider>());
        var wsMr = westShaft.GetComponent<MeshRenderer>();
        if (wsMr != null) wsMr.material = sMr.material;

        var westGrip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        westGrip.name = "WestGripBall";
        westGrip.transform.SetParent(westPivot.transform, false);
        westGrip.transform.localPosition = new Vector3(0f, 0.98f, 0f);
        westGrip.transform.localScale = Vector3.one * 0.36f;
        Destroy(westGrip.GetComponent<Collider>());
        var wgMr = westGrip.GetComponent<MeshRenderer>();
        if (wgMr != null) wgMr.material = gMr.material;

        // 西側光のビーコン柱
        var westBeacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        westBeacon.name = "WestLeverSkyBeacon";
        westBeacon.transform.SetParent(westRoot.transform, false);
        westBeacon.transform.localPosition = new Vector3(0f, 35f, 0f);
        westBeacon.transform.localScale = new Vector3(0.7f, 35f, 0.7f);
        Destroy(westBeacon.GetComponent<Collider>());
        var wbRend = westBeacon.GetComponent<Renderer>();
        if (wbRend != null && bRend != null) wbRend.material = bRend.material;

        // ── 3. オベリスク頂上コンソール（標高137m・登り詰めたプレイヤー用） ──
        var topRoot = new GameObject("SanctuaryTopLeverStructure");
        topRoot.transform.SetParent(transform, false);
        topRoot.transform.position = _topLeverPos;

        var topPed = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        topPed.name = "TopPedestal";
        topPed.transform.SetParent(topRoot.transform, false);
        topPed.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        topPed.transform.localScale = new Vector3(3.0f, 0.35f, 3.0f);
        var tMr = topPed.GetComponent<MeshRenderer>();
        if (tMr != null) tMr.material = pedMr.material;

        var topPivot = new GameObject("TopLeverPivot");
        topPivot.transform.SetParent(topRoot.transform, false);
        topPivot.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        topPivot.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);

        var topShaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        topShaft.name = "TopShaft";
        topShaft.transform.SetParent(topPivot.transform, false);
        topShaft.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        topShaft.transform.localScale = new Vector3(0.14f, 0.45f, 0.14f);
        Destroy(topShaft.GetComponent<Collider>());
        var tsMr = topShaft.GetComponent<MeshRenderer>();
        if (tsMr != null) tsMr.material = sMr.material;

        var topGrip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        topGrip.name = "TopGripBall";
        topGrip.transform.SetParent(topPivot.transform, false);
        topGrip.transform.localPosition = new Vector3(0f, 0.92f, 0f);
        topGrip.transform.localScale = Vector3.one * 0.35f;
        Destroy(topGrip.GetComponent<Collider>());
        var tgMr = topGrip.GetComponent<MeshRenderer>();
        if (tgMr != null) tgMr.material = gMr.material;

        var topCol = topRoot.AddComponent<SphereCollider>();
        topCol.isTrigger = true;
        topCol.radius = 5.5f;
    }

    public bool IsPlayerNearLever => _playerNearby && !_leverPulled;

    void Update()
    {
        var player = AdventurePlayerController.Instance;
        if (player == null) return;

        // 天蓋破壊後、高度120m付近でRust危機イベント（【案1】クライマックス）を開始
        if (IsCanopyBroken && !_climaxCrisisStarted && player.transform.position.y >= 120f)
        {
            _climaxCrisisStarted = true;
            StartCoroutine(ClimaxCrisisSequenceRoutine());
        }

        if (_leverPulled) return;

        // ── 判定：白亜テラス広場全体（半径36m以内、標高58m〜78m）にいるか、またはオベリスク天面頂上にいるか ──
        Vector2 pXZ = new Vector2(player.transform.position.x, player.transform.position.z);
        float distFromCenter = Vector2.Distance(pXZ, new Vector2(512f, 512f));
        bool onTerrace = (distFromCenter < 36f && player.transform.position.y >= 58f && player.transform.position.y <= 78f);
        float distTop = Vector3.Distance(player.transform.position, _topLeverPos);
        _playerNearby = onTerrace || (distTop < 8.5f);

        // キーストーン集積状態によるライトの演出
        var scrapMgr = AdventureScrapManager.Instance;
        bool allCollected = scrapMgr != null && scrapMgr.CollectedCount >= 12;

        if (_leverLight != null)
        {
            if (allCollected)
            {
                float pulse = 2.2f + Mathf.Sin(Time.time * 4.5f) * 1.0f;
                _leverLight.intensity = pulse;
                _leverLight.color = new Color(0.35f, 0.95f, 1.0f); // 準備完了の鮮やかなシアン
            }
            else
            {
                _leverLight.intensity = 1.2f;
                _leverLight.color = new Color(1.0f, 0.75f, 0.25f);
            }
        }

        // インタラクト（Eキー、スペースキー、Enterキー、または直接入力）判定
        if (_playerNearby && (CheckLeverInputTriggered() || (player != null && player.InteractPressed)))
        {
            TryPullLever(allCollected);
        }
    }

    bool CheckLeverInputTriggered()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            if (kb.eKey.wasPressedThisFrame || kb.eKey.isPressed) return true;
            if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) return true;
        }
        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame)) return true;

        try
        {
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKey(KeyCode.E)) return true;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) return true;
        }
        catch { }
        return false;
    }

    void TryPullLever(bool allCollected)
    {
        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();

        if (!allCollected)
        {
            var scrapMgr = AdventureScrapManager.Instance;
            int count = scrapMgr != null ? scrapMgr.CollectedCount : 0;
            int remaining = 12 - count;
            if (drone != null)
            {
                drone.SpeakCustom($"まだレバーがロックされてるみたい…あと{remaining}個の遺物を集めて、僕たちの翼を完全に直そう！", 4.5f);
            }
            if (_audio != null)
                _audio.PlayOneShot(MakeClankSound(), 0.6f);
            return;
        }

        // 天蓋破壊シークエンス開始！
        _leverPulled = true;
        IsCanopyBroken = true;
        AdventureSaveManager.Instance?.SaveGame("天蓋開放・到達記録を保存しました");
        StartCoroutine(SkybreakSequenceRoutine());
    }

    IEnumerator SkybreakSequenceRoutine()
    {
        // 1. レバーをガチャンと手前へ引き倒す
        if (_audio != null)
            _audio.PlayOneShot(MakeHeavyLeverSound(), 0.9f);

        float elapsed = 0f;
        float duration = 0.65f;
        Quaternion startRot = _leverHandle.localRotation;
        Quaternion endRot = Quaternion.Euler(38f, 0f, 0f); // 手前へ強く引き倒す

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            _leverHandle.localRotation = Quaternion.Slerp(startRot, endRot, t * t);
            yield return null;
        }

        // 2. 地響きと火花スパーク
        SpawnLeverSparks(new Vector3(512f, 63.5f, 512f));

        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            drone.SpeakCustom("空が……割れるよ、Niko！つかまって！！", 4.5f);
        }

        yield return new WaitForSeconds(1.0f);

        // 3. 上空の天蓋に幾何学シールドの亀裂（Hex Grid Skybreak）が炸裂！
        SpawnSkybreakCracks(new Vector3(512f, 150f, 512f));

        // 4. シネマティック詩的ナレーション
        if (AdventureScrapHUD.Instance != null)
        {
            AdventureScrapHUD.Instance.ShowPoeticLore(
                "天蓋破壊：未知の荒野への跳躍",
                "空が割れた。100%最適化された無痛の箱庭が、音を立てて崩れ去っていく。\n冷たい本物の風が頬を打つ。傷つく自由を抱きしめて……飛べ、Niko！",
                "【大空の裂け目へダイブ！】中央タワーの光のウインドピラーから跳躍せよ！"
            );
        }

        // 5. タワー中央から上空180mの裂け目へ突き抜ける超巨大「天空スーパーサーマル」噴出！
        BuildSkybreakHyperUpdraft(new Vector3(512f, 62f, 512f));

        if (AdventurePettingAction.Instance != null)
        {
            AdventurePettingAction.Instance.PetRust("ありがとうRust…！君がいたからここまで来られた。行こう！", 3.2f);
        }

        yield return new WaitForSeconds(2.5f);

        if (drone != null)
        {
            drone.SpeakCustom("あれが本物の空だ……！風に乗って、あの裂け目へ飛び込もう、Niko！！", 6.0f);
        }
    }

    void SpawnLeverSparks(Vector3 pos)
    {
        var pGo = new GameObject("LeverSparkBurst");
        pGo.transform.position = pos;
        var ps = pGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startSpeed = 7f;
        main.startLifetime = 0.6f;
        main.startSize = 0.18f;
        main.startColor = new Color(1.0f, 0.85f, 0.35f);
        main.loop = false;
        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 35) });
        ps.Play();
        Destroy(pGo, 1.5f);
    }

    void SpawnSkybreakCracks(Vector3 skyCenter)
    {
        var crackGo = new GameObject("SkybreakEffect");
        crackGo.transform.position = skyCenter;
        var ps = crackGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startSpeed = 22f;
        main.startLifetime = 4.5f;
        main.startSize = 1.8f;
        main.startColor = new Color(0.35f, 0.95f, 1.0f, 0.85f); // シアンと黄金のガラス片破片
        main.loop = true;
        main.maxParticles = 180;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 45f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        var rend = crackGo.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0.45f, 0.95f, 1.0f, 0.85f));
            rend.material = mat;
        }

        ps.Play();
    }

    void BuildSkybreakHyperUpdraft(Vector3 basePos)
    {
        _hyperUpdraftGo = new GameObject("SkybreakHyperUpdraft");
        _hyperUpdraftGo.transform.position = basePos;

        var updraft = _hyperUpdraftGo.AddComponent<AdventureThermalUpdraft>();
        updraft.radius = 24.0f; // 巨大なウインドピラー
        updraft.height = 140.0f; // 高度180m以上の天蓋の裂け目まで突き抜ける
        updraft.liftSpeed = 16.5f; // 超高速で大空へ射出！
    }

    void OnGUI()
    {
        DrawClimaxCrisisGUI();

        if (_leverPulled || !_playerNearby)
        {
            DrawEpilogueGUI();
            return;
        }

        var scrapMgr = AdventureScrapManager.Instance;
        bool allCollected = scrapMgr != null && scrapMgr.CollectedCount >= 12;

        // 特大で押しやすいシネマティック操作ボタン（幅840px、高さ75px、フォント28pt）
        float w = 840f;
        float h = 75f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height - 150f;

        // ボタンの背景ボックス
        var boxRect = new Rect(x, y, w, h);
        GUI.color = new Color(0.02f, 0.05f, 0.10f, 0.95f);
        GUI.DrawTexture(boxRect, Texture2D.whiteTexture);

        // アクセント枠線
        Color accentCol = allCollected ? new Color(0.35f, 0.95f, 1.0f, 0.9f) : new Color(1.0f, 0.85f, 0.40f, 0.9f);
        GUI.color = accentCol;
        GUI.DrawTexture(new Rect(x, y, w, 3.5f), Texture2D.whiteTexture); // 上枠線
        GUI.DrawTexture(new Rect(x, y + h - 3.5f, w, 3.5f), Texture2D.whiteTexture); // 下枠線

        var btnStyle = new GUIStyle(GUI.skin.button);
        btnStyle.fontSize = 28; // 17ptから28ptへ特大化！
        btnStyle.fontStyle = FontStyle.Bold;
        btnStyle.alignment = TextAnchor.MiddleCenter;
        btnStyle.normal.background = Texture2D.whiteTexture;

        if (allCollected)
        {
            // 特大ボタン（Eキーまたはマウスクリックで即座に起動）
            GUI.color = new Color(0f, 0f, 0f, 0.01f); // 背景は透明（背面のDrawTextureを見せる）
            if (GUI.Button(boxRect, GUIContent.none, btnStyle))
            {
                TryPullLever(true);
            }

            // 黒アウトライン付き特大テキスト描画
            GUI.color = Color.white;
            var labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 28;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.alignment = TextAnchor.MiddleCenter;

            string btnText = "【Eキー または ここをクリック】真鍮レバーを引く（天蓋破壊・脱出）";
            // 黒アウトライン
            labelStyle.normal.textColor = new Color(0f, 0f, 0f, 0.95f);
            GUI.Label(new Rect(x - 2f, y - 2f, w, h), btnText, labelStyle);
            GUI.Label(new Rect(x + 2f, y + 2f, w, h), btnText, labelStyle);
            // 本文（輝くエメラルドシアン）
            labelStyle.normal.textColor = new Color(0.35f, 0.98f, 0.88f, 1.0f);
            GUI.Label(boxRect, btnText, labelStyle);
        }
        else
        {
            int count = scrapMgr != null ? scrapMgr.CollectedCount : 0;
            GUI.color = Color.white;
            var labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 24;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.normal.textColor = new Color(1.0f, 0.85f, 0.45f);
            GUI.Label(boxRect, $"【E】真鍮レバーを調べる（要：遺物パーツ 12個 / 現在 {count}個）", labelStyle);
        }

        GUI.color = Color.white;
        DrawEpilogueGUI();
    }

    void DrawClimaxCrisisGUI()
    {
        if (!_climaxCrisisStarted || _climaxOilInjected) return;

        // 映画のような上下黒帯
        Color barCol = new Color(0.02f, 0.04f, 0.08f, 0.88f);
        float barH = Screen.height * 0.12f;
        GUI.color = barCol;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, barH), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, Screen.height - barH, Screen.width, barH), Texture2D.whiteTexture);

        // 中央下部のインタラクティブ注油パネル（特大サイズで大迫力）
        float panelW = 880f;
        float panelH = 155f;
        float px = (Screen.width - panelW) * 0.5f;
        float py = Screen.height - panelH - 50f;

        var boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = Texture2D.whiteTexture;
        GUI.color = new Color(0.02f, 0.05f, 0.10f, 0.96f);
        GUI.Box(new Rect(px, py, panelW, panelH), GUIContent.none, boxStyle);

        // タイトル警告（特大24pt）
        GUI.color = new Color(1.0f, 0.40f, 0.35f, 1.0f);
        var titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 23;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(px, py + 12f, panelW, 32f), "⚠ 警告：極寒気流により相棒Rustが機能停止寸前！ ⚠", titleStyle);

        // アクション促し（黄金特大26pt）
        GUI.color = new Color(1.0f, 0.92f, 0.40f, 1.0f);
        var promptStyle = new GUIStyle(GUI.skin.label);
        promptStyle.fontSize = 26;
        promptStyle.fontStyle = FontStyle.Bold;
        promptStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(px, py + 48f, panelW, 38f), "【E 長押し】最後の常備油を注ぐ — 「一緒に飛ぶんだ、Rust！」", promptStyle);

        // プログレスバー背景（幅760px、太さ26px）
        float barW = 760f;
        float barH2 = 24f;
        float bx = px + (panelW - barW) * 0.5f;
        float by = py + 98f;

        GUI.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);
        GUI.DrawTexture(new Rect(bx, by, barW, barH2), Texture2D.whiteTexture);

        // プログレスバー進行ゲージ（黄金色）
        float fillRatio = Mathf.Clamp01(_oilHoldTimer / OilHoldRequired);
        GUI.color = new Color(1.0f, 0.82f, 0.22f, 1.0f);
        GUI.DrawTexture(new Rect(bx, by, barW * fillRatio, barH2), Texture2D.whiteTexture);

        GUI.color = Color.white;
    }

    IEnumerator ClimaxCrisisSequenceRoutine()
    {
        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        var player = AdventurePlayerController.Instance;

        // 1. スローモーション化（息をのむ緊張感）
        Time.timeScale = 0.35f;

        // 2. Rustの危機演出開始（凍結・失速・悲痛な叫び）
        if (drone != null)
            drone.StartClimaxCrisis();

        // 画面にシネマティックメッセージ
        if (AdventureScrapHUD.Instance != null)
        {
            AdventureScrapHUD.Instance.ShowPoeticLore(
                "緊急事態：凍てつく外気とRustの限界",
                "天蓋の裂け目から吹き込む極寒の逆風が、相棒の古いギアを容赦なく凍らせていく。\n「Niko……僕のエンジンがもたない……僕を置いて、先に行って……！」",
                "【E長押し】最後の常備油を注ぐ — 「一緒に飛ぶんだ、Rust！」"
            );
        }

        // 3. プレイヤーの長押し入力待ち（またはタイムリミット救済）
        float elapsed = 0f;
        while (!_climaxOilInjected && elapsed < 12.0f)
        {
            elapsed += Time.unscaledDeltaTime;

            bool eHolding = false;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.eKey.isPressed) eHolding = true;
            try { if (Input.GetKey(KeyCode.E)) eHolding = true; } catch { }

            if (eHolding)
            {
                _oilHoldTimer += Time.unscaledDeltaTime;
                if (_oilHoldTimer >= OilHoldRequired)
                {
                    _climaxOilInjected = true;
                }
            }
            else
            {
                _oilHoldTimer = Mathf.Max(0f, _oilHoldTimer - Time.unscaledDeltaTime * 1.5f);
            }

            yield return null;
        }

        // 4. 注油完了！Rustを抱きしめる
        _climaxOilInjected = true;
        if (drone != null)
            drone.StartClimaxPetAndOil();

        // 祈りと温もりの時間（1.2秒）
        yield return new WaitForSecondsRealtime(1.2f);

        // 5. 魂の再点火！オーバードライブ突入！
        if (drone != null)
            drone.TriggerClimaxOverdrive();

        // タイムスケールを徐々に復元
        float blend = 0f;
        while (blend < 1f)
        {
            blend += Time.unscaledDeltaTime * 1.6f;
            Time.timeScale = Mathf.Lerp(0.35f, 1.0f, blend);
            yield return null;
        }
        Time.timeScale = 1.0f;

        // 6. 二人の魂のロケットオーバードライブ推進力付与！
        if (player != null)
        {
            player.ApplyGlideBoost(3.2f, 75f);
            player.ApplyUpdraft(28f); // 一気に天蓋（高度150m以上）を突き破る！
        }

        // 7. 天蓋突破（高度150m超え）でエピローグへ
        yield return new WaitForSeconds(1.8f);
        _epilogueTriggered = true;
        StartCoroutine(EpilogueSequenceRoutine());
    }

    void DrawEpilogueGUI()
    {
        if (_epilogueAlpha <= 0.01f) return;

        // シネマティック・レターボックス（画面上下の映画黒帯）
        Color barCol = new Color(0.01f, 0.02f, 0.05f, _epilogueAlpha * 0.96f);
        float barH = Screen.height * 0.16f;
        GUI.color = barCol;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, barH), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, Screen.height - barH, Screen.width, barH), Texture2D.whiteTexture);

        // 画面中央のエピローグ・テキスト（特大サイズで大迫力映画字幕）
        float panelW = 1060f;
        float panelH = 320f;
        float px = (Screen.width - panelW) * 0.5f;
        float py = (Screen.height - panelH) * 0.5f;

        // タイトルスタイル（特大42pt・黄金の映画タイトル）
        var titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 42;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;

        // 本文スタイル（映画字幕25pt）
        var bodyStyle = new GUIStyle(GUI.skin.label);
        bodyStyle.fontSize = 25;
        bodyStyle.alignment = TextAnchor.MiddleCenter;

        // タイトル（黒アウトライン付き黄金テキスト）
        Rect titleRect = new Rect(px, py - 60f, panelW, 55f);
        titleStyle.normal.textColor = new Color(0f, 0f, 0f, _epilogueAlpha * 0.95f);
        GUI.Label(new Rect(titleRect.x - 2f, titleRect.y - 2f, titleRect.width, titleRect.height), "『Rust & Float』", titleStyle);
        GUI.Label(new Rect(titleRect.x + 2f, titleRect.y + 2f, titleRect.width, titleRect.height), "『Rust & Float』", titleStyle);
        titleStyle.normal.textColor = new Color(1.0f, 0.88f, 0.40f, _epilogueAlpha);
        GUI.Label(titleRect, "『Rust & Float』", titleStyle);

        string quote = "「100%最適化された幸福を脱獄した。\n傷つく自由と、風の重さを取り戻すために。」\n\n" +
                       "人は最短距離を走っている時ではなく、\n寄り道をして、躓き、\n予期せぬ美しさに息をのんだ瞬間にこそ\n生きている実感を得られる。\n\n" +
                       "── Niko & Rust の旅は、ここから始まる。";

        // 本文（黒アウトライン付きホワイトテキスト）
        Rect bodyRect = new Rect(px, py, panelW, panelH);
        bodyStyle.normal.textColor = new Color(0f, 0f, 0f, _epilogueAlpha * 0.95f);
        GUI.Label(new Rect(bodyRect.x - 1.5f, bodyRect.y - 1.5f, bodyRect.width, bodyRect.height), quote, bodyStyle);
        GUI.Label(new Rect(bodyRect.x + 1.5f, bodyRect.y + 1.5f, bodyRect.width, bodyRect.height), quote, bodyStyle);
        bodyStyle.normal.textColor = new Color(0.95f, 0.98f, 1.0f, _epilogueAlpha);
        GUI.Label(bodyRect, quote, bodyStyle);

        GUI.color = Color.white;
    }

    IEnumerator EpilogueSequenceRoutine()
    {
        var player = AdventurePlayerController.Instance;
        if (player != null)
        {
            // 無限スーパー滑空ブーストを付与
            player.ApplyGlideBoost(1.6f, 45f);
        }

        // 天蓋の外側に広がる未知の荒野（壮大な山脈シルエットと光芒）を出現
        SpawnWildernessPanorama();

        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            drone.SpeakCustom("わぁぁ……！見て、Niko！世界はこんなに広かったんだ……！！", 8.0f);
        }

        // エピローグテキストのフェードイン（3.5秒かけてじわっと表示）
        float t = 0f;
        while (t < 3.5f)
        {
            t += Time.deltaTime;
            _epilogueAlpha = Mathf.Clamp01(t / 3.5f);
            yield return null;
        }

        // 12秒間じっくり読ませる
        yield return new WaitForSeconds(12.0f);

        // フェードアウト（3秒）
        t = 3.0f;
        while (t > 0f)
        {
            t -= Time.deltaTime;
            _epilogueAlpha = Mathf.Clamp01(t / 3.0f);
            yield return null;
        }
        _epilogueAlpha = 0f;
    }

    /// <summary>天蓋の割れ目の外側に広がる「未知の地球・荒野の山脈シルエット」と光芒を生成</summary>
    void SpawnWildernessPanorama()
    {
        var panoramaGo = new GameObject("WildernessPanorama");
        panoramaGo.transform.position = new Vector3(512f, 90f, 512f);

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mountainMat = new Material(shader);
        mountainMat.color = new Color(0.18f, 0.22f, 0.35f); // 雄大な遠景の藍色シルエット

        // 全周12方向に連なる巨大な未知の山脈・稜線を配置
        for (int i = 0; i < 12; i++)
        {
            float ang = i * 30f * Mathf.Deg2Rad;
            float dist = 680f;
            Vector3 pos = new Vector3(Mathf.Cos(ang) * dist, Random.Range(10f, 40f), Mathf.Sin(ang) * dist);

            var peak = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            peak.name = $"WildernessRidge_{i}";
            peak.transform.SetParent(panoramaGo.transform, false);
            peak.transform.localPosition = pos;
            peak.transform.localScale = new Vector3(260f, Random.Range(85f, 150f), 260f);
            peak.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), i * 30f, Random.Range(-8f, 8f));

            var col = peak.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = peak.GetComponent<Renderer>();
            if (rend != null) rend.material = mountainMat;
        }

        // 天蓋の裂け目から差し込む金色の光芒（God Rays）
        var raysGo = new GameObject("SkybreakGodRays");
        raysGo.transform.SetParent(panoramaGo.transform, false);
        raysGo.transform.localPosition = new Vector3(0f, 60f, 0f);

        var rayShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        var rayMat = new Material(rayShader);
        rayMat.color = new Color(1.0f, 0.92f, 0.65f, 0.35f);

        for (int r = 0; r < 8; r++)
        {
            var ray = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ray.name = $"GodRay_{r}";
            ray.transform.SetParent(raysGo.transform, false);
            ray.transform.localScale = new Vector3(8f, 120f, 8f);
            ray.transform.localRotation = Quaternion.Euler(Random.Range(15f, 35f), r * 45f + 15f, 0f);

            var col = ray.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = ray.GetComponent<Renderer>();
            if (rend != null) rend.material = rayMat;
        }
    }

    /// <summary>オアシス湧水池（480, 455）からタワー台地（512, 512）へ登る白亜の古代神殿アプローチ階段道を生成</summary>
    void BuildTowerStairs(Transform parent, Terrain land)
    {
        var stairsRoot = new GameObject("SanctuaryApproachStairs");
        stairsRoot.transform.SetParent(parent, false);

        Vector3 startP = new Vector3(472f, 48.5f, 455f); // オアシス池のほとり
        Vector3 endP = new Vector3(512f, 62.5f, 512f);   // タワー基壇の入口
        if (land != null)
        {
            startP.y = land.SampleHeight(startP) + land.transform.position.y + 0.2f;
            endP.y = land.SampleHeight(endP) + land.transform.position.y + 0.2f;
        }

        int steps = 22;
        float width = 4.8f;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var marbleMat = new Material(shader);
        marbleMat.SetColor("_BaseColor", new Color(0.92f, 0.94f, 0.96f)); // 純白大理石
        marbleMat.SetFloat("_Smoothness", 0.85f);

        var pillarMat = new Material(shader);
        pillarMat.SetColor("_BaseColor", new Color(0.82f, 0.85f, 0.88f));

        for (int i = 0; i < steps; i++)
        {
            float t0 = (float)i / steps;
            float t1 = (float)(i + 1) / steps;
            Vector3 p0 = Vector3.Lerp(startP, endP, t0);
            Vector3 p1 = Vector3.Lerp(startP, endP, t1);

            if (land != null)
            {
                p0.y = Mathf.Max(p0.y, land.SampleHeight(p0) + land.transform.position.y + 0.15f);
                p1.y = Mathf.Max(p1.y, land.SampleHeight(p1) + land.transform.position.y + 0.15f);
            }

            Vector3 center = (p0 + p1) * 0.5f;
            Vector3 forward = (p1 - p0);
            float len = forward.magnitude;
            if (len < 0.01f) continue;

            Vector3 fwdNorm = forward.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwdNorm).normalized;

            var stepObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stepObj.name = $"MarbleStep_{i}";
            stepObj.transform.SetParent(stairsRoot.transform, false);
            stepObj.transform.position = center;
            stepObj.transform.rotation = Quaternion.LookRotation(fwdNorm, Vector3.up);
            stepObj.transform.localScale = new Vector3(width, 0.32f, len * 1.05f);

            var mr = stepObj.GetComponent<MeshRenderer>();
            if (mr != null) mr.material = marbleMat;

            // 4段ごとに両脇に白亜の装飾オベリスク支柱を配置
            if (i % 4 == 0)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    post.name = $"Pillar_{i}";
                    post.transform.SetParent(stairsRoot.transform, false);
                    post.transform.position = center + right * (side * (width * 0.5f + 0.35f)) + Vector3.up * 0.7f;
                    post.transform.localScale = new Vector3(0.24f, 0.7f, 0.24f);

                    var pmr = post.GetComponent<MeshRenderer>();
                    if (pmr != null) pmr.material = pillarMat;
                    var pcol = post.GetComponent<Collider>();
                    if (pcol != null) pcol.isTrigger = true;
                }
            }
        }
    }

    static AudioClip MakeClankSound()
    {
        int rate = 22050;
        int count = rate / 4;
        float[] d = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            d[i] = Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t * 18f);
        }
        var clip = AudioClip.Create("Clank", count, 1, rate, false);
        clip.SetData(d, 0);
        return clip;
    }

    static AudioClip MakeHeavyLeverSound()
    {
        int rate = 22050;
        int count = (int)(rate * 0.65f);
        float[] d = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float snap = Mathf.Sin(2f * Mathf.PI * 90f * t) * Mathf.Exp(-t * 6f);
            float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 14f) * 0.4f;
            d[i] = snap + noise;
        }
        var clip = AudioClip.Create("HeavyLever", count, 1, rate, false);
        clip.SetData(d, 0);
        return clip;
    }
}
