using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// 『Rust & Float』広大な曲線の水平線＆波が煌くエメラルド〜ブルー海面システム。
/// ・島（中心 512, 512）を取り囲む半径4500m（直径9km）の巨大ラジアル海面メッシュを構築。
/// ・外洋にかけて地球の曲率（なだらかなカーブ）を描き、上空から見ても360度美しい円弧の水平線が出現。
/// ・ビーチ浅瀬のエメラルドグリーンから、沿岸のトロピカルブルー、沖合のディープサファイアブルーへと多層グラデーション。
/// ・太陽光に対するスペキュラ・グリッター（波のきらめき）を海面全体にリアルタイム描画。
/// ・カメラの描画距離（FarClipPlane）を5000mに最適化し、遠景の水平線が一切途切れない。
/// </summary>
[ExecuteAlways]
public class AdventureCurvedHorizonOcean : MonoBehaviour
{
    private static AdventureCurvedHorizonOcean _instance;
    public static AdventureCurvedHorizonOcean Instance => _instance;

    public const string OceanGameObjectName = "OceanPlane";
    public const string MaterialPath = "Assets/RustAndFloat/Materials/ParadiseCurvedOcean.mat";
    public const string ShaderName = "RustAndFloat/ParadiseCurvedOcean";

    public const float OceanCenterY = 5.50f;
    public const float MaxRadius = 4500f;
    public const float InnerRadius = 520f;
    public const float CurvatureDrop = 22.0f;

    [SerializeField] private Material _oceanMaterial;
    [SerializeField] private Mesh _curvedOceanMesh;

    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void RuntimeInit()
    {
        Ensure();
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void EditorInit()
    {
        EditorApplication.delayCall += () =>
        {
            if (!Application.isPlaying)
            {
                Ensure();
            }
        };
    }
#endif

    public static AdventureCurvedHorizonOcean Ensure()
    {
        var existing = Object.FindAnyObjectByType<AdventureCurvedHorizonOcean>(FindObjectsInactive.Include);
        if (existing != null)
        {
            _instance = existing;
            existing.SetupOcean();
            return existing;
        }

        var oceanGo = GameObject.Find(OceanGameObjectName);
        if (oceanGo == null)
        {
            oceanGo = new GameObject(OceanGameObjectName);
        }

        var comp = oceanGo.GetComponent<AdventureCurvedHorizonOcean>() ?? oceanGo.AddComponent<AdventureCurvedHorizonOcean>();
        _instance = comp;
        comp.SetupOcean();
        return comp;
    }

    private void Awake()
    {
        _instance = this;
    }

    private void OnEnable()
    {
        SetupOcean();
    }

    private void Update()
    {
        // カメラの描画距離を5000mに維持（水平線が見切れるのを防止）
        EnsureCameraClipDistance();
    }

    public void SetupOcean()
    {
        transform.position = new Vector3(512f, OceanCenterY, 512f);
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        // コライダーは除去（不要なレイキャストヒット防止）
        var col = GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }

        _meshFilter = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();

        // 1. マテリアルの確認または生成
        EnsureMaterial();

        // 2. なだらかな曲線の巨大ラジアルオーシャンメッシュの生成
        EnsureCurvedOceanMesh();

        // 3. カメラの描画距離設定
        EnsureCameraClipDistance();
    }

    private void EnsureMaterial()
    {
        if (_oceanMaterial != null && _oceanMaterial.shader != null && _oceanMaterial.shader.name == ShaderName)
        {
            _meshRenderer.sharedMaterial = _oceanMaterial;
            return;
        }

#if UNITY_EDITOR
        _oceanMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (_oceanMaterial == null)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogWarning("[AdventureCurvedHorizonOcean] Shader not found: " + ShaderName);
                return;
            }

            _oceanMaterial = new Material(shader);
            _oceanMaterial.name = "ParadiseCurvedOcean";

            // ノーマルマップの割り当て
            var n1 = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Idyllic Fantasy Nature/Textures/Water/Water_Normal_01.png");
            var n2 = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Idyllic Fantasy Nature/Textures/Water/Water_Normal_02.png");
            if (n1 != null) _oceanMaterial.SetTexture("_NormalMap1", n1);
            if (n2 != null) _oceanMaterial.SetTexture("_NormalMap2", n2);

            _oceanMaterial.SetColor("_ShallowColor", new Color(0.05f, 0.96f, 0.75f, 0.82f));
            _oceanMaterial.SetColor("_MidColor", new Color(0.02f, 0.82f, 0.68f, 0.94f));
            _oceanMaterial.SetColor("_DeepColor", new Color(0.01f, 0.48f, 0.50f, 0.99f));
            _oceanMaterial.SetColor("_HorizonColor", new Color(0.35f, 0.85f, 0.82f, 1.0f));
            _oceanMaterial.SetColor("_SunGlitterColor", new Color(1.0f, 0.98f, 0.88f, 1.0f));

            _oceanMaterial.SetFloat("_NormalStrength", 1.25f);
            _oceanMaterial.SetFloat("_SunGlitterIntensity", 5.2f);
            _oceanMaterial.SetFloat("_SunGlitterExponent", 80f);
            _oceanMaterial.SetFloat("_ShallowRadius", 360f);
            _oceanMaterial.SetFloat("_DeepRadius", 800f);
            _oceanMaterial.SetFloat("_HorizonRadius", MaxRadius);
            _oceanMaterial.SetFloat("_CurvatureAmount", CurvatureDrop);

