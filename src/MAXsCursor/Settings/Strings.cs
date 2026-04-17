namespace MAXsCursor.Settings;

// Two-language string table. The app uses plain code-behind (no MVVM, no .resx),
// so screens that need localization call ApplyStrings() after each language switch.
internal static class Strings
{
    private static bool _en;
    public static bool IsEnglish => _en;

    public static void SetLanguage(string lang) => _en = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);

    private const string AppName = "MAXs Cursor v 1.0";

    // Settings window
    public static string AppTitle => _en ? $"{AppName} Settings" : $"{AppName} 设置";
    public static string SectionRing => _en ? "Cursor Ring" : "光标环";
    public static string SectionHud => _en ? "Keyboard Display" : "按键显示";
    public static string RingSize => _en ? "Ring size" : "圆圈大小";
    public static string RingThickness => _en ? "Ring thickness" : "圆圈厚度";
    public static string Opacity => _en ? "Opacity" : "透明度";
    public static string Color => _en ? "Color" : "颜色";
    public static string Saturation => _en ? "Saturation" : "饱和度";
    public static string Lightness => _en ? "Lightness" : "亮度";
    public static string ShowKeys => _en ? "Show keys" : "显示按键";
    public static string ShowMouseButtons => _en ? "Show mouse buttons" : "显示鼠标按键";
    public static string KeyFontSize => _en ? "Key text size" : "按键文字大小";

    // Mouse button chip text
    public static string MouseLeft => _en ? "Left click" : "左键";
    public static string MouseRight => _en ? "Right click" : "右键";
    public static string MouseMiddle => _en ? "Middle click" : "中键";
    public static string MouseWheelUp => _en ? "Wheel ↑" : "滚轮↑";
    public static string MouseWheelDown => _en ? "Wheel ↓" : "滚轮↓";
    public static string AdjustPosition => _en ? "Adjust position..." : "调整位置...";
    public static string PosAutoFollow => _en ? "Follow cursor's monitor" : "自动跟随鼠标显示器";
    public static string PosCustom(int x, int y) => _en ? $"Custom ({x}, {y})" : $"自定义 ({x}, {y})";
    public static string Reset => _en ? "Reset" : "恢复默认";
    public static string Ok => _en ? "OK" : "确定";
    public static string Cancel => _en ? "Cancel" : "取消";
    public static string LanguageLabel => _en ? "Language" : "语言";

    // Picker
    public static string PickerTitle => _en ? $"{AppName} - Adjust HUD position" : $"{AppName} - 调整按键显示位置";
    public static string DragHint => _en
        ? "Drag the whole panel to where you want the shortcut display to appear"
        : "拖动整个面板到希望按键显示出现的位置";

    // Tray
    public static string TrayEnable => _en ? "Enable" : "启用";
    public static string TrayDisable => _en ? "Disable" : "禁用";
    public static string TraySettings => _en ? "Settings..." : "设置...";
    public static string TrayQuit => _en ? "Quit" : "退出";
    public static string TrayTooltipOn => _en ? $"{AppName} (on)" : $"{AppName} (开)";
    public static string TrayTooltipOff => _en ? $"{AppName} (off)" : $"{AppName} (关)";
}
