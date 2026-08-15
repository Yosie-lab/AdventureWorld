using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class AdventurePlayerController : MonoBehaviour
{
    public float walkSpeed = 4.2f;
    public float runSpeed = 7.8f;
    public float jumpHeight = 1.15f;
    public float gravity = -28f;
    public float turnSpeed = 14f;
    public Transform cameraPivot;
    public Vector3 spawnPosition;

    const float Skin = 0.1f;

    CharacterController _cc;
    Animator _anim;
    Terrain _land;
    float _hop;
    bool _grounded = true;
    string _clip;

    public bool InteractPressed { get; private set; }

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _anim = GetComponentInChildren<Animator>();
        if (_anim != null)
        {
            _anim.applyRootMotion = false;
            _anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.quality = SkinQuality.Bone4;
            smr.updateWhenOffscreen = true;
            var b = smr.localBounds;
            if (b.extents.x < 0.04f || b.extents.y < 0.04f || b.extents.z < 0.04f)
                smr.localBounds = new Bounds(Vector3.zero, Vector3.one * 0.12f);
        }
        CacheTerrains();
    }

    void Start()
    {
        CacheTerrains();
        if (spawnPosition == Vector3.zero)
            spawnPosition = transform.position;
        spawnPosition = Stick(spawnPosition);
        Teleport(spawnPosition);
    }

    void Update()
    {
        var kb = Keyboard.current;
        InteractPressed = kb != null && kb.eKey.wasPressedThisFrame;
        if (kb != null && kb.rKey.wasPressedThisFrame)
        {
            Teleport(spawnPosition);
            return;
        }

        Vector2 input = ReadMove(kb);
        bool running = kb != null && kb.leftShiftKey.isPressed;
        float speed = running ? runSpeed : walkSpeed;

        Vector3 planar = Vector3.zero;
        if (input.sqrMagnitude > 0.0001f)
        {
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            if (cameraPivot != null)
            {
                forward = Vector3.ProjectOnPlane(cameraPivot.forward, Vector3.up);
                right = Vector3.ProjectOnPlane(cameraPivot.right, Vector3.up);
                if (forward.sqrMagnitude < 0.001f)
                    forward = transform.forward;
                else
                    forward.Normalize();
                right.Normalize();
            }
            planar = (right * input.x + forward * input.y);
            if (planar.sqrMagnitude > 0.0001f)
            {
                planar.Normalize();
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(planar), turnSpeed * Time.deltaTime);
            }
        }

        Vector3 motion = planar * speed * Time.deltaTime;
        if (!CanStand(transform.position + new Vector3(motion.x, 0f, motion.z)))
            motion.x = motion.z = 0f;

        if (_cc.isGrounded)
        {
            if (_hop < 0f)
                _hop = -2f;
            _grounded = true;
        }
        else
            _grounded = false;

        if (_grounded && kb != null && kb.spaceKey.wasPressedThisFrame)
            _hop = Mathf.Sqrt(jumpHeight * -2f * gravity);

        _hop += gravity * Time.deltaTime;
        motion.y = _hop * Time.deltaTime;
        _cc.Move(motion);
        PlayLocomotion(planar.magnitude * speed, running);
    }

    bool CanStand(Vector3 pos)
    {
        if (_land == null)
            return true;
        Vector3 origin = _land.transform.position;
        Vector3 size = _land.terrainData.size;
        const float edge = 1.5f;
        return pos.x > origin.x + edge
            && pos.x < origin.x + size.x - edge
            && pos.z > origin.z + edge
            && pos.z < origin.z + size.z - edge;
    }

    float GroundY(Vector3 pos)
    {
        if (_land == null)
            return pos.y;
        return _land.SampleHeight(pos) + _land.transform.position.y;
    }

    Vector3 Stick(Vector3 pos)
    {
        pos.y = GroundY(pos) + Skin;
        return pos;
    }

    void Teleport(Vector3 pos)
    {
        pos = Stick(pos);
        _cc.enabled = false;
        transform.position = pos;
        _cc.enabled = true;
        _hop = 0f;
        _grounded = true;
    }

    void CacheTerrains()
    {
        foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
        {
            if (terrain.name == "LandTerrain")
                _land = terrain;
            else if (terrain.name.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var col = terrain.GetComponent<TerrainCollider>();
                if (col != null)
                    col.enabled = false;
            }
        }
        if (_land == null)
            _land = Terrain.activeTerrain;
    }

    static Vector2 ReadMove(Keyboard kb)
    {
        if (kb == null)
            return Vector2.zero;
        Vector2 input = Vector2.zero;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
        return Vector2.ClampMagnitude(input, 1f);
    }

    void PlayLocomotion(float speed, bool running)
    {
        if (_anim == null)
            return;
        string next = speed < 0.2f ? "NikoIdle" : (running ? "NikoRuns" : "NikoWalks");
        if (next != _clip)
        {
            _clip = next;
            _anim.CrossFadeInFixedTime(next, 0.15f);
        }
        if (next == "NikoIdle")
            _anim.speed = 1f;
        else
        {
            float reference = running ? 5.4f : 2.4f;
            _anim.speed = Mathf.Clamp(speed / reference, 0.9f, 1.7f);
        }
    }
}
