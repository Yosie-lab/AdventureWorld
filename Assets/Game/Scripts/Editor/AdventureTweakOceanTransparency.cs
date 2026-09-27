using UnityEngine;
using UnityEditor;

public static class AdventureTweakOceanTransparency
{
    [MenuItem("Adventure/Ocean/Apply High Transparency")]
    public static void ApplyHighTransparency()
    {
        string matPath = "Assets/RustAndFloat/Materials/ParadiseOcean_URP.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Debug.LogError("Failed to load ocean material at " + matPath);
            return;
        }

        Undo.RecordObject(mat, "Tweak Ocean Transparency");

        // 浅瀬の色：透明度を大幅に高めたクリアなアクアエメラルド (alpha: 0.06)
        Color shallowColor = new Color(0.12f, 0.92f, 0.96f, 0.06f);
        mat.SetColor("_Shallow_Color", shallowColor);

        // 深海の色：沖合ではしっかりとしたリゾートのセルリアンブルー (alpha: 0.85)
        Color deepColor = new Color(0.01f, 0.38f, 0.72f, 0.85f);
        mat.SetColor("_Deep_Color", deepColor);

        // 深度フェード係数：1.4 -> 0.18（浅瀬から深海への移行をゆったりにし、水深2m程度まで高い透明感を維持）
        mat.SetFloat("_Depth_Distance", 0.18f);

        // 汀線（水深0）での不透明度：0.03 -> 0.005（水際がガラスのように砂浜へ溶け込む）
        mat.SetFloat("_Coast_Opacity", 0.005f);

        // 屈折強度：0.10 -> 0.05（海底の白砂や小魚が歪みすぎず、クリスタルクリアに透けて見える）
        mat.SetFloat("_Refraction_Strength", 0.05f);

        // 法線強度：0.15 -> 0.09（水面の波立ちの乱反射を抑え、底が綺麗に見通せる透明感）
        mat.SetFloat("_Normal_Strength", 0.09f);

        // スムースネス：ツヤ感を高く維持
        mat.SetFloat("_Smoothness", 0.96f);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        Debug.Log("Paradise Ocean transparency successfully updated! Shallow Alpha=0.06, DepthDistance=0.18, CoastOpacity=0.005");
    }
}
