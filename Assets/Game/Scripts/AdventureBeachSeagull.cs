using UnityEngine;

/// <summary>
/// 砂浜・波打ち際に佇むウミネコ（カモメ）
/// 地上では翼を背中に美しく折りたたんで首をかしげ、プレイヤーが近づくと
/// 翼を左右へバッと大きく広げて力強く羽ばたき、優雅に空へ飛び立つ
/// </summary>
public class AdventureBeachSeagull : MonoBehaviour
{
    private Transform _head;
    private Transform _beak;
    private Transform _leftWing;
    private Transform _rightWing;
    private Transform _tail;
    private Transform _body;
    private Transform _leftLeg;
    private Transform _rightLeg;
    private Transform _player;

    private Vector3 _startPos;
    private Quaternion _startRot;
    private Vector3 _currentGroundPos;

    // 地上行動ステートマシン
    private enum IdleAction
    {
        LookAround,     // 首をかしげて見回し
        Pecking,        // 砂浜をつつく（エサ探し）
        WingFlutter,    // 羽根をパタパタと震わせる（毛づくろい）
        Waddling        // トコトコ歩いて向きを変える
    }
    private IdleAction _currentAction = IdleAction.LookAround;
    private float _actionTimer;
    private float _actionSubTimer;
    private float _headTargetYaw;
    private float _headCurrentYaw;
    private float _headTargetPitch;
    private float _headCurrentPitch;

    // 飛行・着地状態
    private enum BirdState
    {
        Grounded,       // 地上
        TakingOff,      // 飛び立ち上昇
        Soaring,        // 上空旋回
        Landing         // 着地アプローチ
    }
    private BirdState _state = BirdState.Grounded;
    private float _flightTime = 0f;
    private Vector3 _flightDirection;
    private Vector3 _landingTargetPos;
    private float _wingDeployFactor = 0f; // 0=背中に折りたたみ、1=左右に全開展開

    // 地上佇み時の翼の折りたたみ回転（背中に沿って後ろへ）
    private static readonly Quaternion FoldedRotRight = Quaternion.Euler(8f, -76f, -12f);
    private static readonly Quaternion FoldedRotLeft = Quaternion.Euler(8f, 76f, 12f);

    const float TakeoffDistance = 5.8f; // プレイヤーがこの距離に入ると飛び立つ

    [SerializeField] private AudioClip seagullCryClip;
    private AudioSource _audioSource;

    void Start()
    {
        _startPos = transform.position;
        _startRot = transform.rotation;
        _currentGroundPos = _startPos;

        EnsureBirdShape();
        ResolvePlayer();

        _actionTimer = Random.Range(1.2f, 3.0f);
        _wingDeployFactor = 0f;
    }

    void ResolvePlayer()
    {
        if (_player != null) return;
        var p = AdventurePlayerController.Instance ?? Object.FindAnyObjectByType<AdventurePlayerController>();
        if (p != null) _player = p.transform;
        else
        {
            var niko = GameObject.Find("Niko");
            if (niko != null) _player = niko.transform;
        }
    }

