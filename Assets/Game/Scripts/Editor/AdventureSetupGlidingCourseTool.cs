using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// 絶景スカイダイブ＆風のリングくぐり（滑空タイムアタック）コースを
/// シーン内に自動生成・配置するエディタツール。
/// </summary>
public static class AdventureSetupGlidingCourseTool
{
    private const string ROOT_NAME = "GlidingTimeAttackCourse";

    [MenuItem("Adventure/🪂 Setup Sky Gliding Time Attack Course (滑空タイムアタックコース設置)")]
    public static void SetupCourse()
    {
        // 既存の同一ルートがあれば置換または再構築
        var existingRoot = GameObject.Find(ROOT_NAME);
        if (existingRoot != null)
        {
            Undo.DestroyObjectImmediate(existingRoot);
        }

        var courseRoot = new GameObject(ROOT_NAME);
        Undo.RegisterCreatedObjectUndo(courseRoot, "Setup Gliding Course");

        var manager = courseRoot.AddComponent<AdventureGlidingTimeAttackManager>();

        // 1. スタート地点（北の最高峰絶壁フライトデッキ）
        var startPlatform = BuildStartPlatform(courseRoot.transform);
        manager.startPlatform = startPlatform.transform;

        // 2. スタートゲート
        var startGate = BuildStartGate(courseRoot.transform);
        manager.startGate = startGate.transform;

        // 3. 10個の風のリング
        var rings = BuildCourseRings(courseRoot.transform);
        manager.courseRings = rings;

        // 4. ゴール着地ターゲット
        var finishTarget = BuildFinishTarget(courseRoot.transform);
        manager.finishTarget = finishTarget.transform;

        // シーン保存を促すダーティ付け
        EditorUtility.SetDirty(courseRoot);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"<color=#00ffc8><b>[AdventureGliding]</b> 滑空タイムアタックコースを構築しました！ (リング数: {rings.Count})</color>");
    }

    private static GameObject BuildStartPlatform(Transform parent)
    {
        var platformRoot = new GameObject("StartFlightDeck");
        platformRoot.transform.SetParent(parent, false);
        // 北の最高峰絶壁最先端エッジ (X: 508, Y: 94.0, Z: 658)
        platformRoot.transform.position = new Vector3(508f, 94.0f, 658f);
        platformRoot.transform.rotation = Quaternion.Euler(0f, 205f, 0f); // 南西向き（西ビーチ方向へ飛び出す）

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // 絶壁から空中へせり出す飛び出しデッキ（長さ11m、幅5.5m）
        var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        deck.name = "DeckPlanks";
        deck.transform.SetParent(platformRoot.transform, false);
        deck.transform.localPosition = new Vector3(0f, -0.2f, 5.0f);
        deck.transform.localScale = new Vector3(5.5f, 0.45f, 11.0f);

        var deckMat = new Material(shader);
        deckMat.color = new Color(0.42f, 0.30f, 0.20f); // 濃い木目調
        deck.GetComponent<Renderer>().material = deckMat;

        // デッキ先端の滑走路誘導ライン（発光シアン）
        var runway = GameObject.CreatePrimitive(PrimitiveType.Cube);
        runway.name = "RunwayStripe";
        runway.transform.SetParent(platformRoot.transform, false);
        runway.transform.localPosition = new Vector3(0f, 0.05f, 5.5f);
        runway.transform.localScale = new Vector3(0.65f, 0.08f, 9.5f);
        Object.DestroyImmediate(runway.GetComponent<Collider>());

        var runMat = new Material(shader);
        runMat.color = new Color(0.15f, 0.95f, 1.0f);
        runMat.EnableKeyword("_EMISSION");
        runMat.SetColor("_EmissionColor", new Color(0.15f, 0.95f, 1.0f) * 2.5f);
        runway.GetComponent<Renderer>().material = runMat;

        // スタート台座フラッグ柱
        for (int side = -1; side <= 1; side += 2)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = $"DeckPillar_{side}";
            pillar.transform.SetParent(platformRoot.transform, false);
            pillar.transform.localPosition = new Vector3(side * 2.3f, 1.5f, 1.0f);
            pillar.transform.localScale = new Vector3(0.18f, 1.5f, 0.18f);
            Object.DestroyImmediate(pillar.GetComponent<Collider>());

            var pMat = new Material(shader);
            pMat.color = new Color(0.3f, 0.22f, 0.15f);
            pillar.GetComponent<Renderer>().material = pMat;
        }

        return platformRoot;
    }

    private static GameObject BuildStartGate(Transform parent)
    {
        var gateRoot = new GameObject("StartGate");
        gateRoot.transform.SetParent(parent, false);
        gateRoot.transform.position = new Vector3(508f, 95.5f, 649f); // デッキの先端空中
        gateRoot.transform.rotation = Quaternion.Euler(0f, 205f, 0f);

        // スタートゲート用の光る巨大アーチ
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var gateMat = new Material(shader);
        Color gateCol = new Color(0.1f, 0.95f, 1.0f);
        gateMat.color = gateCol;
        gateMat.EnableKeyword("_EMISSION");
        gateMat.SetColor("_EmissionColor", gateCol * 3.5f);

        int segments = 18;
        float radius = 5.0f;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * (Mathf.PI * 2f / segments);
            Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            var seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            seg.name = "GateSeg_" + i;
            seg.transform.SetParent(gateRoot.transform, false);
            seg.transform.localPosition = pos;
            seg.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);

            float deg = angle * Mathf.Rad2Deg;
            seg.transform.localRotation = Quaternion.Euler(0f, 0f, deg + 90f);
            Object.DestroyImmediate(seg.GetComponent<Collider>());
            seg.GetComponent<Renderer>().material = gateMat;
        }

        // 天空への光の柱（スタートゲート視認用）
        var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "StartBeacon";
        beacon.transform.SetParent(gateRoot.transform, false);
        beacon.transform.localPosition = new Vector3(0f, 25f, 0f);
        beacon.transform.localScale = new Vector3(0.35f, 25f, 0.35f);
        Object.DestroyImmediate(beacon.GetComponent<Collider>());
        beacon.GetComponent<Renderer>().material = gateMat;

        // スタート通過感知トリガー
        var trigger = gateRoot.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(10f, 10f, 3.5f);

        gateRoot.AddComponent<GlidingStartTriggerHelper>();

        return gateRoot;
    }

    private static List<AdventureWindRing> BuildCourseRings(Transform parent)
    {
        var ringsRoot = new GameObject("Rings");
        ringsRoot.transform.SetParent(parent, false);

        // 10個の配置座標（北の最高峰絶壁 Y:92m から西ビーチ Y:9m までの絶景パノラマ滑空放物線）
        Vector3[] waypoints = new Vector3[]
        {
            new Vector3(502f, 87.0f, 640f), // Ring 1: 飛び出し直後のダイブ加速
            new Vector3(480f, 79.0f, 595f), // Ring 2: タワー北西の谷間を抜ける
            new Vector3(450f, 70.0f, 545f), // Ring 3: タワー西側の森・遺跡の頭上
            new Vector3(415f, 61.0f, 495f), // Ring 4: 大草原・満開の花畑を見下ろす大空
            new Vector3(370f, 51.0f, 445f), // Ring 5: 中央の丘陵尾根をスリリングに抜ける
            new Vector3(320f, 41.0f, 395f), // Ring 6: 西の風車の頭上
            new Vector3(270f, 31.0f, 345f), // Ring 7: 西の海岸崖への旋回
            new Vector3(220f, 22.0f, 300f), // Ring 8: 海が見える急降下
            new Vector3(180f, 14.5f, 265f), // Ring 9: 西ビーチ白砂直上
            new Vector3(155f, 9.5f, 245f)   // Ring 10: 座礁艇前ラストスパート
        };

        var list = new List<AdventureWindRing>();

        for (int i = 0; i < waypoints.Length; i++)
        {
            var ringGo = new GameObject($"FlightRing_{i + 1:D2}");
            ringGo.transform.SetParent(ringsRoot.transform, false);
            ringGo.transform.position = waypoints[i];

            // 向きを次のリング（またはゴール）へ向ける
            Vector3 targetNext = (i < waypoints.Length - 1) ? waypoints[i + 1] : new Vector3(150f, 6.35f, 235f);
            Vector3 forwardDir = (targetNext - waypoints[i]).normalized;
            if (forwardDir.sqrMagnitude > 0.01f)
            {
                ringGo.transform.rotation = Quaternion.LookRotation(forwardDir);
            }

            var ringComp = ringGo.AddComponent<AdventureWindRing>();
            ringComp.boostMultiplier = 1.95f;
            ringComp.boostDuration = 3.2f;

            // コース進行に合わせて色をグラデーション（エメラルドシアンからサファイアブルーへ）
            float t = (float)i / (waypoints.Length - 1);
            ringComp.ringColor = Color.Lerp(
                new Color(0.15f, 1.0f, 0.70f), // 鮮烈なエメラルドシアン
                new Color(0.20f, 0.85f, 1.0f), // サファイアブルー
                t
            );

            // エディタ上でも即時メッシュ＆光の柱を実体化してシーン保存
            ringComp.CreateRingVisual();
            ringComp.CreateBeaconPillar();

            list.Add(ringComp);
        }

        return list;
    }

    private static GameObject BuildFinishTarget(Transform parent)
    {
        var targetRoot = new GameObject("FinishTarget");
        targetRoot.transform.SetParent(parent, false);
        targetRoot.transform.position = new Vector3(150f, 6.35f, 235f); // 西ビーチ白砂

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // 巨大な光る着地サークル（同心円）
        float[] radii = { 7.5f, 5.0f, 2.5f };
        Color[] colors = {
            new Color(0.1f, 0.85f, 1.0f),
            new Color(0.2f, 1.0f, 0.7f),
            new Color(1.0f, 0.9f, 0.2f) // 中央ゴールド
        };

        for (int r = 0; r < radii.Length; r++)
        {
            int segments = 24;
            float radius = radii[r];
            Color c = colors[r];

            var ringMat = new Material(shader);
            ringMat.color = c;
            ringMat.EnableKeyword("_EMISSION");
            ringMat.SetColor("_EmissionColor", c * 2.5f);

            var cRoot = new GameObject($"Circle_{r}");
            cRoot.transform.SetParent(targetRoot.transform, false);

            for (int i = 0; i < segments; i++)
            {
                float angle = i * (Mathf.PI * 2f / segments);
                Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, 0.05f, Mathf.Sin(angle) * radius);
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                seg.name = $"Seg_{i}";
                seg.transform.SetParent(cRoot.transform, false);
                seg.transform.localPosition = pos;
                seg.transform.localScale = new Vector3(0.4f, 0.05f, 0.75f);

                float deg = angle * Mathf.Rad2Deg;
                seg.transform.localRotation = Quaternion.Euler(0f, -deg + 90f, 0f);
                Object.DestroyImmediate(seg.GetComponent<Collider>());
                seg.GetComponent<Renderer>().material = ringMat;
            }
        }

        // 天空へ伸びるゴールの光柱（高さ35m）
        var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "GoalBeaconPillar";
        beacon.transform.SetParent(targetRoot.transform, false);
        beacon.transform.localPosition = new Vector3(0f, 17.5f, 0f);
        beacon.transform.localScale = new Vector3(0.4f, 17.5f, 0.4f);
        Object.DestroyImmediate(beacon.GetComponent<Collider>());

        var bMat = new Material(shader);
        Color bCol = new Color(1.0f, 0.85f, 0.2f, 0.45f);
        bMat.color = bCol;
        bMat.EnableKeyword("_EMISSION");
        bMat.SetColor("_EmissionColor", bCol * 2.2f);
        beacon.GetComponent<Renderer>().material = bMat;

        // 着地感知トリガー
        var trigger = targetRoot.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 9.0f;

        targetRoot.AddComponent<GlidingGoalTriggerHelper>();

        return targetRoot;
    }
}
