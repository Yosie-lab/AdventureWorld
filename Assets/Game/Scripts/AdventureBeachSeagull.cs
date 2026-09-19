using UnityEngine;

/// <summary>
/// 砂浜・波打ち際に佇むウミネコ（カモメ）
/// 首をかしげたり羽づくろいしたりして過ごし、プレイヤーが近づくと優雅に空へ飛び立つ
/// </summary>
public class AdventureBeachSeagull : MonoBehaviour
{
    private Transform _head;
    private Transform _leftWing;
    private Transform _rightWing;
    private Transform _player;

    private Vector3 _startPos;
    private Quaternion _startRot;
    private float _idleTimer;
    private float _headTargetAngle;
    private float _currentHeadAngle;

    private bool _isTakingOff = false;
    private float _flightTime = 0f;
    private Vector3 _flightDirection;

    const float TakeoffDistance = 5.5f; // プレイヤーがこの距離に入ると飛び立つ

    void Start()
    {
        _startPos = transform.position;
        _startRot = transform.rotation;

        _head = transform.Find("Head");
        _leftWing = transform.Find("LeftWing");
        _rightWing = transform.Find("RightWing");

        var niko = GameObject.Find("Niko");
        if (niko != null) _player = niko.transform;

        _idleTimer = Random.Range(1.5f, 4.0f);
    }

    void Update()
    {
        if (_isTakingOff)
        {
            UpdateFlight();
            return;
        }

        UpdateIdle();
        CheckPlayerDistance();
    }

    void UpdateIdle()
    {
        _idleTimer -= Time.deltaTime;
        if (_idleTimer <= 0f)
        {
            // 時折首をかしげる
            _headTargetAngle = Random.Range(-25f, 25f);
            _idleTimer = Random.Range(2.0f, 5.5f);
        }

        _currentHeadAngle = Mathf.Lerp(_currentHeadAngle, _headTargetAngle, Time.deltaTime * 6f);
        if (_head != null)
        {
            _head.localRotation = Quaternion.Euler(0f, _currentHeadAngle, Mathf.Sin(Time.time * 2f) * 4f);
        }

        // 呼吸のような微小な揺れ
        transform.position = _startPos + Vector3.up * (Mathf.Sin(Time.time * 3f) * 0.015f);
    }

    void CheckPlayerDistance()
    {
        if (_player == null) return;

        float dist = Vector3.Distance(transform.position, _player.position);
        if (dist < TakeoffDistance)
        {
            TakeOff();
        }
    }

    [SerializeField] private AudioClip seagullCryClip;
    private AudioSource _audioSource;

    public void SetCryClip(AudioClip clip)
    {
        seagullCryClip = clip;
    }

    public void TakeOff()
    {
        if (_isTakingOff) return;
        _isTakingOff = true;
        _flightTime = 0f;

        // 飛び立つ瞬間にウミネコの鳴き声を再生
        if (seagullCryClip != null)
        {
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 1.0f; // 3D音響
                _audioSource.minDistance = 3f;
                _audioSource.maxDistance = 25f;
                _audioSource.rolloffMode = AudioRolloffMode.Linear;
            }
            _audioSource.PlayOneShot(seagullCryClip, 0.7f);
        }

        // 海側・上空へ向かって飛び立つ
        Vector3 fromCenter = (transform.position - new Vector3(512f, transform.position.y, 512f)).normalized;
        _flightDirection = (fromCenter + Vector3.up * 0.75f + transform.forward * 0.5f).normalized;

        transform.rotation = Quaternion.LookRotation(_flightDirection);
    }

    void UpdateFlight()
    {
        _flightTime += Time.deltaTime;

        // 上昇・前進飛行
        float speed = Mathf.Lerp(3.5f, 8.0f, _flightTime * 0.5f);
        transform.position += _flightDirection * (speed * Time.deltaTime);

        // 翼の羽ばたき
        float flap = Mathf.Sin(_flightTime * 14f) * 28f;
        if (_leftWing != null) _leftWing.localRotation = Quaternion.Euler(flap, 0f, 15f);
        if (_rightWing != null) _rightWing.localRotation = Quaternion.Euler(flap, 0f, -15f);

        // 十分上空へ行ったら徐々にフェードアウト・消去または再配置
        if (_flightTime > 12f)
        {
            // 元の位置に戻して再着地
            _isTakingOff = false;
            transform.position = _startPos;
            transform.rotation = _startRot;
            if (_leftWing != null) _leftWing.localRotation = Quaternion.Euler(0f, 0f, 12f);
            if (_rightWing != null) _rightWing.localRotation = Quaternion.Euler(0f, 0f, -12f);
        }
    }
}
