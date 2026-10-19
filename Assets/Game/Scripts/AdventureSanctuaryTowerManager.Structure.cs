using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 聖域の塔（Sanctuary Tower）の物理構造・テラス・進入スロープ・階段・レバー台座の生成と地形救出処理
public partial class AdventureSanctuaryTowerManager
{
    #region テラス・進入スロープ・救出処理

    /// <summary>テラス（63m）と周囲の地形（58〜62m）をつなぐ全周16方向のスロープコライダーを生成</summary>
    void BuildPodiumEntryRamps(Transform towerRoot)
    {
        // 既存スロープを削除して多重生成を防止
        var oldRamps = towerRoot.Find("PodiumEntryRamps");
        if (oldRamps != null) Destroy(oldRamps.gameObject);

        var rampsRoot = new GameObject("PodiumEntryRamps");
        rampsRoot.transform.SetParent(towerRoot, false);
        rampsRoot.transform.position = Vector3.zero;

        // テラス中心: (512, 63.0, 512)、半径34.8m
        const float terraceTopY  = 63.0f;   // テラス上面
        const float rampLength   = 6.5f;    // スロープの斜面長（XZ投影）
        const float rampThickness = 1.6f;   // コライダーの物理厚み（段差スタック・下抜け完全防止）
        const float terraceRadius = 34.5f;

        var terrain = Terrain.activeTerrain ?? FindAnyObjectByType<Terrain>();

        // 全周16方向（各22.5°）にスロープを配置。
        // 南側階段出口（degが160°〜200°付近、z < 482 かつ |x-512| < 5.5m）は階段通路のため除外
        const int rampCount = 16;
        for (int i = 0; i < rampCount; i++)
        {
            float outDeg = i * (360f / rampCount);
            float rad = outDeg * Mathf.Deg2Rad;
            Vector3 outDir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

            // テラス縁の位置と、スロープ外端の位置
            Vector3 innerP = new Vector3(512f, terraceTopY, 512f) + outDir * terraceRadius;
            Vector3 outerP = new Vector3(512f, 0f, 512f) + outDir * (terraceRadius + rampLength);

            // 南側階段出口（x∈[506.5, 517.5], z <= 483）は専用大理石階段が通るため除外
            if (innerP.z <= 483f && Mathf.Abs(innerP.x - 512f) <= 5.5f)
                continue;

            // 外端位置の地面（Terrain）高さを取得
            float groundY = 62.0f;
            if (terrain != null)
                groundY = terrain.SampleHeight(outerP) + terrain.transform.position.y;
            outerP.y = Mathf.Min(terraceTopY - 0.1f, groundY - 0.15f);

            // スロープの中心位置
            Vector3 slopeMid = (innerP + outerP) * 0.5f;
            Vector3 fwd = innerP - outerP;
            float actualLen = fwd.magnitude;
            if (actualLen < 0.2f) continue;

            // 傾き角度
            float slopeAngle = Mathf.Atan2(terraceTopY - outerP.y, rampLength) * Mathf.Rad2Deg;
            Quaternion rot = Quaternion.LookRotation(outDir, Vector3.up);

            // 幅（16分割の円周に合わせて隙間なく接続）
            float segWidth = 2f * (terraceRadius + rampLength * 0.5f) * Mathf.Sin((360f / rampCount * 0.5f) * Mathf.Deg2Rad) * 1.12f;

            var rampGo = new GameObject($"PodiumRamp_{outDeg:F0}deg");
            rampGo.transform.SetParent(rampsRoot.transform, false);
            rampGo.transform.position = slopeMid;
            rampGo.transform.rotation = rot * Quaternion.Euler(-slopeAngle, 0f, 0f);

            var box = rampGo.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size   = new Vector3(segWidth, rampThickness, actualLen * 1.15f);
        }
    }

