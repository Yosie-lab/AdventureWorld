using UnityEngine;

public static class AdventureQuestLocations
{
    // ────────────────────────────────────────────────
    // 猫の候補座標（マップ全域・端から端まで・全20箇所）
    // ────────────────────────────────────────────────
    static readonly Vector2[] CatCandidates =
    {
        // 最北端・北東奥エリア
        new Vector2(165f, 260f),   // 最北端の森
        new Vector2(180f, 255f),   // 北端東寄り
        new Vector2(245f, 245f),   // 北東奥の高台
        new Vector2(258f, 230f),   // 北東端の草地
        // 北東〜東端エリア
        new Vector2(213f, 213f),   // 北東平原中央
        new Vector2(228f, 202f),   // 北東平原東
        new Vector2(268f, 185f),   // 東端海岸沿い
        new Vector2(260f, 160f),   // 東の開けた平野
        new Vector2(240f, 175f),   // 東部内陸花畑
        // 南東奥・最南端エリア
        new Vector2(250f,  85f),   // 南東奥の平原
        new Vector2(235f,  75f),   // 南東海岸寄り
        new Vector2(210f, 125f),   // 南東平原
        new Vector2(165f,  70f),   // 最南端海岸
        new Vector2(185f,  75f),   // 南端東寄り草地
        // 南西奥・西端エリア
        new Vector2( 75f,  85f),   // 南西奥の緑地
        new Vector2(115f, 115f),   // 南西平原
        new Vector2( 65f, 180f),   // 西端の開けた草地
        new Vector2( 60f, 150f),   // 西海岸沿い
        // 北西奥・北部中央エリア
        new Vector2( 80f, 255f),   // 北西奥の平野
        new Vector2(168f, 215f),   // 北部中央の道
    };

    // ────────────────────────────────────────────────
    // 犬の候補座標（マップ全域・端から端まで・全20箇所）
    // ────────────────────────────────────────────────
    static readonly Vector2[] DogCandidates =
    {
        // 最北端・北西奥エリア
        new Vector2(150f, 258f),   // 最北端西寄り
        new Vector2( 95f, 260f),   // 北西奥の台地
        new Vector2( 75f, 245f),   // 北西端の草地
        new Vector2(105f, 222f),   // 北西平原西寄り
        // 北西〜西端エリア
        new Vector2(122f, 205f),   // 北西平原中央
        new Vector2(132f, 228f),   // 北西平原北
        new Vector2( 65f, 170f),   // 西端海岸沿い
        new Vector2( 88f, 185f),   // 西部内陸平地
        new Vector2(105f, 150f),   // 西側平野南
        // 南西奥・最南端エリア
        new Vector2( 95f,  75f),   // 南西海岸寄り
        new Vector2( 85f, 105f),   // 南西奥の草地
        new Vector2(120f, 135f),   // 南西平原中央
        new Vector2(145f,  75f),   // 最南端西寄り
        new Vector2(175f,  80f),   // 南端中央草地
        // 南東奥・東端エリア
        new Vector2(245f,  90f),   // 南東奥の平野
        new Vector2(220f,  95f),   // 南東海岸寄り
        new Vector2(195f, 120f),   // 南東内陸
        new Vector2(265f, 175f),   // 東端の開けた平原
        // 北東奥・北部中央エリア
        new Vector2(250f, 235f),   // 北東奥の草原
        new Vector2(185f, 235f),   // 北部中央平地
    };

    // ────────────────────────────────────────────────
    // スズメの候補座標（全方位）
    // ────────────────────────────────────────────────
    static readonly Vector2[] SparrowCandidates =
    {
        new Vector2(185f, 165f),   // 広場東寄り
        new Vector2(170f, 195f),   // 北の入口
        new Vector2(145f, 175f),   // 広場西寄り
        new Vector2(235f, 190f),   // 東部平原
        new Vector2(155f, 240f),   // 北部奥
        new Vector2(195f, 100f),   // 南東草地
    };

    // ────────────────────────────────────────────────
    // マスクラットの候補座標（水辺・池岸）
    // ────────────────────────────────────────────────
    static readonly Vector2[] MuskratCandidates =
    {
        new Vector2(152f, 175f),   // 池の北東岸
        new Vector2(145f, 195f),   // 北西草地入口
        new Vector2(115f, 175f),   // 池の西岸
        new Vector2(135f, 142f),   // 池の南岸
        new Vector2(170f, 110f),   // 南の小川沿い
    };

