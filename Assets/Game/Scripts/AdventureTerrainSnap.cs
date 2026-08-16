using UnityEngine;

public static class AdventureTerrainSnap
{
    public const float LakeEastMinX = 158f;
    public const float LakeEastMaxX = 188f;
    public const float LakeEastMinZ = 138f;
    public const float LakeEastMaxZ = 168f;

    static readonly string[] EnvRoots = { "Trees", "Bushes", "Rocks", "Waterplants" };

    public static Terrain FindLand()
    {
        foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
        {
            if (terrain.name == "LandTerrain")
                return terrain;
        }
        return null;
    }

    public static Terrain FindWater()
    {
        foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
        {
            if (terrain.name == "WaterTerrain")
                return terrain;
        }
        return null;
    }

    public static float GroundY(Terrain land, Vector3 worldPos, float offset = 0f)
    {
        if (land == null)
            return worldPos.y;

        Vector3 local = land.transform.InverseTransformPoint(worldPos);
        Vector3 size = land.terrainData.size;
        if (local.x < 0f || local.x > size.x || local.z < 0f || local.z > size.z)
            return worldPos.y;

        return land.SampleHeight(worldPos) + land.transform.position.y + offset;
    }

    public static void FixLakeEastShore()
    {
        Terrain land = FindLand();
        if (land == null)
            return;

        Terrain water = FindWater();
        CleanTerrainDetailsInRegion(land, water, LakeEastMinX, LakeEastMaxX, LakeEastMinZ, LakeEastMaxZ);
        SnapEnvironmentInRegion(land, LakeEastMinX, LakeEastMaxX, LakeEastMinZ, LakeEastMaxZ);
    }

    public const float NorthWestMinX = 100f;
    public const float NorthWestMaxX = 150f;
    public const float NorthWestMinZ = 190f;
    public const float NorthWestMaxZ = 240f;

    public static void FixNorthWestGrassland()
    {
        Terrain land = FindLand();
        if (land == null)
            return;

        Terrain water = FindWater();
        CleanTerrainDetailsInRegion(land, water, NorthWestMinX, NorthWestMaxX, NorthWestMinZ, NorthWestMaxZ, true);
        SnapEnvironmentInRegion(land, NorthWestMinX, NorthWestMaxX, NorthWestMinZ, NorthWestMaxZ);
    }

    static void SnapEnvironmentInRegion(Terrain land, float minX, float maxX, float minZ, float maxZ)
    {
        Terrain water = FindWater();
        foreach (string rootName in EnvRoots)
        {
            var root = GameObject.Find(rootName);
            if (root == null)
                continue;

            if (rootName == "Waterplants")
                SnapWaterplantsInRegion(root.transform, land, water, minX, maxX, minZ, maxZ);
            else
                SnapEnvironmentRoot(root.transform, land, minX, maxX, minZ, maxZ);
        }
    }

    static void SnapEnvironmentRoot(Transform root, Terrain land, float minX, float maxX, float minZ, float maxZ)
    {
        foreach (Transform child in root)
        {
            if (child.GetComponent<AdventureNpc>() != null
                || child.GetComponent<AdventurePlayerController>() != null)
                continue;

            Vector3 p = child.position;
            if (p.x < minX || p.x > maxX || p.z < minZ || p.z > maxZ)
                continue;

            p.y = GroundY(land, p, 0f);
            child.position = p;
        }
    }

    static void SnapWaterplantsInRegion(
        Transform root,
        Terrain land,
        Terrain water,
        float minX,
        float maxX,
        float minZ,
        float maxZ)
    {
        foreach (Transform child in root)
        {
            Vector3 p = child.position;
            if (p.x < minX || p.x > maxX || p.z < minZ || p.z > maxZ)
                continue;

            float landY = GroundY(land, p, 0f);
            float waterY = water != null ? GroundY(water, p, 0f) : landY;
            if (landY <= waterY + 0.35f)
            {
                child.gameObject.SetActive(false);
                continue;
            }

            p.y = landY;
            child.position = p;
        }
    }

