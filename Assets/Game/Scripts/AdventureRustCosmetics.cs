using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 相棒Rustのドレスアップ・アクセサリー装飾システム。
/// 浜辺で集めた貝殻・シーグラス・琥珀をクラフトしてRustを可愛く着せ替えできる。
/// 頭上の花冠、エメラルド発光アンテナ、サファイア飛行軌跡、太陽琥珀コア、巻貝ホイッスル等を動的に生成・装備。
/// </summary>
public class AdventureRustCosmetics : MonoBehaviour
{
    public static AdventureRustCosmetics Instance { get; private set; }

    public enum CosmeticId
    {
        SakuragaiCrown,     // 🌸 サクラガイの花冠 (Head)
        EmeraldBeacon,      // 🟢 エメラルド・アンテナランプ (Antenna)
        SapphireWings,      // 🔷 サファイアの翼チャーム (Wings)
        AmberSunCore,       // ☀️ 太陽の琥珀コア (Core)
        SeashellConch       // 🐚 純白巻貝のホイッスル (Side)
    }

    public enum CosmeticSlot
    {
        Head,
        Antenna,
        Wings,
        Core,
        Side
    }

    [System.Serializable]
    public class CosmeticDef
    {
        public CosmeticId id;
        public CosmeticSlot slot;
        public string displayName;
        public string description;
        public AdventureBeachSeashellItem.ShellKind requiredKind;
        public int requiredCount;
        public Color themeColor;
        public string rustReaction;
    }

    public static readonly CosmeticDef[] AllDefs = new CosmeticDef[]
    {
        new CosmeticDef
        {
            id = CosmeticId.SakuragaiCrown,
            slot = CosmeticSlot.Head,
            displayName = "サクラガイの花冠",
            description = "薄紅色の貝殻を優美に編み込んだティアラ。桜色の優しい光の粉がふわふわと舞う。",
            requiredKind = AdventureBeachSeashellItem.ShellKind.Sakuragai,
            requiredCount = 1,
            themeColor = new Color(1f, 0.72f, 0.82f),
            rustReaction = "わぁ…！花冠、すっごく可愛い！お花の妖精さんみたい？"
        },
        new CosmeticDef
        {
            id = CosmeticId.EmeraldBeacon,
            slot = CosmeticSlot.Antenna,
            displayName = "エメラルド・アンテナランプ",
            description = "アンテナ先端に装着する深緑のクリスタル。暗がりで神秘的な光を灯し、周囲を照らす。",
            requiredKind = AdventureBeachSeashellItem.ShellKind.SeaGlassEmerald,
            requiredCount = 1,
            themeColor = new Color(0.35f, 0.95f, 0.65f),
            rustReaction = "ピキッ！頭がピカピカ光ってる！これなら暗い森も怖くないよ！"
        },
        new CosmeticDef
        {
            id = CosmeticId.SapphireWings,
            slot = CosmeticSlot.Wings,
            displayName = "サファイアの天使翼チャーム",
            description = "波に磨かれた蒼いサファイア硝子で編まれた天使の翼。羽ばたくたびに清らかな光の羽毛が舞い散り、大空に流麗な軌跡を描く。",
            requiredKind = AdventureBeachSeashellItem.ShellKind.SeaGlassSapphire,
            requiredCount = 1,
            themeColor = new Color(0.25f, 0.75f, 1f),
            rustReaction = "見て見てNiko！天使の翼みたい！青い光がふわふわ舞って、どこまでも飛べそうだよ！"
        },
        new CosmeticDef
        {
            id = CosmeticId.AmberSunCore,
            slot = CosmeticSlot.Core,
            displayName = "太陽の琥珀コア",
            description = "胴体中央のランプが温かい黄金琥珀色に輝き、ホバリング推進炎がゴールドスパークに変化する。",
            requiredKind = AdventureBeachSeashellItem.ShellKind.AmberPebble,
            requiredCount = 1,
            themeColor = new Color(1f, 0.82f, 0.28f),
            rustReaction = "あったかい…！太陽の力が湧いてくるみたい！"
        },
        new CosmeticDef
        {
            id = CosmeticId.SeashellConch,
            slot = CosmeticSlot.Side,
            displayName = "純白巻貝のホイッスル",
            description = "側面に寄り添う小さな純白巻貝。Rustが嬉しそうに鳴くときに澄んだ潮騒のチャイムが重なる。",
            requiredKind = AdventureBeachSeashellItem.ShellKind.SpiralShell,
            requiredCount = 1,
            themeColor = new Color(0.96f, 0.95f, 1f),
            rustReaction = "ポォーッ♪ 耳をすますと、波の音が聴こえるよ！"
        }
    };