    // ────────────────────────────────────────────────
    // プドゥの候補座標（森や草地の端）
    // ────────────────────────────────────────────────
    static readonly Vector2[] PuduCandidates =
    {
        new Vector2(185f, 190f),   // 北東草地の端
        new Vector2(148f, 200f),   // 北西草地の道
        new Vector2(215f, 160f),   // 東の森の端
        new Vector2(105f, 160f),   // 西の森の端
        new Vector2(210f, 105f),   // 南東の静かな林
    };

    // ────────────────────────────────────────────────
    // コロブスの候補座標（広場周辺・中距離）
    // ────────────────────────────────────────────────
    static readonly Vector2[] ColobusCandidates =
    {
        new Vector2(170f, 175f),   // 広場北寄り中央
        new Vector2(185f, 185f),   // 広場北東
        new Vector2(150f, 180f),   // 広場北西
        new Vector2(180f, 145f),   // 広場南東
        new Vector2(140f, 155f),   // 広場西
    };

    // ────────────────────────────────────────────────
    // ヤモリの候補座標（スタート広場・近距離）
    // ────────────────────────────────────────────────
    static readonly Vector2[] GeckoCandidates =
    {
        new Vector2(156f, 164f),   // 元の位置
        new Vector2(175f, 155f),   // 広場の南東
        new Vector2(145f, 160f),   // 広場の南西
        new Vector2(165f, 145f),   // 広場の南
        new Vector2(160f, 180f),   // 広場の北
    };

    // ────────────────────────────────────────────────
    // 現在選択されている座標
    // ────────────────────────────────────────────────
    public static float CatX     = 213f;
    public static float CatZ     = 213f;
    public static float DogX     = 122f;
    public static float DogZ     = 205f;
    public static float SparrowX = 185f;
    public static float SparrowZ = 165f;
    public static float MuskratX = 152f;
    public static float MuskratZ = 175f;
    public static float PuduX    = 185f;
    public static float PuduZ    = 190f;
    public static float ColobusX = 170f;
    public static float ColobusZ = 175f;
    public static float GeckoX   = 156f;
    public static float GeckoZ   = 164f;

    public static Vector3 CatPosition => new Vector3(CatX, 0f, CatZ);
    public static Vector3 DogPosition => new Vector3(DogX, 0f, DogZ);

    // スタート付近の固定看板座標
    public static readonly Vector3 HintStart = new Vector3(165f, 0f, 130f);
    public static readonly Vector3 HintMemo  = new Vector3(165f, 0f, 168f);

    // 猫・犬のトレイル看板はスタート広場中央（165, 150）と目的地の中間付近に自動追従
    public static Vector3 HintCatTrail =>
        new Vector3(
            Mathf.Lerp(165f, CatX, 0.40f),
            0f,
            Mathf.Lerp(150f, CatZ, 0.40f));

    public static Vector3 HintDogTrail =>
        new Vector3(
            Mathf.Lerp(165f, DogX, 0.40f),
            0f,
            Mathf.Lerp(150f, DogZ, 0.40f));

    public static string CatCoordLabel => "X" + Mathf.RoundToInt(CatX) + " Z" + Mathf.RoundToInt(CatZ);
    public static string DogCoordLabel => "X" + Mathf.RoundToInt(DogX) + " Z" + Mathf.RoundToInt(DogZ);

    // 座標から方角（最北端・東の海岸・南東の奥など）を動的・詳細に判定
    public static string GetDirectionLabel(float x, float z)
    {
        float dx = x - 165f;
        float dz = z - 150f;

        bool isFarNorth = z >= 240f;
        bool isFarSouth = z <= 90f;
        bool isFarEast  = x >= 245f;
        bool isFarWest  = x <= 85f;

        if (isFarNorth && isFarEast) return "北東の奥";
        if (isFarNorth && isFarWest) return "北西の奥";
        if (isFarSouth && isFarEast) return "南東の奥";
        if (isFarSouth && isFarWest) return "南西の奥";

        if (isFarNorth) return "最北端";
        if (isFarSouth) return "最南端の海岸";
        if (isFarEast)  return "東の海岸";
        if (isFarWest)  return "西の海岸";

        string ew = dx > 25f ? "東" : dx < -25f ? "西" : "";
        string ns = dz > 25f ? "北" : dz < -25f ? "南" : "";

        if (string.IsNullOrEmpty(ns) && string.IsNullOrEmpty(ew))
            return "中央付近";
        return ns + ew;
    }

