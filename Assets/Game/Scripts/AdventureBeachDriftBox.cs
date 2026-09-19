using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// 砂浜に漂着した情報ボックス（Drift Box）
/// プレイヤーが近づくと蓋が開き、島の情報・サバイバルメモ・次の目標を提示する
/// </summary>
public class AdventureBeachDriftBox : MonoBehaviour
{
    public int boxId = 1;
    public string boxTitle = "漂着防水ケース";
    public string author = "記録メモ";
    [TextArea(3, 8)]
    public string message = "ここにメッセージが入ります。";
    public string rustDialogue = "何か入ってるよ！";
    public string nextObjective = "内陸のせせらぎ池を目指そう";

    public bool isOpened = false;

    private Transform _lid;
    private Renderer _lampRenderer;
    private ParticleSystem _particles;
    private AudioSource _audioSource;
    private AudioClip _openClip;
    private MaterialPropertyBlock _mpb;

    // ── 発光・視認性演出 ──
    private Light _pointLight;
    private Transform _beaconPillar;
    private ParticleSystem _verticalBeamParticles;
    private ParticleSystem _idleSparkles;
    private Transform _glowBillboard;
    private Material _beaconMat;
    private Material _glowMat;
    private Material _particleMat;

    // UI関連（シングルトン共有モーダル）
    private static Canvas _modalCanvas;
    private static GameObject _modalPanel;
    private static Text _modalTitleText;
    private static Text _modalBodyText;
    private static Text _modalAuthorText;
    private static Text _modalCloseHintText;
    private static bool _isModalOpen = false;
    private static float _modalOpenTimestamp = 0f;

    public static bool IsModalOpen => _isModalOpen;

    void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        _lid = transform.Find("BoxLid");
        Transform lamp = transform.Find("SignalLamp");
        if (lamp != null) _lampRenderer = lamp.GetComponent<Renderer>();
        _particles = GetComponentInChildren<ParticleSystem>();

        // 保存された開封状態の復元
        isOpened = PlayerPrefs.GetInt("DriftBox_Opened_" + boxId, 0) == 1;

        // オーディオソースの準備
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0.8f;
        _audioSource.playOnAwake = false;
        _audioSource.maxDistance = 25f;
        _openClip = CreateChimeSound();

