using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MAXsCursor.Core;
using MAXsCursor.Interop;
using MediaColor = System.Windows.Media.Color;
using MediaBrush = System.Windows.Media.SolidColorBrush;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace MAXsCursor.Settings;

internal partial class SettingsWindow : Window
{
    private readonly SettingsModel _model;
    private readonly SettingsModel _original;
    private readonly Action<SettingsModel> _onChanged;
    private bool _suppressChange;

    public SettingsWindow(SettingsModel current, Action<SettingsModel> onChanged)
    {
        InitializeComponent();
        _model = current.Clone();
        _original = current.Clone();
        _onChanged = onChanged;

        LoadFromModel();
        ApplyStrings();

        RadiusSlider.ValueChanged += (_, _) => OnRadiusChanged();
        ThicknessSlider.ValueChanged += (_, _) => OnThicknessChanged();
        OpacitySlider.ValueChanged += (_, _) => OnOpacityChanged();
        HueSlider.ValueChanged += (_, _) => OnColorSliderChanged();
        SaturationSlider.ValueChanged += (_, _) => OnColorSliderChanged();
        LightnessSlider.ValueChanged += (_, _) => OnColorSliderChanged();
        HudFontSizeSlider.ValueChanged += (_, _) => OnHudFontSizeChanged();
        HudEnabledCheck.Checked += (_, _) => OnHudEnabledChanged();
        HudEnabledCheck.Unchecked += (_, _) => OnHudEnabledChanged();
        MouseButtonsCheck.Checked += (_, _) => OnMouseButtonsChanged();
        MouseButtonsCheck.Unchecked += (_, _) => OnMouseButtonsChanged();
        PresSizeSlider.ValueChanged += (_, _) => OnPresAppearanceChanged();
        PresHoleSlider.ValueChanged += (_, _) => OnPresAppearanceChanged();
        PresBorderSlider.ValueChanged += (_, _) => OnPresAppearanceChanged();
        PresOpacitySlider.ValueChanged += (_, _) => OnPresAppearanceChanged();
        PresHueSlider.ValueChanged += (_, _) => OnPresColorChanged();
        PresSatSlider.ValueChanged += (_, _) => OnPresColorChanged();
        PresLightSlider.ValueChanged += (_, _) => OnPresColorChanged();
        RippleEnabledCheck.Checked += (_, _) => OnRippleSettingsChanged();
        RippleEnabledCheck.Unchecked += (_, _) => OnRippleSettingsChanged();
        RippleLeftHueSlider.ValueChanged += (_, _) => OnRippleColorChanged();
        RippleMiddleHueSlider.ValueChanged += (_, _) => OnRippleColorChanged();
        RippleRightHueSlider.ValueChanged += (_, _) => OnRippleColorChanged();
        RippleSizeSlider.ValueChanged += (_, _) => OnRippleSettingsChanged();
        RippleDurationSlider.ValueChanged += (_, _) => OnRippleSettingsChanged();

        // PreviewKeyDown so capture fires before any TextBox or ComboBox can eat the event.
        PreviewKeyDown += OnCaptureKey;
        Closed += (_, _) => EndCapture(commit: false);
    }

