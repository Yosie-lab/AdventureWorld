using UnityEngine;
using System.Linq;

public class AdventureIslandBoundary : MonoBehaviour
{
    public static AdventureIslandBoundary Instance { get; private set; }

    public float walkMinX = 5f;
    public float walkMaxX = 1019f;
    public float walkMinZ = 5f;
    public float walkMaxZ = 1019f;
    public float waterLevel = 5.5f; // Rust & Float の正規海面水位（5.5m）
    public Vector2 lakeCenter = Vector2.zero;
    public float lakeRadius = 0f;
    public float rockSpacing = 14f;
    public bool placeShoreRocks = false; // 広大な1000m島で不要な岩壁生成を抑止

    Terrain _land;
    bool _built;

    public static void Ensure()
    {
        if (Instance != null)
            return;
        var go = new GameObject("IslandBoundary");
        go.AddComponent<AdventureIslandBoundary>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        waterLevel = 5.5f; // Rust & Float 海面水位の絶対保証

        // アクティブTerrainを最優先で取得
        _land = Terrain.activeTerrain ?? Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude).FirstOrDefault();
        if (_land != null && _land.terrainData != null)
        {
            Vector3 size = _land.terrainData.size;
            Vector3 origin = _land.transform.position;
            walkMinX = origin.x + 5f;
            walkMaxX = origin.x + size.x - 5f;
            walkMinZ = origin.z + 5f;
            walkMaxZ = origin.z + size.z - 5f;
            lakeRadius = 0f; // 1000m Grand Sanctuary では旧来の池侵入禁止円を完全無効化
        }
    }

    void Start()
    {
        if (placeShoreRocks)
            BuildShore();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool IsWalkable(Vector3 pos)
    {
        // 極端なマップ外奈落（海のはるか沖合）以外は常に歩行可能とし、見えない壁による硬直を完全防止
        if (pos.x < -300f || pos.x > 1300f || pos.z < -300f || pos.z > 1300f)
            return false;

        return true;
    }

    public Vector3 ClampWalkable(Vector3 pos)
    {
        pos.x = Mathf.Clamp(pos.x, -300f, 1300f);
        pos.z = Mathf.Clamp(pos.z, -300f, 1300f);
        return pos;
    }

    public Vector3 ClipMotion(Vector3 pos, Vector3 motion)
    {
        // 移動量をゼロに削らず、そのままスムーズに通過させる
        return motion;
    }

    public float GroundY(Vector3 pos)
    {
        if (_land == null)
            return pos.y;
        return _land.SampleHeight(pos) + _land.transform.position.y;
    }

    void BuildShore()
    {
        if (_built)
            return;
        _built = true;

        var root = new GameObject("ShoreBoundary").transform;
        root.SetParent(transform, false);

        PlaceRocks(root, new Vector3(walkMinX, 0f, walkMinZ), new Vector3(walkMaxX, 0f, walkMinZ));
        PlaceRocks(root, new Vector3(walkMaxX, 0f, walkMinZ), new Vector3(walkMaxX, 0f, walkMaxZ));
        PlaceRocks(root, new Vector3(walkMaxX, 0f, walkMaxZ), new Vector3(walkMinX, 0f, walkMaxZ));
        PlaceRocks(root, new Vector3(walkMinX, 0f, walkMaxZ), new Vector3(walkMinX, 0f, walkMinZ));
    }

    void PlaceRocks(Transform root, Vector3 a, Vector3 b)
    {
        Vector3 delta = b - a;
        float len = delta.magnitude;
        if (len < 0.1f)
            return;
        Vector3 dir = delta / len;
        int count = Mathf.Max(1, Mathf.FloorToInt(len / rockSpacing));
        for (int i = 0; i <= count; i++)
        {
            float t = count == 0 ? 0.5f : i / (float)count;
            Vector3 p = a + dir * (len * t);
            p.y = GroundY(p) + 0.35f;
            CreateRock(root, p, i);
        }
    }

    void CreateRock(Transform root, Vector3 pos, int index)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "ShoreRock";
        go.transform.SetParent(root, false);
        go.transform.position = pos;
        float s = 1f + (index % 3) * 0.18f;
        go.transform.localScale = new Vector3(1.15f * s, 0.75f * s, 1.05f * s);
        go.transform.rotation = Quaternion.Euler(0f, (index * 53f) % 360f, 0f);
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.45f, 0.47f, 0.42f, 1f);

        var col = go.GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }
}
