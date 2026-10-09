using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 白砂ビーチの波打ち際に打ち上げられた貝殻・シーグラス・琥珀の採取アイテム。
/// 太陽光を反射してキラキラ輝き、近づいてEキーで拾うと
/// クリスタルチャイム音とともに手元へフワリと吸い込まれ、
/// 相棒Rustが嬉しそうに語りかける小気味よい収集ループを提供する。
/// </summary>
public class AdventureBeachSeashellItem : MonoBehaviour
{
    public enum ShellKind
    {
        Sakuragai,          // 桜色のサクラガイ（薄紅色の二枚貝）
        SeaGlassEmerald,    // エメラルド・シーグラス（波に磨かれた緑ガラス）
        SeaGlassSapphire,   // サファイア・シーグラス（深海の蒼いガラス片）
        AmberPebble,        // 太陽の小琥珀（黄金色の樹脂化石）
        SpiralShell         // 純白の小巻貝（耳に当てると波の音）
    }

    [Header("Item Properties")]
    public string itemId;
    public ShellKind kind = ShellKind.Sakuragai;
    public string itemName = "桜色のサクラガイ";
    public string rustReaction = "わぁ、花びらみたいな貝殻だね！";
    public Color themeColor = new Color(1f, 0.75f, 0.85f, 1f);

    bool _isCollected = false;
    bool _isPlayerNear = false;
    bool _isCollecting = false;
    float _collectAnimTimer = 0f;
    Vector3 _startPos;
    Transform _visualRoot;
    ParticleSystem _sparklePs;
    ParticleSystem _twinklePs;
    Light _pointLight;
    AudioSource _audioSource;
    static AudioClip _chimeClip;
    bool _initialized = false;

    public bool IsCollected => _isCollected;
    public Color itemColor => themeColor;

