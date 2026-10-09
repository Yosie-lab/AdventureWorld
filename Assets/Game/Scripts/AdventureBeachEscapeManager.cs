using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 砂浜・海岸線からの快適な帰還をサポートするマネージャー
/// 1. 外周白砂ビーチ8方位への「海風サーマル上昇気流（Beach Thermal Updrafts）」自動配置
/// 2. 砂浜から内陸草地へ繋がる「木製ウッドデッキスロープ道（Boardwalk Ramps）」設置
/// 3. 初めて砂浜へ降りた時の相棒Rustの誘導ガイド
/// </summary>
public class AdventureBeachEscapeManager : MonoBehaviour
{
    static AdventureBeachEscapeManager _instance;
    public static AdventureBeachEscapeManager Instance => _instance;

    bool _hasNotifiedBeachGuide = false;

    Material _cachedWoodMat;
    Material _cachedPostMat;
    Material _cachedLanternMat;
    Material _cachedDriftwoodMat;

    public static void Ensure()
    {
        var existing = Object.FindObjectsByType<AdventureBeachEscapeManager>(FindObjectsInactive.Include);
        foreach (var ex in existing)
        {
            if (ex != null && ex.gameObject != null)
                Destroy(ex.gameObject);
        }
        _instance = null;

        var go = new GameObject("AdventureBeachEscapeManager");
        _instance = go.AddComponent<AdventureBeachEscapeManager>();
    }

    void Awake()
    {
        _instance = this;
        InitMaterials();
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    void InitMaterials()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        _cachedWoodMat = new Material(shader);
        _cachedWoodMat.SetColor("_BaseColor", new Color(0.50f, 0.40f, 0.30f, 1.0f));

        _cachedPostMat = new Material(shader);
        _cachedPostMat.SetColor("_BaseColor", new Color(0.38f, 0.29f, 0.20f, 1.0f));

        _cachedDriftwoodMat = new Material(shader);
        _cachedDriftwoodMat.SetColor("_BaseColor", new Color(0.42f, 0.35f, 0.28f, 1.0f));

        _cachedLanternMat = new Material(shader);
        _cachedLanternMat.SetColor("_BaseColor", new Color(1.0f, 0.92f, 0.65f, 1.0f));
        _cachedLanternMat.EnableKeyword("_EMISSION");
        _cachedLanternMat.SetColor("_EmissionColor", new Color(1.0f, 0.85f, 0.45f) * 1.8f);
    }

    void Start()
    {
        var land = Terrain.activeTerrain ?? FindAnyObjectByType<Terrain>();
        float waterY = 5.5f;
        var bounds = AdventureIslandBoundary.Instance;
        if (bounds != null && !float.IsNegativeInfinity(bounds.waterLevel))
            waterY = bounds.waterLevel;

        var root = new GameObject("BeachEscapeStructures");
        root.transform.SetParent(transform, false);

        BuildBeachThermalVents(root.transform, land, waterY);
        BuildBeachBoardwalkRamps(root.transform, land, waterY);
    }

    void Update()
    {
        if (_hasNotifiedBeachGuide) return;

        var player = AdventurePlayerController.Instance;
        if (player == null) return;
        // プロローグ中・最初の滑空前は喋らない（関門2：冒頭の独り言を減らす）
        if (!player.HasEverGlided) return;
        var prologue = AdventurePrologueDrama.Instance;
        if (prologue != null && prologue.IsPrologueActive) return;

        if (player.IsInBeachOrCoastZone(player.transform.position))
        {
            _hasNotifiedBeachGuide = true;
            var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
            if (drone != null)
            {
                drone.SpeakCustom("広大な砂浜だね！戻るときはSpaceキーで海風に乗るか、海沿いの上昇気流や木道を使ってね！", 5.0f);
            }
        }
    }

