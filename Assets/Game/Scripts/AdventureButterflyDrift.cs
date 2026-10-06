using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 『Rust & Float』蝶々の優雅な浮遊・旋回モーションコンポーネント。
/// ・距離カリング（36m以遠の更新・アニメーション停止）による極めて高効率な負荷削減。
/// ・Idyllic Fantasy Nature のアニメーションイベント 'AnimationEnded' の安全受信（エラーログ連打の完全根絶）。
/// ・静的リスト ActiveButterflies により、ドローン等からの FindObjectsByType ポーリング負荷を完全排除。
/// </summary>
public class AdventureButterflyDrift : MonoBehaviour
{
    public static readonly List<AdventureButterflyDrift> ActiveButterflies = new List<AdventureButterflyDrift>();

    public float radius = 4.5f;
    public float speed = 0.65f;
    public float bob = 0.55f;

    private Vector3 _home;
    private float _t;
    private Animator _animator;
    private bool _isCulled;
    private float _checkTimer;

    private const float CULL_DISTANCE_SQR = 36f * 36f; // 36m以上でカリング
    private static Transform _cameraTransform;

    private void Awake()
    {
        _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        ActiveButterflies.Add(this);
        _home = transform.position;
        _t = Random.Range(0f, 20f);
        speed += Random.Range(-0.10f, 0.15f);
        radius += Random.Range(-0.8f, 1.2f);
        _checkTimer = Random.Range(0f, 0.4f);
    }

    private void OnDisable()
    {
        ActiveButterflies.Remove(this);
    }

    private void Update()
    {
        // 0.3秒ごとに距離カリング判定（GC Alloc ゼロ）
        _checkTimer -= Time.deltaTime;
        if (_checkTimer <= 0f)
        {
            _checkTimer = 0.30f + Random.Range(0f, 0.1f);
            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }

            if (_cameraTransform != null)
            {
                float distSqr = (transform.position - _cameraTransform.position).sqrMagnitude;
                bool shouldCull = distSqr > CULL_DISTANCE_SQR;
                if (shouldCull != _isCulled)
                {
                    _isCulled = shouldCull;
                    if (_animator != null)
                    {
                        _animator.enabled = !_isCulled;
                    }
                }
            }
        }

        if (_isCulled) return;

        _t += Time.deltaTime * speed;
        Vector3 next = _home + new Vector3(
            Mathf.Cos(_t) * radius,
            1.2f + Mathf.Sin(_t * 1.6f) * bob,
            Mathf.Sin(_t * 0.85f) * radius);
        Vector3 dir = next - transform.position;
        transform.position = next;
        if (dir.sqrMagnitude > 0.0004f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 5f * Time.deltaTime);
        }
    }

    /// <summary>
    /// Idyllic Fantasy Nature の蝶々FBXアニメーションクリップに設定された
    /// AnimationEvent 'AnimationEnded' のレシーバー（エラーログの大量発生を完全に防ぐ）
    /// </summary>
    public void AnimationEnded()
    {
        // アニメーション周期終端の正常受信
    }
}