    // 生成された各コスメティクス装飾のルートオブジェクト
    readonly Dictionary<CosmeticId, GameObject> _spawnedVisuals = new Dictionary<CosmeticId, GameObject>();

    // 太陽コア用の元のライト色バックアップ
    Light _rustLightRef;
    Color _defaultLightColor = new Color(0.3f, 0.85f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        Ensure();
    }

    public static void Ensure()
    {
        if (Instance != null) return;
        var existing = Object.FindAnyObjectByType<AdventureRustCosmetics>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject("AdventureRustCosmetics");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AdventureRustCosmetics>();
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
        ApplyAllSavedCosmetics();
    }

    void Update()
    {
        // Rustが後から生成・リスポーンされた場合の追従・適用保証
        var rust = AdventureRustDrone.Instance;
        if (rust != null && _spawnedVisuals.Count == 0)
        {
            ApplyAllSavedCosmetics();
        }
    }

    public static CosmeticDef GetDef(CosmeticId id)
    {
        for (int i = 0; i < AllDefs.Length; i++)
        {
            if (AllDefs[i].id == id) return AllDefs[i];
        }
        return null;
    }

    public bool IsUnlocked(CosmeticId id)
    {
        return PlayerPrefs.GetInt("RustCosmetic_Unlocked_" + id.ToString(), 0) == 1;
    }

    public bool IsEquipped(CosmeticId id)
    {
        return PlayerPrefs.GetInt("RustCosmetic_Equipped_" + id.ToString(), 0) == 1;
    }

    public bool CanCraft(CosmeticId id)
    {
        if (IsUnlocked(id)) return false;
        var def = GetDef(id);
        if (def == null) return false;

        var shellMgr = AdventureBeachSeashellManager.Instance;
        if (shellMgr == null) return false;

        return shellMgr.GetShellCount(def.requiredKind) >= def.requiredCount;
    }

    /// <summary>指定された素材を拾ったことで、新しく作れるようになったアクセサリーの定義を返す（なければnull）</summary>
    public CosmeticDef CheckNewlyCraftable(AdventureBeachSeashellItem.ShellKind kind)
    {
        for (int i = 0; i < AllDefs.Length; i++)
        {
            var def = AllDefs[i];
            if (def.requiredKind == kind && !IsUnlocked(def.id) && CanCraft(def.id))
            {
                var shellMgr = AdventureBeachSeashellManager.Instance;
                if (shellMgr != null && shellMgr.GetShellCount(kind) == def.requiredCount)
                {
                    return def;
                }
            }
        }
        return null;
    }

    /// <summary>現在作れる状態（未作成かつ素材十分）のアクセサリーを1つ返す（なければnull）</summary>
    public CosmeticDef GetFirstCraftableDef()
    {
        for (int i = 0; i < AllDefs.Length; i++)
        {
            var def = AllDefs[i];
            if (!IsUnlocked(def.id) && CanCraft(def.id))
            {
                return def;
            }
        }
        return null;
    }

    /// <summary>素材を消費してアクセサリーをクラフト・アンロック＆自動装備</summary>
    public bool CraftAndEquip(CosmeticId id)
    {
        if (IsUnlocked(id)) return false;
        var def = GetDef(id);
        if (def == null) return false;

        var shellMgr = AdventureBeachSeashellManager.Instance;
        if (shellMgr == null) return false;

        if (!shellMgr.ConsumeShell(def.requiredKind, def.requiredCount))
            return false;

        PlayerPrefs.SetInt("RustCosmetic_Unlocked_" + id.ToString(), 1);
        PlayerPrefs.SetInt("RustCosmetic_Equipped_" + id.ToString(), 1);
        PlayerPrefs.Save();

        ApplyCosmeticVisual(id, true);
        TriggerRustCelebration(def);
        return true;
    }

