namespace MAXsCursor.Settings;

// Plain POCO serialized by System.Text.Json. Values are the only user-tunable
// knobs per current scope: cursor ring size, color, thickness.
internal sealed class SettingsModel
{
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

    // UI language. "zh" or "en". Default "zh" since the primary user is Chinese-speaking.
    public string Language { get; set; } = "zh";

    public static SettingsModel Defaults() => new();

    public SettingsModel Clone() => new()
    {
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
        Language = Language
    };
}
