
using UnityEngine;
using UnityEditor;

public static class RiverInspector
{
    [MenuItem("Adventure/📸 Look At River Steps")]
    public static void LookAtRiverSteps()
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv != null)
        {
            // 段々水面（RiverSeg_14〜19: 220..160, 240..160）を斜めからクローズアップ
            sv.pivot = new Vector3(195f, 10f, 195f);
            sv.rotation = Quaternion.Euler(28f, -45f, 0f);
            sv.size = 35f;
            sv.Repaint();
        }
    }
}
