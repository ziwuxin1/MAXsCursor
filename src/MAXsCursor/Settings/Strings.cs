namespace MAXsCursor.Settings;

// Two-language string table. The app uses plain code-behind (no MVVM, no .resx),
// so screens that need localization call ApplyStrings() after each language switch.
internal static class Strings
{
    private static bool _en;
    public static bool IsEnglish => _en;

    public static void SetLanguage(string lang) => _en = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);

    private const string AppName = "MAXs Cursor v 1.2.1";

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

    // Click ripple section
    public static string SectionPresentation => _en ? "Click ripple" : "点击水波纹";
    public static string RippleEnabled => _en ? "Enable ripple" : "启用水波纹";
    public static string RippleLeftColor => _en ? "Left click color" : "左键颜色";
    public static string RippleMiddleColor => _en ? "Middle click color" : "中键颜色";
    public static string RippleRightColor => _en ? "Right click color" : "右键颜色";
    public static string RippleSize => _en ? "Ripple size" : "水波纹大小";
    public static string RippleDuration => _en ? "Ripple duration" : "水波纹时长";

    // Mouse button chip text
    public static string MouseLeft => _en ? "Left click" : "左键";
    public static string MouseRight => _en ? "Right click" : "右键";
    public static string MouseMiddle => _en ? "Middle click" : "中键";
    public static string MouseWheelUp => _en ? "Wheel ↑" : "滚轮↑";
    public static string MouseWheelDown => _en ? "Wheel ↓" : "滚轮↓";

    // Zoom / annotation window
    public static string ZoomTitle => _en ? $"{AppName} - Zoom" : $"{AppName} - 屏幕放大";
    public static string ZoomThickness => _en ? "Thickness" : "粗细";
    public static string ZoomPen => _en ? "Pen" : "画笔";
    public static string ZoomEraser => _en ? "Eraser" : "橡皮";
    public static string ZoomClear => _en ? "Clear" : "清除";
    public static string ZoomSave => _en ? "Save" : "保存";
    public static string ZoomCopy => _en ? "Copy" : "复制";
    public static string ZoomExit => _en ? "Exit" : "退出";
    public static string ZoomSaved => _en ? "Saved ✓" : "已保存 ✓";
    public static string ZoomSaveFailed => _en ? "Save failed" : "保存失败";
    public static string ZoomCopied => _en ? "Copied to clipboard ✓" : "已复制到剪贴板 ✓";
    public static string ZoomCopyFailed => _en ? "Copy failed" : "复制失败";
    public static string ZoomHint => _en
        ? "Left drag: draw / erase.  Wheel: zoom.  Middle drag or Space+drag: pan.\nB: pen.  E: eraser.  1-7: color.  +/-: thickness.\nC: clear.  Ctrl+Z: undo.  Ctrl+S: save PNG.  Ctrl+C: copy.  Esc: exit."
        : "左键拖动：画笔 / 擦除。滚轮：缩放。中键拖 / 空格+拖：平移。\nB：画笔。E：橡皮。1-7：颜色。+/-：粗细。\nC：清除。Ctrl+Z：撤销。Ctrl+S：保存 PNG。Ctrl+C：复制。Esc：退出。";
    public static string AdjustPosition => _en ? "Adjust position..." : "调整位置...";
    public static string PosAutoFollow => _en ? "Follow cursor's monitor" : "自动跟随鼠标显示器";
    public static string PosCustom(int x, int y) => _en ? $"Custom ({x}, {y})" : $"自定义 ({x}, {y})";
    public static string Reset => _en ? "Reset" : "恢复默认";
    public static string Ok => _en ? "OK" : "确定";
    public static string Cancel => _en ? "Cancel" : "取消";
    public static string LanguageLabel => _en ? "Language" : "语言";

    // Shortcut section
    public static string SectionShortcuts => _en ? "Shortcuts" : "快捷键";
    public static string ShortcutToggleLabel => _en ? "Toggle overlay" : "开关叠加层";
    public static string ShortcutToggleHint => _en ? "Show / hide the cursor ring and key HUD" : "显示 / 隐藏光标环和按键 HUD";
    public static string ShortcutZoomLabel => _en ? "Zoom + annotate" : "放大并标注";
    public static string ShortcutZoomHint => _en ? "Freeze the screen, zoom in, draw on top" : "冻结屏幕，放大后可以画图";
    public static string ShortcutPresentationLabel => _en ? "Click ripple" : "点击水波纹";
    public static string ShortcutPresentationHint => _en ? "Coloured ripple on each click" : "点击时显示彩色水波纹";
    public static string ShortcutCapturePrompt => _en ? "Press new shortcut..." : "按下新快捷键...";
    public static string ShortcutConflictFail => _en ? "Shortcut unavailable, another app may own it" : "快捷键不可用，可能被其它程序占用";
    public static string ShortcutNeedsMod => _en ? "Need a modifier key (Ctrl / Shift / Alt / Win)" : "需要一个修饰键 (Ctrl / Shift / Alt / Win)";

    // How-to-use section
    public static string HelpHeader => _en ? "How to use" : "使用方法";
    public static string HelpBody => _en ? HelpBodyEn : HelpBodyZh;

    private const string HelpBodyZh = @"【光标环】
