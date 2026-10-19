using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 『Rust & Float』島全域の昆虫（蝶々とトンボ）エコシステム統括ディレクター。
/// ・色とりどりの極彩色蝶（モルフォ青、トパーズ黄、ローズ紅、エメラルド緑、夕焼け橙、アメジスト紫）
/// ・水辺をスイスイと滑空・ホバリングする美しいトンボ（茜色赤とんぼ、碧色シオカラトンボ、瑠璃色オニヤンマ）
/// を島中の主要な花畑・オアシス・小川・ビーチ・空中庭園に自然に配置し、
/// プレイヤー（Float/Rust）が歩く時も大空を滑空する時も、世界が生き生きと息づく圧倒的な生命感を届ける。
/// </summary>
public class AdventureIslandInsectsDirector : MonoBehaviour
{
    private static AdventureIslandInsectsDirector _instance;
    public static AdventureIslandInsectsDirector Instance => _instance;

    public const string RootObjectName = "_Island_Insects_EcoSystem";

    private const string PrefabRoot = "Assets/Idyllic Fantasy Nature/Prefabs/Code Related/";
    private static readonly string[] ButterflyPrefabPaths =
    {
        PrefabRoot + "Butterfly_01.prefab",
        PrefabRoot + "Butterfly_02.prefab",
        PrefabRoot + "Butterfly_03.prefab",
    };

    // 蝶々生息スポット（花畑・空中庭園・オアシス・原生林）
    private struct ButterflyHabitat
    {
        public string areaName;
        public Vector3 center;
        public float radius;
        public int count;
        public ButterflyHabitat(string name, Vector3 c, float r, int n)
        {
            areaName = name;
            center = c;
            radius = r;
            count = n;
        }
    }

    private static readonly ButterflyHabitat[] ButterflyHabitats = new[]
    {
        // 1. 西斜面花畑帯（冒険の始まりの斜面）
        new ButterflyHabitat("WestSlope_Meadow", new Vector3(195f, 0f, 250f), 15f, 6),
        // 2. 西側高原中央・風車大花畑
        new ButterflyHabitat("Plateau_BigFlowerField", new Vector3(245f, 0f, 315f), 18f, 7),
        // 3. 白亜サンクチュアリタワー空中庭園（東部テラス）
        new ButterflyHabitat("SanctuaryTower_SkyGarden", new Vector3(525f, 83.5f, 515f), 12f, 5),
        // 4. カルデラ古代オアシス花壇
        new ButterflyHabitat("Caldera_OasisGarden", new Vector3(215f, 0f, 185f), 14f, 5),
        // 5. 東部原生林の陽だまり木漏れ日広場
        new ButterflyHabitat("EasternForest_SunlitGlade", new Vector3(585f, 0f, 480f), 16f, 5),
        // 6. パラダイスビーチ渚花壇（座礁艇北西）
        new ButterflyHabitat("Beach_CoastalFlowers", new Vector3(155f, 0f, 265f), 10f, 4),
        // 7. せせらぎ小川上流の群生地
        new ButterflyHabitat("Brook_UpperFlowers", new Vector3(230f, 0f, 275f), 12f, 4),
    };

    // トンボ生息スポット（水辺・渚・オアシス・噴水）
    private struct DragonflyHabitat
    {
        public string areaName;
        public Vector3 center;
        public float radius;
        public int count;
        public bool isRedDragonfly; // true=赤とんぼ, false=碧色シオカラトンボ
        public DragonflyHabitat(string name, Vector3 c, float r, int n, bool red)
        {
            areaName = name;
            center = c;
            radius = r;
            count = n;
            isRedDragonfly = red;
        }
    }

    private static readonly DragonflyHabitat[] DragonflyHabitats = new[]
    {
        // 1. パラダイスビーチ渚・波打ち際
        new DragonflyHabitat("Beach_SurfLine", new Vector3(135f, 6.2f, 285f), 16f, 4, false),
        // 2. せせらぎ小川の河口〜中流
        new DragonflyHabitat("Brook_Midstream", new Vector3(210f, 6.8f, 260f), 14f, 5, true),
        // 3. カルデラ湧水オアシス池
        new DragonflyHabitat("Caldera_SpringPond", new Vector3(220f, 7.3f, 190f), 15f, 5, true),
        // 4. 南西の静かな入り江・浅瀬
        new DragonflyHabitat("SouthCove_ShallowWaters", new Vector3(175f, 6.2f, 210f), 15f, 4, false),
        // 5. 秘密のマングローブ水路
        new DragonflyHabitat("SecretCove_Waterway", new Vector3(320f, 6.2f, 160f), 14f, 4, false),
        // 6. 白亜タワー麓の清流湧水池
        new DragonflyHabitat("TowerBase_ClearSpring", new Vector3(495f, 32.5f, 485f), 12f, 4, true),
    };

