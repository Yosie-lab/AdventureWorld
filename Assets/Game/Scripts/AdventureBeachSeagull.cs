using UnityEngine;

/// <summary>
/// 砂浜・波打ち際に佇むウミネコ（カモメ）
/// 首をかしげたり羽づくろいしたりして過ごし、プレイヤーが近づくと優雅に空へ飛び立つ
/// </summary>
public class AdventureBeachSeagull : MonoBehaviour
{
    private Transform _head;
    private Transform _beak;
    private Transform _leftWing;
    private Transform _rightWing;
    private Transform _tail;
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

        EnsureBirdShape();

        var niko = GameObject.Find("Niko");
        if (niko != null) _player = niko.transform;

        _idleTimer = Random.Range(1.5f, 4.0f);
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

        // 既存のBody取得
        var body = transform.Find("Body");
        Material whiteFeatherMat = null;
        Material beakMat = null;
        Material wingMat = null;

        if (body != null)
        {
            var rend = body.GetComponent<Renderer>();
            if (rend != null) whiteFeatherMat = rend.sharedMaterial;
            // 胴体を流線型（前後長め、後ろが細くなる紡錘形）に
            body.localScale = new Vector3(0.22f, 0.20f, 0.48f);
            body.localPosition = new Vector3(0f, 0.12f, 0f);
        }

        if (_head != null)
        {
            var rend = _head.GetComponent<Renderer>();
            if (rend != null && whiteFeatherMat == null) whiteFeatherMat = rend.sharedMaterial;
            // 頭部を自然な丸みに
            _head.localScale = new Vector3(0.15f, 0.16f, 0.19f);
            _head.localPosition = new Vector3(0f, 0.22f, 0.17f);

            var oldBeak = _head.Find("Beak");
            if (oldBeak != null)
            {
                var bRend = oldBeak.GetComponent<Renderer>();
                if (bRend != null) beakMat = bRend.sharedMaterial;
                // クチバシを前が細く尖ったピラミッド/コーン風メッシュに差し替え
                var mf = oldBeak.GetComponent<MeshFilter>();
                if (mf != null) mf.sharedMesh = CreateConeMesh(6);
                oldBeak.localScale = new Vector3(0.045f, 0.045f, 0.16f);
                oldBeak.localPosition = new Vector3(0f, -0.01f, 0.12f);
                oldBeak.localRotation = Quaternion.identity;
                _beak = oldBeak;
            }
        }

        // 翼をブロックから薄く先細りした翼メッシュに差し替え
        var wingMesh = CreateBirdWingMesh();

        if (_leftWing != null)
        {
            var rend = _leftWing.GetComponent<Renderer>();
            if (rend != null) wingMat = rend.sharedMaterial;
            var mf = _leftWing.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = wingMesh;
            _leftWing.localScale = new Vector3(0.55f, 0.02f, 0.26f);
            _leftWing.localPosition = new Vector3(-0.11f, 0.14f, 0.02f);
            _leftWing.localRotation = Quaternion.Euler(0f, 0f, 10f);
        }

        if (_rightWing != null)
        {
            var mf = _rightWing.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = wingMesh;
            _rightWing.localScale = new Vector3(-0.55f, 0.02f, 0.26f); // 左右反転
            _rightWing.localPosition = new Vector3(0.11f, 0.14f, 0.02f);
            _rightWing.localRotation = Quaternion.Euler(0f, 0f, -10f);
        }

        // 尾羽（Tail）を追加して後ろ姿も鳥らしく
        if (_tail == null)
        {
            var tailGo = new GameObject("Tail");
            tailGo.transform.SetParent(transform, false);
            tailGo.transform.localPosition = new Vector3(0f, 0.15f, -0.26f);
            tailGo.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
            tailGo.transform.localScale = new Vector3(0.14f, 0.015f, 0.22f);

            var mf = tailGo.AddComponent<MeshFilter>();
            mf.sharedMesh = CreateBirdWingMesh();
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
        verts.Add(tip); // 0: tip

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

    /// <summary>鳥の翼（付け根が厚く先端に向かって細く伸びる翼型メッシュ）</summary>
    static Mesh CreateBirdWingMesh()
    {
        var mesh = new Mesh { name = "ProcBirdWing" };
        var verts = new Vector3[]
        {
            // 付け根前、付け根後、翼中間前、翼中間後、翼端
            new Vector3(0f, 0.5f, 0.35f),
            new Vector3(0f, -0.5f, -0.45f),
            new Vector3(0.55f, 0.3f, 0.2f),
            new Vector3(0.55f, -0.3f, -0.28f),
            new Vector3(1.0f, 0f, -0.05f),
            // 底面側
            new Vector3(0f, -0.5f, 0.35f),
            new Vector3(0f, 0.5f, -0.45f),
            new Vector3(0.55f, -0.3f, 0.2f),
            new Vector3(0.55f, 0.3f, -0.28f),
            new Vector3(1.0f, 0f, -0.05f)
        };

        var tris = new int[]
        {
            // 上面
            0, 2, 1,   1, 2, 3,
            2, 4, 3,
            // 裏面
            5, 6, 7,   6, 8, 7,
            7, 8, 9
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

        // 佇み時の翼の折りたたみ
        if (_leftWing != null) _leftWing.localRotation = Quaternion.Euler(0f, 0f, 14f);
        if (_rightWing != null) _rightWing.localRotation = Quaternion.Euler(0f, 0f, -14f);
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

        // 海側・上空へ向かって飛び立つ（緩やかな放物線上昇）
        Vector3 fromCenter = (transform.position - new Vector3(512f, transform.position.y, 512f)).normalized;
        _flightDirection = (fromCenter + Vector3.up * 0.45f + transform.forward * 0.65f).normalized;

        transform.rotation = Quaternion.LookRotation(_flightDirection);
    }

    void UpdateFlight()
    {
        _flightTime += Time.deltaTime;

        // 上昇・前進飛行
        float speed = Mathf.Lerp(4.0f, 9.5f, _flightTime * 0.35f);
        transform.position += _flightDirection * (speed * Time.deltaTime);

        // 翼の羽ばたき（離陸直後は力強くパタパタ、上昇後は優雅にグライド滑空）
        float flapAngle;
        if (_flightTime < 3.5f)
        {
            flapAngle = Mathf.Sin(_flightTime * 16f) * 32f;
        }
        else
        {
            // 上空ではゆったりとした滑空（グライディング）＋時折の緩やかな羽ばたき
            float glideWobble = Mathf.Sin(_flightTime * 2.2f) * 5f;
            float slowFlap = Mathf.Sin(_flightTime * 5f) * 12f;
            flapAngle = glideWobble + slowFlap;
        }

        if (_leftWing != null) _leftWing.localRotation = Quaternion.Euler(0f, 0f, flapAngle);
        if (_rightWing != null) _rightWing.localRotation = Quaternion.Euler(0f, 0f, -flapAngle);

        // 旋回しながら空へ消えていく
        transform.Rotate(Vector3.up, 12f * Time.deltaTime, Space.World);

        // 十分上空へ行ったら徐々にフェードアウト・再配置
        if (_flightTime > 14f)
        {
            // 元の位置に戻して再着地
            _isTakingOff = false;
            transform.position = _startPos;
            transform.rotation = _startRot;
            if (_leftWing != null) _leftWing.localRotation = Quaternion.Euler(0f, 0f, 14f);
            if (_rightWing != null) _rightWing.localRotation = Quaternion.Euler(0f, 0f, -14f);
        }
    }
}
