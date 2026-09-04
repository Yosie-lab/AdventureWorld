using UnityEngine;

public static class AdventureQuestLocations
{
    // ────────────────────────────────────────────────
    // 猫の候補座標（山や丘を排除し、完全な平坦草地・見晴らし良好な場所のみ厳選）
    // ────────────────────────────────────────────────
    static readonly Vector2[] CatCandidates =
    {
        new Vector2(213f, 213f),   // 北東平原の中央草地（水面+3mで完全平坦化済み）
        new Vector2(198f, 228f),   // 北東平原の北寄り（開けた平地）
        new Vector2(228f, 202f),   // 北東平原の東寄り（木陰のない平地）
        new Vector2(205f, 195f),   // 北東平原の南寄り（見通しの良い平地）
        new Vector2(230f, 180f),   // 東側平野・花畑前の開けた平地
        new Vector2(185f, 155f),   // スタート広場東側の平坦草地
        new Vector2(210f, 140f),   // 南東エリアの開けた平坦草地
        new Vector2(192f, 128f),   // 南東エリアのなだらかな開けた草地
        new Vector2(168f, 205f),   // 北部連絡道路の平坦草地
        new Vector2(180f, 228f),   // 北部平原の見通しの良い平地
    };

    // ────────────────────────────────────────────────
    // 犬の候補座標（山や丘を排除し、完全な平坦草地・見晴らし良好な場所のみ厳選）
    // ────────────────────────────────────────────────
    static readonly Vector2[] DogCandidates =
    {
        new Vector2(122f, 205f),   // 北西平原の中央草地（水面+3mで完全平坦化済み）
        new Vector2(105f, 222f),   // 北西平原の北西寄り（完全平坦草地）
        new Vector2(132f, 228f),   // 北西平原の北寄り（広々とした平地）
        new Vector2( 98f, 200f),   // 北西平原の西寄り（開けた平坦地）
        new Vector2(115f, 180f),   // 北西平原の南寄り・池の北岸（平坦草地）
        new Vector2( 88f, 185f),   // 西側平野の開けた平坦地
        new Vector2(145f, 145f),   // スタート広場南西の平坦草地
        new Vector2(120f, 135f),   // 南西エリアの開けた平坦地
        new Vector2(105f, 150f),   // 西側平野の南寄り平地
        new Vector2(148f, 235f),   // 北部平原の北西寄り平地
    };

    // ────────────────────────────────────────────────
    // スズメの候補座標（開けた草地・平地）
    // ────────────────────────────────────────────────
    static readonly Vector2[] SparrowCandidates =
    {
        new Vector2(185f, 165f),   // スタート広場の東寄り平地
        new Vector2(170f, 195f),   // 北の草地入口
        new Vector2(145f, 175f),   // スタート西寄り平地
        new Vector2(195f, 150f),   // 広場南東の平坦地
        new Vector2(160f, 210f),   // 北の平坦な道沿い
    };

    // ────────────────────────────────────────────────
    // マスクラットの候補座標（池・水辺の平坦な岸）
    // ────────────────────────────────────────────────
    static readonly Vector2[] MuskratCandidates =
    {
        new Vector2(152f, 175f),   // 池の北東岸の平地
        new Vector2(145f, 195f),   // 北西草地入口の平地
        new Vector2(175f, 195f),   // 北の平坦草地
        new Vector2(115f, 175f),   // 池の西岸の平坦地
        new Vector2(135f, 142f),   // 池の南岸の平坦地
    };

    // ────────────────────────────────────────────────
    // プドゥの候補座標（開けた草地の端）
    // ────────────────────────────────────────────────
    static readonly Vector2[] PuduCandidates =
    {
        new Vector2(185f, 190f),   // 北東草地の平坦な端
        new Vector2(148f, 200f),   // 北西草地への平坦な道
        new Vector2(165f, 215f),   // 北の平坦地
        new Vector2(195f, 135f),   // 南東の平坦草地
        new Vector2(110f, 140f),   // 南西の平坦草地
    };

    // ────────────────────────────────────────────────
    // コロブスの候補座標（広場周辺の平坦草地）
    // ────────────────────────────────────────────────
    static readonly Vector2[] ColobusCandidates =
    {
        new Vector2(170f, 175f),   // 広場の北寄り中央
        new Vector2(185f, 185f),   // 広場の北東平地
        new Vector2(150f, 180f),   // 広場の北西平地
        new Vector2(180f, 145f),   // 広場の南東平地
        new Vector2(140f, 155f),   // 広場の西側平地
    };