    public static void AutoInitialize()
    {
        Ensure();
    }

    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = FindAnyObjectByType<AdventureIslandInsectsDirector>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("AdventureIslandInsectsDirector");
        _instance = go.AddComponent<AdventureIslandInsectsDirector>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        BuildInsectsEcoSystemIfNeeded();
    }

    /// <summary>
    /// 島全域の昆虫エコシステムを構築（未生成なら動的に生成）
    /// </summary>
    public static void BuildInsectsEcoSystemIfNeeded(bool forceRebuild = false)
    {
        var root = GameObject.Find(RootObjectName);
        if (!forceRebuild && root != null && root.transform.childCount > 0)
        {
            return; // 既に生成済み
        }

        if (root == null)
        {
            root = new GameObject(RootObjectName);
        }

        var terrain = Terrain.activeTerrain ?? FindAnyObjectByType<Terrain>();
        var rng = new System.Random(4289);

        // プレハブのロード
        GameObject[] butterflyPrefabs = LoadButterflyPrefabs();

        // 1. 蝶々の生成
        Transform butterflyGroup = root.transform.Find("Butterflies_Group");
        if (butterflyGroup == null)
        {
            var bgGo = new GameObject("Butterflies_Group");
            bgGo.transform.SetParent(root.transform, false);
            butterflyGroup = bgGo.transform;
        }

        foreach (var hab in ButterflyHabitats)
        {
            for (int i = 0; i < hab.count; i++)
            {
                Vector2 circle = RandomCirclePoint(rng, hab.radius);
                Vector3 pos = hab.center + new Vector3(circle.x, 0f, circle.y);

                float groundY = (terrain != null) ? terrain.SampleHeight(pos) + terrain.transform.position.y : hab.center.y;
                if (hab.center.y > 20f)
                {
                    // 空中庭園テラス等の人工建造物上の場合
                    pos.y = hab.center.y + 0.8f + (float)rng.NextDouble() * 0.7f;
                }
                else
                {
                    pos.y = Mathf.Max(groundY, 5.6f) + 0.8f + (float)rng.NextDouble() * 0.7f;
                }

                GameObject bGo = null;
                if (butterflyPrefabs != null && butterflyPrefabs.Length > 0)
                {
                    var p = butterflyPrefabs[(i + hab.areaName.Length) % butterflyPrefabs.Length];
                    if (p != null)
                    {
                        bGo = Instantiate(p, pos, Quaternion.identity, butterflyGroup);
                        bGo.name = $"{hab.areaName}_Butterfly_{i + 1:D2}";
                        // 遠景でも認識しやすい適正スケール（12倍）
                        bGo.transform.localScale = Vector3.one * 12f;

                        // 余分な旧スクリプトを除去
                        var oldSpawn = bGo.GetComponent<IdyllicFantasyNature.ButterflySpawn>();
                        if (oldSpawn != null) Destroy(oldSpawn);
                    }
                }

                if (bGo == null)
                {
                    // フォールバック：プロシージャルな美しい蝶
                    bGo = CreateProceduralButterfly(butterflyGroup, pos, rng);
                    bGo.name = $"{hab.areaName}_ProceduralButterfly_{i + 1:D2}";
                }

                var drift = bGo.GetComponent<AdventureButterflyDrift>() ?? bGo.AddComponent<AdventureButterflyDrift>();
                drift.radius = 1.6f + (float)rng.NextDouble() * 1.2f;
                drift.speed = 0.65f + (float)rng.NextDouble() * 0.35f;
                drift.bob = 0.40f + (float)rng.NextDouble() * 0.35f;
                drift.ApplyVividWingColor();
            }
        }

        // 2. トンボの生成
        Transform dragonflyGroup = root.transform.Find("Dragonflies_Group");
        if (dragonflyGroup == null)
        {
            var dgGo = new GameObject("Dragonflies_Group");
            dgGo.transform.SetParent(root.transform, false);
            dragonflyGroup = dgGo.transform;
        }

        foreach (var hab in DragonflyHabitats)
        {
            for (int i = 0; i < hab.count; i++)
            {
                Vector2 circle = RandomCirclePoint(rng, hab.radius);
                Vector3 pos = hab.center + new Vector3(circle.x, 0f, circle.y);
                pos.y = hab.center.y + (float)rng.NextDouble() * 0.8f;

                var dfGo = CreateDragonfly(dragonflyGroup, pos, hab.isRedDragonfly, rng);
                dfGo.name = $"{hab.areaName}_Dragonfly_{i + 1:D2}";
            }
        }

        Debug.Log($"✨ [AdventureIslandInsectsDirector] 島全域に蝶々（計{ButterflyHabitats.Length * 5}匹規模）＆トンボ（計{DragonflyHabitats.Length * 4}匹規模）のエコシステムを配置しました！");
    }

    private static Vector2 RandomCirclePoint(System.Random rng, float maxR)
    {
        double angle = rng.NextDouble() * System.Math.PI * 2.0;
        double r = System.Math.Sqrt(rng.NextDouble()) * maxR;
        return new Vector2((float)(System.Math.Cos(angle) * r), (float)(System.Math.Sin(angle) * r));
    }

    private static GameObject[] LoadButterflyPrefabs()
    {
#if UNITY_EDITOR
        var list = new List<GameObject>();
        foreach (var p in ButterflyPrefabPaths)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (asset != null) list.Add(asset);
        }
        if (list.Count > 0) return list.ToArray();
