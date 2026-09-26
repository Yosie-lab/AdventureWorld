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
    private Transform _player;

    private Vector3 _startPos;
    private Quaternion _startRot;
    private float _idleTimer;
    private float _headTargetAngle;
    private float _currentHeadAngle;

    private bool _isTakingOff = false;
    private float _flightTime = 0f;
    private Vector3 _flightDirection;
    private float _wingDeployFactor = 0f; // 0=背中に折りたたみ、1=左右に全開展開

    // 地上佇み時の翼の折りたたみ回転（背中に沿って後ろへ）
    private static readonly Quaternion FoldedRotRight = Quaternion.Euler(8f, -76f, -12f);
    private static readonly Quaternion FoldedRotLeft = Quaternion.Euler(8f, 76f, 12f);

    const float TakeoffDistance = 5.5f; // プレイヤーがこの距離に入ると飛び立つ

    [SerializeField] private AudioClip seagullCryClip;
    private AudioSource _audioSource;

    void Start()
    {
        _startPos = transform.position;
        _startRot = transform.rotation;

        EnsureBirdShape();

        var niko = GameObject.Find("Niko");
        if (niko != null) _player = niko.transform;

        _idleTimer = Random.Range(1.5f, 4.0f);
        _wingDeployFactor = 0f;
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
            _body.localScale = new Vector3(0.22f, 0.20f, 0.48f);
            _body.localPosition = new Vector3(0f, 0.12f, 0f);
        }

        if (_head != null)
        {
            var rend = _head.GetComponent<Renderer>();
            if (rend != null && whiteFeatherMat == null) whiteFeatherMat = rend.sharedMaterial;
            _head.localScale = new Vector3(0.15f, 0.16f, 0.19f);
            _head.localPosition = new Vector3(0f, 0.22f, 0.17f);

            var oldBeak = _head.Find("Beak");
            if (oldBeak != null)
            {
                var bRend = oldBeak.GetComponent<Renderer>();
                if (bRend != null) beakMat = bRend.sharedMaterial;
                var mf = oldBeak.GetComponent<MeshFilter>();
                if (mf != null) mf.sharedMesh = CreateConeMesh(6);
                oldBeak.localScale = new Vector3(0.045f, 0.045f, 0.16f);
                oldBeak.localPosition = new Vector3(0f, -0.01f, 0.12f);
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
            _rightWing.localPosition = new Vector3(0.09f, 0.14f, 0.02f);
            _rightWing.localScale = new Vector3(0.55f, 1f, 1f);
            _rightWing.localRotation = FoldedRotRight;
        }

        if (_leftWing != null)
        {
            var rend = _leftWing.GetComponent<Renderer>();
            if (rend != null && wingMat == null) wingMat = rend.sharedMaterial;
            var mf = _leftWing.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = leftWingMesh;
            _leftWing.localPosition = new Vector3(-0.09f, 0.14f, 0.02f);
            _leftWing.localScale = new Vector3(0.55f, 1f, 1f);
            _leftWing.localRotation = FoldedRotLeft;
        }

        // 尾羽（Tail）を追加して後ろ姿も鳥らしく
        if (_tail == null)
        {
            var tailGo = new GameObject("Tail");
            tailGo.transform.SetParent(transform, false);
            tailGo.transform.localPosition = new Vector3(0f, 0.15f, -0.24f);
            tailGo.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            tailGo.transform.localScale = new Vector3(0.16f, 0.02f, 0.22f);

            var mf = tailGo.AddComponent<MeshFilter>();
            mf.sharedMesh = CreateTailFanMesh();
            var mr = tailGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = wingMat ?? whiteFeatherMat;
            _tail = tailGo.transform;
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
            _headTargetAngle = Random.Range(-28f, 28f);
            _idleTimer = Random.Range(2.0f, 5.0f);
        }

        _currentHeadAngle = Mathf.Lerp(_currentHeadAngle, _headTargetAngle, Time.deltaTime * 6f);
        if (_head != null)
        {
            _head.localRotation = Quaternion.Euler(0f, _currentHeadAngle, Mathf.Sin(Time.time * 2f) * 4f);
        }

        // 呼吸のような微小な上下揺れ
        transform.position = _startPos + Vector3.up * (Mathf.Sin(Time.time * 2.8f) * 0.012f);

        // 地上佇み時は翼を背中に美しく折りたたむ
        _wingDeployFactor = Mathf.MoveTowards(_wingDeployFactor, 0f, Time.deltaTime * 3.5f);
        if (_rightWing != null) _rightWing.localRotation = FoldedRotRight;
        if (_leftWing != null) _leftWing.localRotation = FoldedRotLeft;
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

        // 海側・上空へ向かって飛び立つ（緩やかな放物線上昇）
        Vector3 fromCenter = (transform.position - new Vector3(512f, transform.position.y, 512f)).normalized;
        _flightDirection = (fromCenter + Vector3.up * 0.48f + transform.forward * 0.65f).normalized;

        transform.rotation = Quaternion.LookRotation(_flightDirection);
    }

    void UpdateFlight()
    {
        _flightTime += Time.deltaTime;

        // 翼を背中から左右水平へ素早くバッと展開（0.25秒で全開）
        _wingDeployFactor = Mathf.MoveTowards(_wingDeployFactor, 1f, Time.deltaTime * 4.0f);

        // 前進＆上昇飛行
        float speed = Mathf.Lerp(4.5f, 10.5f, _flightTime * 0.35f);
        transform.position += _flightDirection * (speed * Time.deltaTime);

        // 羽ばたき周期とアニメーション
        // 離陸直後（0〜3.2秒）: 力強くバサバサと羽ばたく
        // 上昇後（3.2秒〜）: 優雅な滑空（グライディング）と周期的な羽ばたき
        float flapSpeed = _flightTime < 3.2f ? 16f : 9.5f;
        float flapPhase = _flightTime * flapSpeed;

        // 上昇巡航時は滑空モード（風に乗って羽ばたきを休止）を交互に挟む
        bool isGliding = (_flightTime >= 3.2f) && (Mathf.Repeat(_flightTime, 6.0f) > 3.2f);

        float flapRoll;
        float flapPitch;
        float flapYaw;

        if (isGliding)
        {
            // 滑空時: 翼を水平に保ち、風のうねりでわずかに左右に揺れる
            float windBob = Mathf.Sin(_flightTime * 2.2f) * 4f;
            flapRoll = windBob;
            flapPitch = -3f; // 少し迎え角をつけて浮力を得る
            flapYaw = 0f;
        }
        else
        {
            // 羽ばたき時: 上下に力強くダイナミックにストローク
            float rollAmplitude = _flightTime < 3.2f ? 38f : 24f;
            flapRoll = Mathf.Sin(flapPhase) * rollAmplitude;

            // 羽のひねり（打ち下ろし時は前傾・推進力、打ち上げ時は後傾）
            float pitchAmplitude = _flightTime < 3.2f ? 14f : 8f;
            flapPitch = Mathf.Cos(flapPhase) * pitchAmplitude;

            // 前後スイング
            flapYaw = Mathf.Sin(flapPhase) * 6f;
        }

        // 飛行時の目標回転角（左右で反転）
        Quaternion activeRotRight = Quaternion.Euler(flapPitch, flapYaw, -flapRoll);
        Quaternion activeRotLeft = Quaternion.Euler(flapPitch, -flapYaw, flapRoll);

        // 折りたたみ姿勢から全開飛行姿勢へのスムーズな展開ブレンド
        if (_rightWing != null)
        {
            _rightWing.localRotation = Quaternion.Slerp(FoldedRotRight, activeRotRight, _wingDeployFactor);
        }
        if (_leftWing != null)
        {
            _leftWing.localRotation = Quaternion.Slerp(FoldedRotLeft, activeRotLeft, _wingDeployFactor);
        }

        // 胴体（Body）も羽ばたきに合わせて上下にフワフワとリアルに連動
        if (_body != null)
        {
            float bodyBob = isGliding ? 0f : Mathf.Sin(flapPhase) * 0.022f;
            _body.localPosition = new Vector3(0f, 0.12f + bodyBob, 0f);
        }

        // 尾羽（Tail）も上昇時は少し広がり羽ばたきに合わせて小さくピッチ
        if (_tail != null)
        {
            float tailPitch = 12f + (isGliding ? 0f : Mathf.Sin(flapPhase) * 8f);
            _tail.localRotation = Quaternion.Euler(tailPitch, 0f, 0f);
        }

        // 旋回しながら海の上空へ優雅に遠ざかっていく
        transform.Rotate(Vector3.up, 10f * Time.deltaTime, Space.World);

        // 十分上空へ行ったら元の位置へ戻して再着地
        if (_flightTime > 15f)
        {
            _isTakingOff = false;
            _flightTime = 0f;
            _wingDeployFactor = 0f;
            transform.position = _startPos;
            transform.rotation = _startRot;
            if (_rightWing != null) _rightWing.localRotation = FoldedRotRight;
            if (_leftWing != null) _leftWing.localRotation = FoldedRotLeft;
        }
    }
}
