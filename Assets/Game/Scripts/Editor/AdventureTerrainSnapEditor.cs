#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AdventureTerrainSnapEditor
{
    [MenuItem("Adventure/Fix Lake East Shore (175,151)")]
    static void FixLakeEastShore()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Adventure",
                "Play を止めてから実行してください。",
                "OK");
            return;
        }

        ApplyLakeEastFix();
    }

    public static void ApplyLakeEastFix()
    {
        AdventureTerrainSnap.FixLakeEastShore();

        var land = AdventureTerrainSnap.FindLand();
        if (land != null && land.terrainData != null)
            EditorUtility.SetDirty(land.terrainData);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Adventure: lake east shore fixed (x158-188, z138-168).");
    }

    [MenuItem("Adventure/Fix NW Grassland (118,218)")]
    static void FixNorthWestGrassland()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Adventure",
                "Play を止めてから実行してください。",
                "OK");
            return;
        }

        ApplyNorthWestFix();
    }

    public static void ApplyNorthWestFix()
    {
        AdventureTerrainSnap.FixNorthWestGrassland();

        var land = AdventureTerrainSnap.FindLand();
        if (land != null && land.terrainData != null)
            EditorUtility.SetDirty(land.terrainData);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Adventure: north-west grassland fixed (x98-138, z198-238).");
    }

    public static void BatchFixNorthWestGrassland()
    {
        ApplyNorthWestFix();
        EditorApplication.Exit(0);
    }
}
#endif