    /// <summary>外周白砂ビーチの8方位に海風サーマル上昇気流を配置</summary>
    void BuildBeachThermalVents(Transform parent, Terrain land, float waterY)
    {
        var thermalRoot = new GameObject("BeachThermalVents");
        thermalRoot.transform.SetParent(parent, false);

        Vector3 center = new Vector3(512f, 0f, 512f);
        float radius = 438f; // 白砂ビーチの主要波打ち際〜砂浜テラス

        if (land != null && land.terrainData != null)
        {
            Vector3 origin = land.transform.position;
            Vector3 size = land.terrainData.size;
            center = new Vector3(origin.x + size.x * 0.5f, 0f, origin.z + size.z * 0.5f);
            radius = size.x * 0.428f;
        }

        // 8方位（45度間隔）に海風サーマルを配置
        for (int i = 0; i < 8; i++)
        {
            float angleDeg = i * 45f;
            float rad = angleDeg * Mathf.Deg2Rad;
            float x = center.x + Mathf.Cos(rad) * radius;
            float z = center.z + Mathf.Sin(rad) * radius;

            float groundY = waterY;
            if (land != null)
            {
                groundY = land.SampleHeight(new Vector3(x, 0f, z)) + land.transform.position.y;
                groundY = Mathf.Max(groundY, waterY);
            }

            Vector3 ventPos = new Vector3(x, groundY, z);
            CreateBeachThermal(thermalRoot.transform, ventPos, center, i);
        }
    }

    void CreateBeachThermal(Transform parent, Vector3 position, Vector3 islandCenter, int index)
    {
        var thermalGo = new GameObject($"BeachThermal_{index}");
        thermalGo.transform.SetParent(parent, false);
        thermalGo.transform.position = position;

        // 島の内陸を向くように回転
        Vector3 inwardDir = islandCenter - position;
        inwardDir.y = 0f;
        if (inwardDir.sqrMagnitude > 0.1f)
            thermalGo.transform.rotation = Quaternion.LookRotation(inwardDir.normalized);

        var updraft = thermalGo.AddComponent<AdventureThermalUpdraft>();
        updraft.radius = 12.0f; // 広々としたエリアでキャッチ
        updraft.height = 55.0f; // 高度55mまで一気に吹き上げる
        updraft.liftSpeed = 6.2f; // 心地よくスルスルと上昇

        // サーマル地点を示す目印の流木柱（Driftwood Marker）
        var markerGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        markerGo.name = "DriftwoodThermalMarker";
        markerGo.transform.SetParent(thermalGo.transform, false);
        markerGo.transform.localPosition = new Vector3(0f, 1.8f, 0f);
        markerGo.transform.localScale = new Vector3(0.45f, 1.8f, 0.45f);
        markerGo.transform.localRotation = Quaternion.Euler(6f, (index * 47f) % 360f, -4f);

        var mr = markerGo.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.material = _cachedDriftwoodMat;
        }

