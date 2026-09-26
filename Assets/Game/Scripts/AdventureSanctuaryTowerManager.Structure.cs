using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 聖域の塔（Sanctuary Tower）の物理構造・テラス・進入スロープ・階段・レバー台座の生成と地形救出処理
/// </summary>
public partial class AdventureSanctuaryTowerManager
{
    #region テラス・進入スロープ・救出処理

    /// <summary>テラス（63m）と周囲の台地（62m）をつなぐ進入スロープコライダーを4方向に生成</summary>
    void BuildPodiumEntryRamps(Transform towerRoot)
    {
        // 既存スロープを削除して多重生成を防止
        var oldRamps = towerRoot.Find("PodiumEntryRamps");
        if (oldRamps != null) Destroy(oldRamps.gameObject);

        var rampsRoot = new GameObject("PodiumEntryRamps");
        rampsRoot.transform.SetParent(towerRoot, false);
        rampsRoot.transform.position = Vector3.zero;

        // テラス中心: (512, 63.0, 512)、半径34.5m
        // スロープ仕様: 幅 8m、地面側始点 Y=62.0m（テラス外縁+2m手前）、テラス側終点 Y=63.0m
        const float terraceTopY  = 63.0f;   // テラス上面
        const float groundY      = 62.0f;   // 周囲台地面
        const float rampLength   = 5.0f;    // スロープの斜面長（XZ投影）
        const float rampWidth    = 8.0f;    // スロープ幅
        const float rampThickness = 0.8f;   // コライダーの物理厚み（段差スタック防止）

        // (方位角, 注記) — 南側は古代階段が通るので除外
        float[] outDirs = { 0f, 90f, 270f }; // 北(0°=z+)、東(90°=x+)、西(270°=x-)

        float terraceRadius = 34.5f;

        foreach (float outDeg in outDirs)
        {
            float rad = outDeg * Mathf.Deg2Rad;
            // 外向き単位ベクトル
            Vector3 outDir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

            // スロープの中心位置：テラス縁 + rampLength/2 分だけ外側
            Vector3 slopeMid = new Vector3(512f, 0f, 512f)
                               + outDir * (terraceRadius + rampLength * 0.5f);
            // Y: 地面〜テラスの中間点
            slopeMid.y = (groundY + terraceTopY) * 0.5f;

            // 回転：外向き方向（Z+ 前）を法線方向へ
            Quaternion rot = Quaternion.LookRotation(outDir, Vector3.up);

            // 傾き角度 θ = atan((63-62) / rampLength)
            float slopeAngle = Mathf.Atan2(terraceTopY - groundY, rampLength) * Mathf.Rad2Deg;

            var rampGo = new GameObject($"PodiumRamp_{outDeg:F0}deg");
            rampGo.transform.SetParent(rampsRoot.transform, false);
            rampGo.transform.position = slopeMid;
            // OutDir 方向へ pitch（X軸回転）で傾ける
            rampGo.transform.rotation = rot * Quaternion.Euler(-slopeAngle, 0f, 0f);

            var box = rampGo.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size   = new Vector3(rampWidth, rampThickness, rampLength);
        }
    }

