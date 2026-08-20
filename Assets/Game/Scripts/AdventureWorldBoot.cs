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
            AdventureTerrainSnap.FixNorthEastGrassland();
            AdventureTerrainSnap.CarveStartPlateauRamp();
            AdventureTerrainSnap.RemoveUnintendedPuddles();
        }

        AdventureMarkerCleanup.RemoveAllQuestMarkers();
        AdventureMarkerCleanup.RemovePetBeacons();
        AdventureMarkerCleanup.RemoveFloatingShoreBands();
        AdventureMarkerCleanup.RemoveFloatingWaterSurfaces();
        SnapAllLostPets();
        AdventureTerrainSnap.FixNorthWestGrassland();
        AdventureTerrainSnap.FixNorthEastGrassland();
        AdventureTerrainSnap.CarveStartPlateauRamp();
        AdventureTerrainSnap.RemoveUnintendedPuddles();
        AdventureTerrainSnap.ApplyNaturalLandscape();
        AdventureTerrainSnap.FillCliffAtX158Z194();
        AdventureTerrainSnap.NaturalizeNorthWestPondShore();
        AdventureTerrainSnap.EnsureLandWaterSeparation();
        AdventureTerrainSnap.SnapAllFloatingRocksAndTrees();
        AdventureTerrainSnap.ReplaceBlackCubesWithRocksOrBoxes();
        AdventurePrimitiveVisuals.FixAnimalMaterials();
        SetupBrightWorldLighting();
    }

    public static void SetupBrightWorldLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.60f, 0.64f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.50f, 0.54f, 0.52f);
        RenderSettings.ambientGroundColor = new Color(0.38f, 0.40f, 0.38f);
        RenderSettings.ambientIntensity = 0.85f;

        RenderSettings.fog = false;

        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
        {
            if (light.type == LightType.Directional)
            {
                light.intensity = 0.95f;
                light.color = new Color(1.0f, 0.96f, 0.90f);
                light.shadowStrength = 0.65f;
            }
        }
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
