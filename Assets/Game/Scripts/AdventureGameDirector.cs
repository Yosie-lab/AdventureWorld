using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

public class AdventureGameDirector : MonoBehaviour
{
    public AdventurePlayerController player;
    public AdventureNpc capyta;
    public AdventureNpc cat;
    public AdventureNpc dog;
    public AdventureNpc sparrow;
    public AdventureNpc muskrat;
    public AdventureNpc pudu;
    public AdventureNpc colobus;
    public AdventureNpc gecko;

    bool _started;
    bool _foundCat;
    bool _foundDog;
    AdventureNpc _leadFollowPet;
    bool _complete;
    int _capytaTalks;
    int _catTalks;
    int _dogTalks;
    int _sparrowTalks;
    int _muskratTalks;
    int _puduTalks;
    int _colobusTalks;
    int _geckoTalks;
    string _dialogue;
    float _dialogueUntil;
    string _prompt = "";

    Text _questText;
    Text _guideText;
    Text _promptText;
    Text _dialogueText;
    GameObject _dialoguePanel;

    void Awake()
    {
        AdventureWorldBoot.Configure();
        RepositionLostPets();
    }

    void Start()
    {
        EnsureIslandBoundary();
        AdventureWorldBoot.Configure();
        RepositionLostPets();
        AdventureMarkerCleanup.RemoveAllQuestMarkers();
        BuildHud();
        SetupSearchAids();
        StartCoroutine(RepositionLostPetsDelayed());
        ShowDialogue("幻想の森。猫と犬が迷子。砂浜と岩=岸。黄色看板と距離表示も頼って。M / WASD / E / R", 7f);
    }

    IEnumerator RepositionLostPetsDelayed()
    {
        for (int i = 0; i < 8; i++)
        {
            yield return null;
            RepositionLostPets();
        }
    }

    void RepositionLostPets()
    {
        if (cat != null && !cat.IsFollowing())
        {
            AdventureQuestLocations.SnapLostPet(cat.transform, "cat");
            AdventureLostPetVisuals.EnsurePetModel(cat.transform, "cat");
        }
        if (dog != null && !dog.IsFollowing())
        {
            AdventureQuestLocations.SnapLostPet(dog.transform, "dog");
            AdventureLostPetVisuals.EnsurePetModel(dog.transform, "dog");
        }

        var land = FindLandTerrain();
        if (cat != null && !cat.IsFollowing())
            PlaceOnGround(GameObject.Find("Cat"), AdventureQuestLocations.CatX, AdventureQuestLocations.CatZ, land);
        if (dog != null && !dog.IsFollowing())
            PlaceOnGround(GameObject.Find("Dog"), AdventureQuestLocations.DogX, AdventureQuestLocations.DogZ, land);
    }

    void EnsureLostPetsPlaced()
    {
        if (dog != null && !dog.IsFollowing())
        {
            if (!IsNearQuest(dog.transform, AdventureQuestLocations.DogX, AdventureQuestLocations.DogZ))
                AdventureQuestLocations.SnapLostPet(dog.transform, "dog");
            AdventureLostPetVisuals.EnsurePetModel(dog.transform, "dog");
        }
        if (cat != null && !cat.IsFollowing())
        {
            if (!IsNearQuest(cat.transform, AdventureQuestLocations.CatX, AdventureQuestLocations.CatZ))
                AdventureQuestLocations.SnapLostPet(cat.transform, "cat");
            AdventureLostPetVisuals.EnsurePetModel(cat.transform, "cat");
        }
    }

    static bool IsNearQuest(Transform target, float x, float z)
    {
        Vector3 p = target.position;
        float dx = p.x - x;
        float dz = p.z - z;
        return dx * dx + dz * dz <= 16f;
    }

    static Terrain FindLandTerrain()
    {
        foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
        {
            if (terrain.name == "LandTerrain")
                return terrain;
        }

        return null;
    }

    static void PlaceOnGround(GameObject go, float x, float z, Terrain land)
    {
        if (go == null)
            return;
        go.transform.position = new Vector3(x, AdventureQuestLocations.GroundY(land, x, z), z);
    }

    static void EnsureIslandBoundary()
    {
        AdventureIslandBoundary.Ensure();
    }