• 启动后自动在鼠标周围显示，近乎零延迟。
• 颜色 / 大小 / 厚度 / 透明度在上面的 ""光标环"" 区调整，拖动即实时生效。

【按键显示】
• 按下键盘按键时，屏幕中下方弹出 chip。组合键（Ctrl+Shift+S）合并为一个 chip。
• 鼠标左键 / 右键 / 中键 / 滚轮也会显示，可独立开关。
• 位置默认跟随鼠标所在显示器，点 ""调整位置..."" 可以手动固定到任何位置。
• 字号和是否显示在 ""按键显示"" 区调整。

【开关叠加层】
• 默认快捷键 Alt+F5，可在上方 ""快捷键"" 区重新指定。
• 托盘图标右键菜单或双击也能切换；禁用时托盘图标变灰。

【放大并标注】
• 默认快捷键 Ctrl+2（可自定义）。按下后屏幕冻结并放大 2 倍。
• 左键拖 = 画笔；切到橡皮后左键拖 = 擦除，橡皮大小跟粗细滑块联动。
• 滚轮 = 缩放 1×–5×；中键拖 或 空格+左键拖 = 平移视角。
• 数字键 1–7 = 切换颜色；+ / - = 粗细；B = 画笔；E = 橡皮；C = 清除；Ctrl+Z = 撤销。
• Ctrl+S = 保存为 PNG；Ctrl+C = 复制到剪贴板；Esc = 退出。

【点击水波纹】
• 勾选 ""启用水波纹"" 即可开启；快捷键 Alt+F6 是同一个开关，按一下切换（快捷键可改）。
• 开启后，左键 / 中键 / 右键点击时会从鼠标处冒出不同颜色的水波纹，方便观众看清点了哪里。
• 颜色 / 大小 / 时长在 ""点击水波纹"" 区调整。

【系统托盘】
• 右键菜单：启用 / 禁用、设置、退出。
• 双击托盘图标：切换启用 / 禁用。";

    private const string HelpBodyEn = @"Cursor ring
• Follows the system cursor with near-zero latency.
• Color / radius / thickness / opacity tune live from the Cursor ring section above.

Keyboard display
• Each keypress shows up as a chip near the bottom of the current monitor. Combos merge: Ctrl+Shift+S is one chip, not three.
• Mouse left / right / middle / wheel also chip; each has its own toggle.
• Position follows the cursor's monitor by default. Click Adjust position... to pin it anywhere.
• Font size and visibility live in the Keyboard display section.

Toggle overlay
• Alt+F5 by default. Rebind it in the Shortcuts section.
• Tray icon right-click or double-click toggles too. Icon goes grey when disabled.

Zoom + annotate
• Ctrl+2 by default (rebindable). Freezes the current monitor and zooms 2x.
• Left drag = pen. Switch to eraser for erase; eraser size tracks the thickness slider.
• Wheel = zoom 1x–5x. Middle drag or Space+left drag = pan.
• 1–7 = color, + / - = thickness, B = pen, E = eraser, C = clear, Ctrl+Z = undo.
• Ctrl+S = save as PNG, Ctrl+C = copy to clipboard, Esc = exit.

Click ripple
• Tick ""Enable ripple"" to turn it on. Alt+F6 is the same switch, press to toggle (rebindable).
• When on, left / middle / right clicks emit a coloured ripple from the pointer so viewers see exactly where you clicked.
• Color / size / duration tune in the Click ripple section.

System tray
• Right-click menu: Enable / Disable, Settings, Quit.
• Double-click: toggle enable / disable.";

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
