using UnityEngine;

/// <summary>
/// 楽園の水辺や砂浜を舞うトンボ（Dragonfly）
/// スイスイと直線的に飛び、空中でピタッとホバリング（羽の超高速振動）し、
/// 水面や草むらの上を軽快に飛び回るリアルなトンボAI
/// </summary>
public class AdventureDragonfly : MonoBehaviour
{
    [Header("飛翔パラメータ")]
    [SerializeField] private float flySpeed = 3.5f;
    [SerializeField] private float flightRadius = 7f;
    [SerializeField] private float minHeight = 5.8f;
    [SerializeField] private float maxHeight = 7.5f;

    private Vector3 _homePosition;
    private Vector3 _targetPosition;
    private bool _isHovering = false;
    private float _stateTimer = 0f;

    private Transform _leftWings;
    private Transform _rightWings;
    private float _wingPhase = 0f;

    public void Setup(Transform leftWing, Transform rightWing, Vector3 homePos)
    {
        _leftWings = leftWing;
        _rightWings = rightWing;
        _homePosition = homePos;
        _targetPosition = homePos;
        _isHovering = false;
        _stateTimer = Random.Range(1f, 2.5f);
    }

    private void Start()
    {
        if (_homePosition == Vector3.zero) _homePosition = transform.position;
        if (_targetPosition == Vector3.zero) PickNewTarget();
    }

    private void Update()
    {
        // 1. 羽の超高速パタパタ
        _wingPhase += Time.deltaTime * 65f;
        float wingAngle = Mathf.Sin(_wingPhase) * 28f;
        if (_leftWings != null) _leftWings.localRotation = Quaternion.Euler(wingAngle, 0f, 0f);
        if (_rightWings != null) _rightWings.localRotation = Quaternion.Euler(-wingAngle, 0f, 0f);

        // 2. ホバリング vs 直線飛翔の状態遷移
        _stateTimer -= Time.deltaTime;
        if (_stateTimer <= 0f)
        {
            _isHovering = !_isHovering;
            if (_isHovering)
            {
                _stateTimer = Random.Range(1.2f, 3.0f); // ホバリング時間
            }
            else
            {
                PickNewTarget();
                _stateTimer = Random.Range(2.0f, 4.5f); // 飛翔時間
            }
        }

        if (_isHovering)
        {
            // 空中でわずかに揺れながら静止
            float hoverJitterY = Mathf.Sin(Time.time * 6f) * 0.04f;
            float hoverJitterX = Mathf.Cos(Time.time * 4f) * 0.03f;
            transform.position += new Vector3(hoverJitterX, hoverJitterY, 0f) * Time.deltaTime;
        }
        else
        {
            // 目標地点へ向かってスムーズに直線移動
            Vector3 toTarget = _targetPosition - transform.position;
            if (toTarget.sqrMagnitude > 0.05f)
            {
                var targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 7f);
                transform.position = Vector3.MoveTowards(transform.position, _targetPosition, flySpeed * Time.deltaTime);
            }
            else
            {
                _isHovering = true;
                _stateTimer = Random.Range(1.5f, 3.0f);
            }
        }
    }

    private void PickNewTarget()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float dist = Random.Range(1.5f, flightRadius);
        float h = Random.Range(minHeight, maxHeight);
        _targetPosition = _homePosition + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
        _targetPosition.y = h;
    }
}
