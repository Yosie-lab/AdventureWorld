using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 『Rust & Float』蝶々の優雅な浮遊・旋回モーションコンポーネント。
/// ・色鮮やかで美しい羽の極彩色カラーパレット（サファイア、トパーズ、ルビー、エメラルド、タンジェリン、アメジスト）
/// ・花の上をふわりと優美に8の字に舞うホバリング軌道（花蜜を探すようなリアルで愛らしい動き）
/// ・距離カリング（36m以遠の更新・アニメーション停止）による極めて高効率な負荷削減。
/// ・Idyllic Fantasy Nature のアニメーションイベント 'AnimationEnded' の安全受信。
/// ・静的リスト ActiveButterflies により、ドローン等からのポーリング負荷を完全排除。
/// </summary>
public class AdventureButterflyDrift : MonoBehaviour
{
    public static readonly List<AdventureButterflyDrift> ActiveButterflies = new List<AdventureButterflyDrift>();

    public float radius = 2.0f;
    public float speed = 0.65f;
    public float bob = 0.45f;

    private Vector3 _home;
    private float _t;
    private Animator _animator;
    private bool _isCulled;
    private float _checkTimer;
    private bool _colorApplied;

    private const float CULL_DISTANCE_SQR = 36f * 36f; // 36m以上でカリング
    private static Transform _cameraTransform;

    // 鮮やかで美しい極彩色カラーパレット（BaseColor, EmissionColor）
    private static readonly (Color baseCol, Color emitCol)[] VividPalettes = new[]
    {
        // 1. モルフォ・サファイアブルー（澄み渡る空と青い海に映える鮮やかな青）
        (new Color(0.18f, 0.78f, 1.0f, 1f), new Color(0.10f, 0.50f, 0.95f, 1f) * 0.45f),
        // 2. 陽だまりトパーズイエロー（菜の花・ひまわりのように明るい黄金色）
        (new Color(1.0f, 0.92f, 0.15f, 1f), new Color(0.95f, 0.80f, 0.10f, 1f) * 0.45f),
        // 3. ルビー・ローズピンク（南国のハイビスカス・野バラのような鮮やかな紅）
        (new Color(1.0f, 0.28f, 0.68f, 1f), new Color(0.90f, 0.15f, 0.50f, 1f) * 0.45f),
        // 4. エメラルド・ラグーングリーン（爽やかで透き通る翡翠色）
        (new Color(0.20f, 1.0f, 0.72f, 1f), new Color(0.10f, 0.90f, 0.55f, 1f) * 0.45f),
        // 5. サンセット・タンジェリンオレンジ（南国の夕焼けのように温かく鮮やかな橙）
        (new Color(1.0f, 0.58f, 0.10f, 1f), new Color(0.95f, 0.42f, 0.05f, 1f) * 0.45f),
        // 6. アメジスト・ラベンダーパープル（高貴で幻想的な紫）
        (new Color(0.85f, 0.38f, 1.0f, 1f), new Color(0.70f, 0.20f, 0.95f, 1f) * 0.45f),
    };

    private void Awake()
    {
        _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        ActiveButterflies.Add(this);
        _home = transform.position;
        _t = Random.Range(0f, 20f);
        // 花の近くを舞う絶妙な半径（1.5m〜2.6m）
        radius = Random.Range(1.5f, 2.6f);
        bob = Random.Range(0.35f, 0.65f);
        speed = Random.Range(0.55f, 0.85f);
        _checkTimer = Random.Range(0f, 0.4f);

        // 最寄りの花にふわりと吸着・寄り添う
        SnapToNearbyFlower();

        if (!_colorApplied)
        {
            ApplyVividWingColor();
            _colorApplied = true;
        }
    }

    private void OnDisable()
    {
        ActiveButterflies.Remove(this);
    }

    private static readonly Collider[] _flowerCheckBuffer = new Collider[16];

    private void SnapToNearbyFlower()
    {
        // 周囲4m以内に花オブジェクトがあれば、その直上（0.5m上）をHomeに設定（GC Alloc ゼロ）
        int count = Physics.OverlapSphereNonAlloc(transform.position, 4.0f, _flowerCheckBuffer);
        for (int i = 0; i < count; i++)
        {
            var hit = _flowerCheckBuffer[i];
            if (hit != null && (hit.name.Contains("Flower") || (hit.transform.parent != null && hit.transform.parent.name.Contains("Flower"))))
            {
                _home = hit.transform.position + Vector3.up * 0.5f;
                break;
            }
        }
    }

    /// <summary>羽を色鮮やかに染め上げ、光の中でも美しく映えるエミッションを設定</summary>
    public void ApplyVividWingColor()
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        int idx = Random.Range(0, VividPalettes.Length);
        var pal = VividPalettes[idx];

        foreach (var rend in renderers)
        {
            if (rend == null) continue;
            var mats = rend.materials;
            if (mats == null) continue;

            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                // 羽マテリアル（Wing）または2スロット目のマテリアル
                if (m.name.Contains("Wing") || (mats.Length > 1 && i == 1))
                {
                    m.SetColor("_BaseColor", pal.baseCol);
                    m.SetColor("_Color", pal.baseCol);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", pal.emitCol);
                }
            }
            rend.materials = mats;
        }
    }

    private void Update()
    {
        // 0.3秒ごとに距離カリング判定（GC Alloc ゼロ）
        _checkTimer -= Time.deltaTime;
        if (_checkTimer <= 0f)
        {
            _checkTimer = 0.30f + Random.Range(0f, 0.1f);
            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }

            if (_cameraTransform != null)
            {
                float distSqr = (transform.position - _cameraTransform.position).sqrMagnitude;
                bool shouldCull = distSqr > CULL_DISTANCE_SQR;
                if (shouldCull != _isCulled)
                {
                    _isCulled = shouldCull;
                    if (_animator != null)
                    {
                        _animator.enabled = !_isCulled;
                    }
                }
            }
        }

        if (_isCulled) return;

        _t += Time.deltaTime * speed;

        // 花の上をふわりと優美に8の字に周回（リサージュ曲線）＋小刻みな羽ばたき揺らぎ
        float flutter = Mathf.Sin(_t * 4.5f) * 0.08f;
        Vector3 next = _home + new Vector3(
            Mathf.Sin(_t) * radius,
            0.55f + Mathf.Sin(_t * 1.7f) * bob + flutter,
            Mathf.Sin(_t * 2.0f) * (radius * 0.6f));

        Vector3 dir = next - transform.position;
        transform.position = next;
        if (dir.sqrMagnitude > 0.0004f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 6f * Time.deltaTime);
        }
    }

    /// <summary>
    /// Idyllic Fantasy Nature の蝶々FBXアニメーションクリップに設定された
    /// AnimationEvent 'AnimationEnded' のレシーバー（エラーログの大量発生を完全に防ぐ）
    /// </summary>
    public void AnimationEnded()
    {
        // アニメーション周期終端の正常受信
    }
}

