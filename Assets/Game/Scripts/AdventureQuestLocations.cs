using UnityEngine;

public static class AdventureQuestLocations
{
    // ────────────────────────────────────────────────
    // 猫の候補座標（北東エリア・岩崖のない平坦な草地のみ）
    // ────────────────────────────────────────────────
    static readonly Vector2[] CatCandidates =
    {
        new Vector2(213f, 213f),   // 北東草地（元の位置）
        new Vector2(198f, 235f),   // 北東の奥
        new Vector2(232f, 198f),   // 東側草地
    };

    // ────────────────────────────────────────────────
    // 犬の候補座標（北西エリア・岩崖のない平坦な草地のみ）
    // ────────────────────────────────────────────────
    static readonly Vector2[] DogCandidates =
    {
        new Vector2(122f, 205f),   // 北西草地（元の位置）
        new Vector2( 95f, 218f),   // 北西の奥
        new Vector2(112f, 242f),   // 北側草地
    };

    // ────────────────────────────────────────────────
    // スズメの候補座標（明るい草地、高台や入口付近）
    // ────────────────────────────────────────────────
    static readonly Vector2[] SparrowCandidates =
    {
        new Vector2(185f, 165f),   // スタート広場の東寄り
        new Vector2(170f, 195f),   // 北の草地入口
        new Vector2(145f, 175f),   // スタート西寄り
    };

    // ────────────────────────────────────────────────
    // マスクラットの候補座標（池・水辺に近い平地）
    // ────────────────────────────────────────────────
    static readonly Vector2[] MuskratCandidates =
    {
        new Vector2(152f, 175f),   // 池の北西岸（元付近）
        new Vector2(145f, 195f),   // 北西草地入口
        new Vector2(175f, 200f),   // 北の草地
    };

    // ────────────────────────────────────────────────
    // プドゥの候補座標（少し奥まった草地、臆病な印象）
    // ────────────────────────────────────────────────
    static readonly Vector2[] PuduCandidates =
    {
        new Vector2(185f, 190f),   // 北東の草地端
        new Vector2(148f, 200f),   // 北西草地への道
        new Vector2(165f, 215f),   // 北の奥
    };

    // ────────────────────────────────────────────────
    // コロブスの候補座標（広場やや北、嘘つきキャラ）
    // ────────────────────────────────────────────────
    static readonly Vector2[] ColobusCandidates =
    {
        new Vector2(170f, 175f),   // 広場の北寄り中央
        new Vector2(185f, 185f),   // 広場の北東
        new Vector2(150f, 180f),   // 広場の北西
    };

    // ────────────────────────────────────────────────
    // ヤモリの候補座標（スタート広場内、意地悪キャラ）
    // ────────────────────────────────────────────────
    static readonly Vector2[] GeckoCandidates =
    {
        new Vector2(156f, 164f),   // 元の位置
        new Vector2(175f, 155f),   // 広場の南東
        new Vector2(145f, 160f),   // 広場の南西
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

    public static Vector3 CatPosition  => new Vector3(CatX,  0f, CatZ);
    public static Vector3 DogPosition  => new Vector3(DogX,  0f, DogZ);

    // スタート付近の固定看板座標
    public static readonly Vector3 HintStart = new Vector3(165f, 0f, 130f);
    public static readonly Vector3 HintMemo  = new Vector3(165f, 0f, 168f);

    // 猫・犬のトレイル看板はランダム配置後の座標に追従（スタートと目的地の中間付近）
    public static Vector3 HintCatTrail =>
        new Vector3(
            Mathf.Lerp(170f, CatX, 0.38f),
            0f,
            Mathf.Lerp(170f, CatZ, 0.38f));

    public static Vector3 HintDogTrail =>
        new Vector3(
            Mathf.Lerp(160f, DogX, 0.38f),
            0f,
            Mathf.Lerp(170f, DogZ, 0.38f));

    public static string CatCoordLabel => "X" + Mathf.RoundToInt(CatX) + " Z" + Mathf.RoundToInt(CatZ);
    public static string DogCoordLabel => "X" + Mathf.RoundToInt(DogX) + " Z" + Mathf.RoundToInt(DogZ);

    // ────────────────────────────────────────────────
    // ゲームセッションごとに1回だけランダム化する
    // ────────────────────────────────────────────────
    static bool _randomized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetRandomize() => _randomized = false;

    /// <summary>
    /// 猫・犬・その他の動物の出現座標をランダムに選択する。
    /// 1セッション内で初回呼び出し時のみ有効。
    /// </summary>
    public static void Randomize()
    {
        if (_randomized)
            return;
        _randomized = true;

        // 猫・犬（迷子ペット）
        var cat = CatCandidates[Random.Range(0, CatCandidates.Length)];
        CatX = cat.x; CatZ = cat.y;

        var dog = DogCandidates[Random.Range(0, DogCandidates.Length)];
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
            $"[QuestLocations] 猫→({CatX},{CatZ})  犬→({DogX},{DogZ})\n" +
            $"  スズメ→({SparrowX},{SparrowZ})  マスクラット→({MuskratX},{MuskratZ})\n" +
            $"  プドゥ→({PuduX},{PuduZ})  コロブス→({ColobusX},{ColobusZ})  ヤモリ→({GeckoX},{GeckoZ})");
    }

    // ────────────────────────────────────────────────
    // ユーティリティ
    // ────────────────────────────────────────────────

    public static float GroundY(Terrain land, float x, float z, float offset = 0.02f)
    {
        if (land == null)
            return offset;
        return land.SampleHeight(new Vector3(x, 0f, z)) + land.transform.position.y + offset;
    }

    // 看板・設置物用。平坦化された高さに正確にアラインする。
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
