using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;

/// <summary>
/// 楽園の砂浜デコレーション一括構築エディタ拡張
/// ・波打ち際のリアルな寄せ波（寄せては返す白波アニメーション）
/// ・太陽にきらめく海の煌めき（サン・グリッター）
/// ・寄せる波の環境音（波打ち際3Dオーディオ）
/// ・砂浜の波打ち際を泳ぐ小魚の群れ（浅瀬遊泳＆プレイヤー回避AI）
/// ・舞い飛ぶ蝶々（低木や花壇の上をひらひら舞う）
/// ・舞い飛ぶトンボ（水面や草むらをホバリング＆直線飛翔）
/// ・南国のヤシの木（白砂ビーチ沿いに海へ傾く美しいパームツリー）
/// ・砂浜に転がるヤシの実（ココナッツ）
/// ・砂浜のウミネコ（カモメ、佇み＆羽づくろい＆鳴き声離陸）
/// ・砂浜を横歩きする可愛いカニたち
/// ・白砂に散らばる美しい貝殻（サクラガイ、ホタテ、巻き貝、ヒトデ）
/// </summary>
public static class AdventureParadiseBeachDecor
{
    private const string ScenePath = "Assets/RustAndFloat/Scenes/RustAndFloat.unity";

    [MenuItem("Adventure/✨ Decorate Paradise Beach (楽園の砂浜・波・ウミネコ・カニ・貝殻の一括生成)", false, 14)]
    public static void DecorateParadiseBeachMenu()
    {
        ExecuteDecorate(true);
    }

    public static void ExecuteDecorate(bool interactive)
    {
        if (EditorApplication.isPlaying)
        {
            if (interactive)
                EditorUtility.DisplayDialog("停止してください", "■で再生を止めてから実行してください。", "OK");
            return;
        }

        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != ScenePath)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var land = Terrain.activeTerrain;
        if (land == null)
        {
            Debug.LogError("[ParadiseBeachDecor] Terrainが見つかりません。");
            return;
        }

        // 既存の古いデコレーションホルダーがあればクリーンアップ
        var oldDecor = GameObject.Find("ParadiseBeach_Decorations");
        if (oldDecor != null)
        {
            Undo.DestroyObjectImmediate(oldDecor);
        }

        var root = new GameObject("ParadiseBeach_Decorations");
        Undo.RegisterCreatedObjectUndo(root, "Create ParadiseBeach_Decorations");

        var rng = new System.Random(777);

        // 1. マテリアル群の準備
        var urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // 貝殻マテリアル
        var sakuraMat = MakeMat(urpLit, new Color(0.98f, 0.72f, 0.78f), 0.85f);  // サクラガイ（淡い桜ピンク・真珠光沢）
        var whiteShellMat = MakeMat(urpLit, new Color(0.96f, 0.94f, 0.90f), 0.88f); // ホタテ貝（アイボリー真珠色）
        var spiralMat = MakeMat(urpLit, new Color(0.92f, 0.85f, 0.75f), 0.75f);  // 巻き貝（砂ベージュ）
        var starfishMat = MakeMat(urpLit, new Color(0.96f, 0.42f, 0.20f), 0.35f); // ヒトデ（鮮やかな珊瑚オレンジ）

        // ウミネコマテリアル
        var birdBodyMat = MakeMat(urpLit, new Color(0.98f, 0.98f, 1.0f), 0.4f);
        var birdBeakMat = MakeMat(urpLit, new Color(1.0f, 0.78f, 0.05f), 0.5f);
        var birdWingMat = MakeMat(urpLit, new Color(0.45f, 0.48f, 0.52f), 0.4f);

        // カニマテリアル
        var crabRedMat = MakeMat(urpLit, new Color(0.92f, 0.25f, 0.15f), 0.65f);
        var crabOrangeMat = MakeMat(urpLit, new Color(0.96f, 0.48f, 0.12f), 0.65f);
        var crabEyeMat = MakeMat(urpLit, new Color(0.08f, 0.08f, 0.10f), 0.9f);