    /// <summary>
    /// 直方体ブロック（Cube）の四角い形状を、滑らかな鳥らしい流線型メッシュ・階層構造に動的アップグレード
    /// </summary>
    void EnsureBirdShape()
    {
        _head = transform.Find("Head");
        _leftWing = transform.Find("LeftWing");
        _rightWing = transform.Find("RightWing");
        _tail = transform.Find("Tail");
        _body = transform.Find("Body");

        Material whiteFeatherMat = null;
        Material beakMat = null;
        Material wingMat = null;

        if (_body != null)
        {
            var rend = _body.GetComponent<Renderer>();
            if (rend != null) whiteFeatherMat = rend.sharedMaterial;
            // 胴体を流線型（前後長め、後ろが細くなる紡錘形）に
            _body.localScale = new Vector3(0.24f, 0.22f, 0.50f);
            _body.localPosition = new Vector3(0f, 0.15f, 0f);
        }

        if (_head != null)
        {
            var rend = _head.GetComponent<Renderer>();
            if (rend != null && whiteFeatherMat == null) whiteFeatherMat = rend.sharedMaterial;
            _head.localScale = new Vector3(0.16f, 0.17f, 0.20f);
            _head.localPosition = new Vector3(0f, 0.26f, 0.18f);

            var oldBeak = _head.Find("Beak");
            if (oldBeak != null)
            {
                var bRend = oldBeak.GetComponent<Renderer>();
                if (bRend != null) beakMat = bRend.sharedMaterial;
                var mf = oldBeak.GetComponent<MeshFilter>();
                if (mf != null) mf.sharedMesh = CreateConeMesh(6);
                oldBeak.localScale = new Vector3(0.05f, 0.05f, 0.18f);
                oldBeak.localPosition = new Vector3(0f, -0.01f, 0.13f);
                oldBeak.localRotation = Quaternion.identity;
                _beak = oldBeak;
            }
        }

        // 左翼と右翼にそれぞれ外向きの翼型メッシュを適用
        var rightWingMesh = CreateBirdWingMesh(true);
        var leftWingMesh = CreateBirdWingMesh(false);

        if (_rightWing != null)
        {
            var rend = _rightWing.GetComponent<Renderer>();
            if (rend != null) wingMat = rend.sharedMaterial;
            var mf = _rightWing.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = rightWingMesh;
            _rightWing.localPosition = new Vector3(0.10f, 0.16f, 0.02f);
            _rightWing.localScale = new Vector3(0.55f, 1f, 1f);
            _rightWing.localRotation = FoldedRotRight;
        }

        if (_leftWing != null)
        {
            var rend = _leftWing.GetComponent<Renderer>();
            if (rend != null && wingMat == null) wingMat = rend.sharedMaterial;
            var mf = _leftWing.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = leftWingMesh;
            _leftWing.localPosition = new Vector3(-0.10f, 0.16f, 0.02f);
            _leftWing.localScale = new Vector3(0.55f, 1f, 1f);
            _leftWing.localRotation = FoldedRotLeft;
        }

        // 尾羽（Tail）を追加して後ろ姿も鳥らしく
        if (_tail == null)
        {
            var tailGo = new GameObject("Tail");
            tailGo.transform.SetParent(transform, false);
            tailGo.transform.localPosition = new Vector3(0f, 0.18f, -0.26f);
            tailGo.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            tailGo.transform.localScale = new Vector3(0.18f, 0.02f, 0.24f);

            var mf = tailGo.AddComponent<MeshFilter>();
            mf.sharedMesh = CreateTailFanMesh();
            var mr = tailGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = wingMat ?? whiteFeatherMat;
            _tail = tailGo.transform;
        }

        // オレンジ色の小さな脚（Legs）を追加して砂浜の上にしっかり立たせる
        if (_leftLeg == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var legMat = beakMat ?? new Material(shader) { color = new Color(0.96f, 0.65f, 0.10f) };

            for (int side = -1; side <= 1; side += 2)
            {
                var legGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                legGo.name = side < 0 ? "LeftLeg" : "RightLeg";
                legGo.transform.SetParent(transform, false);
                legGo.transform.localPosition = new Vector3(side * 0.06f, 0.06f, -0.02f);
                legGo.transform.localScale = new Vector3(0.022f, 0.06f, 0.022f);
                legGo.GetComponent<Renderer>().sharedMaterial = legMat;
                Destroy(legGo.GetComponent<Collider>());
                if (side < 0) _leftLeg = legGo.transform;
                else _rightLeg = legGo.transform;
            }
        }
    }

