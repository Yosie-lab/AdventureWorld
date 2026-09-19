using UnityEngine;

/// <summary>
/// 砂浜の波打ち際に寄せては返すリアルな白波（Lapping Waves）と海の煌めき（Sun Glitter）
/// 周期的な波の押し引きと、砂浜を濡らして引いていく波打ち際の情緒をプロシージャルに再現
/// </summary>
public class AdventureLappingWaves : MonoBehaviour
{
    [Header("Wave Settings")]
    public float cycleDuration = 5.2f;    // 波の押し引き周期（秒）
    public float advanceDistance = 2.8f;   // 砂浜へ押し寄せる距離（m）
    public float waveWidth = 8.0f;        // 1つの波セグメントの幅

    private MeshRenderer[] _waveRenderers;
    private Vector3[] _basePositions;
    private Vector3[] _shoreDirections;
    private MaterialPropertyBlock _mpb;

    void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        CacheWaveSegments();
    }

    public void CacheWaveSegments()
    {
        var renderers = GetComponentsInChildren<MeshRenderer>();
        var rList = new System.Collections.Generic.List<MeshRenderer>();
        var pList = new System.Collections.Generic.List<Vector3>();
        var dList = new System.Collections.Generic.List<Vector3>();

        foreach (var r in renderers)
        {
            if (r.gameObject.name.Contains("SurfWave"))
            {
                rList.Add(r);
                pList.Add(r.transform.position);

                // 島中心(512, 512)へ向かう向き＝砂浜へ押し寄せるベクトル
                Vector3 toCenter = (new Vector3(512f, r.transform.position.y, 512f) - r.transform.position).normalized;
                dList.Add(toCenter);
            }
        }

        _waveRenderers = rList.ToArray();
        _basePositions = pList.ToArray();
        _shoreDirections = dList.ToArray();
    }

    void Update()
    {
        if (_waveRenderers == null || _waveRenderers.Length == 0) return;

        float tTotal = Time.time;

        for (int i = 0; i < _waveRenderers.Length; i++)
        {
            var r = _waveRenderers[i];
            if (r == null) continue;

            // セグメントごとに位相を少しずらして、波がうねるように届く
            float phaseOffset = i * 0.45f;
            float cycle = Mathf.Repeat((tTotal + phaseOffset) / cycleDuration, 1.0f);

            // 押し寄せ（0.0〜0.45）は速く力強く、引き波（0.45〜1.0）はゆっくり滑らかに
            float waveT;
            float alpha;
            if (cycle < 0.45f)
            {
                // 砂浜へスーッと押し寄せる（前進）
                float p = cycle / 0.45f;
                waveT = Mathf.Sin(p * Mathf.PI * 0.5f); // イーズアウトで駆け上がる
                alpha = Mathf.Lerp(0.15f, 0.90f, Mathf.Sin(p * Mathf.PI));
            }
            else
            {
                // ゆっくり砂浜を濡らしながら引いていく（後退）
                float p = (cycle - 0.45f) / 0.55f;
                waveT = Mathf.Cos(p * Mathf.PI * 0.5f);
                alpha = Mathf.Lerp(0.85f, 0.0f, Mathf.Pow(p, 0.8f));
            }

            Vector3 baseP = _basePositions[i];
            Vector3 shoreDir = _shoreDirections[i];
            Vector3 curP = baseP + shoreDir * (waveT * advanceDistance);
            curP.y = baseP.y + waveT * 0.12f; // 砂浜の傾斜に合わせてわずかに上昇

            r.transform.position = curP;

            // 波の押し引きに合わせた薄い透明度の変化
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", new Color(1f, 1f, 1f, alpha));
            r.SetPropertyBlock(_mpb);
        }
    }
}
