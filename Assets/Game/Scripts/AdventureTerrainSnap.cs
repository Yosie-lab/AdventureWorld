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

    public const float NorthWestMinX = 58f;
    public const float NorthWestMaxX = 150f;
    public const float NorthWestMinZ = 120f;
    public const float NorthWestMaxZ = 240f;

    public static void FixNorthWestGrassland()
    {
        Terrain land = FindLand();
        if (land == null)
            return;

        TerrainData td = land.terrainData;
        if (td != null)
        {
            // 北西エリア(X58-145, Z120-225)の崩れたくぼみ・急傾斜を埋め立て平坦化
            FlattenNorthWestPond(land, td);
        }

        Terrain water = FindWater();
        CleanTerrainDetailsInRegion(land, water, NorthWestMinX, NorthWestMaxX, NorthWestMinZ, NorthWestMaxZ, true);
        SnapEnvironmentInRegion(land, NorthWestMinX, NorthWestMaxX, NorthWestMinZ, NorthWestMaxZ);
    }

    public const float NorthEastMinX = 58f;
    public const float NorthEastMaxX = 275f;
    public const float NorthEastMinZ = 175f;
    public const float NorthEastMaxZ = 275f;

    public static void FixNorthEastGrassland()
    {
        Terrain land = FindLand();
        if (land == null)
            return;

        TerrainData td = land.terrainData;
        if (td != null)
        {
            // 北部全域エリア(X95-275, Z175-275)の段差・崖・未平坦化隙間を平坦化
            FlattenNorthEastArea(land, td);
        }

        Terrain water = FindWater();
        CleanTerrainDetailsInRegion(land, water, NorthEastMinX, NorthEastMaxX, NorthEastMinZ, NorthEastMaxZ, true);
        SnapEnvironmentInRegion(land, NorthEastMinX, NorthEastMaxX, NorthEastMinZ, NorthEastMaxZ);
        RemoveUnintendedPuddles();
    }

    public static void RemoveUnintendedPuddles()
    {
        Terrain land = FindLand();
        if (land == null)
            return;

        TerrainData td = land.terrainData;
        if (td == null)
            return;

        Vector3 terrainPos = land.transform.position;
        Vector3 terrainSize = td.size;
        int res = td.heightmapResolution;
        float invSizeY = 1f / terrainSize.y;

        float[,] heights = td.GetHeights(0, 0, res, res);

        float waterY = 18.0f;
        Terrain water = FindWater();
        if (water != null)
            waterY = water.SampleHeight(new Vector3(165f, 0f, 165f)) + water.transform.position.y;

        float thresholdLocalH = (waterY + 1.0f) - terrainPos.y;
        float fillLocalH = (waterY + 2.5f) - terrainPos.y;

        Vector2 lakeCenter = new Vector2(133f, 169f);
        float lakeRadius = 45f;
        var lakeObj = GameObject.Find("Lake");
        if (lakeObj != null)
        {
            var rends = lakeObj.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++)
                    b.Encapsulate(rends[i].bounds);
                lakeCenter = new Vector2(b.center.x, b.center.z);
                lakeRadius = Mathf.Max(b.extents.x, b.extents.z) + 10f;
            }
        }

        bool changed = false;
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float wx = terrainPos.x + (float)x / (res - 1) * terrainSize.x;
                float wz = terrainPos.z + (float)z / (res - 1) * terrainSize.z;

                float distLake = Vector2.Distance(new Vector2(wx, wz), lakeCenter);
                if (distLake < lakeRadius)
                    continue;

                float currentH = heights[z, x] * terrainSize.y;
                if (currentH < thresholdLocalH)
                {
                    heights[z, x] = fillLocalH * invSizeY;
                    changed = true;
                }
            }
        }

        if (changed)
            td.SetHeights(0, 0, heights);

        SmoothEntireIslandCliffs();
    }

    public static void SmoothEntireIslandCliffs()
    {
        Terrain land = FindLand();
        if (land == null)
            return;

        TerrainData td = land.terrainData;
        if (td == null)
            return;

        Vector3 terrainPos = land.transform.position;
        Vector3 terrainSize = td.size;
        int res = td.heightmapResolution;
        float invSizeY = 1f / terrainSize.y;

        float[,] heights = td.GetHeights(0, 0, res, res);
        float[,] smoothed = (float[,])heights.Clone();

        bool changed = false;
        int passes = 2;

        Vector2 lakeCenter = new Vector2(133f, 169f);
        float lakeRadius = 40f;

        for (int p = 0; p < passes; p++)
        {
            for (int z = 1; z < res - 1; z++)
            {
                for (int x = 1; x < res - 1; x++)
                {
                    float wx = terrainPos.x + (float)x / (res - 1) * terrainSize.x;
                    float wz = terrainPos.z + (float)z / (res - 1) * terrainSize.z;

                    if (wx < 55f || wx > 278f || wz < 55f || wz > 278f)
                        continue;

                    if (Vector2.Distance(new Vector2(wx, wz), lakeCenter) < lakeRadius)
                        continue;

                    float centerH = heights[z, x] * terrainSize.y;

                    float sum = 0f;
                    int count = 0;
                    float maxDiff = 0f;

                    for (int dz = -1; dz <= 1; dz++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            float nh = heights[z + dz, x + dx] * terrainSize.y;
                            sum += nh;
                            count++;
                            float diff = Mathf.Abs(centerH - nh);
                            if (diff > maxDiff)
                                maxDiff = diff;
                        }
                    }

                    float avgH = sum / count;

                    if (maxDiff > 3.0f)
                    {
                        float newH = Mathf.Lerp(centerH, avgH, 0.65f);
                        smoothed[z, x] = newH * invSizeY;
                        changed = true;
                    }
                }
            }
            heights = (float[,])smoothed.Clone();
        }

        if (changed)
            td.SetHeights(0, 0, smoothed);
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

                // 標高差がある急坂・段差・水たまり凹地をしっかり底上げ平坦化する
                float targetH = Mathf.Lerp(currentH, Mathf.Max(currentH, targetLocalH), blend);
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
        // 範囲をX58-150, Z120-235に拡大（X73 Z137北側を完全にカバー）
        float minX = 58f;
        float maxX = 150f;
        float minZ = 120f;
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

                float distLeft = wx <= minX ? featherMargin : wx - (minX - featherMargin);
                float distRight = (maxX + featherMargin) - wx;
                float distBottom = wz <= minZ ? featherMargin : wz - (minZ - featherMargin);
                float distTop = (maxZ + featherMargin) - wz;

                float edgeDist = Mathf.Min(distLeft, distRight, distBottom, distTop);
                float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edgeDist / featherMargin));

                float currentH = heights[pz, px] * terrainSize.y;

                // 池の残滓・くぼみ・段差を完全に埋め立てて高架平坦化する
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

    public static void ApplyNaturalLandscape()
    {
        Terrain land = FindLand();
        if (land == null)
            return;

        TerrainData td = land.terrainData;
        if (td == null)
            return;

        Vector3 terrainPos = land.transform.position;
        Vector3 terrainSize = td.size;
        int res = td.heightmapResolution;
        float invSizeY = 1f / terrainSize.y;

        float[,] heights = td.GetHeights(0, 0, res, res);
        float[,] smoothed = (float[,])heights.Clone();

        bool changed = false;
        Vector2 lakeCenter = new Vector2(133f, 169f);
        float lakeRadius = 40f;
        Vector2 startCenter = new Vector2(165f, 165f);
        float startRadius = 18f;

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float wx = terrainPos.x + (float)x / (res - 1) * terrainSize.x;
                float wz = terrainPos.z + (float)z / (res - 1) * terrainSize.z;

                if (wx < 58f || wx > 275f || wz < 58f || wz > 275f)
                    continue;

                float distLake = Vector2.Distance(new Vector2(wx, wz), lakeCenter);
                if (distLake < lakeRadius)
                    continue;

                float distStart = Vector2.Distance(new Vector2(wx, wz), startCenter);
                if (distStart < startRadius)
                    continue;

                float currentH = heights[z, x] * terrainSize.y;
                float noise1 = (Mathf.PerlinNoise(wx * 0.032f, wz * 0.032f) - 0.5f) * 1.25f;
                float noise2 = (Mathf.PerlinNoise(wx * 0.085f, wz * 0.085f) - 0.5f) * 0.45f;
                float newH = currentH + noise1 + noise2;
                if (Mathf.Abs(currentH - newH) > 0.01f)
                {
                    smoothed[z, x] = newH * invSizeY;
                    changed = true;
                }
            }
        }

        float[,] finalHeights = (float[,])smoothed.Clone();
        for (int z = 2; z < res - 2; z++)
        {
            for (int x = 2; x < res - 2; x++)
            {
                float wx = terrainPos.x + (float)x / (res - 1) * terrainSize.x;
                float wz = terrainPos.z + (float)z / (res - 1) * terrainSize.z;

                if (wx < 58f || wx > 275f || wz < 58f || wz > 275f)
                    continue;

                if (Vector2.Distance(new Vector2(wx, wz), lakeCenter) < lakeRadius - 5f)
                    continue;

                float sum = 0f;
                int count = 0;
                for (int dz = -2; dz <= 2; dz++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        sum += smoothed[z + dz, x + dx] * terrainSize.y;
                        count++;
                    }
                }
                float avgH = sum / count;
                float centerH = smoothed[z, x] * terrainSize.y;
                if (Mathf.Abs(centerH - avgH) > 1.5f)
                {
                    finalHeights[z, x] = Mathf.Lerp(centerH, avgH, 0.45f) * invSizeY;
                    changed = true;
                }
            }
        }

        if (changed)
            td.SetHeights(0, 0, finalHeights);

        EnsureLandWaterSeparation();
        SnapEnvironmentInRegion(land, 58f, 275f, 58f, 275f);
        FillCliffAtX158Z194();
    }

    public static void EnsureLandWaterSeparation()
    {
        Terrain land = FindLand();
        if (land == null)
            return;

        TerrainData td = land.terrainData;
        if (td == null)
            return;

        Vector3 terrainPos = land.transform.position;
        Vector3 terrainSize = td.size;
        int res = td.heightmapResolution;
        float invSizeY = 1f / terrainSize.y;

        float[,] heights = td.GetHeights(0, 0, res, res);

        float waterY = 18.0f;
        Terrain water = FindWater();
        if (water != null)
            waterY = water.SampleHeight(new Vector3(165f, 0f, 165f)) + water.transform.position.y;

        float minSafeLandHeight = (waterY + 1.8f) - terrainPos.y; // 標高 Y >= 19.8m 以上に離隔

        Vector2 lakeCenter = new Vector2(133f, 169f);
        float lakeRadius = 42f;

        bool changed = false;

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float wx = terrainPos.x + (float)x / (res - 1) * terrainSize.x;
                float wz = terrainPos.z + (float)z / (res - 1) * terrainSize.z;

                // 可走エリア（58〜275）で湖エリア以外を完全隔離
                if (wx < 56f || wx > 276f || wz < 56f || wz > 276f)
                    continue;

                float distLake = Vector2.Distance(new Vector2(wx, wz), lakeCenter);
                if (distLake < lakeRadius)
                    continue;

                float currentH = heights[z, x] * terrainSize.y;
                if (currentH < minSafeLandHeight)
                {
                    heights[z, x] = minSafeLandHeight * invSizeY;
                    changed = true;
                }
            }
        }

        if (changed)
            td.SetHeights(0, 0, heights);
    }

    public static void FillCliffAtX158Z194()
    {
        Terrain land = FindLand();
        if (land == null)
            return;

        TerrainData td = land.terrainData;
        if (td == null)
            return;

        Vector3 terrainPos = land.transform.position;
        Vector3 terrainSize = td.size;
        int res = td.heightmapResolution;
        float invSizeX = 1f / terrainSize.x;
        float invSizeZ = 1f / terrainSize.z;
        float invSizeY = 1f / terrainSize.y;

        float centerWorldX = 158f;
        float centerWorldZ = 194f;
        float radius = 18f;

        float minX = centerWorldX - radius;
        float maxX = centerWorldX + radius;
        float minZ = centerWorldZ - radius;
        float maxZ = centerWorldZ + radius;

        int ix0 = Mathf.Clamp(Mathf.FloorToInt((minX - terrainPos.x) * invSizeX * (res - 1)), 0, res - 1);
        int ix1 = Mathf.Clamp(Mathf.CeilToInt ((maxX - terrainPos.x) * invSizeX * (res - 1)), 0, res - 1);
        int iz0 = Mathf.Clamp(Mathf.FloorToInt((minZ - terrainPos.z) * invSizeZ * (res - 1)), 0, res - 1);
        int iz1 = Mathf.Clamp(Mathf.CeilToInt ((maxZ - terrainPos.z) * invSizeZ * (res - 1)), 0, res - 1);
        int patchW = ix1 - ix0 + 1;
        int patchH = iz1 - iz0 + 1;
        if (patchW <= 0 || patchH <= 0)
            return;

        float[,] heights = td.GetHeights(ix0, iz0, patchW, patchH);

        float waterY = 18.0f;
        Terrain water = FindWater();
        if (water != null)
            waterY = water.SampleHeight(new Vector3(158f, 0f, 194f)) + water.transform.position.y;
        float minSafeH = (waterY + 6.5f) - terrainPos.y;

        bool changed = false;

        for (int pz = 0; pz < patchH; pz++)
        {
            for (int px = 0; px < patchW; px++)
            {
                int gx = ix0 + px;
                int gz = iz0 + pz;
                float wx = terrainPos.x + (float)gx / (res - 1) * terrainSize.x;
                float wz = terrainPos.z + (float)gz / (res - 1) * terrainSize.z;

                float dist = Vector2.Distance(new Vector2(wx, wz), new Vector2(centerWorldX, centerWorldZ));
                if (dist > radius)
                    continue;

                float weight = 1f - Mathf.Clamp01(dist / radius);
                weight = Mathf.SmoothStep(0f, 1f, weight);

                float currentH = heights[pz, px] * terrainSize.y;
                float targetH = Mathf.Lerp(currentH, Mathf.Max(currentH, minSafeH), weight);

                if (Mathf.Abs(currentH - targetH) > 0.005f)
                {
                    heights[pz, px] = targetH * invSizeY;
                    changed = true;
                }
            }
        }

        if (changed)
            td.SetHeights(ix0, iz0, heights);

        SnapEnvironmentInRegion(land, minX, maxX, minZ, maxZ);
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

    // 5方向の開通ルート（始点→終点）
    static readonly Vector2[] RoadStarts = {
        CenterStart, // 南へ (案内看板)
        CenterStart, // 北東へ (猫エリア 213,213)
        CenterStart, // 北西へ (犬エリア 122,205)
        CenterStart, // 東へ (東平野 215,165)
        new Vector2(177f, 165f) // 北へ (X177 Z174 なだらかなスロープ)
    };

    static readonly Vector2[] RoadEnds = {
        new Vector2(165f, 118f), // 南 (案内看板)
        new Vector2(213f, 213f), // 北東 (猫)
        new Vector2(122f, 205f), // 北西 (犬)
        new Vector2(215f, 165f), // 東 (東平野)
        new Vector2(177f, 210f)  // 北へ (X177 Z174 なだらかなスロープ)
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
