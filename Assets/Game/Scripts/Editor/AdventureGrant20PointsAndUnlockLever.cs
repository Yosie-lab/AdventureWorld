using UnityEngine;
using UnityEditor;
using System.IO;

public static class AdventureGrant20PointsAndUnlockLever
{
    [MenuItem("Adventure/✨ 20ポイント達成＆レバー即時解放")]
    public static void GrantAndUnlock()
    {
        // 1. PlayerPrefs にドリフトボックス1〜5およびピアノ遺物を設定
        for (int i = 1; i <= 5; i++)
        {
            PlayerPrefs.SetInt("DriftBox_Opened_" + i, 1);
        }
        PlayerPrefs.SetInt("AncientPiano_Relic_Collected", 1);
        PlayerPrefs.SetInt("RustAndFloat_LeverUnlockedNotified", 1);
        PlayerPrefs.Save();

        // 2. セーブデータJSONを直接更新（パーツ10個＋ボックス10pt＋ピアノ3pt＝23pt）
        string savePath = Path.Combine(Application.persistentDataPath, "rust_and_float_save.json");
        try
        {
            if (File.Exists(savePath))
            {
                string json = File.ReadAllText(savePath);
                var data = JsonUtility.FromJson<AdventureSaveManager.SaveData>(json);
                if (data != null)
                {
                    data.openedDriftBoxIds = new System.Collections.Generic.List<int> { 1, 2, 3, 4, 5 };
                    data.isPianoRelicCollected = true;
                    data.totalProgressPoints = (data.collectedCount > 0 ? data.collectedCount : 10) + 10 + 3;
                    File.WriteAllText(savePath, JsonUtility.ToJson(data, true));
                    Debug.Log($"[AdventureGrant] セーブファイル更新完了: 総ポイント {data.totalProgressPoints} pt");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AdventureGrant] セーブ更新警告: {ex.Message}");
        }

        // 3. 実行中ならコンポーネントへ即時同期
        var scrapMgr = AdventureScrapManager.Instance ?? Object.FindFirstObjectByType<AdventureScrapManager>();
        if (scrapMgr != null)
        {
            scrapMgr.IsPianoRelicCollected = true;
            scrapMgr.CheckPointsAndNotifyLeverUnlock();
        }

        var tower = AdventureSanctuaryTowerManager.Instance ?? Object.FindFirstObjectByType<AdventureSanctuaryTowerManager>();
        if (tower != null)
        {
            tower.OnLeverUnlockedByPoints();
        }

        var hud = AdventureScrapHUD.Instance ?? Object.FindFirstObjectByType<AdventureScrapHUD>();
        if (hud != null)
        {
            hud.RefreshQuestDisplay();
            hud.ShowUpgradeBanner("✨ 探索ポイント還元完了！ (計 20+ pt)\n✦ 中央タワーの巨大レバーロックが解除されました！");
        }

        Debug.Log("[AdventureGrant] ✅ 20ポイント達成＆巨大真鍮レバーのロックを即座に解放しました！");
    }
}
