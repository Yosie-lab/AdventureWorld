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

    void Start()
    {
        if (target != null)
            _yaw = target.eulerAngles.y;
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
        transform.SetPositionAndRotation(desired, rot);
    }
}
