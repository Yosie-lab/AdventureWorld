using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 砂浜の漂着情報ボックス（Drift Boxes）の自動生成と管理
/// 何もない砂浜を探索するプレイヤーに、島の地理・進行目標・サバイバルのヒントを提供する
/// </summary>
public class AdventureBeachDriftBoxManager : MonoBehaviour
{
    private static AdventureBeachDriftBoxManager _instance;
    public static AdventureBeachDriftBoxManager Instance => _instance;

    [System.Serializable]
    public struct DriftBoxData
    {
        public int id;
        public string title;
        public string author;
        [TextArea(3, 8)]
        public string message;
        public string rustDialogue;
        public string nextObjective;
        public Vector3 position;
        public float rotationY;
    }

    public static void Ensure()
    {
        var existing = FindObjectsByType<AdventureBeachDriftBoxManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ex in existing)
        {
            if (ex != null && ex.gameObject != null)
            {
                if (Application.isPlaying)
                    Destroy(ex.gameObject);
                else
                    DestroyImmediate(ex.gameObject);
            }
        }
        _instance = null;

        var go = new GameObject("AdventureBeachDriftBoxManager");
        _instance = go.AddComponent<AdventureBeachDriftBoxManager>();
    }

    void Awake()
    {
        _instance = this;
    }

    void Start()
    {
        SpawnAllDriftBoxes();
    }

    public void SpawnAllDriftBoxes()
    {
        // 既存のボックスがあれば削除
        var oldBoxes = transform.Find("DriftBoxesRoot");
        if (oldBoxes != null)
        {
            if (Application.isPlaying)
                Destroy(oldBoxes.gameObject);
            else
                DestroyImmediate(oldBoxes.gameObject);
        }

        var root = new GameObject("DriftBoxesRoot");
        root.transform.SetParent(transform, false);

        var land = Terrain.activeTerrain ?? FindAnyObjectByType<Terrain>();
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // マテリアルの準備
        var woodMat = new Material(shader);
        woodMat.name = "DriftBox_Wood";
        woodMat.SetColor("_BaseColor", new Color(0.60f, 0.40f, 0.24f)); // 温かみのあるチーク木目
        woodMat.SetFloat("_Smoothness", 0.45f);

        var metalMat = new Material(shader);
        metalMat.name = "DriftBox_Brass";
        metalMat.SetColor("_BaseColor", new Color(0.82f, 0.68f, 0.32f)); // 真鍮ゴールド金具
        metalMat.SetFloat("_Metallic", 0.85f);
        metalMat.SetFloat("_Smoothness", 0.75f);

        var lampMat = new Material(shader);
        lampMat.name = "DriftBox_Lamp";
        lampMat.SetColor("_BaseColor", new Color(1.0f, 0.75f, 0.25f));
        lampMat.EnableKeyword("_EMISSION");
        lampMat.SetColor("_EmissionColor", new Color(1.0f, 0.75f, 0.25f) * 3.5f);

        var boxes = GetBoxDefinitions();

        for (int i = 0; i < boxes.Length; i++)
        {
            var data = boxes[i];
            Vector3 pos = data.position;
            if (land != null)
            {
                pos.y = land.SampleHeight(pos) + land.transform.position.y - 0.08f;
            }

            BuildSingleDriftBox(root.transform, data, pos, woodMat, metalMat, lampMat);
        }
    }

    private static DriftBoxData[] GetBoxDefinitions()
    {
        return new DriftBoxData[]
        {
            new DriftBoxData
            {
                id = 1,
                title = "漂着サバイバルケース #1",
                author = "先人の漂着記録",
                message = "気がつくと見知らぬ白砂の海岸に打ち上げられていた。\n\n内陸から心地よいせせらぎが聞こえる。川を遡ると『せせらぎ池』や『カルデラ湖』があるようだ。\n\n水と古代パーツを探すなら、まずは目の前の川沿いに内陸を目指すのが良いだろう。",
                rustDialogue = "ケースの中にサバイバルメモがあるよ！川を遡ると内陸の池や湖に出られるみたいだね。川沿いに行ってみよう！",
                nextObjective = "川沿いに進んで内陸のせせらぎ池を目指そう",
                position = new Vector3(151f, 0f, 270f),
                rotationY = 45f
            },
            new DriftBoxData
            {
                id = 2,
                title = "漂着メンテナンスBOX #2",
                author = "技師の覚書",
                message = "島中に散らばる『古代パーツ』を集めれば、相棒ドローンの故障したブースターが修復されるらしい。\n\nパーツを【3個】集めると『高速ダッシュ』が解放され、広大な島を快適に駆け抜けられるようになる。\n\n空へ伸びる黄金やシアンの光の柱を探せ。",
                rustDialogue = "パーツを3個集めれば僕のブースターが直ってダッシュできるようになるんだ！周りを見渡して光の柱を探そう！",
                nextObjective = "古代パーツを3個集めてブースターダッシュを解放しよう",
                position = new Vector3(163f, 0f, 255f),
                rotationY = 25f
            },
            new DriftBoxData
            {
                id = 3,
                title = "漂着航海カプセル #3",
                author = "飛行士の記録",
                message = "砂浜と内陸の高台の間には、海風が吹き上げる『サーマル上昇気流』や、登りやすい『木道スロープ』がある。\n\n高い崖も、Spaceキーを長押しして海風に乗れば、一気に上空へ舞い上がって高台を飛び越えられるはずだ。",
                rustDialogue = "高い崖も海風の上昇気流に乗れば一気に飛べるんだ！Spaceキー長押しで風に乗ってみよう！",
                nextObjective = "海風の上昇気流や木道を使って高台へ登ろう",
                position = new Vector3(145f, 0f, 320f),
                rotationY = 110f
            },
            new DriftBoxData
            {
                id = 4,
                title = "観測機器コンテナ #4",
                author = "探査隊の記録",
                message = "東の山岳地帯には原生林『大樹海』と岩が敷き詰められた激流が広がる。\n\n古代パーツを【9個】集めると、相棒ドローンに『探知ソナー』が修復され、近くの未発見パーツを音で教えてくれるようになるという。",
                rustDialogue = "東の大樹海かぁ…！パーツを9個集めれば探知ソナーで音を使って探索できるようになるよ！",
                nextObjective = "パーツを9個集めて探知ソナーを解放しよう",
                position = new Vector3(320f, 0f, 138f),
                rotationY = 280f
            },
            new DriftBoxData
            {
                id = 5,
                title = "古びた暗号金庫 #5",
                author = "島の創設者の遺言",
                message = "島の中央にそびえる白亜のタワー…あそこの最上部には、島の空を覆う『見えない天蓋』を開放する鍵がある。\n\nすべての古代パーツ【12個】を集めた時、タワーへの道が完全に開かれ、外の世界への脱出が可能になる。",
                rustDialogue = "島の中央にそびえる巨大タワー…あそこが僕たちの最終目的地だね！全部のパーツを集めて脱出しよう！",
                nextObjective = "古代パーツを12個集めて中央タワーの天蓋を開放しよう",
                position = new Vector3(930f, 0f, 480f),
                rotationY = 205f
            }
        };
    }

    private static void BuildSingleDriftBox(Transform parent, DriftBoxData data, Vector3 pos, Material woodMat, Material metalMat, Material lampMat)
    {
        var boxGo = new GameObject($"DriftBox_{data.id}_{data.title}");
        boxGo.transform.SetParent(parent, false);
        boxGo.transform.position = pos;
        // 砂浜に少し斜めに埋まる自然なチルト
        boxGo.transform.rotation = Quaternion.Euler(3.5f, data.rotationY, -2.5f);

        // コンポーネントの設定
        var boxComp = boxGo.AddComponent<AdventureBeachDriftBox>();
        boxComp.boxId = data.id;
        boxComp.boxTitle = data.title;
        boxComp.author = data.author;
        boxComp.message = data.message;
        boxComp.rustDialogue = data.rustDialogue;
        boxComp.nextObjective = data.nextObjective;

        // 1. ボックス本体（木製チェスト）
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "BoxBody";
        body.transform.SetParent(boxGo.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.22f, 0f);
        body.transform.localScale = new Vector3(0.92f, 0.44f, 0.62f);
        body.GetComponent<Renderer>().sharedMaterial = woodMat;

        // 2. 真鍮の補強金具（左右2本のバンド帯）
        for (int b = -1; b <= 1; b += 2)
        {
            var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
            band.name = "BrassBand";
            band.transform.SetParent(boxGo.transform, false);
            band.transform.localPosition = new Vector3(b * 0.28f, 0.22f, 0f);
            band.transform.localScale = new Vector3(0.08f, 0.45f, 0.64f);
            band.GetComponent<Renderer>().sharedMaterial = metalMat;
            DestroyImmediate(band.GetComponent<Collider>());
        }

        // 3. 開閉する蓋（Lid）: ピボットを後方上部に設定
        var lidPivot = new GameObject("BoxLid");
        lidPivot.transform.SetParent(boxGo.transform, false);
        lidPivot.transform.localPosition = new Vector3(0f, 0.44f, -0.31f);

        // 蓋の木製トップ
        var lidMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lidMesh.name = "LidMesh";
        lidMesh.transform.SetParent(lidPivot.transform, false);
        lidMesh.transform.localPosition = new Vector3(0f, 0.05f, 0.31f);
        lidMesh.transform.localScale = new Vector3(0.96f, 0.10f, 0.65f);
        lidMesh.GetComponent<Renderer>().sharedMaterial = woodMat;
        DestroyImmediate(lidMesh.GetComponent<Collider>());

        // 蓋の真鍮バンド
        for (int b = -1; b <= 1; b += 2)
        {
            var lidBand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lidBand.name = "LidBand";
            lidBand.transform.SetParent(lidPivot.transform, false);
            lidBand.transform.localPosition = new Vector3(b * 0.28f, 0.05f, 0.31f);
            lidBand.transform.localScale = new Vector3(0.08f, 0.11f, 0.66f);
            lidBand.GetComponent<Renderer>().sharedMaterial = metalMat;
            DestroyImmediate(lidBand.GetComponent<Collider>());
        }

        // 蓋のゴールドバックル（正面中央の錠前金具）
        var buckle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        buckle.name = "Buckle";
        buckle.transform.SetParent(lidPivot.transform, false);
        buckle.transform.localPosition = new Vector3(0f, -0.01f, 0.64f);
        buckle.transform.localScale = new Vector3(0.16f, 0.10f, 0.04f);
        buckle.GetComponent<Renderer>().sharedMaterial = metalMat;
        DestroyImmediate(buckle.GetComponent<Collider>());

        // 4. アンテナポール＆シグナルランプ（遠くからでも目立つ目印）
        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "SignalPole";
        pole.transform.SetParent(boxGo.transform, false);
        pole.transform.localPosition = new Vector3(0.38f, 0.68f, 0.22f);
        pole.transform.localScale = new Vector3(0.025f, 0.25f, 0.025f);
        pole.GetComponent<Renderer>().sharedMaterial = metalMat;
        DestroyImmediate(pole.GetComponent<Collider>());

        var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lamp.name = "SignalLamp";
        lamp.transform.SetParent(boxGo.transform, false);
        lamp.transform.localPosition = new Vector3(0.38f, 0.94f, 0.22f);
        lamp.transform.localScale = new Vector3(0.20f, 0.20f, 0.20f);
        lamp.GetComponent<Renderer>().sharedMaterial = lampMat;
        DestroyImmediate(lamp.GetComponent<Collider>());

        // 5. 発見・開封時の華やかなスパークルパーティクル
        var pGo = new GameObject("Sparks");
        pGo.transform.SetParent(boxGo.transform, false);
        pGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        var ps = pGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1.2f;
        main.startLifetime = 1.5f;
        main.startSpeed = 3.2f;
        main.startSize = 0.16f;
        main.startColor = new Color(1.0f, 0.88f, 0.35f);

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 40) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.55f;

        var pRend = pGo.GetComponent<ParticleSystemRenderer>();
        var pMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit"));
        pMat.SetColor("_BaseColor", new Color(1.0f, 0.92f, 0.45f));
        pRend.sharedMaterial = pMat;
    }

#if UNITY_EDITOR
    [MenuItem("Adventure/Beach/Spawn All Drift Boxes")]
    public static void EditorSpawnAllBoxes()
    {
        var manager = FindFirstObjectByType<AdventureBeachDriftBoxManager>();
        if (manager == null)
        {
            var go = new GameObject("AdventureBeachDriftBoxManager");
            Undo.RegisterCreatedObjectUndo(go, "Create Drift Box Manager");
            manager = go.AddComponent<AdventureBeachDriftBoxManager>();
        }

        Undo.RegisterFullObjectHierarchyUndo(manager.gameObject, "Spawn Drift Boxes");
        manager.SpawnAllDriftBoxes();
        EditorUtility.SetDirty(manager.gameObject);
        Debug.Log("Successfully spawned 5 Adventure Beach Drift Boxes with lore, navigation objectives, and animations!");
    }

    [MenuItem("Adventure/Beach/Reset All Drift Boxes (全ボックス未開封化)")]
    public static void EditorResetAllBoxes()
    {
        AdventureBeachDriftBox.ResetAllBoxesStatic();
    }
#endif
}
