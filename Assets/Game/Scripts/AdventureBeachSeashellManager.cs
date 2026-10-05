using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 白砂ビーチの貝殻・シーグラス・琥珀の配置と収集トースト表示を統括するマネージャー。
/// 座礁脱出艇周辺から南北に広がる波打ち際に可憐なアイテムを散りばめ、
/// 拾った瞬間に小気味よい収集トーストを表示する。
/// </summary>
public class AdventureBeachSeashellManager : MonoBehaviour
{
    public static AdventureBeachSeashellManager Instance { get; private set; }

    const string PrefKeyTotalShells = "Seashell_TotalCollected";

    // 収集トースト用 uGUI
    Canvas _canvas;
    CanvasGroup _toastCg;
    Text _toastText;
    float _toastTimer = 0f;

    // 採取音＆Rust反応音用 2D常駐オーディオ
    AudioSource _seAudioSource;
    AudioClip _chimeClip;
    AudioClip _rustPipiClip;

    readonly List<AdventureBeachSeashellItem> _items = new List<AdventureBeachSeashellItem>();

    public static event System.Action OnInventoryChanged;

    public int TotalCollectedCount
    {
        get => PlayerPrefs.GetInt(PrefKeyTotalShells, 0);
        private set
        {
            PlayerPrefs.SetInt(PrefKeyTotalShells, value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>指定した種類の貝殻の現在所持ストック数を取得</summary>
    public int GetShellCount(AdventureBeachSeashellItem.ShellKind kind)
    {
        return PlayerPrefs.GetInt("Seashell_Stock_" + kind.ToString(), 0);
    }

    /// <summary>貝殻を指定数ストックに追加</summary>
    public void AddShell(AdventureBeachSeashellItem.ShellKind kind, int amount = 1)
    {
        int cur = GetShellCount(kind);
        PlayerPrefs.SetInt("Seashell_Stock_" + kind.ToString(), cur + amount);
        PlayerPrefs.Save();
        OnInventoryChanged?.Invoke();
    }

    /// <summary>クラフト等で貝殻を消費（足りていれば消費してtrue）</summary>
    public bool ConsumeShell(AdventureBeachSeashellItem.ShellKind kind, int amount)
    {
        int cur = GetShellCount(kind);
        if (cur < amount) return false;
        PlayerPrefs.SetInt("Seashell_Stock_" + kind.ToString(), cur - amount);
        PlayerPrefs.Save();
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>所持している全種類の貝殻・シーグラスの合計ストック数</summary>
    public int GetTotalStockCount()
    {
        int sum = 0;
        foreach (AdventureBeachSeashellItem.ShellKind k in System.Enum.GetValues(typeof(AdventureBeachSeashellItem.ShellKind)))
        {
            sum += GetShellCount(k);
        }
        return sum;
    }

    /// <summary>所持している貝殻の中から1つ取り出して消費する（カピタとの物々交換用）</summary>
    public bool TryConsumeAnyShell(out AdventureBeachSeashellItem.ShellKind consumedKind)
    {
        foreach (AdventureBeachSeashellItem.ShellKind k in System.Enum.GetValues(typeof(AdventureBeachSeashellItem.ShellKind)))
        {
            if (ConsumeShell(k, 1))
            {
                consumedKind = k;
                return true;
            }
        }
        consumedKind = AdventureBeachSeashellItem.ShellKind.Sakuragai;
        return false;
    }

    /// <summary>指定地点から最も近い未収集の貝殻・シーグラスを取得する（Rustのお宝レーダー連携用）</summary>
    public AdventureBeachSeashellItem GetNearestUncollectedShell(Vector3 playerPos, out float minDistance)
    {
        minDistance = float.MaxValue;
        AdventureBeachSeashellItem nearest = null;
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item == null || item.IsCollected || !item.gameObject.activeInHierarchy) continue;
            float d = Vector3.Distance(playerPos, item.transform.position);
            if (d < minDistance)
            {
                minDistance = d;
                nearest = item;
            }
        }
        return nearest;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        Ensure();
    }

    public static void Ensure()
    {
        if (Instance != null) return;
        var existing = Object.FindAnyObjectByType<AdventureBeachSeashellManager>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject("AdventureBeachSeashellManager");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AdventureBeachSeashellManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        SetupAudio();
        CreateToastUI();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        CheckAndMigrateLegacyStock();
        SpawnBeachSeashells();
    }

    void CheckAndMigrateLegacyStock()
    {
        // 過去に拾われたアイテムID（shell_01〜48）の実態をスキャン
        int collectedCount = 0;
        int[] expectedStockPerKind = new int[5];
        for (int i = 0; i < 48; i++)
        {
            string key = $"Seashell_Collected_shell_{i + 1:D2}";
            if (PlayerPrefs.GetInt(key, 0) == 1)
            {
                collectedCount++;
                int kindIndex = i % 5;
                expectedStockPerKind[kindIndex]++;
            }
        }

        // 現在保存されている各素材のストック数を取得
        int stockSakura = GetShellCount(AdventureBeachSeashellItem.ShellKind.Sakuragai);
        int stockEmerald = GetShellCount(AdventureBeachSeashellItem.ShellKind.SeaGlassEmerald);
        int stockSapphire = GetShellCount(AdventureBeachSeashellItem.ShellKind.SeaGlassSapphire);
        int stockAmber = GetShellCount(AdventureBeachSeashellItem.ShellKind.AmberPebble);
        int stockConch = GetShellCount(AdventureBeachSeashellItem.ShellKind.SpiralShell);
        int totalStock = stockSakura + stockEmerald + stockSapphire + stockAmber + stockConch;

        // 過去のバグにより「サクラガイ以外が0個」だが実際に他のアイテムが拾われていた、
        // または過去の総合カウントと個別ストックに乖離がある場合の自動修復
        bool needRebalance = (stockEmerald == 0 && stockSapphire == 0 && stockAmber == 0 && stockConch == 0 && (stockSakura > 0 || collectedCount > 0));

        if (needRebalance)
        {
            if (collectedCount > 0)
            {
                // 実際に拾ったshell_XXの本来の種別に合わせて正確に復元
                PlayerPrefs.SetInt("Seashell_Stock_" + AdventureBeachSeashellItem.ShellKind.Sakuragai.ToString(), expectedStockPerKind[0]);
                PlayerPrefs.SetInt("Seashell_Stock_" + AdventureBeachSeashellItem.ShellKind.SeaGlassEmerald.ToString(), expectedStockPerKind[1]);
                PlayerPrefs.SetInt("Seashell_Stock_" + AdventureBeachSeashellItem.ShellKind.SeaGlassSapphire.ToString(), expectedStockPerKind[2]);
                PlayerPrefs.SetInt("Seashell_Stock_" + AdventureBeachSeashellItem.ShellKind.AmberPebble.ToString(), expectedStockPerKind[3]);
                PlayerPrefs.SetInt("Seashell_Stock_" + AdventureBeachSeashellItem.ShellKind.SpiralShell.ToString(), expectedStockPerKind[4]);
                TotalCollectedCount = Mathf.Max(TotalCollectedCount, collectedCount);
            }
            else if (stockSakura > 0)
            {
                // サクラガイに偏ってしまっていたストックを全種類に再分配
                int each = stockSakura / 5;
                int rem = stockSakura % 5;
                PlayerPrefs.SetInt("Seashell_Stock_" + AdventureBeachSeashellItem.ShellKind.Sakuragai.ToString(), each + (rem > 0 ? 1 : 0));
                PlayerPrefs.SetInt("Seashell_Stock_" + AdventureBeachSeashellItem.ShellKind.SeaGlassEmerald.ToString(), each + (rem > 1 ? 1 : 0));
                PlayerPrefs.SetInt("Seashell_Stock_" + AdventureBeachSeashellItem.ShellKind.SeaGlassSapphire.ToString(), each + (rem > 2 ? 1 : 0));
                PlayerPrefs.SetInt("Seashell_Stock_" + AdventureBeachSeashellItem.ShellKind.AmberPebble.ToString(), each + (rem > 3 ? 1 : 0));
                PlayerPrefs.SetInt("Seashell_Stock_" + AdventureBeachSeashellItem.ShellKind.SpiralShell.ToString(), each);
            }
            PlayerPrefs.Save();
            OnInventoryChanged?.Invoke();
            Debug.Log("[AdventureBeachSeashellManager] 🐚 素材ポーチのストックデータを正常に再同期・修復しました");
        }
    }

    /// <summary>白砂ビーチの波打ち際に貝殻・シーグラスを美しく配置</summary>
    void SpawnBeachSeashells()
    {
        // 既存アイテムがあれば二重生成を防止
        if (_items.Count > 0) return;

        var land = Terrain.activeTerrain;

        // 西側白砂ビーチの波打ち際ライン（Z: 170〜430、X: 135〜198、砂浜標高 5.68m〜7.2m）
        // 候補座標（全48箇所：サクラ貝・エメラルド・サファイア・琥珀・巻貝が全域に満遍なく散りばめられる）
        Vector3[] spawnPoints =
        {
            // 1. スタート地点・座礁艇まわり（密集をなくし、2箇所のみ控えめに配置）
            new Vector3(148f, 0f, 268f),
            new Vector3(166f, 0f, 292f),

            // 1-b. 島内・内陸水辺・南西砂州への分散配置
            new Vector3(205f, 0f, 170f), // 南西岬の奥
            new Vector3(192f, 0f, 198f), // 南砂浜のヤシの木陰
            new Vector3(132f, 0f, 325f), // 西海岸中央の岩場
            new Vector3(132f, 0f, 375f), // 北西砂浜の岬
            new Vector3(215f, 0f, 265f), // せせらぎ川の河原
            new Vector3(235f, 0f, 280f), // 小川の渡河地点付近

            // 2. 南西岬〜南白砂ビーチ
            new Vector3(175f, 0f, 220f),
            new Vector3(188f, 0f, 205f),
            new Vector3(196f, 0f, 185f),
            new Vector3(168f, 0f, 235f),
            new Vector3(178f, 0f, 250f),
            new Vector3(182f, 0f, 230f),
            new Vector3(190f, 0f, 215f),
            new Vector3(198f, 0f, 195f),
            new Vector3(172f, 0f, 242f),
            new Vector3(185f, 0f, 175f),

            // 3. 焚き火キャンプ〜西海岸中央
            new Vector3(145f, 0f, 305f),
            new Vector3(138f, 0f, 320f),
            new Vector3(142f, 0f, 335f),
            new Vector3(148f, 0f, 348f),
            new Vector3(155f, 0f, 330f),
            new Vector3(150f, 0f, 312f),
            new Vector3(140f, 0f, 328f),
            new Vector3(146f, 0f, 340f),
            new Vector3(152f, 0f, 325f),
            new Vector3(158f, 0f, 318f),

            // 4. 北西砂浜〜波打ち際北端
            new Vector3(135f, 0f, 365f),
            new Vector3(142f, 0f, 380f),
            new Vector3(146f, 0f, 395f),
            new Vector3(152f, 0f, 410f),
            new Vector3(158f, 0f, 422f),
            new Vector3(138f, 0f, 372f),
            new Vector3(144f, 0f, 388f),
            new Vector3(148f, 0f, 402f),
            new Vector3(154f, 0f, 416f),
            new Vector3(162f, 0f, 428f),

            // 5. 段々池の浅瀬アプローチ〜内陸砂地付近
            new Vector3(162f, 0f, 355f),
            new Vector3(168f, 0f, 370f),
            new Vector3(172f, 0f, 315f),
            new Vector3(182f, 0f, 295f),
            new Vector3(185f, 0f, 265f),
            new Vector3(165f, 0f, 340f),
            new Vector3(170f, 0f, 360f),
            new Vector3(176f, 0f, 305f),
            new Vector3(180f, 0f, 280f),
            new Vector3(188f, 0f, 255f),
        };

        var rootGo = new GameObject("BeachSeashells_Root");

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            Vector3 pt = spawnPoints[i];
            // 初回生成時にも自然なランダムジッター（±1.5m）を付与
            Vector2 jit = Random.insideUnitCircle * 1.5f;
            pt.x += jit.x;
            pt.z += jit.y;

            if (land != null)
            {
                float h = land.SampleHeight(pt) + land.transform.position.y;
                pt.y = Mathf.Max(5.68f, h + 0.05f); // 砂浜表面にしっかり乗る高さ
            }
            else
            {
                pt.y = 6.08f;
            }

            var itemGo = new GameObject($"Seashell_{i + 1:D2}");
            itemGo.transform.SetParent(rootGo.transform, false);
            itemGo.transform.position = pt;

            var item = itemGo.AddComponent<AdventureBeachSeashellItem>();
            var kind = (AdventureBeachSeashellItem.ShellKind)(i % 5);
            item.Initialize($"shell_{i + 1:D2}", kind);

            _items.Add(item);
        }

        Debug.Log($"[AdventureBeachSeashellManager] 白砂ビーチに {spawnPoints.Length} 個の貝殻・シーグラスを配置しました");
    }

    /// <summary>貝殻採取時のトースト通知＆ストック加算</summary>
    public void NotifyCollected(AdventureBeachSeashellItem item)
    {
        TotalCollectedCount++;
        AddShell(item.kind, 1);
        int currentStock = GetShellCount(item.kind);

        var newlyCraftable = AdventureRustCosmetics.Instance != null 
            ? AdventureRustCosmetics.Instance.CheckNewlyCraftable(item.kind) 
            : null;

        // 1. 小さく澄んだ上品な採取音（控えめ音量 0.35f）
        if (_seAudioSource != null && _chimeClip != null)
        {
            float seVol = PlayerPrefs.GetFloat("Adventure_SeVolume", 1.0f);
            _seAudioSource.pitch = Random.Range(1.0f, 1.08f);
            _seAudioSource.PlayOneShot(_chimeClip, 0.35f * seVol);
        }

        // 2. Rustの反応（愛らしくピピッ♪と鳴く＋ホップ＆セリフ）
        var drone = AdventureRustDrone.Instance ?? Object.FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            if (_seAudioSource != null && _rustPipiClip != null)
            {
                _seAudioSource.PlayOneShot(_rustPipiClip, 0.40f);
            }
            drone.TriggerSeashellReaction(item.kind, item.itemName, item.rustReaction, newlyCraftable);
        }

        // 3. トースト案内
        if (_toastText != null)
        {
            string hexCol = ColorUtility.ToHtmlStringRGB(item.themeColor);
            if (newlyCraftable != null)
            {
                _toastText.text = $"<color=#{hexCol}><b>✦ {item.itemName}</b></color> <color=#FFFFFF>を拾った！</color> <size=13><color=#66FFAA>({currentStock}個)</color></size>\n<size=14><color=#FFE066>✨「{newlyCraftable.displayName}」が作れるよ！【Bキー】で工房を開こう！</color></size>";
                _toastTimer = 4.2f;
            }
            else
            {
                _toastText.text = $"<color=#{hexCol}><b>✦ {item.itemName}</b></color> <color=#FFFFFF>を拾った</color>  <size=13><color=#FFE066>(所持: {currentStock}個)</color></size>";
                _toastTimer = 2.6f;
            }
        }
    }

