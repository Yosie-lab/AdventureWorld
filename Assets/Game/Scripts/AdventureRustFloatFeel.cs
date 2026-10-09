using UnityEngine;

/// <summary>
/// RustAndFloat 専用の歩行・カメラ体感パラメータ。
/// AdventureWorld の既定値は触らず、RFシーンだけここ経由で揃える。
/// </summary>
public static class AdventureRustFloatFeel
{
    // ── 環境・海面 ──
    public const float SeaLevel = 5.50f;

    // ── 地上移動 ──
    public const float InitialWalkSpeed = 5.7f;       // スタート時（0ポイント）の歩行速度
    public const float TargetWalkSpeed = 10.2f;       // 15ポイント到達時の目標歩行速度（現在の歩行速度）
    public const int SpeedProgressMaxPoints = 15;      // 歩行速度が最大に達するポイント数
    public const float WalkSpeed = TargetWalkSpeed;   // 後方互換用
    public const float RunSpeed = 16.0f;
    public const float DashRunSpeed = 18.5f;
    public const float TurnSpeed = 40f;
    public const float DashTurnSpeed = 48f;

    /// <summary>探索ポイント数（0〜15pt）に応じたNikoの歩行速度を計算（15pt以上は現在の最高速度10.2fを維持）</summary>
    public static float GetProgressWalkSpeed(int totalPoints)
    {
        if (totalPoints >= SpeedProgressMaxPoints)
            return TargetWalkSpeed;
        if (totalPoints <= 0)
            return InitialWalkSpeed;

        float t = Mathf.Clamp01((float)totalPoints / SpeedProgressMaxPoints);
        return Mathf.Lerp(InitialWalkSpeed, TargetWalkSpeed, t);
    }

    /// <summary>立ち止まり→歩き出しの一瞬ブースト</summary>
    public const float StartupBoostMul = 1.55f;
    public const float StartupBoostDur = 0.22f;

    public const float StickSpeedMoving = 1.6f;
    public const float StickSpeedIdle = 1.8f;

    public const float AnimStartupNorm = 0.48f;
    public const float AnimSpeedMul = 1.4f;
    public const float AnimSpeedMax = 2.35f;

    // ── カメラ（昔の小さな島のような広々とした遠目・見晴らしの良いパノラマ視界） ──
    public const float CameraDistance = 8.5f;
    public const float CameraDistanceMin = 7.8f;
    public const float WalkMinDistance = 7.2f;
    public const float Sensitivity = 0.28f; // キビキビと軽快に追従する適正感度
    public const float PositionSmoothTime = 0.01f;
    public const float LookSmoothTime = 0.003f; // 遅延のないキビキビした視点レスポンス
    public const float WalkFov = 68f;
    public const float KeyYawRate = 240f;
    public const float KeyPitchRate = 180f;
    public const float PadLookMul = 280f;

    /// <summary>AdventureSceneContext.IsRustFloat の後方互換ラッパー</summary>
    public static bool IsActiveScene => AdventureSceneContext.IsRustFloat;

    public static void ApplyLocomotionSpeeds(AdventurePlayerController player)
    {
        if (player == null) return;
        int pts = AdventureScrapManager.Instance != null ? AdventureScrapManager.Instance.TotalProgressPoints : 0;
        player.walkSpeed = GetProgressWalkSpeed(pts);
        player.runSpeed = RunSpeed;
        player.turnSpeed = TurnSpeed;
    }

    public static float ApplyStartupBoost(ref float timer, bool justStarted, bool hasMove, float speed)
    {
        if (justStarted)
            timer = StartupBoostDur;
        if (timer > 0f && hasMove)
        {
            timer -= Time.deltaTime;
            return speed * StartupBoostMul;
        }
        timer = 0f;
        return speed;
    }

    public static float GroundStickSpeed(bool moving)
        => moving ? StickSpeedMoving : StickSpeedIdle;

    public static float LocomotionAnimStartNorm(bool fromIdleStartup, bool isIdleClip)
    {
        if (!fromIdleStartup || isIdleClip) return 0f;
        return AnimStartupNorm;
    }

    public static float LocomotionAnimSpeed(float speed, float animRef, bool idle)
    {
        if (idle) return 1f;
        return Mathf.Clamp((speed / Mathf.Max(0.01f, animRef)) * AnimSpeedMul, 1.0f, AnimSpeedMax);
    }

    public static void ApplyCameraDefaults(AdventureCameraFollow cam, Camera unityCam)
    {
        if (cam == null) return;
        cam.sensitivity = Sensitivity;
        cam.positionSmoothTime = PositionSmoothTime;
        cam.lookSmoothTime = LookSmoothTime;
        if (cam.distance < CameraDistanceMin)
            cam.distance = CameraDistance;

        if (unityCam == null) return;
        unityCam.nearClipPlane = 0.05f;
        unityCam.farClipPlane = 1200f;
        unityCam.useOcclusionCulling = false;
        if (unityCam.fieldOfView < 60f || unityCam.fieldOfView > 68f)
            unityCam.fieldOfView = WalkFov;
    }
}
