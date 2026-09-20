using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// カピタ（Capyta / ピアノ演奏カピタ / 野生カピタ / NPCカピタ）の体に固体コライダーを付与し、
/// Niko（CharacterController）が体をすり抜けてしまわないようにする。
/// </summary>
[DisallowMultipleComponent]
public class AdventureCapytaBodyCollider : MonoBehaviour
{
    [Header("Collider Settings")]
    [SerializeField] private CapsuleCollider _capsule;

    void Awake()
    {
        EnsureCollider();
    }

    /// <summary>
    /// カピタの体型・スケールに最適化された固体カプセルコライダーをセットアップ
    /// </summary>
    public void EnsureCollider()
    {
        if (_capsule == null)
        {
            _capsule = GetComponent<CapsuleCollider>();
            if (_capsule == null)
            {
                _capsule = gameObject.AddComponent<CapsuleCollider>();
            }
        }

        // 胴体の前後軸（Z軸方向）にカプセルを配置
        _capsule.direction = 2; // 0=X, 1=Y, 2=Z
        _capsule.radius = 0.58f;
        _capsule.height = 1.65f;
        _capsule.center = new Vector3(0f, 0.76f, 0.15f);

        // 固体コライダーとしてCharacterControllerを確実に押し返す
        _capsule.isTrigger = false;
    }

    /// <summary>
    /// シーン内のすべてのカピタ（ピアノカピタ・野生カピタ・NPCカピタ）を走査し、
    /// すり抜け防止コライダーが存在することを100%保証する。
    /// </summary>
    public static void EnsureAllCapytasInScene()
    {
        var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < allTransforms.Length; i++)
        {
            var t = allTransforms[i];
            if (t == null) continue;

            if (IsCapytaRoot(t))
            {
                var col = t.GetComponent<AdventureCapytaBodyCollider>();
                if (col == null)
                {
                    col = t.gameObject.AddComponent<AdventureCapytaBodyCollider>();
                }
                col.EnsureCollider();
                count++;
            }
        }

        Debug.Log($"[AdventureCapytaBodyCollider] 🐾 シーン内の全カピタ（{count}頭）にすり抜け防止コライダーを適用しました。");
    }

    /// <summary>
    /// カピタの個体ルートオブジェクトか判定
    /// </summary>
    public static bool IsCapytaRoot(Transform t)
    {
        if (t == null) return false;
        string n = t.name;

        // ピアノカピタ
        if (n == "PianistCapyta") return true;

        // プレハブ・野生カピタ・NPC
        if (n == "Capyta") return true;
        if (n.StartsWith("Capyta_") && !n.Contains("Root")) return true;

        // NPC
        var npc = t.GetComponent<AdventureNpc>();
        if (npc != null && (npc.npcId == "capyta" || npc.displayName == "カピタ"))
            return true;

        // 子メッシュ（Armature等）の親がCapytaなら、自身はルートではない
        if (t.parent != null)
        {
            string pn = t.parent.name;
            if (pn == "Capyta" || pn == "PianistCapyta" || (pn.StartsWith("Capyta_") && !pn.Contains("Root")))
                return false;
        }

        return false;
    }

#if UNITY_EDITOR
    [MenuItem("Adventure/🐾 Ensure All Capyta Colliders (全カピタのすり抜け防止)")]
    public static void EditorEnsureAllCapytas()
    {
        EnsureAllCapytasInScene();
    }
#endif
}
