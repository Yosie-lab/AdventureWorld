using UnityEngine;

/// <summary>
/// 楽園の生き物たち（カニ、カエル、トンボ）のアニメーション挙動
/// </summary>

/// <summary>砂浜のカニ（完全接地・傾斜追従・横歩きとハサミ振り）</summary>
public class CrabWander : MonoBehaviour
{
    private Vector3 _startPos;
    private float _timer;
    private float _moveDuration = 2f;
    private float _pauseDuration = 1.5f;
    private bool _moving = true;
    private int _dir = 1;
    private Transform _leftClaw;
    private Transform _rightClaw;
    private Terrain _terrain;
    private float _facingAngle;
    private const float MaxRadius = 2.2f;

    void Start()
    {
        _terrain = Terrain.activeTerrain;
        if (_terrain == null)
        {
            _terrain = FindFirstObjectByType<Terrain>();
        }

        _startPos = transform.position;
        _facingAngle = transform.eulerAngles.y;
        _leftClaw = transform.Find("LeftClaw");
        _rightClaw = transform.Find("RightClaw");
        _dir = Random.value > 0.5f ? 1 : -1;

        // 初期位置を地面にピタリと接地
        SnapToGround();
    }

    void Update()
    {
        _timer += Time.deltaTime;

        if (_moving)
        {
            // 水平方向（カニの真横）への移動ベクトル
            Quaternion flatRot = Quaternion.Euler(0f, _facingAngle, 0f);
            Vector3 rightDir = flatRot * Vector3.right;
            Vector3 moveDelta = rightDir * (_dir * 0.38f * Time.deltaTime);

            Vector3 nextPos = transform.position + moveDelta;

            // 初期位置から離れすぎた場合、または海深く（y < 5.35m）へ向かった場合は反転
            float distFromOrigin = Vector2.Distance(new Vector2(nextPos.x, nextPos.z), new Vector2(_startPos.x, _startPos.z));
            float groundAtNext = GetTerrainHeight(nextPos);

            if (distFromOrigin > MaxRadius || groundAtNext < 5.35f)
            {
                _dir *= -1;
                nextPos = transform.position + rightDir * (_dir * 0.38f * Time.deltaTime);
            }

            transform.position = nextPos;

            // 毎フレーム地面にピタリと吸着させ、浮遊を完全に防ぐ
            SnapToGround(isWalking: true);

            // 歩行中もハサミを前に構える
            if (_leftClaw != null) _leftClaw.localRotation = Quaternion.Euler(6f, -22f, 0f);
            if (_rightClaw != null) _rightClaw.localRotation = Quaternion.Euler(6f, 22f, 0f);

            if (_timer > _moveDuration)
            {
                _timer = 0f;
                _moving = false;
                _pauseDuration = 1f + Random.value * 2f;
            }
        }
        else
        {
            // 停止中も地面に正しく接地
            SnapToGround(isWalking: false);

            // 停止してハサミをチョキチョキ上下＆開閉
            float waveL = Mathf.Sin(Time.time * 8f);
            float waveR = Mathf.Sin(Time.time * 8f + 1.2f);
            if (_leftClaw != null)
                _leftClaw.localRotation = Quaternion.Euler(waveL * 16f, -22f + waveL * 8f, 5f);
            if (_rightClaw != null)
                _rightClaw.localRotation = Quaternion.Euler(waveR * 16f, 22f - waveR * 8f, -5f);

            if (_timer > _pauseDuration)
            {
                _timer = 0f;
                _moving = true;
                _dir *= -1; // 反対へ横歩き
                _moveDuration = 1.5f + Random.value * 2.5f;
            }
        }
    }

    private float GetTerrainHeight(Vector3 worldPos)
    {
        if (_terrain == null) return worldPos.y;
        return _terrain.SampleHeight(worldPos) + _terrain.transform.position.y;
    }

    private void SnapToGround(bool isWalking = false)
    {
        Vector3 pos = transform.position;
        Vector3 groundNormal = Vector3.up;
        float groundY = pos.y;

        if (_terrain != null && _terrain.terrainData != null)
        {
            Vector3 tPos = _terrain.transform.position;
            Vector3 tSize = _terrain.terrainData.size;

            groundY = _terrain.SampleHeight(pos) + tPos.y;

            float u = Mathf.Clamp01((pos.x - tPos.x) / tSize.x);
            float v = Mathf.Clamp01((pos.z - tPos.z) / tSize.z);
            groundNormal = _terrain.terrainData.GetInterpolatedNormal(u, v);
        }
        else
        {
            // Terrainが直接取れない場合のRaycastフォールバック
            if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
                groundNormal = hit.normal;
            }
        }

        // 足の厚みオフセット（0.02m）で白砂にピッタリ接地
        pos.y = groundY + 0.02f;
        transform.position = pos;

        // 地形の法線（傾斜）に沿わせつつ、歩行時の細かなカサカサ揺れを加える
        Vector3 forward = Quaternion.Euler(0f, _facingAngle, 0f) * Vector3.forward;
        Vector3 right = Vector3.Cross(groundNormal, forward).normalized;
        Vector3 correctedForward = Vector3.Cross(right, groundNormal).normalized;

        Quaternion targetRot = Quaternion.LookRotation(correctedForward, groundNormal);

        if (isWalking)
        {
            // 歩行中の小刻みなカサカサ揺れ
            float wobble = Mathf.Sin(Time.time * 22f) * 3.5f;
            targetRot *= Quaternion.Euler(0f, 0f, wobble);
        }

        transform.rotation = targetRot;
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
        if (_wings != null)
        {
            _wings.localScale = new Vector3(0.55f, 0.015f, 0.14f);
        }
    }

    void Update()
    {
        // 高速羽ばたき（スケールを破壊せず、自然な角度振動で羽ばたく）
        if (_wings != null)
            _wings.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 55f) * 22f, 0f, 0f);

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
