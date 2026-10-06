using UnityEngine;
using UnityEditor;
using System.IO;

public static class AdventureCaptureBeachScreenshot
{
    [MenuItem("Adventure/📸 Capture Beach Screenshot", false, 120)]
    public static void CaptureScreenshot()
    {
        // Nikoのスポーン位置 (158, 6.58, 275) 周辺の海岸線を撮影するカメラを配置
        var camGo = new GameObject("BeachDebugCaptureCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.farClipPlane = 12000f;

        // Nikoの視点（海を向く、あるいはビーチ全体を俯瞰する）
        // 1. Niko地上視点 (白砂浜テラスから波打ち際・海を見る)
        camGo.transform.position = new Vector3(207f, 8.2f, 298f);
        camGo.transform.rotation = Quaternion.Euler(8f, -140f, 0f);

        int width = 1280;
        int height = 720;
        var rt = new RenderTexture(width, height, 24);
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        string outPath1 = "beach_ground_view.png";
        File.WriteAllBytes(outPath1, tex.EncodeToPNG());
        Debug.Log($"Saved ground view to {outPath1}");

        // 2. ビーチ上空からの俯瞰視点 (高さ45mから南西三日月ビーチを見下ろす)
        camGo.transform.position = new Vector3(235f, 45f, 325f);
        camGo.transform.rotation = Quaternion.Euler(40f, -140f, 0f);
        cam.Render();

        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        string outPath2 = "beach_aerial_view.png";
        File.WriteAllBytes(outPath2, tex.EncodeToPNG());
        Debug.Log($"Saved aerial view to {outPath2}");

        // 3. 島全体を真上から見下ろすトップダウン視点 (高さ750m)
        camGo.transform.position = new Vector3(512f, 750f, 512f);
        camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cam.Render();

        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        string outPath3 = "island_topdown_view.png";
        File.WriteAllBytes(outPath3, tex.EncodeToPNG());
        Debug.Log($"Saved topdown view to {outPath3}");

        // クリーンアップ
        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(camGo);
    }
}
