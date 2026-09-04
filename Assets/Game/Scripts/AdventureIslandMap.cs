using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class AdventureIslandMap : MonoBehaviour
{
    Transform _player;
    Terrain _land;
    Font _font;
    GameObject _panel;
    RectTransform _playerDot;
    Text _locationText;
    Vector3 _mapOrigin;
    Vector2 _mapSize = new Vector2(300f, 300f);

    struct LandmarkRef
    {
        public string name;
        public Transform transform;
        public RectTransform dotRt;
    }

    readonly List<LandmarkRef> _landmarks = new List<LandmarkRef>();

    static readonly Color TextReadable = new Color(1f, 0.97f, 0.9f, 1f);
    static readonly Color TextOutline = new Color(0f, 0f, 0f, 0.92f);

    public void Setup(Transform player, Font font)
    {
        _player = player;
        _font = font;
        _land = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude).FirstOrDefault(t => t.name == "LandTerrain");
        if (_land != null)
        {
            _mapOrigin = _land.transform.position;
            _mapSize = new Vector2(_land.terrainData.size.x, _land.terrainData.size.z);
        }
        BuildUi();
        _panel.SetActive(false);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.mKey.wasPressedThisFrame)
            _panel.SetActive(!_panel.activeSelf);
    }

    void LateUpdate()
    {
        if (_panel == null || !_panel.activeSelf)
            return;

        if (_player != null && _playerDot != null)
        {
            Vector3 p = _player.position;
            _playerDot.anchorMin = _playerDot.anchorMax = WorldToUv(p);
            if (_locationText != null)
                _locationText.text = "現在地: " + RegionName(p.x, p.z) + "   X" + Mathf.RoundToInt(p.x) + "  Z" + Mathf.RoundToInt(p.z);
        }

        // 各動物・観客のマップ位置動的更新
        for (int i = 0; i < _landmarks.Count; i++)
        {
            var lm = _landmarks[i];
            if (lm.transform == null)
            {
                var go = GameObject.Find(lm.name);
                if (go != null)
                {
                    lm.transform = go.transform;
                    _landmarks[i] = lm;
                }
            }

            if (lm.transform != null && lm.dotRt != null)
            {
                lm.dotRt.anchorMin = lm.dotRt.anchorMax = WorldToUv(lm.transform.position);
            }
        }
    }

    Vector2 WorldToUv(Vector3 world)
    {
        float u = Mathf.Clamp01((world.x - _mapOrigin.x) / _mapSize.x);
        float v = Mathf.Clamp01((world.z - _mapOrigin.z) / _mapSize.y);
        return new Vector2(u, v);
    }

    static string RegionName(float x, float z)
    {
        string ew = x >= 170f ? "東" : x <= 130f ? "西" : "";
        string ns = z >= 170f ? "北" : z <= 130f ? "南" : "";
        if (ns.Length == 0 && ew.Length == 0)
            return "中央";
        if (ns.Length == 0)
            return ew;
        if (ew.Length == 0)
            return ns;
        return ns + ew;
    }

    void BuildUi()
    {
        var rootRt = gameObject.GetComponent<RectTransform>();
        if (rootRt == null)
            rootRt = gameObject.AddComponent<RectTransform>();

        rootRt.anchorMin = new Vector2(1f, 1f);
        rootRt.anchorMax = new Vector2(1f, 1f);
        rootRt.pivot = new Vector2(1f, 1f);
        rootRt.anchoredPosition = new Vector2(-18f, -18f);
        rootRt.sizeDelta = new Vector2(240f, 300f);

        _panel = new GameObject("Panel");
        _panel.transform.SetParent(transform, false);
        var panelRt = _panel.AddComponent<RectTransform>();
        Stretch(panelRt);
        _panel.AddComponent<Image>().color = new Color(0.1f, 0.12f, 0.1f, 0.92f);

        MakeStaticText(_panel.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(220f, 24f), 18, TextAnchor.UpperCenter, "簡略マップ", true);

        var mapArea = new GameObject("MapArea");
        mapArea.transform.SetParent(_panel.transform, false);
        var mapRt = mapArea.AddComponent<RectTransform>();
        mapRt.anchorMin = mapRt.anchorMax = new Vector2(0.5f, 0.5f);
        mapRt.pivot = new Vector2(0.5f, 0.5f);
        mapRt.anchoredPosition = new Vector2(0f, 6f);
        mapRt.sizeDelta = new Vector2(200f, 200f);
        mapArea.AddComponent<Image>().color = new Color(0.2f, 0.45f, 0.78f, 1f);

        var land = MakeImage("Land", mapArea.transform, new Color(0.34f, 0.58f, 0.28f, 1f));
        var landRt = land.GetComponent<RectTransform>();
        landRt.anchorMin = new Vector2(0.04f, 0.04f);
        landRt.anchorMax = new Vector2(0.96f, 0.96f);
        landRt.offsetMin = landRt.offsetMax = Vector2.zero;

        MakeDot(mapArea.transform, WorldToUv(new Vector3(170f, 0f, 166f)), new Vector2(10f, 10f), new Color(0.95f, 0.9f, 0.55f, 1f), "広場");
        PlaceLandmarks(mapArea.transform);

        _playerDot = MakeDot(mapArea.transform, new Vector2(0.5f, 0.5f), new Vector2(14f, 14f), new Color(1f, 0.85f, 0.35f, 1f), null).GetComponent<RectTransform>();

        MakeStaticText(mapArea.transform, "N", new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(28f, 20f), 14, TextAnchor.LowerCenter, "北", true);
        MakeStaticText(mapArea.transform, "S", new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(28f, 20f), 14, TextAnchor.UpperCenter, "南", true);
        MakeStaticText(mapArea.transform, "E", new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(28f, 20f), 14, TextAnchor.MiddleRight, "東", true);
        MakeStaticText(mapArea.transform, "W", new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(28f, 20f), 14, TextAnchor.MiddleLeft, "西", true);

        _locationText = MakeStaticText(_panel.transform, "Location", new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(228f, 20f), 13, TextAnchor.LowerCenter, "", true);
        MakeStaticText(_panel.transform, "Hint", new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(228f, 18f), 11, TextAnchor.LowerCenter, "M キーで開閉", true);
    }

    void PlaceLandmarks(Transform mapArea)
    {
        _landmarks.Clear();
        Color redText = new Color(1f, 0.22f, 0.22f, 1f); // 鮮やかな赤色
        AddLandmark(mapArea, "Capyta", "カ", new Color(0.95f, 0.7f, 0.85f, 1f));
        AddLandmark(mapArea, "Cat", "猫", new Color(1f, 0.35f, 0.35f, 1f), redText);
        AddLandmark(mapArea, "Dog", "犬", new Color(1f, 0.35f, 0.35f, 1f), redText);
        AddLandmark(mapArea, "Sparrow", "雀", new Color(0.9f, 0.9f, 0.9f, 1f));
        AddLandmark(mapArea, "Muskrat", "鼠", new Color(0.8f, 0.7f, 0.55f, 1f));
        AddLandmark(mapArea, "Pudu", "鹿", new Color(0.85f, 0.8f, 0.7f, 1f));
        AddLandmark(mapArea, "Colobus", "猿", new Color(0.7f, 0.55f, 0.55f, 1f));
        AddLandmark(mapArea, "Gecko", "守", new Color(0.55f, 0.75f, 0.55f, 1f));
    }

    void AddLandmark(Transform mapArea, string objectName, string label, Color color, Color? labelColor = null)
    {
        var go = GameObject.Find(objectName);
        Vector3 pos = go != null ? go.transform.position : (objectName == "Cat" ? AdventureQuestLocations.CatPosition : objectName == "Dog" ? AdventureQuestLocations.DogPosition : Vector3.zero);
        var dotGo = MakeDot(mapArea, WorldToUv(pos), new Vector2(8f, 8f), color, label, labelColor);
        _landmarks.Add(new LandmarkRef
        {
            name = objectName,
            transform = go != null ? go.transform : null,
            dotRt = dotGo.GetComponent<RectTransform>()
        });
    }

    GameObject MakeDot(Transform parent, Vector2 uv, Vector2 size, Color color, string label, Color? labelColor = null)
    {
        var go = MakeImage("Dot", parent, color);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = uv;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        if (!string.IsNullOrEmpty(label))
            MakeStaticText(go.transform, "Label", new Vector2(0.5f, 0f), new Vector2(0f, -11f), new Vector2(36f, 18f), 13, TextAnchor.UpperCenter, label, true, labelColor);
        return go;
    }

    GameObject MakeImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        go.AddComponent<Image>().color = color;
        return go;
    }

    Text MakeStaticText(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, string value, bool bold = false, Color? textColor = null)
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
        text.font = _font;
        text.fontSize = fontSize;
        text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.alignment = align;
        text.color = textColor ?? TextReadable;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.text = value;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = TextOutline;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = TextOutline;
        shadow.effectDistance = new Vector2(1f, -1f);

        return text;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}