    private void LoadFromModel()
    {
        _suppressChange = true;
        RadiusSlider.Value = _model.RingRadius;
        ThicknessSlider.Value = _model.RingThickness;
        OpacitySlider.Value = _model.RingOpacity;

        var rgb = ColorParse.Parse(_model.RingColor);
        var (h, s, l) = RgbToHsl(rgb.R, rgb.G, rgb.B);
        HueSlider.Value = h;
        SaturationSlider.Value = s;
        LightnessSlider.Value = Math.Clamp(l, LightnessSlider.Minimum, LightnessSlider.Maximum);

        HudEnabledCheck.IsChecked = _model.HudEnabled;
        MouseButtonsCheck.IsChecked = _model.ShowMouseButtons;
        HudFontSizeSlider.Value = Math.Clamp(_model.HudFontSize, HudFontSizeSlider.Minimum, HudFontSizeSlider.Maximum);
        PresSizeSlider.Value = Math.Clamp(_model.BigCursorSize, PresSizeSlider.Minimum, PresSizeSlider.Maximum);
        PresHoleSlider.Value = Math.Clamp(_model.BigCursorHoleSize, PresHoleSlider.Minimum, PresHoleSlider.Maximum);
        PresBorderSlider.Value = Math.Clamp(_model.BigCursorBorderThickness, PresBorderSlider.Minimum, PresBorderSlider.Maximum);
        PresOpacitySlider.Value = Math.Clamp(_model.BigCursorOpacity, PresOpacitySlider.Minimum, PresOpacitySlider.Maximum);

        var presRgb = ColorParse.Parse(_model.BigCursorColor);
        var (ph, ps, pl) = RgbToHsl(presRgb.R, presRgb.G, presRgb.B);
        PresHueSlider.Value = ph;
        PresSatSlider.Value = ps;
        PresLightSlider.Value = Math.Clamp(pl, PresLightSlider.Minimum, PresLightSlider.Maximum);

        RippleEnabledCheck.IsChecked = _model.ClickRippleEnabled;
        RippleLeftHueSlider.Value = HueOf(_model.LeftClickColor);
        RippleMiddleHueSlider.Value = HueOf(_model.MiddleClickColor);
        RippleRightHueSlider.Value = HueOf(_model.RightClickColor);
        RippleSizeSlider.Value = Math.Clamp(_model.RippleMaxRadius, RippleSizeSlider.Minimum, RippleSizeSlider.Maximum);
        RippleDurationSlider.Value = Math.Clamp(_model.RippleDurationMs, RippleDurationSlider.Minimum, RippleDurationSlider.Maximum);

        // Select matching ComboBoxItem by Tag without triggering SelectionChanged.
        foreach (ComboBoxItem item in LanguageCombo.Items)
        {
            if (string.Equals(item.Tag as string, _model.Language, StringComparison.OrdinalIgnoreCase))
            {
                LanguageCombo.SelectedItem = item;
                break;
            }
        }

        UpdateLabels();
        UpdateSwatch();
        UpdatePresSwatch();
        UpdateRippleSwatches();
        UpdatePositionStatus();
        _suppressChange = false;
    }

    // Applies the current Strings.* table to every visible text in this window.
    // Called on construct and every time the user picks a new language.
    private void ApplyStrings()
    {
        Title = Strings.AppTitle;
        LanguageLabelText.Text = Strings.LanguageLabel;
        SectionRingText.Text = Strings.SectionRing;
        RadiusText.Text = Strings.RingSize;
        ThicknessText.Text = Strings.RingThickness;
        OpacityText.Text = Strings.Opacity;
        ColorText.Text = Strings.Color;
        SaturationText.Text = Strings.Saturation;
        LightnessText.Text = Strings.Lightness;
        SectionHudText.Text = Strings.SectionHud;
        HudEnabledCheck.Content = Strings.ShowKeys;
        MouseButtonsCheck.Content = Strings.ShowMouseButtons;
        HudFontSizeText.Text = Strings.KeyFontSize;
        SectionPresentationText.Text = Strings.SectionPresentation;
        PresSizeText.Text = Strings.PresBigSize;
        PresHoleText.Text = Strings.PresHole;
        PresBorderText.Text = Strings.PresBorder;
        PresOpacityText.Text = Strings.PresOpacity;
        PresColorText.Text = Strings.PresColor;
        RippleEnabledCheck.Content = Strings.RippleEnabled;
        RippleLeftColorText.Text = Strings.RippleLeftColor;
        RippleMiddleColorText.Text = Strings.RippleMiddleColor;
        RippleRightColorText.Text = Strings.RippleRightColor;
        RippleSizeText.Text = Strings.RippleSize;
        RippleDurationText.Text = Strings.RippleDuration;
        AdjustPositionButton.Content = Strings.AdjustPosition;
        SectionShortcutsText.Text = Strings.SectionShortcuts;
        ShortcutToggleLabelText.Text = Strings.ShortcutToggleLabel;
        ShortcutToggleHintText.Text = Strings.ShortcutToggleHint;
        ShortcutZoomLabelText.Text = Strings.ShortcutZoomLabel;
        ShortcutZoomHintText.Text = Strings.ShortcutZoomHint;
        ToggleHotkeyButton.Content = KeyTranslator.FormatHotkey(_model.ToggleHotkeyMods, _model.ToggleHotkeyVk);
        ZoomHotkeyButton.Content = KeyTranslator.FormatHotkey(_model.ZoomHotkeyMods, _model.ZoomHotkeyVk);
        ShortcutPresentationLabelText.Text = Strings.ShortcutPresentationLabel;
        ShortcutPresentationHintText.Text = Strings.ShortcutPresentationHint;
        PresentationHotkeyButton.Content = KeyTranslator.FormatHotkey(_model.PresentationHotkeyMods, _model.PresentationHotkeyVk);
        HelpHeaderText.Text = Strings.HelpHeader;
        HelpBodyText.Text = Strings.HelpBody;
        ResetButton.Content = Strings.Reset;
        OkButton.Content = Strings.Ok;
        CancelButton.Content = Strings.Cancel;
        UpdatePositionStatus();
    }

