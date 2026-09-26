using UnityEngine;

/// <summary>
/// RustAndFloat 専用の歩行・カメラ体感パラメータ。
/// AdventureWorld の既定値は触らず、RFシーンだけここ経由で揃える。
/// </summary>
public static class AdventureRustFloatFeel
{
    // ── 地上移動 ──
    public const float WalkSpeed = 10.2f;
    public const float RunSpeed = 16.0f;
    public const float DashRunSpeed = 18.5f;
    public const float TurnSpeed = 40f;
    public const float DashTurnSpeed = 48f;

    /// <summary>立ち止まり→歩き出しの一瞬ブースト</summary>
    public const float StartupBoostMul = 1.55f;
    public const float StartupBoostDur = 0.22f;

    public const float StickSpeedMoving = 1.6f;
    public const float StickSpeedIdle = 1.8f;

    public const float AnimStartupNorm = 0.48f;
    public const float AnimSpeedMul = 1.4f;
    public const float AnimSpeedMax = 2.35f;

    // ── カメラ（寄りすぎ＝頭切れ、引きすぎ＝鈍く見える） ──
    public const float CameraDistance = 7.4f;
    public const float CameraDistanceMin = 6.8f;
    public const float WalkMinDistance = 5.6f;
    public const float Sensitivity = 0.12f; // 指先に吸い付く滑らかで自然な追従感度
    public const float PositionSmoothTime = 0.01f;
    public const float LookSmoothTime = 0.008f; // 手ブレを柔らかく吸収する微小スムージング
    public const float WalkFov = 64f;
    public const float KeyYawRate = 240f;
    public const float KeyPitchRate = 180f;
    public const float PadLookMul = 280f;

    public static bool IsActiveScene => AdventurePlayerController.IsRustFloatScene();

    public static void ApplyLocomotionSpeeds(AdventurePlayerController player)
    {
        if (player == null) return;
        player.walkSpeed = WalkSpeed;
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
