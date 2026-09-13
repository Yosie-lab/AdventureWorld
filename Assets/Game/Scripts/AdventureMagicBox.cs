using UnityEngine;
using UnityEngine.InputSystem;

public enum MagicBoxSkillType
{
    DoubleJump,
    SuperJumpAndDash,
    PetRadar
}

public class AdventureMagicBox : MonoBehaviour
{
    public MagicBoxSkillType skillType = MagicBoxSkillType.DoubleJump;
    public string skillName = "空中2段ジャンプ";
    public string skillDescription = "空中でスペースキーを押すと、もう一度ジャンプが出来るようになった！";
    public float interactRadius = 3.2f;

    bool _opened = false;
    GameObject _promptTextGo;
    TextMesh _promptTextMesh;
    Transform _cameraTransform;
    static GUIStyle _guiStyle;

    static string _lastUnlockedSkillMessage = "";
    static float _messageTimer = 0f;

    void Start()
    {
        EnsureSolidBoxCollider();
        SetupMagicVisuals();

        var mainCam = Camera.main;
        if (mainCam != null)
            _cameraTransform = mainCam.transform;

        CreatePromptText();
    }

    void EnsureSolidBoxCollider()
    {
        var col = GetComponent<BoxCollider>();
        if (col == null)
            col = gameObject.AddComponent<BoxCollider>();
        col.enabled = true;
        col.isTrigger = false;
    }

