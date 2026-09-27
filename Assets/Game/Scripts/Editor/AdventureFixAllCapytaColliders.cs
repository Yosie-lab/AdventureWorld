using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// カピタのすり抜けを完全防止するため、プレハブおよびシーン内の全カピタに
/// AdventureCapytaBodyCollider と固体BoxColliderをセットアップするエディタツール。
/// </summary>
public static class AdventureFixAllCapytaColliders
{
    [InitializeOnLoadMethod]
    [MenuItem("Adventure/🐾 Fix All Capyta Colliders (全カピタ完全すり抜け防止実行)")]
    public static void Execute()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.Log("[AdventureFixAllCapytaColliders] (PlayMode中) ランタイム走査を実行します...");
            AdventureCapytaBodyCollider.EnsureAllCapytasInScene();
            return;
        }

        Debug.Log("[AdventureFixAllCapytaColliders] === 全カピタすり抜け防止セットアップ開始 ===");

        // 1. プレハブの更新
        string prefabPath = "Assets/Niko&Capyta/Assets/Prefabs/Capyta.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab != null)
        {
            var bodyCol = prefab.GetComponent<AdventureCapytaBodyCollider>();
            if (bodyCol == null)
            {
                bodyCol = prefab.AddComponent<AdventureCapytaBodyCollider>();
            }

            // 古いCapsuleColliderを削除
            var capsules = prefab.GetComponents<CapsuleCollider>();
            for (int i = 0; i < capsules.Length; i++)
            {
                Object.DestroyImmediate(capsules[i], true);
            }

            var box = prefab.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = prefab.AddComponent<BoxCollider>();
            }

            bodyCol.EnsureCollider();
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AdventureFixAllCapytaColliders] ✅ プレハブ更新完了: {prefabPath}");
        }

        // 2. シーン内の全オブジェクトを走査
        var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        List<string> fixedNames = new List<string>();

        for (int i = 0; i < allTransforms.Length; i++)
        {
            var t = allTransforms[i];
            if (t == null) continue;

            if (AdventureCapytaBodyCollider.IsCapytaRoot(t))
            {
                // 古いCapsuleColliderを削除
                var capsules = t.GetComponents<CapsuleCollider>();
                for (int c = 0; c < capsules.Length; c++)
                {
                    Object.DestroyImmediate(capsules[c], true);
                }

                var col = t.GetComponent<AdventureCapytaBodyCollider>();
                if (col == null)
                {
                    col = Undo.AddComponent<AdventureCapytaBodyCollider>(t.gameObject);
                }

                col.EnsureCollider();
                EditorUtility.SetDirty(t.gameObject);
                fixedNames.Add($"{t.name} (pos:{t.position:F1}, scale:{t.localScale:F2})");
            }
        }

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[AdventureFixAllCapytaColliders] 🎉 合計 {fixedNames.Count} 頭のカピタにすり抜け防止コライダーを設定しました！");
        foreach (var item in fixedNames)
        {
            Debug.Log($"   🐾 {item}");
        }

        Debug.Log("[AdventureFixAllCapytaColliders] === セットアップ完了 ===");
    }
}
