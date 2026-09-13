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
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0.42f, 0.35f, 0.28f, 1.0f));
            mr.material = mat;
        }

        var col = markerGo.GetComponent<Collider>();
        if (col != null) col.isTrigger = true; // プレイヤーの邪魔にならない
    }

    /// <summary>砂浜から内陸草地へ駆け上がれる木製ウッドデッキスロープ道（Boardwalk Ramps）を東西南北に設置</summary>
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

        // 4大方角のビーチ〜内陸草地スロープ（西、南、東、北西）
        float[] angles = { 180f, 270f, 0f, 135f };
        for (int i = 0; i < angles.Length; i++)
        {
            float deg = angles[i];
            float rad = deg * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)).normalized;

            // 砂浜の端（半径約440m）から内陸の草原（半径約370m）へ向かってウッドデッキ道を敷設
            Vector3 beachPoint = center + dir * 440f;
            Vector3 inlandPoint = center + dir * 370f;

            if (land != null)
            {
                beachPoint.y = Mathf.Max(waterY + 0.3f, land.SampleHeight(beachPoint) + land.transform.position.y);
                inlandPoint.y = land.SampleHeight(inlandPoint) + land.transform.position.y;
            }
            else
            {
                beachPoint.y = waterY + 0.3f;
                inlandPoint.y = waterY + 12f;
            }

            CreateBoardwalkRamp(rampsRoot.transform, beachPoint, inlandPoint, $"Ramp_{i}", land);
        }
    }

    void CreateBoardwalkRamp(Transform parent, Vector3 beachPoint, Vector3 inlandPoint, string rampName, Terrain land)
    {
        var rampGo = new GameObject(rampName);
        rampGo.transform.SetParent(parent, false);

        int segments = 16;
        float width = 3.6f; // ゆったり走れる幅広ウッドデッキ
        Vector3 totalDir = (inlandPoint - beachPoint);
        Vector3 rightDir = Vector3.Cross(Vector3.up, totalDir).normalized;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var woodMat = new Material(shader);
        woodMat.SetColor("_BaseColor", new Color(0.48f, 0.38f, 0.28f, 1.0f));

        for (int i = 0; i < segments; i++)
        {
            float t0 = (float)i / segments;
            float t1 = (float)(i + 1) / segments;

            Vector3 p0 = Vector3.Lerp(beachPoint, inlandPoint, t0);
            Vector3 p1 = Vector3.Lerp(beachPoint, inlandPoint, t1);

            if (land != null)
            {
                p0.y = Mathf.Max(p0.y, land.SampleHeight(p0) + land.transform.position.y + 0.22f);
                p1.y = Mathf.Max(p1.y, land.SampleHeight(p1) + land.transform.position.y + 0.22f);
            }

            Vector3 center = (p0 + p1) * 0.5f;
            Vector3 forward = (p1 - p0);
            float length = forward.magnitude;
            if (length < 0.01f) continue;

            var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plank.name = $"Plank_{i}";
            plank.transform.SetParent(rampGo.transform, false);
            plank.transform.position = center;
            plank.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            plank.transform.localScale = new Vector3(width, 0.35f, length * 1.02f);

            var mr = plank.GetComponent<MeshRenderer>();
            if (mr != null) mr.material = woodMat;
        }
    }
}
