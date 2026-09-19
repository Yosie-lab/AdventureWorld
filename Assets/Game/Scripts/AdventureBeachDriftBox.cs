using UnityEngine;
using UnityEngine.UI;
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

    // UI関連（シングルトン共有モーダル）
    private static Canvas _modalCanvas;
    private static GameObject _modalPanel;
    private static Text _modalTitleText;
    private static Text _modalBodyText;
    private static Text _modalAuthorText;
    private static Text _modalCloseHintText;
    private static bool _isModalOpen = false;

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

        EnsureModalUI();
    }

    void Start()
    {
        if (isOpened)
        {
            ApplyOpenedStateImmediate();
        }
        else
        {
            SetLampColor(new Color(1.0f, 0.65f, 0.15f), 1.6f); // 未開封: オレンジ点滅
        }
    }

    void Update()
    {
        if (!isOpened)
        {
            // 未開封時はランプがゆったりと呼吸点滅
            float pulse = 1.0f + Mathf.Sin(Time.time * 3.5f) * 0.5f;
            SetLampColor(new Color(1.0f, 0.65f, 0.15f), pulse * 1.5f);

            // プレイヤー接近判定
            var player = AdventurePlayerController.Instance;
            if (player != null)
            {
                float dist = Vector3.Distance(transform.position, player.transform.position);
                if (dist < 2.6f)
                {
                    OpenBox();
                }
            }
        }

        // モーダル表示中のキー入力（SpaceやEnter、E、クリックで閉じる）
        if (_isModalOpen && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0)))
        {
            CloseModal();
        }
    }

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
        // ランプを爽やかなエメラルドグリーンに切り替え
        SetLampColor(new Color(0.2f, 1.0f, 0.6f), 2.2f);

        // パーティクル演出
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

    private void ApplyOpenedStateImmediate()
    {
        if (_lid != null)
        {
            _lid.localRotation = Quaternion.Euler(-95f, 0f, 0f);
        }
        SetLampColor(new Color(0.2f, 1.0f, 0.6f), 1.2f); // 開封済み: 落ち着いた緑
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
        if (_modalCanvas != null) return;

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

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 背景暗転パネル
        var overlay = new GameObject("Overlay");
        overlay.transform.SetParent(canvasGo.transform, false);
        var overlayImg = overlay.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.45f);
        var overlayRt = overlay.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.sizeDelta = Vector2.zero;

        // メインパネル
        _modalPanel = new GameObject("ModalPanel");
        _modalPanel.transform.SetParent(overlay.transform, false);
        var panelImg = _modalPanel.AddComponent<Image>();
        panelImg.color = new Color(0.12f, 0.15f, 0.20f, 0.94f); // 深いネイビーグレー
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
        var closeGo = new GameObject("CloseHintText");
        closeGo.transform.SetParent(_modalPanel.transform, false);
        _modalCloseHintText = closeGo.AddComponent<Text>();
        _modalCloseHintText.font = defaultFont;
        _modalCloseHintText.fontSize = 17;
        _modalCloseHintText.fontStyle = FontStyle.Bold;
        _modalCloseHintText.color = new Color(0.45f, 0.85f, 1.0f);
        _modalCloseHintText.alignment = TextAnchor.MiddleCenter;
        _modalCloseHintText.text = "【 Space または クリックで閉じる 】";
        var closeRt = closeGo.GetComponent<RectTransform>();
        closeRt.anchoredPosition = new Vector2(0, -165);
        closeRt.sizeDelta = new Vector2(500, 40);

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
    }

    public static void CloseModal()
    {
        if (_modalCanvas != null && _modalPanel != null)
        {
            _modalPanel.transform.parent.gameObject.SetActive(false);
        }
        _isModalOpen = false;
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