    /// <summary>タワー白亜テラス上（またはレバー基壇上、スロープ上）なら歩行面Y、それ以外は負の無限大</summary>
    public static float GetTerraceSurfaceY(Vector3 worldPos)
    {
        // 台座天面は 63.00m。台座上面に乗っている／踏み込んでいる高度（62.85m以上）のみ有効。
        // 台座下層（62.85m未満）や東側崖下にいるプレイヤーを誤って台座上面に吸着させてはならない。
        if (worldPos.y < 62.85f || worldPos.y > 68.0f)
            return float.NegativeInfinity;

        Vector2 posXZ = new Vector2(worldPos.x, worldPos.z);

        // 1. 南正面メインレバー台座の範囲（上面 64.02m、半径3.2m）
        Vector2 mainLeverCenter = new Vector2(512f, 501.5f);
        if (Vector2.SqrMagnitude(posXZ - mainLeverCenter) <= 3.2f * 3.2f)
        {
            return 64.02f; // レバー台座の上面
        }

        // 1b. レバー台座アプローチスロープ（台座南側、テラス上面63m → 台座上64.02m）
        if (posXZ.x >= 508f && posXZ.x <= 516f && posXZ.y >= 476f && posXZ.y <= 501.5f)
        {
            if (posXZ.y >= 490f)
            {
                float stepT = Mathf.InverseLerp(490f, 501.5f, posXZ.y);
                return Mathf.Lerp(63.00f, 64.02f, stepT);
            }
            return 63.00f;
        }

        // 2. 白大理石円盤テラスの範囲（中心 512, 512、半径 35.0m、上面 63.00m）
        // ※南側アプローチ階段の回廊（x∈[507.5, 516.5], z <= 481）は専用階段スロープが処理
        if (posXZ.x >= 507.5f && posXZ.x <= 516.5f && posXZ.y <= 481f)
        {
            return float.NegativeInfinity;
        }

        Vector2 towerCenter = new Vector2(512f, 512f);
        if (Vector2.SqrMagnitude(posXZ - towerCenter) <= 35.0f * 35.0f)
        {
            return 63.00f; // テラス上面
        }

        return float.NegativeInfinity;
    }

    /// <summary>台座に潜ってしまったプレイヤーをテラス上面へ即座に安全救出する（下層埋まり・隙間ハマりを完全根絶）</summary>
    public void RescuePlayerIfBuriedInTerrace(AdventurePlayerController player)
    {
        if (player == null) return;
        if (player.IsSkybreakPillarAscending || player.IsAutoGliding) return;
        if (IsCanopyBroken && player.transform.position.y > 80f) return;

        Vector3 pos = player.transform.position;
        float dx = pos.x - 512f;
        float dz = pos.z - 512f;
        float distSq = dx * dx + dz * dz;

        // タワーテラスの範囲内（中心512,512から水平半径35.2m以内）
        if (distSq <= 35.2f * 35.2f)
        {
            // 南側階段の通常歩行（x∈[507.5, 516.5], z <= 481）は階段を登っている最中なので除外
            bool onStairs = (pos.z <= 481.5f && Mathf.Abs(pos.x - 512f) <= 4.8f);
            if (!onStairs)
            {
                // テラス床面(63.0m)よりわずかに下（62.80m〜62.98m）の床板・メッシュ隙間に挟まった時のみ救出。
                // 62.80m未満（床下・崖下・下からのジャンプ突入）は絶対に上面へワープさせない（下抜け・突き抜け防止）！
                if (pos.y < 62.98f && pos.y >= 62.80f)
                {
                    var cc = player.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    player.transform.position = new Vector3(pos.x, 63.02f, pos.z);
                    if (cc != null) cc.enabled = true;
                    player.ForceGroundReset();
                    Debug.LogWarning($"[RustAndFloat] テラス上面に挟まったプレイヤーを安全復帰: {pos} -> Y=63.02");
                }
            }
        }
    }

    #endregion

    #region レバー物理構造・台座・アプローチステップ

    static readonly string[] OrphanLeverNames =
    {
        "SanctuaryLeverStructure",
        "SanctuaryWestLeverStructure",
        "SanctuaryNorthLeverStructure",
        "SanctuaryEastLeverStructure",
        "SanctuaryTopLeverStructure",
    };

    /// <summary>
    /// 指定名のアクティブなレバーだけを消す。keep は作動中の本体なので残す。
    /// シーン全オブジェクトの配列化を避け、レバーを引いた瞬間の停止を防ぐ。
    /// </summary>
    void DestroyOrphanLevers(GameObject keep)
    {
        bool restore = keep != null && keep.activeSelf;
        if (restore)
            keep.SetActive(false);

        for (int n = 0; n < OrphanLeverNames.Length; n++)
        {
            string leverName = OrphanLeverNames[n];
            for (int i = 0; i < 8; i++)
            {
                var go = GameObject.Find(leverName);
                if (go == null || go == keep)
                    break;
                DestroyImmediate(go);
            }
        }

        if (restore)
            keep.SetActive(true);
    }

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

