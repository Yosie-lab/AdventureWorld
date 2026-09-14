using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 漂着パーツの配置と収集状況、および相棒Rustの機能アンロックを統括するマネージャー
/// </summary>
public class AdventureScrapManager : MonoBehaviour
{
    static AdventureScrapManager _instance;
    public static AdventureScrapManager Instance => _instance;

    public const int TotalScrapCount = 12;
    public int CollectedCount { get; private set; } = 0;
    public int collectedCount => CollectedCount;
    public bool hasPetRadar => CollectedCount >= 9;

    readonly List<Vector3> _scrapSpawnPositions = new List<Vector3>
    {
        // 1. スタート地点の目の前（開始時に正面視界に即入るチュートリアル用）
        new Vector3(270f, 0f, 334f),
        // 2. 西側白砂ビーチの漂着木陰
        new Vector3(185f, 0f, 315f),
        // 3. 西側岬の波打ち際
        new Vector3(125f, 0f, 385f),
        // 4. 南西大河の飛び石の岩の上
        new Vector3(280f, 0f, 235f),
        // 5. 草原池の対岸の花畑
        new Vector3(315f, 0f, 345f),
        // 6. カルデラ湖畔の睡蓮の木陰
        new Vector3(385f, 0f, 425f),
        // 7. 中央オアシス湧水池の岩の上
        new Vector3(482f, 0f, 460f),
        // 8. 東の深林大渓流の巨大苔岩
        new Vector3(580f, 0f, 460f),
        // 9. 東部大樹海の古樹の根元
        new Vector3(680f, 0f, 520f),
        // 10. 北東高地の見晴らし岩
        new Vector3(640f, 0f, 650f),
        // 11. 北の大滑空崖のジャンプ台先端
        new Vector3(512f, 0f, 725f),
        // 12. サンクチュアリ中央の石畳テラス
        new Vector3(512f, 0f, 512f)
    };

    readonly List<AdventureScrapItem> _activeItems = new List<AdventureScrapItem>();
    readonly HashSet<int> _collectedIds = new HashSet<int>();

