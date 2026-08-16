using UnityEngine;

[DefaultExecutionOrder(10000)]
public class AdventureNpc : MonoBehaviour
{
    public string npcId;
    public string displayName;
    public float radius = 4.2f;

    void Awake()
    {
        if (IsLostPet() && GetComponent<AdventureLostPetAnchor>() == null)
            gameObject.AddComponent<AdventureLostPetAnchor>();
    }

    void Start()
    {
        if (IsLostPet())
            PrepareLostPet();
    }

    void PrepareLostPet()
    {
        AdventureQuestLocations.SnapLostPet(transform, npcId);
        AdventureLostPetVisuals.EnsurePetModel(transform, npcId);
    }

    bool IsLostPet()
    {
        return npcId == "dog" || npcId == "cat";
    }

    public bool IsInRange(Vector3 playerPosition)
    {
        Vector3 a;
        if (IsLostPet() && AdventureQuestLocations.TryGetLostPetCoords(npcId, out float x, out float z))
            a = new Vector3(x, 0f, z);
        else
        {
            a = transform.position;
            a.y = 0f;
        }

        Vector3 b = playerPosition;
        b.y = 0f;
        return (a - b).sqrMagnitude <= radius * radius;
    }
}
