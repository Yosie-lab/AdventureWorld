using UnityEngine;

public static class AdventureQuestLocations
{
    // 旧座標 (88,145) / (118,218) は Cliffs・Rocks の内部で、
    // ペットがメッシュに埋もれて一切見えなかった。岩も木もない平地に移設。
    public const float CatX = 213f;
    public const float CatZ = 213f;
    public const float DogX = 122f;
    public const float DogZ = 205f;

    public static Vector3 CatPosition => new Vector3(CatX, 0f, CatZ);
    public static Vector3 DogPosition => new Vector3(DogX, 0f, DogZ);

    public static readonly Vector3 HintMemo = new Vector3(168f, 0f, 158f);
    public static readonly Vector3 HintCatTrail = new Vector3(192f, 0f, 190f);
    public static readonly Vector3 HintDogTrail = new Vector3(148f, 0f, 192f);

    public static string CatCoordLabel => "X" + Mathf.RoundToInt(CatX) + " Z" + Mathf.RoundToInt(CatZ);
    public static string DogCoordLabel => "X" + Mathf.RoundToInt(DogX) + " Z" + Mathf.RoundToInt(DogZ);

    public static float GroundY(Terrain land, float x, float z, float offset = 0.02f)
    {
        if (land == null)
            return offset;
        return land.SampleHeight(new Vector3(x, 0f, z)) + land.transform.position.y + offset;
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