    public IEnumerable<int> GetCollectedIds() => _collectedIds;

    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = FindAnyObjectByType<AdventureScrapManager>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("AdventureScrapManager");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AdventureScrapManager>();
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
        SpawnAllScraps();
    }

    void SpawnAllScraps()
    {
        var land = Terrain.activeTerrain ?? FindAnyObjectByType<Terrain>();
        var player = AdventurePlayerController.Instance ?? FindAnyObjectByType<AdventurePlayerController>();
        Vector3 pSpawn = (player != null && player.spawnPosition != Vector3.zero) 
            ? player.spawnPosition 
            : new Vector3(170f, 43f, 166f);

        var root = new GameObject("ScrapItemsRoot");

        // プレイヤーの実際のスポーン位置に連動した序盤の探索導線
        List<Vector3> positions = new List<Vector3>(_scrapSpawnPositions);
        // 1. スタート直後の正面視界（前方10.5m・右2.5m：開始した瞬間に画面中央に必ず光り輝く）
        positions[0] = new Vector3(pSpawn.x + 2.5f, 0f, pSpawn.z + 10.5f);
        // 2. スタート小道沿いの小高い岩場（前方約35m）
        positions[1] = new Vector3(pSpawn.x + 6f, 0f, pSpawn.z + 36f);
        // 3. 西側白砂ビーチへと続く丘の木陰（約72m先）
        positions[2] = new Vector3(pSpawn.x - 18f, 0f, pSpawn.z + 72f);

        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 pos = positions[i];
            if (land != null)
            {
                pos.y = land.SampleHeight(pos) + land.transform.position.y + 1.35f;
            }
            else
            {
                pos.y = 15f;
            }

            int scrapId = i + 1;
            // 既にセーブデータで取得済みの場合は生成をスキップ
            if (_collectedIds.Contains(scrapId))
                continue;

            var scrapGo = new GameObject("ScrapItem_" + scrapId);
            scrapGo.transform.SetParent(root.transform, false);
            scrapGo.transform.position = pos;

            var item = scrapGo.AddComponent<AdventureScrapItem>();
            item.itemId = scrapId;

            // アイテムのバリエーション（ギア・コア・プリズム）
            if (i % 3 == 0)
            {
                item.itemName = "古代の黄金ギア";
                item.itemColor = new Color(1.0f, 0.78f, 0.22f); // 黄金
            }
            else if (i % 3 == 1)
            {
                item.itemName = "エネルギーコア";
                item.itemColor = new Color(0.25f, 0.95f, 0.85f); // シアンエメラルド
            }
            else
            {
                item.itemName = "推進スタビライザー";
                item.itemColor = new Color(1.0f, 0.45f, 0.85f); // マゼンタピンク
            }

            _activeItems.Add(item);
        }
    }

    public void OnScrapCollected(AdventureScrapItem item)
    {
        CollectedCount++;
        _collectedIds.Add(item.itemId);
        _activeItems.Remove(item);

        // HUDに通知
        if (AdventureScrapHUD.Instance != null)
        {
            AdventureScrapHUD.Instance.OnCollect(item.itemName, CollectedCount, TotalScrapCount);
        }

        // 相棒Rustにリアクションさせる
        var drone = FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            drone.OnNikoFoundScrap(CollectedCount);
        }

        // 段階的なアップグレード判定
        CheckUpgrades();

        // オートセーブを実行！
        if (AdventureSaveManager.Instance != null)
        {
            AdventureSaveManager.Instance.SaveGame($"パーツ発見！({CollectedCount}/{TotalScrapCount})");
        }
    }

    /// <summary>セーブデータから収集済みパーツ一覧を適用し、能力とHUDを復元</summary>
    public void ApplyLoadedScraps(List<int> loadedIds)
    {
        if (loadedIds == null) return;

        foreach (var id in loadedIds)
        {
            _collectedIds.Add(id);
        }
        CollectedCount = _collectedIds.Count;

        // 既に生成されているアクティブアイテムから回収済みIDのものを消去
        for (int i = _activeItems.Count - 1; i >= 0; i--)
        {
            var it = _activeItems[i];
            if (it != null && _collectedIds.Contains(it.itemId))
            {
                _activeItems.RemoveAt(i);
                Destroy(it.gameObject);
            }
        }

        // HUDを同期
        if (AdventureScrapHUD.Instance != null)
        {
            AdventureScrapHUD.Instance.OnCollect("", CollectedCount, TotalScrapCount);
        }

        // アンロック能力を一括復元
        ApplyAllUpgradesForCount(CollectedCount);
    }

    /// <summary>獲得数に応じたアンロック能力を全適用</summary>
    public void ApplyAllUpgradesForCount(int count)
    {
        var player = AdventurePlayerController.Instance ?? FindAnyObjectByType<AdventurePlayerController>();
        if (player == null) return;

        if (count >= 3)
        {
            player.runSpeed = 9.4f;
            player.turnSpeed = 16.0f;
        }
        if (count >= 6)
        {
            player.canDoubleJump = true;
        }
        if (count >= 9)
        {
            player.hasPetRadar = true;
        }
        if (count >= 12)
        {
            player.glideForwardSpeed = 11.5f;
            player.glideFallSpeed = -1.35f;
        }
    }

    void CheckUpgrades()
    {
        var player = AdventurePlayerController.Instance ?? FindAnyObjectByType<AdventurePlayerController>();
        if (player == null) return;

        if (CollectedCount == 3)
        {
            // 3個: 黄金ギア完成（ダッシュ速度 7.8 -> 9.4m/s & 旋回強化）
            player.runSpeed = 9.4f;
            player.turnSpeed = 16.0f;
            NotifyLore(
                "キーストーン I：手動の自由と手応え",
                "AIに管理されていた頃、僕らはただ最短ルートを滑らされていた。\nでも今、指先が油で汚れ、歯車が噛み合うたびに、生きている実感が胸を打つ。",
                "【ブースター修復】ダッシュ速度＆クイックターンが向上！"
            );
        }
        else if (CollectedCount == 6)
        {
            // 6個: 反重力エネルギーコア脈動（二段ジャンプ完全解禁！）
            player.canDoubleJump = true;
            NotifyLore(
                "キーストーン II：脈打つ不確かさ",
                "100%最適化された電力にはなかった、温かくて不規則な青い脈動。\n……心臓の鼓動と同じだ。傷つく自由があるからこそ、光は美しい。",
                "【反重力ジャンプ覚醒】空中でSpaceを押すと二段ジャンプが可能に！"
            );
        }
        else if (CollectedCount == 9)
        {
            // 9個: 探知ソナー／古いメモリ想起（遺物レーダー解禁）
            player.hasPetRadar = true;
            NotifyLore(
                "キーストーン III：失われた記憶の断片",
                "Rustの古いメモリから、子供たちの笑い声と夏の波音が再生された。\n効率化のために根絶された“無駄な時間”の中にこそ、愛があったんだ。",
                "【探知ソナー修復】Rustが近くのパーツをピピッとナビゲート！"
            );
        }
        else if (CollectedCount == 12)
        {
            // 12個: 推進スタビライザー完成（全機能同期・スーパーグライダー完全解放）
            player.glideForwardSpeed = 11.5f;
            player.glideFallSpeed = -1.35f;
            NotifyLore(
                "キーストーン IV：風の重さと、未知の空へ",
                "最適化都市では風すら消し去られていた。冷たい向かい風は、前へ進んでいる証拠だ。\nさあ行こう、Rust。島の頂、あの白亜のタワーへ！",
                "【スーパーグライダー解放】大滑空滞空力＆前進推進力が最大化！"
            );
        }
    }

    void NotifyLore(string title, string loreQuote, string unlockEffect)
    {
        if (AdventureScrapHUD.Instance != null)
        {
            AdventureScrapHUD.Instance.ShowPoeticLore(title, loreQuote, unlockEffect);
        }
    }

    void NotifyUpgrade(string message)
    {
        if (AdventureScrapHUD.Instance != null)
        {
            AdventureScrapHUD.Instance.ShowUpgradeBanner(message);
        }
    }

    /// <summary>探知ソナー用：プレイヤーから最も近い未取得パーツの位置を返す</summary>
    public Transform GetNearestScrap(Vector3 playerPos, out float distance)
    {
        var item = GetNearestScrapItem(playerPos, out distance);
        return item != null ? item.transform : null;
    }

    /// <summary>最寄りの未取得アイテム実体を返す</summary>
    public AdventureScrapItem GetNearestScrapItem(Vector3 playerPos, out float distance)
    {
        AdventureScrapItem nearest = null;
        float minDist = float.MaxValue;

        for (int i = 0; i < _activeItems.Count; i++)
        {
            var it = _activeItems[i];
            if (it == null) continue;
            float d = Vector3.Distance(playerPos, it.transform.position);
            if (d < minDist)
            {
                minDist = d;
                nearest = it;
            }
        }

        distance = minDist;
        return nearest;
    }
}
