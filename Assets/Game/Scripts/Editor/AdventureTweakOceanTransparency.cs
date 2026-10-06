using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

public static class AdventureTweakOceanTransparency
{
    private const string MaterialPath = "Assets/RustAndFloat/Materials/ParadiseCurvedOcean.mat";
    private const string MeshPath = "Assets/RustAndFloat/Terrain/ParadiseCurvedOceanMesh.asset";
    private const string ShaderName = "RustAndFloat/ParadiseCurvedOcean";

    [MenuItem("Adventure/Ocean/🏝️ エメラルドグリーン海面を適用 (Apply Emerald Green Ocean)", priority = 200)]
    public static void ApplyHighTransparency()
    {
        if (Application.isPlaying) return;
        Debug.Log("<color=#00ffc8><b>[AdventureCurvedOcean]</b> エメラルドグリーン曲面海面システムの構築を開始します...</color>");

        // 1. ラジアル曲面メッシュ (半径4500m / 直径9km) の生成と保存
        var mesh = AdventureCurvedHorizonOcean.GenerateRadialCurvedOceanMesh();
        var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (existingMesh == null)
        {
            AssetDatabase.CreateAsset(mesh, MeshPath);
        }
        else
        {
            EditorUtility.CopySerialized(mesh, existingMesh);
        }
        AssetDatabase.SaveAssets();

        // 2. マテリアルの生成・エメラルドグリーン色彩設定
        var shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError("[AdventureCurvedOcean] Shader not found: " + ShaderName);
            return;
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        else
        {
            mat.shader = shader;
        }

        var n1 = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Idyllic Fantasy Nature/Textures/Water/Water_Normal_01.png");
        var n2 = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Idyllic Fantasy Nature/Textures/Water/Water_Normal_02.png");
        if (n1 != null) mat.SetTexture("_NormalMap1", n1);
        if (n2 != null) mat.SetTexture("_NormalMap2", n2);

        // 透き通る鮮やかなクリスタルエメラルドグリーン 〜 ターコイズ 〜 ディープエメラルド 〜 水平線ミント
        mat.SetColor("_ShallowColor", new Color(0.015f, 0.98f, 0.78f, 0.82f));
        mat.SetColor("_MidColor", new Color(0.01f, 0.90f, 0.72f, 0.94f));
        mat.SetColor("_DeepColor", new Color(0.005f, 0.62f, 0.55f, 0.99f));
        mat.SetColor("_HorizonColor", new Color(0.22f, 0.90f, 0.84f, 1.0f));
        mat.SetColor("_SunGlitterColor", new Color(1.0f, 0.98f, 0.88f, 1.0f));

        mat.SetFloat("_WaveScale1", 0.035f);
        mat.SetFloat("_WaveScale2", 0.085f);
        mat.SetVector("_WaveSpeed1", new Vector4(0.025f, 0.015f, 0f, 0f));
        mat.SetVector("_WaveSpeed2", new Vector4(-0.02f, 0.03f, 0f, 0f));
        mat.SetFloat("_NormalStrength", 1.25f);
        mat.SetFloat("_Smoothness", 0.985f);
        mat.SetFloat("_SunGlitterIntensity", 4.8f);
        mat.SetFloat("_SunGlitterExponent", 80f);
        mat.SetFloat("_SparkleScale", 1.8f);

        mat.SetVector("_IslandCenter", new Vector4(512f, 5.5f, 512f, 0f));
        mat.SetFloat("_ShallowRadius", 360f);
        mat.SetFloat("_DeepRadius", 800f);
        mat.SetFloat("_HorizonRadius", 4500f);
        mat.SetFloat("_CurvatureAmount", 22.0f);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        // 3. シーン内 OceanPlane への設定
        var oceanGo = GameObject.Find("OceanPlane");
        if (oceanGo == null)
        {
            oceanGo = new GameObject("OceanPlane");
            Undo.RegisterCreatedObjectUndo(oceanGo, "Create OceanPlane");
        }

        Undo.RecordObject(oceanGo.transform, "Setup Ocean Transform");
        oceanGo.transform.position = new Vector3(512f, 5.5f, 512f);
        oceanGo.transform.rotation = Quaternion.identity;
        oceanGo.transform.localScale = Vector3.one;

        var col = oceanGo.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        var filter = oceanGo.GetComponent<MeshFilter>() ?? oceanGo.AddComponent<MeshFilter>();
        var renderer = oceanGo.GetComponent<MeshRenderer>() ?? oceanGo.AddComponent<MeshRenderer>();
        var oceanComp = oceanGo.GetComponent<AdventureCurvedHorizonOcean>() ?? oceanGo.AddComponent<AdventureCurvedHorizonOcean>();

        Undo.RecordObject(filter, "Assign Ocean Mesh");
        filter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);

        Undo.RecordObject(renderer, "Assign Ocean Material");
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = true;

        // 4. 全カメラの描画距離を 5000m に拡張
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cam != null && cam.farClipPlane < 5000f)
            {
                Undo.RecordObject(cam, "Extend Camera Far Clip Plane");
                cam.farClipPlane = 5000f;
                EditorUtility.SetDirty(cam);
            }
        }

        // 5. 島の海岸線の白砂化＆四角い海底の完全消去
        AdventureBeautifyIslandCoastline.BeautifyCoastlineAndRemoveSquareSeaBed();

        EditorUtility.SetDirty(oceanGo);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("<color=#00ffc8><b>[AdventureCurvedOcean]</b> エメラルドグリーン曲面海面システムの構築が完了しました！</color>");

        // 6. 検証スクリーンショットの撮影
        CaptureViews();
    }

    private static void CaptureViews()
    {
        string outDir = "/Users/user/.gemini/antigravity-ide/brain/ed566f25-4713-4c09-bcc2-4dcd6bfc0123";
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        var camGo = new GameObject("TempOceanCaptureCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 65f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 5000f;

        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        // 1. エンディング上空（Y:150m）から西ビーチと彼方の曲線の水平線を見下ろす視点
        cam.transform.position = new Vector3(320f, 150f, 320f);
        cam.transform.rotation = Quaternion.Euler(18f, 235f, 0f);
        cam.Render();
        SaveTexture(rt, Path.Combine(outDir, "ending_sky_horizon_ocean.png"));

        // 2. 高空（Y:240m）から島全体と360度広がる外洋水平線を見渡すパノラマ視点
        cam.transform.position = new Vector3(512f, 240f, 200f);
        cam.transform.rotation = Quaternion.Euler(26f, 0f, 0f);
        cam.Render();
        SaveTexture(rt, Path.Combine(outDir, "ending_high_altitude_panorama.png"));

        // 3. 西ビーチ（砂浜）から見渡す、エメラルドグリーンからブルーへのグラデーションと波のきらめき視点
        cam.transform.position = new Vector3(150f, 7.5f, 260f);
        cam.transform.rotation = Quaternion.Euler(5f, -80f, 0f);
        cam.Render();
        SaveTexture(rt, Path.Combine(outDir, "beach_emerald_sparkle_view.png"));

        cam.targetTexture = null;
        RenderTexture.active = null;
        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGo);

        Debug.Log("<color=#00ffc8><b>[AdventureCurvedOcean]</b> 最新の3アングル検証スクリーンショットを保存しました！</color>");
    }

    private static void SaveTexture(RenderTexture rt, string fullPath)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }
}