    /// <summary>タワー白亜テラス上（またはレバー基壇上、スロープ上）なら歩行面Y、それ以外は負の無限大</summary>
    public static float GetTerraceSurfaceY(Vector3 worldPos)
    {
        Vector2 posXZ = new Vector2(worldPos.x, worldPos.z);

        // 1. 南正面メインレバー台座の範囲（上面 64.02m、半径3.2m）
        Vector2 mainLeverCenter = new Vector2(512f, 501.5f);
        if (Vector2.SqrMagnitude(posXZ - mainLeverCenter) <= 3.2f * 3.2f)
        {
            return 64.02f; // レバー台座の上面
        }

        // 1b. レバー台座アプローチスロープ（台座南側、テラス上面63m → 台座上64.02m）
        // XZ: x∈[508,516], z∈[476,501.5], 線形補間でY算出
        if (posXZ.x >= 508f && posXZ.x <= 516f && posXZ.y >= 476f && posXZ.y <= 501.5f)
        {
            float t = Mathf.InverseLerp(501.5f, 476f, posXZ.y); // 台座に近い=0、遠い=1
            // 台座前（z=490付近）まではテラスと同高度、それより台座に向かってステップ
            // ステップ範囲 z=490〜501.5 のみ高さ変化
            if (posXZ.y >= 490f)
            {
                float stepT = Mathf.InverseLerp(490f, 501.5f, posXZ.y);
                return Mathf.Lerp(63.00f, 64.02f, stepT);
            }
            return 63.00f;
        }

        // 2. 白大理石円盤テラスの範囲（中心 512, 512、半径 34.5m、上面 63.00m）
        Vector2 towerCenter = new Vector2(512f, 512f);
        if (Vector2.SqrMagnitude(posXZ - towerCenter) <= 34.5f * 34.5f)
        {
            return 63.00f; // テラス上面
        }

        // 3. テラス外周スロープ（台地62m → テラス63m）の補間歩行面
        // 北(z+)・東(x+)・西(x-) の3方向スロープ、各幅8m、奥行5m
        const float terraceTopY = 63.0f;
        const float groundTopY  = 62.0f;
        const float rampLen     = 5.0f;
        const float rampHalf    = 4.0f; // 幅/2
        const float terraceR    = 34.5f;
        float cx = 512f, cz = 512f;

        // 北スロープ: z∈[512+34.5, 512+34.5+5], x∈[512-4, 512+4]
        float northInner = cz + terraceR;
        float northOuter = northInner + rampLen;
        if (posXZ.x >= cx - rampHalf && posXZ.x <= cx + rampHalf
            && posXZ.y >= northInner   && posXZ.y <= northOuter)
        {
            float t = Mathf.InverseLerp(northInner, northOuter, posXZ.y);
            return Mathf.Lerp(terraceTopY, groundTopY, t);
        }
        // 東スロープ: x∈[512+34.5, 512+34.5+5], z∈[512-4, 512+4]
        float eastInner = cx + terraceR;
        float eastOuter = eastInner + rampLen;
        if (posXZ.x >= eastInner   && posXZ.x <= eastOuter
            && posXZ.y >= cz - rampHalf && posXZ.y <= cz + rampHalf)
        {
            float t = Mathf.InverseLerp(eastInner, eastOuter, posXZ.x);
            return Mathf.Lerp(terraceTopY, groundTopY, t);
        }
        // 西スロープ: x∈[512-34.5-5, 512-34.5], z∈[512-4, 512+4]
        float westInner = cx - terraceR;
        float westOuter = westInner - rampLen;
        if (posXZ.x >= westOuter   && posXZ.x <= westInner
            && posXZ.y >= cz - rampHalf && posXZ.y <= cz + rampHalf)
        {
            float t = Mathf.InverseLerp(westInner, westOuter, posXZ.x);
            return Mathf.Lerp(terraceTopY, groundTopY, t);
        }

        // それ以外の台地全域はTerrainを歩行
        return float.NegativeInfinity;
    }