    private void UpdatePositionStatus()
    {
        PositionStatusLabel.Text = _model.HudCustomPosition
            ? Strings.PosCustom(_model.HudX, _model.HudY)
            : Strings.PosAutoFollow;
    }

    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressChange) return;
        if (LanguageCombo.SelectedItem is not ComboBoxItem item) return;
        var lang = item.Tag as string ?? "zh";
        if (lang == _model.Language) return;
        _model.Language = lang;
        Strings.SetLanguage(lang);
        ApplyStrings();
        _onChanged(_model);
    }

    private void OnRadiusChanged()
    {
        if (_suppressChange) return;
        _model.RingRadius = RadiusSlider.Value;
        UpdateLabels();
        _onChanged(_model);
    }

    private void OnThicknessChanged()
    {
        if (_suppressChange) return;
        _model.RingThickness = ThicknessSlider.Value;
        UpdateLabels();
        _onChanged(_model);
    }

    private void OnOpacityChanged()
    {
        if (_suppressChange) return;
        _model.RingOpacity = OpacitySlider.Value;
        UpdateLabels();
        UpdateSwatch();
        _onChanged(_model);
    }

    private void OnColorSliderChanged()
    {
        if (_suppressChange) return;
        var (r, g, b) = HslToRgb(HueSlider.Value, SaturationSlider.Value, LightnessSlider.Value);
        _model.RingColor = $"#{r:X2}{g:X2}{b:X2}";
        UpdateLabels();
        UpdateSwatch();
        _onChanged(_model);
    }

    private void OnHudFontSizeChanged()
    {
        if (_suppressChange) return;
        _model.HudFontSize = HudFontSizeSlider.Value;
        UpdateLabels();
        _onChanged(_model);
    }

    private void OnHudEnabledChanged()
    {
        if (_suppressChange) return;
        _model.HudEnabled = HudEnabledCheck.IsChecked == true;
        _onChanged(_model);
    }

    private void OnMouseButtonsChanged()
    {
        if (_suppressChange) return;
        _model.ShowMouseButtons = MouseButtonsCheck.IsChecked == true;
        _onChanged(_model);
    }

    private void OnPresAppearanceChanged()
    {
        if (_suppressChange) return;
        _model.BigCursorSize = PresSizeSlider.Value;
        _model.BigCursorHoleSize = PresHoleSlider.Value;
        _model.BigCursorBorderThickness = PresBorderSlider.Value;
        _model.BigCursorOpacity = PresOpacitySlider.Value;
        UpdateLabels();
        UpdatePresSwatch();
        _onChanged(_model);
    }

    private void OnPresColorChanged()
    {
        if (_suppressChange) return;
        var (r, g, b) = HslToRgb(PresHueSlider.Value, PresSatSlider.Value, PresLightSlider.Value);
        _model.BigCursorColor = $"#{r:X2}{g:X2}{b:X2}";
        UpdatePresSwatch();
        _onChanged(_model);
    }

    private void OnRippleSettingsChanged()
    {
        if (_suppressChange) return;
        _model.ClickRippleEnabled = RippleEnabledCheck.IsChecked == true;
        _model.RippleMaxRadius = RippleSizeSlider.Value;
        _model.RippleDurationMs = (int)Math.Round(RippleDurationSlider.Value);
        UpdateLabels();
        _onChanged(_model);
    }

    private void OnRippleColorChanged()
    {
        if (_suppressChange) return;
        _model.LeftClickColor = HueToHex(RippleLeftHueSlider.Value);
        _model.MiddleClickColor = HueToHex(RippleMiddleHueSlider.Value);
        _model.RightClickColor = HueToHex(RippleRightHueSlider.Value);
        UpdateRippleSwatches();
        _onChanged(_model);
    }

    // Ripple colours are vivid: fixed saturation 1.0 and lightness 0.5, hue chosen by slider.
    private static string HueToHex(double hue)
    {
        var (r, g, b) = HslToRgb(hue, 1.0, 0.5);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static double HueOf(string hex)
    {
        var rgb = ColorParse.Parse(hex);
        var (h, _, _) = RgbToHsl(rgb.R, rgb.G, rgb.B);
        return h;
    }

    private void UpdatePresSwatch()
    {
        var rgb = ColorParse.Parse(_model.BigCursorColor);
        var alpha = (byte)Math.Round(255 * _model.BigCursorOpacity);
        PresColorSwatch.Background = new MediaBrush(MediaColor.FromArgb(alpha, rgb.R, rgb.G, rgb.B));
    }

    private void UpdateRippleSwatches()
    {
        var l = ColorParse.Parse(_model.LeftClickColor);
        var m = ColorParse.Parse(_model.MiddleClickColor);
        var r = ColorParse.Parse(_model.RightClickColor);
        RippleLeftSwatch.Background = new MediaBrush(MediaColor.FromRgb(l.R, l.G, l.B));
        RippleMiddleSwatch.Background = new MediaBrush(MediaColor.FromRgb(m.R, m.G, m.B));
        RippleRightSwatch.Background = new MediaBrush(MediaColor.FromRgb(r.R, r.G, r.B));
    }

    private void UpdateLabels()
    {
        RadiusLabel.Text = $"{RadiusSlider.Value:F0} px";
        ThicknessLabel.Text = $"{ThicknessSlider.Value:F1} px";
        OpacityLabel.Text = $"{OpacitySlider.Value * 100:F0}%";
        SaturationLabel.Text = $"{SaturationSlider.Value * 100:F0}%";
        LightnessLabel.Text = $"{LightnessSlider.Value * 100:F0}%";
        HudFontSizeLabel.Text = $"{HudFontSizeSlider.Value:F0} px";
        PresSizeLabel.Text = $"{PresSizeSlider.Value:F0} px";
        PresHoleLabel.Text = $"{PresHoleSlider.Value:F0} px";
        PresBorderLabel.Text = $"{PresBorderSlider.Value:F1} px";
        PresOpacityLabel.Text = $"{PresOpacitySlider.Value * 100:F0}%";
        RippleSizeLabel.Text = $"{RippleSizeSlider.Value:F0} px";
        RippleDurationLabel.Text = $"{RippleDurationSlider.Value:F0} ms";
    }

    private void UpdateSwatch()
    {
        var rgb = ColorParse.Parse(_model.RingColor);
        var alpha = (byte)Math.Round(255 * _model.RingOpacity);
        ColorSwatch.Background = new MediaBrush(MediaColor.FromArgb(alpha, rgb.R, rgb.G, rgb.B));
    }

    private void AdjustPosition_Click(object sender, RoutedEventArgs e)
    {
        Hide();

        var (startX, startY) = GetPickerStartPosition();
        var picker = new HudPositionPicker(startX, startY, _model.HudFontSize);
        picker.ShowDialog();

        if (picker.Confirmed)
        {
            _model.HudCustomPosition = true;
            _model.HudX = picker.ResultX;
            _model.HudY = picker.ResultY;
            UpdatePositionStatus();
            _onChanged(_model);
        }

        Show();
        Activate();
    }

    private (int x, int y) GetPickerStartPosition()
    {
        if (_model.HudCustomPosition) return (_model.HudX, _model.HudY);

        if (!Win32.GetCursorPos(out var pt)) return (400, 400);

        var hmon = Win32.MonitorFromPoint(pt, Win32.MONITOR_DEFAULTTONEAREST);
        if (hmon == nint.Zero) return (pt.X - 120, pt.Y - 60);

        var mi = new Win32.MONITORINFO
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.MONITORINFO>()
        };
        if (!Win32.GetMonitorInfo(hmon, ref mi)) return (pt.X - 120, pt.Y - 60);

        var cx = mi.rcWork.Left + (mi.rcWork.Right - mi.rcWork.Left) / 2 - 100;
        var cy = mi.rcWork.Bottom - 200;
        return (cx, cy);
    }

    // --- Hotkey capture ---

    private enum HotkeyTarget { None, Toggle, Zoom, Presentation }
    private HotkeyTarget _captureTarget = HotkeyTarget.None;

    private void BeginCaptureToggle_Click(object sender, RoutedEventArgs e) => BeginCapture(HotkeyTarget.Toggle);
    private void BeginCaptureZoom_Click(object sender, RoutedEventArgs e) => BeginCapture(HotkeyTarget.Zoom);
    private void BeginCapturePresentation_Click(object sender, RoutedEventArgs e) => BeginCapture(HotkeyTarget.Presentation);

    private void BeginCapture(HotkeyTarget target)
    {
        _captureTarget = target;
        CaptureButton(target).Content = Strings.ShortcutCapturePrompt;
        if (System.Windows.Application.Current is App app) app.SetHotkeyCapture(true);
        Keyboard.Focus(this);
    }

    private void EndCapture(bool commit, uint mods = 0, uint vk = 0)
    {
        var target = _captureTarget;
        _captureTarget = HotkeyTarget.None;
        if (System.Windows.Application.Current is App app) app.SetHotkeyCapture(false);

        if (commit && target != HotkeyTarget.None)
        {
            switch (target)
            {
                case HotkeyTarget.Toggle:
                    _model.ToggleHotkeyMods = mods;
                    _model.ToggleHotkeyVk = vk;
                    break;
                case HotkeyTarget.Zoom:
                    _model.ZoomHotkeyMods = mods;
                    _model.ZoomHotkeyVk = vk;
                    break;
                case HotkeyTarget.Presentation:
                    _model.PresentationHotkeyMods = mods;
                    _model.PresentationHotkeyVk = vk;
                    break;
            }
            _onChanged(_model);
        }

        // Restore labels (either to the committed new value or back to what was there).
        ToggleHotkeyButton.Content = KeyTranslator.FormatHotkey(_model.ToggleHotkeyMods, _model.ToggleHotkeyVk);
        ZoomHotkeyButton.Content = KeyTranslator.FormatHotkey(_model.ZoomHotkeyMods, _model.ZoomHotkeyVk);
        PresentationHotkeyButton.Content = KeyTranslator.FormatHotkey(_model.PresentationHotkeyMods, _model.PresentationHotkeyVk);
    }

    private void OnCaptureKey(object sender, KeyEventArgs e)
    {
        if (_captureTarget == HotkeyTarget.None) return;

        // Alt combos come in via e.SystemKey when e.Key == System.
        var raw = e.Key == Key.System ? e.SystemKey : e.Key;

        if (raw == Key.Escape)
        {
            EndCapture(commit: false);
            e.Handled = true;
            return;
        }

        // Reject lone modifier keys; we need a real key to bind.
        if (raw is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.System)
        {
            return;
        }

        uint mods = 0;
        var km = Keyboard.Modifiers;
        if ((km & ModifierKeys.Control) != 0) mods |= WindowStyles.MOD_CONTROL;
        if ((km & ModifierKeys.Shift) != 0) mods |= WindowStyles.MOD_SHIFT;
        if ((km & ModifierKeys.Alt) != 0) mods |= WindowStyles.MOD_ALT;
        if ((km & ModifierKeys.Windows) != 0) mods |= WindowStyles.MOD_WIN;

        if (mods == 0)
        {
            // Tell the user they need a modifier; keep capture open.
            var btn = CaptureButton(_captureTarget);
            btn.Content = Strings.ShortcutNeedsMod;
            e.Handled = true;
            return;
        }

        var vk = (uint)KeyInterop.VirtualKeyFromKey(raw);
        EndCapture(commit: true, mods, vk);
        e.Handled = true;
    }

    private System.Windows.Controls.Button CaptureButton(HotkeyTarget target) => target switch
    {
        HotkeyTarget.Zoom => ZoomHotkeyButton,
        HotkeyTarget.Presentation => PresentationHotkeyButton,
        _ => ToggleHotkeyButton
    };

    private void Ok_Click(object sender, RoutedEventArgs e) => Close();

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _onChanged(_original);
        Close();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        var defaults = SettingsModel.Defaults();
        _model.RingRadius = defaults.RingRadius;
        _model.RingThickness = defaults.RingThickness;
        _model.RingOpacity = defaults.RingOpacity;
        _model.RingColor = defaults.RingColor;
        _model.HudEnabled = defaults.HudEnabled;
        _model.HudFontSize = defaults.HudFontSize;
        _model.ShowMouseButtons = defaults.ShowMouseButtons;
        _model.HudCustomPosition = defaults.HudCustomPosition;
        _model.HudX = defaults.HudX;
        _model.HudY = defaults.HudY;
        _model.ToggleHotkeyMods = defaults.ToggleHotkeyMods;
        _model.ToggleHotkeyVk = defaults.ToggleHotkeyVk;
        _model.ZoomHotkeyMods = defaults.ZoomHotkeyMods;
        _model.ZoomHotkeyVk = defaults.ZoomHotkeyVk;
        _model.PresentationHotkeyMods = defaults.PresentationHotkeyMods;
        _model.PresentationHotkeyVk = defaults.PresentationHotkeyVk;
        _model.BigCursorSize = defaults.BigCursorSize;
        _model.BigCursorHoleSize = defaults.BigCursorHoleSize;
        _model.BigCursorBorderThickness = defaults.BigCursorBorderThickness;
        _model.BigCursorColor = defaults.BigCursorColor;
        _model.BigCursorBorderColor = defaults.BigCursorBorderColor;
        _model.BigCursorOpacity = defaults.BigCursorOpacity;
        _model.ClickRippleEnabled = defaults.ClickRippleEnabled;
        _model.LeftClickColor = defaults.LeftClickColor;
        _model.MiddleClickColor = defaults.MiddleClickColor;
        _model.RightClickColor = defaults.RightClickColor;
        _model.RippleMaxRadius = defaults.RippleMaxRadius;
        _model.RippleDurationMs = defaults.RippleDurationMs;
        // keep language — user's display preference survives a settings reset
        LoadFromModel();
        ApplyStrings();
        _onChanged(_model);
    }

    // HSL math

    private static (double h, double s, double l) RgbToHsl(byte r, byte g, byte b)
    {
        var rf = r / 255.0;
        var gf = g / 255.0;
        var bf = b / 255.0;
        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var l = (max + min) / 2.0;

        double h, s;
        if (max - min < 1e-9)
        {
            h = 0;
            s = 0;
        }
        else
        {
            var d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == rf) h = (gf - bf) / d + (gf < bf ? 6 : 0);
            else if (max == gf) h = (bf - rf) / d + 2;
            else h = (rf - gf) / d + 4;
            h /= 6;
        }
        return (h * 360, s, l);
    }

    private static (byte r, byte g, byte b) HslToRgb(double h, double s, double l)
    {
        h /= 360.0;
        double r, g, b;
        if (s < 1e-9)
        {
            r = g = b = l;
        }
        else
        {
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r = Hue2Channel(p, q, h + 1.0 / 3);
            g = Hue2Channel(p, q, h);
            b = Hue2Channel(p, q, h - 1.0 / 3);
        }
        return (
            (byte)Math.Clamp(Math.Round(r * 255), 0, 255),
            (byte)Math.Clamp(Math.Round(g * 255), 0, 255),
            (byte)Math.Clamp(Math.Round(b * 255), 0, 255));
    }

    private static double Hue2Channel(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }
}

internal static class ColorParse
{
    public static MediaColor Parse(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return MediaColor.FromRgb(0xFF, 0x40, 0x60);
        var s = hex.TrimStart('#');
        try
        {
            if (s.Length == 6)
            {
                var r = byte.Parse(s.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var g = byte.Parse(s.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var b = byte.Parse(s.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return MediaColor.FromRgb(r, g, b);
            }
            if (s.Length == 8)
            {
                var r = byte.Parse(s.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var g = byte.Parse(s.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var b = byte.Parse(s.AsSpan(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return MediaColor.FromRgb(r, g, b);
            }
        }
        catch { }
        return MediaColor.FromRgb(0xFF, 0x40, 0x60);
    }
}