    // ────────────────────────────────────────────────
    // ヤモリの候補座標（スタート広場内の平坦地）
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
            Mathf.Lerp(165f, CatX, 0.42f),
            0f,
            Mathf.Lerp(150f, CatZ, 0.42f));

    public static Vector3 HintDogTrail =>
        new Vector3(
            Mathf.Lerp(165f, DogX, 0.42f),
            0f,
            Mathf.Lerp(150f, DogZ, 0.42f));

    public static string CatCoordLabel => "X" + Mathf.RoundToInt(CatX) + " Z" + Mathf.RoundToInt(CatZ);
    public static string DogCoordLabel => "X" + Mathf.RoundToInt(DogX) + " Z" + Mathf.RoundToInt(DogZ);

    // 座標から方角（北・南東など）を動的に判定
    public static string GetDirectionLabel(float x, float z)
    {
        float dx = x - 165f;
        float dz = z - 150f;

        string ew = dx > 20f ? "東" : dx < -20f ? "西" : "";
        string ns = dz > 20f ? "北" : dz < -20f ? "南" : "";

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
    /// 猫・犬・その他の動物の出現座標をランダムに選択する。
    /// 猫と犬は十分な距離（40m以上）離れた平坦な場所に配置される。
    /// </summary>
    public static void Randomize()
    {
        if (_randomized)
            return;
        _randomized = true;

        var land = FindLand();

        // 猫の座標をランダム選択し、平地チェックを適用
        var cat = CatCandidates[Random.Range(0, CatCandidates.Length)];
        Vector3 safeCat = FindSafeFlatPosition(land, cat.x, cat.y);
        CatX = safeCat.x; CatZ = safeCat.z;

        // 犬の座標をランダム選択（猫と40m以上離れた平地）
        Vector2 dog = DogCandidates[Random.Range(0, DogCandidates.Length)];
        for (int retry = 0; retry < 20; retry++)
        {
            dog = DogCandidates[Random.Range(0, DogCandidates.Length)];
            if (Vector2.Distance(new Vector2(CatX, CatZ), dog) >= 40f)
                break;
        }
        Vector3 safeDog = FindSafeFlatPosition(land, dog.x, dog.y);
        DogX = safeDog.x; DogZ = safeDog.z;

        // その他の動物NPC
        var sparrow = SparrowCandidates[Random.Range(0, SparrowCandidates.Length)];
        Vector3 safeSparrow = FindSafeFlatPosition(land, sparrow.x, sparrow.y);
        SparrowX = safeSparrow.x; SparrowZ = safeSparrow.z;

        var muskrat = MuskratCandidates[Random.Range(0, MuskratCandidates.Length)];
        Vector3 safeMuskrat = FindSafeFlatPosition(land, muskrat.x, muskrat.y);
        MuskratX = safeMuskrat.x; MuskratZ = safeMuskrat.z;

        var pudu = PuduCandidates[Random.Range(0, PuduCandidates.Length)];
        Vector3 safePudu = FindSafeFlatPosition(land, pudu.x, pudu.y);
        PuduX = safePudu.x; PuduZ = safePudu.z;

        var colobus = ColobusCandidates[Random.Range(0, ColobusCandidates.Length)];
        Vector3 safeColobus = FindSafeFlatPosition(land, colobus.x, colobus.y);
        ColobusX = safeColobus.x; ColobusZ = safeColobus.z;

        var gecko = GeckoCandidates[Random.Range(0, GeckoCandidates.Length)];
        Vector3 safeGecko = FindSafeFlatPosition(land, gecko.x, gecko.y);
        GeckoX = safeGecko.x; GeckoZ = safeGecko.z;

        Debug.Log(
            $"[QuestLocations] 猫→{CatDirectionLabel}({CatX:F0},{CatZ:F0})  犬→{DogDirectionLabel}({DogX:F0},{DogZ:F0})\n" +
            $"  スズメ→({SparrowX:F0},{SparrowZ:F0})  マスクラット→({MuskratX:F0},{MuskratZ:F0})\n" +
            $"  プドゥ→({PuduX:F0},{PuduZ:F0})  コロブス→({ColobusX:F0},{ColobusZ:F0})  ヤモリ→({GeckoX:F0},{GeckoZ:F0})");
    }

    // ────────────────────────────────────────────────
    // 平地検出・埋まり防止ユーティリティ
    // ────────────────────────────────────────────────

    /// <summary>
    /// 地形の傾斜や凹凸を測定し、丘や山に埋まることなく見晴らしが良い完全な平地座標を返す。
    /// </summary>
    public static Vector3 FindSafeFlatPosition(Terrain land, float targetX, float targetZ, float offset = 0.08f)
    {
        if (land == null)
            return new Vector3(targetX, 21.5f + offset, targetZ);

        float bestX = targetX;
        float bestZ = targetZ;
        float bestY = SampleTerrainWorldY(land, targetX, targetZ);

        // 中心地点の傾斜（周囲1m四方の標高差）を測定
        float centerSlope = GetGroundSlope(land, targetX, targetZ);
        if (centerSlope < 0.12f) // 傾斜が極めて緩やか（約7度未満の平地）ならそのまま採用
        {
            return CheckRaycastHeight(new Vector3(targetX, bestY + offset, targetZ));
        }

        // 傾斜がある（丘・山の斜面など）場合、周囲半径2m〜8mをスパイラル探索して最も平坦な平地へ自動シフト
        float minSlope = centerSlope;
        float[] radii = { 2f, 3.5f, 5f, 7f, 9f };
        int angles = 8;

        for (int r = 0; r < radii.Length; r++)
        {
            float radius = radii[r];
            for (int a = 0; a < angles; a++)
            {
                float rad = (a * 360f / angles) * Mathf.Deg2Rad;
                float testX = targetX + Mathf.Cos(rad) * radius;
                float testZ = targetZ + Mathf.Sin(rad) * radius;

                // 湖や島の境界外はスキップ
                if (testX < 35f || testX > 280f || testZ < 35f || testZ > 280f)
                    continue;
                if (Vector2.Distance(new Vector2(testX, testZ), new Vector2(133f, 169f)) < 38f)
                    continue;

                float slope = GetGroundSlope(land, testX, testZ);
                if (slope < minSlope)
                {
                    minSlope = slope;
                    bestX = testX;
                    bestZ = testZ;
                    bestY = SampleTerrainWorldY(land, testX, testZ);

                    // 十分に平坦な場所が見つかれば即座に決定
                    if (minSlope < 0.08f)
                        break;
                }
            }
            if (minSlope < 0.08f)
                break;
        }

        return CheckRaycastHeight(new Vector3(bestX, bestY + offset, bestZ));
    }

    /// <summary>
    /// 地形の傾斜度合い（1mあたりの最大標高差）を測定する。
    /// </summary>
    static float GetGroundSlope(Terrain land, float x, float z)
    {
        float d = 1.0f;
        float yCenter = SampleTerrainWorldY(land, x, z);
        float yNorth  = SampleTerrainWorldY(land, x, z + d);
        float ySouth  = SampleTerrainWorldY(land, x, z - d);
        float yEast   = SampleTerrainWorldY(land, x + d, z);
        float yWest   = SampleTerrainWorldY(land, x - d, z);

        float maxDiff = Mathf.Max(
            Mathf.Abs(yNorth - yCenter),
            Mathf.Abs(ySouth - yCenter),
            Mathf.Abs(yEast - yCenter),
            Mathf.Abs(yWest - yCenter));

        return maxDiff / d;
    }

    static float SampleTerrainWorldY(Terrain land, float x, float z)
    {
        float h = land.SampleHeight(new Vector3(x, 0f, z)) + land.transform.position.y;
        // 水面（18.0m）より低くならないよう安全保証
        return Mathf.Max(18.3f, h);
    }

    /// <summary>
    /// 真上からRaycastを行い、岩やメッシュなどの衝突オブジェクトの上面に確実に立たせる。
    /// </summary>
    static Vector3 CheckRaycastHeight(Vector3 pos)
    {
        Ray ray = new Ray(new Vector3(pos.x, pos.y + 10f, pos.z), Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 20f))
        {
            // トリガーでない固いコライダーの上に正確に接地
            if (!hit.collider.isTrigger && hit.point.y > pos.y - 0.5f)
            {
                pos.y = hit.point.y + 0.06f;
            }
        }
        return pos;
    }

    public static float GroundY(Terrain land, float x, float z, float offset = 0.08f)
    {
        if (land == null)
            return Mathf.Max(18.5f, offset);
        float h = SampleTerrainWorldY(land, x, z);
        return h + offset;
    }

    public static float WalkableGroundY(Terrain land, float x, float z, float offset = 0.08f)
    {
        return FindSafeFlatPosition(land, x, z, offset).y;
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
        target.position = FindSafeFlatPosition(land, x, z, 0.08f);
    }
}
