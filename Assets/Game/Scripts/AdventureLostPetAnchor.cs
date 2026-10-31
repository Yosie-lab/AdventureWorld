using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class AdventureLostPetAnchor : MonoBehaviour
{
    AdventureNpc _npc;

    void Awake()
    {
        _npc = GetComponent<AdventureNpc>();
        DisableRootAnimator();
        SnapAndVisual();
    }

    void Update()
    {
        SnapAndVisual();
    }

    void LateUpdate()
    {
        SnapAndVisual();
    }

    void SnapAndVisual()
    {
        if (_npc == null || (_npc.npcId != "dog" && _npc.npcId != "cat"))
            return;

        if (_npc.IsFollowing())
        {
            // 追従開始後はアンカーは不要なため破棄し、競合を完全に防ぐ
            Destroy(this);
            return;
        }

        AdventureQuestLocations.SnapLostPet(transform, _npc.npcId);
        AdventureLostPetVisuals.EnsurePetModel(transform, _npc.npcId);
        DisableRootAnimator();
    }

    void DisableRootAnimator()
    {
        var animator = GetComponent<Animator>();
        if (animator != null)
            animator.enabled = false;
    }
}
