using UnityEngine;

[DefaultExecutionOrder(10000)]
public class AdventureNpc : MonoBehaviour
{
    public string npcId;
    public string displayName;
    public float radius = 4.2f;

    void Awake()
    {
        if (IsLostPet())
        {
            if (GetComponent<AdventureLostPetAnchor>() == null)
                gameObject.AddComponent<AdventureLostPetAnchor>();
            if (GetComponent<AdventureLostPetFollower>() == null)
                gameObject.AddComponent<AdventureLostPetFollower>();
        }
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

    public bool IsFollowing()
    {
        var follower = GetComponent<AdventureLostPetFollower>();
        return follower != null && follower.IsFollowing;
    }

    public bool IsInRange(Vector3 playerPosition)
    {
        Vector3 a;
        if (IsLostPet() && !IsFollowing() && AdventureQuestLocations.TryGetLostPetCoords(npcId, out float x, out float z))
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
