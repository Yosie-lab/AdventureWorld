using UnityEngine;

public class AdventureButterflyDrift : MonoBehaviour
{
    public float radius = 5.5f;
    public float speed = 0.7f;
    public float bob = 0.65f;

    Vector3 _home;
    float _t;

    void OnEnable()
    {
        _home = transform.position;
        _t = Random.Range(0f, 20f);
        speed += Random.Range(-0.15f, 0.2f);
        radius += Random.Range(-1.2f, 1.8f);
    }

    void Update()
    {
        _t += Time.deltaTime * speed;
        Vector3 next = _home + new Vector3(
            Mathf.Cos(_t) * radius,
            1.4f + Mathf.Sin(_t * 1.8f) * bob,
            Mathf.Sin(_t * 0.82f) * radius);
        Vector3 dir = next - transform.position;
        transform.position = next;
        if (dir.sqrMagnitude > 0.0004f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 6f * Time.deltaTime);
    }
}
