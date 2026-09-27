using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 白砂ビーチ全域における漂着ゴミ（2030〜2050年代の年代グラデーション）の自動配置と管理
/// </summary>
public class AdventureBeachFlotsamManager : MonoBehaviour
{
    public static AdventureBeachFlotsamManager Instance { get; private set; }

    // 調査ログHUD
    string _inspectTitle = "";
    string _inspectEra = "";
    string _inspectDesc = "";
    float _inspectTimer = 0f;
    Texture2D _hudBg;

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
        SpawnFlotsamItems();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void Ensure()
    {
        if (Instance != null) return;
        var existing = Object.FindAnyObjectByType<AdventureBeachFlotsamManager>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject("BeachFlotsamManager");
        Instance = go.AddComponent<AdventureBeachFlotsamManager>();
    }

    /// <summary>漂着ゴミの調査HUDを表示</summary>
    public void ShowInspectHUD(string title, string era, string desc)
    {
        _inspectTitle = title;
        _inspectEra = era;
        _inspectDesc = desc;
        _inspectTimer = 6.0f;
    }

    void Update()
    {
        if (_inspectTimer > 0f)
            _inspectTimer -= Time.deltaTime;
    }

    void SpawnFlotsamItems()
    {
        // 既存の漂着ゴミがある場合は重複生成を回避
        if (Object.FindObjectsByType<AdventureBeachFlotsam>(FindObjectsSortMode.None).Length > 0)
            return;

        Terrain terrain = Object.FindAnyObjectByType<Terrain>();

        // 漂着ゴミの定義リスト（2030年代〜2050年代の年代グラデーション）
        var items = new List<FlotsamDef>
        {
            // === 2050年代（波打ち際：最新AI管理社会の破片） ===
            new FlotsamDef {
                id = "flotsam_2050_1",
                name = "AIドローンの破損プロペラ",
                era = AdventureBeachFlotsam.FlotsamEra.Era2050,
                eraLabel = "2050年製・最新航空規格",
                description = "軽量カーボン複合材のブレード。自動配達ネットワークの末端機体のもの。",
                dialogue = "ぼくの羽と同じ素材のプロペラだ…でも、もう動かないみたい",
                angleDeg = 195f, // 西〜南西ビーチ
                distRatio = 0.98f // 最も海に近い波打ち際
            },
            new FlotsamDef {
                id = "flotsam_2050_2",
                name = "生体追跡IDリング",
                era = AdventureBeachFlotsam.FlotsamEra.Era2050,
                eraLabel = "2050年代・住民認証端末",
                description = "微弱な青い発光ダイオードが点滅を続けている。個人の心拍と位置を中央AIに送信していた。",
                dialogue = "住民登録の輪っかだね。これで毎日居場所を監視されていたんだ…",
                angleDeg = 210f,
                distRatio = 0.97f
            },
            new FlotsamDef {
                id = "flotsam_2050_3",
                name = "生分解性AIカプセル",
                era = AdventureBeachFlotsam.FlotsamEra.Era2050,
                eraLabel = "2049年・自律補給容器",
                description = "波に洗われて中身は空っぽだが、表面のナノコーティングが朝露を弾いている。",
                dialogue = "自律補給カプセルだよ。海を越えてここまで流れてきたんだね",
                angleDeg = 225f,
                distRatio = 0.96f
            },

            // === 2040年代（砂浜中央：管理社会の全盛期） ===
            new FlotsamDef {
                id = "flotsam_2040_1",
                name = "AI健康最適化バンド",
                era = AdventureBeachFlotsam.FlotsamEra.Era2040,
                eraLabel = "2042年・生体管理機器",
                description = "「体調不良を事前に検知し出勤を最適化する」と刻まれた半透明樹脂バンド。",
                dialogue = "心拍数を測るバンドだ…身につけていると息が詰まりそうだね、Niko",
                angleDeg = 200f,
                distRatio = 0.91f // 砂浜の真ん中
            },
            new FlotsamDef {
                id = "flotsam_2040_2",
                name = "規格化合成樹脂ボトル",
                era = AdventureBeachFlotsam.FlotsamEra.Era2040,
                eraLabel = "2040年・統一飲料規格",
                description = "文字やロゴの一切ない純白の容器。栄養価だけが完全に均一化された世界の痕跡。",
                dialogue = "ラベルが何もない真っ白な容器だ。すべてが均一な世界のものだね",
                angleDeg = 215f,
                distRatio = 0.92f
            },
            new FlotsamDef {
                id = "flotsam_2040_3",
                name = "旧型物流ロボットの摩耗ギア",
                era = AdventureBeachFlotsam.FlotsamEra.Era2040,
                eraLabel = "2039年・オートメーション部品",
                description = "歯車の一部が欠けたチタン合金製ギア。砂が噛んで回転が止まっている。",
                dialogue = "錆びてる…でも、少し油をさしてあげたら動きそうかな？",
                angleDeg = 230f,
                distRatio = 0.90f
            },

            // === 2030年代（砂浜奥・草むら境界・岩陰：アナログな前時代） ===
            new FlotsamDef {
                id = "flotsam_2030_1",
                name = "ひび割れた旧世代スマートフォン",
                era = AdventureBeachFlotsam.FlotsamEra.Era2030,
                eraLabel = "2031年・パーソナルデバイス",
                description = "前面ガラスがクモの巣状に割れた薄型端末。カメラレンズが砂粒に削られている。",
                dialogue = "四角いガラスの板…昔の人は指で触って、遠くの誰かと繋がっていたんだね",
                angleDeg = 205f,
                distRatio = 0.84f // 草原や木立との境界
            },
            new FlotsamDef {
                id = "flotsam_2030_2",
                name = "有線イヤホンの残骸",
                era = AdventureBeachFlotsam.FlotsamEra.Era2030,
                eraLabel = "2033年・アナログ音響器具",
                description = "もつれた白い被覆コードと小さな金属プラグ。直接耳に音を届けていた前時代の遺品。",
                dialogue = "細い線だ…これで耳に直接、人の声や音楽を届けていたんだね",
                angleDeg = 220f,
                distRatio = 0.83f
            },
            new FlotsamDef {
                id = "flotsam_2030_3",
                name = "磁気カセットテープ",
                era = AdventureBeachFlotsam.FlotsamEra.Era2030,
                eraLabel = "2030年・前時代のアナログ記録",
                description = "風化したプラスチックケースから茶色い磁気テープが少し解け出ている。手書きの文字が微かに残る。",
                dialogue = "茶色いテープだ…どんな音楽が吹き込まれていたのかな",
                angleDeg = 235f,
                distRatio = 0.82f
            },

            // === 北西ビーチ（探検者向けの追加年代スポット） ===
            new FlotsamDef {
                id = "flotsam_2050_nw",
                name = "ナノ結晶バッテリーセル",
                era = AdventureBeachFlotsam.FlotsamEra.Era2050,
                eraLabel = "2050年代・高密度電源",
                description = "掌に収まる六角柱の青い結晶。かすかなエネルギー残滓が砂を温めている。",
                dialogue = "小さな結晶なのに、都市をまるごと動かすエネルギーが入ってたんだって",
                angleDeg = 295f,
                distRatio = 0.97f
            },
            new FlotsamDef {
                id = "flotsam_2030_nw",
                name = "手巻き機械式時計の文字盤",
                era = AdventureBeachFlotsam.FlotsamEra.Era2030,
                eraLabel = "2030年代・真鍮製ゼンマイ時計",
                description = "針が止まり真鍮が錆びた時計。電池や通信に頼らず時を刻んでいた職人の遺物。",
                dialogue = "カチカチと音を立てていた機械だね。時間は止まっちゃったけど、温かい手触りだ",
                angleDeg = 290f,
                distRatio = 0.83f
            }
        };

        // 島の中心 (512, 512)、白砂ビーチの半径 (約440m)
        Vector3 islandCenter = new Vector3(512f, 0f, 512f);
        float baseBeachRadius = 455f;

        foreach (var def in items)
        {
            float rad = def.angleDeg * Mathf.Deg2Rad;
            float radius = baseBeachRadius * def.distRatio;
            Vector3 worldPos = islandCenter + new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);

            if (terrain != null)
            {
                float ty = terrain.SampleHeight(worldPos) + terrain.transform.position.y;
                worldPos.y = Mathf.Max(ty, 5.85f);
            }
            else
            {
                worldPos.y = 6.2f;
            }

            var go = new GameObject("Flotsam_" + def.id);
            go.transform.position = worldPos;

            var flotsam = go.AddComponent<AdventureBeachFlotsam>();
            flotsam.itemId = def.id;
            flotsam.itemName = def.name;
            flotsam.era = def.era;
            flotsam.eraLabel = def.eraLabel;
            flotsam.description = def.description;
            flotsam.rustDialogue = def.dialogue;
        }
    }

    void OnGUI()
    {
        if (_inspectTimer <= 0f || string.IsNullOrEmpty(_inspectTitle))
            return;

        int fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.022f, 18f, 26f));
        int subSize = Mathf.RoundToInt(fontSize * 0.72f);

        if (_hudBg == null)
        {
            _hudBg = new Texture2D(1, 1);
            _hudBg.SetPixel(0, 0, new Color(0.06f, 0.10f, 0.16f, 0.92f));
            _hudBg.Apply();
        }

        float boxWidth = Mathf.Clamp(Screen.width * 0.55f, 380f, 680f);
        float boxHeight = fontSize * 3.6f + subSize + 30f;
        float x = (Screen.width - boxWidth) * 0.5f;
        float y = Mathf.Clamp(Screen.height * 0.12f, 50f, 120f);

        float alpha = Mathf.Clamp01(_inspectTimer);
        Color prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);

        // 背景
        Rect rect = new Rect(x, y, boxWidth, boxHeight);
        GUI.DrawTexture(rect, _hudBg);

        // 左端の年代グラデーションアクセントカラーバー
        Color eraCol = new Color(0.3f, 0.9f, 0.85f); // 2050
        if (_inspectEra.Contains("2040")) eraCol = new Color(0.85f, 0.75f, 0.35f); // 2040
        if (_inspectEra.Contains("2030")) eraCol = new Color(0.95f, 0.55f, 0.35f); // 2030

        Rect bar = new Rect(x, y, 6f, boxHeight);
        GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, eraCol, 0, 0);

        // タイトル
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = fontSize;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = new Color(1.0f, 1.0f, 1.0f);

        GUIStyle eraStyle = new GUIStyle(GUI.skin.label);
        eraStyle.fontSize = subSize;
        eraStyle.fontStyle = FontStyle.Bold;
        eraStyle.normal.textColor = eraCol;

        GUIStyle descStyle = new GUIStyle(GUI.skin.label);
        descStyle.fontSize = subSize;
        descStyle.wordWrap = true;
        descStyle.normal.textColor = new Color(0.88f, 0.92f, 0.96f);

        GUI.Label(new Rect(x + 22f, y + 10f, boxWidth - 40f, fontSize + 4f), _inspectTitle, titleStyle);
        GUI.Label(new Rect(x + 22f, y + 14f + fontSize, boxWidth - 40f, subSize + 4f), "【" + _inspectEra + "】", eraStyle);
        GUI.Label(new Rect(x + 22f, y + 20f + fontSize + subSize, boxWidth - 40f, subSize * 2.6f), _inspectDesc, descStyle);

        GUI.color = prev;
    }

    struct FlotsamDef
    {
        public string id;
        public string name;
        public AdventureBeachFlotsam.FlotsamEra era;
        public string eraLabel;
        public string description;
        public string dialogue;
        public float angleDeg;
        public float distRatio;
    }
}
