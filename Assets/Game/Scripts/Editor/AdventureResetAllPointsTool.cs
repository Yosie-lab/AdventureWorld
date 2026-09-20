using UnityEngine;
using UnityEditor;

/// <summary>
/// ゲーム内の全探索ポイント（漂着パーツ12個、ドリフトボックス5箱、ピアノ古代遺物）を
/// PlayerPrefsおよびシーン上のオブジェクトを含めて完全に0にリセットするエディタツール。
/// </summary>
public static class AdventureResetAllPointsTool
{
    [InitializeOnLoadMethod]
    [MenuItem("Adventure/🔄 全探索ポイントを0に完全リセット (パーツ・ボックス・遺物)")]
    public static void ResetAllPointsNow()
    {
        Debug.Log("[AdventureResetAllPointsTool] === 全探索ポイントの完全リセットを開始 ===");

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

        var sm = AdventureScrapManager.Instance ?? Object.FindFirstObjectByType<AdventureScrapManager>();
        if (sm != null)
        {
            sm.ResetAllPointsAndScrapsForNewGame();
        }

        var hud = AdventureScrapHUD.Instance ?? Object.FindFirstObjectByType<AdventureScrapHUD>();
        if (hud != null)
        {
            hud.ResetForNewGame();
        }

        Debug.Log("[AdventureResetAllPointsTool] ✅ 漂着パーツ(0/12)、ドリフトボックス(0/5)、ピアノ古代遺物をすべて0ptに初期化しました！");
    }
}
