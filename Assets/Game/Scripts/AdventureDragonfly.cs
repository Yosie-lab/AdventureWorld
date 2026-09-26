using UnityEngine;

/// <summary>
/// 楽園の水辺や砂浜を舞うトンボ（Dragonfly）
/// スイスイと直線的に飛び、空中でピタッとホバリング（羽の超高速振動）し、
/// 水面や草むらの上を軽快に飛び回るリアルなトンボAI。
/// 歯ブラシのような単一板の不自然さを解消し、頭部・複眼・4枚翅による美しい昆虫造形と自然な羽ばたきを実現。
/// </summary>
public class AdventureDragonfly : MonoBehaviour
{
    [Header("飛翔パラメータ")]
    [SerializeField] private float flySpeed = 3.5f;
    [SerializeField] private float flightRadius = 7f;
    [SerializeField] private float minHeight = 5.8f;
    [SerializeField] private float maxHeight = 7.5f;

    private Vector3 _homePosition;
    private Vector3 _targetPosition;
    private bool _isHovering = false;
    private float _stateTimer = 0f;

    private Transform _leftWings;
    private Transform _rightWings;
    private float _wingPhase = 0f;

    private static Material _sharedWingMat;
    private static Material _sharedEyeMat;

    public void Setup(Transform leftWing, Transform rightWing, Vector3 homePos)
    {
        _leftWings = leftWing;
        _rightWings = rightWing;
        _homePosition = homePos;
        _targetPosition = homePos;
        _isHovering = false;
        _stateTimer = Random.Range(1f, 2.5f);
    }

    private void Awake()
    {
        RebuildVisualsIfNeeded();
    }

    private void Start()
    {
        if (_homePosition == Vector3.zero) _homePosition = transform.position;
        if (_targetPosition == Vector3.zero) PickNewTarget();
    }

    private void Update()
    {
        // 1. 羽の超高速パタパタ（上下羽ばたき：Z軸ロール）
        _wingPhase += Time.deltaTime * 65f;
        float flapAngle = Mathf.Sin(_wingPhase) * 26f;
        if (_leftWings != null) _leftWings.localRotation = Quaternion.Euler(0f, 0f, -flapAngle);
        if (_rightWings != null) _rightWings.localRotation = Quaternion.Euler(0f, 0f, flapAngle);

        // 2. ホバリング vs 直線飛翔の状態遷移
        _stateTimer -= Time.deltaTime;
        if (_stateTimer <= 0f)
        {
            _isHovering = !_isHovering;
            if (_isHovering)
            {
                _stateTimer = Random.Range(1.2f, 3.0f); // ホバリング時間
            }
            else
            {
                PickNewTarget();
                _stateTimer = Random.Range(2.0f, 4.5f); // 飛翔時間
            }
        }

        if (_isHovering)
        {
            // 空中でわずかに揺れながら静止
            float hoverJitterY = Mathf.Sin(Time.time * 6f) * 0.03f;
            float hoverJitterX = Mathf.Cos(Time.time * 4f) * 0.02f;
            transform.position += new Vector3(hoverJitterX, hoverJitterY, 0f) * Time.deltaTime;
        }
        else
        {
            // 目標地点へ向かってスムーズに直線移動
            Vector3 toTarget = _targetPosition - transform.position;
            if (toTarget.sqrMagnitude > 0.05f)
            {
                var targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 7f);
                transform.position = Vector3.MoveTowards(transform.position, _targetPosition, flySpeed * Time.deltaTime);
            }
            else
            {
                _isHovering = true;
                _stateTimer = Random.Range(1.5f, 3.0f);
            }
        }
    }

    private void PickNewTarget()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float dist = Random.Range(1.5f, flightRadius);
        float h = Random.Range(minHeight, maxHeight);
        _targetPosition = _homePosition + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
        _targetPosition.y = h;
    }

    /// <summary>
    /// 頭部複眼やスリムな尾、透明な羽を追加・美化
    /// </summary>
    public void RebuildVisualsIfNeeded()
    {
        // 既に複眼があれば美化済み
        if (transform.Find("ModelRoot") != null || transform.Find("LeftEye") != null) return;

        // 既存のマテリアルを取得
        Material bodyMat = null;
        var body = transform.Find("Body");
        if (body != null)
        {
            var r = body.GetComponent<Renderer>();
            if (r != null) bodyMat = r.sharedMaterial;
        }

        if (_sharedEyeMat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _sharedEyeMat = new Material(shader) { name = "DragonflyEye_Gloss" };
            _sharedEyeMat.color = new Color(0.06f, 0.16f, 0.10f);
            if (_sharedEyeMat.HasProperty("_Smoothness")) _sharedEyeMat.SetFloat("_Smoothness", 0.98f);
        }

        // 頭部と複眼を美化
        var head = transform.Find("Head");
        if (head != null)
        {
            head.localPosition = new Vector3(0f, 0f, 0.12f);
            head.localScale = new Vector3(0.045f, 0.040f, 0.042f);

            var le = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            le.name = "LeftEye";
            le.transform.SetParent(head, false);
            le.transform.localPosition = new Vector3(-0.024f, 0.008f, 0.015f);
            le.transform.localScale = new Vector3(0.030f, 0.030f, 0.034f);
            le.GetComponent<Renderer>().sharedMaterial = _sharedEyeMat;
            DestroyImmediate(le.GetComponent<Collider>());

            var re = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            re.name = "RightEye";
            re.transform.SetParent(head, false);
            re.transform.localPosition = new Vector3(0.024f, 0.008f, 0.015f);
            re.transform.localScale = new Vector3(0.030f, 0.030f, 0.034f);
            re.GetComponent<Renderer>().sharedMaterial = _sharedEyeMat;
            DestroyImmediate(re.GetComponent<Collider>());
        }

        // 尾（Body）を細くスマートに調整
        if (body != null)
        {
            body.localScale = new Vector3(0.020f, 0.22f, 0.020f);
            body.localPosition = new Vector3(0f, 0f, -0.10f);
        }

        _leftWings = transform.Find("LeftWings");
        _rightWings = transform.Find("RightWings");
    }
}