    void Awake()
    {
        _startPos = transform.position;

        // 採取判定コライダー（至近距離での自動接触採取）
        var col = GetComponent<SphereCollider>();
        if (col == null)
            col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 2.2f;

        var rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    /// <summary>Managerから指定ID・種類を受け取って固有の見た目・効果を構築</summary>
    public void Initialize(string id, ShellKind shellKind)
    {
        itemId = id;
        kind = shellKind;
        _startPos = transform.position;

        CreateVisual();
        SetupAudio();
        CreateSparkleFx();
        _initialized = true;

        // セーブ済み判定
        if (!string.IsNullOrEmpty(itemId) && PlayerPrefs.GetInt("Seashell_Collected_" + itemId, 0) == 1)
        {
            _isCollected = true;
            gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        if (!string.IsNullOrEmpty(itemId))
        {
            bool savedCollected = PlayerPrefs.GetInt("Seashell_Collected_" + itemId, 0) == 1;
            if (!savedCollected && _isCollected)
            {
                ResetItemState(transform.position);
            }
        }
    }

    /// <summary>リスタート・ニューゲーム用：内部状態・外見・当たり判定を完全初期化</summary>
    public void ResetItemState(Vector3 newPos)
    {
        _isCollected = false;
        _isCollecting = false;
        _isPlayerNear = false;
        _collectAnimTimer = 0f;

        transform.position = newPos;
        _startPos = newPos;
        transform.localScale = Vector3.one;

        // ビジュアルが旧形式なら高品質リアルモデルへ自動再生成
        if (_visualRoot != null && _visualRoot.Find("Shell_Lower") == null && _visualRoot.Find("SeaGlass_Stone") == null && _visualRoot.Find("Amber_Stone") == null && _visualRoot.Find("Spiral_Shell") == null)
        {
            if (Application.isPlaying) Destroy(_visualRoot.gameObject);
            else DestroyImmediate(_visualRoot.gameObject);
            _visualRoot = null;
            CreateVisual();
        }
        else if (_visualRoot == null)
        {
            CreateVisual();
        }

        if (_visualRoot != null)
        {
            _visualRoot.gameObject.SetActive(true);
        }

        if (_pointLight != null)
        {
            _pointLight.enabled = true;
        }

        var col = GetComponent<SphereCollider>();
        if (col != null)
        {
            col.enabled = true;
        }

        gameObject.SetActive(true);
    }

    void Start()
    {
        if (!_initialized)
        {
            Initialize(itemId, kind);
        }
    }

    void SetupAudio()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0.5f;
        _audioSource.minDistance = 1.5f;
        _audioSource.maxDistance = 15f;
        _audioSource.playOnAwake = false;

        if (_chimeClip == null)
            _chimeClip = CreateCollectChimeClip();
    }

    static AudioClip CreateCollectChimeClip()
    {
        int rate = 44100;
        float duration = 0.38f;
        int count = Mathf.RoundToInt(rate * duration);
        float[] samples = new float[count];

        // 澄んだクリスタルチャイム（F#6: 1480Hz -> A#6: 1865Hz -> C#7: 2217Hz の三和音アルペジオ）
        float[] notes = { 1479.98f, 1864.66f, 2217.46f };
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float sum = 0f;
            for (int n = 0; n < notes.Length; n++)
            {
                float noteStart = n * 0.045f;
                if (t >= noteStart)
                {
                    float noteT = t - noteStart;
                    float env = Mathf.Exp(-noteT * 12f);
                    float s = Mathf.Sin(2f * Mathf.PI * notes[n] * noteT) * 0.32f
                            + Mathf.Sin(2f * Mathf.PI * notes[n] * 2f * noteT) * 0.10f;
                    sum += s * env;
                }
            }
            samples[i] = Mathf.Clamp(sum, -1f, 1f);
        }

        var clip = AudioClip.Create("SeashellChime", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    void CreateVisual()
    {
        var vGo = new GameObject("Visual");
        vGo.transform.SetParent(transform, false);
        _visualRoot = vGo.transform;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        switch (kind)
        {
            case ShellKind.Sakuragai:
                itemName = "桜色のサクラガイ";
                rustReaction = "わぁ、花びらみたいな貝殻だね！";
                themeColor = new Color(1f, 0.68f, 0.82f, 1f);
                CreateRealisticSakuragai(vGo.transform, shader);
                break;

            case ShellKind.SeaGlassEmerald:
                itemName = "エメラルド・シーグラス";
                rustReaction = "波に磨かれて角がすべすべだ！宝石みたい…！";
                themeColor = new Color(0.20f, 0.96f, 0.65f, 1f);
                CreateRealisticSeaGlass(vGo.transform, shader, new Color(0.18f, 0.90f, 0.58f, 0.92f), "Emerald");
                break;

            case ShellKind.SeaGlassSapphire:
                itemName = "サファイア・シーグラス";
                rustReaction = "深海みたいな綺麗な青色！空に透かすとキラキラするよ！";
                themeColor = new Color(0.25f, 0.78f, 1f, 1f);
                CreateRealisticSeaGlass(vGo.transform, shader, new Color(0.20f, 0.72f, 0.98f, 0.92f), "Sapphire");
                break;

            case ShellKind.AmberPebble:
                itemName = "太陽の小琥珀";
                rustReaction = "黄金色に光ってる…！昔の太陽の光を閉じ込めたみたい！";
                themeColor = new Color(1f, 0.82f, 0.22f, 1f);
                CreateRealisticAmber(vGo.transform, shader);
                break;

            case ShellKind.SpiralShell:
                itemName = "純白の小巻貝";
                rustReaction = "耳を当ててみて、Float！遠くの波の音が聞こえるよ！";
                themeColor = new Color(0.96f, 0.98f, 1f, 1f);
                CreateRealisticSpiralShell(vGo.transform, shader);
                break;
        }

        // 砂浜にコロンと自然に乗る緩やかな傾き
        _visualRoot.localRotation = Quaternion.Euler(Random.Range(-4f, 6f), Random.Range(0f, 360f), Random.Range(-5f, 6f));
    }

    #region Realistic Visual Builders
    // メッシュ＆テクスチャの共有キャッシュ（GC削減）
    static Mesh _sakuragaiMesh;
    static Mesh _seaGlassMesh;
    static Mesh _amberMesh;
    static Mesh _spiralMesh;
    static Texture2D _sakuragaiTex;
    static Texture2D _seaGlassEmeraldTex;
    static Texture2D _seaGlassSapphireTex;
    static Texture2D _amberTex;
    static Texture2D _spiralTex;

    void CreateRealisticSakuragai(Transform parent, Shader shader)
    {
        if (_sakuragaiMesh == null) _sakuragaiMesh = BuildSakuragaiMesh();
        if (_sakuragaiTex == null) _sakuragaiTex = GenerateSakuragaiTexture();

        var mat = new Material(shader) { name = "Sakuragai_Mat" };
        mat.mainTexture = _sakuragaiTex;
        mat.color = new Color(1f, 0.88f, 0.93f, 1f);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", _sakuragaiTex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1f, 0.88f, 0.93f, 1f));
        mat.SetFloat("_Smoothness", 0.90f); // 濡れた貝殻の上品なパール光沢
        mat.SetFloat("_Metallic", 0.08f);
        mat.EnableKeyword("_EMISSION");
        // 実物の模様・陰影を優先するため、エミッションは輪郭をほんのり際立たせる程度に抑制
        mat.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.75f) * 0.18f);

        // 下殻（砂に接地）
        var lower = new GameObject("Shell_Lower");
        lower.transform.SetParent(parent, false);
        lower.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        lower.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
        var mfLow = lower.AddComponent<MeshFilter>();
        mfLow.sharedMesh = _sakuragaiMesh;
        var mrLow = lower.AddComponent<MeshRenderer>();
        mrLow.sharedMaterial = mat;

        // 上殻（蝶番を基点にわずかに開いた二枚貝の立体造形）
        var upper = new GameObject("Shell_Upper");
        upper.transform.SetParent(parent, false);
        upper.transform.localPosition = new Vector3(0f, 0.035f, -0.015f);
        upper.transform.localRotation = Quaternion.Euler(-13f, 0f, 0f);
        upper.transform.localScale = new Vector3(0.40f, 0.40f, 0.40f);
        var mfUp = upper.AddComponent<MeshFilter>();
        mfUp.sharedMesh = _sakuragaiMesh;
        var mrUp = upper.AddComponent<MeshRenderer>();
        mrUp.sharedMaterial = mat;
    }

