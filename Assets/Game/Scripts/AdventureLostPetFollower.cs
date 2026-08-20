using UnityEngine;

public class AdventureLostPetFollower : MonoBehaviour
{
    enum Phase { Anchored, Celebrating, Following }

    const float CelebrateSeconds = 2.4f;
    const float FollowBehind = 2.3f;
    const float ArriveDistance = 0.55f;
    const float RunDistance = 6.5f;
    const float WalkSpeed = 3.6f;
    const float RunSpeed = 6.2f;

    Phase _phase = Phase.Anchored;
    Transform _target;
    AdventureNpc _npc;
    Transform _visualRoot;
    Transform _tail;
    Quaternion _tailBaseLocalRot;
    float _celebrateUntil;
    Animator _anim;
    Terrain _land;
    string _idleState;
    string _runState;
    string _currentAnim;
    Transform _legFL;
    Transform _legFR;
    Transform _legBL;
    Transform _legBR;
    float _walkPhase;

    public bool IsFollowing => _phase == Phase.Celebrating || _phase == Phase.Following;

    void Awake()
    {
        _npc = GetComponent<AdventureNpc>();
        if (_npc == null || (_npc.npcId != "dog" && _npc.npcId != "cat"))
            enabled = false;
    }

    public void BeginFollow(Transform target)
    {
        if (_phase != Phase.Anchored || target == null)
            return;

        _target = target;
        _phase = Phase.Celebrating;
        _celebrateUntil = Time.time + CelebrateSeconds;
        ResolveAnimStates();
        CacheVisual();
        PlayIdle();
    }

    void LateUpdate()
    {
        if (_phase == Phase.Celebrating)
        {
            CacheVisual();
            WagTail(32f);
            FaceTarget();
            if (Time.time >= _celebrateUntil)
                _phase = Phase.Following;
            return;
        }

        if (_phase != Phase.Following || _target == null)
            return;

        CacheVisual();
        FollowTarget();
    }

    void FollowTarget()
    {
        Vector3 self = transform.position;
        Vector3 goal = FollowGoal();
        Vector3 delta = goal - self;
        delta.y = 0f;
        float dist = delta.magnitude;

        if (dist > ArriveDistance)
        {
            float speed = dist > RunDistance ? RunSpeed : WalkSpeed;
            Vector3 move = delta.normalized * speed * Time.deltaTime;
            if (move.magnitude > dist - ArriveDistance)
                move = delta.normalized * Mathf.Max(0f, dist - ArriveDistance);

            Vector3 next = self + move;
            if (_land == null)
                _land = AdventureQuestLocations.FindLand();
            next.y = AdventureQuestLocations.GroundY(_land, next.x, next.z);
            transform.position = next;
            FaceDirection(move);
            PlayRun();
            WagTail(10f);
            AnimateLegs(true, speed);
            return;
        }

        FaceTarget();
        PlayIdle();
        WagTail(18f);
        AnimateLegs(false, 0f);
    }

    Vector3 FollowGoal()
    {
        if (_target == null)
            return transform.position;

        Vector3 back = -ForwardOnGround(_target);
        return _target.position + back * FollowBehind;
    }

    static Vector3 ForwardOnGround(Transform target)
    {
        Vector3 forward = target.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = target.rotation * Vector3.forward;
        forward.y = 0f;
        return forward.sqrMagnitude < 0.001f ? Vector3.forward : forward.normalized;
    }

    void ResolveAnimStates()
    {
        if (_npc.npcId == "dog")
        {
            _idleState = "A_CartoonAnimal-Dog_Idle";
            _runState = "A_CartoonAnimal-Dog_Run";
        }
        else
        {
            _idleState = "A_CartoonAnimal-Cat_Idle";
            _runState = "A_CartoonAnimal-Cat__Run";
        }
    }

