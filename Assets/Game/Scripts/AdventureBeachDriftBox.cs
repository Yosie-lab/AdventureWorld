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

    // ── パラメータ定数 ──
    private static class VisualConfig
    {
        public static readonly Color UnopenedGold = new Color(1.0f, 0.72f, 0.18f);
        public static readonly Color OpenedEmerald = new Color(0.20f, 1.0f, 0.60f);
        public static readonly Color OpenedLightColor = new Color(0.25f, 1.0f, 0.65f);

        public const float TriggerDistance = 4.5f; // 2.8fから4.5fへ拡大（確実に反応）
        public const float LightRangeUnopenedBase = 18.0f; // 8.5fから倍増
        public const float LightRangeUnopenedPulse = 4.0f;
        public const float LightIntensityUnopenedBase = 6.5f; // 2.0fから大幅強化
        public const float LightIntensityUnopenedPulse = 3.5f; // 最大10.0fまで脈動

        public const float LightRangeOpened = 8.0f;
        public const float LightIntensityOpened = 2.5f;

        public const float BeaconHeight = 65f; // 22fから65fへ大幅伸長（遠景から一目瞭然）
        public const float BeaconRadius = 0.85f; // 0.38fから2倍以上太く
    }

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
    private Transform _glowHaloBillboard;
    private Material _beaconMat;
    private Material _glowMat;
    private Material _haloMat;
    private Material _particleMat;
    private Coroutine _beaconFadeCoroutine;

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
        _audioSource.maxDistance = 35f;
        _openClip = CreateChimeSound();

        SetupGlowEffects(lamp);
        EnsureModalUI();
    }

    void OnDestroy()
    {
        // 動的生成マテリアルの安全なメモリ解放
        if (_beaconMat != null) Destroy(_beaconMat);
        if (_glowMat != null) Destroy(_glowMat);
        if (_haloMat != null) Destroy(_haloMat);
        if (_particleMat != null) Destroy(_particleMat);
    }

    void Start()
    {
        ApplyVisualState(isOpened, immediate: true);
    }

    void Update()
    {
        // カメラ向きグロービルボードの姿勢追従
        if (Camera.main != null)
        {
            var camRot = Camera.main.transform.rotation;
            if (_glowBillboard != null) _glowBillboard.rotation = camRot;
            if (_glowHaloBillboard != null) _glowHaloBillboard.rotation = camRot;
        }

        // 実行時デバッグ：Shift + B で全ドリフトボックスを即座に未開封リセット
        CheckDebugResetKey();

        if (!isOpened)
        {
            UpdateUnopenedPulses();
            CheckPlayerProximity();
        }
        else
        {
            // 開封済みでも近くでEキーを押せばいつでもメモを再読可能
            CheckReopenProximity();
        }
    }

    private void CheckDebugResetKey()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var kb = Keyboard.current;
        if (kb != null && kb.bKey.wasPressedThisFrame && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed))
        {
            ResetAllBoxesStatic();
        }
