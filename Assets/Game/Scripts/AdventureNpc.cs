using UnityEngine;

public class AdventureNpc : MonoBehaviour
{
    public string npcId;
    public string displayName;
    public float radius = 4.2f;

    public bool IsInRange(Vector3 playerPosition)
    {
        Vector3 a = transform.position;
        a.y = 0f;
        Vector3 b = playerPosition;
        b.y = 0f;
        return (a - b).sqrMagnitude <= radius * radius;
    }
}