    void Update()
    {
        // 収集トーストのフェードアニメーション
        if (_toastTimer > 0f)
        {
            _toastTimer -= Time.deltaTime;
            if (_toastCg != null)
                _toastCg.alpha = Mathf.MoveTowards(_toastCg.alpha, 1.0f, Time.deltaTime * 5f);
        }
        else
        {
            if (_toastCg != null && _toastCg.alpha > 0f)
                _toastCg.alpha = Mathf.MoveTowards(_toastCg.alpha, 0.0f, Time.deltaTime * 2.5f);
        }
    }

    void CreateToastUI()
    {
        var canvasGo = new GameObject("SeashellToast_Canvas");
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 92; // クエストHUD(95)の少し下

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        // 画面下部中央の洗練されたミニマルトーストパネル
        var panelGo = new GameObject("ToastPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var pRt = panelGo.AddComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0.5f, 0f);
        pRt.anchorMax = new Vector2(0.5f, 0f);
        pRt.pivot = new Vector2(0.5f, 0f);
        pRt.anchoredPosition = new Vector2(0f, 52f);
        pRt.sizeDelta = new Vector2(440f, 44f);

        var pBg = panelGo.AddComponent<Image>();
        pBg.color = new Color(0.02f, 0.04f, 0.08f, 0.88f);

        var pOutline = panelGo.AddComponent<Outline>();
        pOutline.effectColor = new Color(0.35f, 0.75f, 1f, 0.45f);
        pOutline.effectDistance = new Vector2(1.5f, -1.5f);

        _toastCg = panelGo.AddComponent<CanvasGroup>();
        _toastCg.alpha = 0f;
        _toastCg.blocksRaycasts = false;
        _toastCg.interactable = false;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(panelGo.transform, false);
        var tRt = textGo.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(16f, 4f);
        tRt.offsetMax = new Vector2(-16f, -4f);

        _toastText = textGo.AddComponent<Text>();
        _toastText.font = ResolveFont();
        _toastText.fontSize = 15;
        _toastText.alignment = TextAnchor.MiddleCenter;
        _toastText.supportRichText = true;
        _toastText.color = Color.white;
    }

    /// <summary>ニューゲーム／リセット時：採取記録を全初期化してアイテムを再アクティブ化</summary>
    public void ResetAllSeashellsData() => ResetForNewGame();

    public void ResetForNewGame()
    {
        for (int i = 1; i <= 48; i++)
        {
            PlayerPrefs.DeleteKey($"Seashell_Collected_shell_{i:D2}");
        }
        PlayerPrefs.DeleteKey(PrefKeyTotalShells);
        foreach (AdventureBeachSeashellItem.ShellKind k in System.Enum.GetValues(typeof(AdventureBeachSeashellItem.ShellKind)))
        {
            PlayerPrefs.DeleteKey("Seashell_Stock_" + k.ToString());
        }
        PlayerPrefs.Save();
        OnInventoryChanged?.Invoke();

        // 既存アイテムを全再表示＆リスタート時の位置ランダムシャッフル
        ReshuffleSeashellPositions();

        Debug.Log("[AdventureBeachSeashellManager] 🐚 貝殻・シーグラスの収集データを完全リセット＆位置を再配置しました");
    }

    /// <summary>各アイテムの座標を地形に沿ってランダムに再配置・微小ジッター</summary>
    public void ReshuffleSeashellPositions()
    {
        var land = Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item == null) continue;
            item.gameObject.SetActive(true);

            Vector3 current = item.transform.position;
            // 周囲 ±2.5m の範囲でランダムに揺らす
            Vector2 offset = Random.insideUnitCircle * 2.5f;
            Vector3 nextPos = new Vector3(current.x + offset.x, current.y, current.z + offset.y);
            if (land != null)
            {
                float h = land.SampleHeight(nextPos) + land.transform.position.y;
                nextPos.y = Mathf.Max(5.68f, h + 0.05f);
            }
            item.transform.position = nextPos;
        }
    }

    Font ResolveFont()
    {
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f != null) return f;
        f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (f != null) return f;
        return Font.CreateDynamicFontFromOSFont("Hiragino Sans", 14);
    }

    void SetupAudio()
    {
        if (_seAudioSource == null)
        {
            _seAudioSource = gameObject.AddComponent<AudioSource>();
            _seAudioSource.spatialBlend = 0f; // 2D音響で確実に届く
            _seAudioSource.playOnAwake = false;
        }

        if (_chimeClip == null)
            _chimeClip = CreateSmallChimeClip();
        if (_rustPipiClip == null)
            _rustPipiClip = CreateRustHappyPipiClip();
    }

    static AudioClip CreateSmallChimeClip()
    {
        int rate = 44100;
        float duration = 0.28f;
        int count = Mathf.RoundToInt(rate * duration);
        float[] samples = new float[count];

        // 澄んだ小さく優しいチャイム（E6: 1318Hz -> G#6: 1661Hz -> B6: 1975Hz の可憐な和音）
        float[] notes = { 1318.51f, 1661.22f, 1975.53f };
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float sum = 0f;
            for (int n = 0; n < notes.Length; n++)
            {
                float noteT = t - n * 0.035f;
                if (noteT >= 0f)
                {
                    float env = Mathf.Exp(-noteT * 18f); // 素早く減衰する小さく優しい音
                    sum += Mathf.Sin(2f * Mathf.PI * notes[n] * noteT) * env * 0.28f;
                }
            }
            samples[i] = Mathf.Clamp(sum, -1f, 1f);
        }

        var clip = AudioClip.Create("SeashellSmallChime", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    static AudioClip CreateRustHappyPipiClip()
    {
        int rate = 44100;
        float duration = 0.22f;
        int count = Mathf.RoundToInt(rate * duration);
        float[] samples = new float[count];

        // 愛らしい電子チャイム音「ピピッ♪」（高めのピロリン）
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float freq = t < 0.10f ? 1760f : 2349f; // A6 -> D7
            float env = Mathf.Exp(-((t % 0.10f) * 22f));
            float s = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.32f;
            samples[i] = Mathf.Clamp(s, -1f, 1f);
        }

        var clip = AudioClip.Create("RustHappyPipi", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
