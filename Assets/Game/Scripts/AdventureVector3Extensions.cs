/// <summary>
/// Vector3 ユーティリティ拡張メソッド。
/// プロジェクト全体で使用する共通ヘルパー。
/// </summary>
internal static class Vector3Ext
{
    /// <summary>Y成分を置き換えた新しい Vector3 を返す</summary>
    public static UnityEngine.Vector3 SetY(this UnityEngine.Vector3 v, float y)
        => new UnityEngine.Vector3(v.x, y, v.z);
}