    /// <summary>先端が尖ったクチバシ用コーンメッシュ</summary>
    static Mesh CreateConeMesh(int subdivisions = 8)
    {
        var mesh = new Mesh { name = "ProcBeakCone" };
        var verts = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();

        Vector3 tip = new Vector3(0f, 0f, 1f);
        verts.Add(tip);

        for (int i = 0; i < subdivisions; i++)
        {
            float angle = (i / (float)subdivisions) * Mathf.PI * 2f;
            verts.Add(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
        }

        for (int i = 0; i < subdivisions; i++)
        {
            int next = (i + 1) % subdivisions;
            tris.Add(0);
            tris.Add(1 + i);
            tris.Add(1 + next);
        }
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// カモメの薄く流線型の翼メッシュ
    /// 原点(0,0,0)が胴体付け根。isRight=trueなら+X、falseなら-Xへ伸びる
    /// </summary>
    static Mesh CreateBirdWingMesh(bool isRight)
    {
        var mesh = new Mesh { name = isRight ? "ProcBirdWingR" : "ProcBirdWingL" };
        float dir = isRight ? 1f : -1f;

        var verts = new Vector3[]
        {
            // 上面 (0〜4): 付け根前, 付け根後, 中間前, 中間後, 翼端
            new Vector3(0f, 0.025f, 0.12f),
            new Vector3(0f, 0.010f, -0.15f),
            new Vector3(dir * 0.45f, 0.018f, 0.08f),
            new Vector3(dir * 0.48f, 0.007f, -0.11f),
            new Vector3(dir * 1.0f, 0.003f, -0.03f),

            // 下面 (5〜9)
            new Vector3(0f, -0.018f, 0.12f),
            new Vector3(0f, -0.007f, -0.15f),
            new Vector3(dir * 0.45f, -0.010f, 0.08f),
            new Vector3(dir * 0.48f, -0.004f, -0.11f),
            new Vector3(dir * 1.0f, -0.002f, -0.03f)
        };

        var tris = new System.Collections.Generic.List<int>();

        void AddQuad(int a, int b, int c, int d, bool flip)
        {
            if (flip)
            {
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
            else
            {
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
        }

        void AddTri(int a, int b, int c, bool flip)
        {
            if (flip)
            {
                tris.Add(a); tris.Add(c); tris.Add(b);
            }
            else
            {
                tris.Add(a); tris.Add(b); tris.Add(c);
            }
        }

        bool flipTop = !isRight;
        AddQuad(0, 2, 1, 3, flipTop);
        AddTri(2, 4, 3, flipTop);

        bool flipBottom = isRight;
        AddQuad(5, 7, 6, 8, flipBottom);
        AddTri(7, 9, 8, flipBottom);

        // 前縁
        AddQuad(0, 5, 2, 7, !isRight);
        AddQuad(2, 7, 4, 9, !isRight);

        // 後縁
        AddQuad(1, 3, 6, 8, isRight);
        AddQuad(3, 4, 8, 9, isRight);

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>後ろに扇状に広がる尾羽メッシュ</summary>
    static Mesh CreateTailFanMesh()
    {
        var mesh = new Mesh { name = "ProcBirdTail" };
        var verts = new Vector3[]
        {
            new Vector3(-0.06f, 0.01f, 0.05f),
            new Vector3(0.06f, 0.01f, 0.05f),
            new Vector3(-0.16f, 0.005f, -0.22f),
            new Vector3(0.16f, 0.005f, -0.22f),

            new Vector3(-0.06f, -0.01f, 0.05f),
            new Vector3(0.06f, -0.01f, 0.05f),
            new Vector3(-0.16f, -0.005f, -0.22f),
            new Vector3(0.16f, -0.005f, -0.22f)
        };
        var tris = new int[]
        {
            0, 1, 2,  1, 3, 2, // 上面
            4, 6, 5,  5, 6, 7  // 下面
        };
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void Update()
    {
        ResolvePlayer();

        switch (_state)
        {
            case BirdState.Grounded:
                UpdateGrounded();
                break;
            case BirdState.TakingOff:
            case BirdState.Soaring:
            case BirdState.Landing:
                UpdateFlightLifecycle();
                break;
        }
    }

    #region Grounded Behaviors (Lively Idle Animation)
    void UpdateGrounded()
    {
        // プレイヤー接近判定（5.8m以内で即座に飛び立つ）
        if (_player != null)
        {
            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist < TakeoffDistance)
            {
                TakeOff();
                return;
            }

            // 接近警戒（5.8m〜8.5m）：プレイヤーの方向をキョロリと見て警戒
            if (dist < 8.5f)
            {
                Vector3 toPlayer = (_player.position - transform.position).normalized;
                float angleToPlayer = Vector3.SignedAngle(transform.forward, toPlayer, Vector3.up);
                _headTargetYaw = Mathf.Clamp(angleToPlayer, -55f, 55f);
                _headTargetPitch = -5f;
            }
        }

        _actionTimer -= Time.deltaTime;
        _actionSubTimer += Time.deltaTime;

        if (_actionTimer <= 0f)
        {
            // 次の仕草へ切り替え
            PickNextIdleAction();
        }

        ExecuteCurrentIdleAction();
    }

    void PickNextIdleAction()
    {
        float r = Random.value;
        if (r < 0.38f)
        {
            _currentAction = IdleAction.LookAround;
            _headTargetYaw = Random.Range(-45f, 45f);
            _headTargetPitch = Random.Range(-10f, 15f);
            _actionTimer = Random.Range(1.8f, 3.8f);
        }
        else if (r < 0.68f)
        {
            _currentAction = IdleAction.Pecking; // 砂浜をつつく！
            _actionTimer = Random.Range(1.6f, 3.2f);
            _headTargetYaw = Random.Range(-15f, 15f);
        }
        else if (r < 0.85f)
        {
            _currentAction = IdleAction.WingFlutter; // 羽ばたき毛づくろい！
            _actionTimer = Random.Range(1.2f, 2.2f);
        }
        else
        {
            _currentAction = IdleAction.Waddling; // トコトコ小走り！
            _actionTimer = Random.Range(1.4f, 2.6f);
            // 向きを少し変える
            transform.Rotate(Vector3.up, Random.Range(-40f, 40f), Space.World);
        }
        _actionSubTimer = 0f;
    }

    void ExecuteCurrentIdleAction()
    {
        float dt = Time.deltaTime;

        switch (_currentAction)
        {
            case IdleAction.LookAround:
                // 首を滑らかに回して見回す
                _headCurrentYaw = Mathf.Lerp(_headCurrentYaw, _headTargetYaw, dt * 7f);
                _headCurrentPitch = Mathf.Lerp(_headCurrentPitch, _headTargetPitch, dt * 7f);
                ApplyHeadAngles(_headCurrentYaw, _headCurrentPitch, Mathf.Sin(Time.time * 2f) * 3f);

                // 呼吸の微小揺れ
                ApplyBreathBob(0.008f);
                _wingDeployFactor = Mathf.MoveTowards(_wingDeployFactor, 0f, dt * 4f);
                SetWingsFolded();
                break;

            case IdleAction.Pecking:
                // 砂浜をつつく仕草（頭を地面に下げてチョンチョンと2〜3回突く）
                float peckFreq = 9.0f;
                float peckCycle = Mathf.Sin(_actionSubTimer * peckFreq);
                float peckDown = Mathf.Clamp01(peckCycle) * 38f + 18f; // 下向き30〜56度

                _headCurrentYaw = Mathf.Lerp(_headCurrentYaw, _headTargetYaw, dt * 6f);
                ApplyHeadAngles(_headCurrentYaw, peckDown, 0f);

                // つつく瞬間に尾羽がピクッと上がる
                if (_tail != null)
                {
                    float tailLift = Mathf.Clamp01(peckCycle) * 16f + 12f;
                    _tail.localRotation = Quaternion.Euler(tailLift, 0f, 0f);
                }

                // 胴体もわずかに前傾
                if (_body != null)
                {
                    float bodyPitch = Mathf.Clamp01(peckCycle) * 8f;
                    _body.localRotation = Quaternion.Euler(bodyPitch, 0f, 0f);
                }
                _wingDeployFactor = Mathf.MoveTowards(_wingDeployFactor, 0f, dt * 4f);
                SetWingsFolded();
                break;

            case IdleAction.WingFlutter:
                // 翼を背中から少し浮かせてパタパタパタッと高速に震わせる（毛づくろい）
                _wingDeployFactor = Mathf.MoveTowards(_wingDeployFactor, 0.28f, dt * 5f);
                float flutterAngle = Mathf.Sin(_actionSubTimer * 26f) * 18f;

                Quaternion flutterR = FoldedRotRight * Quaternion.Euler(0f, 0f, -flutterAngle);
                Quaternion flutterL = FoldedRotLeft * Quaternion.Euler(0f, 0f, flutterAngle);
                if (_rightWing != null) _rightWing.localRotation = flutterR;
                if (_leftWing != null) _leftWing.localRotation = flutterL;

                // 頭を少し横に向けて羽毛を見る
                _headCurrentYaw = Mathf.Lerp(_headCurrentYaw, 25f, dt * 6f);
                _headCurrentPitch = Mathf.Lerp(_headCurrentPitch, 15f, dt * 6f);
                ApplyHeadAngles(_headCurrentYaw, _headCurrentPitch, flutterAngle * 0.2f);
                break;

            case IdleAction.Waddling:
                // トコトコと波打ち際を小刻みステップで歩く
                float walkSpeed = 0.45f;
                transform.position += transform.forward * (walkSpeed * dt);
                KeepOnTerrainSurface();

                // 左右の足踏み・お尻フリフリ横揺れ（Roll）
                float waddleRoll = Mathf.Sin(_actionSubTimer * 10f) * 5.5f;
                float waddlePitch = Mathf.Abs(Mathf.Sin(_actionSubTimer * 10f)) * 3f;
                if (_body != null)
                {
                    _body.localRotation = Quaternion.Euler(waddlePitch, 0f, waddleRoll);
                }

                // 首を前後にピョコピョコ振る（鳩・カモメ特有の歩行ヘッドボブ）
                float headBob = Mathf.Sin(_actionSubTimer * 10f) * 12f;
                ApplyHeadAngles(0f, headBob, -waddleRoll * 0.5f);

                _wingDeployFactor = Mathf.MoveTowards(_wingDeployFactor, 0f, dt * 4f);
                SetWingsFolded();
                break;
        }
    }

    void ApplyHeadAngles(float yaw, float pitch, float roll)
    {
        if (_head != null)
        {
            _head.localRotation = Quaternion.Euler(pitch, yaw, roll);
        }
    }

    void ApplyBreathBob(float amplitude)
    {
        float bob = Mathf.Sin(Time.time * 2.8f) * amplitude;
        transform.position = _currentGroundPos + Vector3.up * bob;
    }

    void SetWingsFolded()
    {
        if (_rightWing != null) _rightWing.localRotation = FoldedRotRight;
        if (_leftWing != null) _leftWing.localRotation = FoldedRotLeft;
    }

    void KeepOnTerrainSurface()
    {
        var land = Terrain.activeTerrain;
        if (land != null)
        {
            float h = land.SampleHeight(transform.position) + land.transform.position.y;
            Vector3 pos = transform.position;
            pos.y = h;
            transform.position = pos;
            _currentGroundPos = pos;
        }
    }
    #endregion

    #region Flight & Landing Lifecycle
    public void SetCryClip(AudioClip clip)
    {
        seagullCryClip = clip;
    }

    /// <summary>プレイヤー接近時に力強く大空へ飛び立つ</summary>
    public void TakeOff()
    {
        if (_state != BirdState.Grounded) return;

        _state = BirdState.TakingOff;
        _flightTime = 0f;
        _wingDeployFactor = 0f;

        // 飛び立つ瞬間にウミネコの鳴き声を再生
        if (seagullCryClip != null)
        {
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 1.0f; // 3D音響
                _audioSource.minDistance = 3f;
                _audioSource.maxDistance = 28f;
                _audioSource.rolloffMode = AudioRolloffMode.Linear;
            }
            _audioSource.pitch = Random.Range(0.96f, 1.06f);
            _audioSource.PlayOneShot(seagullCryClip, 0.75f);
        }

        // 海・大空側へ向かって飛び立つ（斜め前方＋上方）
        Vector3 fromCenter = (transform.position - new Vector3(512f, transform.position.y, 512f)).normalized;
        _flightDirection = (fromCenter * 0.45f + Vector3.up * 0.55f + transform.forward * 0.60f).normalized;
        transform.rotation = Quaternion.LookRotation(_flightDirection);

        // 次の再着地点（周囲の波打ち際 ±10m）をあらかじめ決定
        Vector2 landOffset = Random.insideUnitCircle * 8f;
        _landingTargetPos = new Vector3(_startPos.x + landOffset.x, _startPos.y, _startPos.z + landOffset.y);
        var land = Terrain.activeTerrain;
        if (land != null)
        {
            _landingTargetPos.y = land.SampleHeight(_landingTargetPos) + land.transform.position.y;
        }
    }

    void UpdateFlightLifecycle()
    {
        _flightTime += Time.deltaTime;
        float dt = Time.deltaTime;

        // 翼を背中から左右水平へ素早くバッと全開展開（0.2秒で展開）
        _wingDeployFactor = Mathf.MoveTowards(_wingDeployFactor, 1f, dt * 5.0f);

        if (_flightTime < 3.5f)
        {
            _state = BirdState.TakingOff;
            // 離陸直後：力強くバサバサと羽ばたきながら急上昇
            float speed = Mathf.Lerp(4.5f, 9.5f, _flightTime / 3.5f);
            transform.position += _flightDirection * (speed * dt);
            AnimateWings(isGliding: false, flapSpeed: 16.5f, rollAmp: 40f, pitchAmp: 14f);
        }
        else if (_flightTime < 11.0f)
        {
            _state = BirdState.Soaring;
            // 上空巡航：海風に乗って旋回＆滑空（グライディング）
            float soarTime = _flightTime - 3.5f;
            transform.Rotate(Vector3.up, 16f * dt, Space.World);
            transform.position += transform.forward * (8.5f * dt) + Vector3.up * (Mathf.Sin(soarTime * 1.5f) * 0.8f * dt);

            bool isGliding = Mathf.Repeat(soarTime, 5.0f) > 2.2f;
            AnimateWings(isGliding: isGliding, flapSpeed: 10.0f, rollAmp: 25f, pitchAmp: 8f);
        }
        else
        {
            _state = BirdState.Landing;
            // 再着地アプローチ：着地点に向かって翼を広げエアブレーキをかけながら舞い降りる
            Vector3 toTarget = _landingTargetPos - transform.position;
            float distToTarget = toTarget.magnitude;

            if (distToTarget > 0.4f && _flightTime < 16.0f)
            {
                Vector3 landDir = toTarget.normalized;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(new Vector3(landDir.x, 0f, landDir.z)), dt * 3.5f);
                float landSpeed = Mathf.Clamp(distToTarget * 0.85f, 1.8f, 6.0f);
                transform.position += landDir * (landSpeed * dt);

                // ブレーキ羽ばたき
                AnimateWings(isGliding: false, flapSpeed: 12.0f, rollAmp: 28f, pitchAmp: 16f);
            }
            else
            {
                // タッチダウン（着地完了）！
                FinishLanding();
            }
        }
    }

    void FinishLanding()
    {
        _state = BirdState.Grounded;
        _flightTime = 0f;
        _wingDeployFactor = 0f;
        transform.position = _landingTargetPos;
        _currentGroundPos = _landingTargetPos;
        KeepOnTerrainSurface();
        SetWingsFolded();

        if (_body != null) _body.localRotation = Quaternion.identity;
        if (_tail != null) _tail.localRotation = Quaternion.Euler(14f, 0f, 0f);

        _currentAction = IdleAction.LookAround;
        _actionTimer = Random.Range(2.0f, 4.0f);
        _actionSubTimer = 0f;
    }

    void AnimateWings(bool isGliding, float flapSpeed, float rollAmp, float pitchAmp)
    {
        float flapPhase = _flightTime * flapSpeed;

        float flapRoll = isGliding ? (Mathf.Sin(_flightTime * 2.2f) * 4f) : (Mathf.Sin(flapPhase) * rollAmp);
        float flapPitch = isGliding ? -3f : (Mathf.Cos(flapPhase) * pitchAmp);
        float flapYaw = isGliding ? 0f : (Mathf.Sin(flapPhase) * 6f);

        Quaternion activeRotRight = Quaternion.Euler(flapPitch, flapYaw, -flapRoll);
        Quaternion activeRotLeft = Quaternion.Euler(flapPitch, -flapYaw, flapRoll);

        if (_rightWing != null)
            _rightWing.localRotation = Quaternion.Slerp(FoldedRotRight, activeRotRight, _wingDeployFactor);
        if (_leftWing != null)
            _leftWing.localRotation = Quaternion.Slerp(FoldedRotLeft, activeRotLeft, _wingDeployFactor);

        if (_body != null)
        {
            float bodyBob = isGliding ? 0f : Mathf.Sin(flapPhase) * 0.024f;
            _body.localPosition = new Vector3(0f, 0.15f + bodyBob, 0f);
        }

        if (_tail != null)
        {
            float tailPitch = 12f + (isGliding ? 0f : Mathf.Sin(flapPhase) * 8f);
            _tail.localRotation = Quaternion.Euler(tailPitch, 0f, 0f);
        }
    }
    #endregion
}