    /// <summary>台座に完全に潜って落下したNikoをテラス上面へ安全救出する（通常歩行中は一切介入しない）</summary>
    public void RescuePlayerIfBuriedInTerrace(AdventurePlayerController player)
    {
        if (player == null) return;
        if (player.IsSkybreakPillarAscending || player.IsAutoGliding) return;
        if (IsCanopyBroken && player.transform.position.y > 80f) return;

        Vector3 pos = player.transform.position;
        float terraceY = GetTerraceSurfaceY(pos);
        if (terraceY <= float.NegativeInfinity) return;

        // 床下深く（0.6m以上下）に完全に潜り込んでしまった時のみ緊急救出
        if (pos.y < terraceY - 0.60f)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = new Vector3(pos.x, terraceY + 0.02f, pos.z);
            if (cc != null) cc.enabled = true;
            player.ForceGroundReset();
        }
    }

    #endregion

    #region レバー物理構造・台座・アプローチステップ

    void BuildTowerLever()
    {
        // 1. 自階層配下にある古いレバーオブジェクトを即座に完全一掃
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child != null && (child.name.Contains("LeverStructure") || child.name.Contains("SanctuaryLever")))
            {
                DestroyImmediate(child.gameObject);
            }
        }

        // 2. シーン内にあるすべての古いレバーオブジェクトを根こそぎ即時完全消去
        var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var go in allObjects)
        {
            if (go == null) continue;
            if (go.name == "SanctuaryLeverStructure" || go.name == "SanctuaryWestLeverStructure" ||
                go.name == "SanctuaryNorthLeverStructure" || go.name == "SanctuaryEastLeverStructure" ||
                go.name == "SanctuaryTopLeverStructure")
            {
                DestroyImmediate(go);
            }
        }

        _allLeverHandles.Clear();
        _allLeverLights.Clear();
        _leverHandle = null;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // 大理石台座マテリアル
        var pedMat = new Material(shader);
        pedMat.SetColor("_BaseColor", new Color(0.94f, 0.96f, 0.98f));
        pedMat.SetFloat("_Smoothness", 0.92f);
        if (pedMat.HasProperty("_Metallic")) pedMat.SetFloat("_Metallic", 0.08f);

        // 黄金真鍮ハウジングマテリアル
        var hMat = new Material(shader);
        hMat.SetColor("_BaseColor", new Color(0.92f, 0.72f, 0.28f));
        hMat.SetFloat("_Metallic", 0.98f);
        hMat.SetFloat("_Smoothness", 0.88f);
        if (hMat.HasProperty("_EmissionColor"))
        {
            hMat.EnableKeyword("_EMISSION");
            hMat.SetColor("_EmissionColor", new Color(0.55f, 0.35f, 0.08f) * 1.4f);
        }

        // シャフトマテリアル
        var sMat = new Material(shader);
        sMat.SetColor("_BaseColor", new Color(0.95f, 0.78f, 0.32f));
        sMat.SetFloat("_Metallic", 0.96f);
        sMat.SetFloat("_Smoothness", 0.86f);

        // グリップ球マテリアル（真紅＋発光）
        var gMat = new Material(shader);
        gMat.SetColor("_BaseColor", new Color(0.88f, 0.12f, 0.10f));
        gMat.SetFloat("_Smoothness", 0.78f);
        if (gMat.HasProperty("_EmissionColor"))
        {
            gMat.EnableKeyword("_EMISSION");
            gMat.SetColor("_EmissionColor", new Color(0.85f, 0.08f, 0.05f) * 2.2f);
        }

        // 天を衝く光の柱マテリアル（シアン発光、半透明加算ブレンドで遠景から美しく輝く）
        var bShader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("RustAndFloat/WhiteSmoke")
            ?? Shader.Find("Sprites/Default");
        var bMat = new Material(bShader);
        bMat.SetTexture("_BaseMap", AdventureRustDrone.GetSoftSmokeTexture());
        bMat.SetColor("_BaseColor", new Color(0.45f, 0.98f, 1.0f, 0.82f));
        if (bMat.HasProperty("_Surface")) bMat.SetFloat("_Surface", 1f);
        if (bMat.HasProperty("_Blend")) bMat.SetFloat("_Blend", 1f);
        if (bMat.HasProperty("_Cull")) bMat.SetFloat("_Cull", 0f);
        if (bMat.HasProperty("_ZWrite")) bMat.SetFloat("_ZWrite", 0f);
        bMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        bMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        bMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        bMat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        bMat.renderQueue = 3150;

        // 南側正面メインレバー1基のみを堂々と配備（白亜テラスの特等席）
        CreateLeverStation("SanctuaryLeverStructure", _mainLeverPos, Quaternion.identity, pedMat, hMat, sMat, gMat, bMat, true);
    }

    void CreateLeverStation(string name, Vector3 worldPos, Quaternion rotation,
                            Material pedMat, Material hMat, Material sMat, Material gMat, Material bMat,
                            bool isMain)
    {
        var root = new GameObject(name);
        root.transform.SetParent(transform, false);
        root.transform.position = worldPos;
        root.transform.rotation = rotation;

        // 白亜大理石の巨大円形台座（遠くから一目で分かるサイズ）
        var ped = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ped.name = "LeverPedestal";
        ped.transform.SetParent(root.transform, false);
        ped.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        ped.transform.localScale = new Vector3(6.2f, 0.55f, 6.2f);
        foreach (var c in ped.GetComponents<Collider>())
            Destroy(c);
        var pedSolid = new GameObject("LeverPedestal_SolidWalk");
        pedSolid.transform.SetParent(root.transform, false);
        pedSolid.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        pedSolid.transform.localRotation = Quaternion.identity;
        pedSolid.transform.localScale = Vector3.one;
        var pedBox = pedSolid.AddComponent<BoxCollider>();
        pedBox.center = Vector3.zero;
        pedBox.size = new Vector3(6.2f, 1.1f, 6.2f);
        var pedMr = ped.GetComponent<MeshRenderer>();
        if (pedMr != null) pedMr.material = pedMat;

        // 真鍮ギアハウジング（大型）
        var housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        housing.name = "BrassGearHousing";
        housing.transform.SetParent(root.transform, false);
        housing.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        housing.transform.localScale = new Vector3(2.4f, 1.05f, 1.9f);
        var hMr = housing.GetComponent<MeshRenderer>();
        if (hMr != null) hMr.material = hMat;

        // レバーピボット
        var pivot = new GameObject("LeverPivot");
        pivot.transform.SetParent(root.transform, false);
        pivot.transform.localPosition = new Vector3(0f, 2.05f, 0f);
        pivot.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);

        if (isMain) _leverHandle = pivot.transform;
        _allLeverHandles.Add(pivot.transform);

        // シャフト（太い真鍮棒）
        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Shaft";
        shaft.transform.SetParent(pivot.transform, false);
        shaft.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        shaft.transform.localScale = new Vector3(0.32f, 0.95f, 0.32f);
        Destroy(shaft.GetComponent<Collider>());
        var sMr = shaft.GetComponent<MeshRenderer>();
        if (sMr != null) sMr.material = sMat;

        // 深紅グリップ球（大きく掴みやすい）
        var grip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        grip.name = "GripBall";
        grip.transform.SetParent(pivot.transform, false);
        grip.transform.localPosition = new Vector3(0f, 1.95f, 0f);
        grip.transform.localScale = Vector3.one * 0.95f;
        Destroy(grip.GetComponent<Collider>());
        var gMr = grip.GetComponent<MeshRenderer>();
        if (gMr != null) gMr.material = gMat;

        // 真鍮フランジ／ギア装飾
        var flange = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        flange.name = "BrassFlange";
        flange.transform.SetParent(root.transform, false);
        flange.transform.localPosition = new Vector3(0f, 1.12f, 0f);
        flange.transform.localScale = new Vector3(3.4f, 0.12f, 3.4f);
        Destroy(flange.GetComponent<Collider>());
        var fMr = flange.GetComponent<MeshRenderer>();
        if (fMr != null) fMr.material = hMat;

        var gear = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        gear.name = "BrassGearDisc";
        gear.transform.SetParent(root.transform, false);
        gear.transform.localPosition = new Vector3(0f, 1.72f, -0.55f);
        gear.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        gear.transform.localScale = new Vector3(1.6f, 0.18f, 1.6f);
        Destroy(gear.GetComponent<Collider>());
        var gearMr = gear.GetComponent<MeshRenderer>();
        if (gearMr != null) gearMr.material = sMat;

        var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.name = "MarbleRim";
        rim.transform.SetParent(root.transform, false);
        rim.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        rim.transform.localScale = new Vector3(7.0f, 0.12f, 7.0f);
        Destroy(rim.GetComponent<Collider>());
        var rimMr = rim.GetComponent<MeshRenderer>();
        if (rimMr != null) rimMr.material = pedMat;

        // 天を衝く巨大光柱ビーコン＋外周オーラ
        var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "LeverSkyBeacon";
        beacon.transform.SetParent(root.transform, false);
        beacon.transform.localPosition = new Vector3(0f, 48f, 0f);
        beacon.transform.localScale = new Vector3(1.85f, 48f, 1.85f);
        Destroy(beacon.GetComponent<Collider>());
        var bRend = beacon.GetComponent<Renderer>();
        if (bRend != null) bRend.material = bMat;

        var aura = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        aura.name = "LeverSkyBeaconAura";
        aura.transform.SetParent(root.transform, false);
        aura.transform.localPosition = new Vector3(0f, 42f, 0f);
        aura.transform.localScale = new Vector3(4.2f, 42f, 4.2f);
        Destroy(aura.GetComponent<Collider>());
        var auraMat = new Material(bMat);
        auraMat.SetColor("_BaseColor", new Color(0.55f, 0.92f, 1f, 0.28f));
        var aRend = aura.GetComponent<Renderer>();
        if (aRend != null) aRend.material = auraMat;

        // 発光インジケーターライト
        var lightGo = new GameObject("LeverIndicatorLight");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 3.8f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.45f, 0.98f, 1.0f);
        light.intensity = 11.5f;
        light.range = 55f;

        var rimLightGo = new GameObject("LeverRimLight");
        rimLightGo.transform.SetParent(root.transform, false);
        rimLightGo.transform.localPosition = new Vector3(0f, 2.2f, 1.2f);
        var rimLight = rimLightGo.AddComponent<Light>();
        rimLight.type = LightType.Point;
        rimLight.color = new Color(1f, 0.78f, 0.35f);
        rimLight.intensity = 4.5f;
        rimLight.range = 18f;

        if (isMain) _leverLight = light;
        _allLeverLights.Add(light);
        _allLeverLights.Add(rimLight);

        // 接近判定トリガー
        var col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 12.0f;

        // レバー台座アプローチステップ
        if (isMain)
            BuildLeverApproachSteps(root.transform, worldPos);
    }

    /// <summary>レバー台座（LeverPedestal）南側に Niko が歩いて登れる3段ステップを生成</summary>
    void BuildLeverApproachSteps(Transform leverRoot, Vector3 leverWorldPos)
    {
        var old = leverRoot.Find("LeverApproachSteps");
        if (old != null) Destroy(old.gameObject);

        var stepsRoot = new GameObject("LeverApproachSteps");
        stepsRoot.transform.SetParent(leverRoot, false);
        stepsRoot.transform.position = Vector3.zero;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var marbleMat = new Material(shader);
        marbleMat.SetColor("_BaseColor", new Color(0.92f, 0.94f, 0.96f)); // 純白大理石
        marbleMat.SetFloat("_Smoothness", 0.85f);

        const float terraceY   = 63.00f;
        const float pedestalY  = 64.02f; // 台座上面
        int   numSteps   = 3;
        float stepHeight = (pedestalY - terraceY) / numSteps; // ≈ 0.34m
        float stepDepth  = 1.2f;   // 奥行き（z 方向）
        float stepWidth  = 4.2f;   // 幅
        float stepThick  = 0.8f;   // コライダー物理厚み

        float pedestalSouthEdgeZ = leverWorldPos.z - 3.1f;

        for (int i = 0; i < numSteps; i++)
        {
            float stepTopY  = terraceY + stepHeight * (i + 1);
            float stepMidZ  = pedestalSouthEdgeZ - stepDepth * (numSteps - 1 - i) - stepDepth * 0.5f;

            var stepGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stepGo.name = $"LeverStep_{i}";
            stepGo.transform.SetParent(stepsRoot.transform, false);
            stepGo.transform.position = new Vector3(leverWorldPos.x, stepTopY - stepThick * 0.5f, stepMidZ);
            stepGo.transform.rotation = Quaternion.identity;
            stepGo.transform.localScale = new Vector3(stepWidth, stepThick, stepDepth);

            var mr = stepGo.GetComponent<MeshRenderer>();
            if (mr != null) mr.material = marbleMat;
        }

        // スロープコライダー（ステップスタック完全防止）
        float totalRampLen  = stepDepth * numSteps;
        float rampStartZ    = pedestalSouthEdgeZ - totalRampLen;
        float rampEndZ      = pedestalSouthEdgeZ;
        float rampMidZ      = (rampStartZ + rampEndZ) * 0.5f;
        float rampMidY      = (terraceY + pedestalY) * 0.5f;

        var rampGo = new GameObject("LeverStepRampCollider");
        rampGo.transform.SetParent(stepsRoot.transform, false);
        rampGo.transform.position = new Vector3(leverWorldPos.x, rampMidY, rampMidZ);

        float slopeAngle = Mathf.Atan2(pedestalY - terraceY, totalRampLen) * Mathf.Rad2Deg;
        rampGo.transform.rotation = Quaternion.Euler(-slopeAngle, 0f, 0f);

        var rampBox = rampGo.AddComponent<BoxCollider>();
        rampBox.center = Vector3.zero;
        rampBox.size   = new Vector3(stepWidth, 0.8f, totalRampLen);
    }

    #endregion

    #region 古代アプローチ階段

    /// <summary>オアシス湧水池（480, 455）からタワー台地（512, 512）へ登る白亜の古代神殿アプローチ階段道を生成</summary>
    void BuildTowerStairs(Transform parent, Terrain land)
    {
        var old = parent.Find("SanctuaryApproachStairs");
        if (old != null)
            Destroy(old.gameObject);

        var stairsRoot = new GameObject("SanctuaryApproachStairs");
        stairsRoot.transform.SetParent(parent, false);

        Vector3 startP = new Vector3(472f, 48.5f, 455f); // オアシス池のほとり
        Vector3 endP = new Vector3(512f, TerraceTopY, 478f);
        if (land != null)
        {
            startP.y = land.SampleHeight(startP) + land.transform.position.y + 0.2f;
            endP.y = Mathf.Max(TerraceTopY, land.SampleHeight(endP) + land.transform.position.y + 0.2f);
        }

        int steps = 22;
        float width = 4.8f;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var marbleMat = new Material(shader);
        marbleMat.SetColor("_BaseColor", new Color(0.92f, 0.94f, 0.96f)); // 純白大理石
        marbleMat.SetFloat("_Smoothness", 0.85f);

        var pillarMat = new Material(shader);
        pillarMat.SetColor("_BaseColor", new Color(0.82f, 0.85f, 0.88f));

        for (int i = 0; i < steps; i++)
        {
            float t0 = (float)i / steps;
            float t1 = (float)(i + 1) / steps;
            Vector3 p0 = Vector3.Lerp(startP, endP, t0);
            Vector3 p1 = Vector3.Lerp(startP, endP, t1);

            if (land != null)
            {
                p0.y = Mathf.Max(p0.y, land.SampleHeight(p0) + land.transform.position.y + 0.15f);
                p1.y = Mathf.Max(p1.y, land.SampleHeight(p1) + land.transform.position.y + 0.15f);
            }

            Vector3 center = (p0 + p1) * 0.5f;
            Vector3 forward = (p1 - p0);
            float len = forward.magnitude;
            if (len < 0.01f) continue;

            Vector3 fwdNorm = forward.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwdNorm).normalized;

            var stepObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stepObj.name = $"MarbleStep_{i}";
            stepObj.transform.SetParent(stairsRoot.transform, false);
            stepObj.transform.position = center;
            stepObj.transform.rotation = Quaternion.LookRotation(fwdNorm, Vector3.up);
            stepObj.transform.localScale = new Vector3(width, 0.32f, len * 1.05f);

            var mr = stepObj.GetComponent<MeshRenderer>();
            if (mr != null) mr.material = marbleMat;

            var stepCol = stepObj.GetComponent<Collider>();
            if (stepCol != null) Object.Destroy(stepCol);

            // 4段ごとに両脇に白亜の装飾オベリスク支柱を配置
            if (i % 4 == 0)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    post.name = $"MarblePost_{i}_{side}";
                    post.transform.SetParent(stairsRoot.transform, false);
                    post.transform.position = center + right * (width * 0.5f + 0.4f) * side + Vector3.up * 0.6f;
                    post.transform.localScale = new Vector3(0.35f, 0.8f, 0.35f);
                    var postCol = post.GetComponent<Collider>();
                    if (postCol != null) Object.Destroy(postCol);
                    var pmr = post.GetComponent<MeshRenderer>();
                    if (pmr != null) pmr.material = pillarMat;

                    var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    cap.name = $"PostCap_{i}_{side}";
                    cap.transform.SetParent(post.transform, false);
                    cap.transform.localPosition = new Vector3(0f, 1.0f, 0f);
                    cap.transform.localScale = new Vector3(1.3f, 0.5f, 1.3f);
                    var capCol = cap.GetComponent<Collider>();
                    if (capCol != null) Object.Destroy(capCol);
                    var cmr = cap.GetComponent<MeshRenderer>();
                    if (cmr != null) cmr.material = marbleMat;
                }
            }
        }

        // 階段両側の手すりライト
        Vector3 midP = (startP + endP) * 0.5f;
        var stLight = new GameObject("StairsGuideLight");
        stLight.transform.SetParent(stairsRoot.transform, false);
        stLight.transform.position = midP + Vector3.up * 3.5f;
        var lt = stLight.AddComponent<Light>();
        lt.type = LightType.Point;
        lt.color = new Color(1.0f, 0.88f, 0.65f);
        lt.intensity = 3.5f;
        lt.range = 28f;

        // 階段周辺の岩コライダーの角を丸め、引っ掛かりを防止
        Vector3 oasisPos = new Vector3(480f, 48.2f, 455f);
        var pondRocksRoot = GameObject.Find("SanctuarySpringPond_Rocks");
        if (pondRocksRoot != null)
        {
            SoftenCollidersOnRoot(
                pondRocksRoot.transform,
                startP, endP,
                corridorHalfWidth: 5.5f,
                thinHalfWidth: 2.8f,
                alongTMax: 0.45f,
                forceAllInRadius: false,
                center: oasisPos,
                softRadius: 0f,
                thinRadius: 0f);
        }

        var paradiseDecor = GameObject.Find("ParadiseDecorations");
        if (paradiseDecor != null)
        {
            SoftenCollidersOnRoot(
                paradiseDecor.transform,
                startP, endP,
                corridorHalfWidth: 5.0f,
                thinHalfWidth: 2.4f,
                alongTMax: 0.40f,
                forceAllInRadius: true,
                center: oasisPos,
                softRadius: 22f,
                thinRadius: 14f);
        }

        SoftenLooseRocksNearCorridor(startP, endP, oasisPos);

        // 階段全体を覆うスロープコライダー
        AddStairsRampColliders(stairsRoot.transform, startP, endP, width);
        ClearTowerStreamWalkBlockers(startP, endP);
    }

    /// <summary>
    /// タワー手前のオアシス湧水池〜アプローチ階段まわりで、渓流岩場の歩行阻害を減らす。
    /// 見た目は100%残し、進路・階段周囲の岩コライダーを完全にトリガー化してスタックを根絶する。
    /// </summary>
    static void ClearTowerStreamWalkBlockers(Vector3 startP, Vector3 endP)
    {
        Vector3 oasis = new Vector3(480f, 48.2f, 455f);
        SoftenOrThinRocksAlongPath(startP, endP, corridorHalfWidth: 8.0f, thinHalfWidth: 4.0f, alongTMax: 0.85f);
        SoftenRockRootNearPoint("SanctuarySpringPond_Rocks", oasis, softRadius: 36f, thinRadius: 18f);
        SoftenRockRootNearPoint("MountainGorgeProps", oasis, softRadius: 44f, thinRadius: 20f);
        SoftenRockRootNearPoint("UpperParadiseStream", oasis, softRadius: 48f, thinRadius: 0f);
        SoftenRockRootNearPoint("BeachStepPonds_Rocks", oasis, softRadius: 36f, thinRadius: 0f);

        // 階段足元・池〜階段アプローチ全域の岩を強制トリガー化
        SoftenRocksNearStairFoot(startP);

        // 名称に Rock/Stone を含むオブジェクトも、階段〜池の回廊だけ追加で緩和
        SoftenLooseRocksNearCorridor(startP, endP, oasis);
    }

    static void SoftenOrThinRocksAlongPath(Vector3 startP, Vector3 endP, float corridorHalfWidth, float thinHalfWidth, float alongTMax)
    {
        var rocks = GameObject.Find("SanctuarySpringPond_Rocks");
        if (rocks == null) return;
        SoftenCollidersOnRoot(rocks.transform, startP, endP, corridorHalfWidth, thinHalfWidth, alongTMax, forceAllInRadius: false, center: default, softRadius: 0f, thinRadius: 0f);
    }

    static void SoftenRockRootNearPoint(string rootName, Vector3 center, float softRadius, float thinRadius)
    {
        var root = GameObject.Find(rootName);
        if (root == null) return;
        SoftenCollidersOnRoot(root.transform, default, default, 0f, 0f, 0f, forceAllInRadius: true, center: center, softRadius: softRadius, thinRadius: thinRadius);
    }

    /// <summary>
    /// アプローチ階段最下部（StairRamp_0〜1）および池〜階段進入路付近の岩コライダーを歩行阻害しないよう強制緩和。
    /// </summary>
    static void SoftenRocksNearStairFoot(Vector3 stairStart)
    {
        var all = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var col = all[i];
            if (col == null || col.isTrigger) continue;
            string n = col.gameObject.name;
            if (n.IndexOf("Rock", System.StringComparison.OrdinalIgnoreCase) < 0
                && n.IndexOf("Stone", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            Vector3 c = col.bounds.center;
            float dx = c.x - stairStart.x;
            float dz = c.z - stairStart.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            if (dist > 24f) continue;

            // 見た目は残し、足元の物理だけ外す
            col.isTrigger = true;
        }
    }

    static void SoftenLooseRocksNearCorridor(Vector3 startP, Vector3 endP, Vector3 oasis)
    {
        var all = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var col = all[i];
            if (col == null || col.isTrigger) continue;
            string n = col.gameObject.name;
            if (n.IndexOf("Rock", System.StringComparison.OrdinalIgnoreCase) < 0
                && n.IndexOf("Stone", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            Vector3 c = col.bounds.center;
            float distOasis = Vector2.Distance(new Vector2(c.x, c.z), new Vector2(oasis.x, oasis.z));
            float distPath = DistancePointToSegment(c, startP, endP);
            if (distOasis > 36f && distPath > 9f) continue;

            if (distPath < 3.6f && distOasis < 28f)
            {
                col.gameObject.SetActive(false);
                continue;
            }
            if (distPath < 8.5f || distOasis < 24f)
                col.isTrigger = true;
        }
    }

    static void SoftenCollidersOnRoot(
        Transform root,
        Vector3 startP, Vector3 endP,
        float corridorHalfWidth, float thinHalfWidth, float alongTMax,
        bool forceAllInRadius, Vector3 center, float softRadius, float thinRadius)
    {
        var cols = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            var col = cols[i];
            if (col == null) continue;

            Vector3 c = col.bounds.center;
            bool soft = false;
            bool thin = false;

            if (forceAllInRadius)
            {
                float d = Vector2.Distance(new Vector2(c.x, c.z), new Vector2(center.x, center.z));
                if (thinRadius > 0.1f && d <= thinRadius)
                {
                    if (c.z <= center.z + 2.5f || c.x <= center.x + 4f)
                        thin = true;
                    else
                        soft = true;
                }
                else if (d <= softRadius)
                {
                    soft = true;
                }
            }
            else
            {
                Vector3 ab = endP - startP;
                float denom = ab.sqrMagnitude;
                float t = denom < 0.01f ? 0f : Mathf.Clamp01(Vector3.Dot(c - startP, ab) / denom);
                if (t > alongTMax) continue;
                float dist = DistancePointToSegment(c, startP, endP);
                if (dist <= thinHalfWidth) thin = true;
                else if (dist <= corridorHalfWidth) soft = true;
            }

            if (thin)
            {
                if (col.transform != root)
                    col.gameObject.SetActive(false);
                else
                    col.isTrigger = true;
            }
            else if (soft)
            {
                col.isTrigger = true;
            }
        }
    }

    static float DistancePointToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float denom = ab.sqrMagnitude;
        if (denom < 0.01f) return Vector3.Distance(p, a);
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / denom);
        return Vector3.Distance(p, a + ab * t);
    }

    void AddStairsRampColliders(Transform parent, Vector3 startP, Vector3 endP, float width)
    {
        const int   segments       = 5;
        const float thickness      = 0.35f;
        const float stepHalfHeight = 0.16f;

        var terrain = Terrain.activeTerrain ?? FindAnyObjectByType<Terrain>();

        Vector3 totalForward = (endP - startP).normalized;
        Vector3 entryStart = startP - totalForward * 2.8f;
        if (terrain != null)
        {
            entryStart.y = terrain.SampleHeight(entryStart) + terrain.transform.position.y - 0.15f;
        }
        else
        {
            entryStart.y = startP.y - 0.5f;
        }

        Vector3 entryEnd = startP;
        if (terrain != null)
        {
            entryEnd.y = Mathf.Max(startP.y, terrain.SampleHeight(entryEnd) + terrain.transform.position.y + 0.16f);
        }

        Vector3 entryCenter = (entryStart + entryEnd) * 0.5f;
        Vector3 entryFwd = entryEnd - entryStart;
        float entryLen = entryFwd.magnitude;
        if (entryLen > 0.05f)
        {
            var entryGo = new GameObject("StairRamp_Entry");
            entryGo.transform.SetParent(parent, false);
            entryGo.transform.position = entryCenter;
            entryGo.transform.rotation = Quaternion.LookRotation(entryFwd.normalized, Vector3.up);

            var entryBox = entryGo.AddComponent<BoxCollider>();
            entryBox.center = new Vector3(0f, stepHalfHeight - thickness * 0.5f, 0f);
            entryBox.size   = new Vector3(width * 1.15f, thickness, entryLen * 1.1f);
        }

        for (int s = 0; s < segments; s++)
        {
            float t0 = (float)s       / segments;
            float t1 = (float)(s + 1) / segments;

            Vector3 p0 = Vector3.Lerp(startP, endP, t0);
            Vector3 p1 = Vector3.Lerp(startP, endP, t1);

            if (terrain != null)
            {
                p0.y = Mathf.Max(p0.y, terrain.SampleHeight(p0) + terrain.transform.position.y + 0.16f);
                p1.y = Mathf.Max(p1.y, terrain.SampleHeight(p1) + terrain.transform.position.y + 0.16f);
            }

            Vector3 segCenter  = (p0 + p1) * 0.5f;
            Vector3 segForward = p1 - p0;
            float   segLen     = segForward.magnitude;
            if (segLen < 0.01f) continue;

            var rampGo = new GameObject($"StairRamp_{s}");
            rampGo.transform.SetParent(parent, false);
            rampGo.transform.position = segCenter;
            rampGo.transform.rotation = Quaternion.LookRotation(segForward.normalized, Vector3.up);
            rampGo.transform.localScale = Vector3.one;

            var box = rampGo.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, stepHalfHeight - thickness * 0.5f, 0f);
            box.size   = new Vector3(width, thickness, segLen * (s == segments - 1 ? 1.15f : 1.06f));
        }
    }

    #endregion
}
