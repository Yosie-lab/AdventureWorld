using UnityEngine;
using System.Collections;

/// <summary>
/// 前半オープニングの小さなドラマ3幕：
/// ①Rust極寒・油切れ → ②注油で蘇生 → ③最初のギア／3個目でダッシュ祝福
/// </summary>
public class AdventurePrologueDrama : MonoBehaviour
{
    static AdventurePrologueDrama _instance;
    public static AdventurePrologueDrama Instance => _instance;

    enum Phase
    {
        Idle,
        Act1Distress,
        WaitOil,
        Act2Revived,
        GuideFirstGear,
        WaitFirstScrap,
        FirstGearDone,
        DashCelebrate,
        Complete
    }

    Phase _phase = Phase.Idle;
    bool _oilReceived;
    bool _showOilPrompt;
    bool _showDashBoard;
    bool _dashBoardAdvance;
    float _dashBoardOpenTime;

    public bool IsBlockingSpeech =>
        _phase == Phase.Act1Distress
        || _phase == Phase.WaitOil
        || _phase == Phase.Act2Revived
        || _phase == Phase.DashCelebrate;

    public bool IsWaitingForOil => _phase == Phase.WaitOil;

    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = Object.FindFirstObjectByType<AdventurePrologueDrama>();
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

    /// <summary>オープニングPlay直後／ニューゲーム時に呼ぶ</summary>
    public void BeginAfterOpening()
    {
        var scraps = AdventureScrapManager.Instance ?? Object.FindFirstObjectByType<AdventureScrapManager>();
        if (scraps != null && scraps.CollectedCount > 0)
        {
            _phase = Phase.Complete;
            return;
        }
        if (_phase != Phase.Idle && _phase != Phase.Complete)
            return;

        StopAllCoroutines();
        _oilReceived = false;
        _showOilPrompt = false;
        _showDashBoard = false;
        StartCoroutine(PrologueRoutine());
    }

    public void ResetForNewGame()
    {
        StopAllCoroutines();
        _phase = Phase.Idle;
        _oilReceived = false;
        _showOilPrompt = false;
        _showDashBoard = false;
        _dashBoardAdvance = false;
        var drone = AdventureRustDrone.Instance ?? Object.FindFirstObjectByType<AdventureRustDrone>();
        drone?.EndPrologueDistress();
    }

    /// <summary>Rustへの注油／手当て成功時</summary>
    public void NotifyOilApplied()
    {
        if (_phase == Phase.WaitOil)
            _oilReceived = true;
    }

    /// <summary>パーツ取得時（1個目・3個目の山場）</summary>
    public void NotifyScrapCollected(int count)
    {
        if (count == 1 && (_phase == Phase.GuideFirstGear || _phase == Phase.WaitFirstScrap || _phase == Phase.Act2Revived))
            StartCoroutine(FirstGearRebirthRoutine());
        else if (count == 3 && _phase != Phase.Complete && _phase != Phase.DashCelebrate)
            StartCoroutine(DashCelebrateRoutine());
    }

    IEnumerator PrologueRoutine()
    {
        _phase = Phase.Act1Distress;
        yield return null;

        var drone = AdventureRustDrone.Instance ?? Object.FindFirstObjectByType<AdventureRustDrone>();
        if (drone == null)
        {
            _phase = Phase.Complete;
            yield break;
        }

        drone.ClearSpeech();
        drone.StartPrologueDistress();
        drone.SpeakCustom("キキッ……Niko……塩水で古いギアが……凍りついて動かない……油を……さして……", 5.5f);
        yield return new WaitForSeconds(5.2f);

        _phase = Phase.WaitOil;
        _showOilPrompt = true;
        drone.SpeakCustom("……そばに来て……【E】で油をさして……お願い……", 6.0f);

        float timeout = 90f;
        while (!_oilReceived && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            // 近くでEを押したら確実に受理（Interact漏れ対策）
            PollOilInput(drone);
            yield return null;
        }

        _showOilPrompt = false;
        if (!_oilReceived)
        {
            // タイムアウト時は自動注油して物語を止めない
            drone.CompletePrologueOil();
            _oilReceived = true;
        }

        _phase = Phase.Act2Revived;
        drone.EndPrologueDistress();
        drone.CompletePrologueOil();
        drone.SpeakCustom("……あ……温かい……回路が戻ってきた……！ありがとう、Niko。生きてる実感がするよ……！", 5.8f);
        yield return new WaitForSeconds(5.5f);

        _phase = Phase.GuideFirstGear;
        drone.SpeakCustom("すぐ目の前——脱出艇の脇に、光るギアが落ちてる！あれが僕たちの翼の第一歩だよ！", 6.0f);
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

        var kb = UnityEngine.InputSystem.Keyboard.current;
        bool pressed = (kb != null && kb.eKey.wasPressedThisFrame) || player.InteractPressed;
        try { if (Input.GetKeyDown(KeyCode.E)) pressed = true; } catch { }
        if (!pressed) return;

        drone.CompletePrologueOil();
        NotifyOilApplied();
    }

    IEnumerator FirstGearRebirthRoutine()
    {
        _phase = Phase.FirstGearDone;
        var drone = AdventureRustDrone.Instance ?? Object.FindFirstObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            drone.CelebratePrologueFirstGear();
            // OnNikoFoundScrap の直後に上書きして山場を明確化
            yield return new WaitForSeconds(0.15f);
            drone.SpeakCustom("ピキーン……！ギアが噛み合った……！僕、また飛べそうな気がするよ、Niko！！", 6.0f);
        }
        yield return new WaitForSeconds(5.5f);
        if (drone != null)
            drone.SpeakCustom("あと2個集めればダッシュが戻る！砂浜を歩いて、光る柱を探そう！", 5.5f);
    }

    IEnumerator DashCelebrateRoutine()
    {
        _phase = Phase.DashCelebrate;
        _showDashBoard = true;
        _dashBoardAdvance = false;
        _dashBoardOpenTime = Time.unscaledTime;

        AdventureScrapHUD.Instance?.HideBannerImmediately();
        var drone = AdventureRustDrone.Instance ?? Object.FindFirstObjectByType<AdventureRustDrone>();
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
            drone.SpeakCustom("ピキーン！足が軽い……！【Shift】でダッシュして、草原へ駆け上がろう、Niko！！", 6.0f);

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
        if (_showOilPrompt && _phase == Phase.WaitOil)
            DrawOilPrompt();
        if (_showDashBoard && _phase == Phase.DashCelebrate)
            DrawDashBoard();
    }

    void DrawOilPrompt()
    {
        float w = Mathf.Min(720f, Screen.width * 0.88f);
        float h = 96f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height - 160f;

        GUI.color = new Color(0.02f, 0.05f, 0.10f, 0.82f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.75f, 0.35f, 0.95f);
        GUI.DrawTexture(new Rect(x, y, w, 3f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x, y + h - 3f, w, 3f), Texture2D.whiteTexture);

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        style.normal.textColor = new Color(1f, 0.92f, 0.55f, 1f);
        GUI.Label(new Rect(x, y, w, h), "【E】Rustに油をさして手当てする", style);
        GUI.color = Color.white;
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
            "指先が油で汚れ、歯車が噛み合う——\nそれが、生きている手応えだ。\n\n【ブースター修復】ダッシュが戻った！\n草原へ駆け上がろう。",
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
}
