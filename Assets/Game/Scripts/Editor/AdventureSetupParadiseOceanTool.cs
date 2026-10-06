using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

public static class AdventureSetupParadiseOceanTool
{
    private const string MaterialPath = "Assets/RustAndFloat/Materials/ParadiseCurvedOcean.mat";
    private const string MeshPath = "Assets/RustAndFloat/Terrain/ParadiseCurvedOceanMesh.asset";
    private const string ShaderName = "RustAndFloat/ParadiseCurvedOcean";

    [MenuItem("Adventure/🌊 Setup Beautiful Curved Horizon Ocean (美しい曲線の水平線・エメラルドオーシャン構築)", false, 10)]
    public static void SetupParadiseOcean()
    {
        // 1. マテリアルの生成または更新
        var mat = EnsureMaterialAsset();

        // 2. ラジアル曲率メッシュの生成またはアセット化
        var mesh = EnsureMeshAsset();

        // 3. シーン内の OceanPlane を取得または作成
        var oceanGo = GameObject.Find(AdventureCurvedHorizonOcean.OceanGameObjectName);
        if (oceanGo == null)
        {
            oceanGo = new GameObject(AdventureCurvedHorizonOcean.OceanGameObjectName);
            Undo.RegisterCreatedObjectUndo(oceanGo, "Create OceanPlane");
        }

        Undo.RecordObject(oceanGo.transform, "Setup Ocean Transform");
        oceanGo.transform.position = new Vector3(512f, AdventureCurvedHorizonOcean.OceanCenterY, 512f);
        oceanGo.transform.rotation = Quaternion.identity;
        oceanGo.transform.localScale = Vector3.one;

        // 不要なコライダーを除去
        var col = oceanGo.GetComponent<Collider>();
        if (col != null) Undo.DestroyObjectImmediate(col);

        var filter = oceanGo.GetComponent<MeshFilter>() ?? oceanGo.AddComponent<MeshFilter>();
        var renderer = oceanGo.GetComponent<MeshRenderer>() ?? oceanGo.AddComponent<MeshRenderer>();
        var oceanComp = oceanGo.GetComponent<AdventureCurvedHorizonOcean>() ?? oceanGo.AddComponent<AdventureCurvedHorizonOcean>();

        Undo.RecordObject(filter, "Assign Ocean Mesh");
        filter.sharedMesh = mesh;

        Undo.RecordObject(renderer, "Assign Ocean Material");
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = true;

        // 4. シーン内全カメラの farClipPlane を 5000m に拡張
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

        Debug.Log("<color=#00ffc8><b>[AdventureCurvedHorizonOcean]</b> 美しい曲線の水平線・エメラルド〜ブルーグラデーション海面を構築＆保存しました！（直径9km / 4500mラジアル）</color>");
    }

    private static Material EnsureMaterialAsset()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError("[AdventureSetupParadiseOceanTool] Shader not found: " + ShaderName);
            return null;
        }

        if (mat == null)
        {
            mat = new Material(shader);
            mat.name = "ParadiseCurvedOcean";
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        else
        {
            mat.shader = shader;
        }

        // ノーマルマップの割り当て
        var n1 = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Idyllic Fantasy Nature/Textures/Water/Water_Normal_01.png");
        var n2 = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Idyllic Fantasy Nature/Textures/Water/Water_Normal_02.png");
        if (n1 != null) mat.SetTexture("_NormalMap1", n1);
        if (n2 != null) mat.SetTexture("_NormalMap2", n2);

        // 美しい色彩パレット（鮮やかなクリスタルエメラルド 〜 ターコイズ 〜 ディープエメラルド 〜 水平線ミント）
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
        mat.SetFloat("_SunGlitterIntensity", 5.2f);
        mat.SetFloat("_SunGlitterExponent", 80f);
        mat.SetFloat("_SparkleScale", 1.8f);

        mat.SetVector("_IslandCenter", new Vector4(512f, 5.5f, 512f, 0f));
        mat.SetFloat("_ShallowRadius", 360f);
        mat.SetFloat("_DeepRadius", 800f);
        mat.SetFloat("_HorizonRadius", 4500f);
        mat.SetFloat("_CurvatureAmount", 22.0f);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    private static Mesh EnsureMeshAsset()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (mesh == null)
        {
            mesh = AdventureCurvedHorizonOcean.GenerateRadialCurvedOceanMesh();
            AssetDatabase.CreateAsset(mesh, MeshPath);
            AssetDatabase.SaveAssets();
        }
        return mesh;
    }

    [MenuItem("Adventure/📸 Capture Ending & Horizon Ocean Views (エンディング上空＆水平線撮影)", false, 25)]
    public static void CaptureEndingOceanViews()
    {
        // 撮影前に最新の海面・メッシュ・マテリアル・描画距離を確実に適用
        SetupParadiseOcean();

        string outDir = "/Users/user/.gemini/antigravity-ide/brain/ed566f25-4713-4c09-bcc2-4dcd6bfc0123";
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        var camGo = new GameObject("TempEndingOceanCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 65f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 5000f; // 5000mまで描画

        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        // 1. エンディング上空（Y:150m）から西ビーチと彼方のなだらかな曲線の水平線を見下ろす視点
        cam.transform.position = new Vector3(320f, 150f, 320f);
        cam.transform.rotation = Quaternion.Euler(18f, 235f, 0f);
        cam.Render();
        SaveTexture(rt, Path.Combine(outDir, "ending_sky_horizon_ocean.png"));

        // 2. さらに高空（Y:220m）から島全体と360度広がる外洋水平線を見渡すパノラマ視点
        cam.transform.position = new Vector3(512f, 220f, 200f);
        cam.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
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

        Debug.Log("<color=#00ffc8><b>[AdventureSetupParadiseOceanTool]</b> エンディング上空＆水平線の3枚の撮影が完了しました！</color>");
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
        Debug.Log($"[AdventureSetupParadiseOceanTool] Saved screenshot: {filePath}");
    }
}