    public static string CatDirectionLabel => GetDirectionLabel(CatX, CatZ);
    public static string DogDirectionLabel => GetDirectionLabel(DogX, DogZ);

    // ────────────────────────────────────────────────
    // ゲームセッションごとに1回だけランダム化する
    // ────────────────────────────────────────────────
    static bool _randomized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetRandomize() => _randomized = false;

    /// <summary>
    /// 猫・犬・その他の動物の出現座標をマップ全域からランダムに選択する。
    /// 猫と犬は十分な距離（50m以上）離れた全然違うエリアに配置される。
    /// </summary>
    public static void Randomize()
    {
        if (_randomized)
            return;
        _randomized = true;

        var land = FindLand();

        // 猫の座標を全20箇所からランダム選択
        var cat = CatCandidates[Random.Range(0, CatCandidates.Length)];
        CatX = cat.x; CatZ = cat.y;

        // 犬の座標を全20箇所からランダム選択（猫と50m以上離れた全然違う方角）
        Vector2 dog = DogCandidates[Random.Range(0, DogCandidates.Length)];
        for (int retry = 0; retry < 30; retry++)
        {
            dog = DogCandidates[Random.Range(0, DogCandidates.Length)];
            if (Vector2.Distance(new Vector2(CatX, CatZ), dog) >= 50f)
                break;
        }
        DogX = dog.x; DogZ = dog.y;

        // その他の動物NPC
        var sparrow = SparrowCandidates[Random.Range(0, SparrowCandidates.Length)];
        SparrowX = sparrow.x; SparrowZ = sparrow.y;

        var muskrat = MuskratCandidates[Random.Range(0, MuskratCandidates.Length)];
        MuskratX = muskrat.x; MuskratZ = muskrat.y;

        var pudu = PuduCandidates[Random.Range(0, PuduCandidates.Length)];
        PuduX = pudu.x; PuduZ = pudu.y;

        var colobus = ColobusCandidates[Random.Range(0, ColobusCandidates.Length)];
        ColobusX = colobus.x; ColobusZ = colobus.y;

        var gecko = GeckoCandidates[Random.Range(0, GeckoCandidates.Length)];
        GeckoX = gecko.x; GeckoZ = gecko.y;

        // 配置地点の地形を完全フラット化し、絶対に埋まらない安全スペースを生成
        ApplySpotFlattening();

        Debug.Log(
            $"[QuestLocations 全域配置] 猫→{CatDirectionLabel}({CatX:F0},{CatZ:F0})  犬→{DogDirectionLabel}({DogX:F0},{DogZ:F0})\n" +
            $"  スズメ→({SparrowX:F0},{SparrowZ:F0})  マスクラット→({MuskratX:F0},{MuskratZ:F0})\n" +
            $"  プドゥ→({PuduX:F0},{PuduZ:F0})  コロブス→({ColobusX:F0},{ColobusZ:F0})  ヤモリ→({GeckoX:F0},{GeckoZ:F0})");
    }

    /// <summary>
    /// 猫・犬の配置地点周囲を完全に平坦化して、どんな地形であっても埋まりを100%防止する
    /// </summary>
    public static void ApplySpotFlattening()
    {
        var land = FindLand();
        if (land == null)
            return;

        FlattenSpot(land, CatX, CatZ, 6.0f);
        FlattenSpot(land, DogX, DogZ, 6.0f);
        FlattenSpot(land, SparrowX, SparrowZ, 4.0f);
        FlattenSpot(land, MuskratX, MuskratZ, 4.0f);
        FlattenSpot(land, PuduX, PuduZ, 4.0f);
        FlattenSpot(land, ColobusX, ColobusZ, 4.0f);
        FlattenSpot(land, GeckoX, GeckoZ, 4.0f);
    }

