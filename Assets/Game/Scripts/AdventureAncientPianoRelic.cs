using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 森の奥の古びたピアノと光る古代遺物（Ancient Piano & Glowing Relic）
/// 島の美しいアンビエントBGMが実はここから流れていたという物語を演出する秘境スポット。
/// 起動時または探索ごとに、台地や森の奥の候補地からランダムに選ばれて出現する。
/// </summary>
public class AdventureAncientPianoRelic : MonoBehaviour
{
    private static AdventureAncientPianoRelic _instance;
    public static AdventureAncientPianoRelic Instance => _instance;

    [System.Serializable]
    public struct PianoLocationCandidate
    {
        public string name;
        public Vector3 position;
        public float rotationY;
        public string areaDescription;
    }

    // 候補地5箇所
    public static readonly PianoLocationCandidate[] Candidates = new PianoLocationCandidate[]
    {
        new PianoLocationCandidate
        {
            name = "中央台地・東部木立の奥",
            position = new Vector3(470f, 0f, 540f),
            rotationY = 145f,
            areaDescription = "白亜タワーを見上げる台地、木漏れ日が差す木立の奥"
        },
        new PianoLocationCandidate
        {
            name = "東部大樹海・巨木に囲まれた静寂の広場",
            position = new Vector3(665f, 0f, 385f),
            rotationY = 215f,
            areaDescription = "深い原生林の奥深く、苔むした大岩と巨木が寄り添う静寂の広場"
        },
        new PianoLocationCandidate
        {
            name = "オアシス湧水池の木陰",
            position = new Vector3(474f, 0f, 460f),
            rotationY = 65f,
            areaDescription = "透き通る湧水池のほとり、木々の葉擦れの音が届く木陰"
        },
        new PianoLocationCandidate
        {
            name = "北崖直下・秘密の森の展望テラス",
            position = new Vector3(165f, 0f, 588f),
            rotationY = 310f,
            areaDescription = "海風が吹き抜ける北崖の下、木々に隠された秘密の見晴らし台"
        },
        new PianoLocationCandidate
        {
            name = "北西奥・古木が佇む静かな草原",
            position = new Vector3(215f, 0f, 415f),
            rotationY = 85f,
            areaDescription = "内陸のせせらぎから少し離れた、静かな古木が佇む木立"
        }
    };

    [Header("State")]
    public string selectedLocationName;
    public bool isDiscovered = false;

    private Transform _relicTransform;
    private Renderer _relicRenderer;
    private Light _relicLight;
    private ParticleSystem _relicParticles;
    private AudioSource _pianoAudioSource;
    private AudioClip _pianoChordClip;
    private AudioClip _pianoPerformanceClip;
    private bool _hasPlayedIntroDialogue = false;
    private float _bobTimer = 0f;

    // ピアニスト・カピタ
    private Transform _capytaTransform;
    private Animator _capytaAnimator;
    private ParticleSystem _capytaMusicNotes;
    private float _capytaHappyTimer = 0f;
    private int _interactionCount = 0;

    void Awake()
    {
        _instance = this;
    }

    void OnDisable()
    {
        // ピアノから離れたり無効化された際はBGMダッキングを通常音量にリセット
        if (AdventureMusicDirector.Instance != null)
        {
            AdventureMusicDirector.Instance.SetSpotDucking(0f);
        }
    }

    void Start()
    {
        // まだ配置されていなければランダム選定して配置
        if (transform.childCount == 0)
        {
            SpawnAtRandomLocation();
        }
        else
        {
            InitRuntimeComponents();
        }
    }

    void Update()
    {
        if (_relicTransform != null)
        {
            // 光る遺物のゆったりとした浮遊と回転（ボビング＆ローテーション）
            _bobTimer += Time.deltaTime;
            float bobY = Mathf.Sin(_bobTimer * 1.8f) * 0.08f;
            _relicTransform.localPosition = new Vector3(0.25f, 1.45f + bobY, 0.15f);
            _relicTransform.Rotate(Vector3.up, 32f * Time.deltaTime, Space.World);

            // 遺物の微細な光の明滅
            if (_relicLight != null)
            {
                _relicLight.intensity = 2.4f + Mathf.Sin(_bobTimer * 2.5f) * 0.6f;
            }
        }

        // ピアニスト・カピタの愛らしい演奏スウェイ（リズムに乗って頷く）
        if (_capytaTransform != null)
        {
            if (_capytaHappyTimer > 0f)
            {
                _capytaHappyTimer -= Time.deltaTime;
                if (_capytaHappyTimer <= 0f && _capytaAnimator != null)
                {
                    _capytaAnimator.CrossFade("CapytaSittingIdle", 0.3f);
                }
            }
            else
            {
                float swayAngleX = 6.0f + Mathf.Sin(Time.time * 2.6f) * 3.2f;
                float swayAngleY = Mathf.Sin(Time.time * 1.3f) * 4.0f;
                float swayAngleZ = Mathf.Cos(Time.time * 2.6f) * 2.0f;
                _capytaTransform.localRotation = Quaternion.Euler(swayAngleX, swayAngleY, swayAngleZ);
            }
        }

        // プレイヤー接近判定
        var player = AdventurePlayerController.Instance;
        if (player != null)
        {
            float dist = Vector3.Distance(transform.position, player.transform.position);

            // BGMディレクターへのスポットダッキング連携（35m〜10mで徐々に通常BGMを控えめにしてピアノ生演奏を際立たせる）
            if (AdventureMusicDirector.Instance != null)
            {
                float duck = Mathf.Clamp01((35f - dist) / 25f);
                AdventureMusicDirector.Instance.SetSpotDucking(duck);
            }

            // 1. 初回接近時（8.5m以内）の発見演出
            if (dist < 8.5f && !isDiscovered)
            {
                OnPlayerDiscovered();
            }

            // 2. カピタ接近時（3.5m以内）：ピアノ上の光る遺物（3ポイント）の自動回収
            if (dist < 3.5f && _relicTransform != null && _relicTransform.gameObject.activeSelf)
            {
                var scrapMgr = AdventureScrapManager.Instance;
                if (scrapMgr != null && !scrapMgr.IsPianoRelicCollected)
                {
                    CollectRelic();
                }
            }

            // 3. ピアノ＆カピタの前（3.2m以内）での連弾インタラクション
            if (dist < 3.2f)
            {
                var kb = Keyboard.current;
                bool interactPressed = (kb != null && kb.eKey.wasPressedThisFrame);
                try { if (Input.GetKeyDown(KeyCode.E)) interactPressed = true; } catch { }

                if (interactPressed)
                {
                    PlayPianoWithCapyta();
                }
            }
        }
    }