    void CreateRealisticSeaGlass(Transform parent, Shader shader, Color baseCol, string variant)
    {
        if (_seaGlassMesh == null) _seaGlassMesh = BuildSeaGlassMesh();
        Texture2D tex = null;
        if (variant == "Emerald")
        {
            if (_seaGlassEmeraldTex == null) _seaGlassEmeraldTex = GenerateSeaGlassTexture(new Color(0.18f, 0.92f, 0.58f));
            tex = _seaGlassEmeraldTex;
        }
        else
        {
            if (_seaGlassSapphireTex == null) _seaGlassSapphireTex = GenerateSeaGlassTexture(new Color(0.20f, 0.72f, 0.98f));
            tex = _seaGlassSapphireTex;
        }

        var mat = new Material(shader) { name = $"SeaGlass_{variant}_Mat" };
        mat.mainTexture = tex;
        mat.color = baseCol;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseCol);
        mat.SetFloat("_Smoothness", 0.86f); // 波で洗われたすりガラス（フロスト）の自然な光沢
        mat.SetFloat("_Metallic", 0.05f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", baseCol * 0.22f);

        var glassGo = new GameObject("SeaGlass_Stone");
        glassGo.transform.SetParent(parent, false);
        glassGo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        glassGo.transform.localScale = new Vector3(0.36f, 0.32f, 0.34f);
        var mf = glassGo.AddComponent<MeshFilter>();
        mf.sharedMesh = _seaGlassMesh;
        var mr = glassGo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
    }

