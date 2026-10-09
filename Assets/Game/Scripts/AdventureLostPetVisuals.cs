using UnityEngine;

public static class AdventureLostPetVisuals
{
    static readonly Color DogBody = new Color(0.58f, 0.36f, 0.16f);
    static readonly Color CatBody = new Color(0.72f, 0.48f, 0.22f);

    // 素の犬・猫は高さ約0.9m。プレハブ本来の大きさで表示する。
    const float DogVisualScale = 1f;
    const float CatVisualScale = 1f;

    static float VisualScaleFor(string npcId) =>
        npcId == "dog" ? DogVisualScale : CatVisualScale;

    public static void EnsurePetModel(Transform root, string npcId)
    {
        if (root == null)
            return;

        string visualName = npcId == "dog" ? "DogVisual" : "CatVisual";
        var existingVisual = root.Find(visualName);
        if (existingVisual != null)
        {
            DisableBrokenVisuals(root, visualName);
            existingVisual.gameObject.SetActive(true);
            float scale = existingVisual.GetComponentInChildren<SkinnedMeshRenderer>(true) != null
                ? VisualScaleFor(npcId)
                : VisualScaleFor(npcId) * 0.4f;
            existingVisual.localScale = Vector3.one * scale;
            return;
        }

        DisableBrokenVisuals(root, visualName);

        if (TrySpawnPrefabVisual(root, npcId, visualName))
            return;

        SpawnStandInVisual(root, npcId, visualName);
    }

    static bool TrySpawnPrefabVisual(Transform root, string npcId, string visualName)
    {
        string resourcePath = npcId == "dog"
            ? "LostPets/SM_CartoonAnimal_Dog"
            : "LostPets/SM_CartoonAnimal_Cat";
        var prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null)
        {
            Debug.LogWarning("Adventure: prefab not found in Resources: " + resourcePath);
            return false;
        }

        var visual = Object.Instantiate(prefab, root);
        visual.name = visualName;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one * VisualScaleFor(npcId);

        foreach (var col in visual.GetComponentsInChildren<Collider>(true))
            DestroySafe(col);

        foreach (var smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.updateWhenOffscreen = true;
            smr.enabled = true;
        }

        return true;
    }

    static void SpawnStandInVisual(Transform root, string npcId, string visualName)
    {
        var bodyColor = npcId == "dog" ? DogBody : CatBody;
        var legColor = bodyColor * 0.85f;
        var rootGo = new GameObject(visualName);
        rootGo.transform.SetParent(root, false);
        rootGo.transform.localPosition = Vector3.zero;
        rootGo.transform.localRotation = Quaternion.identity;
        rootGo.transform.localScale = Vector3.one * (VisualScaleFor(npcId) * 0.4f);

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(rootGo.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        body.transform.localScale = new Vector3(1.3f, 0.9f, 2f);
        DestroySafe(body.GetComponent<Collider>());
        AdventurePrimitiveVisuals.ApplyLitColor(body.GetComponent<Renderer>(), bodyColor);

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(rootGo.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.35f, 0.85f);
        head.transform.localScale = Vector3.one * 0.55f;
        DestroySafe(head.GetComponent<Collider>());
        AdventurePrimitiveVisuals.ApplyLitColor(head.GetComponent<Renderer>(), bodyColor);

        // 耳
        CreateEar(rootGo.transform, "EarL", new Vector3(-0.25f, 1.6f, 0.85f), new Vector3(0.18f, 0.35f, 0.18f), bodyColor);
        CreateEar(rootGo.transform, "EarR", new Vector3(0.25f, 1.6f, 0.85f), new Vector3(0.18f, 0.35f, 0.18f), bodyColor);

        // 4本の脚（前左, 前右, 後左, 後右）の付け根（Pivot）とメッシュ
        CreateLeg(rootGo.transform, "Leg_FL", new Vector3(-0.48f, 0.65f, 0.65f), legColor);
        CreateLeg(rootGo.transform, "Leg_FR", new Vector3(0.48f, 0.65f, 0.65f), legColor);
        CreateLeg(rootGo.transform, "Leg_BL", new Vector3(-0.48f, 0.65f, -0.65f), legColor);
        CreateLeg(rootGo.transform, "Leg_BR", new Vector3(0.48f, 0.65f, -0.65f), legColor);

        // 尻尾
        var tailPivot = new GameObject("Tail");
        tailPivot.transform.SetParent(rootGo.transform, false);
        tailPivot.transform.localPosition = new Vector3(0f, 0.95f, -1.0f);
        var tailMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        tailMesh.transform.SetParent(tailPivot.transform, false);
        tailMesh.transform.localPosition = new Vector3(0f, 0.25f, -0.2f);
        tailMesh.transform.localRotation = Quaternion.Euler(40f, 0f, 0f);
        tailMesh.transform.localScale = new Vector3(0.22f, 0.45f, 0.22f);
        DestroySafe(tailMesh.GetComponent<Collider>());
        AdventurePrimitiveVisuals.ApplyLitColor(tailMesh.GetComponent<Renderer>(), bodyColor);
    }

    static void CreateLeg(Transform parent, string name, Vector3 pivotPos, Color color)
    {
        var pivot = new GameObject(name);
        pivot.transform.SetParent(parent, false);
        pivot.transform.localPosition = pivotPos;
        pivot.transform.localRotation = Quaternion.identity;

        var mesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        mesh.name = "LegMesh";
        mesh.transform.SetParent(pivot.transform, false);
        mesh.transform.localPosition = new Vector3(0f, -0.32f, 0f);
        mesh.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
        DestroySafe(mesh.GetComponent<Collider>());
        AdventurePrimitiveVisuals.ApplyLitColor(mesh.GetComponent<Renderer>(), color);
    }

    static void CreateEar(Transform parent, string name, Vector3 pos, Vector3 scale, Color color)
    {
        var ear = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        ear.name = name;
        ear.transform.SetParent(parent, false);
        ear.transform.localPosition = pos;
        ear.transform.localScale = scale;
        DestroySafe(ear.GetComponent<Collider>());
        AdventurePrimitiveVisuals.ApplyLitColor(ear.GetComponent<Renderer>(), color);
    }

    static void DisableBrokenVisuals(Transform root, string keepChildName)
    {
        foreach (Transform child in root)
        {
            if (child.name == keepChildName)
                continue;
            child.gameObject.SetActive(false);
        }
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