    /// <summary>カピタに近づいた時にピアノ上の光る古代遺物（3pt）を回収する演出</summary>
    public void CollectRelic()
    {
        if (_relicTransform == null || !_relicTransform.gameObject.activeSelf) return;

        // 遺物パーティクルの大放出
        if (_relicParticles != null)
        {
            _relicParticles.Emit(45);
        }

        // 遺物を非表示化
        _relicTransform.gameObject.SetActive(false);

        // カピタの喜び反応
        TriggerCapytaHappy();

        // ScrapManagerへ3pt加算通知
        if (AdventureScrapManager.Instance != null)
        {
            AdventureScrapManager.Instance.CollectPianoRelic();
        }
    }

    /// <summary>
    /// 天蓋崩壊シーケンス開始時に呼ぶ：ピアノ演奏を指定秒でフェードアウトして停止し、
    /// BGMダッキングも通常に戻す。
    /// </summary>
    public void FadeOutPiano(float duration = 2.0f)
    {
        // BGMダッキング解除
        if (AdventureMusicDirector.Instance != null)
            AdventureMusicDirector.Instance.SetSpotDucking(0f);

        if (_pianoAudioSource == null || !_pianoAudioSource.isPlaying) return;
        StartCoroutine(FadeOutPianoRoutine(_pianoAudioSource, duration));
    }

    IEnumerator FadeOutPianoRoutine(AudioSource src, float duration)
    {
        if (src == null) yield break;
        float startVol = src.volume;
        float t = 0f;
        while (t < duration && src != null && src.isPlaying)
        {
            t += Time.deltaTime;
            src.volume = Mathf.Lerp(startVol, 0f, t / duration);
            yield return null;
        }
        if (src != null)
        {
            src.Stop();
            src.volume = 0f;
        }
    }

    /// <summary>新規冒険（ニューゲーム）用：遺物を再表示し発見フラグを初期化</summary>
    public void ResetForNewGame()
    {
        isDiscovered = false;
        _hasPlayedIntroDialogue = false;
        PlayerPrefs.DeleteKey("AncientPiano_Relic_Collected");
        PlayerPrefs.DeleteKey("AncientPiano_Discovered");
        PlayerPrefs.Save();

        if (_relicTransform != null)
        {
            _relicTransform.gameObject.SetActive(true);
        }
        Debug.Log("[AdventureAncientPianoRelic] 🎹 古代ピアノ遺物を未回収状態にリセットしました。");
    }

    /// <summary>シーン内のすべての古代ピアノ遺物を未回収状態に一括リセット</summary>
    public static void ResetAllPianoRelicsStatic()
    {
        PlayerPrefs.DeleteKey("AncientPiano_Relic_Collected");
        PlayerPrefs.DeleteKey("AncientPiano_Discovered");
        PlayerPrefs.Save();

        var pianos = Object.FindObjectsByType<AdventureAncientPianoRelic>(FindObjectsInactive.Include);
        foreach (var p in pianos)
        {
            if (p != null) p.ResetForNewGame();
        }
    }