    /// <summary>装備状態を切り替え</summary>
    public void SetEquipped(CosmeticId id, bool equip)
    {
        if (!IsUnlocked(id)) return;
        PlayerPrefs.SetInt("RustCosmetic_Equipped_" + id.ToString(), equip ? 1 : 0);
        PlayerPrefs.Save();

        ApplyCosmeticVisual(id, equip);
        if (equip)
        {
            var def = GetDef(id);
            if (def != null) TriggerRustCelebration(def);
        }
    }

    /// <summary>Rustへの喜び演出とセリフ</summary>
    void TriggerRustCelebration(CosmeticDef def)
    {
        var rust = AdventureRustDrone.Instance;
        if (rust == null) return;

        // 喜んで1回宙返り・祝賀モーション＆セリフ
        rust.TriggerCelebrate();
        rust.SetSpeech(def.rustReaction, 4.5f);
    }

    /// <summary>保存されている全装備アクセサリーをRustに適用</summary>
    public void ApplyAllSavedCosmetics()
    {
        var rust = AdventureRustDrone.Instance;
        if (rust == null) return;

        for (int i = 0; i < AllDefs.Length; i++)
        {
            var def = AllDefs[i];
            bool equip = IsEquipped(def.id);
            ApplyCosmeticVisual(def.id, equip);
        }
    }

    /// <summary>ニューゲーム初期化：コスメティクスのアンロック・装備状態をリセット</summary>
    public void ResetForNewGame()
    {
        for (int i = 0; i < AllDefs.Length; i++)
        {
            var def = AllDefs[i];
            PlayerPrefs.DeleteKey("RustCosmetic_Unlocked_" + def.id.ToString());
            PlayerPrefs.DeleteKey("RustCosmetic_Equipped_" + def.id.ToString());
            ApplyCosmeticVisual(def.id, false);
        }
        PlayerPrefs.Save();
        Debug.Log("[AdventureRustCosmetics] 🎀 Rustの着せ替えアクセサリーを完全リセットしました");
    }

    /// <summary>各アクセサリーの3Dビジュアル生成・破棄</summary>
    void ApplyCosmeticVisual(CosmeticId id, bool active)
    {
        var rust = AdventureRustDrone.Instance;
        if (rust == null) return;

        if (_spawnedVisuals.TryGetValue(id, out var existing))
        {
            if (existing != null)
            {
                existing.SetActive(active);
                if (!active) return;
            }
        }

        if (!active)
        {
            if (id == CosmeticId.AmberSunCore) RevertRustLightColor();
            return;
        }

        Transform rustT = rust.transform;
        Transform body = rustT.Find("Body") ?? rustT;
        Transform antenna = rustT.Find("Antenna") ?? rustT;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        switch (id)
        {
            case CosmeticId.SakuragaiCrown:
                CreateSakuragaiCrownVisual(rustT, body, shader);
                break;
            case CosmeticId.EmeraldBeacon:
                CreateEmeraldBeaconVisual(rustT, antenna, shader);
                break;
            case CosmeticId.SapphireWings:
                CreateSapphireWingsVisual(rustT, body, shader);
                break;
            case CosmeticId.AmberSunCore:
                ApplyAmberSunCoreVisual(rustT, body, shader);
                break;
            case CosmeticId.SeashellConch:
                CreateSeashellConchVisual(rustT, body, shader);
                break;
        }
    }