    void CacheVisual()
    {
        if (_visualRoot == null)
        {
            string visualName = _npc.npcId == "dog" ? "DogVisual" : "CatVisual";
            _visualRoot = transform.Find(visualName);
        }

        if (_visualRoot == null)
            return;

        if (_anim == null)
        {
            _anim = _visualRoot.GetComponentInChildren<Animator>(true);
            if (_anim != null)
                _anim.enabled = true;
        }

        if (_tail == null)
        {
            _tail = FindTail(_visualRoot);
            if (_tail != null)
                _tailBaseLocalRot = _tail.localRotation;
        }

        if (_legFL == null && _visualRoot != null)
        {
            _legFL = FindChildRecursive(_visualRoot, "Leg_FL");
            _legFR = FindChildRecursive(_visualRoot, "Leg_FR");
            _legBL = FindChildRecursive(_visualRoot, "Leg_BL");
            _legBR = FindChildRecursive(_visualRoot, "Leg_BR");
        }
    }

    void AnimateLegs(bool isMoving, float speed)
    {
        if (_legFL == null || _legFR == null || _legBL == null || _legBR == null)
            return;

        if (isMoving)
        {
            float freq = Mathf.Max(speed, WalkSpeed) * 3.2f;
            _walkPhase += Time.deltaTime * freq;

            float swing1 = Mathf.Sin(_walkPhase) * 28f;  // 前左(FL) & 後右(BR)
            float swing2 = -Mathf.Sin(_walkPhase) * 28f; // 前右(FR) & 後左(BL)

            _legFL.localRotation = Quaternion.Euler(swing1, 0f, 0f);
            _legBR.localRotation = Quaternion.Euler(swing1, 0f, 0f);

            _legFR.localRotation = Quaternion.Euler(swing2, 0f, 0f);
            _legBL.localRotation = Quaternion.Euler(swing2, 0f, 0f);

            if (_visualRoot != null)
            {
                float bounce = Mathf.Abs(Mathf.Sin(_walkPhase)) * 0.08f;
                Vector3 pos = _visualRoot.localPosition;
                pos.y = bounce;
                _visualRoot.localPosition = pos;
            }
        }
        else
        {
            float t = Time.deltaTime * 10f;
            _legFL.localRotation = Quaternion.Slerp(_legFL.localRotation, Quaternion.identity, t);
            _legFR.localRotation = Quaternion.Slerp(_legFR.localRotation, Quaternion.identity, t);
            _legBL.localRotation = Quaternion.Slerp(_legBL.localRotation, Quaternion.identity, t);
            _legBR.localRotation = Quaternion.Slerp(_legBR.localRotation, Quaternion.identity, t);

            if (_visualRoot != null)
            {
                Vector3 pos = _visualRoot.localPosition;
                pos.y = Mathf.Lerp(pos.y, 0f, t);
                _visualRoot.localPosition = pos;
            }
        }
    }

    static Transform FindChildRecursive(Transform root, string childName)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName)
                return t;
        }
        return null;
    }

    static Transform FindTail(Transform root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Tail")
                return t;
        }

        return null;
    }

    void WagTail(float degrees)
    {
        if (_tail == null)
            return;

        float swing = Mathf.Sin(Time.time * 9f) * degrees;
        _tail.localRotation = _tailBaseLocalRot * Quaternion.Euler(swing, 0f, 0f);
    }

    void FaceTarget()
    {
        if (_target == null)
            return;

        Vector3 look = _target.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude < 0.001f)
            return;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(look.normalized),
            Time.deltaTime * 10f);
    }

    void FaceDirection(Vector3 move)
    {
        move.y = 0f;
        if (move.sqrMagnitude < 0.001f)
            return;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(move.normalized),
            Time.deltaTime * 12f);
    }

    void PlayIdle()
    {
        PlayAnim(_idleState);
    }

    void PlayRun()
    {
        PlayAnim(_runState);
    }

    void PlayAnim(string state)
    {
        if (_anim == null || string.IsNullOrEmpty(state) || _currentAnim == state)
            return;

        _currentAnim = state;
        _anim.CrossFadeInFixedTime(state, 0.18f);
    }
}