    /// <summary>プレイヤーが初めてピアノとカピタを発見した時の演出</summary>
    private void OnPlayerDiscovered()
    {
        isDiscovered = true;
        PlayerPrefs.SetInt("AncientPiano_Discovered", 1);
        PlayerPrefs.Save();

        // 初回和音の演奏
        PlayPianoMelody();

        // カピタの喜び反応
        TriggerCapytaHappy();

        // 遺物パーティクルのバースト放出
        if (_relicParticles != null)
        {
            _relicParticles.Emit(35);
        }

        // 相棒Rustの特別なセリフ
        if (!_hasPlayedIntroDialogue)
        {
            _hasPlayedIntroDialogue = true;
            var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
            if (drone != null)
            {
                drone.SpeakCustom(
                    "…見て、Niko！島じゅうに流れていたあの優しいメロディ…カピタがピアノを弾いていたんだね…！\nすごく気持ちよさそうに弾いてる…ふふ、癒やされるなぁ♪",
                    8.5f);
            }
        }

        // HUD発見バナーの表示
        var hud = AdventureScrapHUD.Instance ?? FindAnyObjectByType<AdventureScrapHUD>();
        if (hud != null)
        {
            hud.ShowUpgradeBanner(
                $"🎹 秘境を発見: 『ピアニスト・カピタと光る遺物』\n✦ 島を満たす優しい音色はカピタが奏でていた！ 【Eキーで一緒に連弾】");
        }
    }

