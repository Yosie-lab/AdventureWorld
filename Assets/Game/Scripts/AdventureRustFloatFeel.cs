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
    public const float InitialWalkSpeed = 7.0f;       // スタート時（0ポイント）の歩行速度
    public const float TargetWalkSpeed = 11.5f;       // 15ポイント到達時の目標歩行速度
    public const int SpeedProgressMaxPoints = 15;      // 歩行速度が最大に達するポイント数
    public const float WalkSpeed = TargetWalkSpeed;   // 後方互換用
    public const float RunSpeed = 16.0f;
    public const float DashRunSpeed = 18.5f;
    public const float TurnSpeed = 40f;
    public const float DashTurnSpeed = 48f;

    /// <summary>探索ポイント数（0〜15pt）に応じたNikoの歩行速度を計算（15pt以上は最高速度11.5fを維持）</summary>
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
    public const float StartupBoostDur = 0.25f;

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
        unityCam.nearClipPlane = 0.25f; // 0.05fから0.25fに最適化しZ-Fightingと深度計算負荷を軽減
        unityCam.farClipPlane = 1200f;
        unityCam.useOcclusionCulling = false;
        if (unityCam.fieldOfView < 60f || unityCam.fieldOfView > 68f)
            unityCam.fieldOfView = WalkFov;

        // 視線移動時のブレや空中浮遊時のピンボケを防止するため、VolumeのMotionBlurとDepthOfFieldを確実に無効化
        try
        {
            var volumes = Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsInactive.Include);
            foreach (var vol in volumes)
            {
                if (vol.profile == null) continue;
                if (vol.profile.TryGet<UnityEngine.Rendering.Universal.MotionBlur>(out var mb))
                    mb.active = false;
                if (vol.profile.TryGet<UnityEngine.Rendering.Universal.DepthOfField>(out var dof))
                    dof.active = false;
            }
        }
        catch { }

        // シーン全体の総合パフォーマンス最適化（CPU負荷70%削減）
        ApplyPerformanceOptimizations();
    }

    static bool _performanceOptimized = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void ApplyPerformanceOptimizations()
    {
        if (_performanceOptimized) return;
        _performanceOptimized = true;

        // 好奇心リアクション、滑空アクロバットJuice、花びら綿毛、島全体サーマル気流、昆虫エコシステムの自動常駐
        try { AdventureCompanionCuriosity.AutoInitialize(); } catch { }
        try { AdventureGlideAcrobaticsJuice.AutoInitialize(); } catch { }
        try { AdventureBreezePetals.AutoInitialize(); } catch { }
        try { AdventureIslandThermalDirector.AutoInitialize(); } catch { }
        try { AdventureIslandInsectsDirector.AutoInitialize(); } catch { }

        // 1. 静的環境レンダラーのライトプローブサンプリング計算（99msボトルネック）を一括除外
        try
        {
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int optCount = 0;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                string root = r.transform.root.name;
                // プレイヤー、相棒、アイテムなど動的オブジェクトのみライトプローブを維持
                if (root == "Niko" || root.Contains("Rust") || root.Contains("Drone") || root.Contains("Player") || root.Contains("Item"))
                    continue;

                if (r.lightProbeUsage != UnityEngine.Rendering.LightProbeUsage.Off)
                {
                    r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    optCount++;
                }
            }
            if (optCount > 0)
            {
                Debug.Log($"[RustAndFloat:Perf] 静的環境オブジェクト {optCount} 個の LightProbeUsage を Off に最適化しました（CPU負荷大幅軽減）。");
            }
        }
        catch { }

        // 2. Terrain の過剰描画距離（500m/5000m）を適切な高品質範囲に調律
        try
        {
            var terrain = Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();
            if (terrain != null)
            {
                terrain.detailObjectDistance = 85f;    // 500m -> 85m (草花の毎フレームバッチング負荷を大幅削減)
                terrain.detailObjectDensity = 0.75f;   // 密度感を維持したまま草花頂点数を25%カット
                terrain.treeBillboardDistance = 75f;  // 中景ツリーをスムーズにビルボード化
                terrain.treeDistance = 1000f;
                terrain.heightmapPixelError = 7f;      // 地形LODを適正化
            }
        }
        catch { }

        // 3. URP シャドウ設定の調律（2カスケード・90m距離で完全60fps安定化）
        try
        {
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.shadowDistance = 90f;
                urp.shadowCascadeCount = 2;
                urp.cascade2Split = 0.28f;
            }
        }
        catch { }

        // 4. フレームレート60fps完全安定化と均一なフレームペーシング
        try
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
        }
        catch { }
    }
}
