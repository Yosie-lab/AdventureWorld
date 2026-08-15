using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class AdventureCompass : MonoBehaviour
{
    Transform _yawSource;
    Transform _posSource;
    Terrain _land;
    RectTransform _dial;
    Text _coordsText;
    Text _zoneText;
    Font _font;
    Vector2 _lakeCenter = new Vector2(133f, 169f);
    float _lakeRadius = 46f;

    public void Setup(Transform yawSource, Transform posSource, Font font)
    {
        _yawSource = yawSource;
        _posSource = posSource;
        _font = font;
        _land = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude).FirstOrDefault(t => t.name == "LandTerrain");
        var lake = GameObject.Find("Lake");
        if (lake != null)
        {
            _lakeCenter = new Vector2(lake.transform.position.x, lake.transform.position.z);
            _lakeRadius = Mathf.Max(lake.transform.lossyScale.x, lake.transform.lossyScale.z) * 8.5f;
        }
        BuildUi();
    }

    void LateUpdate()
    {
        if (_yawSource != null && _dial != null)
            _dial.localEulerAngles = new Vector3(0f, 0f, -_yawSource.eulerAngles.y);

        if (_posSource != null && _coordsText != null)
        {
            Vector3 p = _posSource.position;
            _coordsText.text = "X" + Mathf.RoundToInt(p.x) + "  Z" + Mathf.RoundToInt(p.z);
            if (_zoneText != null)
                _zoneText.text = ZoneLabel(p);
        }
    }

    void BuildUi()
    {
        var rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-18f, -18f);
        rt.sizeDelta = new Vector2(96f, 128f);

        var bg = new GameObject("Bg");
        bg.transform.SetParent(transform, false);
        Stretch(bg.AddComponent<RectTransform>());
        bg.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.12f, 0.55f);

        var dialGo = new GameObject("Dial");
        dialGo.transform.SetParent(transform, false);
        _dial = dialGo.AddComponent<RectTransform>();
        _dial.anchorMin = new Vector2(0.5f, 1f);
        _dial.anchorMax = new Vector2(0.5f, 1f);
        _dial.pivot = new Vector2(0.5f, 1f);
        _dial.anchoredPosition = new Vector2(0f, 0f);
        _dial.sizeDelta = new Vector2(88f, 88f);

        MakeLabel("N", new Vector2(0.5f, 1f), new Vector2(0f, -4f), 18, new Color(1f, 0.55f, 0.5f));
        MakeLabel("E", new Vector2(1f, 0.5f), new Vector2(-4f, 0f), 15, Color.white);
        MakeLabel("S", new Vector2(0.5f, 0f), new Vector2(0f, 4f), 15, Color.white);
        MakeLabel("W", new Vector2(0f, 0.5f), new Vector2(4f, 0f), 15, Color.white);

        var marker = new GameObject("Marker");
        marker.transform.SetParent(transform, false);
        var mRt = marker.AddComponent<RectTransform>();
        mRt.anchorMin = new Vector2(0.5f, 1f);
        mRt.anchorMax = new Vector2(0.5f, 1f);
        mRt.pivot = new Vector2(0.5f, 1f);
        mRt.anchoredPosition = new Vector2(0f, -3f);
        mRt.sizeDelta = new Vector2(8f, 8f);
        marker.AddComponent<Image>().color = new Color(1f, 0.85f, 0.35f, 0.95f);

        _coordsText = MakeStaticText("Coords", new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(120f, 18f), 12, TextAnchor.LowerCenter, "X0  Z0");
        _zoneText = MakeStaticText("Zone", new Vector2(0.5f, 0f), new Vector2(0f, 42f), new Vector2(120f, 16f), 11, TextAnchor.LowerCenter, "");
        MakeStaticText("Hint", new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(120f, 16f), 11, TextAnchor.LowerCenter, "東→X  北→Z");
    }

    string ZoneLabel(Vector3 p)
    {
        if (_land == null)
            return "";
        Vector3 origin = _land.transform.position;
        Vector3 size = _land.terrainData.size;
        float edge = Mathf.Min(
            Mathf.Min(p.x - origin.x, origin.x + size.x - p.x),
            Mathf.Min(p.z - origin.z, origin.z + size.z - p.z));
        float lakeDist = Vector2.Distance(new Vector2(p.x, p.z), _lakeCenter);
        if (edge < 35f)
            return "海岸（砂地＝岸）";
        if (lakeDist < _lakeRadius + 8f)
            return "池のほとり";
        return "森";
    }

    void MakeLabel(string label, Vector2 anchor, Vector2 pos, int size, Color color)
    {
        var go = new GameObject(label);
        go.transform.SetParent(_dial, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(24f, 24f);
        var text = go.AddComponent<Text>();
        text.font = _font;
        text.fontSize = size;
        text.fontStyle = label == "N" ? FontStyle.Bold : FontStyle.Normal;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.text = label;
    }

    Text MakeStaticText(string name, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, string value)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var text = go.AddComponent<Text>();
        text.font = _font;
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = new Color(0.92f, 0.92f, 0.88f, 0.95f);
        text.text = value;
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