        var col = markerGo.GetComponent<Collider>();
        if (col != null) col.isTrigger = true; // プレイヤーの邪魔にならない
    }

    /// <summary>砂浜から内陸草地へ快適に歩いて登れる木製ウッドデッキスロープ道（Boardwalk Ramps）を主要海岸に設置</summary>
    void BuildBeachBoardwalkRamps(Transform parent, Terrain land, float waterY)
    {
        var rampsRoot = new GameObject("BeachBoardwalkRamps");
        rampsRoot.transform.SetParent(parent, false);

        Vector3 center = new Vector3(512f, 0f, 512f);
        if (land != null && land.terrainData != null)
        {
            Vector3 origin = land.transform.position;
            Vector3 size = land.terrainData.size;
            center = new Vector3(origin.x + size.x * 0.5f, 0f, origin.z + size.z * 0.5f);
        }

        // 外周の主要海岸から、崖上の安全な草地テラスへ登る要所
        // （内陸深部のオアシス渓流池やタワー前には絶対に侵入させない角度を選定）
        float[] angles = {
            180f, // 西側ビーチ
            205f, // 南西ビーチ（スタート海岸南）
            270f, // 南側ビーチ
            315f, // 北西ビーチ
            0f,   // 東側ビーチ
            60f,  // 北東ビーチ
        };

        for (int i = 0; i < angles.Length; i++)
            PlaceAngleBoardwalk(rampsRoot.transform, land, waterY, center, angles[i]);

        // スタート座礁艇前：浜辺から西の台地へ登るメイン木道（標高20mの草地テラスで自然に接地完了）
        PlacePinnedBoardwalk(
            rampsRoot.transform, land, waterY,
            beachXZ: new Vector2(152f, 268f),
            inlandXZ: new Vector2(240f, 310f),
            name: "BoardwalkRamp_SpawnWest");
    }

    void PlaceAngleBoardwalk(Transform parent, Terrain land, float waterY, Vector3 center, float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)).normalized;

        // 砂浜の始点（波打ち際より少し上の安定した砂浜面）
        float rBeach = 448f;
        if (land != null)
        {
            while (rBeach > 390f && (land.SampleHeight(center + dir * rBeach) + land.transform.position.y) < waterY + 0.35f)
                rBeach -= 2f;
        }

        // 内陸側の終点：標高18〜22mの草地テラスに到達した地点で終了
        // タワー手前の渓流池（標高45〜50m、半径150m以内）へは絶対に進入させない
        float rInland = rBeach - 65f;
        if (land != null)
        {
            for (float r = rBeach - 30f; r >= 280f; r -= 5f)
            {
                Vector3 checkP = center + dir * r;
                float h = land.SampleHeight(checkP) + land.transform.position.y;
                // タワー前ゾーン（中心から150m以内）や高地（標高26m以上）には入らない
                if (r < 220f || h >= 22f)
                {
                    rInland = r;
                    break;
                }
                rInland = r;
            }
        }

        Vector3 beachPoint = center + dir * rBeach;
        Vector3 inlandPoint = center + dir * rInland;

        if (land != null)
        {
            beachPoint.y = land.SampleHeight(beachPoint) + land.transform.position.y;
            inlandPoint.y = land.SampleHeight(inlandPoint) + land.transform.position.y;
        }
        else
        {
            beachPoint.y = waterY + 0.35f;
            inlandPoint.y = waterY + 12f;
        }

        // 高低差がほぼ無い平坦帯はスキップ
        if (Mathf.Abs(inlandPoint.y - beachPoint.y) < 0.8f && inlandPoint.y < waterY + 3.0f)
            return;

        // 勾配を緩く（約9〜12度）保つよう水平距離を確保（内陸深部ではなく崖上草地テラスで収束）
        EnsureGentleGrade(ref beachPoint, ref inlandPoint, land, center, dir);

        CreateBoardwalkRamp(parent, beachPoint, inlandPoint, $"BoardwalkRamp_{Mathf.RoundToInt(deg)}deg", land);
    }

    void PlacePinnedBoardwalk(Transform parent, Terrain land, float waterY, Vector2 beachXZ, Vector2 inlandXZ, string name)
    {
        Vector3 beachPoint = new Vector3(beachXZ.x, waterY + 0.35f, beachXZ.y);
        Vector3 inlandPoint = new Vector3(inlandXZ.x, waterY + 12f, inlandXZ.y);
        if (land != null)
        {
            beachPoint.y = Mathf.Max(waterY + 0.25f, land.SampleHeight(beachPoint) + land.transform.position.y);
            inlandPoint.y = land.SampleHeight(inlandPoint) + land.transform.position.y;
        }
        EnsureGentleGrade(ref beachPoint, ref inlandPoint, land, default, default);
        CreateBoardwalkRamp(parent, beachPoint, inlandPoint, name, land);
    }

    /// <summary>
    /// 高低差に対して水平距離を確保し、最大勾配を約10〜12°（歩きやすい自然なスロープ）に調整
    /// ただし標高24m以上の高地やタワー手前には侵入させない
    /// </summary>
    static void EnsureGentleGrade(ref Vector3 beachPoint, ref Vector3 inlandPoint, Terrain land, Vector3 center, Vector3 dir)
    {
        const float maxGrade = 0.20f; // 約11.3度（歩きやすく自然な登坂勾配）
        Vector3 flat = inlandPoint - beachPoint;
        flat.y = 0f;
        float horiz = flat.magnitude;
        float rise = inlandPoint.y - beachPoint.y;
        if (horiz < 1f) return;

        float need = Mathf.Abs(rise) / maxGrade;
        if (need <= horiz) return;

        Vector3 flatDir = flat / horiz;
        if (dir.sqrMagnitude > 0.01f)
        {
            Vector3 inward = -dir;
            if (Vector3.Dot(inward, flatDir) > 0.2f)
                flatDir = inward.normalized;
        }

        Vector3 testInland = beachPoint + flatDir * need;
        if (land != null)
        {
            float testY = land.SampleHeight(testInland) + land.transform.position.y;
            // 標高24m以上の山地や高台へは伸ばさず、崖上テラスで止める
            if (testY <= 24f)
            {
                inlandPoint = testInland;
                inlandPoint.y = testY;
            }
        }
        else
        {
            inlandPoint = testInland;
        }
    }

    void CreateBoardwalkRamp(Transform parent, Vector3 beachPoint, Vector3 inlandPoint, string rampName, Terrain land)
    {
        var rampGo = new GameObject(rampName);
        rampGo.transform.SetParent(parent, false);

        int segments = 32;
        float width = 3.8f;
        const float PlankThickness = 0.14f; // 14cmのしっかりした木の厚板（壁ではなく板！）

        // 始点と終点を大地（地形表面）に完全に接地させる
        if (land != null)
        {
            beachPoint.y = land.SampleHeight(beachPoint) + land.transform.position.y;
            inlandPoint.y = land.SampleHeight(inlandPoint) + land.transform.position.y;
        }

        Vector3[] points = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            // 始点(t=0)と終点(t=1)は完全に大地に接地（t=0では砂に埋まり、t=1では草地に埋まる）
            float u = t * t * (3f - 2f * t); // smoothstep
            Vector3 p = Vector3.Lerp(beachPoint, inlandPoint, t);
            float smoothY = Mathf.Lerp(beachPoint.y, inlandPoint.y, u);

            if (land != null)
            {
                float ty = land.SampleHeight(p) + land.transform.position.y;
                // 始点と終点は段差ゼロで地面にすーっと接続（-0.01mでわずかに沈み込む）
                if (i == 0 || i == segments)
                {
                    p.y = ty - 0.01f;
                }
                else
                {
                    // 途中の起伏：地形より下には潜らず、かつ急なデコボコを均す
                    float groundTarget = ty + 0.06f;
                    p.y = Mathf.Max(smoothY, groundTarget);
                }
            }
            else
            {
                p.y = smoothY;
            }
            points[i] = p;
        }

        // デッキ板（Plank）の配置：薄い厚板（0.14m）として配置し、壁化を根絶
        for (int i = 0; i < segments; i++)
        {
            Vector3 p0 = points[i];
            Vector3 p1 = points[i + 1];
            Vector3 center = (p0 + p1) * 0.5f;
            Vector3 forward = (p1 - p0);
            float length = forward.magnitude;
            if (length < 0.01f) continue;

            Vector3 fwdNorm = forward.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwdNorm).normalized;

            // 地面高さを取得
            float groundUnder = center.y;
            if (land != null)
                groundUnder = land.SampleHeight(center) + land.transform.position.y;

            // 板の中心位置：上面が自然な歩行面になり、厚みは0.14m（板の厚み）
            float plankCenterY = center.y + PlankThickness * 0.5f;

            var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plank.name = $"Plank_{i}";
            plank.transform.SetParent(rampGo.transform, false);
            plank.transform.position = new Vector3(center.x, plankCenterY, center.z);
            plank.transform.rotation = Quaternion.LookRotation(fwdNorm, Vector3.up);
            plank.transform.localScale = new Vector3(width, PlankThickness, length * 1.04f);

            var mr = plank.GetComponent<MeshRenderer>();
            if (mr != null) mr.material = _cachedWoodMat;

            // 板が地面から浮いている（0.3m以上の落差がある）場合は、
            // 「壁」で塞ぐのではなく、本物の桟橋・木道のように木製丸太支柱（Wooden Piles）を地面まで下ろす
            float gap = center.y - groundUnder;
            if (gap > 0.35f && (i % 3 == 0 || i == segments - 1))
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float pileHeight = gap + PlankThickness;
                    var pile = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    pile.name = $"WoodenPile_{i}_{(side < 0 ? "L" : "R")}";
                    pile.transform.SetParent(rampGo.transform, false);
                    pile.transform.position = center + right * (side * (width * 0.5f - 0.25f)) - Vector3.up * (pileHeight * 0.5f - PlankThickness);
                    pile.transform.localScale = new Vector3(0.24f, pileHeight * 0.5f, 0.24f);

                    var pileMr = pile.GetComponent<MeshRenderer>();
                    if (pileMr != null) pileMr.material = _cachedPostMat;

                    var pcol = pile.GetComponent<Collider>();
                    if (pcol != null) Object.DestroyImmediate(pcol);
                }
            }

            // 4セグメントごとに両脇に手すり支柱（Wooden Posts）を配置
            if (i % 4 == 0 || i == segments - 1)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    post.name = $"RailingPost_{i}_{(side < 0 ? "L" : "R")}";
                    post.transform.SetParent(rampGo.transform, false);
                    post.transform.position = center + right * (side * (width * 0.5f - 0.15f)) + Vector3.up * (PlankThickness + 0.45f);
                    post.transform.localScale = new Vector3(0.18f, 0.45f, 0.18f);

                    var postMr = post.GetComponent<MeshRenderer>();
                    if (postMr != null) postMr.material = _cachedPostMat;

                    var col = post.GetComponent<Collider>();
                    if (col != null) Object.DestroyImmediate(col);
                }
            }
        }

        // 入口（砂浜側）と出口（内陸側）に案内ランタンポストを配置

        CreateLanternPost(rampGo.transform, points[0], Vector3.Cross(Vector3.up, (points[1] - points[0]).normalized).normalized * (width * 0.5f + 0.6f));
        CreateLanternPost(rampGo.transform, points[segments], Vector3.Cross(Vector3.up, (points[segments] - points[segments - 1]).normalized).normalized * (width * 0.5f + 0.6f));
    }

    void CreateLanternPost(Transform parent, Vector3 basePos, Vector3 offset)
    {
        var lanternRoot = new GameObject("LanternPost");
        lanternRoot.transform.SetParent(parent, false);
        lanternRoot.transform.position = basePos + offset;

        // 支柱
        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.transform.SetParent(lanternRoot.transform, false);
        post.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        post.transform.localScale = new Vector3(0.22f, 1.2f, 0.22f);
        var pmr = post.GetComponent<MeshRenderer>();
        if (pmr != null) pmr.material = _cachedPostMat;
        var pcol = post.GetComponent<Collider>();
        if (pcol != null) pcol.isTrigger = true;

        // ランタン本体（発光体）
        var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lamp.transform.SetParent(lanternRoot.transform, false);
        lamp.transform.localPosition = new Vector3(0f, 2.3f, 0f);
        lamp.transform.localScale = Vector3.one * 0.42f;
        var lmr = lamp.GetComponent<MeshRenderer>();
        if (lmr != null) lmr.material = _cachedLanternMat;
        var lcol = lamp.GetComponent<Collider>();
        if (lcol != null) lcol.isTrigger = true;

        // 温かい点光源ライト
        var light = lanternRoot.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1.0f, 0.88f, 0.65f);
        light.intensity = 2.4f;
        light.range = 16f;
    }
}