    void Update()
    {
        EnsureLostPetsPlaced();

        if (player == null)
            return;

        AdventureNpc nearNpc = NearestNpc();
        AdventureHintSign nearHint = nearNpc == null ? NearestHintSign() : null;
        if (nearNpc != null)
            _prompt = "E  話す  —  " + nearNpc.displayName;
        else if (nearHint != null)
            _prompt = "E  読む  —  " + nearHint.displayName;
        else
            _prompt = "";

        if (player.InteractPressed)
        {
            if (nearNpc != null)
                Talk(nearNpc);
            else if (nearHint != null)
                ReadHint(nearHint);
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && Time.unscaledTime > _dialogueUntil)
            _dialogue = "";

        RefreshHud();
    }

    AdventureNpc NearestNpc()
    {
        AdventureNpc best = null;
        float bestDist = float.MaxValue;
        Vector3 p = player.transform.position;
        AdventureNpc[] npcs = { capyta, cat, dog, sparrow, muskrat, pudu, colobus, gecko };
        foreach (var npc in npcs)
        {
            if (npc == null || !npc.IsInRange(p))
                continue;
            float d = (npc.transform.position - p).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = npc;
            }
        }
        return best;
    }

    AdventureHintSign NearestHintSign()
    {
        AdventureHintSign best = null;
        float bestDist = float.MaxValue;
        Vector3 p = player.transform.position;
        foreach (var hint in Object.FindObjectsByType<AdventureHintSign>(FindObjectsInactive.Exclude))
        {
            if (hint == null || !hint.IsInRange(p))
                continue;
            float d = (hint.transform.position - p).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = hint;
            }
        }
        return best;
    }

    void ReadHint(AdventureHintSign hint)
    {
        _started = true;
        ShowDialogue(hint.message, 6f);
    }

    void SetupSearchAids()
    {
        if (cat != null)
            cat.radius = 6f;
        if (dog != null)
            dog.radius = 6f;

        SpawnHintSign(
            AdventureQuestLocations.HintStart,
            "スタート",
            "M=マップ。猫は北東 " + AdventureQuestLocations.CatCoordLabel
                + "、犬は北西 " + AdventureQuestLocations.DogCoordLabel + "。");
        SpawnHintSign(
            AdventureQuestLocations.HintMemo,
            "迷子メモ",
            "猫→北東の草地 " + AdventureQuestLocations.CatCoordLabel
                + "　犬→北西 " + AdventureQuestLocations.DogCoordLabel + "。Mでマップ。");
        SpawnHintSign(
            AdventureQuestLocations.HintCatTrail,
            "猫の足跡",
            "この先、北東の草地へ。" + AdventureQuestLocations.CatCoordLabel + " 付近。木の看板をたどって。");
        SpawnHintSign(
            AdventureQuestLocations.HintDogTrail,
            "犬の足跡",
            "北西の草地へ。" + AdventureQuestLocations.DogCoordLabel + " 付近。木の看板をたどって。");

        if (cat != null)
        {
            RemoveOldSearchMarkers(cat.transform);
            cat.radius = 6f;
        }

        if (dog != null)
        {
            RemoveOldSearchMarkers(dog.transform);
            dog.radius = 8f;
        }

        AdventureMarkerCleanup.RemovePetBeacons();
    }

    static void RemoveOldSearchMarkers(Transform target)
    {
        if (target == null)
            return;

        for (int i = target.childCount - 1; i >= 0; i--)
        {
            var child = target.GetChild(i);
            if (child.name == "SearchMarker" || child.name == "FindRing" || child.name == "FindLight")
                Object.Destroy(child.gameObject);
        }
    }

    void SpawnHintSign(Vector3 worldPos, string title, string message)
    {
        var existing = GameObject.Find("Hint_" + title);
        if (existing != null)
            Destroy(existing);

        worldPos.y = AdventureQuestLocations.WalkableGroundY(FindLandTerrain(), worldPos.x, worldPos.z);

        var go = new GameObject("Hint_" + title);
        go.transform.position = worldPos;
        go.transform.rotation = Quaternion.Euler(0f, (worldPos.x + worldPos.z) * 3.7f % 360f, 0f);

        var sign = go.AddComponent<AdventureHintSign>();
        sign.displayName = title;
        sign.message = title + "「" + message + "」";

        AdventureHintSignVisuals.Build(go.transform, title);

        var trigger = go.AddComponent<CapsuleCollider>();
        trigger.isTrigger = true;
        trigger.radius = 5f;
        trigger.height = 5f;
        trigger.center = new Vector3(0f, 2.5f, 0f);
    }

    static void AttachMarker(Transform target, string label, Color color, float height = 3.2f)
    {
        var go = new GameObject("SearchMarker");
        go.transform.SetParent(target, false);
        go.transform.localPosition = Vector3.zero;
        var marker = go.AddComponent<AdventureTargetMarker>();
        marker.label = label;
        marker.color = color;
        marker.height = height;
        marker.Build();
    }

    void Talk(AdventureNpc npc)
    {
        _started = true;
        switch (npc.npcId)
        {
            case "capyta":
                TalkCapyta(npc);
                break;
            case "cat":
                TalkCat();
                break;
            case "dog":
                TalkDog();
                break;
            case "sparrow":
                TalkSparrow();
                break;
            case "muskrat":
                TalkMuskrat();
                break;
            case "pudu":
                TalkPudu();
                break;
            case "colobus":
                TalkColobus();
                break;
            case "gecko":
                TalkGecko();
                break;
        }
    }

    void TalkCapyta(AdventureNpc npc)
    {
        if (_complete)
        {
            ShowDialogue("カピタ「猫も犬も戻ってきた。親切な子の話を信じて、よかった。」", 5.5f);
            Play(npc, "CapytaDance");
            return;
        }
        if (_foundCat && _foundDog)
        {
            _complete = true;
            ShowDialogue("カピタ「よく見つけたね。意地悪な嘘には乗らなかったんだね。」", 5.5f);
            Play(npc, "CapytaDance");
            return;
        }

        Play(npc, "CapytaSittingIdleLooksRight");
        int step = _capytaTalks++ % 3;
        if (!_foundCat && !_foundDog)
        {
            if (step == 0)
                ShowDialogue("カピタ「猫と犬がはぐれた。スズメとマスクラットは親切。サルとヤモリは嘘をつくよ。」", 6.2f);
            else if (step == 1)
                ShowDialogue("カピタ「猫は北東の草地。犬は北西の草地。崖の端にはいない。」", 5.8f);
            else
                ShowDialogue("カピタ「プドゥは怖がりだけど正直。コロブスの『北の崖へ』は嘘。」", 5.8f);
            return;
        }
        if (!_foundCat)
            ShowDialogue("カピタ「犬は無事。猫は北東の草地。ヤモリの言う池の中は嘘だよ。」", 5.5f);
        else
            ShowDialogue("カピタ「猫は無事。犬は北西の草地。森の端まで行かないで。」", 5.5f);
    }

    void TalkCat()
    {
        bool firstFind = !_foundCat;
        _foundCat = true;
        int step = _catTalks++ % 3;
        if (firstFind)
            BeginPetFollow(cat);
        if (_foundDog)
        {
            ShowDialogue("猫「にゃあ。犬とも会えた。カピタに無事だって伝えて。」", 5.5f);
            return;
        }
        if (step == 0)
            ShowDialogue("猫「にゃー、丘で迷った。犬は北西の草地へ行った。コロブスの話は信じないで。」", 6.2f);
        else if (step == 1)
            ShowDialogue("猫「迷子情報：犬は北東にはいない。北西の広い緑。崖の端でもない。」", 5.8f);
        else
            ShowDialogue("猫「スズメは空から見てる。親切だよ。」", 5f);
    }

    void TalkDog()
    {
        bool firstFind = !_foundDog;
        _foundDog = true;
        int step = _dogTalks++ % 3;
        if (firstFind)
            BeginPetFollow(dog);
        if (_foundCat)
        {
            ShowDialogue("犬「ワン！猫も無事か。カピタへ報告だ。」", 5.5f);
            return;
        }
        if (step == 0)
            ShowDialogue("犬「ワン、匂いを辿って迷子。猫は北東の草地。ヤモリは意地悪だから無視。」", 6.2f);
        else if (step == 1)
            ShowDialogue("犬「迷子情報：猫は池にも北西にもいない。ずっと東の高い草地。」", 5.8f);
        else
            ShowDialogue("犬「マスクラットの匂いは当たってる。親切なんだ。」", 5f);
    }

    void TalkSparrow()
    {
        Play(sparrow, "Idle_A");
        int step = _sparrowTalks++ % 3;
        if (step == 0)
            ShowDialogue("スズメ「上から見た。猫は北東の草地、犬は北西の草地。教えてあげる。」", 6f);
        else if (step == 1)
            ShowDialogue("スズメ「親切な情報：森のいちばん端は真っ暗。迷子はそんなとこにいない。」", 5.8f);
        else
            ShowDialogue(_foundCat && _foundDog
                ? "スズメ「ふたりとも見つけたなら、カピタへ。」"
                : "スズメ「コロブスとヤモリはからかうのが好き。方角が反対だったら嘘。」", 5.8f);
    }

    void TalkMuskrat()
    {
        Play(muskrat, "Idle_A");
        int step = _muskratTalks++ % 3;
        if (step == 0)
            ShowDialogue("マスクラット「匂いを嗅いだよ。犬は西の草地、猫は東の丘。池の中にはいない。教えてあげる。」", 6.2f);
        else if (step == 1)
            ShowDialogue("マスクラット「親切な情報：ヤモリは『泳げ』って言うけど、嘘。岸で迷うだけ。」", 5.8f);
        else
            ShowDialogue(_foundCat && _foundDog
                ? "マスクラット「もう揃ったね。カピタが待ってる。」"
                : "マスクラット「プドゥは小さい声だけど、本当のことしか言わないよ。」", 5.5f);
    }

    void TalkPudu()
    {
        Play(pudu, "Fear");
        int step = _puduTalks++ % 3;
        if (step == 0)
            ShowDialogue("プドゥ「…こ、こわい。でも教える。猫は北東の草地。犬は北西の緑。」", 6f);
        else if (step == 1)
            ShowDialogue("プドゥ「コロブスに『あっち行け』って言われた。北の崖は行かないで。」", 5.8f);
        else
            ShowDialogue("プドゥ「親切にするね。朝日のあたる北東が、猫。」", 5.5f);
    }

    void TalkColobus()
    {
        Play(colobus, "Clicked");
        int step = _colobusTalks++ % 3;
        if (step == 0)
            ShowDialogue("コロブス「へっ。猫？ 北の崖の端だよ。まっすぐ行けばいい。」", 5.8f);
        else if (step == 1)
            ShowDialogue("コロブス「犬は池のまんなか。泳げるだろ。教えてやったぞ。」", 5.5f);
        else
            ShowDialogue("コロブス「スズメの話なんて信じるな。僕のほうが詳しい。」", 5.5f);
    }

    void TalkGecko()
    {
        Play(gecko, "Attack");
        int step = _geckoTalks++ % 3;
        if (step == 0)
            ShowDialogue("ヤモリ「邪魔だ。犬は西の真っ暗なほう。あっち行け。」", 5.5f);
        else if (step == 1)
            ShowDialogue("ヤモリ「猫はもういない。探すだけ無駄。帰れ。」", 5.2f);
        else
            ShowDialogue("ヤモリ「親切ぶるスズメが嫌いなんだよ。信じるな。」", 5.2f);
    }

    void BeginPetFollow(AdventureNpc pet)
    {
        if (pet == null || player == null)
            return;

        var follower = pet.GetComponent<AdventureLostPetFollower>();
        if (follower == null)
            return;

        Transform target;
        if (_leadFollowPet == null)
        {
            _leadFollowPet = pet;
            target = player.transform;
        }
        else
        {
            target = _leadFollowPet.transform;
        }

        follower.BeginFollow(target);
    }

    static void Play(AdventureNpc npc, string state)
    {
        if (npc == null)
            return;
        var anim = npc.GetComponentInChildren<Animator>();
        if (anim != null)
            anim.CrossFadeInFixedTime(state, 0.2f);
    }

    void ShowDialogue(string text, float seconds)
    {
        _dialogue = text;
        _dialogueUntil = Time.unscaledTime + seconds;
    }

    void RefreshHud()
    {
        if (_questText != null)
            _questText.text = QuestLine();
        if (_guideText != null)
            _guideText.text = GuideLine();
        if (_promptText != null)
            _promptText.text = _prompt;
        bool show = !string.IsNullOrEmpty(_dialogue) && Time.unscaledTime <= _dialogueUntil;
        if (_dialoguePanel != null)
            _dialoguePanel.SetActive(show);
        if (show && _dialogueText != null)
            _dialogueText.text = _dialogue;
    }

    string QuestLine()
    {
        if (_complete)
            return "クエスト完了  みんな、幻想の森で揃った";
        if (!_started)
            return "クエスト  猫と犬を探す（嘘と本当がある）";
        string cat = _foundCat ? "猫 ✓" : "猫 ？";
        string dog = _foundDog ? "犬 ✓" : "犬 ？";
        if (_foundCat && _foundDog)
            return "クエスト  カピタのもとへ戻る    " + cat + "  " + dog;
        return "クエスト  迷子の猫と犬    " + cat + "  " + dog;
    }

    string GuideLine()
    {
        if (_complete || player == null)
            return "";
        string line = "";
        if (!_foundCat && cat != null)
            line += "猫→" + BearingLabel(player.transform.position, cat.transform.position) + " " + HorizontalDistance(player.transform.position, cat.transform.position) + "m   ";
        if (!_foundDog && dog != null)
            line += "犬→" + BearingLabel(player.transform.position, dog.transform.position) + " " + HorizontalDistance(player.transform.position, dog.transform.position) + "m";
        if (line.Length == 0)
            return "";
        return "ヒント  " + line.Trim();
    }

    static int HorizontalDistance(Vector3 from, Vector3 to)
    {
        from.y = 0f;
        to.y = 0f;
        return Mathf.RoundToInt(Vector3.Distance(from, to));
    }

    static string BearingLabel(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        delta.y = 0f;
        if (delta.sqrMagnitude < 0.01f)
            return "近";
        float angle = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
        if (angle < 0f)
            angle += 360f;
        if (angle >= 337.5f || angle < 22.5f) return "北";
        if (angle < 67.5f) return "北東";
        if (angle < 112.5f) return "東";
        if (angle < 157.5f) return "南東";
        if (angle < 202.5f) return "南";
        if (angle < 247.5f) return "南西";
        if (angle < 292.5f) return "西";
        return "北西";
    }

    void BuildHud()
    {
        var canvasGo = new GameObject("AdventureHUD");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        canvasGo.AddComponent<GraphicRaycaster>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        _questText = MakeText(canvasGo.transform, "Quest", new Vector2(24, -24), new Vector2(0, 1), new Vector2(720, 64), 22, TextAnchor.UpperLeft, font);
        _guideText = MakeText(canvasGo.transform, "Guide", new Vector2(24, -92), new Vector2(0, 1), new Vector2(760, 40), 18, TextAnchor.UpperLeft, font);
        _promptText = MakeText(canvasGo.transform, "Prompt", new Vector2(0, 88), new Vector2(0.5f, 0), new Vector2(760, 40), 20, TextAnchor.MiddleCenter, font);

        _dialoguePanel = new GameObject("Dialogue");
        _dialoguePanel.transform.SetParent(canvasGo.transform, false);
        var panelRt = _dialoguePanel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0f);
        panelRt.anchorMax = new Vector2(0.5f, 0f);
        panelRt.pivot = new Vector2(0.5f, 0f);
        panelRt.anchoredPosition = new Vector2(0f, 22f);
        panelRt.sizeDelta = new Vector2(780, 72);
        var img = _dialoguePanel.AddComponent<Image>();
        img.color = new Color(0.16f, 0.18f, 0.16f, 0.55f);
        _dialogueText = MakeText(_dialoguePanel.transform, "Line", Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(740, 60), 20, TextAnchor.MiddleLeft, font);
        _dialoguePanel.SetActive(false);

        var mapGo = new GameObject("IslandMap");
        mapGo.transform.SetParent(canvasGo.transform, false);
        var map = mapGo.AddComponent<AdventureIslandMap>();
        map.Setup(player != null ? player.transform : null, font);
    }

    static Text MakeText(Transform parent, string name, Vector2 pos, Vector2 anchor, Vector2 size, int fontSize, TextAnchor align, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
        outline.effectDistance = new Vector2(1f, -1f);
        return text;
    }
}
