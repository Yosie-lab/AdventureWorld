using UnityEngine;
using System.Linq;

[DefaultExecutionOrder(-300)]
public class AdventureRustFloatIsland : MonoBehaviour
{
    public float waterLevel = 5.5f;
    public float walkPadding = 10f;

    void Awake()
    {
        var land = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude)
            .FirstOrDefault(t => t.name == "LandTerrain" || t.name == "IslandTerrain");
        if (land == null)
            return;

        var bounds = AdventureIslandBoundary.Instance;
        if (bounds == null)
        {
            var go = new GameObject("IslandBoundary");
            bounds = go.AddComponent<AdventureIslandBoundary>();
        }

        Vector3 origin = land.transform.position;
        Vector3 size = land.terrainData.size;
        bounds.walkMinX = origin.x + walkPadding;
        bounds.walkMaxX = origin.x + size.x - walkPadding;
        bounds.walkMinZ = origin.z + walkPadding;
        bounds.walkMaxZ = origin.z + size.z - walkPadding;
        bounds.waterLevel = waterLevel;
        bounds.lakeRadius = 0f;
        bounds.placeShoreRocks = false;
    }
}
