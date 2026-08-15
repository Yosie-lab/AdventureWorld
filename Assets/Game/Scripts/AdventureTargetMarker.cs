using UnityEngine;

public class AdventureTargetMarker : MonoBehaviour
{
    public string label = "?";
    public Color color = new Color(1f, 0.85f, 0.35f, 1f);
    public float height = 3.2f;
    public float bobAmplitude = 0.25f;
    public float bobSpeed = 2.2f;

    Transform _visual;
    float _baseY;

    public void Build()
    {
        _visual = new GameObject("Visual").transform;
        _visual.SetParent(transform, false);

        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.transform.SetParent(_visual, false);
        post.transform.localScale = new Vector3(0.18f, 1.4f, 0.18f);
        post.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        Tint(post, color * 0.85f);
        Object.Destroy(post.GetComponent<Collider>());

        var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.transform.SetParent(_visual, false);
        orb.transform.localScale = Vector3.one * 0.55f;
        orb.transform.localPosition = new Vector3(0f, height, 0f);
        Tint(orb, color);
        Object.Destroy(orb.GetComponent<Collider>());

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(_visual, false);
        labelGo.transform.localPosition = new Vector3(0f, height + 0.55f, 0f);
        var text = labelGo.AddComponent<TextMesh>();
        text.text = label;
        text.characterSize = 0.12f;
        text.fontSize = 64;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _baseY = transform.position.y;
    }

    void LateUpdate()
    {
        if (_visual == null)
            return;

        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 toCam = cam.transform.position - _visual.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.001f)
                _visual.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
        }

        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        _visual.localPosition = new Vector3(0f, bob, 0f);
    }

    static void Tint(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null)
            return;
        var mat = renderer.material;
        mat.color = color;
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 0.35f);
        }
    }
}
