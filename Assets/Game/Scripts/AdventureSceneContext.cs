using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// シーン識別・コンテキスト判定の一元化クラス。
/// 毎フレームの SceneManager.GetActiveScene().name によるヒープアロケーション（GC Alloc）と
/// 文字列比較を完全に排除し、キャッシュされた bool フラグでゼロオーバーヘッド判定を行う。
/// </summary>
public static class AdventureSceneContext
{
    // ─── シーン名定数 ─────────────────────────────────────────────────────

    /// <summary>RustAndFloat の本番シーン名</summary>
    public const string SceneRustAndFloat = "RustAndFloat";

    /// <summary>RustAndFloat のフラットテスト用シーン名</summary>
    public const string SceneRustAndFlat  = "RustAndFlat";

    /// <summary>AdventureWorld のメインシーン名</summary>
    public const string SceneAdventureWorld = "AdventureWorld";

    // ─── キャッシュ領域 ───────────────────────────────────────────────────
    private static string s_cachedActiveSceneName = string.Empty;
    private static bool s_cachedIsRustFloat = false;
    private static bool s_cachedIsAdventureWorld = false;
    private static bool s_initialized = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        RefreshCache();
        if (!s_initialized)
        {
            s_initialized = true;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
    }

    private static void OnActiveSceneChanged(Scene current, Scene next)
    {
        RefreshCache();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshCache();
    }

    public static void RefreshCache()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return;

        s_cachedActiveSceneName = scene.name;
        s_cachedIsRustFloat = (s_cachedActiveSceneName == SceneRustAndFloat || s_cachedActiveSceneName == SceneRustAndFlat);
        s_cachedIsAdventureWorld = (s_cachedActiveSceneName == SceneAdventureWorld);
    }

    // ─── シーン判定プロパティ ─────────────────────────────────────────────

    /// <summary>現在のアクティブシーン名（キャッシュ済み）</summary>
    public static string ActiveSceneName
    {
        get
        {
            if (string.IsNullOrEmpty(s_cachedActiveSceneName))
                RefreshCache();
            return s_cachedActiveSceneName;
        }
    }

    /// <summary>RustAndFloat（本番 or フラット）シーンで動作中か（ゼロアロケーション高速判定）</summary>
    public static bool IsRustFloat
    {
        get
        {
            if (string.IsNullOrEmpty(s_cachedActiveSceneName))
                RefreshCache();
            return s_cachedIsRustFloat;
        }
    }

    /// <summary>AdventureWorld シーンで動作中か（ゼロアロケーション高速判定）</summary>
    public static bool IsAdventureWorld
    {
        get
        {
            if (string.IsNullOrEmpty(s_cachedActiveSceneName))
                RefreshCache();
            return s_cachedIsAdventureWorld;
        }
    }

    // ─── AdventurePlayerController との後方互換ラッパー ──────────────────

    /// <summary>
    /// 既存コードとの互換性のためのラッパー。
    /// 新規コードでは <see cref="IsRustFloat"/> を直接使うこと。
    /// </summary>
    public static bool IsRustFloatScene() => IsRustFloat;
}
