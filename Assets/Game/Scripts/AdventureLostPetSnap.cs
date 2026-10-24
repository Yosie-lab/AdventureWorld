using UnityEngine;

[DefaultExecutionOrder(-100)]
public class AdventureLostPetSnap : MonoBehaviour
{
    void Awake()
    {
        Snap();
    }

    void Start()
    {
        Snap();
    }

    void Snap()
    {
        var npc = GetComponent<AdventureNpc>();
        if (npc != null)
            AdventureQuestLocations.SnapLostPet(transform, npc.npcId);
    }
}