    /// <summary>プレイヤーがEキーでカピタと一緒にピアノを連弾する</summary>
    public void PlayPianoWithCapyta()
    {
        PlayPianoMelody();
        TriggerCapytaHappy();

        _interactionCount++;
        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            string[] rustPraises = new string[]
            {
                "わぁ、素敵な連弾だね！カピタも嬉しそうに目を細めてるよ♪",
                "ポロ〜ン…♪ 風に乗って、島のすみずみまで音が届いていくね",
                "カピタ『ブヒヒ…♪（ご機嫌にリズムに合わせて鼻を鳴らした！）』",
                "ふふっ、息ピッタリ！二人の音楽で島が優しさに包まれていくよ…！"
            };
            string line = rustPraises[(_interactionCount - 1) % rustPraises.Length];
            drone.SpeakCustom(line, 4.5f);
        }
    }

    private void TriggerCapytaHappy()
    {
        _capytaHappyTimer = 2.5f;
        if (_capytaAnimator != null)
        {
            _capytaAnimator.CrossFade("CapytaSittingIdleLooksRight", 0.2f);
        }
        if (_capytaMusicNotes != null)
        {
            _capytaMusicNotes.Emit(12);
        }
    }

    /// <summary>ピアノの澄んだ美しい和音を奏でる</summary>
    public void PlayPianoMelody()
    {
        if (_pianoAudioSource != null && _pianoChordClip != null)
        {
            _pianoAudioSource.pitch = Random.Range(0.98f, 1.02f);
            _pianoAudioSource.PlayOneShot(_pianoChordClip, 0.95f);
        }

        if (_relicParticles != null)
        {
            _relicParticles.Emit(18);
        }
    }

    private void InitRuntimeComponents()
    {
        _relicTransform = transform.Find("GlowingRelicPivot");
        if (_relicTransform != null)
        {
            var relicMesh = _relicTransform.Find("RelicMesh");
            if (relicMesh != null) _relicRenderer = relicMesh.GetComponent<Renderer>();
            _relicLight = _relicTransform.GetComponentInChildren<Light>();
            _relicParticles = _relicTransform.GetComponentInChildren<ParticleSystem>();

            var scrapMgr = AdventureScrapManager.Instance;
            if (scrapMgr != null && scrapMgr.IsPianoRelicCollected)
            {
                _relicTransform.gameObject.SetActive(false);
            }
        }

        // ピアニスト・カピタの取得とすり抜け防止コライダーの保証
        _capytaTransform = transform.Find("PianistCapyta");
        if (_capytaTransform != null)
        {
            _capytaAnimator = _capytaTransform.GetComponentInChildren<Animator>();
            _capytaMusicNotes = _capytaTransform.GetComponentInChildren<ParticleSystem>();

            var bodyCol = _capytaTransform.GetComponent<AdventureCapytaBodyCollider>();
            if (bodyCol == null) bodyCol = _capytaTransform.gameObject.AddComponent<AdventureCapytaBodyCollider>();
            bodyCol.EnsureCollider();
        }

        // ピアノ専用の3Dオーディオソース
        _pianoAudioSource = gameObject.GetComponent<AudioSource>();
        if (_pianoAudioSource == null) _pianoAudioSource = gameObject.AddComponent<AudioSource>();
        _pianoAudioSource.spatialBlend = 0.75f;
        _pianoAudioSource.minDistance = 6.5f;
        _pianoAudioSource.maxDistance = 45f;
        _pianoAudioSource.rolloffMode = AudioRolloffMode.Custom;

        // 遠くからでも風に乗って美しく届き、近づくとしっかり響く自然な減衰カーブ
        var curve = new AnimationCurve(
            new Keyframe(0f, 1f, 0f, 0f),
            new Keyframe(6.5f / 45f, 1f, 0f, -1.2f),
            new Keyframe(16f / 45f, 0.55f, -1.5f, -1.5f),
            new Keyframe(28f / 45f, 0.22f, -0.8f, -0.8f),
            new Keyframe(40f / 45f, 0.06f, -0.3f, -0.3f),
            new Keyframe(1f, 0f, 0f, 0f)
        );
        _pianoAudioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);

        _pianoAudioSource.volume = 0.92f;
        _pianoAudioSource.playOnAwake = false;

        _pianoPerformanceClip = CreateFeltPianoPerformanceClip();
        _pianoChordClip = CreateFeltPianoChord();

        // カピタがピアノの前で奏でる美しい自動演奏ループを開始
        if (_pianoPerformanceClip != null)
        {
            _pianoAudioSource.clip = _pianoPerformanceClip;
            _pianoAudioSource.loop = true;
            if (!_pianoAudioSource.isPlaying)
            {
                _pianoAudioSource.Play();
            }
        }
    }

    /// <summary>ランダムな候補地へピアノと遺物をスポーン配置する</summary>
    public void SpawnAtRandomLocation()
    {
        int idx = Random.Range(0, Candidates.Length);
        SpawnAtLocation(idx);
    }

    /// <summary>指定した候補地へピアノと遺物をスポーン配置する</summary>
    public void SpawnAtLocation(int idx)
    {
        // 既存のオブジェクトを掃除
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }

        idx = Mathf.Clamp(idx, 0, Candidates.Length - 1);
        var cand = Candidates[idx];
        selectedLocationName = cand.name;

        // 地形高さをサンプリングして接地
        Vector3 pos = cand.position;
        RaycastHit groundHit;
        if (Physics.Raycast(new Vector3(pos.x, 150f, pos.z), Vector3.down, out groundHit, 200f))
        {
            pos.y = groundHit.point.y - 0.02f;
        }
        else
        {
            var land = Terrain.activeTerrain ?? FindAnyObjectByType<Terrain>();
            if (land != null)
            {
                pos.y = land.SampleHeight(pos) + land.transform.position.y - 0.02f;
            }
        }

        transform.position = pos;
        transform.rotation = Quaternion.Euler(0f, cand.rotationY, 0f);

        // ピアノ、光る古代遺物、ピアニスト・カピタのプロシージャル造形
        BuildPianoModel();
        BuildGlowingRelic();
        BuildPianistCapyta();

        InitRuntimeComponents();

        Debug.Log($"[AdventureAncientPiano] 🎹 古びたピアノとピアニスト・カピタを『{cand.name}』({pos.ToString("F1")})に配置しました！");
    }

    /// <summary>アンティーク調の古びたグランドピアノのプロシージャル造形</summary>
    private void BuildPianoModel()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // 1. アンティーク・ウォールナット木目マテリアル
        var woodMat = new Material(shader);
        woodMat.name = "Piano_AntiqueWalnut";
        woodMat.SetColor("_BaseColor", new Color(0.24f, 0.16f, 0.11f)); // 深みのあるヴィンテージ木目
        woodMat.SetFloat("_Smoothness", 0.65f);

        // 2. 白鍵マテリアル（経年変化したアイボリー）
        var whiteKeyMat = new Material(shader);
        whiteKeyMat.name = "Piano_IvoryKeys";
        whiteKeyMat.SetColor("_BaseColor", new Color(0.92f, 0.89f, 0.82f));
        whiteKeyMat.SetFloat("_Smoothness", 0.70f);

        // 3. 黒鍵マテリアル（黒檀エボニー）
        var blackKeyMat = new Material(shader);
        blackKeyMat.name = "Piano_EbonyKeys";
        blackKeyMat.SetColor("_BaseColor", new Color(0.10f, 0.10f, 0.11f));
        blackKeyMat.SetFloat("_Smoothness", 0.75f);

        // 4. 真鍮ゴールド金具（ペダル・ヒンジ）
        var brassMat = new Material(shader);
        brassMat.name = "Piano_Brass";
        brassMat.SetColor("_BaseColor", new Color(0.82f, 0.68f, 0.32f));
        brassMat.SetFloat("_Metallic", 0.85f);
        brassMat.SetFloat("_Smoothness", 0.75f);

        var root = new GameObject("PianoModel");
        root.transform.SetParent(transform, false);

        // ── ピアノ本体ケース ──
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "BodyMain";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.82f, 0f);
        body.transform.localScale = new Vector3(1.70f, 0.45f, 2.10f);
        body.GetComponent<Renderer>().sharedMaterial = woodMat;

        // ピアノ後部のカーブ翼（グランドピアノ特有の優美な斜め尾部）
        var tail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tail.name = "BodyTail";
        tail.transform.SetParent(root.transform, false);
        tail.transform.localPosition = new Vector3(0.35f, 0.82f, 0.85f);
        tail.transform.localScale = new Vector3(0.95f, 0.225f, 0.95f);
        tail.GetComponent<Renderer>().sharedMaterial = woodMat;
        RemoveCollider(tail);

        // ── 開かれた大屋根（天板・Lid） ──
        var lidPivot = new GameObject("PianoLidPivot");
        lidPivot.transform.SetParent(root.transform, false);
        lidPivot.transform.localPosition = new Vector3(-0.84f, 1.05f, 0f);
        lidPivot.transform.localRotation = Quaternion.Euler(0f, 0f, -32f); // 32度優雅に開かれた天板

        var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lid.name = "LidBoard";
        lid.transform.SetParent(lidPivot.transform, false);
        lid.transform.localPosition = new Vector3(0.85f, 0.03f, 0f);
        lid.transform.localScale = new Vector3(1.72f, 0.05f, 2.12f);
        lid.GetComponent<Renderer>().sharedMaterial = woodMat;
        RemoveCollider(lid);

        // 屋根を支える突上棒（Prop Stick）
        var stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stick.name = "LidStick";
        stick.transform.SetParent(root.transform, false);
        stick.transform.localPosition = new Vector3(0.48f, 1.25f, 0.45f);
        stick.transform.localScale = new Vector3(0.025f, 0.28f, 0.025f);
        stick.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
        stick.GetComponent<Renderer>().sharedMaterial = brassMat;
        RemoveCollider(stick);

        // ── 鍵盤ベッド＆キー（鍵盤部） ──
        var keyBed = GameObject.CreatePrimitive(PrimitiveType.Cube);
        keyBed.name = "KeyboardBed";
        keyBed.transform.SetParent(root.transform, false);
        keyBed.transform.localPosition = new Vector3(0f, 0.72f, -1.02f);
        keyBed.transform.localScale = new Vector3(1.42f, 0.08f, 0.35f);
        keyBed.GetComponent<Renderer>().sharedMaterial = woodMat;

        // 白鍵（一列に並ぶ鍵盤）
        int whiteKeyCount = 21;
        float keyWidth = 1.34f / whiteKeyCount;
        for (int k = 0; k < whiteKeyCount; k++)
        {
            var wk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wk.name = "WhiteKey_" + k;
            wk.transform.SetParent(keyBed.transform, false);
            float kx = -0.67f + (k + 0.5f) * keyWidth;
            wk.transform.localPosition = new Vector3(kx, 0.55f, 0.02f);
            wk.transform.localScale = new Vector3(keyWidth * 0.92f, 0.35f, 0.88f);
            wk.GetComponent<Renderer>().sharedMaterial = whiteKeyMat;
            RemoveCollider(wk);
        }

        // 黒鍵（白鍵の隙間に並ぶ鍵盤）
        int[] blackKeyIndices = new int[] { 0, 1, 3, 4, 5, 7, 8, 10, 11, 12, 14, 15, 17, 18, 19 };
        foreach (int bkIdx in blackKeyIndices)
        {
            var bk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bk.name = "BlackKey_" + bkIdx;
            bk.transform.SetParent(keyBed.transform, false);
            float bkx = -0.67f + (bkIdx + 1.0f) * keyWidth;
            bk.transform.localPosition = new Vector3(bkx, 0.85f, -0.08f);
            bk.transform.localScale = new Vector3(keyWidth * 0.65f, 0.45f, 0.55f);
            bk.GetComponent<Renderer>().sharedMaterial = blackKeyMat;
            RemoveCollider(bk);
        }

        // ── 譜面台＆古びた楽譜 ──
        var musicStand = GameObject.CreatePrimitive(PrimitiveType.Cube);
        musicStand.name = "MusicStand";
        musicStand.transform.SetParent(root.transform, false);
        musicStand.transform.localPosition = new Vector3(0f, 1.10f, -0.65f);
        musicStand.transform.localScale = new Vector3(0.72f, 0.26f, 0.04f);
        musicStand.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
        musicStand.GetComponent<Renderer>().sharedMaterial = woodMat;
        RemoveCollider(musicStand);

        var sheet = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sheet.name = "MusicSheet";
        sheet.transform.SetParent(musicStand.transform, false);
        sheet.transform.localPosition = new Vector3(0f, 0.02f, -0.55f);
        sheet.transform.localScale = new Vector3(0.55f, 0.36f, 1f);
        sheet.GetComponent<Renderer>().sharedMaterial = whiteKeyMat;
        RemoveCollider(sheet);

        // ── 3本のピアノ脚（左前・右前・後部） ──
        Vector3[] legPositions = new Vector3[]
        {
            new Vector3(-0.70f, 0.30f, -0.85f),
            new Vector3( 0.70f, 0.30f, -0.85f),
            new Vector3( 0.28f, 0.30f,  0.88f)
        };
        for (int l = 0; l < legPositions.Length; l++)
        {
            var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leg.name = "PianoLeg_" + l;
            leg.transform.SetParent(root.transform, false);
            leg.transform.localPosition = legPositions[l];
            leg.transform.localScale = new Vector3(0.12f, 0.30f, 0.12f);
            leg.GetComponent<Renderer>().sharedMaterial = woodMat;
            // 足元にキャスター金具
            var caster = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            caster.name = "Caster";
            caster.transform.SetParent(leg.transform, false);
            caster.transform.localPosition = new Vector3(0f, -0.95f, 0f);
            caster.transform.localScale = new Vector3(1.3f, 0.4f, 1.3f);
            caster.GetComponent<Renderer>().sharedMaterial = brassMat;
            RemoveCollider(caster);
        }

        // ── ペダルリラ（足元の中央ペダル） ──
        var lyre = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lyre.name = "PedalLyre";
        lyre.transform.SetParent(root.transform, false);
        lyre.transform.localPosition = new Vector3(0f, 0.18f, -0.35f);
        lyre.transform.localScale = new Vector3(0.24f, 0.24f, 0.08f);
        lyre.GetComponent<Renderer>().sharedMaterial = woodMat;
        RemoveCollider(lyre);

        for (int p = -1; p <= 1; p++)
        {
            var pedal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pedal.name = "BrassPedal_" + p;
            pedal.transform.SetParent(lyre.transform, false);
            pedal.transform.localPosition = new Vector3(p * 0.32f, -0.45f, -0.25f);
            pedal.transform.localScale = new Vector3(0.18f, 0.15f, 0.75f);
            pedal.GetComponent<Renderer>().sharedMaterial = brassMat;
            RemoveCollider(pedal);
        }

        // ── ピアノ丸椅子（Stool） ──
        var stool = new GameObject("PianoStool");
        stool.transform.SetParent(root.transform, false);
        stool.transform.localPosition = new Vector3(0f, 0f, -1.52f);

        var seat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        seat.name = "Seat";
        seat.transform.SetParent(stool.transform, false);
        seat.transform.localPosition = new Vector3(0f, 0.46f, 0f);
        seat.transform.localScale = new Vector3(0.55f, 0.06f, 0.55f);
        seat.GetComponent<Renderer>().sharedMaterial = woodMat;

        var stoolLeg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stoolLeg.name = "StoolLeg";
        stoolLeg.transform.SetParent(stool.transform, false);
        stoolLeg.transform.localPosition = new Vector3(0f, 0.22f, 0f);
        stoolLeg.transform.localScale = new Vector3(0.08f, 0.22f, 0.08f);
        stoolLeg.GetComponent<Renderer>().sharedMaterial = brassMat;
        RemoveCollider(stoolLeg);
    }

    /// <summary>丸椅子に座って愛らしくピアノを奏でるカピタの生成</summary>
    private void BuildPianistCapyta()
    {
        GameObject prefab = null;
#if UNITY_EDITOR
        prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Niko&Capyta/Assets/Prefabs/Capyta.prefab");
#endif
        if (prefab == null) return;

        var capy = Instantiate(prefab, transform);
        capy.name = "PianistCapyta";
        capy.transform.localPosition = new Vector3(0f, 0.38f, -1.48f);
        capy.transform.localRotation = Quaternion.Euler(6.0f, 0f, 0f);
        capy.transform.localScale = Vector3.one * 0.40f;

        // すり抜け防止固体コライダーの付与
        var bodyCol = capy.AddComponent<AdventureCapytaBodyCollider>();
        bodyCol.EnsureCollider();

        // アニメーション初期化（お座り待機モーション）
        var anim = capy.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.Play("CapytaSittingIdle", 0, 0f);
        }

        // カピタの頭上に舞う音符＆キラキラパーティクル
        var noteObj = new GameObject("CapytaMusicNotes");
        noteObj.transform.SetParent(capy.transform, false);
        noteObj.transform.localPosition = new Vector3(0f, 1.9f, 0.3f);

        var ps = noteObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = true;
        main.loop = true;
        main.startLifetime = 2.0f;
        main.startSpeed = 0.45f;
        main.startSize = 0.22f;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.90f, 0.35f, 0.95f), new Color(0.35f, 0.95f, 0.88f, 0.95f));
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 3.5f; // 常時ふわふわと優しい音符が舞う

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.30f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.y = new ParticleSystem.MinMaxCurve(0.40f, 0.75f);

        var pRend = noteObj.GetComponent<ParticleSystemRenderer>();
        var pMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit"));
        pMat.mainTexture = CreateNoteTexture();
        pMat.SetColor("_BaseColor", Color.white);
        pRend.sharedMaterial = pMat;
    }

    /// <summary>プロシージャル音符（♪）テクスチャの生成</summary>
    private Texture2D CreateNoteTexture()
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(1f, 1f, 1f, 0f);
        Color note = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 音符の丸い玉（符頭）: 中心(22, 20), 半径9
                float dHead = Vector2.Distance(new Vector2(x, y * 0.9f), new Vector2(22f, 18f));
                // 符幹（棒）: x: 28~31, y: 18~50
                bool isStem = (x >= 28 && x <= 32 && y >= 18 && y <= 50);
                // 符尾（旗）: x: 30~46, y: 40~52 のカーブ
                float flagCurve = 50f - (x - 30) * 0.7f;
                bool isFlag = (x >= 30 && x <= 46 && y >= flagCurve - 4f && y <= flagCurve + 2f);

                if (dHead <= 8.5f || isStem || isFlag)
                {
                    tex.SetPixel(x, y, note);
                }
                else
                {
                    tex.SetPixel(x, y, clear);
                }
            }
        }
        tex.Apply();
        return tex;
    }

    /// <summary>ピアノの上に静かに浮かび、光を放つ古代遺物（Relic）の造形</summary>
    private void BuildGlowingRelic()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        var relicPivot = new GameObject("GlowingRelicPivot");
        relicPivot.transform.SetParent(transform, false);
        relicPivot.transform.localPosition = new Vector3(0.25f, 1.45f, 0.15f);

        // 幻想的なエメラルドシアンのマテリアル（強エミッション）
        var relicMat = new Material(shader);
        relicMat.name = "Relic_CrystalGaze";
        Color relicCol = new Color(0.25f, 0.95f, 0.88f);
        relicMat.SetColor("_BaseColor", relicCol);
        relicMat.EnableKeyword("_EMISSION");
        relicMat.SetColor("_EmissionColor", relicCol * 2.8f);
        relicMat.SetFloat("_Metallic", 0.4f);
        relicMat.SetFloat("_Smoothness", 0.92f);

        // 1. 中央の神秘的なクリスタルオーブ
        var crystal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crystal.name = "RelicMesh";
        crystal.transform.SetParent(relicPivot.transform, false);
        crystal.transform.localScale = new Vector3(0.38f, 0.50f, 0.38f);
        crystal.GetComponent<Renderer>().sharedMaterial = relicMat;
        RemoveCollider(crystal);

        // 2. クリスタルを囲む幾何学的な光のリング（軌道環）
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "RelicOrbitRing";
        ring.transform.SetParent(relicPivot.transform, false);
        ring.transform.localScale = new Vector3(0.65f, 0.015f, 0.65f);
        ring.transform.localRotation = Quaternion.Euler(35f, 20f, 0f);
        ring.GetComponent<Renderer>().sharedMaterial = relicMat;
        RemoveCollider(ring);

        // 3. 柔らかく周囲の木立を照らすポイントライト
        var lightGo = new GameObject("RelicPointLight");
        lightGo.transform.SetParent(relicPivot.transform, false);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.35f, 0.95f, 0.88f);
        light.intensity = 2.6f;
        light.range = 8.5f;

        // 4. 立ち上る幻想的なオーラ・パーティクル
        var psGo = new GameObject("RelicAuraParticles");
        psGo.transform.SetParent(relicPivot.transform, false);
        var ps = psGo.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.playOnAwake = true;
        main.loop = true;
        main.startLifetime = 2.2f;
        main.startSpeed = 0.35f;
        main.startSize = 0.09f;
        main.startColor = new Color(0.4f, 0.98f, 0.90f, 0.85f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 12f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.y = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);

        var pRend = psGo.GetComponent<ParticleSystemRenderer>();
        var pMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit"));
        pMat.SetColor("_BaseColor", new Color(0.4f, 0.98f, 0.90f, 0.9f));
        pRend.sharedMaterial = pMat;
    }

    private static void RemoveCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }
    }

    /// <summary>
    /// カピタがピアノの前で奏でる温かく愛らしいフェルトピアノ自動演奏（約16秒シームレスループ、BPM 60）
    /// ノスタルジックな Cmaj9 → Am9 → Fmaj7 → G6/9 進行にのせたアルペジオと優しいメロディ
    /// </summary>
    private static AudioClip CreateFeltPianoPerformanceClip()
    {
        int rate = 44100;
        float duration = 16.0f;
        int samples = (int)(rate * duration);
        float[] data = new float[samples];

        // 演奏ノート定義 (開始秒, 周波数Hz, ベロシティ音量, 減衰速度)
        var notes = new (float time, float freq, float vel, float decayRate)[]
        {
            // ── 小節1: Cmaj9 (0.0s - 4.0s) ──
            // 左手アルペジオ
            (0.00f, 130.81f, 0.45f, 1.4f), // C3
            (0.60f, 196.00f, 0.38f, 1.6f), // G3
            (1.20f, 246.94f, 0.35f, 1.8f), // B3
            (1.80f, 329.63f, 0.36f, 2.0f), // E4
            (2.60f, 246.94f, 0.30f, 2.0f), // B3
            // 右手メロディ
            (0.35f, 659.25f, 0.52f, 2.2f), // E5
            (1.50f, 587.33f, 0.48f, 2.4f), // D5
            (2.80f, 493.88f, 0.55f, 2.0f), // B4

            // ── 小節2: Am9 (4.0s - 8.0s) ──
            // 左手アルペジオ
            (4.00f, 110.00f, 0.48f, 1.3f), // A2
            (4.60f, 164.81f, 0.38f, 1.6f), // E3
            (5.20f, 196.00f, 0.36f, 1.8f), // G3
            (5.80f, 261.63f, 0.35f, 2.0f), // C4
            (6.60f, 196.00f, 0.30f, 2.0f), // G3
            // 右手メロディ
            (4.35f, 523.25f, 0.50f, 2.2f), // C5
            (5.50f, 493.88f, 0.46f, 2.4f), // B4
            (6.75f, 440.00f, 0.52f, 2.0f), // A4

            // ── 小節3: Fmaj7 (8.0s - 12.0s) ──
            // 左手アルペジオ
            (8.00f, 87.31f,  0.50f, 1.2f), // F2
            (8.60f, 130.81f, 0.40f, 1.5f), // C3
            (9.20f, 164.81f, 0.38f, 1.7f), // E3
            (9.80f, 220.00f, 0.36f, 1.9f), // A3
            (10.60f, 164.81f,0.30f, 2.0f), // E3
            // 右手メロディ
            (8.35f, 440.00f, 0.48f, 2.2f), // A4
            (9.50f, 523.25f, 0.50f, 2.3f), // C5
            (10.50f, 659.25f, 0.56f, 2.2f), // E5
            (11.20f, 783.99f, 0.58f, 2.5f), // G5

            // ── 小節4: G6/9 (12.0s - 16.0s) ──
            // 左手アルペジオ
            (12.00f, 98.00f,  0.48f, 1.3f), // G2
            (12.60f, 146.83f, 0.38f, 1.6f), // D3
            (13.20f, 196.00f, 0.36f, 1.8f), // G3
            (13.80f, 246.94f, 0.35f, 2.0f), // B3
            (14.60f, 196.00f, 0.30f, 2.0f), // G3
            // 右手メロディ
            (12.35f, 659.25f, 0.52f, 2.2f), // E5
            (13.50f, 587.33f, 0.48f, 2.4f), // D5
            (14.70f, 493.88f, 0.50f, 2.2f), // B4
        };

        for (int n = 0; n < notes.Length; n++)
        {
            var note = notes[n];
            int startSample = (int)(note.time * rate);
            int noteLen = (int)(3.5f * rate); // 最大3.5秒の余韻
            int endSample = Mathf.Min(samples, startSample + noteLen);

            for (int i = startSample; i < endSample; i++)
            {
                float t = (float)(i - startSample) / rate;
                // フェルトピアノ特有の穏やかな立ち上がりと自然な指数減衰
                float attack = Mathf.Clamp01(t * 140f);
                float decay = Mathf.Exp(-t * note.decayRate);
                float env = attack * decay * note.vel;

                // 基音 + 温かみのある第2倍音・第3倍音
                float f0 = note.freq;
                float w = Mathf.Sin(2f * Mathf.PI * f0 * t) * 0.65f
                        + Mathf.Sin(4f * Mathf.PI * f0 * t) * 0.24f
                        + Mathf.Sin(6f * Mathf.PI * f0 * t) * 0.08f;

                data[i] += w * env * 0.42f;
            }

            // ループ終端の余韻が先頭に自然に重なるようにブレンド（シームレス接続）
            if (startSample + noteLen > samples)
            {
                int wrapLen = (startSample + noteLen) - samples;
                for (int i = 0; i < wrapLen; i++)
                {
                    float t = (float)(samples - startSample + i) / rate;
                    float attack = Mathf.Clamp01(t * 140f);
                    float decay = Mathf.Exp(-t * note.decayRate);
                    float env = attack * decay * note.vel;

                    float f0 = note.freq;
                    float w = Mathf.Sin(2f * Mathf.PI * f0 * t) * 0.65f
                            + Mathf.Sin(4f * Mathf.PI * f0 * t) * 0.24f
                            + Mathf.Sin(6f * Mathf.PI * f0 * t) * 0.08f;

                    data[i] += w * env * 0.42f;
                }
            }
        }

        // ループ端のクロスフェードで微小なクリックを完全除去
        int fadeSamples = (int)(rate * 0.15f);
        for (int i = 0; i < fadeSamples; i++)
        {
            float w = (float)i / fadeSamples;
            data[i] *= Mathf.SmoothStep(0.85f, 1.0f, w);
            data[samples - 1 - i] *= Mathf.SmoothStep(0.85f, 1.0f, w);
        }

        var clip = AudioClip.Create("PianoPerformanceLoop", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>暖かく美しいフェルトピアノの和音コードを合成</summary>
    private static AudioClip CreateFeltPianoChord()
    {
        int rate = 44100;
        float duration = 4.2f;
        int samples = (int)(rate * duration);
        float[] data = new float[samples];

        // 豊かで温かいメジャー9th和音（C4: 261.63Hz, G4: 392.00Hz, B4: 493.88Hz, D5: 587.33Hz, E5: 659.25Hz）
        float[] freqs = new float[] { 261.63f, 392.00f, 493.88f, 587.33f, 659.25f };
        float[] amps = new float[] { 0.30f, 0.24f, 0.20f, 0.16f, 0.12f };

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / rate;
            float sum = 0f;
            for (int f = 0; f < freqs.Length; f++)
            {
                // フェルトピアノ特有の緩やかなアタックと深い減衰
                float attack = Mathf.Clamp01(t * 120f);
                float decay = Mathf.Exp(-t * (1.2f + f * 0.25f));
                // 基音 + 暖かみのある第2倍音
                float s1 = Mathf.Sin(2f * Mathf.PI * freqs[f] * t);
                float s2 = Mathf.Sin(2f * Mathf.PI * freqs[f] * 2f * t) * 0.25f;
                sum += (s1 + s2) * amps[f] * attack * decay;
            }
            data[i] = sum * 0.75f;
        }

        var clip = AudioClip.Create("PianoFeltChord", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

#if UNITY_EDITOR
    [MenuItem("Adventure/Spawn Ancient Piano Relic (古びたピアノと光る遺物)")]
    public static void EditorSpawnPianoRelic()
    {
        var existing = FindFirstObjectByType<AdventureAncientPianoRelic>();
        if (existing == null)
        {
            var go = new GameObject("AdventureAncientPianoRelic");
            Undo.RegisterCreatedObjectUndo(go, "Create Ancient Piano Relic");
            existing = go.AddComponent<AdventureAncientPianoRelic>();
        }

        Undo.RegisterFullObjectHierarchyUndo(existing.gameObject, "Spawn Piano Relic");
        existing.SpawnAtRandomLocation();
        EditorUtility.SetDirty(existing.gameObject);
        Debug.Log($"Successfully placed Ancient Piano & Glowing Relic at {existing.selectedLocationName}!");
    }
#endif
}
