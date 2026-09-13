using UnityEngine;

/// <summary>
/// ぽっかりと浮かぶ白い雲が、風に乗ってゆっくりと空を漂うスクリプト
/// </summary>
public class AdventureCloudDrift : MonoBehaviour
{
    [SerializeField] private float driftSpeed = 1.2f;
    [SerializeField] private Vector3 driftDirection = new Vector3(-1f, 0f, 0.2f);
    [SerializeField] private float bobSpeed = 0.5f;
    [SerializeField] private float bobHeight = 1.5f;
    [SerializeField] private float boundMinX = -200f;
    [SerializeField] private float boundMaxX = 1200f;
    [SerializeField] private float boundMinZ = -200f;
    [SerializeField] private float boundMaxZ = 1200f;

    private float baseHeight;
    private float seed;

    void Start()
    {
        baseHeight = transform.position.y;
        seed = Random.Range(0f, 100f);
        driftDirection.Normalize();
    }

    void Update()
    {
        // 水平ドリフト
        Vector3 pos = transform.position;
        pos += driftDirection * (driftSpeed * Time.deltaTime);

        // 緩やかな上下の浮遊感
        float bob = Mathf.Sin(Time.time * bobSpeed + seed) * bobHeight;
        pos.y = baseHeight + bob;

        // 境界外に出たら反対側からループ
        if (pos.x < boundMinX) pos.x = boundMaxX;
        if (pos.x > boundMaxX) pos.x = boundMinX;
        if (pos.z < boundMinZ) pos.z = boundMaxZ;
        if (pos.z > boundMaxZ) pos.z = boundMinZ;

        transform.position = pos;
    }
}
