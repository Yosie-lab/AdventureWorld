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
        var existing = Object.FindObjectsByType<AdventureBeachEscapeManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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

        // 漂着ゴミや探索スポットが密集する南西〜西ビーチ（195°〜235°）を中心に、各海岸線へ木道を多数設置
        float[] angles = {
            195f, // 南西ビーチ（漂着ゴミ・2050年AIドローンプロペラ前）
            212f, // 南西ビーチ中央（生体追跡リング・健康バンド前）
            228f, // 南西ビーチ奥（2040年ロボットギア・2030年スマホ前）
            180f, // 真西ビーチテラス（Hawaii Beach Houseヤシ林前）
            245f, // 南南西ビーチ
            270f, // 真南ビーチ
            295f, // 北西ビーチ（高密度電源結晶前）
            0f,   // 真東ビーチ
            60f   // 北東ビーチ
        };

        for (int i = 0; i < angles.Length; i++)
        {
            float deg = angles[i];
            float rad = deg * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)).normalized;

            // 砂浜の波打ち際手前（半径約445m）から内陸の草原（半径約370m）へ向かってウッドデッキ道を敷設
            Vector3 beachPoint = center + dir * 445f;
            Vector3 inlandPoint = center + dir * 370f;

            if (land != null)
            {
                beachPoint.y = Mathf.Max(waterY + 0.35f, land.SampleHeight(beachPoint) + land.transform.position.y);
                inlandPoint.y = land.SampleHeight(inlandPoint) + land.transform.position.y;
            }
            else
            {
                beachPoint.y = waterY + 0.35f;
                inlandPoint.y = waterY + 12f;
            }

            CreateBoardwalkRamp(rampsRoot.transform, beachPoint, inlandPoint, $"BoardwalkRamp_{Mathf.RoundToInt(deg)}deg", land);
        }
    }

    void CreateBoardwalkRamp(Transform parent, Vector3 beachPoint, Vector3 inlandPoint, string rampName, Terrain land)
    {
        var rampGo = new GameObject(rampName);
        rampGo.transform.SetParent(parent, false);

        int segments = 28; // 高密度分割で地形の傾斜にぴったり追従
        float width = 4.2f; // ゆったり広々歩ける幅広ウッドデッキ
        Vector3 totalDir = (inlandPoint - beachPoint);

        // 各セグメントの標高を地形から計算
        Vector3[] points = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 p = Vector3.Lerp(beachPoint, inlandPoint, t);
            if (land != null)
            {
                float ty = land.SampleHeight(p) + land.transform.position.y;
                // 砂浜の端（t=0）は地面に少し埋め込んでスムーズに乗り込めるようにし、中間〜頂上は地面より+0.18m浮かせる
                float lift = Mathf.Lerp(0.04f, 0.22f, Mathf.Sin(t * Mathf.PI * 0.5f));
                p.y = Mathf.Max(p.y, ty + lift);
            }
            points[i] = p;
        }

        // デッキ板（Plank）の配置
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

            var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plank.name = $"Plank_{i}";
            plank.transform.SetParent(rampGo.transform, false);
            plank.transform.position = center;
            plank.transform.rotation = Quaternion.LookRotation(fwdNorm, Vector3.up);
            plank.transform.localScale = new Vector3(width, 0.30f, length * 1.04f);

            var mr = plank.GetComponent<MeshRenderer>();
            if (mr != null) mr.material = _cachedWoodMat;

            // 4セグメントごとに両脇に手すり支柱（Wooden Posts）を配置
            if (i % 4 == 0 || i == segments - 1)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    post.name = $"RailingPost_{i}_{(side < 0 ? "L" : "R")}";
                    post.transform.SetParent(rampGo.transform, false);
                    post.transform.position = center + right * (side * (width * 0.5f - 0.15f)) + Vector3.up * 0.55f;
                    post.transform.localScale = new Vector3(0.18f, 0.55f, 0.18f);

                    var postMr = post.GetComponent<MeshRenderer>();
                    if (postMr != null) postMr.material = _cachedPostMat;

                    var col = post.GetComponent<Collider>();
                    if (col != null) col.isTrigger = true; // 歩行の邪魔にならない
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
