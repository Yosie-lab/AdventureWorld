using UnityEngine;

public static class AdventureMarkerCleanup
{
    public static void RemoveAllQuestMarkers()
    {
        var dogMarker = GameObject.Find("Marker_犬");
        if (dogMarker != null)
            Object.Destroy(dogMarker);

        var catMarker = GameObject.Find("Marker_猫");
        if (catMarker != null)
            Object.Destroy(catMarker);
    }
}