        // 小魚マテリアル（シアン＆シルバーの熱帯魚）
        var fishMat = MakeMetallicMat(urpLit, new Color(0.40f, 0.85f, 0.95f), 0.7f, 0.88f);

        // ヤシの実（ココナッツ）マテリアル
        var coconutMat = MakeMat(urpLit, new Color(0.28f, 0.16f, 0.08f), 0.35f);

        // トンボ胴体＆羽マテリアル
        var dragonflyBodyMat = MakeMetallicMat(urpLit, new Color(0.10f, 0.82f, 0.88f), 0.4f, 0.85f);
        var dragonflyWingMat = MakeTransparentMat(urpLit, new Color(0.95f, 1.0f, 1.0f, 0.40f), 0.95f);

        // ====================================================================
        // B. 寄せる波の環境音（Ambient Wave Sound）
        // ====================================================================
        CreateOceanAmbientSound(root.transform);

        // ====================================================================
        // C. 海の煌めきパーティクル（Sun Glitter）
        // ====================================================================
        CreateSunGlitterVFX(root.transform);

        // ====================================================================
        // D. 浅瀬を泳ぐ小魚の群れ（Shallow Fish Schools）
        // ====================================================================
        CreateFishSchools(root.transform, fishMat);

        // ====================================================================
        // E. 飛んでいる蝶々（Butterflies）
        // ====================================================================
        CreateButterflies(root.transform);

        // ====================================================================
        // F. 飛んでいるトンボ（Dragonflies）
        // ====================================================================
        CreateDragonflies(root.transform, dragonflyBodyMat, dragonflyWingMat);

        // ====================================================================
        // G. 南国のヤシの木（樹上にヤシの実の房付き）
        // ====================================================================
        var freshCoconutMat = MakeMat(urpLit, new Color(0.48f, 0.68f, 0.22f), 0.65f); // 南国のフレッシュな緑のヤシの実
        CreatePalmTrees(root.transform, land, freshCoconutMat);

        // ====================================================================
        // I. 砂浜のウミネコ（Seagulls）
        // ====================================================================
        var gullsHolder = new GameObject("BeachSeagulls");
        gullsHolder.transform.SetParent(root.transform, false);
        var seagullClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/AudioFiles/06_birds/seagulls_1.wav");

        Vector3[] gullPositions = new Vector3[]
        {
            new Vector3(152f, 5.58f, 260f),
            new Vector3(149f, 5.56f, 272f),
            new Vector3(155f, 5.62f, 285f),
            new Vector3(151f, 5.58f, 248f),
            new Vector3(148f, 5.55f, 298f),
        };

        for (int i = 0; i < gullPositions.Length; i++)
        {
            Vector3 p = gullPositions[i];
            p.y = land.SampleHeight(p) + 0.01f;
            CreateBeachSeagull(gullsHolder.transform, p, birdBodyMat, birdBeakMat, birdWingMat, seagullClip, rng);
        }

        // ====================================================================
        // J. 砂浜を横歩きする可愛いカニたち（Crabs）
        // ====================================================================
        var crabsHolder = new GameObject("BeachCrabs");
        crabsHolder.transform.SetParent(root.transform, false);

        for (int i = 0; i < 14; i++)
        {
            float z = 235f + (float)rng.NextDouble() * 85f;
            float minX = FindShorelineX(land, z, 5.52f);
            float maxX = minX + 16f;
            float x = Mathf.Lerp(minX, maxX, (float)rng.NextDouble());

            Vector3 pos = new Vector3(x, 0f, z);
            pos.y = land.SampleHeight(pos) + 0.02f;

            var mat = (i % 2 == 0) ? crabRedMat : crabOrangeMat;
            CreateCuteCrab(crabsHolder.transform, pos, mat, crabEyeMat, rng);
        }

        // ====================================================================
        // K. 白砂に散らばる美しい貝殻とヒトデ（Seashells & Starfish）
        // ====================================================================
        var shellsHolder = new GameObject("BeachSeashells");
        shellsHolder.transform.SetParent(root.transform, false);

