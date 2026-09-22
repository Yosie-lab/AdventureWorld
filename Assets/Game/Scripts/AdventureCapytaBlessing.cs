using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// カピタ（Capyta）に話しかけると、スーパージャンプと潤滑油を授ける。
/// 機嫌（その場の気分）で油の量が大きく変わる。
/// </summary>
public class AdventureCapytaBlessing : MonoBehaviour
{
    static AdventureCapytaBlessing _instance;
    public static AdventureCapytaBlessing Instance => _instance;

    /// <summary>カピタ会話プロンプト表示中（Eキーはカピタ優先）</summary>
    public static bool IsTalkPromptActive =>
        _instance != null && _instance._promptVisible;

    /// <summary>プレイヤーがカピタ会話レンジ内か（Rust手当てより優先判定用）</summary>
    public static bool IsPlayerNearTalkableCapyta(Vector3 playerPos)
    {
        Ensure();
        Transform nearest = FindNearestCapyta(playerPos, out float dist);
        // Rust手当て(5.5m)との取り合いを防ぐため、カピタ会話を最優先保護
        return nearest != null && dist <= TalkRadius + 0.8f;
    }

    public const float SuperJumpMultiplier = 1.55f;
    const float TalkRadius = 5.2f;
    const string PrefKey = "RustAndFloat_CapytaSuperJump";

    enum Mood { Calm, Happy, Generous, Jackpot }

    static readonly string[] CapytaByMood =
    {
        "ブヒ…今日はまあまあ。油、これくらいで我慢してね。",
        "ブヒヒ！機嫌がいいよ。潤滑油、多めにあげるね！",
        "プヒヒ……！今日は気前がいい日。缶をあけて、たっぷりの油を持っていって！",
        "ブヒッヒッヒ！！最高の気分だ！！油を山盛りにしてあげる！！Rustをぬるぬるにしてあげて！",
    };

    static readonly string[] CapytaFirst =
    {
        "ブヒヒ…！大地の弾力と、相棒のための潤滑油をわけてあげるね！機嫌次第でもっと出すよ！",
        "ブヒッ。Rustのために油をたっぷり。調子がいい日は、もっと山盛りにしてあげる！",
    };

    static readonly string[] RustByMood =
    {
        "ピロッ……油もらったよ。あとで整備しよう",
        "カピタ機嫌がいいね。油、ありがたい",
        "わぁ……油がたくさん。助かるよ、Niko",
        "ピキーッ……山盛りだ。これでしばらく安心だね",
    };

    static readonly string[] RustFirst =
    {
        "わぁ…！カピタの祝福だ！スーパージャンプと油をもらったよ、Niko！！",
        "ピキーッ！カピタ優しい…！ジャンプも油も…Niko、あとで撫でてね……？",
    };

