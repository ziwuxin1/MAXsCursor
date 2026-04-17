using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MAXsCursor.Interop;
using MediaColor = System.Windows.Media.Color;
using MediaBrush = System.Windows.Media.SolidColorBrush;

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
        AdjustPositionButton.Content = Strings.AdjustPosition;
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

    private void UpdateLabels()
    {
        RadiusLabel.Text = $"{RadiusSlider.Value:F0} px";
        ThicknessLabel.Text = $"{ThicknessSlider.Value:F1} px";
        OpacityLabel.Text = $"{OpacitySlider.Value * 100:F0}%";
        SaturationLabel.Text = $"{SaturationSlider.Value * 100:F0}%";
        LightnessLabel.Text = $"{LightnessSlider.Value * 100:F0}%";
        HudFontSizeLabel.Text = $"{HudFontSizeSlider.Value:F0} px";
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
        // keep language — user's display preference survives a settings reset
        LoadFromModel();
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