    void SetupMagicVisuals()
    {
        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            Color goldColor = new Color(1.0f, 0.85f, 0.2f);
            AdventurePrimitiveVisuals.ApplyLitColor(rend, goldColor, true);
        }
        transform.localScale = Vector3.one * 1.3f;
    }

    void CreatePromptText()
    {
        if (_promptTextGo != null)
            return;

        _promptTextGo = new GameObject("MagicBoxPrompt");
        _promptTextGo.transform.SetParent(transform, false);
        _promptTextGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);

        _promptTextMesh = _promptTextGo.AddComponent<TextMesh>();
        _promptTextMesh.characterSize = 0.15f;
        _promptTextMesh.fontSize = 36;
        _promptTextMesh.anchor = TextAnchor.MiddleCenter;
        _promptTextMesh.alignment = TextAlignment.Center;
        _promptTextMesh.color = Color.yellow;
        _promptTextMesh.text = "✨ [E] マジックボックスを開く ✨";

        _promptTextGo.SetActive(false);
    }

    void Update()
    {
        if (_cameraTransform == null && Camera.main != null)
            _cameraTransform = Camera.main.transform;

        if (_promptTextGo != null && _promptTextGo.activeSelf && _cameraTransform != null)
        {
            _promptTextGo.transform.rotation = Quaternion.LookRotation(_promptTextGo.transform.position - _cameraTransform.position);
        }

        if (_opened)
            return;

        var player = FindObjectOfType<AdventurePlayerController>();
        if (player == null)
            return;

        float dist = Vector3.Distance(transform.position, player.transform.position);
        bool inRange = dist <= interactRadius;

        if (_promptTextGo != null)
            _promptTextGo.SetActive(inRange);

        var kb = Keyboard.current;
        if (inRange && (player.InteractPressed || (kb != null && kb.eKey.wasPressedThisFrame)))
        {
            OpenMagicBox(player);
        }
    }

    void OpenMagicBox(AdventurePlayerController player)
    {
        _opened = true;
        if (_promptTextGo != null)
            _promptTextGo.SetActive(false);

        switch (skillType)
        {
            case MagicBoxSkillType.DoubleJump:
                player.canDoubleJump = true;
                _lastUnlockedSkillMessage = "★ スペシャル能力獲得！ ★\n【空中2段ジャンプ (Double Jump)】が出来るようになった！";
                break;
            case MagicBoxSkillType.SuperJumpAndDash:
                player.jumpMultiplier = 1.35f;
                player.moveSpeedMultiplier = 1.35f;
                _lastUnlockedSkillMessage = "★ スペシャル能力獲得！ ★\n【ハイジャンプ & スピードダッシュ】がパワーアップした！";
                break;
            case MagicBoxSkillType.PetRadar:
                player.hasPetRadar = true;
                _lastUnlockedSkillMessage = "★ スペシャル能力獲得！ ★\n【迷子ペット探知レーダー】が解放された！";
                break;
        }

        _messageTimer = 30.0f;

        // 開封エフェクト・黄金の輝き
        var rend = GetComponent<Renderer>();
        if (rend != null)
            AdventurePrimitiveVisuals.ApplyLitColor(rend, new Color(0.4f, 1.0f, 0.4f), true);

        // 光のアニメーション
        transform.position += Vector3.up * 0.3f;
    }

    static int _lastMessageFrame = -1;

    void OnGUI()
    {
        if (_messageTimer > 0f)
        {
            if (Time.frameCount != _lastMessageFrame)
            {
                _lastMessageFrame = Time.frameCount;
                _messageTimer -= Time.unscaledDeltaTime;
            }

            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            {
                _messageTimer = 0f;
            }

            if (_guiStyle == null)
            {
                _guiStyle = new GUIStyle(GUI.skin.box);
                _guiStyle.fontSize = 30;
                _guiStyle.fontStyle = FontStyle.Bold;
                _guiStyle.normal.textColor = Color.yellow;
                _guiStyle.alignment = TextAnchor.MiddleCenter;
                _guiStyle.wordWrap = true;
            }

            float w = Mathf.Min(Screen.width * 0.88f, 760f);
            float h = 165f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.20f;

            string displayStr = _lastUnlockedSkillMessage + "\n<size=22><color=#ffffff>（[Space] または [Enter] キーで閉じます）</color></size>";
            GUI.Box(new Rect(x, y, w, h), displayStr, _guiStyle);
        }
    }

    public static void SpawnMagicBoxes(Terrain land)
    {
        if (land == null)
            return;

        // 各スキルボックスの候補座標（岩・崖のない平坦な草地のみ）
        Vector3[] doubleJumpCandidates =
        {
            new Vector3(145f, 0f, 155f),   // 候補A
            new Vector3(178f, 0f, 148f),   // 候補B
            new Vector3(158f, 0f, 175f),   // 候補C
        };
        Vector3[] superJumpCandidates =
        {
            new Vector3(205f, 0f, 215f),   // 候補A（元の位置）
            new Vector3(222f, 0f, 200f),   // 候補B
            new Vector3(195f, 0f, 228f),   // 候補C
        };
        Vector3[] petRadarCandidates =
        {
            new Vector3(115f, 0f, 205f),   // 候補A（元の位置）
            new Vector3(100f, 0f, 225f),   // 候補B
            new Vector3( 88f, 0f, 210f),   // 候補C
        };

        // ランダムに1か所を選択して配置
        CreateBoxAt(
            doubleJumpCandidates[Random.Range(0, doubleJumpCandidates.Length)],
            MagicBoxSkillType.DoubleJump, "2段ジャンプ宝箱", land);
        CreateBoxAt(
            superJumpCandidates[Random.Range(0, superJumpCandidates.Length)],
            MagicBoxSkillType.SuperJumpAndDash, "ダッシュ宝箱", land);
        CreateBoxAt(
            petRadarCandidates[Random.Range(0, petRadarCandidates.Length)],
            MagicBoxSkillType.PetRadar, "ペットレーダー宝箱", land);
    }

    static void CreateBoxAt(Vector3 pos, MagicBoxSkillType skill, string name, Terrain land)
    {
        if (GameObject.Find(name) != null)
            return;

        float groundY = land.SampleHeight(pos) + land.transform.position.y;
        pos.y = groundY + 0.65f;

        var boxGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boxGo.name = name;
        boxGo.transform.position = pos;

        var magicBox = boxGo.AddComponent<AdventureMagicBox>();
        magicBox.skillType = skill;
    }
}
