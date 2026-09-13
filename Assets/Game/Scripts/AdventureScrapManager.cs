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
        var root = new GameObject("ScrapItemsRoot");

        for (int i = 0; i < _scrapSpawnPositions.Count; i++)
        {
            Vector3 pos = _scrapSpawnPositions[i];
            if (land != null)
            {
                pos.y = land.SampleHeight(pos) + land.transform.position.y + 1.35f;
            }
            else
            {
                pos.y = 15f;
            }

            var scrapGo = new GameObject("ScrapItem_" + (i + 1));
            scrapGo.transform.SetParent(root.transform, false);
            scrapGo.transform.position = pos;

            var item = scrapGo.AddComponent<AdventureScrapItem>();
            item.itemId = i + 1;

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
    }

    void CheckUpgrades()
    {
        var player = AdventurePlayerController.Instance ?? FindAnyObjectByType<AdventurePlayerController>();
        if (player == null) return;

        if (CollectedCount == 3)
        {
            // 3個: ブースター修復（ダッシュ速度 7.8 -> 9.2m/s）
            player.runSpeed = 9.2f;
            NotifyUpgrade("【ブースター修復！】\nRustの推進力でダッシュ速度がアップした！");
        }
        else if (CollectedCount == 6)
        {
            // 6個: 反重力機能修復（二段ジャンプ完全解禁！）
            player.canDoubleJump = true;
            NotifyUpgrade("【反重力ジャンプ解禁！】\n空中でSpaceを押すと二段ジャンプができる！");
        }
        else if (CollectedCount == 9)
        {
            // 9個: 探知ソナー修復（近くの漂着パーツを音と光でナビゲート）
            player.hasPetRadar = true;
            NotifyUpgrade("【探知ソナー修復！】\nRustが近くのパーツをピピッと教えてくれる！");
        }
        else if (CollectedCount == 12)
        {
            // 12個: 大滑空ブースター展開（滑空滞空・前進速度大幅強化）
            player.glideForwardSpeed = 10.5f;
            player.glideFallSpeed = -1.6f;
            NotifyUpgrade("【スーパーグライダー解禁！】\n風に乗って島全体を悠々と大滑空できる！");
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
        Transform nearest = null;
        float minDist = float.MaxValue;

        for (int i = 0; i < _activeItems.Count; i++)
        {
            var it = _activeItems[i];
            if (it == null) continue;
            float d = Vector3.Distance(playerPos, it.transform.position);
            if (d < minDist)
            {
                minDist = d;
                nearest = it.transform;
            }
        }

        distance = minDist;
        return nearest;
    }
}
