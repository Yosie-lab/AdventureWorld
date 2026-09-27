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
        var existing = Object.FindFirstObjectByType<AdventureBeachSeashellManager>();
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
        CreateToastUI();
    }

    void Start()
    {
        CheckAndMigrateLegacyStock();
        SpawnBeachSeashells();
    }

    void CheckAndMigrateLegacyStock()
    {
        // 過去に拾った実績があるが個別ストックが未記録の場合の移行
        int total = TotalCollectedCount;
        if (total > 0)
        {
            int currentSum = GetShellCount(AdventureBeachSeashellItem.ShellKind.Sakuragai)
                           + GetShellCount(AdventureBeachSeashellItem.ShellKind.SeaGlassEmerald)
                           + GetShellCount(AdventureBeachSeashellItem.ShellKind.SeaGlassSapphire)
                           + GetShellCount(AdventureBeachSeashellItem.ShellKind.AmberPebble)
                           + GetShellCount(AdventureBeachSeashellItem.ShellKind.SpiralShell);
            if (currentSum == 0)
            {
                // バランスよく分配
                int each = total / 5;
                int rem = total % 5;
                AddShell(AdventureBeachSeashellItem.ShellKind.Sakuragai, each + (rem > 0 ? 1 : 0));
                AddShell(AdventureBeachSeashellItem.ShellKind.SeaGlassEmerald, each + (rem > 1 ? 1 : 0));
                AddShell(AdventureBeachSeashellItem.ShellKind.SeaGlassSapphire, each + (rem > 2 ? 1 : 0));
                AddShell(AdventureBeachSeashellItem.ShellKind.AmberPebble, each + (rem > 3 ? 1 : 0));
                AddShell(AdventureBeachSeashellItem.ShellKind.SpiralShell, each);
            }
        }
    }

    /// <summary>白砂ビーチの波打ち際に貝殻・シーグラスを美しく配置</summary>
    void SpawnBeachSeashells()
    {
        // 既存アイテムがあれば二重生成を防止
        if (_items.Count > 0) return;

        var land = Terrain.activeTerrain;

        // 西側白砂ビーチの波打ち際ライン（Z: 175〜420、X: 135〜190、砂浜標高 5.95m〜7.2m）
        // 候補座標（全24箇所）
        Vector3[] spawnPoints =
        {
            // 1. スタート地点・座礁艇まわり
            new Vector3(156f, 0f, 274f),
            new Vector3(162f, 0f, 281f),
            new Vector3(151f, 0f, 268f),
            new Vector3(166f, 0f, 288f),

            // 2. 南西岬〜南白砂ビーチ
            new Vector3(175f, 0f, 220f),
            new Vector3(188f, 0f, 205f),
            new Vector3(196f, 0f, 185f),
            new Vector3(168f, 0f, 235f),
            new Vector3(178f, 0f, 250f),

            // 3. 焚き火キャンプ〜西海岸中央
            new Vector3(145f, 0f, 305f),
            new Vector3(138f, 0f, 320f),
            new Vector3(142f, 0f, 335f),
            new Vector3(148f, 0f, 348f),
            new Vector3(155f, 0f, 330f),

            // 4. 北西砂浜〜波打ち際北端
            new Vector3(135f, 0f, 365f),
            new Vector3(142f, 0f, 380f),
            new Vector3(146f, 0f, 395f),
            new Vector3(152f, 0f, 410f),
            new Vector3(158f, 0f, 422f),

            // 5. 段々池の浅瀬アプローチ付近
            new Vector3(162f, 0f, 355f),
            new Vector3(168f, 0f, 370f),
            new Vector3(172f, 0f, 315f),
            new Vector3(182f, 0f, 295f),
            new Vector3(185f, 0f, 265f),
        };

        var rootGo = new GameObject("BeachSeashells_Root");

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            Vector3 pt = spawnPoints[i];
            if (land != null)
            {
                float h = land.SampleHeight(pt) + land.transform.position.y;
                pt.y = Mathf.Max(5.95f, h + 0.04f); // 砂浜表面にほんのり埋もれる高さ
            }
            else
            {
                pt.y = 6.08f;
            }

            var itemGo = new GameObject($"Seashell_{i + 1:D2}");
            itemGo.transform.SetParent(rootGo.transform, false);
            itemGo.transform.position = pt;

            var item = itemGo.AddComponent<AdventureBeachSeashellItem>();
            item.itemId = $"shell_{i + 1:D2}";

            // 種類をバリエーション豊かに割り振り
            switch (i % 5)
            {
                case 0: item.kind = AdventureBeachSeashellItem.ShellKind.Sakuragai; break;
                case 1: item.kind = AdventureBeachSeashellItem.ShellKind.SeaGlassEmerald; break;
                case 2: item.kind = AdventureBeachSeashellItem.ShellKind.SeaGlassSapphire; break;
                case 3: item.kind = AdventureBeachSeashellItem.ShellKind.AmberPebble; break;
                case 4: item.kind = AdventureBeachSeashellItem.ShellKind.SpiralShell; break;
            }

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

        if (_toastText != null)
        {
            string hexCol = ColorUtility.ToHtmlStringRGB(item.themeColor);
            _toastText.text = $"<color=#{hexCol}><b>✦ {item.itemName}</b></color> <color=#FFFFFF>を拾った</color>  <size=13><color=#FFE066>(所持: {currentStock}個)</color></size>";
        }

        _toastTimer = 2.6f;
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
    public void ResetForNewGame()
    {
        for (int i = 1; i <= 24; i++)
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

        // 既存アイテムを全再表示
        foreach (var item in _items)
        {
            if (item != null)
            {
                item.gameObject.SetActive(true);
            }
        }

        Debug.Log("[AdventureBeachSeashellManager] 🐚 貝殻・シーグラスの収集データを完全リセットしました");
    }

    Font ResolveFont()
    {
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f != null) return f;
        f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (f != null) return f;
        return Font.CreateDynamicFontFromOSFont("Hiragino Sans", 14);
    }
}
