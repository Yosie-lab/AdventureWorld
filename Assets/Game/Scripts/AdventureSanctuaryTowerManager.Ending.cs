using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// RustAndFloat エンディング系 partial の索引と共通ヘルパー。
/// 実体はフェーズごとに分割:
/// Canopy.cs — 天蓋台本〜光柱 / Climax.cs — 凍結危機・注油 / Epilogue.cs — 映画字幕〜クリア
/// 進行判定の窓口は AdventureStoryFlow。
/// </summary>
public partial class AdventureSanctuaryTowerManager
{
    /// <summary>台本送り・注油・ダイブ長押しの決定系がすべて離された</summary>
    static bool IsEndingAdvanceInputReleased()
    {
        return !AdventureInputReader.OilHoldButtons && !AdventureInputReader.MouseRightHeld;
    }

    void TickScriptAdvanceInputRelease()
    {
        if (_canopyBeatIndex == 0)
        {
            // レバー余韻の短いガード後は第1幕もクリック送り可
            if (_scriptRequireInputRelease
                && Time.unscaledTime - _scriptBoardOpenedAt >= 0.35f
                && IsEndingAdvanceInputReleased())
                _scriptRequireInputRelease = false;
            return;
        }
        if (_scriptRequireInputRelease && IsEndingAdvanceInputReleased())
            _scriptRequireInputRelease = false;
    }

    static bool IsManualScriptAdvancePressed()
    {
        if (AdventureInputReader.DialogAdvanceDown || AdventureInputReader.MouseRightDown)
            return true;
        var pad = Gamepad.current;
        return pad != null && pad.buttonWest.wasPressedThisFrame;
    }

    /// <summary>手動台本送り（1フレーム1回）。Canopy / Climax / Notify から共用</summary>
    bool PollManualScriptAdvance(float minShowSec, bool blockCanopyBeat0)
    {
        if (!_scriptBoardVisible || _scriptBoardAdvance)
            return false;
        if (_scriptRequireInputRelease)
            return false;
        float min = _canopyBeatIndex == 0 ? 0.35f : minShowSec;
        if (Time.unscaledTime - _scriptBoardOpenedAt < min)
            return false;
        if (!IsManualScriptAdvancePressed())
            return false;
        _scriptBoardAdvance = true;
        return true;
    }
}