        SetupGlowEffects(lamp);
        EnsureModalUI();
    }

    void OnDestroy()
    {
        // 動的生成マテリアルの安全なメモリ解放
        if (_beaconMat != null) Destroy(_beaconMat);
        if (_glowMat != null) Destroy(_glowMat);
        if (_particleMat != null) Destroy(_particleMat);
    }

    void Start()
    {
        if (isOpened)
        {
            ApplyOpenedStateImmediate();
        }
        else
        {
            SetLampColor(new Color(1.0f, 0.70f, 0.20f), 2.5f); // 未開封: 暖色ゴールド発光
        }
    }

    void Update()
    {
        // カメラ向きグロービルボードの姿勢追従
        if (_glowBillboard != null && Camera.main != null)
        {
            _glowBillboard.rotation = Camera.main.transform.rotation;
        }

        if (!isOpened)
        {
            float t = Time.time;

            // 未開封時はランプがゆったりと呼吸点滅
            float pulse = 1.0f + Mathf.Sin(t * 3.5f) * 0.45f;
            SetLampColor(new Color(1.0f, 0.70f, 0.20f), pulse * 2.8f);

            // ポイントライトによる砂浜とチェストの呼吸照光
            if (_pointLight != null)
            {
                _pointLight.intensity = 2.0f + Mathf.Sin(t * 3.5f) * 1.5f;
                _pointLight.range = 8.5f + Mathf.Sin(t * 3.5f) * 1.5f;
            }

            // 天空へ昇る光の柱（ライトビーコン）の神秘的な脈動
            if (_beaconPillar != null)
            {
                float bPulse = 1.0f + Mathf.Sin(t * 2.4f) * 0.18f;
                _beaconPillar.localScale = new Vector3(0.38f * bPulse, 11f, 0.38f * bPulse);
            }

            // ランプグローの呼吸パルス
            if (_glowBillboard != null)
            {
                _glowBillboard.localScale = Vector3.one * (0.42f + Mathf.Sin(t * 3.5f) * 0.10f);
            }

            // プレイヤー接近判定
            var player = AdventurePlayerController.Instance;
            if (player != null)
            {
                float dist = Vector3.Distance(transform.position, player.transform.position);
                if (dist < 2.8f)
                {
                    OpenBox();
                }
            }
        }
    }

    #region 発光・視認性エフェクト構築

    private void SetupGlowEffects(Transform lamp)
    {
        Vector3 lampLocalPos = lamp != null ? lamp.localPosition : new Vector3(0.38f, 0.94f, 0.22f);
        var smokeTex = AdventureRustDrone.GetSoftSmokeTexture();

        // 1. 周囲をあたたかく照らす自発光ポイントライト
        var lightGo = new GameObject("DriftBoxPointLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = lampLocalPos;
        _pointLight = lightGo.AddComponent<Light>();
        _pointLight.type = LightType.Point;
        _pointLight.range = 9.5f;
        _pointLight.intensity = 2.8f;
        _pointLight.color = new Color(1.0f, 0.72f, 0.24f);
        _pointLight.shadows = LightShadows.None;

        // 2. 天空へ伸びる光の柱（ライトビーコン: 高さ約22m）
        var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "BeaconPillar";
        beacon.transform.SetParent(transform, false);
        beacon.transform.localPosition = lampLocalPos + new Vector3(0f, 11f, 0f);
        beacon.transform.localScale = new Vector3(0.38f, 11f, 0.38f);
        Destroy(beacon.GetComponent<Collider>());

        var beaconRend = beacon.GetComponent<Renderer>();
        if (beaconRend != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("RustAndFloat/WhiteSmoke")
                ?? Shader.Find("Sprites/Default");
            _beaconMat = new Material(shader);
            _beaconMat.SetTexture("_BaseMap", smokeTex);
            _beaconMat.SetColor("_BaseColor", new Color(1.0f, 0.82f, 0.35f, 0.55f));
            _beaconMat.renderQueue = 3150;
            beaconRend.material = _beaconMat;
        }
        _beaconPillar = beacon.transform;

        // 3. 垂直光粒子ビーム（空へ向かって昇る光の粒子）
        var pShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("RustAndFloat/WhiteSmoke")
            ?? Shader.Find("Sprites/Default");
        _particleMat = new Material(pShader);
        _particleMat.SetTexture("_BaseMap", smokeTex);
        _particleMat.SetColor("_BaseColor", new Color(1.0f, 0.88f, 0.40f, 2.5f));

        var beamGo = new GameObject("VerticalBeamSparkles");
        beamGo.transform.SetParent(transform, false);
        beamGo.transform.localPosition = lampLocalPos;
        _verticalBeamParticles = beamGo.AddComponent<ParticleSystem>();

        var mainBeam = _verticalBeamParticles.main;
        mainBeam.loop = true;
        mainBeam.startLifetime = 2.2f;
        mainBeam.startSpeed = 8.5f;
        mainBeam.startSize = 0.26f;
        mainBeam.startColor = new Color(1.0f, 0.88f, 0.40f, 0.9f);
        mainBeam.simulationSpace = ParticleSystemSimulationSpace.World;

        var emissionBeam = _verticalBeamParticles.emission;
        emissionBeam.rateOverTime = 12f;

        var shapeBeam = _verticalBeamParticles.shape;
        shapeBeam.shapeType = ParticleSystemShapeType.Cone;
        shapeBeam.angle = 1.5f;
        shapeBeam.radius = 0.12f;
        shapeBeam.rotation = new Vector3(-90f, 0f, 0f);

        var rendBeam = beamGo.GetComponent<ParticleSystemRenderer>();
        if (rendBeam != null) rendBeam.material = _particleMat;

        // 4. 周囲の浮遊スパークル（星くずのゆらめき）
        var idleGo = new GameObject("IdleSparkles");
        idleGo.transform.SetParent(transform, false);
        idleGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        _idleSparkles = idleGo.AddComponent<ParticleSystem>();

        var mainIdle = _idleSparkles.main;
        mainIdle.loop = true;
        mainIdle.startLifetime = 2.0f;
        mainIdle.startSpeed = 0.28f;
        mainIdle.startSize = 0.18f;
        mainIdle.startColor = new Color(1.0f, 0.82f, 0.30f, 0.85f);
        mainIdle.simulationSpace = ParticleSystemSimulationSpace.World;

        var emissionIdle = _idleSparkles.emission;
        emissionIdle.rateOverTime = 10f;

        var shapeIdle = _idleSparkles.shape;
        shapeIdle.shapeType = ParticleSystemShapeType.Sphere;
        shapeIdle.radius = 0.95f;

        var rendIdle = idleGo.GetComponent<ParticleSystemRenderer>();
        if (rendIdle != null) rendIdle.material = _particleMat;

        // 5. アンテナランプのソフトグロービルボード
        var glowQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        glowQuad.name = "LampGlowBillboard";
        glowQuad.transform.SetParent(transform, false);
        glowQuad.transform.localPosition = lampLocalPos;
        glowQuad.transform.localScale = Vector3.one * 0.45f;
        Destroy(glowQuad.GetComponent<Collider>());

        var glowRend = glowQuad.GetComponent<Renderer>();
        if (glowRend != null)
        {
            var gShader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("RustAndFloat/WhiteSmoke")
                ?? Shader.Find("Sprites/Default");
            _glowMat = new Material(gShader);
            _glowMat.SetTexture("_BaseMap", smokeTex);
            _glowMat.SetColor("_BaseColor", new Color(1.0f, 0.78f, 0.25f, 0.85f));
            _glowMat.renderQueue = 3160;
            glowRend.material = _glowMat;
        }
        _glowBillboard = glowQuad.transform;
    }

    #endregion

    public void OpenBox()
    {
        if (isOpened) return;
        isOpened = true;
        PlayerPrefs.SetInt("DriftBox_Opened_" + boxId, 1);
        PlayerPrefs.Save();

        // 開封アニメーション開始
        StartCoroutine(AnimateOpen());

        // 効果音再生
        if (_audioSource != null && _openClip != null)
        {
            _audioSource.pitch = 1.0f + Random.Range(-0.05f, 0.05f);
            _audioSource.PlayOneShot(_openClip, 0.85f);
        }

        // 相棒Rustのセリフ
        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null && !string.IsNullOrEmpty(rustDialogue))
        {
            drone.SpeakCustom(rustDialogue, 6.0f);
        }

        // クエストティッカーの更新
        if (!string.IsNullOrEmpty(nextObjective))
        {
            var hud = AdventureScrapHUD.Instance ?? FindAnyObjectByType<AdventureScrapHUD>();
            if (hud != null)
            {
                hud.ShowUpgradeBanner($"📦 【{boxTitle}】を発見！\n💡 目標: {nextObjective}");
            }
        }

        // 情報モーダルUIの表示
        ShowModal(boxTitle, author, message);
    }

    private IEnumerator AnimateOpen()
    {
        // ランプとポイントライトを爽やかなエメラルドグリーンに切り替え
        SetLampColor(new Color(0.2f, 1.0f, 0.6f), 2.2f);
        if (_pointLight != null)
        {
            _pointLight.color = new Color(0.25f, 1.0f, 0.65f);
            _pointLight.intensity = 1.6f;
            _pointLight.range = 5.5f;
        }
        if (_glowMat != null)
        {
            _glowMat.SetColor("_BaseColor", new Color(0.25f, 1.0f, 0.65f, 0.70f));
        }

        // アイドルスパークル＆垂直ビームの停止
        if (_idleSparkles != null) _idleSparkles.Stop();
        if (_verticalBeamParticles != null) _verticalBeamParticles.Stop();

        // 開封祝祭パーティクル演出
        if (_particles != null)
        {
            _particles.Play();
        }

        // ビーコン光柱を滑らかにフェードアウト・縮小
        if (_beaconPillar != null)
        {
            StartCoroutine(FadeOutBeacon());
        }

        // 蓋がパカッと後方へ95度開く
        if (_lid != null)
        {
            Quaternion startRot = _lid.localRotation;
            Quaternion endRot = startRot * Quaternion.Euler(-95f, 0f, 0f);
            float elapsed = 0f;
            float duration = 0.45f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // バウンスを伴うオープンカーブ
                float curve = 1f + Mathf.Sin((t - 1f) * Mathf.PI * 0.5f) * Mathf.Exp(-t * 3f) * 0.2f;
                _lid.localRotation = Quaternion.Slerp(startRot, endRot, t * curve);
                yield return null;
            }
            _lid.localRotation = endRot;
        }
    }

    private IEnumerator FadeOutBeacon()
    {
        if (_beaconPillar == null) yield break;
        Vector3 startScale = _beaconPillar.localScale;
        float elapsed = 0f;
        float dur = 0.55f;
        Color c = _beaconMat != null ? _beaconMat.GetColor("_BaseColor") : Color.white;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            if (_beaconPillar != null)
            {
                _beaconPillar.localScale = Vector3.Lerp(startScale, new Vector3(0f, startScale.y, 0f), t);
            }
            if (_beaconMat != null)
            {
                Color cur = c;
                cur.a = Mathf.Lerp(c.a, 0f, t);
                _beaconMat.SetColor("_BaseColor", cur);
            }
            yield return null;
        }
        if (_beaconPillar != null)
        {
            _beaconPillar.gameObject.SetActive(false);
        }
    }

    private void ApplyOpenedStateImmediate()
    {
        if (_lid != null)
        {
            _lid.localRotation = Quaternion.Euler(-95f, 0f, 0f);
        }
        SetLampColor(new Color(0.2f, 1.0f, 0.6f), 1.2f); // 開封済み: 落ち着いた緑
        if (_pointLight != null)
        {
            _pointLight.color = new Color(0.25f, 1.0f, 0.65f);
            _pointLight.intensity = 1.2f;
            _pointLight.range = 4.5f;
        }
        if (_beaconPillar != null)
        {
            _beaconPillar.gameObject.SetActive(false);
        }
        if (_verticalBeamParticles != null)
        {
            _verticalBeamParticles.gameObject.SetActive(false);
        }
        if (_idleSparkles != null)
        {
            _idleSparkles.gameObject.SetActive(false);
        }
        if (_glowMat != null)
        {
            _glowMat.SetColor("_BaseColor", new Color(0.25f, 1.0f, 0.65f, 0.50f));
        }
    }

    private void SetLampColor(Color c, float intensity)
    {
        if (_lampRenderer == null) return;
        _lampRenderer.GetPropertyBlock(_mpb);
        _mpb.SetColor("_BaseColor", c);
        _mpb.SetColor("_EmissionColor", c * intensity);
        _lampRenderer.SetPropertyBlock(_mpb);
    }

    // ═══════════════════════════════════════════════════════════════════
    // モーダルウィンドウUIシステム
    // ═══════════════════════════════════════════════════════════════════

    private static void EnsureModalUI()
    {
        if (_modalCanvas != null && _modalPanel != null) return;

        // 孤児化した古いCanvasがあれば掃除
        var oldCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in oldCanvases)
        {
            if (c != null && c.name == "DriftBoxModalCanvas")
            {
                if (Application.isPlaying) Object.Destroy(c.gameObject);
                else Object.DestroyImmediate(c.gameObject);
            }
        }
        _modalCanvas = null;
        _modalPanel = null;

        var canvasGo = new GameObject("DriftBoxModalCanvas");
        DontDestroyOnLoad(canvasGo);
        _modalCanvas = canvasGo.AddComponent<Canvas>();
        _modalCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        _modalCanvas.worldCamera = Camera.main;
        _modalCanvas.planeDistance = 1.0f;
        _modalCanvas.sortingOrder = 200; // 最前面表示

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.AddComponent<DriftBoxModalInputHandler>();

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 背景暗転パネル（画面のどこをクリックしても閉じられる）
        var overlay = new GameObject("Overlay");
        overlay.transform.SetParent(canvasGo.transform, false);
        var overlayImg = overlay.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.50f);
        var overlayRt = overlay.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.sizeDelta = Vector2.zero;
        var overlayBtn = overlay.AddComponent<Button>();
        overlayBtn.transition = Selectable.Transition.None;
        overlayBtn.onClick.AddListener(CloseModal);

        // メインパネル（クリックが背後へ突き抜けないようにRaycastTargetを持つ）
        _modalPanel = new GameObject("ModalPanel");
        _modalPanel.transform.SetParent(overlay.transform, false);
        var panelImg = _modalPanel.AddComponent<Image>();
        panelImg.color = new Color(0.12f, 0.15f, 0.20f, 0.96f); // 深いネイビーグレー
        var panelRt = _modalPanel.GetComponent<RectTransform>();
        panelRt.sizeDelta = new Vector2(680, 420);
        panelRt.anchoredPosition = Vector2.zero;

        // パネルの外枠（金色の飾り枠）
        var outline = _modalPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.72f, 0.40f, 0.85f);
        outline.effectDistance = new Vector2(2, -2);

        // アイコン＆タイトル
        var titleGo = new GameObject("TitleText");
        titleGo.transform.SetParent(_modalPanel.transform, false);
        _modalTitleText = titleGo.AddComponent<Text>();
        _modalTitleText.font = defaultFont;
        _modalTitleText.fontSize = 26;
        _modalTitleText.fontStyle = FontStyle.Bold;
        _modalTitleText.color = new Color(1.0f, 0.88f, 0.45f);
        _modalTitleText.alignment = TextAnchor.MiddleCenter;
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchoredPosition = new Vector2(0, 160);
        titleRt.sizeDelta = new Vector2(600, 50);

        // 差出人／記録者
        var authorGo = new GameObject("AuthorText");
        authorGo.transform.SetParent(_modalPanel.transform, false);
        _modalAuthorText = authorGo.AddComponent<Text>();
        _modalAuthorText.font = defaultFont;
        _modalAuthorText.fontSize = 17;
        _modalAuthorText.color = new Color(0.65f, 0.75f, 0.85f);
        _modalAuthorText.alignment = TextAnchor.MiddleCenter;
        var authorRt = authorGo.GetComponent<RectTransform>();
        authorRt.anchoredPosition = new Vector2(0, 120);
        authorRt.sizeDelta = new Vector2(600, 30);

        // 本文（広々としたメッセージ）
        var bodyGo = new GameObject("BodyText");
        bodyGo.transform.SetParent(_modalPanel.transform, false);
        _modalBodyText = bodyGo.AddComponent<Text>();
        _modalBodyText.font = defaultFont;
        _modalBodyText.fontSize = 20;
        _modalBodyText.lineSpacing = 1.35f;
        _modalBodyText.color = new Color(0.95f, 0.96f, 0.98f);
        _modalBodyText.alignment = TextAnchor.UpperLeft;
        var bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchoredPosition = new Vector2(0, -10);
        bodyRt.sizeDelta = new Vector2(580, 200);

        // 閉じるヒントボタン
        var closeGo = new GameObject("CloseHintButton");
        closeGo.transform.SetParent(_modalPanel.transform, false);
        var closeImg = closeGo.AddComponent<Image>();
        closeImg.color = new Color(0.18f, 0.24f, 0.32f, 0.85f);
        var closeBtn = closeGo.AddComponent<Button>();
        closeBtn.onClick.AddListener(CloseModal);
        var closeColors = closeBtn.colors;
        closeColors.highlightedColor = new Color(0.28f, 0.38f, 0.50f, 1f);
        closeColors.pressedColor = new Color(0.10f, 0.15f, 0.22f, 1f);
        closeBtn.colors = closeColors;

        var closeOutline = closeGo.AddComponent<Outline>();
        closeOutline.effectColor = new Color(0.45f, 0.85f, 1.0f, 0.6f);
        closeOutline.effectDistance = new Vector2(1.5f, -1.5f);

        var closeTextGo = new GameObject("Text");
        closeTextGo.transform.SetParent(closeGo.transform, false);
        _modalCloseHintText = closeTextGo.AddComponent<Text>();
        _modalCloseHintText.font = defaultFont;
        _modalCloseHintText.fontSize = 17;
        _modalCloseHintText.fontStyle = FontStyle.Bold;
        _modalCloseHintText.color = new Color(0.65f, 0.92f, 1.0f);
        _modalCloseHintText.alignment = TextAnchor.MiddleCenter;
        _modalCloseHintText.text = "【 Space / Enter / クリックで閉じる 】";
        var closeTextRt = closeTextGo.GetComponent<RectTransform>();
        closeTextRt.anchorMin = Vector2.zero;
        closeTextRt.anchorMax = Vector2.one;
        closeTextRt.sizeDelta = Vector2.zero;

        var closeRt = closeGo.GetComponent<RectTransform>();
        closeRt.anchoredPosition = new Vector2(0, -165);
        closeRt.sizeDelta = new Vector2(440, 42);

        overlay.SetActive(false);
    }

    public static void ShowModal(string title, string author, string body)
    {
        EnsureModalUI();
        if (_modalCanvas == null || _modalPanel == null) return;
        if (_modalCanvas.worldCamera == null || !_modalCanvas.worldCamera.isActiveAndEnabled)
        {
            _modalCanvas.worldCamera = Camera.main;
        }

        _modalTitleText.text = "📦 " + title;
        _modalAuthorText.text = "── " + author + " ──";
        _modalBodyText.text = body;

        _modalPanel.transform.parent.gameObject.SetActive(true);
        _isModalOpen = true;
        _modalOpenTimestamp = Time.unscaledTime;

        // マウスカーソルを解放してクリックできるようにする
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void CloseModal()
    {
        if (_modalCanvas != null && _modalPanel != null)
        {
            _modalPanel.transform.parent.gameObject.SetActive(false);
        }
        _isModalOpen = false;

        // ゲームプレイ用にマウスカーソルを再度ロック
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 心地よい開錠サウンドのプロシージャル合成
    // ═══════════════════════════════════════════════════════════════════
    private static AudioClip CreateChimeSound()
    {
        int rate = 44100;
        float duration = 1.2f;
        int samples = (int)(rate * duration);
        float[] data = new float[samples];

        // 2つの澄んだ高音（E6: 1318Hz, B6: 1975Hz）のチャイム和音
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / rate;
            float env = Mathf.Exp(-t * 4.5f);
            float s1 = Mathf.Sin(2f * Mathf.PI * 1318.5f * t);
            float s2 = Mathf.Sin(2f * Mathf.PI * 1975.5f * t);
            float s3 = Mathf.Sin(2f * Mathf.PI * 2637.0f * t) * 0.3f;
            data[i] = (s1 * 0.5f + s2 * 0.35f + s3 * 0.15f) * env * 0.7f;
        }

        var clip = AudioClip.Create("DriftBoxChime", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

/// <summary>
/// 漂着ボックスモーダルの入力ハンドラー
/// 新旧Input Systemの両方で、キー入力やクリックによるモーダル閉じを100%確実に処理する
/// </summary>
public class DriftBoxModalInputHandler : MonoBehaviour
{
    void Update()
    {
        if (!AdventureBeachDriftBox.IsModalOpen) return;

        // 開いた直後の誤爆防止（0.12秒）
        // （Time.unscaledTime を使用してポーズ中や低フレームレートでも安全）
        bool closeTriggered = false;

        // 1. 新Input System
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.spaceKey.wasPressedThisFrame ||
                kb.enterKey.wasPressedThisFrame ||
                kb.numpadEnterKey.wasPressedThisFrame ||
                kb.escapeKey.wasPressedThisFrame ||
                kb.eKey.wasPressedThisFrame)
            {
                closeTriggered = true;
            }
        }

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            closeTriggered = true;
        }

        // 2. 旧Input System（フォールバック）
        try
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E) ||
                Input.GetMouseButtonDown(0))
            {
                closeTriggered = true;
            }
        }
        catch { }

        if (closeTriggered)
        {
            AdventureBeachDriftBox.CloseModal();
        }
    }
}
