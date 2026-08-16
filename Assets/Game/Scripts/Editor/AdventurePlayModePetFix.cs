#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
static class AdventurePlayModePetFix
{
    const string DogPrefabPath = "Assets/PolyOne/Cartoon Dog, Cat/Prefab/SM_CartoonAnimal_Dog.prefab";
    const string CatPrefabPath = "Assets/PolyOne/Cartoon Dog, Cat/Prefab/SM_CartoonAnimal_Cat.prefab";

    static AdventurePlayModePetFix()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            SnapLostPetsInScene(false);

        if (state == PlayModeStateChange.EnteredPlayMode)
            EditorApplication.delayCall += SnapLostPetsInPlayMode;
    }

    [MenuItem("Adventure/Rebuild Lost Pet Visuals (Save Scene)")]
    public static void RebuildLostPetVisualsMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Adventure", "Play を止めてから実行してください。", "OK");
            return;
        }

        var land = AdventureQuestLocations.FindLand();
        RebuildVisual("Dog", DogPrefabPath, "DogVisual", AdventureQuestLocations.DogX, AdventureQuestLocations.DogZ, land);
        RebuildVisual("Cat", CatPrefabPath, "CatVisual", AdventureQuestLocations.CatX, AdventureQuestLocations.CatZ, land);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Adventure: Dog/Cat visuals rebuilt and scene saved.");
    }

    [MenuItem("Adventure/Ensure Lost Pet Beacons (Save Scene)")]
    public static void EnsureBeaconsMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Adventure", "Play を止めてから実行してください。", "OK");
            return;
        }

        AdventureLostPetVisuals.EnsureBeacons();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Adventure: DogBeacon/CatBeacon placed and scene saved.");
    }

    [MenuItem("Adventure/Fix Lost Pets (Unpack + Save)")]
    public static void FixLostPetsMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Adventure", "Play を止めてから実行してください。", "OK");
            return;
        }

        UnpackLostPet("Cat");
        UnpackLostPet("Dog");
        SnapLostPetsInScene(true);
        RebuildLostPetVisualsMenu();
        Debug.Log("Adventure: Cat/Dog unpacked, snapped, visuals rebuilt, scene saved.");
    }

    [MenuItem("Adventure/Snap Lost Pets (Save Scene)")]
    public static void SnapLostPetsMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Adventure", "Play を止めてから実行してください。", "OK");
            return;
        }

        SnapLostPetsInScene(true);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Adventure: Cat/Dog snapped and scene saved.");
    }

    static void UnpackLostPet(string name)
    {
        var go = GameObject.Find(name);
        if (go == null)
        {
            Debug.LogWarning("Adventure: " + name + " not found.");
            return;
        }

        if (PrefabUtility.IsPartOfPrefabInstance(go))
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
    }

    static void SnapLostPetsInPlayMode()
    {
        if (!EditorApplication.isPlaying)
            return;

        SnapLostPetsInScene(true);
    }

    static void SnapLostPetsInScene(bool log)
    {
        var land = AdventureQuestLocations.FindLand();
        SnapOne("Cat", AdventureQuestLocations.CatX, AdventureQuestLocations.CatZ, land, log);
        SnapOne("Dog", AdventureQuestLocations.DogX, AdventureQuestLocations.DogZ, land, log);
    }

    static void SnapOne(string name, float x, float z, Terrain land, bool log)
    {
        var go = GameObject.Find(name);
        if (go == null)
        {
            if (log)
                Debug.LogWarning("Adventure: " + name + " not found.");
            return;
        }

        var animator = go.GetComponent<Animator>();
        if (animator != null)
            animator.enabled = false;

        go.transform.SetPositionAndRotation(
            new Vector3(x, AdventureQuestLocations.GroundY(land, x, z), z),
            go.transform.rotation);

        var npc = go.GetComponent<AdventureNpc>();
        if (npc != null)
        {
            if (go.GetComponent<AdventureLostPetAnchor>() == null)
                go.AddComponent<AdventureLostPetAnchor>();
            AdventureLostPetVisuals.EnsurePetModel(go.transform, npc.npcId);
        }

        if (log)
            Debug.Log("Adventure: " + name + " -> " + go.transform.position);
    }

    static void RebuildVisual(string name, string prefabPath, string visualName, float x, float z, Terrain land)
    {
        var go = GameObject.Find(name);
        if (go == null)
        {
            Debug.LogWarning("Adventure: " + name + " not found.");
            return;
        }

        for (int i = go.transform.childCount - 1; i >= 0; i--)
        {
            var child = go.transform.GetChild(i);
            if (child.name == visualName)
                continue;
            Object.DestroyImmediate(child.gameObject);
        }

        Transform visual = go.transform.Find(visualName);
        if (visual == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError("Adventure: prefab not found: " + prefabPath);
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform);
            instance.name = visualName;
            visual = instance.transform;
        }

        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        visual.localScale = Vector3.one * (name == "Dog" ? 1f : 3f);
        visual.gameObject.SetActive(true);

        foreach (var col in visual.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(col);

        var rootAnimator = go.GetComponent<Animator>();
        if (rootAnimator != null)
            rootAnimator.enabled = false;

        if (go.GetComponent<AdventureLostPetAnchor>() == null)
            go.AddComponent<AdventureLostPetAnchor>();

        go.transform.SetPositionAndRotation(
            new Vector3(x, AdventureQuestLocations.GroundY(land, x, z), z),
            go.transform.rotation);
    }
}
#endif
