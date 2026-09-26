using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 西側白砂ビーチ・座礁艇前の目の前にある段々水面（RiverSeg_14〜17）を、
/// 自然な大きさ・高さ・並びの天然岩と水草（アシ）で美化し、美しい段々池・せせらぎ渓流にするエディタ拡張。
/// </summary>
public static class AdventureBeautifyStepPonds
{
    private static readonly string[] MediumRockPaths = new string[]
    {
        "Assets/Idyllic Fantasy Nature/Prefabs/Stone_Medium_01.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Stone_Medium_02.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Stone_Medium_03.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Rock_Medium_01.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Rock_Medium_02.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Rock_Medium_03.prefab"
    };

    private static readonly string[] SmallRockPaths = new string[]
    {
        "Assets/Idyllic Fantasy Nature/Prefabs/Rock_Small_01.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Rock_Small_02.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Rock_Small_03.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Stones_01.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Stones_02.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Stones_03.prefab"
    };

    private static readonly string[] ReedsPaths = new string[]
    {
        "Assets/Idyllic Fantasy Nature/Prefabs/Reeds_01.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Reeds_02.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Reeds_03.prefab"
    };

    private static readonly string[] BushPaths = new string[]
    {
        "Assets/Idyllic Fantasy Nature/Prefabs/Bush_01_01.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Bush_02_01.prefab",
        "Assets/Idyllic Fantasy Nature/Prefabs/Bush_03_01.prefab"
    };

    [MenuItem("Adventure/Beautify Beach Step Ponds", false, 10)]
    public static void BeautifyStepPondsMenu()
    {
        ExecuteBeautify(true);
    }

