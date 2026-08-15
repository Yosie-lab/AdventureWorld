using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
    Text _promptText;
    Text _dialogueText;
    GameObject _dialoguePanel;

    void Start()
    {
        BuildHud();
        ShowDialogue("幻想の森。猫と犬が迷子。Mで島マップ。WASD / E話す / R戻る", 6.5f);
    }

    void Update()
    {
        if (player == null)
            return;

        AdventureNpc near = NearestNpc();
        _prompt = near == null ? "" : "E  話す  —  " + near.displayName;

        if (player.InteractPressed && near != null)
            Talk(near);

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
                ShowDialogue("カピタ「猫は南東の高い丘。犬は北東の低い草地。崖の端にはいない。」", 5.8f);
            else
                ShowDialogue("カピタ「プドゥは怖がりだけど正直。コロブスの『北の崖へ』は嘘。」", 5.8f);
            return;
        }
        if (!_foundCat)
            ShowDialogue("カピタ「犬は無事。猫は南東の丘。ヤモリの言う池の中は嘘だよ。」", 5.5f);
        else
            ShowDialogue("カピタ「猫は無事。犬は北東の草地。森の端まで行かないで。」", 5.5f);
    }

    void TalkCat()
    {
        _foundCat = true;
        int step = _catTalks++ % 3;
        if (_foundDog)
        {
            ShowDialogue("猫「にゃあ。犬とも会えた。カピタに無事だって伝えて。」", 5.5f);
            return;
        }
        if (step == 0)
            ShowDialogue("猫「にゃー、丘で迷った。犬は低い草地へ行った。北東。コロブスの話は信じないで。」", 6.2f);
        else if (step == 1)
            ShowDialogue("猫「迷子情報：犬は高い丘にはいない。下の広い緑。崖の端でもない。」", 5.8f);
        else
            ShowDialogue("猫「スズメは空から見てる。親切だよ。」", 5f);
    }

    void TalkDog()
    {
        _foundDog = true;
        int step = _dogTalks++ % 3;
        if (_foundCat)
        {
            ShowDialogue("犬「ワン！猫も無事か。カピタへ報告だ。」", 5.5f);
            return;
        }
        if (step == 0)
            ShowDialogue("犬「ワン、匂いを辿って迷子。猫は花の丘、南東。ヤモリは意地悪だから無視。」", 6.2f);
        else if (step == 1)
            ShowDialogue("犬「迷子情報：猫は草地にも池にもいない。風の強い高い丘。」", 5.8f);
        else
            ShowDialogue("犬「マスクラットの匂いは当たってる。親切なんだ。」", 5f);
    }

    void TalkSparrow()
    {
        Play(sparrow, "Idle_A");
        int step = _sparrowTalks++ % 3;
        if (step == 0)
            ShowDialogue("スズメ「上から見た。猫は南東の丘、犬は北東の草地。教えてあげる。」", 6f);
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
            ShowDialogue("マスクラット「匂いを嗅いだよ。犬は草地、猫は丘。池の中にはいない。教えてあげる。」", 6.2f);
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
            ShowDialogue("プドゥ「…こ、こわい。でも教える。猫はもっと上の丘。犬は下の緑。」", 6f);
        else if (step == 1)
            ShowDialogue("プドゥ「コロブスに『あっち行け』って言われた。北の崖は行かないで。」", 5.8f);
        else
            ShowDialogue("プドゥ「親切にするね。花の匂いがする高いところが、猫。」", 5.5f);
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
