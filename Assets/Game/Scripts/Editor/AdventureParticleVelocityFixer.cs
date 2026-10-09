using UnityEngine;
using UnityEditor;

public static class AdventureParticleVelocityFixer
{
    [MenuItem("RustAndFloat/Sanitize Particle Velocity Curves")]
    public static void Sanitize()
    {
        var allPs = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int fixedCount = 0;
        foreach (var ps in allPs)
        {
            if (ps == null) continue;
            var vel = ps.velocityOverLifetime;
            if (vel.enabled)
            {
                var mx = vel.x.mode;
                var my = vel.y.mode;
                var mz = vel.z.mode;
                if (mx != my || mx != mz || my != mz)
                {
                    Debug.LogWarning($"[ParticleFixer] 不一致検出: {ps.gameObject.name} in {ps.transform.root.name} (X={mx}, Y={my}, Z={mz})");
                    // 統一
                    var targetMode = mx;
                    SetMode(vel, targetMode);
                    fixedCount++;
                }
            }
        }
        Debug.Log($"[ParticleFixer] 検査完了: 合計 {allPs.Length} 個中 {fixedCount} 個の不一致を修正しました。");
    }

    static void SetMode(ParticleSystem.VelocityOverLifetimeModule vel, ParticleSystemCurveMode mode)
    {
        var vx = vel.x;
        var vy = vel.y;
        var vz = vel.z;
        vx.mode = mode;
        vy.mode = mode;
        vz.mode = mode;
        vel.x = vx;
        vel.y = vy;
        vel.z = vz;
    }
}
