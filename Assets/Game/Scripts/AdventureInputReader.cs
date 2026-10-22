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

    /// <summary>移動入力が1つでもある（MoveAxis と同じ判定）</summary>
    public static bool HasAnyMove => MoveAxis.sqrMagnitude > 0.0001f;

    /// <summary>マウスを動かして視点操作しようとしている（取得クリック直後のカーソル復帰用）</summary>
    public static bool HasMouseLookIntent
    {
        get
        {
            var mouse = Mouse.current;
            if (mouse == null) return false;
            Vector2 delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude > 0.04f) return true;
            try
            {
                float mx = Input.GetAxisRaw("Mouse X");
                float my = Input.GetAxisRaw("Mouse Y");
                return mx * mx + my * my > 0.0004f;
            }
            catch { return false; }
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

    /// <summary>Space / J / E / Enter / マウス左クリック / パッドボタンが押されている（決定・クライマックス注油・ダイブ長押し）</summary>
    public static bool OilHoldButtons
    {
        get
        {
            if (MouseLeftHeld) return true;
            try
            {
                if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.J)
                    || Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Return)) return true;
                if (Input.GetMouseButton(0)) return true;
            }
            catch { }
            var kb = Keyboard;
            if (kb != null && (kb.spaceKey.isPressed || kb.jKey.isPressed
                               || kb.eKey.isPressed  || kb.enterKey.isPressed))
                return true;
            var gp = Gamepad.current;
            if (gp != null && (gp.buttonSouth.isPressed || gp.buttonWest.isPressed))
                return true;
            return false;
        }
    }

    /// <summary>Eキー（＋ゲームパッド West）のみ。このフレームに押された</summary>
    public static bool InteractEKeyDown
    {
        get
        {
            try { if (Input.GetKeyDown(KeyCode.E)) return true; } catch { }
            var kb = Keyboard;
            if (kb != null && kb.eKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            return gp != null && gp.buttonWest.wasPressedThisFrame;
        }
    }

    /// <summary>Eキーまたは左クリックがこのフレームに押された（インタラクト）</summary>
    public static bool InteractDown
    {
        get
        {
            if (MouseLeftDown) return true;
            return InteractEKeyDown;
        }
    }

    /// <summary>FキーまたはゲームパッドY/△ボタンがこのフレームに押された（Rustへのパーツ回収・指示）</summary>
    public static bool CommandFetchDown
    {
        get
        {
            try { if (Input.GetKeyDown(KeyCode.F)) return true; } catch { }
            var kb = Keyboard;
            if (kb != null && kb.fKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            return gp != null && gp.buttonNorth.wasPressedThisFrame;
        }
    }

    /// <summary>Qキー、マウス中クリック、ゲームパッドLBがこのフレームに押された（Rustへの道案内リクエスト）</summary>
    public static bool GuideDown
    {
        get
        {
            try { if (Input.GetKeyDown(KeyCode.Q) || Input.GetMouseButtonDown(2)) return true; } catch { }
            var kb = Keyboard;
            if (kb != null && kb.qKey.wasPressedThisFrame) return true;
            var mouse = Mouse.current;
            if (mouse != null && mouse.middleButton.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            return gp != null && gp.leftShoulder.wasPressedThisFrame;
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

    /// <summary>Rキーが押されている（リセット誤爆防止の長押し判定用）</summary>
    public static bool ResetHeld
    {
        get
        {
            try { if (Input.GetKey(KeyCode.R)) return true; } catch { }
            var kb = Keyboard;
            return kb != null && kb.rKey.isPressed;
        }
    }

    /// <summary>Nキーがこのフレームに押された（ニューゲーム等）</summary>
    public static bool NDown
    {
        get
        {
            try { if (Input.GetKeyDown(KeyCode.N)) return true; } catch { }
            var kb = Keyboard;
            return kb != null && kb.nKey.wasPressedThisFrame;
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

    /// <summary>EscapeキーまたはゲームパッドStart/Menuボタンがこのフレームに押された</summary>
    public static bool EscapeDown
    {
        get
        {
            try { if (Input.GetKeyDown(KeyCode.Escape)) return true; } catch { }
            var kb = Keyboard;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            if (gp != null && (gp.startButton.wasPressedThisFrame || gp.selectButton.wasPressedThisFrame)) return true;
            return false;
        }
    }

    /// <summary>タイトルメニュー用 1〜4 キー（新旧 Input 両対応）</summary>
    public static bool TitleMenuDigitDown(int digit)
    {
        if (digit < 1 || digit > 4) return false;

        try
        {
            switch (digit)
            {
                case 1:
                    if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) return true;
                    break;
                case 2:
                    if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) return true;
                    break;
                case 3:
                    if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) return true;
                    break;
                case 4:
                    if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) return true;
                    break;
            }
        }
        catch { }

        var kb = Keyboard;
        if (kb == null) return false;

        return digit switch
        {
            1 => kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame,
            2 => kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame,
            3 => kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame,
            4 => kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame,
            _ => false
        };
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

    /// <summary>マウス右クリックがこのフレームに押された</summary>
    public static bool MouseRightDown
    {
        get
        {
            try { if (Input.GetMouseButtonDown(1)) return true; } catch { }
            var mouse = Mouse.current;
            return mouse != null && mouse.rightButton.wasPressedThisFrame;
        }
    }

    /// <summary>マウス左ボタンが押されている（長押し）</summary>
    public static bool MouseLeftHeld
    {
        get
        {
            try { if (Input.GetMouseButton(0)) return true; } catch { }
            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.isPressed;
        }
    }

    /// <summary>マウス右ボタンが押されている（長押し）</summary>
    public static bool MouseRightHeld
    {
        get
        {
            try { if (Input.GetMouseButton(1)) return true; } catch { }
            var mouse = Mouse.current;
            return mouse != null && mouse.rightButton.isPressed;
        }
    }

    /// <summary>ダイアログ送りボタン群（左クリック / Space / J / E / Enter / パッド南ボタン・スタートボタン）がこのフレームに押された</summary>
    public static bool DialogAdvanceDown
    {
        get
        {
            if (MouseLeftDown || SpaceDown || JDown || InteractDown || EnterDown) return true;
            var gp = Gamepad.current;
            return gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame);
        }
    }

    // ─── 内部ヘルパー ────────────────────────────────────────────────────

    static bool GetKey(KeyCode code)
    {
        try { return Input.GetKey(code); }
        catch { return false; }
    }
}
