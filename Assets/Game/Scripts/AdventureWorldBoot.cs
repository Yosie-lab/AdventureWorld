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

            // ゲーム起動のたびに猫・犬の出現座標をランダム選択する
            AdventureQuestLocations.Randomize();

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
        SnapAllLostPets();
        AdventurePrimitiveVisuals.FixAnimalMaterials();
        SetupBrightWorldLighting();
    }

    public static void SetupBrightWorldLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.70f, 0.82f, 0.95f);   // 爽やかな青空の照り返し
        RenderSettings.ambientEquatorColor = new Color(0.62f, 0.72f, 0.68f); // 鮮やかな緑地地平
        RenderSettings.ambientGroundColor = new Color(0.45f, 0.50f, 0.45f);
        RenderSettings.ambientIntensity = 1.10f;

        RenderSettings.fog = false;

        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
        {
            if (light.type == LightType.Directional)
            {
                light.intensity = 1.25f;
                light.color = new Color(1.0f, 0.97f, 0.92f); // 温かみのある澄んだ陽光
                light.shadowStrength = 0.55f; // ソフトで美しい影
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