    /// <summary>
    /// 指定された座標の周囲半径radius内を完全な平地に均し、見晴らしの良いスペースを作る。
    /// </summary>
    static void FlattenSpot(Terrain land, float worldX, float worldZ, float radius)
    {
        if (land == null || land.terrainData == null)
            return;

        TerrainData td = land.terrainData;
        Vector3 tPos = land.transform.position;
        Vector3 tSize = td.size;
        int res = td.heightmapResolution;

        float invSizeX = 1f / tSize.x;
        float invSizeZ = 1f / tSize.z;
        float invSizeY = 1f / tSize.y;

        int ix0 = Mathf.Clamp(Mathf.FloorToInt((worldX - radius - 2f - tPos.x) * invSizeX * (res - 1)), 0, res - 1);
        int ix1 = Mathf.Clamp(Mathf.CeilToInt ((worldX + radius + 2f - tPos.x) * invSizeX * (res - 1)), 0, res - 1);
        int iz0 = Mathf.Clamp(Mathf.FloorToInt((worldZ - radius - 2f - tPos.z) * invSizeZ * (res - 1)), 0, res - 1);
        int iz1 = Mathf.Clamp(Mathf.CeilToInt ((worldZ + radius + 2f - tPos.z) * invSizeZ * (res - 1)), 0, res - 1);

        int patchW = ix1 - ix0 + 1;
        int patchH = iz1 - iz0 + 1;
        if (patchW <= 0 || patchH <= 0)
            return;

        float targetWorldH = land.SampleHeight(new Vector3(worldX, 0f, worldZ)) + tPos.y;
        // nikoに合わせた歩行可能な平地標高（20.2m〜22.2m）に制限・均す
        targetWorldH = Mathf.Clamp(targetWorldH, 20.2f, 22.2f);
        float targetLocalH = targetWorldH - tPos.y;

        float[,] heights = td.GetHeights(ix0, iz0, patchW, patchH);
        bool changed = false;

        for (int pz = 0; pz < patchH; pz++)
        {
            for (int px = 0; px < patchW; px++)
            {
                int gx = ix0 + px;
                int gz = iz0 + pz;
                float wx = tPos.x + (float)gx / (res - 1) * tSize.x;
                float wz = tPos.z + (float)gz / (res - 1) * tSize.z;

                float dist = Vector2.Distance(new Vector2(wx, wz), new Vector2(worldX, worldZ));
                if (dist > radius + 2f)
                    continue;

                float blend = 1f;
                if (dist > radius)
                    blend = 1f - (dist - radius) / 2f;
                blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blend));

                float currentH = heights[pz, px] * tSize.y;
                float newH = Mathf.Lerp(currentH, targetLocalH, blend);

                if (Mathf.Abs(currentH - newH) > 0.01f)
                {
                    heights[pz, px] = newH * invSizeY;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            td.SetHeights(ix0, iz0, heights);
        }
    }

    // ────────────────────────────────────────────────
    // 接地・ユーティリティ
    // ────────────────────────────────────────────────

    public static float GroundY(Terrain land, float x, float z, float offset = 0.12f)
    {
        if (land == null)
            return 21.5f + offset;
        float h = land.SampleHeight(new Vector3(x, 0f, z)) + land.transform.position.y;
        // nikoの高さ帯（20.2m〜22.5m）に合わせて山や丘の高所を排除
        h = Mathf.Clamp(h, 20.2f, 22.5f);
        return h + offset;
    }

    public static float WalkableGroundY(Terrain land, float x, float z, float offset = 0.12f)
    {
        return GroundY(land, x, z, offset);
    }

    public static Vector3 FindSafeFlatPosition(Terrain land, float x, float z, float offset = 0.12f)
    {
        float y = GroundY(land, x, z, offset);
        return new Vector3(x, y, z);
    }

    public static bool TryGetLostPetCoords(string npcId, out float x, out float z)
    {
        switch (npcId)
        {
            case "dog":
                x = DogX;
                z = DogZ;
                return true;
            case "cat":
                x = CatX;
                z = CatZ;
                return true;
            default:
                x = z = 0f;
                return false;
        }
    }

    public static Terrain FindLand()
    {
        foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
        {
            if (terrain.name == "LandTerrain")
                return terrain;
        }
        return null;
    }

    public static void SnapLostPet(Transform target, string npcId)
    {
        if (target == null || !TryGetLostPetCoords(npcId, out float x, out float z))
            return;

        var land = FindLand();
        float y = GroundY(land, x, z, 0.12f);

        // 真上からのRaycastで岩や障害物の上面に確実に接地
        Ray ray = new Ray(new Vector3(x, y + 10f, z), Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 20f))
        {
            if (!hit.collider.isTrigger && hit.point.y > y - 0.5f)
            {
                y = hit.point.y + 0.08f;
            }
        }

        target.position = new Vector3(x, y, z);
    }
}
