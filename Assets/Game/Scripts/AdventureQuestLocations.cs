using UnityEngine;

public static class AdventureQuestLocations
{
    // ────────────────────────────────────────────────
    // 猫の候補座標（島全域の歩行可能な平坦草地・全12箇所）
    // ────────────────────────────────────────────────
    static readonly Vector2[] CatCandidates =
    {
        new Vector2(213f, 213f),   // 北東草地（従来の場所）
        new Vector2(205f, 240f),   // 北東の奥・丘の近く
        new Vector2(235f, 215f),   // 北東〜東の木陰
        new Vector2(255f, 175f),   // 東の海岸沿い草地
        new Vector2(245f, 140f),   // 東南東の開けた平地
        new Vector2(225f, 115f),   // 南東の静かな草地
        new Vector2(185f,  85f),   // 南の森の小道
        new Vector2(170f, 235f),   // 北部中央の小高い平地
        new Vector2( 95f, 225f),   // 北西の奥まった草地
        new Vector2( 65f, 165f),   // 西側の森の端
        new Vector2( 85f,  95f),   // 南西の穏やかな緑地
        new Vector2(185f, 165f),   // 池の東側の木陰
    };

    // ────────────────────────────────────────────────
    // 犬の候補座標（島全域の歩行可能な平坦草地・全12箇所）
    // ────────────────────────────────────────────────
    static readonly Vector2[] DogCandidates =
    {
        new Vector2(122f, 205f),   // 北西草地（従来の場所）
        new Vector2(115f, 240f),   // 北西の北寄り平地
        new Vector2( 85f, 215f),   // 北西の奥の草地
        new Vector2( 60f, 185f),   // 西の海岸寄り平地
        new Vector2( 80f, 150f),   // 池の西側の小道
        new Vector2( 95f, 110f),   // 南西の開けた草地
        new Vector2(115f,  85f),   // 南西の小山付近
        new Vector2(145f,  75f),   // 南の海岸沿い草地
        new Vector2(210f,  95f),   // 南東ののどかな平地
        new Vector2(260f, 160f),   // 東の丘のふもと
        new Vector2(150f, 245f),   // 北部中央の平地
        new Vector2(220f, 230f),   // 北東の草地端
    };

    // ────────────────────────────────────────────────
    // スズメの候補座標（明るい草地、高台や入口付近・全5箇所）
    // ────────────────────────────────────────────────
    static readonly Vector2[] SparrowCandidates =
    {
        new Vector2(185f, 165f),   // スタート広場の東寄り
        new Vector2(170f, 195f),   // 北の草地入口
        new Vector2(145f, 175f),   // スタート西寄り
        new Vector2(200f, 150f),   // 広場南東の丘
        new Vector2(160f, 210f),   // 北の小道沿い
    };

    // ────────────────────────────────────────────────
    // マスクラットの候補座標（池・水辺に近い平地・全5箇所）
    // ────────────────────────────────────────────────
    static readonly Vector2[] MuskratCandidates =
    {
        new Vector2(152f, 175f),   // 池の北東岸
        new Vector2(145f, 195f),   // 北西草地入口
        new Vector2(175f, 200f),   // 北の草地
        new Vector2(115f, 175f),   // 池の西岸
        new Vector2(135f, 140f),   // 池の南岸
    };

    // ────────────────────────────────────────────────
    // プドゥの候補座標（少し奥まった草地・全5箇所）
    // ────────────────────────────────────────────────
    static readonly Vector2[] PuduCandidates =
    {
        new Vector2(185f, 190f),   // 北東の草地端
        new Vector2(148f, 200f),   // 北西草地への道
        new Vector2(165f, 215f),   // 北の奥
        new Vector2(195f, 120f),   // 南東の森
        new Vector2(105f, 130f),   // 南西の林
    };

    // ────────────────────────────────────────────────
    // コロブスの候補座標（広場やや北、木陰など・全5箇所）
    // ────────────────────────────────────────────────
    static readonly Vector2[] ColobusCandidates =
    {
        new Vector2(170f, 175f),   // 広場の北寄り中央
        new Vector2(185f, 185f),   // 広場の北東
        new Vector2(150f, 180f),   // 広場の北西
        new Vector2(180f, 140f),   // 広場の南東
        new Vector2(140f, 155f),   // 広場の西
    };

    // ────────────────────────────────────────────────
    // ヤモリの候補座標（スタート広場周辺・全5箇所）
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
    // ゲーム起動のたびに Randomize() でランダム選択される座標
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
    /// 猫と犬は十分な距離（40m以上）離れた全然違う場所に配置される。
    /// </summary>
    public static void Randomize()
    {
        if (_randomized)
            return;
        _randomized = true;

        // 猫の座標をランダム選択
        var cat = CatCandidates[Random.Range(0, CatCandidates.Length)];
        CatX = cat.x; CatZ = cat.y;

        // 犬の座標をランダム選択（猫と近すぎないように最低40m離す）
        Vector2 dog = DogCandidates[Random.Range(0, DogCandidates.Length)];
        for (int retry = 0; retry < 20; retry++)
        {
            dog = DogCandidates[Random.Range(0, DogCandidates.Length)];
            if (Vector2.Distance(cat, dog) >= 40f)
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

        Debug.Log(
            $"[QuestLocations] 猫→{CatDirectionLabel}({CatX},{CatZ})  犬→{DogDirectionLabel}({DogX},{DogZ})\n" +
            $"  スズメ→({SparrowX},{SparrowZ})  マスクラット→({MuskratX},{MuskratZ})\n" +
            $"  プドゥ→({PuduX},{PuduZ})  コロブス→({ColobusX},{ColobusZ})  ヤモリ→({GeckoX},{GeckoZ})");
    }

    // ────────────────────────────────────────────────
    // ユーティリティ
    // ────────────────────────────────────────────────

    public static float GroundY(Terrain land, float x, float z, float offset = 0.02f)
    {
        if (land == null)
            return Mathf.Max(18.5f, offset);
        float h = land.SampleHeight(new Vector3(x, 0f, z)) + land.transform.position.y;
        // 水面（約18.0f）より下にめり込まない安全マージン
        return Mathf.Max(18.2f, h) + offset;
    }

    public static float WalkableGroundY(Terrain land, float x, float z, float offset = 0.02f)
    {
        return GroundY(land, x, z, offset);
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
        target.position = new Vector3(x, GroundY(land, x, z), z);
    }
}
