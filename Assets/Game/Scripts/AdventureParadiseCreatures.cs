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

/// <summary>
/// 小川や草むらをホバリング＆飛行する美しいリアルなトンボ。
/// 歯ブラシのような単一板の不自然な形状を解消し、
/// 頭部・大きな複眼・引き締まった胸部・細く伸びる尾・4枚の透明な翅による本物の昆虫造形と
/// 前後翅の位相差羽ばたき・繊細なホバリング挙動を実現。
/// </summary>
public class DragonflyFlight : MonoBehaviour
{
    Vector3 _home;
    Vector3 _target;
    float _speed = 2.5f;
    float _hoverTimer;

    Transform _leftForeWing;
    Transform _rightForeWing;
    Transform _leftHindWing;
    Transform _rightHindWing;

    static Material _sharedWingMat;
    static Material _sharedEyeMat;

    void Awake()
    {
        RebuildDragonflyVisuals();
    }

    void Start()
    {
        _home = transform.position;
        _target = _home + Random.insideUnitSphere * 4f;
        _target.y = _home.y + Random.Range(-0.4f, 0.9f);
    }

    void Update()
    {
        // 昆虫特有の超高速・前後位相差羽ばたき
        float t = Time.time * 60f;
        float flapFore = Mathf.Sin(t) * 26f;
        float flapHind = Mathf.Sin(t + 1.05f) * 23f; // 前後で位相をずらす

        if (_leftForeWing != null) _leftForeWing.localRotation = Quaternion.Euler(0f, 0f, -flapFore);
        if (_rightForeWing != null) _rightForeWing.localRotation = Quaternion.Euler(0f, 0f, flapFore);
        if (_leftHindWing != null) _leftHindWing.localRotation = Quaternion.Euler(0f, 0f, -flapHind);
        if (_rightHindWing != null) _rightHindWing.localRotation = Quaternion.Euler(0f, 0f, flapHind);

        // 飛行移動とホバリング
        transform.position = Vector3.MoveTowards(transform.position, _target, _speed * Time.deltaTime);
        Vector3 dir = _target - transform.position;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 7f);

