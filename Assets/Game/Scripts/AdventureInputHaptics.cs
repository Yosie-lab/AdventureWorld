using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ゲームパッド・触覚ハプティクスエンジン（DualSense / Xbox / Steam Deck 等）。
/// 着地インパクト、滑空エアロランブル、風の道、パーツ回収等の手応えを手元に直接フィードバックする。
/// </summary>
public class AdventureInputHaptics : MonoBehaviour
{
    private static AdventureInputHaptics _instance;
    public static AdventureInputHaptics Instance => Ensure();

    private class ContinuousRumble
    {
        public float low;
        public float high;
        public float expireTime;
    }

    private static float _pulseLow = 0f;
    private static float _pulseHigh = 0f;
    private static float _pulseEndTime = 0f;

    private static readonly Dictionary<string, ContinuousRumble> _continuousSources = new Dictionary<string, ContinuousRumble>();

    public static AdventureInputHaptics Ensure()
    {
        if (_instance != null) return _instance;
        _instance = FindAnyObjectByType<AdventureInputHaptics>();
        if (_instance != null) return _instance;

        var go = new GameObject("AdventureInputHaptics");
        _instance = go.AddComponent<AdventureInputHaptics>();
        DontDestroyOnLoad(go);
        return _instance;
    }

    private void Awake()
    {
        if (_instance == null) _instance = this;
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Update()
    {
        Ensure();
        UpdateMotors();
    }

    private void OnDisable()
    {
        StopAllMotors();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            StopAllMotors();
    }

    /// <summary>全モーターの即時停止</summary>
    public static void StopAllMotors()
    {
        _pulseLow = 0f;
        _pulseHigh = 0f;
        _pulseEndTime = 0f;
        _continuousSources.Clear();

        var gp = Gamepad.current;
        if (gp != null)
        {
            try { gp.SetMotorSpeeds(0f, 0f); } catch { }
        }
    }

    /// <summary>
    /// 単発ハプティクスパルスの再生
    /// </summary>
    /// <param name="low">低周波（重い衝撃、左モーター 0.0〜1.0）</param>
    /// <param name="high">高周波（微細な手応え、右モーター 0.0〜1.0）</param>
    /// <param name="duration">持続秒数</param>
    public static void PlayPulse(float low, float high, float duration)
    {
        Ensure();
        _pulseLow = Mathf.Max(_pulseLow, low);
        _pulseHigh = Mathf.Max(_pulseHigh, high);
        _pulseEndTime = Mathf.Max(_pulseEndTime, Time.unscaledTime + duration);
    }

    /// <summary>
    /// 持続振動の設定（エアロランブル、ウィンドストリーム等）
    /// </summary>
    public static void SetContinuous(string key, float low, float high, float timeout = 0.25f)
    {
        Ensure();
        if (!_continuousSources.TryGetValue(key, out var rumble))
        {
            rumble = new ContinuousRumble();
            _continuousSources[key] = rumble;
        }
        rumble.low = low;
        rumble.high = high;
        rumble.expireTime = Time.unscaledTime + timeout;
    }

    /// <summary>持続振動の停止</summary>
    public static void StopContinuous(string key)
    {
        if (_continuousSources.ContainsKey(key))
            _continuousSources.Remove(key);
    }

    // ─── プリセットイベント ───────────────────────────────────────────

    /// <summary>着地インパクト衝撃（落下速度・高度に応じたズシンとした手応え）</summary>
    public static void PlayLandingImpact(float verticalSpeed)
    {
        float speed = Mathf.Abs(verticalSpeed);
        if (speed < 2.5f) return;

        float intensity = Mathf.InverseLerp(2.5f, 18.0f, speed);
        float low = Mathf.Lerp(0.20f, 0.95f, intensity);
        float high = Mathf.Lerp(0.05f, 0.40f, intensity);
        float dur = Mathf.Lerp(0.08f, 0.22f, intensity);

        PlayPulse(low, high, dur);
    }

    /// <summary>ジャンプ・二段ジャンプ踏切パルス</summary>
    public static void PlayJump(bool isDoubleJump)
    {
        if (isDoubleJump)
            PlayPulse(0.15f, 0.45f, 0.10f); // 軽快で爽快な高周波弾性
        else
            PlayPulse(0.25f, 0.15f, 0.08f); // 地面を蹴る重み
    }

    /// <summary>風のリング・ウィンドストリーム突入（フッと推進力を感じる気流パルス）</summary>
    public static void PlayWindStreamEntry()
    {
        PlayPulse(0.35f, 0.70f, 0.18f);
    }

    /// <summary>スクラップパーツ回収・ギア再生（きらびやかなゴールドチャイム振動）</summary>
    public static void PlayScrapPickupChime()
    {
        PlayPulse(0.40f, 0.85f, 0.30f);
    }

    /// <summary>高速滑空エアロランブル（速度に応じた微小な風圧高周波振動）</summary>
    public static void UpdateGlideAeroRumble(float speed, bool isGliding)
    {
        if (!isGliding || speed < 9.5f)
        {
            StopContinuous("GlideAero");
            return;
        }

        float t = Mathf.InverseLerp(9.5f, 24.0f, speed);
        float low = Mathf.Lerp(0.0f, 0.25f, t);
        float high = Mathf.Lerp(0.08f, 0.50f, t);

        SetContinuous("GlideAero", low, high, 0.20f);
    }

    /// <summary>斜面スライディング振動（シャーッという滑走の摩擦手応え）</summary>
    public static void UpdateSlopeSlideRumble(float speed, bool isSliding)
    {
        if (!isSliding)
        {
            StopContinuous("SlopeSlide");
            return;
        }

        float t = Mathf.InverseLerp(3.0f, 15.0f, speed);
        float low = Mathf.Lerp(0.12f, 0.45f, t);
        float high = Mathf.Lerp(0.15f, 0.55f, t);

        SetContinuous("SlopeSlide", low, high, 0.18f);
    }

    // ─── モーター更新ループ ───────────────────────────────────────────

    private void UpdateMotors()
    {
        // ポーズ中は振動カット
        if (AdventurePauseMenu.IsOpen)
        {
            StopAllMotors();
            return;
        }

        var gp = Gamepad.current;
        if (gp == null) return;

        float masterScale = Mathf.Clamp01(PlayerPrefs.GetFloat("Adventure_HapticsIntensity", 1f));
        if (masterScale < 0.01f)
        {
            try { gp.SetMotorSpeeds(0f, 0f); } catch { }
            return;
        }

        float totalLow = 0f;
        float totalHigh = 0f;

        float now = Time.unscaledTime;

        // 1. パルス成分
        if (now < _pulseEndTime)
        {
            float remainT = Mathf.Clamp01((_pulseEndTime - now) / 0.20f);
            totalLow = Mathf.Max(totalLow, _pulseLow * remainT);
            totalHigh = Mathf.Max(totalHigh, _pulseHigh * remainT);
        }
        else
        {
            _pulseLow = 0f;
            _pulseHigh = 0f;
        }

        // 2. 持続成分
        List<string> expiredKeys = null;
        foreach (var kvp in _continuousSources)
        {
            if (now > kvp.Value.expireTime)
            {
                if (expiredKeys == null) expiredKeys = new List<string>();
                expiredKeys.Add(kvp.Key);
                continue;
            }
            totalLow = Mathf.Max(totalLow, kvp.Value.low);
            totalHigh = Mathf.Max(totalHigh, kvp.Value.high);
        }

        if (expiredKeys != null)
        {
            foreach (var k in expiredKeys)
                _continuousSources.Remove(k);
        }

        // 3. モーターへ出力
        float finalLow = Mathf.Clamp01(totalLow * masterScale);
        float finalHigh = Mathf.Clamp01(totalHigh * masterScale);

        try
        {
            gp.SetMotorSpeeds(finalLow, finalHigh);
        }
        catch
        {
            // 非対応コントローラーのエラー防止
        }
    }
}
