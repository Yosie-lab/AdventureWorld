using UnityEngine;
using UnityEditor;

public static class AdventureInspectRampDetails
{
    [MenuItem("Adventure/Debug/Inspect Boardwalk Ramp")]
    public static void InspectRamp()
    {
        var ramp = GameObject.Find("BoardwalkRamp_212deg");
        if (ramp == null)
        {
            Debug.LogError("BoardwalkRamp_212deg not found!");
            return;
        }

        Terrain land = Terrain.activeTerrain ?? Object.FindFirstObjectByType<Terrain>();

        Debug.Log($"=== BoardwalkRamp_212deg Info (Total Children: {ramp.transform.childCount}) ===");
        for (int i = 0; i < ramp.transform.childCount; i++)
        {
            Transform child = ramp.transform.GetChild(i);
            Vector3 p = child.position;
            float ty = land != null ? (land.SampleHeight(p) + land.transform.position.y) : 0f;
            float diff = p.y - ty;
            if (child.name.StartsWith("Plank") && (i % 5 == 0 || i == ramp.transform.childCount - 1))
            {
                Debug.Log($"Child {i}: '{child.name}' at pos {p}, terrain Y={ty:F2}, diff={diff:F2}m");
            }
        }
    }
}
