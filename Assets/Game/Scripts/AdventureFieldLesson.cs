using UnityEngine;

/// <summary>
/// 砂浜と深い森の標。次に必要な手掛かりと、その地形で動きやすくなるスキルを渡す。
/// </summary>
public class AdventureFieldLesson : MonoBehaviour
{
    public enum Kind { BeachTide, DeepForest }

    const string KeyTide = "RustAndFloat_TideStep";
    const string KeyCanopy = "RustAndFloat_CanopyRead";

    static readonly Vector3[] DeepGroves =
    {
        new Vector3(680f, 0f, 530f),
        new Vector3(490f, 0f, 680f),
        new Vector3(720f, 0f, 380f),
        new Vector3(620f, 0f, 640f),
    };
    static readonly float[] DeepRadii = { 75f, 65f, 70f, 65f };

    public Kind kind = Kind.BeachTide;

    public static bool HasTideStep { get; private set; }
    public static bool HasCanopyRead { get; private set; }

    TextMesh _prompt;
    Transform _cam;
    bool _promptKnown;

    public static void Ensure()
    {
        Load();
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (scene != "RustAndFlat" && scene != "RustAndFloat")
            return;

        Terrain land = null;
        foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
        {
            if (t != null && t.name == "LandTerrain")
            {
                land = t;
                break;
            }
        }
        if (land == null)
            land = Object.FindAnyObjectByType<Terrain>();

        Spawn(Kind.BeachTide, "FieldLesson_Beach", new Vector3(145f, 0f, 310f), land);
        Spawn(Kind.DeepForest, "FieldLesson_Forest", new Vector3(680f, 0f, 530f), land);
    }

    public static void ClearForNewGame()
    {
        PlayerPrefs.DeleteKey(KeyTide);
        PlayerPrefs.DeleteKey(KeyCanopy);
        PlayerPrefs.Save();
        HasTideStep = false;
        HasCanopyRead = false;
        foreach (var lesson in Object.FindObjectsByType<AdventureFieldLesson>(FindObjectsInactive.Exclude))
            lesson.RefreshLearnedLook();
    }

    public static bool IsInDeepForest(Vector3 pos)
    {
        for (int i = 0; i < DeepGroves.Length; i++)
        {
            float dx = pos.x - DeepGroves[i].x;
            float dz = pos.z - DeepGroves[i].z;
            if (dx * dx + dz * dz <= DeepRadii[i] * DeepRadii[i])
                return true;
        }
        return false;
    }

    /// <summary>習得スキルに応じて、Rustの照準距離と「近くの届きにくいパーツ」候補距離を広げる。</summary>
    public static void GetRustAimAssist(Vector3 nikoPos, ref float maxDist, ref float bestDot, ref float fallbackRange)
    {
        var player = AdventurePlayerController.Instance;
        bool beach = HasTideStep && player != null && player.IsInBeachOrCoastZone(nikoPos);
        bool forest = HasCanopyRead && IsInDeepForest(nikoPos);
        if (beach)
        {
            maxDist = Mathf.Max(maxDist, 48f);
            bestDot = Mathf.Min(bestDot, 0.84f);
            fallbackRange = Mathf.Max(fallbackRange, 18f);
        }
        if (forest)
        {
            maxDist = Mathf.Max(maxDist, 54f);
            bestDot = Mathf.Min(bestDot, 0.76f);
            fallbackRange = Mathf.Max(fallbackRange, 24f);
        }
    }

    public static string NextNeed()
    {
        var mgr = AdventureScrapManager.Instance;
        int scraps = mgr != null ? mgr.CollectedCount : 0;
        int pts = mgr != null ? mgr.TotalProgressPoints : 0;
        bool lever = mgr != null && mgr.IsLeverUnlocked;
        if (AdventureSanctuaryTowerManager.IsCanopyBroken)
            return "天蓋は開いた。光の柱に乗って、Rustと空の上へ。";
        if (lever)
            return "ポイントは足りている。島の中央、白亜のタワーのレバーを引こう。";
        if (scraps < 3)
            return "古代ギアがあと" + (3 - scraps) + "個。3個でダッシュが戻る。届かない場所は正面を見てF、Rustが取りに行く。";
        if (scraps < 6)
            return "ギアがあと" + (6 - scraps) + "個で二段ジャンプ。草地の魔法箱も能力になる。";
        if (scraps < 9)
            return "ギアがあと" + (9 - scraps) + "個でソナー。東の深い森（おおよそ680, 530）に木漏れ日の標がある。";
        if (scraps < 12)
            return "ギアがあと" + (12 - scraps) + "個で大滑空。そのあとポイント20でタワーの天蓋。";
        if (pts < AdventureScrapManager.RequiredPointsForCanopy)
            return "ギアは揃った。漂着箱やピアノの遺物でポイントを20まで。今 " + pts + " / 20。";
        return "中央タワーへ向かおう。";
    }

