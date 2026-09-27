using UnityEngine;
using UnityEditor;

public static class AdventureSnapAllCrabsToGround
{
    [MenuItem("Adventure/Crabs/Snap All Crabs To Ground")]
    public static void SnapAllCrabs()
    {
        var crabs = Object.FindObjectsByType<CrabWander>(FindObjectsSortMode.None);
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            terrain = Object.FindFirstObjectByType<Terrain>();
        }

        if (terrain == null)
        {
            Debug.LogError("No terrain found!");
            return;
        }

        int count = 0;
        foreach (var crab in crabs)
        {
            Undo.RecordObject(crab.transform, "Snap Crab to Ground");
            Vector3 pos = crab.transform.position;
            float groundY = terrain.SampleHeight(pos) + terrain.transform.position.y;

            // 地面法線に合わせる
            Vector3 tPos = terrain.transform.position;
            Vector3 tSize = terrain.terrainData.size;
            float u = Mathf.Clamp01((pos.x - tPos.x) / tSize.x);
            float v = Mathf.Clamp01((pos.z - tPos.z) / tSize.z);
            Vector3 normal = terrain.terrainData.GetInterpolatedNormal(u, v);

            pos.y = groundY + 0.02f;
            crab.transform.position = pos;

            Vector3 forward = Quaternion.Euler(0f, crab.transform.eulerAngles.y, 0f) * Vector3.forward;
            Vector3 right = Vector3.Cross(normal, forward).normalized;
            Vector3 correctedForward = Vector3.Cross(right, normal).normalized;
            crab.transform.rotation = Quaternion.LookRotation(correctedForward, normal);

            EditorUtility.SetDirty(crab.gameObject);
            count++;
        }

        Debug.Log($"Successfully snapped {count} crabs to terrain ground!");
    }
}
