using UnityEngine;

/// <summary>
/// 画面モード・解像度（Mac / Unity 6 向けに fullScreenMode と SetResolution を併用）
/// </summary>
public static class AdventureDisplaySettings
{
    const string PrefFullscreen = "RustFloat_Fullscreen";
    const string PrefResW = "RustFloat_ResW";
    const string PrefResH = "RustFloat_ResH";

    public static bool IsWindowed =>
        Screen.fullScreenMode == FullScreenMode.Windowed;

    public static bool IsFullscreen =>
        !IsWindowed;

    public static void GetSavedResolution(out int w, out int h)
    {
        w = PlayerPrefs.GetInt(PrefResW, Screen.width > 0 ? Screen.width : 1280);
        h = PlayerPrefs.GetInt(PrefResH, Screen.height > 0 ? Screen.height : 720);
        if (w < 640) w = 1280;
        if (h < 480) h = 720;
    }

    public static void SavePreferences(bool fullscreen, int width, int height)
    {
        PlayerPrefs.SetInt(PrefFullscreen, fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(PrefResW, width);
        PlayerPrefs.SetInt(PrefResH, height);
        PlayerPrefs.Save();
    }

    static RefreshRate CurrentRefreshRate()
    {
        var ratio = Screen.currentResolution.refreshRateRatio;
        if (ratio.denominator > 0 && ratio.numerator > 0)
            return ratio;
        return new RefreshRate { numerator = 60, denominator = 1 };
    }

    static FullScreenMode FullscreenModeForPlatform()
    {
#if UNITY_STANDALONE_WIN
        return FullScreenMode.FullScreenWindow;
#else
        return FullScreenMode.FullScreenWindow;
#endif
    }

    public static void ApplyFullscreen()
    {
        Resolution native = Screen.currentResolution;
        var mode = FullscreenModeForPlatform();
        var hz = CurrentRefreshRate();
        Screen.SetResolution(native.width, native.height, mode, hz);
        if (Screen.fullScreenMode == FullScreenMode.Windowed)
            Screen.fullScreenMode = mode;
    }

    public static void ApplyWindowed(int width, int height)
    {
        int w = Mathf.Clamp(width, 960, Screen.currentResolution.width);
        int h = Mathf.Clamp(height, 540, Screen.currentResolution.height);
        var hz = CurrentRefreshRate();

        Screen.fullScreenMode = FullScreenMode.Windowed;
        Screen.SetResolution(w, h, FullScreenMode.Windowed, hz);

        if (Screen.fullScreenMode != FullScreenMode.Windowed)
        {
            Screen.SetResolution(w, h, FullScreenMode.Windowed, hz);
            Screen.fullScreenMode = FullScreenMode.Windowed;
        }
    }

    public static void ApplyResolution(int width, int height)
    {
        if (IsWindowed)
            ApplyWindowed(width, height);
        else
        {
            var hz = CurrentRefreshRate();
            Screen.SetResolution(width, height, Screen.fullScreenMode, hz);
        }
    }

    public static string DescribeCurrent()
    {
        string s = $"{Screen.width}×{Screen.height}  ({Screen.fullScreenMode})";
#if UNITY_EDITOR
        s += "  ※Unity Editor の Play では切替が Game タブにだけ反映されることがあります";
#endif
        return s;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplySavedOnLaunch()
    {
        if (!PlayerPrefs.HasKey(PrefFullscreen))
            return;

        bool fs = PlayerPrefs.GetInt(PrefFullscreen, 1) == 1;
        GetSavedResolution(out int w, out int h);
        if (fs)
            ApplyFullscreen();
        else
            ApplyWindowed(w, h);
    }
}
