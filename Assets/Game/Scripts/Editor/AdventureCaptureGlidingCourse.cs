using UnityEngine;
using UnityEditor;
using System.IO;

public static class AdventureCaptureGlidingCourse
{
    [MenuItem("Adventure/📸 Capture Gliding Course Views (滑空コース撮影)", false, 20)]
    public static void CaptureCourseView()
    {
        string outDir = "/Users/user/.gemini/antigravity-ide/brain/ed566f25-4713-4c09-bcc2-4dcd6bfc0123";
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        var camGo = new GameObject("TempCourseCaptureCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 65f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 2500f;

        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        // 1. 北の最高峰絶壁スタート台からの見下ろし視点（絶景ダイブ前）
        cam.transform.position = new Vector3(507.5f, 95.8f, 658f);
        cam.transform.rotation = Quaternion.Euler(14f, 205f, 0f);
        cam.Render();
        SaveTexture(rt, Path.Combine(outDir, "gliding_start_deck_view.png"));

        // 2. 上空からコース全体を見渡す鳥瞰視点（タワーと連続リング、西ビーチへの航路）
        cam.transform.position = new Vector3(530f, 150f, 650f);
        cam.transform.rotation = Quaternion.Euler(38f, 225f, 0f);
        cam.Render();
        SaveTexture(rt, Path.Combine(outDir, "gliding_course_overview.png"));

        // 3. 西ビーチのゴール着地サークルからの見上げ視点（南国の青空と降下コース）
        cam.transform.position = new Vector3(140f, 8.5f, 215f);
        cam.transform.rotation = Quaternion.Euler(-18f, 45f, 0f);
        cam.Render();
        SaveTexture(rt, Path.Combine(outDir, "gliding_finish_beach_view.png"));

        cam.targetTexture = null;
        RenderTexture.active = null;
        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGo);

        Debug.Log("<color=#00ffc8><b>[AdventureCaptureGliding]</b> 3枚の滑空コース撮影が完了しました！</color>");
    }

    private static void SaveTexture(RenderTexture rt, string filePath)
    {
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(filePath, bytes);
        Object.DestroyImmediate(tex);
        Debug.Log($"[AdventureCaptureGliding] Saved screenshot: {filePath}");
    }
}
