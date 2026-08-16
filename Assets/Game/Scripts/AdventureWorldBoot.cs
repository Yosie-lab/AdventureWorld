using UnityEngine;

[DefaultExecutionOrder(-200)]
public static class AdventureWorldBoot
{
    static bool _configured;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Reset()
    {
        _configured = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AfterSceneLoad()
    {
        Configure();
    }

    public static void Configure()
    {
        if (!_configured)
        {
            _configured = true;
            foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
            {
                if (terrain.name == "WaterTerrain")
                {
                    terrain.drawHeightmap = false;
                    terrain.drawTreesAndFoliage = false;
                    terrain.detailObjectDensity = 0f;
                    var col = terrain.GetComponent<TerrainCollider>();
                    if (col != null)
                        col.enabled = false;
                }
                else if (terrain.name == "LandTerrain")
                {
                    terrain.detailObjectDistance = 0f;
                    terrain.detailObjectDensity = 0f;
                }
            }

            AdventureTerrainSnap.FixNorthWestGrassland();
        }

        AdventureMarkerCleanup.RemoveAllQuestMarkers();
        SnapAllLostPets();
        AdventureLostPetVisuals.EnsureBeacons();
        AdventureTerrainSnap.FixNorthWestGrassland();
    }

    static void SnapAllLostPets()
    {
        foreach (var npc in Object.FindObjectsByType<AdventureNpc>(FindObjectsInactive.Exclude))
        {
            if (npc.npcId != "dog" && npc.npcId != "cat")
                continue;

            if (npc.GetComponent<AdventureLostPetAnchor>() == null)
                npc.gameObject.AddComponent<AdventureLostPetAnchor>();

            AdventureQuestLocations.SnapLostPet(npc.transform, npc.npcId);
            AdventureLostPetVisuals.EnsurePetModel(npc.transform, npc.npcId);
        }
    }
}
