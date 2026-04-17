using System.Text;
using MAXsCursor.Interop;
using MAXsCursor.Settings;

namespace MAXsCursor.Core;

internal static class KeyTranslator
{
    // HUD policy:
    //   1. A non-modifier keypress with held modifiers emits "Ctrl+Shift+S"-style chip.
    //   2. A modifier key that is pressed and released WITHOUT combining with anything
    //      else emits its own chip on release: "Alt" / "Ctrl" / "Shift" / "Win".
    //   3. CapsLock is suppressed entirely because it is a stateful toggle.
    //
    // Routing: HookManager decides which events to enqueue. This method only translates.
    public static string? ToDisplayText(uint vk, ModifierMask mods)
    {
        // Caller only enqueues a modifier with mods=None when it's a bare release.
        if (IsBareModifierKey(vk) && mods == ModifierMask.None)
        {
            return BareModifierName(vk);
        }

        // Skip CapsLock (toggle, not a shortcut).
        if (vk == Win32.VK_CAPITAL) return null;

        // A modifier event with mods attached shouldn't happen in the current pipeline,
        // but skip defensively so it cannot produce a weird "Shift+Shift" string.
        if (IsBareModifierKey(vk)) return null;

        var keyText = VkToText(vk);
        if (keyText is null) return null;

        if (mods == ModifierMask.None) return keyText;

        var sb = new StringBuilder(24);
        if ((mods & ModifierMask.Ctrl) != 0) sb.Append("Ctrl+");
        if ((mods & ModifierMask.Shift) != 0) sb.Append("Shift+");
        if ((mods & ModifierMask.Alt) != 0) sb.Append("Alt+");
        if ((mods & ModifierMask.Win) != 0) sb.Append("Win+");
        sb.Append(keyText);
        return sb.ToString();
    }

    public static string? MouseToDisplayText(MouseButton button, ModifierMask mods)
    {
        var text = button switch
        {
            MouseButton.Left => Strings.MouseLeft,
            MouseButton.Right => Strings.MouseRight,
            MouseButton.Middle => Strings.MouseMiddle,
            MouseButton.X1 => "Mouse 4",
            MouseButton.X2 => "Mouse 5",
            MouseButton.WheelUp => Strings.MouseWheelUp,
            MouseButton.WheelDown => Strings.MouseWheelDown,
            _ => null
        };
        if (text is null) return null;

        if (mods == ModifierMask.None) return text;

        var sb = new StringBuilder(32);
        if ((mods & ModifierMask.Ctrl) != 0) sb.Append("Ctrl+");
        if ((mods & ModifierMask.Shift) != 0) sb.Append("Shift+");
        if ((mods & ModifierMask.Alt) != 0) sb.Append("Alt+");
        if ((mods & ModifierMask.Win) != 0) sb.Append("Win+");
        sb.Append(text);
        return sb.ToString();
    }

    // Format a (mods, vk) pair for display in a hotkey button. Works with both
    // RegisterHotKey mod flags (MOD_ALT=1, MOD_CONTROL=2, MOD_SHIFT=4, MOD_WIN=8)
    // and Win32 virtual-key codes.
    public static string FormatHotkey(uint mods, uint vk)
    {
        var sb = new StringBuilder(24);
        if ((mods & Interop.WindowStyles.MOD_CONTROL) != 0) sb.Append("Ctrl+");
        if ((mods & Interop.WindowStyles.MOD_SHIFT) != 0) sb.Append("Shift+");
        if ((mods & Interop.WindowStyles.MOD_ALT) != 0) sb.Append("Alt+");
        if ((mods & Interop.WindowStyles.MOD_WIN) != 0) sb.Append("Win+");
        sb.Append(VkToText(vk) ?? $"VK_{vk:X2}");
        return sb.ToString();
    }

    public static bool IsBareModifierKey(uint vk) => vk switch
    {
        Win32.VK_SHIFT or Win32.VK_LSHIFT or Win32.VK_RSHIFT => true,
        Win32.VK_CONTROL or Win32.VK_LCONTROL or Win32.VK_RCONTROL => true,
        Win32.VK_MENU or Win32.VK_LMENU or Win32.VK_RMENU => true,
        Win32.VK_LWIN or Win32.VK_RWIN => true,
        _ => false
    };

    private static string BareModifierName(uint vk) => vk switch
    {
        Win32.VK_SHIFT or Win32.VK_LSHIFT or Win32.VK_RSHIFT => "Shift",
        Win32.VK_CONTROL or Win32.VK_LCONTROL or Win32.VK_RCONTROL => "Ctrl",
        Win32.VK_MENU or Win32.VK_LMENU or Win32.VK_RMENU => "Alt",
        Win32.VK_LWIN or Win32.VK_RWIN => "Win",
        _ => string.Empty
    };

    private static string? VkToText(uint vk)
    {
        // Digits 0-9 on the main row: VK codes 0x30-0x39 map to '0'-'9'.
        if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
        // Letters A-Z: VK codes 0x41-0x5A map to 'A'-'Z'.
        if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
        // Numpad digits 0-9.
        if (vk >= 0x60 && vk <= 0x69) return "Num" + (vk - 0x60).ToString();
        // Function keys F1..F24.
        if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x6F).ToString();

        return vk switch
        {
            Win32.VK_BACK => "Backspace",
            Win32.VK_TAB => "Tab",
            Win32.VK_RETURN => "Enter",
            Win32.VK_PAUSE => "Pause",
            Win32.VK_ESCAPE => "Esc",
            Win32.VK_SPACE => "Space",
            Win32.VK_PRIOR => "PgUp",
            Win32.VK_NEXT => "PgDn",
            Win32.VK_END => "End",
            Win32.VK_HOME => "Home",
            Win32.VK_LEFT => "Left",
            Win32.VK_UP => "Up",
            Win32.VK_RIGHT => "Right",
            Win32.VK_DOWN => "Down",
            Win32.VK_INSERT => "Ins",
            Win32.VK_DELETE => "Del",
            0x6A => "Num*",
            0x6B => "Num+",
            0x6D => "Num-",
            0x6E => "Num.",
            0x6F => "Num/",
            0xBA => ";",
            0xBB => "=",
            0xBC => ",",
            0xBD => "-",
            0xBE => ".",
            0xBF => "/",
            0xC0 => "`",
            0xDB => "[",
            0xDC => "\\",
            0xDD => "]",
            0xDE => "'",
            _ => null
        };
    }
}
