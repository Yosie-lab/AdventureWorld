using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class AdventurePlayerController : MonoBehaviour
{
    public float walkSpeed = 4.2f;
    public float runSpeed = 7.8f;
    public float jumpHeight = 2.2f;
    public float gravity = -24f;
    public float turnSpeed = 14f;
    public Transform cameraPivot;
    public Vector3 spawnPosition;

    public bool canDoubleJump = false;
    public float moveSpeedMultiplier = 1.0f;
    public float jumpMultiplier = 1.0f;
    public bool hasPetRadar = false;

    [Header("Glide")]
    public bool canGlide = true;
    public float glideFallSpeed = -2.4f;
    public float glideForwardSpeed = 7.2f;
    public float glideTurnSpeed = 5.5f;
    public float glideEnterDelay = 0.18f;

    bool _doubleJumpUsed = false;
    bool _gliding;
    float _airborneTime;
    Vector3 _airMomentum;

    const float Skin = 0.1f;

    CharacterController _cc;
    Animator _anim;
    Terrain _land;
    float _hop;
    bool _grounded = true;
    string _clip;

    public bool InteractPressed { get; private set; }
    public bool IsGliding => _gliding;
    public bool IsGrounded => _grounded;

    void Awake()
    {
#if UNITY_EDITOR
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#endif
        if (Keyboard.current == null)
        {
            try { InputSystem.AddDevice<Keyboard>(); }
            catch (System.Exception) { }
        }

        _cc = GetComponent<CharacterController>();
        _cc.slopeLimit = 50f;
        _cc.stepOffset = 0.45f;
        _cc.minMoveDistance = 0f;
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
        AdventureIslandBoundary.Ensure();
        AdventureBeachWavesManager.Ensure();
        AdventureCicadaAmbienceManager.Ensure();
        if (GetComponent<AdventureNikoFootsteps>() == null)
            gameObject.AddComponent<AdventureNikoFootsteps>();
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (scene != "RustAndFlat" && scene != "RustAndFloat")
            AdventureMarkerCleanup.RemoveFloatingWaterSurfaces();
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

        Vector2 input = ReadMove();
        bool running = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
        float speed = (running ? runSpeed : walkSpeed) * moveSpeedMultiplier;
        bool holdGlide = canGlide && kb != null && kb.spaceKey.isPressed;

        if (Floating() || (_cc.isGrounded && !TooSteep() && !StandingOnSeafloor()))
        {
            if (_hop < 0f)
                _hop = -2f;
            _grounded = true;
            _doubleJumpUsed = false;
            _gliding = false;
            _airborneTime = 0f;
        }
        else
        {
            _grounded = false;
            _airborneTime += Time.deltaTime;
        }

        float effectiveJumpHeight = jumpHeight * jumpMultiplier;
        if (kb != null && kb.spaceKey.wasPressedThisFrame)
        {
            if (_grounded)
            {
                _hop = Mathf.Sqrt(effectiveJumpHeight * -2f * gravity);
                _grounded = false;
                _airborneTime = 0f;
            }
            else if (canDoubleJump && !_doubleJumpUsed && !_gliding)
            {
                _hop = Mathf.Sqrt(effectiveJumpHeight * -1.8f * gravity);
                _doubleJumpUsed = true;
            }
        }

        _gliding = !_grounded && holdGlide && _airborneTime >= glideEnterDelay;

        Vector3 camForward = transform.forward;
        Vector3 camRight = transform.right;
        if (cameraPivot != null)
        {
            camForward = Vector3.ProjectOnPlane(cameraPivot.forward, Vector3.up);
            camRight = Vector3.ProjectOnPlane(cameraPivot.right, Vector3.up);
            if (camForward.sqrMagnitude < 0.001f)
                camForward = transform.forward;
            else
                camForward.Normalize();
            camRight.Normalize();
        }

        Vector3 wishWalk = Vector3.zero;
        if (input.sqrMagnitude > 0.0001f)
            wishWalk = Vector3.ClampMagnitude(camRight * input.x + camForward * input.y, 1f) * speed;

        Vector3 horizontal;
        if (_grounded)
        {
            horizontal = wishWalk;
            _airMomentum = wishWalk;
            if (wishWalk.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(wishWalk), turnSpeed * Time.deltaTime);
        }
        else if (_gliding)
        {
            float glideSpeed = glideForwardSpeed * moveSpeedMultiplier;
            if (input.y < -0.1f)
                glideSpeed *= 0.5f;
            else if (input.y > 0.1f)
                glideSpeed *= 1.12f;
            Vector3 wishGlide = camForward * glideSpeed + camRight * input.x * glideSpeed * 0.18f;
            if (_airMomentum.sqrMagnitude < 4f)
                _airMomentum = camForward * (glideSpeed * 0.8f);
            _airMomentum = Vector3.MoveTowards(_airMomentum, wishGlide, 5.5f * Time.deltaTime);
            if (_airMomentum.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(_airMomentum), glideTurnSpeed * Time.deltaTime);
            _hop = Mathf.MoveTowards(_hop, glideFallSpeed, 14f * Time.deltaTime);
            horizontal = _airMomentum;
        }
        else
        {
            _airMomentum = Vector3.MoveTowards(_airMomentum, Vector3.zero, 2.2f * Time.deltaTime);
            horizontal = _airMomentum;
            _hop += gravity * Time.deltaTime;
        }

        Vector3 motion = horizontal * Time.deltaTime;
        motion = ClipMotion(motion);
        motion.y = _hop * Time.deltaTime;
        _cc.Move(motion);
        FloatOnWater();
        KeepWalkable();
        PlayLocomotion(_grounded ? horizontal.magnitude : 0f, running && _grounded);
    }

    Vector3 ClipMotion(Vector3 motion)
    {
        var bounds = AdventureIslandBoundary.Instance;
        if (bounds != null)
            return bounds.ClipMotion(transform.position, motion);
        return motion;
    }

    void KeepWalkable()
    {
        var bounds = AdventureIslandBoundary.Instance;
        if (bounds == null)
            return;

        Vector3 pos = transform.position;
        if (bounds.IsWalkable(pos))
            return;

        Vector3 clamped = bounds.ClampWalkable(pos);
        if (!_grounded && _hop > 0f)
        {
            _cc.enabled = false;
            transform.position = new Vector3(clamped.x, pos.y, clamped.z);
            _cc.enabled = true;
        }
        else
        {
            clamped.y = SurfaceY(clamped) + Skin;
            Teleport(clamped);
        }
    }

    float WaterY()
    {
        var bounds = AdventureIslandBoundary.Instance;
        return bounds != null ? bounds.waterLevel : float.NegativeInfinity;
    }

    bool OverWater(Vector3 pos)
    {
        return GroundY(pos) < WaterY() - 0.2f;
    }

    bool Floating()
    {
        Vector3 pos = transform.position;
        return OverWater(pos) && pos.y <= WaterY() + 0.45f;
    }

    bool StandingOnSeafloor()
    {
        Vector3 pos = transform.position;
        return OverWater(pos) && pos.y < WaterY() - 0.05f;
    }

    bool TooSteep()
    {
        if (_land == null || _land.terrainData == null)
            return false;
        Vector3 origin = _land.transform.position;
        Vector3 size = _land.terrainData.size;
        float nx = Mathf.Clamp01((transform.position.x - origin.x) / size.x);
        float nz = Mathf.Clamp01((transform.position.z - origin.z) / size.z);
        return _land.terrainData.GetInterpolatedNormal(nx, nz).y < 0.68f;
    }

    void FloatOnWater()
    {
        Vector3 pos = transform.position;
        if (!OverWater(pos))
            return;
        float surface = WaterY() + Skin;
        if (pos.y > surface)
            return;

        _cc.enabled = false;
        transform.position = new Vector3(pos.x, surface, pos.z);
        _cc.enabled = true;
        _hop = Mathf.Max(_hop, 0f);
        _grounded = true;
        _gliding = false;
        _airborneTime = 0f;
    }

    float GroundY(Vector3 pos)
    {
        if (_land == null)
            return pos.y;
        return _land.SampleHeight(pos) + _land.transform.position.y;
    }

    float SurfaceY(Vector3 pos)
    {
        float landY = GroundY(pos);
        float water = WaterY();
        return landY < water ? water : landY;
    }

    Vector3 Stick(Vector3 pos)
    {
        var bounds = AdventureIslandBoundary.Instance;
        if (bounds != null)
            pos = bounds.ClampWalkable(pos);
        pos.y = SurfaceY(pos) + Skin;
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
        _gliding = false;
        _airborneTime = 0f;
        _airMomentum = Vector3.zero;
    }

    void CacheTerrains()
    {
        foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
        {
            if (terrain.name == "LandTerrain" || terrain.name == "IslandTerrain")
                _land = terrain;
            else if (terrain.name.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var col = terrain.GetComponent<TerrainCollider>();
                if (col != null)
                    col.enabled = false;
            }
        }
    }

    static Vector2 ReadMove()
    {
        Vector2 input = Vector2.zero;
        var kb = Keyboard.current;
        if (kb == null)
        {
            foreach (var device in InputSystem.devices)
            {
                if (device is Keyboard found)
                {
                    kb = found;
                    break;
                }
            }
        }
        if (kb == null)
            return Vector2.zero;
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
