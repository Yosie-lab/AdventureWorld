#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Mac ビルドで Windowed ↔ FullScreenWindow の切替が効くよう Player Settings を整える
/// </summary>
[InitializeOnLoad]
static class AdventureDisplayPlayerSettings
{
    static AdventureDisplayPlayerSettings()
    {
        if (PlayerSettings.fullScreenMode == FullScreenMode.ExclusiveFullScreen)
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;

        PlayerSettings.resizableWindow = true;
    }
}
#endif
