using UnityEngine;

public class AdventureHintSign : MonoBehaviour
{
    public string displayName = "看板";
    public string message;
    public float radius = 4.5f;

    public bool IsInRange(Vector3 playerPosition)
    {
        Vector3 a = transform.position;
        a.y = 0f;
        Vector3 b = playerPosition;
        b.y = 0f;
        return (a - b).sqrMagnitude <= radius * radius;
    }
}