    #region Visual Builders
    /// <summary>🌸 サクラガイの花冠：頭上に並ぶ桜色シェル＋花びらパーティクル</summary>
    void CreateSakuragaiCrownVisual(Transform rustT, Transform body, Shader shader)
    {
        var root = new GameObject("Cosmetic_SakuragaiCrown");
        root.transform.SetParent(rustT, false);
        root.transform.localPosition = new Vector3(0f, 0.28f, 0f);

        var shellMat = new Material(shader);
        shellMat.name = "Mat_SakuragaiCrown";
        shellMat.SetColor("_BaseColor", new Color(1f, 0.76f, 0.85f, 1f));
        shellMat.SetFloat("_Smoothness", 0.85f);
        shellMat.EnableKeyword("_EMISSION");
        shellMat.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.75f) * 0.35f);

        // 6枚の小さなサクラガイをリング状にティアラ配置
        int count = 6;
        float radius = 0.20f;
        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * Mathf.PI * 2f;
            var petal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            petal.name = "Petal_" + i;
            petal.transform.SetParent(root.transform, false);
            petal.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle * 3f) * 0.02f, Mathf.Sin(angle) * radius);
            petal.transform.localRotation = Quaternion.Euler(25f, -angle * Mathf.Rad2Deg + 90f, 0f);
            petal.transform.localScale = new Vector3(0.06f, 0.015f, 0.08f);
            petal.GetComponent<Renderer>().sharedMaterial = shellMat;
            RemoveCollider(petal);
        }

        // 桜色の微粒子パーティクル
        var psGo = new GameObject("SakuragaiCrown_Fx");
        psGo.transform.SetParent(root.transform, false);
        psGo.transform.localPosition = Vector3.up * 0.05f;
        var ps = psGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = 1.8f;
        main.startSpeed = 0.15f;
        main.startSize = 0.06f;
        main.startColor = new Color(1f, 0.75f, 0.88f, 0.85f);
        main.maxParticles = 24;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 3.5f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.22f;

        var rend = psGo.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit"));

        _spawnedVisuals[CosmeticId.SakuragaiCrown] = root;
    }

    /// <summary>🟢 エメラルド・アンテナランプ：アンテナ先端のエメラルドクリスタル＋発光</summary>
    void CreateEmeraldBeaconVisual(Transform rustT, Transform antenna, Shader shader)
    {
        var root = new GameObject("Cosmetic_EmeraldBeacon");
        root.transform.SetParent(antenna, false);
        root.transform.localPosition = new Vector3(0f, 0.65f, 0f); // アンテナ先端

        var crystal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crystal.name = "EmeraldCrystal";
        crystal.transform.SetParent(root.transform, false);
        crystal.transform.localScale = new Vector3(0.12f, 0.16f, 0.12f);

        var mat = new Material(shader);
        mat.name = "Mat_EmeraldBeacon";
        mat.SetColor("_BaseColor", new Color(0.2f, 0.95f, 0.55f));
        mat.SetFloat("_Metallic", 0.2f);
        mat.SetFloat("_Smoothness", 0.92f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.25f, 1.0f, 0.6f) * 1.8f);
        crystal.GetComponent<Renderer>().sharedMaterial = mat;
        RemoveCollider(crystal);

        // 周囲を照らす優しい緑のPointLight
        var light = root.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.35f, 1.0f, 0.65f);
        light.range = 3.2f;
        light.intensity = 1.35f;

        _spawnedVisuals[CosmeticId.EmeraldBeacon] = root;
    }

    /// <summary>🔷 サファイアの天使翼チャーム：階層フェザー構造のエンジェルウィング＋優美な羽ばたきアニメ＋蒼い光粒子＆Trail</summary>
    void CreateSapphireWingsVisual(Transform rustT, Transform body, Shader shader)
    {
        var root = new GameObject("Cosmetic_SapphireWings");
        root.transform.SetParent(rustT, false);
        root.transform.localPosition = Vector3.zero;

        // メイン羽毛マテリアル（透き通るサファイアブルー＋高光沢＋神秘的エミッション）
        var wingMat = new Material(shader);
        wingMat.name = "Mat_SapphireWing_Main";
        wingMat.SetColor("_BaseColor", new Color(0.18f, 0.68f, 1f, 0.90f));
        wingMat.SetFloat("_Smoothness", 0.96f);
        wingMat.SetFloat("_Metallic", 0.05f);
        wingMat.EnableKeyword("_EMISSION");
        wingMat.SetColor("_EmissionColor", new Color(0.25f, 0.75f, 1f) * 0.92f);

        // 翼上部アーチ羽毛マテリアル（聖なる光を反射する純白〜淡青のクリスタルハイライト）
        var archMat = new Material(shader);
        archMat.name = "Mat_SapphireWing_Arch";
        archMat.SetColor("_BaseColor", new Color(0.72f, 0.92f, 1f, 0.94f));
        archMat.SetFloat("_Smoothness", 0.98f);
        archMat.SetFloat("_Metallic", 0.1f);
        archMat.EnableKeyword("_EMISSION");
        archMat.SetColor("_EmissionColor", new Color(0.55f, 0.85f, 1f) * 1.1f);

        var trailMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit"));
        trailMat.SetColor("_BaseColor", new Color(0.35f, 0.85f, 1f, 0.85f));

        Transform leftPivot = null;
        Transform rightPivot = null;
        Transform leftTip = null;
        Transform rightTip = null;

        // 左右の翼ピボット生成
        for (int side = 0; side < 2; side++)
        {
            bool isRight = (side == 1);
            float dir = isRight ? 1f : -1f;

            var pivotGo = new GameObject(isRight ? "WingPivot_R" : "WingPivot_L");
            pivotGo.transform.SetParent(root.transform, false);
            // Rustの背中上部・斜め後ろ
            pivotGo.transform.localPosition = new Vector3(dir * 0.16f, 0.09f, -0.12f);
            // 翼の自然な開き角度（少し外・後方に広がり、上に向かって立ち上がる優美なアーチ）
            pivotGo.transform.localRotation = Quaternion.Euler(8f, dir * 26f, dir * 18f);

            if (isRight) rightPivot = pivotGo.transform;
            else leftPivot = pivotGo.transform;

            // 天使の翼を形作るフェザー群（上層・中層・主風切羽の3階層・計10枚）
            // 定義: (localPos, localRot, localScale, isArch)
            var featherDefs = new (Vector3 pos, Vector3 rot, Vector3 scale, bool isArch)[]
            {
                // 【上層: 小雨覆・アーチ羽毛 3枚】肩のふんわりとした丸みと天使の光沢
                (new Vector3(dir * 0.02f, 0.04f, 0.01f),  new Vector3(8f, dir * 8f, -dir * 18f),   new Vector3(0.038f, 0.012f, 0.09f), true),
                (new Vector3(dir * 0.06f, 0.07f, -0.01f), new Vector3(12f, dir * 14f, -dir * 28f), new Vector3(0.040f, 0.012f, 0.11f), true),
                (new Vector3(dir * 0.10f, 0.09f, -0.03f), new Vector3(14f, dir * 20f, -dir * 38f), new Vector3(0.038f, 0.011f, 0.12f), true),

                // 【中層: 次列風切羽 3枚】主翼の厚みと優美なカーブを支える中間フェザー
                (new Vector3(dir * 0.04f, 0.01f, -0.03f), new Vector3(4f, dir * 18f, -dir * 12f),  new Vector3(0.044f, 0.012f, 0.14f), false),
                (new Vector3(dir * 0.08f, 0.03f, -0.06f), new Vector3(2f, dir * 24f, -dir * 20f),  new Vector3(0.045f, 0.011f, 0.16f), false),
                (new Vector3(dir * 0.12f, 0.05f, -0.09f), new Vector3(0f, dir * 28f, -dir * 28f),  new Vector3(0.043f, 0.010f, 0.18f), false),

                // 【下層: 初列風切羽 4枚】天使の翼を象徴する、扇状に広がる長くすらりとした流線型フェザー
                (new Vector3(dir * 0.07f, -0.02f, -0.05f), new Vector3(0f, dir * 22f, -dir * 8f),   new Vector3(0.046f, 0.010f, 0.20f), false),
                (new Vector3(dir * 0.11f, -0.01f, -0.09f), new Vector3(-3f, dir * 26f, -dir * 16f), new Vector3(0.047f, 0.009f, 0.23f), false),
                (new Vector3(dir * 0.15f, 0.01f, -0.13f),  new Vector3(-6f, dir * 30f, -dir * 24f), new Vector3(0.048f, 0.009f, 0.26f), false),
                (new Vector3(dir * 0.19f, 0.03f, -0.18f),  new Vector3(-9f, dir * 34f, -dir * 32f), new Vector3(0.046f, 0.008f, 0.29f), false),
            };

            for (int f = 0; f < featherDefs.Length; f++)
            {
                var def = featherDefs[f];
                var featherGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                featherGo.name = $"Feather_{f}";
                featherGo.transform.SetParent(pivotGo.transform, false);
                featherGo.transform.localPosition = def.pos;
                featherGo.transform.localRotation = Quaternion.Euler(def.rot);
                featherGo.transform.localScale = def.scale;
                featherGo.GetComponent<Renderer>().sharedMaterial = def.isArch ? archMat : wingMat;
                RemoveCollider(featherGo);

                // 最長羽（最後の初列風切羽）の先端を記録
                if (f == featherDefs.Length - 1)
                {
                    if (isRight) rightTip = featherGo.transform;
                    else leftTip = featherGo.transform;

                    // 翼端サファイアTrail（光跡）
                    var trailGo = new GameObject("AngelTrail");
                    trailGo.transform.SetParent(featherGo.transform, false);
                    trailGo.transform.localPosition = new Vector3(0f, 0f, 0.5f); // 羽先先端
                    var trail = trailGo.AddComponent<TrailRenderer>();
                    trail.time = 0.55f;
                    trail.startWidth = 0.07f;
                    trail.endWidth = 0.0f;
                    trail.material = trailMat;
                    trail.startColor = new Color(0.35f, 0.88f, 1f, 0.85f);
                    trail.endColor = new Color(0.18f, 0.60f, 1f, 0.0f);
                }
            }
        }

        // 天使の羽ばたきアニメーションコンポーネントをアタッチ
        var angelAnim = root.AddComponent<AdventureRustAngelWings>();
        angelAnim.Init(leftPivot, rightPivot, leftTip, rightTip);

        // 天使の光粒子・羽毛ダストパーティクル（優雅に漂うサファイア＆ピュアホワイトの光粉）
        var psGo = new GameObject("AngelFeatherSparkle_Fx");
        psGo.transform.SetParent(root.transform, false);
        psGo.transform.localPosition = new Vector3(0f, 0.10f, -0.15f);
        var ps = psGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = 1.6f;
        main.startSpeed = 0.12f;
        main.startSize = 0.06f;
        main.startColor = new Color(0.55f, 0.90f, 1f, 0.85f);
        main.maxParticles = 28;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 4.2f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.22f;

        var rend = psGo.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit"));

        _spawnedVisuals[CosmeticId.SapphireWings] = root;
    }

    /// <summary>☀️ 太陽の琥珀コア：胴体ライトを黄金色に変更＋ゴールドスパーク</summary>
    void ApplyAmberSunCoreVisual(Transform rustT, Transform body, Shader shader)
    {
        var root = new GameObject("Cosmetic_AmberSunCore");
        root.transform.SetParent(rustT, false);
        root.transform.localPosition = new Vector3(0f, 0.02f, 0.18f); // 胴体正面

        var amberMat = new Material(shader);
        amberMat.name = "Mat_AmberCore";
        amberMat.SetColor("_BaseColor", new Color(1f, 0.78f, 0.15f));
        amberMat.SetFloat("_Metallic", 0.1f);
        amberMat.SetFloat("_Smoothness", 0.95f);
        amberMat.EnableKeyword("_EMISSION");
        amberMat.SetColor("_EmissionColor", new Color(1f, 0.72f, 0.18f) * 2.2f);

        var coreObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        coreObj.name = "AmberCoreGem";
        coreObj.transform.SetParent(root.transform, false);
        coreObj.transform.localScale = new Vector3(0.14f, 0.14f, 0.09f);
        coreObj.GetComponent<Renderer>().sharedMaterial = amberMat;
        RemoveCollider(coreObj);

        // Rust自身の既存ポイントライトを黄金色に更新
        var rustLight = rustT.GetComponentInChildren<Light>();
        if (rustLight != null)
        {
            _rustLightRef = rustLight;
            _defaultLightColor = rustLight.color;
            rustLight.color = new Color(1.0f, 0.82f, 0.32f);
        }

        // ゴールドスパーク微粒子
        var psGo = new GameObject("AmberSparkle_Fx");
        psGo.transform.SetParent(root.transform, false);
        var ps = psGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = 1.0f;
        main.startSpeed = 0.25f;
        main.startSize = 0.05f;
        main.startColor = new Color(1f, 0.85f, 0.35f, 0.9f);
        main.maxParticles = 20;

        var emission = ps.emission;
        emission.rateOverTime = 4.0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f;

        var rend = psGo.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit"));

        _spawnedVisuals[CosmeticId.AmberSunCore] = root;
    }

    void RevertRustLightColor()
    {
        if (_rustLightRef != null)
        {
            _rustLightRef.color = _defaultLightColor;
        }
    }

    /// <summary>🐚 純白巻貝のホイッスル：側面に寄り添う巻貝コーン</summary>
    void CreateSeashellConchVisual(Transform rustT, Transform body, Shader shader)
    {
        var root = new GameObject("Cosmetic_SeashellConch");
        root.transform.SetParent(rustT, false);
        root.transform.localPosition = new Vector3(0.24f, -0.06f, 0.08f);
        root.transform.localRotation = Quaternion.Euler(20f, 45f, -30f);

        var shellMat = new Material(shader);
        shellMat.name = "Mat_SeashellConch";
        shellMat.SetColor("_BaseColor", new Color(0.97f, 0.96f, 0.94f));
        shellMat.SetFloat("_Smoothness", 0.75f);

        var conch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        conch.name = "ConchBody";
        conch.transform.SetParent(root.transform, false);
        conch.transform.localScale = new Vector3(0.07f, 0.09f, 0.07f);
        conch.GetComponent<Renderer>().sharedMaterial = shellMat;
        RemoveCollider(conch);

        var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tip.name = "ConchTip";
        tip.transform.SetParent(root.transform, false);
        tip.transform.localPosition = new Vector3(0f, 0.10f, 0f);
        tip.transform.localScale = new Vector3(0.045f, 0.06f, 0.045f);
        tip.GetComponent<Renderer>().sharedMaterial = shellMat;
        RemoveCollider(tip);

        _spawnedVisuals[CosmeticId.SeashellConch] = root;
    }

    static void RemoveCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }
    }
    #endregion
}

