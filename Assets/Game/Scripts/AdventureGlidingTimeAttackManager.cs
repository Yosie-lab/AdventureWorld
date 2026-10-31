using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 絶景スカイダイブ＆風のリングくぐり（滑空タイムアタック）マネージャー
/// 北の絶壁から西のビーチまで、10個の光る気流リングをくぐり抜ける爽快滑空コース。
/// [R]キーによる即時リトライで極上のリプレイ中毒性を実現。
/// </summary>
public partial class AdventureGlidingTimeAttackManager : MonoBehaviour
{
    private static AdventureGlidingTimeAttackManager _instance;
    public static AdventureGlidingTimeAttackManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Object.FindAnyObjectByType<AdventureGlidingTimeAttackManager>();
                if (_instance == null)
                {
                    _instance = Ensure();
                }
            }
            return _instance;
        }
    }

    [Header("コース構成")]
    public Transform startPlatform;
    public Transform startGate;
    public Transform finishTarget;
    public List<AdventureWindRing> courseRings = new List<AdventureWindRing>();

    [Header("タイムアタック状態")]
    public bool isRunning = false;
    public float currentTimer = 0f;
    public int ringsPassedCount = 0;
    public float bestTime = 999f;
    public string bestRank = "-";

    [Header("リザルト表示")]
    private bool _showResult = false;
    public bool IsShowingResult => _showResult;
    private float _resultTimer = 0f;
    private string _currentResultRank = "";
    private bool _isNewRecord = false;

    // スタート台の位置と向き（北の最高峰絶壁エッジ・南西方向を見下ろす大パノラマ絶景）
    // 木製床天面(96.525m)に対し、安全に着地・助走できる滑走路中央位置（足元の埋まり・引っかかりを防ぐ安全高度96.75m）
    public static readonly Vector3 START_POS = new Vector3(506.52f, 96.75f, 654.83f);
    public static readonly Quaternion START_ROT = Quaternion.Euler(0f, 205f, 0f); // 南西向き（西ビーチ方向）

    /// <summary>
    /// スタートフライトデッキ上（上面96.525m）にいればその表面Yを返す（テレポート時の足元スナップ・埋まり防止用）
    /// </summary>
    public static float GetFlightDeckSurfaceY(Vector3 worldPos)
    {
        // フライトデッキ中心付近 (505, 652) かつ 高度 80m〜105m
        float dx = worldPos.x - 505f;
        float dz = worldPos.z - 652f;
        if (dx * dx + dz * dz <= 18f * 18f && worldPos.y >= 80f && worldPos.y <= 105f)
        {
            return 96.525f;
        }
        return float.NegativeInfinity;
    }

    // ゴールターゲット位置
    private static readonly Vector3 GOAL_POS = new Vector3(150f, 6.5f, 240f);

    private GUIStyle _timeStyle;
    private GUIStyle _ringStyle;
    private GUIStyle _retryStyle;
    private GUIStyle _resultTitleStyle;
    private GUIStyle _resultBodyStyle;
    private GUIStyle _resultBtnStyle;
    private Texture2D _panelBg;

    private AudioSource _audioSource;
    private static AudioClip _startWhistleClip;
    private static AudioClip _ringDingClip;
    private static AudioClip _goalFanfareClip;

    private readonly HashSet<AdventureWindRing> _passedRings = new HashSet<AdventureWindRing>();
    private string _cachedRingDots = "";
    private int _cachedRingDotsPassed = -1;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // ベストタイム読み込み
        bestTime = PlayerPrefs.GetFloat("GlidingTA_BestTime", 999f);
        bestRank = PlayerPrefs.GetString("GlidingTA_BestRank", "-");

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f;
        _audioSource.volume = 0.9f;

        if (_startWhistleClip == null) _startWhistleClip = SynthesizeStartWhistle();
        if (_ringDingClip == null) _ringDingClip = SynthesizeRingDing();
        if (_goalFanfareClip == null) _goalFanfareClip = SynthesizeGoalFanfare();

        // 起動時にコース周辺の障害物コライダーを確実に掃除
        ClearCorridorObstacles();
    }

    void Start()
    {
        ClearCorridorObstacles();
        UpdateCourseVisibility();
    }

    void OnEnable()
    {
        AdventureWindRing.OnRingPassed += HandleRingPassed;
    }

    void OnDisable()
    {
        AdventureWindRing.OnRingPassed -= HandleRingPassed;
    }

    /// <summary>
    /// タイムアタック解放状態（砂浜の焚き火に火を灯すとアンロックされる）
    /// </summary>
    public static bool IsTimeAttackUnlocked
    {
        get
        {
            return (AdventureBeachCampfire.Instance != null && AdventureBeachCampfire.Instance.IsLit)
                || PlayerPrefs.GetInt(AdventureBeachCampfire.PrefKeyCampfireLit, 0) == 1;
        }
    }

    private bool _notifiedPlatformLocked = false;

    public bool IsGlidingActiveOrRecent
    {
        get
        {
            // 焚き火未点火の場合はタイムアタック関連HUDを一切表示しない
            if (!IsTimeAttackUnlocked) return false;

            if (isRunning || _showResult) return true;
            var player = AdventurePlayerController.Resolve();
            if (player != null)
            {
                // スタート台周辺（北の崖周辺）にいる場合
                if (Vector3.Distance(player.transform.position, START_POS) < 75f)
                    return true;
            }
            return false;
        }
    }

    /// <summary>タイムアタックのHUDやワープボタンを表示すべきかどうかの判定</summary>
    public bool ShouldShowTAHud => IsTimeAttackUnlocked && IsGlidingActiveOrRecent;

    void Update()
    {
        // タイムアタック未解放時：プレイヤーがスタート台（北の絶壁）に徒歩で接近したら案内を一度表示
        if (!IsTimeAttackUnlocked)
        {
            var p = AdventurePlayerController.Resolve();
            if (p != null && Vector3.Distance(p.transform.position, START_POS) < 18f)
            {
                if (!_notifiedPlatformLocked)
                {
                    _notifiedPlatformLocked = true;
                    AdventureNotificationToast.Show("🔥 砂浜の焚き火に火を灯すと、スカイダイブコースが解放されます！", 4.0f);
                    var rust = AdventureRustDrone.Instance;
                    if (rust != null)
                    {
                        rust.SpeakCustom("ここがスカイダイブ台だね！砂浜の焚き火に火を灯したら、風のリングが目覚めて飛べるようになるよ！", 5.0f);
                    }
                }
            }
            return;
        }

        // リザルトモーダル表示中の入力処理（マウスクリック＆キーボード[R]/[Esc]/[Space]の直接検知）
        if (_showResult)
        {
            TickResultModalInput();
            return;
        }

        // スタート台周辺または走行中のみ [R] キーによるリトライを受け付ける（島探索中の誤爆ワープ防止）
        if (AdventureInputReader.ResetDown || AdventureInputReader.TDown)
        {
            if (isRunning || ShouldShowTAHud)
            {
                RetryAtStart();
            }
        }

        if (isRunning)
        {
            // 走行中に [Esc] キーが押された場合、タイムアタックを中断してその場で通常探索へ戻る
            if (AdventureInputReader.EscapeDown)
            {
                AbortTrialAndResumeExploration();
                return;
            }

            currentTimer += Time.deltaTime;

            // ゴール判定（着地サークル近辺への進入、またはビーチへの着地）
            CheckGoalArrival();
        }
    }

    /// <summary>
    /// 砂浜の焚き火が点火された瞬間のコールバック（コース解放演出）
    /// ★タイムアタックのボード（リザルト画面等）は絶対に出さず、解放案内のみを美しく表示する
    /// </summary>
    public void OnCampfireLit(bool silent = false)
    {
        // タイムアタックボード（リザルト画面や走行HUD）は絶対に非表示にし、状態をクリーンに保つ
        ResetTrial();
        HideResultModalUI();
        _showResult = false;
        isRunning = false;

        UpdateCourseVisibility();
        ClearCorridorObstacles();

        if (!silent)
        {
            if (_audioSource != null && _startWhistleClip != null)
            {
                _audioSource.pitch = 1.15f;
                _audioSource.PlayOneShot(_startWhistleClip, 0.7f);
            }

            AdventureNotificationToast.Show("🪂 【スカイダイブコース解放！】 焚き火の上昇気流で空路が開通！ [R]キーでいつでも挑戦可能！", 5.0f);
            var drone = AdventureRustDrone.Instance;
            if (drone != null)
            {
                drone.SpeakCustom("わぁっ！焚き火の上昇気流で、北の絶壁のダイブコースがオープンしたよ！【Rキー】を押せば、いつでもスタート台へ飛べるよ！", 6.5f);
                drone.TriggerAffectionJoy();
                drone.SetExpression(AdventureRustDrone.FaceExpressionType.Joyful, 5.0f);
            }
        }
    }

    /// <summary>
    /// 焚き火の点火状態に応じて、コース（スタートゲート・リング・ゴールサークル）の有効/無効を切り替える
    /// </summary>
    public void UpdateCourseVisibility()
    {
        bool unlocked = IsTimeAttackUnlocked;

        if (startGate != null) startGate.gameObject.SetActive(unlocked);
        if (finishTarget != null) finishTarget.gameObject.SetActive(unlocked);

        if (courseRings != null)
        {
            for (int i = 0; i < courseRings.Count; i++)
            {
                if (courseRings[i] != null)
                {
                    courseRings[i].gameObject.SetActive(unlocked);
                }
            }
        }
    }

    /// <summary>
    /// タイムアタック走行を安全に中断し、その場で通常探索モードに戻す
    /// </summary>
    public void AbortTrialAndResumeExploration()
    {
        isRunning = false;
        _showResult = false;
        HideResultModalUI();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        var player = AdventurePlayerController.Resolve();
        if (player != null)
        {
            player.ForceGroundReset();
        }

        var drone = AdventureRustDrone.Instance;
        if (drone != null)
        {
            drone.SetSpeech(AdventureLocalizationManager.Get("ta_resume_exploration"), 3.0f);
        }

        Debug.Log("[RustAndFloat] タイムアタック中断 → 通常ゲーム探索へ復帰");
    }

    /// <summary>
    /// リザルト画面を閉じ、通常ゲーム（島探索）へシームレスに復帰する
    /// </summary>
    public void CloseResultAndResumeGame()
    {
        _showResult = false;
        isRunning = false;
        currentTimer = 0f;
        ringsPassedCount = 0;
        _passedRings.Clear();
        HideResultModalUI();

        // マウスカーソルをゲームプレイ用（非表示・ロック）に戻す
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        var player = AdventurePlayerController.Resolve();
        if (player != null)
        {
            player.ForceGroundReset();
        }

        var drone = AdventureRustDrone.Instance;
        if (drone != null)
        {
            drone.SetSpeech(AdventureLocalizationManager.Get("ta_resume_exploration"), 3.5f);
        }

        Debug.Log("[RustAndFloat] タイムアタックリザルト終了 → 通常ゲーム探索へ復帰");
    }

    /// <summary>
    /// スタート台へプレイヤーを即時テレポートし、リセット
    /// </summary>
    public void RetryAtStart()
    {
        // タイムアタック未解放の場合は案内を表示してテレポートを遮断
        if (!IsTimeAttackUnlocked)
        {
            AdventureNotificationToast.Show("🔥 砂浜の焚き火に火を灯すと、スカイダイブコースが解放されます！", 3.5f);
            var rustDrone = AdventureRustDrone.Instance;
            if (rustDrone != null)
            {
                rustDrone.SpeakCustom("まずは砂浜の焚き火に火をつけよう！あったかい火が灯ったら、絶壁のダイブコースがオープンするよ！", 4.5f);
            }
            return;
        }

        // タイムアタック開始・リトライ時は確実にゲーム開始状態として入力を完全開放
        AdventureRustFloatOpening.IsGameStarted = true;

        var player = AdventurePlayerController.Resolve();
        if (player != null)
        {
            // キャラクターコントローラーの一時無効化で安全にテレポート
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.transform.position = START_POS;
            player.transform.rotation = START_ROT;

            if (cc != null) cc.enabled = true;

            // 速度と滑空状態・接地状態を完全リセット
            player.ForceGroundReset();

            var rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // カメラの向きもプレイヤー背後へスナップ（スタート台では見下ろし角21°で草の被りを避け絶景を見渡す）
            var cam = Object.FindAnyObjectByType<AdventureCameraFollow>();
            if (cam != null) cam.SnapBehindTarget(21f);

            // 相棒Rustもプレイヤーの元へ即座に合流
            var rust = AdventureRustDrone.Instance;
            if (rust != null) rust.TeleportNearPlayer();

            // ゲームプレイ入力およびカーソル状態を即時復帰
            AdventureGameplayInputGate.TryRestoreExplorationLookIfReady();
        }

        // コース空路周辺の樹木コライダーを確実に非干渉化
        ClearCorridorObstacles();

        ResetTrial();

        AdventureNotificationToast.Show("🪂 スタート台へ帰還！風を切って飛び出そう！ [Space]で滑空", 2.8f);

        var drone = AdventureRustDrone.Instance;
        if (drone != null)
        {
            drone.SetSpeech("スタート台に戻ったよ！深呼吸して、最高のダイブをキメよう！", 3.0f);
        }
    }

    public void ResetTrial()
    {
        isRunning = false;
        currentTimer = 0f;
        ringsPassedCount = 0;
        _passedRings.Clear();
        _showResult = false;
        HideResultModalUI();
    }

    public void StartTrial()
    {
        if (!IsTimeAttackUnlocked || isRunning) return;

        var player = AdventurePlayerController.Resolve();
        // ★誤爆防止ガード：スタート台（北の絶壁）から100m以上離れた場所（浜辺など）では絶対にタイムアタックを開始しない！
        if (player != null && Vector3.Distance(player.transform.position, START_POS) > 100f)
        {
            return;
        }

        // 空路上の障害物を確実にクリアして離陸
        ClearCorridorObstacles();

        isRunning = true;
        currentTimer = 0f;
        ringsPassedCount = 0;
        _passedRings.Clear();
        _showResult = false;
        HideResultModalUI();

        if (_audioSource != null && _startWhistleClip != null)
            _audioSource.PlayOneShot(_startWhistleClip, 0.85f);

        AdventureNotificationToast.Show("🪂 スカイダイブ START!! 風のリングを連続でくぐれ！", 2.5f);

        var drone = AdventureRustDrone.Instance;
        if (drone != null)
        {
            drone.SetSpeech("スカイダイブスタート！風を掴め、Float！全速前進だーっ！", 3.5f);
        }
    }

    void HandleRingPassed(AdventureWindRing ring, AdventurePlayerController player)
    {
        if (!IsTimeAttackUnlocked) return;

        // コース対象リングか判定
        if (!courseRings.Contains(ring)) return;

        // ★未開始時は、スタート台直後の最初のリング（courseRings[0]）かつスタート台近傍にいる時のみ自動スタート！
        // （浜辺の Ring 13〜15 等に地上で触れてもタイムアタックを誤発火させない）
        if (!isRunning && !_showResult)
        {
            if (courseRings.Count > 0 && ring == courseRings[0])
            {
                StartTrial();
            }
            else
            {
                return; // コース途中リングでの勝手な開始を防止
            }
        }

        if (!_passedRings.Contains(ring))
        {
            _passedRings.Add(ring);
            ringsPassedCount++;

            if (_audioSource != null && _ringDingClip != null)
                _audioSource.PlayOneShot(_ringDingClip, 0.9f);

            string toast = $"✨ リング通過！ [{ringsPassedCount}/{courseRings.Count}] (加速ブースト中!)";
            AdventureNotificationToast.Show(toast, 1.4f);

            var drone = AdventureRustDrone.Instance;
            if (drone != null)
            {
                if (ringsPassedCount == courseRings.Count)
                {
                    drone.SetSpeech("パーフェクト！全リング通過だ！そのままビーチのゴールへダイブしろ！", 3.5f);
                }
                else if (ringsPassedCount == 1)
                {
                    drone.SetSpeech("ナイスイン！いいぞ、その調子で次へ繋げろ！", 2.2f);
                }
                else if (ringsPassedCount == 5)
                {
                    drone.SetSpeech("半分突破！素晴らしい滑空軌道だ！", 2.2f);
                }
            }
        }
    }

    void CheckGoalArrival()
    {
        if (!isRunning) return;

        var player = AdventurePlayerController.Resolve();
        if (player == null) return;

        // ★誤爆防止ガード：スタート台から実際にコース攻略を開始し、最低8秒経過・リングを3個以上くぐり・スタート台から200m以上離脱していること
        // （浜辺での歩行・ジャンプ・探索中にリザルトボードが出る誤爆を物理的・論理的に完全遮断）
        if (currentTimer < 8.0f || ringsPassedCount < 3 || Vector3.Distance(player.transform.position, START_POS) < 200f)
            return;

        Vector3 pPos = player.transform.position;
        float distToGoal = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(GOAL_POS.x, GOAL_POS.z));

        // 1. ゴール地点（着地サークル・ビーコン）周辺（半径32m以内、高度22m以下）
        bool nearGoal = distToGoal <= 32f && pPos.y <= 22f;

        // 2. 西ビーチエリア（X: 120〜210, Z: 200〜295）への進入・着地（リングを少なくとも4つ以上通過していること）
        bool inBeachArea = (pPos.x >= 120f && pPos.x <= 210f && pPos.z >= 200f && pPos.z <= 295f);
        bool landedInBeach = inBeachArea && ringsPassedCount >= 4 && (!player.IsGliding || player.IsGrounded || pPos.y <= 14f);

        // 3. 後半リング通過後、ビーチ側へ到達した瞬間
        bool finishedFlightLanding = (ringsPassedCount >= 6) && (distToGoal <= 45f || (inBeachArea && pPos.y <= 16f));

        if (nearGoal || landedInBeach || finishedFlightLanding)
        {
            FinishTrial();
        }
    }

    public void FinishTrial()
    {
        if (!isRunning) return;

        isRunning = false;
        _showResult = true;
        _resultTimer = currentTimer;

        // ゴール到達時にプレイヤーを確実に地上へソフトランディング着地
        var player = AdventurePlayerController.Resolve();
        if (player != null)
        {
            player.ForceGroundReset();
        }

        // 厳格なインディー最高峰基準によるランク計算
        // SS (神風アルティメット 👑): 29.5秒以内 かつ リング全通過 (全10個パーフェクト)
        // S  (神風マスター 🌟):      33.0秒以内 かつ リング9個以上
        // A  (爽快フライヤー 🥇):    40.0秒以内 かつ リング7個以上
        // B  (スカイダイバー 🥈):    50.0秒以内 かつ リング4個以上
        // C  (のんびりフライト 🥉): それ以外
        int total = courseRings.Count > 0 ? courseRings.Count : 10;
        bool isSS = _resultTimer <= 29.5f && ringsPassedCount >= total && total >= 8;
        bool isS = !isSS && _resultTimer <= 33.0f && ringsPassedCount >= 9;
        bool isA = !isSS && !isS && _resultTimer <= 40.0f && ringsPassedCount >= 7;
        bool isB = !isSS && !isS && !isA && (_resultTimer <= 50.0f && ringsPassedCount >= 4);

        if (isSS)
        {
            _currentResultRank = AdventureLocalizationManager.Get("ta_rank_ss");
        }
        else if (isS)
        {
            _currentResultRank = AdventureLocalizationManager.Get("ta_rank_s");
        }
        else if (isA)
        {
            _currentResultRank = AdventureLocalizationManager.Get("ta_rank_a");
        }
        else if (isB)
        {
            _currentResultRank = AdventureLocalizationManager.Get("ta_rank_b");
        }
        else
        {
            _currentResultRank = AdventureLocalizationManager.Get("ta_rank_c");
        }

        // ベストタイム判定
        if (_resultTimer < bestTime)
        {
            bestTime = _resultTimer;
            bestRank = _currentResultRank;
            _isNewRecord = true;
            PlayerPrefs.SetFloat("GlidingTA_BestTime", bestTime);
            PlayerPrefs.SetString("GlidingTA_BestRank", bestRank);
            PlayerPrefs.Save();
        }
        else
        {
            _isNewRecord = false;
        }

        if (_audioSource != null && _goalFanfareClip != null)
            _audioSource.PlayOneShot(_goalFanfareClip, 0.95f);

        if (isSS)
        {
            AdventureInputHaptics.PlayTimeAttackSSCelebration();
        }
        else
        {
            AdventureInputHaptics.PlayScrapPickupChime();
        }

        var drone = AdventureRustDrone.Instance;
        if (drone != null)
        {
            if (_isNewRecord)
            {
                drone.SetExpression(AdventureRustDrone.FaceExpressionType.Joyful, 6.0f);
            }
            else if (isSS || isS || isA)
            {
                drone.SetExpression(AdventureRustDrone.FaceExpressionType.Smiling, 5.0f);
            }

            if (isSS)
            {
                drone.TriggerCelebrate();
                drone.SetSpeech(AdventureLocalizationManager.GetFormat("ta_clear_ss", _resultTimer), 6.5f);
            }
            else if (isS)
            {
                drone.TriggerAffectionJoy();
                drone.SetSpeech(AdventureLocalizationManager.GetFormat("ta_clear_s", _resultTimer), 5.5f);
            }
            else if (isA)
            {
                drone.TriggerAffectionJoy();
                drone.SetSpeech(AdventureLocalizationManager.GetFormat("ta_clear_a", _resultTimer), 5.0f);
            }
            else
            {
                if (_isNewRecord)
                {
                    drone.TriggerAffectionJoy();
                }
                drone.SetSpeech(AdventureLocalizationManager.GetFormat("ta_clear_generic", _resultTimer), 5.0f);
            }
        }

        // uGUI高精細リザルトモーダルの表示（マウスカーソル自動解放対応）
        EnsureResultModalUI();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnGUI()
    {
        // タイムアタック領域外、またはリザルト表示中はOnGUIボタンを描画しない（通常探索の邪魔をしない）
        if (!ShouldShowTAHud || _showResult) return;

        EnsureStyles();

        // 画面右上にいつでも即座にスタート台へ行けるボタン（クリックまたは [R] / [T] キー対応）
        float sW = Screen.width;
        float bW = 190f;
        float bH = 36f;
        Rect warpBtn = new Rect(sW - bW - 18f, 55f, bW, bH);
        GUI.color = new Color(0.12f, 0.45f, 0.85f, 0.85f);
        if (GUI.Button(warpBtn, AdventureLocalizationManager.Get("ta_btn_warp")))
        {
            RetryAtStart();
        }
        GUI.color = Color.white;

        // 走行中のHUD
        if (isRunning)
        {
            // 上部中央タイマー
            float screenW = Screen.width;
            float panelW = 360f;
            float panelH = 75f;
            Rect panelRect = new Rect((screenW - panelW) * 0.5f, 15f, panelW, panelH);

            GUI.color = new Color(0.04f, 0.08f, 0.15f, 0.85f);
            GUI.DrawTexture(panelRect, _panelBg);
            GUI.color = Color.white;

            // タイム文字列
            int min = (int)(currentTimer / 60f);
            float sec = currentTimer % 60f;
            string timeStr = string.Format("{0:00}:{1:00.00}", min, sec);

            // ベストタイム表示（記録がある場合）
            string bestDisplay = (bestTime < 900f)
                ? $"  <size=16><color=#FFE066>(BEST: {bestTime:F2}s)</color></size>"
                : "";

            GUI.Label(new Rect(panelRect.x, panelRect.y + 4, panelW, 36), $"⏱  {timeStr}{bestDisplay}", _timeStyle);

            // リング数
            int total = courseRings.Count > 0 ? courseRings.Count : 10;
            if (_cachedRingDotsPassed != ringsPassedCount)
            {
                _cachedRingDotsPassed = ringsPassedCount;
                var sb = new System.Text.StringBuilder(total);
                for (int i = 0; i < total; i++)
                {
                    sb.Append(i < ringsPassedCount ? "◆" : "◇");
                }
                _cachedRingDots = sb.ToString();
            }
            string ringText = AdventureLocalizationManager.GetFormat("ta_hud_rings", ringsPassedCount, total, _cachedRingDots);
            GUI.Label(new Rect(panelRect.x, panelRect.y + 40, panelW, 26), ringText, _ringStyle);

            // リトライ案内
            string retryText = AdventureLocalizationManager.Get("ta_hud_retry");
            GUI.Label(new Rect(panelRect.x, panelRect.y + panelH + 5, panelW, 22), retryText, _retryStyle);
        }

        // リザルト画面は uGUI モーダル（AdventureGlidingTimeAttackManager.UI.cs）で美しく描画
    }

    static Font _cachedTaFont;
    static Font ResolveTaFont()
    {
        if (_cachedTaFont != null)
        {
            try
            {
                CharacterInfo info;
                _cachedTaFont.RequestCharactersInTexture("あA", 24, FontStyle.Normal);
                if (!_cachedTaFont.GetCharacterInfo('あ', out info, 24, FontStyle.Normal) &&
                    !_cachedTaFont.GetCharacterInfo('A', out info, 24, FontStyle.Normal))
                {
                    _cachedTaFont = null;
                }
            }
            catch { _cachedTaFont = null; }
        }
        if (_cachedTaFont != null) return _cachedTaFont;

        string[] candidates = {
            "PingFang SC",
            "Hiragino Sans GB",
            "Heiti SC",
            "HiraginoSans-W3",
            "HiraginoSans-W6",
            "Hiragino Kaku Gothic ProN",
            "Hiragino Kaku Gothic ProN W3",
            "YuGothic",
            "Yu Gothic",
            "YuGothic-Medium",
            "Apple SD Gothic Neo",
            "Microsoft YaHei",
            "SimHei",
            "Arial Unicode MS",
            "Helvetica Neue",
            "Arial"
        };

        try
        {
            Font multiFont = Font.CreateDynamicFontFromOSFont(candidates, 24);
            if (multiFont != null)
            {
                _cachedTaFont = multiFont;
                return multiFont;
            }
        }
        catch { }

        for (int i = 0; i < candidates.Length; i++)
        {
            try
            {
                Font font = Font.CreateDynamicFontFromOSFont(candidates[i], 24);
                if (font == null) continue;
                font.RequestCharactersInTexture("あA", 24, FontStyle.Normal);
                CharacterInfo info;
                if (font.GetCharacterInfo('あ', out info, 24, FontStyle.Normal) ||
                    font.GetCharacterInfo('A', out info, 24, FontStyle.Normal))
                {
                    _cachedTaFont = font;
                    return font;
                }
            }
            catch { }
        }

        _cachedTaFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        return _cachedTaFont;
    }

    void EnsureStyles()
    {
        Font sharedFont = ResolveTaFont();

        if (_panelBg == null)
        {
            _panelBg = new Texture2D(1, 1);
            _panelBg.SetPixel(0, 0, Color.white);
            _panelBg.Apply();
        }

        if (_timeStyle == null)
        {
            _timeStyle = new GUIStyle();
            _timeStyle.font = sharedFont;
            _timeStyle.fontSize = 24;
            _timeStyle.fontStyle = FontStyle.Bold;
            _timeStyle.alignment = TextAnchor.MiddleCenter;
            _timeStyle.normal.textColor = new Color(0.3f, 0.95f, 1.0f);
            _timeStyle.richText = true;
        }

        if (_ringStyle == null)
        {
            _ringStyle = new GUIStyle();
            _ringStyle.font = sharedFont;
            _ringStyle.fontSize = 15;
            _ringStyle.alignment = TextAnchor.MiddleCenter;
            _ringStyle.normal.textColor = new Color(0.85f, 1.0f, 0.85f);
        }

        if (_retryStyle == null)
        {
            _retryStyle = new GUIStyle();
            _retryStyle.font = sharedFont;
            _retryStyle.fontSize = 12;
            _retryStyle.alignment = TextAnchor.MiddleCenter;
            _retryStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f, 0.8f);
        }

        if (_resultTitleStyle == null)
        {
            _resultTitleStyle = new GUIStyle();
            _resultTitleStyle.font = sharedFont;
            _resultTitleStyle.fontSize = 24;
            _resultTitleStyle.fontStyle = FontStyle.Bold;
            _resultTitleStyle.alignment = TextAnchor.MiddleCenter;
            _resultTitleStyle.normal.textColor = new Color(1.0f, 0.90f, 0.25f);
        }

        if (_resultBodyStyle == null)
        {
            _resultBodyStyle = new GUIStyle();
            _resultBodyStyle.font = sharedFont;
            _resultBodyStyle.fontSize = 18;
            _resultBodyStyle.fontStyle = FontStyle.Bold;
            _resultBodyStyle.alignment = TextAnchor.UpperLeft;
            _resultBodyStyle.normal.textColor = new Color(0.95f, 0.98f, 1.0f);
            _resultBodyStyle.richText = true;
        }

        if (_resultBtnStyle == null)
        {
            _resultBtnStyle = new GUIStyle(GUI.skin.button);
            _resultBtnStyle.font = sharedFont;
            _resultBtnStyle.fontSize = 18;
            _resultBtnStyle.fontStyle = FontStyle.Bold;
            _resultBtnStyle.alignment = TextAnchor.MiddleCenter;
            _resultBtnStyle.normal.textColor = Color.white;
        }
    }

    #region 音声合成 (Audio Synthesis)
    static AudioClip SynthesizeStartWhistle()
    {
        const int rate = 44100;
        float duration = 0.65f;
        int count = (int)(rate * duration);
        float[] data = new float[count];

        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float freq = Mathf.Lerp(600f, 1200f, t * t);
            phase += 2f * Mathf.PI * freq / rate;
            float env = Mathf.Sin(t * Mathf.PI);
            data[i] = Mathf.Sin(phase) * env * 0.6f;
        }

        var clip = AudioClip.Create("StartWhistle", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip SynthesizeRingDing()
    {
        const int rate = 44100;
        float duration = 0.55f;
        int count = (int)(rate * duration);
        float[] data = new float[count];

        float p1 = 0f, p2 = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            p1 += 2f * Mathf.PI * 880f / rate;  // A5
            p2 += 2f * Mathf.PI * 1320f / rate; // E6
            float env = Mathf.Exp(-t * 6.0f);
            data[i] = (Mathf.Sin(p1) * 0.5f + Mathf.Sin(p2) * 0.4f) * env * 0.7f;
        }

        var clip = AudioClip.Create("RingDing", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip SynthesizeGoalFanfare()
    {
        const int rate = 44100;
        float duration = 1.8f;
        int count = (int)(rate * duration);
        float[] data = new float[count];

        // 3音アルペジオファンファーレ (C5 -> E5 -> G5 -> C6)
        float[] freqs = { 523.25f, 659.25f, 783.99f, 1046.50f };
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float sample = 0f;

            for (int f = 0; f < freqs.Length; f++)
            {
                float noteStart = f * 0.22f;
                if (t >= noteStart)
                {
                    float noteT = t - noteStart;
                    float freq = freqs[f];
                    float phase = 2f * Mathf.PI * freq * noteT;
                    float env = Mathf.Exp(-noteT * 2.8f);
                    sample += Mathf.Sin(phase) * env * 0.35f;
                }
            }

            data[i] = Mathf.Clamp(sample, -1f, 1f) * 0.8f;
        }

        var clip = AudioClip.Create("GoalFanfare", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
    #endregion

    #region 実行時自動保証 (Ensure)

    public const string COURSE_ROOT_NAME = "GlidingTimeAttackCourse";

    /// <summary>
    /// シーン内にタイムアタックコースおよびマネージャーが存在することを保証する
    /// </summary>
    public static AdventureGlidingTimeAttackManager Ensure()
    {
        if (_instance != null) return _instance;

        var existing = Object.FindAnyObjectByType<AdventureGlidingTimeAttackManager>();
        if (existing != null)
        {
            _instance = existing;
            return _instance;
        }

        var courseRoot = GameObject.Find(COURSE_ROOT_NAME);
        if (courseRoot == null)
        {
            courseRoot = new GameObject(COURSE_ROOT_NAME);
        }

        var mgr = courseRoot.GetComponent<AdventureGlidingTimeAttackManager>();
        if (mgr == null)
        {
            mgr = courseRoot.AddComponent<AdventureGlidingTimeAttackManager>();
        }
        _instance = mgr;

        // コースオブジェクト（デッキ、ゲート、リング10基、ゴール）が未構築なら動的生成
        mgr.BuildCourseIfMissing();

        // スタート台周辺の草花をクリア（視界確保）
        AdventureTerrainSnap.FixFlightDeckGrass();

        return mgr;
    }

    private void BuildCourseIfMissing()
    {
        // 既存オブジェクトの更新（位置や高さの更新を確実に反映）
        var oldPlatform = transform.Find("StartFlightDeck");
        if (oldPlatform != null && startPlatform == null)
        {
            if (Application.isPlaying) Destroy(oldPlatform.gameObject);
            else DestroyImmediate(oldPlatform.gameObject);
        }
        if (startPlatform == null)
        {
            startPlatform = BuildStartPlatformRuntime(transform).transform;
        }

        var oldGate = transform.Find("StartGate");
        if (oldGate != null && startGate == null)
        {
            if (Application.isPlaying) Destroy(oldGate.gameObject);
            else DestroyImmediate(oldGate.gameObject);
        }
        if (startGate == null)
        {
            startGate = BuildStartGateRuntime(transform).transform;
        }

        if (courseRings == null || courseRings.Count < 15)
        {
            var r = transform.Find("Rings");
            if (r != null)
            {
                if (Application.isPlaying) Destroy(r.gameObject);
                else DestroyImmediate(r.gameObject);
            }
            courseRings = BuildCourseRingsRuntime(transform);
        }

        if (finishTarget == null)
        {
            var f = transform.Find("FinishTarget");
            finishTarget = f != null ? f : BuildFinishTargetRuntime(transform).transform;
        }

        // コース空路周辺（樹木の梢や枝コライダー）の干渉を掃除
        ClearCorridorObstacles();
    }

    /// <summary>
    /// タイムアタック航路（リング近辺およびリング間コリドー）にある樹木・巨岩・木道板等の障害物コライダーを掃除し、空中での突然のスタック・引っ掛かりを防止
    /// </summary>
    public void ClearCorridorObstacles()
    {
        if (courseRings == null || courseRings.Count == 0) return;
        var allCols = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude);

        // スタートデッキから各リング、ゴールまでの航路ウェイポイント列を構築
        var pts = new List<Vector3>(courseRings.Count + 2);
        pts.Add(START_POS);
        for (int r = 0; r < courseRings.Count; r++)
        {
            if (courseRings[r] != null) pts.Add(courseRings[r].transform.position);
        }
        pts.Add(GOAL_POS);

        for (int i = 0; i < allCols.Length; i++)
        {
            var col = allCols[i];
            if (col == null || col.isTrigger) continue;
            if (col is TerrainCollider) continue;

            // プレイヤー自身・相棒・スタートデッキの足場床・ゴールターゲット本体は絶対に無効化しない
            string colName = col.name;
            if (col.CompareTag("Player") || colName.Contains("Niko") || colName.Contains("Rust")
                || colName == "DeckPlanks" || colName == "FinishTarget" || colName.Contains("Gate"))
                continue;

            Transform pTr = col.transform;
            bool isObstacle = false;
            while (pTr != null)
            {
                string n = pTr.name.ToLower();
                if (n.Contains("tree") || n.Contains("branch") || n.Contains("foliage")
                    || n.Contains("leaves") || n.Contains("willow") || n.Contains("broadleaf")
                    || n.Contains("fir") || n.Contains("pine") || n.Contains("blossom")
                    || n.Contains("stone") || n.Contains("rock") || n.Contains("boulder")
                    || n.Contains("plank") || n.Contains("ramp") || n.Contains("bridge")
                    || n.Contains("fence") || n.Contains("stair") || n.Contains("capyta"))
                {
                    isObstacle = true;
                    break;
                }
                pTr = pTr.parent;
            }

            if (!isObstacle) continue;

            Vector3 center = col.bounds.center;

            // 1. 各リング中心から半径22m以内であれば無効化
            bool shouldDisable = false;
            for (int p = 0; p < pts.Count; p++)
            {
                if (Vector3.Distance(center, pts[p]) <= 22.0f)
                {
                    shouldDisable = true;
                    break;
                }
            }

            // 2. リング間の飛行セグメント（空路コリドー幅18m・高度差22m）に近接していれば無効化
            if (!shouldDisable)
            {
                for (int s = 0; s < pts.Count - 1; s++)
                {
                    Vector3 a = pts[s];
                    Vector3 b = pts[s + 1];
                    float dist = SqrDistanceToSegment(center, a, b);
                    if (dist <= 18.0f * 18.0f && Mathf.Abs(center.y - Mathf.Lerp(a.y, b.y, 0.5f)) <= 22.0f)
                    {
                        shouldDisable = true;
                        break;
                    }
                }
            }

            if (shouldDisable)
            {
                col.enabled = false;
            }
        }
    }

    private static float SqrDistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float l2 = ab.sqrMagnitude;
        if (l2 < 0.0001f) return (point - a).sqrMagnitude;
        float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / l2);
        Vector3 proj = a + t * ab;
        return (point - proj).sqrMagnitude;
    }

    private static GameObject BuildStartPlatformRuntime(Transform parent)
    {
        var platformRoot = new GameObject("StartFlightDeck");
        platformRoot.transform.SetParent(parent, false);
        platformRoot.transform.position = new Vector3(508f, 96.5f, 658f);
        platformRoot.transform.rotation = Quaternion.Euler(0f, 205f, 0f);

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        deck.name = "DeckPlanks";
        deck.transform.SetParent(platformRoot.transform, false);
        deck.transform.localPosition = new Vector3(0f, -0.2f, 5.5f);
        deck.transform.localScale = new Vector3(6.0f, 0.45f, 12.0f);

        // CharacterController の引っ掛かりを防止し、確実な足場として機能するボックスコライダー
        var deckCol = deck.GetComponent<BoxCollider>();
        if (deckCol != null)
        {
            deckCol.size = new Vector3(1f, 1f, 1f);
            deckCol.center = Vector3.zero;
            // 摩擦ゼロのマテリアルを適用し、助走・ダッシュ時の足元の引っかかりを完全排除
            var smoothMat = new PhysicsMaterial("FlightDeckSmooth")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            deckCol.material = smoothMat;
        }

        var deckMat = new Material(shader);
        deckMat.color = new Color(0.42f, 0.30f, 0.20f);
        var r = deck.GetComponent<Renderer>();
        if (r != null) r.material = deckMat;

        var runway = GameObject.CreatePrimitive(PrimitiveType.Cube);
        runway.name = "RunwayStripe";
        runway.transform.SetParent(platformRoot.transform, false);
        runway.transform.localPosition = new Vector3(0f, 0.05f, 5.5f);
        runway.transform.localScale = new Vector3(0.65f, 0.08f, 10.5f);
        var col = runway.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }

        var runMat = new Material(shader);
        runMat.color = new Color(0.15f, 0.95f, 1.0f);
        runMat.EnableKeyword("_EMISSION");
        runMat.SetColor("_EmissionColor", new Color(0.15f, 0.95f, 1.0f) * 2.5f);
        var runR = runway.GetComponent<Renderer>();
        if (runR != null) runR.material = runMat;

        for (int side = -1; side <= 1; side += 2)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = $"DeckPillar_{side}";
            pillar.transform.SetParent(platformRoot.transform, false);
            pillar.transform.localPosition = new Vector3(side * 2.6f, 1.5f, 1.0f);
            pillar.transform.localScale = new Vector3(0.18f, 1.5f, 0.18f);
            var pCol = pillar.GetComponent<Collider>();
            if (pCol != null)
            {
                if (Application.isPlaying) Destroy(pCol);
                else DestroyImmediate(pCol);
            }

            var pMat = new Material(shader);
            pMat.color = new Color(0.3f, 0.22f, 0.15f);
            var pR = pillar.GetComponent<Renderer>();
            if (pR != null) pR.material = pMat;
        }

        return platformRoot;
    }

    private static GameObject BuildStartGateRuntime(Transform parent)
    {
        var gateRoot = new GameObject("StartGate");
        gateRoot.transform.SetParent(parent, false);
        gateRoot.transform.position = new Vector3(503.98f, 98.0f, 649.39f);
        gateRoot.transform.rotation = Quaternion.Euler(0f, 205f, 0f);

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var gateMat = new Material(shader);
        Color gateCol = new Color(0.1f, 0.95f, 1.0f);
        gateMat.color = gateCol;
        gateMat.EnableKeyword("_EMISSION");
        gateMat.SetColor("_EmissionColor", gateCol * 3.5f);

        int segments = 18;
        float radius = 5.0f;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * (Mathf.PI * 2f / segments);
            Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            var seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            seg.name = "GateSeg_" + i;
            seg.transform.SetParent(gateRoot.transform, false);
            seg.transform.localPosition = pos;
            seg.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);

            float deg = angle * Mathf.Rad2Deg;
            seg.transform.localRotation = Quaternion.Euler(0f, 0f, deg + 90f);
            var sCol = seg.GetComponent<Collider>();
            if (sCol != null) Destroy(sCol);
            var sR = seg.GetComponent<Renderer>();
            if (sR != null) sR.material = gateMat;
        }

        var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "StartBeacon";
        beacon.transform.SetParent(gateRoot.transform, false);
        beacon.transform.localPosition = new Vector3(0f, 25f, 0f);
        beacon.transform.localScale = new Vector3(0.35f, 25f, 0.35f);
        var bCol = beacon.GetComponent<Collider>();
        if (bCol != null) Destroy(bCol);
        var bR = beacon.GetComponent<Renderer>();
        if (bR != null) bR.material = gateMat;

        var trigger = gateRoot.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(10f, 10f, 3.5f);

        gateRoot.AddComponent<GlidingStartTriggerHelper>();

        return gateRoot;
    }

    private static List<AdventureWindRing> BuildCourseRingsRuntime(Transform parent)
    {
        var ringsRoot = new GameObject("Rings");
        ringsRoot.transform.SetParent(parent, false);

        Vector3[] waypoints = new Vector3[]
        {
            new Vector3(502f, 88.5f, 642f), // Ring 01: 飛び出し加速
            new Vector3(488f, 83.5f, 615f), // Ring 02: 谷間へ進入
            new Vector3(472f, 78.0f, 585f), // Ring 03: 谷間中央
            new Vector3(455f, 72.5f, 555f), // Ring 04: 西の森頭上
            new Vector3(435f, 66.5f, 520f), // Ring 05: 遺跡頭上
            new Vector3(410f, 60.0f, 485f), // Ring 06: 大草原花畑上空
            new Vector3(385f, 54.0f, 455f), // Ring 07: 草原中央
            new Vector3(355f, 47.5f, 425f), // Ring 08: 丘陵尾根スルー
            new Vector3(325f, 41.0f, 395f), // Ring 09: 西風車頭上
            new Vector3(295f, 35.0f, 365f), // Ring 10: 西海岸アプローチ
            new Vector3(265f, 29.0f, 335f), // Ring 11: 海岸崖への旋回
            new Vector3(230f, 22.5f, 305f), // Ring 12: 海が見える急降下
            new Vector3(195f, 16.5f, 278f), // Ring 13: 西ビーチ白砂直上
            new Vector3(170f, 12.0f, 258f), // Ring 14: 座礁艇前ラストスパート
            new Vector3(155f,  9.0f, 245f)  // Ring 15: 着地サークル手前
        };

        var list = new List<AdventureWindRing>();

        for (int i = 0; i < waypoints.Length; i++)
        {
            var ringGo = new GameObject($"FlightRing_{i + 1:D2}");
            ringGo.transform.SetParent(ringsRoot.transform, false);
            ringGo.transform.position = waypoints[i];

            Vector3 targetNext = (i < waypoints.Length - 1) ? waypoints[i + 1] : new Vector3(150f, 6.35f, 235f);
            Vector3 forwardDir = (targetNext - waypoints[i]).normalized;
            if (forwardDir.sqrMagnitude > 0.01f)
            {
                ringGo.transform.rotation = Quaternion.LookRotation(forwardDir);
            }

            var ringComp = ringGo.AddComponent<AdventureWindRing>();
            ringComp.boostMultiplier = 1.25f;
            ringComp.boostDuration = 4.5f;

            float t = (float)i / (waypoints.Length - 1);
            ringComp.ringColor = Color.Lerp(
                new Color(0.15f, 1.0f, 0.70f),
                new Color(0.20f, 0.85f, 1.0f),
                t
            );

            ringComp.CreateRingVisual();
            list.Add(ringComp);
        }

        return list;
    }

    private static GameObject BuildFinishTargetRuntime(Transform parent)
    {
        var targetRoot = new GameObject("FinishTarget");
        targetRoot.transform.SetParent(parent, false);
        targetRoot.transform.position = new Vector3(150f, 6.35f, 235f);

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        float[] radii = { 7.5f, 5.0f, 2.5f };
        Color[] colors = {
            new Color(0.1f, 0.85f, 1.0f),
            new Color(0.2f, 1.0f, 0.7f),
            new Color(1.0f, 0.9f, 0.2f)
        };

        for (int r = 0; r < radii.Length; r++)
        {
            int segments = 24;
            float radius = radii[r];
            Color c = colors[r];

            var ringMat = new Material(shader);
            ringMat.color = c;
            ringMat.EnableKeyword("_EMISSION");
            ringMat.SetColor("_EmissionColor", c * 2.5f);

            var cRoot = new GameObject($"Circle_{r}");
            cRoot.transform.SetParent(targetRoot.transform, false);

            for (int i = 0; i < segments; i++)
            {
                float angle = i * (Mathf.PI * 2f / segments);
                Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, 0.05f, Mathf.Sin(angle) * radius);
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                seg.name = $"Seg_{i}";
                seg.transform.SetParent(cRoot.transform, false);
                seg.transform.localPosition = pos;
                seg.transform.localScale = new Vector3(0.4f, 0.05f, 0.75f);

                float deg = angle * Mathf.Rad2Deg;
                seg.transform.localRotation = Quaternion.Euler(0f, -deg + 90f, 0f);
                var sCol = seg.GetComponent<Collider>();
                if (sCol != null) Destroy(sCol);
                var sR = seg.GetComponent<Renderer>();
                if (sR != null) sR.material = ringMat;
            }
        }

        var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "GoalBeaconPillar";
        beacon.transform.SetParent(targetRoot.transform, false);
        beacon.transform.localPosition = new Vector3(0f, 17.5f, 0f);
        beacon.transform.localScale = new Vector3(0.4f, 17.5f, 0.4f);
        var bCol = beacon.GetComponent<Collider>();
        if (bCol != null) Destroy(bCol);

        var bMat = new Material(shader);
        Color bColVal = new Color(1.0f, 0.85f, 0.2f, 0.45f);
        bMat.color = bColVal;
        bMat.EnableKeyword("_EMISSION");
        bMat.SetColor("_EmissionColor", bColVal * 2.2f);
        var bR = beacon.GetComponent<Renderer>();
        if (bR != null) bR.material = bMat;

        var trigger = targetRoot.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 20.0f;

        targetRoot.AddComponent<GlidingGoalTriggerHelper>();

        return targetRoot;
    }

    #endregion
}

/// <summary>
/// スタートゲート進入検知ヘルパー
/// </summary>
public class GlidingStartTriggerHelper : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<AdventurePlayerController>()
            ?? other.GetComponentInParent<AdventurePlayerController>();
        if (player != null)
        {
            var mgr = AdventureGlidingTimeAttackManager.Instance;
            if (mgr != null && !mgr.isRunning)
            {
                mgr.StartTrial();
            }
        }
    }
}

/// <summary>
/// ゴール着地検知ヘルパー
/// </summary>
public class GlidingGoalTriggerHelper : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<AdventurePlayerController>()
            ?? other.GetComponentInParent<AdventurePlayerController>();
        if (player != null)
        {
            var mgr = AdventureGlidingTimeAttackManager.Instance;
            // 実際に飛行してコースを攻略中（最低4秒以上かつリングをくぐっている）のみゴール処理
            if (mgr != null && mgr.isRunning && mgr.currentTimer >= 4.0f && mgr.ringsPassedCount >= 1)
            {
                mgr.FinishTrial();
            }
        }
    }
}