        for (int i = 0; i < 42; i++)
        {
            float z = 240f + (float)rng.NextDouble() * 75f;
            float minX = FindShorelineX(land, z, 5.52f);
            float maxX = minX + 22f;
            float x = Mathf.Lerp(minX, maxX, (float)rng.NextDouble());

            Vector3 pos = new Vector3(x, 0f, z);
            pos.y = land.SampleHeight(pos);

            int type = i % 4;
            if (type == 0)
                CreateSakuraShell(shellsHolder.transform, pos, sakuraMat, rng);
            else if (type == 1)
                CreateScallopShell(shellsHolder.transform, pos, whiteShellMat, rng);
            else if (type == 2)
                CreateSpiralShell(shellsHolder.transform, pos, spiralMat, rng);
            else
                CreateStarfish(shellsHolder.transform, pos, starfishMat, rng);
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();

        Debug.Log("<color=#00FFAA><b>[ParadiseBeachDecor]</b> 楽園の砂浜（小魚・蝶々・トンボ・波の音・ヤシの木・ヤシの実・ウミネコ・カニ・貝殻）の一括生成・配置が完了しました！</color>");
    }

    private static float FindShorelineX(Terrain land, float z, float targetY)
    {
        for (float x = 160f; x >= 120f; x -= 0.5f)
        {
            float y = land.SampleHeight(new Vector3(x, 0f, z));
            if (y <= targetY) return x;
        }
        return 142f;
    }

    private static void CreateOceanAmbientSound(Transform parent)
    {
        var audioGo = new GameObject("OceanWaves_AmbientSound");
        audioGo.transform.SetParent(parent, false);
        audioGo.transform.position = new Vector3(150f, 5.5f, 268f); // ビーチ中央の汀線

        var waveClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RustAndFloat/Audio/Ambience/ocean_waves_grand.wav");
        var shoreClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/AudioFiles/03_amb/watershore_amb.wav");

        if (waveClip != null)
        {
            var src1 = audioGo.AddComponent<AudioSource>();
            src1.clip = waveClip;
            src1.loop = true;
            src1.playOnAwake = true;
            src1.volume = 0.55f;
            src1.spatialBlend = 0.65f;
            src1.minDistance = 6f;
            src1.maxDistance = 55f;
            src1.rolloffMode = AudioRolloffMode.Logarithmic;
        }

        if (shoreClip != null)
        {
            var childGo = new GameObject("WaterShore_Detail");
            childGo.transform.SetParent(audioGo.transform, false);
            var src2 = childGo.AddComponent<AudioSource>();
            src2.clip = shoreClip;
            src2.loop = true;
            src2.playOnAwake = true;
            src2.volume = 0.35f;
            src2.spatialBlend = 0.5f;
            src2.minDistance = 4f;
            src2.maxDistance = 40f;
        }
    }

    private static void CreateFishSchools(Transform parent, Material fishMat)
    {
        var fishesHolder = new GameObject("ShallowFish_Schools");
        fishesHolder.transform.SetParent(parent, false);

        Vector3[] schoolCenters = new Vector3[]
        {
            new Vector3(145f, 5.15f, 258f), // 南寄り浅瀬
            new Vector3(143f, 5.15f, 274f), // 中央浅瀬
            new Vector3(144f, 5.15f, 290f), // 北寄り浅瀬
        };

        for (int i = 0; i < schoolCenters.Length; i++)
        {
            var schoolGo = new GameObject($"FishSchool_{i + 1}");
            schoolGo.transform.SetParent(fishesHolder.transform, false);
            var school = schoolGo.AddComponent<AdventureShallowFishSchool>();
            school.Initialize(fishMat, schoolCenters[i], 7);
        }
    }