/// <summary>
/// 相棒Rustのサファイア天使翼の優美な羽ばたき・しなりアニメーションコンポーネント。
/// ホバリング待機時は呼吸のようにゆったりと羽ばたき、飛行・加速時は軽やかに躍動して天使の飛翔を再現する。
/// </summary>
public class AdventureRustAngelWings : MonoBehaviour
{
    Transform _leftPivot;
    Transform _rightPivot;
    Transform _leftTip;
    Transform _rightTip;

    Quaternion _leftBaseRot;
    Quaternion _rightBaseRot;

    Vector3 _lastPos;
    float _speedSmooth;
    bool _initialized;

    public void Init(Transform leftPivot, Transform rightPivot, Transform leftTip, Transform rightTip)
    {
        _leftPivot = leftPivot;
        _rightPivot = rightPivot;
        _leftTip = leftTip;
        _rightTip = rightTip;

        if (_leftPivot != null) _leftBaseRot = _leftPivot.localRotation;
        if (_rightPivot != null) _rightBaseRot = _rightPivot.localRotation;

        _lastPos = transform.position;
        _initialized = true;
    }

    void Update()
    {
        if (!_initialized) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 移動速度の推定とスムージング
        float currentSpeed = (transform.position - _lastPos).magnitude / Mathf.Max(dt, 0.0001f);
        _lastPos = transform.position;
        _speedSmooth = Mathf.Lerp(_speedSmooth, Mathf.Clamp(currentSpeed, 0f, 16f), dt * 3.5f);

        // 待機中（ホバリング）と飛行中での羽ばたき周波数の滑らかなブレンド
        // ホバリング: 周期約1.6秒でふわりと優雅に深呼吸
        // 飛行中: パタパタと軽やかに天使の羽ばたき（周波数上昇）
        float flapSpeed = Mathf.Lerp(3.8f, 9.2f, Mathf.Clamp01(_speedSmooth / 5.5f));
        float flapTime = Time.time * flapSpeed;

        float flapSin = Mathf.Sin(flapTime);
        float flapCos = Mathf.Cos(flapTime);

        // ロール角（上下の羽ばたき）: 待機時±12度、飛行時±22度
        float flapRoll = flapSin * Mathf.Lerp(12f, 22f, Mathf.Clamp01(_speedSmooth / 5.0f));
        // ピッチ角（前後のしなり・迎え角）
        float flapPitch = flapCos * Mathf.Lerp(3.5f, 7.5f, Mathf.Clamp01(_speedSmooth / 5.0f));
        // 後退角（飛行速度に伴い、風圧を受けて翼が後方へ自然にしなる）
        float sweepBack = Mathf.Lerp(0f, 13f, Mathf.Clamp01(_speedSmooth / 7.0f));

        if (_leftPivot != null)
        {
            _leftPivot.localRotation = _leftBaseRot * Quaternion.Euler(flapPitch, sweepBack, flapRoll);
        }
        if (_rightPivot != null)
        {
            _rightPivot.localRotation = _rightBaseRot * Quaternion.Euler(flapPitch, -sweepBack, -flapRoll);
        }
    }
}

