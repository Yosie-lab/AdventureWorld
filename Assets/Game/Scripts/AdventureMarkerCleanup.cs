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
        // 1. WaterTerrain (水面テレイン) の完全非描画・非アクティブ・消去
        foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include))
        {
            if (terrain == null) continue;
            if (terrain.name.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                terrain.name.IndexOf("Sea", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                terrain.name.IndexOf("Ocean", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                terrain.drawHeightmap = false;
                terrain.drawTreesAndFoliage = false;
                terrain.enabled = false;
                var col = terrain.GetComponent<TerrainCollider>();
                if (col != null)
                    col.enabled = false;
                Object.Destroy(terrain.gameObject);
            }
        }

        // 2. 「Lake」以外の頭上・広域に漂う不要な水面メッシュ・Plane・Waterオブジェクトの徹底削除
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t == null || t.gameObject == null)
                continue;

            string name = t.name;

            // 中央および北西の池(Lake)は保護する
            bool isLake = name.Equals("Lake", System.StringComparison.OrdinalIgnoreCase) ||
                          name.IndexOf("Lake", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (isLake)
            {
                continue;
            }

            // 「Water」「Sea」「Ocean」「Pond」など水面に関連するオブジェクトの判定
            bool isWaterName = name.Equals("Water", System.StringComparison.OrdinalIgnoreCase) ||
                               name.StartsWith("Water", System.StringComparison.OrdinalIgnoreCase) ||
                               name.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                               name.Equals("Sea", System.StringComparison.OrdinalIgnoreCase) ||
                               name.StartsWith("Sea", System.StringComparison.OrdinalIgnoreCase) ||
                               name.IndexOf("Sea", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                               name.Equals("Ocean", System.StringComparison.OrdinalIgnoreCase) ||
                               name.StartsWith("Ocean", System.StringComparison.OrdinalIgnoreCase) ||
                               name.IndexOf("Ocean", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                               name.IndexOf("Pond", System.StringComparison.OrdinalIgnoreCase) >= 0;

            // マテリアル名に Water / Sea / Ocean が含まれる水面メッシュの判定
            bool isWaterMaterial = false;
            var mr = t.GetComponent<Renderer>();
            if (mr != null && mr.sharedMaterials != null)
            {
                foreach (var mat in mr.sharedMaterials)
                {
                    if (mat != null &&
                        (mat.name.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         mat.name.IndexOf("Sea", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         mat.name.IndexOf("Ocean", System.StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        isWaterMaterial = true;
                        break;
                    }
                }
            }

            if (isWaterName || isWaterMaterial)
            {
                Object.Destroy(t.gameObject);
            }
        }
    }

    public static void RemoveFloatingRocks()
    {
        Terrain land = AdventureQuestLocations.FindLand();

        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t == null || t.gameObject == null)
                continue;

            if (t.GetComponent<AdventureNpc>() != null ||
                t.GetComponent<AdventurePlayerController>() != null ||
                t.GetComponent<AdventureMagicBox>() != null)
                continue;

            string nameLower = t.name.ToLower();
            bool isRock = nameLower.Contains("rock") || nameLower.Contains("stone") ||
                          nameLower.Contains("boulder") || nameLower.Contains("debris") ||
                          nameLower.Contains("cliff");

            if (isRock)
            {
                Vector3 pos = t.position;
                float groundY = land != null ? (land.SampleHeight(pos) + land.transform.position.y) : 2.5f;

                bool nearStartSquare = (pos.x >= 130f && pos.x <= 190f && pos.z >= 130f && pos.z <= 190f);
                if ((nearStartSquare && pos.y > groundY + 0.4f) || (pos.y > groundY + 3.0f))
                {
                    Object.Destroy(t.gameObject);
                }
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