    private static void CreateButterflies(Transform parent)
    {
        var holder = new GameObject("BeachButterflies");
        holder.transform.SetParent(parent, false);

        string[] prefabs = new string[]
        {
            "Assets/Idyllic Fantasy Nature/Prefabs/Code Related/Butterfly_01.prefab",
            "Assets/Idyllic Fantasy Nature/Prefabs/Code Related/Butterfly_02.prefab",
            "Assets/Idyllic Fantasy Nature/Prefabs/Code Related/Butterfly_03.prefab"
        };

        Vector3[] spawnPositions = new Vector3[]
        {
            new Vector3(158f, 6.3f, 262f),
            new Vector3(162f, 6.6f, 274f),
            new Vector3(156f, 6.2f, 282f),
            new Vector3(164f, 6.8f, 250f),
            new Vector3(160f, 6.4f, 295f)
        };

        for (int i = 0; i < spawnPositions.Length; i++)
        {
            var pPath = prefabs[i % prefabs.Length];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(pPath);
            if (prefab == null) continue;

            var b = Object.Instantiate(prefab, spawnPositions[i], Quaternion.Euler(0f, i * 72f, 0f), holder.transform);
            b.name = $"Butterfly_{i + 1:D2}";
            b.transform.localScale = Vector3.one * 1.3f;
        }
    }

    private static void CreateDragonflies(Transform parent, Material bodyMat, Material wingMat)
    {
        var holder = new GameObject("BeachDragonflies");
        holder.transform.SetParent(parent, false);

        Vector3[] spawnPositions = new Vector3[]
        {
            new Vector3(149f, 6.1f, 255f),
            new Vector3(147f, 6.0f, 270f),
            new Vector3(151f, 6.2f, 285f),
            new Vector3(146f, 5.9f, 298f),
        };

        for (int i = 0; i < spawnPositions.Length; i++)
        {
            var dfGo = new GameObject($"Dragonfly_{i + 1:D2}");
            dfGo.transform.SetParent(holder.transform, false);
            dfGo.transform.position = spawnPositions[i];

            // 胴体（Body: 細長いエメラルド）
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(dfGo.transform, false);
            body.transform.localScale = new Vector3(0.04f, 0.22f, 0.04f);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.GetComponent<Renderer>().sharedMaterial = bodyMat;
            Object.DestroyImmediate(body.GetComponent<Collider>());

            // 頭部（Head: 丸い頭）
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(dfGo.transform, false);
            head.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
            head.transform.localPosition = new Vector3(0f, 0f, 0.24f);
            head.GetComponent<Renderer>().sharedMaterial = bodyMat;
            Object.DestroyImmediate(head.GetComponent<Collider>());

            // 左右の羽（Wings: 2対・半透明）
            var leftWingPivot = new GameObject("LeftWings");
            leftWingPivot.transform.SetParent(dfGo.transform, false);
            leftWingPivot.transform.localPosition = new Vector3(0.04f, 0.02f, 0.08f);

            var lw1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lw1.transform.SetParent(leftWingPivot.transform, false);
            lw1.transform.localPosition = new Vector3(0.22f, 0f, 0.03f);
            lw1.transform.localScale = new Vector3(0.42f, 0.005f, 0.08f);
            lw1.GetComponent<Renderer>().sharedMaterial = wingMat;
            Object.DestroyImmediate(lw1.GetComponent<Collider>());

            var lw2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lw2.transform.SetParent(leftWingPivot.transform, false);
            lw2.transform.localPosition = new Vector3(0.18f, 0f, -0.05f);
            lw2.transform.localScale = new Vector3(0.34f, 0.005f, 0.07f);
            lw2.GetComponent<Renderer>().sharedMaterial = wingMat;
            Object.DestroyImmediate(lw2.GetComponent<Collider>());

            var rightWingPivot = new GameObject("RightWings");
            rightWingPivot.transform.SetParent(dfGo.transform, false);
            rightWingPivot.transform.localPosition = new Vector3(-0.04f, 0.02f, 0.08f);

            var rw1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rw1.transform.SetParent(rightWingPivot.transform, false);
            rw1.transform.localPosition = new Vector3(-0.22f, 0f, 0.03f);
            rw1.transform.localScale = new Vector3(0.42f, 0.005f, 0.08f);
            rw1.GetComponent<Renderer>().sharedMaterial = wingMat;
            Object.DestroyImmediate(rw1.GetComponent<Collider>());

            var rw2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rw2.transform.SetParent(rightWingPivot.transform, false);
            rw2.transform.localPosition = new Vector3(-0.18f, 0f, -0.05f);
            rw2.transform.localScale = new Vector3(0.34f, 0.005f, 0.07f);
            rw2.GetComponent<Renderer>().sharedMaterial = wingMat;
            Object.DestroyImmediate(rw2.GetComponent<Collider>());

            var df = dfGo.AddComponent<AdventureDragonfly>();
            df.Setup(leftWingPivot.transform, rightWingPivot.transform, spawnPositions[i]);
        }
    }