    static void CleanTerrainDetailsInRegion(
        Terrain land,
        Terrain water,
        float minX,
        float maxX,
        float minZ,
        float maxZ,
        bool aggressive = false)
    {
        TerrainData td = land.terrainData;
        if (td == null || td.detailPrototypes == null || td.detailPrototypes.Length == 0)
            return;

        int width = td.detailResolution;
        int height = td.detailResolution;
        if (width <= 0 || height <= 0)
            return;

        Vector3 size = td.size;
        ResolveLakeBounds(out Vector2 lakeCenter, out float lakeRadius);
        int x0, x1, z0, z1;
        DetailIndexRange(land, td, minX, maxX, minZ, maxZ, width, height, out x0, out x1, out z0, out z1);

        for (int layer = 0; layer < td.detailPrototypes.Length; layer++)
        {
            int[,] map = td.GetDetailLayer(0, 0, width, height, layer);
            bool changed = false;
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (map[z, x] == 0)
                        continue;

                    float nx = x / (width - 1f);
                    float nz = z / (height - 1f);
                    float lx = nx * size.x;
                    float lz = nz * size.z;
                    float h = td.GetInterpolatedHeight(nx, nz);
                    Vector3 world = land.transform.TransformPoint(new Vector3(lx, h, lz));
                    if (world.x < minX || world.x > maxX || world.z < minZ || world.z > maxZ)
                        continue;

                    if (aggressive)
                    {
                        map[z, x] = 0;
                        changed = true;
                        continue;
                    }

                    if (!ShouldRemoveGrass(world, lx, lz, nx, nz, size, lakeCenter, lakeRadius, land, water, td, aggressive))
                        continue;

                    map[z, x] = 0;
                    changed = true;
                }
            }

            if (changed)
                td.SetDetailLayer(0, 0, layer, map);
        }
    }

    static void ResolveLakeBounds(out Vector2 lakeCenter, out float lakeRadius)
    {
        lakeCenter = new Vector2(133f, 169f);
        lakeRadius = 50f;

        var lake = GameObject.Find("Lake");
        if (lake == null)
            return;

        Renderer[] rends = lake.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0)
            return;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);
        lakeCenter = new Vector2(b.center.x, b.center.z);
        lakeRadius = Mathf.Max(b.extents.x, b.extents.z) + 14f;
    }

    static void DetailIndexRange(
        Terrain land,
        TerrainData td,
        float minX,
        float maxX,
        float minZ,
        float maxZ,
        int width,
        int height,
        out int x0,
        out int x1,
        out int z0,
        out int z1)
    {
        Vector3 size = td.size;
        Vector3 minLocal = land.transform.InverseTransformPoint(new Vector3(minX, 0f, minZ));
        Vector3 maxLocal = land.transform.InverseTransformPoint(new Vector3(maxX, 0f, maxZ));
        float lx0 = Mathf.Clamp(Mathf.Min(minLocal.x, maxLocal.x), 0f, size.x);
        float lx1 = Mathf.Clamp(Mathf.Max(minLocal.x, maxLocal.x), 0f, size.x);
        float lz0 = Mathf.Clamp(Mathf.Min(minLocal.z, maxLocal.z), 0f, size.z);
        float lz1 = Mathf.Clamp(Mathf.Max(minLocal.z, maxLocal.z), 0f, size.z);

        x0 = Mathf.Clamp(Mathf.FloorToInt(lx0 / size.x * (width - 1)), 0, width - 1);
        x1 = Mathf.Clamp(Mathf.CeilToInt(lx1 / size.x * (width - 1)), 0, width - 1);
        z0 = Mathf.Clamp(Mathf.FloorToInt(lz0 / size.z * (height - 1)), 0, height - 1);
        z1 = Mathf.Clamp(Mathf.CeilToInt(lz1 / size.z * (height - 1)), 0, height - 1);
    }

    static bool ShouldRemoveGrass(
        Vector3 world,
        float localX,
        float localZ,
        float normX,
        float normZ,
        Vector3 size,
        Vector2 lakeCenter,
        float lakeRadius,
        Terrain land,
        Terrain water,
        TerrainData td,
        bool aggressive = false)
    {
        float landY = GroundY(land, world, 0f);
        float waterY = water != null ? GroundY(water, world, 0f) : landY - 20f;
        float aboveWater = landY - waterY;
        float steepness = td.GetSteepness(normX, normZ);
        float distLake = Vector2.Distance(new Vector2(world.x, world.z), lakeCenter);

        if (aggressive && steepness > 14f)
            return true;

        if (distLake < lakeRadius + 6f && aboveWater < 5f)
            return true;

        if (landY < waterY + 1.5f)
            return true;

        if (steepness > 32f && aboveWater > 1.5f)
            return true;

        if (aboveWater < 3f && steepness > 22f)
            return true;

        return false;
    }
}