#endif
        return null;
    }

    /// <summary>
    /// プロシージャルな美しい蝶々の生成（フォールバック）
    /// </summary>
    private static GameObject CreateProceduralButterfly(Transform parent, Vector3 pos, System.Random rng)
    {
        var go = new GameObject("Butterfly");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        // 胴体
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(go.transform, false);
        body.transform.localScale = new Vector3(0.04f, 0.12f, 0.04f);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Destroy(body.GetComponent<Collider>());

        // 翅（左右）
        var wingL = GameObject.CreatePrimitive(PrimitiveType.Quad);
        wingL.name = "Wing_L";
        wingL.transform.SetParent(go.transform, false);
        wingL.transform.localPosition = new Vector3(-0.16f, 0.02f, 0f);
        wingL.transform.localScale = new Vector3(0.28f, 0.28f, 1f);
        wingL.transform.localRotation = Quaternion.Euler(15f, -20f, 0f);
        Destroy(wingL.GetComponent<Collider>());

        var wingR = GameObject.CreatePrimitive(PrimitiveType.Quad);
        wingR.name = "Wing_R";
        wingR.transform.SetParent(go.transform, false);
        wingR.transform.localPosition = new Vector3(0.16f, 0.02f, 0f);
        wingR.transform.localScale = new Vector3(0.28f, 0.28f, 1f);
        wingR.transform.localRotation = Quaternion.Euler(15f, 20f, 0f);
        Destroy(wingR.GetComponent<Collider>());

        return go;
    }

    /// <summary>
    /// 美しいプロポーショナルなトンボの生成
    /// </summary>
    private static GameObject CreateDragonfly(Transform parent, Vector3 pos, bool isRed, System.Random rng)
    {
        var go = new GameObject("Dragonfly");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        // 遠景（滑空中）でも存在感が伝わるゲームスケール（1.7倍）
        go.transform.localScale = Vector3.one * 1.7f;

        // DragonflyFlightコンポーネントを付与
        var flight = go.AddComponent<DragonflyFlight>();
        flight.RebuildDragonflyVisuals();

        // 茜色（赤とんぼ）or 碧色（シオカラトンボ）のマテリアル適用
        var modelRoot = go.transform.Find("ModelRoot");
        if (modelRoot != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var bodyMat = new Material(shader) { name = isRed ? "DragonflyBody_AkanRed" : "DragonflyBody_AzureCyan" };

            Color baseCol = isRed ? new Color(0.92f, 0.22f, 0.12f) : new Color(0.12f, 0.78f, 0.88f);
            Color emitCol = isRed ? new Color(0.85f, 0.18f, 0.08f) * 0.35f : new Color(0.08f, 0.65f, 0.82f) * 0.35f;

            bodyMat.color = baseCol;
            bodyMat.EnableKeyword("_EMISSION");
            bodyMat.SetColor("_EmissionColor", emitCol);
            if (bodyMat.HasProperty("_Smoothness")) bodyMat.SetFloat("_Smoothness", 0.90f);

            var thorax = modelRoot.Find("Thorax");
            if (thorax != null) thorax.GetComponent<Renderer>().sharedMaterial = bodyMat;

            var head = modelRoot.Find("Head");
            if (head != null) head.GetComponent<Renderer>().sharedMaterial = bodyMat;

            var abdomen = modelRoot.Find("Abdomen");
            if (abdomen != null) abdomen.GetComponent<Renderer>().sharedMaterial = bodyMat;
        }

        return go;
    }
}