        // 2. マネージャー配下にいない古いレバーだけを名前で消す（全GameObject走査はしない）
        DestroyOrphanLevers(keep: null);

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
        var bColor = new Color(0.45f, 0.98f, 1.0f, 0.30f);
        bMat.color = bColor;
        bMat.SetColor("_BaseColor", bColor);
        bMat.SetColor("_Color", bColor);
        if (bMat.HasProperty("_Surface")) bMat.SetFloat("_Surface", 1f);
        if (bMat.HasProperty("_Blend")) bMat.SetFloat("_Blend", 1f);
        if (bMat.HasProperty("_Cull")) bMat.SetFloat("_Cull", 0f);
        if (bMat.HasProperty("_ZWrite")) bMat.SetFloat("_ZWrite", 0f);
        bMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        bMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        bMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        bMat.EnableKeyword("_BLENDMODE_ADD");
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
        pedSolid.transform.localPosition = new Vector3(0f, -0.55f, 0f);
        pedSolid.transform.localRotation = Quaternion.identity;
        pedSolid.transform.localScale = Vector3.one;
        var pedBox = pedSolid.AddComponent<BoxCollider>();
        pedBox.center = Vector3.zero;
        pedBox.size = new Vector3(6.2f, 3.2f, 6.2f);
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
        foreach (var c in rim.GetComponents<Collider>())
            Destroy(c);
        var rimSolid = new GameObject("MarbleRim_SolidWalk");
        rimSolid.transform.SetParent(root.transform, false);
        rimSolid.transform.localPosition = new Vector3(0f, -0.8f, 0f);
        var rimBox = rimSolid.AddComponent<BoxCollider>();
        rimBox.size = new Vector3(7.0f, 2.4f, 7.0f);
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

        const float TerraceFloorY = 63.00f; // 白亜大理石テラスの床面高さ
        Vector3 startP = new Vector3(472f, 48.5f, 455f); // オアシス池のほとり
        Vector3 endP = new Vector3(512f, TerraceFloorY, 478f); // テラス上面に段差ゼロでぴったり接続！
        if (land != null)
        {
            startP.y = land.SampleHeight(startP) + land.transform.position.y + 0.2f;
            endP.y = Mathf.Max(TerraceFloorY, land.SampleHeight(endP) + land.transform.position.y + 0.10f);
        }

        int steps = 22;
        float width = 4.8f;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var marbleMat = new Material(shader);
        marbleMat.SetColor("_BaseColor", new Color(0.92f, 0.94f, 0.96f)); // 純白大理石
        marbleMat.SetFloat("_Smoothness", 0.85f);

        var pillarMat = new Material(shader);
        pillarMat.SetColor("_BaseColor", new Color(0.82f, 0.85f, 0.88f));

        // 階段最上部からテラス床面への接続大理石スラブ
        // 上面をテラス床面より0.025mだけ高くオフセットし、同一平面重なり（Z-fighting/チカチカ）を物理的に完全解消
        var bridgeObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bridgeObj.name = "StairsToTerraceBridge";
        bridgeObj.transform.SetParent(stairsRoot.transform, false);
        bridgeObj.transform.position = new Vector3(512f, TerraceFloorY - 1.50f + 0.025f, 479.5f);
        bridgeObj.transform.localScale = new Vector3(width, 3.0f, 3.8f);
        var bridgeMr = bridgeObj.GetComponent<MeshRenderer>();
        if (bridgeMr != null) bridgeMr.material = marbleMat;

        Vector3[] stepPoints = new Vector3[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector3 p = Vector3.Lerp(startP, endP, t);
            if (land != null)
            {
                p.y = Mathf.Max(p.y, land.SampleHeight(p) + land.transform.position.y + 0.15f);
            }
            stepPoints[i] = p;
        }

