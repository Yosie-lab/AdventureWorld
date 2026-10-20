using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 現在の Game / Scene 視点をスタート前待機画像（CurrentSceneCapture）に書き出す。
/// </summary>
public static class AdventureCapturePreStartSplash
{
    const string TextureAssetPath = "Assets/RustAndFloat/Textures/CurrentSceneCapture.png";
    const string ResourcesAssetPath = "Assets/RustAndFloat/Resources/CurrentSceneCapture.png";
    const int Width = 1280;
    const int Height = 720;

    [MenuItem("RustAndFloat/📸 Capture Pre-Start Splash (Current View)", false, 12)]
    public static void CaptureFromCurrentView()
    {
        if (!TryGetCaptureCamera(out Camera cam, out bool tempCam))
        {
            EditorUtility.DisplayDialog(
                "Capture Pre-Start Splash",
                "Main Camera または Scene ビューのカメラが見つかりません。",
                "OK");
            return;
        }

        try
        {
            byte[] png = RenderCameraToPng(cam, Width, Height);
            string texAbs = Path.GetFullPath(TextureAssetPath);
            string resAbs = Path.GetFullPath(ResourcesAssetPath);
            File.WriteAllBytes(texAbs, png);
            File.WriteAllBytes(resAbs, png);
            AssetDatabase.ImportAsset(TextureAssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(ResourcesAssetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log(
                $"<color=#00ffc8><b>[PreStartSplash]</b> 待機画像を更新しました（{Width}x{Height}）→ {TextureAssetPath}</color>");
        }
        finally
        {
            if (tempCam && cam != null)
                Object.DestroyImmediate(cam.gameObject);
        }
    }

    static bool TryGetCaptureCamera(out Camera cam, out bool tempCam)
    {
        cam = null;
        tempCam = false;

        if (Application.isPlaying)
        {
            cam = Camera.main;
            if (cam == null)
                cam = Object.FindAnyObjectByType<Camera>();
            return cam != null;
        }

        if (SceneView.lastActiveSceneView != null)
        {
            var svCam = SceneView.lastActiveSceneView.camera;
            if (svCam != null)
            {
                var go = new GameObject("__PreStartSplashCaptureCam");
                go.hideFlags = HideFlags.HideAndDontSave;
                cam = go.AddComponent<Camera>();
                cam.CopyFrom(svCam);
                cam.enabled = true;
                tempCam = true;
                return true;
            }
        }

        cam = Object.FindAnyObjectByType<Camera>();
        return cam != null;
    }

    static byte[] RenderCameraToPng(Camera cam, int width, int height)
    {
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        cam.targetTexture = prevTarget;
        RenderTexture.active = prevActive;
        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
        return png;
    }
}
