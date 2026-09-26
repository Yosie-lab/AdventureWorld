using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 『Rust & Float』砂浜ナラティブマネージャー
/// 外のAI管理社会からの脱出と、この楽園の島（箱庭サンクチュアリ）に至る物語を
/// 砂浜の探索を通じて振り返ることができるナラティブ体験を統括する。
/// </summary>
public class AdventureBeachNarrativeManager : MonoBehaviour
{
    static AdventureBeachNarrativeManager _instance;
    public static AdventureBeachNarrativeManager Instance => _instance;

    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = Object.FindAnyObjectByType<AdventureBeachNarrativeManager>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("AdventureBeachNarrativeManager");
        _instance = go.AddComponent<AdventureBeachNarrativeManager>();
    }

    // ── 読み物UI制御 ──
    bool _isShowingModal = false;
    float _ignoreCloseUntil;
    public bool IsShowingModal => _isShowingModal;
    string _modalTitle = "";
    string _modalSubTitle = "";
    string _modalBody = "";
    string _modalRustComment = "";
    AudioSource _audio;

    // ── 収集記録（セーブ連動用ハッシュセット） ──
    readonly HashSet<string> _readLogIds = new HashSet<string>();

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        SetupAudio();
    }

    void Start()
    {
        BuildBeachNarrativeWorld();
    }

    void SetupAudio()
    {
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.spatialBlend = 0f;
        _audio.playOnAwake = false;
    }

    /// <summary>砂浜全周のナラティブランドマーク・アイテムを自動構築</summary>
    void BuildBeachNarrativeWorld()
    {
        var root = new GameObject("BeachNarrativeObjects");
        root.transform.SetParent(transform, false);

        var land = Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();

        // 1. 二人の座礁漂着艇（エスケープ・カプセル）
        CreateEscapePod(root.transform, new Vector3(152f, 0f, 275f), land);

        // 2. 初日のビーチキャンプ跡（焚き火と流木シェルター）
        CreateBeachCamp(root.transform, new Vector3(185f, 0f, 335f), land);

        // 3. 外の世界の記憶（漂着メモリチップ 全5箇所）
        CreateMemoryChip(root.transform, "mem_ai_plan", "AI配給計画書（生活記録）", "2050年・都市コード704",
            "【全市民最適化日課表】\n" +
            "・起床：06:00:00（バイタル同期後、覚醒光照射）\n" +
            "・栄養摂取：06:12:00（均一合成ペースト 420kcal）\n" +
            "・最短通勤：06:30:00（指定歩道・私語及び寄り道禁止）\n" +
            "・感情補正：定期パルスにより心拍数を一定に保持\n\n" +
            "※注意：旧型整備ドローン（固体識別：Rust）は部品供給終了につき、本日22時をもってスクラップ炉へ移送。",
            "「……この通知が届いた夜、Nikoが僕の電源ケーブルを引っこ抜いて、整備室の窓を蹴破ったんだよね。すごく手が震えてたのを覚えてるよ」",
            new Vector3(140f, 0f, 360f), land);

        CreateMemoryChip(root.transform, "mem_toy_photo", "色褪せた古い写真", "2038年・管理社会の前夜",
            "砂浜の防水ケースから見つかった、印画紙の写真。\n" +
            "小さな子どもが、ピカピカに輝く真新しい球体ドローン（Rustと同型機）を抱きしめて満面の笑みを浮かべている。\n\n" +
            "裏面の手書き文字：\n" +
            "『大好きな相棒へ。大人になっても、ずっと一緒に飛ぼうね』",
            "「わぁ……僕の昔の仲間だ。昔の人はね、役に立つかどうかじゃなくて、『好きだから』って理由で僕たちを大切にしてくれていたんだ」",
            new Vector3(220f, 0f, 175f), land);

        CreateMemoryChip(root.transform, "mem_wild_seeds", "無認可の植物種子カプセル", "2046年・遺伝子統制外",
            "手のひらサイズの密閉ガラス瓶。\n" +
            "中には不揃いな形をした、小さな本物の野花の種が詰まっている。\n\n" +
            "「均一化されたプラスチック緑化都市には存在してはならない、\n" +
            "勝手に芽吹き、勝手に咲く無駄な雑草の種。\n" +
            "誰かが未来へ手渡そうと、海へ託した小さな反逆の証」",
            "「この島の花畑、すごくいい匂いがするよね。あんなに綺麗なのに、外の都市では『非効率な雑草』って呼ばれていたなんて不思議だね」",
            new Vector3(360f, 0f, 115f), land);

        CreateMemoryChip(root.transform, "mem_rain_recorder", "雨音のボイスレコーダー", "2044年・気象制御都市",
            "ボタンを押すと、ノイズ混じりのスピーカーからザーザーと激しい雨と雷の音が再生される。\n\n" +
            "男の声の録音：\n" +
            "『都市の気象ドームの外側で、本物の土砂降りに遭った。\n" +
            "服はずぶ濡れで最悪だったが……頬を伝う雨粒の冷たさに、自分が生きている実感がして、なぜか涙が止まらなかった。この音を忘れたくない』",
            "「外の街には雨も風もなかったんだ。この島で初めて本物の海風を浴びたとき、錆びた僕のセンサーがビックリして煙吹いちゃったよ」",
            new Vector3(720f, 0f, 220f), land);

        CreateMemoryChip(root.transform, "mem_world_map", "外の世界の手描き地図", "2050年代・脱出者の記録",
            "防水布に煤と泥で描かれた粗削りな地図。\n" +
            "中央に巨大な幾何学ドーム『サンクチュアリ・アイランド』が描かれ、その外側の海の果てには、広大な山脈と『未知の荒野（Wild Frontier）』が記されている。\n\n" +
            "走り書き：\n" +
            "『ドームの島は天国だが、檻の中だ。本当の世界は、空の裂け目の向こうに広がっている』",
            "「見て、Niko！この島の外側にも、こんなに広い世界があるんだ……！僕たちの翼を直して、いつかあの空の向こうへ行こうね！」",
            new Vector3(840f, 0f, 450f), land);

        // 4. 先人のメッセージボトル（全3箇所）
        CreateMessageBottle(root.transform, "bottle_sanctuary_truth", "漂着ボトル：楽園の真実",
            "コルク栓で厳重に密封された琥珀色のガラス瓶。\n\n" +
            "『見知らぬ友へ。\n" +
            "この島に辿り着いた君は、青い空と澄んだ水に救われたことだろう。\n" +
            "ここは外の過酷な都市を逃れた人々のために築かれた最後の箱庭だ。\n" +
            "飢えも病も管理もない。だが、上空をよく見てほしい。\n" +
            "あの太陽も雲も、巨大なホログラム天蓋の幻影にすぎない。\n\n" +
            "私は安らかな飼育を受け入れた。だが君がまだ本物の風を求めるなら……\n" +
            "島の中央の白亜タワーへ登り、真鍮のレバーを引いて天蓋を割れ』",
            new Vector3(810f, 0f, 600f), land);

        CreateMessageBottle(root.transform, "bottle_wings", "漂着ボトル：翼を持つ者へ",
            "透明なガラス瓶の中の羊皮紙。\n\n" +
            "『人は最短距離を走るためだけに生まれてきたのではない。\n" +
            "寄り道をして、躓き、足元に咲く名もなき花に足を止めた時、\n" +
            "初めて魂は呼吸を取り戻す。\n\n" +
            "君の隣にいる小さな機械の友を、どうか最期まで愛してやってくれ。\n" +
            "無駄の中にこそ、かけがえのない光がある』",
            new Vector3(512f, 0f, 860f), land);

        CreateMessageBottle(root.transform, "bottle_wind", "漂着ボトル：潮風の詩",
            "小さな丸い小瓶。\n\n" +
            "『砂浜に打ち寄せる波は、遠い外の世界の風を運んでくる。\n" +
            "海風に乗って翼を広げれば、身体は重力を忘れて宙に浮かぶ。\n" +
            "風を恐れるな。向かい風こそが、高く飛び立つための力なのだから』",
            new Vector3(620f, 0f, 140f), land);
    }

    /// <summary>二人の座礁漂着艇（温かみのある木造手漕ぎボート）</summary>
    void CreateEscapePod(Transform parent, Vector3 pos, Terrain land)
    {
        // 既存の古いオブジェクト（旧潜水艇ポッド等）があれば掃除
        var old = GameObject.Find("NikoRust_EscapePod");
        if (old != null) Destroy(old);
        var oldBoat = GameObject.Find("NikoRust_WoodenBoat");
        if (oldBoat != null) Destroy(oldBoat);

        pos = AlignToGround(pos, land, 0.25f);
        var boatGo = new GameObject("NikoRust_WoodenBoat");
        boatGo.transform.SetParent(parent, false);
        boatGo.transform.position = pos;
        // 波打ち際に乗り上げて優しく傾いた自然な座礁姿勢
        boatGo.transform.rotation = Quaternion.Euler(6f, 65f, -9f);

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // 1. アンティーク木材マテリアル（チーク・オーク調、温かい木目）
        var woodMat = new Material(shader);
        woodMat.name = "Boat_AgedWood";
        woodMat.SetColor("_BaseColor", new Color(0.46f, 0.33f, 0.22f));
        woodMat.SetFloat("_Smoothness", 0.32f);

        // 2. 船体内装木材マテリアル（やや明るい板材）
        var innerWoodMat = new Material(shader);
        innerWoodMat.name = "Boat_InnerPlank";
        innerWoodMat.SetColor("_BaseColor", new Color(0.56f, 0.42f, 0.28f));
        innerWoodMat.SetFloat("_Smoothness", 0.30f);

        // 3. 真鍮金具マテリアル（留め金・ランタンフレーム）
        var brassMat = new Material(shader);
        brassMat.name = "Boat_Brass";
        brassMat.SetColor("_BaseColor", new Color(0.78f, 0.62f, 0.32f));
        brassMat.SetFloat("_Metallic", 0.85f);
        brassMat.SetFloat("_Smoothness", 0.65f);

        // 4. ランタン発光マテリアル（温かい琥珀色の灯火）
        var lanternGlowMat = new Material(shader);
        lanternGlowMat.name = "Boat_LanternGlow";
        Color glowCol = new Color(1.0f, 0.76f, 0.42f);
        lanternGlowMat.SetColor("_BaseColor", glowCol);
        lanternGlowMat.EnableKeyword("_EMISSION");
        lanternGlowMat.SetColor("_EmissionColor", glowCol * 2.2f);

        // 5. 帆布・ロープマテリアル（生成りキャンバス）
        var canvasMat = new Material(shader);
        canvasMat.name = "Boat_Canvas";
        canvasMat.SetColor("_BaseColor", new Color(0.85f, 0.82f, 0.74f));
        canvasMat.SetFloat("_Smoothness", 0.20f);

        var boatRoot = new GameObject("BoatModel");
        boatRoot.transform.SetParent(boatGo.transform, false);

        // ── A. 船底（Bottom Planks / Keel） ──
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "BoatFloor";
        floor.transform.SetParent(boatRoot.transform, false);
        floor.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        floor.transform.localScale = new Vector3(1.35f, 0.12f, 3.8f);
        floor.GetComponent<Renderer>().sharedMaterial = innerWoodMat;

        // キール（竜骨：船底中央を貫く強固な角材）
        var keel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        keel.name = "BoatKeel";
        keel.transform.SetParent(boatRoot.transform, false);
        keel.transform.localPosition = new Vector3(0f, -0.06f, 0f);
        keel.transform.localScale = new Vector3(0.20f, 0.18f, 4.2f);
        keel.GetComponent<Renderer>().sharedMaterial = woodMat;

        // ── B. 船首（Bow - 尖った前部・舳先） ──
        var stem = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stem.name = "BowStem";
        stem.transform.SetParent(boatRoot.transform, false);
        stem.transform.localPosition = new Vector3(0f, 0.45f, 2.15f);
        stem.transform.localRotation = Quaternion.Euler(32f, 0f, 0f);
        stem.transform.localScale = new Vector3(0.18f, 0.95f, 0.22f);
        stem.GetComponent<Renderer>().sharedMaterial = woodMat;

        var bowDeck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bowDeck.name = "BowDeck";
        bowDeck.transform.SetParent(boatRoot.transform, false);
        bowDeck.transform.localPosition = new Vector3(0f, 0.42f, 1.75f);
        bowDeck.transform.localRotation = Quaternion.Euler(16f, 0f, 0f);
        bowDeck.transform.localScale = new Vector3(0.95f, 0.10f, 0.90f);
        bowDeck.GetComponent<Renderer>().sharedMaterial = woodMat;

        // ── C. 舷側（Gunwales / Side Planks - 左舷・右舷） ──
        var portSide = GameObject.CreatePrimitive(PrimitiveType.Cube);
        portSide.name = "PortGunwale";
        portSide.transform.SetParent(boatRoot.transform, false);
        portSide.transform.localPosition = new Vector3(-0.72f, 0.38f, 0f);
        portSide.transform.localRotation = Quaternion.Euler(0f, 0f, -14f);
        portSide.transform.localScale = new Vector3(0.12f, 0.65f, 3.85f);
        portSide.GetComponent<Renderer>().sharedMaterial = woodMat;

        var stbdSide = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stbdSide.name = "StarboardGunwale";
        stbdSide.transform.SetParent(boatRoot.transform, false);
        stbdSide.transform.localPosition = new Vector3(0.72f, 0.38f, 0f);
        stbdSide.transform.localRotation = Quaternion.Euler(0f, 0f, 14f);
        stbdSide.transform.localScale = new Vector3(0.12f, 0.65f, 3.85f);
        stbdSide.GetComponent<Renderer>().sharedMaterial = woodMat;

        var portBowSide = GameObject.CreatePrimitive(PrimitiveType.Cube);
        portBowSide.name = "PortBowSide";
        portBowSide.transform.SetParent(boatRoot.transform, false);
        portBowSide.transform.localPosition = new Vector3(-0.42f, 0.42f, 1.88f);
        portBowSide.transform.localRotation = Quaternion.Euler(8f, 22f, -12f);
        portBowSide.transform.localScale = new Vector3(0.12f, 0.60f, 0.95f);
        portBowSide.GetComponent<Renderer>().sharedMaterial = woodMat;

        var stbdBowSide = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stbdBowSide.name = "StarboardBowSide";
        stbdBowSide.transform.SetParent(boatRoot.transform, false);
        stbdBowSide.transform.localPosition = new Vector3(0.42f, 0.42f, 1.88f);
        stbdBowSide.transform.localRotation = Quaternion.Euler(8f, -22f, 12f);
        stbdBowSide.transform.localScale = new Vector3(0.12f, 0.60f, 0.95f);
        stbdBowSide.GetComponent<Renderer>().sharedMaterial = woodMat;

        // ── D. 船尾（Transom - 後部隔壁板） ──
        var transom = GameObject.CreatePrimitive(PrimitiveType.Cube);
        transom.name = "Transom";
        transom.transform.SetParent(boatRoot.transform, false);
        transom.transform.localPosition = new Vector3(0f, 0.36f, -1.90f);
        transom.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
        transom.transform.localScale = new Vector3(1.30f, 0.62f, 0.12f);
        transom.GetComponent<Renderer>().sharedMaterial = woodMat;

        // ── E. 座席ベンチ（Thwarts） ──
        var centerSeat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        centerSeat.name = "CenterSeat";
        centerSeat.transform.SetParent(boatRoot.transform, false);
        centerSeat.transform.localPosition = new Vector3(0f, 0.32f, 0.15f);
        centerSeat.transform.localScale = new Vector3(1.32f, 0.08f, 0.45f);
        centerSeat.GetComponent<Renderer>().sharedMaterial = innerWoodMat;

        var sternSeat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sternSeat.name = "SternSeat";
        sternSeat.transform.SetParent(boatRoot.transform, false);
        sternSeat.transform.localPosition = new Vector3(0f, 0.30f, -1.40f);
        sternSeat.transform.localScale = new Vector3(1.22f, 0.08f, 0.42f);
        sternSeat.GetComponent<Renderer>().sharedMaterial = innerWoodMat;

        // ── F. 手漕ぎオール2本（Wooden Oars） ──
        CreateOar(boatRoot.transform, new Vector3(-0.78f, 0.42f, 0.25f), Quaternion.Euler(22f, 65f, -32f), woodMat, innerWoodMat);
        CreateOar(boatRoot.transform, new Vector3(0.35f, 0.28f, -0.45f), Quaternion.Euler(-12f, 15f, 8f), woodMat, innerWoodMat);

        // ── G. 航海用真鍮ランタン（温かい琥珀色の明かり） ──
        var lanternGo = new GameObject("NavLantern");
        lanternGo.transform.SetParent(boatRoot.transform, false);
        lanternGo.transform.localPosition = new Vector3(0f, 0.62f, 1.60f);

        var lanternFrame = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lanternFrame.name = "LanternFrame";
        lanternFrame.transform.SetParent(lanternGo.transform, false);
        lanternFrame.transform.localScale = new Vector3(0.20f, 0.22f, 0.20f);
        lanternFrame.GetComponent<Renderer>().sharedMaterial = brassMat;
        Destroy(lanternFrame.GetComponent<Collider>());

        var lanternGlass = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lanternGlass.name = "LanternGlass";
        lanternGlass.transform.SetParent(lanternGo.transform, false);
        lanternGlass.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        lanternGlass.transform.localScale = new Vector3(0.16f, 0.24f, 0.16f);
        lanternGlass.GetComponent<Renderer>().sharedMaterial = lanternGlowMat;
        Destroy(lanternGlass.GetComponent<Collider>());

        var light = lanternGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = glowCol;
        light.range = 5.5f;
        light.intensity = 1.85f;

        // ── H. たたんだ帆布ロール（旅の荷物） ──
        var canvasRoll = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        canvasRoll.name = "FadedCanvasRoll";
        canvasRoll.transform.SetParent(boatRoot.transform, false);
        canvasRoll.transform.localPosition = new Vector3(-0.25f, 0.22f, -1.05f);
        canvasRoll.transform.localRotation = Quaternion.Euler(0f, 35f, 90f);
        canvasRoll.transform.localScale = new Vector3(0.26f, 0.55f, 0.26f);
        canvasRoll.GetComponent<Renderer>().sharedMaterial = canvasMat;
        Destroy(canvasRoll.GetComponent<Collider>());

        // 個別パーツのコライダーを掃除し、船体全体を包む滑らかなBoxColliderを配置
        foreach (var col in boatRoot.GetComponentsInChildren<Collider>())
        {
            Destroy(col);
        }
        var mainCol = boatGo.AddComponent<BoxCollider>();
        mainCol.center = new Vector3(0f, 0.35f, 0f);
        mainCol.size = new Vector3(1.85f, 0.85f, 4.3f);

        // インタラクション判定トリガー
        var trigger = boatGo.AddComponent<AdventureBeachNarrativeSpot>();
        trigger.spotId = "escape_boat";
        trigger.title = "✦ 二人の漂着艇（木造手漕ぎボート） ✦";
        trigger.subTitle = "外の管理都市コード704から海を渡ってきた小さな木の小舟";
        trigger.bodyText =
            "潮風と荒波に耐え抜いた、小さな木造の手漕ぎボート。\n\n" +
            "【Nikoの航海日誌】\n" +
            "「2050年7月14日 深夜。\n" +
            "『効率性欠如』としてスクラップ処分が決まったRustを整備ドックから連れ出し、夜の海へ漕ぎ出した。\n" +
            "小さな木造ボートで真っ暗な外洋に出たときは、冷たい荒波が打ち寄せて死ぬほど怖かった。\n" +
            "でも、暗闇の中でRustが小さな体で船首に座って、ランタンの灯りで前を照らし続けてくれたから、迷わずに漕ぎ続けられた。\n\n" +
            "オールを握る手の豆がつぶれ、波に揺られてどれくらい経っただろう。\n" +
            "気がつくとこの波の静かな美しい白砂ビーチに打ち上げられていた。\n" +
            "見上げた空は眩しいほど青く、風に乗って蝉の声が聞こえて……\n" +
            "二人で抱き合って、声を出して泣いた」";
        trigger.rustDialogue = "「あの航海、怖かったね Niko……でも、Nikoが一生懸命オールを漕いで僕を守ってくれたから、僕の回路はショートしなかったんだよ」";

        // 漂着艇のそばに回収可能な特殊サプライ（潤滑油缶）を配置
        var oilGo = new GameObject("PodSupplyOil");
        oilGo.transform.SetParent(boatGo.transform, false);
        oilGo.transform.localPosition = new Vector3(1.3f, 0.1f, 0.6f);
        var oilDrop = oilGo.AddComponent<AdventureRustOilDrop>();
        oilDrop.amount = 3; // たっぷり3個分の潤滑油
    }

    /// <summary>木製手漕ぎオールの生成ヘルパー</summary>
    void CreateOar(Transform parent, Vector3 localPos, Quaternion localRot, Material shaftMat, Material bladeMat)
    {
        var oarGo = new GameObject("WoodenOar");
        oarGo.transform.SetParent(parent, false);
        oarGo.transform.localPosition = localPos;
        oarGo.transform.localRotation = localRot;

        // 長い木製シャフト（柄）
        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "OarShaft";
        shaft.transform.SetParent(oarGo.transform, false);
        shaft.transform.localScale = new Vector3(0.06f, 1.25f, 0.06f);
        shaft.GetComponent<Renderer>().sharedMaterial = shaftMat;
        Destroy(shaft.GetComponent<Collider>());

        // 水かきブレード（櫂の平らな先端）
        var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade.name = "OarBlade";
        blade.transform.SetParent(oarGo.transform, false);
        blade.transform.localPosition = new Vector3(0f, -1.25f, 0f);
        blade.transform.localScale = new Vector3(0.24f, 0.65f, 0.04f);
        blade.GetComponent<Renderer>().sharedMaterial = bladeMat;
        Destroy(blade.GetComponent<Collider>());
    }

    /// <summary>初日のビーチキャンプ（焚き火と流木シェルター）</summary>
    void CreateBeachCamp(Transform parent, Vector3 pos, Terrain land)
    {
        pos = AlignToGround(pos, land, 0.1f);
        var campGo = new GameObject("BeachCamp_Shelter");
        campGo.transform.SetParent(parent, false);
        campGo.transform.position = pos;

        // 焚き火の囲み石
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stone.transform.SetParent(campGo.transform, false);
            stone.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.75f, 0.1f, Mathf.Sin(angle) * 0.75f);
            stone.transform.localScale = new Vector3(0.35f, 0.25f, 0.35f);
            var ren = stone.GetComponent<Renderer>();
            if (ren != null) ren.material.color = new Color(0.2f, 0.2f, 0.22f);
        }

        // 灰と燃えさしの微かな暖色グロー
        var fireLight = new GameObject("EmberLight").AddComponent<Light>();
        fireLight.transform.SetParent(campGo.transform, false);
        fireLight.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        fireLight.type = LightType.Point;
        fireLight.color = new Color(1.0f, 0.65f, 0.25f);
        fireLight.range = 4.5f;
        fireLight.intensity = 1.6f;

        // 流木の簡易ベンチ
        var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        log.transform.SetParent(campGo.transform, false);
        log.transform.localPosition = new Vector3(0f, 0.25f, 1.4f);
        log.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        log.transform.localScale = new Vector3(0.45f, 1.2f, 0.45f);
        var lRen = log.GetComponent<Renderer>();
        if (lRen != null) lRen.material.color = new Color(0.38f, 0.28f, 0.18f);

        var trigger = campGo.AddComponent<AdventureBeachNarrativeSpot>();
        trigger.spotId = "beach_camp";
        trigger.title = "✦ 漂着初日のビーチキャンプ（追憶の焚き火） ✦";
        trigger.subTitle = "二人が初めて火を囲んで眠った場所";
        trigger.bodyText =
            "流木と石で組まれた小さな焚き火跡。\n\n" +
            "島に漂着した初日の夜、服を乾かし、震える肩を寄せ合って火を灯した。\n" +
            "外の都市では見ることのできなかった無数の星と、静かな波の音。\n" +
            "「僕たち、本当に逃げてこられたんだね」\n" +
            "二つの影が、火の粉と一緒に夜空へ揺れていた。";
        trigger.rustDialogue = "「あの日、ここで食べた保存食、半分こしたね。少し砂の味がしたけど、今まで食べたどのオイルよりも温かかったよ」";
    }

    /// <summary>外の世界の記憶チップ</summary>
    void CreateMemoryChip(Transform parent, string id, string title, string subTitle, string body, string rustVoice, Vector3 pos, Terrain land)
    {
        pos = AlignToGround(pos, land, 0.25f);
        var chipGo = new GameObject("MemChip_" + id);
        chipGo.transform.SetParent(parent, false);
        chipGo.transform.position = pos;

        // 青くきらめくナノチップの結晶
        var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crystal.transform.SetParent(chipGo.transform, false);
        crystal.transform.localScale = new Vector3(0.4f, 0.6f, 0.25f);
        crystal.transform.localRotation = Quaternion.Euler(25f, 40f, 15f);

        var ren = crystal.GetComponent<Renderer>();
        if (ren != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(0.2f, 0.85f, 1.0f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.1f, 0.6f, 1.0f) * 1.5f);
            ren.material = mat;
        }

        // 光彩ライト
        var pl = chipGo.AddComponent<Light>();
        pl.type = LightType.Point;
        pl.color = new Color(0.3f, 0.85f, 1.0f);
        pl.range = 3.5f;
        pl.intensity = 1.8f;

        var trigger = chipGo.AddComponent<AdventureBeachNarrativeSpot>();
        trigger.spotId = id;
        trigger.title = "✦ " + title + " ✦";
        trigger.subTitle = subTitle;
        trigger.bodyText = body;
        trigger.rustDialogue = rustVoice;
        trigger.isMemoryItem = true;
    }

    /// <summary>先人のメッセージボトル</summary>
    void CreateMessageBottle(Transform parent, string id, string title, string body, Vector3 pos, Terrain land)
    {
        pos = AlignToGround(pos, land, 0.2f);
        var bottleGo = new GameObject("Bottle_" + id);
        bottleGo.transform.SetParent(parent, false);
        bottleGo.transform.position = pos;

        // ガラス瓶モデル
        var bottle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bottle.transform.SetParent(bottleGo.transform, false);
        bottle.transform.localScale = new Vector3(0.25f, 0.45f, 0.25f);
        bottle.transform.localRotation = Quaternion.Euler(65f, 15f, 0f);

        var ren = bottle.GetComponent<Renderer>();
        if (ren != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(0.85f, 0.95f, 0.8f, 0.65f); // 半透明グリーンガラス
            mat.SetFloat("_Smoothness", 0.95f);
            ren.material = mat;
        }

        var pl = bottleGo.AddComponent<Light>();
        pl.type = LightType.Point;
        pl.color = new Color(0.9f, 0.95f, 0.5f);
        pl.range = 3.0f;
        pl.intensity = 1.4f;

        var trigger = bottleGo.AddComponent<AdventureBeachNarrativeSpot>();
        trigger.spotId = id;
        trigger.title = "✦ " + title + " ✦";
        trigger.subTitle = "かつてこの島を訪れた先人のボトルレター";
        trigger.bodyText = body;
        trigger.rustDialogue = "「海を越えて届いた手紙だ……遠い昔の人も、この砂浜で空を見上げていたんだね」";
        trigger.isBottle = true;
    }

    Vector3 AlignToGround(Vector3 pos, Terrain land, float offset)
    {
        if (Physics.Raycast(new Vector3(pos.x, 150f, pos.z), Vector3.down, out RaycastHit hit, 200f))
        {
            pos.y = hit.point.y + offset;
        }
        else if (land != null)
        {
            pos.y = land.SampleHeight(pos) + land.transform.position.y + offset;
        }
        else
        {
            pos.y = 6.2f + offset;
        }
        return pos;
    }

    /// <summary>ナラティブモーダルを開く</summary>
    public void OpenNarrativeModal(string id, string title, string subTitle, string body, string rustComment)
    {
        // 漂着サバイバルケースボードが開いている時はナラティブモーダルを開かない（画面重なり完全防止）
        if (AdventureBeachDriftBox.IsModalOpen)
            return;

        _modalTitle = title;
        _modalSubTitle = subTitle;
        _modalBody = body;
        _modalRustComment = rustComment;
        _isShowingModal = true;
        _readLogIds.Add(id);
        _ignoreCloseUntil = Time.unscaledTime + 0.28f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (_audio != null)
        {
            _audio.PlayOneShot(MakePaperSound(), 0.8f);
        }

        if (!string.IsNullOrEmpty(rustComment))
        {
            var drone = AdventureRustDrone.Instance ?? Object.FindAnyObjectByType<AdventureRustDrone>();
            drone?.SpeakCustom(rustComment, 6.0f);
        }
    }

    public void CloseModal()
    {
        _isShowingModal = false;
        if (!AdventureStoryFlow.WantsFreeCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void OnGUI()
    {
        if (!_isShowingModal) return;

        // 全画面の薄い半透明オーバーレイ
        GUI.color = new Color(0.01f, 0.02f, 0.04f, 0.45f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        // 中央の物語ボード（幅960px, 高さ520px）
        float bw = Mathf.Min(960f, Screen.width * 0.94f);
        float bh = Mathf.Min(530f, Screen.height * 0.88f);
        float bx = (Screen.width - bw) * 0.5f;
        float by = (Screen.height - bh) * 0.5f;

        // 深藍色の半透明ダークガラス（透明度を上げて背後の景色を残す）
        GUI.color = new Color(0.02f, 0.05f, 0.10f, 0.68f);
        GUI.DrawTexture(new Rect(bx, by, bw, bh), Texture2D.whiteTexture);

        // 黄金とエメラルドシアンのフレーム
        GUI.color = new Color(0.35f, 0.90f, 0.98f, 0.95f);
        GUI.DrawTexture(new Rect(bx, by, bw, 3.5f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(bx, by + bh - 3.5f, bw, 3.5f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(bx, by, 3.5f, bh), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(bx + bw - 3.5f, by, 3.5f, bh), Texture2D.whiteTexture);

        // 1. タイトル
        var titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 32;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        DrawShadowed(new Rect(bx + 20f, by + 22f, bw - 40f, 42f), _modalTitle, titleStyle, new Color(1.0f, 0.88f, 0.40f), 1.5f);

        // 2. サブタイトル（年代・出所）
        var subStyle = new GUIStyle(GUI.skin.label);
        subStyle.fontSize = 18;
        subStyle.fontStyle = FontStyle.Normal;
        subStyle.alignment = TextAnchor.MiddleCenter;
        DrawShadowed(new Rect(bx + 20f, by + 66f, bw - 40f, 28f), "— " + _modalSubTitle + " —", subStyle, new Color(0.4f, 0.9f, 1.0f), 1.2f);

        // 3. 本文
        var bodyStyle = new GUIStyle(GUI.skin.label);
        bodyStyle.fontSize = 22;
        bodyStyle.fontStyle = FontStyle.Normal;
        bodyStyle.wordWrap = true;
        bodyStyle.alignment = TextAnchor.UpperLeft;
        DrawShadowed(new Rect(bx + 45f, by + 105f, bw - 90f, bh - 200f), _modalBody, bodyStyle, new Color(0.95f, 0.98f, 1.0f), 1.5f);

        // 4. 閉じるボタン
        float btnW = 320f;
        float btnH = 55f;
        float btnX = (Screen.width - btnW) * 0.5f;
        float btnY = by + bh - 72f;

        GUI.color = new Color(0.12f, 0.40f, 0.75f, 0.90f);
        GUI.DrawTexture(new Rect(btnX, btnY, btnW, btnH), Texture2D.whiteTexture);
        GUI.color = new Color(1.0f, 0.85f, 0.40f, 1.0f);
        GUI.DrawTexture(new Rect(btnX, btnY, btnW, 2.5f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(btnX, btnY + btnH - 2.5f, btnW, 2.5f), Texture2D.whiteTexture);

        var btnStyle = new GUIStyle(GUI.skin.button);
        btnStyle.fontSize = 22;
        btnStyle.fontStyle = FontStyle.Bold;
        btnStyle.alignment = TextAnchor.MiddleCenter;
        btnStyle.normal.background = Texture2D.whiteTexture;

        GUI.color = new Color(0f, 0f, 0f, 0.01f);
        bool clicked = GUI.Button(new Rect(btnX, btnY, btnW, btnH), GUIContent.none, btnStyle);

        var btnLbl = new GUIStyle(GUI.skin.label);
        btnLbl.fontSize = 22;
        btnLbl.fontStyle = FontStyle.Bold;
        btnLbl.alignment = TextAnchor.MiddleCenter;
        DrawShadowed(new Rect(btnX, btnY, btnW, btnH), "【E / Space / クリック】閉じる", btnLbl, new Color(1.0f, 0.95f, 0.8f), 1.5f);

        if (Time.unscaledTime < _ignoreCloseUntil)
        {
            GUI.color = Color.white;
            return;
        }

        var kb = UnityEngine.InputSystem.Keyboard.current;
        bool closeKey = kb != null && (kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame);
        var mouse = UnityEngine.InputSystem.Mouse.current;
        bool pointer = mouse != null && mouse.leftButton.wasPressedThisFrame;
        if (clicked || closeKey || pointer)
            CloseModal();

        GUI.color = Color.white;
    }

    static void DrawShadowed(Rect rect, string text, GUIStyle style, Color textColor, float offset = 1.5f)
    {
        Color orig = style.normal.textColor;
        style.normal.textColor = new Color(0f, 0f, 0f, 0.90f);
        GUI.Label(new Rect(rect.x + offset, rect.y + offset, rect.width, rect.height), text, style);
        style.normal.textColor = textColor;
        GUI.Label(rect, text, style);
        style.normal.textColor = orig;
    }

    static AudioClip _cachedPaperClip;

    static AudioClip MakePaperSound()
    {
        if (_cachedPaperClip != null) return _cachedPaperClip;
        int rate = 22050;
        float duration = 0.22f;
        int count = (int)(rate * duration);
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 22f);
            samples[i] = noise * 0.45f;
        }
        var clip = AudioClip.Create("PaperFlip", count, 1, rate, false);
        clip.SetData(samples, 0);
        _cachedPaperClip = clip;
        return clip;
    }
}

/// <summary>砂浜ナラティブスポットのインタラクションコンポーネント</summary>
public class AdventureBeachNarrativeSpot : MonoBehaviour
{
    public string spotId;
    public string title;
    public string subTitle;
    [TextArea] public string bodyText;
    public string rustDialogue;
    public bool isMemoryItem = false;
    public bool isBottle = false;

    bool _playerNearby = false;

    void Update()
    {
        var player = AdventurePlayerController.Instance;
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.transform.position);
        _playerNearby = (dist < 4.2f) && !AdventureBeachDriftBox.IsModalOpen;

        if (_playerNearby)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame)
            {
                if (!AdventureBeachDriftBox.IsModalOpen)
                    AdventureBeachNarrativeManager.Instance?.OpenNarrativeModal(spotId, title, subTitle, bodyText, rustDialogue);
            }
        }
    }

    void OnGUI()
    {
        if (!_playerNearby || AdventureBeachDriftBox.IsModalOpen) return;

        // プレイヤー頭上に【Eキー】調べるHUDを表示
        Vector3 screenPos = Camera.main != null ? Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.2f) : Vector3.zero;
        if (screenPos.z < 0.5f) return;

        float w = 360f;
        float h = 48f;
        float x = screenPos.x - w * 0.5f;
        float y = Screen.height - screenPos.y - h;

        // 背景
        GUI.color = new Color(0.02f, 0.05f, 0.10f, 0.88f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

        GUI.color = new Color(0.35f, 0.90f, 1.0f, 0.9f);
        GUI.DrawTexture(new Rect(x, y + h - 2f, w, 2f), Texture2D.whiteTexture);

        var style = new GUIStyle(GUI.skin.label);
        style.fontSize = 20;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;

        string prompt = "【Eキー】" + (isMemoryItem ? "記憶チップを再生" : (isBottle ? "手紙を読む" : "調べる"));
        GUI.color = Color.white;

        // 影
        style.normal.textColor = Color.black;
        GUI.Label(new Rect(x + 1.5f, y + 1.5f, w, h), prompt, style);
        style.normal.textColor = new Color(1.0f, 0.92f, 0.5f);
        GUI.Label(new Rect(x, y, w, h), prompt, style);
    }
}