        for (int i = 0; i < steps; i++)
        {
            Vector3 p0 = stepPoints[i];
            Vector3 p1 = stepPoints[i + 1];

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

            // ステップ自体のBoxColliderを保持（下から頭や体が板の中にめり込むのを完全阻止）
            var stepCol = stepObj.GetComponent<BoxCollider>();
            if (stepCol != null)
            {
                stepCol.size = Vector3.one;
            }

            // 全段に地面深くまで届く白亜大理石の石造り基礎壁（基壇）を隙間なく配置
            // これにより、下から潜り込んで頭や体が抜ける空間を視覚的・物理的に100%遮断する
            float groundUnder = center.y;
            if (land != null)
            {
                float gMid = land.SampleHeight(center) + land.transform.position.y;
                float gLeft = land.SampleHeight(center - right * (width * 0.5f)) + land.transform.position.y;
                float gRight = land.SampleHeight(center + right * (width * 0.5f)) + land.transform.position.y;
                groundUnder = Mathf.Min(gMid, Mathf.Min(gLeft, gRight));
            }
            float gap = Mathf.Max(0.2f, center.y - groundUnder);
            float baseHeight = gap + 0.8f; // 地面下0.8mまで深く食い込ませる
            var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseObj.name = $"MarbleStepFoundation_{i}";
            baseObj.transform.SetParent(stairsRoot.transform, false);
            baseObj.transform.position = new Vector3(center.x, center.y - (baseHeight * 0.5f - 0.16f), center.z);
            baseObj.transform.rotation = Quaternion.LookRotation(fwdNorm, Vector3.up);
            baseObj.transform.localScale = new Vector3(width * 1.02f, baseHeight, len * 1.08f);

            var bmr = baseObj.GetComponent<MeshRenderer>();
            if (bmr != null) bmr.material = marbleMat;

            // 階段の左右両脇から下へ潜り込まないよう、両サイドに地面まで届く側壁ガードを配置
            // （最上段はテラスへスムーズに抜けるため上面の高さをステップ天面に揃える）
            for (int side = -1; side <= 1; side += 2)
            {
                var sideWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sideWall.name = $"MarbleSideWall_{i}_{(side < 0 ? "L" : "R")}";
                sideWall.transform.SetParent(stairsRoot.transform, false);
                Vector3 wallPos = center + right * (width * 0.5f + 0.25f) * side;
                float wallH = (i == steps - 1) ? baseHeight : (baseHeight + 0.5f);
                wallPos.y = center.y - (baseHeight * 0.5f - 0.16f);
                sideWall.transform.position = wallPos;
                sideWall.transform.rotation = Quaternion.LookRotation(fwdNorm, Vector3.up);
                sideWall.transform.localScale = new Vector3(0.5f, wallH, len * 1.08f);
                var swMr = sideWall.GetComponent<MeshRenderer>();
                if (swMr != null) swMr.material = marbleMat;
            }

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

        // 階段全体を覆う精密・重厚スロープコライダー（各段完全一致・厚み1.4m・両脇安全ガード壁）
        AddStairsRampColliders(stairsRoot.transform, stepPoints, width);
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
    /// アプローチ階段最下部および池〜階段進入路付近の岩が歩行阻害しないようクリアにする。
    /// </summary>
    static void SoftenRocksNearStairFoot(Vector3 stairStart)
    {
        var all = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude);
        for (int i = 0; i < all.Length; i++)
        {
            var col = all[i];
            if (col == null) continue;
            string n = col.gameObject.name;
            if (n.IndexOf("Rock", System.StringComparison.OrdinalIgnoreCase) < 0
                && n.IndexOf("Stone", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            Vector3 c = col.bounds.center;
            float dx = c.x - stairStart.x;
            float dz = c.z - stairStart.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            // 階段入口の足元（6.0m以内）にある岩は歩行の引っ掛かり・めり込み防止のため非アクティブ化
            if (dist < 6.0f)
            {
                col.gameObject.SetActive(false);
            }
        }
    }

    static void SoftenLooseRocksNearCorridor(Vector3 startP, Vector3 endP, Vector3 oasis)
    {
        var all = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude);
        for (int i = 0; i < all.Length; i++)
        {
            var col = all[i];
            if (col == null) continue;
            string n = col.gameObject.name;
            if (n.IndexOf("Rock", System.StringComparison.OrdinalIgnoreCase) < 0
                && n.IndexOf("Stone", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            Vector3 c = col.bounds.center;
            float distPath = DistancePointToSegment(c, startP, endP);

            // 1. 階段回廊およびアプローチ進路周辺（幅6.5m以内）に重なる岩は非アクティブ化して完全な登坂空間を確保
            if (distPath < 6.5f)
            {
                col.gameObject.SetActive(false);
                continue;
            }

            // 2. タワー横の崖・台座外周全域（東側断崖・南東・南・南西・階段横、中心(512,512)から半径55m以内、Y∈[28, 65]）にある岩は
            //    プレイヤーが崖下やテラス横で挟まったりスタックしないよう完全通行可能（トリガー化／通過許可）にする
            float dxTower = c.x - 512f;
            float dzTower = c.z - 512f;
            float distTowerSq = dxTower * dxTower + dzTower * dzTower;
            if (distTowerSq <= 55f * 55f && c.y >= 28f && c.y <= 65f)
            {
                MakeColliderPassable(col);
            }
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
                    MakeColliderPassable(col);
            }
            else if (soft)
            {
                MakeColliderPassable(col);
            }
        }
    }

    static void MakeColliderPassable(Collider col)
    {
        if (col == null) return;
        if (col is MeshCollider mc && !mc.convex)
        {
            // Concave MeshCollider は PhysX で isTrigger=true が未サポートのため enabled=false で通行可能にする
            col.enabled = false;
            return;
        }
        col.isTrigger = true;
    }

    static float DistancePointToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float denom = ab.sqrMagnitude;
        if (denom < 0.01f) return Vector3.Distance(p, a);
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / denom);
        return Vector3.Distance(p, a + ab * t);
    }

