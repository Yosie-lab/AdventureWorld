using UnityEditor;

/// <summary>CLI / エージェントから待機 PNG の再インポートを強制する。</summary>
public static class AdventureForceReimportPreStartSplash
{
    const string SessionKey = "RustAndFloat.PreStartPngReimport.v2";

    [UnityEditor.InitializeOnLoadMethod]
    static void AutoReimportOncePerEditorSession()
    {
        EditorApplication.delayCall += () =>
        {
            if (SessionState.GetBool(SessionKey, false))
                return;
            SessionState.SetBool(SessionKey, true);
            Run(silent: true);
        };
    }

    const string TextureAssetPath = "Assets/RustAndFloat/Textures/CurrentSceneCapture.png";
    const string ResourcesAssetPath = "Assets/RustAndFloat/Resources/CurrentSceneCapture.png";

    [MenuItem("RustAndFloat/🔁 Force Reimport Pre-Start PNGs", false, 14)]
    public static void RunFromMenu()
    {
        Run(silent: false);
    }

    public static void Run(bool silent = false)
    {
        AssetDatabase.ImportAsset(TextureAssetPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(ResourcesAssetPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        if (!silent)
        {
            UnityEngine.Debug.Log(
                "<color=#00ffc8><b>[PreStartSplash]</b> CurrentSceneCapture を2か所 Reimport しました。</color>");
        }
    }
}