    public static void ExecuteBeautify(bool interactive)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[AdventureBeautifyStepPonds] Playモードを停止してから実行してください。");
            EditorApplication.isPlaying = false;
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.isLoaded || string.IsNullOrEmpty(scene.path))
        {
            scene = EditorSceneManager.OpenScene("Assets/RustAndFloat/Scenes/RustAndFloat.unity", OpenSceneMode.Single);
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("[AdventureBeautifyStepPonds] Terrainが見つかりません。");
            return;
        }

        // プレハブのロード
        var medRocks = LoadPrefabs(MediumRockPaths);
        var smallRocks = LoadPrefabs(SmallRockPaths);
        var reeds = LoadPrefabs(ReedsPaths);
        var bushes = LoadPrefabs(BushPaths);

        // 1. 以前作成した岩ホルダーのクリーンアップ
        CleanupOldHolder("BeachStepPonds_Rocks");
        CleanupOldHolder("RiverSeg_15_Rocks");
        CleanupOldHolder("RiverSeg_16_Rocks");

        // 2. 海の上に浮いていたセグメントを完全に無効化
        var seg19 = GameObject.Find("RiverSeg_19");
        if (seg19 != null)
        {
            Undo.RecordObject(seg19, "Disable RiverSeg_19");
            seg19.SetActive(false);
        }

        var seg18 = GameObject.Find("RiverSeg_18");
        if (seg18 != null)
        {
            Undo.RecordObject(seg18, "Disable RiverSeg_18");
            seg18.SetActive(false);
        }

        // 3. 最下流セグメント RiverSeg_17 を砂浜の波打ち際の内側に完全に収める
        var seg17 = GameObject.Find("RiverSeg_17");
        if (seg17 != null)
        {
            Undo.RecordObject(seg17, "Adjust RiverSeg_17 for Beach Shore");
            // 陸側（砂浜側）へ寄せて長さを6m（scale.z=0.6）にし、先端が海に飛び出さないよう陸地内に着地
            seg17.transform.position = new Vector3(196.0f, 7.95f, 195.0f);
            seg17.transform.rotation = Quaternion.Euler(0f, 226f, 0f);
            seg17.transform.localScale = new Vector3(1.5f, 1f, 0.65f);
            seg17.SetActive(true);
        }

        // 4. ルートホルダーの作成
        GameObject rootHolder = new GameObject("BeachStepPonds_Rocks");
        rootHolder.transform.position = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(rootHolder, "Create BeachStepPonds_Rocks");

        Random.InitState(12345);

        // 対象段々水面セグメント：RiverSeg_14（崖下上段）〜 RiverSeg_17（砂浜最下段）
        int startSeg = 14;
        int endSeg = 17;

        for (int s = startSeg; s <= endSeg; s++)
        {
            string segName = "RiverSeg_" + s;
            GameObject segGo = GameObject.Find(segName);
            if (segGo == null || !segGo.activeInHierarchy) continue;

            Vector3 segPos = segGo.transform.position;
            Quaternion segRot = segGo.transform.rotation;
            Vector3 segScale = segGo.transform.lossyScale;

            float halfWidth = segScale.x * 5.0f;   // Planeの標準幅(10m) * scale.x / 2
            float halfLength = segScale.z * 5.0f;  // Planeの標準長(10m) * scale.z / 2

            // ========================================================
            // A. 左岸・右岸の自然な岩組み（護岸石組み＋水草＋低木）
            // 巨大化させず、自然な0.7〜1.0倍サイズで隙間なく並べる
            // ========================================================
            for (int side = -1; side <= 1; side += 2)
            {
                float stepZ = 1.3f;
                int countAlongZ = Mathf.Max(3, Mathf.RoundToInt(halfLength * 2f / stepZ));

                for (int i = 0; i <= countAlongZ; i++)
                {
                    float t = (float)i / countAlongZ;
                    float localZ = Mathf.Lerp(-halfLength + 0.2f, halfLength - 0.2f, t);

                    // 1列目：水際護岸岩（水面のフチを直接カバーして人工的な直線を消す）
                    float localX1 = side * (halfWidth - Random.Range(0.1f, 0.45f));
                    SpawnRock(rootHolder.transform, terrain, segPos, segRot, localX1, localZ, medRocks, smallRocks, 0.65f, 0.85f);

                    // 2列目：土手側岩（自然な厚みと起伏を作る）
                    float localX2 = side * (halfWidth + Random.Range(0.35f, 0.85f));
                    float localZ2 = localZ + Random.Range(-0.25f, 0.25f);
                    SpawnRock(rootHolder.transform, terrain, segPos, segRot, localX2, localZ2, medRocks, smallRocks, 0.60f, 0.80f);

                    // 水草（アシ）の配置
                    if (reeds.Count > 0 && Random.value < 0.4f)
                    {
                        float reedLocalX = side * (halfWidth + Random.Range(0.1f, 0.6f));
                        float reedLocalZ = localZ + Random.Range(-0.2f, 0.2f);
                        Vector3 reedWorld = segPos + segRot * new Vector3(reedLocalX, 0f, reedLocalZ);
                        float ty = terrain.SampleHeight(reedWorld) + terrain.transform.position.y;
                        if (ty >= 5.5f)
                        {
                            reedWorld.y = ty;
                            var reedPrefab = reeds[Random.Range(0, reeds.Count)];
                            var reedGo = (GameObject)PrefabUtility.InstantiatePrefab(reedPrefab, rootHolder.transform);
                            reedGo.transform.position = reedWorld;
                            reedGo.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                            reedGo.transform.localScale = Vector3.one * Random.Range(0.75f, 1.15f);
                            SnapToGround(reedGo, terrain, 0.08f);
                        }
                    }

                    // 土手の低木
                    if (bushes.Count > 0 && Random.value < 0.15f)
                    {
                        float bushLocalX = side * (halfWidth + Random.Range(0.9f, 1.6f));
                        Vector3 bushWorld = segPos + segRot * new Vector3(bushLocalX, 0f, localZ);
                        float ty = terrain.SampleHeight(bushWorld) + terrain.transform.position.y;
                        if (ty >= 5.5f)
                        {
                            bushWorld.y = ty;
                            var bushPrefab = bushes[Random.Range(0, bushes.Count)];
                            var bushGo = (GameObject)PrefabUtility.InstantiatePrefab(bushPrefab, rootHolder.transform);
                            bushGo.transform.position = bushWorld;
                            bushGo.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                            bushGo.transform.localScale = Vector3.one * Random.Range(0.65f, 0.9f);
                            SnapToGround(bushGo, terrain, 0.03f);
                        }
                    }
                }
            }

            // ========================================================
            // B. 各段の落ち口（小滝・堰：下流端 localZ = +halfLength）の横一列岩組み
            // 上の段から下の段へ流れる段差を受け止め、落差のエッジを完全に隠す
            // ========================================================
            int stepCount = 11;
            for (int j = 0; j < stepCount; j++)
            {
                float t = (float)j / (stepCount - 1);
                // 左右幅いっぱいに平たい石を並べる
                float localX = Mathf.Lerp(-halfWidth + 0.3f, halfWidth - 0.3f, t) + Random.Range(-0.15f, 0.15f);
                float localZ = halfLength - Random.Range(0.05f, 0.45f); // 落ち口エッジ

                Vector3 worldPos = segPos + segRot * new Vector3(localX, 0f, localZ);
                float groundY = terrain.SampleHeight(worldPos) + terrain.transform.position.y;
                if (groundY < 5.4f) continue;

                worldPos.y = Mathf.Max(segPos.y - 0.1f, groundY - 0.08f);

                // 平たい中型岩・小型岩を使用
                var rockPrefab = (j % 2 == 0 && medRocks.Count > 0)
                    ? medRocks[Random.Range(0, medRocks.Count)]
                    : smallRocks[Random.Range(0, smallRocks.Count)];

                // 落ち口の岩組み
                GameObject stepRock = (GameObject)PrefabUtility.InstantiatePrefab(rockPrefab, rootHolder.transform);
                stepRock.transform.position = worldPos;
                stepRock.transform.rotation = Quaternion.Euler(
                    Random.Range(-5f, 5f),
                    Random.Range(0f, 360f),
                    Random.Range(-5f, 5f)
                );
                // 自然なサイズ（0.70〜0.95倍）
                stepRock.transform.localScale = Vector3.one * Random.Range(0.70f, 0.95f);
                SnapToGround(stepRock, terrain, 0.05f);
            }

            // ========================================================
            // C. 段々池の中央の飛び石・せせらぎ岩（自然な池の表情）
            // ========================================================
            int insideRockCount = Random.Range(2, 4);
            for (int k = 0; k < insideRockCount; k++)
            {
                float localX = Random.Range(-halfWidth * 0.55f, halfWidth * 0.55f);
                float localZ = Random.Range(-halfLength * 0.6f, halfLength * 0.6f);
                Vector3 worldPos = segPos + segRot * new Vector3(localX, 0f, localZ);
                float groundY = terrain.SampleHeight(worldPos) + terrain.transform.position.y;
                if (groundY < 5.4f) continue;

                var prefab = smallRocks[Random.Range(0, smallRocks.Count)];
                GameObject inRock = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rootHolder.transform);
                inRock.transform.position = worldPos;
                inRock.transform.rotation = Quaternion.Euler(
                    Random.Range(-6f, 6f),
                    Random.Range(0f, 360f),
                    Random.Range(-6f, 6f)
                );
                inRock.transform.localScale = Vector3.one * Random.Range(0.75f, 1.05f);
                SnapToGround(inRock, terrain, 0.05f);
            }

            // ========================================================
            // D. 最下流（RiverSeg_17の先端）の波打ち際フィニッシュ
            // 砂浜の波打ち際へと自然に水流が溶け込む扇状の丸石・小石敷き
            // ========================================================
            if (s == endSeg)
            {
                // 河口先端を丸く囲む帯状の石敷き
                int mouthCount = 20;
                for (int m = 0; m < mouthCount; m++)
                {
                    float t = (float)m / (mouthCount - 1);
                    float localX = Mathf.Lerp(-halfWidth - 0.8f, halfWidth + 0.8f, t) + Random.Range(-0.3f, 0.3f);
                    float localZ = halfLength + Random.Range(0.0f, 1.2f); // 砂浜の波打ち際

                    Vector3 worldPos = segPos + segRot * new Vector3(localX, 0f, localZ);
                    float groundY = terrain.SampleHeight(worldPos) + terrain.transform.position.y;
                    if (groundY < 5.4f) continue;

                    var mPrefab = smallRocks[Random.Range(0, smallRocks.Count)];
                    GameObject mRock = (GameObject)PrefabUtility.InstantiatePrefab(mPrefab, rootHolder.transform);
                    mRock.transform.position = worldPos;
                    mRock.transform.rotation = Quaternion.Euler(
                        Random.Range(-8f, 8f),
                        Random.Range(0f, 360f),
                        Random.Range(-8f, 8f)
                    );
                    mRock.transform.localScale = Vector3.one * Random.Range(0.75f, 1.15f);
                    SnapToGround(mRock, terrain, 0.05f);
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("<color=#00FFAA><b>[AdventureBeautifyStepPonds]</b> 白砂ビーチ前段々池（RiverSeg_14〜17）の両岸・落ち口・渚の岩組み美化が完了し、正常に保存しました！</color>");

        // Sceneビューを白砂ビーチ（座礁艇前）から段々水面を見渡すベストアングルに設定
        var sv = SceneView.lastActiveSceneView;
        if (sv != null)
        {
            sv.pivot = new Vector3(196f, 9.5f, 208f);
            sv.rotation = Quaternion.Euler(20f, 38f, 0f);
            sv.size = 28f;
            sv.Repaint();
        }

        if (interactive)
        {
            EditorUtility.DisplayDialog("段々池の美化完了", "白砂ビーチ前の段々池（RiverSeg_14〜17）の左右両岸・段差堰・渚の岩組みと水草の配置が完了しました！\n海上の突き出し・影も完全に解消されています。", "OK");
        }
    }

    private static void SpawnRock(Transform parent, Terrain terrain, Vector3 segPos, Quaternion segRot,
        float localX, float localZ, List<GameObject> med, List<GameObject> small, float minS, float maxS)
    {
        Vector3 worldPos = segPos + segRot * new Vector3(localX, 0f, localZ);
        float groundY = terrain.SampleHeight(worldPos) + terrain.transform.position.y;
        if (groundY < 5.4f) return;

        var list = (Random.value < 0.65f && med.Count > 0) ? med : small;
        var prefab = list[Random.Range(0, list.Count)];

        GameObject rock = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        rock.transform.position = worldPos;
        rock.transform.rotation = Quaternion.Euler(
            Random.Range(-8f, 8f),
            Random.Range(0f, 360f),
            Random.Range(-8f, 8f)
        );
        rock.transform.localScale = Vector3.one * Random.Range(minS, maxS);
        SnapToGround(rock, terrain, 0.05f);
    }

    private static void SnapToGround(GameObject go, Terrain terrain, float embed)
    {
        if (go == null || terrain == null) return;
        
        // 複合プレハブ（Stones_01/02/03など）の場合は各小石を地形に密着
        if (go.transform.childCount > 0 && go.name.StartsWith("Stones_"))
        {
            for (int c = 0; c < go.transform.childCount; c++)
            {
                var child = go.transform.GetChild(c);
                SnapSingleObjectToGround(child.gameObject, terrain, embed);
            }
        }
        else
        {
            SnapSingleObjectToGround(go, terrain, embed);
        }
    }

    private static void SnapSingleObjectToGround(GameObject go, Terrain terrain, float embed)
    {
        var rends = go.GetComponentsInChildren<MeshRenderer>(true);
        if (rends == null || rends.Length == 0) return;

        float minY = float.MaxValue;
        foreach (var r in rends)
        {
            if (r.bounds.extents.sqrMagnitude > 0.001f && r.bounds.min.y < minY)
                minY = r.bounds.min.y;
        }
        if (minY == float.MaxValue) return;

        float gy = terrain.SampleHeight(go.transform.position) + terrain.transform.position.y;
        float deltaY = (gy - embed) - minY;
        go.transform.position = new Vector3(go.transform.position.x, go.transform.position.y + deltaY, go.transform.position.z);
    }

    private static void CleanupOldHolder(string name)
    {
        GameObject holder = GameObject.Find(name);
        if (holder != null)
        {
            Undo.DestroyObjectImmediate(holder);
        }
    }

    private static List<GameObject> LoadPrefabs(string[] paths)
    {
        var list = new List<GameObject>();
        foreach (var p in paths)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go != null) list.Add(go);
        }
        return list;
    }
}
