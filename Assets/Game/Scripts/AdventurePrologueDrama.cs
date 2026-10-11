using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 前半オープニングの小さなドラマ：
/// 遭難直後の波音とRustの目覚まし → Rust極寒・油切れ → Nikoのいたわりと注油 → 甘え寄り添い → 最初のギア／3個目でダッシュ祝福
/// </summary>
public class AdventurePrologueDrama : MonoBehaviour
{
    static AdventurePrologueDrama _instance;
    public static AdventurePrologueDrama Instance => _instance;

    enum Phase
    {
        Idle,
        Awakening,
        Act1Distress,
        WaitOil,
        Act2Revived,
        GuideFirstGear,
        WaitFirstScrap,
        FirstGearDone,
        SecondGearBond,
        DashCelebrate,
        Complete
    }

    Phase _phase = Phase.Idle;
    bool _oilReceived;
    bool _showDashBoard;
    bool _dashBoardAdvance;
    float _dashBoardOpenTime;
    bool _secondGearDone;
    bool _skipAwakening;
    bool _advanceSubtitle; // クリックやキー入力によるセリフ送り

    float _awakeningStartTime = 0f;
    const float WAKEUP_INPUT_GUARD_SEC = 0.35f; // 開始直後の余韻ガードを0.35秒に短縮し、すぐにキー入力を受け付ける

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _instance = null;
    }

    public bool IsAwakening => _phase == Phase.Awakening;
    public bool IsShowingDashBoard => _showDashBoard && _phase == Phase.DashCelebrate;

    /// <summary>キー入力などにより遭難目覚め演出を即座にスキップして起き上がる</summary>
    public void SkipAwakening()
    {
        if (_phase == Phase.Awakening)
        {
            _skipAwakening = true;
            _advanceSubtitle = true;
        }
    }

    public bool IsBlockingSpeech =>
        _phase == Phase.Awakening
        || _phase == Phase.Act1Distress
        || _phase == Phase.WaitOil
        || _phase == Phase.Act2Revived
        || _phase == Phase.FirstGearDone
        || _phase == Phase.SecondGearBond
        || _phase == Phase.DashCelebrate;

    public bool IsWaitingForOil => _phase == Phase.WaitOil;
    public bool IsPrologueActive => _phase != Phase.Complete && _phase != Phase.Idle;

    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = Object.FindAnyObjectByType<AdventurePrologueDrama>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }
        var go = new GameObject("AdventurePrologueDrama");
        _instance = go.AddComponent<AdventurePrologueDrama>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    void Update()
    {
        if (_phase == Phase.Awakening)
        {
            if (Time.unscaledTime - _awakeningStartTime >= WAKEUP_INPUT_GUARD_SEC)
            {
                if (CheckAdvanceInput())
                {
                    _advanceSubtitle = true;
                }
                if (CheckWakeupInput())
                {
                    SkipAwakening();
                }
            }
        }
    }

    /// <summary>セリフ送り入力（クリック・Space・Enter・Eキー）</summary>
    bool CheckAdvanceInput()
    {
        // 1. マウスクリック（左クリック）
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            return true;

        // 2. キーボード入力
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame ||
                kb.eKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame ||
                kb.aKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
                return true;
        }

        // 3. ゲームパッド
        var gp = UnityEngine.InputSystem.Gamepad.current;
        if (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame))
            return true;

        // 4. レガシーInputフォールバック
        try
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) ||
                Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D))
                return true;
        }
        catch { }

        return false;
    }

    /// <summary>即時スキップ操作（Escapeキーや右クリック）</summary>
    bool CheckWakeupInput()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            return true;

        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            return true;

        try
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                return true;
        }
        catch { }

        return false;
    }

    /// <summary>オープニングPlay直後／ニューゲーム時に呼ぶ</summary>
    public void BeginAfterOpening()
    {
        var scraps = AdventureScrapManager.Instance ?? Object.FindAnyObjectByType<AdventureScrapManager>();
        if (scraps != null && scraps.CollectedCount > 0)
        {
            _phase = Phase.Complete;
            return;
        }
        if (_phase != Phase.Idle && _phase != Phase.Complete)
            return;

        PlayAwakeningIntro(force: false);
    }

    /// <summary>遭難目覚ましイントロ演出を開始（force=trueで進行状況問わず強制再生）</summary>
    public void PlayAwakeningIntro(bool force = true)
    {
        StopAllCoroutines();
        _phase = Phase.Idle;
        _oilReceived = false;
        _showDashBoard = false;
        _secondGearDone = false;
        _skipAwakening = false;
        _awakeningStartTime = Time.unscaledTime;
        StartCoroutine(PrologueRoutine());
    }

    public void ResetForNewGame()
    {
        StopAllCoroutines();
        _phase = Phase.Idle;
        _oilReceived = false;
        _showDashBoard = false;
        _dashBoardAdvance = false;
        _secondGearDone = false;
        _skipAwakening = false;
        var drone = AdventureRustDrone.Instance ?? Object.FindAnyObjectByType<AdventureRustDrone>();
        drone?.EndPrologueDistress();

        var eyelidCanvas = GameObject.Find("AwakeningEyelidCanvas");
        if (eyelidCanvas != null) Destroy(eyelidCanvas);
        var camFollow = AdventureCameraFollow.InstanceOrFind();
        if (camFollow != null)
        {
            camFollow.enabled = true;
            camFollow.SnapBehindTarget();
        }
        AdventureMusicDirector.Instance?.SetSpotDucking(0f);
    }

    /// <summary>Rustへの注油／手当て成功時</summary>
    public void NotifyOilApplied()
    {
        if (_phase == Phase.WaitOil)
            _oilReceived = true;
    }

    /// <summary>パーツ取得時（1・2・3個目の山場）</summary>
    public void NotifyScrapCollected(int count)
    {
        if (count == 1 && (_phase == Phase.GuideFirstGear || _phase == Phase.WaitFirstScrap || _phase == Phase.Act2Revived))
            StartCoroutine(FirstGearRebirthRoutine());
        else if (count == 2 && !_secondGearDone && _phase != Phase.Complete && _phase != Phase.DashCelebrate)
            StartCoroutine(SecondGearBondRoutine());
        else if (count == 3 && _phase != Phase.Complete && _phase != Phase.DashCelebrate)
            StartCoroutine(DashCelebrateRoutine());
    }

    IEnumerator SpeakRust(AdventureRustDrone drone, string text, float hold)
    {
        if (drone == null) yield break;
        drone.SpeakCustom(text, hold);
        yield return new WaitForSeconds(hold);
    }

    IEnumerator SpeakNiko(AdventureRustDrone drone, string text, float hold)
    {
        if (drone == null) yield break;
        drone.SpeakAsNiko(text, hold);
        yield return new WaitForSeconds(hold);
    }

    IEnumerator PrologueRoutine()
    {
        // 遭難直後の波音とRustが心配そうに覗き込んで起こしに来る目覚めシークエンス
        yield return StartCoroutine(AwakeningSequence());

        _phase = Phase.Act1Distress;
        yield return null;

        var drone = AdventureRustDrone.Instance ?? Object.FindAnyObjectByType<AdventureRustDrone>();
        if (drone == null)
        {
            _phase = Phase.Complete;
            yield break;
        }

        drone.ClearSpeech();
        drone.StartPrologueDistress();

        // ① 極寒・しがみつき → Nikoのいたわり → Rustの甘えお願い
        yield return SpeakRust(drone,
            "キキッ……Float……塩水で古いギアが……凍りついて動かない……", 4.8f);
        yield return SpeakNiko(drone,
            "大丈夫だよ、Rust。ここにいるから。ぎゅっとしてていいよ", 4.6f);
        yield return SpeakRust(drone,
            "……うぅ……もっとそばにいたい……手が……あったかいの……", 4.8f);
        yield return SpeakNiko(drone,
            "よしよし。怖かったね。油をさして、ゆっくり温めてあげる", 4.6f);
        yield return SpeakRust(drone,
            "……お願い……【E】で油をさして……Floatの手、必要……", 5.2f);

        _phase = Phase.WaitOil;

        float timeout = 90f;
        float nextRemind = 22f;
        while (!_oilReceived && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            nextRemind -= Time.deltaTime;
            if (nextRemind <= 0f)
            {
                string[] reminds =
                {
                    "……うぅ……まだ冷たいよ……Floatの手、ほしい……【E】で……",
                    "……そばにいて……油を……さして……お願い……ぎゅっ……",
                    "ピピッ……か、硬い……Float……手当て……して……",
                };
                drone.SpeakCustom(reminds[Random.Range(0, reminds.Length)], 4.2f);
                nextRemind = 20f;
            }
            PollOilInput(drone);
            yield return null;
        }

        if (!_oilReceived)
        {
            drone.CompletePrologueOil();
            _oilReceived = true;
        }

        // ② 蘇生 → 胸元で甘え寄り添い → 最初のギアへ
        _phase = Phase.Act2Revived;
        drone.EndPrologueDistress();
        drone.CompletePrologueOil();
        drone.SetPettingState(true, 22f);

        yield return SpeakRust(drone,
            "……あ……温かい……回路が戻ってきた……！ありがとう、Float……", 5.2f);
        yield return SpeakNiko(drone,
            "よかった……また声が聞けて安心したよ。よしよし、いい子だね", 4.8f);
        yield return SpeakRust(drone,
            "えへへ……もうちょっと、胸のあたりにいたい……ピピッ……", 4.8f);
        yield return SpeakNiko(drone,
            "もちろん。ずっと一緒だよ。怖かったら、すぐくっついてていいからね", 4.8f);
        yield return SpeakRust(drone,
            "……うん。Floatの手の匂い、好き……もうギシギシしないよ", 4.6f);
        yield return SpeakNiko(drone,
            "さぁ、光るギアを取りに行こう。一歩ずつ、僕がそばにいるよ", 4.6f);

        drone.SetPettingState(false, 0f);

        _phase = Phase.GuideFirstGear;
        yield return SpeakRust(drone,
            "うん……！すぐ目の前——脱出艇の脇に、光るギアが落ちてるよ！", 5.5f);
        _phase = Phase.WaitFirstScrap;
    }

    void PollOilInput(AdventureRustDrone drone)
    {
        if (drone == null) return;
        var player = AdventurePlayerController.Instance;
        if (player == null) return;

        Vector3 diff = drone.transform.position - player.transform.position;
        float horizontal = new Vector2(diff.x, diff.z).magnitude;
        if (horizontal > 6.5f) return;

        bool pressed = AdventureInputReader.InteractDown || player.InteractPressed;
        if (!pressed) return;

        drone.CompletePrologueOil();
        NotifyOilApplied();
    }

    IEnumerator FirstGearRebirthRoutine()
    {
        _phase = Phase.FirstGearDone;
        var drone = AdventureRustDrone.Instance ?? Object.FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            drone.CelebratePrologueFirstGear();
            yield return new WaitForSeconds(0.15f);
            yield return SpeakRust(drone,
                "ピキーン……！ギアが噛み合った……！僕、また飛べそうな気がするよ！！", 5.2f);
            yield return SpeakNiko(drone,
                "すごいよ、Rust。一歩ずつ、ちゃんと戻ってきてるね", 4.4f);
            yield return SpeakRust(drone,
                "……Floatがいてくれるから、怖くないよ。もっとくっついててもいい……？", 5.0f);
            yield return SpeakNiko(drone,
                "いいよ。甘えてて。あと2個集めよう——砂浜の光る柱を探そう", 4.8f);
        }
        else
        {
            yield return new WaitForSeconds(1f);
        }
    }

    IEnumerator SecondGearBondRoutine()
    {
        _secondGearDone = true;
        _phase = Phase.SecondGearBond;
        var drone = AdventureRustDrone.Instance ?? Object.FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            yield return new WaitForSeconds(0.2f);
            yield return SpeakRust(drone,
                "ピロッ……もうひとつ繋がったよ。胸の奥が、すこし暖かい……", 4.8f);
            yield return SpeakNiko(drone,
                "調子はどう？無理してたら、すぐ撫でてあげるからね", 4.4f);
            yield return SpeakRust(drone,
                "えへへ……今は大丈夫。でも、撫でられるの好き……あと1個だよ！", 5.0f);
        }
        if (_phase == Phase.SecondGearBond)
            _phase = Phase.WaitFirstScrap;
    }

    IEnumerator DashCelebrateRoutine()
    {
        // 漂着カプセル等の情報ボードが開いている場合は閉じてからキーストーンボードを提示（画面重なり完全防止）
        if (AdventureBeachDriftBox.IsModalOpen)
        {
            AdventureBeachDriftBox.CloseModal();
            yield return null;
        }

        _phase = Phase.DashCelebrate;
        _showDashBoard = true;
        _dashBoardAdvance = false;
        _dashBoardOpenTime = Time.unscaledTime;

        AdventureScrapHUD.Instance?.HideBannerImmediately();
        var drone = AdventureRustDrone.Instance ?? Object.FindAnyObjectByType<AdventureRustDrone>();
        drone?.ClearSpeech();
        drone?.CelebratePrologueDashUnlock();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        while (!_dashBoardAdvance)
        {
            PollDashBoardAdvance();
            yield return null;
        }

        _showDashBoard = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (drone != null)
        {
            yield return SpeakRust(drone,
                "ピキーン！足が軽い……！ダッシュが戻ったよ、Float！！", 4.8f);
            yield return SpeakNiko(drone,
                "やったね！一緒に草原へ駆け上がろう。手、離さないよ", 4.6f);
            yield return SpeakRust(drone,
                "うん……！【Shift】で走って……僕、後ろでくっついてるからね！", 5.2f);
        }

        _phase = Phase.Complete;
    }

    void PollDashBoardAdvance()
    {
        if (Time.unscaledTime - _dashBoardOpenTime < 0.45f) return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
            _dashBoardAdvance = true;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            _dashBoardAdvance = true;
        try
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
                _dashBoardAdvance = true;
        }
        catch { }
    }

    void OnGUI()
    {
        // 油プロンプトボードは出さない（セリフ案内のみ）
        if (_showDashBoard && _phase == Phase.DashCelebrate)
            DrawDashBoard();
    }

    void DrawOilPrompt()
    {
        // 削除済み：【E】Rustに油を… の画面ボードは出さない
    }

    void DrawDashBoard()
    {
        GUI.color = new Color(0.01f, 0.02f, 0.05f, 0.55f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        float w = Mathf.Min(980f, Screen.width * 0.9f);
        float h = Mathf.Min(420f, Screen.height * 0.55f);
        float x = (Screen.width - w) * 0.5f;
        float y = (Screen.height - h) * 0.42f;

        GUI.color = new Color(0.02f, 0.06f, 0.12f, 0.94f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.85f, 0.4f, 0.95f);
        GUI.DrawTexture(new Rect(x, y, w, 4f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x, y + h - 4f, w, 4f), Texture2D.whiteTexture);

        var title = new GUIStyle(GUI.skin.label)
        {
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        title.normal.textColor = new Color(1f, 0.9f, 0.45f, 1f);
        GUI.Label(new Rect(x + 24f, y + 28f, w - 48f, 56f), "✦ キーストーン I：手動の自由 ✦", title);

        var body = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        body.normal.textColor = new Color(0.92f, 0.96f, 1f, 1f);
        GUI.Label(new Rect(x + 36f, y + 100f, w - 72f, 180f),
            "指先が油で汚れ、歯車が噛み合う——\nそれが、生きている手応えだ。\n\n【ブースター修復】ダッシュが戻った！\nRustと手をつないで、草原へ駆け上がろう。",
            body);

        var hint = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        hint.normal.textColor = new Color(0.45f, 0.95f, 0.9f, 1f);
        GUI.Label(new Rect(x, y + h - 70f, w, 40f), "【Space / クリック】で続ける", hint);
        GUI.color = Color.white;
    }

    #region Awakening Sequence (遭難直後の波音とRustの目覚まし)
    IEnumerator AwakeningSequence()
    {
        _phase = Phase.Awakening;

        var player = AdventurePlayerController.Resolve();
        var drone = AdventureRustDrone.Instance ?? Object.FindAnyObjectByType<AdventureRustDrone>();
        var camFollow = AdventureCameraFollow.InstanceOrFind();
        Camera mainCam = Camera.main;

        // BGMをダッキングして静かな波音を際立たせる
        AdventureMusicDirector.Ensure();
        var music = AdventureMusicDirector.Instance;
        if (music != null)
            music.SetSpotDucking(1.0f);

        // 波音AudioSourceを動的生成して再生
        AudioSource waveSource = gameObject.AddComponent<AudioSource>();
        waveSource.loop = true;
        waveSource.spatialBlend = 0f; // 2Dステレオ
        waveSource.volume = 0f;
        waveSource.clip = LoadOrMakeWaveClip();
        waveSource.Play();

        // まぶたUIと字幕Canvasの生成
        RectTransform upperEyelid, lowerEyelid;
        Text subtitleText;
        CanvasGroup subtitleCg;
        GameObject eyelidCanvasGo = CreateAwakeningUI(out upperEyelid, out lowerEyelid, out subtitleText, out subtitleCg);
        SetEyelidsOpen(upperEyelid, lowerEyelid, 0f); // 初期は完全閉眼（暗転）

        // カメラ制御の一時乗っ取り（Nikoの仰向け視点）
        if (camFollow != null)
            camFollow.enabled = false;

        Transform playerTransform = player != null ? player.transform : transform;
        Vector3 playerPos = playerTransform.position;
        Vector3 playerForward = playerTransform.forward;

        // 仰向け視点：地面すれすれから空を見上げるアングル
        Vector3 supineCamPos = playerPos + Vector3.up * 0.35f + playerForward * 0.15f;
        Quaternion supineCamRot = Quaternion.Euler(-75f, playerTransform.eulerAngles.y, 0f);

        if (mainCam != null)
        {
            mainCam.transform.position = supineCamPos;
            mainCam.transform.rotation = supineCamRot;
        }

        // RustドローンをNikoの顔の上で心配そうに見下ろす姿勢に配置
        if (drone != null)
        {
            drone.ClearSpeech();
            Vector3 rustHoverPos = playerPos + Vector3.up * 0.88f + playerForward * 0.20f;
            Quaternion rustTiltRot = Quaternion.Euler(68f, playerTransform.eulerAngles.y + 180f, 15f);
            drone.transform.position = rustHoverPos;
            drone.transform.rotation = rustTiltRot;
        }

        // 即座にスキップして起き上がるクリーンアップ関数
        void QuickWakeupCleanup()
        {
            if (eyelidCanvasGo != null)
                Destroy(eyelidCanvasGo);
            if (waveSource != null)
            {
                waveSource.Stop();
                Destroy(waveSource);
            }
            if (music != null)
                music.SetSpotDucking(0f);
            if (camFollow != null)
            {
                camFollow.enabled = true;
                camFollow.SnapBehindTarget();
            }
            if (drone != null)
            {
                Vector3 rustGoalPos = playerPos + playerForward * 1.2f + Vector3.up * 1.2f;
                drone.transform.position = rustGoalPos;
                drone.transform.rotation = Quaternion.LookRotation(playerPos + Vector3.up * 1.2f - rustGoalPos);
            }
        }

        IEnumerator WaitOrSkip(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && !_skipAwakening && !_advanceSubtitle)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _advanceSubtitle = false;
        }

        if (_skipAwakening) { QuickWakeupCleanup(); yield break; }

        // --- シーン1: 暗闇の中で波の音と遠い意識 ---
        float fadeT = 0f;
        while (fadeT < 1.2f && !_skipAwakening && !_advanceSubtitle)
        {
            fadeT += Time.unscaledDeltaTime;
            if (waveSource != null)
                waveSource.volume = Mathf.Lerp(0f, 0.72f, fadeT / 1.2f);
            yield return null;
        }
        if (_skipAwakening) { QuickWakeupCleanup(); yield break; }
        _advanceSubtitle = false;

        yield return WaitOrSkip(0.3f);
        if (_skipAwakening) { QuickWakeupCleanup(); yield break; }
        yield return ShowSubtitle(subtitleText, subtitleCg, "……ザザァ……ザザァ……", 1.6f);
        if (_skipAwakening) { QuickWakeupCleanup(); yield break; }
        yield return WaitOrSkip(0.3f);
        if (_skipAwakening) { QuickWakeupCleanup(); yield break; }
        yield return ShowSubtitle(subtitleText, subtitleCg, "……遠くで、波の音が聴こえる。", 1.8f);
        if (_skipAwakening) { QuickWakeupCleanup(); yield break; }
        yield return WaitOrSkip(0.4f);
        if (_skipAwakening) { QuickWakeupCleanup(); yield break; }

        // Rustの遠い呼びかけ
        if (drone != null)
            drone.SpeakCustom("……Float？　……Float……？", 2.5f);
        yield return ShowSubtitle(subtitleText, subtitleCg, "Rust 「……Float？　……Float……？」", 1.8f);
        if (_skipAwakening) { QuickWakeupCleanup(); yield break; }
        yield return WaitOrSkip(0.3f);
        if (_skipAwakening) { QuickWakeupCleanup(); yield break; }

        // --- シーン2: 薄目を開けるが力尽きてまた閉じる ---
        float eyeT = 0f;
        while (eyeT < 0.8f && !_skipAwakening && !_advanceSubtitle)
        {
            eyeT += Time.unscaledDeltaTime;
            float factor = Mathf.SmoothStep(0f, 0.35f, eyeT / 0.8f);
            SetEyelidsOpen(upperEyelid, lowerEyelid, factor);
            yield return null;
        }
        if (_skipAwakening) { QuickWakeupCleanup(); yield break; }
        _advanceSubtitle = false;

        yield return WaitOrSkip(0.5f);
        if (_skipAwakening) { QuickWakeupCleanup(); yield break; }

        // 再び意識が途切れ、まぶたが閉じる
        eyeT = 0f;
        while (eyeT < 0.6f && !_skipAwakening && !_advanceSubtitle)
        {
            eyeT += Time.unscaledDeltaTime;
            float factor = Mathf.SmoothStep(0.35f, 0f, eyeT / 0.6f);
            SetEyelidsOpen(upperEyelid, lowerEyelid, factor);
            yield return null;
        }
        if (_skipAwakening) { QuickWakeupCleanup(); yield break; }
        _advanceSubtitle = false;

        yield return WaitOrSkip(0.3f);
        if (_skipAwakening) { QuickWakeupCleanup(); yield break; }

        // Rustがさらに顔に近づき必死に呼びかける
        if (drone != null)
        {
            Vector3 rustCloserPos = playerPos + Vector3.up * 0.65f + playerForward * 0.18f;
            drone.transform.position = rustCloserPos;
            drone.SpeakCustom("Float……！　目を覚まして、Float……！！", 2.8f);
        }
        yield return ShowSubtitle(subtitleText, subtitleCg, "Rust 「Float……！　目を覚まして、Float……！！」", 2.0f);
        if (_skipAwakening) { QuickWakeupCleanup(); yield break; }
        yield return WaitOrSkip(0.3f);
        if (_skipAwakening) { QuickWakeupCleanup(); yield break; }

        // --- シーン3: 完全開眼と起き上がりカメラワーク ---
        Vector3 targetCamPos = playerPos + Vector3.up * 1.55f - playerForward * 3.6f;
        Quaternion targetCamRot = Quaternion.Euler(6f, playerTransform.eulerAngles.y, 0f);

        float riseT = 0f;
        float riseDuration = 1.8f;
        while (riseT < riseDuration && !_skipAwakening)
        {
            riseT += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(riseT / riseDuration);
            float smoothU = Mathf.SmoothStep(0f, 1f, u);

            // まぶた全開へ
            SetEyelidsOpen(upperEyelid, lowerEyelid, smoothU);

            // カメラの起き上がりドリー＆チルト
            if (mainCam != null)
            {
                mainCam.transform.position = Vector3.Lerp(supineCamPos, targetCamPos, smoothU);
                mainCam.transform.rotation = Quaternion.Slerp(supineCamRot, targetCamRot, smoothU);
            }

            // Rustの姿勢も通常ホバリングへ戻す
            if (drone != null)
            {
                Vector3 rustGoalPos = playerPos + playerForward * 1.2f + Vector3.up * 1.2f;
                drone.transform.position = Vector3.Lerp(drone.transform.position, rustGoalPos, Time.unscaledDeltaTime * 4f);
                drone.transform.rotation = Quaternion.Slerp(drone.transform.rotation, Quaternion.LookRotation(playerPos + Vector3.up * 1.2f - drone.transform.position), Time.unscaledDeltaTime * 5f);
            }

            yield return null;
        }
        if (_skipAwakening) { QuickWakeupCleanup(); yield break; }

        // まぶたUIと字幕Canvasの破棄
        if (eyelidCanvasGo != null)
            Destroy(eyelidCanvasGo);

        // --- シーン4: Rustの歓喜宙返りと安堵 ---
        if (drone != null)
        {
            drone.TriggerCelebration("ピピッ！……よかったぁぁ！！気がついた……！", 2.2f);
        }
        yield return WaitOrSkip(2.4f);
        if (_skipAwakening) { QuickWakeupCleanup(); yield break; }

        if (drone != null)
        {
            yield return SpeakRust(drone, "脱出ポッドが海に落ちて……ボクたち、この島に打ち上げられたんだ！", 4.5f);
        }
        if (_skipAwakening) { QuickWakeupCleanup(); yield break; }

        // BGMフェードイン＆波音フェードアウト
        StartCoroutine(FadeInBgmAndFadeOutWave(waveSource));

        // カメラ制御復帰
        if (camFollow != null)
        {
            camFollow.enabled = true;
            camFollow.SnapBehindTarget();
        }

        yield return WaitOrSkip(1.0f);
    }

    GameObject CreateAwakeningUI(out RectTransform upperEyelid, out RectTransform lowerEyelid, out Text subtitleText, out CanvasGroup subtitleCg)
    {
        var canvasGo = new GameObject("AwakeningEyelidCanvas");
        DontDestroyOnLoad(canvasGo);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 700;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        // 上まぶた
        var upperGo = new GameObject("UpperEyelid");
        upperGo.transform.SetParent(canvasGo.transform, false);
        var upperImg = upperGo.AddComponent<Image>();
        upperImg.color = Color.black;
        upperEyelid = upperGo.GetComponent<RectTransform>();
        upperEyelid.anchorMin = new Vector2(0f, 0.5f);
        upperEyelid.anchorMax = new Vector2(1f, 1f);
        upperEyelid.pivot = new Vector2(0.5f, 1f);
        upperEyelid.offsetMin = Vector2.zero;
        upperEyelid.offsetMax = Vector2.zero;

        // 下まぶた
        var lowerGo = new GameObject("LowerEyelid");
        lowerGo.transform.SetParent(canvasGo.transform, false);
        var lowerImg = lowerGo.AddComponent<Image>();
        lowerImg.color = Color.black;
        lowerEyelid = lowerGo.GetComponent<RectTransform>();
        lowerEyelid.anchorMin = new Vector2(0f, 0f);
        lowerEyelid.anchorMax = new Vector2(1f, 0.5f);
        lowerEyelid.pivot = new Vector2(0.5f, 0f);
        lowerEyelid.offsetMin = Vector2.zero;
        lowerEyelid.offsetMax = Vector2.zero;

        // 字幕コンテナ
        var subGo = new GameObject("AwakeningSubtitle");
        subGo.transform.SetParent(canvasGo.transform, false);
        var subRt = subGo.AddComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0.1f, 0.12f);
        subRt.anchorMax = new Vector2(0.9f, 0.28f);
        subRt.offsetMin = Vector2.zero;
        subRt.offsetMax = Vector2.zero;
        subtitleCg = subGo.AddComponent<CanvasGroup>();
        subtitleCg.alpha = 0f;

        // 字幕テキスト
        subtitleText = subGo.AddComponent<Text>();
        subtitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        subtitleText.fontSize = 32;
        subtitleText.alignment = TextAnchor.MiddleCenter;
        subtitleText.color = new Color(0.95f, 0.98f, 1.0f, 1.0f);

        var outline = subGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        // スキップ案内テキスト
        var hintGo = new GameObject("AwakeningSkipHint");
        hintGo.transform.SetParent(canvasGo.transform, false);
        var hintRt = hintGo.AddComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0.15f, 0.03f);
        hintRt.anchorMax = new Vector2(0.85f, 0.08f);
        hintRt.offsetMin = Vector2.zero;
        hintRt.offsetMax = Vector2.zero;
        var hintText = hintGo.AddComponent<Text>();
        hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        hintText.fontSize = 24;
        hintText.alignment = TextAnchor.MiddleCenter;
        hintText.color = new Color(1f, 0.92f, 0.65f, 0.95f);
        hintText.text = "【クリック / Space】で進む　【Escape】でスキップ";
        var hintOutline = hintGo.AddComponent<Outline>();
        hintOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        hintOutline.effectDistance = new Vector2(1.5f, -1.5f);

        return canvasGo;
    }

    void SetEyelidsOpen(RectTransform upper, RectTransform lower, float openFactor)
    {
        if (upper == null || lower == null) return;
        float halfScreen = 540f;
        float offset = halfScreen * Mathf.Clamp01(openFactor);
        upper.anchoredPosition = new Vector2(0f, offset);
        lower.anchoredPosition = new Vector2(0f, -offset);
    }

    IEnumerator ShowSubtitle(Text text, CanvasGroup cg, string message, float duration)
    {
        if (text == null || cg == null) yield break;
        text.text = message;

        // フェードイン
        float t = 0f;
        while (t < 0.20f && !_skipAwakening && !_advanceSubtitle)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(0f, 1f, t / 0.20f);
            yield return null;
        }
        if (_skipAwakening) yield break;
        cg.alpha = 1f;

        // 表示待機（クリックやキー入力があれば即時完了して次へ）
        float waitElapsed = 0f;
        while (waitElapsed < duration && !_skipAwakening && !_advanceSubtitle)
        {
            waitElapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        _advanceSubtitle = false;
        if (_skipAwakening) yield break;

        // フェードアウト
        t = 0f;
        while (t < 0.20f && !_skipAwakening)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(1f, 0f, t / 0.20f);
            yield return null;
        }
        cg.alpha = 0f;
    }

    IEnumerator FadeInBgmAndFadeOutWave(AudioSource waveSource)
    {
        var music = AdventureMusicDirector.Instance;
        float t = 0f;
        float duration = 3.0f;
        float startWaveVol = waveSource != null ? waveSource.volume : 0.72f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float factor = Mathf.Clamp01(t / duration);

            if (music != null)
                music.SetSpotDucking(Mathf.Lerp(1f, 0f, factor));

            if (waveSource != null)
                waveSource.volume = Mathf.Lerp(startWaveVol, 0.18f, factor);

            yield return null;
        }

        if (music != null)
            music.SetSpotDucking(0f);

        yield return new WaitForSeconds(12f);
        if (waveSource != null)
        {
            t = 0f;
            float cur = waveSource.volume;
            while (t < 3f && waveSource != null)
            {
                t += Time.deltaTime;
                waveSource.volume = Mathf.Lerp(cur, 0f, t / 3f);
                yield return null;
            }
            if (waveSource != null)
                Destroy(waveSource);
        }
    }

    static AudioClip LoadOrMakeWaveClip()
    {
        AudioClip clip = null;
#if UNITY_EDITOR
        clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RustAndFloat/Audio/Ambience/ocean_waves_grand.wav");
#endif
        if (clip == null)
            clip = Resources.Load<AudioClip>("ocean_waves_grand");
        if (clip == null)
            clip = MakeGentleShoreWavesClip();
        return clip;
    }

    static AudioClip MakeGentleShoreWavesClip()
    {
        int sampleRate = 44100;
        float lengthSec = 7.5f;
        int samples = (int)(sampleRate * lengthSec);
        float[] data = new float[samples];

        float b0 = 0f, b1 = 0f, b2 = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float white = Random.Range(-1f, 1f);
            b0 = 0.99765f * b0 + white * 0.0990460f;
            b1 = 0.96300f * b1 + white * 0.2965164f;
            b2 = 0.57000f * b2 + white * 1.0526913f;
            float pink = (b0 + b1 + b2 + white * 0.1848f) * 0.09f;

            float cycle = (t % 3.75f) / 3.75f;
            float waveEnv = Mathf.Sin(cycle * Mathf.PI);
            waveEnv = Mathf.Pow(waveEnv, 2.0f);

            data[i] = Mathf.Clamp(pink * (0.15f + waveEnv * 0.85f), -1f, 1f);
        }

        var clip = AudioClip.Create("ProceduralShoreWaves", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
    #endregion
}
