
using UnityEngine;
using UnityEditor;

public static class PondVisualInspector
{
    [MenuItem("Adventure/📸 Focus Meadow Lowland Pond")]
    public static void FocusMeadow()
    {
        var pond = GameObject.Find("MeadowLowlandPond");
        if (pond != null)
        {
            Selection.activeGameObject = pond;
            SceneView.lastActiveSceneView.FrameSelected();
        }
    }

    [MenuItem("Adventure/📸 Focus Sanctuary Spring Pond")]
    public static void FocusSanctuary()
    {
        var pond = GameObject.Find("SanctuarySpringPond");
        if (pond != null)
        {
            Selection.activeGameObject = pond;
            SceneView.lastActiveSceneView.FrameSelected();
        }
    }
}
