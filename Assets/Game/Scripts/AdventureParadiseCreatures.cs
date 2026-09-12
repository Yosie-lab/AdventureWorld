using UnityEngine;

/// <summary>
/// 楽園の生き物たち（カニ、カエル、トンボ）のアニメーション挙動
/// </summary>

/// <summary>砂浜のカニ（横歩きとハサミ振り）</summary>
public class CrabWander : MonoBehaviour
{
    Vector3 _startPos;
    float _timer;
    float _moveDuration = 2f;
    float _pauseDuration = 1.5f;
    bool _moving = true;
    int _dir = 1;
    Transform _leftClaw;
    Transform _rightClaw;

    void Start()
    {
        _startPos = transform.position;
        _leftClaw = transform.Find("LeftClaw");
        _rightClaw = transform.Find("RightClaw");
        _dir = Random.value > 0.5f ? 1 : -1;
    }

    void Update()
    {
        _timer += Time.deltaTime;

        if (_moving)
        {
            // カサカサと横歩き
            transform.Translate(Vector3.right * (_dir * 0.45f * Time.deltaTime), Space.Self);
            // 揺れ
            transform.localRotation = Quaternion.Euler(0f, transform.localEulerAngles.y, Mathf.Sin(Time.time * 18f) * 3f);

            if (_timer > _moveDuration)
            {
                _timer = 0f;
                _moving = false;
                _pauseDuration = 1f + Random.value * 2f;
            }
        }
        else
        {
            // 停止してハサミをチョキチョキ
            if (_leftClaw != null)
                _leftClaw.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 8f) * 20f, 0f, 15f);
            if (_rightClaw != null)
                _rightClaw.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 8f + 1f) * 20f, 0f, -15f);

            if (_timer > _pauseDuration)
            {
                _timer = 0f;
                _moving = true;
                _dir *= -1; // 反対へ横歩き
                _moveDuration = 1.5f + Random.value * 2.5f;
            }
        }
    }
}

/// <summary>池や小川のカエル（時々ピョンと跳ねる）</summary>
public class FrogHop : MonoBehaviour
{
    Vector3 _basePos;
    float _nextHop;
    bool _isHopping;
    float _hopTime;
    Vector3 _hopTarget;

    void Start()
    {
        _basePos = transform.position;
        _nextHop = 2f + Random.value * 4f;
    }

    void Update()
    {
        if (_isHopping)
        {
            _hopTime += Time.deltaTime * 3.2f;
            float t = Mathf.Clamp01(_hopTime);
            // 放物線ジャンプ
            float arc = Mathf.Sin(t * Mathf.PI) * 0.45f;
            transform.position = Vector3.Lerp(_basePos, _hopTarget, t) + Vector3.up * arc;

            if (t >= 1f)
            {
                _isHopping = false;
                _basePos = transform.position;
                _nextHop = 2.5f + Random.value * 5f;
            }
        }
        else
        {
            _nextHop -= Time.deltaTime;
            if (_nextHop <= 0f)
            {
                _isHopping = true;
                _hopTime = 0f;
                // 向きを変えて小さく跳ねる
                float ang = Random.value * 360f;
                transform.rotation = Quaternion.Euler(0f, ang, 0f);
                _hopTarget = _basePos + transform.forward * (0.35f + Random.value * 0.4f);
            }
        }
    }
}

/// <summary>小川や草むらをホバリング＆飛行するトンボ</summary>
public class DragonflyFlight : MonoBehaviour
{
    Vector3 _home;
    Vector3 _target;
    float _speed = 2.5f;
    float _hoverTimer;
    Transform _wings;

    void Start()
    {
        _home = transform.position;
        _target = _home + Random.insideUnitSphere * 4f;
        _target.y = _home.y + Random.Range(-0.5f, 0.8f);
        _wings = transform.Find("Wings");
    }

    void Update()
    {
        // 高速羽ばたき
        if (_wings != null)
            _wings.localScale = new Vector3(1f, Mathf.Sin(Time.time * 65f) * 0.8f, 1f);

        // 飛行移動
        transform.position = Vector3.MoveTowards(transform.position, _target, _speed * Time.deltaTime);
        Vector3 dir = _target - transform.position;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 6f);

        if (Vector3.Distance(transform.position, _target) < 0.2f)
        {
            _hoverTimer += Time.deltaTime;
            if (_hoverTimer > 1.2f)
            {
                _hoverTimer = 0f;
                _target = _home + Random.insideUnitSphere * 6f;
                _target.y = _home.y + Random.Range(-0.4f, 1.2f);
                _speed = 1.8f + Random.value * 2.2f;
            }
        }
    }
}
