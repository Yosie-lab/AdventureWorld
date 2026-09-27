using UnityEngine;
using UnityEditor;

public static class AdventureInspectBeachObjects
{
    [MenuItem("Adventure/Debug/Inspect Beach Objects")]
    public static void InspectObjects()
    {
        var allRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        Debug.Log($"Total MeshRenderers in scene: {allRenderers.Length}");

        foreach (var r in allRenderers)
        {
            Vector3 p = r.transform.position;
            if (p.x >= 120f && p.x <= 170f && p.z >= 240f && p.z <= 310f)
            {
                // カニや貝殻、魚以外の構造物を探す
                string n = r.gameObject.name;
                if (!n.Contains("Crab") && !n.Contains("Shell") && !n.Contains("Fish") && !n.Contains("Leg") && !n.Contains("Claw") && !n.Contains("Eye"))
                {
                    Debug.Log($"Beach Object: '{n}' (Parent: '{r.transform.parent?.name}') at pos {p}, scale {r.transform.lossyScale}");
                }
            }
        }
    }
}