    void AddStairsRampColliders(Transform parent, Vector3[] points, float width)
    {
        const float thickness      = 1.4f;  // 1.4mの重厚コライダー（下面が地面深く食い込み裏抜け・隙間ゼロ）
        const float stepHalfHeight = 0.16f; // ステップ天面
        const float guardHeight    = 1.4f;  // 両脇安全ガード壁高さ
        const float guardThickness = 0.45f; // 両脇安全ガード壁厚み

        var terrain = Terrain.activeTerrain ?? FindAnyObjectByType<Terrain>();
        Vector3 startP = points[0];
        Vector3 endP = points[points.Length - 1];
        Vector3 totalForward = (endP - startP).normalized;

        // 1. 入口エントランス台（池の岸辺からなだらかに大理石階段へ登れるワイドスロープ）
        Vector3 entryStart = startP - totalForward * 3.8f;
        if (terrain != null)
        {
            entryStart.y = terrain.SampleHeight(entryStart) + terrain.transform.position.y - 0.25f;
        }
        else
        {
            entryStart.y = startP.y - 0.6f;
        }

        Vector3 entryEnd = startP + totalForward * 0.8f;
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
            entryBox.size   = new Vector3(width * 1.35f, thickness, entryLen * 1.15f);
        }

        // 2. ステップ数と1対1で完全一致する精密・高密度スロープコライダー（22分割）
        int segCount = points.Length - 1;
        for (int s = 0; s < segCount; s++)
        {
            Vector3 p0 = points[s];
            Vector3 p1 = points[s + 1];

            Vector3 segCenter  = (p0 + p1) * 0.5f;
            Vector3 segForward = p1 - p0;
            float   segLen     = segForward.magnitude;
            if (segLen < 0.01f) continue;

            Vector3 segFwdNorm = segForward.normalized;

            var rampGo = new GameObject($"StairRamp_{s}");
            rampGo.transform.SetParent(parent, false);
            rampGo.transform.position = segCenter;
            rampGo.transform.rotation = Quaternion.LookRotation(segFwdNorm, Vector3.up);
            rampGo.transform.localScale = Vector3.one;

            // 地面（Terrain）との落差を取得し、コライダー厚みを地面深くまで自動延長
            float gUnder = segCenter.y;
            if (terrain != null)
                gUnder = terrain.SampleHeight(segCenter) + terrain.transform.position.y;
            float segDrop = Mathf.Max(0f, segCenter.y - gUnder);
            float actualThickness = Mathf.Max(thickness, segDrop + 1.2f);

            // 歩行床スロープ（上面はステップ天面、下面は地面深く食い込み隙間ゼロ・下抜け100%遮断）
            var box = rampGo.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, stepHalfHeight - actualThickness * 0.5f, 0f);
            box.size   = new Vector3(width, actualThickness, segLen * (s == segCount - 1 ? 1.15f : 1.08f));

