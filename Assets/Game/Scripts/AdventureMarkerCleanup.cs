using UnityEngine;

public static class AdventureMarkerCleanup
{
    public static void RemoveAllQuestMarkers()
    {
        DestroyIfExists("Marker_犬");
        DestroyIfExists("Marker_猫");
    }

    public static void RemovePetBeacons()
    {
        DestroyIfExists("CatBeacon");
        DestroyIfExists("DogBeacon");
        DestroyIfExists("CatBeacon_old");
        DestroyIfExists("DogBeacon_old");
    }

    public static void RemoveFloatingShoreBands()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t.name != "SandBand")
                continue;
            Object.Destroy(t.gameObject);
        }
    }

    public static void RemoveFloatingWaterSurfaces()
    {
        // 1. WaterTerrain (水面テレイン) の完全非非描画・消去
        foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include))
        {
            if (terrain.name.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                terrain.drawHeightmap = false;
                terrain.drawTreesAndFoliage = false;
                terrain.enabled = false;
                var col = terrain.GetComponent<TerrainCollider>();
                if (col != null)
                    col.enabled = false;
            }
        }

        // 2. 「Lake」以外の頭上・広域に漂う不要な水面メッシュ・Plane・Waterオブジェクトの削除
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t == null)
                continue;

            string name = t.name;
            if (name == "Lake" || name.Contains("Lake"))
                continue; // 中央の池(Lake)は保持

            // 水面メッシュや WaterTerrain, WaterPlane, WaterSurface などの残骸を消去
            if (name.Equals("WaterTerrain", System.StringComparison.OrdinalIgnoreCase) ||
                name.Equals("WaterPlane", System.StringComparison.OrdinalIgnoreCase) ||
                name.Equals("WaterSurface", System.StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Water_Tile", System.StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("WaterTile", System.StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Sea", System.StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Ocean", System.StringComparison.OrdinalIgnoreCase))
            {
                Object.Destroy(t.gameObject);
            }
        }
    }

    static void DestroyIfExists(string name)
    {
        var go = GameObject.Find(name);
        if (go != null)
            Object.Destroy(go);
    }
}