    private static void CreatePalmTrees(Transform parent, Terrain land, Material coconutMat)
    {
        var holder = new GameObject("BeachPalmTrees");
        holder.transform.SetParent(parent, false);

        // 既存のHawaii_Palmメッシュとマテリアルをロード
        var palmMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Hawaii Beach House (PBR, HDRP)/Models/Jungle_Plant_10.fbx");
        var palmMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RustAndFloat/Materials/HawaiiPalm_URP.mat");

        Vector3[] palmPositions = new Vector3[]
        {
            new Vector3(162f, 0f, 252f),
            new Vector3(166f, 0f, 266f),
            new Vector3(163f, 0f, 280f),
            new Vector3(168f, 0f, 294f),
            new Vector3(160f, 0f, 308f),
        };

        float[] palmTilts = new float[] { 14f, 18f, 12f, 16f, 15f };
        float[] palmScales = new float[] { 1.15f, 1.25f, 1.05f, 1.20f, 1.10f };

        for (int i = 0; i < palmPositions.Length; i++)
        {
            var p = palmPositions[i];
            p.y = land.SampleHeight(p) - 0.1f;

            var treeGo = new GameObject($"ParadisePalm_{i + 1:D2}");
            treeGo.transform.SetParent(holder.transform, false);
            treeGo.transform.position = p;
            // 海側（西）へ少し傾いた南国らしい優雅な角度
            treeGo.transform.rotation = Quaternion.Euler(0f, -70f + i * 25f, palmTilts[i]);
            treeGo.transform.localScale = Vector3.one * palmScales[i];

            var mf = treeGo.AddComponent<MeshFilter>();
            mf.sharedMesh = palmMesh;
            var mr = treeGo.AddComponent<MeshRenderer>();
            if (palmMat != null) mr.sharedMaterial = palmMat;

            // ヤシの木の頭頂・葉元にフレッシュな緑のヤシの実（ココナッツの房）を3〜4個配置
            var clusterGo = new GameObject("CoconutCluster");
            clusterGo.transform.SetParent(treeGo.transform, false);
            clusterGo.transform.localPosition = new Vector3(0f, 2.05f, 0f);

            for (int c = 0; c < 3; c++)
            {
                float ang = c * 120f * Mathf.Deg2Rad;
                var coco = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                coco.name = $"TreeCoconut_{c + 1}";
                coco.transform.SetParent(clusterGo.transform, false);
                coco.transform.localPosition = new Vector3(Mathf.Cos(ang) * 0.16f, -0.04f * c, Mathf.Sin(ang) * 0.16f);
                coco.transform.localScale = new Vector3(0.18f, 0.24f, 0.18f); // 瑞々しい緑のココナッツ
                coco.GetComponent<Renderer>().sharedMaterial = coconutMat;
                Object.DestroyImmediate(coco.GetComponent<Collider>());
            }
        }
    }