    void CreateRealisticAmber(Transform parent, Shader shader)
    {
        if (_amberMesh == null) _amberMesh = BuildAmberMesh();
        if (_amberTex == null) _amberTex = GenerateAmberTexture();

        var mat = new Material(shader) { name = "Amber_Mat" };
        mat.mainTexture = _amberTex;
        mat.color = new Color(1f, 0.82f, 0.22f, 1f);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", _amberTex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1f, 0.82f, 0.22f, 1f));
        mat.SetFloat("_Smoothness", 0.94f); // 磨かれた琥珀のトロリとした高い平滑度
        mat.SetFloat("_Metallic", 0.04f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(1f, 0.75f, 0.15f) * 0.22f);

        var amberGo = new GameObject("Amber_Stone");
        amberGo.transform.SetParent(parent, false);
        amberGo.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        amberGo.transform.localScale = new Vector3(0.34f, 0.30f, 0.38f);
        var mf = amberGo.AddComponent<MeshFilter>();
        mf.sharedMesh = _amberMesh;
        var mr = amberGo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;

        // 内部に輝く黄金色のコア（内包物・化石の光の核）
        var coreGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        coreGo.name = "Amber_InnerCore";
        coreGo.transform.SetParent(amberGo.transform, false);
        coreGo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        coreGo.transform.localScale = new Vector3(0.24f, 0.20f, 0.24f);
        Destroy(coreGo.GetComponent<Collider>());
        var coreMat = new Material(shader);
        coreMat.color = new Color(1f, 0.92f, 0.35f, 1f);
        coreMat.EnableKeyword("_EMISSION");
        coreMat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.2f) * 0.4f);
        coreGo.GetComponent<Renderer>().sharedMaterial = coreMat;
    }

    void CreateRealisticSpiralShell(Transform parent, Shader shader)
    {
        if (_spiralMesh == null) _spiralMesh = BuildSpiralMesh();
        if (_spiralTex == null) _spiralTex = GenerateSpiralTexture();

        var mat = new Material(shader) { name = "SpiralShell_Mat" };
        mat.mainTexture = _spiralTex;
        mat.color = new Color(0.98f, 0.97f, 0.94f, 1f);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", _spiralTex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.98f, 0.97f, 0.94f, 1f));
        mat.SetFloat("_Smoothness", 0.82f); // 陶器・磁器のようなしっとりした質感
        mat.SetFloat("_Metallic", 0.05f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.92f, 0.95f, 1f) * 0.16f);

        var shellGo = new GameObject("Spiral_Shell");
        shellGo.transform.SetParent(parent, false);
        shellGo.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        shellGo.transform.localRotation = Quaternion.Euler(6f, 0f, 72f);
        shellGo.transform.localScale = new Vector3(0.35f, 0.38f, 0.35f);
        var mf = shellGo.AddComponent<MeshFilter>();
        mf.sharedMesh = _spiralMesh;
        var mr = shellGo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
    }
    #endregion

    #region Procedural Mesh Generators
    /// <summary>扇形に広がる本物のサクラガイの薄いドーム二枚貝メッシュ</summary>
    static Mesh BuildSakuragaiMesh()
    {
        var mesh = new Mesh { name = "Proc_SakuragaiMesh" };
        int radSteps = 9;
        int angSteps = 16;
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();

        for (int r = 0; r <= radSteps; r++)
        {
            float rNorm = (float)r / radSteps;
            float radius = rNorm * 0.55f;

            for (int a = 0; a <= angSteps; a++)
            {
                float aNorm = (float)a / angSteps; // 0..1
                float angleDeg = Mathf.Lerp(-52f, 52f, aNorm);
                float rad = angleDeg * Mathf.Deg2Rad;

                // 扇形の座標
                float x = Mathf.Sin(rad) * radius * (1f + 0.15f * Mathf.Cos(rad));
                float z = Mathf.Cos(rad) * radius;

                // 貝殻のふっくらしたドーム湾曲
                float dome = Mathf.Sin(rNorm * Mathf.PI * 0.85f) * 0.10f * (1f - Mathf.Abs(aNorm - 0.5f) * 0.6f);
                // 放射条線（貝殻のリブ筋）
                float rib = Mathf.Sin(aNorm * Mathf.PI * 14f) * 0.007f * rNorm;
                float y = dome + rib;

                vertices.Add(new Vector3(x, y, z));
                uvs.Add(new Vector2(aNorm, rNorm));
                normals.Add(Vector3.up);
            }
        }

        // 面のインデックス
        for (int r = 0; r < radSteps; r++)
        {
            for (int a = 0; a < angSteps; a++)
            {
                int cur = r * (angSteps + 1) + a;
                int next = cur + angSteps + 1;

                triangles.Add(cur);
                triangles.Add(next);
                triangles.Add(cur + 1);

                triangles.Add(cur + 1);
                triangles.Add(next);
                triangles.Add(next + 1);

                // 裏面（両面表示）
                triangles.Add(cur + 1);
                triangles.Add(next);
                triangles.Add(cur);

                triangles.Add(next + 1);
                triangles.Add(next);
                triangles.Add(cur + 1);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>波に磨かれて角が丸まったオーバル多面体シーグラス小石メッシュ</summary>
    static Mesh BuildSeaGlassMesh()
    {
        var mesh = new Mesh { name = "Proc_SeaGlassMesh" };
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        int ringSegments = 12;
        // 上極、上リング、赤道リング、下リング、下極
        Vector3 topPole = new Vector3(0f, 0.16f, 0f);
        Vector3 botPole = new Vector3(0f, -0.12f, 0f);

        // 頂点生成
        vertices.Add(topPole);
        uvs.Add(new Vector2(0.5f, 1f));

        // Ring 1 (上部ドーム)
        for (int i = 0; i < ringSegments; i++)
        {
            float ang = i * Mathf.PI * 2f / ringSegments;
            float rX = 0.32f * (1f + 0.08f * Mathf.Cos(ang * 2f));
            float rZ = 0.44f * (1f + 0.06f * Mathf.Sin(ang * 3f));
            vertices.Add(new Vector3(Mathf.Cos(ang) * rX * 0.72f, 0.11f, Mathf.Sin(ang) * rZ * 0.72f));
            uvs.Add(new Vector2((float)i / ringSegments, 0.75f));
        }

        // Ring 2 (中央エッジ・波の丸み)
        for (int i = 0; i < ringSegments; i++)
        {
            float ang = i * Mathf.PI * 2f / ringSegments;
            float rX = 0.36f * (1f + 0.08f * Mathf.Cos(ang * 2f));
            float rZ = 0.48f * (1f + 0.06f * Mathf.Sin(ang * 3f));
            vertices.Add(new Vector3(Mathf.Cos(ang) * rX, 0.02f, Mathf.Sin(ang) * rZ));
            uvs.Add(new Vector2((float)i / ringSegments, 0.5f));
        }

        // Ring 3 (下部ドーム)
        for (int i = 0; i < ringSegments; i++)
        {
            float ang = i * Mathf.PI * 2f / ringSegments;
            float rX = 0.30f * (1f + 0.08f * Mathf.Cos(ang * 2f));
            float rZ = 0.40f * (1f + 0.06f * Mathf.Sin(ang * 3f));
            vertices.Add(new Vector3(Mathf.Cos(ang) * rX * 0.70f, -0.07f, Mathf.Sin(ang) * rZ * 0.70f));
            uvs.Add(new Vector2((float)i / ringSegments, 0.25f));
        }

        int botPoleIndex = vertices.Count;
        vertices.Add(botPole);
        uvs.Add(new Vector2(0.5f, 0f));

        // Top cap triangles
        for (int i = 0; i < ringSegments; i++)
        {
            int next = (i + 1) % ringSegments;
            triangles.Add(0);
            triangles.Add(1 + i);
            triangles.Add(1 + next);
        }

        // Ring 1 to Ring 2
        int r1Base = 1;
        int r2Base = 1 + ringSegments;
        for (int i = 0; i < ringSegments; i++)
        {
            int next = (i + 1) % ringSegments;
            triangles.Add(r1Base + i);
            triangles.Add(r2Base + i);
            triangles.Add(r1Base + next);

            triangles.Add(r1Base + next);
            triangles.Add(r2Base + i);
            triangles.Add(r2Base + next);
        }

        // Ring 2 to Ring 3
        int r3Base = 1 + ringSegments * 2;
        for (int i = 0; i < ringSegments; i++)
        {
            int next = (i + 1) % ringSegments;
            triangles.Add(r2Base + i);
            triangles.Add(r3Base + i);
            triangles.Add(r2Base + next);

            triangles.Add(r2Base + next);
            triangles.Add(r3Base + i);
            triangles.Add(r3Base + next);
        }

        // Bottom cap triangles
        for (int i = 0; i < ringSegments; i++)
        {
            int next = (i + 1) % ringSegments;
            triangles.Add(botPoleIndex);
            triangles.Add(r3Base + next);
            triangles.Add(r3Base + i);
        }

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>なめらかな涙滴（ティアドロップ）オーバル琥珀メッシュ</summary>
    static Mesh BuildAmberMesh()
    {
        var mesh = new Mesh { name = "Proc_AmberMesh" };
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        int latLines = 10;
        int lonLines = 14;

        for (int lat = 0; lat <= latLines; lat++)
        {
            float v = (float)lat / latLines;
            float pitch = (v - 0.5f) * Mathf.PI; // -PI/2 .. PI/2
            float y = Mathf.Sin(pitch) * 0.22f;
            float rBase = Mathf.Cos(pitch);

            // 涙滴変形（先端にかけてなだらかに細くなる）
            float taper = Mathf.Lerp(0.70f, 1.15f, v);

            for (int lon = 0; lon <= lonLines; lon++)
            {
                float u = (float)lon / lonLines;
                float yaw = u * Mathf.PI * 2f;

                float x = Mathf.Cos(yaw) * rBase * 0.28f * taper;
                float z = Mathf.Sin(yaw) * rBase * 0.38f * taper;

                vertices.Add(new Vector3(x, y, z));
                uvs.Add(new Vector2(u, v));
            }
        }

        for (int lat = 0; lat < latLines; lat++)
        {
            for (int lon = 0; lon < lonLines; lon++)
            {
                int cur = lat * (lonLines + 1) + lon;
                int next = cur + lonLines + 1;

                triangles.Add(cur);
                triangles.Add(next);
                triangles.Add(cur + 1);

                triangles.Add(cur + 1);
                triangles.Add(next);
                triangles.Add(next + 1);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>先端が尖り段差のある螺旋小巻貝メッシュ</summary>
    static Mesh BuildSpiralMesh()
    {
        var mesh = new Mesh { name = "Proc_SpiralShellMesh" };
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        int turns = 4;
        int stepsPerTurn = 10;
        int totalSteps = turns * stepsPerTurn;
        int ringSegs = 8;

        for (int s = 0; s <= totalSteps; s++)
        {
            float t = (float)s / totalSteps; // 0: 先端, 1: 開口部
            float spiralAngle = s * (Mathf.PI * 2f / stepsPerTurn);

            // 螺旋の芯線
            float spiralRadius = t * 0.12f;
            float tubeRadius = Mathf.Lerp(0.018f, 0.11f, t * t);
            float spiralZ = t * 0.42f;

            Vector3 center = new Vector3(Mathf.Cos(spiralAngle) * spiralRadius, Mathf.Sin(spiralAngle) * spiralRadius, spiralZ);

            for (int r = 0; r <= ringSegs; r++)
            {
                float ringAngle = r * (Mathf.PI * 2f / ringSegs);
                Vector3 offset = new Vector3(Mathf.Cos(ringAngle) * tubeRadius, Mathf.Sin(ringAngle) * tubeRadius, 0f);
                vertices.Add(center + offset);
                uvs.Add(new Vector2((float)r / ringSegs, t));
            }
        }

        for (int s = 0; s < totalSteps; s++)
        {
            for (int r = 0; r < ringSegs; r++)
            {
                int cur = s * (ringSegs + 1) + r;
                int next = cur + ringSegs + 1;

                triangles.Add(cur);
                triangles.Add(next);
                triangles.Add(cur + 1);

                triangles.Add(cur + 1);
                triangles.Add(next);
                triangles.Add(next + 1);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
    #endregion

    #region Procedural Texture Generators
    static Texture2D GenerateSakuragaiTexture()
    {
        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var basePink = new Color(1f, 0.72f, 0.83f, 1f);
        var whiteRib = new Color(1f, 0.94f, 0.97f, 1f);
        var edgeRose = new Color(1f, 0.52f, 0.70f, 1f);

        for (int y = 0; y < size; y++)
        {
            float v = (float)y / size; // 蝶番(0) -> 縁(1)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size; // 左 -> 右
                // 放射条線
                float ribSin = Mathf.Sin(u * Mathf.PI * 18f);
                float ribFactor = Mathf.Clamp01(ribSin * 0.5f + 0.5f);

                // 成長線の微小な縞
                float ringFactor = Mathf.Sin(v * Mathf.PI * 22f) * 0.08f;

                Color c = Color.Lerp(basePink, whiteRib, ribFactor * 0.45f);
                c = Color.Lerp(c, edgeRose, v * 0.40f + ringFactor);
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    static Texture2D GenerateSeaGlassTexture(Color mainColor)
    {
        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color frostWhite = new Color(0.92f, 0.98f, 0.96f, 1f);

        for (int y = 0; y < size; y++)
        {
            float v = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                // すりガラスの縁取りフロスト感
                float frost = Mathf.Clamp01(d * 0.65f + Random.Range(-0.04f, 0.04f));
                Color c = Color.Lerp(mainColor, frostWhite, frost * 0.40f);
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    static Texture2D GenerateAmberTexture()
    {
        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color centerGold = new Color(1f, 0.92f, 0.40f, 1f);
        Color edgeAmber = new Color(0.95f, 0.60f, 0.10f, 1f);

        for (int y = 0; y < size; y++)
        {
            float v = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;
                float dist = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                Color c = Color.Lerp(centerGold, edgeAmber, Mathf.Clamp01(dist * 0.85f));
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    static Texture2D GenerateSpiralTexture()
    {
        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color ivoryWhite = new Color(0.98f, 0.98f, 0.95f, 1f);
        Color creamLine = new Color(0.88f, 0.82f, 0.72f, 1f);

        for (int y = 0; y < size; y++)
        {
            float v = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;
                float stripe = Mathf.Sin((u + v * 3f) * Mathf.PI * 8f);
                Color c = Color.Lerp(ivoryWhite, creamLine, Mathf.Clamp01(stripe * 0.35f + 0.15f));
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }
    #endregion

    static Shader GetSafeUnlitShader()
    {
        return Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Mobile/Particles/Additive")
            ?? Shader.Find("Unlit/Transparent");
    }

    static Material CreateSafeGlowMaterial(Texture2D tex, Color color, string name)
    {
        var shader = GetSafeUnlitShader();
        var mat = new Material(shader) { name = name };
        mat.mainTexture = tex;
        mat.color = color;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1f); // Additive
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // Double-sided
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);

        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3100;
        return mat;
    }

    void CreateSparkleFx()
    {
        var smokeTex = AdventureRustDrone.GetSoftSmokeTexture();

        // 1. 周囲の砂浜をほんのり染める上品なポイントライト（強すぎず実物の陰影を活かす）
        var lightGo = new GameObject("ItemPointLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.18f, 0f);
        _pointLight = lightGo.AddComponent<Light>();
        _pointLight.type = LightType.Point;
        _pointLight.range = 1.8f;
        _pointLight.intensity = 0.45f;
        _pointLight.color = themeColor;
        _pointLight.shadows = LightShadows.None;

        // 2. 実物の周囲で時折キラッと小さく瞬くジュエリースパークル（実物を隠さない微小粒子）
        var dustGo = new GameObject("JewelSparkle");
        dustGo.transform.SetParent(transform, false);
        dustGo.transform.localPosition = Vector3.up * 0.08f;

        _sparklePs = dustGo.AddComponent<ParticleSystem>();
        var mainDust = _sparklePs.main;
        mainDust.loop = true;
        mainDust.startLifetime = 1.2f;
        mainDust.startSpeed = 0.12f;
        mainDust.startSize = 0.06f; // 小さく可憐なきらめき
        mainDust.startColor = new Color(themeColor.r, themeColor.g, themeColor.b, 0.85f);

        var emissionDust = _sparklePs.emission;
        emissionDust.rateOverTime = 1.8f; // 控えめにキラッと瞬く

        var shapeDust = _sparklePs.shape;
        shapeDust.shapeType = ParticleSystemShapeType.Sphere;
        shapeDust.radius = 0.28f;

        var rendDust = dustGo.GetComponent<ParticleSystemRenderer>();
        if (rendDust != null)
        {
            rendDust.sharedMaterial = CreateSafeGlowMaterial(smokeTex, themeColor, "JewelSparkle_Mat");
            rendDust.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rendDust.receiveShadows = false;
        }
    }

    void Update()
    {
        if (_isCollected) return;

        // 採取アニメーション（Nikoの手元へフワリと吸い込まれる）
        if (_isCollecting)
        {
            _collectAnimTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_collectAnimTimer / 0.32f);

            var player = AdventurePlayerController.Instance;
            Vector3 targetHandPos = player != null ? player.transform.position + Vector3.up * 1.1f : transform.position;

            // 弧を描くイージング補間
            float arc = Mathf.Sin(t * Mathf.PI) * 0.45f;
            transform.position = Vector3.Lerp(_startPos, targetHandPos, Mathf.SmoothStep(0f, 1f, t)) + Vector3.up * arc;
            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.15f, t);

            if (t >= 1f)
            {
                FinishCollection();
            }
            return;
        }

        // ライトの優しいパルス
        float pulseTime = (Time.time * 3.2f) + (itemId != null ? itemId.GetHashCode() % 10 : 0);
        float pulse = Mathf.Sin(pulseTime);

        if (_pointLight != null)
        {
            _pointLight.intensity = 1.8f + pulse * 0.45f;
            _pointLight.range = 3.5f + pulse * 0.6f;
        }

        // プレイヤー接近判定
        var p = AdventurePlayerController.Instance ?? Object.FindAnyObjectByType<AdventurePlayerController>();
        if (p != null)
        {
            // オープニングボード表示中やプロローグ目覚め中は採取・プロンプトを停止
            if (ShouldSuppressInteraction())
            {
                _isPlayerNear = false;
                return;
            }

            Vector3 itemPos = transform.position;
            Vector3 playerPos = p.transform.position;
            float flatDist = Vector2.Distance(new Vector2(itemPos.x, itemPos.z), new Vector2(playerPos.x, playerPos.z));
            float heightDiff = Mathf.Abs(itemPos.y - playerPos.y);

            // すぐ近く（水平2.6m、高さ差3.0m以内）まで近づくと自動で手元へフワリと吸い込み採取！
            const float autoPickupDist = 2.6f;
            const float promptDist = 3.8f;

            if (flatDist < autoPickupDist && heightDiff < 3.0f)
            {
                _isPlayerNear = false;
                StartCollecting();
                return;
            }

            // 少し離れた距離（2.6m〜3.8m）ではEキー/クリックでの手動採取も受付
            _isPlayerNear = (flatDist < promptDist && heightDiff < 3.0f);

            if (_isPlayerNear)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                var mouse = UnityEngine.InputSystem.Mouse.current;
                bool pressed = (kb != null && kb.eKey.wasPressedThisFrame)
                            || (mouse != null && mouse.leftButton.wasPressedThisFrame);
                try { if (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0)) pressed = true; } catch { }

                if (pressed && !AdventurePauseMenu.IsOpen)
                {
                    StartCollecting();
                }
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // 高速ダッシュやジャンプ等で接触した場合も確実に自動採取
        if (_isCollecting || _isCollected || ShouldSuppressInteraction()) return;
        if (other.GetComponentInParent<AdventurePlayerController>() != null)
        {
            StartCollecting();
        }
    }

    static bool ShouldSuppressInteraction()
    {
        if (AdventureRustFloatOpening.Instance != null && AdventureRustFloatOpening.Instance.IsModalBoardOpen()) return true;
        if (AdventurePrologueDrama.Instance != null && AdventurePrologueDrama.Instance.IsAwakening) return true;
        if (AdventurePauseMenu.IsOpen) return true;
        return false;
    }

    void StartCollecting()
    {
        if (_isCollecting || _isCollected) return;
        _isCollecting = true;
        _collectAnimTimer = 0f;
        _startPos = transform.position;

        // 澄んだクリスタル採取音
        float seVol = PlayerPrefs.GetFloat("Adventure_SeVolume", 1.0f);
        if (_audioSource != null && _chimeClip != null)
        {
            _audioSource.pitch = Random.Range(0.98f, 1.05f);
            _audioSource.PlayOneShot(_chimeClip, 0.75f * seVol);
        }

        // スパークルバースト
        if (_sparklePs != null)
        {
            _sparklePs.Emit(14);
        }

        // ライトを消灯
        if (_pointLight != null) _pointLight.enabled = false;
    }

    void FinishCollection()
    {
        _isCollected = true;
        _isCollecting = false;

        // 保存
        if (!string.IsNullOrEmpty(itemId))
        {
            PlayerPrefs.SetInt("Seashell_Collected_" + itemId, 1);
            PlayerPrefs.Save();
        }

        // トースト通知＆音響・Rustリアクション発火
        if (AdventureBeachSeashellManager.Instance != null)
        {
            AdventureBeachSeashellManager.Instance.NotifyCollected(this);
        }

        gameObject.SetActive(false);
    }

    void OnGUI()
    {
        if (_isCollected || _isCollecting || !_isPlayerNear || ShouldSuppressInteraction()) return;

        // 採取プロンプトHUD（画面内スクリーン座標に小さく「【E】拾う」）
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(transform.position + Vector3.up * 0.22f);
        if (screenPos.z < 0.5f) return;

        float x = screenPos.x;
        float y = Screen.height - screenPos.y;

        var style = new GUIStyle(GUI.skin.box);
        style.fontSize = 13;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = new Color(1f, 0.95f, 0.6f);
        style.alignment = TextAnchor.MiddleCenter;

        string prompt = $"【E】{itemName}を拾う";
        Vector2 size = style.CalcSize(new GUIContent(prompt)) + new Vector2(16f, 6f);
        GUI.Box(new Rect(x - size.x * 0.5f, y - size.y * 0.5f, size.x, size.y), prompt, style);
    }
}
