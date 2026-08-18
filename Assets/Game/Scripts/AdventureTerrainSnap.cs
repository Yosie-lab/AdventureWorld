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

        TerrainData td = land.terrainData;
        if (td != null)
        {
            // 北西エリア(X105-145, Z185-225, X121 Z195周辺)の崩れた池のくぼみを埋め立て平坦化
            FlattenNorthWestPond(land, td);
        }

        Terrain water = FindWater();
        CleanTerrainDetailsInRegion(land, water, NorthWestMinX, NorthWestMaxX, NorthWestMinZ, NorthWestMaxZ, true);
        SnapEnvironmentInRegion(land, NorthWestMinX, NorthWestMaxX, NorthWestMinZ, NorthWestMaxZ);
    }

    public const float NorthEastMinX = 175f;
    public const float NorthEastMaxX = 242f;
    public const float NorthEastMinZ = 175f;
    public const float NorthEastMaxZ = 242f;

    public static void FixNorthEastGrassland()
    {
        Terrain land = FindLand();
        if (land == null)
            return;

        TerrainData td = land.terrainData;
        if (td != null)
        {
            // 北東・北端エリア(X175-242, Z175-242, X219 Z242周辺)の段差・崖を平坦化
            FlattenNorthEastArea(land, td);
        }

        Terrain water = FindWater();
        CleanTerrainDetailsInRegion(land, water, NorthEastMinX, NorthEastMaxX, NorthEastMinZ, NorthEastMaxZ, true);
        SnapEnvironmentInRegion(land, NorthEastMinX, NorthEastMaxX, NorthEastMinZ, NorthEastMaxZ);
    }

    static void FlattenNorthEastArea(Terrain land, TerrainData td)
    {
        float minX = NorthEastMinX;
        float maxX = NorthEastMaxX;
        float minZ = NorthEastMinZ;
        float maxZ = NorthEastMaxZ;

        Vector3 terrainPos = land.transform.position;
        Vector3 terrainSize = td.size;
        int res = td.heightmapResolution;
        float invSizeX = 1f / terrainSize.x;
        float invSizeZ = 1f / terrainSize.z;
        float invSizeY = 1f / terrainSize.y;

        int ix0 = Mathf.Clamp(Mathf.FloorToInt((minX - terrainPos.x) * invSizeX * (res - 1)), 0, res - 1);
        int ix1 = Mathf.Clamp(Mathf.CeilToInt ((maxX - terrainPos.x) * invSizeX * (res - 1)), 0, res - 1);
        int iz0 = Mathf.Clamp(Mathf.FloorToInt((minZ - terrainPos.z) * invSizeZ * (res - 1)), 0, res - 1);
        int iz1 = Mathf.Clamp(Mathf.CeilToInt ((maxZ - terrainPos.z) * invSizeZ * (res - 1)), 0, res - 1);
        int patchW = ix1 - ix0 + 1;
        int patchH = iz1 - iz0 + 1;
        if (patchW <= 0 || patchH <= 0)
            return;

        float[,] heights = td.GetHeights(ix0, iz0, patchW, patchH);

        // 自然な北東草地標高（水面 + 2.5m ≒ 20.5m 〜 22.0m）
        float waterY = 18.0f;
        Terrain water = FindWater();
        if (water != null)
            waterY = water.SampleHeight(new Vector3(210f, 0f, 210f)) + water.transform.position.y;
        float targetLocalH = (waterY + 3.0f) - terrainPos.y;

        bool changed = false;
        const float featherMargin = 6.0f;

        for (int pz = 0; pz < patchH; pz++)
        {
            for (int px = 0; px < patchW; px++)
            {
                int gx = ix0 + px;
                int gz = iz0 + pz;
                float wx = terrainPos.x + (float)gx / (res - 1) * terrainSize.x;
                float wz = terrainPos.z + (float)gz / (res - 1) * terrainSize.z;

                if (wx < minX - featherMargin || wx > maxX + featherMargin ||
                    wz < minZ - featherMargin || wz > maxZ + featherMargin)
                    continue;

                float edgeDist = Mathf.Min(
                    wx - (minX - featherMargin), (maxX + featherMargin) - wx,
                    wz - (minZ - featherMargin), (maxZ + featherMargin) - wz);
                float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edgeDist / featherMargin));

                float currentH = heights[pz, px] * terrainSize.y;

                // 標高差がある急坂・段差を平坦化して南へスムーズに移動できるようにする
                float targetH = Mathf.Lerp(currentH, targetLocalH, blend);
                if (Mathf.Abs(currentH - targetH) > 0.02f)
                {
                    heights[pz, px] = targetH * invSizeY;
                    changed = true;
                }
            }
        }

        if (changed)
            td.SetHeights(ix0, iz0, heights);
    }

    static void FlattenNorthWestPond(Terrain land, TerrainData td)
    {
        // 範囲をX95-150, Z175-235に拡大（X120 Z194を完全にカバー）
        float minX = 95f;
        float maxX = 150f;
        float minZ = 175f;
        float maxZ = 235f;

        Vector3 terrainPos = land.transform.position;
        Vector3 terrainSize = td.size;
        int res = td.heightmapResolution;
        float invSizeX = 1f / terrainSize.x;
        float invSizeZ = 1f / terrainSize.z;
        float invSizeY = 1f / terrainSize.y;

        int ix0 = Mathf.Clamp(Mathf.FloorToInt((minX - terrainPos.x) * invSizeX * (res - 1)), 0, res - 1);
        int ix1 = Mathf.Clamp(Mathf.CeilToInt ((maxX - terrainPos.x) * invSizeX * (res - 1)), 0, res - 1);
        int iz0 = Mathf.Clamp(Mathf.FloorToInt((minZ - terrainPos.z) * invSizeZ * (res - 1)), 0, res - 1);
        int iz1 = Mathf.Clamp(Mathf.CeilToInt ((maxZ - terrainPos.z) * invSizeZ * (res - 1)), 0, res - 1);
        int patchW = ix1 - ix0 + 1;
        int patchH = iz1 - iz0 + 1;
        if (patchW <= 0 || patchH <= 0)
            return;

        float[,] heights = td.GetHeights(ix0, iz0, patchW, patchH);

        // 周囲の自然な草地標高（水面 + 3.0m ≒ 21.0m のしっかりした地上高）
        float waterY = 18.0f;
        Terrain water = FindWater();
        if (water != null)
            waterY = water.SampleHeight(new Vector3(120f, 0f, 195f)) + water.transform.position.y;
        float targetLocalH = (waterY + 3.0f) - terrainPos.y;

        bool changed = false;
        const float featherMargin = 10.0f;

        for (int pz = 0; pz < patchH; pz++)
        {
            for (int px = 0; px < patchW; px++)
            {
                int gx = ix0 + px;
                int gz = iz0 + pz;
                float wx = terrainPos.x + (float)gx / (res - 1) * terrainSize.x;
                float wz = terrainPos.z + (float)gz / (res - 1) * terrainSize.z;

                if (wx < minX - featherMargin || wx > maxX + featherMargin ||
                    wz < minZ - featherMargin || wz > maxZ + featherMargin)
                    continue;

                float edgeDist = Mathf.Min(
                    wx - (minX - featherMargin), (maxX + featherMargin) - wx,
                    wz - (minZ - featherMargin), (maxZ + featherMargin) - wz);
                float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edgeDist / featherMargin));

                float currentH = heights[pz, px] * terrainSize.y;

                // 池の残滓・くぼみを完全に埋め立てて高架平坦化する
                float targetH = Mathf.Lerp(currentH, Mathf.Max(currentH, targetLocalH), blend);
                if (Mathf.Abs(currentH - targetH) > 0.005f)
                {
                    heights[pz, px] = targetH * invSizeY;
                    changed = true;
                }
            }
        }

        if (changed)
            td.SetHeights(ix0, iz0, heights);
    }

    // ── スタート地点の全方向通路開通 ──────────────────
    //
    // 問題: スタート地点(165, 165)の狭い範囲だけを掘り下げると四方が高さ20m以上の岸壁に囲まれる。
    // 解決:
    //   1) スタート地点を高台自然標高(Y≈34m)で広々と平坦化する。
    //   2) 四方（北東:猫, 北西:犬, 南:看板/海岸, 東:東平野）へ向かう幅16mのなだらかな開通通路を生成する。

    static readonly Vector2 CenterStart = new Vector2(165f, 165f); // スタート広場中心
    const float CenterRadius = 18f; // スタート広場半径
    const float CenterTargetHeightWorld = 34.0f; // スタート広場の標高（ワールドY）

    // 4方向の開通ルート（始点→終点）
    static readonly Vector2[] RoadStarts = {
        CenterStart, // 南へ (案内看板)
        CenterStart, // 北東へ (猫エリア 213,213)
        CenterStart, // 北西へ (犬エリア 122,205)
        CenterStart  // 東へ (東平野 215,165)
    };

    static readonly Vector2[] RoadEnds = {
        new Vector2(165f, 118f), // 南 (案内看板)
        new Vector2(213f, 213f), // 北東 (猫)
        new Vector2(122f, 205f), // 北西 (犬)
        new Vector2(215f, 165f)  // 東 (東平野)
    };

    const float RoadHalfWidth = 8.0f; // 道路幅 16メートル

    static bool _rampCarved;

    public static void CarveStartPlateauRamp()
    {
        if (_rampCarved)
            return;

        Terrain land = FindLand();
        if (land == null)
            return;

        TerrainData td = land.terrainData;
        if (td == null)
            return;

        _rampCarved = true;

        Vector3 terrainPos = land.transform.position;
        Vector3 terrainSize = td.size;
        int res = td.heightmapResolution;
        float invSizeX = 1f / terrainSize.x;
        float invSizeZ = 1f / terrainSize.z;
        float invSizeY = 1f / terrainSize.y;

        // ── 1. スタート広場中央の平坦化 ──
        FlattenCenterPlateau(land, td, terrainPos, terrainSize, res, invSizeX, invSizeZ, invSizeY);

        // ── 2. 四方へのなだらかな開通道路の生成 ──
        CarveCorridorRoads(land, td, terrainPos, terrainSize, res, invSizeX, invSizeZ, invSizeY);

        // 広場＆全開通道路領域のデブリス除去＆オブジェクト接地
        CleanTerrainDetailsInRegion(land, FindWater(), 90f, 235f, 90f, 235f, true);
        SnapEnvironmentInRegion(land, 90f, 235f, 90f, 235f);

        Physics.SyncTransforms();
    }

    static void FlattenCenterPlateau(
        Terrain land, TerrainData td,
        Vector3 terrainPos, Vector3 terrainSize, int res,
        float invSizeX, float invSizeZ, float invSizeY)
    {
        float minX = CenterStart.x - CenterRadius - 10f;
        float maxX = CenterStart.x + CenterRadius + 10f;
        float minZ = CenterStart.y - CenterRadius - 10f;
        float maxZ = CenterStart.y + CenterRadius + 10f;

        int ix0 = Mathf.Clamp(Mathf.FloorToInt((minX - terrainPos.x) * invSizeX * (res - 1)), 0, res - 1);
        int ix1 = Mathf.Clamp(Mathf.CeilToInt ((maxX - terrainPos.x) * invSizeX * (res - 1)), 0, res - 1);
        int iz0 = Mathf.Clamp(Mathf.FloorToInt((minZ - terrainPos.z) * invSizeZ * (res - 1)), 0, res - 1);
        int iz1 = Mathf.Clamp(Mathf.CeilToInt ((maxZ - terrainPos.z) * invSizeZ * (res - 1)), 0, res - 1);
        int patchW = ix1 - ix0 + 1;
        int patchH = iz1 - iz0 + 1;
        if (patchW <= 0 || patchH <= 0)
            return;

        float[,] heights = td.GetHeights(ix0, iz0, patchW, patchH);
        float targetLocalH = CenterTargetHeightWorld - terrainPos.y;
        bool changed = false;

        for (int pz = 0; pz < patchH; pz++)
        {
            for (int px = 0; px < patchW; px++)
            {
                int gx = ix0 + px;
                int gz = iz0 + pz;
                float wx = terrainPos.x + (float)gx / (res - 1) * terrainSize.x;
                float wz = terrainPos.z + (float)gz / (res - 1) * terrainSize.z;

                float dist = Vector2.Distance(new Vector2(wx, wz), CenterStart);
                if (dist > CenterRadius + 8f)
                    continue;

                float blend = 1f;
                if (dist > CenterRadius)
                    blend = 1f - Mathf.Clamp01((dist - CenterRadius) / 8f);
                blend = Mathf.SmoothStep(0f, 1f, blend);

                float currentH = heights[pz, px] * terrainSize.y;
                float targetH = Mathf.Lerp(currentH, targetLocalH, blend);
                if (Mathf.Abs(currentH - targetH) > 0.01f)
                {
                    heights[pz, px] = targetH * invSizeY;
                    changed = true;
                }
            }
        }

        if (changed)
            td.SetHeights(ix0, iz0, heights);
    }

    static void CarveCorridorRoads(
        Terrain land, TerrainData td,
        Vector3 terrainPos, Vector3 terrainSize, int res,
        float invSizeX, float invSizeZ, float invSizeY)
    {
        for (int i = 0; i < RoadStarts.Length; i++)
        {
            Vector2 pStart = RoadStarts[i];
            Vector2 pEnd = RoadEnds[i];
            Vector2 dir = (pEnd - pStart).normalized;
            float len = (pEnd - pStart).magnitude;
            Vector2 perp = new Vector2(-dir.y, dir.x);

            float startH = td.GetInterpolatedHeight(
                (pStart.x - terrainPos.x) * invSizeX,
                (pStart.y - terrainPos.z) * invSizeZ);
            float endH = td.GetInterpolatedHeight(
                (pEnd.x - terrainPos.x) * invSizeX,
                (pEnd.y - terrainPos.z) * invSizeZ);

            float margin = RoadHalfWidth + 6f;
            float minX = Mathf.Min(pStart.x, pEnd.x) - margin;
            float maxX = Mathf.Max(pStart.x, pEnd.x) + margin;
            float minZ = Mathf.Min(pStart.y, pEnd.y) - margin;
            float maxZ = Mathf.Max(pStart.y, pEnd.y) + margin;

            int ix0 = Mathf.Clamp(Mathf.FloorToInt((minX - terrainPos.x) * invSizeX * (res - 1)), 0, res - 1);
            int ix1 = Mathf.Clamp(Mathf.CeilToInt ((maxX - terrainPos.x) * invSizeX * (res - 1)), 0, res - 1);
            int iz0 = Mathf.Clamp(Mathf.FloorToInt((minZ - terrainPos.z) * invSizeZ * (res - 1)), 0, res - 1);
            int iz1 = Mathf.Clamp(Mathf.CeilToInt ((maxZ - terrainPos.z) * invSizeZ * (res - 1)), 0, res - 1);
            int patchW = ix1 - ix0 + 1;
            int patchH = iz1 - iz0 + 1;
            if (patchW <= 0 || patchH <= 0)
                continue;

            float[,] heights = td.GetHeights(ix0, iz0, patchW, patchH);
            bool changed = false;

            for (int pz = 0; pz < patchH; pz++)
            {
                for (int px = 0; px < patchW; px++)
                {
                    int gx = ix0 + px;
                    int gz = iz0 + pz;
                    float wx = terrainPos.x + (float)gx / (res - 1) * terrainSize.x;
                    float wz = terrainPos.z + (float)gz / (res - 1) * terrainSize.z;

                    Vector2 toP = new Vector2(wx, wz) - pStart;
                    float along = Vector2.Dot(toP, dir);
                    float across = Mathf.Abs(Vector2.Dot(toP, perp));

                    if (along < -4f || along > len + 4f || across > RoadHalfWidth + 4f)
                        continue;

                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(along / len));
                    float idealH = Mathf.Lerp(startH, endH, t);

                    float widthWeight = 1f;
                    if (across > RoadHalfWidth)
                        widthWeight = 1f - Mathf.Clamp01((across - RoadHalfWidth) / 4f);
                    widthWeight = Mathf.SmoothStep(0f, 1f, widthWeight);

                    float currentH = heights[pz, px] * terrainSize.y;
                    float targetH = Mathf.Lerp(currentH, idealH, widthWeight);
                    if (Mathf.Abs(currentH - targetH) > 0.01f)
                    {
                        heights[pz, px] = targetH * invSizeY;
                        changed = true;
                    }
                }
            }

            if (changed)
                td.SetHeights(ix0, iz0, heights);
        }
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
