using UnityEngine;

/// <summary>
/// スタート待機中にクライマックス大空ショット（島・海・キラキリ海面）をライブ背景として見せる。
/// </summary>
public static class AdventureTitleSplashScene
{
    public static bool IsActive { get; private set; }

    static Transform _pivot;

    public const float ClimaxPivotY = 156f;
    public const float ClimaxPivotYaw = 32f;
    static readonly Vector3 ClimaxPivotPos = new Vector3(512f, ClimaxPivotY, 512f);

    public static void SetActive(bool enabled)
    {
        if (enabled == IsActive)
            return;

        if (enabled && !AdventureRustFloatFeel.IsActiveScene)
            return;

        IsActive = enabled;
        if (enabled)
        {
            EnsurePivot();
            AdventureCurvedHorizonOcean.Ensure();
            AdventureSkybreakVisuals.SpawnWildernessPanorama(coldCrisis: false);
            AdventureSkybreakVisuals.ApplyLiberationOceanSparkle(true);

            var cam = AdventureCameraFollow.InstanceOrFind();
            cam?.EnableTitleClimaxSplash(true, _pivot);
        }
        else
        {
            AdventureCameraFollow.InstanceOrFind()?.EnableTitleClimaxSplash(false, null);
            AdventureSkybreakVisuals.ApplyLiberationOceanSparkle(false);
            AdventureSkybreakVisuals.DestroyNamed("WildernessPanorama");
            AdventureCloudDrift.SetFluffyCloudsVisible(true);

            if (_pivot != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(_pivot.gameObject);
                else
                    Object.DestroyImmediate(_pivot.gameObject);
                _pivot = null;
            }
        }
    }

    static void EnsurePivot()
    {
        if (_pivot != null)
            return;

        var go = new GameObject("TitleSplash_ClimaxPivot");
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.position = ClimaxPivotPos;
        go.transform.rotation = Quaternion.Euler(0f, ClimaxPivotYaw, 0f);
        _pivot = go.transform;
    }
}