    private static void CreateBeachSeagull(Transform parent, Vector3 pos, Material bodyMat, Material beakMat, Material wingMat, AudioClip cryClip, System.Random rng)
    {
        var gull = new GameObject("BeachSeagull");
        gull.transform.SetParent(parent, false);
        gull.transform.position = pos;
        gull.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "Body";
        body.transform.SetParent(gull.transform, false);
        body.transform.localScale = new Vector3(0.24f, 0.22f, 0.45f);
        body.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        body.GetComponent<Renderer>().sharedMaterial = bodyMat;
        Object.DestroyImmediate(body.GetComponent<Collider>());

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(gull.transform, false);
        head.transform.localScale = new Vector3(0.18f, 0.18f, 0.22f);
        head.transform.localPosition = new Vector3(0f, 0.24f, 0.18f);
        head.GetComponent<Renderer>().sharedMaterial = bodyMat;
        Object.DestroyImmediate(head.GetComponent<Collider>());

        var beak = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beak.name = "Beak";
        beak.transform.SetParent(head.transform, false);
        beak.transform.localScale = new Vector3(0.06f, 0.05f, 0.22f);
        beak.transform.localPosition = new Vector3(0f, -0.02f, 0.16f);
        beak.GetComponent<Renderer>().sharedMaterial = beakMat;
        Object.DestroyImmediate(beak.GetComponent<Collider>());

        for (int side = -1; side <= 1; side += 2)
        {
            var wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wing.name = side < 0 ? "LeftWing" : "RightWing";
            wing.transform.SetParent(gull.transform, false);
            wing.transform.localScale = new Vector3(0.04f, 0.18f, 0.42f);
            wing.transform.localPosition = new Vector3(side * 0.13f, 0.14f, -0.05f);
            wing.transform.localRotation = Quaternion.Euler(0f, 0f, side * -12f);
            wing.GetComponent<Renderer>().sharedMaterial = wingMat;
            Object.DestroyImmediate(wing.GetComponent<Collider>());
        }

        var seagullScript = gull.AddComponent<AdventureBeachSeagull>();
        if (cryClip != null)
        {
            seagullScript.SetCryClip(cryClip);
        }
    }

    private static void CreateCuteCrab(Transform parent, Vector3 pos, Material crabMat, Material eyeMat, System.Random rng)
    {
        var crab = new GameObject("BeachCrab");
        crab.transform.SetParent(parent, false);
        crab.transform.position = pos;
        crab.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "Shell";
        body.transform.SetParent(crab.transform, false);
        body.transform.localScale = new Vector3(0.22f, 0.09f, 0.16f);
        body.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        body.GetComponent<Renderer>().sharedMaterial = crabMat;
        Object.DestroyImmediate(body.GetComponent<Collider>());

        for (int side = -1; side <= 1; side += 2)
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = side < 0 ? "LeftEye" : "RightEye";
            eye.transform.SetParent(crab.transform, false);
            eye.transform.localScale = new Vector3(0.035f, 0.05f, 0.035f);
            eye.transform.localPosition = new Vector3(side * 0.05f, 0.11f, 0.06f);
            eye.GetComponent<Renderer>().sharedMaterial = eyeMat;
            Object.DestroyImmediate(eye.GetComponent<Collider>());
        }

        for (int side = -1; side <= 1; side += 2)
        {
            var claw = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            claw.name = side < 0 ? "LeftClaw" : "RightClaw";
            claw.transform.SetParent(crab.transform, false);
            claw.transform.localScale = new Vector3(0.08f, 0.05f, 0.10f);
            claw.transform.localPosition = new Vector3(side * 0.14f, 0.07f, 0.10f);
            claw.transform.localRotation = Quaternion.Euler(0f, side * 30f, 0f);
            claw.GetComponent<Renderer>().sharedMaterial = crabMat;
            Object.DestroyImmediate(claw.GetComponent<Collider>());
        }

