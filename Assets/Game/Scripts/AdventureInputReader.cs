using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 新旧Input System 両対応の入力読み取りユーティリティ。
/// Keyboard.current（新InputSystem）と Input.GetKey（旧InputSystem）を
/// try/catch で統合し、各所に散在していた重複コードを一元化する。
/// </summary>
public static class AdventureInputReader
{
    // ─── キーボード取得 ───────────────────────────────────────────────────

    /// <summary>現在のキーボードデバイスを取得（見つからなければnull）</summary>
    public static Keyboard Keyboard
    {
        get
        {
            var kb = Keyboard.current;
            if (kb != null) return kb;
            foreach (var device in InputSystem.devices)
                if (device is Keyboard found) return found;
            return null;
        }
    }

    // ─── 移動入力 ────────────────────────────────────────────────────────

    /// <summary>WASD・矢印キー・スティックの統合移動ベクトル（最大長1）</summary>
    public static Vector2 MoveAxis
    {
        get
        {
            float x = 0f, y = 0f;

            // 旧InputSystem
            try
            {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    y =  1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  y = -1f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  x = -1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x =  1f;
            }
            catch { }

            // 新InputSystem
            var kb = Keyboard;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    y =  1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  y = -1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  x = -1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x =  1f;
            }

            // 旧InputSystem軸
            try
            {
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                if (v >  0.01f) y =  1f;
                if (v < -0.01f) y = -1f;
                if (h >  0.01f) x =  1f;
                if (h < -0.01f) x = -1f;
            }
            catch { }

            // ゲームパッド
            var gp = Gamepad.current;
            if (gp != null)
            {
                Vector2 stick = gp.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.04f)
                    return Vector2.ClampMagnitude(stick, 1f);
                if (gp.dpad.up.isPressed)    y =  1f;
                if (gp.dpad.down.isPressed)  y = -1f;
                if (gp.dpad.left.isPressed)  x = -1f;
                if (gp.dpad.right.isPressed) x =  1f;
            }

            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        }
    }

    /// <summary>移動入力が1つでもある</summary>
    public static bool HasAnyMove
    {
        get
        {
            try
            {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    return true;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  return true;
                if (GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  return true;
                if (GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) return true;
                if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f
                    || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f) return true;
            }
            catch { }

            var kb = Keyboard;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    return true;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  return true;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  return true;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) return true;
            }

            var gp = Gamepad.current;
            if (gp != null)
            {
                if (gp.leftStick.ReadValue().sqrMagnitude > 0.04f) return true;
                if (gp.dpad.up.isPressed || gp.dpad.down.isPressed
                    || gp.dpad.left.isPressed || gp.dpad.right.isPressed) return true;
            }

            return false;
        }
    }

    // ─── アクションキー ──────────────────────────────────────────────────

    /// <summary>Shiftキーが押されている（ダッシュ）</summary>
    public static bool ShiftHeld
    {
        get
        {
            try
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return true;
            }
            catch { }
            var kb = Keyboard;
            return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
        }
    }

    /// <summary>Spaceキーが押されている（滑空保持）</summary>
    public static bool SpaceHeld
    {
        get
        {
            try { if (Input.GetKey(KeyCode.Space)) return true; } catch { }
            var kb = Keyboard;
            return kb != null && kb.spaceKey.isPressed;
        }
    }

    /// <summary>Spaceキーがこのフレームに押された</summary>
    public static bool SpaceDown
    {
        get
        {
            try { if (Input.GetKeyDown(KeyCode.Space)) return true; } catch { }
            var kb = Keyboard;
            return kb != null && kb.spaceKey.wasPressedThisFrame;
        }
    }

    /// <summary>Jキーがこのフレームに押された（小ジャンプ）</summary>
    public static bool JDown
    {
        get
        {
            try { if (Input.GetKeyDown(KeyCode.J)) return true; } catch { }
            var kb = Keyboard;
            return kb != null && kb.jKey.wasPressedThisFrame;
        }
    }

    /// <summary>Space または Jキーが押されている</summary>
    public static bool SpaceOrJHeld
    {
        get
        {
            try { if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.J)) return true; } catch { }
            var kb = Keyboard;
            return kb != null && (kb.spaceKey.isPressed || kb.jKey.isPressed);
        }
    }

    /// <summary>Space または Jキーがこのフレームに押された</summary>
    public static bool SpaceOrJDown
    {
        get
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J)) return true;
            }
            catch { }
            var kb = Keyboard;
            return kb != null && (kb.spaceKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame);
        }
    }

    /// <summary>Space / J / E / Enter が押されている（クライマックス注油）</summary>
    public static bool OilHoldButtons
    {
        get
        {
            try
            {
                if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.J)
                    || Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Return)) return true;
            }
            catch { }
            var kb = Keyboard;
            return kb != null && (kb.spaceKey.isPressed || kb.jKey.isPressed
                                  || kb.eKey.isPressed  || kb.enterKey.isPressed);
        }
    }

    /// <summary>Eキーまたは左クリックがこのフレームに押された（インタラクト）</summary>
    public static bool InteractDown
    {
        get
        {
            if (MouseLeftDown) return true;
            try { if (Input.GetKeyDown(KeyCode.E)) return true; } catch { }
            var kb = Keyboard;
            if (kb != null && kb.eKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            return gp != null && gp.buttonWest.wasPressedThisFrame;
        }
    }

    /// <summary>Rキーがこのフレームに押された（リセット）</summary>
    public static bool ResetDown
    {
        get
        {
            try { if (Input.GetKeyDown(KeyCode.R)) return true; } catch { }
            var kb = Keyboard;
            return kb != null && kb.rKey.wasPressedThisFrame;
        }
    }

    /// <summary>K / F5キーがこのフレームに押された（クイックセーブ）</summary>
    public static bool QuickSaveDown
    {
        get
        {
            try { if (Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.F5)) return true; } catch { }
            var kb = Keyboard;
            return kb != null && (kb.kKey.wasPressedThisFrame || kb.f5Key.wasPressedThisFrame);
        }
    }

    /// <summary>Escapeキーがこのフレームに押された</summary>
    public static bool EscapeDown
    {
        get
        {
            try { if (Input.GetKeyDown(KeyCode.Escape)) return true; } catch { }
            var kb = Keyboard;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
        }
    }

    /// <summary>Enterキー（Return / NumpadEnter）がこのフレームに押された</summary>
    public static bool EnterDown
    {
        get
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) return true;
            }
            catch { }
            var kb = Keyboard;
            return kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
        }
    }

    /// <summary>マウス左クリックがこのフレームに押された</summary>
    public static bool MouseLeftDown
    {
        get
        {
            try { if (Input.GetMouseButtonDown(0)) return true; } catch { }
            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }
    }

    /// <summary>ダイアログ送りボタン群（左クリック / Space / J / E / Enter）がこのフレームに押された</summary>
    public static bool DialogAdvanceDown
        => MouseLeftDown || SpaceDown || JDown || InteractDown || EnterDown;

    // ─── 内部ヘルパー ────────────────────────────────────────────────────

    static bool GetKey(KeyCode code)
    {
        try { return Input.GetKey(code); }
        catch { return false; }
    }
}