            // 3. 両脇の見えない安全ガード壁コライダー（階段からの滑落・岩隙間挟まり防止）
            // 最上段（s == segCount - 1）はテラス出口のため、手すり壁がテラス進入を妨げないよう除外
            if (s < segCount - 1)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var guardGo = new GameObject($"SideGuard_{s}_{(side < 0 ? "L" : "R")}");
                    guardGo.transform.SetParent(rampGo.transform, false);
                    guardGo.transform.localPosition = new Vector3((width * 0.5f + guardThickness * 0.5f) * side, stepHalfHeight + guardHeight * 0.5f - 0.2f, 0f);
                    var guardBox = guardGo.AddComponent<BoxCollider>();
                    guardBox.size = new Vector3(guardThickness, guardHeight, segLen * 1.08f);
                }
            }
        }
    }

    /// <summary>台座東側〜南東側の崖下空洞をソリッドBoxCollider群で密閉（下からの突き抜け・頭挟まりを物理的に100%遮断）</summary>
    void EnsurePodiumCliffFoundation(GameObject tower)
    {
        var found = tower.transform.Find("PodiumCliffFoundation");
        if (found == null)
        {
            var go = new GameObject("PodiumCliffFoundation");
            go.transform.SetParent(tower.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            found = go.transform;
        }

        // 過去に付与されていた不要なメッシュ描画コンポーネント（座標オフセットにより空中に浮遊するバグ原因）を完全破棄
        var mr = found.GetComponent<MeshRenderer>();
        if (mr != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(mr);
            else Destroy(mr);
#else
            Destroy(mr);
#endif
        }
        var mf = found.GetComponent<MeshFilter>();
        if (mf != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(mf);
            else Destroy(mf);
#else
            Destroy(mf);
#endif
        }

        // 東側崖下からの侵入・頭突き抜けを物理的に100%遮断するソリッドBoxCollider群を保証
        var wallBoxes = found.Find("SolidWallColliders");
        if (wallBoxes == null)
        {
            var wallRoot = new GameObject("SolidWallColliders");
            wallRoot.transform.SetParent(found, false);
            wallRoot.transform.localPosition = Vector3.zero;
            wallRoot.transform.localRotation = Quaternion.identity;

            var terr = Terrain.activeTerrain ?? FindAnyObjectByType<Terrain>();
            const float topY = 62.35f;
            const float radius = 34.2f;
            const float thickness = 2.5f;

            void BuildBoxes(int count, float startDeg, float endDeg, string prefix)
            {
                float arcLen = (endDeg - startDeg) * Mathf.Deg2Rad * radius;
                float segWidth = (arcLen / count) * 1.15f;
                for (int i = 0; i < count; i++)
                {
                    float t = (float)(i + 0.5f) / count;
                    float deg = Mathf.Lerp(startDeg, endDeg, t);
                    float rad = deg * Mathf.Deg2Rad;
                    float nx = Mathf.Sin(rad);
                    float nz = Mathf.Cos(rad);
                    Vector3 outDir = new Vector3(nx, 0, nz);
                    float px = 512f + nx * radius;
                    float pz = 512f + nz * radius;
                    float gh = 45f;
                    if (terr != null) gh = terr.SampleHeight(new Vector3(px, 0, pz)) + terr.transform.position.y;
                    float botY = Mathf.Min(topY - 1.0f, gh - 3.0f);
                    float h = topY - botY;
                    float midY = (topY + botY) * 0.5f;

                    var bGo = new GameObject($"{prefix}_{deg:F0}deg");
                    bGo.transform.SetParent(wallRoot.transform, false);
                    bGo.transform.position = new Vector3(px, midY, pz);
                    bGo.transform.rotation = Quaternion.LookRotation(outDir, Vector3.up);
                    var box = bGo.AddComponent<BoxCollider>();
                    box.size = new Vector3(segWidth, h, thickness);
                }
            }

            BuildBoxes(24, 60f, 182f, "EastWallBox");
            BuildBoxes(6, 184.5f, 205f, "WestWallBox");
        }
    }

    #endregion
}
