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
        var player = AdventurePlayerController.InstanceOrFind();
        if (player != null) _player = player.transform;

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

        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Unlit/Color");

        var bodyMat = new Material(shader) { name = $"Fish_{fishType}_BodyMat" };
        var finMat = new Material(shader) { name = $"Fish_{fishType}_FinMat" };

        Color bodyCol = Color.white;
        Color finCol = Color.white;
        Vector3 bodyScale = new Vector3(0.10f, 0.28f, 0.42f); // ふっくらとした厚みと体高のある魚体

        switch (fishType)
        {
            case FishType.SilverMinnow:
                bodyCol = new Color(0.85f, 0.95f, 1.0f); // 澄んだ銀白色
                finCol = new Color(0.90f, 0.98f, 1.0f);
                bodyScale = new Vector3(0.06f, 0.10f, 0.30f);
                break;

            case FishType.YellowTang:
                bodyCol = new Color(1.0f, 0.88f, 0.05f); // 鮮烈なキイロハギ（南国レモンイエロー）
                finCol = new Color(1.0f, 0.92f, 0.15f);
                bodyScale = new Vector3(0.09f, 0.30f, 0.40f);
                break;

            case FishType.BlueTang:
                bodyCol = new Color(0.08f, 0.42f, 0.98f); // ナンヨウハギ（鮮やかなコバルトブルー）
                finCol = new Color(1.0f, 0.88f, 0.10f);  // ナンヨウハギの特徴的な黄色いヒレ！
                bodyScale = new Vector3(0.10f, 0.28f, 0.42f);
                break;

            case FishType.MoorishIdol:
                bodyCol = new Color(0.98f, 0.95f, 0.85f); // ツノダシ（白基調）
                finCol = new Color(1.0f, 0.85f, 0.15f);
                bodyScale = new Vector3(0.08f, 0.32f, 0.38f);
                break;
        }

        bodyMat.SetColor("_BaseColor", bodyCol);
        bodyMat.color = bodyCol;
        finMat.SetColor("_BaseColor", finCol);
        finMat.color = finCol;

        // 1. 滑らかでふっくらした丸みのある魚体（Sphereベースで四角感を完全解消）
        var bodySphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bodySphere.name = "BodyMesh";
        bodySphere.transform.SetParent(visGo.transform, false);
        bodySphere.transform.localScale = bodyScale;
        var bodyRend = bodySphere.GetComponent<MeshRenderer>();
        bodyRend.sharedMaterial = bodyMat;
        bodyRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        bodyRend.receiveShadows = false;
        Destroy(bodySphere.GetComponent<Collider>());

        // 2. ツノダシ特有の黒いトロピカルストライプ帯
        if (fishType == FishType.MoorishIdol)
        {
            var stripeMat = new Material(shader) { name = "IdolStripe_Mat" };
            stripeMat.SetColor("_BaseColor", new Color(0.08f, 0.08f, 0.10f));
            stripeMat.color = new Color(0.08f, 0.08f, 0.10f);

            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stripe.name = "BodyStripe";
            stripe.transform.SetParent(visGo.transform, false);
            stripe.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            stripe.transform.localScale = new Vector3(bodyScale.x * 1.02f, 0.035f, bodyScale.y * 1.02f);
            stripe.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var stripeRend = stripe.GetComponent<MeshRenderer>();
            stripeRend.sharedMaterial = stripeMat;
            stripeRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            stripeRend.receiveShadows = false;
            Destroy(stripe.GetComponent<Collider>());
        }

        // 3. 背びれ（Dorsal Fin: 魚体の上に扇状に広がる美しいヒレ）
        var dorsalGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dorsalGo.name = "DorsalFin";
        dorsalGo.transform.SetParent(visGo.transform, false);
        if (fishType == FishType.MoorishIdol)
        {
            // ツノダシの優雅に長く伸びるツノアンテナ
            dorsalGo.transform.localPosition = new Vector3(0f, bodyScale.y * 0.70f, 0.08f);
            dorsalGo.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);
            dorsalGo.transform.localScale = new Vector3(0.015f, 0.32f, 0.045f);
        }
        else
        {
            dorsalGo.transform.localPosition = new Vector3(0f, bodyScale.y * 0.48f, -0.04f);
            dorsalGo.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            dorsalGo.transform.localScale = new Vector3(0.015f, 0.12f, bodyScale.z * 0.55f);
        }
        var dorsalRend = dorsalGo.GetComponent<MeshRenderer>();
        dorsalRend.sharedMaterial = finMat;
        dorsalRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dorsalRend.receiveShadows = false;
        Destroy(dorsalGo.GetComponent<Collider>());

        // 4. つぶらな愛らしい魚の目玉（白目ベース＋黒い瞳）
        var eyeWhiteMat = new Material(shader) { name = "FishEye_White" };
        eyeWhiteMat.SetColor("_BaseColor", Color.white);
        eyeWhiteMat.color = Color.white;

        var eyePupilMat = new Material(shader) { name = "FishEye_Pupil" };
        eyePupilMat.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.05f));
        eyePupilMat.color = new Color(0.05f, 0.05f, 0.05f);

        for (int side = -1; side <= 1; side += 2)
        {
            // 白目
            var eyeWhite = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eyeWhite.name = side < 0 ? "LeftEyeWhite" : "RightEyeWhite";
            eyeWhite.transform.SetParent(visGo.transform, false);
            eyeWhite.transform.localPosition = new Vector3(side * (bodyScale.x * 0.46f), 0.05f, bodyScale.z * 0.28f);
            eyeWhite.transform.localScale = new Vector3(0.038f, 0.046f, 0.046f);
            var ewRend = eyeWhite.GetComponent<MeshRenderer>();
            ewRend.sharedMaterial = eyeWhiteMat;
            ewRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ewRend.receiveShadows = false;
            Destroy(eyeWhite.GetComponent<Collider>());

            // 黒目（瞳）
            var pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pupil.name = "Pupil";
            pupil.transform.SetParent(eyeWhite.transform, false);
            pupil.transform.localPosition = new Vector3(side * 0.32f, 0f, 0.10f);
            pupil.transform.localScale = Vector3.one * 0.65f;
            var pRend = pupil.GetComponent<MeshRenderer>();
            pRend.sharedMaterial = eyePupilMat;
            pRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pRend.receiveShadows = false;
            Destroy(pupil.GetComponent<Collider>());
        }

        // 5. 尾びれ（左右にしなやかに振れる扇状のヒレ）
        var tailGo = new GameObject("TailFin");
        tailGo.transform.SetParent(visGo.transform, false);
        tailGo.transform.localPosition = new Vector3(0f, 0f, -bodyScale.z * 0.45f);

        var finCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        finCube.name = "TailFinBlade";
        finCube.transform.SetParent(tailGo.transform, false);
        finCube.transform.localPosition = new Vector3(0f, 0f, -0.09f);
        finCube.transform.localScale = new Vector3(0.016f, bodyScale.y * 0.65f, 0.18f);
        var tailRend = finCube.GetComponent<MeshRenderer>();
        tailRend.sharedMaterial = finMat;
        tailRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tailRend.receiveShadows = false;
        Destroy(finCube.GetComponent<Collider>());

        _tailFin = tailGo.transform;
    }

    // 浅瀬の遊泳深度定数
    public const float MaxFishSurfaceY = 5.15f; // 海面(5.50m)より深く潜らせ、水面突き抜けチカチカを防止

    void Update()
    {
        UpdateAI();
        UpdateSwimmingAnimation();
    }

    void UpdateAI()
    {
        if (_player == null)
        {
            var p = AdventurePlayerController.InstanceOrFind();
            if (p != null) _player = p.transform;
        }

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

        // 水深クランプ（海面 5.50m より下、水底より上）
        var land = Terrain.activeTerrain;
        float bedY = land != null ? (land.SampleHeight(transform.position) + land.transform.position.y) : 4.0f;
        float safeY = Mathf.Clamp(targetDepth, bedY + 0.18f, MaxFishSurfaceY); // 海面(5.50m)より深く潜らせ、水面とのチカチカ交差を完全防止
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