    bool _promptVisible;
    float _lastTalkTime = -10f;
    int _talkIndex;

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
        SpawnBeachCapitasIfNeeded();
        AdventureCapytaBodyCollider.EnsureAllCapytasInScene();
    }

    static readonly Vector3[] BeachCapytaSpots =
    {
        new Vector3(148f, 0f, 248f), // スタート南方（Nikoスポーンから約28m）
        new Vector3(132f, 0f, 328f), // 西砂浜中央帯
        new Vector3(205f, 0f, 198f), // 南砂浜
    };

    /// <summary>砂浜にカピタを少しだけ配置（既に Beach 個体がいれば位置だけ補正）</summary>
    static void SpawnBeachCapitasIfNeeded()
    {
        var land = Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();
        Vector3 nikoSpawn = ResolveNikoSpawnXZ();

        var existingBeach = FindBeachCapitas();
        if (existingBeach.Count > 0)
        {
            // 過去の二重生成ぶんを掃除
            for (int i = BeachCapytaSpots.Length; i < existingBeach.Count; i++)
            {
                if (existingBeach[i] != null)
                    Object.Destroy(existingBeach[i].gameObject);
            }
            for (int i = 0; i < existingBeach.Count && i < BeachCapytaSpots.Length; i++)
                PlaceCapytaOnGround(existingBeach[i], BeachCapytaSpots[i], land);
            PushCapitasClearOfPoint(nikoSpawn, 10f, land);
            return;
        }

        GameObject prefab = null;
#if UNITY_EDITOR
        prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Niko&Capyta/Assets/Prefabs/Capyta.prefab");
#endif
        if (prefab == null) return;

        var root = new GameObject("Capyta_Beach_Root");
        for (int i = 0; i < BeachCapytaSpots.Length; i++)
        {
            Vector3 p = GroundAt(BeachCapytaSpots[i], land);
            var go = Object.Instantiate(prefab, p, Quaternion.Euler(0f, 40f + i * 70f, 0f), root.transform);
            go.name = "Capyta_Beach_" + i;
            go.transform.localScale = Vector3.one * (0.92f + i * 0.04f);
        }

        PushCapitasClearOfPoint(nikoSpawn, 10f, land);
    }

    static System.Collections.Generic.List<Transform> FindBeachCapitas()
    {
        var list = new System.Collections.Generic.List<Transform>(4);
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            string n = t.name;
            if (!n.StartsWith("Capyta_Beach_") || n.Contains("Root")) continue;
            list.Add(t);
        }
        return list;
    }

    static Vector3 ResolveNikoSpawnXZ()
    {
        var player = AdventurePlayerController.Instance
                     ?? Object.FindAnyObjectByType<AdventurePlayerController>();
        if (player != null)
        {
            if (player.spawnPosition != Vector3.zero)
                return new Vector3(player.spawnPosition.x, 0f, player.spawnPosition.z);
            return new Vector3(player.transform.position.x, 0f, player.transform.position.z);
        }
        return new Vector3(158f, 0f, 275f);
    }

    static Vector3 GroundAt(Vector3 xz, Terrain land)
    {
        Vector3 p = xz;
        if (land != null)
            p.y = land.SampleHeight(p) + land.transform.position.y;
        else
            p.y = 1f;
        return p;
    }

    static void PlaceCapytaOnGround(Transform capy, Vector3 xz, Terrain land)
    {
        if (capy == null) return;
        capy.position = GroundAt(xz, land);
    }

    /// <summary>Nikoスポーン付近にいるカピタを外側へ押し出す</summary>
    static void PushCapitasClearOfPoint(Vector3 centerXZ, float minDist, Terrain land)
    {
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (!IsCapytaInstanceRoot(t)) continue;

            Vector3 p = t.position;
            float dx = p.x - centerXZ.x;
            float dz = p.z - centerXZ.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            if (dist >= minDist) continue;

            Vector3 dir = dist > 0.05f
                ? new Vector3(dx, 0f, dz).normalized
                : new Vector3(-1f, 0f, -0.4f).normalized;
            Vector3 next = new Vector3(centerXZ.x, 0f, centerXZ.z) + dir * (minDist + 2f);
            t.position = GroundAt(next, land);
        }
    }

    /// <summary>
    /// 会話可能なカピタ本体か判定。
    /// Prefab内部の子メッシュや Capyta_Beach_Root などのコンテナは除外する。
    /// </summary>
    static bool IsCapytaInstanceRoot(Transform t)
    {
        if (t == null) return false;
        return AdventureCapytaBodyCollider.IsCapytaRoot(t);
    }

    public void ResetForNewGame()
    {
        PlayerPrefs.SetInt(PrefKey, 0);
        PlayerPrefs.Save();
        _talkIndex = 0;
        var player = AdventurePlayerController.Instance
                     ?? Object.FindAnyObjectByType<AdventurePlayerController>();
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
                     ?? Object.FindAnyObjectByType<AdventurePlayerController>();
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
            drone?.SpeakCustom(RustFirst[0], 5.5f);
            AdventureScrapHUD.Instance?.ShowUpgradeBanner(
                "✦ カピタの祝福 ✦  【スーパージャンプ】獲得！（Spaceで高く跳べる）");
        }
    }

    /// <summary>機嫌ロール：普段から多め。機嫌良し〜大盤振る舞いで山盛り。</summary>
    static void RollMood(out Mood mood, out int amount)
    {
        float r = Random.value;
        // 15% Calm / 35% Happy / 35% Generous / 15% Jackpot
        if (r < 0.15f)
        {
            mood = Mood.Calm;
            amount = Random.Range(5, 9);       // 5〜8
        }
        else if (r < 0.50f)
        {
            mood = Mood.Happy;
            amount = Random.Range(10, 16);     // 10〜15
        }
        else if (r < 0.85f)
        {
            mood = Mood.Generous;
            amount = Random.Range(18, 28);     // 18〜27
        }
        else
        {
            mood = Mood.Jackpot;
            amount = Random.Range(30, 49);     // 30〜48
        }
    }

    static string MoodLabel(Mood mood)
    {
        switch (mood)
        {
            case Mood.Calm: return "ふつうの機嫌";
            case Mood.Happy: return "ご機嫌";
            case Mood.Generous: return "気前よし";
            default: return "大盤振る舞い！！";
        }
    }

    /// <summary>カピタ会話で潤滑油を渡す（機嫌で量変動）</summary>
    void GrantOilFromCapyta(bool showSpeech = true)
    {
        var drone = AdventureRustDrone.Instance ?? Object.FindFirstObjectByType<AdventureRustDrone>();
        if (drone == null) return;

        RollMood(out Mood mood, out int amount);
        if (_talkIndex == 0)
            amount = Mathf.Max(amount, 12);

        drone.oilCount = Mathf.Max(0, drone.oilCount) + amount;

        if (!showSpeech)
        {
            _talkIndex++;
            return;
        }

        int mi = (int)mood;
        string combined =
            $"カピタ「{CapytaByMood[mi]}」\n" +
            $"Rust「{RustByMood[mi]}（油 +{amount}／所持: {drone.oilCount}）」";
        drone.SpeakAs($"🐾 カピタ（{MoodLabel(mood)}）", new Color(0.40f, 1f, 0.70f), combined, 5.0f);
        PlayGiftChime();
        _talkIndex++;
    }

    static void PlayGiftChime()
    {
        AdventureScrapManager.Ensure();
        AdventureScrapManager.Instance?.PlayCelebrationChime(0.22f);
    }

    private float _nextColliderCheckTime = 0f;

    void Update()
    {
        // 4秒おきに全カピタのコライダー存在を安全保証（新規生成カピタ対応）
        if (Time.unscaledTime >= _nextColliderCheckTime)
        {
            _nextColliderCheckTime = Time.unscaledTime + 4.0f;
            AdventureCapytaBodyCollider.EnsureAllCapytasInScene();
        }

        var player = AdventurePlayerController.Resolve();
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

        if (Time.unscaledTime < _talkCooldownUntil)
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

        if (ePressed && Time.unscaledTime - _lastTalkTime > 0.45f)
        {
            _lastTalkTime = Time.unscaledTime;
            _talkCooldownUntil = Time.unscaledTime + 5.0f; // 会話中はプロンプトを隠してボード重複を防止
            _promptVisible = false;
            TalkToCapyta(player, nearest);
        }
    }

    private float _talkCooldownUntil = 0f;

    void TalkToCapyta(AdventurePlayerController player, Transform capy)
    {
        TryPlayCapytaReaction(capy);

        var drone = AdventureRustDrone.Instance ?? Object.FindFirstObjectByType<AdventureRustDrone>();
        bool firstJump = !player.hasCapytaSuperJump;

        if (firstJump)
            GrantSuperJump(silent: true);

        RollMood(out Mood mood, out int amount);
        if (firstJump)
            amount = Mathf.Max(amount, 16);

        if (drone != null)
            drone.oilCount = Mathf.Max(0, drone.oilCount) + amount;

        int oilNow = drone != null ? drone.oilCount : amount;
        int mi = (int)mood;

        // ボードの多重重なりを完全解消：
        // カピタの言葉と相棒Rustの声を美しい1つのシネマダイアログに統合
        if (firstJump)
        {
            int ci = Random.Range(0, CapytaFirst.Length);
            int ri = Random.Range(0, RustFirst.Length);

            string combined =
                $"カピタ「{CapytaFirst[ci]}」\n" +
                $"Rust「{RustFirst[ri]}（油 +{amount}／所持: {oilNow}）」";

            drone?.SpeakAs("🐾 カピタの祝福 ✦ スーパージャンプ獲得！", new Color(0.40f, 1f, 0.70f), combined, 6.2f);
        }
        else
        {
            string combined =
                $"カピタ「{CapytaByMood[mi]}」\n" +
                $"Rust「{RustByMood[mi]}（油 +{amount}／所持: {oilNow}）」";

            drone?.SpeakAs($"🐾 カピタ（{MoodLabel(mood)}）", new Color(0.40f, 1f, 0.70f), combined, 5.0f);
        }

        PlayGiftChime();
        _talkIndex++;
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


    static Transform FindNearestCapyta(Vector3 playerPos, out float bestDist)
    {
        bestDist = float.MaxValue;
        Transform best = null;

        var npcs = Object.FindObjectsByType<AdventureNpc>(FindObjectsInactive.Exclude);
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

        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (!IsCapytaInstanceRoot(t)) continue;
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

        // カピタ会話プロンプト（視認性の高いエメラルドグリーンの美しいバナー）
        float scale = Mathf.Clamp(Screen.height / 720f, 1f, 1.35f);
        float w = Mathf.Min(460f * scale, Screen.width * 0.65f);
        float h = 38f * scale;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height - (185f * scale);
        float bar = 3f * scale;

        // 背景ボックス
        GUI.color = new Color(0.02f, 0.10f, 0.07f, 0.85f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        // 上部アクセントライン
        GUI.color = new Color(0.35f, 0.98f, 0.65f, 0.95f);
        GUI.DrawTexture(new Rect(x, y, w, bar), Texture2D.whiteTexture);

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(16f * scale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false
        };
        style.normal.textColor = new Color(0.45f, 1f, 0.75f, 1.0f);
        string tip = AdventurePlayerController.Instance != null && AdventurePlayerController.Instance.hasCapytaSuperJump
            ? "🐾 【E】カピタと話す"
            : "🐾 【E】カピタと話す（スーパージャンプ＆潤滑油）";
        GUI.Label(new Rect(x, y, w, h), tip, style);
        GUI.color = Color.white;
    }
}
