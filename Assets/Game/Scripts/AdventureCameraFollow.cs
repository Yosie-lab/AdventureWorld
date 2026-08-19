using UnityEngine;
using UnityEngine.InputSystem;

public class AdventureCameraFollow : MonoBehaviour
{
    public Transform target;
    public float height = 1.7f;
    public float distance = 6.5f;
    public float sensitivity = 0.07f;
    public float pitchMin = -5f;
    public float pitchMax = 28f;

    float _yaw;
    float _pitch = 12f;
    Terrain _land;
    Camera _cam;

    void Start()
    {
        if (target != null)
            _yaw = target.eulerAngles.y;
        _land = AdventureQuestLocations.FindLand();
        _cam = GetComponent<Camera>();
        if (_cam != null)
            _cam.nearClipPlane = 0.1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            bool locked = Cursor.lockState != CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        if (kb != null && kb.rKey.wasPressedThisFrame)
        {
            _pitch = 12f;
            if (target != null)
                _yaw = target.eulerAngles.y;
        }

        var mouse = Mouse.current;
        if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 delta = mouse.delta.ReadValue();
            _yaw += delta.x * sensitivity;
            _pitch = Mathf.Clamp(_pitch - delta.y * sensitivity, pitchMin, pitchMax);
        }

        if (target == null)
            return;

        Vector3 pivot = target.position + Vector3.up * height;
        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 desired = pivot + rot * new Vector3(0f, 0f, -distance);
        desired.y = Mathf.Max(desired.y, target.position.y + 1.4f);
        transform.SetPositionAndRotation(Deocclude(pivot, desired), rot);
    }

    // 急斜面ではカメラが地中に潜り、地面の裏側と空が見えて描画が壊れる。
    Vector3 Deocclude(Vector3 pivot, Vector3 desired)
    {
        Vector3 offset = desired - pivot;
        float dist = offset.magnitude;
        float castRadius = 0.35f;
        if (dist > 0.01f
            && Physics.SphereCast(pivot, castRadius, offset / dist, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            // 壁・崖・地面のメッシュ内にカメラが入ってNearClipで裏抜けしないよう、ヒット地点から安全マージンを確保
            float safeDist = Mathf.Max(hit.distance - 0.12f, 0.8f);
            desired = pivot + (offset / dist) * safeDist;
        }

        if (_land == null)
            _land = AdventureQuestLocations.FindLand();
        if (_land != null)
            desired.y = Mathf.Max(desired.y, AdventureQuestLocations.GroundY(_land, desired.x, desired.z) + 0.8f);

        return desired;
    }
}
