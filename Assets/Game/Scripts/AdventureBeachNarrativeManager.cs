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

    /// <summary>二人の座礁漂着艇（エスケープ・ポッド）</summary>
    void CreateEscapePod(Transform parent, Vector3 pos, Terrain land)
    {
        pos = AlignToGround(pos, land, 0.4f);
        var podGo = new GameObject("NikoRust_EscapePod");
        podGo.transform.SetParent(parent, false);
        podGo.transform.position = pos;
        podGo.transform.rotation = Quaternion.Euler(14f, 65f, -8f); // 砂浜に斜めに突き刺さった座礁姿勢

        // 船体（丸みを帯びたSF脱出カプセル）
        var hull = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        hull.name = "PodHull";
        hull.transform.SetParent(podGo.transform, false);
        hull.transform.localScale = new Vector3(2.4f, 3.8f, 2.4f);
        hull.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        var ren = hull.GetComponent<Renderer>();
        if (ren != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(0.25f, 0.30f, 0.38f); // 煤けたダークスチール
            mat.SetFloat("_Metallic", 0.85f);
            mat.SetFloat("_Smoothness", 0.45f);
            ren.material = mat;
        }

        // 開いたキャノピーハッチ（脱出した痕跡）
        var hatch = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hatch.name = "OpenHatch";
        hatch.transform.SetParent(podGo.transform, false);
        hatch.transform.localPosition = new Vector3(0.8f, 0.9f, 0.5f);
        hatch.transform.localRotation = Quaternion.Euler(-35f, 25f, 15f);
        hatch.transform.localScale = new Vector3(1.2f, 0.15f, 1.8f);

        // キャビン内の微かな青いエマージェンシーランプ
        var lightGo = new GameObject("EmergencyBeacon");
        lightGo.transform.SetParent(podGo.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.5f, 0.2f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.2f, 0.85f, 1.0f);
        light.range = 6.5f;
        light.intensity = 2.2f;

        // インタラクション判定トリガー
        var trigger = podGo.AddComponent<AdventureBeachNarrativeSpot>();
        trigger.spotId = "escape_pod";
        trigger.title = "✦ 二人の漂着艇（エスケープ・カプセル） ✦";
        trigger.subTitle = "外の管理都市コード704からの脱出艇";
        trigger.bodyText =
            "砂浜に深く突き刺さった、煤と傷だらけの脱出ポッド。\n\n" +
            "【Nikoの脱出航海ログ】\n" +
            "「2050年7月14日 深夜。\n" +
            "『効率性欠如』としてスクラップ処分が決まったRustを整備ドックから連れ出し、夜の海へ飛び出した。\n" +
            "追手のアラートが鳴り響く中、真っ暗な外洋の荒波は冷たくて、死ぬほど怖かった。\n" +
            "でも、暗闇の中でRustがピピッて小さく鳴いてくれたから、前を向けた。\n\n" +
            "燃料が尽き、波に揺られてどれくらい経っただろう。\n" +
            "気がつくとこの波の静かな砂浜に打ち上げられていた。\n" +
            "見上げた空は眩しいほど青く、風に乗って蝉の声が聞こえて……\n" +
            "二人で抱き合って、声を出して泣いた」";
        trigger.rustDialogue = "「あの航海、怖かったね Niko……でも、Nikoが僕の手をずっと離さないでいてくれたから、僕の回路はショートしなかったんだよ」";

        // 漂着艇のそばに回収可能な特殊サプライ（潤滑油缶）を配置
        var oilGo = new GameObject("PodSupplyOil");
        oilGo.transform.SetParent(podGo.transform, false);
        oilGo.transform.localPosition = new Vector3(1.2f, -0.4f, 0.8f);
        var oilDrop = oilGo.AddComponent<AdventureRustOilDrop>();
        oilDrop.amount = 3; // たっぷり3個分の潤滑油
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
        _modalTitle = title;
        _modalSubTitle = subTitle;
        _modalBody = body;
        _modalRustComment = rustComment;
        _isShowingModal = true;
        _readLogIds.Add(id);

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
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
        DrawShadowed(new Rect(btnX, btnY, btnW, btnH), "【E / Space】閉じる", btnLbl, new Color(1.0f, 0.95f, 0.8f), 1.5f);

        // キー入力検知
        var kb = UnityEngine.InputSystem.Keyboard.current;
        bool closeKey = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame));
        if (clicked || closeKey)
        {
            CloseModal();
        }

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

    AudioClip MakePaperSound()
    {
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
        _playerNearby = (dist < 4.2f);

        if (_playerNearby)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame)
            {
                AdventureBeachNarrativeManager.Instance?.OpenNarrativeModal(spotId, title, subTitle, bodyText, rustDialogue);
            }
        }
    }

    void OnGUI()
    {
        if (!_playerNearby) return;

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
