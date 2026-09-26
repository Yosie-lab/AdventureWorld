using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 浅瀬の小魚の群れ（Schooling Fish）および優雅な熱帯魚（Tropical Coral Fish）。
/// 浅瀬の海底コースティクス下を群れで回遊し、プレイヤーが近づくとサッと外海へダッシュ逃走する。
/// </summary>
public class AdventureSchoolingFish : MonoBehaviour
{
    public enum FishType
    {
        SilverMinnow,    // 銀白色に輝く小魚（群れ）
        YellowTang,      // キイロハギ（鮮やかなレモンイエロー）
        BlueTang,        // ナンヨウハギ（ロイヤルブルー＆イエロー）
        MoorishIdol      // ツノダシ（白・黄・黒のトロピカルストライプ）
    }

    public FishType fishType = FishType.SilverMinnow;
    public Vector3 schoolCenter;
    public float swimRadius = 4.0f;
    public float targetDepth = 5.2f; // 海面(5.95m)より下の遊泳高度

    private Vector3 _velocity;
    private float _speed;
    private float _tailCycle;
    private Transform _tailFin;
    private Transform _player;
    private Transform _fishVisual;

    private bool _isFleeing = false;
    private float _fleeTimer = 0f;
    private Vector3 _wanderOffset;
    private float _nextWanderTime;

    void Start()
    {
        var niko = GameObject.Find("Niko");
        if (niko != null) _player = niko.transform;

        BuildFishMesh();

        _speed = (fishType == FishType.SilverMinnow) ? Random.Range(1.8f, 2.6f) : Random.Range(1.1f, 1.7f);
        _tailCycle = Random.Range(0f, 10f);
        _velocity = transform.forward * _speed;
        _wanderOffset = Random.insideUnitSphere * swimRadius;
        _wanderOffset.y = 0f;
    }

    void BuildFishMesh()
    {
        var visGo = new GameObject("Visual");
        visGo.transform.SetParent(transform, false);
        _fishVisual = visGo.transform;

        var mf = visGo.AddComponent<MeshFilter>();
        var mr = visGo.AddComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

        float bodyScale = 1.0f;
        switch (fishType)
        {
            case FishType.SilverMinnow:
                mf.sharedMesh = CreateMinnowBodyMesh();
                mat.color = new Color(0.85f, 0.95f, 1.0f); // 銀白色・水色光沢
                mat.SetFloat("_Metallic", 0.85f);
                mat.SetFloat("_Smoothness", 0.92f);
                bodyScale = 0.22f;
                break;

            case FishType.YellowTang:
                mf.sharedMesh = CreateTangBodyMesh();
                mat.color = new Color(1.0f, 0.90f, 0.12f); // 鮮やかなキイロハギ
                mat.SetFloat("_Smoothness", 0.75f);
                bodyScale = 0.32f;
                break;

            case FishType.BlueTang:
                mf.sharedMesh = CreateTangBodyMesh();
                mat.color = new Color(0.12f, 0.42f, 0.95f); // ナンヨウハギ・コバルトブルー
                mat.SetFloat("_Smoothness", 0.78f);
                bodyScale = 0.30f;
                break;

            case FishType.MoorishIdol:
                mf.sharedMesh = CreateIdolBodyMesh();
                mat.color = new Color(0.98f, 0.92f, 0.45f); // ツノダシ
                mat.SetFloat("_Smoothness", 0.70f);
                bodyScale = 0.34f;
                break;
        }

        mr.sharedMaterial = mat;
        visGo.transform.localScale = Vector3.one * bodyScale;

        // 尾びれ（左右にしなやかに振れる別Transform）
        var tailGo = new GameObject("TailFin");
        tailGo.transform.SetParent(visGo.transform, false);
        tailGo.transform.localPosition = new Vector3(0f, 0f, -0.45f);

        var tailMf = tailGo.AddComponent<MeshFilter>();
        tailMf.sharedMesh = CreateFinMesh(fishType);
        var tailMr = tailGo.AddComponent<MeshRenderer>();
        var tailMat = new Material(mat);
        if (fishType == FishType.BlueTang)
            tailMat.color = new Color(1.0f, 0.90f, 0.15f); // ナンヨウハギの特徴的な黄色い尾びれ！
        tailMr.sharedMaterial = tailMat;
        _tailFin = tailGo.transform;
    }