            AssetDatabase.CreateAsset(_oceanMaterial, MaterialPath);
            AssetDatabase.SaveAssets();
        }
#endif

        if (_oceanMaterial != null)
        {
            _meshRenderer.sharedMaterial = _oceanMaterial;
        }
    }

    private void EnsureCurvedOceanMesh()
    {
#if UNITY_EDITOR
        var assetMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/RustAndFloat/Terrain/ParadiseCurvedOceanMesh.asset");
        if (assetMesh != null)
        {
            _curvedOceanMesh = assetMesh;
            _meshFilter.sharedMesh = assetMesh;
            return;
        }
#endif
        if (_curvedOceanMesh != null && _meshFilter.sharedMesh == _curvedOceanMesh)
        {
            return;
        }

        _curvedOceanMesh = GenerateRadialCurvedOceanMesh();
        _meshFilter.sharedMesh = _curvedOceanMesh;
    }

    /// <summary>
    /// 半径4500m（直径9km）、外周にかけて緩やかな曲率で下がる360度ラジアル海面メッシュを構築
    /// </summary>
    public static Mesh GenerateRadialCurvedOceanMesh()
    {
        var mesh = new Mesh();
        mesh.name = "ParadiseCurvedOceanMesh";

        int rings = 48;       // 半径方向の分割数
        int segments = 72;    // 円周方向の分割数（5度刻みの極めて滑らかな円弧）

        int vertCount = (rings + 1) * segments + 1; // 中心点含む
        Vector3[] vertices = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        Color[] colors = new Color[vertCount];

        // 中心頂点 (0, 0, 0)
        vertices[0] = Vector3.zero;
        normals[0] = Vector3.up;
        uvs[0] = new Vector2(0.5f, 0.5f);
        colors[0] = new Color(0f, 0f, 0f, 1f);

        int vertIndex = 1;

        for (int r = 1; r <= rings; r++)
        {
            float normR = r / (float)rings;
            // 内側（島周辺）は等間隔、外側（外洋）へ向かって放射状に広がる2次カーブ
            float radius = (normR <= 0.25f)
                ? Mathf.Lerp(30f, InnerRadius, normR / 0.25f)
                : Mathf.Lerp(InnerRadius, MaxRadius, Mathf.Pow((normR - 0.25f) / 0.75f, 1.4f));

            // 地球の曲率シミュレーション：内側は平坦、外側で放物線状に緩やかに下降
            float drop = 0f;
            if (radius > InnerRadius)
            {
                float tCurv = (radius - InnerRadius) / (MaxRadius - InnerRadius);
                drop = Mathf.Pow(tCurv, 1.85f) * CurvatureDrop;
            }

            for (int s = 0; s < segments; s++)
            {
                float angle = s * (Mathf.PI * 2f / segments);
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                float y = -drop; // 中心高さ基準からの下降量

                vertices[vertIndex] = new Vector3(x, y, z);
                // 法線：曲率に応じてわずかに外向きに傾斜
                Vector3 n = (radius > InnerRadius)
                    ? new Vector3(x * 0.0001f, 1f, z * 0.0001f).normalized
                    : Vector3.up;
                normals[vertIndex] = n;

                uvs[vertIndex] = new Vector2(x * 0.01f, z * 0.01f);
                colors[vertIndex] = new Color(normR, radius / MaxRadius, drop / CurvatureDrop, 1f);

                vertIndex++;
            }
        }

        // インデックス（三角形）の生成
        int triCount = segments * 3 + (rings - 1) * segments * 6;
        int[] triangles = new int[triCount];
        int triIndex = 0;

        // 最初のリング（中心点との接続ファン: 時計回り）
        for (int s = 0; s < segments; s++)
        {
            int nextS = (s + 1) % segments;
            triangles[triIndex++] = 0;
            triangles[triIndex++] = 1 + nextS;
            triangles[triIndex++] = 1 + s;
        }

        // 以降のリング間のクアッド（時計回り）
        for (int r = 1; r < rings; r++)
        {
            int currentRingStart = 1 + (r - 1) * segments;
            int nextRingStart = 1 + r * segments;

            for (int s = 0; s < segments; s++)
            {
                int nextS = (s + 1) % segments;

                int c0 = currentRingStart + s;
                int c1 = currentRingStart + nextS;
                int n0 = nextRingStart + s;
                int n1 = nextRingStart + nextS;

                // 三角形1 (c0 -> c1 -> n0)
                triangles[triIndex++] = c0;
                triangles[triIndex++] = c1;
                triangles[triIndex++] = n0;

                // 三角形2 (c1 -> n1 -> n0)
                triangles[triIndex++] = c1;
                triangles[triIndex++] = n1;
                triangles[triIndex++] = n0;
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    private void EnsureCameraClipDistance()
    {
        var cam = Camera.main;
        if (cam != null && cam.farClipPlane < 5000f)
        {
            cam.farClipPlane = 5000f;
        }

        // 全アクティブカメラに対しても同様に拡張
        foreach (var c in Camera.allCameras)
        {
            if (c != null && c.farClipPlane < 5000f)
            {
                c.farClipPlane = 5000f;
            }
        }
    }
}
