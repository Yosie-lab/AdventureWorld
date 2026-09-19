using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 砂浜の波打ち際・浅瀬を泳ぐ小魚の群れコンポーネント
/// 浅瀬（水深5.0〜5.4m）をスーッと回遊し、尾びれを振って泳ぎ、
/// プレイヤーが水に入って近づくとサッと沖へ逃げるインタラクティブな群れAI
/// </summary>
public class AdventureShallowFishSchool : MonoBehaviour
{
    [Header("群れの設定")]
    [SerializeField] private int fishCount = 6;
    [SerializeField] private float swimRadius = 8f;
    [SerializeField] private float baseSpeed = 1.6f;
    [SerializeField] private float fleeSpeed = 3.8f;
    [SerializeField] private float waterSurfaceY = 5.45f;
    [SerializeField] private float swimDepth = 0.35f; // 水面下約35cmを泳ぐ

    private List<SingleFish> _fishList = new List<SingleFish>();
    private Vector3 _homeCenter;
    private Transform _playerTransform;
    private float _fleeCooldown = 0f;

    private class SingleFish
    {
        public Transform root;
        public Transform tail;
        public Vector3 velocity;
        public float tailPhase;
        public float tailSpeed;
        public float personalOffset;
    }

    public void Initialize(Material fishMat, Vector3 center, int count = 6)
    {
        _homeCenter = center;
        fishCount = count;
        transform.position = center;

        var niko = GameObject.Find("Niko");
        if (niko != null) _playerTransform = niko.transform;

        for (int i = 0; i < fishCount; i++)
        {
            var f = CreateFishObject(i, fishMat);
            _fishList.Add(f);
        }
    }

    private void Start()
    {
        if (_playerTransform == null)
        {
            var niko = GameObject.Find("Niko");
            if (niko != null) _playerTransform = niko.transform;
        }
        if (_homeCenter == Vector3.zero)
        {
            _homeCenter = transform.position;
        }
    }

    private SingleFish CreateFishObject(int index, Material fishMat)
    {
        var fishGo = new GameObject($"SmallFish_{index:D2}");
        fishGo.transform.SetParent(transform, false);

        float angle = (index / (float)fishCount) * Mathf.PI * 2f;
        float dist = Random.Range(0.5f, 2.0f);
        Vector3 localPos = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
        fishGo.transform.localPosition = localPos;

        // 魚体（Body）
        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "Body";
        body.transform.SetParent(fishGo.transform, false);
        body.transform.localScale = new Vector3(0.08f, 0.12f, 0.32f); // 流線型の小魚
        var bodyCol = body.GetComponent<Collider>();
        if (bodyCol != null) Destroy(bodyCol);
        var rend = body.GetComponent<MeshRenderer>();
        if (rend != null && fishMat != null) rend.sharedMaterial = fishMat;

        // 尾びれ（Tail）
        var tailPivot = new GameObject("TailPivot");
        tailPivot.transform.SetParent(fishGo.transform, false);
        tailPivot.transform.localPosition = new Vector3(0f, 0f, -0.14f);

        var tailFin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tailFin.name = "TailFin";
        tailFin.transform.SetParent(tailPivot.transform, false);
        tailFin.transform.localPosition = new Vector3(0f, 0f, -0.06f);
        tailFin.transform.localScale = new Vector3(0.015f, 0.12f, 0.10f);
        var tailCol = tailFin.GetComponent<Collider>();
        if (tailCol != null) Destroy(tailCol);
        var tailRend = tailFin.GetComponent<MeshRenderer>();
        if (tailRend != null && fishMat != null) tailRend.sharedMaterial = fishMat;

        // 背びれ（Dorsal Fin）
        var dorsal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dorsal.name = "DorsalFin";
        dorsal.transform.SetParent(fishGo.transform, false);
        dorsal.transform.localPosition = new Vector3(0f, 0.07f, -0.02f);
        dorsal.transform.localScale = new Vector3(0.012f, 0.06f, 0.12f);
        var dCol = dorsal.GetComponent<Collider>();
        if (dCol != null) Destroy(dCol);
        if (rend != null && fishMat != null) dorsal.GetComponent<MeshRenderer>().sharedMaterial = fishMat;

        return new SingleFish
        {
            root = fishGo.transform,
            tail = tailPivot.transform,
            velocity = fishGo.transform.forward * baseSpeed,
            tailPhase = Random.Range(0f, Mathf.PI * 2f),
            tailSpeed = Random.Range(10f, 15f),
            personalOffset = Random.Range(-0.4f, 0.4f)
        };
    }