#endif
    }

    private void UpdateUnopenedPulses()
    {
        float t = Time.time;

        // ランプの呼吸点滅（眩しい黄金色）
        float pulse = 1.0f + Mathf.Sin(t * 3.5f) * 0.5f;
        SetLampColor(VisualConfig.UnopenedGold, pulse * 5.0f);

        // ポイントライトによる砂浜とチェストの呼吸照光（遠くまで届く）
        if (_pointLight != null)
        {
            _pointLight.intensity = VisualConfig.LightIntensityUnopenedBase + Mathf.Sin(t * 3.5f) * VisualConfig.LightIntensityUnopenedPulse;
            _pointLight.range = VisualConfig.LightRangeUnopenedBase + Mathf.Sin(t * 3.5f) * VisualConfig.LightRangeUnopenedPulse;
        }

        // 天空へ昇る光の柱（ライトビーコン）の神秘的な脈動
        if (_beaconPillar != null)
        {
            float bPulse = 1.0f + Mathf.Sin(t * 2.2f) * 0.22f;
            _beaconPillar.localScale = new Vector3(VisualConfig.BeaconRadius * bPulse, VisualConfig.BeaconHeight * 0.5f, VisualConfig.BeaconRadius * bPulse);
        }

        // ランプグロー（内側コア＋外側ハロー）の呼吸パルス
        if (_glowBillboard != null)
        {
            _glowBillboard.localScale = Vector3.one * (0.65f + Mathf.Sin(t * 3.5f) * 0.15f);
        }
        if (_glowHaloBillboard != null)
        {
            _glowHaloBillboard.localScale = Vector3.one * (1.6f + Mathf.Sin(t * 2.0f) * 0.35f);
        }
    }

    private void CheckPlayerProximity()
    {
        var player = AdventurePlayerController.Instance;
        if (player != null)
        {
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist < VisualConfig.TriggerDistance)
            {
                // 4.5m接近で自動開封、またはEキーでも即時開封
                OpenBox();
            }
        }
    }

    private void CheckReopenProximity()
    {
        if (_isModalOpen) return;
        var player = AdventurePlayerController.Instance;
        if (player != null)
        {
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist < VisualConfig.TriggerDistance)
            {
                bool ePressed = false;
                var kb = Keyboard.current;
                if (kb != null && kb.eKey.wasPressedThisFrame) ePressed = true;
                try
                {
                    if (Input.GetKeyDown(KeyCode.E)) ePressed = true;
                }
                catch { }

                if (ePressed)
                {
                    ShowModal(boxTitle, author, message);
                }
            }
        }
    }

    #region 発光・視認性エフェクト構築

    private static Shader GetSafeUnlitShader()
    {
        return Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("RustAndFloat/WhiteSmoke")
            ?? Shader.Find("Sprites/Default");
    }

    private static Material CreateTransparentAdditiveMaterial(Shader shader, Texture2D tex, Color color, int renderQueue = 3100)
    {
        var mat = new Material(shader);
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", color);

        // URP 半透明・加算ブレンド・両面描画設定
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1f); // Additive
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // Double-sided (Cull Off)
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);

        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = renderQueue;
        return mat;
    }

    private void SetupGlowEffects(Transform lamp)
    {
        Vector3 lampLocalPos = lamp != null ? lamp.localPosition : new Vector3(0.38f, 0.94f, 0.22f);
        var smokeTex = AdventureRustDrone.GetSoftSmokeTexture();
        var unlitShader = GetSafeUnlitShader();

        // 1. 周囲の広範囲を照らす強力な自発光ポイントライト
        var lightGo = new GameObject("DriftBoxPointLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = lampLocalPos;
        _pointLight = lightGo.AddComponent<Light>();
        _pointLight.type = LightType.Point;
        _pointLight.range = VisualConfig.LightRangeUnopenedBase;
        _pointLight.intensity = VisualConfig.LightIntensityUnopenedBase;
        _pointLight.color = VisualConfig.UnopenedGold;
        _pointLight.shadows = LightShadows.None;

        // 2. 天空を貫く超巨大光柱（ライトビーコン: 高さ65m、半径0.85m）
        var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "BeaconPillar";
        beacon.transform.SetParent(transform, false);
        beacon.transform.localPosition = lampLocalPos + new Vector3(0f, VisualConfig.BeaconHeight * 0.5f, 0f);
        beacon.transform.localScale = new Vector3(VisualConfig.BeaconRadius, VisualConfig.BeaconHeight * 0.5f, VisualConfig.BeaconRadius);
        Destroy(beacon.GetComponent<Collider>());

        var beaconRend = beacon.GetComponent<Renderer>();
        if (beaconRend != null)
        {
            _beaconMat = CreateTransparentAdditiveMaterial(unlitShader, smokeTex, new Color(1.0f, 0.82f, 0.25f, 0.65f), 3120);
            beaconRend.material = _beaconMat;
        }
        _beaconPillar = beacon.transform;

        // 3. 垂直光粒子ビーム（空へ高速で昇る星屑の柱）
        _particleMat = CreateTransparentAdditiveMaterial(unlitShader, smokeTex, new Color(1.0f, 0.92f, 0.45f, 1.0f), 3140);

        var beamGo = new GameObject("VerticalBeamSparkles");
        beamGo.transform.SetParent(transform, false);
        beamGo.transform.localPosition = lampLocalPos;
        _verticalBeamParticles = beamGo.AddComponent<ParticleSystem>();

        var mainBeam = _verticalBeamParticles.main;
        mainBeam.loop = true;
        mainBeam.startLifetime = 3.5f;
        mainBeam.startSpeed = 14.0f;
        mainBeam.startSize = 0.35f;
        mainBeam.startColor = new Color(1.0f, 0.90f, 0.45f, 0.95f);
        mainBeam.simulationSpace = ParticleSystemSimulationSpace.World;

        var emissionBeam = _verticalBeamParticles.emission;
        emissionBeam.rateOverTime = 25f; // 25個/秒で連続上昇

        var shapeBeam = _verticalBeamParticles.shape;
        shapeBeam.shapeType = ParticleSystemShapeType.Cone;
        shapeBeam.angle = 2.0f;
        shapeBeam.radius = 0.25f;
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
        mainIdle.startLifetime = 2.2f;
        mainIdle.startSpeed = 0.35f;
        mainIdle.startSize = 0.24f;
        mainIdle.startColor = new Color(1.0f, 0.85f, 0.30f, 0.90f);
        mainIdle.simulationSpace = ParticleSystemSimulationSpace.World;

        var emissionIdle = _idleSparkles.emission;
        emissionIdle.rateOverTime = 18f;

        var shapeIdle = _idleSparkles.shape;
        shapeIdle.shapeType = ParticleSystemShapeType.Sphere;
        shapeIdle.radius = 1.4f;

        var rendIdle = idleGo.GetComponent<ParticleSystemRenderer>();
        if (rendIdle != null) rendIdle.material = _particleMat;

        // 5. アンテナランプのソフトグロー（内側高輝度コア）
        var glowQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        glowQuad.name = "LampGlowCore";
        glowQuad.transform.SetParent(transform, false);
        glowQuad.transform.localPosition = lampLocalPos;
        glowQuad.transform.localScale = Vector3.one * 0.65f;
        Destroy(glowQuad.GetComponent<Collider>());

        var glowRend = glowQuad.GetComponent<Renderer>();
        if (glowRend != null)
        {
            _glowMat = CreateTransparentAdditiveMaterial(unlitShader, smokeTex, new Color(1.0f, 0.90f, 0.50f, 0.95f), 3160);
            glowRend.material = _glowMat;
        }
        _glowBillboard = glowQuad.transform;

        // 6. アンテナランプの広範囲オーラハロー（遠景用外側グロー）
        var haloQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        haloQuad.name = "LampGlowHalo";
        haloQuad.transform.SetParent(transform, false);
        haloQuad.transform.localPosition = lampLocalPos;
        haloQuad.transform.localScale = Vector3.one * 1.6f;
        Destroy(haloQuad.GetComponent<Collider>());

        var haloRend = haloQuad.GetComponent<Renderer>();
        if (haloRend != null)
        {
            _haloMat = CreateTransparentAdditiveMaterial(unlitShader, smokeTex, new Color(1.0f, 0.70f, 0.15f, 0.45f), 3150);
            haloRend.material = _haloMat;
        }
        _glowHaloBillboard = haloQuad.transform;
    }

    #endregion

    /// <summary>
    /// 開封・未開封のビジュアル状態を一元適用
    /// </summary>
    public void ApplyVisualState(bool opened, bool immediate)
    {
        if (opened)
        {
            if (_lid != null)
            {
                if (immediate) _lid.localRotation = Quaternion.Euler(-95f, 0f, 0f);
            }

            SetLampColor(VisualConfig.OpenedEmerald, immediate ? 1.4f : 2.5f);

            if (_pointLight != null)
            {
                _pointLight.color = VisualConfig.OpenedLightColor;
                _pointLight.intensity = VisualConfig.LightIntensityOpened;
                _pointLight.range = VisualConfig.LightRangeOpened;
            }

            if (_glowMat != null)
            {
                _glowMat.SetColor("_BaseColor", new Color(0.25f, 1.0f, 0.65f, immediate ? 0.50f : 0.70f));
            }
            if (_haloMat != null)
            {
                _haloMat.SetColor("_BaseColor", new Color(0.20f, 1.0f, 0.60f, 0.25f));
            }

            // アイドルスパークル＆垂直ビームの停止
            if (_idleSparkles != null)
            {
                if (immediate) _idleSparkles.gameObject.SetActive(false);
                else _idleSparkles.Stop();
            }

            if (_verticalBeamParticles != null)
            {
                if (immediate) _verticalBeamParticles.gameObject.SetActive(false);
                else _verticalBeamParticles.Stop();
            }

            // ビーコン光柱の処理
            if (_beaconPillar != null)
            {
                if (immediate)
                {
                    _beaconPillar.gameObject.SetActive(false);
                }
                else
                {
                    if (_beaconFadeCoroutine != null) StopCoroutine(_beaconFadeCoroutine);
                    _beaconFadeCoroutine = StartCoroutine(FadeOutBeacon());
                }
            }
        }
        else
        {
            if (_beaconFadeCoroutine != null)
            {
                StopCoroutine(_beaconFadeCoroutine);
                _beaconFadeCoroutine = null;
            }

            if (_lid != null)
            {
                _lid.localRotation = Quaternion.identity;
            }

            SetLampColor(VisualConfig.UnopenedGold, 4.5f);

            if (_pointLight != null)
            {
                _pointLight.color = VisualConfig.UnopenedGold;
                _pointLight.intensity = VisualConfig.LightIntensityUnopenedBase;
                _pointLight.range = VisualConfig.LightRangeUnopenedBase;
            }

            if (_beaconMat != null)
            {
                _beaconMat.SetColor("_BaseColor", new Color(1.0f, 0.82f, 0.25f, 0.65f));
            }
            if (_glowMat != null)
            {
                _glowMat.SetColor("_BaseColor", new Color(1.0f, 0.90f, 0.50f, 0.95f));
            }
            if (_haloMat != null)
            {
                _haloMat.SetColor("_BaseColor", new Color(1.0f, 0.70f, 0.15f, 0.45f));
            }

            if (_beaconPillar != null)
            {
                _beaconPillar.gameObject.SetActive(true);
                _beaconPillar.localScale = new Vector3(VisualConfig.BeaconRadius, VisualConfig.BeaconHeight * 0.5f, VisualConfig.BeaconRadius);
            }

            if (_verticalBeamParticles != null)
            {
                _verticalBeamParticles.gameObject.SetActive(true);
                if (!_verticalBeamParticles.isPlaying) _verticalBeamParticles.Play();
            }

            if (_idleSparkles != null)
            {
                _idleSparkles.gameObject.SetActive(true);
                if (!_idleSparkles.isPlaying) _idleSparkles.Play();
            }

            if (_glowHaloBillboard != null)
            {
                _glowHaloBillboard.gameObject.SetActive(true);
                _glowHaloBillboard.localScale = Vector3.one * 1.6f;
            }
        }
    }

    [ContextMenu("Reset Box to Unopened")]
    public void ResetBoxToUnopened()
    {
        PlayerPrefs.DeleteKey("DriftBox_Opened_" + boxId);
        PlayerPrefs.Save();
        isOpened = false;
        ApplyVisualState(false, immediate: true);
        Debug.Log($"[DriftBox] ボックス #{boxId} ({boxTitle}) を未開封状態にリセットしました。");
        var scrapMgr = AdventureScrapManager.Instance;
        if (scrapMgr != null) scrapMgr.CheckPointsAndNotifyLeverUnlock();
    }

    [ContextMenu("Force Open Box")]
    public void ForceOpenBox()
    {
        OpenBox();
    }

    /// <summary>
    /// 全ドリフトボックス（#1〜#5）を未開封状態へ完全リセットし、ポイントとHUDを再同期
    /// </summary>
    public static void ResetAllBoxesStatic()
    {
        for (int i = 1; i <= 5; i++)
        {
            PlayerPrefs.DeleteKey("DriftBox_Opened_" + i);
        }
        PlayerPrefs.Save();

        var boxes = Object.FindObjectsByType<AdventureBeachDriftBox>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in boxes)
        {
            if (b != null)
            {
                b.isOpened = false;
                b.ApplyVisualState(false, immediate: true);
            }
        }

        var scrapMgr = AdventureScrapManager.Instance;
        if (scrapMgr != null)
        {
            scrapMgr.CheckPointsAndNotifyLeverUnlock();
        }

        var hud = AdventureScrapHUD.Instance ?? Object.FindFirstObjectByType<AdventureScrapHUD>();
        if (hud != null)
        {
            int pts = scrapMgr != null ? scrapMgr.TotalProgressPoints : 0;
            hud.ShowUpgradeBanner($"📦 全ドリフトボックスを未開封にリセット！\n✦ 現在の探索ポイント: {pts} / {AdventureScrapManager.RequiredPointsForCanopy} pt");
        }

        Debug.Log("📦 【DriftBox】全5個のドリフトボックスを未開封状態にリセットしました！");
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Adventure/📦 全ドリフトボックスを未開封にリセット (Reset All Drift Boxes)")]
    public static void EditorResetAllBoxes()
    {
        ResetAllBoxesStatic();
    }
#endif

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

        // 総合探索ポイント（+2 pt）加算とレバーロック解除チェック
        var scrapMgr = AdventureScrapManager.Instance;
        if (scrapMgr != null)
        {
            scrapMgr.OnDriftBoxOpened(boxId, boxTitle);
        }

        // 相棒Rustのセリフ
        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null && !string.IsNullOrEmpty(rustDialogue))
        {
            drone.SpeakCustom(rustDialogue, 6.0f);
        }

        // クエストティッカーの更新（+2 pt 獲得と現在ポイント）
        var hud = AdventureScrapHUD.Instance ?? FindAnyObjectByType<AdventureScrapHUD>();
        if (hud != null)
        {
            int pts = scrapMgr != null ? scrapMgr.TotalProgressPoints : 0;
            string objText = !string.IsNullOrEmpty(nextObjective) ? $"\n💡 目標: {nextObjective}" : "";
            hud.ShowUpgradeBanner($"📦 【{boxTitle}】を開封！ (+2 pt)\n✦ 探索ポイント: {pts} / {AdventureScrapManager.RequiredPointsForCanopy} pt{objText}");
        }

        // 情報モーダルUIの表示
        ShowModal(boxTitle, author, message);
    }

    private IEnumerator AnimateOpen()
    {
        // ビジュアルを開封状態へ移行（演出付き）
        ApplyVisualState(opened: true, immediate: false);

        // 開封祝祭パーティクル演出
        if (_particles != null)
        {
            _particles.Play();
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
            if (_glowHaloBillboard != null)
            {
                _glowHaloBillboard.localScale = Vector3.Lerp(Vector3.one * 1.6f, Vector3.zero, t);
            }
            yield return null;
        }
        if (_beaconPillar != null)
        {
            _beaconPillar.gameObject.SetActive(false);
        }
        if (_glowHaloBillboard != null)
        {
            _glowHaloBillboard.gameObject.SetActive(false);
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

        // メインパネル
        _modalPanel = new GameObject("ModalPanel");
        _modalPanel.transform.SetParent(overlay.transform, false);
        var panelImg = _modalPanel.AddComponent<Image>();
        panelImg.color = new Color(0.12f, 0.15f, 0.20f, 0.96f); // 深いネイビーグレー
        var panelRt = _modalPanel.GetComponent<RectTransform>();
        panelRt.sizeDelta = new Vector2(680, 420);
        panelRt.anchoredPosition = Vector2.zero;

        // パネルの外枠（金色の飾り枠）
        AddOutline(_modalPanel, new Color(0.85f, 0.72f, 0.40f, 0.85f), new Vector2(2, -2));

        // アイコン＆タイトル
        _modalTitleText = CreateTextElement(_modalPanel.transform, "TitleText", defaultFont, 26,
            new Color(1.0f, 0.88f, 0.45f), FontStyle.Bold, TextAnchor.MiddleCenter,
            new Vector2(0, 160), new Vector2(600, 50));

        // 差出人／記録者
        _modalAuthorText = CreateTextElement(_modalPanel.transform, "AuthorText", defaultFont, 17,
            new Color(0.65f, 0.75f, 0.85f), FontStyle.Normal, TextAnchor.MiddleCenter,
            new Vector2(0, 120), new Vector2(600, 30));

        // 本文
        _modalBodyText = CreateTextElement(_modalPanel.transform, "BodyText", defaultFont, 20,
            new Color(0.95f, 0.96f, 0.98f), FontStyle.Normal, TextAnchor.UpperLeft,
            new Vector2(0, -10), new Vector2(580, 200), lineSpacing: 1.35f);

        // 閉じるヒントボタン
        CreateCloseButton(_modalPanel.transform, defaultFont);

        overlay.SetActive(false);
    }

    #region UIヘルパーメソッド

    private static Text CreateTextElement(Transform parent, string name, Font font, int fontSize,
        Color color, FontStyle fontStyle, TextAnchor alignment, Vector2 pos, Vector2 size, float lineSpacing = 1.0f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<Text>();
        txt.font = font;
        txt.fontSize = fontSize;
        txt.fontStyle = fontStyle;
        txt.color = color;
        txt.alignment = alignment;
        txt.lineSpacing = lineSpacing;

        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return txt;
    }

    private static void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        var outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
    }

    private static void CreateCloseButton(Transform parent, Font font)
    {
        var closeGo = new GameObject("CloseHintButton");
        closeGo.transform.SetParent(parent, false);
        var closeImg = closeGo.AddComponent<Image>();
        closeImg.color = new Color(0.18f, 0.24f, 0.32f, 0.85f);
        var closeBtn = closeGo.AddComponent<Button>();
        closeBtn.onClick.AddListener(CloseModal);
        var closeColors = closeBtn.colors;
        closeColors.highlightedColor = new Color(0.28f, 0.38f, 0.50f, 1f);
        closeColors.pressedColor = new Color(0.10f, 0.15f, 0.22f, 1f);
        closeBtn.colors = closeColors;

        AddOutline(closeGo, new Color(0.45f, 0.85f, 1.0f, 0.6f), new Vector2(1.5f, -1.5f));

        var closeText = CreateTextElement(closeGo.transform, "Text", font, 17,
            new Color(0.65f, 0.92f, 1.0f), FontStyle.Bold, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.zero);
        closeText.text = "【 Space / Enter / クリックで閉じる 】";
        var closeTextRt = closeText.GetComponent<RectTransform>();
        closeTextRt.anchorMin = Vector2.zero;
        closeTextRt.anchorMax = Vector2.one;
        closeTextRt.sizeDelta = Vector2.zero;

        var closeRt = closeGo.GetComponent<RectTransform>();
        closeRt.anchoredPosition = new Vector2(0, -165);
        closeRt.sizeDelta = new Vector2(440, 42);
    }

    #endregion

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
