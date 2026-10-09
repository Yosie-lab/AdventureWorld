
using UnityEngine;
using UnityEditor;

public static class BeachCameraInspector
{
    [MenuItem("Adventure/📸 Look Around Beach")]
    public static void LookAround()
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv != null)
        {
            sv.pivot = new Vector3(175f, 15f, 250f);
            sv.rotation = Quaternion.Euler(35f, 50f, 0f);
            sv.size = 60f;
            sv.Repaint();
        }
    }
}
