using UnityEngine;
using UnityEditor;

/// <summary>
/// ゲーム内の全探索ポイント（漂着パーツ12個、ドリフトボックス5箱、ピアノ古代遺物）を
/// PlayerPrefsおよびシーン上のオブジェクトを含めて完全に0にリセットするエディタツール。
/// </summary>
public static class AdventureResetAllPointsTool
{
    [MenuItem("Adventure/🔄 全探索ポイントを0に完全リセット (パーツ・ボックス・遺物)")]
    public static void ResetAllPointsNow()
    {
        Debug.Log("[AdventureResetAllPointsTool] === 全探索ポイントの完全リセットを開始 ===");

        // 0. Rustの装備PlayerPrefs削除＆未装備化
        var cosmeticIds = System.Enum.GetValues(typeof(AdventureRustCosmetics.CosmeticId));
        foreach (AdventureRustCosmetics.CosmeticId cid in cosmeticIds)
        {
            PlayerPrefs.DeleteKey("RustCosmetic_Unlocked_" + cid.ToString());
            PlayerPrefs.DeleteKey("RustCosmetic_Equipped_" + cid.ToString());
            PlayerPrefs.SetInt("RustCosmetic_Equipped_" + cid.ToString(), 0);
        }
        var allDrones = Object.FindObjectsByType<AdventureRustDrone>(FindObjectsInactive.Include);
        foreach (var drone in allDrones)
        {
            if (drone == null) continue;
            var children = drone.GetComponentsInChildren<Transform>(true);
            foreach (var t in children)
            {
                if (t != null && t.gameObject != null && t.gameObject.name.StartsWith("Cosmetic_"))
                {
                    Object.DestroyImmediate(t.gameObject);
                }
            }
        }

        // 1. ドリフトボックス（1〜5）のPlayerPrefs削除と未開封化
        for (int i = 1; i <= 5; i++)
        {
            PlayerPrefs.DeleteKey("DriftBox_Opened_" + i);
        }

        // 2. ピアノ古代遺物のPlayerPrefs削除
        PlayerPrefs.DeleteKey("AncientPiano_Relic_Collected");
        PlayerPrefs.DeleteKey("AncientPiano_Discovered");

        // 3. レバーロック通知フラグの削除
        PlayerPrefs.DeleteKey("RustAndFloat_LeverUnlockedNotified");
        PlayerPrefs.DeleteKey("Adventure_LeverUnlockedNotified");

        PlayerPrefs.Save();

        // 4. シーン内コンポーネントのリセット
        AdventureBeachDriftBox.ResetAllBoxesStatic();
        AdventureAncientPianoRelic.ResetAllPianoRelicsStatic();

        var sm = AdventureScrapManager.Instance ?? Object.FindAnyObjectByType<AdventureScrapManager>();
        if (sm != null)
        {
            sm.ResetAllPointsAndScrapsForNewGame();
        }

        var hud = AdventureScrapHUD.Instance ?? Object.FindAnyObjectByType<AdventureScrapHUD>();
        if (hud != null)
        {
            hud.ResetForNewGame();
        }

        // 5. 砂浜の貝殻・シーグラス（全48個）の再配置＆リセット
        var shellMgr = AdventureBeachSeashellManager.Instance ?? Object.FindAnyObjectByType<AdventureBeachSeashellManager>();
        if (shellMgr != null)
        {
            shellMgr.ResetForNewGame();
        }

        // 6. Rustの着せ替えアクセサリー完全解除（未装備初期化）
        AdventureRustCosmetics.Ensure();
        AdventureRustCosmetics.Instance?.ResetForNewGame();
        AdventureRustCosmetics.Instance?.UnequipAll();

        Debug.Log("[AdventureResetAllPointsTool] ✅ 漂着パーツ(0/12)、ドリフトボックス(0/5)、ピアノ古代遺物、貝殻・シーグラス(48個)、Rustの装備をすべて初期化しました！");
    }

    [MenuItem("Adventure/🐚 砂浜の貝殻・シーグラスを全リスポーン（再配置）")]
    public static void RespawnAllBeachSeashellsNow()
    {
        var shellMgr = AdventureBeachSeashellManager.Instance ?? Object.FindAnyObjectByType<AdventureBeachSeashellManager>();
        if (shellMgr != null)
        {
            shellMgr.ResetForNewGame();
            Debug.Log("[AdventureResetAllPointsTool] 🐚 砂浜の全貝殻・シーグラスをリスポーンしました！");
        }
    }

    [MenuItem("Adventure/Unequip All Rust Cosmetics")]
    public static void UnequipAllRustCosmeticsNow()
    {
        AdventureRustCosmetics.Ensure();
        AdventureRustCosmetics.Instance?.UnequipAll();
        Debug.Log("[AdventureResetAllPointsTool] 🎀 Rustの装備をすべて外しました！");
    }
}
