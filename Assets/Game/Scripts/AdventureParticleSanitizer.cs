using UnityEngine;

/// <summary>
/// Unity 6 での Particle Velocity curves must all be in the same mode ネイティブエラー・フリーズを完全防止するサニタイザー
/// </summary>
public static class AdventureParticleSanitizer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void Initialize()
    {
        SanitizeAllParticles();
    }

    public static void SanitizeAllParticles()
    {
        var allPs = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include);
        int fixedCount = 0;
        for (int i = 0; i < allPs.Length; i++)
        {
            var ps = allPs[i];
            if (ps == null) continue;
            var vel = ps.velocityOverLifetime;
            if (vel.enabled)
            {
                var mx = vel.x.mode;
                var my = vel.y.mode;
                var mz = vel.z.mode;
                if (mx != my || mx != mz || my != mz)
                {
                    // モードを統一してネイティブアサーション連打を防止
                    var targetMode = (mx == ParticleSystemCurveMode.TwoCurves || my == ParticleSystemCurveMode.TwoCurves || mz == ParticleSystemCurveMode.TwoCurves)
                        ? ParticleSystemCurveMode.TwoCurves
                        : mx;

                    var vx = vel.x;
                    var vy = vel.y;
                    var vz = vel.z;
                    vx.mode = targetMode;
                    vy.mode = targetMode;
                    vz.mode = targetMode;
                    vel.x = vx;
                    vel.y = vy;
                    vel.z = vz;
                    fixedCount++;
                }
            }
        }
        if (fixedCount > 0)
        {
            Debug.Log($"[RustAndFloat] パーティクル速度カーブ不一致を {fixedCount} 件サニタイズ（フリーズ防止完了）");
        }
    }
}
