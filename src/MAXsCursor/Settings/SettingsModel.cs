namespace MAXsCursor.Settings;

// Plain POCO serialized by System.Text.Json. Values are the only user-tunable
// knobs per current scope: cursor ring size, color, thickness.
internal sealed class SettingsModel
{
    // Bump when a saved value needs a one-time migration. Files written before versioning
    // have no field, which deserializes to 0.
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; set; }

    // Factory defaults tuned for Junliang's recording setup: green ring at ~36 px
    // with slightly thicker stroke for high-refresh screens, 28 px chips, 90% opacity.
    public double RingRadius { get; set; } = 36.0;
    public double RingThickness { get; set; } = 6.7;

    // RRGGBB hex. Alpha in the string is ignored; use RingOpacity for transparency.
    public string RingColor { get; set; } = "#02FF60";

    // 0.0 fully transparent, 1.0 fully opaque.
    public double RingOpacity { get; set; } = 0.9;

    // Keyboard HUD
    public bool HudEnabled { get; set; } = true;
    public double HudFontSize { get; set; } = 28.0;
    public bool ShowMouseButtons { get; set; } = true;

    // When false, HUD auto-follows the cursor's monitor bottom-center.
    // When true, HUD is pinned to (HudX, HudY) in physical pixels.
    public bool HudCustomPosition { get; set; } = false;
    public int HudX { get; set; } = 0;
    public int HudY { get; set; } = 0;

    // Global hotkeys. Modifiers encoded as MOD_ALT=1, MOD_CONTROL=2, MOD_SHIFT=4, MOD_WIN=8.
    // Vk is the raw Win32 virtual-key code (e.g. VK_F5 = 0x74 = 116, VK_F7 = 0x76 = 118).
    public uint ToggleHotkeyMods { get; set; } = 0x0001;   // Alt
    public uint ToggleHotkeyVk { get; set; } = 0x74;       // F5
    // Alt+F7, grouped with Alt+F5 / Alt+F6. The old default Ctrl+2 was registered globally
    // and stole Ctrl+2 from every other app (e.g. "go to tab 2" in browsers).
    public uint ZoomHotkeyMods { get; set; } = 0x0001;     // Alt
    public uint ZoomHotkeyVk { get; set; } = 0x76;         // F7

    // --- Click ripple mode (coloured ripple on each click) ---
    // The on/off state persists, shared by the settings checkbox and the Alt+F6 hotkey.

    // Click ripple toggle hotkey. Default Alt+F6 (VK_F6 = 0x75).
    public uint PresentationHotkeyMods { get; set; } = 0x0001; // Alt
    public uint PresentationHotkeyVk { get; set; } = 0x75;     // F6

    // Click ripple: expanding ring per click, distinct colour per button.
    public bool ClickRippleEnabled { get; set; } = true;
    public string LeftClickColor { get; set; } = "#FFE000";    // yellow
    public string MiddleClickColor { get; set; } = "#00E676";  // green
    public string RightClickColor { get; set; } = "#2979FF";   // blue
    public double RippleMaxRadius { get; set; } = 48.0;        // dip
    public int RippleDurationMs { get; set; } = 420;

    // UI language. "zh" or "en". Default "zh" since the primary user is Chinese-speaking.
    public string Language { get; set; } = "zh";

    public static SettingsModel Defaults() => new() { SchemaVersion = CurrentSchemaVersion };

    // One-time upgrades for files saved by older versions. Returns true if anything changed.
    public bool Migrate()
    {
        if (SchemaVersion >= CurrentSchemaVersion) return false;

        // v0 -> v1: move the zoom hotkey off Ctrl+2 unless the user picked something else.
        if (SchemaVersion < 1 && ZoomHotkeyMods == 0x0002 && ZoomHotkeyVk == 0x32)
        {
            ZoomHotkeyMods = 0x0001;
            ZoomHotkeyVk = 0x76;
        }

        SchemaVersion = CurrentSchemaVersion;
        return true;
    }

    public SettingsModel Clone() => new()
    {
        SchemaVersion = SchemaVersion,
        RingRadius = RingRadius,
        RingThickness = RingThickness,
        RingColor = RingColor,
        RingOpacity = RingOpacity,
        HudEnabled = HudEnabled,
        HudFontSize = HudFontSize,
        ShowMouseButtons = ShowMouseButtons,
        HudCustomPosition = HudCustomPosition,
        HudX = HudX,
        HudY = HudY,
        ToggleHotkeyMods = ToggleHotkeyMods,
        ToggleHotkeyVk = ToggleHotkeyVk,
        ZoomHotkeyMods = ZoomHotkeyMods,
        ZoomHotkeyVk = ZoomHotkeyVk,
        PresentationHotkeyMods = PresentationHotkeyMods,
        PresentationHotkeyVk = PresentationHotkeyVk,
        ClickRippleEnabled = ClickRippleEnabled,
        LeftClickColor = LeftClickColor,
        MiddleClickColor = MiddleClickColor,
        RightClickColor = RightClickColor,
        RippleMaxRadius = RippleMaxRadius,
        RippleDurationMs = RippleDurationMs,
        Language = Language
    };
}
