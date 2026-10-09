using UnityEngine;

/// <summary>
/// 波打ち際をチョコチョコ歩く小さなヤドカリ。
/// 濡れた白砂を横歩きし、プレイヤーが近づくと「コロンッ！」と殻にキュッと隠れて丸くなる。
/// 危険が去るとおそるおそる目玉を出して再び歩き出す愛らしい海辺の生き物。
/// </summary>
public class AdventureHermitCrab : MonoBehaviour
{
    private Transform _shell;
    private Transform _crabBody;
    private Transform _leftEye;
    private Transform _rightEye;
    private Transform _leftClaw;
    private Transform _rightClaw;
    private Transform _player;

    private Vector3 _startPos;
    private Vector3 _moveDir;
    private float _stepTimer;
    private float _walkCycle;
    private bool _isHiding = false;
    private float _hideTimer = 0f;
    private float _peekTimer = 0f;

    const float FleeDistance = 1.8f;
    const float SafeDistance = 3.2f;

    void Start()
    {
        _startPos = transform.position;
        _moveDir = (Random.value > 0.5f ? transform.right : -transform.right);

        var player = AdventurePlayerController.InstanceOrFind();
        if (player != null) _player = player.transform;

        BuildHermitCrab();
    }

    void BuildHermitCrab()
    {
        // 1. 背中の巻貝の殻（スパイラルシェル）
        var shellGo = new GameObject("Shell");
        shellGo.transform.SetParent(transform, false);
        shellGo.transform.localPosition = new Vector3(0f, 0.055f, -0.02f);
        shellGo.transform.localRotation = Quaternion.Euler(-25f, 35f, 15f);
        shellGo.transform.localScale = Vector3.one * 0.13f;

        var mf = shellGo.AddComponent<MeshFilter>();
        mf.sharedMesh = CreateSpiralShellMesh();
        var mr = shellGo.AddComponent<MeshRenderer>();
        var shellMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        // 砂浜に溶け込むアイボリー〜薄ピンクの巻貝
        shellMat.color = new Color(0.96f, 0.88f, 0.82f);
        shellMat.SetFloat("_Smoothness", 0.45f);
        mr.sharedMaterial = shellMat;
        _shell = shellGo.transform;

        // 2. 殻から出るヤドカリ本体（体・ハサミ・目玉）
        var bodyGo = new GameObject("CrabBody");
        bodyGo.transform.SetParent(transform, false);
        bodyGo.transform.localPosition = Vector3.zero;
        _crabBody = bodyGo.transform;

        var crabMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        crabMat.color = new Color(0.95f, 0.42f, 0.28f); // 鮮やかな朱色のヤドカリ
        crabMat.SetFloat("_Smoothness", 0.55f);

        var eyeMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        eyeMat.color = new Color(0.08f, 0.08f, 0.08f); // つぶらな黒い瞳
        eyeMat.SetFloat("_Smoothness", 0.95f);

        // 頭胸部
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(_crabBody, false);
        head.transform.localScale = new Vector3(0.065f, 0.04f, 0.07f);
        head.transform.localPosition = new Vector3(0f, 0.03f, 0.035f);
        head.GetComponent<Renderer>().sharedMaterial = crabMat;
        Destroy(head.GetComponent<Collider>());

        // 左右の目玉（細い柄の先に黒い瞳）
        for (int side = -1; side <= 1; side += 2)
        {
            var eyeStem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            eyeStem.name = side < 0 ? "LeftEyeStem" : "RightEyeStem";
            eyeStem.transform.SetParent(_crabBody, false);
            eyeStem.transform.localScale = new Vector3(0.008f, 0.02f, 0.008f);
            eyeStem.transform.localPosition = new Vector3(side * 0.018f, 0.055f, 0.06f);
            eyeStem.transform.localRotation = Quaternion.Euler(20f, side * 15f, 0f);
            eyeStem.GetComponent<Renderer>().sharedMaterial = crabMat;
            Destroy(eyeStem.GetComponent<Collider>());

            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = side < 0 ? "LeftEye" : "RightEye";
            eye.transform.SetParent(eyeStem.transform, false);
            eye.transform.localScale = new Vector3(2.0f, 0.8f, 2.0f);
            eye.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            eye.GetComponent<Renderer>().sharedMaterial = eyeMat;
            Destroy(eye.GetComponent<Collider>());

            if (side < 0) _leftEye = eyeStem.transform;
            else _rightEye = eyeStem.transform;
        }

        // 左右のハサミ（左が少し小さく、右が大きい非対称の可愛いハサミ）
        for (int side = -1; side <= 1; side += 2)
        {
            var claw = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            claw.name = side < 0 ? "LeftClaw" : "RightClaw";
            claw.transform.SetParent(_crabBody, false);
            float clawScale = side > 0 ? 0.032f : 0.024f; // 右ハサミが大きい
            claw.transform.localScale = new Vector3(clawScale, clawScale * 0.7f, clawScale * 1.5f);
            claw.transform.localPosition = new Vector3(side * 0.035f, 0.025f, 0.065f);
            claw.transform.localRotation = Quaternion.Euler(0f, side * 25f, 0f);
            claw.GetComponent<Renderer>().sharedMaterial = crabMat;
            Destroy(claw.GetComponent<Collider>());

            if (side < 0) _leftClaw = claw.transform;
            else _rightClaw = claw.transform;
        }

        // 小さな歩脚（左右に3対）
        for (int side = -1; side <= 1; side += 2)
        {
            for (int legIdx = 0; legIdx < 3; legIdx++)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = $"Leg_{side}_{legIdx}";
                leg.transform.SetParent(_crabBody, false);
                leg.transform.localScale = new Vector3(0.012f, 0.01f, 0.045f);
                leg.transform.localPosition = new Vector3(side * 0.038f, 0.015f, 0.02f - legIdx * 0.025f);
                leg.transform.localRotation = Quaternion.Euler(-15f, side * (55f + legIdx * 15f), side * -25f);
                leg.GetComponent<Renderer>().sharedMaterial = crabMat;
                Destroy(leg.GetComponent<Collider>());
            }
        }
    }

    /// <summary>多重コーンによる螺旋状の美しい巻貝メッシュ</summary>
    static Mesh CreateSpiralShellMesh()
    {
        var mesh = new Mesh { name = "ProcHermitShell" };
        var verts = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();

        const int steps = 14;
        const int ringSegs = 8;

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            float radius = Mathf.Lerp(0.08f, 0.65f, Mathf.Pow(t, 0.7f));
            float z = Mathf.Lerp(1.1f, -0.2f, t);
            float spiralAngle = t * Mathf.PI * 4.5f;
            Vector3 center = new Vector3(Mathf.Cos(spiralAngle) * radius * 0.45f, Mathf.Sin(spiralAngle) * radius * 0.35f, z);

            for (int j = 0; j < ringSegs; j++)
            {
                float ang = (float)j / ringSegs * Mathf.PI * 2f;
                Vector3 ringOffset = new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, 0f);
                verts.Add(center + ringOffset);
            }
        }

        for (int i = 0; i < steps; i++)
        {
            int ringA = i * ringSegs;
            int ringB = (i + 1) * ringSegs;
            for (int j = 0; j < ringSegs; j++)
            {
                int next = (j + 1) % ringSegs;
                tris.Add(ringA + j);
                tris.Add(ringB + j);
                tris.Add(ringA + next);

                tris.Add(ringA + next);
                tris.Add(ringB + j);
                tris.Add(ringB + next);
            }
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void Update()
    {
        if (_player == null)
        {
            var p = AdventurePlayerController.InstanceOrFind();
            if (p != null) _player = p.transform;
        }

        if (_player != null)
        {
            float dist = Vector3.Distance(transform.position, _player.position);

            if (!_isHiding && dist < FleeDistance)
            {
                // プレイヤー接近！殻にコロンと隠れる
                HideInShell();
            }
            else if (_isHiding && dist > SafeDistance)
            {
                _peekTimer += Time.deltaTime;
                if (_peekTimer > 2.0f)
                {
                    // 危険が去ったのでおそるおそる殻から出てくる
                    EmergeFromShell();
                }
            }
            else if (_isHiding)
            {
                _peekTimer = 0f;
            }
        }

        if (_isHiding)
        {
            UpdateHidingAnimation();
        }
        else
        {
            UpdateWalking();
        }
    }

    void HideInShell()
    {
        _isHiding = true;
        _hideTimer = 0f;
        _peekTimer = 0f;
    }

    void EmergeFromShell()
    {
        _isHiding = false;
        _hideTimer = 0f;
    }

    void UpdateHidingAnimation()
    {
        _hideTimer += Time.deltaTime * 6.0f;
        float t = Mathf.Clamp01(_hideTimer);

        // 体全体を殻の奥へシュッと引き込む
        if (_crabBody != null)
        {
            _crabBody.localPosition = Vector3.Lerp(Vector3.zero, new Vector3(0f, 0.01f, -0.06f), t);
            _crabBody.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.45f, t);
        }

        // 殻がコロンと地面に転がる揺れ
        if (_shell != null)
        {
            float wobble = Mathf.Sin(_hideTimer * 12f) * (1f - t) * 8f;
            _shell.localRotation = Quaternion.Euler(-25f + wobble, 35f, 15f);
        }
    }

    void UpdateWalking()
    {
        // 体を元の大きさに復帰
        if (_crabBody != null)
        {
            _crabBody.localPosition = Vector3.MoveTowards(_crabBody.localPosition, Vector3.zero, Time.deltaTime * 0.3f);
            _crabBody.localScale = Vector3.MoveTowards(_crabBody.localScale, Vector3.one, Time.deltaTime * 2.5f);
        }

        _stepTimer += Time.deltaTime;
        _walkCycle += Time.deltaTime * 6.5f;

        // 横歩き（チョコチョコとした小刻みな歩行）
        float walkSpeed = 0.22f;
        transform.position += _moveDir * (walkSpeed * Time.deltaTime);

        // 歩行時の愛らしい上下バウンス＆殻の揺れ
        float bob = Mathf.Abs(Mathf.Sin(_walkCycle)) * 0.008f;
        if (_shell != null)
        {
            float shellSway = Mathf.Sin(_walkCycle * 0.5f) * 4.5f;
            _shell.localPosition = new Vector3(0f, 0.055f + bob, -0.02f);
            _shell.localRotation = Quaternion.Euler(-25f, 35f + shellSway, 15f);
        }

        // 目玉がキョロキョロ動く
        if (_leftEye != null)
            _leftEye.localRotation = Quaternion.Euler(20f + Mathf.Sin(Time.time * 3f) * 6f, -15f, 0f);
        if (_rightEye != null)
            _rightEye.localRotation = Quaternion.Euler(20f + Mathf.Cos(Time.time * 2.5f) * 6f, 15f, 0f);

        // 一定距離歩いたら向きを反転して波打ち際を往復
        if (Vector3.Distance(transform.position, _startPos) > 3.2f)
        {
            _moveDir = (_startPos - transform.position).normalized;
            // 進行方向に向き直る（横歩きなので側面を向ける）
            transform.rotation = Quaternion.LookRotation(Vector3.Cross(_moveDir, Vector3.up));
        }

        // 地形接地
        var land = Terrain.activeTerrain;
        if (land != null)
        {
            Vector3 p = transform.position;
            p.y = land.SampleHeight(p) + land.transform.position.y;
            transform.position = p;
        }
    }
}
