using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 白砂ビーチ＆浅瀬の海中生態系マネージャー。
/// 浅瀬（水深0.4m〜1.8m）に小魚の群れ・色とりどりの熱帯魚・サンゴ礁・揺れる海草を配置し、
/// 波打ち際の濡れ砂にチョコチョコ歩くヤドカリたちを自動配置して、息を呑む生きた海辺を創造する。
/// </summary>
public class AdventureBeachEcosystem : MonoBehaviour
{
    public static AdventureBeachEcosystem Instance { get; private set; }

    const float SeaLevel = 5.95f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        Ensure();
    }

    public static void Ensure()
    {
        if (Instance != null) return;
        var existing = Object.FindFirstObjectByType<AdventureBeachEcosystem>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject("AdventureBeachEcosystem");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AdventureBeachEcosystem>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        BuildEcosystem();
    }

    void BuildEcosystem()
    {
        // 既存のルートがあれば二重生成防止
        if (GameObject.Find("Beach_Ecosystem_Root") != null) return;

        var root = new GameObject("Beach_Ecosystem_Root");
        var land = Terrain.activeTerrain;

        // 1. サンゴ礁＆海草クラスタ（西側海岸の浅瀬、岩場周辺）
        Vector3[] reefCenters =
        {
            new Vector3(145f, 0f, 265f), // 座礁艇の沖合浅瀬
            new Vector3(138f, 0f, 315f), // 焚き火沖合
            new Vector3(158f, 0f, 215f), // 南西岬沖
            new Vector3(130f, 0f, 360f)  // 北西浅瀬
        };

        for (int i = 0; i < reefCenters.Length; i++)
        {
            Vector3 center = reefCenters[i];
            float bedY = land != null ? (land.SampleHeight(center) + land.transform.position.y) : 4.6f;
            center.y = bedY;

            SpawnCoralReefCluster(root.transform, center, i);
        }

        // 2. 小魚の群れ（銀色に光る小魚）
        Vector3[] schoolAnchors =
        {
            new Vector3(148f, 5.2f, 258f),
            new Vector3(140f, 5.1f, 305f),
            new Vector3(132f, 5.0f, 348f)
        };

        for (int s = 0; s < schoolAnchors.Length; s++)
        {
            SpawnFishSchool(root.transform, schoolAnchors[s], 12 + s * 2);
        }

        // 3. 優雅な熱帯魚（キイロハギ、ナンヨウハギ、ツノダシ）
        SpawnTropicalFishFamily(root.transform, reefCenters);

        // 4. 波打ち際のヤドカリ（濡れ砂の汀線）
        Vector3[] crabSpots =
        {
            new Vector3(155f, 0f, 276f), // 座礁艇手前の濡れ砂
            new Vector3(160f, 0f, 262f),
            new Vector3(148f, 0f, 318f), // 焚き火キャンプ手前
            new Vector3(142f, 0f, 342f),
            new Vector3(168f, 0f, 232f)  // 南岬の汀線
        };

        for (int c = 0; c < crabSpots.Length; c++)
        {
            Vector3 p = crabSpots[c];
            float y = land != null ? (land.SampleHeight(p) + land.transform.position.y) : 6.05f;
            p.y = y;
            SpawnHermitCrab(root.transform, p);
        }

        Debug.Log("[AdventureBeachEcosystem] 浅瀬の小魚の群れ・熱帯魚・サンゴ礁・ヤドカリの生態系を生成しました");
    }

    /// <summary>小魚の群れをスポーン</summary>
    void SpawnFishSchool(Transform parent, Vector3 anchor, int count)
    {
        var schoolParent = new GameObject($"FishSchool_{anchor.x:F0}_{anchor.z:F0}");
        schoolParent.transform.SetParent(parent, false);

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = anchor + Random.insideUnitSphere * 2.8f;
            pos.y = Mathf.Clamp(pos.y, 4.6f, 5.65f);

            var fishGo = new GameObject($"Minnow_{i}");
            fishGo.transform.SetParent(schoolParent.transform, false);
            fishGo.transform.position = pos;
            fishGo.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var fish = fishGo.AddComponent<AdventureSchoolingFish>();
            fish.fishType = AdventureSchoolingFish.FishType.SilverMinnow;
            fish.schoolCenter = anchor;
            fish.swimRadius = 4.2f;
            fish.targetDepth = anchor.y;
        }
    }

    /// <summary>熱帯魚ファミリー（キイロハギ、ナンヨウハギ、ツノダシ）をスポーン</summary>
    void SpawnTropicalFishFamily(Transform parent, Vector3[] reefCenters)
    {
        var tropicalParent = new GameObject("TropicalFish_Group");
        tropicalParent.transform.SetParent(parent, false);

        var fishTypes = new AdventureSchoolingFish.FishType[]
        {
            AdventureSchoolingFish.FishType.YellowTang,
            AdventureSchoolingFish.FishType.BlueTang,
            AdventureSchoolingFish.FishType.MoorishIdol
        };

        for (int r = 0; r < reefCenters.Length; r++)
        {
            Vector3 center = reefCenters[r];
            center.y = Mathf.Clamp(center.y + 0.65f, 4.8f, 5.65f);

            for (int t = 0; t < 2; t++)
            {
                var type = fishTypes[(r + t) % fishTypes.Length];
                Vector3 pos = center + Random.insideUnitSphere * 2.2f;
                pos.y = center.y + Random.Range(-0.2f, 0.2f);

                var fishGo = new GameObject($"Tropical_{type}_{r}_{t}");
                fishGo.transform.SetParent(tropicalParent.transform, false);
                fishGo.transform.position = pos;
                fishGo.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                var fish = fishGo.AddComponent<AdventureSchoolingFish>();
                fish.fishType = type;
                fish.schoolCenter = center;
                fish.swimRadius = 3.6f;
                fish.targetDepth = center.y;
            }
        }
    }

    /// <summary>サンゴ礁クラスタ（テーブルサンゴ、パイプサンゴ、揺れる海草）</summary>
    void SpawnCoralReefCluster(Transform parent, Vector3 center, int index)
    {
        var reefRoot = new GameObject($"CoralReef_{index}");
        reefRoot.transform.SetParent(parent, false);
        reefRoot.transform.position = center;

        var coralMatPink = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        coralMatPink.color = new Color(0.98f, 0.42f, 0.65f); // 桃色サンゴ
        coralMatPink.SetFloat("_Smoothness", 0.35f);

        var coralMatCyan = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        coralMatCyan.color = new Color(0.25f, 0.85f, 0.92f); // 瑠璃色サンゴ
        coralMatCyan.SetFloat("_Smoothness", 0.40f);

        var kelpMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        kelpMat.color = new Color(0.18f, 0.58f, 0.22f); // 海藻グリーン
        kelpMat.SetFloat("_Smoothness", 0.50f);

        // 1. テーブルサンゴ（平たい円盤状サンゴの重なり）
        for (int i = 0; i < 3; i++)
        {
            var coral = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coral.name = $"TableCoral_{i}";
            coral.transform.SetParent(reefRoot.transform, false);
            coral.transform.localPosition = new Vector3(Random.Range(-1.2f, 1.2f), 0.08f + i * 0.12f, Random.Range(-1.2f, 1.2f));
            coral.transform.localRotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
            coral.transform.localScale = new Vector3(Random.Range(0.8f, 1.4f), 0.045f, Random.Range(0.8f, 1.4f));
            coral.GetComponent<Renderer>().sharedMaterial = (i % 2 == 0) ? coralMatPink : coralMatCyan;
            Destroy(coral.GetComponent<Collider>());
        }

        // 2. エダサンゴ・パイプサンゴ
        for (int i = 0; i < 4; i++)
        {
            var branch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            branch.name = $"BranchCoral_{i}";
            branch.transform.SetParent(reefRoot.transform, false);
            branch.transform.localPosition = new Vector3(Random.Range(-0.8f, 0.8f), 0.22f, Random.Range(-0.8f, 0.8f));
            branch.transform.localRotation = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0f, 360f), Random.Range(-25f, 25f));
            branch.transform.localScale = new Vector3(0.08f, Random.Range(0.18f, 0.35f), 0.08f);
            branch.GetComponent<Renderer>().sharedMaterial = coralMatPink;
            Destroy(branch.GetComponent<Collider>());
        }

        // 3. 水流でゆらゆら揺れる海草（Kelp）
        for (int k = 0; k < 5; k++)
        {
            var kelp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            kelp.name = $"KelpRibbon_{k}";
            kelp.transform.SetParent(reefRoot.transform, false);
            kelp.transform.localPosition = new Vector3(Random.Range(-1.4f, 1.4f), 0.45f, Random.Range(-1.4f, 1.4f));
            kelp.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            kelp.transform.localScale = new Vector3(0.12f, Random.Range(0.7f, 1.1f), 1f);
            kelp.GetComponent<Renderer>().sharedMaterial = kelpMat;
            Destroy(kelp.GetComponent<Collider>());

            // 海藻のゆらめきスクリプトを追加
            kelp.AddComponent<AdventureKelpSway>();
        }
    }

    /// <summary>ヤドカリをスポーン</summary>
    void SpawnHermitCrab(Transform parent, Vector3 pos)
    {
        var crabGo = new GameObject("BeachHermitCrab");
        crabGo.transform.SetParent(parent, false);
        crabGo.transform.position = pos;
        crabGo.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        crabGo.AddComponent<AdventureHermitCrab>();
    }
}

/// <summary>水流に合わせて海草がゆらゆら揺れるコンポーネント</summary>
public class AdventureKelpSway : MonoBehaviour
{
    private float _phaseOffset;
    private float _swaySpeed;
    private Quaternion _baseRot;

    void Start()
    {
        _baseRot = transform.localRotation;
        _phaseOffset = transform.position.x * 2.1f + transform.position.z * 1.7f;
        _swaySpeed = Random.Range(1.3f, 1.8f);
    }

    void Update()
    {
        float swayAngle = Mathf.Sin(Time.time * _swaySpeed + _phaseOffset) * 14f;
        transform.localRotation = _baseRot * Quaternion.Euler(swayAngle, 0f, swayAngle * 0.4f);
    }
}
