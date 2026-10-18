using UnityEngine;
using UnityEditor;

public static class AdventureGlidingCoordinateFinder
{
    [MenuItem("Adventure/Debug/Scan High Points")]
    public static void Scan()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("Active terrain not found");
            return;
        }

        Vector3 tPos = terrain.transform.position;
        var tData = terrain.terrainData;

        // 北側エリア (X: 450-550, Z: 650-850) の最高地点を探す
        float maxH = -1f;
        Vector3 maxPos = Vector3.zero;

        for (float x = 400; x <= 600; x += 10)
        {
            for (float z = 600; z <= 850; z += 10)
            {
                float h = terrain.SampleHeight(new Vector3(x, 0, z));
                if (h > maxH)
                {
                    maxH = h;
                    maxPos = new Vector3(x, h, z);
                }
            }
        }

        Debug.Log($"[GlidingScan] 北側最高峰: Pos={maxPos}, Height={maxH}");

        // 中央タワーの位置と高さ
        var tower = GameObject.Find("Tower") ?? GameObject.Find("SanctuaryTower") ?? GameObject.Find("Sanctuary_Tower");
        if (tower != null)
        {
            Debug.Log($"[GlidingScan] タワー検出: {tower.name}, Pos={tower.transform.position}");
        }

        // 北側の見晴らしのよい崖縁（Zが小さくなる方向＝南に向かって標高が急降下する崖のエッジ）を探す
        // X: 480〜540 のラインで崖エッジを検出
        for (float x = 480; x <= 540; x += 20)
        {
            for (float z = 800; z >= 600; z -= 10)
            {
                float hCurr = terrain.SampleHeight(new Vector3(x, 0, z));
                float hNext = terrain.SampleHeight(new Vector3(x, 0, z - 20));
                float drop = hCurr - hNext;
                if (hCurr > 50f && drop > 15f)
                {
                    Debug.Log($"[GlidingScan] 絶壁エッジ検出! X={x}, Z={z}, H={hCurr:F1}m, 20m先落差={drop:F1}m (南向き崖)");
                    break;
                }
            }
        }
    }
}
