
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

    [MenuItem("Adventure/📸 Capture Sky Island Views")]
    public static void CaptureSkyViews()
    {
        string outDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../.gemini/antigravity-ide/brain/ed566f25-4713-4c09-bcc2-4dcd6bfc0123/.tempmediaStorage"));
        if (!System.IO.Directory.Exists(outDir)) System.IO.Directory.CreateDirectory(outDir);

        var camGo = new GameObject("__DiagnosticSkyCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 65f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 3000f;

        int width = 960;
        int height = 540;
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        var t = Terrain.activeTerrain;

        // 1. 南上空から全体俯瞰
        cam.transform.position = new Vector3(500f, 320f, 200f);
        cam.transform.eulerAngles = new Vector3(40f, 0f, 0f);
        SaveCam(cam, rt, System.IO.Path.Combine(outDir, "sky_south_overall.png"));

        // 2. 高度450mからトップダウン
        cam.transform.position = new Vector3(500f, 450f, 500f);
        cam.transform.eulerAngles = new Vector3(80f, 0f, 0f);
        SaveCam(cam, rt, System.IO.Path.Combine(outDir, "sky_topdown.png"));

        // 3. 西の海岸・草原・高原
        cam.transform.position = new Vector3(180f, 180f, 300f);
        cam.transform.eulerAngles = new Vector3(30f, 60f, 0f);
        SaveCam(cam, rt, System.IO.Path.Combine(outDir, "sky_west_coast.png"));

        // 4. 西の大草原・花畑と蝶々（見渡すアングル）
        Vector3 p1 = new Vector3(320f, 0f, 320f);
        if (t != null) p1.y = t.SampleHeight(p1) + t.transform.position.y + 1.8f;
        cam.transform.position = p1;
        cam.transform.eulerAngles = new Vector3(15f, 45f, 0f);
        SaveCam(cam, rt, System.IO.Path.Combine(outDir, "ground_west_slope_flowers.png"));

        // 5. 中央タワー広場手前の花壇と蝶々（見渡すアングル）
        Vector3 p2 = new Vector3(460f, 0f, 512f);
        if (t != null) p2.y = t.SampleHeight(p2) + t.transform.position.y + 2.0f;
        cam.transform.position = p2;
        cam.transform.eulerAngles = new Vector3(12f, 85f, 0f);
        SaveCam(cam, rt, System.IO.Path.Combine(outDir, "ground_tower_plaza_flowers.png"));

        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGo);

        // ライティング等の診断出力
        if (t != null && t.terrainData != null)
        {
            Debug.Log($"[VisualDiagnose] Terrain Pos={t.transform.position}, Size={t.terrainData.size}");
            var layers = t.terrainData.terrainLayers;
            Debug.Log($"[VisualDiagnose] Active Terrain: {t.name}, layers={layers.Length}");
            for (int i = 0; i < layers.Length; i++)
            {
                var lyr = layers[i];
                Debug.Log($"[VisualDiagnose] Layer[{i}]: {(lyr != null ? lyr.name : "null")}, diff={(lyr != null && lyr.diffuseTexture != null ? lyr.diffuseTexture.name : "none")}");
            }
        }
        var dirLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in dirLights)
        {
            if (l.type == LightType.Directional)
            {
                Debug.Log($"[VisualDiagnose] Directional Light: {l.gameObject.name}, Color={l.color}, Intensity={l.intensity}");
            }
        }
        Debug.Log($"[VisualDiagnose] Fog: {RenderSettings.fog}, Color={RenderSettings.fogColor}, Density={RenderSettings.fogDensity}");
        Debug.Log($"[VisualDiagnose] Ambient: {RenderSettings.ambientLight}, Mode={RenderSettings.ambientMode}");
        Debug.Log($"[VisualDiagnose] Sky captures saved to {outDir}");
    }

    private static void SaveCam(Camera cam, RenderTexture rt, string path)
    {
        RenderTexture.active = rt;
        cam.Render();
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }
}