        if (Vector3.Distance(transform.position, _target) < 0.25f)
        {
            _hoverTimer += Time.deltaTime;
            // ホバリング中の繊細な上下微振動
            transform.position += new Vector3(0f, Mathf.Sin(Time.time * 8f) * 0.003f, 0f);

            if (_hoverTimer > 1.4f)
            {
                _hoverTimer = 0f;
                _target = _home + Random.insideUnitSphere * 5.5f;
                _target.y = _home.y + Random.Range(-0.3f, 1.1f);
                _speed = 1.9f + Random.value * 2.3f;
            }
        }
    }

    /// <summary>
    /// 旧来の四角い板ブロック（歯ブラシ風）を完全撤去し、
    /// 丸い頭部、大きな複眼、引き締まった胸部、細く伸びる尾、透明な4枚の翅による本物のトンボ造形を生成
    /// </summary>
    [ContextMenu("Rebuild Visuals")]
    public void RebuildDragonflyVisuals()
    {
        // 既存の旧パーツからマテリアルを取得
        Material bodyMat = null;
        var oldBody = transform.Find("Body");
        if (oldBody != null)
        {
            var r = oldBody.GetComponent<Renderer>();
            if (r != null) bodyMat = r.sharedMaterial;
        }

        // 旧パーツ（歯ブラシのブラシ状の板など）を全削除
        var oldWings = transform.Find("Wings");
        if (oldWings != null) DestroyImmediate(oldWings.gameObject);
        if (oldBody != null) DestroyImmediate(oldBody.gameObject);
        var oldModel = transform.Find("ModelRoot");
        if (oldModel != null) DestroyImmediate(oldModel.gameObject);

        // マテリアル準備
        if (_sharedWingMat == null) _sharedWingMat = CreateWingMaterial();
        if (_sharedEyeMat == null) _sharedEyeMat = CreateEyeMaterial();
        if (bodyMat == null) bodyMat = CreateDefaultBodyMaterial();

        var modelRoot = new GameObject("ModelRoot");
        modelRoot.transform.SetParent(transform, false);

        // 1. 胸部（Thorax: 中心となるしっかりした胸）
        var thorax = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        thorax.name = "Thorax";
        thorax.transform.SetParent(modelRoot.transform, false);
        thorax.transform.localPosition = new Vector3(0f, 0f, 0.035f);
        thorax.transform.localScale = new Vector3(0.048f, 0.058f, 0.085f);
        thorax.GetComponent<Renderer>().sharedMaterial = bodyMat;
        DestroyImmediate(thorax.GetComponent<Collider>());

        // 2. 頭部（Head: 前方の丸い頭）
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(modelRoot.transform, false);
        head.transform.localPosition = new Vector3(0f, 0f, 0.095f);
        head.transform.localScale = new Vector3(0.042f, 0.038f, 0.040f);
        head.GetComponent<Renderer>().sharedMaterial = bodyMat;
        DestroyImmediate(head.GetComponent<Collider>());

        // 3. 大きな複眼（左右のつぶらな目: エメラルドブラック）
        var leftEye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leftEye.name = "LeftEye";
        leftEye.transform.SetParent(modelRoot.transform, false);
        leftEye.transform.localPosition = new Vector3(-0.022f, 0.008f, 0.105f);
        leftEye.transform.localScale = new Vector3(0.028f, 0.028f, 0.032f);
        leftEye.GetComponent<Renderer>().sharedMaterial = _sharedEyeMat;
        DestroyImmediate(leftEye.GetComponent<Collider>());

        var rightEye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rightEye.name = "RightEye";
        rightEye.transform.SetParent(modelRoot.transform, false);
        rightEye.transform.localPosition = new Vector3(0.022f, 0.008f, 0.105f);
        rightEye.transform.localScale = new Vector3(0.028f, 0.028f, 0.032f);
        rightEye.GetComponent<Renderer>().sharedMaterial = _sharedEyeMat;
        DestroyImmediate(rightEye.GetComponent<Collider>());

        // 4. 腹部・尾（Abdomen: 後方に細長く伸びる美しい節尾）
        var tail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tail.name = "Tail";
        tail.transform.SetParent(modelRoot.transform, false);
        tail.transform.localPosition = new Vector3(0f, 0.002f, -0.125f);
        tail.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        tail.transform.localScale = new Vector3(0.016f, 0.18f, 0.016f); // 極細でスマート
        tail.GetComponent<Renderer>().sharedMaterial = bodyMat;
        DestroyImmediate(tail.GetComponent<Collider>());

        // 尾の先端ポッチ
        var tailTip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tailTip.name = "TailTip";
        tailTip.transform.SetParent(modelRoot.transform, false);
        tailTip.transform.localPosition = new Vector3(0f, 0.002f, -0.31f);
        tailTip.transform.localScale = new Vector3(0.014f, 0.014f, 0.020f);
        tailTip.GetComponent<Renderer>().sharedMaterial = bodyMat;
        DestroyImmediate(tailTip.GetComponent<Collider>());

        // 5. 翅（4枚の繊細な透明羽）
        // (a) 左前翅
        var lfPivot = new GameObject("LeftForeWingPivot");
        lfPivot.transform.SetParent(modelRoot.transform, false);
        lfPivot.transform.localPosition = new Vector3(-0.024f, 0.028f, 0.055f);
        var lfMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lfMesh.transform.SetParent(lfPivot.transform, false);
        lfMesh.transform.localPosition = new Vector3(-0.13f, 0f, 0.01f);
        lfMesh.transform.localRotation = Quaternion.Euler(0f, -8f, 0f);
        lfMesh.transform.localScale = new Vector3(0.26f, 0.002f, 0.052f);
        lfMesh.GetComponent<Renderer>().sharedMaterial = _sharedWingMat;
        DestroyImmediate(lfMesh.GetComponent<Collider>());
        _leftForeWing = lfPivot.transform;

        // (b) 右前翅
        var rfPivot = new GameObject("RightForeWingPivot");
        rfPivot.transform.SetParent(modelRoot.transform, false);
        rfPivot.transform.localPosition = new Vector3(0.024f, 0.028f, 0.055f);
        var rfMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rfMesh.transform.SetParent(rfPivot.transform, false);
        rfMesh.transform.localPosition = new Vector3(0.13f, 0f, 0.01f);
        rfMesh.transform.localRotation = Quaternion.Euler(0f, 8f, 0f);
        rfMesh.transform.localScale = new Vector3(0.26f, 0.002f, 0.052f);
        rfMesh.GetComponent<Renderer>().sharedMaterial = _sharedWingMat;
        DestroyImmediate(rfMesh.GetComponent<Collider>());
        _rightForeWing = rfPivot.transform;

        // (c) 左後翅
        var lhPivot = new GameObject("LeftHindWingPivot");
        lhPivot.transform.SetParent(modelRoot.transform, false);
        lhPivot.transform.localPosition = new Vector3(-0.022f, 0.024f, 0.018f);
        var lhMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lhMesh.transform.SetParent(lhPivot.transform, false);
        lhMesh.transform.localPosition = new Vector3(-0.11f, 0f, -0.01f);
        lhMesh.transform.localRotation = Quaternion.Euler(0f, -16f, 0f);
        lhMesh.transform.localScale = new Vector3(0.22f, 0.002f, 0.058f);
        lhMesh.GetComponent<Renderer>().sharedMaterial = _sharedWingMat;
        DestroyImmediate(lhMesh.GetComponent<Collider>());
        _leftHindWing = lhPivot.transform;

        // (d) 右後翅
        var rhPivot = new GameObject("RightHindWingPivot");
        rhPivot.transform.SetParent(modelRoot.transform, false);
        rhPivot.transform.localPosition = new Vector3(0.022f, 0.024f, 0.018f);
        var rhMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rhMesh.transform.SetParent(rhPivot.transform, false);
        rhMesh.transform.localPosition = new Vector3(0.11f, 0f, -0.01f);
        rhMesh.transform.localRotation = Quaternion.Euler(0f, 16f, 0f);
        rhMesh.transform.localScale = new Vector3(0.22f, 0.002f, 0.058f);
        rhMesh.GetComponent<Renderer>().sharedMaterial = _sharedWingMat;
        DestroyImmediate(rhMesh.GetComponent<Collider>());
        _rightHindWing = rhPivot.transform;
    }

    static Material CreateWingMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader);
        mat.name = "DragonflyWing_Trans";
        mat.SetFloat("_Surface", 1f); // Transparent
        mat.SetFloat("_Blend", 0f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.color = new Color(0.85f, 0.95f, 1.0f, 0.32f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.95f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.15f);
        return mat;
    }

    static Material CreateEyeMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader);
        mat.name = "DragonflyEye_Gloss";
        mat.color = new Color(0.06f, 0.16f, 0.10f); // 深いエメラルドブラック
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.98f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.4f);
        return mat;
    }

    static Material CreateDefaultBodyMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader);
        mat.name = "DragonflyBody_Red";
        mat.color = new Color(0.85f, 0.18f, 0.14f); // 鮮やかな赤とんぼ
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.85f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.3f);
        return mat;
    }
}