        for (int leg = -1; leg <= 1; leg++)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var legGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                legGo.name = "Leg";
                legGo.transform.SetParent(crab.transform, false);
                legGo.transform.localScale = new Vector3(0.08f, 0.02f, 0.02f);
                legGo.transform.localPosition = new Vector3(side * 0.12f, 0.025f, leg * 0.04f);
                legGo.transform.localRotation = Quaternion.Euler(0f, 0f, side * -25f);
                legGo.GetComponent<Renderer>().sharedMaterial = crabMat;
                Object.DestroyImmediate(legGo.GetComponent<Collider>());
            }
        }

        crab.AddComponent<CrabWander>();
    }

    private static void CreateSakuraShell(Transform parent, Vector3 pos, Material mat, System.Random rng)
    {
        var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shell.name = "SakuraShell";
        shell.transform.SetParent(parent, false);
        shell.transform.position = pos;
        shell.transform.localScale = new Vector3(0.12f, 0.025f, 0.14f);
        shell.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 10f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 8f);
        shell.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(shell.GetComponent<Collider>());
    }

    private static void CreateScallopShell(Transform parent, Vector3 pos, Material mat, System.Random rng)
    {
        var shell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shell.name = "ScallopShell";
        shell.transform.SetParent(parent, false);
        shell.transform.position = pos;
        shell.transform.localScale = new Vector3(0.15f, 0.018f, 0.15f);
        shell.transform.rotation = Quaternion.Euler(5f + (float)rng.NextDouble() * 12f, (float)rng.NextDouble() * 360f, 0f);
        shell.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(shell.GetComponent<Collider>());
    }

    private static void CreateSpiralShell(Transform parent, Vector3 pos, Material mat, System.Random rng)
    {
        var shell = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        shell.name = "SpiralShell";
        shell.transform.SetParent(parent, false);
        shell.transform.position = pos;
        shell.transform.localScale = new Vector3(0.06f, 0.14f, 0.06f);
        shell.transform.rotation = Quaternion.Euler(85f, (float)rng.NextDouble() * 360f, 0f);
        shell.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(shell.GetComponent<Collider>());
    }

    private static void CreateStarfish(Transform parent, Vector3 pos, Material mat, System.Random rng)
    {
        var starfish = new GameObject("Starfish");
        starfish.transform.SetParent(parent, false);
        starfish.transform.position = pos;
        starfish.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

        var center = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        center.name = "Center";
        center.transform.SetParent(starfish.transform, false);
        center.transform.localScale = new Vector3(0.07f, 0.015f, 0.07f);
        center.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(center.GetComponent<Collider>());

        for (int i = 0; i < 5; i++)
        {
            float armAngle = i * 72f;
            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = $"Arm_{i}";
            arm.transform.SetParent(starfish.transform, false);
            arm.transform.localScale = new Vector3(0.035f, 0.012f, 0.14f);
            arm.transform.localRotation = Quaternion.Euler(0f, armAngle, 0f);
            arm.transform.localPosition = Quaternion.Euler(0f, armAngle, 0f) * new Vector3(0f, 0f, 0.08f);
            arm.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(arm.GetComponent<Collider>());
        }
    }

    private static void CreateSunGlitterVFX(Transform parent)
    {
        var glitterGo = new GameObject("OceanSunGlitter");
        glitterGo.transform.SetParent(parent, false);
        glitterGo.transform.position = new Vector3(140f, 5.58f, 275f);

        var ps = glitterGo.AddComponent<ParticleSystem>();
        var psr = glitterGo.GetComponent<ParticleSystemRenderer>();
        var glitterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RustAndFloat/Materials/OceanSunGlitter.mat");
        if (glitterMat != null)
        {
            psr.sharedMaterial = glitterMat;
        }

        var main = ps.main;
        main.loop = true;
        main.startLifetime = 1.8f;
        main.startSpeed = 0.08f;
        main.startSize = 0.28f;
        main.startColor = new Color(1f, 1f, 0.95f, 0.9f);
        main.maxParticles = 120;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 32f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(35f, 0.2f, 80f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.95f, 0.8f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        var curve = new AnimationCurve();
        curve.AddKey(0f, 0.2f);
        curve.AddKey(0.5f, 1.0f);
        curve.AddKey(1f, 0.1f);
        sz.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    private static Material MakeMat(Shader shader, Color color, float smoothness)
    {
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }

    private static Material MakeMetallicMat(Shader shader, Color color, float metallic, float smoothness)
    {
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }

    private static Material MakeTransparentMat(Shader shader, Color color, float smoothness)
    {
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Surface", 1); // Transparent
        mat.SetFloat("_Blend", 0);   // Alpha
        mat.renderQueue = 3000;
        return mat;
    }
}
