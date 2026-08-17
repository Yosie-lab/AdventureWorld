using UnityEngine;

public static class AdventureMarkerCleanup
{
    public static void RemoveAllQuestMarkers()
    {
        DestroyIfExists("Marker_犬");
        DestroyIfExists("Marker_猫");
    }

    public static void RemovePetBeacons()
    {
        DestroyIfExists("CatBeacon");
        DestroyIfExists("DogBeacon");
        DestroyIfExists("CatBeacon_old");
        DestroyIfExists("DogBeacon_old");
    }

    public static void RemoveFloatingShoreBands()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name != "SandBand")
                continue;
            Object.Destroy(t.gameObject);
        }
    }

    static void DestroyIfExists(string name)
    {
        var go = GameObject.Find(name);
        if (go != null)
            Object.Destroy(go);
    }
}
