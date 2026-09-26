using UnityEngine;
using UnityEditor;
using System.IO;

public static class AdventureGrant20PointsAndUnlockLever
{
    [MenuItem("Adventure/🔄 レバーを倒す直前のシーンに現状復帰 (F7相当)")]
    public static void ResetToLeverScene()
    {
        Debug.Log("[AdventureGrant] === レバーを倒す直前のシーンへの現状復帰を開始 ===");

        // 1. PlayerPrefs の天蓋開放・クリアフラグを消去し、21ポイント状態を保存
        PlayerPrefs.SetInt("RustAndFloat_CanopyBroken", 0);
        PlayerPrefs.SetInt("RustAndFloat_GameCleared", 0);
        PlayerPrefs.SetInt("RustAndFloat_LeverUnlockedNotified", 1);
        PlayerPrefs.SetInt("DriftBox_Opened_1", 1);
        PlayerPrefs.SetInt("DriftBox_Opened_2", 1);
        PlayerPrefs.SetInt("DriftBox_Opened_3", 1);
        PlayerPrefs.SetInt("AncientPiano_Relic_Collected", 1);
        PlayerPrefs.Save();

        // 2. セーブデータJSONを直接更新（天蓋未開放、レバー手前3.2m）
        string savePath = Path.Combine(Application.persistentDataPath, "rust_and_float_save.json");
        try
        {
            if (File.Exists(savePath))
            {
                string json = File.ReadAllText(savePath);
                var data = JsonUtility.FromJson<AdventureSaveManager.SaveData>(json);
                if (data != null)
                {
                    data.isCanopyBroken = false;
                    data.posX = 512.0f;
                    data.posY = 63.3f;
                    data.posZ = 498.3f;
                    data.rotY = 0f;
                    data.openedDriftBoxIds = new System.Collections.Generic.List<int> { 1, 2, 3 };
                    data.isPianoRelicCollected = true;
                    data.totalProgressPoints = 21;
                    File.WriteAllText(savePath, JsonUtility.ToJson(data, true));
                    Debug.Log("[AdventureGrant] セーブファイルを天蓋未開放・レバー眼前座標に更新しました。");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AdventureGrant] セーブファイル更新警告: {ex.Message}");
        }

        // 3. Playモード中であれば、AdventureSanctuaryTowerManagerのセットアップ処理を実行
        var tower = AdventureSanctuaryTowerManager.Instance ?? Object.FindFirstObjectByType<AdventureSanctuaryTowerManager>();
        if (tower != null)
        {
            tower.DebugSetup20PointsState(warpToLever: true);
        }

        var hud = AdventureScrapHUD.Instance ?? Object.FindFirstObjectByType<AdventureScrapHUD>();
        if (hud != null)
        {
            hud.RefreshQuestDisplay();
            hud.ShowUpgradeBanner("✨ レバーを倒す直前のシーンへ現状復帰しました！\n✦ 【Eキー / Space】で巨大真鍮レバーを引こう！");
        }

        Debug.Log("[AdventureGrant] ✅ レバーを倒す直前のシーンへの現状復帰が完了しました！");
    }

    [MenuItem("Adventure/✨ 20ポイント達成＆レバー即時解放")]
    public static void GrantAndUnlock() => ResetToLeverScene();
}