    static void Load()
    {
        HasTideStep = PlayerPrefs.GetInt(KeyTide, 0) == 1;
        HasCanopyRead = PlayerPrefs.GetInt(KeyCanopy, 0) == 1;
    }

    static void Spawn(Kind kind, string name, Vector3 pos, Terrain land)
    {
        if (GameObject.Find(name) != null)
            return;

        if (land != null)
            pos.y = land.SampleHeight(pos) + land.transform.position.y;
        else
            pos.y = 6.5f;

        var root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        root.name = name;
        root.transform.position = pos + Vector3.up * 0.55f;
        root.transform.localScale = new Vector3(0.42f, 0.7f, 0.42f);
        var col = root.GetComponent<CapsuleCollider>();
        if (col == null)
            col = root.AddComponent<CapsuleCollider>();
        col.isTrigger = false;

        var lesson = root.AddComponent<AdventureFieldLesson>();
        lesson.kind = kind;

        var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "LessonOrb";
        orb.transform.SetParent(root.transform, false);
        orb.transform.localPosition = new Vector3(0f, 1.15f, 0f);
        orb.transform.localScale = Vector3.one * 0.55f;
        var orbCol = orb.GetComponent<Collider>();
        if (orbCol != null)
            Destroy(orbCol);

        Color tint = kind == Kind.BeachTide
            ? new Color(1f, 0.82f, 0.45f)
            : new Color(0.45f, 0.9f, 0.55f);
        var rend = root.GetComponent<Renderer>();
        var orbRend = orb.GetComponent<Renderer>();
        if (rend != null)
            AdventurePrimitiveVisuals.ApplyLitColor(rend, tint * 0.65f, true);
        if (orbRend != null)
            AdventurePrimitiveVisuals.ApplyLitColor(orbRend, tint, true);
    }

    void Start()
    {
        Load();
        var cam = Camera.main;
        if (cam != null)
            _cam = cam.transform;

        var promptGo = new GameObject("LessonPrompt");
        promptGo.transform.SetParent(transform, false);
        promptGo.transform.localPosition = new Vector3(0f, 2.1f, 0f);
        _prompt = promptGo.AddComponent<TextMesh>();
        _prompt.characterSize = 0.12f;
        _prompt.fontSize = 32;
        _prompt.anchor = TextAnchor.MiddleCenter;
        _prompt.alignment = TextAlignment.Center;
        _prompt.color = new Color(1f, 0.95f, 0.75f);
        promptGo.SetActive(false);
        RefreshLearnedLook();
    }

    void Update()
    {
        if (_cam == null && Camera.main != null)
            _cam = Camera.main.transform;

        var player = AdventurePlayerController.Instance;
        bool inRange = player != null && Vector3.Distance(transform.position, player.transform.position) <= 3.4f;
        if (_prompt != null)
        {
            _prompt.gameObject.SetActive(inRange);
            if (inRange && _cam != null)
                _prompt.transform.rotation = Quaternion.LookRotation(_prompt.transform.position - _cam.position);
        }

        bool known = OwnsSkill();
        if (known != _promptKnown)
            RefreshLearnedLook();

        if (!inRange || player == null || !player.InteractPressed)
            return;

        GrantAndTell(player);
    }

    bool OwnsSkill()
    {
        return kind == Kind.BeachTide ? HasTideStep : HasCanopyRead;
    }

    void RefreshLearnedLook()
    {
        _promptKnown = OwnsSkill();
        if (_prompt != null)
        {
            _prompt.text = _promptKnown
                ? "[E] 次に必要なことを聞く"
                : (kind == Kind.BeachTide ? "[E] 潮目の標に触れる" : "[E] 木漏れ日の標に触れる");
        }
    }

    void GrantAndTell(AdventurePlayerController player)
    {
        bool first = !OwnsSkill();
        string skillLine;
        string title;
        if (kind == Kind.BeachTide)
        {
            HasTideStep = true;
            PlayerPrefs.SetInt(KeyTide, 1);
            title = first ? "潮目の標：潮足" : "潮目の標";
            skillLine = "砂浜の足が軽くなる。海岸では、少し遠いパーツもFでRustに頼める。";
        }
        else
        {
            HasCanopyRead = true;
            PlayerPrefs.SetInt(KeyCanopy, 1);
            title = first ? "木漏れ日の標：梢読み" : "木漏れ日の標";
            skillLine = "深い森で滑空が落ちにくい。木の上や茂みの奥は、FでRustが取りに行く。";
        }
        PlayerPrefs.Save();
        RefreshLearnedLook();

        string next = NextNeed();
        var drone = AdventureRustDrone.Instance ?? Object.FindFirstObjectByType<AdventureRustDrone>();
        drone?.SpeakCustom(first ? skillLine + " " + next : next, 6.2f);

        if (AdventureScrapHUD.Instance != null)
            AdventureScrapHUD.Instance.ShowPoeticLore(title, next, first ? skillLine : "手掛かりを更新した");
    }
}