    private void Update()
    {
        if (_fishList.Count == 0) return;

        // プレイヤー接近検知（プレイヤーが水辺5m以内かつ水深近くにいる場合）
        bool isPlayerFlee = false;
        Vector3 fleeDir = Vector3.zero;
        if (_playerTransform != null)
        {
            float distToPlayer = Vector3.Distance(_playerTransform.position, transform.position);
            // プレイヤーが浅瀬に入った、または波打ち際に近づいた
            if (distToPlayer < 4.8f && _playerTransform.position.y < 6.8f)
            {
                isPlayerFlee = true;
                fleeDir = (transform.position - _playerTransform.position);
                fleeDir.y = 0f;
                // 海側（西: Xマイナス方向）へ逃げる
                if (fleeDir.x > -0.2f) fleeDir.x = -1f;
                fleeDir.Normalize();
                _fleeCooldown = 3.5f;
            }
        }

        if (_fleeCooldown > 0f)
        {
            _fleeCooldown -= Time.deltaTime;
            isPlayerFlee = true;
        }

        // 群れの中心の更新（ゆるやかな周回または逃避）
        float t = Time.time;
        Vector3 targetCenter = _homeCenter;
        float currentSpeed = baseSpeed;

        if (isPlayerFlee)
        {
            // 沖合（西）へ一時的に逃げる
            targetCenter = _homeCenter + new Vector3(-5.5f, 0f, Mathf.Sin(t) * 2f);
            currentSpeed = fleeSpeed;
        }
        else
        {
            // 浅瀬をのんびり楕円軌道で周回
            float ox = Mathf.Sin(t * 0.4f) * (swimRadius * 0.4f);
            float oz = Mathf.Cos(t * 0.35f) * swimRadius;
            targetCenter = _homeCenter + new Vector3(ox, 0f, oz);
        }

        // 水深の高さを一定に保つ（水面下）
        targetCenter.y = waterSurfaceY - swimDepth;

        transform.position = Vector3.Lerp(transform.position, targetCenter, Time.deltaTime * (isPlayerFlee ? 2.5f : 0.8f));

        // 各個体の遊泳と尾びれのアニメーション
        for (int i = 0; i < _fishList.Count; i++)
        {
            var f = _fishList[i];
            if (f.root == null) continue;

            // 尾びれのパタパタ（スイミング）
            float currentTailSpeed = f.tailSpeed * (isPlayerFlee ? 2.2f : 1.0f);
            f.tailPhase += Time.deltaTime * currentTailSpeed;
            float tailAngle = Mathf.Sin(f.tailPhase) * (isPlayerFlee ? 32f : 18f);
            f.tail.localRotation = Quaternion.Euler(0f, tailAngle, 0f);

            // 前進と旋回
            Vector3 forward = transform.position - f.root.position;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.01f)
            {
                var targetRot = Quaternion.LookRotation(forward.normalized, Vector3.up);
                f.root.rotation = Quaternion.Slerp(f.root.rotation, targetRot, Time.deltaTime * (isPlayerFlee ? 8f : 3f));
            }

            // 個体の位置を群れ中心に追従
            f.root.position += f.root.forward * (currentSpeed * Time.deltaTime);
            // 水深キープ
            var pos = f.root.position;
            pos.y = waterSurfaceY - swimDepth + Mathf.Sin(t * 2f + f.personalOffset) * 0.04f;
            f.root.position = pos;
        }
    }
}
