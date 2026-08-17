using UnityEngine;
using System.Linq;

public class AdventureIslandBoundary : MonoBehaviour
{
    public static AdventureIslandBoundary Instance { get; private set; }

    public float walkMinX = 58f;
    public float walkMaxX = 242f;
    public float walkMinZ = 58f;
    public float walkMaxZ = 242f;
    public float waterLevel = 18f;
    public Vector2 lakeCenter = new Vector2(133f, 169f);
    public float lakeRadius = 36f;
    public float rockSpacing = 14f;

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
        _land = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude).FirstOrDefault(t => t.name == "LandTerrain");
    }

    void Start()
    {
        BuildShore();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool IsWalkable(Vector3 pos)
    {
        if (pos.x < walkMinX || pos.x > walkMaxX || pos.z < walkMinZ || pos.z > walkMaxZ)
            return false;

        return true;
    }

    public Vector3 ClampWalkable(Vector3 pos)
    {
        pos.x = Mathf.Clamp(pos.x, walkMinX, walkMaxX);
        pos.z = Mathf.Clamp(pos.z, walkMinZ, walkMaxZ);

        Vector2 flat = new Vector2(pos.x, pos.z);
        Vector2 delta = flat - lakeCenter;
        float dist = delta.magnitude;
        if (dist < lakeRadius && dist > 0.001f)
        {
            Vector2 edge = lakeCenter + delta / dist * lakeRadius;
            pos.x = edge.x;
            pos.z = edge.y;
        }

        return pos;
    }

    public Vector3 ClipMotion(Vector3 pos, Vector3 motion)
    {
        if (motion.sqrMagnitude < 0.000001f)
            return motion;

        float nextX = pos.x + motion.x;
        if (!IsWalkable(new Vector3(nextX, pos.y, pos.z)))
            motion.x = ClampWalkable(new Vector3(nextX, pos.y, pos.z)).x - pos.x;

        float nextZ = pos.z + motion.z;
        if (!IsWalkable(new Vector3(pos.x + motion.x, pos.y, nextZ)))
            motion.z = ClampWalkable(new Vector3(pos.x + motion.x, pos.y, nextZ)).z - pos.z;

        Vector3 dest = pos + new Vector3(motion.x, 0f, motion.z);
        if (!IsWalkable(dest))
        {
            motion.x = 0f;
            motion.z = 0f;
        }

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
    }
}
