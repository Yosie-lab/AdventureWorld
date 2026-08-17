using UnityEngine;

public static class AdventureHintSignVisuals
{
    static readonly Color PostWood = new Color(0.34f, 0.24f, 0.14f);
    static readonly Color BoardWood = new Color(0.60f, 0.49f, 0.34f);
    static readonly Color TrimWood = new Color(0.40f, 0.30f, 0.20f);
    static readonly Color CarvedText = new Color(0.26f, 0.18f, 0.10f);
    static readonly Color StoneBase = new Color(0.48f, 0.45f, 0.40f);
    static readonly Color Moss = new Color(0.36f, 0.46f, 0.28f);

    public static void Build(Transform root, string title)
    {
        if (root == null)
            return;

        var existing = root.Find("Visual");
        if (existing != null)
            DestroySafe(existing.gameObject);

        var visual = new GameObject("Visual");
        visual.transform.SetParent(root, false);

        AddStoneBase(visual.transform);
        AddPost(visual.transform, new Vector3(-0.36f, 0f, 0f));
        AddPost(visual.transform, new Vector3(0.36f, 0f, 0f));

        AddCube(visual.transform, "CrossBar", new Vector3(1.0f, 0.09f, 0.11f), new Vector3(0f, 2.45f, 0f), PostWood, 0.1f);

        var board = AddCube(visual.transform, "Board", new Vector3(1.08f, 0.68f, 0.05f), new Vector3(0f, 1.95f, 0.03f), BoardWood, 0.14f);
        board.transform.localRotation = Quaternion.Euler(7f, 0f, 0f);

        AddTrim(board.transform, new Vector3(0f, 0.36f, -0.52f), new Vector3(1.02f, 0.05f, 0.02f));
        AddTrim(board.transform, new Vector3(0f, -0.36f, -0.52f), new Vector3(1.02f, 0.05f, 0.02f));
        AddTrim(board.transform, new Vector3(-0.52f, 0f, -0.52f), new Vector3(0.05f, 0.66f, 0.02f));
        AddTrim(board.transform, new Vector3(0.52f, 0f, -0.52f), new Vector3(0.05f, 0.66f, 0.02f));

        AddMoss(visual.transform, new Vector3(-0.36f, 0.55f, 0.05f));
        AddTitle(board.transform, title);
    }

    // 迷子のそばに立てる小さな木製標。巨大な発光柱は使わない。
    public static void BuildPetMarker(Transform root, string label, Color accent)
    {
        if (root == null)
            return;

        var existing = root.Find("Visual");
        if (existing != null)
            DestroySafe(existing.gameObject);

        var visual = new GameObject("Visual");
        visual.transform.SetParent(root, false);

        AddStoneBase(visual.transform, 0.38f, 0.06f);

        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.name = "Post";
        post.transform.SetParent(visual.transform, false);
        post.transform.localScale = new Vector3(0.09f, 0.95f, 0.09f);
        post.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        DestroyCollider(post);
        AdventurePrimitiveVisuals.ApplyWood(post.GetComponent<Renderer>(), PostWood, 0.08f);

        var board = AddCube(visual.transform, "Board", new Vector3(0.62f, 0.42f, 0.04f), new Vector3(0f, 1.62f, 0.02f), BoardWood, 0.14f);
        board.transform.localRotation = Quaternion.Euler(5f, 0f, 0f);

        AddTrim(board.transform, new Vector3(0f, 0.22f, -0.52f), new Vector3(0.58f, 0.04f, 0.02f), accent);
        AddTrim(board.transform, new Vector3(0f, -0.22f, -0.52f), new Vector3(0.58f, 0.04f, 0.02f), accent);

        AddMoss(visual.transform, new Vector3(0.04f, 0.35f, 0.04f));
        AddTitle(board.transform, label, 0.11f, 36);
    }

    static void AddStoneBase(Transform parent, float width = 0.52f, float height = 0.07f)
    {
        var stone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stone.name = "Base";
        stone.transform.SetParent(parent, false);
        stone.transform.localScale = new Vector3(width, height, width * 0.82f);
        stone.transform.localPosition = new Vector3(0f, height, 0f);
        DestroyCollider(stone);
        AdventurePrimitiveVisuals.ApplyWood(stone.GetComponent<Renderer>(), StoneBase, 0.06f);
    }

    static void AddPost(Transform parent, Vector3 localPos)
    {
        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.name = "Post";
        post.transform.SetParent(parent, false);
        post.transform.localScale = new Vector3(0.11f, 1.25f, 0.11f);
        post.transform.localPosition = localPos + new Vector3(0f, 1.25f, 0f);
        DestroyCollider(post);
        AdventurePrimitiveVisuals.ApplyWood(post.GetComponent<Renderer>(), PostWood, 0.08f);
    }

    static Transform AddCube(
        Transform parent,
        string name,
        Vector3 scale,
        Vector3 localPos,
        Color color,
        float smoothness)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localScale = scale;
        cube.transform.localPosition = localPos;
        DestroyCollider(cube);
        AdventurePrimitiveVisuals.ApplyWood(cube.GetComponent<Renderer>(), color, smoothness);
        return cube.transform;
    }

    static void AddTrim(Transform board, Vector3 localPos, Vector3 scale, Color color = default)
    {
        if (color == default)
            color = TrimWood;

        var trim = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trim.name = "Trim";
        trim.transform.SetParent(board, false);
        trim.transform.localScale = scale;
        trim.transform.localPosition = localPos;
        DestroyCollider(trim);
        AdventurePrimitiveVisuals.ApplyWood(trim.GetComponent<Renderer>(), color, 0.1f);
    }

    static void AddMoss(Transform parent, Vector3 localPos)
    {
        var moss = GameObject.CreatePrimitive(PrimitiveType.Cube);
        moss.name = "Moss";
        moss.transform.SetParent(parent, false);
        moss.transform.localScale = new Vector3(0.14f, 0.05f, 0.10f);
        moss.transform.localPosition = localPos;
        DestroyCollider(moss);
        AdventurePrimitiveVisuals.ApplyLitColor(moss.GetComponent<Renderer>(), Moss);
    }

    static void AddTitle(Transform board, string title, float scale = 0.075f, int fontSize = 48)
    {
        var labelGo = new GameObject("Title");
        labelGo.transform.SetParent(board, false);
        labelGo.transform.localPosition = new Vector3(0f, 0.04f, -0.55f);
        labelGo.transform.localRotation = Quaternion.identity;
        labelGo.transform.localScale = Vector3.one * scale;

        var text = labelGo.AddComponent<TextMesh>();
        text.text = title;
        text.characterSize = 1f;
        text.fontSize = fontSize;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = CarvedText;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    static void DestroyCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null)
            DestroySafe(col);
    }

    static void DestroySafe(Object obj)
    {
        if (obj == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object.DestroyImmediate(obj);
            return;
        }
#endif
        Object.Destroy(obj);
    }
}
