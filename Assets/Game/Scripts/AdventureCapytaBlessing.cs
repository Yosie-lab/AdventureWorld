using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// カピタ（Capyta）に話しかけると、スーパージャンプと潤滑油を授ける。
/// RustAndFloat の配置名 "Capyta_*" と AdventureNpc(capyta) の両方に対応。
/// </summary>
public class AdventureCapytaBlessing : MonoBehaviour
{
    static AdventureCapytaBlessing _instance;
    public static AdventureCapytaBlessing Instance => _instance;

    public const float SuperJumpMultiplier = 1.55f;
    const float TalkRadius = 4.8f;
    const int OilGiftAmount = 1;
    const string PrefKey = "RustAndFloat_CapytaSuperJump";

    bool _promptVisible;
    float _lastTalkTime = -10f;

    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = Object.FindFirstObjectByType<AdventureCapytaBlessing>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }
        var go = new GameObject("AdventureCapytaBlessing");
        _instance = go.AddComponent<AdventureCapytaBlessing>();
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

    void Start()
    {
        if (PlayerPrefs.GetInt(PrefKey, 0) == 1)
            GrantSuperJump(silent: true);
    }

    public void ResetForNewGame()
    {
        PlayerPrefs.SetInt(PrefKey, 0);
        PlayerPrefs.Save();
        var player = AdventurePlayerController.Instance
                     ?? Object.FindFirstObjectByType<AdventurePlayerController>();
        if (player != null)
        {
            player.hasCapytaSuperJump = false;
            if (Mathf.Approximately(player.jumpMultiplier, SuperJumpMultiplier)
                || player.jumpMultiplier > 1.01f)
                player.jumpMultiplier = 1.0f;
        }
    }

    /// <summary>AdventureGameDirector のカピタ会話からも呼べる（スーパージャンプ＋油）</summary>
    public static void GrantSuperJumpFromTalk(bool showFx = true)
    {
        Ensure();
        if (_instance == null) return;
        _instance.GrantSuperJump(silent: !showFx);
        _instance.GrantOilFromCapyta(showSpeech: showFx);
    }

    void GrantSuperJump(bool silent)
    {
        var player = AdventurePlayerController.Instance
                     ?? Object.FindFirstObjectByType<AdventurePlayerController>();
        if (player == null) return;

        bool already = player.hasCapytaSuperJump;
        player.hasCapytaSuperJump = true;
        player.jumpMultiplier = SuperJumpMultiplier;
        PlayerPrefs.SetInt(PrefKey, 1);
        PlayerPrefs.Save();

        if (silent) return;

        var drone = AdventureRustDrone.Instance ?? Object.FindFirstObjectByType<AdventureRustDrone>();
        if (!already)
        {
            drone?.SpeakCustom("わぁ…！カピタの祝福だ！ジャンプがふわっと高く跳べるよ、Niko！！", 5.5f);
            AdventureScrapHUD.Instance?.ShowUpgradeBanner(
                "✦ カピタの祝福 ✦  【スーパージャンプ】獲得！（Spaceで高く跳べる）");
        }
        else
        {
            drone?.SpeakCustom("カピタ、また会えて嬉しいね！スーパージャンプ、まだ効いてるよ！", 4.5f);
        }
    }

    /// <summary>カピタ会話で潤滑油を1つ渡す</summary>
    void GrantOilFromCapyta(bool showSpeech = true)
    {
        var drone = AdventureRustDrone.Instance ?? Object.FindFirstObjectByType<AdventureRustDrone>();
        if (drone == null) return;

        drone.oilCount = Mathf.Max(0, drone.oilCount) + OilGiftAmount;
        if (!showSpeech) return;

        drone.SpeakCustom($"カピタが潤滑油をくれたよ！（所持: {drone.oilCount}）", 4.0f);
        AdventureScrapHUD.Instance?.ShowUpgradeBanner(
            $"✦ カピタの贈り物 ✦  潤滑油 +{OilGiftAmount}（所持: {drone.oilCount}）");
    }

    void Update()
    {
        var player = AdventurePlayerController.Instance;
        if (player == null)
        {
            _promptVisible = false;
            return;
        }

        var tower = AdventureSanctuaryTowerManager.Instance;
        if (tower != null && (tower.IsSkybreakModalActive || tower.IsClimaxOilPromptActive
            || (tower.IsPlayerNearLever && tower.IsLeverReadyToOpen)))
        {
            _promptVisible = false;
            return;
        }

        Transform nearest = FindNearestCapyta(player.transform.position, out float dist);
        if (nearest == null || dist > TalkRadius)
        {
            _promptVisible = false;
            return;
        }

        _promptVisible = true;

        bool ePressed = player.InteractPressed;
        var kb = Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame) ePressed = true;
        try { if (Input.GetKeyDown(KeyCode.E)) ePressed = true; } catch { }

        if (ePressed && Time.unscaledTime - _lastTalkTime > 0.6f)
        {
            _lastTalkTime = Time.unscaledTime;
            TalkToCapyta(player, nearest);
        }
    }

    void TalkToCapyta(AdventurePlayerController player, Transform capy)
    {
        TryPlayCapytaReaction(capy);

        var drone = AdventureRustDrone.Instance ?? Object.FindFirstObjectByType<AdventureRustDrone>();
        bool firstJump = !player.hasCapytaSuperJump;

        if (firstJump)
            GrantSuperJump(silent: true);

        // 会話のたび油を1つ渡す（スーパージャンプは初回のみ）
        if (drone != null)
            drone.oilCount = Mathf.Max(0, drone.oilCount) + OilGiftAmount;

        if (firstJump)
        {
            ShowSpeechBubble("ブヒヒ…！大地の弾力と、相棒のための潤滑油をわけてあげるね！");
            drone?.SpeakCustom(
                $"わぁ…！カピタの祝福だ！スーパージャンプと潤滑油をもらったよ！！（油: {drone.oilCount}）",
                5.5f);
            AdventureScrapHUD.Instance?.ShowUpgradeBanner(
                $"✦ カピタの祝福 ✦  スーパージャンプ＆潤滑油 +{OilGiftAmount}");
        }
        else
        {
            ShowSpeechBubble("ブヒ…また油を持っていって。Rustを大事にしてね。");
            drone?.SpeakCustom($"カピタが潤滑油をくれたよ！（所持: {drone.oilCount}）", 4.0f);
            AdventureScrapHUD.Instance?.ShowUpgradeBanner(
                $"✦ カピタの贈り物 ✦  潤滑油 +{OilGiftAmount}（所持: {drone.oilCount}）");
        }
    }

    static void TryPlayCapytaReaction(Transform capy)
    {
        if (capy == null) return;
        var anim = capy.GetComponentInChildren<Animator>();
        if (anim == null) return;
        if (HasState(anim, "CapytaDance"))
            anim.Play("CapytaDance", 0, 0f);
        else if (HasState(anim, "CapytaSittingIdleLooksRight"))
            anim.Play("CapytaSittingIdleLooksRight", 0, 0f);
    }

    static bool HasState(Animator anim, string stateName)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return false;
        for (int i = 0; i < anim.layerCount; i++)
        {
            if (anim.HasState(i, Animator.StringToHash(stateName)))
                return true;
        }
        return false;
    }

    static void ShowSpeechBubble(string text)
    {
        AdventureScrapHUD.Instance?.ShowUpgradeBanner($"カピタ「{text}」");
    }

    static Transform FindNearestCapyta(Vector3 playerPos, out float bestDist)
    {
        bestDist = float.MaxValue;
        Transform best = null;

        var npcs = Object.FindObjectsByType<AdventureNpc>(FindObjectsSortMode.None);
        for (int i = 0; i < npcs.Length; i++)
        {
            var n = npcs[i];
            if (n == null) continue;
            if (n.npcId != "capyta" && n.displayName != "カピタ")
                continue;
            float d = FlatDist(playerPos, n.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = n.transform;
            }
        }

        var all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            if (!t.name.StartsWith("Capyta")) continue;
            if (t.parent != null && t.parent.name.StartsWith("Capyta")) continue;
            float d = FlatDist(playerPos, t.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = t;
            }
        }

        return best;
    }

    static float FlatDist(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    void OnGUI()
    {
        if (!_promptVisible) return;

        float w = Mathf.Min(640f, Screen.width * 0.86f);
        float h = 64f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height - 150f;

        GUI.color = new Color(0.05f, 0.12f, 0.08f, 0.78f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = new Color(0.55f, 0.95f, 0.65f, 0.95f);
        GUI.DrawTexture(new Rect(x, y, w, 3f), Texture2D.whiteTexture);

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = new Color(0.9f, 1f, 0.85f, 1f);
        string tip = AdventurePlayerController.Instance != null && AdventurePlayerController.Instance.hasCapytaSuperJump
            ? "【E】カピタと話す（潤滑油をもらえる）"
            : "【E】カピタと話す（スーパージャンプ＆潤滑油）";
        GUI.Label(new Rect(x, y, w, h), tip, style);
        GUI.color = Color.white;
    }
}
