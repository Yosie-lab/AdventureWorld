using UnityEngine;

/// <summary>
/// 風リング（地上・空中共通）の連続通過コンボ。5秒以内の次リングで加算（最大3＝油量3倍まで）。
/// </summary>
public static class AdventureWindRingCombo
{
    const float ComboWindowSeconds = 5f;
    const int MaxCombo = 3;

    static int _comboCount;
    static float _lastPassTime = -10f;

    public static int RegisterPass()
    {
        if (Time.time - _lastPassTime < ComboWindowSeconds)
            _comboCount = Mathf.Min(_comboCount + 1, MaxCombo);
        else
            _comboCount = 1;
        _lastPassTime = Time.time;
        return _comboCount;
    }

    public static int Current => _comboCount;
}