    /// <summary>小魚の流線型紡錘形ボディ</summary>
    static Mesh CreateMinnowBodyMesh()
    {
        var mesh = new Mesh { name = "ProcMinnow" };
        var verts = new Vector3[]
        {
            new Vector3(0f, 0f, 0.45f),      // 0: 口先
            new Vector3(0.06f, 0.08f, 0.12f),// 1: 右上
            new Vector3(-0.06f, 0.08f, 0.12f),// 2: 左上
            new Vector3(0.06f, -0.06f, 0.12f),// 3: 右下
            new Vector3(-0.06f, -0.06f, 0.12f),// 4: 左下
            new Vector3(0.02f, 0.03f, -0.45f),// 5: 尾部右
            new Vector3(-0.02f, 0.03f, -0.45f),// 6: 尾部左
            new Vector3(0f, -0.02f, -0.45f)  // 7: 尾部下
        };

        var tris = new int[]
        {
            // 頭部
            0, 1, 2,  0, 3, 1,  0, 2, 4,  0, 4, 3,
            // 胴体
            1, 5, 2,  2, 5, 6,
            1, 3, 5,  3, 7, 5,
            2, 6, 4,  4, 6, 7,
            3, 4, 7
        };

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>ハギ系の扁平で丸みのある魚体</summary>
    static Mesh CreateTangBodyMesh()
    {
        var mesh = new Mesh { name = "ProcTang" };
        var verts = new Vector3[]
        {
            new Vector3(0f, 0f, 0.42f),      // 口先
            new Vector3(0.045f, 0.22f, 0.08f), // 背中右
            new Vector3(-0.045f, 0.22f, 0.08f),// 背中左
            new Vector3(0.045f, -0.16f, 0.08f),// 腹右
            new Vector3(-0.045f, -0.16f, 0.08f),// 腹左
            new Vector3(0.02f, 0.05f, -0.42f), // 尾柄右
            new Vector3(-0.02f, 0.05f, -0.42f),// 尾柄左
            new Vector3(0f, -0.04f, -0.42f)
        };

        var tris = new int[]
        {
            0, 1, 2,  0, 3, 1,  0, 2, 4,  0, 4, 3,
            1, 5, 2,  2, 5, 6,
            1, 3, 5,  3, 7, 5,
            2, 6, 4,  4, 6, 7,
            3, 4, 7
        };

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>ツノダシの高くそびえる背びれ魚体</summary>
    static Mesh CreateIdolBodyMesh()
    {
        var mesh = new Mesh { name = "ProcIdol" };
        var verts = new Vector3[]
        {
            new Vector3(0f, -0.02f, 0.45f),   // 突き出た細い口先
            new Vector3(0.035f, 0.45f, 0.12f),// 長く伸びる背びれアンテナ
            new Vector3(-0.035f, 0.45f, 0.12f),
            new Vector3(0.035f, -0.22f, 0.10f),
            new Vector3(-0.035f, -0.22f, 0.10f),
            new Vector3(0.015f, 0.04f, -0.38f),
            new Vector3(-0.015f, 0.04f, -0.38f),
            new Vector3(0f, -0.03f, -0.38f)
        };

        var tris = new int[]
        {
            0, 1, 2,  0, 3, 1,  0, 2, 4,  0, 4, 3,
            1, 5, 2,  2, 5, 6,
            1, 3, 5,  3, 7, 5,
            2, 6, 4,  4, 6, 7,
            3, 4, 7
        };

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>扇状の尾びれメッシュ</summary>
    static Mesh CreateFinMesh(FishType type)
    {
        var mesh = new Mesh { name = "ProcFin" };
        float spread = (type == FishType.SilverMinnow) ? 0.15f : 0.22f;
        float length = (type == FishType.SilverMinnow) ? 0.24f : 0.28f;

        var verts = new Vector3[]
        {
            new Vector3(0f, 0.02f, 0f),
            new Vector3(0f, -0.02f, 0f),
            new Vector3(0f, spread, -length),
            new Vector3(0f, -spread, -length),
            new Vector3(0f, 0f, -length * 0.7f) // V字スリット
        };

        var tris = new int[]
        {
            0, 2, 4,  0, 4, 1,  1, 4, 3, // 表面
            0, 4, 2,  0, 1, 4,  1, 3, 4  // 裏面
        };

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void Update()
    {
        UpdateAI();
        UpdateSwimmingAnimation();
    }

    void UpdateAI()
    {
        // プレイヤー接近判定
        if (_player != null)
        {
            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist < 4.2f)
            {
                _isFleeing = true;
                _fleeTimer = 4.5f;
            }
        }

        if (_isFleeing)
        {
            _fleeTimer -= Time.deltaTime;
            if (_fleeTimer <= 0f) _isFleeing = false;
        }

        Vector3 targetPos;
        float curSpeed;

        if (_isFleeing && _player != null)
        {
            // プレイヤーと反対側の外海・深海方向へダッシュ逃走
            Vector3 fleeDir = (transform.position - _player.position).normalized;
            fleeDir.y = 0f;
            targetPos = transform.position + fleeDir * 8f;
            curSpeed = _speed * 3.2f;
        }
        else
        {
            if (Time.time > _nextWanderTime)
            {
                _nextWanderTime = Time.time + Random.Range(3f, 6f);
                _wanderOffset = Random.insideUnitSphere * swimRadius;
                _wanderOffset.y = 0f;
            }
            targetPos = schoolCenter + _wanderOffset;
            curSpeed = _speed;
        }

        // 水深クランプ（海面 5.95m より下、水底より上）
        var land = Terrain.activeTerrain;
        float bedY = land != null ? (land.SampleHeight(transform.position) + land.transform.position.y) : 4.0f;
        float safeY = Mathf.Clamp(targetDepth, bedY + 0.15f, 5.75f);
        targetPos.y = safeY;

        // スムーズな遊泳旋回
        Vector3 desiredVel = (targetPos - transform.position).normalized * curSpeed;
        _velocity = Vector3.RotateTowards(_velocity, desiredVel, 4.5f * Time.deltaTime, curSpeed);

        if (_velocity.sqrMagnitude > 0.01f)
        {
            transform.position += _velocity * Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(_velocity), 6f * Time.deltaTime);
        }
    }

    void UpdateSwimmingAnimation()
    {
        float speedFactor = _isFleeing ? 3.0f : 1.0f;
        _tailCycle += Time.deltaTime * 12f * speedFactor;

        // 尾びれが左右にしなやかにスイング
        if (_tailFin != null)
        {
            float finAngle = Mathf.Sin(_tailCycle) * (_isFleeing ? 36f : 24f);
            _tailFin.localRotation = Quaternion.Euler(0f, finAngle, 0f);
        }

        // 魚体全体の微小なロール揺れ
        if (_fishVisual != null)
        {
            float bodyRoll = Mathf.Sin(_tailCycle * 0.5f) * 6f;
            float bobY = Mathf.Sin(Time.time * 2.5f) * 0.015f;
            _fishVisual.localPosition = new Vector3(0f, bobY, 0f);
            _fishVisual.localRotation = Quaternion.Euler(0f, 0f, bodyRoll);
        }
    }
}
