using UnityEngine.SceneManagement;

/// <summary>
/// シーン識別・コンテキスト判定の一元化クラス。
/// 各所に散在していた "RustAndFloat" / "RustAndFlat" の文字列比較を集約する。
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

    // ─── シーン判定プロパティ ─────────────────────────────────────────────

    /// <summary>現在のアクティブシーン名</summary>
    public static string ActiveSceneName
        => SceneManager.GetActiveScene().name;

    /// <summary>RustAndFloat（本番 or フラット）シーンで動作中か</summary>
    public static bool IsRustFloat
    {
        get
        {
            string s = ActiveSceneName;
            return s == SceneRustAndFloat || s == SceneRustAndFlat;
        }
    }

    /// <summary>AdventureWorld シーンで動作中か</summary>
    public static bool IsAdventureWorld
        => ActiveSceneName == SceneAdventureWorld;

    // ─── AdventurePlayerController との後方互換ラッパー ──────────────────

    /// <summary>
    /// 既存コードとの互換性のためのラッパー。
    /// 新規コードでは <see cref="IsRustFloat"/> を直接使うこと。
    /// </summary>
    public static bool IsRustFloatScene() => IsRustFloat;
}
