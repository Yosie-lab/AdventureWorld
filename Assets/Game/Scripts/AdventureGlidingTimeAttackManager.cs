using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 絶景スカイダイブ＆風のリングくぐり（滑空タイムアタック）マネージャー
/// 北の絶壁から西のビーチまで、10個の光る気流リングをくぐり抜ける爽快滑空コース。
/// [R]キーによる即時リトライで極上のリプレイ中毒性を実現。
/// </summary>
public class AdventureGlidingTimeAttackManager : MonoBehaviour
{
    private static AdventureGlidingTimeAttackManager _instance;
    public static AdventureGlidingTimeAttackManager Instance => _instance;

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
    private float _resultTimer = 0f;
    private string _currentResultRank = "";
    private bool _isNewRecord = false;

    // スタート台の位置と向き（北の最高峰絶壁エッジ・南西方向を見下ろす大パノラマ絶景）
    private static readonly Vector3 START_POS = new Vector3(508f, 94.5f, 656f);
    private static readonly Quaternion START_ROT = Quaternion.Euler(0f, 205f, 0f); // 南西向き（西ビーチ方向）

    // ゴールターゲット位置
    private static readonly Vector3 GOAL_POS = new Vector3(150f, 6.5f, 240f);

    private GUIStyle _timeStyle;
    private GUIStyle _ringStyle;
    private GUIStyle _retryStyle;
    private GUIStyle _resultTitleStyle;
    private GUIStyle _resultBodyStyle;
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
    }

    void OnEnable()
    {
        AdventureWindRing.OnRingPassed += HandleRingPassed;
    }

    void OnDisable()
    {
        AdventureWindRing.OnRingPassed -= HandleRingPassed;
    }

    public bool IsGlidingActiveOrRecent
    {
        get
        {
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

    void Update()
    {
        // [R]キーまたは[T]キーでいつでもスタート地点へ即時リトライ（新旧InputSystem両対応）
        bool rPressed = false;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && (kb.rKey.wasPressedThisFrame || kb.tKey.wasPressedThisFrame))
        {
            rPressed = true;
        }
        else
        {
            try { if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.T)) rPressed = true; } catch { }
        }

        if (rPressed)
        {
            RetryAtStart();
        }

        if (isRunning)
        {
            currentTimer += Time.deltaTime;

            // ゴール判定（着地サークル近辺への進入、またはビーチへの着地）
            CheckGoalArrival();
        }
    }

    /// <summary>
    /// スタート台へプレイヤーを即時テレポートし、リセット
    /// </summary>
    public void RetryAtStart()
    {
        var player = AdventurePlayerController.Resolve();
        if (player != null)
        {
            // キャラクターコントローラーの一時無効化で安全にテレポート
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.transform.position = START_POS;
            player.transform.rotation = START_ROT;

            if (cc != null) cc.enabled = true;

            // 速度と滑空状態をリセット
            var rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // カメラの向きもプレイヤー背後にスナップ
            var cam = Object.FindAnyObjectByType<AdventureCameraFollow>();
            if (cam != null) cam.SnapBehindTarget();
        }

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
    }

    public void StartTrial()
    {
        if (isRunning) return;

        isRunning = true;
        currentTimer = 0f;
        ringsPassedCount = 0;
        _passedRings.Clear();
        _showResult = false;

        if (_audioSource != null && _startWhistleClip != null)
            _audioSource.PlayOneShot(_startWhistleClip, 0.85f);

        AdventureNotificationToast.Show("🪂 スカイダイブ START!! 風のリングを連続でくぐれ！", 2.5f);

        var drone = AdventureRustDrone.Instance;
        if (drone != null)
        {
            drone.SetSpeech("スカイダイブスタート！風を掴め、Niko！全速前進だーっ！", 3.5f);
        }
    }

    void HandleRingPassed(AdventureWindRing ring, AdventurePlayerController player)
    {
        // コース対象リングか判定
        if (!courseRings.Contains(ring)) return;

        // まだ開始していない場合、最初のリングで自動スタート
        if (!isRunning && !_showResult)
        {
            StartTrial();
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
        var player = AdventurePlayerController.Resolve();
        if (player == null) return;

        Vector3 pPos = player.transform.position;
        float distToGoal = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(GOAL_POS.x, GOAL_POS.z));

        // ゴール地点半径14m以内かつ高度低め、または西ビーチに着地した瞬間
        bool nearGoal = distToGoal <= 14f && pPos.y <= 12f;
        bool landedNearBeach = distToGoal <= 35f && !player.IsGliding && pPos.y <= 8.5f;

        if (nearGoal || landedNearBeach)
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

        // ランク計算
        // S: 34秒以内 & リング8個以上
        // A: 44秒以内 & リング6個以上
        // B: 58秒以内 & リング3個以上
        // C: それ以外
        int total = courseRings.Count > 0 ? courseRings.Count : 10;
        if (_resultTimer <= 34.0f && ringsPassedCount >= 8)
        {
            _currentResultRank = "S (神風マスター 🌟)";
        }
        else if (_resultTimer <= 44.0f && ringsPassedCount >= 6)
        {
            _currentResultRank = "A (爽快フライヤー 🥇)";
        }
        else if (_resultTimer <= 58.0f)
        {
            _currentResultRank = "B (スカイダイバー 🥈)";
        }
        else
        {
            _currentResultRank = "C (のんびりフライト 🥉)";
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

        var drone = AdventureRustDrone.Instance;
        if (drone != null)
        {
            if (_currentResultRank.StartsWith("S"))
            {
                drone.SetSpeech($"信じられないスピードだ、Niko！Sランク達成！！お前、翼が生えてるんじゃないか！？（タイム: {_resultTimer:F2}秒）", 5.5f);
            }
            else if (_currentResultRank.StartsWith("A"))
            {
                drone.SetSpeech($"ナイスゴール！Aランクだ！風に乗る感覚、最高だったろ！？（タイム: {_resultTimer:F2}秒）", 5.0f);
            }
            else
            {
                drone.SetSpeech($"無事着地！気持ちいい滑空だったね！[R]キーでスタートに戻って、次はもっと上を狙おう！（タイム: {_resultTimer:F2}秒）", 5.0f);
            }
        }
    }

    void OnGUI()
    {
        EnsureStyles();

        // 画面右上にいつでも即座にスタート台へ行けるボタン（クリックまたは [R] / [T] キー対応）
        float sW = Screen.width;
        float bW = 175f;
        float bH = 36f;
        Rect warpBtn = new Rect(sW - bW - 18f, 55f, bW, bH);
        GUI.color = new Color(0.12f, 0.45f, 0.85f, 0.85f);
        if (GUI.Button(warpBtn, "🪂 [R] スカイダイブ台"))
        {
            RetryAtStart();
        }
        GUI.color = Color.white;

        // 走行中のHUD
        if (isRunning)
        {
            // 上部中央タイマー
            float screenW = Screen.width;
            float panelW = 340f;
            float panelH = 75f;
            Rect panelRect = new Rect((screenW - panelW) * 0.5f, 15f, panelW, panelH);

            GUI.color = new Color(0.04f, 0.08f, 0.15f, 0.85f);
            GUI.DrawTexture(panelRect, _panelBg);
            GUI.color = Color.white;

            // タイム文字列
            int min = (int)(currentTimer / 60f);
            float sec = currentTimer % 60f;
            string timeStr = string.Format("{0:00}:{1:00.00}", min, sec);

            GUI.Label(new Rect(panelRect.x, panelRect.y + 4, panelW, 36), $"⏱  {timeStr}", _timeStyle);

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
            GUI.Label(new Rect(panelRect.x, panelRect.y + 40, panelW, 26), $"リング: {ringsPassedCount}/{total}  {_cachedRingDots}", _ringStyle);

            // リトライ案内
            GUI.Label(new Rect(panelRect.x, panelRect.y + panelH + 5, panelW, 22), "🔄 [R]キーでいつでもスタート台へ即時リトライ", _retryStyle);
        }

        // リザルト画面
        if (_showResult)
        {
            float screenW = Screen.width;
            float screenH = Screen.height;
            float resW = 460f;
            float resH = 260f;
            Rect resRect = new Rect((screenW - resW) * 0.5f, (screenH - resH) * 0.4f, resW, resH);

            GUI.color = new Color(0.02f, 0.06f, 0.14f, 0.94f);
            GUI.DrawTexture(resRect, _panelBg);
            GUI.color = Color.white;

            // 枠線アクセント
            GUI.color = new Color(0.2f, 0.9f, 1.0f, 0.8f);
            GUI.DrawTexture(new Rect(resRect.x, resRect.y, resW, 4), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(resRect.x, resRect.y + resH - 4, resW, 4), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string title = _isNewRecord ? "🏆 NEW RECORD! 達成！" : "🏁 FLIGHT FINISHED!";
            GUI.Label(new Rect(resRect.x, resRect.y + 14, resW, 36), title, _resultTitleStyle);

            int total = courseRings.Count > 0 ? courseRings.Count : 10;
            string details = $"タイム: {_resultTimer:F2} 秒\n" +
                             $"通過リング: {ringsPassedCount} / {total}\n" +
                             $"評価ランク: {_currentResultRank}\n" +
                             $"ベストタイム: {bestTime:F2} 秒 ({bestRank})";

            GUI.Label(new Rect(resRect.x + 30, resRect.y + 60, resW - 60, 130), details, _resultBodyStyle);

            // リトライボタン
            if (GUI.Button(new Rect(resRect.x + 80, resRect.y + 200, resW - 160, 42), "🪂 もう一度飛ぶ！ [R]キー"))
            {
                RetryAtStart();
            }
        }
    }

    void EnsureStyles()
    {
        if (_panelBg == null)
        {
            _panelBg = new Texture2D(1, 1);
            _panelBg.SetPixel(0, 0, Color.white);
            _panelBg.Apply();
        }

        if (_timeStyle == null)
        {
            _timeStyle = new GUIStyle();
            _timeStyle.fontSize = 24;
            _timeStyle.fontStyle = FontStyle.Bold;
            _timeStyle.alignment = TextAnchor.MiddleCenter;
            _timeStyle.normal.textColor = new Color(0.3f, 0.95f, 1.0f);
        }

        if (_ringStyle == null)
        {
            _ringStyle = new GUIStyle();
            _ringStyle.fontSize = 15;
            _ringStyle.alignment = TextAnchor.MiddleCenter;
            _ringStyle.normal.textColor = new Color(0.85f, 1.0f, 0.85f);
        }

        if (_retryStyle == null)
        {
            _retryStyle = new GUIStyle();
            _retryStyle.fontSize = 12;
            _retryStyle.alignment = TextAnchor.MiddleCenter;
            _retryStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f, 0.8f);
        }

        if (_resultTitleStyle == null)
        {
            _resultTitleStyle = new GUIStyle();
            _resultTitleStyle.fontSize = 24;
            _resultTitleStyle.fontStyle = FontStyle.Bold;
            _resultTitleStyle.alignment = TextAnchor.MiddleCenter;
            _resultTitleStyle.normal.textColor = new Color(1.0f, 0.9f, 0.2f);
        }

        if (_resultBodyStyle == null)
        {
            _resultBodyStyle = new GUIStyle();
            _resultBodyStyle.fontSize = 16;
            _resultBodyStyle.alignment = TextAnchor.UpperLeft;
            _resultBodyStyle.normal.textColor = Color.white;
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
            if (mgr != null && mgr.isRunning)
            {
                mgr.FinishTrial();
            }
        }
    }
}
